// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using Foundation.Communication;
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.System
{
    public class NullWindow : IWindow
    {
        private bool shouldClose = false;


        public string Title { get; set; } = "Rezin Window";
        public int Width => 0;
        public int Height => 0;
        public bool IsVisible => false;
        public bool ShouldClose => shouldClose;

        private readonly EventDispatcher events = new EventDispatcher();
        public EventDispatcher Events => events;

        public void ProcessEvents()
        {
            
        }
        public void Close()
        {
            shouldClose = true;
        }
    }
   
}
