namespace CookingLib
{
    /// <summary>What happens when one item roasts over a fire. Leave a field as it is to keep the game's.</summary>
    public class RoastingSpec
    {
        /// <summary>Unique id written as author.thing.</summary>
        public string Id;

        /// <summary>The item, by the game's item or prefab name or number, copied from the dump. Each stage is its own item.</summary>
        public string Item;

        /// <summary>What it turns into when cooked: a prefab or item name from the dump, Roasting.Nothing to vanish, or null for the game's.</summary>
        public string CookedInto;

        /// <summary>What it turns into when burnt: a prefab or item name from the dump, Roasting.Nothing to vanish, or null for the game's.</summary>
        public string BurntInto;

        /// <summary>How fast it cooks, from 0 to 100. 4 is four times as fast, 0 never cooks.</summary>
        public float CookSpeed = 1f;

        /// <summary>How fast it burns, from 0 to 100. 4 is four times as fast, 0 never burns.</summary>
        public float BurnSpeed = 1f;
    }
}
