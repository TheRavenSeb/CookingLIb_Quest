using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Il2CppAlta.Console;
using Il2CppAlta.Cooking;
using Il2CppAlta.Liquid;
using CookingLib;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(CookingLibExample.Example), "CookingLibExample", "0.2.1", "Shadow")]
[assembly: MelonGame("Il2CppAlta", "A Township Tale")]

namespace CookingLibExample
{
    public class Example : MelonMod
    {
        static bool commandsAdded;
        static MelonLogger.Instance log;

        public override void OnInitializeMelon()
        {
            log = LoggerInstance;
            ProbeSwitches.Init();
            CookingLib.Main.Setup += Setup;
            StewEvents.Made += args => LogStew("made", args);
            StewEvents.StageChanged += args => LogStew("stage changed", args);
            Drinks.Drank += LogDrink;
            Roasting.Roasted += LogRoast;
            Drinks.Ate += args => log.Msg("ate: " + args.UserName + " " + args.UserId + ", " + (args.Item == null ? "no item" : args.Item.name) + ", " + args.Effects.Count + " effects"
                + (args.Edible == null ? "" : ", changed by " + args.Edible.Id));
        }

        public override void OnUpdate()
        {
            if (commandsAdded || !CookingLib.Main.IsServer || NetworkSceneManager.Current == null)
                return;

            commandsAdded = true;
            CommandService.CommandCollection.ProcessModule(typeof(ExampleCommands), typeof(ExampleCommands).Assembly, null);
            CommandService.CommandCollection.ProcessModule(typeof(ProbeSwitches), typeof(ProbeSwitches).Assembly, null);
        }

        static void Setup()
        {
            Liquids.RegisterServerOnly(new LiquidSpec
            {
                Id = "example.ashwater",
                Name = "Ash Water",
                Look = "Water Liquid Visual Data",
                Color = new Color(0.55f, 0.53f, 0.5f),
                Effects = { new EffectSpec { Effect = "Heal", Strength = 0.5f } },
                Chunks = { "Apple Ripe Chunk Visual" }
            });

            Liquids.RegisterShared(60001, new LiquidSpec
            {
                Id = "example.blackdye",
                Name = "Black Dye",
                Look = "Water Liquid Visual Data",
                Color = new Color(0.08f, 0.08f, 0.08f),
                ColorsStews = true,
                CountsAsWater = true
            });

            Liquids.RegisterServerOnly(new LiquidSpec
            {
                Id = "example.reddye",
                Name = "Red Dye",
                Look = "Default Stew Raw Liquid Visual Data",
                Color = new Color(0.75f, 0.1f, 0.08f),
                ColorsStews = true,
                CountsAsWater = true
            });

            Liquids.RegisterServerOnly(new LiquidSpec
            {
                Id = "example.weakskin",
                Name = "Weak Oakskin",
                Look = "Water Liquid Visual Data",
                Color = new Color(0.62f, 0.5f, 0.35f),
                Effects = { new EffectSpec { Effect = "Damage Protection Indirect effect", Strength = 0.35f } }
            });

            Liquids.RegisterServerOnly(new LiquidSpec
            {
                Id = "example.oakskin",
                Name = "Oakskin",
                Look = "Water Liquid Visual Data",
                Color = new Color(0.45f, 0.3f, 0.15f),
                Effects = { new EffectSpec { Effect = "Damage Protection Indirect effect", Strength = 2f } }
            });

            Liquids.RegisterServerOnly(new LiquidSpec
            {
                Id = "example.regen",
                Name = "Regen",
                Look = "Water Liquid Visual Data",
                Color = new Color(0.9f, 0.45f, 0.6f),
                Effects = { new EffectSpec { Effect = "Heal", Strength = 2f, OverSeconds = 20 } },
                Thirst = 10f
            });

            StewRecipes.Register(new StewRecipeSpec
            {
                Id = "example.ashstew",
                Look = "Vegetable Ragu",
                BaseLiquids = new List<string> { "example.ashwater" },
                Slots =
                {
                    new SlotSpec { Category = "Mushrooms" },
                    new SlotSpec { Category = "Mushrooms" },
                    new SlotSpec { Category = "Vegetables Category" },
                    new SlotSpec { Category = "Vegetables Category" }
                },
                ExtraEffects = { new EffectSpec { Effect = "Heal", Strength = 1f } },
                Tint = new Color(0.45f, 0.42f, 0.38f)
            });

            StewRecipes.Register(new StewRecipeSpec
            {
                Id = "example.porkstew",
                Look = "Vegetable Ragu",
                BaseLiquids = new List<string> { "example.reddye", "example.blackdye" },
                Slots =
                {
                    new SlotSpec { Category = "Pork" },
                    new SlotSpec { Category = "Pork" },
                    new SlotSpec { Category = "Pork" },
                    new SlotSpec { Category = "Pork" }
                },
                Tint = new Color(0.55f, 0.36f, 0.22f),
                IgnoresDyes = true
            });

            StewRecipes.RegisterShared(64001, new StewRecipeSpec
            {
                Id = "example.hearthstew",
                Look = "Vegetable Ragu",
                Slots =
                {
                    new SlotSpec { Items = { "Carrot Full Ripe", "Carrot Full Cooked" } },
                    new SlotSpec { Items = { "Potato Full Ripe", "Potato Full Cooked" }, Glyph = "Glyph_Potato_Full" },
                    new SlotSpec { Category = "Mushrooms" },
                    new SlotSpec { Category = "Fruit" }
                },
                ExtraEffects = { new EffectSpec { Effect = "Heal", Strength = 0.5f } }
            });

            VanillaRecipes.Modify(new VanillaRecipeSpec
            {
                Id = "example.berrystew",
                Recipe = "Berry Stew",
                ExtraEffects = new List<EffectSpec> { new EffectSpec { Effect = "Speed Indirect Effect", Strength = 0.5f } },
                EffectModifiers = new List<EffectSpec> { new EffectSpec { Effect = "Nourish", Strength = 1.55f } },
                Tint = new Color(0.1f, 0.9f, 0.1f)
            });

            VanillaRecipes.Disable("example.nopoultry", "Poultry Stew");

            VanillaIngredients.Modify(new IngredientSpec
            {
                Id = "example.carrot",
                Item = "Carrot Full Ripe",
                Effects = new List<EffectSpec> { new EffectSpec { Effect = "Speed Indirect Effect", Strength = 0.3f } },
                Chunk = "Salt Chunk Visual"
            });

            VanillaIngredients.Modify(new IngredientSpec
            {
                Id = "example.cookedcarrot",
                Item = "Carrot Full Cooked",
                Category = "Berries"
            });

            VanillaGarnishes.Modify(new GarnishSpec
            {
                Id = "example.salt",
                Item = "Salt",
                Multipliers = new List<EffectSpec> { new EffectSpec { Effect = "Protection", Strength = 2f } }
            });

            VanillaGarnishes.Modify(new GarnishSpec
            {
                Id = "example.garlic",
                Item = "Garlic Full Cooked",
                Effects = new List<EffectSpec> { new EffectSpec { Effect = "Nourish", Strength = 0.5f } }
            });

            Edibles.Register(new EdibleSpec
            {
                Id = "example.babuleg",
                Item = "Babu Leg Full Cooked",
                Effects = new List<EffectSpec>
                {
                    new EffectSpec { Effect = "Heal", Strength = 0.3f, OverSeconds = 10 },
                    new EffectSpec { Effect = "Nourish", Strength = 1.2f }
                }
            });

            Edibles.Register(new EdibleSpec
            {
                Id = "example.daismeat",
                Item = "Dais Meat Full Cooked",
                Effects = new List<EffectSpec>
                {
                    new EffectSpec { Effect = "Nourish", Strength = 0.2f },
                    new EffectSpec { Effect = "Speed", Strength = 0.3f }
                }
            });

            VanillaLiquids.Modify(new VanillaLiquidSpec
            {
                Id = "example.health",
                Liquid = "Health Potion",
                Effects = new List<EffectSpec> { new EffectSpec { Effect = "Heal", Strength = 3f } }
            });

            if (ProbeSwitches.WaterAtBoot)
                VanillaLiquids.Modify(ProbeSwitches.WaterSpec);

            Roasting.Register(new RoastingSpec
            {
                Id = "example.roastpickaxe",
                Item = "Garlic Full Ripe",
                CookedInto = "Pickaxe Head",
                CookSpeed = 4f
            });

            Roasting.Register(new RoastingSpec
            {
                Id = "example.roastnever",
                Item = "Garlic Half Ripe",
                BurntInto = Roasting.Nothing,
                CookSpeed = 0f,
                BurnSpeed = 4f
            });

            Roasting.Register(new RoastingSpec
            {
                Id = "example.roastfast",
                Item = "Garlic Full Unripe",
                CookedInto = "Pickaxe Head",
                CookSpeed = 100f
            });

            if (ProbeSwitches.RoastAtBoot)
                Roasting.Register(ProbeSwitches.RoastSpec());

            Infusions.Register(new InfusionSpec
            {
                Id = "example.coaldye",
                Item = "Coal",
                OutputLiquid = "example.blackdye"
            });

            Infusions.Register(new InfusionSpec
            {
                Id = "example.reddyeinfusion",
                Item = "Dais Meat Full Ripe",
                OutputLiquid = "example.reddye"
            });

            Infusions.Register(new InfusionSpec
            {
                Id = "example.regeninfusion",
                Item = "Dais Meat Half Ripe",
                OutputLiquid = "example.regen"
            });

            Infusions.Register(new InfusionSpec
            {
                Id = "example.ashinfusion",
                Item = "Coal Ash",
                OutputLiquid = "example.ashwater"
            });

            Infusions.Register(new InfusionSpec
            {
                Id = "example.weakskininfusion",
                Item = "Copper Ingot",
                OutputLiquid = "example.weakskin"
            });

            Infusions.Register(new InfusionSpec
            {
                Id = "example.oakskininfusion",
                Item = "Iron Ingot",
                OutputLiquid = "example.oakskin"
            });
        }

        static void LogDrink(DrinkArgs args)
        {
            LiquidInfo liquid = args.Liquid;
            string what = liquid == null ? "nothing" : liquid.Kind + " " + (liquid.Name ?? "no name") + ", look " + liquid.Look;
            if (liquid != null && liquid.Stage != null)
                what += ", " + liquid.Stage + ", " + liquid.Ingredients.Count + " ingredients";
            string thirst = liquid == null || !liquid.Thirst.HasValue ? "default" : liquid.Thirst.Value.ToString(CultureInfo.InvariantCulture);
            log.Msg("drank: " + args.UserName + " " + args.UserId + (args.Player == null ? " (left)" : "") + ", " + args.Units + " of " + args.MouthSize + ", " + what + ", thirst " + thirst);
        }

        static void LogRoast(RoastedEvent args)
        {
            PhysicalMaterialPart part = args.Result == null ? null : args.Result.GetComponentInChildren<PhysicalMaterialPart>();
            string material = part == null || part.PhysicalMaterial == null ? "no material" : part.PhysicalMaterial.name;
            log.Msg("roasted: " + args.FromName + (args.Burnt ? " burnt into " : " cooked into ") + (args.ResultName ?? "nothing") + ", " + material
                + ", last held by " + (args.UserName ?? "nobody") + " " + args.UserId + (args.Rule == null ? ", game roast" : ", rule " + args.Rule.Id));
        }

        static void LogStew(string what, StewEvent args)
        {
            string cooks = string.Join(", ", args.Cooks.Select(cook => cook.UserName + " " + cook.UserId).ToArray());
            int stews = args.Pot == null ? -1 : args.Pot.GetComponentsInChildren<StewComponent>(true).Length;
            log.Msg("stew " + what + ": pot " + args.PotId + ", " + args.Outcome + " " + (args.Recipe ?? "no recipe") + ", " + args.Stage + " " + args.StewLook
                + ", base " + args.BaseLiquid + ", " + args.Ingredients.Count + " ingredients, " + args.CarriedOver + " carried over" + (args.IsRecheck ? ", recheck" : "")
                + ", cooks " + (cooks.Length == 0 ? "none" : cooks) + ", stews in pot " + stews);
        }
    }

    [Module("cookinglibexample", "CookingLibExample: find")]
    internal static class ExampleCommands
    {
        [Command("find", "Names the CookingLib liquid in every loaded container, using Liquids.Find")]
        public static string Find()
        {
            StringBuilder text = new StringBuilder();
            foreach (LiquidContainer container in Object.FindObjectsOfType<LiquidContainer>())
            {
                LibLiquid liquid = Liquids.Find(container.ContainedLiquid);
                if (liquid != null)
                    text.Append(container.transform.root.name).Append(" holds ").Append(liquid.Id).Append('\n');
            }
            return text.Length == 0 ? "no container holds a CookingLib liquid" : text.ToString();
        }
    }
}
