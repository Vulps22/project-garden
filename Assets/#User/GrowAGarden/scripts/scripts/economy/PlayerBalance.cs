using SomniumSpace.Bridge.Player;

namespace GrowAGarden
{
    public class PlayerBalance
    {
        private readonly string _id;
        private readonly string _name;
        private int _balance;

        /// <summary>
        /// Creates a new PlayerBalance from a live ISomniumPlayer. Used by master client on player join.
        /// </summary>
        /// <param name="player">The Somnium platform player object.</param>
        /// <param name="startingBalance">The initial Thatch balance.</param>
        public PlayerBalance(ISomniumPlayer player, int startingBalance)
        {
            _id = player.Properties.Id;
            _name = player.Properties.NickName;
            _balance = startingBalance;
        }

        /// <summary>
        /// Reconstructs a PlayerBalance from sync data. Used by all clients on balance broadcast receive.
        /// </summary>
        /// <param name="id">The player's platform ID.</param>
        /// <param name="name">The player's display name.</param>
        /// <param name="balance">The current Thatch balance.</param>
        public PlayerBalance(string id, string name, int balance)
        {
            _id = id;
            _name = name;
            _balance = balance;
        }

        /// <summary>Returns the player's platform ID.</summary>
        public string GetID() => _id;

        /// <summary>Returns the player's display name.</summary>
        public string GetPlayerName() => _name;

        /// <summary>Returns the player's current Thatch balance.</summary>
        public int GetBalance() => _balance;

        /// <summary>
        /// Adds Thatch to the player's balance. Master client only.
        /// </summary>
        /// <param name="amount">The amount of Thatch to add.</param>
        public void AddBalance(int amount)
        {
            if (!SceneNetworking.IsMasterClient) return;
            _balance += amount;
        }

        /// <summary>
        /// Deducts Thatch from the player's balance. Master client only.
        /// </summary>
        /// <param name="amount">The amount of Thatch to deduct.</param>
        public void RemoveBalance(int amount)
        {
            if (!SceneNetworking.IsMasterClient) return;
            _balance -= amount;
        }
    }
}
