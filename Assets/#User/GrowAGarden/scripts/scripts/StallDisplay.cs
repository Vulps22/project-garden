using TMPro;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// The stall's sign. Shows what is actually on the shelf.
    ///
    /// It was three prefabs' worth of hardcoded text until stock became dynamic, and it had
    /// already drifted: BuyPointTurnip's sign read "S: 28" while TurnipSeed.Init() said 25. A sign
    /// authored by hand is a second copy of the balance numbers, and second copies go stale.
    ///
    /// **Driven by the stock, not by the decision.** The obvious wiring is to update the sign when
    /// the master rolls a new crop, and it would be wrong — Offer() runs on the master alone, so
    /// every other client would keep the old sign over the new stock. The seed arriving in the
    /// socket is the announcement, on every client, so the sign follows the seed. That is the same
    /// rule the roll itself obeys, one level down.
    ///
    /// Deliberately not cleared when the socket empties: a restock is a frame or two away and
    /// blanking the sign in between reads as a fault rather than as a gap.
    /// </summary>
    public class StallDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _buyPrice;
        [SerializeField] private TMP_Text _sellValue;

        public void Show(SeedDefinition definition)
        {
            if (definition == null) return;

            if (_name != null) _name.text = definition.displayName;
            if (_buyPrice != null) _buyPrice.text = $"B: {definition.buyPrice}";
            if (_sellValue != null) _sellValue.text = $"S: {definition.sellValue}";
        }

        private void OnValidate()
        {
            // Found by name because the text objects live inside the nested stall visual, which is
            // shared by every stall and should not have to know it is being read.
            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            {
                if (_name == null && text.gameObject.name == "Name") _name = text;
                if (_buyPrice == null && text.gameObject.name == "PriceBuy") _buyPrice = text;
                if (_sellValue == null && text.gameObject.name == "PriceSell") _sellValue = text;
            }
        }
    }
}
