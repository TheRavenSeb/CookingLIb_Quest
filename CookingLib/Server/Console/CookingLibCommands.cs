using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Il2Cpp.Alta;
using Il2Cpp.Alta.Alchemy;
using Il2Cpp.Alta.Console;
using Il2Cpp.Alta.Cooking;
using Il2Cpp.Alta.Inventory;
using Il2Cpp.Alta.Liquid;
using Il2Cpp.Alta.Networking;
using Il2Cpp.Alta.Pages;
using MelonLoader.Utils;
using UnityEngine;

namespace CookingLib
{
    [Module("cookinglib", "CookingLib: status, dump, foods, pots, unlock")]
    internal static class CookingLibCommands
    {
        static readonly FieldInfo androidColorField = typeof(LiquidVisualData).GetField("androidLiquidColor", BindingFlags.NonPublic | BindingFlags.Instance);

        [Command("status", "Registered liquids, stew recipes, infusions and roasting rules, and why any were refused")]
        public static string Status()
        {
            StringBuilder text = new StringBuilder("CookingLib " + Main.Version + "\n");

            text.Append("liquids: ").Append(Liquids.All.Count).Append('\n');
            foreach (LibLiquid liquid in Liquids.All)
            {
                string state = liquid.Error != null ? "refused: " + liquid.Error : liquid.IsShared ? "shared as " + liquid.BaseDefinition.Hash : "server only, pot number " + liquid.BaseDefinition.Hash;
                text.Append("  ").Append(liquid.Id).Append("  ").Append(state).Append(LiquidFlags(liquid.Spec)).Append(AliasNotes(liquid.Spec?.Effects)).Append('\n');
            }

            text.Append("shared recipe pages: ").Append(StewRecipes.SharedPages ? "on" : "off").Append('\n');
            text.Append("stew recipes: ").Append(StewRecipes.All.Count).Append('\n');
            foreach (LibStewRecipe recipe in StewRecipes.All)
            {
                string state = StewRecipes.Describe(recipe);
                string flags = recipe.Spec != null && recipe.Spec.IgnoresDyes ? ", ignores dyes" : "";
                foreach (string spread in StewRecipes.SpreadBases(recipe))
                    flags += ", base " + spread + " uses OverSeconds, the stew gives it at once";
                if (recipe.Error == null && recipe.Spec.Slots != null && !Reachability.IsReachable(recipe.Spec.Slots.Count))
                    flags += ", no pot can normally make it (" + recipe.Spec.Slots.Count + " slots, pots lock at " + Reachability.CountsText() + ")";
                text.Append("  ").Append(recipe.Id).Append("  ").Append(state).Append(flags).Append(AliasNotes(RecipeEffects(recipe.Spec))).Append('\n');
            }

            Safe.Run("vanilla status", () => VanillaReport.Status(text));

            int dyes = Liquids.All.Count(l => l.Error == null && l.Spec.ColorsStews);
            int waters = Liquids.All.Count(l => l.Error == null && l.Spec.CountsAsWater);
            text.Append("stew dyes: ").Append(ServerPatches.StewContentOn ? dyes + " liquids, " + StewContentPatch.DyeTintWrites + " dye tint writes" : "off, the stew content patch failed to apply").Append('\n');
            text.Append("counts as water: ").Append(ServerPatches.StewMatchOn ? waters + " liquids, " + StewMatchPatch.WaterMatches + " Water recipe matches" : "off, the recipe matcher patch failed to apply").Append('\n');

            text.Append("infusions: ").Append(Infusions.All.Count).Append(", ").Append(InfusionPatch.Done).Append(" done, ")
                .Append(InfusionPatch.Refused).Append(" refused on endless sources and rivers, ").Append(InfusionPatch.PotBases).Append(" pot bases followed\n");
            foreach (LibInfusion infusion in Infusions.All)
            {
                string state = infusion.Error != null ? "refused: " + infusion.Error : infusion.IsReady ? "ready" : "waiting for the world";
                text.Append("  ").Append(infusion.Id).Append("  ").Append(state).Append('\n');
            }

            Safe.Run("roast status", () => RoastReport.Status(text));

            string events = StewEvents.Error;
            text.Append("stew events: ").Append(events != null ? "off, " + events
                : "ready, Made " + Handlers(StewEvents.MadeHandlers) + " (fired " + StewEvents.MadeFired + "), StageChanged "
                    + Handlers(StewEvents.StageChangedHandlers) + " (fired " + StewEvents.StageChangedFired + ")").Append('\n');

            string drinks = Drinks.Error;
            text.Append("drinks: ").Append(drinks == null ? "on" : drinks).Append("; handlers Drank ").Append(Drinks.DrankHandlers).Append(", Ate ").Append(Drinks.AteHandlers)
                .Append(", CanDrink ").Append(Drinks.CanDrinkHandlers).Append("; ").Append(Drinks.DrinkCount).Append(" drinks, ").Append(Drinks.EatCount).Append(" eats, ")
                .Append(Drinks.RefusedPours).Append(" refused pours, ").Append(Drinks.NoPlayer).Append(" with no player, ").Append(LiquidIdentity.Tagged).Append(" stews tagged\n");
            int spreads = Liquids.All.Count(l => l.Error == null && !l.IsShared && EffectSpread.Any(l.Spec.Effects));
            text.Append("over time: ").Append(ServerPatches.SpreadLoadOn ? spreads + " server only liquids, restored " + SpreadLoadPatch.Restored + " times after a load" : "off, the load patch failed to apply").Append('\n');

            Safe.Run("pot status", () => PotsReport.Status(text));

            text.Append("caught failures: ").Append(Safe.Failures).Append(", handler failures: ").Append(EventQueue.HandlerFailures).Append('\n');
            return text.ToString();
        }

        [Command("dump", "Writes every name a spec can use to UserData CookingLib-dump.txt")]
        public static string Dump()
        {
            try
            {
                string path = Path.Combine(MelonEnvironment.UserDataDirectory, "CookingLib-dump.txt");
                File.WriteAllText(path, BuildDump());
                return "wrote " + path;
            }
            catch (Exception e)
            {
                Log.Error("dump failed: " + e);
                return "dump failed: " + e.Message;
            }
        }

        [Command("pots", "Pot definitions, pot rules in force, which recipes pots can make, and every loaded pot")]
        public static string Pots()
        {
            try
            {
                return PotsReport.Run();
            }
            catch (Exception e)
            {
                Log.Error("pots failed: " + e);
                return "pots failed: " + e.Message;
            }
        }

        [Command("foods", "Food and stew values to UserData CookingLib-foods.tsv. foods <section> also prints one, foods mix <stage> <item>, ... [+ <garnish>, ...] runs the stew math")]
        public static string Foods(string args = "")
        {
            try
            {
                return FoodsTable.Run(args);
            }
            catch (Exception e)
            {
                Log.Error("foods failed: " + e);
                return "foods failed: " + e.Message;
            }
        }

        [Command("unlock", "Unlocks a shared recipe's journal page for everyone in this world: unlock <id>")]
        public static string Unlock(string id = "")
        {
            try
            {
                LibStewRecipe recipe = StewRecipes.FindShared(id);
                if (recipe == null)
                    return "no shared recipe named '" + id + "'" + Names.Suggest(id, StewRecipes.All.Where(r => r.IsShared && r.Recipe != null).Select(r => r.Id), "Shared recipes");
                if (!recipe.HasPage)
                    return recipe.Id + " has no page, SharedRecipePages is false";
                if (StewRecipes.IsUnlocked(recipe))
                    return recipe.Id + " is already unlocked";
                return StewRecipes.UnlockPage(recipe.Id) ? "unlocked " + recipe.Id + ", page " + recipe.Number : "unlock failed, see the log";
            }
            catch (Exception e)
            {
                Log.Error("unlock failed: " + e);
                return "unlock failed: " + e.Message;
            }
        }

        internal static void Register()
        {
            try
            {
                if (CommandService.CommandCollection.ProcessModule(typeof(CookingLibCommands), typeof(CookingLibCommands).Assembly, null) == null)
                    Log.Warn("console commands were not registered");
            }
            catch (Exception e)
            {
                Log.Error("console commands failed to register: " + e);
            }
        }

        static string BuildDump()
        {
            StringBuilder text = new StringBuilder("CookingLib " + Main.Version + " dump. Copy names from here into specs.\n\n");
            GameLiquids(text);
            OurLiquids(text);
            Looks(text);
            Section<LiquidVisualChunkDefinition>(text, "Chunks");
            Effects(text);
            Aliases(text);
            Section<StewRecipe>(text, "Stew recipes");
            Section<Glyph>(text, "Glyphs");
            Safe.Run("vanilla dump", () => VanillaReport.Dump(text));
            Stews(text);
            StewEventTable(text);
            Safe.Run("pot dump", () => PotsReport.Dump(text));
            Safe.Run("roast dump", () => RoastReport.Dump(text));
            DrinkNames(text);
            EndlessSources(text);

            text.Append("== Cookable categories ==\n");
            foreach (CookableCategoryDefinition category in Names.Categories())
                text.Append(category.name).Append('\n');

            text.Append("\n== Items ==\n");
            foreach (Item item in HashedGeneralValue<Item>.All.Where(i => i != null).OrderBy(i => i.name, StringComparer.OrdinalIgnoreCase))
            {
                CookableItem cookable = item.GetComponent<CookableItem>();
                text.Append(item.name).Append("  ").Append(item.Hash);
                if (item.Prefab != null && item.Prefab.name != item.name)
                    text.Append("  prefab ").Append(item.Prefab.name);
                if (cookable != null && cookable.GetType() == typeof(CookableItem))
                    text.Append("  stew ingredient");
                text.Append('\n');
            }
            return text.ToString();
        }

        static void Section<T>(StringBuilder text, string title) where T : HashedGeneralValue<T>
        {
            text.Append("== ").Append(title).Append(" ==\n");
            foreach (T value in HashedGeneralValue<T>.All.Where(v => v != null).OrderBy(v => v.name, StringComparer.OrdinalIgnoreCase))
                text.Append(value.name).Append("  ").Append(value.Hash).Append('\n');
            text.Append('\n');
        }

        static void GameLiquids(StringBuilder text)
        {
            text.Append("== Liquids ==\n");
            foreach (LiquidDefinition liquid in Liquids.GameLiquids().OrderBy(l => l.name, StringComparer.OrdinalIgnoreCase))
                text.Append(liquid.name).Append("  ").Append(liquid.Hash).Append('\n');
            text.Append('\n');
        }

        static void OurLiquids(StringBuilder text)
        {
            text.Append("== CookingLib liquids (key server only ones by id; a pot saves a server only base by its pot number) ==\n");
            foreach (LibLiquid liquid in Liquids.All.Where(l => l.Error == null).OrderBy(l => l.Id, StringComparer.OrdinalIgnoreCase))
            {
                string state = liquid.IsShared ? "shared " + liquid.BaseDefinition.Hash : "server only, pot number " + liquid.BaseDefinition.Hash + " as " + liquid.BaseDefinition.name;
                text.Append(liquid.Id).Append("  ").Append(state).Append(LiquidFlags(liquid.Spec)).Append(AliasNotes(liquid.Spec.Effects)).Append('\n');
            }
            text.Append('\n');
        }

        static void Looks(StringBuilder text)
        {
            text.Append("== Liquid looks (main colour, then Android colour) ==\n");
            foreach (LiquidVisualData look in HashedGeneralValue<LiquidVisualData>.All.Where(v => v != null).OrderBy(v => v.name, StringComparer.OrdinalIgnoreCase))
            {
                string android = androidColorField == null ? "?" : Rgba((Color)androidColorField.GetValue(look));
                text.Append(look.name).Append("  ").Append(look.Hash).Append("  ").Append(Rgba(look.LiquidColor)).Append("  ").Append(android).Append('\n');
            }
            text.Append('\n');
        }

        static void Aliases(StringBuilder text)
        {
            text.Append("== Effect aliases (a spec may use these, the game's names keep working) ==\n");
            foreach (EffectNames.EffectAlias alias in EffectNames.Aliases)
            {
                EffectDefinition effect = EffectNames.Resolve(alias);
                text.Append(string.Join(", ", alias.Spellings)).Append("  ").Append(effect == null ? "missing" : effect.name + " " + effect.Hash);
                List<string> users = AliasUsers(alias);
                if (users.Count > 0)
                    text.Append("  used by ").Append(string.Join(", ", users.ToArray()));
                text.Append('\n');
            }
            text.Append('\n');
        }

        static List<string> AliasUsers(EffectNames.EffectAlias alias)
        {
            List<string> users = Liquids.All.Where(l => UsesAlias(l.Spec?.Effects, alias)).Select(l => l.Id).ToList();
            users.AddRange(StewRecipes.All.Where(r => UsesAlias(RecipeEffects(r.Spec), alias)).Select(r => r.Id));
            return users;
        }

        static bool UsesAlias(IEnumerable<EffectSpec> effects, EffectNames.EffectAlias alias) =>
            effects != null && effects.Any(effect => effect != null && EffectNames.UsedAlias(effect.Effect) == alias);

        static void Stews(StringBuilder text)
        {
            text.Append("== Stews (tint, look, then the ingredients, liquid and extras multipliers, xp for a stage flagged for the game's unused stew experience; changed by names the CookingLib edit) ==\n");
            foreach (NetworkPrefab prefab in PrefabWalk.With<StewComponent>().OrderBy(p => p.name, StringComparer.OrdinalIgnoreCase))
            {
                StewComponent stew = prefab.GetComponent<StewComponent>();
                text.Append(prefab.name).Append("  ").Append(prefab.Hash).Append("  ").Append(Rgba(stew.TintColor)).Append("  ")
                    .Append(stew.LiquidVisualData == null ? "none" : stew.LiquidVisualData.name).Append("  x").Append(Claims.Number(stew.BaseEffectsMultiplier))
                    .Append(" x").Append(Claims.Number(stew.BaseLiquidEffectsMultiplier)).Append(" x").Append(Claims.Number(stew.ExtraEffectsMultiplier))
                    .Append("  xp ").Append(stew.IsAwardingExperienceStage ? "yes" : "no").Append(VanillaReport.ChangedBy(stew)).Append('\n');
            }
            text.Append('\n');
        }

        static void StewEventTable(StringBuilder text)
        {
            text.Append("== Stew events (stew, number, stage, family, recipes) ==\n");
            if (!StewStages.Ready())
            {
                text.Append(StewStages.Error).Append("\n\n");
                return;
            }

            text.Append("default stew ").Append(PrefabName(StewStages.DefaultRaw)).Append('\n');
            text.Append("non recipe stew ").Append(PrefabName(StewStages.NonRecipeRaw)).Append('\n');
            foreach (StewStages.Entry entry in StewStages.All.OrderBy(e => e.Raw.name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Stage))
            {
                List<StewRecipe> recipes = entry.Recipes;
                text.Append(entry.Name).Append("  ").Append(entry.Hash).Append("  ").Append(entry.Stage).Append(entry.IsFinal ? ", final" : "")
                    .Append("  from ").Append(entry.Raw.name).Append("  ").Append(recipes.Count == 0 ? "no recipe" : string.Join(", ", recipes.Select(r => r.name).ToArray())).Append('\n');
            }
            text.Append('\n');
        }

        static void DrinkNames(StringBuilder text)
        {
            text.Append("== Drink names (kind, then a name Liquids.Identify gives) ==\n");
            foreach (LiquidDefinition liquid in Liquids.GameLiquids().OrderBy(l => l.name, StringComparer.OrdinalIgnoreCase))
                text.Append("Vanilla  ").Append(liquid.name).Append('\n');
            foreach (LibLiquid liquid in Liquids.All.Where(l => l.Error == null).OrderBy(l => l.Id, StringComparer.OrdinalIgnoreCase))
                text.Append(liquid.IsShared ? "Shared  " : "ServerOnly  ").Append(liquid.Id).Append('\n');
            foreach (StewRecipe recipe in HashedGeneralValue<StewRecipe>.All.Where(r => r != null).OrderBy(r => r.name, StringComparer.OrdinalIgnoreCase))
            {
                LibStewRecipe ours = StewRecipes.Find(recipe);
                text.Append("Stew  ").Append(ours != null ? ours.Id : recipe.name).Append('\n');
            }
            text.Append("Stew  no name: the default and non recipe stews, and any stew that went through a save\n");
            text.Append("Custom  no name\n\n");
        }

        static string PrefabName(uint hash)
        {
            StewStages.Entry entry = StewStages.Get(hash);
            return entry == null ? "missing " + hash : entry.Name + " " + hash;
        }

        static string Handlers(int count) => count == 1 ? "1 handler" : count + " handlers";

        static string LiquidFlags(LiquidSpec spec)
        {
            if (spec == null)
                return "";
            return (spec.ColorsStews ? ", colours stews" : "") + (spec.CountsAsWater ? ", counts as water" : "") + (EffectSpread.Any(spec.Effects) ? ", gives effects over time" : "");
        }

        static IEnumerable<EffectSpec> RecipeEffects(StewRecipeSpec spec)
        {
            if (spec == null)
                return null;
            return (spec.ExtraEffects ?? new List<EffectSpec>()).Concat(spec.EffectModifiers ?? new List<EffectSpec>());
        }

        internal static string AliasNotes(IEnumerable<EffectSpec> effects)
        {
            if (effects == null)
                return "";
            List<string> notes = effects.Where(e => e != null).Select(e => EffectNames.AliasNote(e.Effect)).Where(n => n != null).Distinct().ToList();
            return notes.Count == 0 ? "" : "  aliases: " + string.Join(", ", notes.ToArray());
        }

        static string Rgba(Color c) =>
            string.Format(CultureInfo.InvariantCulture, "{0:0.000} {1:0.000} {2:0.000} {3:0.000}", c.r, c.g, c.b, c.a);

        static void Effects(StringBuilder text)
        {
            Dictionary<EffectDefinition, List<string>> uses = EffectUses();
            text.Append("== Effects (settings, then the strengths the game uses) ==\n");
            foreach (EffectDefinition effect in HashedGeneralValue<EffectDefinition>.All.Where(e => e != null).OrderBy(e => e.name, StringComparer.OrdinalIgnoreCase))
            {
                text.Append(effect.name).Append("  ").Append(effect.Hash).Append("  ").Append(effect.GetType().Name).Append("  ").Append(Settings(effect)).Append('\n');
                if (uses.TryGetValue(effect, out List<string> list))
                {
                    foreach (string use in list)
                        text.Append("    ").Append(use).Append('\n');
                }
            }
            text.Append('\n');
        }

        static string Settings(EffectDefinition effect)
        {
            List<string> parts = new List<string>();
            for (Type type = effect.GetType(); type != typeof(EffectDefinition); type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsPublic || field.IsDefined(typeof(SerializeField), false))
                        parts.Add(field.Name + " " + Show(field.GetValue(effect)));
                }
            }
            return string.Join(", ", parts.ToArray());
        }

        static string Show(object value)
        {
            if (value is HashedGeneralValue hashed)
                return hashed == null ? "none" : hashed.name + " " + hashed.Hash;
            if (value is UnityEngine.Object asset)
                return asset == null ? "none" : asset.name;
            return value == null ? "none" : value.ToString();
        }

        static Dictionary<EffectDefinition, List<string>> EffectUses()
        {
            Dictionary<EffectDefinition, List<string>> uses = new Dictionary<EffectDefinition, List<string>>();
            foreach (LiquidDefinition liquid in Liquids.GameLiquids().Where(l => l.Data != null))
                AddUses(uses, liquid.Data.Effects, "liquid " + liquid.name);
            foreach (LibLiquid liquid in Liquids.All.Where(l => l.Error == null))
                AddUses(uses, liquid.Data.Effects, "liquid " + liquid.Id);
            foreach (StewRecipe recipe in HashedGeneralValue<StewRecipe>.All.Where(r => r != null))
            {
                AddUses(uses, recipe.ExtraEffects, "recipe " + recipe.name + " extra");
                AddUses(uses, recipe.EffectModifiers, "recipe " + recipe.name + " modifier");
            }
            foreach (Item item in HashedGeneralValue<Item>.All.Where(i => i != null))
            {
                CookableItem cookable = item.GetComponent<CookableItem>();
                if (cookable != null && cookable.GetType() == typeof(CookableItem))
                    AddUses(uses, cookable.Effects, "ingredient " + item.name);
            }
            return uses;
        }

        static void AddUses(Dictionary<EffectDefinition, List<string>> uses, IEnumerable<EffectDefinitionApplication> effects, string where)
        {
            if (effects == null)
                return;

            foreach (EffectDefinitionApplication effect in effects)
            {
                if (effect == null || effect.EffectToModify == null)
                    continue;
                if (!uses.TryGetValue(effect.EffectToModify, out List<string> list))
                    uses[effect.EffectToModify] = list = new List<string>();
                list.Add(where + "  " + effect.StrengthMultiplier);
            }
        }

        static void EndlessSources(StringBuilder text)
        {
            text.Append("== Endless liquid sources (infusions skip these) ==\n");
            foreach (NetworkPrefab prefab in PrefabWalk.All().OrderBy(p => p.name, StringComparer.OrdinalIgnoreCase))
            {
                if (prefab.GetComponentsInChildren<LiquidContainer>(true).Any(InfusionPatch.IsEndless))
                    text.Append(prefab.name).Append("  ").Append(prefab.Hash).Append('\n');
            }
            text.Append('\n');
        }
    }
}
