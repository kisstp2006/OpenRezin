// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using System.Numerics;
using System.Runtime.InteropServices;

namespace Rendering;

[StructLayout(LayoutKind.Sequential)]
public struct CameraBufferData
{
    public Matrix4x4 View;
    public Matrix4x4 Projection;
}