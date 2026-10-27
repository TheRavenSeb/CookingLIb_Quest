using System.Collections.Generic;

namespace CookingLib
{
    /// <summary>What to change about every cooking pot. Leave a field null to keep the game's value.</summary>
    public class PotRulesSpec
    {
        /// <summary>Unique id, written as author.thing.</summary>
        public string Id;

        /// <summary>How many ingredients lock a pot and pick its stew, 1 to 16. The game uses 4.</summary>
        public int? Ingredients;

        /// <summary>How many garnishes a stew takes, 0 to 16. The game uses 3.</summary>
        public int? Garnishes;

        /// <summary>How hot the water and each ingredient must be before it stews, above 23 and below 100. The game uses 80.</summary>
        public float? StewingTemperature;

        /// <summary>Game effects every stew gets, replacing the game's Feed 3. An empty list gives none.</summary>
        public List<EffectSpec> GlobalEffects;

        /// <summary>The stew a pot locks on when its ingredients match no recipe: a Raw stew prefab from the dump, not the default stew. The game uses Non Recipe Raw Stew.</summary>
        public string NoRecipeStew;

        /// <summary>How hard a locked pot pushes an ingredient out, 0 to 1000. The game uses 300. At 0 no push is applied.</summary>
        public float? RejectionForce;
    }
}
