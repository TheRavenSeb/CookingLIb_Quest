using System.Collections.Generic;

namespace CookingLib
{
    /// <summary>Changes to one of the game's garnishes, Salt or a Garlic stage. Leave a field null to keep the game's value.</summary>
    public class GarnishSpec
    {
        /// <summary>Unique id written as author.thing.</summary>
        public string Id;

        /// <summary>The garnish, by the game's item or prefab name or number, copied from the dump. Stew ingredients are not garnishes.</summary>
        public string Item;

        /// <summary>Effects the garnish adds to a stew. Its ingredient page shows the ones that are not 0, at most 3. Set it from Main.Setup only.</summary>
        public List<EffectSpec> Effects;

        /// <summary>Multipliers on effects already in the stew, Strength 0 to 10 each. An effect the stew lacks stays absent. Server only.</summary>
        public List<EffectSpec> Multipliers;

        /// <summary>The chunk the garnish leaves floating in a stew, copied from the dump's Chunks. An empty string means none. Server only.</summary>
        public string Chunk;
    }
}
