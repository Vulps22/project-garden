using UnityEngine;

namespace Vulps.GrowAGarden.Show
{
    /// <summary>
    /// The light show's sounds, on the Dome in GardenIsland: a beam's climb at its flower, night falling (once the beam
    /// reaches the dome) and drawing back at the star, and the chord of a convergence. The sources are children of the
    /// Dome so they exist from load, which is what puts them on Somnium's Ambience volume; each is moved to where its
    /// sound happens. Synthesised by sounds/Show/show_sounds.py. Local on every client; nothing is sent.
    /// </summary>
    public class ShowSounds : MonoBehaviour
    {
        [Tooltip("Sources for beams climbing, used in turn so two can overlap.")]
        [SerializeField] private AudioSource[] _beams = new AudioSource[0];

        [Tooltip("Night falling over the island.")]
        [SerializeField] private AudioSource _drop;

        [Tooltip("Night drawing back.")]
        [SerializeField] private AudioSource _lift;

        [Tooltip("A second Moonflower joining.")]
        [SerializeField] private AudioSource _converge;

        private int _nextBeam;
        private float _dropAt = -1f;

        private void Awake()
        {
            ShowManager.BeamClimbing += OnBeamClimbing;
            ShowManager.Raising += OnRaising;
            ShowManager.VisibilityChanged += OnVisibilityChanged;
            ShowManager.Converging += OnConverging;
        }

        private void OnDestroy()
        {
            ShowManager.BeamClimbing -= OnBeamClimbing;
            ShowManager.Raising -= OnRaising;
            ShowManager.VisibilityChanged -= OnVisibilityChanged;
            ShowManager.Converging -= OnConverging;
        }

        private void OnBeamClimbing(Vector3 from)
        {
            if (_beams.Length == 0) return;
            AudioSource source = _beams[_nextBeam];
            _nextBeam = (_nextBeam + 1) % _beams.Length;
            Play(source, from);
        }

        /// <summary>The drop starts when the beam reaches the dome, as the night does.</summary>
        private void OnRaising(MoonflowerPlant plant) => _dropAt = Time.time + ShowManager.BeamSeconds;

        /// <summary>Gone: the night draws back. A takeover by the next Moonflower is a Raising instead, so it drops again.</summary>
        private void OnVisibilityChanged(bool visible)
        {
            _dropAt = -1f;
            if (!visible) Play(_lift, ShowManager.Star);
        }

        private void OnConverging() => Play(_converge, ShowManager.HasDome ? ShowManager.ApexAbove(ShowManager.DomeCentre) : ShowManager.Star);

        private void Update()
        {
            if (_dropAt >= 0f && Time.time >= _dropAt)
            {
                _dropAt = -1f;
                if (ShowManager.IsVisible) Play(_drop, ShowManager.Star);
            }
        }

        private static void Play(AudioSource source, Vector3 at)
        {
            if (source == null || source.clip == null) return;
            source.transform.position = at;
            source.Stop();
            source.Play();
        }
    }
}
