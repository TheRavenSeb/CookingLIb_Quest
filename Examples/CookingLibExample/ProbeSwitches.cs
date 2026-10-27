using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Il2CppAlta.Console;
using CookingLib;
using MelonLoader;
using UnityEngine;

namespace CookingLibExample
{
    [Module("cookingprobes", "CookingLibExample test switches, off at start except water and roast, which are read at boot: list, on <name>, off <name>")]
    internal static class ProbeSwitches
    {
        internal static readonly VanillaLiquidSpec WaterSpec = new VanillaLiquidSpec
        {
            Id = "example.water",
            Liquid = "Water",
            AllowWater = true,
            Effects = new List<EffectSpec> { new EffectSpec { Effect = "Reveal", Strength = 1f }, new EffectSpec { Effect = "Heal", Strength = 0.1f } }
        };

        static MelonPreferences_Entry<bool> water;
        static MelonPreferences_Entry<bool> roast;
        static MelonPreferences_Entry<string> roastInto;

        static readonly List<Switch> switches = new List<Switch>
        {
            Rules("rules", "Ingredients 3, Garnishes 1, StewingTemperature 70, GlobalEffects Feed 3 and Heal 0.2", new PotRulesSpec
            {
                Id = "example.rules",
                Ingredients = 3,
                Garnishes = 1,
                StewingTemperature = 70f,
                GlobalEffects = new List<EffectSpec> { new EffectSpec { Effect = "Feed", Strength = 3f }, new EffectSpec { Effect = "Heal", Strength = 0.2f } }
            }),
            Rules("hot", "StewingTemperature 95", new PotRulesSpec { Id = "example.hotpot", StewingTemperature = 95f }),
            Rules("big", "Ingredients 8", new PotRulesSpec { Id = "example.bigpot", Ingredients = 8 }),
            Rules("feed10", "GlobalEffects Feed 10", new PotRulesSpec { Id = "example.feed10", GlobalEffects = new List<EffectSpec> { new EffectSpec { Effect = "Feed", Strength = 10f } } }),
            Rules("feed5", "GlobalEffects Feed 5", new PotRulesSpec { Id = "example.feed5", GlobalEffects = new List<EffectSpec> { new EffectSpec { Effect = "Feed", Strength = 5f } } }),
            Rules("norecipe", "NoRecipeStew Pumpkin Recipe Raw Stew, so a pot that matches no recipe looks like pumpkin stew", new PotRulesSpec { Id = "example.norecipe", NoRecipeStew = "Pumpkin Recipe Raw Stew" }),
            Rules("push0", "RejectionForce 0, so a locked pot keeps an ingredient dropped in", new PotRulesSpec { Id = "example.push0", RejectionForce = 0f }),
            Rules("push1000", "RejectionForce 1000", new PotRulesSpec { Id = "example.push1000", RejectionForce = 1000f }),
            Stew("stew", "Non Recipe Raw Stew counts its ingredients x1.5 and turns green, on stews already in pots too", new StewSpec
            {
                Id = "example.nonrecipestew",
                Stew = "Non Recipe Raw Stew",
                Ingredients = 1.5f,
                Tint = new Color(0.2f, 0.8f, 0.2f)
            }),
            new Switch("nowater", "players can't drink the game's Water", () =>
            {
                Drinks.CanDrink += RefuseWater;
                return null;
            }, () => Drinks.CanDrink -= RefuseWater),
            new Switch("water", "the game's Water gives Reveal 1 and Heal 0.1, read at boot, so restart the server after on or off", () => SaveWater(true), () => SaveWater(false)),
            new Switch("roast", "Garlic Half Cooked burns into the RoastPartsInto prefab, Pouch unless MelonPreferences.cfg names another, read at boot, so restart the server after on or off", () => SaveRoast(true), () => SaveRoast(false))
        };

        internal static bool WaterAtBoot => water != null && water.Value;

        internal static bool RoastAtBoot => roast != null && roast.Value;

        internal static RoastingSpec RoastSpec() => new RoastingSpec
        {
            Id = "example.roastparts",
            Item = "Garlic Half Cooked",
            BurntInto = roastInto.Value,
            BurnSpeed = 4f
        };

        internal static void Init()
        {
            MelonPreferences_Category category = MelonPreferences.CreateCategory("CookingLibExample");
            water = category.CreateEntry("WaterEdit", false, "Water edit probe, read at boot");
            roast = category.CreateEntry("RoastParts", false, "Roast probe: Garlic Half Cooked burns into RoastPartsInto, read at boot");
            roastInto = category.CreateEntry("RoastPartsInto", "Pouch", "A prefab from the dump's roast results with parts");
            Find("water").IsOn = water.Value;
            Find("roast").IsOn = roast.Value;
        }

        [Command("list", "Every switch, what it does and whether it is on")]
        public static string List()
        {
            StringBuilder text = new StringBuilder();
            foreach (Switch item in switches)
                text.Append(item.Name).Append(item.IsOn ? "  on  " : "  off  ").Append(item.Help).Append('\n');
            return text.ToString();
        }

        [Command("on", "Turns a switch on: on <name>")]
        public static string On(string name = "")
        {
            Switch item = Find(name);
            if (item == null)
                return Missing(name);
            if (item.IsOn)
                return item.Name + " is already on";

            string error = item.TurnOn();
            if (error != null)
                return item.Name + " stays off, refused: " + error;
            item.IsOn = true;
            return item.Name + " on: " + item.Help;
        }

        [Command("off", "Turns a switch off: off <name>")]
        public static string Off(string name = "")
        {
            Switch item = Find(name);
            if (item == null)
                return Missing(name);

            item.TurnOff();
            item.IsOn = false;
            return item.Name + " off";
        }

        static Switch Rules(string name, string help, PotRulesSpec spec) =>
            new Switch(name, help, () => PotRules.Set(spec).Error, () => PotRules.Remove(spec.Id));

        static Switch Stew(string name, string help, StewSpec spec) =>
            new Switch(name, help, () => VanillaStews.Modify(spec).Error, () => VanillaStews.Revert(spec.Id));

        static string SaveWater(bool on)
        {
            water.Value = on;
            MelonPreferences.Save();
            return null;
        }

        static string SaveRoast(bool on)
        {
            roast.Value = on;
            MelonPreferences.Save();
            return null;
        }

        static bool RefuseWater(DrinkCheck check) => !(check.Liquid != null && check.Liquid.Kind == LiquidKind.Vanilla && check.Liquid.Name == "Water");

        static Switch Find(string name) => switches.FirstOrDefault(s => string.Equals(s.Name, (name ?? "").Trim(), StringComparison.OrdinalIgnoreCase));

        static string Missing(string name) => "no switch named '" + name + "', switches: " + string.Join(", ", switches.Select(s => s.Name).ToArray());

        sealed class Switch
        {
            internal Switch(string name, string help, Func<string> turnOn, Action turnOff)
            {
                Name = name;
                Help = help;
                TurnOn = turnOn;
                TurnOff = turnOff;
            }

            internal string Name { get; }

            internal string Help { get; }

            internal Func<string> TurnOn { get; }

            internal Action TurnOff { get; }

            internal bool IsOn { get; set; }
        }
    }
}
