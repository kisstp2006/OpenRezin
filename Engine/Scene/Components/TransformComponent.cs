// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using System;
using System.Numerics;


namespace Engine.Scene.Components
{
    // Stores an entity's local position, rotation, and scale without attaching
    // behavior to the ECS component itself.
    public struct TransformComponent
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        public TransformComponent(Vector3 position)
        {
            Position = position;
            Rotation = Quaternion.Identity;
            Scale = Vector3.One;
        }

        // Combines scale, rotation, and translation into the entity's local
        // transformation matrix using System.Numerics row-vector convention.
        public readonly Matrix4x4 LocalMatrix =>
        Matrix4x4.CreateScale(Scale) *
        Matrix4x4.CreateFromQuaternion(Rotation) *
        Matrix4x4.CreateTranslation(Position);
    }
}
