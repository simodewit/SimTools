using System;
using System.IO;
using System.Threading;

namespace SimTools.Debug
{
    public static class Diag
    {
        private static readonly object _lock = new object();
        private static readonly string _path =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SimTools.debug.log");

        public static void Log(string message)
        {
            var line = $"{DateTime.Now:O} [T{Thread.CurrentThread.ManagedThreadId}] {message}";
            System.Diagnostics.Debug.WriteLine(line); 
            try
            {
                lock(_lock)
                {
                    File.AppendAllText(_path, line + Environment.NewLine);
                }
            }
            catch
            {
                // Best-effort logging; never throw.
            }
        }

        public static void LogEx(string where, Exception ex)
        {
            Log($"{where} EX: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
