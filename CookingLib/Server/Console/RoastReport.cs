using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Il2CppAlta.Cooking;
using Il2CppAlta.Networking;

namespace CookingLib
{
    internal static class RoastReport
    {
        internal static void Status(StringBuilder text)
        {
            int handlers = Roasting.RoastedHandlers;
            text.Append("roasting: ").Append(Roasting.All.Count).Append(", Roasted ").Append(handlers == 1 ? "1 handler" : handlers + " handlers")
                .Append(" (fired ").Append(Roasting.RoastedFired).Append(')')
                .Append(ServerPatches.RoastOn ? "" : ", off, the roasting patches failed to apply").Append('\n');
            foreach (LibRoasting rule in Roasting.All)
            {
                string target = rule.Prefab != null ? rule.Prefab.name : rule.Spec?.Item;
                string state = rule.Error != null ? "refused: " + rule.Error
                    : rule.IsReady ? "ready, " + Roasting.Describe(rule) + ", cooked " + rule.TimesCooked + ", burnt " + rule.TimesBurnt + ", held in pots " + rule.Held.Count + Shared(rule)
                    : "waiting for the world";
                text.Append("  ").Append(rule.Id).Append("  ").Append(target).Append(": ").Append(state).Append('\n');
            }
            foreach (string cycle in Roasting.Cycles())
                text.Append("  warning: ").Append(cycle).Append('\n');
        }

        internal static void Dump(StringBuilder text)
        {
            List<NetworkPrefab> prefabs = PrefabWalk.All().OrderBy(p => p.name, System.StringComparer.OrdinalIgnoreCase).ToList();
            text.Append("== Roasting (prefab and number; cook and burn: flag and what it turns into; weak, optimal and burning temperature and seconds; instant burn; stew; Cookables; embedded entities) ==\n");
            foreach (NetworkPrefab prefab in prefabs)
            {
                Cookable[] cookables = prefab.GetComponentsInChildren<Cookable>(true);
                if (cookables.Length == 0)
                    continue;

                Cookable cookable = cookables[0];
                CookingTemperatures timing = cookable.CookingTemperatures;
                text.Append(prefab.name).Append(' ').Append(prefab.Hash)
                    .Append("  cook ").Append(cookable.IsCookable ? "yes" : "no").Append(" into ").Append(Name(Roasting.CookedVersion(cookable)))
                    .Append(", burn ").Append(cookable.IsBurnable ? "yes" : "no").Append(" into ").Append(Name(Roasting.BurntVersion(cookable)));
                if (timing != null)
                    text.Append(", weak ").Append(Pair(timing.WeakCooking)).Append(", optimal ").Append(Pair(timing.OptimalCooking))
                        .Append(", burning ").Append(Pair(timing.BurningCooking)).Append(", instant ").Append(Number(timing.InstantBurnTemperature));
                text.Append(", stew ").Append(prefab.GetComponentInChildren<StewComponent>(true) != null ? "yes" : "no")
                    .Append(", Cookables ").Append(cookables.Length).Append(", embedded ").Append(prefab.Embedded.Count).Append('\n');
            }

            text.Append("\n== Roast results with parts (prefabs with a Pickup and embedded entities) ==\n");
            foreach (NetworkPrefab prefab in prefabs)
            {
                if (prefab.Embedded.Count > 0 && prefab.GetComponentInChildren<Pickup>(true) != null && prefab.GetComponentInChildren<StewComponent>(true) == null)
                    text.Append(prefab.name).Append(' ').Append(prefab.Hash).Append("  embedded ").Append(prefab.Embedded.Count).Append('\n');
            }

            text.Append("\n== Roasting rule items loaded now (prefab, entity, cooked and burnt progress, timing) ==\n");
            foreach (Cookable live in UnityEngine.Object.FindObjectsOfType<Cookable>())
            {
                LibRoasting rule = Roasting.RuleFor(Roasting.PrefabOf(live));
                if (rule == null)
                    continue;

                string timing = rule.Error != null ? "rule off" : rule.Temperatures == null ? "game timing, the rule keeps it"
                    : ReferenceEquals(Roasting.Timing(live), rule.Temperatures) ? "rule timing" : "game timing until it loads again";
                text.Append(rule.Prefab.name).Append("  entity ").Append(live.Entity == null ? 0 : live.Entity.Identifier)
                    .Append("  cooked ").Append(Number(live.CookedProgress)).Append(", burnt ").Append(Number(live.BurntProgress)).Append(", ").Append(timing).Append('\n');
            }
            text.Append('\n');
        }

        static string Shared(LibRoasting rule)
        {
            LibRoasting owner = Roasting.RuleFor(rule.Prefab.Hash);
            return owner == null || owner == rule ? "" : ", the same as " + owner.Id;
        }

        static string Name(NetworkPrefab prefab) => prefab == null ? "nothing" : prefab.name;

        static string Pair(CookingTemperatureDuration value) =>
            value == null ? "none" : Number(value.Temperature) + "/" + Number(value.Duration) + "s";

        static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
