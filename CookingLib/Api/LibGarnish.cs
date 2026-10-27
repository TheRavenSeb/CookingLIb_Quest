using Il2CppAlta.Alchemy;
using Il2CppAlta.Cooking;
using Il2CppAlta.Inventory;

namespace CookingLib
{
    /// <summary>A change to a garnish. Check Error before using it.</summary>
    public class LibGarnish
    {
        internal LibGarnish(GarnishSpec spec)
        {
            Spec = spec;
        }

        /// <summary>The spec it was made with.</summary>
        public GarnishSpec Spec { get; }

        /// <summary>The id from the spec.</summary>
        public string Id => Spec?.Id;

        /// <summary>Why it was refused, or null.</summary>
        public string Error { get; internal set; }

        /// <summary>True once its changes are in the game: Effects at Setup on both sides, Multipliers and Chunk on the server once the world is up.</summary>
        public bool IsApplied { get; internal set; }

        /// <summary>The game's garnish it changes.</summary>
        public CookingGarnish Garnish { get; internal set; }

        internal Item Item { get; set; }

        internal EffectDefinitionApplication[] Effects { get; set; }

        internal EffectDefinitionApplication[] Multipliers { get; set; }

        internal LiquidVisualChunkDefinition Chunk { get; set; }

        internal bool PagesApplied { get; set; }

        internal bool WorldApplied { get; set; }

        internal bool HasPages => Spec != null && Spec.Effects != null;

        internal bool HasWorld => Spec != null && (Spec.Multipliers != null || Spec.Chunk != null);
    }
}
