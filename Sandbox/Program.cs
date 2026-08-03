using Engine.Application;
using Engine.Scene.Components;
using Foundation;
using System.Numerics;

var app = GameApplication.Create(EngineInfo.Name + " - Sandbox", 800, 600, RendererBackend.OpenGL);

// The triangle sits on the z = 0 plane, so the camera has to stand back from it.
var cameraEntity = app.Scene.World.CreateEntity();
app.Scene.World.AddComponent(cameraEntity, new TransformComponent(new Vector3(0, 0, 2)));
app.Scene.World.AddComponent(cameraEntity, new CameraComponent());

app.Run();