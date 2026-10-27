using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Il2CppAlta;
using Il2CppAlta.Alchemy;
using Il2CppAlta.Cooking;
using Il2CppAlta.Global;
using Il2CppAlta.Inventory;
using Il2CppAlta.Liquid;
using Il2CppAlta.Networking;

namespace CookingLib
{
    internal static class PotKnobs
    {
        const uint FullnessHash = 34710;
        const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        static readonly FieldInfo ingredientsField = typeof(PotDefinition).GetField("minNumberOfIngredients", PrivateInstance);
        static readonly FieldInfo garnishesField = typeof(PotDefinition).GetField("numberOfGarnishes", PrivateInstance);
        static readonly FieldInfo temperatureField = typeof(CookingSettings).GetField("minimumStewingTemperature", PrivateInstance);
        static readonly FieldInfo globalField = typeof(CookingSettings).GetField("globalStewEffects", PrivateInstance);
        static readonly FieldInfo noRecipeField = typeof(CookingSettings).GetField("nonRecipeStewPrefab", PrivateInstance);
        static readonly FieldInfo pushField = typeof(CookingSettings).GetField("ingredientRejectionForce", PrivateInstance);

        static List<Pot> pots;
        static bool written;

        internal static string Error { get; private set; }

        internal static bool Built => pots != null;

        internal static IReadOnlyList<Pot> Definitions => pots ?? new List<Pot>();

        internal static float GameTemperature { get; private set; }

        internal static EffectDefinitionApplication[] GameGlobal { get; private set; }

        internal static NetworkPrefab GameNoRecipe { get; private set; }

        internal static float GamePush { get; private set; }

        internal static CookingSettings Settings => GlobalSettings<CookingSettings>.Instance;

        internal static float LiveTemperature => Settings.MinimumStewingTemperature;

        internal static EffectDefinitionApplication[] LiveGlobal => Settings.GlobalStewEffects ?? new EffectDefinitionApplication[0];

        internal static NetworkPrefab LiveNoRecipe => Settings.NonRecipeStewPrefab;

        internal static float LivePush => pushField == null ? 0f : (float)pushField.GetValue(Settings);

        internal static bool Ready()
        {
            if (pots != null)
                return true;
            if (Error != null)
                return false;

            try
            {
                if (ingredientsField == null || garnishesField == null || temperatureField == null || globalField == null || noRecipeField == null || pushField == null)
                    throw new InvalidOperationException("the game's pot fields were not found");
                CookingSettings settings = Settings;
                if (settings == null)
                    throw new InvalidOperationException("the game's cooking settings are not loaded");

                Stopwatch watch = Stopwatch.StartNew();
                Dictionary<PotDefinition, Pot> found = new Dictionary<PotDefinition, Pot>();
                foreach (NetworkPrefab prefab in PrefabWalk.All())
                {
                    foreach (CookingPot pot in prefab.GetComponentsInChildren<CookingPot>(true))
                    {
                        PotDefinition definition = pot.Definition;
                        if (definition == null)
                            continue;
                        if (!found.TryGetValue(definition, out Pot entry))
                            found[definition] = entry = new Pot(definition);
                        if (!entry.Prefabs.Contains(prefab))
                            entry.Prefabs.Add(prefab);
                    }
                }

                GameTemperature = settings.MinimumStewingTemperature;
                GameGlobal = settings.GlobalStewEffects ?? new EffectDefinitionApplication[0];
                GameNoRecipe = settings.NonRecipeStewPrefab;
                GamePush = (float)pushField.GetValue(settings);
                pots = found.Values.ToList();
                Log.Info("pot rules: " + pots.Count + " pot definitions on " + pots.Sum(p => p.Prefabs.Count) + " prefabs, found in " + watch.ElapsedMilliseconds + " ms");
                if (pots.Count == 0)
                    Log.Warn("pot rules: no prefab carries a cooking pot, so pot rules reach no pot");
                return true;
            }
            catch (Exception e)
            {
                Error = e is InvalidOperationException ? e.Message : e.ToString();
                Log.Error("pot rules can't reach the game's pots: " + Error);
                return false;
            }
        }

        internal static bool IsKnown(PotDefinition definition) => pots != null && pots.Any(p => p.Definition == definition);

        internal static bool Write(int? ingredients, int? garnishes, float? temperature, EffectDefinitionApplication[] global, NetworkPrefab noRecipe, float? push)
        {
            if (!Ready())
                return false;
            bool any = ingredients.HasValue || garnishes.HasValue || temperature.HasValue || global != null || noRecipe != null || push.HasValue;
            if (!any && !written)
                return true;

            try
            {
                foreach (Pot pot in pots)
                {
                    ingredientsField.SetValue(pot.Definition, ingredients ?? pot.GameIngredients);
                    garnishesField.SetValue(pot.Definition, garnishes ?? pot.GameGarnishes);
                }
                CookingSettings settings = Settings;
                temperatureField.SetValue(settings, temperature ?? GameTemperature);
                globalField.SetValue(settings, global != null ? global.ToArray() : GameGlobal);
                noRecipeField.SetValue(settings, noRecipe != null ? noRecipe : GameNoRecipe);
                pushField.SetValue(settings, push ?? GamePush);
                written = any;
                return true;
            }
            catch (Exception e)
            {
                Safe.Fail("pot rules write", e);
                return false;
            }
        }

        internal static float WorstFeed(bool game)
        {
            if (pots == null || pots.Count == 0)
                return Gate(game ? GameGlobal : LiveGlobal);

            int count = pots.Max(p => game ? p.GameIngredients : p.Ingredients);
            int garnishCount = pots.Max(p => game ? p.GameGarnishes : p.Garnishes);
            List<StewComponent> stews = StewStages.All.Select(e => e.Stew).Where(s => s != null).ToList();
            float ingredientTimes = stews.Count == 0 ? 1f : Math.Max(0f, stews.Max(s => Times(s, VanillaEdits.StewIngredients, s.BaseEffectsMultiplier, game)));
            float liquidTimes = stews.Count == 0 ? 1f : Math.Max(0f, stews.Max(s => Times(s, VanillaEdits.StewBaseLiquid, s.BaseLiquidEffectsMultiplier, game)));
            float extraTimes = stews.Count == 0 ? 1f : Math.Max(0f, stews.Max(s => Times(s, VanillaEdits.StewExtras, s.ExtraEffectsMultiplier, game)));

            List<CookableItem> cookables = HashedGeneralValue<Item>.All.Where(i => i != null).Select(i => i.GetComponent<CookableItem>()).Where(c => c != null).ToList();
            List<CookingGarnish> garnishes = cookables.OfType<CookingGarnish>().ToList();
            float ingredient = Best(cookables.Where(c => c.GetType() == typeof(CookableItem)).Select(c => Gate(Effects(c, VanillaEdits.CookableEffects, c.Effects, game))));
            float garnishAdds = Best(garnishes.Select(g => Gate(Effects(g, VanillaEdits.CookableEffects, g.Effects, game))));
            float garnishTimes = Math.Max(1f, Best(garnishes.Select(g => Multiplier(Effects(g, VanillaEdits.GarnishMultipliers, g.EffectMultipliers, game)))));
            float liquid = Best(Liquids.GameLiquids().Where(l => l.Data != null).Select(l => l.Data).Concat(Liquids.All.Where(l => l.Error == null).Select(l => l.Data))
                .Select(d => Gate(Effects(d, VanillaEdits.LiquidEffects, d.Effects, game))));
            List<StewRecipe> recipes = HashedGeneralValue<StewRecipe>.All.Where(r => r != null).ToList();
            float extra = Best(recipes.Select(r => Gate(Effects(r, VanillaEdits.ExtraEffects, r.ExtraEffects, game))));
            float modifier = Math.Max(1f, Best(recipes.Select(r => Multiplier(Effects(r, VanillaEdits.EffectModifiers, r.EffectModifiers, game)))) * extraTimes);

            float sum = Gate(game ? GameGlobal : LiveGlobal) + count * ingredient * ingredientTimes + liquid * liquidTimes + extra * extraTimes + garnishCount * garnishAdds;
            return sum * (float)Math.Pow(garnishTimes, garnishCount) * modifier;
        }

        internal static float Gate(IEnumerable<EffectDefinitionApplication> effects) =>
            (effects ?? Enumerable.Empty<EffectDefinitionApplication>()).Where(e => e != null && IsFullness(e.EffectToModify)).Sum(e => e.StrengthMultiplier);

        static float Multiplier(IEnumerable<EffectDefinitionApplication> effects) =>
            Best((effects ?? Enumerable.Empty<EffectDefinitionApplication>()).Where(e => e != null && IsFullness(e.EffectToModify)).Select(e => e.StrengthMultiplier));

        static float Times(StewComponent stew, FieldInfo field, float live, bool game) => game ? (float)VanillaEdits.Original(stew, field, live) : live;

        static IEnumerable<EffectDefinitionApplication> Effects(object target, FieldInfo field, IEnumerable<EffectDefinitionApplication> live, bool game) =>
            game ? VanillaEdits.Original(target, field, live) as IEnumerable<EffectDefinitionApplication> : live;

        static bool IsFullness(EffectDefinition effect) => effect is StatChangeEffect stat && stat.StatToAffect != null && stat.StatToAffect.Hash == FullnessHash;

        static float Best(IEnumerable<float> values)
        {
            float best = 0f;
            foreach (float value in values)
            {
                if (value > best)
                    best = value;
            }
            return best;
        }

        internal sealed class Pot
        {
            internal Pot(PotDefinition definition)
            {
                Definition = definition;
                GameIngredients = definition.MinNumberOfIngredients;
                GameGarnishes = definition.NumberOfGarnishes;
            }

            internal PotDefinition Definition { get; }

            internal List<NetworkPrefab> Prefabs { get; } = new List<NetworkPrefab>();

            internal int GameIngredients { get; }

            internal int GameGarnishes { get; }

            internal int Ingredients => Definition.MinNumberOfIngredients;

            internal int Garnishes => Definition.NumberOfGarnishes;
        }
    }
}
