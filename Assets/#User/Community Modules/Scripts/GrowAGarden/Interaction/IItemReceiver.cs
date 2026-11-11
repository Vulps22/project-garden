using UnityEngine;

namespace Vulps.GrowAGarden.Interaction
{
    /// <summary>
    /// A place a held item is put into: the sell point, the hut's claim zone, a tutorial zone. A VR hand carries the item
    /// into its trigger; a 2D player presses F at it, and the item is handed over the same way (docs/2d-players.md).
    /// </summary>
    public interface IItemReceiver
    {
        /// <summary>What putting this item here does, for the prompt ("Sell", "Claim"); null if it takes nothing like it.</summary>
        string VerbFor(GameObject item);

        /// <summary>Takes a held item, as if a hand had carried it in. Called by the holder's own client.</summary>
        void Receive(GameObject item);

        /// <summary>Where the item is seen to go as it is handed over.</summary>
        Vector3 ReceivePoint { get; }
    }
}
