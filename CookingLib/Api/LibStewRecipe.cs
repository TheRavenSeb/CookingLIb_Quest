using Il2CppAlta.Cooking;

namespace CookingLib
{
    /// <summary>A stew recipe a mod registered. Check Error before using it.</summary>
    public class LibStewRecipe
    {
        internal LibStewRecipe(StewRecipeSpec spec, bool isShared = false, uint number = 0)
        {
            Spec = spec;
            IsShared = isShared;
            Number = number;
        }

        /// <summary>The spec it was registered with.</summary>
        public StewRecipeSpec Spec { get; }

        /// <summary>The id from the spec.</summary>
        public string Id => Spec?.Id;

        /// <summary>Why it was refused, or null.</summary>
        public string Error { get; internal set; }

        /// <summary>The game's recipe. A shared recipe's is built at Setup on both sides, a server only one's on the server once the world is up.</summary>
        public StewRecipe Recipe { get; internal set; }

        /// <summary>True for a recipe from RegisterShared.</summary>
        public bool IsShared { get; }

        /// <summary>The number RegisterShared took, 0 for a server only recipe.</summary>
        public uint Number { get; }

        /// <summary>True when it has a journal page. On the server that also needs SharedRecipePages on. A shared recipe whose cooking was refused keeps its page.</summary>
        public bool HasPage => PageBuilt && (!Main.IsServer || StewRecipes.SharedPages);

        internal bool PageBuilt { get; set; }

        internal LiquidRef Result { get; set; }
    }
}
