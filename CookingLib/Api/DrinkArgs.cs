using Il2CppAlta.Liquid;

namespace CookingLib
{
    /// <summary>A copy of one drink, taken when it happened.</summary>
    public class DrinkArgs
    {
        readonly Player player;

        internal DrinkArgs(Player player, UserInfoAndRole info)
        {
            this.player = player;
            if (info == null)
                return;

            UserId = info.Identifier;
            UserName = info.Username;
            Linked = true;
        }

        /// <summary>The player who drank, or null when they left before the handler ran.</summary>
        public Player Player => player == null ? null : player;

        /// <summary>The player's account id from their connection. Always set.</summary>
        public int UserId { get; }

        /// <summary>The player's account name from their connection. Always set.</summary>
        public string UserName { get; }

        /// <summary>How much liquid landed in the mouth: one per pour or sip, more for a dunk. The game applies the effects once either way.</summary>
        public int Units { get; internal set; }

        /// <summary>How much liquid the mouth holds.</summary>
        public int MouthSize { get; internal set; }

        /// <summary>What the player drank.</summary>
        public LiquidInfo Liquid { get; internal set; }

        internal LiquidData Taken { get; set; }

        internal bool Linked { get; }
    }
}
