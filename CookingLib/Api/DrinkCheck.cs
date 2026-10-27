namespace CookingLib
{
    /// <summary>Liquid about to land in a player's mouth, for Drinks.CanDrink.</summary>
    public class DrinkCheck
    {
        internal DrinkCheck(Player player, UserInfoAndRole info, LiquidInfo liquid)
        {
            Player = player;
            UserId = info.Identifier;
            UserName = info.Username;
            Liquid = liquid;
        }

        /// <summary>The player about to drink.</summary>
        public Player Player { get; }

        /// <summary>The player's account id from their connection.</summary>
        public int UserId { get; }

        /// <summary>The player's account name from their connection.</summary>
        public string UserName { get; }

        /// <summary>What the player is about to drink.</summary>
        public LiquidInfo Liquid { get; }
    }
}
