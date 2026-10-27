using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppAlta;
using Il2CppAlta.Cooking;
using Il2CppAlta.Inventory;

namespace CookingLib
{
    /// <summary>Changes what the game's own garnishes, Salt and Garlic, do to a stew. Nothing is saved, so without the mod the game's values come back.</summary>
    public static class VanillaGarnishes
    {
        static readonly List<LibGarnish> all = new List<LibGarnish>();
        static bool worldUp;

        /// <summary>Every change mods asked for and have not reverted, refused ones included.</summary>
        public static IReadOnlyList<LibGarnish> All => all;

        /// <summary>Changes a game garnish. Call it from Main.Setup on the server and on players' machines. Effects apply at once, Multipliers and Chunk on the server once the world is up. Never throws.</summary>
        public static LibGarnish Modify(GarnishSpec spec)
        {
            LibGarnish entry = new LibGarnish(spec);
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
                Log.Warn("vanilla garnish " + entry.Id + " refused: " + entry.Error);
                return entry;
            }

            ApplyPages(entry);
            if (worldUp)
                ApplyWorld(entry);
            Applied(entry);
            return entry;
        }

        /// <summary>Undoes the change with this id and gives back the game's values. True if it was in place or waiting.</summary>
        public static bool Revert(string id)
        {
            try
            {
                List<LibGarnish> gone = all.Where(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)).ToList();
                LibGarnish entry = gone.FirstOrDefault(e => e.Error == null);
                all.RemoveAll(gone.Contains);
                if (entry != null)
                    VanillaEdits.Release(entry.Id);
                else
                    Claims.Forget(id);
                if (entry == null)
                    return false;

                entry.IsApplied = entry.PagesApplied = entry.WorldApplied = false;
                Log.Info("vanilla garnish " + entry.Id + " reverted");
                return true;
            }
            catch (Exception e)
            {
                Safe.Fail("vanilla garnish revert", e);
                return false;
            }
        }

        internal static IEnumerable<LibGarnish> Live => all.Where(e => e.Error == null);

        internal static void ApplyAll()
        {
            worldUp = true;
            foreach (LibGarnish entry in Live.ToList())
                ApplyWorld(entry);
        }

        internal static CookingGarnish Garnish(Item item) => item == null ? null : item.GetComponent<CookableItem>() as CookingGarnish;

        internal static IEnumerable<Item> Items() =>
            HashedGeneralValue<Item>.All.Where(item => item != null && Garnish(item) != null).OrderBy(item => item.name, StringComparer.OrdinalIgnoreCase);

        static void Check(LibGarnish entry)
        {
            GarnishSpec spec = entry.Spec;
            if (spec == null)
                throw new ArgumentException("spec is null");
            if (string.IsNullOrEmpty(spec.Id) || spec.Id.IndexOf('.') <= 0)
                throw new ArgumentException("id must look like author.thing");
            if (Live.Any(e => string.Equals(e.Id, spec.Id, StringComparison.OrdinalIgnoreCase)) || Claims.Holds(spec.Id))
                throw new ArgumentException("id is already used, Revert it first");
            if (string.IsNullOrEmpty(spec.Item))
                throw new ArgumentException("Item is empty");
            entry.Item = Names.NeedItemExact(spec.Item);
            entry.Garnish = NeedGarnish(entry.Item, spec.Item);

            if (!entry.HasPages && !entry.HasWorld)
                throw new ArgumentException("set at least one of Effects, Multipliers, Chunk");
            if (entry.HasPages && Main.SetupDone)
                throw new ArgumentException("Effects change journal pages, so set them from Main.Setup");
            entry.Effects = VanillaEdits.Effects(spec.Effects, "Effects");
            if (entry.Effects != null)
                PageBudget.Check(PageKind.Ingredient, entry.Item.name + "'s ingredient page", entry.Effects);
            entry.Multipliers = VanillaEdits.Multipliers(spec.Multipliers, "Multipliers");
            if (!string.IsNullOrEmpty(spec.Chunk))
                entry.Chunk = Names.Need<LiquidVisualChunkDefinition>(spec.Chunk, "chunk", "Chunks");
        }

        static CookingGarnish NeedGarnish(Item item, string name)
        {
            CookingGarnish garnish = Garnish(item);
            if (garnish != null)
                return garnish;
            string what = item.GetComponent<CookableItem>() != null ? " is a stew ingredient, not a garnish" : " is not a garnish";
            throw new ArgumentException(item.name + what + Names.Suggest(name, Items().Select(i => i.name), "Garnishes"));
        }

        static IEnumerable<Claims.Claim> ClaimsOf(LibGarnish entry)
        {
            if (entry.Effects != null)
                yield return Claims.Garnish(entry.Item.Hash, entry.Item.name, "effects", VanillaEdits.Text(entry.Effects));
            if (entry.Multipliers != null)
                yield return Claims.Garnish(entry.Item.Hash, entry.Item.name, "multipliers", VanillaEdits.Text(entry.Multipliers));
            if (entry.Spec.Chunk != null)
                yield return Claims.Garnish(entry.Item.Hash, entry.Item.name, "chunk", entry.Chunk == null ? "none" : entry.Chunk.name);
        }

        static void ApplyPages(LibGarnish entry)
        {
            if (!entry.HasPages)
                return;

            try
            {
                CookingGarnish garnish = entry.Garnish;
                VanillaEdits.Write(Key(entry, "effects"), garnish, VanillaEdits.Text(garnish.Effects), VanillaEdits.CookableEffects, entry.Effects.ToArray());
                entry.PagesApplied = true;
                Log.Info("vanilla garnish " + entry.Id + ": " + entry.Item.name + " effects set, " + VanillaEdits.Text(entry.Effects));
            }
            catch (Exception e)
            {
                Safe.Fail("vanilla garnish " + entry.Id + " effects", e);
            }
        }

        static void ApplyWorld(LibGarnish entry)
        {
            if (!Main.IsServer || !entry.HasWorld || entry.Error != null)
            {
                Applied(entry);
                return;
            }

            try
            {
                CookingGarnish garnish = entry.Garnish;
                List<string> done = new List<string>();
                if (entry.Multipliers != null)
                {
                    VanillaEdits.Write(Key(entry, "multipliers"), garnish, VanillaEdits.Text(garnish.EffectMultipliers), VanillaEdits.GarnishMultipliers, entry.Multipliers.ToArray());
                    done.Add("multipliers " + VanillaEdits.Text(entry.Multipliers));
                }
                if (entry.Spec.Chunk != null)
                {
                    VanillaEdits.Write(Key(entry, "chunk"), garnish, garnish.ChunkVisual == null ? "none" : garnish.ChunkVisual.name, VanillaEdits.CookableChunk, entry.Chunk);
                    done.Add("chunk " + (entry.Chunk == null ? "none" : entry.Chunk.name));
                }
                entry.WorldApplied = true;
                Log.Info("vanilla garnish " + entry.Id + ": " + entry.Item.name + " " + string.Join("; ", done.ToArray()));
            }
            catch (Exception e)
            {
                entry.Error = e is ArgumentException ? e.Message : e.ToString();
                entry.PagesApplied = entry.WorldApplied = false;
                VanillaEdits.Release(entry.Id);
                Log.Warn("vanilla garnish " + entry.Id + " refused: " + entry.Error);
            }
            Applied(entry);
        }

        static void Applied(LibGarnish entry)
        {
            bool pages = !entry.HasPages || entry.PagesApplied;
            bool world = !Main.IsServer || !entry.HasWorld || entry.WorldApplied;
            entry.IsApplied = entry.Error == null && pages && world && (entry.HasPages || Main.IsServer && entry.WorldApplied);
        }

        static Claims.Claim Key(LibGarnish entry, string field) => Claims.Garnish(entry.Item.Hash, entry.Item.name, field, null);
    }
}
