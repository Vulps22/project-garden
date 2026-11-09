using UnityEngine;
using Vulps.GrowAGarden.Ownership;
using Vulps.GrowAGarden.Tutorial;

namespace Vulps.GrowAGarden.Interaction
{
    /// <summary>A trigger round a buy point: the local player walking in hears the stocked seed's voiceover, once ever. Local only.</summary>
    [RequireComponent(typeof(Collider))]
    public class ApproachVoiceZone : MonoBehaviour
    {
        [Tooltip("The trigger. Auto-filled; off unless the stocked seed has a voiceover.")]
        [SerializeField] private Collider _zone;

        private SeedDefinition _seed;

        private void OnValidate()
        {
            if (_zone == null) _zone = GetComponent<Collider>();
        }

        private void Awake()
        {
            if (_zone == null) _zone = GetComponent<Collider>();
            if (_seed == null) _zone.enabled = false;
        }

        /// <summary>The stall's stock changed: listen only while it is a seed with something to say.</summary>
        public void Show(SeedDefinition seed)
        {
            _seed = seed != null && seed.approachVoice != null ? seed : null;
            if (_zone == null) _zone = GetComponent<Collider>();
            if (_zone != null) _zone.enabled = _seed != null;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_seed == null || !PlayerManager.IsLocalPlayer(other)) return;
            TipNarrator.PlaySeed(_seed);
        }
    }
}
