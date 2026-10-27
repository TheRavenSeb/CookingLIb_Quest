using System.Collections.Generic;
using Il2CppAlta.Cooking;
using Il2CppAlta.Liquid;
using UnityEngine;

namespace CookingLib
{
    /// <summary>A copy of a pot and its stew, taken when the event happened.</summary>
    public class StewEvent
    {
        internal StewEvent()
        {
        }

        /// <summary>The pot. It may be gone by the time a handler runs.</summary>
        public CookingPot Pot { get; internal set; }

        /// <summary>The pot's entity id, for this session's logs only. It changes across restarts.</summary>
        public uint PotId { get; internal set; }

        /// <summary>Where the pot was.</summary>
        public Vector3 Position { get; internal set; }

        /// <summary>Recipe, NoRecipe or Unfinished.</summary>
        public StewOutcome Outcome { get; internal set; }

        /// <summary>The recipe as a CookingLib id or a vanilla recipe name, or null.</summary>
        public string Recipe { get; internal set; }

        /// <summary>True when Recipe is a CookingLib id.</summary>
        public bool IsCookingLibRecipe { get; internal set; }

        /// <summary>The stew's stage after the change.</summary>
        public StewStage Stage { get; internal set; }

        /// <summary>The stew prefab name, for example Pumpkin Recipe Cooked Stew. For BurntAway, the stew that burnt away.</summary>
        public string StewLook { get; internal set; }

        /// <summary>The stew prefab's flag for the game's unused stew experience. False for BurntAway.</summary>
        public bool IsAwardingExperienceStage { get; internal set; }

        /// <summary>The pot's base as a CookingLib id or a vanilla liquid name, or null.</summary>
        public string BaseLiquid { get; internal set; }

        /// <summary>Item names of the ingredients in the order they went in. One the game failed to load stays null.</summary>
        public IReadOnlyList<string> Ingredients { get; internal set; }

        /// <summary>Item names of the garnishes, which never count as ingredients.</summary>
        public IReadOnlyList<string> Garnishes { get; internal set; }

        /// <summary>Who added each of the last AddedBy.Count ingredients, in the same order.</summary>
        public IReadOnlyList<StewCook> AddedBy { get; internal set; }

        /// <summary>The players in AddedBy, each once, without empty entries.</summary>
        public IReadOnlyList<StewCook> Cooks { get; internal set; }

        /// <summary>How many of the first ingredients came from a save, so they have no AddedBy entry.</summary>
        public int CarriedOver { get; internal set; }

        /// <summary>True when the pot was already locked and checked its ingredients again.</summary>
        public bool IsRecheck { get; internal set; }

        /// <summary>The pot's liquid after the change, or null for BurntAway.</summary>
        public LiquidContent Content { get; internal set; }
    }
}
