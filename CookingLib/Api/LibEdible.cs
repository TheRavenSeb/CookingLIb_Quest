using System.Collections.Generic;
using Il2CppAlta.Alchemy;
using Il2CppAlta.Inventory;

namespace CookingLib
{
    /// <summary>A change to a food. Check Error before using it.</summary>
    public class LibEdible
    {
        internal LibEdible(EdibleSpec spec)
        {
            Spec = spec;
        }

        /// <summary>The spec it was made with.</summary>
        public EdibleSpec Spec { get; }

        /// <summary>The id from the spec.</summary>
        public string Id => Spec?.Id;

        /// <summary>Why it was refused, or null. It can be set when the change applies, if the item turns out not to be a food.</summary>
        public string Error { get; internal set; }

        /// <summary>True once the food gives the new effects.</summary>
        public bool IsReady { get; internal set; }

        /// <summary>True when it changes one of the game's own foods. Always true in CookingLib 0.2.</summary>
        public bool IsVanillaFood => true;

        /// <summary>The food's item.</summary>
        public Item Item { get; internal set; }

        /// <summary>Things worth knowing about this change, such as pages in books that show the old values.</summary>
        public IReadOnlyList<string> Warnings => Notes;

        internal List<string> Notes { get; } = new List<string>();

        internal uint PrefabHash { get; set; }

        internal EffectDefinitionApplication[] Effects { get; set; }
    }
}
