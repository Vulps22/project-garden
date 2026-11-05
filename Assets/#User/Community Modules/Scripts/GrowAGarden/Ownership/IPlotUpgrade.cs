namespace Vulps.GrowAGarden.Ownership
{
    /// <summary>Something standing in a Plot that raises the profit of what the Plot grows (docs/economy.md → Upgrades).</summary>
    public interface IPlotUpgrade
    {
        /// <summary>Which upgrade this is: two of a kind on one Plot count once.</summary>
        string Kind { get; }

        /// <summary>The share of profit it adds right now: 0 until it is grown.</summary>
        float ActiveBonus { get; }
    }
}
