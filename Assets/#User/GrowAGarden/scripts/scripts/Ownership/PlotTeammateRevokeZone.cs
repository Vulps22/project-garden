using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// The hut's interior bounds — carrying a wall parchment out through here revokes that
    /// teammate. A raw OnTriggerExit cannot be trusted: grabbing something toggles
    /// Rigidbody.isKinematic, which makes Unity recreate the PhysX actor and fire a spurious exit
    /// for everything it was overlapping with nothing having moved — see Interaction/Socket.cs's
    /// own class note for the same trap. This does the same geometry check Socket.HasLeft does,
    /// without pulling Socket itself in — its Hold/Recall path needs a NetworkObject, which these
    /// local parchments deliberately don't have.
    /// </summary>
    public class PlotTeammateRevokeZone : MonoBehaviour
    {
        [Tooltip("The hut interior volume. Defaults to the collider on this object.")]
        [SerializeField] private Collider _bounds;

        private void OnTriggerExit(Collider other)
        {
            if (!other.TryGetComponent(out TeammateSlotDisplay slot) || !slot.IsHeld) return;
            if (!HasLeft(other.gameObject)) return;   // the actor was re-created; nothing moved

            slot.Plot.RequestRemoveTeammate(slot.TeammateId);
            slot.Hide();
        }

        private bool HasLeft(GameObject candidate)
        {
            if (_bounds == null) return true;   // no volume to test against; trust the event
            return !_bounds.bounds.Contains(candidate.transform.position);
        }

        private void OnValidate()
        {
            if (_bounds == null) _bounds = GetComponent<Collider>();
        }
    }
}
