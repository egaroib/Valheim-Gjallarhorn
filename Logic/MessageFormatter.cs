using System;
using Gjallarhorn.Config;
using Gjallarhorn.Model;

namespace Gjallarhorn.Logic
{
    /// <summary>
    /// Picks a template from the pool matching the death's cause and fills in the tokens.
    /// </summary>
    public static class MessageFormatter
    {
        private static readonly Random Rng = new Random();
        private static readonly char[] Separator = { ';' };

        public static string Death(DeathReport report)
        {
            var template = Pick(PoolFor(report.Cause));

            var killer = string.IsNullOrWhiteSpace(report.KillerName)
                ? "an unseen hand"
                : report.KillerName;

            if (GjallarhornConfig.IncludeStarRank.Value && report.KillerLevel > 1)
            {
                killer = NameTable.WithStars(killer, report.KillerLevel);
            }

            var biome = GjallarhornConfig.IncludeBiome.Value
                ? NameTable.Biome(report.Biome)
                : "the wilds";

            return template
                .Replace("{victim}", Blank(report.VictimName, "A Viking"))
                .Replace("{killer}", killer)
                .Replace("{biome}", biome);
        }

        public static string BossDefeat(string bossName, string killerName, string biome)
        {
            var template = Pick(GjallarhornConfig.BossDefeatMessages.Value);

            return template
                .Replace("{boss}", Blank(bossName, "a great beast"))
                .Replace("{killer}", Blank(killerName, "the Vikings"))
                .Replace("{biome}", GjallarhornConfig.IncludeBiome.Value
                    ? NameTable.Biome(biome)
                    : "the wilds");
        }

        private static string PoolFor(DeathCause cause)
        {
            switch (cause)
            {
                case DeathCause.Monster:     return GjallarhornConfig.MonsterMessages.Value;
                case DeathCause.Boss:        return GjallarhornConfig.BossKilledPlayerMessages.Value;
                case DeathCause.Pvp:         return GjallarhornConfig.PvpMessages.Value;
                case DeathCause.Overwhelmed: return GjallarhornConfig.OverwhelmedMessages.Value;
                case DeathCause.Fall:        return GjallarhornConfig.FallMessages.Value;
                case DeathCause.Drowning:    return GjallarhornConfig.DrowningMessages.Value;
                case DeathCause.Fire:        return GjallarhornConfig.FireMessages.Value;
                case DeathCause.Freezing:    return GjallarhornConfig.FreezingMessages.Value;
                case DeathCause.Poison:      return GjallarhornConfig.PoisonMessages.Value;
                case DeathCause.Tree:        return GjallarhornConfig.TreeMessages.Value;
                case DeathCause.Environment: return GjallarhornConfig.EnvironmentMessages.Value;
                case DeathCause.Self:        return GjallarhornConfig.SelfMessages.Value;
                default:                     return GjallarhornConfig.GenericMessages.Value;
            }
        }

        /// <summary>
        /// Picks one template at random. An emptied-out pool falls back to a plain line
        /// rather than throwing, so a bad config edit cannot take the feed down.
        /// </summary>
        private static string Pick(string pool)
        {
            if (string.IsNullOrWhiteSpace(pool))
            {
                return "{victim} has died in {biome}";
            }

            var options = pool.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
            if (options.Length == 0)
            {
                return "{victim} has died in {biome}";
            }

            lock (Rng)
            {
                return options[Rng.Next(options.Length)].Trim();
            }
        }

        private static string Blank(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
