using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppAlta;
using Il2CppAlta.Cooking;

namespace CookingLib
{
    /// <summary>Changes what the game's own stew ingredients add to a stew. Nothing is saved, so without the mod the game's values come back.</summary>
    public static class VanillaIngredients
    {
        static readonly List<LibIngredient> all = new List<LibIngredient>();
        static bool worldUp;

        /// <summary>Every change mods asked for and have not reverted, refused ones included.</summary>
        public static IReadOnlyList<LibIngredient> All => all;

        /// <summary>Changes a game stew ingredient. Call it from Main.Setup on the server and on players' machines. Effects apply at once, Chunk and Category on the server once the world is up. Never throws.</summary>
        public static LibIngredient Modify(IngredientSpec spec)
        {
            LibIngredient entry = new LibIngredient(spec);
            try
            {
                Check(entry);
                string clash = Claims.Take(entry.Id, ClaimsOf(entry));
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
                Log.Warn("vanilla ingredient " + entry.Id + " refused: " + entry.Error);
                return entry;
            }

            ApplyPages(entry);
            if (worldUp)
            {
                ApplyWorld(entry);
                Reachability.Check();
            }
            Applied(entry);
            return entry;
        }

        /// <summary>Undoes the change with this id and gives back the game's values. True if it was in place or waiting.</summary>
        public static bool Revert(string id)
        {
            try
            {
                List<LibIngredient> gone = all.Where(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)).ToList();
                LibIngredient entry = gone.FirstOrDefault(e => e.Error == null);
                all.RemoveAll(gone.Contains);
                if (entry != null)
                    VanillaEdits.Release(entry.Id);
                else
                    Claims.Forget(id);
                if (entry == null)
                    return false;

                entry.IsApplied = entry.PagesApplied = entry.WorldApplied = false;
                entry.Touched = new List<string>();
                Log.Info("vanilla ingredient " + entry.Id + " reverted");
                return true;
            }
            catch (Exception e)
            {
                Safe.Fail("vanilla ingredient revert", e);
                return false;
            }
        }

        internal static IEnumerable<LibIngredient> Live => all.Where(e => e.Error == null);

        internal static void ApplyAll()
        {
            worldUp = true;
            foreach (LibIngredient entry in Live.ToList())
                ApplyWorld(entry);
        }

        internal static void Check(LibIngredient entry)
        {
            IngredientSpec spec = entry.Spec;
            if (spec == null)
                throw new ArgumentException("spec is null");
            if (string.IsNullOrEmpty(spec.Id) || spec.Id.IndexOf('.') <= 0)
                throw new ArgumentException("id must look like author.thing");
            if (Live.Any(e => string.Equals(e.Id, spec.Id, StringComparison.OrdinalIgnoreCase)) || Claims.Holds(spec.Id))
                throw new ArgumentException("id is already used, Revert it first");
            if (string.IsNullOrEmpty(spec.Item))
                throw new ArgumentException("Item is empty");
            entry.Item = Names.NeedItemExact(spec.Item);
            entry.Cookable = SlotItems.NeedVanilla(entry.Item);

            if (!entry.HasPages && !entry.HasWorld)
                throw new ArgumentException("set at least one of Effects, Chunk, Category");
            if (entry.HasPages && Main.SetupDone)
                throw new ArgumentException("Effects change journal pages, so set them from Main.Setup");
            entry.Effects = VanillaEdits.Effects(spec.Effects, "Effects");
            if (entry.Effects != null)
                PageBudget.Check(PageKind.Ingredient, entry.Item.name + "'s ingredient page", entry.Effects);
            if (!string.IsNullOrEmpty(spec.Chunk))
                entry.Chunk = Names.Need<LiquidVisualChunkDefinition>(spec.Chunk, "chunk", "Chunks");
            if (!string.IsNullOrEmpty(spec.Category))
                entry.Category = Names.NeedCategory(spec.Category);
        }

        internal static string Fields(LibIngredient entry, bool pages)
        {
            List<string> parts = new List<string>();
            if (pages && entry.Spec.Effects != null)
                parts.Add("Effects");
            if (!pages && entry.Spec.Chunk != null)
                parts.Add("Chunk");
            if (!pages && entry.Spec.Category != null)
                parts.Add("Category");
            return string.Join(", ", parts.ToArray());
        }

        static IEnumerable<Claims.Claim> ClaimsOf(LibIngredient entry)
        {
            if (entry.Effects != null)
                yield return Claims.Ingredient(entry.Item, "effects", VanillaEdits.Text(entry.Effects));
            if (entry.Spec.Chunk != null)
                yield return Claims.Ingredient(entry.Item, "chunk", entry.Chunk == null ? "none" : entry.Chunk.name);
            if (entry.Spec.Category != null)
                yield return Claims.Ingredient(entry.Item, "category", entry.Category == null ? "none" : entry.Category.name);
        }

        static void ApplyPages(LibIngredient entry)
        {
            if (!entry.HasPages)
                return;

            try
            {
                CookableItem cookable = entry.Cookable;
                VanillaEdits.Write(Key(entry, "effects"), cookable, VanillaEdits.Text(cookable.Effects), VanillaEdits.CookableEffects, entry.Effects.ToArray());
                entry.PagesApplied = true;
                Log.Info("vanilla ingredient " + entry.Id + ": " + entry.Item.name + " effects set, " + VanillaEdits.Text(entry.Effects));
            }
            catch (Exception e)
            {
                Safe.Fail("vanilla ingredient " + entry.Id + " effects", e);
            }
        }

        static void ApplyWorld(LibIngredient entry)
        {
            if (!Main.IsServer || !entry.HasWorld || entry.Error != null)
            {
                Applied(entry);
                return;
            }

            try
            {
                CookableItem cookable = entry.Cookable;
                List<string> done = new List<string>();
                if (entry.Spec.Chunk != null)
                {
                    VanillaEdits.Write(Key(entry, "chunk"), cookable, cookable.ChunkVisual == null ? "none" : cookable.ChunkVisual.name, VanillaEdits.CookableChunk, entry.Chunk);
                    done.Add("chunk " + (entry.Chunk == null ? "none" : entry.Chunk.name));
                }
                if (entry.Spec.Category != null)
                {
                    Names.Categories();
                    VanillaEdits.Edit edit = VanillaEdits.Write(Key(entry, "category"), cookable, cookable.Category == null ? "none" : cookable.Category.name,
                        VanillaEdits.CookableCategory, entry.Category, VanillaEdits.ResetCategories);
                    CookableCategoryDefinition old = Names.Categories().FirstOrDefault(c => c.name == edit.Was);
                    entry.Touched = Touched(cookable, old, entry.Category);
                    done.Add("category " + edit.Was + " to " + (entry.Category == null ? "none" : entry.Category.name)
                        + (entry.Touched.Count == 0 ? ", touches no recipe" : ", touches " + string.Join(", ", entry.Touched.ToArray())));
                }
                entry.WorldApplied = true;
                Log.Info("vanilla ingredient " + entry.Id + ": " + entry.Item.name + " " + string.Join("; ", done.ToArray()));
            }
            catch (Exception e)
            {
                entry.Error = e is ArgumentException ? e.Message : e.ToString();
                entry.PagesApplied = entry.WorldApplied = false;
                VanillaEdits.Release(entry.Id);
                Log.Warn("vanilla ingredient " + entry.Id + " refused: " + entry.Error);
            }
            Applied(entry);
        }

        static void Applied(LibIngredient entry)
        {
            bool pages = !entry.HasPages || entry.PagesApplied;
            bool world = !Main.IsServer || !entry.HasWorld || entry.WorldApplied;
            entry.IsApplied = entry.Error == null && pages && world && (entry.HasPages || Main.IsServer && entry.WorldApplied);
        }

        static List<string> Touched(CookableItem cookable, CookableCategoryDefinition old, CookableCategoryDefinition now)
        {
            List<string> touched = new List<string>();
            foreach (StewRecipe recipe in HashedGeneralValue<StewRecipe>.All.Where(r => r != null).OrderBy(r => r.name, StringComparer.OrdinalIgnoreCase))
            {
                List<StewIngredientBase> slots = recipe.StewIngredients ?? new List<StewIngredientBase>();
                List<CookableCategoryDefinition> categories = slots.OfType<CookableCategoryDefinition>().ToList();
                if (!categories.Any(c => c == old && old != null || c == now && now != null))
                    continue;

                bool order = now != null && categories.Any(c => c == now)
                    && slots.OfType<CookableCollectionDefinition>().Any(slot => slot.Cookables.Contains(cookable));
                LibStewRecipe ours = StewRecipes.Find(recipe);
                touched.Add((ours != null ? ours.Id : recipe.name) + (order ? " (drop order decides)" : ""));
            }
            return touched;
        }

        static Claims.Claim Key(LibIngredient entry, string field) => Claims.Ingredient(entry.Item, field, null);
    }
}
