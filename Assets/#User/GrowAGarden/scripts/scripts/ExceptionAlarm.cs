using TMPro;
using UnityEngine;

namespace GrowAGarden
{
    public class ExceptionAlarm : MonoBehaviour
    {
        // TEMP: disabled while diagnosing master-client authority. The sun is being
        // dropped as the alarm mechanism anyway. Tick this in the Inspector (or flip
        // the default) to re-arm.
        [SerializeField] private bool _armed = false;

        [SerializeField] private Light _sun;

        private void OnEnable()
        {
            if (!_armed) return;
            Application.logMessageReceived += OnLog;
        }

        private void OnDisable() => Application.logMessageReceived -= OnLog;

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception && stackTrace.Contains("GrowAGarden"))
                _sun.enabled = false;
        }
    }
}
