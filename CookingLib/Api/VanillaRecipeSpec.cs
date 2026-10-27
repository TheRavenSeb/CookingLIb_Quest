using System.Collections.Generic;
using UnityEngine;

namespace CookingLib
{
    /// <summary>Changes to one of the game's own stew recipes. Leave a field null to keep the game's value.</summary>
    public class VanillaRecipeSpec
    {
        /// <summary>Unique id written as author.thing.</summary>
        public string Id;

        /// <summary>The game's stew recipe name or number, copied from the dump.</summary>
        public string Recipe;

        /// <summary>Effects the stew gains on top of its ingredients. Shares the recipe page's 3 effects with EffectModifiers. Set it from Main.Setup only.</summary>
        public List<EffectSpec> ExtraEffects;

        /// <summary>Multipliers on the stew's whole total for an effect, scaled by the stew's stage. Written as given, with no added x1. Set it from Main.Setup only.</summary>
        public List<EffectSpec> EffectModifiers;

        /// <summary>Optional colour in place of the stew's own. Every channel from 0 to 1.</summary>
        public Color? Tint;

        /// <summary>True keeps the recipe's colour on a base liquid that sets ColorsStews.</summary>
        public bool IgnoresDyes;

        /// <summary>Optional registered id or game liquid name the pot holds instead of the stew.</summary>
        public string ResultLiquid;

        /// <summary>Liquids the pot may hold for this recipe: registered ids or game liquid names. Replaces the game's list.</summary>
        public List<string> BaseLiquids;
    }
}
