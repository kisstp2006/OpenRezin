// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using Core.System;
using Foundation;
using Foundation.Logger;
using Foundation.Math;
using Rendering.Shader;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.SDL;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Rendering
{
    public unsafe class VulkanRenderer : IRenderer, IDisposable
    {
        private readonly Vk vk;
        private Instance instance;
        private PhysicalDevice physicalDevice;

        private Device device;
        private Queue graphicsQueue;

        private KhrSurface? khrSurface;
        private SurfaceKHR surface;

        private KhrSwapchain? khrSwapchain;

        private SwapchainKHR swapchain;

        // Cached from CreateSwapchain so later steps (image views, resize)
        // don't need to recompute them.
        private Image[] swapchainImages;
        private Format swapchainImageFormat;
        private Extent2D swapchainExtent;


        private ImageView[] swapchainImageViews;

        private RenderPass renderPass;

        private PipelineLayout pipelineLayout;
        private Pipeline pipeline;


        private Framebuffer[] swapchainFramebuffers;

        private CommandPool commandPool;

        private CommandBuffer[] commandBuffers;

        private Semaphore imageAvailableSemaphore;
        private Semaphore renderFinishedSemaphore;
        private Fence inFlightFence;

        private Foundation.Math.Color clearColor;
        private readonly SilkWindow window;

        private Buffer vertexBuffer;
        private DeviceMemory vertexBufferMemory;

        // Describes and owns the Vulkan resources that expose the camera UBO as
        // set 0, binding 0 to the vertex shader.
        private DescriptorSetLayout cameraDescriptorSetLayout;

        private Buffer cameraUniformBuffer;
        private DeviceMemory cameraUniformBufferMemory;
        private void* cameraUniformBufferMapped;

        private DescriptorPool cameraDescriptorPool;
        private DescriptorSet cameraDescriptorSet;

        private Semaphore[] renderFinishedSemaphores;   // one per swapchain image

        private const string ValidationLayerName = "VK_LAYER_KHRONOS_validation";

        private Buffer objectUniformBuffer;
        private DeviceMemory objectUniformBufferMemory;
        private void* objectUniformBufferMapped;

        private ObjectBufferData currentObjectData = new()
        {
            Model = Matrix4x4.Identity
        };

        private CameraBufferData currentCameraData = new()
        {
            View = Matrix4x4.Identity,
            Projection = Matrix4x4.Identity
        };

        // test
        static readonly Vertex[] vertices = new Vertex[]
        {
            new Vertex { Position = new Vector2(0.0f, -0.5f), Color = new Vector3(1, 0, 0) },
            new Vertex { Position = new Vector2(0.5f,  0.5f), Color = new Vector3(0, 1, 0) },
            new Vertex { Position = new Vector2(-0.5f, 0.5f), Color = new Vector3(0, 0, 1) },
        };

        /// <summary>
        /// // Represents the indices of the queue families that are required for rendering.
        /// </summary>
        struct QueueFamilyIndices
        {
            // Nullable because we don't know yet whether this GPU even has a queue family
            // that supports graphics - it starts "unset" until FindQueueFamilis finds one.
            public uint? GraphicsFamily { get; set; }
            // Nullable because we don't know yet whether this GPU even has a queue family yet
            public uint? PresentFamily { get; set; }
            // True once every queue family we need (currently just graphics) has been found.
            public readonly bool IsComplete() => GraphicsFamily.HasValue && PresentFamily.HasValue;


        }

        // Groups the surface capabilities, formats, and presentation modes that
        // determine whether this GPU can present images to the current window.
        private struct SwapchainSupportDetails
        {
            public SurfaceCapabilitiesKHR Capabilities;
            public SurfaceFormatKHR[] Formats;
            public PresentModeKHR[] PresentModes;
        }

        public VulkanRenderer(SilkWindow window)
        {
            vk = Vk.GetApi();
            CreateInstance(window);
            CreateSurface(window);
            Log.Info("VulkanRenderer initialized successfully.");
            SelectPhysicalDevice();
            CreateLogicalDevice();
            CreateCameraDescriptorSetLayout();
            CreateSwapchain(window);
            CreateImageViews();
            CreateRenderPass();
            CreateGraphicsPipeline();
            CreateFrameBuffer();
            CreateCommandPool();
            CreateCommandBuffers();
            CreateVertexBuffer();
            CreateCameraUniformBuffer();
            CreateObjectUniformBuffer();
            CreateCameraDescriptorSet();
            CreateSyncObjects();

            this.window = window;
        }

        // Allocates one descriptor set and connects its binding 0 entry to the
        // already-created camera uniform buffer.
        private void CreateCameraDescriptorSet()
        {
            var poolSize = new DescriptorPoolSize
            {
                Type = DescriptorType.UniformBuffer,
                DescriptorCount = 2
            };

            var poolInfo = new DescriptorPoolCreateInfo
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                PoolSizeCount = 1,
                PPoolSizes = &poolSize,
                MaxSets = 1
            };

            if (vk.CreateDescriptorPool(
        device,
        in poolInfo,
        null,
        out cameraDescriptorPool) != Result.Success)
            {
                throw new Exception(
                    "Failed to create camera descriptor pool.");
            }
            // Allocate the descriptor set first so we can reference it when
            // writing the descriptor bindings.
            DescriptorSetLayout layout = cameraDescriptorSetLayout;

            var allocateInfo = new DescriptorSetAllocateInfo
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = cameraDescriptorPool,
                DescriptorSetCount = 1,
                PSetLayouts = &layout
            };

            DescriptorSet allocatedSet = default;

            if (vk.AllocateDescriptorSets(
                device,
                in allocateInfo,
                &allocatedSet) != Result.Success)
            {
                throw new Exception(
                    "Failed to allocate camera descriptor set.");
            }

            cameraDescriptorSet = allocatedSet;

            // Prepare the buffer infos and two writes (binding 0 = camera,
            // binding 1 = object) and update the descriptor set in a single call.
            var cameraBufferInfo = new DescriptorBufferInfo
            {
                Buffer = cameraUniformBuffer,
                Offset = 0,
                Range = (ulong)sizeof(CameraBufferData)
            };

            var objectBufferInfo = new DescriptorBufferInfo
            {
                Buffer = objectUniformBuffer,
                Offset = 0,
                Range = (ulong)sizeof(ObjectBufferData)
            };

            var descriptorWrites = stackalloc WriteDescriptorSet[2];

            descriptorWrites[0] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = cameraDescriptorSet,
                DstBinding = 0,
                DstArrayElement = 0,
                DescriptorType = DescriptorType.UniformBuffer,
                DescriptorCount = 1,
                PBufferInfo = &cameraBufferInfo
            };

            descriptorWrites[1] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = cameraDescriptorSet,
                DstBinding = 1,
                DstArrayElement = 0,
                DescriptorType = DescriptorType.UniformBuffer,
                DescriptorCount = 1,
                PBufferInfo = &objectBufferInfo
            };

            vk.UpdateDescriptorSets(
                device,
                2,
                descriptorWrites,
                0,
                null);
        }

        // Allocates host-visible coherent memory and maps it once so later frame
        // updates require only a direct memory copy, not repeated map calls.
        private void CreateCameraUniformBuffer()
        {
            ulong bufferSize = (ulong)sizeof(CameraBufferData);


            var bufferInfo = new BufferCreateInfo
            {
                SType = StructureType.BufferCreateInfo,
                Size = bufferSize,
                Usage = BufferUsageFlags.UniformBufferBit,
                SharingMode = SharingMode.Exclusive
            };
            if (vk.CreateBuffer(
                device,
                in bufferInfo,
                null,
                out cameraUniformBuffer) != Result.Success)
            {
                throw new Exception(
                    "Failed to create camera uniform buffer.");
            }

            vk.GetBufferMemoryRequirements(device, cameraUniformBuffer, out MemoryRequirements requirements);

            var allocationInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = requirements.Size,
                MemoryTypeIndex = FindMemoryType(
            requirements.MemoryTypeBits,
            MemoryPropertyFlags.HostVisibleBit |
            MemoryPropertyFlags.HostCoherentBit)
            };

            if (vk.AllocateMemory(
                device,
                in allocationInfo,
                null,
                out cameraUniformBufferMemory) != Result.Success)
            {
                throw new Exception(
                    "Failed to allocate camera uniform buffer memory.");
            }

            if (vk.BindBufferMemory(
                device,
                cameraUniformBuffer,
                cameraUniformBufferMemory,
                0) != Result.Success)
            {
                throw new Exception(
                    "Failed to bind camera uniform buffer memory.");
            }
            void* mappedData = null;


            if (vk.MapMemory(
                device,
                cameraUniformBufferMemory,
                0,
                bufferSize,
                0,
                &mappedData) != Result.Success)
            {
                throw new Exception(
                    "Failed to map camera uniform buffer memory.");
            }
            cameraUniformBufferMapped = mappedData;
        }

        // Declares that set 0, binding 0 contains one uniform buffer visible to
        // the vertex stage; the pipeline layout and shader must match this.
        // Declares that set 0 contains two uniform buffers visible to the vertex
        // stage: binding 0 for the camera, binding 1 for the per-object model matrix.
        private void CreateCameraDescriptorSetLayout()
        {
            var bindings = stackalloc DescriptorSetLayoutBinding[2];

            bindings[0] = new DescriptorSetLayoutBinding
            {
                Binding = 0,
                DescriptorType = DescriptorType.UniformBuffer,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.VertexBit,
                PImmutableSamplers = null
            };

            bindings[1] = new DescriptorSetLayoutBinding
            {
                Binding = 1,
                DescriptorType = DescriptorType.UniformBuffer,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.VertexBit,
                PImmutableSamplers = null
            };

            var layoutInfo = new DescriptorSetLayoutCreateInfo
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = 2,
                PBindings = bindings
            };

            if (vk.CreateDescriptorSetLayout(
                device,
                in layoutInfo,
                null,
                out cameraDescriptorSetLayout) != Result.Success)
            {
                throw new Exception(
                    "Failed to create camera descriptor set layout.");
            }
        }

        private void CreateInstance(SilkWindow window)
        {
            ArgumentNullException.ThrowIfNull(window);
            // Identifies the app/engine to the driver and validation layers.
            var appInfo = new ApplicationInfo
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = (byte*)Marshal.StringToHGlobalAnsi(EngineInfo.Name),
                ApplicationVersion = (Version32)EngineInfo.Version,
                PEngineName = (byte*)Marshal.StringToHGlobalAnsi(EngineInfo.Name),
                EngineVersion = (Version32)EngineInfo.Version,
                ApiVersion = Vk.Version13
            };

            // Ask the windowing library which instance extensions the OS needs for presentation.
            var reqiredExtensions = window.NativeWindow.VkSurface!.GetRequiredExtensions(out uint extensionCount);

            // Stays empty in release builds, or when the SDK isn't installed on this machine.
            byte*[] enabledLayers = new byte*[0];
            byte* layerName = null;

#if DEBUG
            if (IsValidationLayerAvailable(ValidationLayerName))
            {
                layerName = (byte*)Marshal.StringToHGlobalAnsi(ValidationLayerName);
                enabledLayers = new byte*[] { layerName };
                Log.Info($"Enabling {ValidationLayerName}.");
            }
            else
            {
                Log.Info($"{ValidationLayerName} not available - continuing without validation.");
            }
#endif

            fixed (byte** enabledLayersPtr = enabledLayers)
            {
                var createInfo = new InstanceCreateInfo
                {
                    SType = StructureType.InstanceCreateInfo,
                    PApplicationInfo = &appInfo,
                    EnabledExtensionCount = extensionCount,
                    PpEnabledExtensionNames = reqiredExtensions,
                    EnabledLayerCount = (uint)enabledLayers.Length,
                    PpEnabledLayerNames = enabledLayersPtr
                };

                if (vk.CreateInstance(in createInfo, null, out instance) != Result.Success)
                {
                    Log.Error("Failed to create Vulkan instance.");
                    throw new Exception("Failed to create Vulkan instance.");
                }
            }

            // The native strings were only needed for the call above.
            Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
            Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);

            if (layerName != null)
                Marshal.FreeHGlobal((IntPtr)layerName);
        }

        // Prefers a discrete GPU, falls back to integrated, then to whatever is available.
        private void SelectPhysicalDevice(bool preferDiscreteGPU = true)
        {

            var devices = vk.GetPhysicalDevices(instance);
            if (preferDiscreteGPU)
            {
                foreach (var device in devices)
                {
                    var properties = vk.GetPhysicalDeviceProperties(device);
                    Log.Info($"Found GPU: {Marshal.PtrToStringAnsi((IntPtr)properties.DeviceName)}");

                    if (properties.DeviceType == PhysicalDeviceType.DiscreteGpu && IsDeviceSuitable(device, surface))
                    {
                        physicalDevice = device;
                        Log.Info($"Selected discrete GPU: {Marshal.PtrToStringAnsi((IntPtr)properties.DeviceName)}");
                        return;
                    }
                }
            }
            else
            {
                foreach (var device in devices)
                {
                    var properties = vk.GetPhysicalDeviceProperties(device);
                    Log.Info($"Found GPU: {Marshal.PtrToStringAnsi((IntPtr)properties.DeviceName)}");

                    if (properties.DeviceType == PhysicalDeviceType.IntegratedGpu && IsDeviceSuitable(device, surface))
                    {
                        physicalDevice = device;
                        Log.Info($"Selected integrated GPU: {Marshal.PtrToStringAnsi((IntPtr)properties.DeviceName)}");
                        return;
                    }
                }
            }
            // No preferred GPU type found - try again with any available GPU.
            foreach (var device in devices)
            {
                var properties = vk.GetPhysicalDeviceProperties(device);
                Log.Info($"Found GPU: {Marshal.PtrToStringAnsi((IntPtr)properties.DeviceName)}");

                if (IsDeviceSuitable(device, surface))
                {
                    physicalDevice = device;
                    Log.Info($"Selected fallback GPU: {Marshal.PtrToStringAnsi((IntPtr)properties.DeviceName)}");
                    return;
                }
                else
                {
                    Log.Warn($"GPU {Marshal.PtrToStringAnsi((IntPtr)properties.DeviceName)} is not suitable for rendering.");
                }
            }
            Log.Error("No suitable GPU found with Vulkan support.");
            throw new NotSupportedException("No suitable GPU found with Vulkan support.");
        }
        private QueueFamilyIndices FindQueueFamilies(PhysicalDevice device, SurfaceKHR surface)
        {
            QueueFamilyIndices indices = new QueueFamilyIndices();

            // First call: pass null for the buffer, Vulkan just tells us how many
            // queue families this device has, via queueFamilyCount.
            uint queueFamilyCount = 0;
            vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, null);

            QueueFamilyProperties[] queueFamilies = new QueueFamilyProperties[queueFamilyCount];

            // fixed pins the array in memory so the GC can't move it while we hold
            // a raw pointer into it - needed because the second call writes the
            // actual queue family data straight into that native pointer.
            fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
            {
                vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, queueFamiliesPtr);
            }

            // Walk the families and remember the index of the first one that
            // supports graphics commands - that's the "address" we'll submit
            // draw calls to later.
            uint i = 0;
            foreach (var queueFamily in queueFamilies)
            {
                if (queueFamily.QueueFlags.HasFlag(QueueFlags.GraphicsBit))
                    indices.GraphicsFamily = i;

                if (khrSurface!.GetPhysicalDeviceSurfaceSupport(device, i, surface, out Bool32 presentSupport) == Result.Success)
                {
                    // Bool32 stores 0 or 1 in .Value
                    if (presentSupport.Value != 0)
                        indices.PresentFamily = i;
                }

                if (indices.IsComplete())
                    break;

                i++;
            }

            return indices;
        }

        // A GPU is usable only if its queues, swapchain extension, formats, and
        // presentation modes satisfy everything required by this renderer.
        private bool IsDeviceSuitable(PhysicalDevice device, SurfaceKHR surface)
        {
            var indices = FindQueueFamilies(device, surface);

            if (!indices.IsComplete())
                return false;

            if (!CheckDeviceExtensionSupport(device))
                return false;

            var swapchainSupport =
                QuerySwapchainSupport(device);

            return swapchainSupport.Formats.Length > 0 &&
                   swapchainSupport.PresentModes.Length > 0;
        }

        // Enumerates the extensions exposed by one GPU and verifies that the
        // device-level VK_KHR_swapchain extension is available.
        private bool CheckDeviceExtensionSupport(PhysicalDevice device)
        {
            uint extensionCount = 0;
            ExtensionProperties[] availableExtensions;
            const string requiredExtensionName = "VK_KHR_swapchain";

            var countResult = vk.EnumerateDeviceExtensionProperties(
                device,
                (byte*)null,
                ref extensionCount,
                null);

            if (countResult != Result.Success)
            {
                Log.Error("Failed to count device extensions.");
                return false;
            }


            if (extensionCount > 0)
            {
                availableExtensions =
                    new ExtensionProperties[extensionCount];

                fixed (ExtensionProperties* extensionsPtr = availableExtensions)
                {
                    var result = vk.EnumerateDeviceExtensionProperties(
                        device,
                        (byte*)null,
                        ref extensionCount,
                        extensionsPtr);

                    if (result != Result.Success)
                    {
                        Log.Error("Failed to enumerate device extensions.");
                        return false;
                    }
                }


                foreach (var extension in availableExtensions)
                {
                    string extensionName =
                        Marshal.PtrToStringAnsi(
                            (IntPtr)extension.ExtensionName)
                        ?? string.Empty;

                    if (extensionName == requiredExtensionName)
                        return true;
                }
                return false;
            }
            {
                Log.Error(requiredExtensionName + " extension not found on device.");
                return false;
            }

        }

        // Queries all surface-dependent swapchain choices using Vulkan's
        // count-first, allocate-second enumeration pattern.
        private SwapchainSupportDetails QuerySwapchainSupport(
    PhysicalDevice device)
        {
            var details = new SwapchainSupportDetails
            {
                Formats = Array.Empty<SurfaceFormatKHR>(),
                PresentModes = Array.Empty<PresentModeKHR>()
            };

            // Surface capabilities
            Result capabilitiesResult =
                khrSurface!.GetPhysicalDeviceSurfaceCapabilities(
                    device,
                    surface,
                    out details.Capabilities);

            if (capabilitiesResult != Result.Success)
            {
                Log.Error("Failed to query surface capabilities.");
                return details;
            }

            // Surface formats
            uint formatCount = 0;

            Result formatCountResult =
                khrSurface.GetPhysicalDeviceSurfaceFormats(
                    device,
                    surface,
                    ref formatCount,
                    null);

            if (formatCountResult != Result.Success)
            {
                Log.Error("Failed to count surface formats.");
                return details;
            }

            if (formatCount > 0)
            {
                details.Formats = new SurfaceFormatKHR[formatCount];

                fixed (SurfaceFormatKHR* formatsPtr = details.Formats)
                {
                    Result formatsResult =
                        khrSurface.GetPhysicalDeviceSurfaceFormats(
                            device,
                            surface,
                            ref formatCount,
                            formatsPtr);

                    if (formatsResult != Result.Success)
                    {
                        Log.Error("Failed to query surface formats.");
                        details.Formats = Array.Empty<SurfaceFormatKHR>();
                        return details;
                    }
                }
            }

            // Present modes
            uint presentModeCount = 0;

            Result presentModeCountResult =
                khrSurface.GetPhysicalDeviceSurfacePresentModes(
                    device,
                    surface,
                    ref presentModeCount,
                    null);

            if (presentModeCountResult != Result.Success)
            {
                Log.Error("Failed to count present modes.");
                return details;
            }

            if (presentModeCount > 0)
            {
                details.PresentModes =
                    new PresentModeKHR[presentModeCount];

                fixed (PresentModeKHR* presentModesPtr =
                    details.PresentModes)
                {
                    Result presentModesResult =
                        khrSurface.GetPhysicalDeviceSurfacePresentModes(
                            device,
                            surface,
                            ref presentModeCount,
                            presentModesPtr);

                    if (presentModesResult != Result.Success)
                    {
                        Log.Error("Failed to query present modes.");
                        details.PresentModes =
                            Array.Empty<PresentModeKHR>();

                        return details;
                    }
                }
            }

            return details;
        }

        private void CreateLogicalDevice()
        {
            var indices = FindQueueFamilies(physicalDevice, surface);
            float queuePriority = 1.0f;

            // The "!" only silences the compiler - it doesn't guarantee a value,
            // so we check IsComplete() ourselves before trusting it below.
            if (!indices.IsComplete())
            {
                Log.Error("Selected GPU has no graphics-capable queue family.");
                throw new Exception("Selected GPU has no graphics-capable queue family.");
            }

            var queueCreateInfo = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = indices.GraphicsFamily!.Value,
                QueueCount = 1
            };

            // Vulkan requires a priority per queue, even with just one queue.
            queueCreateInfo.PQueuePriorities = &queuePriority;

            var deviceFeatures = new PhysicalDeviceFeatures();

            // VK_KHR_swapchain is a device-level extension - it must be enabled here,
            // not at instance creation, or its functions won't be loadable later.
            byte* extensionName = (byte*)Marshal.StringToHGlobalAnsi("VK_KHR_swapchain");
            byte*[] deviceExtensions = new byte*[] { extensionName };

            fixed (byte** deviceExtensionsPtr = deviceExtensions)
            {
                var createInfo = new DeviceCreateInfo
                {
                    SType = StructureType.DeviceCreateInfo,
                    QueueCreateInfoCount = 1,
                    PQueueCreateInfos = &queueCreateInfo,
                    EnabledExtensionCount = (uint)deviceExtensions.Length,
                    PpEnabledExtensionNames = deviceExtensionsPtr,
                    EnabledLayerCount = 0,
                    PEnabledFeatures = &deviceFeatures
                };

                if (vk.CreateDevice(physicalDevice, in createInfo, null, out device) != Result.Success)
                {
                    Log.Error("Failed to create logical device.");
                    throw new Exception("Failed to create logical device.");
                }
            }

            Marshal.FreeHGlobal((IntPtr)extensionName);

            // Fetch the usable queue handle for the family we picked above.
            vk.GetDeviceQueue(device, indices.GraphicsFamily!.Value, 0, out graphicsQueue);
        }

        // Wraps the native OS window handle in a Vulkan-presentable surface.
        private void CreateSurface(SilkWindow window)
        {
            if (window == null)
            {
                Log.Error("Failed to create Vulkan surface: Window is null.");
                throw new ArgumentNullException(nameof(window));
            }

            if (!vk.TryGetInstanceExtension<KhrSurface>(instance, out khrSurface))
            {
                Log.Error("KHR_surface extension not found.");
                throw new NotSupportedException("KHR_surface extension not found.");
            }

            try
            {
                surface = window.NativeWindow.VkSurface!.Create<AllocationCallbacks>(instance.ToHandle(), null).ToSurface();
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to create Vulkan surface: {ex.Message}");
                throw new Exception("Failed to create Vulkan surface.", ex);
            }
        }

        private void CreateSwapchain(SilkWindow window)
        {
            if (khrSurface!.GetPhysicalDeviceSurfaceCapabilities(physicalDevice, surface, out SurfaceCapabilitiesKHR capabilities) == Result.Success)
            {
                uint formatCount = 0;

                khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, surface, ref formatCount, null);
                //lets allocate an array for the formats with the exact size
                SurfaceFormatKHR[] formats = new SurfaceFormatKHR[formatCount];

                uint presentModeCount = 0;
                PresentModeKHR[] presentModes = new PresentModeKHR[presentModeCount];

                // lets create a fixed array in the memory containing the formats and pass it to the function to fill it with the formats
                fixed (SurfaceFormatKHR* formatsPtr = formats)
                {
                    khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, surface, ref formatCount, formatsPtr);
                }
                SurfaceFormatKHR chosenFormat = formats[0];

                foreach (var format in formats)
                {
                    if (format.Format == Format.B8G8R8A8Srgb && format.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
                    {
                        chosenFormat = format;
                        break;
                    }
                }
                khrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice, surface, ref presentModeCount, null);
                presentModes = new PresentModeKHR[presentModeCount];
                PresentModeKHR chosenPresentMode = PresentModeKHR.FifoKhr;

                fixed (PresentModeKHR* presentModesPtr = presentModes)
                {
                    khrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice, surface, ref presentModeCount, presentModesPtr);
                }

                foreach (var presentMode in presentModes)
                {
                    if (presentMode == PresentModeKHR.MailboxKhr)
                    {
                        Log.Info("Selected present mode: Mailbox");
                        chosenPresentMode = presentMode;
                        break;
                    }
                }

                Extent2D extent;

                if (capabilities.CurrentExtent.Width == uint.MaxValue)
                {
                    extent = new Extent2D
                    {
                        Width = Math.Clamp((uint)window.Width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width),

                        Height = Math.Clamp((uint)window.Height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height)
                    };
                }
                else
                {
                    extent = capabilities.CurrentExtent;
                }

                uint imageCount = capabilities.MinImageCount + 1;

                if (capabilities.MaxImageCount > 0 && imageCount > capabilities.MaxImageCount)
                    imageCount = capabilities.MaxImageCount;

                if (!vk.TryGetDeviceExtension<KhrSwapchain>(instance, device, out khrSwapchain))
                {
                    Log.Error("VK_KHR_swapchain extension not found.");
                    throw new NotSupportedException("VK_KHR_swapchain extension not found.");
                }


                SwapchainCreateInfoKHR swapchainCreateInfo = new SwapchainCreateInfoKHR
                {
                    SType = StructureType.SwapchainCreateInfoKhr,
                    Surface = surface,
                    MinImageCount = imageCount,
                    ImageFormat = chosenFormat.Format,
                    ImageColorSpace = chosenFormat.ColorSpace,
                    ImageExtent = extent,
                    ImageArrayLayers = 1,
                    ImageSharingMode = SharingMode.Exclusive,
                    ImageUsage = ImageUsageFlags.ColorAttachmentBit,
                    PreTransform = capabilities.CurrentTransform,
                    CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
                    PresentMode = chosenPresentMode,
                    Clipped = true,
                    OldSwapchain = default
                };

                if (khrSwapchain.CreateSwapchain(device, in swapchainCreateInfo, null, out swapchain) != Result.Success)
                {
                    Log.Error("Failed to create swapchain.");
                    throw new Exception("Failed to create swapchain.");
                }

                // Driver may have created more images than we asked for,
                // so re-query the real count before allocating the array.
                uint swapchainImageCount = 0;
                khrSwapchain.GetSwapchainImages(device, swapchain, ref swapchainImageCount, null);
                swapchainImages = new Image[swapchainImageCount];
                fixed (Image* swapchainImagesPtr = swapchainImages)
                {
                    // Second call fills the pinned array with the actual image handles.
                    khrSwapchain.GetSwapchainImages(device, swapchain, ref swapchainImageCount, swapchainImagesPtr);
                }
                // Save for later steps - the local chosenFormat/extent go out of scope here.
                swapchainImageFormat = chosenFormat.Format;
                swapchainExtent = extent;
            }
            else
            {
                Log.Error("Failed to get surface capabilities.");
                throw new Exception("Failed to get surface capabilities.");
            }
        }
        // Each raw swapchain image needs its own view before the GPU can use it as a render target.
        private void CreateImageViews()
        {
            swapchainImageViews = new ImageView[swapchainImages.Length];

            for (int i = 0; i < swapchainImages.Length; i++)
            {
                // Plain 2D color image, no mips, no array layers - matches a swapchain image.
                var createInfo = new ImageViewCreateInfo
                {
                    SType = StructureType.ImageViewCreateInfo,
                    Image = swapchainImages[i],
                    ViewType = ImageViewType.Type2D,
                    Format = swapchainImageFormat,
                    Components = new ComponentMapping
                    {
                        R = ComponentSwizzle.Identity,
                        G = ComponentSwizzle.Identity,
                        B = ComponentSwizzle.Identity,
                        A = ComponentSwizzle.Identity
                    },
                    SubresourceRange = new ImageSubresourceRange
                    {
                        AspectMask = ImageAspectFlags.ColorBit,
                        BaseMipLevel = 0,
                        LevelCount = 1,
                        BaseArrayLayer = 0,
                        LayerCount = 1
                    }
                };
                if (vk.CreateImageView(device, in createInfo, null, out swapchainImageViews[i]) != Result.Success)
                {
                    Log.Error($"Failed to create image view for swapchain image {i}.");
                    throw new Exception($"Failed to create image view for swapchain image {i}.");
                }
            }

        }
        private void CreateRenderPass()
        {
            // Single color attachment: clear at the start of the pass, keep the
            // result so it can be presented once the pass ends.
            var colorAttachment = new AttachmentDescription
            {
                Format = swapchainImageFormat,
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.PresentSrcKhr
            };

            // Points the subpass at attachment index 0 (the one defined above).
            var colorAttachmentRef = new AttachmentReference
            {
                Attachment = 0,
                Layout = ImageLayout.ColorAttachmentOptimal
            };

            // One graphics subpass that writes to our single color attachment.
            var subpass = new SubpassDescription
            {
                PipelineBindPoint = PipelineBindPoint.Graphics,
                ColorAttachmentCount = 1,
                PColorAttachments = &colorAttachmentRef
            };

            var renderPassInfo = new RenderPassCreateInfo
            {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = 1,
                PAttachments = &colorAttachment,
                SubpassCount = 1,
                PSubpasses = &subpass
            };

            if (vk.CreateRenderPass(device, in renderPassInfo, null, out renderPass) != Result.Success)
            {
                Log.Error("Failed to create render pass.");
                throw new Exception("Failed to create render pass.");
            }

        }

        private ShaderModule CreateShaderModule(byte[] code)
        {
            fixed (byte* codePtr = code)
            {
                // SPIR-V is defined as a stream of 32-bit words, so PCode wants uint*
                // even though we read the file as raw bytes.
                var createInfo = new ShaderModuleCreateInfo
                {
                    SType = StructureType.ShaderModuleCreateInfo,
                    CodeSize = (nuint)code.Length,
                    PCode = (uint*)codePtr
                };

                if (vk.CreateShaderModule(device, in createInfo, null, out ShaderModule shaderModule) != Result.Success)
                {
                    Log.Error("Failed to create shader module.");
                    throw new Exception("Failed to create shader module.");
                }

                return shaderModule;
            }
        }
        private void CreateGraphicsPipeline()
        {
            // Raw SPIR-V bytecode compiled ahead of time from the GLSL sources via glslc.
            byte[] vertShaderCode = ShaderCompiler.CompileHlslToSpirv(
                    "Shaders/triangle.hlsl",
                    "VSMain",
                    "vs_6_0");
            byte[] fragShaderCode = ShaderCompiler.CompileHlslToSpirv(
                    "Shaders/triangle.hlsl",
                    "PSMain",
                    "ps_6_0");

            ShaderModule vertexShaderModule = CreateShaderModule(vertShaderCode);
            ShaderModule fragmentShaderModule = CreateShaderModule(fragShaderCode);

            // Binds each shader module to its pipeline stage; "VSMain"/"PSMain" is the GLSL entry point.
            var vertShaderStageInfo = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = vertexShaderModule,
                PName = (byte*)Marshal.StringToHGlobalAnsi("VSMain")
            };

            var fragShaderStageInfo = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = fragmentShaderModule,
                PName = (byte*)Marshal.StringToHGlobalAnsi("PSMain")
            };

            PipelineShaderStageCreateInfo[] shaderStages = new PipelineShaderStageCreateInfo[] { vertShaderStageInfo, fragShaderStageInfo };

            var bindingDescription = new VertexInputBindingDescription
            {
                Binding = 0,
                Stride = (uint)sizeof(Vertex),
                InputRate = VertexInputRate.Vertex
            };

            var attributeDescriptions = stackalloc VertexInputAttributeDescription[2];

            attributeDescriptions[0] = new VertexInputAttributeDescription
            {
                Binding = 0,
                Location = 0,                       // matches Location 0 (POSITION) in the shader
                Format = Format.R32G32Sfloat,       // 2 floats
                Offset = 0
            };

            attributeDescriptions[1] = new VertexInputAttributeDescription
            {
                Binding = 0,
                Location = 1,                       // matches Location 1 (COLOR0) in the shader
                Format = Format.R32G32B32Sfloat,    // 3 floats
                Offset = (uint)sizeof(Vector2)      // Color starts after Position
            };

            var vertexInputInfo = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 1,
                PVertexBindingDescriptions = &bindingDescription,
                VertexAttributeDescriptionCount = 2,
                PVertexAttributeDescriptions = attributeDescriptions
            };

            // Every 3 vertices form one independent triangle (like GL_TRIANGLES in OpenGL).
            var inputAssembly = new PipelineInputAssemblyStateCreateInfo
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = PrimitiveTopology.TriangleList,
                PrimitiveRestartEnable = false
            };

            // Maps normalized device coords to the full swapchain image in pixels.
            var viewport = new Viewport
            {
                X = 0,
                Y = 0,
                Width = swapchainExtent.Width,
                Height = swapchainExtent.Height,
                MinDepth = 0,
                MaxDepth = 1
            };

            // No extra clipping beyond the viewport - scissor covers the whole framebuffer.
            var scissor = new Rect2D
            {
                Offset = new Offset2D(0, 0),
                Extent = swapchainExtent
            };

            var viewportState = new PipelineViewportStateCreateInfo
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                PViewports = &viewport,
                ScissorCount = 1,
                PScissors = &scissor
            };

            // Fill (solid) triangles, cull back faces, clockwise winding counts as "front".
            var rasterizer = new PipelineRasterizationStateCreateInfo
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                DepthClampEnable = false,
                RasterizerDiscardEnable = false,
                PolygonMode = PolygonMode.Fill,
                LineWidth = 1.0f,
                CullMode = CullModeFlags.BackBit,
                FrontFace = FrontFace.Clockwise,
                DepthBiasEnable = false
            };

            // No MSAA for now - one sample per pixel.
            var multisampling = new PipelineMultisampleStateCreateInfo
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                SampleShadingEnable = false,
                RasterizationSamples = SampleCountFlags.Count1Bit
            };

            // Write all RGBA channels, no blending - new fragments overwrite what's there.
            var colorBlendAttachment = new PipelineColorBlendAttachmentState
            {
                ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit
                    | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
                BlendEnable = false
            };

            // Wraps the per-attachment blend state above (we only have 1 color attachment).
            var colorBlending = new PipelineColorBlendStateCreateInfo
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = false,
                AttachmentCount = 1,
                PAttachments = &colorBlendAttachment
            };

            DescriptorSetLayout descriptorSetLayout =
                cameraDescriptorSetLayout;


            // Exposes the camera descriptor layout as set 0; no push constants are used yet.
            var pipelineLayoutInfo = new PipelineLayoutCreateInfo
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = 1,
                PSetLayouts = &descriptorSetLayout,
                PushConstantRangeCount = 0
            };

            if (vk.CreatePipelineLayout(device, in pipelineLayoutInfo, null, out pipelineLayout) != Result.Success)
            {
                Log.Error("Failed to create pipeline layout.");
                throw new Exception("Failed to create pipeline layout.");
            }

            // Ties every state object above, plus the layout and render pass, into one pipeline.
            fixed (PipelineShaderStageCreateInfo* shaderStagesPtr = shaderStages)
            {
                var pipelineInfo = new GraphicsPipelineCreateInfo
                {
                    SType = StructureType.GraphicsPipelineCreateInfo,
                    StageCount = 2,
                    PStages = shaderStagesPtr,
                    PVertexInputState = &vertexInputInfo,
                    PInputAssemblyState = &inputAssembly,
                    PViewportState = &viewportState,
                    PRasterizationState = &rasterizer,
                    PMultisampleState = &multisampling,
                    PColorBlendState = &colorBlending,
                    Layout = pipelineLayout,
                    RenderPass = renderPass,
                    Subpass = 0,
                    BasePipelineHandle = default
                };

                // Can create multiple pipelines in one call (createInfoCount) - we only need 1.
                if (vk.CreateGraphicsPipelines(device, default, 1, in pipelineInfo, null, out pipeline) != Result.Success)
                {
                    Log.Error("Failed to create graphics pipeline.");
                    throw new Exception("Failed to create graphics pipeline.");
                }
            }

            // The entry-point name strings were only needed for pipeline creation.
            Marshal.FreeHGlobal((IntPtr)vertShaderStageInfo.PName);
            Marshal.FreeHGlobal((IntPtr)fragShaderStageInfo.PName);

            //We can destroy the shader modules after creating the pipeline, as they are no longer needed.
            vk.DestroyShaderModule(device, vertexShaderModule, null);
            vk.DestroyShaderModule(device, fragmentShaderModule, null);
        }

        private void CreateFrameBuffer()
        {
            swapchainFramebuffers = new Framebuffer[swapchainImageViews.Length];

            for (int i = 0; i < swapchainImageViews.Length; i++)
            {
                var attachment = swapchainImageViews[i];

                var framebufferInfo = new FramebufferCreateInfo
                {
                    SType = StructureType.FramebufferCreateInfo,
                    RenderPass = renderPass,
                    AttachmentCount = 1,
                    PAttachments = &attachment,
                    Width = swapchainExtent.Width,
                    Height = swapchainExtent.Height,
                    Layers = 1
                };

                if (vk.CreateFramebuffer(device, in framebufferInfo, null, out swapchainFramebuffers[i]) != Result.Success)
                {
                    Log.Error("Failed to create framebuffer.");
                    throw new Exception("Failed to create framebuffer.");
                }
            }
        }

        private void CreateVertexBuffer()
        {
            ulong bufferSize = (ulong)(sizeof(Vertex) * vertices.Length);

            var bufferInfo = new BufferCreateInfo
            {
                SType = StructureType.BufferCreateInfo,
                Size = bufferSize,
                Usage = BufferUsageFlags.VertexBufferBit,
                SharingMode = SharingMode.Exclusive
            };

            if (vk.CreateBuffer(device, in bufferInfo, null, out vertexBuffer) != Result.Success)
            {
                Log.Error("Failed to create vertex buffer.");
                throw new Exception("Failed to create vertex buffer.");
            }

            // The buffer is just a handle - it has no memory behind it yet.
            vk.GetBufferMemoryRequirements(device, vertexBuffer, out MemoryRequirements memRequirements);

            var allocInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memRequirements.Size,
                MemoryTypeIndex = FindMemoryType(
                    memRequirements.MemoryTypeBits,
                    MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit)
            };

            if (vk.AllocateMemory(device, in allocInfo, null, out vertexBufferMemory) != Result.Success)
            {
                Log.Error("Failed to allocate vertex buffer memory.");
                throw new Exception("Failed to allocate vertex buffer memory.");
            }

            vk.BindBufferMemory(device, vertexBuffer, vertexBufferMemory, 0);

            // Map the GPU memory into our address space, copy, then unmap.
            void* data;
            vk.MapMemory(device, vertexBufferMemory, 0, bufferSize, 0, &data);
            vertices.AsSpan().CopyTo(new Span<Vertex>(data, vertices.Length));
            vk.UnmapMemory(device, vertexBufferMemory);
        }

        private void CreateCommandPool()
        {
            var indices = FindQueueFamilies(physicalDevice, surface);
            if (!indices.IsComplete())
            {
                Log.Error("Selected GPU has no graphics-capable queue family.");
                throw new Exception("Selected GPU has no graphics-capable queue family.");
            }
            var poolInfo = new CommandPoolCreateInfo
            {
                SType = StructureType.CommandPoolCreateInfo,
                QueueFamilyIndex = indices.GraphicsFamily!.Value,
                Flags = CommandPoolCreateFlags.ResetCommandBufferBit
            };
            if (vk.CreateCommandPool(device, in poolInfo, null, out commandPool) != Result.Success)
            {
                Log.Error("Failed to create command pool.");
                throw new Exception("Failed to create command pool.");
            }
        }

        private void CreateCommandBuffers()
        {
            commandBuffers = new CommandBuffer[swapchainFramebuffers.Length];
            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = commandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = (uint)commandBuffers.Length
            };
            fixed (CommandBuffer* commandBuffersPtr = commandBuffers)
            {
                if (vk.AllocateCommandBuffers(device, in allocInfo, commandBuffersPtr) != Result.Success)
                {
                    Log.Error("Failed to allocate command buffers.");
                    throw new Exception("Failed to allocate command buffers.");
                }
            }
        }

        private void CreateSyncObjects()
        {
            var semaphoreInfo = new SemaphoreCreateInfo
            {
                SType = StructureType.SemaphoreCreateInfo
            };

            if (vk.CreateSemaphore(device, in semaphoreInfo, null, out imageAvailableSemaphore) != Result.Success)
            {
                Log.Error("Failed to create image available semaphore.");
                throw new Exception("Failed to create image available semaphore.");
            }

            // A presented image's semaphore stays in use until that image is acquired
            // again, so each swapchain image needs its own.
            renderFinishedSemaphores = new Semaphore[swapchainImages.Length];

            for (int i = 0; i < renderFinishedSemaphores.Length; i++)
            {
                if (vk.CreateSemaphore(device, in semaphoreInfo, null, out renderFinishedSemaphores[i]) != Result.Success)
                {
                    Log.Error($"Failed to create render finished semaphore {i}.");
                    throw new Exception($"Failed to create render finished semaphore {i}.");
                }
            }

            var fenceInfo = new FenceCreateInfo
            {
                SType = StructureType.FenceCreateInfo,
                Flags = FenceCreateFlags.SignaledBit
            };

            if (vk.CreateFence(device, in fenceInfo, null, out inFlightFence) != Result.Success)
            {
                Log.Error("Failed to create in-flight fence.");
                throw new Exception("Failed to create in-flight fence.");
            }
        }

        private void CreateObjectUniformBuffer()
        {
            ulong bufferSize = (ulong)sizeof(ObjectBufferData);

            var bufferInfo = new BufferCreateInfo
            {
                SType = StructureType.BufferCreateInfo,
                Size = bufferSize,
                Usage = BufferUsageFlags.UniformBufferBit,
                SharingMode = SharingMode.Exclusive
            };

            if (vk.CreateBuffer(device, in bufferInfo, null, out objectUniformBuffer) != Result.Success)
            {
                throw new Exception("Failed to create object uniform buffer.");
            }

            vk.GetBufferMemoryRequirements(device, objectUniformBuffer, out MemoryRequirements requirements);

            var allocationInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = requirements.Size,
                MemoryTypeIndex = FindMemoryType(
                    requirements.MemoryTypeBits,
                    MemoryPropertyFlags.HostVisibleBit |
                    MemoryPropertyFlags.HostCoherentBit)
            };

            if (vk.AllocateMemory(device, in allocationInfo, null, out objectUniformBufferMemory) != Result.Success)
            {
                throw new Exception("Failed to allocate object uniform buffer memory.");
            }

            if (vk.BindBufferMemory(device, objectUniformBuffer, objectUniformBufferMemory, 0) != Result.Success)
            {
                throw new Exception("Failed to bind object uniform buffer memory.");
            }

            void* mappedData = null;

            if (vk.MapMemory(device, objectUniformBufferMemory, 0, bufferSize, 0, &mappedData) != Result.Success)
            {
                throw new Exception("Failed to map object uniform buffer memory.");
            }

            objectUniformBufferMapped = mappedData;
        }

        private void RecordCommandBuffer(CommandBuffer commandBuffer, uint imageIndex)
        {
            var beginInfo = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo };

            if (vk.BeginCommandBuffer(commandBuffer, in beginInfo) != Result.Success)
            {
                Log.Error("Failed to begin recording command buffer.");
                throw new Exception("Failed to begin recording command buffer.");
            }

            var clearColor = new ClearValue { Color = new ClearColorValue(0, 0, 0, 1) };

            var renderPassInfo = new RenderPassBeginInfo
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = renderPass,
                Framebuffer = swapchainFramebuffers[(int)imageIndex],
                RenderArea = new Rect2D(new Offset2D(0, 0), swapchainExtent),
                ClearValueCount = 1,
                PClearValues = &clearColor
            };

            vk.CmdBeginRenderPass(commandBuffer, in renderPassInfo, SubpassContents.Inline);
            vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, pipeline);

            // Bind the camera UBO descriptor at set 0 before issuing the draw.
            var descriptorSets =
                stackalloc[] { cameraDescriptorSet };

            vk.CmdBindDescriptorSets(
                commandBuffer,
                PipelineBindPoint.Graphics,
                pipelineLayout,
                0,     // firstSet
                1,     // descriptorSetCount
                descriptorSets,
                0,     // dynamicOffsetCount
                null);


            var vertexBuffers = stackalloc[] { vertexBuffer };
            var offsets = stackalloc ulong[] { 0 };
            vk.CmdBindVertexBuffers(commandBuffer, 0, 1, vertexBuffers, offsets);
            vk.CmdDraw(commandBuffer, (uint)vertices.Length, 1, 0, 0);
            vk.CmdEndRenderPass(commandBuffer);

            if (vk.EndCommandBuffer(commandBuffer) != Result.Success)
            {
                Log.Error("Failed to record command buffer.");
                throw new Exception("Failed to record command buffer.");
            }
        }

        private bool IsValidationLayerAvailable(string layerName)
        {
            uint layerCount = 0;
            vk.EnumerateInstanceLayerProperties(ref layerCount, null);
            var availableLayers = new LayerProperties[layerCount];
            fixed (LayerProperties* availableLayersPtr = availableLayers)
            {
                vk.EnumerateInstanceLayerProperties(ref layerCount, availableLayersPtr);
            }
            foreach (var layer in availableLayers)
            {
                string currentLayerName = Marshal.PtrToStringAnsi((IntPtr)layer.LayerName)!;
                if (currentLayerName == layerName)
                {
                    return true;
                }
            }
            return false;
        }

        private void CleanupSwapchain()
        {
            foreach (var framebuffer in swapchainFramebuffers)
            {
                vk.DestroyFramebuffer(device, framebuffer, null);
            }

            vk.DestroyPipeline(device, pipeline, null); //Maybe we dont need this
            vk.DestroyPipelineLayout(device, pipelineLayout, null);

            foreach (var imageView in swapchainImageViews)
            {
                vk.DestroyImageView(device, imageView, null);
            }
            khrSwapchain!.DestroySwapchain(device, swapchain, null);
        }

        private void RecreateSwapchain()
        {
            if (window.Width <= 0 || window.Height <= 0)
                return;

            // Make sure the GPU isn't still using the old resources before destroying them.
            vk.DeviceWaitIdle(device);
            CleanupSwapchain();
            CreateSwapchain(window);
            CreateImageViews();
            CreateGraphicsPipeline();
            CreateFrameBuffer();
        }

        private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
        {
            vk.GetPhysicalDeviceMemoryProperties(physicalDevice, out PhysicalDeviceMemoryProperties memProperties);

            for (int i = 0; i < memProperties.MemoryTypeCount; i++)
            {
                // typeFilter is a bitmask: bit i set means memory type i is allowed for this buffer.
                bool allowedByBuffer = (typeFilter & (1 << i)) != 0;
                bool hasAllProperties = (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties;

                if (allowedByBuffer && hasAllProperties)
                    return (uint)i;
            }

            Log.Error("Failed to find a suitable memory type.");
            throw new Exception("Failed to find a suitable memory type.");
        }

        private void DrawFrame()
        {
            vk.WaitForFences(device, 1, in inFlightFence, true, ulong.MaxValue);

            // The fence guarantees that the previous frame has finished reading
            // this single persistently mapped camera buffer.
            *(CameraBufferData*)cameraUniformBufferMapped =
                currentCameraData;
            *(CameraBufferData*)cameraUniformBufferMapped = currentCameraData;
            *(ObjectBufferData*)objectUniformBufferMapped = currentObjectData;


            vk.ResetFences(device, 1, in inFlightFence);

            uint imageIndex;
            khrSwapchain!.AcquireNextImage(device, swapchain, ulong.MaxValue, imageAvailableSemaphore, default, &imageIndex);

            vk.ResetCommandBuffer(commandBuffers[imageIndex], CommandBufferResetFlags.None);
            RecordCommandBuffer(commandBuffers[imageIndex], imageIndex);

            var waitSemaphores = stackalloc[] { imageAvailableSemaphore };
            var waitStages = stackalloc[] { PipelineStageFlags.ColorAttachmentOutputBit };
            var signalSemaphores = stackalloc[] { renderFinishedSemaphores[imageIndex] };
            var commandBuffer = commandBuffers[imageIndex];

            var submitInfo = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = waitSemaphores,
                PWaitDstStageMask = waitStages,
                CommandBufferCount = 1,
                PCommandBuffers = &commandBuffer,
                SignalSemaphoreCount = 1,
                PSignalSemaphores = signalSemaphores
            };

            if (vk.QueueSubmit(graphicsQueue, 1, in submitInfo, inFlightFence) != Result.Success)
            {
                Log.Error("Failed to submit draw command buffer.");
                throw new Exception("Failed to submit draw command buffer.");
            }

            var swapchains = stackalloc[] { swapchain };
            var presentInfo = new PresentInfoKHR
            {
                SType = StructureType.PresentInfoKhr,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = signalSemaphores,
                SwapchainCount = 1,
                PSwapchains = swapchains,
                PImageIndices = &imageIndex
            };

            khrSwapchain.QueuePresent(graphicsQueue, in presentInfo);
        }

        public void Clear(Foundation.Math.Color color)
        {
            clearColor = color;
        }

        public void Present()
        {
            DrawFrame();
        }

        // Stores the newest camera state on the CPU; DrawFrame will copy it only
        // after its fence proves the GPU no longer reads the uniform buffer.
        public void SetCamera(in CameraBufferData cameraData)
        {
            currentCameraData = cameraData;
        }

        public void Resize(int width, int height)
        {
            RecreateSwapchain();
        }

        public void Dispose()
        {
            vk.DeviceWaitIdle(device);
            vk.DestroyDescriptorPool(device, cameraDescriptorPool, null);
            vk.UnmapMemory(device, cameraUniformBufferMemory);
            vk.UnmapMemory(device, objectUniformBufferMemory);
            vk.DestroyBuffer(device, cameraUniformBuffer, null);
            vk.DestroyBuffer(device, objectUniformBuffer, null);
            vk.FreeMemory(device, cameraUniformBufferMemory, null);
            vk.FreeMemory(device, objectUniformBufferMemory, null);
            foreach (var semaphore in renderFinishedSemaphores)
            {
                vk.DestroySemaphore(device, semaphore, null);
            }
            vk.DestroySemaphore(device, imageAvailableSemaphore, null);
            vk.DestroyFence(device, inFlightFence, null);
            vk.DestroyCommandPool(device, commandPool, null);
            vk.DestroyBuffer(device, vertexBuffer, null);
            CleanupSwapchain();
            vk.DestroyDescriptorSetLayout(device, cameraDescriptorSetLayout, null);
            vk.DestroyRenderPass(device, renderPass, null);
            vk.FreeMemory(device, vertexBufferMemory, null);
            vk.DestroyDevice(device, null);
            khrSurface!.DestroySurface(instance, surface, null);
            vk.DestroyInstance(instance, null);
        }

        public void SetModel(in ObjectBufferData objectData)
        {
            currentObjectData = objectData;
        }
    }
}


