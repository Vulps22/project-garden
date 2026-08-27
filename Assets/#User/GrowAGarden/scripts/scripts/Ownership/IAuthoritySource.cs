namespace GrowAGarden
{
    /// <summary>
    /// Declares whether the master client should own this object right now.
    ///
    /// Fusion transfers state authority to whoever grabs an object and never transfers it back,
    /// so unsold stock ends up owned by whichever player last touched it. That is why
    /// UnifiedPool has to beg for authority with a 20 second timeout on every sale, and why a
    /// rejected purchase leaves a shop seed owned by the player who could not afford it.
    /// </summary>
    public interface IAuthoritySource
    {
        /// <summary>True when this belongs to the world rather than to a player.</summary>
        bool ShouldMasterOwn { get; }
    }
}
