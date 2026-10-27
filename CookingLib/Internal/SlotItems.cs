using System;
using Il2CppAlta.Cooking;
using Il2CppAlta.Inventory;

namespace CookingLib
{
    internal static class SlotItems
    {
        internal static CookableItem Resolve(string name) => NeedVanilla(Names.NeedItem(name));

        internal static CookableItem OnPage(Item item)
        {
            CookableItem cookable = Vanilla(item);
            if (cookable == null && item.GetComponent<CookableItem>() is CookingGarnish)
                throw new ArgumentException(item.name + " is a garnish, not a stew ingredient");
            return cookable;
        }

        internal static CookableItem Vanilla(Item item)
        {
            CookableItem cookable = item == null ? null : item.GetComponent<CookableItem>();
            return cookable != null && cookable.GetType() == typeof(CookableItem) ? cookable : null;
        }

        internal static CookableItem NeedVanilla(Item item)
        {
            CookableItem cookable = Vanilla(item);
            if (cookable != null)
                return cookable;
            if (item.GetComponent<CookableItem>() is CookingGarnish)
                throw new ArgumentException(item.name + " is a garnish, not a stew ingredient");
            throw new ArgumentException(item.name + " can't go in a stew");
        }
    }
}
