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
