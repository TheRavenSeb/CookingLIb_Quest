using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Il2CppAlta;
using Il2CppAlta.Alchemy;
using Il2CppAlta.Cooking;
using Il2CppAlta.Liquid;
using UnityEngine;

namespace CookingLib
{
    /// <summary>Changes the game's own stew recipes in place, or turns one off. Nothing is saved, so without the mod the game's recipes come back.</summary>
    public static class VanillaRecipes
    {
        static readonly List<LibVanillaRecipe> all = new List<LibVanillaRecipe>();
        static readonly Dictionary<StewRecipe, Override> overrides = new Dictionary<StewRecipe, Override>();
        static bool worldUp;

        /// <summary>Every change mods asked for and have not reverted, refused ones included.</summary>
        public static IReadOnlyList<LibVanillaRecipe> All => all;

        /// <summary>Changes a game recipe. Call it from Main.Setup on the server and on players' machines. Page fields apply at once, the rest on the server once the world is up. Never throws.</summary>
        public static LibVanillaRecipe Modify(VanillaRecipeSpec spec) => Add(new LibVanillaRecipe(spec, false));

        /// <summary>Turns a game recipe off: no pot makes it and random recipe pages skip it. Applies on the server once the world is up, or at once after that. Never throws.</summary>
        public static LibVanillaRecipe Disable(string id, string recipe) => Add(new LibVanillaRecipe(new VanillaRecipeSpec { Id = id, Recipe = recipe }, true));

        /// <summary>True when a mod turned this recipe off and the server applied it.</summary>
        public static bool IsDisabled(StewRecipe recipe) => recipe != null && PageGuardPatch.Disabled.Contains(recipe);

        /// <summary>Undoes the change with this id and gives back the game's values. True if it was in place or waiting.</summary>
        public static bool Revert(string id)
        {
            try
            {
                List<LibVanillaRecipe> gone = all.Where(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)).ToList();
                LibVanillaRecipe edit = gone.FirstOrDefault(e => e.Error == null);
                all.RemoveAll(gone.Contains);
                if (edit != null)
                    VanillaEdits.Release(edit.Id);
                else
                    Claims.Forget(id);
                if (edit == null)
                    return false;

                edit.IsApplied = edit.PagesApplied = edit.WorldApplied = false;
                Log.Info("vanilla recipe " + edit.Id + " reverted");
                Changed();
                return true;
            }
            catch (Exception e)
            {
                Safe.Fail("vanilla recipe revert", e);
                return false;
            }
        }

        internal static IEnumerable<LibVanillaRecipe> Live => all.Where(e => e.Error == null);

        internal static Override OverrideOf(StewRecipe recipe) => recipe != null && overrides.TryGetValue(recipe, out Override found) ? found : null;

        internal static void ApplyAll()
        {
            worldUp = true;
            foreach (LibVanillaRecipe edit in Live.ToList())
                ApplyWorld(edit);
            Sync();
        }

        internal static string Fields(LibVanillaRecipe edit)
        {
            VanillaRecipeSpec spec = edit.Spec;
            List<string> parts = new List<string>();
            if (spec.Tint.HasValue)
                parts.Add("Tint");
            if (spec.IgnoresDyes)
                parts.Add("IgnoresDyes");
            if (!string.IsNullOrEmpty(spec.ResultLiquid))
                parts.Add("ResultLiquid");
            if (spec.BaseLiquids != null)
                parts.Add("BaseLiquids");
            return string.Join(", ", parts.ToArray());
        }

        internal static string PageFields(LibVanillaRecipe edit)
        {
            List<string> parts = new List<string>();
            if (edit.Spec.ExtraEffects != null)
                parts.Add("ExtraEffects");
            if (edit.Spec.EffectModifiers != null)
                parts.Add("EffectModifiers");
            return string.Join(", ", parts.ToArray());
        }

        internal static string PageText(EffectDefinitionApplication[] extras, EffectDefinitionApplication[] modifiers) =>
            "extras " + VanillaEdits.Text(extras) + "; modifiers " + VanillaEdits.Text(modifiers);

        internal static string BaseText(IEnumerable<LiquidDefinition> liquids)
        {
            List<string> names = (liquids ?? Enumerable.Empty<LiquidDefinition>()).Select(LiquidName).ToList();
            return names.Count == 0 ? "none" : string.Join(", ", names.ToArray());
        }

        static LibVanillaRecipe Add(LibVanillaRecipe edit)
        {
            try
            {
                Validate(edit);
                string clash = Claims.Take(edit.Id, ClaimsOf(edit));
                if (clash != null)
                    throw new ArgumentException(clash);
            }
            catch (Exception e)
            {
                edit.Error = e is ArgumentException ? e.Message : e.ToString();
            }

            all.Add(edit);
            if (edit.Error != null)
            {
                Log.Warn("vanilla recipe " + edit.Id + " refused: " + edit.Error);
                return edit;
            }

            ApplyPages(edit);
            if (worldUp)
            {
                ApplyWorld(edit);
                Changed();
            }
            Applied(edit);
            return edit;
        }

        static void Changed()
        {
            Sync();
            if (worldUp)
                Reachability.Check();
        }

        static void Sync()
        {
            overrides.Clear();
            PageGuardPatch.Disabled.Clear();
            foreach (LibVanillaRecipe edit in Live.Where(e => e.WorldApplied))
            {
                if (edit.IsDisable)
                {
                    PageGuardPatch.Disabled.Add(edit.Recipe);
                    continue;
                }

                VanillaRecipeSpec spec = edit.Spec;
                bool colour = spec.Tint.HasValue || spec.IgnoresDyes;
                if (!colour && edit.Result == null)
                    continue;
                if (!overrides.TryGetValue(edit.Recipe, out Override found))
                    overrides[edit.Recipe] = found = new Override();
                if (colour)
                {
                    found.Tint = spec.Tint;
                    found.IgnoresDyes = spec.IgnoresDyes;
                }
                if (edit.Result != null)
                    found.Result = edit.Result;
            }
            LiquidIdentity.Forget();
        }

        static void ApplyPages(LibVanillaRecipe edit)
        {
            if (!edit.HasPages)
                return;

            try
            {
                StewRecipe recipe = edit.Recipe;
                Claims.Claim key = Key(recipe, "effects");
                string was = PageText(recipe.ExtraEffects, recipe.EffectModifiers);
                VanillaEdits.Write(key, recipe, was, VanillaEdits.ExtraEffects, edit.Extras.ToArray(), () => VanillaEdits.ResetModifiers(recipe));
                VanillaEdits.Write(key, recipe, was, VanillaEdits.EffectModifiers, edit.Modifiers.ToArray());
                edit.PagesApplied = true;
                Log.Info("vanilla recipe " + edit.Id + ": " + recipe.name + " page fields set, " + PageText(edit.Extras, edit.Modifiers));
            }
            catch (Exception e)
            {
                Safe.Fail("vanilla recipe " + edit.Id + " page fields", e);
            }
        }

        static void ApplyWorld(LibVanillaRecipe edit)
        {
            if (!Main.IsServer || !edit.HasWorld || edit.Error != null)
            {
                Applied(edit);
                return;
            }

            try
            {
                StewRecipe recipe = edit.Recipe;
                string bases = BaseText(VanillaEdits.PossibleLiquids.GetValue(recipe) as List<LiquidDefinition>);
                VanillaRecipeSpec spec = edit.Spec;
                if (edit.IsDisable)
                {
                    VanillaEdits.Write(Key(recipe, "base liquids"), recipe, bases, VanillaEdits.PossibleLiquids, new List<LiquidDefinition>());
                    Log.Info("vanilla recipe " + edit.Id + ": " + recipe.name + " turned off");
                }
                else
                {
                    edit.Result = string.IsNullOrEmpty(spec.ResultLiquid) ? null : LiquidRef.Resolve(spec.ResultLiquid);
                    if (spec.BaseLiquids != null)
                    {
                        edit.Bases = spec.BaseLiquids.Select(name => LiquidRef.Resolve(name).Definition).ToList();
                        if (edit.Bases.Any(liquid => liquid == null))
                            throw new ArgumentException("a base liquid has no definition");
                        VanillaEdits.Write(Key(recipe, "base liquids"), recipe, bases, VanillaEdits.PossibleLiquids, new List<LiquidDefinition>(edit.Bases));
                    }
                    if (spec.Tint.HasValue || spec.IgnoresDyes)
                        VanillaEdits.Write(Key(recipe, "colour"), recipe, "the stew's own");
                    if (edit.Result != null)
                        VanillaEdits.Write(Key(recipe, "result"), recipe, "none");
                    Log.Info("vanilla recipe " + edit.Id + ": " + recipe.name + " " + Fields(edit) + " applied");
                }
                edit.WorldApplied = true;
            }
            catch (Exception e)
            {
                edit.Error = e is ArgumentException ? e.Message : e.ToString();
                edit.PagesApplied = edit.WorldApplied = false;
                VanillaEdits.Release(edit.Id);
                Log.Warn("vanilla recipe " + edit.Id + " refused: " + edit.Error);
            }
            Applied(edit);
        }

        static void Applied(LibVanillaRecipe edit)
        {
            bool pages = !edit.HasPages || edit.PagesApplied;
            bool world = !Main.IsServer || !edit.HasWorld || edit.WorldApplied;
            edit.IsApplied = edit.Error == null && pages && world && (edit.HasPages || Main.IsServer && edit.WorldApplied);
        }

        static List<Claims.Claim> ClaimsOf(LibVanillaRecipe edit)
        {
            StewRecipe recipe = edit.Recipe;
            List<Claims.Claim> claims = new List<Claims.Claim>();
            if (edit.IsDisable)
            {
                foreach (string field in new[] { "effects", "colour", "result", "base liquids" })
                    claims.Add(Claims.Recipe(recipe, field, "disabled"));
                return claims;
            }

            VanillaRecipeSpec spec = edit.Spec;
            if (edit.HasPages)
                claims.Add(Claims.Recipe(recipe, "effects", PageText(edit.Extras, edit.Modifiers)));
            if (spec.Tint.HasValue || spec.IgnoresDyes)
                claims.Add(Claims.Recipe(recipe, "colour", (spec.Tint.HasValue ? "tint " + Rgba(spec.Tint.Value) : "the stew's own") + (spec.IgnoresDyes ? ", ignores dyes" : "")));
            if (!string.IsNullOrEmpty(spec.ResultLiquid))
                claims.Add(Claims.Recipe(recipe, "result", LiquidText(spec.ResultLiquid)));
            if (spec.BaseLiquids != null)
                claims.Add(Claims.Recipe(recipe, "base liquids", string.Join(", ", spec.BaseLiquids.Select(LiquidText).Distinct().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray())));
            return claims;
        }

        static void Validate(LibVanillaRecipe edit)
        {
            VanillaRecipeSpec spec = edit.Spec;
            if (spec == null)
                throw new ArgumentException("spec is null");
            if (string.IsNullOrEmpty(spec.Id) || spec.Id.IndexOf('.') <= 0)
                throw new ArgumentException("id must look like author.thing");
            if (Live.Any(e => string.Equals(e.Id, spec.Id, StringComparison.OrdinalIgnoreCase)) || Claims.Holds(spec.Id))
                throw new ArgumentException("id is already used, Revert it first");
            if (string.IsNullOrEmpty(spec.Recipe))
                throw new ArgumentException("Recipe is empty");
            edit.Recipe = NeedRecipe(spec.Recipe);
            if (edit.IsDisable)
                return;

            if (!edit.HasPages && !edit.HasWorld)
                throw new ArgumentException("set at least one of ExtraEffects, EffectModifiers, Tint, IgnoresDyes, ResultLiquid, BaseLiquids");
            if (edit.HasPages && Main.SetupDone)
                throw new ArgumentException("ExtraEffects and EffectModifiers change journal pages, so set them from Main.Setup");
            if (spec.Tint.HasValue && !LiquidFactory.IsValidColor(spec.Tint.Value))
                throw new ArgumentException("tint channels must be 0 to 1");
            if (spec.BaseLiquids != null && (spec.BaseLiquids.Count == 0 || spec.BaseLiquids.Any(string.IsNullOrEmpty)))
                throw new ArgumentException("BaseLiquids is empty or holds an empty name");
            if (!edit.HasPages)
                return;

            edit.Extras = VanillaEdits.Effects(spec.ExtraEffects, "ExtraEffects") ?? (edit.Recipe.ExtraEffects ?? new EffectDefinitionApplication[0]).ToArray();
            edit.Modifiers = VanillaEdits.Effects(spec.EffectModifiers, "EffectModifiers") ?? (edit.Recipe.EffectModifiers ?? new EffectDefinitionApplication[0]).ToArray();
            PageBudget.Check(PageKind.Recipe, edit.Recipe.name + "'s recipe page", edit.Extras.Concat(edit.Modifiers), Reachability.SlotsOf(edit.Recipe));
        }

        static StewRecipe NeedRecipe(string name)
        {
            StewRecipe recipe = Names.Find<StewRecipe>(name);
            if (recipe != null && StewRecipes.Find(recipe) != null)
                throw new ArgumentException(recipe.name + " is a CookingLib recipe, change its own spec");
            if (recipe != null)
                return recipe;

            IEnumerable<string> names = HashedGeneralValue<StewRecipe>.All.Where(r => r != null && StewRecipes.Find(r) == null).Select(r => r.name);
            throw new ArgumentException("no game stew recipe named '" + name + "'" + Names.Suggest(name, names, "Stew recipes"));
        }

        static Claims.Claim Key(StewRecipe recipe, string field) => Claims.Recipe(recipe, field, null);

        static string LiquidText(string name)
        {
            try
            {
                LiquidRef liquid = LiquidRef.Resolve(name);
                return liquid.Lib != null ? liquid.Lib.Id : liquid.Vanilla.name;
            }
            catch (ArgumentException)
            {
                return name.Trim();
            }
        }

        static string LiquidName(LiquidDefinition liquid)
        {
            if (liquid == null)
                return "none";
            LibLiquid ours = Liquids.FindByDefinition(liquid);
            return ours != null ? ours.Id : liquid.name;
        }

        static string Rgba(Color c) =>
            string.Join(" ", new[] { c.r, c.g, c.b, c.a }.Select(v => v.ToString("0.###", CultureInfo.InvariantCulture)).ToArray());

        internal sealed class Override
        {
            internal Color? Tint { get; set; }

            internal bool IgnoresDyes { get; set; }

            internal LiquidRef Result { get; set; }
        }
    }
}
