using UnityEngine;

namespace Vulps.GrowAGarden.Show
{
    /// <summary>
    /// The scoreboard dancing to the Garden's music, under the dome only: the name rows bob in a wave to the beat, and
    /// the leader's row punches bigger on each kick, fading in with the night. Rows are moved from where they rest, so
    /// the board's own layout is untouched in daylight. Local; nothing is sent.
    /// </summary>
    public class ScoreboardGroove : MonoBehaviour
    {
        [Tooltip("The name and balance rows, top first; the first is the leader's.")]
        [SerializeField] private Transform[] _rows = new Transform[0];

        [Tooltip("How far a row bobs, in metres, with the music at full.")]
        [SerializeField] private float _bob = 0.03f;

        [Tooltip("How much bigger the leader's row punches on a kick.")]
        [SerializeField] private float _leaderPunch = 0.18f;

        private Vector3[] _restPosition;
        private Vector3 _leaderScale;
        private bool _moving;

        private void Start()
        {
            _restPosition = new Vector3[_rows.Length];
            for (int i = 0; i < _rows.Length; i++) if (_rows[i] != null) _restPosition[i] = _rows[i].localPosition;
            if (_rows.Length > 0 && _rows[0] != null) _leaderScale = _rows[0].localScale;
        }

        private void Update()
        {
            // Only under the dome: night is this client's head inside a closed one.
            float night = ShowManager.Night;
            if (night <= 0f) { if (_moving) Rest(); _moving = false; return; }
            _moving = true;

            float music = Mathf.Clamp01(ShowManager.Level(0) + ShowManager.Level(1) + ShowManager.Level(2)) * night;
            float beat = ShowManager.Beat;
            for (int i = 0; i < _rows.Length; i++)
            {
                if (_rows[i] == null) continue;
                // A wave down the board, a beat long.
                float offset = _bob * music * Mathf.Sin((beat - i * 0.12f) * Mathf.PI * 2f);
                Vector3 up = _rows[i].parent == null ? Vector3.up : _rows[i].parent.InverseTransformDirection(Vector3.up);
                _rows[i].localPosition = _restPosition[i] + up * offset;
            }
            if (_rows.Length > 0 && _rows[0] != null) _rows[0].localScale = _leaderScale * (1f + _leaderPunch * ShowManager.KickGlow * night);
        }

        private void Rest()
        {
            for (int i = 0; i < _rows.Length; i++) if (_rows[i] != null) _rows[i].localPosition = _restPosition[i];
            if (_rows.Length > 0 && _rows[0] != null) _rows[0].localScale = _leaderScale;
        }
    }
}
