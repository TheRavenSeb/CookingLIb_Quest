using System;
using Il2Cpp.Alta.Cooking;
using Il2Cpp.Alta.Liquid;
using HarmonyLib;

namespace CookingLib
{
    internal static class PotBasePatch
    {
        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(CookingPot), "StartWarmingUpStage"),
                prefix: new HarmonyMethod(typeof(PotBasePatch), nameof(Prefix)));
        }

        static void Prefix(CookingPot __instance, ref bool isSettingLiquidOfStewManager, StewManager ___stewManager)
        {
            try
            {
                if (!isSettingLiquidOfStewManager || ___stewManager == null)
                    return;

                if (___stewManager.Ingredients.Count > 0)
                {
                    isSettingLiquidOfStewManager = false;
                    return;
                }

                LiquidContent content = __instance.CookingLiquid.LiquidContainer.ContainedLiquid;
                if (content == null || !content.IsCustom)
                    return;

                LibLiquid liquid = Liquids.FindServerOnly(content.Data);
                if (liquid != null)
                    ___stewManager.SetBaseLiquid(liquid.BaseDefinition);
            }
            catch (Exception e)
            {
                Safe.Fail(nameof(PotBasePatch), e);
            }
        }
    }
}
