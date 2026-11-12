using UnityEngine;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.Climbing
{
    /// <summary>
    /// Climbing for a 2D player, as in established games (Vulps): walking squarely into climbable
    /// rock grabs on by itself, with no key. On the face the walking keys move along it, up and
    /// across; climbing past the top steps onto the ledge, climbing down to the ground stands, and
    /// jump lets go.
    /// </summary>
    public partial class ClimbController
    {
        [Header("2D players")]

        [Tooltip("How far past the body capsule the rock may be and still be pressed against, in metres.")]
        [SerializeField] private float _desktopReach = 0.35f;

        [Tooltip("How squarely the walk must face the rock to grab it: 1 head-on, 0 alongside. Walking past it at an angle does not grab.")]
        [SerializeField] private float _desktopPushDot = 0.7f;

        [Tooltip("Seconds of pressing into the rock before it grabs on, so brushing past does not.")]
        [SerializeField] private float _desktopPushSeconds = 0.15f;

        [Tooltip("Climbing speed on the face, metres per second.")]
        [SerializeField] private float _desktopClimbSpeed = 2f;

        [Tooltip("Gap kept between the body capsule and the face while climbing, in metres.")]
        [SerializeField] private float _desktopHoldGap = 0.05f;

        [Tooltip("Seconds after letting go before the rock can be grabbed again, so a jump off it is not caught at once.")]
        [SerializeField] private float _desktopRegrabDelay = 0.5f;

        private Collider _wall;
        private Vector3 _face;   // flat, out of the rock
        private float _pushFor;
        private float _nextGrabAt;

        private void UpdateDesktop()
        {
            if (!PlayerManager.TryGetLocalBody(out Vector3 centre, out float radius, out float height)) return;
            if (_climbing) ClimbFace(centre, radius, height);
            else WatchForPress(centre, radius);
        }

        /// <summary>Walking squarely into steep climbable rock for a moment grabs it.</summary>
        private void WatchForPress(Vector3 centre, float radius)
        {
            Vector3 walk = WalkDirection();
            if (walk == Vector3.zero || Time.time < _nextGrabAt) { _pushFor = 0f; return; }

            bool pressing = Nearest(centre, walk, radius + _desktopReach, out RaycastHit hit, requireClimbable: true)
                            && hit.normal.y < _standMinNormalY
                            && Vector3.Dot(walk, -Flat(hit.normal)) >= _desktopPushDot;
            if (!pressing) { _pushFor = 0f; return; }

            _pushFor += Time.deltaTime;
            if (_pushFor < _desktopPushSeconds) return;

            _pushFor = 0f;
            _wall = hit.collider;
            _face = Flat(hit.normal);
            bool body = TakeBody();
            Logger.Info($"UpdateDesktop() '{gameObject.name}' — 2D: walked into '{_wall.name}' at {hit.point}; climbing, body off={body}");
        }

        /// <summary>One frame on the face: let go on jump, otherwise move along the rock and stay against it.</summary>
        private void ClimbFace(Vector3 centre, float radius, float height)
        {
            if (DesktopControls.JumpPressed) { LetGo("jumped off"); return; }

            Vector2 move = DesktopControls.Move;
            Vector3 towards = -_face;
            if (towards == Vector3.zero) { LetGo("lost the face"); return; }

            if (!Nearest(centre, towards, radius + _desktopReach * 2f, out RaycastHit hit, requireClimbable: true))
            {
                if (move.y > 0f) TopOut(towards, radius);
                else LetGo("went off the edge of the face");
                return;
            }

            Vector3 face = Flat(hit.normal);
            if (face != Vector3.zero) _face = face;   // follow the rock round as it curves
            else face = _face;
            _wall = hit.collider;

            if (move.y < 0f && FeetOnGround(centre, height)) { LetGo("climbed down to the ground"); return; }

            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, hit.normal).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, -face);
            Vector3 along = (up * move.y + right * move.x) * (_desktopClimbSpeed * Time.deltaTime);

            Vector3 held = hit.point + face * (radius + _desktopHoldGap);
            held.y = centre.y;
            Place(_root.position + held - centre + along);
        }

        /// <summary>Climbed past the lip: step forward over it, and letting go stands the feet on top.</summary>
        private void TopOut(Vector3 towards, float radius)
        {
            Place(_root.position + towards * (radius * 2f + 0.2f));
            LetGo("topped out");
        }

        private void LetGo(string why)
        {
            _face = Vector3.zero;
            _nextGrabAt = Time.time + _desktopRegrabDelay;
            Logger.Info($"LetGo() '{gameObject.name}' — 2D: {why}");
            EndClimb();
        }

        /// <summary>Ground within a hand's breadth under the capsule's feet.</summary>
        private bool FeetOnGround(Vector3 centre, float height)
        {
            Vector3 feet = centre + Vector3.up * (0.1f - height * 0.5f);
            return Nearest(feet, Vector3.down, 0.25f, out RaycastHit hit, requireClimbable: false) && hit.normal.y >= _standMinNormalY;
        }

        /// <summary>Where the walking keys point, flat, relative to the camera; zero with nothing held.</summary>
        private static Vector3 WalkDirection()
        {
            Vector2 move = DesktopControls.Move;
            Transform view = DesktopControls.View;
            if (move.sqrMagnitude < 0.25f || view == null) return Vector3.zero;
            return Flat(Flat(view.forward) * move.y + Flat(view.right) * move.x);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f ? Vector3.zero : v.normalized;
        }
    }
}
