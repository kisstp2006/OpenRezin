// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using Foundation.Math;
using Core.System;
using Foundation.Logger;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using EngineColor = Foundation.Math.Color;


namespace Rendering
{
    public class OpenGLRenderer : IRenderer
    {
        private readonly SilkWindow window;
        private readonly GL gl;

        public OpenGLRenderer(SilkWindow window)
        {
            ArgumentNullException.ThrowIfNull(window);
            this.window = window;
            this.gl = GL.GetApi(window.NativeWindow);
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
