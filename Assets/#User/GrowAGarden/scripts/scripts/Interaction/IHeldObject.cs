namespace GrowAGarden
{
    /// <summary>
    /// Something that can be held by one player at a time.
    ///
    /// Exists so grab arbitration can be a component of its own rather than another
    /// responsibility on PlantSeed, which already owns lifecycle, growth, serialization and
    /// planting.
    /// </summary>
    public interface IHeldObject
    {
        /// <summary>Id of the player currently holding this, or null/empty when free.</summary>
        string HolderId { get; }
    }
}
