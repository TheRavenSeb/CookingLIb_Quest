using System;
using Il2CppAlta.Cooking;
using Il2CppAlta.Liquid;
using HarmonyLib;
using UnityEngine;

namespace CookingLib
{
    internal static class StewContentPatch
    {
        internal static int DyeTintWrites { get; private set; }

        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(StewManager), "CreateContentWithTint"),
                prefix: new HarmonyMethod(typeof(StewContentPatch), nameof(Prefix)),
                postfix: new HarmonyMethod(typeof(StewContentPatch), nameof(Postfix)));
        }

        internal static LiquidRef ResultOf(StewRecipe recipe) => StewRecipes.Find(recipe)?.Result ?? VanillaRecipes.OverrideOf(recipe)?.Result;

        static void Prefix(StewManager __instance, StewRecipe ___currentRecipe, ref Color colorTint, out bool __state)
        {
            __state = false;
            try
            {
                LibStewRecipe ours = StewRecipes.Find(___currentRecipe);
                VanillaRecipes.Override edit = ours == null ? VanillaRecipes.OverrideOf(___currentRecipe) : null;
                Color? tint = ours != null ? ours.Spec.Tint : edit?.Tint;
                if (tint.HasValue)
                    colorTint = tint.Value;
                if (ours != null && ours.Spec.IgnoresDyes || edit != null && edit.IgnoresDyes || ResultOf(___currentRecipe) != null)
                    return;

                LibLiquid dye = Liquids.FindByDefinition(__instance.StewLiquidBase);
                if (dye != null && dye.Spec.ColorsStews)
                {
                    colorTint = dye.Data.ColorTint;
                    __state = true;
                }
            }
            catch (Exception e)
            {
                Safe.Fail(nameof(StewContentPatch) + " tint", e);
            }
        }

        static void Postfix(StewManager __instance, StewRecipe ___currentRecipe, StewComponent ___stew, ref LiquidContent __result, bool __state)
        {
            try
            {
                LiquidRef result = ResultOf(___currentRecipe);
                if (result != null)
                {
                    __result = result.CreateContent();
                    return;
                }

                if (__state)
                    DyeTintWrites++;
                LiquidIdentity.Tag(__result, ___currentRecipe, ___stew, __instance.Ingredients);
            }
            catch (Exception e)
            {
                Safe.Fail(nameof(StewContentPatch) + " content", e);
            }
        }
    }
}
