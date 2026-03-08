using TMPro;
using UnityEngine;

namespace GrowAGarden
{
    public class ExceptionAlarm : MonoBehaviour
    {
        [SerializeField] private Light _sun;

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception && stackTrace.Contains("GrowAGarden"))
                _sun.enabled = false;
        }
    }
}
