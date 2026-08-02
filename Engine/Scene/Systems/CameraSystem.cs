// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using Engine.Scene.Components;
using System.Numerics;
using ECS.World;
using Foundation.Types;
using Rendering;

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
        public static Matrix4x4 CalculateProjectionMatrix(in CameraComponent camera,float aspectRatio)
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

        public static bool TryGetCameraData(World world, float aspectRatio, out CameraBufferData cameraData)
        {
            cameraData = default;

            bool found = world.TryFindFirst<CameraComponent, TransformComponent>(static (camera, transform) => camera.Enabled, out Id cameraEntity);

            if (!found)
                return false;

            if (!world.TryGetComponent(
            cameraEntity,
            out CameraComponent camera) ||
        !world.TryGetComponent(
            cameraEntity,
            out TransformComponent transform))
            {
                return false;
            }

            cameraData = new CameraBufferData
            {
                View = CalculateViewMatrix(in transform),
                Projection = CalculateProjectionMatrix(
                    in camera,
                    aspectRatio)
            };

            return true;

        }
    }
}
