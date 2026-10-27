using System;
using System.Collections.Generic;

namespace CookingLib
{
    /// <summary>Drink and eat events. They fire on the server only.</summary>
    public static class Drinks
    {
        /// <summary>A player drank: liquid landed in their mouth and the game applied its effects. Handlers run on the main thread from the next frame.</summary>
        public static event Action<DrinkArgs> Drank;

        /// <summary>A player finished eating and the game applied the food's effects. It also fires for anything another mod makes edible. Handlers run on the main thread from the next frame.</summary>
        public static event Action<EatArgs> Ate;

        /// <summary>Asked when liquid is about to land in a player's mouth. False refuses it, as a full stomach does. It runs at once and often, so keep it quick, give the same answer for the same check, and never move liquid. A handler that throws allows.</summary>
        public static event Func<DrinkCheck, bool> CanDrink;

        /// <summary>Why some of the events are off, or null when they all work. Always set on players' machines.</summary>
        public static string Error
        {
            get
            {
                if (!Main.IsServer)
                    return "drink and eat events run on the server only";

                List<string> off = new List<string>();
                if (!ServerPatches.DrinksOn)
                    off.Add("Drank");
                if (!ServerPatches.MouthFilterOn)
                    off.Add("CanDrink");
                if (!ServerPatches.EatsOn)
                    off.Add("Ate");
                return off.Count == 0 ? null : "the patches for " + string.Join(", ", off.ToArray()) + " failed to apply, see the log";
            }
        }

        internal static int DrinkCount { get; private set; }

        internal static int EatCount { get; private set; }

        internal static int RefusedPours { get; set; }

        internal static int NoPlayer { get; private set; }

        internal static int DrankHandlers => Drank?.GetInvocationList().Length ?? 0;

        internal static int AteHandlers => Ate?.GetInvocationList().Length ?? 0;

        internal static int CanDrinkHandlers => CanDrink?.GetInvocationList().Length ?? 0;

        internal static bool Checking => CanDrink != null;

        internal static void Drink(DrinkArgs args)
        {
            DrinkCount++;
            if (!args.Linked)
            {
                NoPlayer++;
                return;
            }
            if (Drank == null)
                return;

            args.Liquid = Liquids.Identify(args.Taken);
            EventQueue.Enqueue(Drank, args, nameof(Drank));
        }

        internal static void Eat(EatArgs args)
        {
            EatCount++;
            if (!args.Linked)
            {
                NoPlayer++;
                return;
            }
            EventQueue.Enqueue(Ate, args, nameof(Ate));
        }

        internal static bool Allows(DrinkCheck check) => EventQueue.Check(CanDrink, check, nameof(CanDrink));
    }
}
