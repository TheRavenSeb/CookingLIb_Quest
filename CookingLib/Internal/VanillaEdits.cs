using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppAlta.Alchemy;
using Il2CppAlta.Cooking;
using Il2CppAlta.Liquid;

namespace CookingLib
{
    internal static class VanillaEdits
    {
        const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        internal static readonly FieldInfo ExtraEffects = typeof(StewRecipe).GetField("extraEffects", PrivateInstance);
        internal static readonly FieldInfo EffectModifiers = typeof(StewRecipe).GetField("effectModifiers", PrivateInstance);
        internal static readonly FieldInfo PossibleLiquids = typeof(StewRecipe).GetField("possibleLiquids", PrivateInstance);
        internal static readonly FieldInfo CookableEffects = typeof(CookableItem).GetField("effects", PrivateInstance);
        internal static readonly FieldInfo CookableChunk = typeof(CookableItem).GetField("chunkVisual", PrivateInstance);
        internal static readonly FieldInfo CookableCategory = typeof(CookableItem).GetField("category", PrivateInstance);
        internal static readonly FieldInfo FoodEffects = typeof(EdibleObject).GetField("consumptionEffects", PrivateInstance);
        internal static readonly FieldInfo LiquidEffects = typeof(LiquidData).GetField("effects", PrivateInstance);
        internal static readonly FieldInfo GarnishMultipliers = typeof(CookingGarnish).GetField("effectMultipliers", PrivateInstance);
        internal static readonly FieldInfo StewTint = typeof(StewComponent).GetField("tintColor", PrivateInstance);
        internal static readonly FieldInfo StewIngredients = typeof(StewComponent).GetField("baseEffectsMultiplier", PrivateInstance);
        internal static readonly FieldInfo StewBaseLiquid = typeof(StewComponent).GetField("baseLiquidEffectsMultiplier", PrivateInstance);
        internal static readonly FieldInfo StewExtras = typeof(StewComponent).GetField("extraEffectsMultiplier", PrivateInstance);

        const uint FullnessHash = 34710;

        static readonly FieldInfo duration = typeof(EffectDefinitionApplication).GetField("durationInSeconds", PrivateInstance);

        static readonly FieldInfo modifiersMap = typeof(StewRecipe).GetField("effectModifiersMap", PrivateInstance);
        static readonly FieldInfo allCookables = typeof(CookableCategoryDefinition).GetField("allCookableItems", BindingFlags.NonPublic | BindingFlags.Static);

        static readonly List<Edit> edits = new List<Edit>();

        internal static IReadOnlyList<Edit> All => edits;

        internal static IEnumerable<Edit> Of(object target) => edits.Where(e => ReferenceEquals(e.Target, target));

        internal static Edit Find(string key) => edits.FirstOrDefault(e => e.Key == key);

        internal static object Original(object target, FieldInfo field, object live)
        {
            foreach (Edit edit in Of(target))
            {
                if (edit.TryOriginal(field, out object saved))
                    return saved;
            }
            return live;
        }

        internal static Edit Write(Claims.Claim claim, object target, string was, FieldInfo field = null, object value = null, Action reset = null)
        {
            Edit edit = Find(claim.Key);
            if (edit == null)
                edits.Add(edit = new Edit(claim, target, was, reset));
            if (field != null)
                edit.Set(field, value);
            edit.Reset?.Invoke();
            return edit;
        }

        internal static List<string> Release(string id)
        {
            List<string> freed = Claims.Release(id);
            foreach (string key in freed)
            {
                Edit edit = Find(key);
                if (edit == null)
                    continue;
                edits.Remove(edit);
                Safe.Run("vanilla restore " + edit.Label, edit.Undo);
            }
            return freed;
        }

        internal static void ResetModifiers(StewRecipe recipe) => modifiersMap.SetValue(recipe, null);

        internal static void ResetCategories() => allCookables.SetValue(null, null);

        internal static EffectDefinitionApplication[] Effects(List<EffectSpec> specs, string field)
        {
            if (specs == null)
                return null;
            if (specs.Contains(null))
                throw new ArgumentException(field + " can't hold a null effect");
            EffectSpread.Refuse(specs, field);

            List<EffectDefinitionApplication> effects = new List<EffectDefinitionApplication>();
            foreach (EffectSpec spec in specs)
            {
                if (!LiquidFactory.IsWithin100(spec.Strength))
                    throw new ArgumentException("effect strengths must be minus 100 to 100");
                EffectDefinition definition = EffectNames.Need(spec.Effect, EffectKinds.Game);
                if (effects.Any(e => e.EffectToModify == definition))
                    throw new ArgumentException(field + " lists " + definition.name + " twice");
                effects.Add(new EffectDefinitionApplication(definition, spec.Strength));
            }
            return effects.ToArray();
        }

        internal static EffectDefinitionApplication[] Multipliers(List<EffectSpec> specs, string field)
        {
            if (specs == null)
                return null;
            if (specs.Contains(null))
                throw new ArgumentException(field + " can't hold a null effect");
            EffectSpread.Refuse(specs, field);

            List<EffectDefinitionApplication> effects = new List<EffectDefinitionApplication>();
            foreach (EffectSpec spec in specs)
            {
                if (!IsMultiplier(spec.Strength))
                    throw new ArgumentException(field + " must be 0 to 10");
                EffectDefinition definition = EffectNames.Need(spec.Effect, EffectKinds.Game);
                if (effects.Any(e => e.EffectToModify == definition))
                    throw new ArgumentException(field + " lists " + definition.name + " twice");
                effects.Add(new EffectDefinitionApplication(definition, spec.Strength));
            }
            return effects.ToArray();
        }

        internal static bool IsMultiplier(float value) => !float.IsNaN(value) && value >= 0f && value <= 10f;

        internal static EffectDefinitionApplication[] Replace(List<EffectSpec> specs, EffectDefinitionApplication[] game, string what)
        {
            if (specs == null)
                throw new ArgumentException("Effects is null, use an empty list for none");
            if (specs.Contains(null))
                throw new ArgumentException("Effects can't hold a null effect");

            List<EffectDefinitionApplication> effects = new List<EffectDefinitionApplication>();
            foreach (EffectSpec spec in specs)
            {
                if (!LiquidFactory.IsWithin100(spec.Strength))
                    throw new ArgumentException("effect strengths must be minus 100 to 100");
                EffectDefinition definition = EffectNames.Need(spec.Effect, EffectKinds.Game);
                if (IsFullness(definition))
                    throw new ArgumentException(definition.name + " changes fullness, which stays the game's on " + what + " because vanilla players judge fullness with their own copy. Raise Nourish instead");
                if (effects.Any(e => e.EffectToModify == definition))
                    throw new ArgumentException("Effects lists " + definition.name + " twice");

                int seconds = EffectSpread.Check(spec, definition);
                EffectDefinitionApplication own = game?.FirstOrDefault(e => e != null && e.EffectToModify == definition);
                EffectDefinitionApplication entry = seconds == 0 && own != null ? own.Copy() : new EffectDefinitionApplication(definition, spec.Strength, seconds);
                entry.StrengthMultiplier = spec.Strength;
                effects.Add(entry);
            }

            for (int i = 0; game != null && i < game.Length; i++)
            {
                if (game[i] != null && IsFullness(game[i].EffectToModify))
                    effects.Insert(Math.Min(i, effects.Count), game[i].Copy());
            }
            return effects.ToArray();
        }

        internal static bool IsFullness(EffectDefinition effect) =>
            effect is StatChangeEffect stat && stat.StatToAffect != null && stat.StatToAffect.Hash == FullnessHash;

        internal static int Seconds(EffectDefinitionApplication effect) => effect == null || duration == null ? 0 : (int)duration.GetValue(effect);

        internal static bool Same(EffectDefinitionApplication[] a, EffectDefinitionApplication[] b)
        {
            if (a == null || b == null)
                return a == b;
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] == null || b[i] == null)
                {
                    if (a[i] != b[i])
                        return false;
                    continue;
                }
                if (a[i].EffectToModify != b[i].EffectToModify || a[i].StrengthMultiplier != b[i].StrengthMultiplier || Seconds(a[i]) != Seconds(b[i]))
                    return false;
            }
            return true;
        }

        internal static string Text(IEnumerable<EffectDefinitionApplication> effects)
        {
            List<string> parts = (effects ?? Enumerable.Empty<EffectDefinitionApplication>()).Where(e => e != null && e.EffectToModify != null)
                .Select(e => EffectNames.ShortName(e.EffectToModify) + " " + Claims.Number(e.StrengthMultiplier) + (Seconds(e) > 0 ? " over " + Seconds(e) + " s" : "")).ToList();
            return parts.Count == 0 ? "none" : string.Join(", ", parts.ToArray());
        }

        internal sealed class Edit
        {
            readonly List<KeyValuePair<FieldInfo, object>> saved = new List<KeyValuePair<FieldInfo, object>>();

            internal Edit(Claims.Claim claim, object target, string was, Action reset)
            {
                Key = claim.Key;
                Label = claim.Label;
                Field = claim.Key.Substring(claim.Key.LastIndexOf('/') + 1);
                Target = target;
                Was = was ?? "none";
                Reset = reset;
            }

            internal string Key { get; }

            internal string Label { get; }

            internal string Field { get; }

            internal object Target { get; }

            internal string Was { get; }

            internal object Original => saved.Count == 0 ? null : saved[0].Value;

            internal string By => Claims.OwnerOf(Key);

            internal Action Reset { get; }

            internal void Set(FieldInfo field, object value)
            {
                if (!saved.Any(s => s.Key == field))
                    saved.Add(new KeyValuePair<FieldInfo, object>(field, field.GetValue(Target)));
                field.SetValue(Target, value);
            }

            internal bool TryOriginal(FieldInfo field, out object value)
            {
                foreach (KeyValuePair<FieldInfo, object> pair in saved)
                {
                    if (pair.Key != field)
                        continue;
                    value = pair.Value;
                    return true;
                }
                value = null;
                return false;
            }

            internal void Undo()
            {
                foreach (KeyValuePair<FieldInfo, object> field in saved)
                    field.Key.SetValue(Target, field.Value);
                Reset?.Invoke();
            }
        }
    }
}
