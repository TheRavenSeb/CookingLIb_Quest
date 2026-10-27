using UnityEngine;

namespace CookingLib
{
    /// <summary>Changes to one of the game's stew prefabs, one stage of one stew. Leave a field null to keep the game's value. Server only.</summary>
    public class StewSpec
    {
        /// <summary>Unique id written as author.thing.</summary>
        public string Id;

        /// <summary>The stew prefab name or number, copied from the dump's stews, such as Pumpkin Recipe Cooked Stew.</summary>
        public string Stew;

        /// <summary>Multiplier on what the ingredients add, 0 to 10. The game uses 1.</summary>
        public float? Ingredients;

        /// <summary>Multiplier on what the base liquid adds, 0 to 10. The game uses 1.</summary>
        public float? BaseLiquid;

        /// <summary>Multiplier on the recipe's extra effects and its modifiers, 0 to 10. The game's own stages use it to scale a recipe by stage.</summary>
        public float? Extras;

        /// <summary>The stew's colour, every channel 0 to 1. A recipe Tint and a liquid that colours stews still win.</summary>
        public Color? Tint;
    }
}
