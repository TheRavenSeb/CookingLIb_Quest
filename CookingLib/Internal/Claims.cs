using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Il2CppAlta.Alchemy;
using Il2CppAlta.Cooking;
using Il2CppAlta.Inventory;
using Il2CppAlta.Liquid;
using Il2CppAlta.Networking;

namespace CookingLib
{
    internal static class Claims
    {
        static readonly List<Holding> held = new List<Holding>();
        static readonly List<Refusal> refused = new List<Refusal>();

        internal static IReadOnlyList<Holding> Held => held;

        internal static IReadOnlyList<Refusal> Refused => refused;

        internal static Claim Pot(string knob, string value) => new Claim("pots/" + knob, "pots " + knob, value);

        internal static Claim Food(uint prefabHash, string name, string field, string value) =>
            new Claim("food/" + prefabHash + "/" + field, "food " + name + " " + field, value);

        internal static Claim Ingredient(Item item, string field, string value) =>
            new Claim("ingredient/" + item.Hash + "/" + field, "ingredient " + item.name + " " + field, value);

        internal static Claim Garnish(uint itemHash, string name, string field, string value) =>
            new Claim("garnish/" + itemHash + "/" + field, "garnish " + name + " " + field, value);

        internal static Claim Stew(uint prefabHash, string name, string field, string value) =>
            new Claim("stew/" + prefabHash + "/" + field, "stew " + name + " " + field, value);

        internal static Claim Recipe(StewRecipe recipe, string field, string value) =>
            new Claim("recipe/" + recipe.Hash + "/" + field, "recipe " + recipe.name + " " + field, value);

        internal static Claim Liquid(LiquidDefinition liquid, string field, string value) =>
            new Claim("liquid/" + liquid.Hash + "/" + field, "liquid " + liquid.name + " " + field, value);

        internal static Claim Roast(NetworkPrefab prefab, string value) => new Claim("roast/" + prefab.Hash, "roast " + prefab.name, value);

        internal static Claim RecipeNumber(uint number, string value) => new Claim("recipe number/" + number, "recipe number " + number, value);

        internal static string Effects(IEnumerable<EffectDefinitionApplication> effects)
        {
            List<string> parts = (effects ?? Enumerable.Empty<EffectDefinitionApplication>()).Where(e => e != null && e.EffectToModify != null)
                .Select(e => EffectNames.ShortName(e.EffectToModify) + " " + Number(e.StrengthMultiplier)).ToList();
            parts.Sort(StringComparer.OrdinalIgnoreCase);
            return parts.Count == 0 ? "none" : string.Join(", ", parts.ToArray());
        }

        internal static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        internal static string Take(string id, IEnumerable<Claim> claims)
        {
            List<Claim> wanted = claims.ToList();
            List<string> clashes = new List<string>();
            foreach (Claim claim in wanted)
            {
                Holding holding = Find(claim.Key);
                if (holding == null || holding.Value == claim.Value || holding.Holders.Contains(id))
                    continue;

                clashes.Add(claim.Label + " is already " + holding.Value + ", set by " + holding.Owner);
                refused.Add(new Refusal(claim, holding.Owner, holding.Value, id));
            }
            if (clashes.Count > 0)
                return string.Join("; ", clashes.ToArray());

            foreach (Claim claim in wanted)
            {
                Holding holding = Find(claim.Key);
                if (holding == null)
                    held.Add(holding = new Holding(claim));
                if (!holding.Holders.Contains(id))
                    holding.Holders.Add(id);
            }
            return null;
        }

        internal static List<string> Release(string id)
        {
            List<string> freed = new List<string>();
            foreach (Holding holding in held.ToList())
            {
                if (!holding.Holders.Remove(id) || holding.Holders.Count > 0)
                    continue;
                held.Remove(holding);
                freed.Add(holding.Key);
            }
            refused.RemoveAll(r => r.Id == id || r.Owner == id);
            return freed;
        }

        internal static void Forget(string id) => refused.RemoveAll(r => r.Id == id);

        internal static string OwnerOf(string key) => Find(key)?.Owner;

        internal static bool Holds(string id) => held.Any(h => h.Holders.Any(holder => string.Equals(holder, id, StringComparison.OrdinalIgnoreCase)));

        static Holding Find(string key) => held.FirstOrDefault(h => h.Key == key);

        internal sealed class Claim
        {
            internal Claim(string key, string label, string value)
            {
                Key = key;
                Label = label;
                Value = value ?? "none";
            }

            internal string Key { get; }

            internal string Label { get; }

            internal string Value { get; }
        }

        internal sealed class Holding
        {
            internal Holding(Claim claim)
            {
                Key = claim.Key;
                Label = claim.Label;
                Value = claim.Value;
            }

            internal string Key { get; }

            internal string Label { get; }

            internal string Value { get; }

            internal List<string> Holders { get; } = new List<string>();

            internal string Owner => Holders.Count == 0 ? null : Holders[0];
        }

        internal sealed class Refusal
        {
            internal Refusal(Claim claim, string owner, string ownerValue, string id)
            {
                Label = claim.Label;
                Value = claim.Value;
                Owner = owner;
                OwnerValue = ownerValue;
                Id = id;
            }

            internal string Label { get; }

            internal string Value { get; }

            internal string Owner { get; }

            internal string OwnerValue { get; }

            internal string Id { get; }
        }
    }
}
