using System.Collections.Generic;

namespace CookingLib
{
    /// <summary>A change to the effects of one of the game's own liquids, such as a potion.</summary>
    public class VanillaLiquidSpec
    {
        /// <summary>Unique id written as author.thing.</summary>
        public string Id;

        /// <summary>The game's liquid, by name or number, copied from the dump.</summary>
        public string Liquid;

        /// <summary>Replaces the liquid's effects, Feed apart: Feed stays the game's and is kept. An empty list gives none.</summary>
        public List<EffectSpec> Effects;

        /// <summary>Must be true to change Water, which every vanilla stew, river and pond uses.</summary>
        public bool AllowWater;
    }
}
