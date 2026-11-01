using Fusion;
using UnityEngine;
using Vulps.GrowAGarden.Ownership;
using Vulps.GrowAGarden.Trip;

namespace Vulps.GrowAGarden.Modules
{
    /// <summary>
    /// A produce that can be eaten: held at the mouth for a moment, it is gone and its eater is
    /// tripping. The eater holds it, so the eater has authority over it and ends it; the trip is the
    /// eater's alone. See docs/weed.md.
    /// </summary>
    public class EdibleModule : BehaviourModule
    {
        [Tooltip("Strength of the trip, 0 to 1. A bloom is the intense one.")]
        [SerializeField] private float _strength = 1f;

        [Tooltip("Seconds the trip lasts, before it eases off.")]
        [SerializeField] private float _seconds = 180f;

        [Tooltip("How close to the mouth counts as eating, in metres.")]
        [SerializeField] private float _mouthRadius = 0.12f;

        [Tooltip("Seconds held at the mouth before it is eaten, so a hand passing the face does not eat it.")]
        [SerializeField] private float _dwellSeconds = 0.5f;

        private float _atMouth;
        private bool _eaten;
        private bool _warnedAuthority;

        private void Update()
        {
            if (_eaten || !HeldByMe() || !AtMouth()) { _atMouth = 0f; return; }

            _atMouth += Time.deltaTime;
            if (_atMouth >= _dwellSeconds) Eat();
        }

        private bool HeldByMe() =>
            Produce.IsHarvested && !string.IsNullOrEmpty(Produce.HolderId) && Produce.HolderId == PlayerManager.LocalPlayerId;

        private bool AtMouth()
        {
            Vector3? mouth = PlayerManager.LocalMouthPosition;
            return mouth != null && Vector3.Distance(mouth.Value, transform.position) <= _mouthRadius;
        }

        private void Eat()
        {
            NetworkObject obj = GetComponent<NetworkObject>();
            if (obj == null || !obj.HasStateAuthority)
            {
                if (!_warnedAuthority) Logger.Warn($"Eat() '{gameObject.name}' — held at the mouth but this client has no authority over it yet");
                _warnedAuthority = true;
                return;
            }

            _eaten = true;
            TripManager.Begin(_strength, _seconds, $"ate '{gameObject.name}'", flipsControls: true);
            WorldManager.DespawnIfStateAuthority(obj);
            Logger.Info($"Eat() '{gameObject.name}' — eaten");
        }
    }
}
