using System.Collections.Generic;

namespace CookingLib
{
    /// <summary>One ingredient slot of a stew recipe: some items, or one vanilla category.</summary>
    public class SlotSpec
    {
        /// <summary>Items that fit this slot, by the game's item or prefab names, copied from the dump.</summary>
        public List<string> Items = new List<string>();

        /// <summary>A vanilla cookable category instead of Items, copied from the dump.</summary>
        public string Category;

        /// <summary>A game glyph from the dump the recipe page shows for an Items slot. Null shows the first item's own. A Category slot always shows its category's.</summary>
        public string Glyph;
    }
}
