using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppAlta.Inventory;
using Il2CppAlta.Liquid;

namespace CookingLib
{
    /// <summary>Registers infusions. They run on the server only.</summary>
    public static class Infusions
    {
        static readonly List<LibInfusion> all = new List<LibInfusion>();
        static bool worldUp;

        /// <summary>Every infusion mods tried to register, refused ones included.</summary>
        public static IReadOnlyList<LibInfusion> All => all;

        /// <summary>Registers an infusion. Call it from Main.Setup. The server resolves it once the world is up.</summary>
        public static LibInfusion Register(InfusionSpec spec)
        {
            LibInfusion infusion = new LibInfusion(spec);
            try
            {
                Validate(spec);
            }
            catch (ArgumentException e)
            {
                infusion.Error = e.Message;
            }

            all.Add(infusion);
            if (infusion.Error != null)
                Log.Warn("infusion " + infusion.Id + " refused: " + infusion.Error);
            else if (worldUp)
                Resolve(infusion);
            return infusion;
        }

        internal static void ResolveAll()
        {
            worldUp = true;
            foreach (LibInfusion infusion in all)
            {
                if (infusion.Error == null)
                    Resolve(infusion);
            }
        }

        internal static LibInfusion Find(Item item, LiquidContent content)
        {
            foreach (LibInfusion infusion in all)
            {
                if (infusion.IsReady && ReferenceEquals(infusion.Item, item) && infusion.Inputs.Any(input => input.Matches(content)))
                    return infusion;
            }
            return null;
        }

        static void Resolve(LibInfusion infusion)
        {
            try
            {
                InfusionSpec spec = infusion.Spec;
                infusion.Inputs = spec.InputLiquids.Select(LiquidRef.Resolve).ToList();
                infusion.Output = LiquidRef.Resolve(spec.OutputLiquid);
                infusion.Item = Names.NeedItem(spec.Item);
                Log.Info("infusion " + infusion.Id + " ready");
            }
            catch (Exception e)
            {
                infusion.Error = e is ArgumentException ? e.Message : e.ToString();
                Log.Warn("infusion " + infusion.Id + " refused: " + infusion.Error);
            }
        }

        static void Validate(InfusionSpec spec)
        {
            if (spec == null)
                throw new ArgumentException("spec is null");
            if (string.IsNullOrEmpty(spec.Id) || spec.Id.IndexOf('.') <= 0)
                throw new ArgumentException("id must look like author.thing");
            if (all.Any(other => other.Error == null && string.Equals(other.Id, spec.Id, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("id is already registered");
            if (string.IsNullOrEmpty(spec.Item))
                throw new ArgumentException("Item is empty");
            if (spec.InputLiquids == null || spec.InputLiquids.Count == 0 || spec.InputLiquids.Any(string.IsNullOrEmpty))
                throw new ArgumentException("InputLiquids is empty or holds an empty name");
            if (string.IsNullOrEmpty(spec.OutputLiquid))
                throw new ArgumentException("OutputLiquid is empty");
        }
    }
}
