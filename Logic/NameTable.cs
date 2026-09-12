using System;
using System.Collections.Generic;
using System.Text;

namespace Gjallarhorn.Logic
{
    /// <summary>
    /// Turns internal identifiers into something worth reading in a kill feed.
    ///
    /// This does not use Valheim's <c>Localization</c>: that class lives in an assembly the
    /// dedicated server has no reason to initialise, and its singleton is set up by the
    /// client-side startup path. A static table is deterministic and works headless, at the
    /// cost of being English-only.
    /// </summary>
    public static class NameTable
    {
        /// <summary>
        /// Prefab name -> display name, for creatures whose prefab name is not the name
        /// players know them by. Anything absent falls through to <see cref="Prettify"/>,
        /// which handles the majority (Troll, Boar, Greydwarf, Wraith...) correctly on its own.
        /// </summary>
        private static readonly Dictionary<string, string> Creatures =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Black Forest
            { "Greydwarf_Elite", "Greydwarf Brute" },
            { "Greydwarf_Shaman", "Greydwarf Shaman" },
            { "Skeleton_NoArcher", "Skeleton" },
            { "gd_king", "The Elder" },

            // Swamp
            { "Blob", "Blob" },
            { "BlobElite", "Oozer" },
            { "Draugr_Elite", "Draugr Elite" },
            { "Draugr_Ranged", "Draugr Archer" },
            { "Leech", "Leech" },
            { "Surtling", "Surtling" },
            { "Wraith", "Wraith" },

            // Mountain
            { "StoneGolem", "Stone Golem" },
            { "Hatchling", "Drake" },
            { "Ulv", "Ulv" },
            { "Fenring_Cultist", "Cultist" },

            // Plains
            { "Goblin", "Fuling" },
            { "GoblinArcher", "Fuling Archer" },
            { "GoblinBrute", "Fuling Berserker" },
            { "GoblinShaman", "Fuling Shaman" },
            { "GoblinKing", "Yagluth" },
            { "Deathsquito", "Deathsquito" },
            { "BlobTar", "Growth" },
            { "Lox", "Lox" },

            // Mistlands
            { "Seeker", "Seeker" },
            { "SeekerBrute", "Seeker Soldier" },
            { "SeekerBrood", "Seeker Brood" },
            { "SeekerQueen", "The Queen" },
            { "Dverger", "Dvergr" },
            { "DvergerMage", "Dvergr Mage" },
            { "DvergerMageFire", "Dvergr Rogue" },
            { "DvergerMageIce", "Dvergr Mage" },
            { "DvergerMageSupport", "Dvergr Mage" },
            { "Gjall", "Gjall" },
            { "Tick", "Tick" },

            // Ashlands
            { "Charred_Melee", "Charred Warrior" },
            { "Charred_Archer", "Charred Marksman" },
            { "Charred_Mage", "Charred Twitcher" },
            { "Charred_Twitcher", "Charred Twitcher" },
            { "FallenValkyrie", "Fallen Valkyrie" },
            { "Morgen", "Morgen" },
            { "Volture", "Volture" },
            { "Asksvin", "Asksvin" },
            { "BonemawSerpent", "Bonemaw" },
            { "Fader", "Fader" },

            // Bosses / misc
            { "Eikthyr", "Eikthyr" },
            { "Bonemass", "Bonemass" },
            { "Dragon", "Moder" },
            { "Serpent", "Sea Serpent" },
        };

        /// <summary>
        /// Global key set on boss defeat -> the boss's display name. Keys come from each
        /// boss prefab's <c>m_defeatSetGlobalKey</c>; the five in <c>GlobalKeys</c> plus the
        /// two later bosses, which are plain strings and never made it into the enum.
        /// </summary>
        private static readonly Dictionary<string, string> BossKeys =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "defeated_eikthyr", "Eikthyr" },
            { "defeated_gdking", "The Elder" },
            { "defeated_bonemass", "Bonemass" },
            { "defeated_dragon", "Moder" },
            { "defeated_goblinking", "Yagluth" },
            { "defeated_queen", "The Queen" },
            { "defeated_fader", "Fader" },
        };

        /// <summary>
        /// True when this global key means "a boss just died". Used rather than a whitelist
        /// so a boss added in a future update still announces, with a prettified name.
        /// </summary>
        public static bool IsBossDefeatKey(string globalKey)
        {
            return !string.IsNullOrEmpty(globalKey)
                && globalKey.StartsWith("defeated_", StringComparison.OrdinalIgnoreCase);
        }

        public static string BossFromDefeatKey(string globalKey)
        {
            if (string.IsNullOrEmpty(globalKey))
            {
                return "a great beast";
            }

            if (BossKeys.TryGetValue(globalKey.Trim(), out var known))
            {
                return known;
            }

            // "defeated_somethingnew" -> "Somethingnew"
            var suffix = globalKey.Trim();
            var underscore = suffix.IndexOf('_');
            if (underscore >= 0 && underscore + 1 < suffix.Length)
            {
                suffix = suffix.Substring(underscore + 1);
            }

            return Prettify(suffix);
        }

        /// <summary>
        /// Resolves a creature to its display name. Accepts a prefab name ("GoblinBrute"),
        /// an instantiated name ("GoblinBrute(Clone)"), or a localisation token
        /// ("$enemy_goblinbrute") -- all three turn up depending on where the caller got it.
        /// </summary>
        public static string Creature(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "something unseen";
            }

            var name = raw.Trim();

            if (name.EndsWith("(Clone)", StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - "(Clone)".Length).Trim();
            }

            if (name.StartsWith("$", StringComparison.Ordinal))
            {
                name = name.Substring(1);
                if (name.StartsWith("enemy_", StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring("enemy_".Length);
                }
            }

            return Creatures.TryGetValue(name, out var display) ? display : Prettify(name);
        }

        /// <summary>Adds a star prefix: level 1 is unstarred, level 3 reads "2-star".</summary>
        public static string WithStars(string creature, int level)
        {
            return level >= 2 ? $"{level - 1}-star {creature}" : creature;
        }

        private static readonly Dictionary<string, string> Biomes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "None", "the wilds" },
            { "Meadows", "the Meadows" },
            { "BlackForest", "the Black Forest" },
            { "Swamp", "the Swamp" },
            { "Mountain", "the Mountains" },
            { "Plains", "the Plains" },
            { "Ocean", "the Ocean" },
            { "Mistlands", "the Mistlands" },
            { "AshLands", "the Ashlands" },
            { "DeepNorth", "the Deep North" },
        };

        public static string Biome(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "the wilds";
            }

            return Biomes.TryGetValue(raw.Trim(), out var display) ? display : "the wilds";
        }

        /// <summary>
        /// "GoblinBrute" -> "Goblin Brute", "Skeleton_Poison" -> "Skeleton Poison".
        /// Deliberately conservative: it only inserts spaces, so an unmapped prefab reads
        /// awkwardly at worst rather than wrongly.
        /// </summary>
        private static string Prettify(string name)
        {
            name = name.Replace('_', ' ').Trim();
            if (name.Length == 0)
            {
                return "something unseen";
            }

            var sb = new StringBuilder(name.Length + 8);
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]) && name[i - 1] != ' ')
                {
                    sb.Append(' ');
                }
                sb.Append(c);
            }

            var result = sb.ToString();
            return char.ToUpperInvariant(result[0]) + result.Substring(1);
        }
    }
}
