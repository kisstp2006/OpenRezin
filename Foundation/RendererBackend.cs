// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using System;
using System.Collections.Generic;
using System.Text;

namespace Foundation
{
    public enum RendererBackend
    {
        Null,
        OpenGL, // keep opengl as a reminder that i want to implement it lateron
        Vulkan //Our current real working backend
    }
}
