using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppAlta.Alchemy;
using Il2CppAlta.Networking;

namespace CookingLib
{
    /// <summary>Changes every cooking pot: ingredient count, garnish slots, stewing temperature, the effects every stew gets, the stew a pot locks on with no recipe, and the push on an ingredient dropped into a locked pot. Server only.</summary>
    public static class PotRules
    {
        static readonly List<LibPotRules> all = new List<LibPotRules>();
        static bool worldUp;

        /// <summary>Every pot rules a mod set and has not removed, refused ones included.</summary>
        public static IReadOnlyList<LibPotRules> All => all;

        /// <summary>Sets pot rules. Call it from Main.Setup or later. The server applies them once the world is up, or at once after that. Never throws.</summary>
        public static LibPotRules Set(PotRulesSpec spec)
        {
            LibPotRules rules = new LibPotRules(spec);
            try
            {
                Validate(rules);
                string clash = Claims.Take(spec.Id, ClaimsOf(rules));
                if (clash != null)
                    throw new ArgumentException(clash);
            }
            catch (Exception e)
            {
                rules.Error = e is ArgumentException ? e.Message : e.ToString();
            }

            all.Add(rules);
            if (rules.Error != null)
            {
                Log.Warn("pot rules " + rules.Id + " refused: " + rules.Error);
                return rules;
            }

            Log.Info("pot rules " + rules.Id + " set: " + Describe(rules));
            if (worldUp)
                Changed();
            return rules;
        }

        /// <summary>The pot rules with this id that were not refused, or null.</summary>
        public static LibPotRules Get(string id)
        {
            foreach (LibPotRules rules in all)
            {
                if (rules.Error == null && string.Equals(rules.Id, id, StringComparison.OrdinalIgnoreCase))
                    return rules;
            }
            return null;
        }

        /// <summary>Removes the pot rules with this id and gives back the game's values it changed. True if it was set.</summary>
        public static bool Remove(string id)
        {
            try
            {
                List<LibPotRules> gone = all.Where(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase)).ToList();
                LibPotRules rules = gone.FirstOrDefault(r => r.Error == null);
                all.RemoveAll(gone.Contains);
                if (rules != null)
                    Claims.Release(rules.Id);
                else
                    Claims.Forget(id);
                if (rules == null)
                    return false;

                rules.IsApplied = false;
                Log.Info("pot rules " + rules.Id + " removed");
                if (worldUp)
                    Changed();
                return true;
            }
            catch (Exception e)
            {
                Safe.Fail("pot rules remove", e);
                return false;
            }
        }

        internal static IEnumerable<LibPotRules> Live => all.Where(r => r.Error == null);

        internal static void ApplyAll()
        {
            worldUp = true;
            Apply();
        }

        internal static string Describe(LibPotRules rules)
        {
            PotRulesSpec spec = rules.Spec;
            List<string> parts = new List<string>();
            if (spec.Ingredients.HasValue)
                parts.Add("Ingredients " + spec.Ingredients.Value);
            if (spec.Garnishes.HasValue)
                parts.Add("Garnishes " + spec.Garnishes.Value);
            if (spec.StewingTemperature.HasValue)
                parts.Add("StewingTemperature " + Claims.Number(spec.StewingTemperature.Value));
            if (rules.GlobalEffects != null)
                parts.Add("GlobalEffects " + Claims.Effects(rules.GlobalEffects));
            if (spec.NoRecipeStew != null)
                parts.Add("NoRecipeStew " + (rules.NoRecipeStew != null ? rules.NoRecipeStew.name : spec.NoRecipeStew.Trim()));
            if (spec.RejectionForce.HasValue)
                parts.Add("RejectionForce " + Claims.Number(spec.RejectionForce.Value));
            return string.Join(", ", parts.ToArray());
        }

        static void Changed()
        {
            Apply();
            Reachability.Check();
        }

        static void Apply()
        {
            if (!Main.IsServer)
                return;

            try
            {
                foreach (LibPotRules rules in Live.Where(r => r.Spec.NoRecipeStew != null && r.NoRecipeStew == null).ToList())
                    Resolve(rules);

                List<LibPotRules> live = Live.ToList();
                int? ingredients = live.Select(r => r.Spec.Ingredients).FirstOrDefault(v => v.HasValue);
                int? garnishes = live.Select(r => r.Spec.Garnishes).FirstOrDefault(v => v.HasValue);
                float? temperature = live.Select(r => r.Spec.StewingTemperature).FirstOrDefault(v => v.HasValue);
                EffectDefinitionApplication[] global = live.Select(r => r.GlobalEffects).FirstOrDefault(v => v != null);
                NetworkPrefab noRecipe = live.Select(r => r.NoRecipeStew).FirstOrDefault(v => v != null);
                float? push = live.Select(r => r.Spec.RejectionForce).FirstOrDefault(v => v.HasValue);
                bool applied = PotKnobs.Write(ingredients, garnishes, temperature, global, noRecipe, push);
                foreach (LibPotRules rules in live)
                    rules.IsApplied = applied;
            }
            catch (Exception e)
            {
                Safe.Fail("pot rules apply", e);
            }
        }

        static void Resolve(LibPotRules rules)
        {
            try
            {
                StewStages.Entry stew = VanillaStews.NeedStew(rules.Spec.NoRecipeStew);
                if (stew.Stage != StewStage.Raw)
                    throw new ArgumentException(stew.Name + " is a " + stew.Stage + " stew, NoRecipeStew must be a Raw one");
                if (stew.Hash == StewStages.DefaultRaw)
                    throw new ArgumentException(stew.Name + " is the default stew, so a pot locked on it would take ingredients again after a reload; NoRecipeStew must be another Raw stew");
                string clash = Claims.Take(rules.Id, new[] { Claims.Pot("NoRecipeStew", stew.Name) });
                if (clash != null)
                    throw new ArgumentException(clash);
                rules.NoRecipeStew = stew.Prefab;
            }
            catch (Exception e)
            {
                rules.Error = e is ArgumentException ? e.Message : e.ToString();
                rules.IsApplied = false;
                Claims.Release(rules.Id);
                Log.Warn("pot rules " + rules.Id + " refused: " + rules.Error);
            }
        }

        static IEnumerable<Claims.Claim> ClaimsOf(LibPotRules rules)
        {
            PotRulesSpec spec = rules.Spec;
            if (spec.Ingredients.HasValue)
                yield return Claims.Pot("Ingredients", spec.Ingredients.Value.ToString());
            if (spec.Garnishes.HasValue)
                yield return Claims.Pot("Garnishes", spec.Garnishes.Value.ToString());
            if (spec.StewingTemperature.HasValue)
                yield return Claims.Pot("StewingTemperature", Claims.Number(spec.StewingTemperature.Value));
            if (rules.GlobalEffects != null)
                yield return Claims.Pot("GlobalEffects", Claims.Effects(rules.GlobalEffects));
            if (spec.RejectionForce.HasValue)
                yield return Claims.Pot("RejectionForce", Claims.Number(spec.RejectionForce.Value));
        }

        static void Validate(LibPotRules rules)
        {
            PotRulesSpec spec = rules.Spec;
            if (spec == null)
                throw new ArgumentException("spec is null");
            if (string.IsNullOrEmpty(spec.Id) || spec.Id.IndexOf('.') <= 0)
                throw new ArgumentException("id must look like author.thing");
            if (Get(spec.Id) != null)
                throw new ArgumentException("id is already set, Remove it first");
            if (Claims.Holds(spec.Id))
                throw new ArgumentException("id is already used by another CookingLib change");
            if (!spec.Ingredients.HasValue && !spec.Garnishes.HasValue && !spec.StewingTemperature.HasValue && spec.GlobalEffects == null
                && spec.NoRecipeStew == null && !spec.RejectionForce.HasValue)
                throw new ArgumentException("set at least one of Ingredients, Garnishes, StewingTemperature, GlobalEffects, NoRecipeStew, RejectionForce");
            if (spec.Ingredients.HasValue && (spec.Ingredients.Value < 1 || spec.Ingredients.Value > 16))
                throw new ArgumentException("Ingredients must be 1 to 16");
            if (spec.Garnishes.HasValue && (spec.Garnishes.Value < 0 || spec.Garnishes.Value > 16))
                throw new ArgumentException("Garnishes must be 0 to 16");
            if (spec.StewingTemperature.HasValue && !(spec.StewingTemperature.Value > 23f && spec.StewingTemperature.Value < 100f))
                throw new ArgumentException("StewingTemperature must be above 23 and below 100");
            if (spec.NoRecipeStew != null && spec.NoRecipeStew.Trim().Length == 0)
                throw new ArgumentException("NoRecipeStew is empty");
            if (spec.RejectionForce.HasValue && !(spec.RejectionForce.Value >= 0f && spec.RejectionForce.Value <= 1000f))
                throw new ArgumentException("RejectionForce must be 0 to 1000");
            if (spec.GlobalEffects == null)
                return;

            if (spec.GlobalEffects.Contains(null))
                throw new ArgumentException("GlobalEffects can't hold a null effect");
            if (spec.GlobalEffects.Any(effect => !LiquidFactory.IsWithin100(effect.Strength)))
                throw new ArgumentException("effect strengths must be minus 100 to 100");
            EffectSpread.Refuse(spec.GlobalEffects, "GlobalEffects");

            List<EffectDefinitionApplication> effects = new List<EffectDefinitionApplication>();
            foreach (EffectSpec effect in spec.GlobalEffects)
            {
                EffectDefinition definition = EffectNames.Need(effect.Effect, EffectKinds.Game);
                if (effects.Any(existing => existing.EffectToModify == definition))
                    throw new ArgumentException("GlobalEffects lists " + definition.name + " twice");
                effects.Add(new EffectDefinitionApplication(definition, effect.Strength));
            }
            rules.GlobalEffects = effects.ToArray();
        }
    }
}
