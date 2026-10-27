using System;
using Il2CppAlta.Liquid;

namespace CookingLib
{
    internal static class SharedLiquidRegistrar
    {
        internal static void Register(LiquidDefinition definition)
        {
            uint hash = definition.Hash;
            if (hash == 0 || hash > 65535)
                throw new ArgumentException("shared liquid numbers must be 1 to 65535");
            if (!Registry.Add(definition))
                throw new ArgumentException("liquid number " + hash + " is already taken by " + LiquidDefinition.Get(hash).name);
        }
    }
}
