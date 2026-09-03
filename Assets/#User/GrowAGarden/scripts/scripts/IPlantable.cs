using Fusion;

namespace GrowAGarden
{
    /// <summary>
    /// Something a player can put in the ground.
    ///
    /// A trait rather than "a Seed", deliberately. What a plot needs to know is who is offering,
    /// whose it is, and what should grow — none of which is specific to seeds. Planting a sword to
    /// grow an auto-harvester is then a prefab and an Inspector field rather than a refactor, and
    /// it costs nothing to leave the door open now.
    /// </summary>
    public interface IPlantable
    {
        /// <summary>Fusion id, so a plot can name this in an RPC.</summary>
        uint NetworkId { get; }

        /// <summary>Who owns this, and therefore who will own the plot it goes into.</summary>
        string OwnerId { get; }

        /// <summary>Who is physically holding it. Only the holder may ask to plant.</summary>
        string HolderId { get; }

        /// <summary>What grows when this is planted. Must be registered on SceneNetworking.</summary>
        NetworkObject PlantPrefab { get; }

        /// <summary>True when this is in a state where planting is allowed at all.</summary>
        bool CanBePlanted { get; }

        /// <summary>The master said yes. Runs on every client; take yourself out of the world.</summary>
        void ApplyPlanted();
    }
}
