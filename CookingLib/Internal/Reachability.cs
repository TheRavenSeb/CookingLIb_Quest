using System.Collections.Generic;
using System.Linq;
using Il2CppAlta;
using Il2CppAlta.Cooking;

namespace CookingLib
{
    internal static class Reachability
    {
        static readonly Dictionary<string, string> warned = new Dictionary<string, string>();
        static string lastCounts;

        internal static bool Known => PotKnobs.Built && PotKnobs.Definitions.Count > 0;

        internal static bool CountChanged => Known && PotKnobs.Definitions.Any(p => p.Ingredients != p.GameIngredients);

        internal static List<int> Counts() => Known ? PotKnobs.Definitions.Select(p => p.Ingredients).Distinct().OrderBy(n => n).ToList() : new List<int>();

        internal static string CountsText() => string.Join(" or ", Counts().Select(n => n.ToString()).ToArray());

        internal static bool IsReachable(int slots) => !Known || PotKnobs.Definitions.Any(p => p.Ingredients == slots);

        internal static bool IsReachable(StewRecipe recipe) => recipe == null || IsReachable(SlotsOf(recipe));

        internal static int SlotsOf(StewRecipe recipe) => recipe.StewIngredients == null ? 0 : recipe.StewIngredients.Count;

        internal static List<StewRecipe> Unreachable() =>
            HashedGeneralValue<StewRecipe>.All.Where(r => r != null && !IsReachable(r)).OrderBy(r => r.name).ToList();

        internal static void Check() => Safe.Run("reachability check", Warn);

        static void Warn()
        {
            if (!Known)
                return;

            string counts = CountsText();
            if (counts != lastCounts)
            {
                int game = HashedGeneralValue<StewRecipe>.All.Count(r => r != null && StewRecipes.Find(r) == null && !IsReachable(r));
                if (lastCounts != null || CountChanged)
                    Log.Info("pots lock at " + counts + " ingredients" + (game > 0 ? ", so " + game + " game recipes can't normally be made and random recipe pages skip them" : ""));
                lastCounts = counts;
            }

            foreach (LibStewRecipe recipe in StewRecipes.All)
            {
                if (recipe.Error != null || recipe.Spec == null || recipe.Spec.Slots == null)
                    continue;
                int slots = recipe.Spec.Slots.Count;
                if (IsReachable(slots))
                {
                    warned.Remove(recipe.Id);
                    continue;
                }
                if (warned.TryGetValue(recipe.Id, out string seen) && seen == counts)
                    continue;
                warned[recipe.Id] = counts;
                Log.Warn("stew recipe " + recipe.Id + " has " + slots + " slots but pots lock at " + counts + " ingredients, so it can't normally be made");
            }
        }
    }
}
