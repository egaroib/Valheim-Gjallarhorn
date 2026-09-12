using BepInEx.Configuration;

namespace Gjallarhorn.Config
{
    /// <summary>
    /// Every tunable, bound once at Awake.
    ///
    /// Anything that changes what other players see is admin-only, so the server dictates it
    /// and a client cannot give itself a different kill feed. The only non-admin entry is
    /// verbose logging, which is a local debugging preference.
    /// </summary>
    public static class GjallarhornConfig
    {
        // ── Channels ─────────────────────────────────────────────────────────────
        public static ConfigEntry<bool> AnnounceDeaths;
        public static ConfigEntry<bool> AnnounceBossDefeats;
        public static ConfigEntry<bool> UseTopLeftFeed;
        public static ConfigEntry<bool> UseChatLine;
        public static ConfigEntry<bool> BossUsesCenterBanner;
        public static ConfigEntry<string> ChatSenderName;

        // ── Content ──────────────────────────────────────────────────────────────
        public static ConfigEntry<bool> IncludeBiome;
        public static ConfigEntry<bool> IncludeStarRank;
        public static ConfigEntry<bool> LogToConsole;

        // ── Detection ────────────────────────────────────────────────────────────
        public static ConfigEntry<float> PollInterval;
        public static ConfigEntry<float> ExactReportGrace;
        public static ConfigEntry<float> KillerSearchRadius;
        public static ConfigEntry<float> SwarmRadius;
        public static ConfigEntry<int> SwarmThreshold;

        // ── Message pools ────────────────────────────────────────────────────────
        public static ConfigEntry<string> MonsterMessages;
        public static ConfigEntry<string> BossKilledPlayerMessages;
        public static ConfigEntry<string> PvpMessages;
        public static ConfigEntry<string> OverwhelmedMessages;
        public static ConfigEntry<string> FallMessages;
        public static ConfigEntry<string> DrowningMessages;
        public static ConfigEntry<string> FireMessages;
        public static ConfigEntry<string> FreezingMessages;
        public static ConfigEntry<string> PoisonMessages;
        public static ConfigEntry<string> TreeMessages;
        public static ConfigEntry<string> EnvironmentMessages;
        public static ConfigEntry<string> SelfMessages;
        public static ConfigEntry<string> GenericMessages;
        public static ConfigEntry<string> BossDefeatMessages;

        public static ConfigEntry<bool> VerboseLogging;

        public static void Bind(ConfigFile config)
        {
            var admin = new ConfigurationManagerAttributes { IsAdminOnly = true };

            VerboseLogging = config.Bind(
                "0 - Diagnostics", "VerboseLogging", false,
                "Log every poll tick, proximity scan candidate, and broadcast. Very noisy; " +
                "for debugging only. Local preference, not synced.");

            // ── Channels ──
            AnnounceDeaths = config.Bind(
                "1 - Channels", "AnnounceDeaths", true,
                new ConfigDescription("Announce player deaths server-wide.", null, admin));

            AnnounceBossDefeats = config.Bind(
                "1 - Channels", "AnnounceBossDefeats", true,
                new ConfigDescription("Announce when a boss is defeated.", null, admin));

            UseTopLeftFeed = config.Bind(
                "1 - Channels", "UseTopLeftFeed", true,
                new ConfigDescription(
                    "Show death announcements in the top-left message feed, where pickup and " +
                    "skill-up messages appear.", null, admin));

            UseChatLine = config.Bind(
                "1 - Channels", "UseChatLine", true,
                new ConfigDescription(
                    "Also post death announcements as a chat line, so they stay in the chat " +
                    "history. Players standing near the death, or with the map open, will also " +
                    "see it as floating text at the spot -- that is vanilla chat behaviour, not " +
                    "something this mod can suppress.", null, admin));

            BossUsesCenterBanner = config.Bind(
                "1 - Channels", "BossUsesCenterBanner", true,
                new ConfigDescription(
                    "Boss defeats get the large centre-screen banner instead of the top-left " +
                    "feed. Player deaths never use the centre banner.", null, admin));

            ChatSenderName = config.Bind(
                "1 - Channels", "ChatSenderName", "Gjallarhorn",
                new ConfigDescription(
                    "Name shown as the speaker on chat lines.", null, admin));

            // ── Content ──
            IncludeBiome = config.Bind(
                "2 - Content", "IncludeBiome", true,
                new ConfigDescription(
                    "Resolve the {biome} token. When false, {biome} renders as 'the wilds'.",
                    null, admin));

            IncludeStarRank = config.Bind(
                "2 - Content", "IncludeStarRank", true,
                new ConfigDescription(
                    "Prefix starred creatures in {killer}, e.g. '2-star Greydwarf'.", null, admin));

            LogToConsole = config.Bind(
                "2 - Content", "LogToConsole", true,
                new ConfigDescription(
                    "Write every announcement to the server console log.", null, admin));

            // ── Detection ──
            PollInterval = config.Bind(
                "3 - Detection", "PollInterval", 1.0f,
                new ConfigDescription(
                    "Seconds between server-side checks of each connected player's death flag. " +
                    "Only used for players who do not have Gjallarhorn installed.",
                    new AcceptableValueRange<float>(0.25f, 10f), admin));

            ExactReportGrace = config.Bind(
                "3 - Detection", "ExactReportGrace", 1.5f,
                new ConfigDescription(
                    "After the server notices a death, wait this long for the victim's client to " +
                    "send exact details before falling back to a guess. Players with Gjallarhorn " +
                    "installed get an accurate killer; raising this only delays the fallback.",
                    new AcceptableValueRange<float>(0f, 10f), admin));

            KillerSearchRadius = config.Bind(
                "3 - Detection", "KillerSearchRadius", 20f,
                new ConfigDescription(
                    "Radius searched for a likely killer when guessing. Guess only.",
                    new AcceptableValueRange<float>(2f, 64f), admin));

            SwarmRadius = config.Bind(
                "3 - Detection", "SwarmRadius", 6f,
                new ConfigDescription(
                    "Radius counted for the 'overwhelmed by a horde' message. Guess only.",
                    new AcceptableValueRange<float>(2f, 32f), admin));

            SwarmThreshold = config.Bind(
                "3 - Detection", "SwarmThreshold", 4,
                new ConfigDescription(
                    "Hostiles within SwarmRadius needed before a death reads as a horde death.",
                    new AcceptableValueRange<int>(2, 20), admin));

            // ── Message pools. Semicolon-separated; one is picked at random. ──
            MonsterMessages = Pool(config, "MonsterMessages",
                "{victim} was slain by {killer} in {biome}" +
                ";{victim} was torn apart by {killer}" +
                ";{killer} claimed the soul of {victim} in {biome}" +
                ";{victim} fell before the wrath of {killer}",
                "Killed by a creature. Tokens: {victim} {killer} {biome}", admin);

            BossKilledPlayerMessages = Pool(config, "BossKilledPlayerMessages",
                "{victim} was annihilated by {killer}" +
                ";The legendary {killer} crushed {victim} into dust" +
                ";{victim} dared challenge {killer}, and was destroyed",
                "Killed by a boss. Tokens: {victim} {killer} {biome}", admin);

            PvpMessages = Pool(config, "PvpMessages",
                "{victim} was cut down by {killer}" +
                ";{killer} sent {victim} to the halls of Valhalla" +
                ";{victim} lost a duel with {killer} in {biome}",
                "Killed by another player. Tokens: {victim} {killer} {biome}", admin);

            OverwhelmedMessages = Pool(config, "OverwhelmedMessages",
                "{victim} was overwhelmed by a horde in {biome}" +
                ";{victim} made a last stand against overwhelming odds" +
                ";{victim} fell fighting, surrounded, in {biome}",
                "Killed while surrounded. Tokens: {victim} {biome}", admin);

            FallMessages = Pool(config, "FallMessages",
                "{victim} discovered that Valheim has gravity" +
                ";{victim} fell to their death in {biome}" +
                ";The ground rose up to meet {victim}",
                "Fall damage. Tokens: {victim} {biome}", admin);

            DrowningMessages = Pool(config, "DrowningMessages",
                "{victim} was claimed by the sea" +
                ";{victim} drowned in {biome}" +
                ";The waves closed over {victim}",
                "Drowning or lost at sea. Tokens: {victim} {biome}", admin);

            FireMessages = Pool(config, "FireMessages",
                "{victim} burned to ash in {biome}" +
                ";{victim} learned that fire is hot" +
                ";The flames took {victim}",
                "Burning, cinders, lava, incinerator. Tokens: {victim} {biome}", admin);

            FreezingMessages = Pool(config, "FreezingMessages",
                "{victim} froze to death in {biome}" +
                ";The cold took {victim}" +
                ";{victim} should have packed warmer clothes",
                "Freezing. Tokens: {victim} {biome}", admin);

            PoisonMessages = Pool(config, "PoisonMessages",
                "{victim} succumbed to poison in {biome}" +
                ";Venom finished what {killer} started" +
                ";{victim} died gasping, poisoned, in {biome}",
                "Poison. Tokens: {victim} {killer} {biome}", admin);

            TreeMessages = Pool(config, "TreeMessages",
                "{victim} was crushed by a falling tree" +
                ";The forest took its revenge on {victim}" +
                ";{victim} lost an argument with a tree in {biome}",
                "Killed by a felled tree. Tokens: {victim} {biome}", admin);

            EnvironmentMessages = Pool(config, "EnvironmentMessages",
                "{victim} died to their own surroundings in {biome}" +
                ";{victim} was undone by the terrain" +
                ";{biome} itself finished {victim}",
                "Carts, boats, structures, turrets, stalagtites, smoke, edge of world. " +
                "Tokens: {victim} {biome}", admin);

            SelfMessages = Pool(config, "SelfMessages",
                "{victim} died by their own hand" +
                ";{victim} made a poor decision in {biome}",
                "Self-inflicted. Tokens: {victim} {biome}", admin);

            GenericMessages = Pool(config, "GenericMessages",
                "{victim} has departed for the halls of Valhalla" +
                ";The Norns have cut the thread of {victim}'s life" +
                ";{victim} died in {biome}",
                "Fallback when the cause is unknown. Tokens: {victim} {biome}", admin);

            BossDefeatMessages = Pool(config, "BossDefeatMessages",
                "{boss} has been defeated!" +
                ";{killer} has slain {boss}!" +
                ";The mighty {boss} falls. Gjallarhorns will sing of this day.",
                "Boss defeated. {killer} renders as 'the Vikings' when the killer is unknown. " +
                "Tokens: {boss} {killer} {biome}", admin);
        }

        private static ConfigEntry<string> Pool(
            ConfigFile config, string key, string value, string help,
            ConfigurationManagerAttributes admin)
        {
            return config.Bind("4 - Messages", key, value,
                new ConfigDescription(help + " Separate alternatives with semicolons.", null, admin));
        }
    }
}
