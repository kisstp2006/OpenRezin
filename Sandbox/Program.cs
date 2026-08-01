using Engine.Application;
using Foundation;

var app = GameApplication.Create(EngineInfo.Name + " - Sandbox", 800, 600, RendererBackend.Vulkan);
app.Run();