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
            PlantSeed plant = other.GetComponent<PlantSeed>();
            if (plant == null) return;
            if (plant.IsSeed) return; // Don't sell seeds, only plants

            // Master only. A null grabber means we do not know who to pay, so abandon the
            // sale rather than consuming the plant for nothing — it stays grabbable and the
            // player can drop it in again once the grabber RPC has landed.
            PlayerBalance seller = plant.GetGrabber();
            if (seller == null)
            {
                Logger.Warn($"OnTriggerEnter() '{gameObject.name}' — plant '{plant.name}' has no known grabber, sale abandoned");
                return;
            }

            EconomyManager.Instance.AddBalance(seller.GetID(), plant.seedDefinition.sellValue);
            plant.Sell();
        }
    }
}