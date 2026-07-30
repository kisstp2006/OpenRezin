using Foundation.Logger.Sinks;
using System.Runtime.CompilerServices;

namespace Foundation.Logger
{
    public enum LogSeverity
    {
        INFO = 2,
        WARNING = 3,
        ERROR = 4,
        UNKNOWN = -1,
    }

    public class LogEntry
    {
        public LogSeverity Severity;
        public string File;
        public int Line;
        public string Message;
    }

    public static class Log
    {
        static List<LogEntry> logs = new List<LogEntry>();
        static List<ILogSink> sinks = new List<ILogSink>();

        static Log()
        {
            sinks.Add(new ConsoleSink());
        }

        public static void Info(string message, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            var entry = new LogEntry { Severity = LogSeverity.INFO, File = file, Line = line, Message = message };
            logs.Add(entry);
            

            foreach (var sink in sinks)
                sink.Write(entry);
        }

        public static void Warn(string message, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            var entry = new LogEntry { Severity = LogSeverity.WARNING, File = file, Line = line, Message = message };
            logs.Add(entry);
            

            foreach (var sink in sinks)
                sink.Write(entry);
        }

        public static void Error(string message, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            var entry = new LogEntry { Severity = LogSeverity.ERROR, File = file, Line = line, Message = message };
            logs.Add(entry);
            

            foreach (var sink in sinks)
                sink.Write(entry);
        }
    }
}
