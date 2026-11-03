using UnityEngine;
using Vulps.GrowAGarden.Crops;
using Vulps.GrowAGarden.Interaction;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.DevTools
{
    /// <summary>
    /// A Developer Market tool: touch a planted slot, a plant or a fruit with it and the master grows the plant at once
    /// (what it bears comes out ripe) or ripens the fruit. Only the holder's client asks, because only it knows its hand
    /// is on the wand; the master checks developer access. See docs/dev-tools.md.
    /// </summary>
    public class GrowWand : MonoBehaviour
    {
        private const float SAME_TARGET_SECONDS = 1f;   // one touch is one ask, however many colliders it brushes

        private IHeldObject _held;
        private Object _lastTarget;
        private float _lastAskAt = float.NegativeInfinity;

        private void Awake() => _held = GetComponent<IHeldObject>();

        private void OnTriggerEnter(Collider other)
        {
            if (_held == null || string.IsNullOrEmpty(_held.HolderId) || _held.HolderId != PlayerManager.LocalPlayerId) return;

            PlantSlot slot = other.GetComponent<PlantSlot>();
            if (slot != null && slot.Plot != null) { Ask(slot, slot.Plot.NetworkId, slot.Plot.IndexOf(slot), 0); return; }

            Plant plant = other.GetComponentInParent<Plant>();
            if (plant != null) { Ask(plant, 0, -1, plant.NetworkId); return; }

            Produce produce = other.GetComponentInParent<Produce>();
            if (produce != null && !produce.IsRipe) Ask(produce, 0, -1, produce.NetworkId);
        }

        private void Ask(Object target, uint plotId, int slotIndex, uint targetId)
        {
            if (target == _lastTarget && Time.time - _lastAskAt < SAME_TARGET_SECONDS) return;
            _lastTarget = target;
            _lastAskAt = Time.time;
            DeveloperMarket.RequestGrow(plotId, slotIndex, targetId);
        }
    }
}
