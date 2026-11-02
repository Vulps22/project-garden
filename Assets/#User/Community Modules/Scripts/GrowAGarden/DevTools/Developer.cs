using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.DevTools
{
    /// <summary>Who may use the debug tools: Vulps's Somnium account, and nobody else.</summary>
    public static class Developer
    {
        /// <summary>Vulps22's Somnium user id, as Somnium's player container reports it.</summary>
        public const string ID = "effa171e-b6a2-43c2-8b71-ee1a312e706d";

        /// <summary>The local player is the developer.</summary>
        public static bool IsLocal => PlayerManager.LocalPlayerId == ID;

        /// <summary>The local player is the developer and the master client.</summary>
        public static bool IsLocalMaster => IsLocal && PlayerManager.IsMaster;
    }
}
