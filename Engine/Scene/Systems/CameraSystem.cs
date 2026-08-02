// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using Engine.Scene.Components;
using System.Numerics;

namespace Engine.Scene.Systems
{
    public static class CameraSystem
    {
        // Converts the camera entity's position and rotation into the matrix
        // that transforms world-space positions into camera/view space.
        public static Matrix4x4 CalculateViewMatrix(
        in TransformComponent transform)
        {
            Vector3 forward = Vector3.Transform(
                -Vector3.UnitZ,
                transform.Rotation);

            Vector3 up = Vector3.Transform(
                Vector3.UnitY,
                transform.Rotation);

            return Matrix4x4.CreateLookAt(
                transform.Position,
                transform.Position + forward,
                up);
        }
        // Builds the shared perspective projection; its 0..1 depth range matches
        // Vulkan directly and OpenGL after ClipControl is configured.
        public static Matrix4x4 CalculateProjectionMatrix(
            in CameraComponent camera,
            float aspectRatio)
        {
            if (aspectRatio <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(aspectRatio));
            }

            return Matrix4x4.CreatePerspectiveFieldOfView(
                camera.VerticalFieldOfView,
                aspectRatio,
                camera.NearPlane,
                camera.FarPlane);
        }
    }
}
