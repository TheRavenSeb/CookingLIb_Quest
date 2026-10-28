using System;
using System.Reflection;
using Il2Cpp.Alta.Networking;
using HarmonyLib;

namespace CookingLib
{
    internal static class PrefabMapPatch
    {
        static readonly FieldInfo mapField = typeof(PrefabManager).GetField("prefabMap", BindingFlags.NonPublic | BindingFlags.Static);

        internal static bool BuiltEarly { get; private set; }

        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            if (mapField == null)
                throw new MissingFieldException("PrefabManager", "prefabMap");
            BuiltEarly = mapField.GetValue(null) != null;
            harmony.Patch(AccessTools.Method(typeof(PrefabManager), nameof(PrefabManager.PrepareSpawnSetups)),
                postfix: new HarmonyMethod(typeof(PrefabMapPatch), nameof(Postfix)) { priority = Priority.First });
            if (BuiltEarly)
                Edibles.MapReady(true);
        }

        static void Postfix()
        {
            if (Edibles.Ready)
                return;

            try
            {
                Edibles.MapReady(false);
            }
            catch (Exception e)
            {
                Safe.Fail(nameof(PrefabMapPatch), e);
            }
        }
    }
}
