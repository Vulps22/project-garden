using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace GrowAGarden
{
    public class WorldDebugConsole : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private int _maxLines = 20;
        [SerializeField] private string _filter = "[NetworkGrabbable]";

        private Queue<string> _lines = new Queue<string>();

        private void OnEnable()
        {
            Application.logMessageReceived += OnLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= OnLog;
        }

        private void OnLog(string message, string stackTrace, LogType type)
        {
            // Only show lines that match the filter (if one is set)
            if (!string.IsNullOrEmpty(_filter) && !message.Contains(_filter))
                return;

            string prefix = type switch
            {
                LogType.Log => "<color=blue>[LOG]</color>",
                LogType.Error => "<color=red>[ERR]</color>",
                LogType.Exception => "<color=red>[EXC]</color>",
                LogType.Warning => "<color=yellow>[WRN]</color>",
                _ => "<color=white>[LOG]</color>"
            };

            string line = $"{prefix} {message}";

            // For exceptions, append first line of stack trace
            if (type == LogType.Exception && !string.IsNullOrEmpty(stackTrace))
            {
                string firstStack = stackTrace.Split('\n')[0].Trim();
                line += $"\n  <color=grey>{firstStack}</color>";
            }

            _lines.Enqueue(line);
            while (_lines.Count > _maxLines)
                _lines.Dequeue();

            _text.text = string.Join("\n", _lines);
        }
    }
}