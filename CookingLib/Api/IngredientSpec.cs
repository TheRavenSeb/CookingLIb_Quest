using System.Collections.Generic;

namespace CookingLib
{
    /// <summary>What a stew ingredient adds to a stew. For a game food, leave a field null to keep the game's value.</summary>
    public class IngredientSpec
    {
        /// <summary>Unique id written as author.thing.</summary>
        public string Id;

        /// <summary>The item, by the game's item or prefab name or number, copied from the dump. Garnishes are not stew ingredients.</summary>
        public string Item;

        /// <summary>Effects the item adds to a stew. Its ingredient page shows the ones that are not 0, at most 3. Set it from Main.Setup only.</summary>
        public List<EffectSpec> Effects;

        /// <summary>The chunk the item leaves floating in a stew, copied from the dump's Chunks. An empty string means none.</summary>
        public string Chunk;

        /// <summary>The cookable category recipes match the item by, copied from the dump. An empty string means none.</summary>
        public string Category;
    }
}
