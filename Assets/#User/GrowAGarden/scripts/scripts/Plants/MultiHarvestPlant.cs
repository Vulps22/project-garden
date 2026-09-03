using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// Yields again and again: an apple tree, a grape vine, a blueberry bush. (#43)
    ///
    /// Written but unused — no crop is configured for it yet, so it rides along untested. Before a
    /// crop uses it, note the open problem: nothing ends a multi-harvest plant. With plants
    /// ungrabbable there is no uproot gesture at all, so it holds its plot until its owner leaves
    /// and the garden lease expires. That is an answer, but a thin one.
    /// </summary>
    public class MultiHarvestPlant : BearingPlant
    {
        [Tooltip("Seconds before an emptied socket bears again. Zero makes the plant a vending " +
                 "machine, which reads badly; a pause is usually what you want.")]
        [SerializeField] private float _regrowDelay = 30f;

        private float _bearAgainAt = -1f;

        protected override void OnProduceHarvested(ProduceSlot slot)
        {
            _bearAgainAt = Time.time + _regrowDelay;
        }

        private void LateUpdate()
        {
            if (_bearAgainAt < 0f || Time.time < _bearAgainAt) return;
            if (!SceneNetworking.IsMasterClient) return;

            _bearAgainAt = -1f;
            Bear();
        }
    }
}
