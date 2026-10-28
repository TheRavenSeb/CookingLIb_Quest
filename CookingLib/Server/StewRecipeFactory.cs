using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppAlta;
using Il2CppAlta.Alchemy;
using Il2CppAlta.Cooking;
using Il2CppAlta.Inventory;
using Il2CppAlta.Liquid;
using Il2CppAlta.Networking;
using Il2CppAlta.Pages;
using UnityEngine;

namespace CookingLib
{
    internal static class StewRecipeFactory
    {
        const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        static readonly FieldInfo lookField = typeof(StewRecipe).GetField("baseVersionHash", PrivateInstance);
        static readonly FieldInfo liquidsField = typeof(StewRecipe).GetField("possibleLiquids", PrivateInstance);
        static readonly FieldInfo slotsField = typeof(StewRecipe).GetField("stewIngredients", PrivateInstance);
        static readonly FieldInfo extraField = typeof(StewRecipe).GetField("extraEffects", PrivateInstance);
        static readonly FieldInfo modifiersField = typeof(StewRecipe).GetField("effectModifiers", PrivateInstance);
        static readonly FieldInfo cookablesField = typeof(CookableCollectionDefinition).GetField("cookables", PrivateInstance);

        internal static void Build(LibStewRecipe lib)
        {
            Resolve(lib.Spec, out uint look, out List<LiquidDefinition> liquids, out LiquidRef result);
            StewRecipe recipe = Create(lib.Spec, Registry.NumberFor(lib.Spec.Id));
            if (!Registry.Add(recipe))
                throw new ArgumentException("recipe number " + recipe.Hash + " is taken, pick another id");

            Write(lib, recipe, look, liquids, result);
        }

        internal static StewRecipe BuildPage(LibStewRecipe lib)
        {
            StewRecipe recipe = Create(lib.Spec, lib.Number);
            PageBudget.Check(PageKind.Recipe, lib.Id + "'s recipe page", recipe.ExtraEffects.Concat(recipe.EffectModifiers), recipe.StewIngredients.Count);
            return recipe;
        }

        internal static void Cook(LibStewRecipe lib)
        {
            Resolve(lib.Spec, out uint look, out List<LiquidDefinition> liquids, out LiquidRef result);
            Write(lib, lib.Recipe, look, liquids, result);
        }

        internal static List<int> BlankSlots(StewRecipe recipe) =>
            Enumerable.Range(0, recipe.StewIngredients.Count).Where(i => recipe.StewIngredients[i] == null || recipe.StewIngredients[i].glyph == null).Select(i => i + 1).ToList();

        static void Resolve(StewRecipeSpec spec, out uint look, out List<LiquidDefinition> liquids, out LiquidRef result)
        {
            look = Look(spec.Look);
            liquids = spec.BaseLiquids.Select(name => LiquidRef.Resolve(name).Definition).ToList();
            if (liquids.Any(liquid => liquid == null))
                throw new ArgumentException("a base liquid has no definition");
            result = string.IsNullOrEmpty(spec.ResultLiquid) ? null : LiquidRef.Resolve(spec.ResultLiquid);
        }

        static void Write(LibStewRecipe lib, StewRecipe recipe, uint look, List<LiquidDefinition> liquids, LiquidRef result)
        {
            lookField.SetValue(recipe, look);
            liquidsField.SetValue(recipe, liquids);
            lib.Recipe = recipe;
            lib.Result = result;
        }

        static StewRecipe Create(StewRecipeSpec spec, uint number)
        {
            List<EffectDefinitionApplication> modifiers = new List<EffectDefinitionApplication>();
            foreach (EffectSpec effect in spec.EffectModifiers)
                AddOnce(modifiers, effect, "modifier");

            List<EffectDefinitionApplication> extra = new List<EffectDefinitionApplication>();
            foreach (EffectSpec effect in spec.ExtraEffects)
                AddOnce(extra, effect, "extra effect");

            List<StewIngredientBase> slots = new List<StewIngredientBase>();
            for (int i = 0; i < spec.Slots.Count; i++)
                slots.Add(Slot(spec.Id + " slot " + (i + 1), spec.Slots[i]));

            StewRecipe recipe = ScriptableObject.CreateInstance<StewRecipe>();
            recipe.name = spec.Id;
            recipe.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Registry.SetHash(recipe, number);
            liquidsField.SetValue(recipe, new List<LiquidDefinition>());
            slotsField.SetValue(recipe, slots);
            extraField.SetValue(recipe, extra.ToArray());
            modifiersField.SetValue(recipe, modifiers.ToArray());
            return recipe;
        }

        static uint Look(string name)
        {
            StewRecipe vanilla = Names.Find<StewRecipe>(name);
            if (vanilla != null && StewRecipes.Find(vanilla) == null)
                return vanilla.BaseVersionHash;

            NetworkPrefab prefab = Names.FindPrefab(name);
            if (prefab == null)
                throw new ArgumentException("no stew recipe or prefab named '" + name + "'" + Names.Suggest(name, LookNames(), "Stews"));
            if (prefab.GetComponent<StewComponent>() == null)
                throw new ArgumentException(prefab.name + " is not a stew, Look takes a stew recipe or a stew prefab");
            return prefab.Hash;
        }

        static IEnumerable<string> LookNames() =>
            HashedGeneralValue<StewRecipe>.All.Where(recipe => recipe != null && StewRecipes.Find(recipe) == null).Select(recipe => recipe.name)
                .Concat(PrefabWalk.With<StewComponent>().Select(prefab => prefab.name));

        static void AddOnce(List<EffectDefinitionApplication> list, EffectSpec effect, string kind)
        {
            EffectDefinition definition = EffectNames.Need(effect.Effect, EffectNames.ForServer);
            if (list.Any(existing => existing.EffectToModify == definition))
                throw new ArgumentException(kind + " " + definition.name + " is listed twice");
            list.Add(new EffectDefinitionApplication(definition, effect.Strength));
        }

        static StewIngredientBase Slot(string name, SlotSpec slot)
        {
            if (!string.IsNullOrEmpty(slot.Category))
                return Names.NeedCategory(slot.Category);

            List<CookableItem> cookables = new List<CookableItem>();
            Glyph glyph = string.IsNullOrEmpty(slot.Glyph) ? null : Names.Need<Glyph>(slot.Glyph, "glyph", "Glyphs");
            for (int i = 0; i < slot.Items.Count; i++)
            {
                string itemName = slot.Items[i];
                Item item = Names.NeedItem(itemName);
                if (i == 0 && string.IsNullOrEmpty(slot.Glyph))
                    glyph = item.Glyph;
                CookableItem cookable = Main.IsServer ? SlotItems.Resolve(itemName) : SlotItems.OnPage(item);
                if (cookable != null && !cookables.Contains(cookable))
                    cookables.Add(cookable);
            }

            CookableCollectionDefinition collection = ScriptableObject.CreateInstance<CookableCollectionDefinition>();
            collection.name = name;
            collection.hideFlags = HideFlags.DontUnloadUnusedAsset;
            collection.glyph = glyph;
            cookablesField.SetValue(collection, cookables);
            return collection;
        }
    }
}
