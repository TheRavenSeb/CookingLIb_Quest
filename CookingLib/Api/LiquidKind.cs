namespace CookingLib
{
    /// <summary>What kind of liquid Liquids.Identify found.</summary>
    public enum LiquidKind
    {
        /// <summary>One of the game's own liquids, such as Water or a potion.</summary>
        Vanilla,

        /// <summary>A shared CookingLib liquid.</summary>
        Shared,

        /// <summary>A server only CookingLib liquid.</summary>
        ServerOnly,

        /// <summary>A stew from a pot.</summary>
        Stew,

        /// <summary>Any other liquid made by the game or a mod.</summary>
        Custom
    }
}
