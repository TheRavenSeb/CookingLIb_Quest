namespace CookingLib
{
    /// <summary>One effect and its strength.</summary>
    public class EffectSpec
    {
        /// <summary>The game's effect name or number, copied from the dump, or a short alias such as Speed, Protection or Glow.</summary>
        public string Effect;

        /// <summary>Strength multiplier, from minus 100 to 100.</summary>
        public float Strength = 1f;

        /// <summary>Gives Heal or Nourish out over this many whole seconds, 1 to 3600, instead of at once. 0 gives it at once. Refused in stew recipes, since a stew always gives its effects at once.</summary>
        public int OverSeconds;
    }
}
