namespace GrowAGarden
{
    /// <summary>
    /// Who a player is, in this game's own words: an id and a display name.
    ///
    /// A snapshot taken at the moment it was asked for, not a handle on Somnium's live player
    /// object. That is the point of it — almost everything that used to hold an ISomniumPlayer
    /// only ever read an id off it, and holding the SDK's type to do that put the SDK's name in
    /// twelve files.
    ///
    /// A departed player's identity is cleared by <see cref="PlayerManager.PlayerLeft"/>, not by
    /// a field emptying out underneath whoever kept it. That was already true — the event exists
    /// because SingleHolderFilter would otherwise refuse an object to everyone for the rest of the
    /// session — so nothing depended on the live object going hollow.
    /// </summary>
    public readonly struct PlayerIdentity
    {
        /// <summary>Nobody: what a lookup returns for an id this client has never seen.</summary>
        public static readonly PlayerIdentity None = default;

        public string Id { get; }
        public string Name { get; }

        /// <summary>
        /// Whether this names anyone. Ask this rather than comparing against null — a struct is
        /// never null, so a null check on one silently always passes.
        /// </summary>
        public bool Exists => !string.IsNullOrEmpty(Id);

        public PlayerIdentity(string id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
