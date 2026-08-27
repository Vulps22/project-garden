using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace GrowAGarden
{
    /// <summary>
    /// Refuses a grab when someone else is already holding the object. Runs before the grab
    /// commits, so a refused attempt never moves anything.
    ///
    /// Extracted from PlantSeed (#45). Note this now applies to bought seeds as well: the old
    /// PlantSeed.Process short-circuited with `if (IsBought) return true`, which skipped the
    /// holder check entirely once a seed had been paid for, so a held seed could be taken out
    /// of someone's hand.
    ///
    /// Affordability is deliberately NOT checked here. A seed you cannot afford is still
    /// grabbable; the shop rejects the purchase at the slot boundary instead, which gives the
    /// player something to feel rather than a seed that silently refuses to move.
    /// </summary>
    public class SingleHolderFilter : MonoBehaviour, IXRSelectFilter
    {
        private IHeldObject _held;

        public bool canProcess => true;

        private void Awake()
        {
            _held = GetComponent<IHeldObject>();
            if (_held == null)
                Logger.Error($"Awake() '{gameObject.name}' — no IHeldObject on this object, grabs will not be arbitrated");
        }

        private void OnEnable()
        {
            // IXRFilterList exposes only Add/Remove -- no Contains -- but Unity pairs
            // OnEnable with OnDisable, so this cannot register twice.
            var interactable = GetComponent<XRGrabInteractable>();
            if (interactable != null) interactable.selectFilters.Add(this);
        }

        private void OnDisable()
        {
            var interactable = GetComponent<XRGrabInteractable>();
            if (interactable != null) interactable.selectFilters.Remove(this);
        }

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            string holder = _held?.HolderId;
            if (string.IsNullOrEmpty(holder)) return true;   // nobody holds it

            // The balance table is the only source of the local player's id, and it is cleared
            // and rebuilt from every master broadcast, so it can be transiently absent. Refuse
            // rather than guess — the next broadcast restores it and the player grabs again.
            PlayerBalance local = EconomyManager.Instance == null
                ? null
                : EconomyManager.Instance.GetLocalPlayer();
            if (local == null)
            {
                Logger.Warn($"Process() '{gameObject.name}' — local identity unavailable, refusing grab");
                return false;
            }

            return local.GetID() == holder;   // only the holder may keep hold of it
        }
    }
}
