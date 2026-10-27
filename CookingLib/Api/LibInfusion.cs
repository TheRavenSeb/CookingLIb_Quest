using System.Collections.Generic;
using Il2CppAlta.Inventory;

namespace CookingLib
{
    /// <summary>An infusion a mod registered. Check Error before using it.</summary>
    public class LibInfusion
    {
        internal LibInfusion(InfusionSpec spec)
        {
            Spec = spec;
        }

        /// <summary>The spec it was registered with.</summary>
        public InfusionSpec Spec { get; }

        /// <summary>The id from the spec.</summary>
        public string Id => Spec?.Id;

        /// <summary>Why it was refused, or null.</summary>
        public string Error { get; internal set; }

        /// <summary>True on the server once the world is up and every name resolved.</summary>
        public bool IsReady => Error == null && !ReferenceEquals(Item, null);

        internal Item Item { get; set; }

        internal List<LiquidRef> Inputs { get; set; }

        internal LiquidRef Output { get; set; }
    }
}
