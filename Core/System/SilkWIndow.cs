using Foundation.Communication;
using Foundation;
using Silk.NET.Maths;
using Silk.NET.SDL;
using Silk.NET.Windowing;
using SilkIWindow = Silk.NET.Windowing.IWindow;

namespace Core.System
{
    public class SilkWindow : IWindow
    {
       
        private readonly SilkIWindow window;

        public SilkWindow(string title, int width, int height, RendererBackend backend = RendererBackend.Vulkan,bool swapAutomatically = false)
        {
            var options = backend == RendererBackend.Vulkan ? WindowOptions.DefaultVulkan : WindowOptions.Default;
            options.Title = title;
            options.Size = new Vector2D<int>(width, height);
            options.ShouldSwapAutomatically = swapAutomatically; // We will handle buffer swapping manually in the renderer

            if (backend == RendererBackend.OpenGL)
            {
                options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(4, 6));
            }

            window = Silk.NET.Windowing.Window.Create(options);
            window.Initialize();

            window.Resize += newSize =>
            {
                events.Dispatch(new WindowResizedEvent { Width = newSize.X, Height = newSize.Y });
            };
        }

        public string Title
        {
            get => window.Title;
            set => window.Title = value;
        }
        public int Width => window.Size.X;
        public int Height => window.Size.Y;
        public bool IsVisible => window.IsVisible;
        public bool ShouldClose => window.IsClosing;
        public SilkIWindow NativeWindow => window;

        private readonly EventDispatcher events = new EventDispatcher();
        public EventDispatcher Events => events;


        public void ProcessEvents()
        {
            window.DoEvents();
            if (!window.IsClosing) window.DoUpdate();
            if (!window.IsClosing) window.DoRender();
        }
        public void Close() => window.Close();
    }
}
