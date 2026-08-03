// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using Core.System;
using Engine.Scene;
using Engine.Scene.Systems;
using Foundation;
using Foundation.Logger;
using Foundation.Math;
using Foundation.Time;
using Rendering;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Engine.Application
{
    public class GameApplication
    {
        private readonly IWindow window;
        private readonly IRenderer renderer;
        private readonly Clock clock = new Clock();
        private readonly GameScene scene = new GameScene();
        public GameScene Scene => scene;

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
            scene.Start();
            while (!window.ShouldClose)
            {
                clock.Tick();
                window.ProcessEvents();

                scene.Update(clock.DeltaTime);

                renderer.Clear(Color.Black);

                // A 0-height window (minimized) would make the projection matrix invalid.
                float aspectRatio = window.Height > 0
                    ? window.Width / (float)window.Height
                    : 0.0f;

                if (aspectRatio > 0.0f &&
                    CameraSystem.TryGetCameraData(scene.World, aspectRatio, out var cameraData))
                {
                    renderer.SetCamera(in cameraData);
                }

                renderer.SetModel(new ObjectBufferData
                {
                    Model = Matrix4x4.CreateRotationZ(clock.TotalTime)
                });


                renderer.Present();

                clock.LimitFrameRate();
            }
            Shutdown();
        }
        public void Shutdown()
        {
            scene.Stop();
            if ( renderer != null && renderer is IDisposable disposableRenderer)
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
