using System;
using System.Linq;
using System.Reflection;
using Il2Cpp.Alta.Liquid;
using HarmonyLib;

namespace CookingLib
{
    internal static class SpreadLoadPatch
    {
        static readonly FieldInfo effectsField = typeof(LiquidData).GetField("effects", BindingFlags.NonPublic | BindingFlags.Instance);

        internal static int Restored { get; private set; }

        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            if (effectsField == null)
                throw new MissingFieldException("LiquidData", "effects");
            harmony.Patch(AccessTools.Method(typeof(LiquidData), nameof(LiquidData.Load)),
                postfix: new HarmonyMethod(typeof(SpreadLoadPatch), nameof(Postfix)));
        }

        static void Postfix(LiquidData __instance)
        {
            try
            {
                if (!Liquids.ServerOnlySpreads)
                    return;

                LibLiquid liquid = Liquids.FindServerOnlyByFingerprint(__instance);
                if (liquid == null || !EffectSpread.Any(liquid.Spec.Effects))
                    return;

                effectsField.SetValue(__instance, liquid.Data.Effects.ToArray());
                Restored++;
            }
            catch (Exception e)
            {
                Safe.Fail(nameof(SpreadLoadPatch), e);
            }
        }
    }
}
