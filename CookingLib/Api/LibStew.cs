using System.Collections.Generic;
using Il2CppAlta.Networking;

namespace CookingLib
{
    /// <summary>A change to a stew prefab. Check Error before using it.</summary>
    public class LibStew
    {
        internal LibStew(StewSpec spec)
        {
            Spec = spec;
        }

        /// <summary>The spec it was made with.</summary>
        public StewSpec Spec { get; }

        /// <summary>The id from the spec.</summary>
        public string Id => Spec?.Id;

        /// <summary>Why it was refused, or null. It can be set once the world is up, if the name resolves to nothing or another mod holds the field.</summary>
        public string Error { get; internal set; }

        /// <summary>True on the server once the prefab, the stews in pots and reloading pots carry the change.</summary>
        public bool IsApplied { get; internal set; }

        /// <summary>The game's stew prefab it changes. Null until the server resolves it.</summary>
        public NetworkPrefab Prefab { get; internal set; }

        /// <summary>Things worth knowing about this change, such as recipe modifiers an Extras change also scales.</summary>
        public IReadOnlyList<string> Warnings => Notes;

        internal List<string> Notes { get; } = new List<string>();

        internal StewStages.Entry Entry { get; set; }
    }
}
