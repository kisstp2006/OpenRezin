using Core.System;
using Foundation.Logger;

var window = new SilkWindow("Rezin Engine", 800, 600);

window.Events.Subscribe<WindowResizedEvent>(e =>
{
    Log.Info($"Resized to {e.Width}x{e.Height}");
});

while (!window.ShouldClose)
{
    window.ProcessEvents();
}