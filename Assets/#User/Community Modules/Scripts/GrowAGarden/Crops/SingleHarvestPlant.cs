namespace Vulps.GrowAGarden.Crops
{
    /// <summary>
    /// Bears once, and withers when every socket has been harvested its plant's HarvestCount times:
    /// a pumpkin, weed. It never bears again by itself; for regrowing fruit see MultiHarvestPlant.
    ///
    /// Named for the rule rather than for botany: the question the game asks is *after this plant
    /// yields, does it end or continue?*
    /// </summary>
    public class SingleHarvestPlant : BearingPlant
    {
        protected override void OnProduceHarvested(ProduceSlot slot)
        {
            if (AllSlotsSpent()) BeginWithering();
        }
    }
}
