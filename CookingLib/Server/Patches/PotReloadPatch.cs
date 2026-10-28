using System;
using System.Linq;
using Il2Cpp.Alta.Cooking;
using Il2Cpp.Alta.Liquid;
using Il2Cpp.Alta.Networking;
using HarmonyLib;
using UnityEngine;

namespace CookingLib
{
    internal static class PotReloadPatch
    {
        static readonly AccessTools.FieldRef<CookingPot, CookingPot.CookingStage> potStage = AccessTools.FieldRefAccess<CookingPot, CookingPot.CookingStage>("currentStage");
        static readonly AccessTools.FieldRef<CookingPot, StewManager> potManager = AccessTools.FieldRefAccess<CookingPot, StewManager>("stewManager");
        static readonly AccessTools.FieldRef<StewManager, LiquidDefinition> managerBase = AccessTools.FieldRefAccess<StewManager, LiquidDefinition>("stewLiquidBase");
        static readonly AccessTools.FieldRef<StewManager, StewRecipe> managerRecipe = AccessTools.FieldRefAccess<StewManager, StewRecipe>("currentRecipe");
        static readonly AccessTools.FieldRef<StewManager, bool> managerRealRecipe = AccessTools.FieldRefAccess<StewManager, bool>("isRealRecipe");
        static readonly AccessTools.FieldRef<StewManager, NetworkEntity> managerSurface = AccessTools.FieldRefAccess<StewManager, NetworkEntity>("liquidSurfaceEntity");

        static bool baseLogged;

        internal static int Resumed { get; private set; }

        internal static int WithRecipe { get; private set; }

        internal static int BaseFallbacks { get; private set; }

        internal static int Skipped { get; private set; }

        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(CookingPot), "LoadAtCurrentStage"),
                prefix: new HarmonyMethod(typeof(PotReloadPatch), nameof(Prefix)));
        }

        static void Prefix(CookingPot __instance)
        {
            try
            {
                Resume(__instance);
            }
            catch (Exception e)
            {
                Skipped++;
                Safe.Fail(nameof(PotReloadPatch), e);
                Safe.Run(nameof(PotReloadPatch) + " base", () => KeepBase(__instance));
            }
            Safe.Run(nameof(PotReloadPatch) + " stews", () => VanillaStews.CopyOnto(__instance));
        }

        static void KeepBase(CookingPot pot)
        {
            StewManager manager = potManager(pot);
            if (potStage(pot) == CookingPot.CookingStage.Preparation && manager != null && managerBase(manager) == null && manager.Ingredients.Count > 0)
                UseWater(manager, LiquidDefinition.Get(LiquidFactory.WaterHash));
        }

        static void Resume(CookingPot pot)
        {
            StewManager manager = potManager(pot);
            if (potStage(pot) != CookingPot.CookingStage.Preparation || manager == null || pot.CookingLiquid == null
                || !pot.CookingLiquid.IsNotEmpty || manager.Ingredients.Count == 0)
                return;

            Main.WorldUp();
            LiquidDefinition water = managerBase(manager) == null ? LiquidDefinition.Get(LiquidFactory.WaterHash) : null;
            LiquidDefinition baseLiquid = water != null ? water : managerBase(manager);
            if (baseLiquid == null || !StewStages.Ready() || StewStages.DefaultRaw == 0 || pot.Definition == null)
            {
                Skip(manager, water);
                return;
            }

            StewComponent stew = Pick(pot, manager);
            uint stewHash = stew != null && stew.Entity != null && stew.Entity.Prefab != null ? stew.Entity.Prefab.Hash : 0;
            if (stewHash == 0)
            {
                Skip(manager, water);
                return;
            }

            bool checkRan = manager.Ingredients.Count >= pot.Definition.MinNumberOfIngredients;
            bool changedStage = stewHash != StewStages.DefaultRaw;
            StewRecipe recipe = checkRan || changedStage ? StewRecipe.FindStewRecipeForIngredients(baseLiquid, manager.Ingredients) : null;
            if (!StewStages.InChain(stewHash, recipe))
                recipe = null;

            if (!MoveFirst(pot, stew))
            {
                Skip(manager, water);
                return;
            }

            UseWater(manager, water);
            managerRecipe(manager) = recipe;
            managerRealRecipe(manager) = recipe != null;
            potStage(pot) = changedStage || recipe != null ? CookingPot.CookingStage.StewLocked : CookingPot.CookingStage.Stewing;
            Resumed++;
            if (recipe != null)
                WithRecipe++;
        }

        static StewComponent Pick(CookingPot pot, StewManager manager)
        {
            NetworkEntity surface = managerSurface(manager);
            StewComponent[] stews = pot.GetComponentsInChildren<StewComponent>(true);
            if (surface == null || stews.Length == 0 || stews.Any(stew => stew.transform.parent != surface.transform))
                return null;

            LiquidContent content = pot.CookingLiquid.LiquidContainer.ContainedLiquid;
            LiquidVisualData look = !ReferenceEquals(content, null) && content.Data != null ? content.Data.LiquidVisual : null;
            StewComponent picked = look == null ? null : stews.LastOrDefault(stew => stew.LiquidVisualData == look);
            return picked != null ? picked : stews[stews.Length - 1];
        }

        static bool MoveFirst(CookingPot pot, StewComponent stew)
        {
            Transform moved = stew.transform;
            int index = moved.GetSiblingIndex();
            moved.SetAsFirstSibling();
            if (pot.GetComponentInChildren<StewComponent>(true) == stew)
                return true;
            moved.SetSiblingIndex(index);
            return false;
        }

        static void Skip(StewManager manager, LiquidDefinition water)
        {
            UseWater(manager, water);
            Skipped++;
        }

        static void UseWater(StewManager manager, LiquidDefinition water)
        {
            if (water == null)
                return;

            managerBase(manager) = water;
            BaseFallbacks++;
            if (baseLogged)
                return;
            baseLogged = true;
            Log.Warn("pots: base missing, using Water");
        }
    }
}
