using System.Collections.Generic;

namespace CookingLib
{
    /// <summary>An item dropped into a liquid turns that liquid into another one.</summary>
    public class InfusionSpec
    {
        /// <summary>Unique id written as author.thing.</summary>
        public string Id;

        /// <summary>The item to drop in, by the game's item or prefab name, copied from the dump.</summary>
        public string Item;

        /// <summary>Liquids it works in: registered ids or vanilla liquid names. Assign a new list to replace the default Water.</summary>
        public List<string> InputLiquids = new List<string> { "Water" };

        /// <summary>The registered id or vanilla liquid name the container ends up holding.</summary>
        public string OutputLiquid;

        /// <summary>True destroys the item once it has worked.</summary>
        public bool ConsumeItem = true;
    }
}
