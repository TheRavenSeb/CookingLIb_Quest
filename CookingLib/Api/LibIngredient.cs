using System.Collections.Generic;
using Il2CppAlta.Alchemy;
using Il2CppAlta.Cooking;
using Il2CppAlta.Inventory;

namespace CookingLib
{
    /// <summary>A change to a stew ingredient. Check Error before using it.</summary>
    public class LibIngredient
    {
        internal LibIngredient(IngredientSpec spec)
        {
            Spec = spec;
        }

        /// <summary>The spec it was made with.</summary>
        public IngredientSpec Spec { get; }

        /// <summary>The id from the spec.</summary>
        public string Id => Spec?.Id;

        /// <summary>Why it was refused, or null.</summary>
        public string Error { get; internal set; }

        /// <summary>True once its changes are in the game: Effects at Setup on both sides, Chunk and Category on the server once the world is up.</summary>
        public bool IsApplied { get; internal set; }

        /// <summary>The game's stew ingredient it changes.</summary>
        public CookableItem Cookable { get; internal set; }

        /// <summary>Recipes a Category change touches, with (drop order decides) on those where the order ingredients go in picks the recipe. Empty until the server applies it.</summary>
        public IReadOnlyList<string> TouchedRecipes => Touched;

        internal List<string> Touched { get; set; } = new List<string>();

        internal Item Item { get; set; }

        internal EffectDefinitionApplication[] Effects { get; set; }

        internal LiquidVisualChunkDefinition Chunk { get; set; }

        internal CookableCategoryDefinition Category { get; set; }

        internal bool PagesApplied { get; set; }

        internal bool WorldApplied { get; set; }

        internal bool HasPages => Spec != null && Spec.Effects != null;

        internal bool HasWorld => Spec != null && (Spec.Chunk != null || Spec.Category != null);
    }
}
