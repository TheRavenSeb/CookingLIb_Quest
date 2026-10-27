using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace CookingLib.Tests
{
    internal static class Program
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        static readonly Assembly lib = typeof(LiquidKind).Assembly;
        static int passed, failed;

        static int Main()
        {
            AppDomain.CurrentDomain.AssemblyResolve += FromGame;

            Run("Norm", NormTests);
            Run("Suggest", SuggestTests);
            Run("effect aliases", AliasTests);
            Run("claims", ClaimTests);
            Run("page budget", PageTests);
            Run("reachability", ReachTests);
            Run("NumberFor", NumberTests);
            Run("multipliers", MultiplierTests);
            Run("garnish and stew claims", EditClaimTests);
            Run("pot knobs", PotRuleTests);
            Run("stew specs", StewSpecTests);
            Run("garnish specs", GarnishSpecTests);
            Run("vanilla edits", OriginalTests);

            Console.WriteLine(passed + " passed, " + failed + " failed");
            return failed == 0 ? 0 : 1;
        }

        static void NormTests()
        {
            Same("babulegfullcooked", Norm("Babu Leg Full Cooked"), "spaces and case");
            Same("damageprotectionindirecteffect", Norm("Damage_Protection-Indirect effect"), "underscores and hyphens");
            Same("birdsnest", Norm("Bird's Nest"), "apostrophes");
            Same("", Norm(null), "null");
            Same("luminosiyindirecteffect", Norm("Luminosiy indirect effect"), "the game's Luminosiy spelling");
            Same(false, Norm("Luminosiy indirect effect") == Norm("Luminosity Indirect Effect"), "Luminosiy and Luminosity stay apart");
        }

        static void SuggestTests()
        {
            string[] spellings = Aliases().SelectMany(a => (string[])Get(a, "Spellings")).ToArray();
            string[] garlic = { "Garlic Full Ripe", "Garlic Half Ripe", "Garlic Full Cooked", "Garlic Half Cooked", "Carrot" };
            string[] many = Enumerable.Range(1, 21).Select(i => "Name " + i).ToArray();

            Same(". Did you mean Speed?", Suggest("Speeed", new[] { "Speed Indirect Effect", "Heal", "Feed" }, "Effects", spellings), "a typo finds the alias");
            Same(". Did you mean Garlic Full Ripe, Garlic Half Ripe, Garlic Full Cooked?", Suggest("Garlic", garlic, "Items", null), "at most 3, closest first");
            Same(". Did you mean Heal?", Suggest("Hea", new[] { "Heal", "heal", "HEAL" }, "Effects", null), "duplicates ignore case");
            Same(". Effects: Feed, Heal, Nourish", Suggest("zzz", new[] { "Nourish", "Heal", "Feed" }, "Effects", null), "nothing close lists a short list");
            Same("", Suggest("zzz", many, "Names", null), "nothing close and more than 20 names");
            Same(". Names: Name 1, Name 10, Name 11, Name 12, Name 13, Name 2, Name 3, Name 4, Name 5, Name 6, Name 7, Name 8, Name 9", Suggest("zzz", many.Take(13), "Names", null), "a short list is sorted");
            Same(19, Suggest("zzz", many.Take(20), "Names", null).Count(c => c == ','), "20 names are still listed");
        }

        static void AliasTests()
        {
            Same(11, Aliases().Count, "alias count");

            var table = new (uint Number, string Game, string Short)[]
            {
                (6764, "Speed Indirect Effect", "Speed"),
                (654, "Damage Indirect effect", "Damage"),
                (64858, "Damage Protection Indirect effect", "Protection"),
                (65294, "Luminosiy indirect effect", "Glow"),
                (48456, "Heal", "Heal"),
                (3280, "Feed", "Feed"),
                (20046, "Nourish", "Nourish"),
                (29250, "Poison over Time", "Poison"),
                (62720, "Reveal Effect", "Reveal"),
                (44252, "Teleport Home", "TeleportHome"),
                (27214, "Teleport Original Respawn", "TeleportRespawn")
            };
            foreach (var row in table)
            {
                object alias = Aliases().FirstOrDefault(a => (uint)Get(a, "Number") == row.Number);
                Same(row.Game, alias == null ? null : Get(alias, "GameName"), "game name of " + row.Number);
                Same(row.Short, alias == null ? null : ((string[])Get(alias, "Spellings"))[0], "short name of " + row.Number);
            }

            var spelled = new (string Name, uint Number)[]
            {
                ("Speed", 6764), ("Damage", 654), ("Protection", 64858), ("Damage Protection", 64858), ("Glow", 65294),
                ("Luminosity", 65294), ("Luminosity Indirect Effect", 65294), ("Heal", 48456), ("Feed", 3280), ("Nourish", 20046),
                ("Poison", 29250), ("Reveal", 62720), ("TeleportHome", 44252), ("TeleportRespawn", 27214),
                ("teleport home", 44252), ("GLOW", 65294), ("damage_protection", 64858)
            };
            foreach (var row in spelled)
            {
                object alias = AliasFor(row.Name);
                Same(row.Number, alias == null ? null : Get(alias, "Number"), "alias " + row.Name);
            }

            Same(null, AliasFor("Luminosiy indirect effect"), "the game's own name is not an alias");
            Same(null, AliasFor("Speed Indirect Effect"), "a game name is matched by name, not alias");
            Same(null, AliasFor("Speedy"), "near misses are not aliases");
            Same(null, AliasFor(""), "empty");
        }

        static void ClaimTests()
        {
            Same(null, Take("a.one", Pot("ingredients", "5")), "the first claim wins");
            Same("a.one", OwnerOf("pots/ingredients"), "owner after the first claim");
            Same(null, Take("b.two", Pot("ingredients", "5")), "an equal value agrees");
            Same("a.one", OwnerOf("pots/ingredients"), "an agreeing claim does not take over");
            Same("pots ingredients is already 5, set by a.one", Take("c.three", Pot("ingredients", "6")), "a later different value is refused naming the owner");
            Same(1, Count(Get(T("Claims"), "Refused")), "the refusal is recorded");
            Same(true, Take("c.three", Pot("garnishes", "2"), Pot("ingredients", "6")) != null, "a refusal is whole");
            Same(null, OwnerOf("pots/garnishes"), "nothing from a refused set is held");

            Same(0, Count(Release("a.one")), "release keeps a value someone else agreed to");
            Same("b.two", OwnerOf("pots/ingredients"), "the agreeing mod owns it after the release");
            Same(0, Count(Get(T("Claims"), "Refused")), "release clears refusals against the owner");
            Same("pots ingredients is already 5, set by b.two", Take("c.three", Pot("ingredients", "6")), "still refused while b.two holds it");

            Same("pots/ingredients", ((IList)Release("b.two"))[0], "the last holder frees the key");
            Same(null, Take("c.three", Pot("ingredients", "6")), "a free key can be taken");
            Same("c.three", OwnerOf("pots/ingredients"), "new owner");
            Same(1, Count(Release("c.three")), "release frees it again");
            Same(0, Count(Get(T("Claims"), "Held")), "nothing left held");

            Same(null, Take("x.first", Call(T("Claims"), "RecipeNumber", 64001u, "x.first")), "a recipe number is claimed");
            Same("recipe number 64001 is already x.first, set by x.first", Take("x.second", Call(T("Claims"), "RecipeNumber", 64001u, "x.second")), "two ids on one recipe number");
            Release("x.first");
            Release("x.second");
            Same("none", Get(Pot("ingredients", null), "Value"), "a null value is none");
        }

        static void PageTests()
        {
            Type budget = T("PageBudget");
            Type kind = T("PageKind");
            object recipe = Enum.Parse(kind, "Recipe");
            object edible = Enum.Parse(kind, "Edible");
            object ingredient = Enum.Parse(kind, "Ingredient");
            Type effect = budget.GetMethod("Check", Any).GetParameters()[2].ParameterType.GetGenericArguments()[0];
            Array none = Array.CreateInstance(effect, 0);
            Array zeros = Array.CreateInstance(effect, 5);
            for (int i = 0; i < zeros.Length; i++)
                zeros.SetValue(Activator.CreateInstance(effect, null, 0f, 0), i);
            Array oneZero = Array.CreateInstance(effect, 1);
            oneZero.SetValue(zeros.GetValue(0), 0);

            Same(false, Get(budget, "Live"), "the game's page settings are not readable offline");
            Same(4, Get(budget, "Slots"), "fallback slots");
            Same(3, Call(budget, "Effects", recipe), "fallback recipe effects");
            Same(3, Call(budget, "Effects", edible), "fallback food effects");
            Same(3, Call(budget, "Effects", ingredient), "fallback ingredient effects");
            Same(true, ((string)Call(budget, "Lengths")).EndsWith("(the game's page settings were not readable, so 4 and 3)"), "lengths say they fell back");

            Same(null, Try(() => Call(budget, "Check", recipe, "page", none, 4)), "4 slots fit");
            Same("page has room for 4 ingredients and this gives it 5", Try(() => Call(budget, "Check", recipe, "page", none, 5)), "5 slots are refused");
            Same(null, Try(() => Call(budget, "Check", ingredient, "page", zeros, 0)), "an effect at 0 takes no room on an ingredient page");
            Same(null, Try(() => Call(budget, "Check", edible, "page", zeros, 0)), "an effect at 0 takes no room on a food page");
            Same("effect none has no page glyph, so page can't show it", Try(() => Call(budget, "Check", recipe, "page", oneZero, 0)), "on a recipe page every entry counts");
        }

        static void ReachTests()
        {
            Type reach = T("Reachability");
            Type knobs = T("PotKnobs");
            FieldInfo pots = knobs.GetField("pots", Any);

            Same(false, Get(reach, "Known"), "pots are unknown offline");
            Same(true, Call(reach, "IsReachable", 5), "unknown pots reach every count");
            Same(0, Count(Call(reach, "Counts")), "unknown pots list no counts");

            Type pot = knobs.GetNestedType("Pot", Any);
            ConstructorInfo make = pot.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
            Type definition = make.GetParameters()[0].ParameterType;
            FieldInfo ingredients = definition.GetField("minNumberOfIngredients", Any);
            IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(pot));
            object plain = FormatterServices.GetUninitializedObject(definition);
            object changed = FormatterServices.GetUninitializedObject(definition);
            ingredients.SetValue(plain, 4);
            ingredients.SetValue(changed, 4);
            list.Add(make.Invoke(new[] { plain }));
            list.Add(make.Invoke(new[] { changed }));

            try
            {
                pots.SetValue(null, list);
                Same(true, Get(reach, "Known"), "two stand in pots are known");
                Same(false, Get(reach, "CountChanged"), "game counts are unchanged");
                Same("4", Call(reach, "CountsText"), "one count");
                Same(false, Call(reach, "IsReachable", 5), "5 slots can't be made at 4");

                ingredients.SetValue(changed, 5);
                Same(true, Get(reach, "CountChanged"), "a rule changed one pot");
                Same("4 or 5", Call(reach, "CountsText"), "both counts, in order");
                Same(true, Call(reach, "IsReachable", 4), "4 slots reachable");
                Same(true, Call(reach, "IsReachable", 5), "5 slots reachable");
                Same(false, Call(reach, "IsReachable", 3), "3 slots not reachable");
            }
            finally
            {
                pots.SetValue(null, null);
            }
        }

        static void NumberTests()
        {
            Type registry = T("Registry");
            Same(404188086u, Call(registry, "NumberFor", "example.ashwater"), "pinned number for example.ashwater");
            Same(593930330u, Call(registry, "NumberFor", "example.reddye"), "pinned number for example.reddye");
            Same(Call(registry, "NumberFor", "example.reddye"), Call(registry, "NumberFor", "Example.RedDye"), "ids ignore case");
            Same(94210u, Call(registry, "NumberFor", "low.7826"), "a hash at or below 65535 moves above it");

            int outside = 0;
            for (int i = 0; i < 20000; i++)
            {
                uint number = (uint)Call(registry, "NumberFor", "range.test" + i);
                if (number <= 65535 || number > 0x7FFFFFFF)
                    outside++;
            }
            Same(0, outside, "every number is above 65535 and below 2^31");
        }

        static void MultiplierTests()
        {
            Type edits = T("VanillaEdits");
            Same(true, Call(edits, "IsMultiplier", 0f), "0 is a multiplier");
            Same(true, Call(edits, "IsMultiplier", 10f), "10 is a multiplier");
            Same(false, Call(edits, "IsMultiplier", -0.5f), "below 0 is not");
            Same(false, Call(edits, "IsMultiplier", 10.5f), "above 10 is not");
            Same(false, Call(edits, "IsMultiplier", float.NaN), "NaN is not");

            Same(null, Call(edits, "Multipliers", null, "Multipliers"), "null keeps the game's");
            Same("Multipliers can't hold a null effect", Try(() => Call(edits, "Multipliers", new List<EffectSpec> { null }, "Multipliers")), "a null entry is refused");
            Same("Multipliers must be 0 to 10", Try(() => Call(edits, "Multipliers", Specs("Speed", 11f), "Multipliers")), "11 is refused before the name is looked up");
            Same("Multipliers must be 0 to 10", Try(() => Call(edits, "Multipliers", Specs("Speed", -1f), "Multipliers")), "minus 1 is refused");
            Same("Multipliers can't use OverSeconds, a stew gives its effects at once", Try(() => Call(edits, "Multipliers", new List<EffectSpec> { new EffectSpec { Effect = "Heal", Strength = 2f, OverSeconds = 5 } }, "Multipliers")), "OverSeconds is refused");
        }

        static void EditClaimTests()
        {
            object garnish = Call(T("Claims"), "Garnish", 7u, "Salt", "multipliers", "Protection 2");
            Same("garnish/7/multipliers", Get(garnish, "Key"), "garnish claim key");
            Same("garnish Salt multipliers", Get(garnish, "Label"), "garnish claim label");
            object stew = Call(T("Claims"), "Stew", 9u, "Pumpkin Recipe Raw Stew", "tint", "0.1 0.9 0.1 1");
            Same("stew/9/tint", Get(stew, "Key"), "stew claim key");
            Same("stew Pumpkin Recipe Raw Stew tint", Get(stew, "Label"), "stew claim label");

            Same(null, Take("a.one", garnish, stew), "garnish and stew fields are claimed");
            Same("garnish Salt multipliers is already Protection 2, set by a.one", Take("b.two", Call(T("Claims"), "Garnish", 7u, "Salt", "multipliers", "Protection 3")), "another value is refused");
            Same(null, Take("b.two", Call(T("Claims"), "Stew", 9u, "Pumpkin Recipe Raw Stew", "tint", "0.1 0.9 0.1 1")), "the same tint agrees");
            Same(1, Count(Release("a.one")), "release frees the garnish field, b.two keeps the tint");
            Same("b.two", OwnerOf("stew/9/tint"), "the agreeing mod owns the tint");
            Same(1, Count(Release("b.two")), "the last holder frees the tint");
            Same(0, Count(Get(T("Claims"), "Held")), "nothing left held");
        }

        static void PotRuleTests()
        {
            Same("RejectionForce must be 0 to 1000", PotRules.Set(new PotRulesSpec { Id = "t.push", RejectionForce = 1001f }).Error, "1001 is refused");
            Same("RejectionForce must be 0 to 1000", PotRules.Set(new PotRulesSpec { Id = "t.push", RejectionForce = -1f }).Error, "minus 1 is refused");
            Same("RejectionForce must be 0 to 1000", PotRules.Set(new PotRulesSpec { Id = "t.push", RejectionForce = float.NaN }).Error, "NaN is refused");
            Same("NoRecipeStew is empty", PotRules.Set(new PotRulesSpec { Id = "t.stew", NoRecipeStew = " " }).Error, "an empty NoRecipeStew is refused");
            Same("set at least one of Ingredients, Garnishes, StewingTemperature, GlobalEffects, NoRecipeStew, RejectionForce", PotRules.Set(new PotRulesSpec { Id = "t.none" }).Error, "an empty spec is refused");

            LibPotRules push = PotRules.Set(new PotRulesSpec { Id = "t.push", RejectionForce = 0f });
            Same(null, push.Error, "0 is allowed");
            Same(false, push.IsApplied, "not applied offline");
            Same("t.push", OwnerOf("pots/RejectionForce"), "the push is claimed");
            Same("pots RejectionForce is already 0, set by t.push", PotRules.Set(new PotRulesSpec { Id = "t.other", RejectionForce = 1000f }).Error, "another push is refused");
            LibPotRules stew = PotRules.Set(new PotRulesSpec { Id = "t.stew", NoRecipeStew = " Pumpkin Recipe Raw Stew " });
            Same(null, stew.Error, "a stew name waits for the world");
            Same("NoRecipeStew Pumpkin Recipe Raw Stew", Call(T("PotRules"), "Describe", stew), "the name is trimmed");
            Same(null, OwnerOf("pots/NoRecipeStew"), "the stew name is claimed once it resolves");
            Same(true, PotRules.Remove("t.push"), "remove the push");
            Same(true, PotRules.Remove("t.stew"), "remove the stew");
            Same(false, PotRules.Remove("t.other"), "a refused rule was never set");
            Same(0, Count(Get(T("Claims"), "Held")), "nothing left held");
        }

        static void StewSpecTests()
        {
            Same("spec is null", VanillaStews.Modify(null).Error, "a null spec is refused");
            Same("id must look like author.thing", VanillaStews.Modify(new StewSpec { Id = "stew", Stew = "Non Recipe Raw Stew", Extras = 1f }).Error, "an id without a dot is refused");
            Same("Stew is empty", VanillaStews.Modify(new StewSpec { Id = "t.stew", Ingredients = 1f }).Error, "an empty stew name is refused");
            Same("set at least one of Ingredients, BaseLiquid, Extras, Tint", VanillaStews.Modify(new StewSpec { Id = "t.stew", Stew = "Non Recipe Raw Stew" }).Error, "an empty spec is refused");
            Same("Ingredients must be 0 to 10", VanillaStews.Modify(new StewSpec { Id = "t.stew", Stew = "Non Recipe Raw Stew", Ingredients = 11f }).Error, "11 is refused");
            Same("BaseLiquid must be 0 to 10", VanillaStews.Modify(new StewSpec { Id = "t.stew", Stew = "Non Recipe Raw Stew", BaseLiquid = -1f }).Error, "minus 1 is refused");
            Same("Extras must be 0 to 10", VanillaStews.Modify(new StewSpec { Id = "t.stew", Stew = "Non Recipe Raw Stew", Extras = float.NaN }).Error, "NaN is refused");
            Same("tint channels must be 0 to 1", VanillaStews.Modify(Tinted("t.stew", 2f)).Error, "a channel above 1 is refused");
            Same(null, VanillaStews.Modify(Tinted("t.tint", 1f)).Error, "a channel at 1 is allowed");

            LibStew entry = VanillaStews.Modify(new StewSpec { Id = "t.stew", Stew = "Non Recipe Raw Stew", Ingredients = 1.5f });
            Same(null, entry.Error, "a good spec waits for the world");
            Same(false, entry.IsApplied, "not applied offline");
            Same(null, Get(entry, "Prefab"), "no prefab offline");
            Same("id is already used, Revert it first", VanillaStews.Modify(new StewSpec { Id = "T.Stew", Stew = "Non Recipe Raw Stew", Extras = 2f }).Error, "ids ignore case");
            Same(true, VanillaStews.Revert("t.stew"), "revert");
            Same(false, VanillaStews.Revert("t.stew"), "revert twice");
            Same(true, VanillaStews.Revert("t.tint"), "revert the tint");
            Same(0, VanillaStews.All.Count(e => e.Error == null), "nothing left live");
        }

        static void GarnishSpecTests()
        {
            Same("spec is null", VanillaGarnishes.Modify(null).Error, "a null spec is refused");
            Same("id must look like author.thing", VanillaGarnishes.Modify(new GarnishSpec { Id = "salt", Item = "Salt" }).Error, "an id without a dot is refused");
            Same("Item is empty", VanillaGarnishes.Modify(new GarnishSpec { Id = "t.salt" }).Error, "an empty item is refused");
            Same(false, VanillaGarnishes.Revert("t.salt"), "a refused change is not reverted");
        }

        static void OriginalTests()
        {
            Type edits = T("VanillaEdits");
            Probe probe = new Probe();
            FieldInfo value = typeof(Probe).GetField("value", Any);
            FieldInfo other = typeof(Probe).GetField("other", Any);
            object claim = Pot("probe", "2");

            Same(null, Take("t.edit", claim), "the probe key is held");
            Call(edits, "Write", claim, probe, "x1", value, 2f, null);
            Same(2f, probe.value, "the write lands");
            Same(1f, Call(edits, "Original", probe, value, 5f), "the original is kept");
            Same(5f, Call(edits, "Original", probe, other, 5f), "an untouched field gives the live value");
            Same(5f, Call(edits, "Original", new Probe(), value, 5f), "another target gives the live value");
            Same(1, Count(Call(edits, "Release", "t.edit")), "release frees the key");
            Same(1f, probe.value, "release undoes the write");
            Same(5f, Call(edits, "Original", probe, value, 5f), "nothing original after the release");

            Type color = typeof(StewSpec).GetField("Tint").FieldType.GetGenericArguments()[0];
            Same("0.2 0.8 0.2 1", Call(T("VanillaStews"), "Rgba", Activator.CreateInstance(color, 0.2f, 0.8f, 0.2f, 1f)), "a tint prints with up to 3 decimals");
        }

        static List<EffectSpec> Specs(string effect, float strength) => new List<EffectSpec> { new EffectSpec { Effect = effect, Strength = strength } };

        sealed class Probe
        {
            internal float value = 1f;

            internal float other = 3f;
        }

        static StewSpec Tinted(string id, float red)
        {
            StewSpec spec = new StewSpec { Id = id, Stew = "Non Recipe Raw Stew" };
            FieldInfo tint = typeof(StewSpec).GetField("Tint");
            Type color = tint.FieldType.GetGenericArguments()[0];
            tint.SetValue(spec, Activator.CreateInstance(color, red, 0f, 0f, 1f));
            return spec;
        }

        static string Norm(string name) => (string)Call(T("Names"), "Norm", name);

        static string Suggest(string name, IEnumerable<string> names, string plural, IEnumerable<string> alsoClose) =>
            (string)Call(T("Names"), "Suggest", name, names, plural, alsoClose);

        static List<object> Aliases() => ((IEnumerable)Get(T("EffectNames"), "Aliases")).Cast<object>().ToList();

        static object AliasFor(string name) => Call(T("EffectNames"), "AliasFor", name);

        static object Pot(string knob, string value) => Call(T("Claims"), "Pot", knob, value);

        static string Take(string id, params object[] claims)
        {
            Array typed = Array.CreateInstance(T("Claims").GetNestedType("Claim", Any), claims.Length);
            Array.Copy(claims, typed, claims.Length);
            return (string)Call(T("Claims"), "Take", id, typed);
        }

        static string OwnerOf(string key) => (string)Call(T("Claims"), "OwnerOf", key);

        static object Release(string id) => Call(T("Claims"), "Release", id);

        static int Count(object list) => ((IEnumerable)list).Cast<object>().Count();

        static Type T(string name) => lib.GetType("CookingLib." + name, true);

        static object Get(Type type, string name) => type.GetProperty(name, Any).GetValue(null);

        static object Get(object target, string name) => target.GetType().GetProperty(name, Any).GetValue(target);

        static object Call(Type type, string name, params object[] args)
        {
            MethodInfo method = type.GetMethods(Any).Where(m => m.Name == name).Single(m => Fits(m.GetParameters(), args));
            try
            {
                return method.Invoke(null, args);
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }
        }

        static bool Fits(ParameterInfo[] parameters, object[] args) =>
            parameters.Length == args.Length
            && parameters.Zip(args, (p, a) => a == null ? !p.ParameterType.IsValueType : p.ParameterType.IsInstanceOfType(a)).All(ok => ok);

        static string Try(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (ArgumentException e)
            {
                return e.Message;
            }
        }

        static void Same(object expected, object actual, string what)
        {
            if (Equals(expected, actual))
            {
                passed++;
                return;
            }
            failed++;
            Console.WriteLine("  FAIL " + what + ": expected " + Show(expected) + ", got " + Show(actual));
        }

        static string Show(object value) => value == null ? "null" : "'" + value + "'";

        static void Run(string name, Action tests)
        {
            int before = passed + failed;
            int failedBefore = failed;
            try
            {
                tests();
            }
            catch (Exception e)
            {
                failed++;
                Console.WriteLine("  FAIL " + name + " threw " + e);
            }
            Console.WriteLine(name + ": " + (passed + failed - before) + " checks, " + (failed - failedBefore) + " failed");
        }

        static Assembly FromGame(object sender, ResolveEventArgs args)
        {
            string game = typeof(Program).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().First(a => a.Key == "GamePath").Value;
            string file = new AssemblyName(args.Name).Name + ".dll";
            foreach (string folder in new[] { "A Township Tale_Data\\Managed", "MelonLoader\\net35" })
            {
                string path = Path.Combine(Path.Combine(game, folder), file);
                if (File.Exists(path))
                    return Assembly.LoadFrom(path);
            }
            return null;
        }
    }
}
