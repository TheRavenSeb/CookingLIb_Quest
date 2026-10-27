namespace CookingLib
{
    /// <summary>The player who last held one ingredient of a stew.</summary>
    public class StewCook
    {
        internal StewCook(Player player)
        {
            if (player == null)
                return;

            Player = player;
            UserInfoAndRole info = player.ConnectionToRemotePlayer?.UserInfo;
            if (info != null)
            {
                UserId = info.Identifier;
                UserName = info.Username;
            }
        }

        /// <summary>The player, or null when nobody held the item or the player had left. It may also be gone by the time a handler runs.</summary>
        public Player Player { get; }

        /// <summary>The player's account id from their connection, or 0 for an empty entry.</summary>
        public int UserId { get; }

        /// <summary>The player's account name from their connection, or null for an empty entry.</summary>
        public string UserName { get; }
    }
}
