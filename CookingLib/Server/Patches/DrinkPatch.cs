using System;
using Il2CppAlta.Liquid;
using HarmonyLib;

namespace CookingLib
{
    internal static class DrinkPatch
    {
        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(PlayerDrinkingMouth), "LiquidAmountChanged"),
                prefix: new HarmonyMethod(typeof(DrinkPatch), nameof(Prefix)) { priority = Priority.First },
                postfix: new HarmonyMethod(typeof(DrinkPatch), nameof(Postfix)) { priority = Priority.Last });
        }

        static void Prefix(ILiquidContainer container, int oldLevel, int newLevel, ConsumptionManager ___consumer, out DrinkArgs __state)
        {
            __state = null;
            try
            {
                if (container == null || newLevel <= oldLevel)
                    return;

                Player player = PlayerIds.Of(___consumer);
                LiquidContent content = container.ContainedLiquid;
                __state = new DrinkArgs(player, PlayerIds.InfoOf(player))
                {
                    Units = newLevel - oldLevel,
                    MouthSize = container.ContainerSize,
                    Taken = content is null ? null : content.Data
                };
            }
            catch (Exception e)
            {
                Safe.Fail(nameof(DrinkPatch) + " Drank", e);
            }
        }

        static void Postfix(ILiquidContainer container, int newLevel, DrinkArgs __state)
        {
            try
            {
                if (__state != null && container.CurrentContentLevel < newLevel)
                    Drinks.Drink(__state);
            }
            catch (Exception e)
            {
                Safe.Fail(nameof(DrinkPatch) + " Drank", e);
            }
        }
    }
}
