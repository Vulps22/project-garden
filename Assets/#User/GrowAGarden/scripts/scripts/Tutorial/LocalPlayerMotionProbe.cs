using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// Raises <see cref="LocalPlayerMoved"/> the first time the local player actually goes
    /// somewhere, and then does nothing for the rest of the session.
    ///
    /// "They have spawned" has no event on the bridge worth trusting for a narrator cue.
    /// <see cref="PlayerManager.LocalPlayerJoined"/> fires when Somnium admits the player to the
    /// session, which is before the headset is tracking, before the world has finished arriving
    /// around them, and on a slow join before they are in any state to listen. Movement is the
    /// first moment we know for certain there is a person in there and they are paying attention.
    ///
    /// Two details that are the whole reason this is a component rather than four lines inline:
    ///
    /// The anchor is taken <em>after</em> the head has been present for <see cref="_settleSeconds"/>,
    /// because Somnium teleports the player to the spawn point after the head exists — anchoring on
    /// the first non-null frame reads that teleport as several metres of walking and fires
    /// immediately.
    ///
    /// The distance is horizontal only, and the threshold is set well above head sway. A standing
    /// player's head wanders a few tens of centimetres without them going anywhere, and the spawn
    /// drop is gravity rather than a decision, so neither should count as movement.
    /// </summary>
    [DisallowMultipleComponent]
    public class LocalPlayerMotionProbe : MonoBehaviour
    {
        /// <summary>
        /// The local player has left where they spawned. Raised once per session, ever.
        ///
        /// Static because it is a fact about the one local player, in the same shape as
        /// <see cref="PlayerManager.LocalPlayerJoined"/>, and because the thing that wants it is a
        /// singleton that should not have to hold a reference to find out.
        /// </summary>
        public static event System.Action LocalPlayerMoved;

        [Tooltip("Horizontal metres from the settled spawn position that count as having moved. " +
                 "Well above head sway: a standing player's head wanders a few tens of centimetres " +
                 "without them going anywhere.")]
        [SerializeField] private float _moveThreshold = 1f;

        [Tooltip("Seconds the head must have existed before the anchor is taken. Covers Somnium's " +
                 "spawn teleport, which happens after the head does and would otherwise read as " +
                 "several metres of walking.")]
        [SerializeField] private float _settleSeconds = 1.5f;

        private Vector3 _anchor;
        private bool _anchored;
        private bool _fired;
        private float _headSeenAt = -1f;

        /// <summary>
        /// Statics outlive a scene when domain reload is off, and a stale subscriber list means the
        /// next session's first movement is delivered to destroyed objects. SceneNetworking clears
        /// its own static events for the same reason.
        /// </summary>
        private void OnDestroy()
        {
            LocalPlayerMoved = null;
        }

        private void Update()
        {
            if (_fired) return;

            // PlayerManager.LocalPlayerHead resolves itself when the rig was not ready at join, so
            // there is nothing to work around here — reading it every frame until it exists is the
            // supported way to wait for a spawn.
            Transform head = PlayerManager.LocalPlayerHead;
            if (head == null)
            {
                _headSeenAt = -1f;   // they have not spawned yet, or have gone; start the clock over
                return;
            }

            if (_headSeenAt < 0f) _headSeenAt = Time.time;
            if (Time.time - _headSeenAt < _settleSeconds) return;

            if (!_anchored)
            {
                _anchor = head.position;
                _anchored = true;
                Logger.Info($"Update() '{gameObject.name}' — spawn anchor {_anchor}, " +
                            $"waiting for {_moveThreshold:F1}m of horizontal movement");
                return;
            }

            Vector3 travelled = head.position - _anchor;
            travelled.y = 0f;
            if (travelled.sqrMagnitude < _moveThreshold * _moveThreshold) return;

            _fired = true;
            Logger.Info($"Update() '{gameObject.name}' — local player moved {travelled.magnitude:F2}m from spawn");
            LocalPlayerMoved?.Invoke();
        }
    }
}
