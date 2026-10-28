using System;
using HarmonyLib;

namespace CookingLib
{
    internal static class ServerPatches
    {
        internal static bool PagesGuarded { get; private set; }

        internal static bool StewContentOn { get; private set; }

        internal static bool StewMatchOn { get; private set; }

        internal static bool StewEventsOn { get; private set; }

        internal static bool DrinksOn { get; private set; }

        internal static bool MouthFilterOn { get; private set; }

        internal static bool EatsOn { get; private set; }

        internal static bool SpreadLoadOn { get; private set; }

        internal static bool PotReloadOn { get; private set; }

        internal static bool PrefabMapOn { get; private set; }

        internal static bool RoastOn { get; private set; }

        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            PagesGuarded = Run(PageGuardPatch.Apply, harmony);
            Run(PotBasePatch.Apply, harmony);
            StewContentOn = Run(StewContentPatch.Apply, harmony);
            StewMatchOn = Run(StewMatchPatch.Apply, harmony);
            StewEventsOn = Run(StewEventPatch.Apply, harmony);
            Run(InfusionPatch.Apply, harmony);
            DrinksOn = Run(DrinkPatch.Apply, harmony);
            MouthFilterOn = Run(MouthFilterPatch.Apply, harmony);
            EatsOn = Run(EatPatch.Apply, harmony);
            SpreadLoadOn = Run(SpreadLoadPatch.Apply, harmony);
            PotReloadOn = Run(PotReloadPatch.Apply, harmony);
            PrefabMapOn = Run(PrefabMapPatch.Apply, harmony);
            RoastOn = Run(RoastPatch.Apply, harmony);
        }

        static bool Run(Action<HarmonyLib.Harmony> apply, HarmonyLib.Harmony harmony)
        {
            try
            {
                apply(harmony);
                return true;
            }
            catch (Exception e)
            {
                Log.Error(apply.Method.DeclaringType.Name + " failed to apply: " + e);
                return false;
            }
        }
    }
}
