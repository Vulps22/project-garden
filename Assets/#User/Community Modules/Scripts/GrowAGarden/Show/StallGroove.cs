using TMPro;
using UnityEngine;

namespace Vulps.GrowAGarden.Show
{
    /// <summary>
    /// A stall dancing to the Garden's music, on the BuyPoint prefab: the barrel squashes on each kick like a speaker
    /// cone and pumps with its own band, the crop's name lights up in the stall's colour, and a pool of that colour
    /// glows on the ground beneath it. Colour and band come from where the stall stands, so every client agrees. Only
    /// stalls in earshot of the screen move; the rest, out on the islands, stay still and dark. Local; nothing is sent.
    /// </summary>
    public class StallGroove : MonoBehaviour
    {
        [Tooltip("What bounces: the barrel and its sign.")]
        [SerializeField] private Transform _body;

        [Tooltip("The crop's name on the sign, which lights up.")]
        [SerializeField] private TMP_Text _name;

        [Tooltip("The pool of light on the ground (ShowGlow).")]
        [SerializeField] private Renderer _glow;

        [Tooltip("Scale change on a kick: wider and shorter, a speaker cone's push.")]
        [SerializeField] private Vector3 _kickSquash = new Vector3(0.06f, -0.1f, 0.06f);

        [Tooltip("Extra height at the top of the stall's band.")]
        [SerializeField] private float _pump = 0.04f;

        private static readonly int COLOR = Shader.PropertyToID("_Color");
        private static readonly int INTENSITY = Shader.PropertyToID("_Intensity");

        private Vector3 _rest;
        private Color _restColour;
        private Color _colour;
        private int _band;
        private bool _near;
        private float _checkAt;
        private MaterialPropertyBlock _block;

        private void Start()
        {
            _block = new MaterialPropertyBlock();
            if (_body != null) _rest = _body.localScale;
            if (_name != null) _restColour = _name.color;
            // From where it stands: the same on every client.
            Vector3 p = transform.position;
            int hash = Mathf.Abs(Mathf.RoundToInt(p.x * 73.1f) * 31 + Mathf.RoundToInt(p.z * 151.7f) * 17);
            _colour = Color.HSVToRGB((hash % 360) / 360f, 0.75f, 1f);
            _band = hash % 3;   // bass, low-mid or mid: the highs are too twitchy for a barrel
            if (_glow != null) _glow.enabled = false;
        }

        private void Update()
        {
            if (Time.time >= _checkAt)
            {
                _checkAt = Time.time + 2f + Random.value;
                bool near = ShowManager.InEarshot(transform.position);
                if (near != _near) { _near = near; if (!near) Rest(); if (_glow != null) _glow.enabled = near; }
            }
            if (!_near) return;

            float level = ShowManager.Level(_band), kick = ShowManager.KickGlow;
            if (_body != null) _body.localScale = Vector3.Scale(_rest, Vector3.one + _kickSquash * kick + Vector3.up * _pump * level);
            if (_name != null) _name.color = Color.Lerp(_restColour, _colour, Mathf.Clamp01(level * 1.3f + kick * 0.5f));
            if (_glow != null)
            {
                _glow.GetPropertyBlock(_block);
                _block.SetColor(COLOR, _colour);
                _block.SetFloat(INTENSITY, 0.25f + 1.6f * level + 0.8f * kick);
                _glow.SetPropertyBlock(_block);
            }
        }

        private void Rest()
        {
            if (_body != null) _body.localScale = _rest;
            if (_name != null) _name.color = _restColour;
        }
    }
}
