namespace GrowAGarden
{
    /// <summary>
    /// Something standing in a plot, which can be removed from it.
    ///
    /// Two unrelated classes occupy plots: a Plant while it grows, and a RootedProduce once the
    /// plant has become the crop. The plot does not care which — it only needs to be able to clear
    /// the ground when a garden lease expires. An interface rather than a common base class,
    /// because a plant and a produce have nothing else in common.
    /// </summary>
    public interface IPlotOccupant
    {
        /// <summary>Remove this from the world. Master only; despawning needs state authority.</summary>
        void Uproot();
    }
}
