using System;
using System.Collections.Generic;
using Il2Cpp.Alta.Cooking;
using Il2Cpp.Alta.Liquid;
using HarmonyLib;

namespace CookingLib
{
    internal static class StewMatchPatch
    {
        internal static int WaterMatches { get; private set; }

        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(StewRecipe), nameof(StewRecipe.FindStewRecipeForIngredients)),
                postfix: new HarmonyMethod(typeof(StewMatchPatch), nameof(Postfix)));
        }

        static void Postfix(LiquidDefinition liquid, IEnumerable<CookableItem> ingredients, ref StewRecipe __result)
        {
            try
            {
                if (__result != null || liquid == null)
                    return;

                LibLiquid ours = Liquids.FindByDefinition(liquid);
                if (ours == null || !ours.Spec.CountsAsWater)
                    return;

                LiquidDefinition water = LiquidDefinition.Get(LiquidFactory.WaterHash);
                if (water == null)
                    return;

                StewRecipe recipe = StewRecipe.FindStewRecipeForIngredients(water, ingredients);
                if (recipe != null)
                {
                    __result = recipe;
                    WaterMatches++;
                }
            }
            catch (Exception e)
            {
                Safe.Fail(nameof(StewMatchPatch), e);
            }
        }
    }
}
