// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using System;
using System.Collections.Generic;
using Foundation.Math;
using System.Text;

namespace Rendering
{
    public interface IRenderer
    {
        void Clear(Color color);
        void Present();
        void Resize(int width, int height);
        // Supplies backend-independent camera matrices without exposing ECS
        // components to the rendering layer.
        void SetCamera(in CameraBufferData cameraData);

        void SetModel(in ObjectBufferData objectData);


    }
}
