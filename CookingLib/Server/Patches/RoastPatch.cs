using System;
using Il2Cpp.Alta.Cooking;
using Il2Cpp.Alta.Networking;
using HarmonyLib;
using UnityEngine;

namespace CookingLib
{
    internal static class RoastPatch
    {
        static readonly AccessTools.FieldRef<CookingPot, CookingPot.CookingStage> potStage = AccessTools.FieldRefAccess<CookingPot, CookingPot.CookingStage>("currentStage");
        static CookingPot[] pots;
        static float potsAt;
        static Swap current;

        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(AccessTools.DeclaredMethod(typeof(Cookable), nameof(Cookable.InitializeAsServer), new[] { typeof(SpawnData) }),
                prefix: new HarmonyMethod(typeof(RoastPatch), nameof(SpeedPrefix)));
            harmony.Patch(AccessTools.Method(typeof(Cookable), "SpawnCookedVersion"),
                prefix: new HarmonyMethod(typeof(RoastPatch), nameof(CookPrefix)),
                postfix: new HarmonyMethod(typeof(RoastPatch), nameof(SwapPostfix)),
                finalizer: new HarmonyMethod(typeof(RoastPatch), nameof(SwapFinalizer)));
            harmony.Patch(AccessTools.Method(typeof(Cookable), "SwapToBurntVersion"),
                prefix: new HarmonyMethod(typeof(RoastPatch), nameof(BurnPrefix)),
                postfix: new HarmonyMethod(typeof(RoastPatch), nameof(SwapPostfix)),
                finalizer: new HarmonyMethod(typeof(RoastPatch), nameof(SwapFinalizer)));
        }

        static void SpeedPrefix(Cookable __instance)
        {
            try
            {
                if (!Roasting.Timed)
                    return;

                LibRoasting rule = Roasting.RuleFor(Roasting.PrefabOf(__instance));
                if (rule != null && rule.Error == null && rule.Temperatures != null)
                    Roasting.Timing(__instance) = rule.Temperatures;
            }
            catch (Exception e)
            {
                Safe.Fail(nameof(RoastPatch) + " speed", e);
            }
        }

        static bool CookPrefix(Cookable __instance, ref NetworkPrefab ___cookedVersion) => Before(__instance, false, ref ___cookedVersion);

        static bool BurnPrefix(Cookable __instance, ref NetworkPrefab ___burntVersion) => Before(__instance, true, ref ___burntVersion);

        static bool Before(Cookable cookable, bool burnt, ref NetworkPrefab version)
        {
            current = null;
            try
            {
                if (!Roasting.Active || IsStew(cookable))
                    return true;

                uint hash = Roasting.PrefabOf(cookable);
                LibRoasting rule = Roasting.RuleFor(hash);
                if (rule != null)
                {
                    version = rule.Version(burnt);
                    if (rule.Error == null && InLiquidPot(cookable))
                    {
                        rule.Held.Add(cookable.GetInstanceID());
                        return false;
                    }
                }
                if ((rule == null || rule.Error != null) && Roasting.RoastedHandlers == 0)
                    return true;

                Swap swap = new Swap(cookable, burnt, rule != null && rule.Error == null ? rule : null, hash, version);
                if (burnt)
                    cookable.SwappedToBurntVersion += swap.Took;
                else
                    cookable.SwappedToCookedVersion += swap.Took;
                current = swap;
            }
            catch (Exception e)
            {
                Safe.Fail(nameof(RoastPatch) + " swap", e);
            }
            return true;
        }

        static void SwapPostfix()
        {
            try
            {
                Swap swap = current;
                if (swap == null || !swap.Ran)
                    return;

                if (swap.Rule != null && swap.Burnt)
                    swap.Rule.TimesBurnt++;
                else if (swap.Rule != null)
                    swap.Rule.TimesCooked++;
                Roasting.Queue(swap.Event);
            }
            catch (Exception e)
            {
                Safe.Fail(nameof(RoastPatch) + " Roasted", e);
            }
        }

        static Exception SwapFinalizer(Exception __exception)
        {
            Swap swap = current;
            current = null;
            try
            {
                if (swap == null)
                    return __exception;

                if (swap.Burnt)
                    swap.Cookable.SwappedToBurntVersion -= swap.Took;
                else
                    swap.Cookable.SwappedToCookedVersion -= swap.Took;
                if (__exception == null || swap.Rule == null || !swap.Rule.Sets(swap.Burnt))
                    return __exception;

                Roasting.Failed(swap.Rule, swap.Burnt, __exception);
                return null;
            }
            catch (Exception e)
            {
                Safe.Fail(nameof(RoastPatch) + " finalizer", e);
                return __exception;
            }
        }

        static bool IsStew(Cookable cookable)
        {
            if (cookable.GetComponentInParent<StewComponent>() != null)
                return true;

            NetworkEntity entity = cookable.Entity;
            NetworkEntity root = entity == null ? null : entity.PrefabRoot;
            return root != null && root.GetComponentInChildren<StewComponent>(true) != null;
        }

        static bool InLiquidPot(Cookable cookable)
        {
            if (pots == null || Time.unscaledTime - potsAt > 1f || Time.unscaledTime < potsAt)
            {
                pots = UnityEngine.Object.FindObjectsOfType<CookingPot>();
                potsAt = Time.unscaledTime;
            }

            foreach (CookingPot pot in pots)
            {
                if (pot != null && potStage(pot) != CookingPot.CookingStage.Preparation && pot.ContainedCookables.Contains(cookable))
                    return true;
            }
            return false;
        }

        sealed class Swap
        {
            internal Swap(Cookable cookable, bool burnt, LibRoasting rule, uint hash, NetworkPrefab version)
            {
                Cookable = cookable;
                Burnt = burnt;
                Rule = rule;
                NetworkPrefab from = PrefabManager.GetPrefab(hash);
                Event = new RoastedEvent
                {
                    FromName = from == null ? null : from.name,
                    FromPrefabHash = hash,
                    ResultName = version == null ? null : version.name,
                    Burnt = burnt,
                    Position = cookable.transform.position,
                    Rule = rule
                };

                Pickup pickup = cookable.Pickup;
                PlayerController controller = pickup == null ? null : pickup.LastPlayerInteractor;
                Player player = controller == null ? null : controller.NetworkPlayer;
                if (player == null)
                    return;

                Event.LastHolder = player;
                UserInfoAndRole info = PlayerIds.InfoOf(player);
                if (info != null)
                {
                    Event.UserId = info.Identifier;
                    Event.UserName = info.Username;
                }
            }

            internal Cookable Cookable { get; }

            internal bool Burnt { get; }

            internal LibRoasting Rule { get; }

            internal RoastedEvent Event { get; }

            internal bool Ran { get; private set; }

            internal void Took(NetworkEntity entity)
            {
                Event.Result = entity;
                Ran = true;
            }
        }
    }
}
