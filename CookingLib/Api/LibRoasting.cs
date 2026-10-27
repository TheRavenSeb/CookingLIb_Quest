using System.Collections.Generic;
using Il2CppAlta.Cooking;
using Il2CppAlta.Networking;

namespace CookingLib
{
    /// <summary>A roasting rule a mod registered. Check Error before using it.</summary>
    public class LibRoasting
    {
        internal LibRoasting(RoastingSpec spec)
        {
            Spec = spec;
        }

        /// <summary>The spec it was registered with.</summary>
        public RoastingSpec Spec { get; }

        /// <summary>The id from the spec.</summary>
        public string Id => Spec?.Id;

        /// <summary>Why it was refused, or null. It is also set when a swap into its result failed; the item then roasts as in the game.</summary>
        public string Error { get; internal set; }

        /// <summary>True on the server once the world is up and every name resolved.</summary>
        public bool IsReady => Error == null && Prefab != null;

        /// <summary>How many times an item roasted under this rule cooked.</summary>
        public int TimesCooked { get; internal set; }

        /// <summary>How many times an item roasted under this rule burnt.</summary>
        public int TimesBurnt { get; internal set; }

        internal NetworkPrefab Prefab { get; set; }

        internal NetworkPrefab Cooked { get; set; }

        internal NetworkPrefab Burnt { get; set; }

        internal bool SetsCooked { get; set; }

        internal bool SetsBurnt { get; set; }

        internal NetworkPrefab GameCooked { get; set; }

        internal NetworkPrefab GameBurnt { get; set; }

        internal CookingTemperatures Temperatures { get; set; }

        internal HashSet<int> Held { get; } = new HashSet<int>();

        internal bool Sets(bool burnt) => burnt ? SetsBurnt : SetsCooked;

        internal NetworkPrefab Version(bool burnt)
        {
            if (Error == null && Sets(burnt))
                return burnt ? Burnt : Cooked;
            return burnt ? GameBurnt : GameCooked;
        }
    }
}
