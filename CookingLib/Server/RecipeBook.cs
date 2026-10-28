using System;
using System.Linq;
using System.Reflection;
using Il2Cpp.Alta.Cooking;
using Il2Cpp.Alta.Pages;

namespace CookingLib
{
    internal static class RecipeBook
    {
        internal const uint Collection = 27908;

        static readonly FieldInfo basesField = typeof(PageCollection).GetField("pageBases", BindingFlags.NonPublic | BindingFlags.Instance);

        internal static string Error { get; private set; }

        internal static bool Holds(StewRecipe recipe) => Safe.Run("recipe book", () => Bases(Registry.Find<PageCollection>(Collection)).Contains(recipe), false);

        internal static bool Append(StewRecipe recipe)
        {
            try
            {
                PageCollection book = Registry.Find<PageCollection>(Collection);
                if (book == null)
                    throw new InvalidOperationException("page collection " + Collection + " is missing");

                UnityEngine.Object[] bases = Bases(book);
                if (!bases.Contains(recipe))
                    basesField.SetValue(book, bases.Concat(new UnityEngine.Object[] { recipe }).ToArray());
                return true;
            }
            catch (Exception e)
            {
                Error = e.Message;
                Safe.Fail(nameof(RecipeBook), e);
                return false;
            }
        }

        static UnityEngine.Object[] Bases(PageCollection book) =>
            book == null ? new UnityEngine.Object[0] : (UnityEngine.Object[])basesField.GetValue(book) ?? new UnityEngine.Object[0];
    }
}
