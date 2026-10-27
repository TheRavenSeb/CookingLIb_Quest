using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppAlta;
using Il2CppAlta.Cooking;
using Il2CppAlta.Global;
using Il2CppAlta.Networking;

namespace CookingLib
{
    internal static class StewStages
    {
        static readonly FieldInfo cookedField = typeof(Cookable).GetField("cookedVersion", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo burntField = typeof(Cookable).GetField("burntVersion", BindingFlags.NonPublic | BindingFlags.Instance);

        static Dictionary<uint, Entry> table;

        internal static string Error { get; private set; }

        internal static IEnumerable<Entry> All => Ready() ? table.Values : Enumerable.Empty<Entry>();

        internal static bool Ready()
        {
            if (table != null)
                return true;
            if (Error != null)
                return false;

            try
            {
                table = Build();
                Log.Info("stew stage table: " + table.Count + " stew prefabs");
                return true;
            }
            catch (Exception e)
            {
                Error = "the stew stage table failed to build: " + e.Message;
                Log.Error(Error + "\n" + e);
                return false;
            }
        }

        internal static Entry Get(uint prefabHash) =>
            Ready() && table.TryGetValue(prefabHash, out Entry entry) ? entry : null;

        internal static List<Entry> Chain(uint startHash) => Ready() ? Walk(table, startHash) : new List<Entry>();

        internal static Entry Find(uint startHash, StewStage stage) => Chain(startHash).FirstOrDefault(entry => entry.Stage == stage);

        internal static bool InChain(uint stewHash, StewRecipe recipe) =>
            !ReferenceEquals(recipe, null) && Chain(recipe.BaseVersionHash).Any(entry => entry.Hash == stewHash);

        internal static List<StewRecipe> RecipesFor(uint stewHash) =>
            HashedGeneralValue<StewRecipe>.All.Where(recipe => recipe != null && InChain(stewHash, recipe)).ToList();

        internal static uint DefaultRaw
        {
            get
            {
                StewComponent stew = GlobalSettings<CookingSettings>.Instance.DefaultStew;
                return stew == null || stew.Entity == null || stew.Entity.Prefab == null ? 0 : stew.Entity.Prefab.Hash;
            }
        }

        internal static uint NonRecipeRaw
        {
            get
            {
                NetworkPrefab prefab = GlobalSettings<CookingSettings>.Instance.NonRecipeStewPrefab;
                return prefab == null ? 0 : prefab.Hash;
            }
        }

        static Dictionary<uint, Entry> Build()
        {
            if (cookedField == null || burntField == null)
                throw new InvalidOperationException("the game's Cookable stage fields are missing");

            Dictionary<uint, Entry> entries = new Dictionary<uint, Entry>();
            foreach (NetworkPrefab prefab in PrefabWalk.With<StewComponent>())
            {
                if (entries.ContainsKey(prefab.Hash))
                    continue;
                StewComponent stew = prefab.GetComponent<StewComponent>();
                Cookable cookable = stew.Cookable;
                entries[prefab.Hash] = new Entry(prefab, stew, Next(cookable, cookedField), Next(cookable, burntField));
            }
            if (entries.Count == 0)
                throw new InvalidOperationException("no stew prefabs found");

            foreach (Entry entry in entries.Values)
            {
                if (entry.BurntVersion != null && entries.TryGetValue(entry.BurntVersion.Hash, out Entry burnt))
                    burnt.Stage = StewStage.Burnt;
            }
            foreach (Entry entry in entries.Values)
            {
                if (entry.CookedVersion != null && entries.TryGetValue(entry.CookedVersion.Hash, out Entry cooked) && cooked.Stage == StewStage.Raw)
                    cooked.Stage = StewStage.Cooked;
            }

            foreach (Entry raw in entries.Values.Where(e => e.Stage == StewStage.Raw).OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            {
                foreach (Entry member in Walk(entries, raw.Hash))
                {
                    if (member.Raw == null)
                        member.Raw = raw.Prefab;
                }
            }
            foreach (Entry entry in entries.Values.Where(e => e.Raw == null))
                entry.Raw = entry.Prefab;
            return entries;
        }

        static List<Entry> Walk(Dictionary<uint, Entry> entries, uint startHash)
        {
            List<Entry> chain = new List<Entry>();
            Queue<uint> open = new Queue<uint>();
            open.Enqueue(startHash);
            while (open.Count > 0)
            {
                if (!entries.TryGetValue(open.Dequeue(), out Entry entry) || chain.Contains(entry))
                    continue;
                chain.Add(entry);
                if (entry.CookedVersion != null)
                    open.Enqueue(entry.CookedVersion.Hash);
                if (entry.BurntVersion != null)
                    open.Enqueue(entry.BurntVersion.Hash);
            }
            return chain;
        }

        static NetworkPrefab Next(Cookable cookable, FieldInfo field) =>
            cookable == null ? null : field.GetValue(cookable) as NetworkPrefab;

        internal sealed class Entry
        {
            internal Entry(NetworkPrefab prefab, StewComponent stew, NetworkPrefab cookedVersion, NetworkPrefab burntVersion)
            {
                Prefab = prefab;
                Hash = prefab.Hash;
                Name = prefab.name;
                Stew = stew;
                CookedVersion = cookedVersion;
                BurntVersion = burntVersion;
            }

            internal NetworkPrefab Prefab { get; }

            internal uint Hash { get; }

            internal string Name { get; }

            internal StewComponent Stew { get; }

            internal StewStage Stage { get; set; }

            internal NetworkPrefab Raw { get; set; }

            internal NetworkPrefab CookedVersion { get; }

            internal NetworkPrefab BurntVersion { get; }

            internal bool IsFinal => Stew.IsFinalStage;

            internal List<StewRecipe> Recipes => RecipesFor(Hash);
        }
    }
}
