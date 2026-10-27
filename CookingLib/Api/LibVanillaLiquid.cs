using System.Collections.Generic;
using Il2CppAlta.Alchemy;
using Il2CppAlta.Liquid;

namespace CookingLib
{
    /// <summary>A change to one of the game's liquids. Check Error before using it.</summary>
    public class LibVanillaLiquid
    {
        internal LibVanillaLiquid(VanillaLiquidSpec spec)
        {
            Spec = spec;
        }

        /// <summary>The spec it was made with.</summary>
        public VanillaLiquidSpec Spec { get; }

        /// <summary>The id from the spec.</summary>
        public string Id => Spec?.Id;

        /// <summary>Why it was refused, or null.</summary>
        public string Error { get; internal set; }

        /// <summary>True once the liquid gives the new effects, right after Main.PostSetup.</summary>
        public bool IsApplied { get; internal set; }

        /// <summary>The game's liquid it changes.</summary>
        public LiquidDefinition Definition { get; internal set; }

        /// <summary>Things worth knowing about this change, such as a Water change reaching every stew.</summary>
        public IReadOnlyList<string> Warnings => Notes;

        internal List<string> Notes { get; } = new List<string>();

        internal EffectDefinitionApplication[] Effects { get; set; }
    }
}
