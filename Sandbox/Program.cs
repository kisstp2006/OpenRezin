using Core.System;
using Engine.Application;
using Rendering;

var window = new SilkWindow("Rezin Engine", 800, 600);
var renderer = new NullRenderer(); // while we dont have a renderer, we can use a null renderer to avoid null reference exceptions

var app = new GameApplication(window, renderer);
app.Run();