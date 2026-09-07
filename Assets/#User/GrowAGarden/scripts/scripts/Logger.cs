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

        private static bool _sessionAnnounced;

        /// <summary>
        /// Prints one banner at the top of this run's log, once.
        ///
        /// The client log is append-only across sessions and routinely hundreds of megabytes, so
        /// every histogram and every grep is over *every* run the file remembers, not the one you
        /// just did. Two separate diagnoses have been made against a previous session's evidence
        /// and reported as if they described the current build. The banner is the line that makes
        /// "which run is this" answerable.
        ///
        /// Idempotent, so wiring it to more than one manager is harmless.
        /// </summary>
        public static void BeginSession()
        {
            if (_sessionAnnounced) return;
            _sessionAnnounced = true;

            Debug.Log($"[GrowAGarden] Logging Session Started {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }

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
