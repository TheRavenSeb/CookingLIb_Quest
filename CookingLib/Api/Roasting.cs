using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppAlta.Cooking;
using Il2CppAlta.Inventory;
using Il2CppAlta.Networking;
using HarmonyLib;

namespace CookingLib
{
    /// <summary>Changes what items turn into over a fire, and how fast they cook and burn. Rules run on the server only.</summary>
    public static class Roasting
    {
        /// <summary>Use as CookedInto or BurntInto to make the item vanish.</summary>
        public const string Nothing = "nothing";

        internal static readonly AccessTools.FieldRef<Cookable, CookingTemperatures> Timing = AccessTools.FieldRefAccess<Cookable, CookingTemperatures>("cookingTemperatures");
        internal static readonly AccessTools.FieldRef<Cookable, NetworkPrefab> CookedVersion = AccessTools.FieldRefAccess<Cookable, NetworkPrefab>("cookedVersion");
        internal static readonly AccessTools.FieldRef<Cookable, NetworkPrefab> BurntVersion = AccessTools.FieldRefAccess<Cookable, NetworkPrefab>("burntVersion");

        static readonly FieldInfo weak = typeof(CookingTemperatures).GetField("weakCooking", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo optimal = typeof(CookingTemperatures).GetField("optimalCooking", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo burning = typeof(CookingTemperatures).GetField("burningCooking", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo instant = typeof(CookingTemperatures).GetField("instantBurnTemperature", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly List<LibRoasting> all = new List<LibRoasting>();
        static readonly Dictionary<uint, LibRoasting> byPrefab = new Dictionary<uint, LibRoasting>();
        static bool worldUp;

        /// <summary>Every rule mods tried to register, refused ones included.</summary>
        public static IReadOnlyList<LibRoasting> All => all;

        /// <summary>An item cooked or burnt over a fire, under a rule or as in the game. Stews never fire it. Server only; handlers run on the main thread from the next frame.</summary>
        public static event Action<RoastedEvent> Roasted;

        /// <summary>Registers a rule. Call it from Main.Setup. The server resolves it once the world is up, or at once after that. Never throws.</summary>
        public static LibRoasting Register(RoastingSpec spec)
        {
            LibRoasting rule = new LibRoasting(spec);
            try
            {
                Validate(spec);
            }
            catch (ArgumentException e)
            {
                rule.Error = e.Message;
            }

            all.Add(rule);
            if (rule.Error != null)
            {
                Log.Warn("roast " + rule.Id + " refused: " + rule.Error);
            }
            else if (worldUp)
            {
                Resolve(rule);
                if (rule.IsReady)
                    Sweep();
            }
            return rule;
        }

        internal static bool Active => byPrefab.Count > 0 || Roasted != null;

        internal static bool Timed { get; private set; }

        internal static int RoastedFired { get; private set; }

        internal static int RoastedHandlers => Roasted?.GetInvocationList().Length ?? 0;

        internal static LibRoasting RuleFor(uint prefabHash) => byPrefab.TryGetValue(prefabHash, out LibRoasting rule) ? rule : null;

        internal static uint PrefabOf(Cookable cookable)
        {
            NetworkEntity entity = cookable.Entity;
            NetworkEntity root = entity == null ? null : entity.PrefabRoot;
            NetworkPrefab prefab = root == null ? null : root.GetComponent<NetworkPrefab>();
            return prefab == null ? 0 : prefab.Hash;
        }

        internal static void ResolveAll()
        {
            worldUp = true;
            List<LibRoasting> waiting = all.Where(r => r.Error == null).ToList();
            if (waiting.Count == 0)
                return;

            foreach (LibRoasting rule in waiting)
                Resolve(rule);
            Sweep();
        }

        internal static void Queue(RoastedEvent args)
        {
            if (Roasted == null)
                return;

            RoastedFired++;
            EventQueue.Enqueue(Roasted, args, nameof(Roasted));
        }

        internal static void Failed(LibRoasting rule, bool burnt, Exception e)
        {
            string into = (burnt ? rule.Burnt : rule.Cooked)?.name ?? Nothing;
            foreach (LibRoasting same in all.Where(r => r.Error == null && r.Prefab == rule.Prefab))
                same.Error = "turning " + rule.Prefab.name + " into " + into + " failed, so it roasts as in the game again: " + e.Message;
            Timed = byPrefab.Values.Any(r => r.Error == null && r.Temperatures != null);
            Log.Error("roast swap failed for " + rule.Id + ", " + rule.Prefab.name + " roasts as in the game from now on: " + e);
        }

        internal static string Describe(LibRoasting rule)
        {
            RoastingSpec spec = rule.Spec;
            return Part("cook", rule.SetsCooked, rule.Cooked, spec.CookSpeed) + ", " + Part("burn", rule.SetsBurnt, rule.Burnt, spec.BurnSpeed);
        }

        internal static List<string> Cycles()
        {
            List<string> found = new List<string>();
            HashSet<string> seen = new HashSet<string>();
            foreach (LibRoasting rule in byPrefab.Values.Where(r => r.Error == null))
            {
                foreach (NetworkPrefab next in Results(rule.Prefab.Hash))
                {
                    if (!Results(next.Hash).Any(back => back.Hash == rule.Prefab.Hash))
                        continue;
                    uint low = Math.Min(rule.Prefab.Hash, next.Hash), high = Math.Max(rule.Prefab.Hash, next.Hash);
                    if (seen.Add(low + "/" + high))
                        found.Add(rule.Prefab.name + " and " + next.name + " roast into each other, so a hot fire can swap them back and forth every frame");
                }
            }
            return found;
        }

        static IEnumerable<NetworkPrefab> Results(uint hash)
        {
            NetworkPrefab prefab = PrefabManager.GetPrefab(hash);
            Cookable[] cookables = prefab == null ? new Cookable[0] : prefab.GetComponentsInChildren<Cookable>(true);
            LibRoasting rule = RuleFor(hash);
            foreach (Cookable cookable in cookables)
            {
                bool live = rule != null && rule.Error == null;
                if (cookable.IsCookable && !(live && rule.Spec.CookSpeed == 0f))
                {
                    NetworkPrefab cooked = live ? rule.Version(false) : CookedVersion(cookable);
                    if (cooked != null)
                        yield return cooked;
                }
                if (cookable.IsBurnable && !(live && rule.Spec.BurnSpeed == 0f))
                {
                    NetworkPrefab burnt = live ? rule.Version(true) : BurntVersion(cookable);
                    if (burnt != null)
                        yield return burnt;
                }
            }
        }

        static string Part(string verb, bool sets, NetworkPrefab into, float speed)
        {
            if (speed == 0f)
                return "never " + verb + "s";
            string result = !sets ? " as in the game" : into == null ? " into nothing" : " into " + into.name;
            return verb + "s" + result + (speed == 1f ? "" : " at speed " + Claims.Number(speed));
        }

        static void Resolve(LibRoasting rule)
        {
            try
            {
                if (!ServerPatches.RoastOn)
                    throw new ArgumentException("the roasting patches failed to apply, see the log");

                RoastingSpec spec = rule.Spec;
                NetworkPrefab item = NeedPrefab(spec.Item, "Item");
                if (item.GetComponentInChildren<StewComponent>(true) != null)
                    throw new ArgumentException(item.name + " is a stew, and stews change stage in the pot, never by a rule");
                Cookable[] cookables = item.GetComponentsInChildren<Cookable>(true);
                if (cookables.Length != 1)
                    throw new ArgumentException(item.name + (cookables.Length == 0 ? " has no Cookable, so it never roasts" : " has " + cookables.Length + " Cookables, a rule needs exactly one"));

                Cookable template = cookables[0];
                if (!template.IsCookable && (spec.CookedInto != null || spec.CookSpeed != 1f))
                    throw new ArgumentException(item.name + " never cooks in the game, so leave CookedInto null and CookSpeed 1");
                if (!template.IsBurnable && (spec.BurntInto != null || spec.BurnSpeed != 1f))
                    throw new ArgumentException(item.name + " never burns in the game, so leave BurntInto null and BurnSpeed 1");

                rule.SetsCooked = spec.CookedInto != null;
                rule.SetsBurnt = spec.BurntInto != null;
                rule.Cooked = Result(spec.CookedInto, "CookedInto", item);
                rule.Burnt = Result(spec.BurntInto, "BurntInto", item);
                rule.GameCooked = CookedVersion(template);
                rule.GameBurnt = BurntVersion(template);
                rule.Temperatures = Scale(template.CookingTemperatures, spec.CookSpeed, spec.BurnSpeed, item.name);

                if (Claims.Holds(rule.Id))
                    throw new ArgumentException("id is already registered");
                string clash = Claims.Take(rule.Id, new[] { Claims.Roast(item, Describe(rule)) });
                if (clash != null)
                    throw new ArgumentException(clash);

                rule.Prefab = item;
                if (!byPrefab.ContainsKey(item.Hash))
                    byPrefab[item.Hash] = rule;
                Timed |= rule.Temperatures != null;
                Log.Info("roast " + rule.Id + " ready: " + item.name + " " + Describe(rule));
            }
            catch (Exception e)
            {
                rule.Error = e is ArgumentException ? e.Message : e.ToString();
                rule.Prefab = null;
                Log.Warn("roast " + rule.Id + " refused: " + rule.Error);
            }
        }

        static NetworkPrefab Result(string name, string field, NetworkPrefab item)
        {
            if (name == null || string.Equals(name.Trim(), Nothing, StringComparison.OrdinalIgnoreCase))
                return null;

            NetworkPrefab prefab = NeedPrefab(name, field);
            if (prefab == item)
                throw new ArgumentException(field + " " + prefab.name + " is the item itself");
            if (prefab.GetComponentInChildren<StewComponent>(true) != null)
                throw new ArgumentException(field + " " + prefab.name + " is a stew, and stews can't come from a roast");
            if (prefab.GetComponentInChildren<Pickup>(true) == null)
                throw new ArgumentException(field + " " + prefab.name + " has no Pickup, so it can't come from a roast");
            return prefab;
        }

        static NetworkPrefab NeedPrefab(string name, string field)
        {
            NetworkPrefab prefab = Names.FindPrefab(name);
            Item item = Names.Find<Item>(name);
            NetworkPrefab itemPrefab = item == null ? null : item.Prefab;
            if (prefab != null && itemPrefab != null && prefab != itemPrefab)
                throw new ArgumentException(field + " '" + name + "' names both the item " + item.name + " and the prefab " + prefab.name + ", use a name only one of them has");
            if (prefab != null)
                return prefab;
            if (itemPrefab != null)
                return itemPrefab;
            if (item != null)
                throw new ArgumentException(field + " " + item.name + " has no prefab, so it can't roast");

            throw new ArgumentException(field + ": no item or prefab named '" + name + "'" + Names.Suggest(name, PrefabWalk.All().Select(p => p.name), "Prefabs"));
        }

        static CookingTemperatures Scale(CookingTemperatures game, float cook, float burn, string name)
        {
            if (cook == 1f && burn == 1f)
                return null;
            if (game == null || game.WeakCooking == null || game.OptimalCooking == null || game.BurningCooking == null)
                throw new ArgumentException(name + " has no cooking temperatures to speed up");

            CookingTemperatures copy = new CookingTemperatures();
            weak.SetValue(copy, Scaled(game.WeakCooking, cook));
            optimal.SetValue(copy, Scaled(game.OptimalCooking, cook));
            burning.SetValue(copy, Scaled(game.BurningCooking, burn));
            instant.SetValue(copy, burn == 0f ? float.MaxValue : game.InstantBurnTemperature);
            return copy;
        }

        static CookingTemperatureDuration Scaled(CookingTemperatureDuration game, float speed) =>
            new CookingTemperatureDuration(game.Temperature, speed == 0f ? float.MaxValue : game.Duration / speed);

        static void Sweep()
        {
            int count = Safe.Run("roast sweep", SweepNow, 0);
            Log.Info("roasting: applied to " + count + " live items");
        }

        static int SweepNow()
        {
            int count = 0;
            foreach (Cookable live in UnityEngine.Object.FindObjectsOfType<Cookable>())
            {
                LibRoasting rule = RuleFor(PrefabOf(live));
                if (rule == null || rule.Error != null || rule.Temperatures == null || ReferenceEquals(Timing(live), rule.Temperatures))
                    continue;

                Timing(live) = rule.Temperatures;
                count++;
            }
            return count;
        }

        static void Validate(RoastingSpec spec)
        {
            if (spec == null)
                throw new ArgumentException("spec is null");
            if (string.IsNullOrEmpty(spec.Id) || spec.Id.IndexOf('.') <= 0)
                throw new ArgumentException("id must look like author.thing");
            if (all.Any(other => other.Error == null && string.Equals(other.Id, spec.Id, StringComparison.OrdinalIgnoreCase)) || Claims.Holds(spec.Id))
                throw new ArgumentException("id is already registered");
            if (string.IsNullOrEmpty(spec.Item))
                throw new ArgumentException("Item is empty");
            if (spec.CookedInto != null && spec.CookedInto.Trim().Length == 0)
                throw new ArgumentException("CookedInto is empty, leave it null to keep the game's");
            if (spec.BurntInto != null && spec.BurntInto.Trim().Length == 0)
                throw new ArgumentException("BurntInto is empty, leave it null to keep the game's");
            if (float.IsNaN(spec.CookSpeed) || spec.CookSpeed < 0f || spec.CookSpeed > 100f)
                throw new ArgumentException("CookSpeed must be 0 to 100");
            if (float.IsNaN(spec.BurnSpeed) || spec.BurnSpeed < 0f || spec.BurnSpeed > 100f)
                throw new ArgumentException("BurnSpeed must be 0 to 100");
            if (spec.CookSpeed == 0f && spec.CookedInto != null)
                throw new ArgumentException("CookSpeed 0 never cooks, so leave CookedInto null");
            if (spec.BurnSpeed == 0f && spec.BurntInto != null)
                throw new ArgumentException("BurnSpeed 0 never burns, so leave BurntInto null");
        }
    }
}
