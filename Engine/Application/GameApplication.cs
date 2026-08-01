// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using Core.System;
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
        }
    }
}
