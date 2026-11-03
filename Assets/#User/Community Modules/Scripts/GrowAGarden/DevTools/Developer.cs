using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.DevTools
{
    /// <summary>
    /// Who may use the debug tools: Vulps's Somnium account, the owner, always; and anyone the owner grants access to for
    /// this session (<see cref="DeveloperAccess"/>).
    /// </summary>
    public static class Developer
    {
        /// <summary>Vulps22's Somnium user id, as Somnium's player container reports it.</summary>
        public const string ID = "effa171e-b6a2-43c2-8b71-ee1a312e706d";

        /// <summary>A player who may use the debug tools: the owner, or someone granted them this session.</summary>
        public static bool Has(string playerId) => !string.IsNullOrEmpty(playerId) && (playerId == ID || DeveloperAccess.IsGranted(playerId));

        /// <summary>The local player may use the debug tools.</summary>
        public static bool IsLocal => Has(PlayerManager.LocalPlayerId);

        /// <summary>The local player is the owner, Vulps: the only one who grants access.</summary>
        public static bool IsLocalOwner => PlayerManager.LocalPlayerId == ID;

        /// <summary>The local player is the owner and the master client.</summary>
        public static bool IsLocalMaster => IsLocalOwner && PlayerManager.IsMaster;
    }
}
