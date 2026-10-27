using System.Collections.Generic;
using Il2CppAlta.Alchemy;
using Il2CppAlta.Cooking;
using Il2CppAlta.Liquid;

namespace CookingLib
{
    /// <summary>A change to one of the game's stew recipes, or a recipe turned off. Check Error before using it.</summary>
    public class LibVanillaRecipe
    {
        internal LibVanillaRecipe(VanillaRecipeSpec spec, bool isDisable)
        {
            Spec = spec;
            IsDisable = isDisable;
        }

        /// <summary>The spec it was made with. For a recipe turned off, only Id and Recipe are set.</summary>
        public VanillaRecipeSpec Spec { get; }

        /// <summary>The id from the spec.</summary>
        public string Id => Spec?.Id;

        /// <summary>Why it was refused, or null.</summary>
        public string Error { get; internal set; }

        /// <summary>True when it came from VanillaRecipes.Disable.</summary>
        public bool IsDisable { get; }

        /// <summary>True once its changes are in the game: page fields at Setup on both sides, the rest on the server once the world is up.</summary>
        public bool IsApplied { get; internal set; }

        /// <summary>The game's recipe it changes.</summary>
        public StewRecipe Recipe { get; internal set; }

        internal EffectDefinitionApplication[] Extras { get; set; }

        internal EffectDefinitionApplication[] Modifiers { get; set; }

        internal bool PagesApplied { get; set; }

        internal bool WorldApplied { get; set; }

        internal LiquidRef Result { get; set; }

        internal List<LiquidDefinition> Bases { get; set; }

        internal bool HasPages => !IsDisable && Spec != null && (Spec.ExtraEffects != null || Spec.EffectModifiers != null);

        internal bool HasWorld => IsDisable || Spec != null && (Spec.Tint.HasValue || Spec.IgnoresDyes || !string.IsNullOrEmpty(Spec.ResultLiquid) || Spec.BaseLiquids != null);
    }
}
