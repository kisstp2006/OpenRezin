// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Foundation.Logger;

namespace Rendering.Shader
{
    public class ShaderCompiler
    {
        public static byte[] CompileHlslToSpirv(string hlslPath, string entryPoint, string targetProfile, bool invertY = false)
        {
            string dxcPath = FindDxc();
            string tempOutput = Path.GetTempFileName();

            string invertYFlag = invertY ? "-fvk-invert-y" : "";


            var startInfo = new ProcessStartInfo
            {
                FileName = dxcPath,
                Arguments = $"-spirv -T {targetProfile} -E {entryPoint} {invertYFlag} -Fo \"{tempOutput}\" \"{hlslPath}\"",
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = Process.Start(startInfo)!;
            string errorOutput = process.StandardError.ReadToEnd();

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                File.Delete(tempOutput);
                Log.Error($"DXC failed to compile {hlslPath} ({entryPoint}): {errorOutput}");
                throw new Exception($"DXC failed to compile {hlslPath} ({entryPoint}): {errorOutput}");
            }

            Log.Info($"Successfully compiled {hlslPath} ({entryPoint})");
            byte[] spirv = File.ReadAllBytes(tempOutput);
            File.Delete(tempOutput);
            return spirv;
        }
        private static string FindDxc()
        {
            string? vulkanSdk = Environment.GetEnvironmentVariable("VULKAN_SDK");
            if (vulkanSdk == null)
            {
                Log.Error("VULKAN_SDK environment variable not set - required to locate dxc.");
                throw new Exception("VULKAN_SDK environment variable not set - required to locate dxc.");
            }

            string executableName = OperatingSystem.IsWindows() ? "dxc.exe" : "dxc";
            string dxcPath = Path.Combine(vulkanSdk, "bin", executableName);

            if (!File.Exists(dxcPath))
            {
                Log.Error($"dxc not found at {dxcPath}.");
                throw new Exception($"dxc not found at {dxcPath}.");
            }

            Log.Info($"Found dxc at {dxcPath}.");
            return dxcPath;
        }
    }
}

