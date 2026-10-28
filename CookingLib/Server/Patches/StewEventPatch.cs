using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppAlta.Cooking;
using Il2CppAlta.Liquid;
using Il2CppAlta.Networking;
using HarmonyLib;
using UnityEngine;

namespace CookingLib
{
    internal static class StewEventPatch
    {
        static readonly AccessTools.FieldRef<CookingPot, CookingPot.CookingStage> potStage = AccessTools.FieldRefAccess<CookingPot, CookingPot.CookingStage>("currentStage");

        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(StewManager), "CheckForValidRecipe"),
                prefix: new HarmonyMethod(typeof(StewEventPatch), nameof(CheckPrefix)),
                postfix: new HarmonyMethod(typeof(StewEventPatch), nameof(CheckPostfix)));
            harmony.Patch(AccessTools.Method(typeof(StewManager), "HandleNewStewStage"),
                prefix: new HarmonyMethod(typeof(StewEventPatch), nameof(StagePrefix)),
                postfix: new HarmonyMethod(typeof(StewEventPatch), nameof(StagePostfix)));
        }

        static void CheckPrefix(CookingPot ___pot, out bool __state)
        {
            __state = false;
            try
            {
                if (StewEvents.WantsMade)
                    __state = IsLocked(___pot);
            }
            catch (Exception e)
            {
                Safe.Fail(nameof(StewEventPatch) + " Made", e);
            }
        }

        static void CheckPostfix(StewManager __instance, CookingPot ___pot, StewComponent ___stew, StewRecipe ___currentRecipe, List<Player> ___cookingPlayers, bool __state)
        {
            try
            {
                if (StewEvents.WantsMade)
                    StewEvents.QueueMade(Snapshot(__instance, ___pot, ___stew, ___currentRecipe, ___cookingPlayers, false, __state));
            }
            catch (Exception e)
            {
                Safe.Fail(nameof(StewEventPatch) + " Made", e);
            }
        }

        static void StagePrefix(NetworkEntity newStewEntity, StewManager __instance, CookingPot ___pot, StewComponent ___stew, StewRecipe ___currentRecipe, List<Player> ___cookingPlayers, out Before __state)
        {
            __state = null;
            try
            {
                if (!StewEvents.Wanted)
                    return;

                __state = new Before { WasLocked = IsLocked(___pot) };
                if (newStewEntity == null)
                    __state.Gone = Snapshot(__instance, ___pot, ___stew, ___currentRecipe, ___cookingPlayers, true, false);
            }
            catch (Exception e)
            {
                Safe.Fail(nameof(StewEventPatch) + " StageChanged", e);
            }
        }

        static void StagePostfix(NetworkEntity newStewEntity, StewManager __instance, CookingPot ___pot, StewComponent ___stew, StewRecipe ___currentRecipe, List<Player> ___cookingPlayers, Before __state)
        {
            try
            {
                if (__state == null)
                    return;

                if (newStewEntity == null)
                {
                    if (__state.Gone != null && StewEvents.WantsStageChanged)
                        StewEvents.QueueStageChanged(__state.Gone);
                    return;
                }

                StewEvent args = Snapshot(__instance, ___pot, ___stew, ___currentRecipe, ___cookingPlayers, false, false);
                if (!__state.WasLocked && StewEvents.WantsMade)
                    StewEvents.QueueMade(args);
                if (StewEvents.WantsStageChanged)
                    StewEvents.QueueStageChanged(args);
            }
            catch (Exception e)
            {
                Safe.Fail(nameof(StewEventPatch) + " StageChanged", e);
            }
        }

        static bool IsLocked(CookingPot pot) => pot != null && potStage(pot) == CookingPot.CookingStage.StewLocked;

        static StewEvent Snapshot(StewManager manager, CookingPot pot, StewComponent stew, StewRecipe recipe, List<Player> players, bool burntAway, bool isRecheck)
        {
            List<StewCook> addedBy = (players ?? new List<Player>()).Select(player => new StewCook(player)).ToList();
            List<CookableItem> ingredients = manager.Ingredients ?? new List<CookableItem>();
            NetworkPrefab look = stew != null && stew.Entity != null ? stew.Entity.Prefab : null;
            StewStages.Entry entry = look == null ? null : StewStages.Get(look.Hash);
            LibStewRecipe ours = StewRecipes.Find(recipe);
            LiquidDefinition baseLiquid = manager.StewLiquidBase;
            LibLiquid lib = Liquids.FindByDefinition(baseLiquid);

            return new StewEvent
            {
                Pot = pot,
                PotId = pot != null && pot.Entity != null ? pot.Entity.Identifier : 0,
                Position = pot != null ? pot.transform.position : Vector3.zero,
                Outcome = Outcome(pot, recipe, ingredients.Count),
                Recipe = ours != null ? ours.Id : recipe != null ? recipe.name : null,
                IsCookingLibRecipe = ours != null,
                Stage = burntAway ? StewStage.BurntAway : entry != null ? entry.Stage : StewStage.Raw,
                StewLook = look == null ? null : look.name,
                IsAwardingExperienceStage = !burntAway && stew != null && stew.IsAwardingExperienceStage,
                BaseLiquid = lib != null ? lib.Id : baseLiquid != null ? baseLiquid.name : null,
                Ingredients = ItemNames(ingredients),
                Garnishes = ItemNames(manager.Garnishes),
                AddedBy = addedBy.AsReadOnly(),
                Cooks = addedBy.Where(cook => cook.Player != null).GroupBy(cook => cook.UserId).Select(group => group.First()).ToList().AsReadOnly(),
                CarriedOver = Math.Max(0, ingredients.Count - addedBy.Count),
                IsRecheck = isRecheck,
                Content = burntAway || pot == null ? null : Copy(pot.CookingLiquid.LiquidContainer.ContainedLiquid)
            };
        }

        static StewOutcome Outcome(CookingPot pot, StewRecipe recipe, int count)
        {
            if (recipe != null)
                return StewOutcome.Recipe;
            return pot != null && pot.Definition != null && count >= pot.Definition.MinNumberOfIngredients ? StewOutcome.NoRecipe : StewOutcome.Unfinished;
        }

        static IReadOnlyList<string> ItemNames<T>(List<T> items) where T : CookableItem =>
            (items ?? new List<T>()).Select(item => item == null || item.Item == null ? null : item.Item.name).ToList().AsReadOnly();

        static LiquidContent Copy(LiquidContent content) =>
            ReferenceEquals(content, null) ? null : new LiquidContent { IsCustom = content.IsCustom, Liquid = content.Liquid, Data = content.Data };

        internal sealed class Before
        {
            internal bool WasLocked;

            internal StewEvent Gone;
        }
    }
}
