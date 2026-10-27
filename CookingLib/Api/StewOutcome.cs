namespace CookingLib
{
    /// <summary>What a pot's stew turned out as.</summary>
    public enum StewOutcome
    {
        /// <summary>The ingredients matched a recipe.</summary>
        Recipe,

        /// <summary>The pot checked its ingredients and no recipe matched.</summary>
        NoRecipe,

        /// <summary>The stew cooked or burnt before the pot had enough ingredients to check.</summary>
        Unfinished
    }
}
