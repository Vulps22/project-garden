namespace GrowAGarden
{
    /// <summary>
    /// Yields once per socket and then dies: a pumpkin, a pea, a tomato.
    ///
    /// Named for the rule rather than for botany. "Vining" is a growth habit and says nothing about
    /// lifespan — it spans annuals and perennials alike — and no botanical term matches the question
    /// the game actually asks, which is *after this plant yields, does it end or continue?*
    ///
    /// A pumpkin has one socket and so withers on its first harvest. A tomato with four withers on
    /// the fourth, which is the same rule and needs no second class.
    /// </summary>
    public class SingleHarvestPlant : BearingPlant
    {
        protected override void OnProduceHarvested(ProduceSlot slot)
        {
            if (AllSlotsHarvested()) BeginWithering();
        }
    }
}
