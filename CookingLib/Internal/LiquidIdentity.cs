using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Il2CppAlta;
using Il2CppAlta.Cooking;
using Il2CppAlta.Liquid;
using Il2CppAlta.Networking;

namespace CookingLib
{
    internal static class LiquidIdentity
    {
        static readonly ConditionalWeakTable<LiquidData, StewTag> tags = new ConditionalWeakTable<LiquidData, StewTag>();
        static readonly IReadOnlyList<string> none = new List<string>().AsReadOnly();
        static ConditionalWeakTable<LiquidData, LiquidInfo> cache = new ConditionalWeakTable<LiquidData, LiquidInfo>();

        internal static int Tagged { get; private set; }

        internal static void Forget() => cache = new ConditionalWeakTable<LiquidData, LiquidInfo>();

        internal static void Tag(LiquidContent content, StewRecipe recipe, StewComponent stew, List<CookableItem> ingredients)
        {
            if (content is null || content.Data is null)
                return;

            NetworkPrefab look = stew != null && stew.Entity != null ? stew.Entity.Prefab : null;
            List<string> names = (ingredients ?? new List<CookableItem>()).Select(item => item == null || item.Item == null ? null : item.Item.name).ToList();
            tags.Remove(content.Data);
            tags.Add(content.Data, new StewTag(recipe, look == null ? null : look.name, names.AsReadOnly()));
            Tagged++;
        }

        internal static LiquidInfo Identify(LiquidData data) => data is null ? null : Safe.Run(nameof(Liquids.Identify), () => cache.GetValue(data, Build), null);

        static LiquidInfo Build(LiquidData data)
        {
            LiquidInfo info = Preset(data) ?? Ours(Liquids.FindServerOnlyByReference(data)) ?? Stew(data) ?? Ours(Liquids.FindServerOnlyByFingerprint(data)) ?? Other(data);
            info.Look = data.LiquidVisual == null ? null : data.LiquidVisual.name;
            info.Data = data;
            if (info.Ingredients == null)
                info.Ingredients = none;
            return info;
        }

        static LiquidInfo Preset(LiquidData data)
        {
            foreach (LiquidDefinition definition in HashedGeneralValue<LiquidDefinition>.All)
            {
                if (definition == null || !ReferenceEquals(definition.Data, data))
                    continue;
                return Ours(Liquids.FindByDefinition(definition)) ?? new LiquidInfo { Kind = LiquidKind.Vanilla, Name = definition.name };
            }
            return null;
        }

        static LiquidInfo Ours(LibLiquid liquid)
        {
            if (liquid == null)
                return null;
            return new LiquidInfo { Kind = liquid.IsShared ? LiquidKind.Shared : LiquidKind.ServerOnly, Name = liquid.Id, Liquid = liquid };
        }

        static LiquidInfo Stew(LiquidData data)
        {
            if (!tags.TryGetValue(data, out StewTag tag))
                return null;

            LibStewRecipe ours = StewRecipes.Find(tag.Recipe);
            return new LiquidInfo
            {
                Kind = LiquidKind.Stew,
                Name = ours != null ? ours.Id : tag.Recipe != null ? tag.Recipe.name : null,
                Recipe = ours,
                Stage = tag.Stage,
                Ingredients = tag.Ingredients
            };
        }

        static LiquidInfo Other(LiquidData data)
        {
            bool stewLook = data.LiquidVisual != null && data.LiquidVisual.name.IndexOf("Stew", StringComparison.OrdinalIgnoreCase) >= 0;
            return new LiquidInfo { Kind = stewLook ? LiquidKind.Stew : LiquidKind.Custom };
        }

        sealed class StewTag
        {
            internal StewTag(StewRecipe recipe, string stage, IReadOnlyList<string> ingredients)
            {
                Recipe = recipe;
                Stage = stage;
                Ingredients = ingredients;
            }

            internal StewRecipe Recipe { get; }

            internal string Stage { get; }

            internal IReadOnlyList<string> Ingredients { get; }
        }
    }
}
