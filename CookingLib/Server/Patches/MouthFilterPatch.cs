using System;
using System.Runtime.CompilerServices;
using Il2Cpp.Alta.Liquid;
using HarmonyLib;
using UnityEngine;

namespace CookingLib
{
    internal static class MouthFilterPatch
    {
        const float PourGap = 2f;

        static readonly ConditionalWeakTable<PlayerDrinkingMouth, Mouth> mouths = new ConditionalWeakTable<PlayerDrinkingMouth, Mouth>();

        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(AccessTools.DeclaredMethod(typeof(PlayerDrinkingMouth), nameof(PlayerDrinkingMouth.Initialize)),
                postfix: new HarmonyMethod(typeof(MouthFilterPatch), nameof(Postfix)));
        }

        static void Postfix(PlayerDrinkingMouth __instance, ILiquidContainer ___container, ConsumptionManager ___consumer)
        {
            try
            {
                if (___container == null || mouths.TryGetValue(__instance, out _))
                    return;

                Mouth mouth = new Mouth(___consumer);
                mouths.Add(__instance, mouth);
                ___container.AddFilter += mouth.Filter;
            }
            catch (Exception e)
            {
                Safe.Fail(nameof(MouthFilterPatch), e);
            }
        }

        sealed class Mouth
        {
            readonly ConsumptionManager consumer;
            int frame = -1;
            LiquidData checkedData;
            bool allowed = true;
            LiquidData refusedData;
            float refusedAt = float.MinValue;

            internal Mouth(ConsumptionManager consumer)
            {
                this.consumer = consumer;
            }

            internal void Filter(LiquidData data, ref bool result)
            {
                try
                {
                    if (!Drinks.Checking || data is null)
                        return;

                    if (frame != Time.frameCount || !ReferenceEquals(checkedData, data))
                    {
                        frame = Time.frameCount;
                        checkedData = data;
                        allowed = Decide(data);
                    }
                    if (allowed)
                        return;

                    result = false;
                    if (!ReferenceEquals(refusedData, data) || Time.time - refusedAt > PourGap)
                        Drinks.RefusedPours++;
                    refusedData = data;
                    refusedAt = Time.time;
                }
                catch (Exception e)
                {
                    Safe.Fail(nameof(MouthFilterPatch) + " CanDrink", e);
                }
            }

            bool Decide(LiquidData data)
            {
                Player player = PlayerIds.Of(consumer);
                UserInfoAndRole info = PlayerIds.InfoOf(player);
                if (info == null)
                    return true;
                return Drinks.Allows(new DrinkCheck(player, info, Liquids.Identify(data)));
            }
        }
    }
}
