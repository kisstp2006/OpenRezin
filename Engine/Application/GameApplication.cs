// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using Core.System;
using Foundation;
using Foundation.Logger;
using Foundation.Math;
using Foundation.Time;
using Rendering;
using System;
using System.Collections.Generic;
using System.Text;

namespace Engine.Application
{
    public class GameApplication
    {
        private readonly IWindow window;
        private readonly IRenderer renderer;
        private readonly Clock clock = new Clock();

        public GameApplication(IWindow window, IRenderer renderer)
        {
            this.window = window;
            this.renderer = renderer;

            window.Events.Subscribe<WindowResizedEvent>(e =>
            {
                renderer.Resize(e.Width, e.Height);
            });

            Log.Info("Application initialized successfully.");
        }

        public void Run()
        {
            while (!window.ShouldClose)
            {
                clock.Tick();
                window.ProcessEvents();

                renderer.Clear(Color.Black);
                // Később itt jön a tényleges jelenet renderelése
                renderer.Present();

                clock.LimitFrameRate();
            }
            Shutdown();
        }
        public void Shutdown()
        {
            if( renderer != null && renderer is IDisposable disposableRenderer)
            {
                disposableRenderer.Dispose(); //We dont need to dispose the nullrenderer, but we need to dispose the real ones
            }
            window.Close();

            Log.Info("Application shutdown successfully.");
        }

        public static GameApplication Create(string title, int width, int height, RendererBackend backend)
        {
            // Provide safe defaults so variables are always definitely assigned
            IWindow window = new NullWindow();
            IRenderer renderer = new NullRenderer();

            switch(backend)
            {
                case RendererBackend.Null:
                window = new NullWindow();
                renderer = new NullRenderer();
                break;
                case RendererBackend.Vulkan:
                var silkWindow = new SilkWindow(title+ " (Vulkan)", width, height, backend);
                window = silkWindow;
                renderer = new VulkanRenderer(silkWindow);
                break;
                case RendererBackend.OpenGL:
                var silkWindowGL = new SilkWindow(title+ " (OpenGL)", width, height, backend);
                window = silkWindowGL;
                renderer = new OpenGLRenderer(silkWindowGL);
                break;
            }
            return new GameApplication(window, renderer);
        }
    }
}
