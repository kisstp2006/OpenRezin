using Core.System;
using Engine.Application;
using Rendering;
using Foundation.Logger;
using Foundation;

var window = new SilkWindow(EngineInfo.Name+ " - Sandbox", 800, 600);
var renderer = new VulkanRenderer(window); // while we dont have a renderer, we can use a null renderer to avoid null reference exceptions

var app = new GameApplication(window, renderer);
app.Run();