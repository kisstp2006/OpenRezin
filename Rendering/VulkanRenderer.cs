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
using System.Runtime.InteropServices;
using System.Text;

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
        }

        private void CreateInstance(SilkWindow window)
        {
            var appInfo = new ApplicationInfo
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = (byte*)Marshal.StringToHGlobalAnsi(EngineInfo.Name),
                ApplicationVersion = (Version32)EngineInfo.Version,
                PEngineName = (byte*)Marshal.StringToHGlobalAnsi(EngineInfo.Name),
                EngineVersion = (Version32)EngineInfo.Version,
                ApiVersion = Vk.Version13
            };

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

            Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
            Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);


            
        }

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

            var createInfo = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &queueCreateInfo,
                EnabledExtensionCount = 0,
                EnabledLayerCount = 0,
                PEnabledFeatures = &deviceFeatures
            };

            
            // The actual "connection" to the GPU - every later Vulkan call goes through this.
            if (vk.CreateDevice(physicalDevice, in createInfo, null, out device) != Result.Success)
            {
                Log.Error("Failed to create logical device.");
                throw new Exception("Failed to create logical device.");
            }

            // Fetch the usable queue handle for the family we picked above.
            vk.GetDeviceQueue(device, indices.GraphicsFamily!.Value, 0, out graphicsQueue);
        }

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

            }
            else
            {
                Log.Error("Failed to get surface capabilities.");
                throw new Exception("Failed to get surface capabilities.");
            }
        }
        public void Clear(Foundation.Math.Color color)
        {
            
        }

        public void Present()
        {
            
        }

        public void Resize(int width, int height)
        {
            
        }
    }
}
