// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using System;
using System.Collections.Generic;
using System.Text;

namespace Engine.Scene.Components
{
    // Stores projection settings only; the camera's position and orientation
    // come from the TransformComponent on the same entity.
    public struct CameraComponent
    {
        // Disabled cameras are ignored when the scene selects its first
        // available camera for rendering.
        public bool Enabled;

        public float VerticalFieldOfView;
        public float NearPlane;
        public float FarPlane;

        public CameraComponent()
        {
            VerticalFieldOfView = MathF.PI / 3.0f; // 60 fok
            NearPlane = 0.1f;
            FarPlane = 1000.0f;

            Enabled = true;
        }
    }
}
