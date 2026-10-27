using Il2CppAlta.Networking;
using UnityEngine;

namespace CookingLib
{
    /// <summary>A copy of one roast, taken when the item cooked or burnt.</summary>
    public class RoastedEvent
    {
        internal RoastedEvent()
        {
        }

        /// <summary>The prefab name of the item that roasted.</summary>
        public string FromName { get; internal set; }

        /// <summary>The prefab number of the item that roasted.</summary>
        public uint FromPrefabHash { get; internal set; }

        /// <summary>What it turned into, or null when it vanished. It may be gone by the time a handler runs.</summary>
        public NetworkEntity Result { get; internal set; }

        /// <summary>The prefab name of what it turned into, or null when it vanished.</summary>
        public string ResultName { get; internal set; }

        /// <summary>True when it burnt, false when it cooked.</summary>
        public bool Burnt { get; internal set; }

        /// <summary>Where the item was.</summary>
        public Vector3 Position { get; internal set; }

        /// <summary>The last player who held the item, or null. Not proof of who lit the fire. It may also be gone by the time a handler runs.</summary>
        public Player LastHolder { get; internal set; }

        /// <summary>LastHolder's account id from their connection, or 0 when nobody held it.</summary>
        public int UserId { get; internal set; }

        /// <summary>LastHolder's account name from their connection, or null when nobody held it.</summary>
        public string UserName { get; internal set; }

        /// <summary>The rule that roasted it, or null when it roasted as in the game.</summary>
        public LibRoasting Rule { get; internal set; }
    }
}
