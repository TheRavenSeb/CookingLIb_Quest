using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Il2CppAlta.Alchemy;
using Il2CppAlta.Inventory;
using HarmonyLib;

namespace CookingLib
{
    internal static class EatPatch
    {
        static readonly ConditionalWeakTable<ConsumptionManager, List<EdibleObject>> eating = new ConditionalWeakTable<ConsumptionManager, List<EdibleObject>>();

        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(EdibleObject), nameof(EdibleObject.StartEat)),
                prefix: new HarmonyMethod(typeof(EatPatch), nameof(StartPrefix)));
            harmony.Patch(AccessTools.Method(typeof(ConsumptionManager), "HandleConsumptionEffects"),
                postfix: new HarmonyMethod(typeof(EatPatch), nameof(EffectsPostfix)));
        }

        static void StartPrefix(EdibleObject __instance, ConsumptionManager consumer)
        {
            try
            {
                if (consumer == null || !__instance.enabled)
                    return;

                List<EdibleObject> edibles = eating.GetOrCreateValue(consumer);
                edibles.RemoveAll(edible => edible == null || edible == __instance);
                edibles.Add(__instance);
            }
            catch (Exception e)
            {
                Safe.Fail(nameof(EatPatch) + " start", e);
            }
        }

        static void EffectsPostfix(ConsumptionManager __instance, IEnumerable<EffectDefinitionApplication> effects, bool __runOriginal)
        {
            try
            {
                if (!__runOriginal || effects == null || !eating.TryGetValue(__instance, out List<EdibleObject> edibles))
                    return;
                EdibleObject edible = edibles.FirstOrDefault(e => e != null && ReferenceEquals(e.ConsumptionEffects, effects));
                if (edible == null)
                    return;

                edibles.Remove(edible);
                Player player = PlayerIds.Of(__instance);
                Pickup pickup = edible.GetComponent<Pickup>();
                Item item = pickup == null ? null : pickup.Item;
                Drinks.Eat(new EatArgs(player, PlayerIds.InfoOf(player))
                {
                    Item = item,
                    Effects = effects.Where(effect => effect != null).Select(effect => effect.Copy()).ToList().AsReadOnly(),
                    Edible = Edibles.Find(item)
                });
            }
            catch (Exception e)
            {
                Safe.Fail(nameof(EatPatch) + " Ate", e);
            }
        }
    }
}
