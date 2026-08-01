// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using Foundation.Math;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Foundation;
using Foundation.Logger;

namespace Rendering
{
    public unsafe class VulkanRenderer : IRenderer
    {
        private readonly Vk vk;
        private Instance instance;
        private PhysicalDevice physicalDevice;

        private Device device;
        private Queue graphicsQueue;

        /// <summary>
        /// // Represents the indices of the queue families that are required for rendering.
        /// </summary>
        struct QueueFamilyIndices
        {
            // Nullable because we don't know yet whether this GPU even has a queue family
            // that supports graphics - it starts "unset" until FindQueueFamilis finds one.
            public uint? GraphicsFamily { get; set; }

            // True once every queue family we need (currently just graphics) has been found.
            public bool IsComplete() => GraphicsFamily.HasValue;
        }

        public VulkanRenderer()
        {
            vk = Vk.GetApi();
            CreateInstance();
            Log.Info("VulkanRenderer initialized successfully.");
            SelectPhysicalDevice();
        }

        private void CreateInstance()
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

            var createInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledExtensionCount = 0,
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
        private QueueFamilyIndices FindQueueFamilies(PhysicalDevice device)
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

                if (indices.IsComplete())
                    break;

                i++;
            }

            return indices;
        }

        private void CreateLogicalDevice()
        {
            var indices = FindQueueFamilies(physicalDevice);
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
        public void Clear(Color color)
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
