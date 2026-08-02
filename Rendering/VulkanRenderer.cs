// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using Core.System;
using Foundation;
using Foundation.Logger;
using Foundation.Math;
using Silk.NET.Core;
using Silk.NET.SDL;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using System.Runtime.InteropServices;
using System.Text;

using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Rendering
{
    public unsafe class VulkanRenderer : IRenderer
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
        private SilkWindow window;

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
            public bool IsComplete() => GraphicsFamily.HasValue && PresentFamily.HasValue;

        
        }

        public VulkanRenderer(SilkWindow window)
        {
            vk = Vk.GetApi();
            CreateInstance(window);
            CreateSurface(window);
            Log.Info("VulkanRenderer initialized successfully.");
            SelectPhysicalDevice();
            CreateLogicalDevice();
            CreateSwapchain(window);
            CreateImageViews();
            CreateRenderPass();
            CreateGraphicsPipeline();
            CreateFrameBuffer();
            CreateCommandPool();
            CreateCommandBuffers();
            CreateSyncObjects();

            this.window = window;
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

            var createInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledExtensionCount = extensionCount,
                PpEnabledExtensionNames = reqiredExtensions,
                EnabledLayerCount = 0
            };

            if (vk.CreateInstance(in createInfo, null, out instance) != Result.Success)
            {
                Log.Error("Failed to create Vulkan instance.");
                throw new Exception("Failed to create Vulkan instance.");
            }

            // The native strings were only needed for the call above.
            Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
            Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);



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

                    if (properties.DeviceType == PhysicalDeviceType.DiscreteGpu)
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

                    if (properties.DeviceType == PhysicalDeviceType.IntegratedGpu)
                    {
                        physicalDevice = device;
                        Log.Info($"Selected integrated GPU: {Marshal.PtrToStringAnsi((IntPtr)properties.DeviceName)}");
                        return;
                    }
                }
            }
            // No preferred GPU type found - fall back to the first available device.
            if (physicalDevice.Handle == 0)
                physicalDevice = devices.FirstOrDefault();

            if (physicalDevice.Handle == 0)
                {
                    Log.Error("No GPUs found with Vulkan support.");
                    throw new Exception("No GPUs found with Vulkan support.");
                }
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
            if(khrSurface!.GetPhysicalDeviceSurfaceCapabilities(physicalDevice, surface, out SurfaceCapabilitiesKHR capabilities) == Result.Success){
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

                foreach(var presentMode in presentModes)
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

                if(capabilities.MaxImageCount>0 && imageCount > capabilities.MaxImageCount)
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
                    ImageSharingMode= SharingMode.Exclusive,
                    ImageUsage = ImageUsageFlags.ColorAttachmentBit,
                    PreTransform = capabilities.CurrentTransform,
                    CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
                    PresentMode = chosenPresentMode,
                    Clipped = true,
                    OldSwapchain = default
                };

                 if(khrSwapchain.CreateSwapchain(device, in swapchainCreateInfo, null, out swapchain) != Result.Success)
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
            byte[] vertShaderCode = File.ReadAllBytes("Shaders/triangle.vert.spv");
            byte[] fragShaderCode = File.ReadAllBytes("Shaders/triangle.frag.spv");

            ShaderModule vertexShaderModule = CreateShaderModule(vertShaderCode);
            ShaderModule fragmentShaderModule = CreateShaderModule(fragShaderCode);

            // Binds each shader module to its pipeline stage; "main" is the GLSL entry point.
            var vertShaderStageInfo = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = vertexShaderModule,
                PName = (byte*)Marshal.StringToHGlobalAnsi("main")
            };

            var fragShaderStageInfo = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = fragmentShaderModule,
                PName = (byte*)Marshal.StringToHGlobalAnsi("main")
            };

            PipelineShaderStageCreateInfo[] shaderStages = new PipelineShaderStageCreateInfo[] { vertShaderStageInfo, fragShaderStageInfo };

            // No vertex buffer yet - the triangle's positions/colors are hardcoded in the shader.
            var vertexInputInfo = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 0,
                PVertexBindingDescriptions = null,
                VertexAttributeDescriptionCount = 0,
                PVertexAttributeDescriptions = null
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

            // No descriptor sets or push constants yet - the shader has no external inputs.
            var pipelineLayoutInfo = new PipelineLayoutCreateInfo
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = 0,
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

            for(int i = 0; i < swapchainImageViews.Length; i++)
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

            if (vk.CreateSemaphore(device, in semaphoreInfo, null, out renderFinishedSemaphore) != Result.Success)
            {
                Log.Error("Failed to create render finished semaphore.");
                throw new Exception("Failed to create render finished semaphore.");
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
            vk.CmdDraw(commandBuffer, 3, 1, 0, 0);
            vk.CmdEndRenderPass(commandBuffer);

            if (vk.EndCommandBuffer(commandBuffer) != Result.Success)
            {
                Log.Error("Failed to record command buffer.");
                throw new Exception("Failed to record command buffer.");
            }
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


        private void DrawFrame()
        {
            vk.WaitForFences(device, 1, in inFlightFence, true, ulong.MaxValue);
            vk.ResetFences(device, 1, in inFlightFence);

            uint imageIndex;
            khrSwapchain!.AcquireNextImage(device, swapchain, ulong.MaxValue, imageAvailableSemaphore, default, &imageIndex);

            vk.ResetCommandBuffer(commandBuffers[imageIndex], CommandBufferResetFlags.None);
            RecordCommandBuffer(commandBuffers[imageIndex], imageIndex);

            var waitSemaphores = stackalloc[] { imageAvailableSemaphore };
            var waitStages = stackalloc[] { PipelineStageFlags.ColorAttachmentOutputBit };
            var signalSemaphores = stackalloc[] { renderFinishedSemaphore };
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

        public void Resize(int width, int height)
        {
            RecreateSwapchain();
        }
    }
}
