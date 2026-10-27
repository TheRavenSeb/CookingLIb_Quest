namespace CookingLib
{
    /// <summary>The stage of a stew in a pot.</summary>
    public enum StewStage
    {
        /// <summary>Not cooked yet.</summary>
        Raw,

        /// <summary>Cooked.</summary>
        Cooked,

        /// <summary>Burnt.</summary>
        Burnt,

        /// <summary>The stew turned into nothing and the pot emptied.</summary>
        BurntAway
    }
}
