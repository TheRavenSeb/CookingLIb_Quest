using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Il2CppAlta.Cooking;
using UnityEngine;

namespace CookingLib
{
    /// <summary>Changes the game's stew prefabs: how much the ingredients, the base liquid and the recipe count at each stage, and the stew's colour. Server only. Nothing is saved, so without the mod the game's values come back.</summary>
    public static class VanillaStews
    {
        static readonly FieldInfo[] fields = { VanillaEdits.StewIngredients, VanillaEdits.StewBaseLiquid, VanillaEdits.StewExtras, VanillaEdits.StewTint };
        static readonly List<LibStew> all = new List<LibStew>();
        static bool worldUp;

        /// <summary>Every change mods asked for and have not reverted, refused ones included.</summary>
        public static IReadOnlyList<LibStew> All => all;

        /// <summary>Changes a stew prefab. Call it from Main.Setup or later. The server applies it once the world is up, or at once after that, to stews already in pots too. Never throws.</summary>
        public static LibStew Modify(StewSpec spec)
        {
            LibStew entry = new LibStew(spec);
            try
            {
                Check(entry);
            }
            catch (Exception e)
            {
                entry.Error = e is ArgumentException ? e.Message : e.ToString();
            }

            all.Add(entry);
            if (entry.Error != null)
            {
                Log.Warn("vanilla stew " + entry.Id + " refused: " + entry.Error);
                return entry;
            }

            if (worldUp && Apply(entry))
                Sweep(new[] { entry.Prefab.Hash });
            return entry;
        }

        /// <summary>Undoes the change with this id and gives back the game's values, on stews already in pots too. True if it was in place or waiting.</summary>
        public static bool Revert(string id)
        {
            try
            {
                List<LibStew> gone = all.Where(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)).ToList();
                LibStew entry = gone.FirstOrDefault(e => e.Error == null);
                all.RemoveAll(gone.Contains);
                List<string> freed = new List<string>();
                if (entry != null)
                    freed.AddRange(VanillaEdits.Release(entry.Id));
                else
                    Claims.Forget(id);
                if (entry == null)
                    return false;

                bool applied = entry.IsApplied && entry.Prefab != null;
                entry.IsApplied = false;
                Log.Info("vanilla stew " + entry.Id + " reverted");
                if (applied && freed.Count > 0)
                    Sweep(new[] { entry.Prefab.Hash });
                return true;
            }
            catch (Exception e)
            {
                Safe.Fail("vanilla stew revert", e);
                return false;
            }
        }

        internal static IEnumerable<LibStew> Live => all.Where(e => e.Error == null);

        internal static int Swept { get; private set; }

        internal static int Reloaded { get; private set; }

        internal static int Copied { get; private set; }

        internal static void ApplyAll()
        {
            worldUp = true;
            List<uint> changed = new List<uint>();
            foreach (LibStew entry in Live.ToList())
            {
                if (Apply(entry) && !changed.Contains(entry.Prefab.Hash))
                    changed.Add(entry.Prefab.Hash);
            }
            if (changed.Count > 0)
                Sweep(changed);
        }

        internal static void CopyOnto(CookingPot pot)
        {
            if (!Live.Any(e => e.IsApplied))
                return;

            foreach (StewComponent stew in pot.GetComponentsInChildren<StewComponent>(true))
            {
                Reloaded++;
                if (Copy(stew))
                    Copied++;
            }
        }

        internal static StewStages.Entry NeedStew(string name)
        {
            if (!StewStages.Ready())
                throw new ArgumentException(StewStages.Error);

            string key = Names.Norm(name);
            StewStages.Entry found = StewStages.All.FirstOrDefault(e => Names.Norm(e.Name) == key || e.Hash.ToString() == key);
            if (found == null)
                throw new ArgumentException("no stew prefab named '" + name + "'" + Names.Suggest(name, StewStages.All.Select(e => e.Name), "Stews"));
            return found;
        }

        internal static string Rgba(Color c) =>
            string.Join(" ", new[] { c.r, c.g, c.b, c.a }.Select(v => v.ToString("0.###", CultureInfo.InvariantCulture)).ToArray());

        static void Check(LibStew entry)
        {
            StewSpec spec = entry.Spec;
            if (spec == null)
                throw new ArgumentException("spec is null");
            if (string.IsNullOrEmpty(spec.Id) || spec.Id.IndexOf('.') <= 0)
                throw new ArgumentException("id must look like author.thing");
            if (Live.Any(e => string.Equals(e.Id, spec.Id, StringComparison.OrdinalIgnoreCase)) || Claims.Holds(spec.Id))
                throw new ArgumentException("id is already used, Revert it first");
            if (string.IsNullOrEmpty(spec.Stew))
                throw new ArgumentException("Stew is empty");
            if (!spec.Ingredients.HasValue && !spec.BaseLiquid.HasValue && !spec.Extras.HasValue && !spec.Tint.HasValue)
                throw new ArgumentException("set at least one of Ingredients, BaseLiquid, Extras, Tint");
            if (spec.Ingredients.HasValue && !VanillaEdits.IsMultiplier(spec.Ingredients.Value))
                throw new ArgumentException("Ingredients must be 0 to 10");
            if (spec.BaseLiquid.HasValue && !VanillaEdits.IsMultiplier(spec.BaseLiquid.Value))
                throw new ArgumentException("BaseLiquid must be 0 to 10");
            if (spec.Extras.HasValue && !VanillaEdits.IsMultiplier(spec.Extras.Value))
                throw new ArgumentException("Extras must be 0 to 10");
            if (spec.Tint.HasValue && !LiquidFactory.IsValidColor(spec.Tint.Value))
                throw new ArgumentException("tint channels must be 0 to 1");
        }

        static bool Apply(LibStew entry)
        {
            if (!Main.IsServer || entry.Error != null)
                return false;

            try
            {
                StewStages.Entry stage = NeedStew(entry.Spec.Stew);
                if (Claims.Holds(entry.Id))
                    throw new ArgumentException("id is already used, Revert it first");
                string clash = Claims.Take(entry.Id, ClaimsOf(entry, stage));
                if (clash != null)
                    throw new ArgumentException(clash);

                entry.Prefab = stage.Prefab;
                entry.Entry = stage;
                StewComponent stew = stage.Stew;
                StewSpec spec = entry.Spec;
                List<string> done = new List<string>();
                if (spec.Ingredients.HasValue)
                {
                    VanillaEdits.Write(Key(stage, "ingredients"), stew, "x" + Claims.Number(stew.BaseEffectsMultiplier), VanillaEdits.StewIngredients, spec.Ingredients.Value);
                    done.Add("ingredients x" + Claims.Number(spec.Ingredients.Value));
                }
                if (spec.BaseLiquid.HasValue)
                {
                    VanillaEdits.Write(Key(stage, "liquid"), stew, "x" + Claims.Number(stew.BaseLiquidEffectsMultiplier), VanillaEdits.StewBaseLiquid, spec.BaseLiquid.Value);
                    done.Add("liquid x" + Claims.Number(spec.BaseLiquid.Value));
                }
                if (spec.Extras.HasValue)
                {
                    VanillaEdits.Write(Key(stage, "extras"), stew, "x" + Claims.Number(stew.ExtraEffectsMultiplier), VanillaEdits.StewExtras, spec.Extras.Value);
                    done.Add("extras x" + Claims.Number(spec.Extras.Value));
                }
                if (spec.Tint.HasValue)
                {
                    VanillaEdits.Write(Key(stage, "tint"), stew, Rgba(stew.TintColor), VanillaEdits.StewTint, spec.Tint.Value);
                    done.Add("tint " + Rgba(spec.Tint.Value));
                }

                entry.Notes.Clear();
                Warn(entry, stage);
                entry.IsApplied = true;
                Log.Info("vanilla stew " + entry.Id + ": " + stage.Name + " " + string.Join(", ", done.ToArray())
                    + (entry.Notes.Count == 0 ? "" : "; " + string.Join("; ", entry.Notes.ToArray())));
                return true;
            }
            catch (Exception e)
            {
                entry.Error = e is ArgumentException ? e.Message : e.ToString();
                entry.IsApplied = false;
                entry.Prefab = null;
                VanillaEdits.Release(entry.Id);
                Log.Warn("vanilla stew " + entry.Id + " refused: " + entry.Error);
                return false;
            }
        }

        static void Warn(LibStew entry, StewStages.Entry stage)
        {
            List<StewRecipe> recipes = stage.Recipes;
            if (entry.Spec.Extras.HasValue)
            {
                entry.Notes.Add(recipes.Count == 0 ? "no recipe makes this stew, so Extras changes nothing"
                    : "Extras also scales the modifiers of " + string.Join(", ", recipes.Select(RecipeName).ToArray()));
            }
            if (!entry.Spec.Tint.HasValue)
                return;

            List<string> borrowed = recipes.Select(StewRecipes.Find).Where(r => r != null && r.Spec != null && !r.Spec.Tint.HasValue).Select(r => r.Id).ToList();
            if (borrowed.Count > 0)
                entry.Notes.Add("Tint also colours " + string.Join(", ", borrowed.ToArray()) + ", which borrow this look and set no Tint");
        }

        static string RecipeName(StewRecipe recipe)
        {
            LibStewRecipe ours = StewRecipes.Find(recipe);
            return ours != null ? ours.Id : recipe.name;
        }

        static IEnumerable<Claims.Claim> ClaimsOf(LibStew entry, StewStages.Entry stage)
        {
            StewSpec spec = entry.Spec;
            if (spec.Ingredients.HasValue)
                yield return Claims.Stew(stage.Hash, stage.Name, "ingredients", Claims.Number(spec.Ingredients.Value));
            if (spec.BaseLiquid.HasValue)
                yield return Claims.Stew(stage.Hash, stage.Name, "liquid", Claims.Number(spec.BaseLiquid.Value));
            if (spec.Extras.HasValue)
                yield return Claims.Stew(stage.Hash, stage.Name, "extras", Claims.Number(spec.Extras.Value));
            if (spec.Tint.HasValue)
                yield return Claims.Stew(stage.Hash, stage.Name, "tint", Rgba(spec.Tint.Value));
        }

        static void Sweep(IEnumerable<uint> hashes)
        {
            HashSet<uint> wanted = new HashSet<uint>(hashes);
            int count = Safe.Run("stew sweep", () => SweepNow(wanted), 0);
            Swept += count;
            Log.Info("stews: swept " + count);
        }

        static int SweepNow(HashSet<uint> hashes)
        {
            int count = 0;
            foreach (StewComponent live in UnityEngine.Object.FindObjectsOfType<StewComponent>())
            {
                if (hashes.Contains(HashOf(live)) && Copy(live))
                    count++;
            }
            return count;
        }

        static bool Copy(StewComponent live)
        {
            StewStages.Entry entry = StewStages.Get(HashOf(live));
            StewComponent source = entry == null ? null : entry.Stew;
            if (source == null || source == live)
                return false;

            bool changed = false;
            foreach (FieldInfo field in fields)
            {
                object value = field.GetValue(source);
                if (Equals(field.GetValue(live), value))
                    continue;
                field.SetValue(live, value);
                changed = true;
            }
            return changed;
        }

        static uint HashOf(StewComponent stew) => stew.Entity != null && stew.Entity.Prefab != null ? stew.Entity.Prefab.Hash : 0;

        static Claims.Claim Key(StewStages.Entry stage, string field) => Claims.Stew(stage.Hash, stage.Name, field, null);
    }
}
