using Il2CppAlta.Alchemy;
using Il2CppAlta.Networking;

namespace CookingLib
{
    /// <summary>Pot rules a mod set. Check Error before using it.</summary>
    public class LibPotRules
    {
        internal LibPotRules(PotRulesSpec spec)
        {
            Spec = spec;
        }

        /// <summary>The spec it was set with.</summary>
        public PotRulesSpec Spec { get; }

        /// <summary>The id from the spec.</summary>
        public string Id => Spec?.Id;

        /// <summary>Why it was refused, or null. It can be set once the world is up, if NoRecipeStew resolves to nothing.</summary>
        public string Error { get; internal set; }

        /// <summary>True on the server once the rules are in the game's pot settings.</summary>
        public bool IsApplied { get; internal set; }

        internal EffectDefinitionApplication[] GlobalEffects { get; set; }

        internal NetworkPrefab NoRecipeStew { get; set; }
    }
}
