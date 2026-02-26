//using System;
//using System.Diagnostics;
//using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace GrowAGarden
{
    public static class Logger
    {
        /*
        private static StreamWriter _writer;

        private static StreamWriter Writer
        {
            get
            {
                if (_writer != null) return _writer;

                string path = Path.Combine(Application.persistentDataPath, "growAGarden.log");
                _writer = new StreamWriter(path, append: true) { AutoFlush = true };
                _writer.WriteLine($"\n=== Session started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");

                Application.quitting += () =>
                {
                    _writer.WriteLine($"=== Session ended {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                    _writer.Close();
                };

                return _writer;
            }
        }*/

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Log(string message)   => Write("LOG  ", message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Info(string message)  => Write("INFO ", message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Warn(string message)  => Write("WARN ", message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Error(string message) => Write("ERROR", message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Write(string level, string message)
        {
            Debug.Log($"[{level}] {message}");
            /*
            // Frame 0 = Write, Frame 1 = Log/Info/Warn/Error, Frame 2 = actual caller
            string caller = new StackFrame(2).GetMethod()?.DeclaringType?.Name ?? "?";
            Writer.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{level}] [{caller}] {message}");
            */
        }
    }
}
