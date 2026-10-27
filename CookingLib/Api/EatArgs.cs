using System.Collections.Generic;
using Il2CppAlta.Alchemy;
using Il2CppAlta.Inventory;

namespace CookingLib
{
    /// <summary>A copy of one finished meal, taken when it happened.</summary>
    public class EatArgs
    {
        readonly Player player;

        internal EatArgs(Player player, UserInfoAndRole info)
        {
            this.player = player;
            if (info == null)
                return;

            UserId = info.Identifier;
            UserName = info.Username;
            Linked = true;
        }

        /// <summary>The player who ate, or null when they left before the handler ran.</summary>
        public Player Player => player == null ? null : player;

        /// <summary>The player's account id from their connection. Always set.</summary>
        public int UserId { get; }

        /// <summary>The player's account name from their connection. Always set.</summary>
        public string UserName { get; }

        /// <summary>The item that was eaten, or null when it has none. The food itself is gone by now.</summary>
        public Item Item { get; internal set; }

        /// <summary>Copies of the effects the game applied.</summary>
        public IReadOnlyList<EffectDefinitionApplication> Effects { get; internal set; }

        /// <summary>The CookingLib change in place on this food, or null when it gave the game's values.</summary>
        public LibEdible Edible { get; internal set; }

        internal bool Linked { get; }
    }
}
