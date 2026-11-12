using UnityEngine;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.Flight
{
    /// <summary>
    /// Flight for a 2D player (Vulps): a double jump takes off into a hover; forward flies, back brakes to a hover
    /// without leaving flight; a jump boosts height and a double jump ends flight. Flying, the camera stands in for
    /// the wrists: where it looks is the pitch, through the same model and stall, and the way it faces is the way
    /// flown. Landing is the same contact as in VR.
    /// </summary>
    public partial class FlightController
    {
        [Header("2D players")]

        [Tooltip("How far under the feet still counts as standing, in metres. A jump pressed with nothing this close below is the second jump, and takes off.")]
        [SerializeField] private float _desktopGroundCheck = 0.3f;

        [Tooltip("Seconds within which a second jump in flight is a double jump, and ends flight.")]
        [SerializeField] private float _doubleJumpSeconds = 0.35f;

        private static readonly RaycastHit[] _groundHits = new RaycastHit[8];

        private bool _braking;
        private float _lastJumpAt = -1f;

        private void UpdateDesktop()
        {
            bool jump = DesktopControls.JumpPressed && Time.frameCount != _climbEndedFrame;   // the jump that let go of a wall is not the second jump

            if (!_flying)
            {
                if (jump && !OnGround()) TakeOff();
                if (!_flying)
                {
                    Report(0f, 0f, Vector3.zero, true);
                    return;
                }
                jump = false;   // that press was the take-off
            }
            else if (Touching() && Time.time - _airborneSince > _launchGrace)
            {
                SetFlying(false);
                Report(0f, 0f, Vector3.zero, true);
                return;
            }

            if (jump && Jump()) return;
            Steer(DesktopControls.Move.y);

            Transform view = DesktopControls.View;
            float pitch = _braking ? _settings.StallAngleDeg : view != null ? Elevation(view, HandAxis.Forward) : 0f;
            if (!_braking && Trip.TripManager.ControlsFlipped) pitch = -pitch;

            Vector3 local = _model.Step(pitch, 0f, Time.deltaTime, out _);

            Vector3 facing = view != null ? view.forward : _head != null ? _head.forward : _root.forward;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.0001f) facing = _root.forward;
            facing.Normalize();

            _velocity = facing * local.z + Vector3.up * local.y;
            Write(_root.position + _velocity * Time.deltaTime);

            Report(pitch, 0f, local, false);
        }

        /// <summary>The second jump: into flight, hovering until forward is pressed.</summary>
        private void TakeOff()
        {
            SetFlying(true);
            if (!_flying) return;   // the tutorial's lock turned it straight back

            _braking = true;
            _lastJumpAt = -1f;
            Logger.Info($"TakeOff() '{gameObject.name}' — 2D double jump at {_root.position}; hovering");
        }

        /// <summary>A jump in flight boosts height; a second within the double-jump window ends flight. True if flight ended.</summary>
        private bool Jump()
        {
            if (_lastJumpAt >= 0f && Time.time - _lastJumpAt <= _doubleJumpSeconds)
            {
                _lastJumpAt = -1f;
                SetFlying(false);
                Logger.Info($"Jump() '{gameObject.name}' — 2D double jump: flight ended at {_root.position}");
                return true;
            }

            _lastJumpAt = Time.time;
            _model.Flap();
            Logger.Info($"Jump() '{gameObject.name}' — 2D boost at {_root.position.y:F1}m");
            return false;
        }

        /// <summary>Forward flies, from the glide speed if the brake had slowed it; back brakes to a hover. Either holds until the other.</summary>
        private void Steer(float forward)
        {
            if (forward > 0.5f && _braking)
            {
                _braking = false;
                if (_model.Speed < _settings.BestGlideSpeed) _model.ResumeAt(_settings.BestGlideSpeed);
                Logger.Info($"Steer() '{gameObject.name}' — 2D: flying");
            }
            else if (forward < -0.5f && !_braking)
            {
                _braking = true;
                Logger.Info($"Steer() '{gameObject.name}' — 2D: braking to a hover");
            }
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
