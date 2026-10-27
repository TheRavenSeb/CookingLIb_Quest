using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppAlta.Alchemy;

namespace CookingLib
{
    internal static class EffectSpread
    {
        internal const int MaxSeconds = 3600;

        const uint Heal = 48456;
        const uint Nourish = 20046;

        internal static int Check(EffectSpec spec, EffectDefinition effect)
        {
            if (spec.OverSeconds == 0)
                return 0;
            if (spec.OverSeconds < 1 || spec.OverSeconds > MaxSeconds)
                throw new ArgumentException("OverSeconds must be 1 to " + MaxSeconds + ", or 0 for at once");
            if (effect == null || effect.Hash != Heal && effect.Hash != Nourish)
                throw new ArgumentException("only Heal and Nourish can use OverSeconds, not " + (effect == null ? "nothing" : effect.name));
            return spec.OverSeconds;
        }

        internal static void Refuse(IEnumerable<EffectSpec> specs, string where)
        {
            if (specs != null && specs.Any(spec => spec != null && spec.OverSeconds != 0))
                throw new ArgumentException(where + " can't use OverSeconds, a stew gives its effects at once");
        }

        internal static bool Any(IEnumerable<EffectSpec> specs) => specs != null && specs.Any(spec => spec != null && spec.OverSeconds != 0);
    }
}
