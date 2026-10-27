using System.Collections.Generic;

namespace CookingLib
{
    /// <summary>What a food gives when eaten. CookingLib 0.2 changes the game's own foods only.</summary>
    public class EdibleSpec
    {
        /// <summary>Unique id written as author.thing.</summary>
        public string Id;

        /// <summary>The food, by the game's item or prefab name or number, copied from the dump. Each stage is its own food.</summary>
        public string Item;

        /// <summary>Replaces what the food gives, Feed apart: Feed stays the game's and is kept. Its food page shows the ones that are not 0, Feed included, at most 3.</summary>
        public List<EffectSpec> Effects = new List<EffectSpec>();

        /// <summary>For a later CookingLib. Leave it 0.</summary>
        public int Bites;

        /// <summary>For a later CookingLib. Leave it 0.</summary>
        public float BiteSeconds;

        /// <summary>For a later CookingLib. Ignored.</summary>
        public string LikeFood = "Apple Full Ripe";
    }
}
