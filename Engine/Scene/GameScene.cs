// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using System;
using System.Collections.Generic;
using System.Text;
using ECS.World;

namespace Engine.Scene
{
    public sealed class GameScene
    {
        private readonly World world = new World();

        private bool started;
        public World World => world;

        public void Start()
        {
            if (started)
                return;

            started = true;

           // One-time system setup goes here.
        }

        public void Update(float deltaTime)
        {
            if (!started)
                return;

        }

        public void Stop()
        {
            if (!started)
                return;

            started = false;

            // Systems that own resources release them here.
        }
    }
}
