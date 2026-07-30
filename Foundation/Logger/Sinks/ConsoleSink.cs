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
            public class ConsoleSink : ILogSink
            {
                public void Write(LogEntry entry)
                {
                    Console.ForegroundColor = entry.Severity switch
                    {
                        LogSeverity.INFO => ConsoleColor.Blue,
                        LogSeverity.WARNING => ConsoleColor.Yellow,
                        LogSeverity.ERROR => ConsoleColor.Red,
                        LogSeverity.UNKNOWN => ConsoleColor.White,
                        _ => ConsoleColor.Gray // or we can reset the console color
                    };

                    Console.WriteLine($"{entry.File}({entry.Line}): {entry.Message}");
                    Console.ResetColor();
                }
            }

        }

    }

}

