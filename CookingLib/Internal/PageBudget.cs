using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppAlta.Alchemy;
using Il2CppAlta.Global;

namespace CookingLib
{
    internal enum PageKind
    {
        Recipe = 3,
        Edible = 5,
        Ingredient = 6
    }

    internal static class PageBudget
    {
        internal const int FallbackSlots = 4;
        internal const int FallbackEffects = 3;

        static int slots, recipe, edible, ingredient;

        internal static bool Live => Read();

        internal static int Slots => Read() ? slots : FallbackSlots;

        internal static int Effects(PageKind kind)
        {
            if (!Read())
                return FallbackEffects;
            return kind == PageKind.Recipe ? recipe : kind == PageKind.Edible ? edible : ingredient;
        }

        internal static string Lengths() =>
            "recipe pages " + Slots + " slots and " + Effects(PageKind.Recipe) + " effects, food pages " + Effects(PageKind.Edible)
            + " effects, ingredient pages " + Effects(PageKind.Ingredient) + " effects" + (Live ? "" : " (the game's page settings were not readable, so 4 and 3)");

        internal static void Check(PageKind kind, string page, IEnumerable<EffectDefinitionApplication> effects, int slotCount = 0)
        {
            List<EffectDefinitionApplication> shown = (effects ?? Enumerable.Empty<EffectDefinitionApplication>())
                .Where(e => e != null && (kind == PageKind.Recipe || e.StrengthMultiplier != 0f)).ToList();

            foreach (EffectDefinitionApplication effect in shown)
            {
                if (effect.EffectToModify == null || effect.EffectToModify.effectGlyph == null)
                    throw new ArgumentException("effect " + (effect.EffectToModify == null ? "none" : effect.EffectToModify.name) + " has no page glyph, so " + page + " can't show it");
            }

            int room = Effects(kind);
            if (shown.Count > room)
            {
                string counted = kind == PageKind.Recipe ? "extra effects and modifiers share them" : "every effect that is not 0 takes one";
                throw new ArgumentException(page + " has room for " + room + " effects and this gives it " + shown.Count + " (" + counted + ")");
            }

            if (kind == PageKind.Recipe && slotCount > Slots)
                throw new ArgumentException(page + " has room for " + Slots + " ingredients and this gives it " + slotCount);
        }

        static bool Read()
        {
            if (slots > 0)
                return true;

            try
            {
                int s = GlobalSettings<StewRecipePageSettings>.Instance.ingredients.Length;
                int r = GlobalSettings<StewRecipePageSettings>.Instance.effects.Length;
                int e = GlobalSettings<EdiblePageSettings>.Instance.effects.Length;
                int i = GlobalSettings<IngredientPageSettings>.Instance.effects.Length;
                if (s <= 0 || r <= 0 || e <= 0 || i <= 0)
                    return false;
                recipe = r;
                edible = e;
                ingredient = i;
                slots = s;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
