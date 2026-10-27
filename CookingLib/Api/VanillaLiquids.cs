using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppAlta.Alchemy;
using Il2CppAlta.Liquid;

namespace CookingLib
{
    /// <summary>Changes the effects of the game's own liquids, such as potions. Nothing is saved, so without the mod the game's values come back.</summary>
    public static class VanillaLiquids
    {
        static readonly List<LibVanillaLiquid> all = new List<LibVanillaLiquid>();

        /// <summary>Every change mods asked for and have not reverted, refused ones included.</summary>
        public static IReadOnlyList<LibVanillaLiquid> All => all;

        /// <summary>Changes a game liquid's effects. Call it from Main.Setup on the server and on players' machines; it applies right after Main.PostSetup and later calls are refused. Never throws.</summary>
        public static LibVanillaLiquid Modify(VanillaLiquidSpec spec)
        {
            LibVanillaLiquid entry = new LibVanillaLiquid(spec);
            try
            {
                Check(entry);
                string clash = Claims.Take(entry.Id, new[] { Claims.Liquid(entry.Definition, "effects", VanillaEdits.Text(entry.Effects)) });
                if (clash != null)
                    throw new ArgumentException(clash);
            }
            catch (Exception e)
            {
                entry.Error = e is ArgumentException ? e.Message : e.ToString();
            }

            all.Add(entry);
            if (entry.Error != null)
                Log.Warn("vanilla liquid " + entry.Id + " refused: " + entry.Error);
            return entry;
        }

        /// <summary>Undoes the change with this id and gives back the game's effects. True if it was in place or waiting.</summary>
        public static bool Revert(string id)
        {
            try
            {
                List<LibVanillaLiquid> gone = all.Where(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)).ToList();
                LibVanillaLiquid entry = gone.FirstOrDefault(e => e.Error == null);
                all.RemoveAll(gone.Contains);
                if (entry != null)
                    VanillaEdits.Release(entry.Id);
                else
                    Claims.Forget(id);
                if (entry == null)
                    return false;

                entry.IsApplied = false;
                Log.Info("vanilla liquid " + entry.Id + " reverted");
                return true;
            }
            catch (Exception e)
            {
                Safe.Fail("vanilla liquid revert", e);
                return false;
            }
        }

        internal static IEnumerable<LibVanillaLiquid> Live => all.Where(e => e.Error == null);

        internal static void ApplyAll()
        {
            foreach (LibVanillaLiquid entry in Live.Where(e => !e.IsApplied).ToList())
                Apply(entry);
        }

        static void Check(LibVanillaLiquid entry)
        {
            VanillaLiquidSpec spec = entry.Spec;
            if (spec == null)
                throw new ArgumentException("spec is null");
            if (Main.SetupDone)
                throw new ArgumentException("call it from Main.Setup, a game liquid's effects change at Setup only");
            if (string.IsNullOrEmpty(spec.Id) || spec.Id.IndexOf('.') <= 0)
                throw new ArgumentException("id must look like author.thing");
            if (Live.Any(e => string.Equals(e.Id, spec.Id, StringComparison.OrdinalIgnoreCase)) || Claims.Holds(spec.Id))
                throw new ArgumentException("id is already used, Revert it first");
            if (string.IsNullOrEmpty(spec.Liquid))
                throw new ArgumentException("Liquid is empty");

            LiquidDefinition definition = Names.Find<LiquidDefinition>(spec.Liquid);
            if (definition == null)
                throw new ArgumentException("no game liquid named '" + spec.Liquid + "'" + Names.Suggest(spec.Liquid, Liquids.GameLiquids().Select(l => l.name), "Liquids"));
            LibLiquid ours = Liquids.FindByDefinition(definition);
            if (ours != null)
                throw new ArgumentException(spec.Liquid + " is the CookingLib liquid " + ours.Id + ", change its LiquidSpec instead");
            if (definition.Data == null)
                throw new ArgumentException(definition.name + " has no liquid data");
            if (definition.Hash == LiquidFactory.WaterHash && !spec.AllowWater)
                throw new ArgumentException("Water is the base of every game stew and fills every river and pond, set AllowWater to change it");

            entry.Definition = definition;
            EffectDefinitionApplication[] game = VanillaEdits.Find(Key(entry).Key)?.Original as EffectDefinitionApplication[] ?? definition.Data.Effects.ToArray();
            entry.Effects = VanillaEdits.Replace(spec.Effects, game, "game liquids");
            if (definition.Hash == LiquidFactory.WaterHash)
                entry.Notes.Add("changes every stew and every water sip");
        }

        static void Apply(LibVanillaLiquid entry)
        {
            try
            {
                LiquidData data = entry.Definition.Data;
                VanillaEdits.Write(Key(entry), data, VanillaEdits.Text(data.Effects), VanillaEdits.LiquidEffects, entry.Effects.ToArray());
                entry.IsApplied = true;
                Log.Info("vanilla liquid " + entry.Id + ": " + entry.Definition.name + " gives " + VanillaEdits.Text(entry.Effects)
                    + (entry.Notes.Count == 0 ? "" : ", " + string.Join(", ", entry.Notes.ToArray())));
            }
            catch (Exception e)
            {
                entry.Error = e is ArgumentException ? e.Message : e.ToString();
                VanillaEdits.Release(entry.Id);
                Log.Warn("vanilla liquid " + entry.Id + " refused: " + entry.Error);
            }
        }

        static Claims.Claim Key(LibVanillaLiquid entry) => Claims.Liquid(entry.Definition, "effects", null);
    }
}
