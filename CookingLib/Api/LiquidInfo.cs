using System.Collections.Generic;
using Il2CppAlta.Liquid;

namespace CookingLib
{
    /// <summary>What a liquid is, from Liquids.Identify.</summary>
    public class LiquidInfo
    {
        internal LiquidInfo()
        {
        }

        /// <summary>The kind of liquid.</summary>
        public LiquidKind Kind { get; internal set; }

        /// <summary>The game liquid's name, the CookingLib liquid's id, or the stew's recipe id or name. Null when not known, such as a stew with no recipe or one that went through a save.</summary>
        public string Name { get; internal set; }

        /// <summary>The name of the liquid's look.</summary>
        public string Look { get; internal set; }

        /// <summary>The CookingLib liquid for Shared and ServerOnly, else null.</summary>
        public LibLiquid Liquid { get; internal set; }

        /// <summary>The CookingLib recipe that made a stew, else null. Lost when the stew goes through a save.</summary>
        public LibStewRecipe Recipe { get; internal set; }

        /// <summary>The stew's prefab name, which names its stage, for example Blueberry Recipe Raw Stew. Null when not known.</summary>
        public string Stage { get; internal set; }

        /// <summary>The stew's ingredients in the order they went in. Empty when not known.</summary>
        public IReadOnlyList<string> Ingredients { get; internal set; }

        /// <summary>Thirst from the CookingLib liquid's spec for Shared and ServerOnly, else null. Null also means the thirst mod decides.</summary>
        public float? Thirst => (Kind == LiquidKind.Shared || Kind == LiquidKind.ServerOnly) && Liquid != null ? Liquid.Spec.Thirst : null;

        /// <summary>The liquid data that was identified.</summary>
        public LiquidData Data { get; internal set; }
    }
}
