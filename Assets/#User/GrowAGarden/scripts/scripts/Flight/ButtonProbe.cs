using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace GrowAGarden
{
    /// <summary>
    /// ⚠ Temporary instrumentation. Delete once the boost has a button.
    ///
    /// Logs every XR input feature as it changes, so one run in-world says exactly which device
    /// and which usage Somnium's jump is bound to — rather than us guessing a binding, uploading,
    /// and finding out it was the wrong one.
    ///
    /// It **enumerates** each device's features via TryGetFeatureUsages instead of polling a
    /// hardcoded list of CommonUsages. Runtimes disagree about which usages they expose and under
    /// what names, and a guessed list would silently miss the one button that matters. Whatever
    /// the headset actually offers is what gets watched.
    ///
    /// Only rising and falling edges are logged, never a per-frame state dump: the client log is
    /// append-only and a per-frame write is how it reached 647 MB once already.
    /// </summary>
    public class ButtonProbe : MonoBehaviour
    {
        [Tooltip("Off by default. This is a diagnostic, not a feature.")]
        [SerializeField] private bool _enabled = true;

        [Tooltip("Also log analogue axes when they cross this from rest. Triggers and grips are " +
                 "often floats rather than booleans, and a jump may well be one of them. Zero " +
                 "disables axis watching entirely.")]
        [SerializeField] private float _axisThreshold = 0.6f;

        [Tooltip("Seconds between re-checking which devices exist. Controllers connect late and " +
                 "reconnect when they sleep.")]
        [SerializeField] private float _rescanInterval = 5f;

        private readonly List<InputDevice> _devices = new();
        private readonly List<InputFeatureUsage> _usages = new();
        private readonly Dictionary<string, bool> _bools = new();
        private readonly Dictionary<string, bool> _axesOver = new();
        private float _nextScanAt;

        private void Update()
        {
            if (!_enabled) return;

            if (Time.time >= _nextScanAt)
            {
                _nextScanAt = Time.time + Mathf.Max(1f, _rescanInterval);
                Rescan();
            }

            for (int d = 0; d < _devices.Count; d++)
            {
                InputDevice device = _devices[d];
                if (!device.isValid) continue;

                _usages.Clear();
                if (!device.TryGetFeatureUsages(_usages)) continue;

                for (int u = 0; u < _usages.Count; u++)
                {
                    InputFeatureUsage usage = _usages[u];
                    string key = device.name + "/" + usage.name;

                    if (usage.type == typeof(bool))
                    {
                        if (!device.TryGetFeatureValue(usage.As<bool>(), out bool value)) continue;
                        _bools.TryGetValue(key, out bool was);
                        if (value == was) continue;
                        _bools[key] = value;
                        if (value) Logger.Info($"Button() '{gameObject.name}' — DOWN {key}");
                        else Logger.Info($"Button() '{gameObject.name}' — up   {key}");
                    }
                    else if (_axisThreshold > 0f && usage.type == typeof(float))
                    {
                        if (!device.TryGetFeatureValue(usage.As<float>(), out float value)) continue;
                        bool over = value >= _axisThreshold;
                        _axesOver.TryGetValue(key, out bool wasOver);
                        if (over == wasOver) continue;
                        _axesOver[key] = over;
                        if (over) Logger.Info($"Button() '{gameObject.name}' — AXIS {key} = {value:F2}");
                    }
                }
            }
        }

        /// <summary>Re-reads the device list and reports what each one offers, once per device.</summary>
        private void Rescan()
        {
            int before = _devices.Count;
            InputDevices.GetDevices(_devices);
            if (_devices.Count == before) return;

            foreach (InputDevice device in _devices)
            {
                if (!device.isValid) continue;
                _usages.Clear();
                device.TryGetFeatureUsages(_usages);

                var names = new List<string>();
                foreach (InputFeatureUsage usage in _usages)
                    if (usage.type == typeof(bool) || usage.type == typeof(float))
                        names.Add(usage.name + ":" + (usage.type == typeof(bool) ? "b" : "f"));

                Logger.Info($"Rescan() '{gameObject.name}' — '{device.name}' " +
                            $"chars={device.characteristics} features=[{string.Join(", ", names)}]");
            }
        }
    }
}
