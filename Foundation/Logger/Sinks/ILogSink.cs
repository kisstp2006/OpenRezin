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