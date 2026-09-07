using Fusion;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// A plant that *is* its crop: a carrot, a turnip. When it finishes growing it is replaced by
    /// its produce and stops existing — there is no plant left standing next to the carrot,
    /// because pulling the carrot is the harvest.
    ///
    /// The produce it leaves behind inherits the plot (see RootedProduce): a carrot in the ground
    /// occupies that ground until somebody pulls it.
    /// </summary>
    public class RootedPlant : Plant
    {
        [Tooltip("What this becomes when it is grown. Must also be listed on SceneNetworking.")]
        [SerializeField] private NetworkObject _producePrefab;

        protected override void OnFullyGrown()
        {
            Logger.Info($"OnFullyGrown() '{gameObject.name}' — grown at {transform.position}; becoming produce");

            Produce produce = SpawnProduce(_producePrefab, transform.position, transform.rotation);
            if (produce == null)
            {
                // Nothing to become. Leave the plant standing rather than freeing a plot that
                // still visibly holds a crop; the next Update will not retry, so this shows up as
                // a grown plant that never yields, which is the honest symptom.
                Logger.Error($"OnFullyGrown() '{gameObject.name}' — produce could not be spawned; plot left occupied");
                return;
            }

            produce.Init(_plot, _slotIndex, System.DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            // The slot passes to the produce rather than being freed — a carrot in the ground
            // occupies that ground until somebody pulls it, so nobody may sow over it. End() must
            // therefore not release the slot on the way out.
            if (_plot != null) _plot.SetOccupant(_slotIndex, produce);
            _plot = null;
            _slotIndex = -1;
            End();
        }
    }
}
