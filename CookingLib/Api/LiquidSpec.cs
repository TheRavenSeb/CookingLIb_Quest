using System.Collections.Generic;
using UnityEngine;

namespace CookingLib
{
    /// <summary>Everything a mod says about a new liquid.</summary>
    public class LiquidSpec
    {
        /// <summary>Unique id written as author.thing, for example shadow.blackdye.</summary>
        public string Id;

        /// <summary>Display name for logs and the dump. Falls back to the id.</summary>
        public string Name;

        /// <summary>A vanilla liquid look, copied from the dump.</summary>
        public string Look;

        /// <summary>Tint over the look. Every channel from 0 to 1.</summary>
        public Color Color = Color.white;

        /// <summary>Effects a drinker gets.</summary>
        public List<EffectSpec> Effects = new List<EffectSpec>();

        /// <summary>Vanilla chunk looks floating in the liquid, copied from the dump.</summary>
        public List<string> Chunks = new List<string>();

        /// <summary>True lets a splash apply the effects, like a teleport potion.</summary>
        public bool ConsumableThroughSkin;

        /// <summary>How strong a splash is when ConsumableThroughSkin is true.</summary>
        public float SplashStrength = 1f;

        /// <summary>True makes a stew cooked on this liquid take its colour at every stage, unless the recipe sets IgnoresDyes. A recipe's ResultLiquid is never recoloured.</summary>
        public bool ColorsStews;

        /// <summary>True lets the game's Water recipes also match a pot of this liquid. A recipe that lists this liquid still wins.</summary>
        public bool CountsAsWater;

        /// <summary>Thirst this liquid gives, for thirst mods such as HydrateOrDiedrate. CookingLib only stores it. Minus 100 to 100, where 100 is a full thirst bar and negative makes the drinker thirstier. Null leaves it to the thirst mod, HydrateOrDiedrate then uses 40.</summary>
        public float? Thirst { get; set; }
    }
}
