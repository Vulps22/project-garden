using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Vulps.GrowAGarden.EditorTools
{
    /// <summary>Raises Logger.BUILD by one when Somnium's uploader logs that an upload finished, so the next upload's banner says a new number.</summary>
    [InitializeOnLoad]
    public static class BuildNumberBump
    {
        private const string UPLOAD_DONE = "[Build & Upload]: Build and upload complete.";
        private const string LOGGER_PATH = "Assets/#User/Community Modules/Scripts/GrowAGarden/Logger.cs";
        private static readonly Regex BuildLine = new Regex(@"public const int BUILD = (\d+);");

        static BuildNumberBump() => Application.logMessageReceived += OnLog;

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            if (!message.StartsWith(UPLOAD_DONE)) return;
            EditorApplication.delayCall += Bump;   // after the uploader has finished logging, never mid-upload
        }

        private static void Bump()
        {
            string text = File.ReadAllText(LOGGER_PATH);
            Match match = BuildLine.Match(text);
            if (!match.Success)
            {
                Logger.Warn($"Bump() — no BUILD line in {LOGGER_PATH}; raise it by hand");
                return;
            }

            int next = int.Parse(match.Groups[1].Value) + 1;
            File.WriteAllText(LOGGER_PATH, BuildLine.Replace(text, $"public const int BUILD = {next};", 1));
            Logger.Info($"Bump() — upload complete: Logger.BUILD raised to {next}");
            AssetDatabase.ImportAsset(LOGGER_PATH);
        }
    }
}
