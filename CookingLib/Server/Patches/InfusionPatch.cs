using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppAlta.Cooking;
using Il2CppAlta.Liquid;
using HarmonyLib;
using UnityEngine;

namespace CookingLib
{
    internal static class InfusionPatch
    {
        static readonly FieldInfo endlessField = typeof(LiquidContainer).GetField("isInfiniteSource", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly HashSet<string> logged = new HashSet<string>();

        internal static int Done { get; private set; }

        internal static int Refused { get; private set; }

        internal static int PotBases { get; private set; }

        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            if (endlessField == null)
                throw new MissingFieldException("LiquidContainer", "isInfiniteSource");
            harmony.Patch(AccessTools.Method(typeof(LiquidContainerTrigger), nameof(LiquidContainerTrigger.OnTriggerEnter)),
                postfix: new HarmonyMethod(typeof(InfusionPatch), nameof(Postfix)) { priority = Priority.Last });
        }

        internal static bool IsEndless(LiquidContainer container) => (bool)endlessField.GetValue(container);

        static void Postfix(Collider other, ILiquidContainer ___thisContainer, Dictionary<Rigidbody, int> ___handledRigidbodies)
        {
            try
            {
                Infuse(other, ___thisContainer, ___handledRigidbodies);
            }
            catch (Exception e)
            {
                Safe.Fail(nameof(InfusionPatch), e);
            }
        }

        static void Infuse(Collider other, ILiquidContainer target, Dictionary<Rigidbody, int> handled)
        {
            if (Infusions.All.Count == 0 || other == null || target == null)
                return;

            Rigidbody body = other.attachedRigidbody;
            if (body == null || !handled.TryGetValue(body, out int entries) || entries != 1)
                return;

            Pickup pickup = body.GetComponent<Pickup>();
            if (pickup == null || pickup.Item == null)
                return;

            LibInfusion infusion = Infusions.Find(pickup.Item, target.ContainedLiquid);
            if (infusion == null)
                return;

            LiquidContainer container = target as LiquidContainer;
            bool refused = container == null || ReferenceEquals(container, GlobalLiquidContainer.Instance) || IsEndless(container);
            Note(target, container, refused);
            if (refused)
            {
                Refused++;
                return;
            }

            ((ILiquidContainerInternal)container).SetContent(infusion.Output.CreateContent());
            Done++;
            if (infusion.Spec.ConsumeItem)
                pickup.Entity.Spawner.Destroy(pickup.Entity.PrefabRoot);
            FollowBase(container, infusion.Output.Definition);
        }

        static void FollowBase(LiquidContainer container, LiquidDefinition definition)
        {
            StewManager stews = container.GetComponentInParent<StewManager>();
            if (stews == null || definition == null || stews.Ingredients.Count > 0)
                return;

            stews.SetBaseLiquid(definition);
            PotBases++;
        }

        static void Note(ILiquidContainer target, LiquidContainer container, bool refused)
        {
            string name = target is Component component ? component.transform.root.name : "?";
            string line = name + " (" + target.GetType().Name + (container != null ? ", isInfiniteSource " + IsEndless(container) : "") + ")" + (refused ? " refused" : " infused");
            if (logged.Add(line))
                Log.Info("infusion into " + line);
        }
    }
}
