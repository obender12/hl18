using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace hl18
{
    // log helper (console only)
    public class Log
    {
        static Log() => Console.OutputEncoding = Encoding.UTF8;

        [Conditional("DEBUG")]
        public static void Debug(string text, params object[] args)
        {
            write("DEBUG", text, args);
        }

        [Conditional("EPOLL_DEBUG")]
        public static void Epoll(string text, params object[] args)
        {
            write($"[T:{Thread.CurrentThread.ManagedThreadId}]", text, args);
        }

        public static void Info(string text, params object[] args) => write("INFO", text, args);
        public static void Warning(string text, params object[] args) => write("WARNING", text, args);
        public static void Error(string text, params object[] args) => write("ERROR", text, args);

        public static object locker = new object();
        private static void write(string level, string text, params object[] args)
        {
            var prefix = string.Format("[{0}] {1}: ", Stats.Watch.Elapsed, level);
            lock (locker)
                Console.WriteLine(prefix + text, args);
        }
    }
}
