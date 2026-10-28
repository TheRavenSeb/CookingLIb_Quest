using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Il2CppAlta;
using Il2CppAlta.Alchemy;
using Il2CppAlta.Cooking;
using Il2CppAlta.Global;
using Il2CppAlta.Inventory;
using Il2CppAlta.Liquid;
using Il2CppAlta.Networking;
using MelonLoader.Utils;

namespace CookingLib
{
    internal static class FoodsTable
    {
        const uint FullnessHash = 34710;

        static readonly FieldInfo durationField = typeof(EffectDefinitionApplication).GetField("durationInSeconds", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo liquidsField = typeof(StewRecipe).GetField("possibleLiquids", BindingFlags.NonPublic | BindingFlags.Instance);

        static readonly string[] sections = { "liquids", "eaten", "ingredients", "garnishes", "recipes", "stages" };

        static readonly KeyValuePair<string, uint>[] columns =
        {
            new KeyValuePair<string, uint>("Feed", 3280),
            new KeyValuePair<string, uint>("Nourish", 20046),
            new KeyValuePair<string, uint>("Heal", 48456),
            new KeyValuePair<string, uint>("Speed", 6764),
            new KeyValuePair<string, uint>("Damage", 654),
            new KeyValuePair<string, uint>("Protection", 64858),
            new KeyValuePair<string, uint>("Glow", 65294)
        };

        const string Header = "kind\thow\tname\thash\tstage\tFeed\tNourish\tHeal\tSpeed\tDamage\tProtection\tGlow\tother\tover_s\tfx\tgate\twas\tby";

        internal static string Run(string args)
        {
            string text = (args ?? "").Trim();
            string[] words = text.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            string first = words.Length == 0 ? "" : words[0].ToLowerInvariant();
            if (first == "mix")
                return Mix(words.Length > 1 ? words[1] : "");
            if (first.Length > 0 && (words.Length > 1 || (first != "all" && !sections.Contains(first))))
                return "no foods section named '" + text + "'" + Names.Suggest(text, sections.Concat(new[] { "all", "mix" }), "Sections");

            Stopwatch watch = Stopwatch.StartNew();
            List<NetworkPrefab> prefabs = PrefabWalk.All();
            Dictionary<NetworkPrefab, EdibleObject> edibles = new Dictionary<NetworkPrefab, EdibleObject>();
            foreach (NetworkPrefab prefab in prefabs)
            {
                EdibleObject edible = prefab.GetComponent<EdibleObject>();
                if (edible != null)
                    edibles[prefab] = edible;
            }
            watch.Stop();

            Dictionary<string, List<Line>> rows = new Dictionary<string, List<Line>>
            {
                ["liquids"] = LiquidRows(),
                ["eaten"] = EatenRows(edibles),
                ["ingredients"] = IngredientRows(),
                ["garnishes"] = GarnishRows(),
                ["recipes"] = RecipeRows(out int recipes),
                ["stages"] = StageRows()
            };

            string path = Path.Combine(MelonEnvironment.UserDataDirectory, "CookingLib-foods.tsv");
            File.WriteAllText(path, Table(rows));

            int garnishes = HashedGeneralValue<Item>.All.Count(item => item != null && Garnish(item) != null);
            StringBuilder reply = new StringBuilder("vanilla edits applied: ").Append(VanillaEdits.All.Count).Append('\n').Append("foods: ")
                .Append(rows["liquids"].Count).Append(" liquids, ")
                .Append(rows["eaten"].Count).Append(" eaten foods, ")
                .Append(rows["ingredients"].Count).Append(" stew ingredients, ")
                .Append(garnishes).Append(" garnishes, ")
                .Append(recipes).Append(" recipes, ")
                .Append(rows["stages"].Count).Append(" stew stages\n")
                .Append("walked ").Append(prefabs.Count).Append(" prefabs in ").Append(watch.ElapsedMilliseconds).Append(" ms\n");
            foreach (string section in sections.Where(s => first == "all" || s == first))
                AppendSection(reply, section, rows[section].Select(line => line.Console));
            return reply.Append("wrote ").Append(path).ToString();
        }

        static string Table(Dictionary<string, List<Line>> rows)
        {
            StringBuilder text = new StringBuilder();
            text.Append("# CookingLib ").Append(Main.Version).Append(" foods: the game's live values, tab separated.\n");
            text.Append("# vanilla edits applied: ").Append(VanillaEdits.All.Count).Append('\n');
            text.Append("# Effect columns by number: Feed 3280, Nourish 20046, Heal 48456, Speed 6764, Damage 654, Protection 64858, Glow 65294 (the game's Luminosiy indirect effect).\n");
            text.Append("# other: every other effect. A * marks an effect listed more than once, summed. over_s: effects the game spreads over that many seconds.\n");
            text.Append("# fx: effects that are not zero, a ! past 3 (a page shows 3). gate: the Feed that counts toward the fullness cap.\n");
            text.Append("# Speed, Damage, Protection and Glow are buffs that last 900 s each.\n");
            text.Append("# recipes: per stage, fixed is the part no ingredient changes on that base, modifier multiplies the stew's whole total for that effect.\n");
            text.Append("# was and by: the game's value before a CookingLib edit and the mod that made it. Edits by other mods don't show.\n");
            foreach (string section in sections)
                AppendSection(text, section, rows[section].Select(line => line.Tsv));
            return text.ToString();
        }

        static void AppendSection(StringBuilder text, string section, IEnumerable<string> rows)
        {
            text.Append("## ").Append(section).Append('\n').Append(Header).Append('\n');
            foreach (string row in rows)
                text.Append(row).Append('\n');
        }

        static List<Line> LiquidRows()
        {
            List<Line> rows = Liquids.GameLiquids().OrderBy(l => l.name, StringComparer.OrdinalIgnoreCase)
                .Select(l => Row("liquid", "game", l.name, l.Hash, "", l.Data?.Effects, null, l.Data, Before(l.Data))).ToList();
            foreach (LibLiquid liquid in Liquids.All.Where(l => l.Error == null).OrderBy(l => l.Id, StringComparer.OrdinalIgnoreCase))
                rows.Add(Row("liquid", liquid.IsShared ? "shared" : "server only", liquid.Id, liquid.BaseDefinition.Hash, "", liquid.Data?.Effects));
            return rows;
        }

        static List<Line> EatenRows(Dictionary<NetworkPrefab, EdibleObject> edibles)
        {
            List<Line> rows = new List<Line>();
            HashSet<NetworkPrefab> covered = new HashSet<NetworkPrefab>();
            foreach (Item item in HashedGeneralValue<Item>.All.Where(i => i != null && i.Prefab != null).OrderBy(i => i.name, StringComparer.OrdinalIgnoreCase))
            {
                if (!edibles.TryGetValue(item.Prefab, out EdibleObject edible))
                    continue;
                covered.Add(item.Prefab);
                rows.Add(Row("eaten", "item", item.name, item.Hash, StageOf(item), edible.ConsumptionEffects, null, edible, Before(edible)));
            }
            foreach (KeyValuePair<NetworkPrefab, EdibleObject> pair in edibles.Where(p => !covered.Contains(p.Key)).OrderBy(p => p.Key.name, StringComparer.OrdinalIgnoreCase))
                rows.Add(Row("eaten", "prefab", pair.Key.name, pair.Key.Hash, "", pair.Value.ConsumptionEffects, null, pair.Value, Before(pair.Value)));
            return rows;
        }

        static List<Line> IngredientRows()
        {
            List<Line> rows = new List<Line>();
            foreach (Item item in HashedGeneralValue<Item>.All.Where(i => i != null).OrderBy(i => i.name, StringComparer.OrdinalIgnoreCase))
            {
                CookableItem cookable = item.GetComponent<CookableItem>();
                if (cookable != null && cookable.GetType() == typeof(CookableItem))
                    rows.Add(Row("ingredient", cookable.Category == null ? "no category" : cookable.Category.name, item.name, item.Hash, StageOf(item), cookable.Effects, null, cookable, Before(cookable)));
            }
            return rows;
        }

        static List<Line> GarnishRows()
        {
            List<Line> rows = new List<Line>();
            foreach (Item item in HashedGeneralValue<Item>.All.Where(i => i != null).OrderBy(i => i.name, StringComparer.OrdinalIgnoreCase))
            {
                CookingGarnish garnish = Garnish(item);
                if (garnish == null)
                    continue;
                rows.Add(Row("garnish", "adds", item.name, item.Hash, StageOf(item), garnish.Effects, null, garnish, Before(garnish), "effects"));
                rows.Add(Row("garnish", "times", item.name, item.Hash, StageOf(item), garnish.EffectMultipliers, null, garnish, Before(garnish, VanillaEdits.GarnishMultipliers), "multipliers"));
            }
            return rows;
        }

        static List<Line> RecipeRows(out int count)
        {
            List<Line> rows = new List<Line>();
            List<StewRecipe> recipes = HashedGeneralValue<StewRecipe>.All.Where(r => r != null).OrderBy(r => r.name, StringComparer.OrdinalIgnoreCase).ToList();
            count = recipes.Count;
            foreach (StewRecipe recipe in recipes)
            {
                List<LiquidDefinition> bases = (liquidsField?.GetValue(recipe) as List<LiquidDefinition> ?? new List<LiquidDefinition>()).Where(l => l != null && l.Data != null).ToList();
                rows.Add(Row("recipe", "slots", recipe.name, recipe.Hash, "", null, Slots(recipe, bases), recipe));
                foreach (StewStages.Entry stage in StewStages.Chain(recipe.BaseVersionHash).OrderBy(e => e.Stage))
                {
                    foreach (LiquidDefinition liquid in bases)
                        rows.Add(Row("recipe", "fixed on " + LiquidName(liquid), recipe.name, recipe.Hash, stage.Stage.ToString(), Effects(recipe, stage.Stew, liquid, new List<CookableItem>(), new List<CookingGarnish>()), null, recipe, null, "effects"));
                    if (recipe.EffectModifiers != null && recipe.EffectModifiers.Length > 0)
                    {
                        IEnumerable<EffectDefinitionApplication> modifiers = recipe.EffectModifiers.Where(m => m != null)
                            .Select(m => new EffectDefinitionApplication(m.EffectToModify, m.StrengthMultiplier * stage.Stew.ExtraEffectsMultiplier));
                        rows.Add(Row("recipe", "modifier", recipe.name, recipe.Hash, stage.Stage.ToString(), modifiers, null, recipe, null, "effects"));
                    }
                }
            }
            return rows;
        }

        static List<Line> StageRows()
        {
            uint defaultRaw = StewStages.DefaultRaw;
            uint nonRecipeRaw = StewStages.NonRecipeRaw;
            List<Line> rows = new List<Line>();
            foreach (StewStages.Entry entry in StewStages.All.OrderBy(e => e.Raw.name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Stage))
            {
                List<StewRecipe> recipes = entry.Recipes;
                string how = recipes.Count > 0 ? string.Join(" / ", recipes.Select(r => r.name).ToArray())
                    : entry.Raw.Hash == defaultRaw ? "default stew" : entry.Raw.Hash == nonRecipeRaw ? "non recipe stew" : "no recipe";
                StewComponent stew = entry.Stew;
                string multipliers = "liquid " + Number(stew.BaseLiquidEffectsMultiplier) + ", ingredients " + Number(stew.BaseEffectsMultiplier)
                    + ", extras " + Number(stew.ExtraEffectsMultiplier) + (entry.IsFinal ? ", final" : "");
                rows.Add(Row("stage", how, entry.Name, entry.Hash, entry.Stage.ToString(), null, multipliers, stew));
            }
            return rows;
        }

        static string Mix(string text)
        {
            string[] words = text.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2)
                return "use: cookinglib foods mix <Raw|Cooked|Burnt> <item>, <item>, ... [+ <garnish>, ...]";
            if (!Enum.TryParse(words[0], true, out StewStage stage) || stage == StewStage.BurntAway || !Enum.IsDefined(typeof(StewStage), stage))
                return "stage must be Raw, Cooked or Burnt";

            string[] parts = words[1].Split('+');
            if (parts.Length > 2)
                return "use one + before the garnishes";

            try
            {
                List<CookableItem> ingredients = Split(parts[0]).Select(Ingredient).ToList();
                List<CookingGarnish> garnishes = parts.Length > 1 ? Split(parts[1]).Select(NeedGarnish).ToList() : new List<CookingGarnish>();
                if (ingredients.Count == 0)
                    return "name at least one ingredient";

                LiquidDefinition water = LiquidDefinition.Get(LiquidFactory.WaterHash);
                if (water == null || water.Data == null)
                    return "the game's Water is missing";

                StewRecipe recipe = StewRecipe.FindStewRecipeForIngredients(water, ingredients);
                uint start = recipe != null ? recipe.BaseVersionHash : StewStages.NonRecipeRaw;
                StewStages.Entry entry = StewStages.Find(start, stage);
                if (entry == null)
                    return StewStages.Error ?? "no " + stage + " stage for " + (recipe != null ? recipe.name : "the non recipe stew");

                List<EffectDefinitionApplication> effects = Effects(recipe, entry.Stew, water, ingredients, garnishes);
                StringBuilder reply = new StringBuilder(recipe != null ? recipe.name : "Non recipe stew").Append(", ").Append(stage)
                    .Append(" (").Append(entry.Name).Append("), on Water: ");
                reply.Append(effects.Count == 0 ? "no effects" : string.Join(", ", effects.Select(e => EffectName(e.EffectToModify) + " " + Number(e.StrengthMultiplier)).ToArray()));
                if (StewContentPatch.ResultOf(recipe) != null)
                    reply.Append(". In a pot its ResultLiquid replaces this");
                return reply.ToString();
            }
            catch (ArgumentException e)
            {
                return e.Message;
            }
        }

        static List<EffectDefinitionApplication> Effects(StewRecipe recipe, StewComponent stew, LiquidDefinition liquid, List<CookableItem> ingredients, List<CookingGarnish> garnishes)
        {
            StewContentArguments arguments = new StewContentArguments
            {
                CurrentRecipe = recipe,
                Stew = stew,
                LiquidBase = liquid,
                Ingredients = ingredients,
                Garnishes = garnishes
            };
            return StewRecipe.GetStewEffects(arguments).ToList();
        }

        static IEnumerable<string> Split(string names) =>
            names.Split(',').Select(name => name.Trim()).Where(name => name.Length > 0);

        static CookableItem Ingredient(string name)
        {
            Item item = Names.NeedItem(name);
            CookableItem cookable = item.GetComponent<CookableItem>();
            if (cookable == null || cookable.GetType() != typeof(CookableItem))
                throw new ArgumentException(item.name + " can't go in a stew");
            return cookable;
        }

        static CookingGarnish NeedGarnish(string name)
        {
            Item item = Names.NeedItem(name);
            CookingGarnish garnish = Garnish(item);
            if (garnish == null)
                throw new ArgumentException(item.name + " is not a garnish");
            return garnish;
        }

        static CookingGarnish Garnish(Item item) => item.GetComponent<CookableItem>() as CookingGarnish;

        static string StageOf(Item item)
        {
            CookingStage stage = item.GetComponent<CookingStage>();
            return stage == null ? "" : stage.stage.ToString();
        }

        static string LiquidName(LiquidDefinition liquid)
        {
            LibLiquid ours = Liquids.FindByDefinition(liquid);
            return ours != null ? ours.Id : liquid.name;
        }

        static string Slots(StewRecipe recipe, List<LiquidDefinition> bases)
        {
            List<string> parts = new List<string> { "base " + (VanillaRecipes.IsDisabled(recipe) ? "disabled" : string.Join(" / ", bases.Select(LiquidName).ToArray())) };
            List<StewIngredientBase> slots = recipe.StewIngredients ?? new List<StewIngredientBase>();
            for (int i = 0; i < slots.Count; i++)
            {
                StewIngredientBase slot = slots[i];
                string pool = slot is CookableCategoryDefinition category ? category.name
                    : slot == null ? "none" : string.Join(" / ", slot.Cookables.Where(c => c != null && c.Item != null).Select(c => c.Item.name).ToArray());
                parts.Add("slot " + (i + 1) + " " + pool);
            }
            return string.Join("; ", parts.ToArray());
        }

        static IEnumerable<EffectDefinitionApplication> Before(object target) =>
            target == null ? null : VanillaEdits.Of(target).FirstOrDefault(e => e.Field == "effects")?.Original as IEnumerable<EffectDefinitionApplication>;

        static IEnumerable<EffectDefinitionApplication> Before(object target, FieldInfo field) =>
            target == null ? null : VanillaEdits.Original(target, field, null) as IEnumerable<EffectDefinitionApplication>;

        static Line Row(string kind, string how, string name, uint hash, string stage, IEnumerable<EffectDefinitionApplication> effects, string extra = null,
            object target = null, IEnumerable<EffectDefinitionApplication> before = null, string field = null)
        {
            Dictionary<EffectDefinition, float> totals = new Dictionary<EffectDefinition, float>();
            Dictionary<EffectDefinition, int> counts = new Dictionary<EffectDefinition, int>();
            List<string> spreads = new List<string>();
            float gate = 0f;
            foreach (EffectDefinitionApplication effect in effects ?? Enumerable.Empty<EffectDefinitionApplication>())
            {
                if (effect == null || effect.EffectToModify == null)
                    continue;
                EffectDefinition definition = effect.EffectToModify;
                totals.TryGetValue(definition, out float total);
                totals[definition] = total + effect.StrengthMultiplier;
                counts.TryGetValue(definition, out int count);
                counts[definition] = count + 1;
                int seconds = durationField == null ? 0 : (int)durationField.GetValue(effect);
                if (seconds > 0)
                    spreads.Add(EffectName(definition) + ":" + seconds);
                if (definition is StatChangeEffect stat && stat.StatToAffect != null && stat.StatToAffect.Hash == FullnessHash)
                    gate += effect.StrengthMultiplier;
            }

            List<string> cells = new List<string> { kind, how, name, hash.ToString(CultureInfo.InvariantCulture), stage };
            List<string> shown = new List<string>(cells);
            foreach (KeyValuePair<string, uint> column in columns)
            {
                EffectDefinition definition = totals.Keys.FirstOrDefault(e => e.Hash == column.Value);
                string cell = definition == null ? "" : Value(totals[definition], counts[definition]);
                cells.Add(cell);
                float now = definition == null ? 0f : totals[definition];
                float was = before == null ? now : before.Where(e => e != null && e.EffectToModify != null && e.EffectToModify.Hash == column.Value).Sum(e => e.StrengthMultiplier);
                shown.Add(was == now ? cell : (cell.Length == 0 ? "0" : cell) + " (was " + Number(was) + ")");
            }

            List<string> other = new List<string>();
            if (!string.IsNullOrEmpty(extra))
                other.Add(extra);
            other.AddRange(totals.Keys.Where(e => columns.All(c => c.Value != e.Hash)).OrderBy(e => e.name, StringComparer.OrdinalIgnoreCase)
                .Select(e => e.name + ":" + Value(totals[e], counts[e])));
            int fx = totals.Values.Count(v => v != 0f);

            List<VanillaEdits.Edit> edits = target == null ? new List<VanillaEdits.Edit>() : VanillaEdits.Of(target).Where(e => field == null || e.Field == field).ToList();
            List<string> tail = new List<string>
            {
                string.Join(", ", other.ToArray()),
                string.Join(", ", spreads.ToArray()),
                fx == 0 ? "" : fx > 3 ? fx + "!" : fx.ToString(CultureInfo.InvariantCulture),
                gate == 0f ? "" : Number(gate),
                string.Join("; ", edits.Select(e => e.Field == "effects" ? e.Was : e.Field + " " + e.Was).ToArray()),
                string.Join(", ", edits.Select(e => e.By ?? "?").Distinct().ToArray())
            };
            cells.AddRange(tail);
            shown.AddRange(tail);
            return new Line(string.Join("\t", cells.Select(Clean).ToArray()), string.Join("\t", shown.Select(Clean).ToArray()));
        }

        static string Value(float total, int count) => Number(total) + (count > 1 ? "*" : "");

        static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        static string EffectName(EffectDefinition effect)
        {
            foreach (KeyValuePair<string, uint> column in columns)
            {
                if (column.Value == effect.Hash)
                    return column.Key;
            }
            return effect.name;
        }

        static string Clean(string cell) => cell == null ? "" : cell.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');

        sealed class Line
        {
            internal Line(string tsv, string console)
            {
                Tsv = tsv;
                Console = console;
            }

            internal string Tsv { get; }

            internal string Console { get; }
        }
    }
}
