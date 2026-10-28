using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppAlta;
using Il2CppAlta.Cooking;
using Il2CppAlta.Pages;
using HarmonyLib;

namespace CookingLib
{
    internal static class PageGuardPatch
    {
        internal static readonly HashSet<StewRecipe> Disabled = new HashSet<StewRecipe>();

        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(StewRecipePageProvider), nameof(StewRecipePageProvider.GetRandom)),
                prefix: new HarmonyMethod(typeof(PageGuardPatch), nameof(Prefix)));
        }

        internal static bool IsCandidate(StewRecipe recipe) =>
            recipe != null && !IsOwned(recipe) && !Disabled.Contains(recipe) && (!Reachability.CountChanged || Reachability.IsReachable(recipe));

        static bool IsOwned(StewRecipe recipe)
        {
            LibStewRecipe ours = StewRecipes.Find(recipe);
            return ours != null && !(ours.IsShared && ours.HasPage);
        }

        static bool Prefix(StewRecipePageProvider __instance, ref PageData __result)
        {
            __result = null;
            try
            {
                List<StewRecipe> candidates = HashedGeneralValue<StewRecipe>.All.Where(IsCandidate).ToList();
                if (candidates.Count == 0)
                    return false;

                StewRecipe pick = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                if (GlobalPagesManager.IsNew(__instance.Identifier, pick.Hash))
                    __result = __instance.GeneratePage(pick);
            }
            catch (Exception e)
            {
                __result = null;
                Safe.Fail(nameof(PageGuardPatch), e);
            }
            return false;
        }
    }
}
