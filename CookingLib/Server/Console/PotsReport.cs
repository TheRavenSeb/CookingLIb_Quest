using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Il2Cpp.Alta;
using Il2Cpp.Alta.Alchemy;
using Il2Cpp.Alta.Cooking;
using Il2Cpp.Alta.Liquid;
using Il2Cpp.Alta.Networking;
using HarmonyLib;
using UnityEngine;

namespace CookingLib
{
    internal static class PotsReport
    {
        const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        static readonly AccessTools.FieldRef<CookingPot, CookingPot.CookingStage> potStage = AccessTools.FieldRefAccess<CookingPot, CookingPot.CookingStage>("currentStage");
        static readonly AccessTools.FieldRef<CookingPot, StewManager> potManager = AccessTools.FieldRefAccess<CookingPot, StewManager>("stewManager");
        static readonly AccessTools.FieldRef<StewManager, StewComponent> managerStew = AccessTools.FieldRefAccess<StewManager, StewComponent>("stew");
        static readonly AccessTools.FieldRef<StewManager, StewRecipe> managerRecipe = AccessTools.FieldRefAccess<StewManager, StewRecipe>("currentRecipe");
        static readonly string[] mouthFields = { "statToMonitor", "effectHandlingStrength", "canConsumeWastefully", "canConsumeWhenStatIsFull" };

        internal static string Run()
        {
            if (!PotKnobs.Ready())
                return "pots: off, " + PotKnobs.Error;

            StringBuilder text = new StringBuilder();
            Definitions(text);
            Knobs(text);
            Rules(text);
            Reachable(text, true);
            Reloads(text);
            Loaded(text);
            return text.ToString();
        }

        internal static void Status(StringBuilder text)
        {
            Rules(text);
            if (!Main.IsServer)
                return;
            Reloads(text);
            if (!PotKnobs.Built)
            {
                text.Append("pots: ").Append(PotKnobs.Error != null ? "off, " + PotKnobs.Error : "waiting for the world").Append('\n');
                return;
            }

            text.Append("pots: lock at ").Append(Reachability.Known ? Reachability.CountsText() : "?").Append(" ingredients, worst-case Feed of a full pot ")
                .Append(Claims.Number(PotKnobs.WorstFeed(false))).Append(" (game ").Append(Claims.Number(PotKnobs.WorstFeed(true))).Append(")\n");
            foreach (string warning in Warnings())
                text.Append("  ").Append(warning).Append('\n');
            Reachable(text, false);
            ClaimLines(text);
        }

        internal static void Dump(StringBuilder text)
        {
            text.Append("== Pots (definitions, the settings pot rules change, then the mouth's drink gate) ==\n");
            if (!PotKnobs.Ready())
            {
                text.Append(PotKnobs.Error).Append("\n\n");
                return;
            }

            Definitions(text);
            Knobs(text);
            CookingSettings settings = PotKnobs.Settings;
            text.Append("fade out ").Append(Claims.Number(settings.CookableAddedToStewFadeoutDuration))
                .Append(" s, missing modifiers count as 1 ").Append(settings.IsAssumingNoEffectModificationOnMissing).Append(", decay ").Append(Claims.Number(settings.CookableDecayDuration))
                .Append(" s, items in a stew at most ").Append(settings.MaxNumberOfItemsInStew).Append('\n');
            Mouths(text);
            text.Append('\n');
        }

        static void Definitions(StringBuilder text)
        {
            text.Append("pot definitions: ").Append(PotKnobs.Definitions.Count).Append('\n');
            foreach (PotKnobs.Pot pot in PotKnobs.Definitions)
            {
                text.Append("  ").Append(pot.Definition.name).Append("  ingredients ").Append(pot.Ingredients).Append(" (game ").Append(pot.GameIngredients)
                    .Append("), garnishes ").Append(pot.Garnishes).Append(" (game ").Append(pot.GameGarnishes).Append(")  on ")
                    .Append(string.Join(", ", pot.Prefabs.Select(p => p.name + " " + p.Hash).ToArray())).Append('\n');
            }
        }

        static void Knobs(StringBuilder text)
        {
            text.Append("stewing temperature ").Append(Claims.Number(PotKnobs.LiveTemperature)).Append(" (game ").Append(Claims.Number(PotKnobs.GameTemperature))
                .Append("), every stew gets ").Append(Claims.Effects(PotKnobs.LiveGlobal)).Append(" (game ").Append(Claims.Effects(PotKnobs.GameGlobal)).Append(")\n");
            text.Append("no recipe stew ").Append(PrefabName(PotKnobs.LiveNoRecipe)).Append(" (game ").Append(PrefabName(PotKnobs.GameNoRecipe))
                .Append("), locked pot push ").Append(Claims.Number(PotKnobs.LivePush)).Append(" (game ").Append(Claims.Number(PotKnobs.GamePush)).Append(")\n");
        }

        static string PrefabName(NetworkPrefab prefab) => prefab == null ? "none" : prefab.name + " " + prefab.Hash;

        static void Rules(StringBuilder text)
        {
            text.Append("pot rules: ").Append(PotRules.All.Count).Append('\n');
            foreach (LibPotRules rules in PotRules.All)
            {
                text.Append("  ").Append(rules.Id).Append("  ").Append(State(rules));
                if (rules.Error == null)
                    text.Append(": ").Append(Live(rules)).Append(CookingLibCommands.AliasNotes(rules.Spec.GlobalEffects));
                text.Append('\n');
            }
        }

        static string State(LibPotRules rules)
        {
            if (rules.Error != null)
                return "refused: " + rules.Error;
            if (!Main.IsServer)
                return "recorded, pot rules apply on the server only";
            if (rules.IsApplied)
                return "applied";
            return PotKnobs.Error != null ? "not applied, " + PotKnobs.Error : "waiting for the world";
        }

        static string Live(LibPotRules rules)
        {
            PotRulesSpec spec = rules.Spec;
            bool known = PotKnobs.Built && Main.IsServer;
            PotKnobs.Pot first = PotKnobs.Definitions.FirstOrDefault();
            List<string> parts = new List<string>();
            if (spec.Ingredients.HasValue)
                parts.Add("Ingredients " + spec.Ingredients.Value + Game(first == null ? null : first.GameIngredients.ToString()));
            if (spec.Garnishes.HasValue)
                parts.Add("Garnishes " + spec.Garnishes.Value + Game(first == null ? null : first.GameGarnishes.ToString()));
            if (spec.StewingTemperature.HasValue)
                parts.Add("StewingTemperature " + Claims.Number(spec.StewingTemperature.Value) + Game(known ? Claims.Number(PotKnobs.GameTemperature) : null));
            if (rules.GlobalEffects != null)
                parts.Add("GlobalEffects " + Claims.Effects(rules.GlobalEffects) + Game(known ? Claims.Effects(PotKnobs.GameGlobal) : null));
            if (spec.NoRecipeStew != null)
                parts.Add("NoRecipeStew " + (rules.NoRecipeStew != null ? rules.NoRecipeStew.name : spec.NoRecipeStew.Trim()) + Game(known && PotKnobs.GameNoRecipe != null ? PotKnobs.GameNoRecipe.name : null));
            if (spec.RejectionForce.HasValue)
                parts.Add("RejectionForce " + Claims.Number(spec.RejectionForce.Value) + Game(known ? Claims.Number(PotKnobs.GamePush) : null));
            return string.Join(", ", parts.ToArray());
        }

        static string Game(string value) => value == null ? "" : " (game " + value + ")";

        static List<string> Warnings()
        {
            List<string> warnings = new List<string>();
            float worst = PotKnobs.WorstFeed(false);
            float game = PotKnobs.WorstFeed(true);
            if (worst > game + 0.001f)
                warnings.Add("a full pot's stew can carry Feed " + Claims.Number(worst) + ", past the game's own " + Claims.Number(game) + ", so players are refused stews at a fullness the game allows");
            if (PotKnobs.LiveTemperature > 95f)
                warnings.Add("stewing above 95 may never happen if the pot water can't get that hot");

            NetworkPrefab noRecipe = PotKnobs.LiveNoRecipe;
            if (noRecipe != null && noRecipe != PotKnobs.GameNoRecipe)
            {
                List<StewRecipe> recipes = StewStages.Get(noRecipe.Hash)?.Recipes ?? new List<StewRecipe>();
                if (recipes.Count > 0)
                    warnings.Add("the no recipe stew " + noRecipe.name + " is the look of " + string.Join(", ", recipes.Select(RecipeName).ToArray()) + ", so a pot that matches no recipe shows that stew and gives none of its effects");
            }

            if (PotKnobs.LiveGlobal.Length == 0)
            {
                HashSet<LiquidVisualData> stewLooks = new HashSet<LiquidVisualData>(StewStages.All.Where(e => e.Stew != null && e.Stew.LiquidVisualData != null).Select(e => e.Stew.LiquidVisualData));
                List<string> dyes = Liquids.All.Where(l => l.Error == null && !l.IsShared && l.Spec.ColorsStews && l.Data != null && stewLooks.Contains(l.Data.LiquidVisual)).Select(l => l.Id).ToList();
                if (dyes.Count > 0)
                    warnings.Add("stews get no global effect, so a stew dyed by " + string.Join(", ", dyes.ToArray()) + " (a stew look) can pass for that dye after a restart; give the dye another look or keep an effect in GlobalEffects");
            }
            return warnings;
        }

        static void Reachable(StringBuilder text, bool full)
        {
            if (!Reachability.Known)
                return;

            List<StewRecipe> unreachable = Reachability.Unreachable();
            if (full)
            {
                text.Append("pots lock at ").Append(Reachability.CountsText()).Append(" ingredients").Append(Reachability.CountChanged ? ", random recipe pages skip recipes no pot can make" : "").Append('\n');
                foreach (StewRecipe recipe in unreachable)
                    text.Append("  can't normally be made: ").Append(RecipeName(recipe)).Append(", ").Append(Reachability.SlotsOf(recipe)).Append(" slots\n");
                return;
            }

            if (unreachable.Count == 0)
                return;
            int game = unreachable.Count(r => StewRecipes.Find(r) == null);
            List<string> parts = new List<string>();
            if (game > 0)
                parts.Add(game + " game recipes");
            parts.AddRange(unreachable.Select(StewRecipes.Find).Where(r => r != null).Select(r => r.Id));
            text.Append("can't normally be made at ").Append(Reachability.CountsText()).Append(" ingredients: ").Append(string.Join(", ", parts.ToArray())).Append(" (cookinglib pots lists them)\n");
        }

        static void Reloads(StringBuilder text)
        {
            text.Append("pot reloads: ");
            if (!ServerPatches.PotReloadOn)
            {
                text.Append("off, the reload patch failed to apply\n");
                return;
            }
            text.Append(PotReloadPatch.Resumed).Append(" resumed, ").Append(PotReloadPatch.WithRecipe).Append(" with a recipe, ")
                .Append(PotReloadPatch.BaseFallbacks).Append(" bases missing and set to Water, ").Append(PotReloadPatch.Skipped).Append(" skipped\n");
        }

        static void ClaimLines(StringBuilder text)
        {
            if (Claims.Held.Count == 0 && Claims.Refused.Count == 0)
                return;

            text.Append("claims: ").Append(Claims.Held.Count).Append(" held, ").Append(Claims.Refused.Count).Append(" refused\n");
            foreach (Claims.Holding holding in Claims.Held)
                text.Append("  ").Append(holding.Label).Append(" = ").Append(holding.Value).Append(", set by ").Append(string.Join(", ", holding.Holders.ToArray())).Append('\n');
            foreach (Claims.Refusal refusal in Claims.Refused)
            {
                text.Append("  refused: ").Append(refusal.Id).Append(" wanted ").Append(refusal.Label).Append(" = ").Append(refusal.Value)
                    .Append(", held by ").Append(refusal.Owner).Append(" = ").Append(refusal.OwnerValue).Append('\n');
            }
        }

        static void Loaded(StringBuilder text)
        {
            CookingPot[] pots = UnityEngine.Object.FindObjectsOfType<CookingPot>();
            text.Append("loaded pots: ").Append(pots.Length).Append('\n');
            foreach (CookingPot pot in pots)
                text.Append("  ").Append(Safe.Run("pots line", () => Line(pot), "unreadable pot")).Append('\n');
        }

        static string Line(CookingPot pot)
        {
            NetworkPrefab prefab = pot.Entity != null ? pot.Entity.Prefab : null;
            Vector3 at = pot.transform.position;
            StringBuilder line = new StringBuilder(prefab != null ? prefab.name + " " + prefab.Hash : pot.transform.root.name);
            line.Append(" at ").Append(string.Format(CultureInfo.InvariantCulture, "{0:0.0} {1:0.0} {2:0.0}", at.x, at.y, at.z)).Append(", ").Append(potStage(pot));
            if (pot.CookingLiquid != null)
                line.Append(", ").Append(Claims.Number(pot.CookingLiquid.Temperature)).Append(" degrees");

            PotDefinition definition = pot.Definition;
            StewManager manager = potManager(pot);
            if (manager != null)
            {
                LiquidDefinition baseLiquid = manager.StewLiquidBase;
                LibLiquid ours = Liquids.FindByDefinition(baseLiquid);
                line.Append(", base ").Append(ours != null ? ours.Id : baseLiquid != null ? baseLiquid.name : "none");
                line.Append(", ingredients ").Append(Items(manager.Ingredients, definition == null ? "?" : definition.MinNumberOfIngredients.ToString()));
                line.Append(", garnishes ").Append(Items(manager.Garnishes, definition == null ? "?" : definition.NumberOfGarnishes.ToString()));
                StewComponent stew = managerStew(manager);
                NetworkPrefab look = stew != null && stew.Entity != null ? stew.Entity.Prefab : null;
                line.Append(", stew ").Append(look != null ? look.name : "none").Append(", recipe ").Append(RecipeName(managerRecipe(manager)));
            }

            LiquidContent content = pot.CookingLiquid != null && pot.CookingLiquid.LiquidContainer != null ? pot.CookingLiquid.LiquidContainer.ContainedLiquid : null;
            if (!ReferenceEquals(content, null) && content.Data != null)
                line.Append(", liquid ").Append(Claims.Effects(content.Data.Effects));
            if (definition == null)
                line.Append(", no pot definition");
            else if (!PotKnobs.IsKnown(definition))
                line.Append(", stray pot definition ").Append(definition.name).Append(", pot rules don't reach it");
            return line.ToString();
        }

        static string Items<T>(List<T> items, string cap) where T : CookableItem
        {
            List<string> names = (items ?? new List<T>()).Select(item => item == null || item.Item == null ? "?" : item.Item.name).ToList();
            return names.Count + " of " + cap + (names.Count == 0 ? "" : " (" + string.Join(", ", names.ToArray()) + ")");
        }

        static string RecipeName(StewRecipe recipe)
        {
            if (recipe == null)
                return "none";
            LibStewRecipe ours = StewRecipes.Find(recipe);
            return ours != null ? ours.Id : recipe.name;
        }

        static void Mouths(StringBuilder text)
        {
            int found = 0;
            foreach (NetworkPrefab prefab in PrefabWalk.All())
            {
                foreach (ConsumptionManager consumer in prefab.GetComponentsInChildren<ConsumptionManager>(true))
                {
                    found++;
                    text.Append("drink gate on ").Append(prefab.name).Append(" ").Append(consumer.name).Append(": ")
                        .Append(string.Join(", ", mouthFields.Select(name => name + " " + Field(consumer, name)).ToArray())).Append('\n');
                }
            }
            if (found == 0)
                text.Append("drink gate: no prefab carries a ConsumptionManager\n");
        }

        static string Field(ConsumptionManager consumer, string name)
        {
            FieldInfo field = typeof(ConsumptionManager).GetField(name, PrivateInstance);
            return field == null ? "?" : Show(field.GetValue(consumer));
        }

        static string Show(object value)
        {
            if (value is UnityEngine.Object asset)
                return asset == null ? "none" : asset.name;
            if (value is float number)
                return Claims.Number(number);
            return value == null ? "none" : value.ToString();
        }
    }
}
