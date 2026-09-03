namespace GrowAGarden
{
    /// <summary>
    /// Declares what something is worth right now.
    ///
    /// Exists so <see cref="SellableEntity"/> can ask the one thing that knows, instead of
    /// carrying a copy that goes stale — the same reason <see cref="IKinematicSource"/> exists.
    /// A crop answers from its definition; a prop with no definition does not implement this at
    /// all and the trait falls back to the number on its own Inspector.
    /// </summary>
    public interface ISellValueSource
    {
        int SellValue { get; }
    }
}
