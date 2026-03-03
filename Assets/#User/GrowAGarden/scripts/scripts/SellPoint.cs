using GrowAGarden;
using UnityEngine;

namespace GrowAGarden
{
    public class SellPoint : MonoBehaviour
    {

        /// <summary>
        /// Master Client attempts to sell the plant that enters the sell point. Send money to the player (when economy added) and return the plant to the pool.
        /// </summary>
        /// <param name="other"></param>
        private void OnTriggerEnter(Collider other)
        {
            if (!SceneNetworking.IsMasterClient) return;
            UnifiedPlantSeed plant = other.GetComponent<UnifiedPlantSeed>();
            if (plant == null) return;
            if (plant.IsSeed) return; // Don't sell seeds, only plants
            EconomyManager.Instance.AddBalance(plant.GetGrabber().GetID(), plant.seedDefinition.sellValue);
            plant.ToBeSold();
        }
    }
}