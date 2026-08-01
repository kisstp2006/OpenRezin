// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using System;
using System.Collections.Generic;
using System.Text;
using Foundation;
using Foundation.Communication;

namespace Core.System
{
    public interface IWindow
    {
        string Title { get; set; }
        int Width { get; }
        int Height { get; }
        bool IsVisible { get; }
        bool ShouldClose { get; }

        EventDispatcher Events { get; }

        void ProcessEvents();
        void Close();
    }
}
