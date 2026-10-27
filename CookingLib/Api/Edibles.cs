using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppAlta.Alchemy;
using Il2CppAlta.Inventory;
using Il2CppAlta.Networking;

namespace CookingLib
{
    /// <summary>Changes what the game's own foods give when eaten. Nothing is saved, so without the mod the game's values come back.</summary>
    public static class Edibles
    {
        static readonly FieldInfo prefabHash = typeof(Item).GetField("prefabHash", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly List<LibEdible> all = new List<LibEdible>();
        static bool late;

        /// <summary>Every change mods asked for and have not reverted, refused ones included.</summary>
        public static IReadOnlyList<LibEdible> All => all;

        /// <summary>Changes a game food. Call it from Main.Setup on the server and on players' machines. A later call applies at once. Never throws.</summary>
        public static LibEdible Register(EdibleSpec spec)
        {
            LibEdible entry = new LibEdible(spec);
            try
            {
                Check(entry);
                string clash = Claims.Take(entry.Id, new[] { Claims.Food(entry.PrefabHash, entry.Item.name, "effects", VanillaEdits.Text(entry.Effects)) });
                if (clash != null)
                    throw new ArgumentException(clash);
            }
            catch (Exception e)
            {
                entry.Error = e is ArgumentException ? e.Message : e.ToString();
            }

            all.Add(entry);
            if (entry.Error != null)
            {
                Log.Warn("food " + entry.Id + " refused: " + entry.Error);
                return entry;
            }

            if (Ready)
                Finish(entry);
            else if (Main.SetupDone && !(Main.IsServer && ServerPatches.PrefabMapOn))
                Walk();
            return entry;
        }

        /// <summary>The change in place on this food, or null when it gives the game's values.</summary>
        public static LibEdible Find(Item item) => item == null ? null : all.FirstOrDefault(e => e.Error == null && e.IsReady && e.Item == item);

        /// <summary>Undoes the change with this id and gives back the game's values, on foods already spawned too. True if it was in place or waiting.</summary>
        public static bool Revert(string id)
        {
            try
            {
                List<LibEdible> gone = all.Where(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)).ToList();
                LibEdible entry = gone.FirstOrDefault(e => e.Error == null);
                all.RemoveAll(gone.Contains);
                List<string> freed = new List<string>();
                if (entry != null)
                    freed.AddRange(VanillaEdits.Release(entry.Id));
                else
                    Claims.Forget(id);
                if (entry == null)
                    return false;

                bool applied = entry.IsReady;
                entry.IsReady = false;
                Log.Info("food " + entry.Id + " reverted");
                if (applied && freed.Count > 0 && late)
                    Sweep(new[] { entry.PrefabHash });
                return true;
            }
            catch (Exception e)
            {
                Safe.Fail("food revert", e);
                return false;
            }
        }

        internal static bool Ready { get; private set; }

        internal static int Swept { get; private set; }

        internal static IEnumerable<LibEdible> Live => all.Where(e => e.Error == null);

        internal static void MapReady(bool early)
        {
            Ready = true;
            if (early)
                late = true;
            foreach (LibEdible entry in Live.Where(e => !e.IsReady).ToList())
                Finish(entry);
        }

        internal static void AfterSetup()
        {
            if (!Ready && Live.Any() && !(Main.IsServer && ServerPatches.PrefabMapOn))
                Walk();
            if (!Main.IsServer)
                late = true;
        }

        internal static void WorldUp()
        {
            late = true;
            List<uint> changed = Live.Where(e => e.IsReady).Select(e => e.PrefabHash).Distinct().ToList();
            if (changed.Count > 0)
                Sweep(changed);
        }

        static void Walk()
        {
            PrefabWalk.All();
            if (!Ready)
                MapReady(false);
        }

        static void Check(LibEdible entry)
        {
            EdibleSpec spec = entry.Spec;
            if (spec == null)
                throw new ArgumentException("spec is null");
            if (string.IsNullOrEmpty(spec.Id) || spec.Id.IndexOf('.') <= 0)
                throw new ArgumentException("id must look like author.thing");
            if (Live.Any(e => string.Equals(e.Id, spec.Id, StringComparison.OrdinalIgnoreCase)) || Claims.Holds(spec.Id))
                throw new ArgumentException("id is already used, Revert it first");
            if (spec.Bites != 0 || spec.BiteSeconds != 0f)
                throw new ArgumentException("Bites and BiteSeconds are for a later CookingLib, leave them 0");
            if (string.IsNullOrEmpty(spec.Item))
                throw new ArgumentException("Item is empty");

            entry.Item = Names.NeedItemExact(spec.Item);
            entry.PrefabHash = prefabHash != null ? (uint)prefabHash.GetValue(entry.Item) : entry.Item.Prefab == null ? 0 : entry.Item.Prefab.Hash;
            if (entry.PrefabHash == 0)
                throw new ArgumentException(entry.Item.name + " has no prefab, so it is not a food");
            entry.Effects = VanillaEdits.Replace(spec.Effects, null, "foods");
            PageBudget.Check(PageKind.Edible, entry.Item.name + "'s food page", entry.Effects);
        }

        static void Finish(LibEdible entry)
        {
            try
            {
                NetworkPrefab prefab = PrefabManager.GetPrefab(entry.PrefabHash);
                EdibleObject edible = prefab == null ? null : prefab.GetComponent<EdibleObject>();
                if (edible == null)
                    throw new ArgumentException(entry.Item.name + " is not a food, its prefab has no EdibleObject");

                Claims.Claim key = Claims.Food(entry.PrefabHash, entry.Item.name, "effects", null);
                EffectDefinitionApplication[] game = VanillaEdits.Find(key.Key)?.Original as EffectDefinitionApplication[] ?? edible.ConsumptionEffects;
                EffectDefinitionApplication[] effects = VanillaEdits.Replace(entry.Spec.Effects, game, "foods");
                PageBudget.Check(PageKind.Edible, entry.Item.name + "'s food page", effects);
                VanillaEdits.Write(key, edible, VanillaEdits.Text(edible.ConsumptionEffects), VanillaEdits.FoodEffects, effects);
                entry.Effects = effects;
                entry.IsReady = true;
                Log.Info("food " + entry.Id + ": " + entry.Item.name + " gives " + VanillaEdits.Text(effects));
            }
            catch (Exception e)
            {
                entry.Error = e is ArgumentException ? e.Message : e.ToString();
                entry.IsReady = false;
                VanillaEdits.Release(entry.Id);
                Log.Warn("food " + entry.Id + " refused: " + entry.Error);
                return;
            }

            if (Main.SetupDone)
                entry.Notes.Add("changed after Setup, so books show its old food page until they reload");
            if (late)
                Sweep(new[] { entry.PrefabHash });
        }

        static void Sweep(IEnumerable<uint> hashes)
        {
            HashSet<uint> wanted = new HashSet<uint>(hashes);
            int count = Safe.Run("food sweep", () => SweepNow(wanted), 0);
            Swept += count;
            Log.Info("foods: swept " + count);
        }

        static int SweepNow(HashSet<uint> hashes)
        {
            int count = 0;
            foreach (EdibleObject live in UnityEngine.Object.FindObjectsOfType<EdibleObject>())
            {
                NetworkPrefab own = live.GetComponent<NetworkPrefab>();
                if (own == null || !hashes.Contains(own.Hash))
                    continue;

                NetworkPrefab prefab = PrefabManager.GetPrefab(own.Hash);
                EdibleObject source = prefab == null ? null : prefab.GetComponent<EdibleObject>();
                if (source == null || source == live || VanillaEdits.Same(live.ConsumptionEffects, source.ConsumptionEffects))
                    continue;

                VanillaEdits.FoodEffects.SetValue(live, source.ConsumptionEffects.ToArray());
                count++;
            }
            return count;
        }
    }
}
