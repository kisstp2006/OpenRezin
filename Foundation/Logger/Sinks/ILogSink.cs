// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using Foundation.Logger;
using System;
using System.Collections.Generic;
using System.Text;

namespace Foundation
{
    namespace Logger
    {
        namespace Sinks
        {
            public interface ILogSink
            {
                public void Write(LogEntry entry);
            }
        }
    }
}