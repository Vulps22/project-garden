namespace Vulps.GrowAGarden.Ownership
{
    /// <summary>Who should simulate an object right now.</summary>
    public enum AuthorityRule
    {
        /// <summary>The master, wherever it is. For things with no physics, such as plants.</summary>
        Master,

        /// <summary>A player in the sector the object is in, who has the ground under it loaded.</summary>
        Sector,

        /// <summary>Whoever has it now. A held object stays with the hand holding it.</summary>
        Holder,
    }

    /// <summary>Declares which client should hold state authority over this object right now.</summary>
    public interface IAuthoritySource
    {
        /// <summary>The rule this object's authority follows in its current state.</summary>
        AuthorityRule Authority { get; }
    }
}
