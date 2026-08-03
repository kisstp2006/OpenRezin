// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Rendering
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ObjectBufferData
    {
        public Matrix4x4 Model;
    }
}
