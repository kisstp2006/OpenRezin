using Core.System;
using Foundation.Logger;
using Foundation.Time;

var window = new SilkWindow("Rezin Engine", 800, 600);
var clock = new Clock();


window.Events.Subscribe<WindowResizedEvent>(e =>
{
    Log.Info($"Resized to {e.Width}x{e.Height}");
});

while (!window.ShouldClose)
{
    
    window.ProcessEvents();
    clock.Tick();
    
    //Later here comes the render loop and logic update

    Log.Info($"Frame Time: {clock.DeltaTime} seconds, FPS: {clock.GetFPS():F2}");

    // Limit the frame rate to 60 FPS
    clock.LimitFrameRate();
}