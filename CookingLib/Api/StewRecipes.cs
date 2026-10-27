using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppAlta.Cooking;
using Il2CppAlta.Pages;
using MelonLoader;

namespace CookingLib
{
    /// <summary>Registers stew recipes. Server only ones let vanilla players join; shared ones also get a journal page for players with the mod.</summary>
    public static class StewRecipes
    {
        static readonly List<LibStewRecipe> all = new List<LibStewRecipe>();
        static bool worldUp;

        /// <summary>Every stew recipe mods tried to register, refused ones included.</summary>
        public static IReadOnlyList<LibStewRecipe> All => all;

        /// <summary>True when the server's MelonPreferences.cfg sets SharedRecipePages under CookingLib, read at boot. Always false on players' machines.</summary>
        public static bool SharedPages { get; private set; }

        /// <summary>Registers a stew recipe. Call it from Main.Setup. The server builds it once the world is up.</summary>
        public static LibStewRecipe Register(StewRecipeSpec spec)
        {
            LibStewRecipe recipe = new LibStewRecipe(spec);
            try
            {
                Validate(spec);
            }
            catch (ArgumentException e)
            {
                recipe.Error = e.Message;
            }

            all.Add(recipe);
            if (recipe.Error != null)
                Log.Warn("stew recipe " + recipe.Id + " refused: " + recipe.Error);
            else if (worldUp)
            {
                Build(recipe);
                Reachability.Check();
            }
            return recipe;
        }

        /// <summary>Registers a stew recipe with a journal page under a number from 1 to 65535 that no other mod uses, forever per world. Call it from Main.Setup on the server and on every player's machine.</summary>
        public static LibStewRecipe RegisterShared(uint number, StewRecipeSpec spec)
        {
            LibStewRecipe recipe = new LibStewRecipe(spec, true, number);
            try
            {
                ValidateShared(number, spec);
                StewRecipe built = StewRecipeFactory.BuildPage(recipe);
                string clash = Claims.Take(spec.Id, new[] { Claims.RecipeNumber(number, spec.Id) });
                if (clash != null)
                    throw new ArgumentException(clash);
                try
                {
                    StewRecipe holder = Registry.Find<StewRecipe>(number);
                    if (holder != null || !Registry.Add(built))
                        throw new ArgumentException("recipe number " + number + " is taken by " + (holder == null ? "another mod" : holder.name));
                }
                catch (Exception)
                {
                    Claims.Release(spec.Id);
                    throw;
                }
                recipe.Recipe = built;
                recipe.PageBuilt = true;
            }
            catch (Exception e)
            {
                recipe.Error = e is ArgumentException ? e.Message : e.ToString();
            }

            all.Add(recipe);
            if (recipe.Error != null)
            {
                Log.Warn("stew recipe " + recipe.Id + " refused: " + recipe.Error);
                return recipe;
            }

            bool inBook = Main.IsServer && SharedPages && RecipeBook.Append(recipe.Recipe);
            Log.Info("stew recipe " + recipe.Id + " shared as " + number + ", page lengths " + PageBudget.Slots + " and " + PageBudget.Effects(PageKind.Recipe)
                + (PageBudget.Live ? "" : " (page settings not readable)") + (Main.IsServer ? ", " + PageState(recipe, inBook) : ""));
            foreach (int slot in StewRecipeFactory.BlankSlots(recipe.Recipe))
                Log.Warn("stew recipe " + recipe.Id + ": slot " + slot + " has no glyph, its page leaves it blank");
            return recipe;
        }

        /// <summary>Server only. Unlocks a shared recipe's journal page for everyone in this world. False before the world is up, without a page, or when it fails.</summary>
        public static bool UnlockPage(string id)
        {
            LibStewRecipe recipe = FindShared(id);
            if (!Main.IsServer || !worldUp || recipe == null || !recipe.HasPage || GlobalPagesManager.Instance == null)
                return false;

            try
            {
                GlobalPagesManager.Instance.AddPage((int)PageKind.Recipe, recipe.Number);
                return true;
            }
            catch (Exception e)
            {
                Safe.Fail(nameof(UnlockPage), e);
                return false;
            }
        }

        /// <summary>The stew recipe with this id that was not refused, or null.</summary>
        public static LibStewRecipe Get(string id)
        {
            foreach (LibStewRecipe recipe in all)
            {
                if (recipe.Error == null && string.Equals(recipe.Id, id, StringComparison.OrdinalIgnoreCase))
                    return recipe;
            }
            return null;
        }

        internal static LibStewRecipe Find(StewRecipe recipe)
        {
            if (ReferenceEquals(recipe, null))
                return null;

            foreach (LibStewRecipe ours in all)
            {
                if (ReferenceEquals(ours.Recipe, recipe))
                    return ours;
            }
            return null;
        }

        internal static LibStewRecipe FindShared(string id) =>
            all.FirstOrDefault(r => r.IsShared && r.PageBuilt && string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));

        internal static bool IsUnlocked(LibStewRecipe recipe) => !GlobalPagesManager.IsNew((int)PageKind.Recipe, recipe.Number);

        internal static void ReadFlag()
        {
            MelonPreferences_Category category = MelonPreferences.CreateCategory("CookingLib");
            SharedPages = category.CreateEntry("SharedRecipePages", false, "Shared recipe pages in loot and the Stewing Recipes book. Every player needs CookingLib and the recipe mods").Value;
            Log.Info("shared recipe pages " + (SharedPages ? "on" : "off"));
        }

        internal static string Describe(LibStewRecipe recipe)
        {
            if (!recipe.IsShared)
                return recipe.Error != null ? "refused: " + recipe.Error : recipe.Recipe != null ? "recipe " + recipe.Recipe.Hash : "waiting for the world";
            if (!recipe.PageBuilt)
                return "refused: " + recipe.Error;

            string state = "shared as " + recipe.Number + ", ";
            if (recipe.Error != null)
                state += (recipe.HasPage ? "page only, cooking refused: " : "refused: ") + recipe.Error;
            else
                state += PageState(recipe, RecipeBook.Holds(recipe.Recipe)) + (worldUp ? "" : ", waiting for the world");
            List<int> blank = StewRecipeFactory.BlankSlots(recipe.Recipe);
            return state + (blank.Count == 0 ? "" : ", no glyph on slot " + string.Join(", ", blank.Select(n => n.ToString()).ToArray()));
        }

        static string PageState(LibStewRecipe recipe, bool inBook)
        {
            if (!SharedPages)
                return "no page (SharedRecipePages is false)";
            return inBook ? "page in book " + RecipeBook.Collection : "page, not in the book" + (RecipeBook.Error == null ? "" : ": " + RecipeBook.Error);
        }

        internal static List<string> SpreadBases(LibStewRecipe recipe)
        {
            if (recipe.Spec == null || recipe.Spec.BaseLiquids == null)
                return new List<string>();
            return recipe.Spec.BaseLiquids.Select(Liquids.Get).Where(liquid => liquid != null && EffectSpread.Any(liquid.Spec.Effects)).Select(liquid => liquid.Id).ToList();
        }

        internal static void ResolveAll()
        {
            worldUp = true;
            foreach (LibStewRecipe recipe in all)
            {
                if (recipe.Error == null)
                    Build(recipe);
            }
        }

        static void Build(LibStewRecipe recipe)
        {
            try
            {
                if (!ServerPatches.PagesGuarded)
                    throw new ArgumentException("the page guard patch failed, so recipes stay off");

                if (recipe.IsShared)
                    StewRecipeFactory.Cook(recipe);
                else
                    StewRecipeFactory.Build(recipe);
                LiquidIdentity.Forget();
                Log.Info("stew recipe " + recipe.Id + " ready as recipe " + recipe.Recipe.Hash);
                foreach (string spread in SpreadBases(recipe))
                    Log.Warn("stew recipe " + recipe.Id + ": base " + spread + " uses OverSeconds, the stew gives it at once");
            }
            catch (Exception e)
            {
                recipe.Error = e is ArgumentException ? e.Message : e.ToString();
                Log.Warn("stew recipe " + recipe.Id + (recipe.IsShared ? " keeps its page, cooking refused: " : " refused: ") + recipe.Error);
            }
        }

        static void ValidateShared(uint number, StewRecipeSpec spec)
        {
            Validate(spec);
            if (Main.SetupDone)
                throw new ArgumentException("call it from Main.Setup, journal pages are fixed after that");
            if (Claims.Holds(spec.Id))
                throw new ArgumentException("id is already used");
            if (number == 0 || number > 65535)
                throw new ArgumentException("shared recipe numbers must be 1 to 65535");
            if (Main.IsServer && !ServerPatches.PagesGuarded)
                throw new ArgumentException("the page guard patch failed, so shared recipes stay off");
        }

        static void Validate(StewRecipeSpec spec)
        {
            if (spec == null)
                throw new ArgumentException("spec is null");
            if (string.IsNullOrEmpty(spec.Id) || spec.Id.IndexOf('.') <= 0)
                throw new ArgumentException("id must look like author.thing");
            if (Get(spec.Id) != null || FindShared(spec.Id) != null)
                throw new ArgumentException("id is already registered");
            if (string.IsNullOrEmpty(spec.Look))
                throw new ArgumentException("Look is empty");
            if (spec.BaseLiquids == null || spec.BaseLiquids.Count == 0 || spec.BaseLiquids.Any(string.IsNullOrEmpty))
                throw new ArgumentException("BaseLiquids is empty or holds an empty name");
            if (spec.Slots == null || spec.Slots.Count == 0)
                throw new ArgumentException("Slots is empty");

            foreach (SlotSpec slot in spec.Slots)
            {
                bool hasItems = slot != null && slot.Items != null && slot.Items.Count > 0;
                bool hasCategory = slot != null && !string.IsNullOrEmpty(slot.Category);
                if (hasItems == hasCategory)
                    throw new ArgumentException("every slot needs Items or a Category, not both");
                if (!string.IsNullOrEmpty(slot.Glyph) && hasCategory)
                    throw new ArgumentException("Glyph is for Items slots, a Category slot shows its category's own");
            }

            if (spec.ExtraEffects == null || spec.EffectModifiers == null || spec.ExtraEffects.Contains(null) || spec.EffectModifiers.Contains(null))
                throw new ArgumentException("ExtraEffects and EffectModifiers can't be null or hold a null effect");
            if (spec.ExtraEffects.Count > 3)
                throw new ArgumentException("a stew shows at most 3 extra effects");
            if (spec.ExtraEffects.Concat(spec.EffectModifiers).Any(effect => !LiquidFactory.IsWithin100(effect.Strength)))
                throw new ArgumentException("effect strengths must be minus 100 to 100");
            EffectSpread.Refuse(spec.ExtraEffects, "ExtraEffects");
            EffectSpread.Refuse(spec.EffectModifiers, "EffectModifiers");
            if (spec.Tint.HasValue && !LiquidFactory.IsValidColor(spec.Tint.Value))
                throw new ArgumentException("tint channels must be 0 to 1");
        }
    }
}
