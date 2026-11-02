using UnityEngine;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.Crops
{
    /// <summary>
    /// Bears again after each harvest, for a season: a tomato, an eggplant. Each socket fruits its
    /// plant's HarvestCount times, then the plant withers and frees its slot, which is the ending
    /// #43 was missing.
    /// </summary>
    public class MultiHarvestPlant : BearingPlant
    {
        [Tooltip("Seconds before an emptied socket bears again. Zero makes the plant a vending " +
                 "machine, which reads badly; a pause is usually what you want.")]
        [SerializeField] private float _regrowDelay = 30f;

        private float _bearAgainAt = -1f;

        /// <summary>A season, not forever: once every socket has borne its plant's HarvestCount times, it withers.</summary>
        protected override void OnProduceHarvested(ProduceSlot slot)
        {
            if (AllSlotsSpent())
            {
                _bearAgainAt = -1f;
                BeginWithering();
                return;
            }
            _bearAgainAt = Time.time + _regrowDelay;
        }

        private void LateUpdate()
        {
            if (_bearAgainAt < 0f || Time.time < _bearAgainAt) return;
            if (!PlayerManager.IsMaster) return;

            _bearAgainAt = -1f;
            Bear();
        }
    }
}
