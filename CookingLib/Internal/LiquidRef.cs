using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppAlta.Liquid;

namespace CookingLib
{
    internal class LiquidRef
    {
        LiquidRef(LibLiquid lib, LiquidDefinition vanilla)
        {
            Lib = lib;
            Vanilla = vanilla;
        }

        internal LibLiquid Lib { get; }

        internal LiquidDefinition Vanilla { get; }

        internal LiquidDefinition Definition => Lib != null ? Lib.BaseDefinition : Vanilla;

        internal static LiquidRef Resolve(string name)
        {
            LibLiquid lib = Liquids.Get(name);
            if (lib != null)
                return new LiquidRef(lib, null);

            LiquidDefinition found = Names.Find<LiquidDefinition>(name);
            if (found != null)
            {
                LibLiquid ours = Liquids.FindByDefinition(found);
                return ours != null ? new LiquidRef(ours, null) : new LiquidRef(null, found);
            }

            IEnumerable<string> names = Liquids.All.Where(l => l.Error == null).Select(l => l.Id).Concat(Liquids.GameLiquids().Select(l => l.name));
            throw new ArgumentException("no liquid named '" + name + "'" + Names.Suggest(name, names, "Liquids"));
        }

        internal bool Matches(LiquidContent content)
        {
            if (content == null)
                return false;
            if (Lib != null && !Lib.IsShared)
                return content.IsCustom && Liquids.FindServerOnly(content.Data) == Lib;
            return !content.IsCustom && ReferenceEquals(content.Liquid, Definition);
        }

        internal LiquidContent CreateContent() => Lib != null ? Lib.CreateContent() : Vanilla.GetAsLiquidContent();
    }
}
