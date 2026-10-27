using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppAlta;
using Il2CppAlta.Alchemy;

namespace CookingLib
{
    [Flags]
    internal enum EffectKinds
    {
        None = 0,
        Game = 1,
        Shared = 2,
        ServerOnly = 4,
        Placeholder = 8
    }

    internal static class EffectNames
    {
        internal const EffectKinds ForShared = EffectKinds.Game | EffectKinds.Shared;
        internal const EffectKinds ForServer = EffectKinds.Game | EffectKinds.Shared | EffectKinds.ServerOnly;

        static readonly EffectAlias[] aliases =
        {
            new EffectAlias(6764, "Speed Indirect Effect", "Speed"),
            new EffectAlias(654, "Damage Indirect effect", "Damage"),
            new EffectAlias(64858, "Damage Protection Indirect effect", "Protection", "Damage Protection"),
            new EffectAlias(65294, "Luminosiy indirect effect", "Glow", "Luminosity", "Luminosity Indirect Effect"),
            new EffectAlias(48456, "Heal", "Heal"),
            new EffectAlias(3280, "Feed", "Feed"),
            new EffectAlias(20046, "Nourish", "Nourish"),
            new EffectAlias(29250, "Poison over Time", "Poison"),
            new EffectAlias(62720, "Reveal Effect", "Reveal"),
            new EffectAlias(44252, "Teleport Home", "TeleportHome"),
            new EffectAlias(27214, "Teleport Original Respawn", "TeleportRespawn")
        };

        static HashSet<EffectDefinition> game;
        static bool emptyLogged;

        internal static IReadOnlyList<EffectAlias> Aliases => aliases;

        internal static void TakeGameSnapshot()
        {
            if (game != null && game.Count > 0)
                return;

            game = new HashSet<EffectDefinition>(HashedGeneralValue<EffectDefinition>.All.Where(e => e != null));
            if (game.Count == 0 && !emptyLogged)
            {
                emptyLogged = true;
                Log.Error("the game's effects failed to load, every effect name will be refused");
            }
        }

        internal static EffectKinds KindOf(EffectDefinition effect)
        {
            TakeGameSnapshot();
            return effect != null && game.Contains(effect) ? EffectKinds.Game : EffectKinds.None;
        }

        internal static EffectDefinition Need(string name, EffectKinds allowed)
        {
            List<EffectDefinition> candidates = Candidates(allowed);
            EffectDefinition effect = ByNumberOrName(candidates, name) ?? ByAlias(candidates, AliasFor(name));
            if (effect != null)
                return effect;

            EffectDefinition other = Names.Find<EffectDefinition>(name);
            if (other != null)
                throw new ArgumentException("effect " + other.name + " is not one of the game's own effects");
            throw new ArgumentException("no effect named '" + name + "'" + Names.Suggest(name, candidates.Select(e => e.name), "Effects", aliases.SelectMany(a => a.Spellings)));
        }

        internal static EffectAlias UsedAlias(string name)
        {
            List<EffectDefinition> candidates = Candidates(EffectKinds.Game);
            return ByNumberOrName(candidates, name) == null ? AliasFor(name) : null;
        }

        internal static string AliasNote(string name)
        {
            EffectAlias alias = UsedAlias(name);
            if (alias == null)
                return null;
            EffectDefinition target = ByAlias(Candidates(EffectKinds.Game), alias);
            return name + " = " + (target == null ? "missing" : target.name);
        }

        internal static EffectDefinition Resolve(EffectAlias alias) => ByAlias(Candidates(EffectKinds.Game), alias);

        internal static string ShortName(EffectDefinition effect)
        {
            EffectAlias alias = aliases.FirstOrDefault(a => a.Number == effect.Hash);
            return alias != null ? alias.Spellings[0] : effect.name;
        }

        static List<EffectDefinition> Candidates(EffectKinds allowed) =>
            HashedGeneralValue<EffectDefinition>.All.Where(e => e != null && (KindOf(e) & allowed) != 0).ToList();

        static EffectDefinition ByNumberOrName(List<EffectDefinition> candidates, string name)
        {
            string key = Names.Norm(name);
            if (key.Length == 0)
                return null;
            if (uint.TryParse(key, out uint hash))
            {
                EffectDefinition byNumber = candidates.FirstOrDefault(e => e.Hash == hash);
                if (byNumber != null)
                    return byNumber;
            }
            return candidates.FirstOrDefault(e => Names.Norm(e.name) == key);
        }

        static EffectAlias AliasFor(string name)
        {
            string key = Names.Norm(name);
            return key.Length == 0 ? null : aliases.FirstOrDefault(a => a.Spellings.Any(n => Names.Norm(n) == key));
        }

        static EffectDefinition ByAlias(List<EffectDefinition> candidates, EffectAlias alias)
        {
            if (alias == null)
                return null;
            return candidates.FirstOrDefault(e => e.Hash == alias.Number) ?? candidates.FirstOrDefault(e => Names.Norm(e.name) == Names.Norm(alias.GameName));
        }

        internal class EffectAlias
        {
            internal EffectAlias(uint number, string gameName, params string[] spellings)
            {
                Number = number;
                GameName = gameName;
                Spellings = spellings;
            }

            internal uint Number { get; }

            internal string GameName { get; }

            internal string[] Spellings { get; }
        }
    }
}
