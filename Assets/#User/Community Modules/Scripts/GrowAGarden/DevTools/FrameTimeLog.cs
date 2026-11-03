using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Vulps.GrowAGarden.Detail;
using Vulps.GrowAGarden.Settings;

namespace Vulps.GrowAGarden.DevTools
{
    /// <summary>
    /// The efficiency benchmark: every 60 s, one log line of how the frames went (average, p95, p99, worst, the share over
    /// the headset's budget, and Unity's own CPU and GPU frame times when the client has them) with what the player was
    /// doing, so runs can be compared like for like. In VR the frame rate is locked to the refresh rate, so the average
    /// only moves once frames miss: the over-budget share and the percentiles are what show a change first.
    /// Local, passive, one line a minute. See docs/testing.md → Frame time benchmark.
    /// </summary>
    public class FrameTimeLog : MonoBehaviour
    {
        private const float WINDOW_SECONDS = 60f;

        private readonly List<float> _frames = new List<float>(8192);
        private readonly FrameTiming[] _timing = new FrameTiming[1];
        private readonly List<XRDisplaySubsystem> _displays = new List<XRDisplaySubsystem>();
        private double _cpuSum, _gpuSum;
        private int _timed;
        private float _windowStart = -1f;
        private bool _timingReported;

        // Measuring frames is the one thing that has to happen every frame: there is no event for "a frame took this long".
        private void Update()
        {
            if (_windowStart < 0f) { _windowStart = Time.unscaledTime; return; }
            _frames.Add(Time.unscaledDeltaTime * 1000f);

            if (FrameTimingManager.IsFeatureEnabled())
            {
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, _timing) > 0)
                {
                    _cpuSum += _timing[0].cpuFrameTime;
                    _gpuSum += _timing[0].gpuFrameTime;
                    _timed++;
                }
            }

            if (Time.unscaledTime - _windowStart < WINDOW_SECONDS) return;
            Report();
            _frames.Clear();
            _cpuSum = _gpuSum = 0;
            _timed = 0;
            _windowStart = Time.unscaledTime;
        }

        private void Report()
        {
            if (_frames.Count == 0) return;
            if (!_timingReported)
            {
                _timingReported = true;
                Logger.Info($"Report() '{gameObject.name}' — Unity frame timing (CPU/GPU) is {(FrameTimingManager.IsFeatureEnabled() ? "available" : "not available")} in this client");
            }

            float hz = RefreshRate();
            float budget = 1000f / hz;
            float sum = 0f, worst = 0f;
            int over = 0;
            foreach (float ms in _frames)
            {
                sum += ms;
                if (ms > worst) worst = ms;
                if (ms > budget * 1.05f) over++;   // 5% slack: frame pacing jitters around the budget
            }
            _frames.Sort();
            float avg = sum / _frames.Count;
            float p95 = _frames[Mathf.Min(_frames.Count - 1, (int)(_frames.Count * 0.95f))];
            float p99 = _frames[Mathf.Min(_frames.Count - 1, (int)(_frames.Count * 0.99f))];
            string timing = _timed > 0 ? $" cpu={_cpuSum / _timed:0.0} gpu={_gpuSum / _timed:0.0}" : string.Empty;

            Logger.Info($"Report() '{gameObject.name}' — PERF {_frames.Count} frames: avg={avg:0.0} p95={p95:0.0} p99={p99:0.0} max={worst:0.0} ms, " +
                        $"over={100f * over / _frames.Count:0.0}% of {budget:0.0} ms @{hz:0}Hz{timing} | {Context()}");
        }

        /// <summary>What the player was doing, so two lines can be compared like for like.</summary>
        private static string Context()
        {
            string bubble = DetailManager.CurrentBubble;
            GameSettings settings = GameSettings.GetInstance();
            int players = 0;
            foreach (var _ in DeveloperAccess.Present) players++;
            Vector3 at = PlayerTracker.HasPosition ? PlayerTracker.Position : Vector3.zero;
            return $"night={(Show.ShowManager.Night > 0f ? "yes" : "no")} bubble={(bubble ?? "none")} players={players} " +
                   $"draw={(settings != null ? settings.DrawDistance : GameSettings.DEFAULT_DRAW_DISTANCE):0}m tripping={(Shader.GetGlobalFloat("_GAG_Trip") > 0f ? "yes" : "no")} " +
                   $"at=({at.x:0},{at.y:0},{at.z:0})";
        }

        private float RefreshRate()
        {
            SubsystemManager.GetSubsystems(_displays);
            foreach (XRDisplaySubsystem display in _displays)
                if (display.running && display.TryGetDisplayRefreshRate(out float hz) && hz > 1f) return hz;
            double screen = Screen.currentResolution.refreshRateRatio.value;
            return screen > 1.0 ? (float)screen : 60f;
        }
    }
}
