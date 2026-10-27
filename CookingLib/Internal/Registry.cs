using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppAlta;

namespace CookingLib
{
    internal static class Registry
    {
        const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        static readonly FieldInfo hashField = typeof(HashedGeneralValue).GetField("hash", BindingFlags.NonPublic | BindingFlags.Instance);

        internal static void SetHash(HashedGeneralValue value, uint hash) => hashField.SetValue(value, (int)hash);

        internal static T Find<T>(uint hash) where T : HashedGeneralValue<T>
        {
            Dictionary<uint, T> items = Items<T>();
            return items != null && items.TryGetValue(hash, out T value) ? value : null;
        }

        static Dictionary<uint, T> Items<T>() where T : HashedGeneralValue<T>
        {
            HashedGeneralValue<T>.CheckItems();
            return (Dictionary<uint, T>)typeof(HashedGeneralValue<T>).GetField("items", PrivateStatic).GetValue(null);
        }

        internal static bool Add<T>(T value) where T : HashedGeneralValue<T>
        {
            Dictionary<uint, T> items = Items<T>();
            if (items == null || items.Count == 0)
                throw new ArgumentException("the game's " + typeof(T).Name + " list failed to load, so nothing can join it");
            if (items.ContainsKey(value.Hash))
                return false;

            items.Add(value.Hash, value);
            typeof(HashedGeneralValue<T>).GetField("itemsByName", PrivateStatic).SetValue(null, null);
            return true;
        }

        internal static uint NumberFor(string id)
        {
            uint hash = 2166136261;
            foreach (char c in id.ToLowerInvariant())
            {
                hash ^= c;
                hash *= 16777619;
            }
            hash &= 0x7FFFFFFF;
            return hash <= 65535 ? hash + 65536 : hash;
        }
    }
}
