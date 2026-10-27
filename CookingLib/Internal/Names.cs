using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Il2CppAlta;
using Il2CppAlta.Cooking;
using Il2CppAlta.Inventory;
using Il2CppAlta.Networking;

namespace CookingLib
{
    internal static class Names
    {
        static List<CookableCategoryDefinition> categories;

        internal static string Norm(string name)
        {
            if (name == null)
                return "";

            StringBuilder key = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (c == ' ' || c == '_' || c == '-' || c == '\'')
                    continue;
                key.Append(char.ToLowerInvariant(c));
            }
            return key.ToString();
        }

        internal static T Find<T>(string name) where T : HashedGeneralValue<T>
        {
            string key = Norm(name);
            if (key.Length == 0)
                return null;

            uint.TryParse(key, out uint hash);
            foreach (T value in HashedGeneralValue<T>.All)
            {
                if (value == null)
                    continue;
                if (hash != 0 ? value.Hash == hash : Norm(value.name) == key)
                    return value;
            }
            return null;
        }

        internal static T Need<T>(string name, string kind, string plural) where T : HashedGeneralValue<T>
        {
            T value = Find<T>(name);
            if (value == null)
                throw new ArgumentException("no " + kind + " named '" + name + "'" + Suggest(name, HashedGeneralValue<T>.All.Where(v => v != null).Select(v => v.name), plural));
            return value;
        }

        internal static Item NeedItem(string name)
        {
            Item item = Find<Item>(name);
            if (item != null)
                return item;

            string key = Norm(name);
            foreach (Item candidate in HashedGeneralValue<Item>.All)
            {
                NetworkPrefab prefab = candidate == null ? null : candidate.Prefab;
                if (prefab != null && (Norm(prefab.name) == key || prefab.Hash.ToString() == key))
                    return candidate;
            }

            IEnumerable<Item> items = HashedGeneralValue<Item>.All.Where(i => i != null);
            IEnumerable<string> names = items.Select(i => i.name).Concat(items.Where(i => i.Prefab != null).Select(i => i.Prefab.name));
            throw new ArgumentException("no item named '" + name + "'" + Suggest(name, names, "Items"));
        }

        internal static Item NeedItemExact(string name)
        {
            Item item = NeedItem(name);
            string key = Norm(name);
            if (!uint.TryParse(key, out uint number))
                return item;

            NetworkPrefab prefab = Safe.Run("prefab lookup", () => PrefabWalk.All().FirstOrDefault(p => p.Hash == number), null);
            if (prefab != null && item.Prefab != prefab)
                throw new ArgumentException("number " + number + " names the item " + item.name + " and the prefab " + prefab.name + ", use a name");
            return item;
        }

        internal static List<CookableCategoryDefinition> Categories()
        {
            if (categories == null || categories.Count == 0)
            {
                categories = HashedGeneralValue<Item>.All.Where(i => i != null).Select(i => i.GetComponent<CookableItem>()).Where(c => c != null && c.Category != null)
                    .Select(c => c.Category).Distinct().OrderBy(c => c.name, StringComparer.OrdinalIgnoreCase).ToList();
            }
            return categories;
        }

        internal static CookableCategoryDefinition NeedCategory(string name)
        {
            string key = Norm(name);
            foreach (CookableCategoryDefinition category in Categories())
            {
                if (Norm(category.name) == key)
                    return category;
            }

            throw new ArgumentException("no cookable category named '" + name + "'" + Suggest(name, Categories().Select(c => c.name), "Categories"));
        }

        internal static NetworkPrefab FindPrefab(string name)
        {
            string key = Norm(name);
            foreach (NetworkPrefab prefab in PrefabWalk.All())
            {
                if (Norm(prefab.name) == key || prefab.Hash.ToString() == key)
                    return prefab;
            }
            return null;
        }

        internal static string Suggest(string name, IEnumerable<string> names, string plural, IEnumerable<string> alsoClose = null)
        {
            string key = Norm(name);
            List<string> listed = Distinct(names);
            List<string> close = Distinct(alsoClose == null ? listed : listed.Concat(alsoClose))
                .Select(candidate => new { Name = candidate, Score = Closeness(key, Norm(candidate)) })
                .Where(match => match.Score >= 0)
                .OrderBy(match => match.Score)
                .ThenBy(match => match.Name, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .Select(match => match.Name)
                .ToList();

            if (close.Count > 0)
                return ". Did you mean " + string.Join(", ", close.ToArray()) + "?";
            if (listed.Count > 0 && listed.Count <= 20)
                return ". " + plural + ": " + string.Join(", ", listed.ToArray());
            return "";
        }

        static List<string> Distinct(IEnumerable<string> names) =>
            names.Where(n => !string.IsNullOrEmpty(n)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

        static int Closeness(string key, string candidate)
        {
            if (key.Length == 0 || candidate.Length == 0 || key == candidate)
                return -1;
            if (candidate.Contains(key) || key.Contains(candidate))
                return Math.Abs(candidate.Length - key.Length);

            int limit = key.Length / 3;
            if (limit == 0 || Math.Abs(candidate.Length - key.Length) > limit)
                return -1;
            int distance = Distance(key, candidate);
            return distance <= limit ? 1000 + distance : -1;
        }

        static int Distance(string a, string b)
        {
            int[] previous = new int[b.Length + 1];
            int[] current = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++)
                previous[j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int swap = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                    current[j] = Math.Min(swap, Math.Min(previous[j] + 1, current[j - 1] + 1));
                }
                int[] row = previous;
                previous = current;
                current = row;
            }
            return previous[b.Length];
        }
    }
}
