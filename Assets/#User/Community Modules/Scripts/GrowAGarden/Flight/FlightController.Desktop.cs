using UnityEngine;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.Flight
{
    /// <summary>
    /// Flight for a 2D player, as in established games (Vulps): jump, then jump again in the air to
    /// take off. The camera stands in for the wrists: where it looks is the pitch, through the same
    /// model and the same stall, and it is the way flown. Landing is the same contact as in VR.
    /// </summary>
    public partial class FlightController
    {
        [Header("2D players")]

        [Tooltip("How far under the feet still counts as standing, in metres. A jump pressed with nothing this close below is the second jump, and takes off.")]
        [SerializeField] private float _desktopGroundCheck = 0.3f;

        private static readonly RaycastHit[] _groundHits = new RaycastHit[8];

        private void UpdateDesktop()
        {
            bool jump = DesktopControls.JumpPressed && Time.frameCount != _climbEndedFrame;   // the jump that let go of a wall is not the second jump

            if (_flying)
            {
                if (Touching() && Time.time - _airborneSince > _launchGrace)
                {
                    SetFlying(false);
                    Report(0f, 0f, Vector3.zero, true);
                    return;
                }
            }
            else
            {
                if (jump && !OnGround()) TakeOff();
                if (!_flying)
                {
                    Report(0f, 0f, Vector3.zero, true);
                    return;
                }
            }

            Transform view = DesktopControls.View;
            float pitch = view != null ? Elevation(view, HandAxis.Forward) : 0f;
            if (Trip.TripManager.ControlsFlipped) pitch = -pitch;

            Vector3 local = _model.Step(pitch, 0f, Time.deltaTime, out _);

            Vector3 facing = view != null ? view.forward : _head != null ? _head.forward : _root.forward;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.0001f) facing = _root.forward;
            facing.Normalize();

            _velocity = facing * local.z + Vector3.up * local.y;
            Write(_root.position + _velocity * Time.deltaTime);

            Report(pitch, 0f, local, false);
        }

        /// <summary>The second jump: into flight, thrown upward by the flap, which is then spent until landing.</summary>
        private void TakeOff()
        {
            SetFlying(true);
            if (!_flying) return;   // the tutorial's lock turned it straight back

            _flapSpent = true;
            _model.Flap();
            Logger.Info($"TakeOff() '{gameObject.name}' — 2D double jump at {_root.position}; flap spent");
        }

        /// <summary>Whether there is ground just under the body capsule's feet, ignoring the player's own colliders.</summary>
        private bool OnGround()
        {
            if (!PlayerManager.TryGetLocalBody(out Vector3 centre, out _, out float height)) return true;

            Vector3 from = centre + Vector3.up * (0.1f - height * 0.5f);
            int n = Physics.RaycastNonAlloc(from, Vector3.down, _groundHits, 0.1f + _desktopGroundCheck, _terrainMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (_groundHits[i].collider != null && !IsOwn(_groundHits[i].collider)) return true;
            return false;
        }
    }
}
