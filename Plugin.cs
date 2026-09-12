using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Utils;
using Gjallarhorn.Config;
using Gjallarhorn.Net;
using Gjallarhorn.Server;

namespace Gjallarhorn
{
    /// <summary>
    /// Server-side Viking death announcements and boss-defeat chronicles.
    ///
    /// The mod is hybrid by design, which is why it is NotEnforced rather than
    /// EveryoneMustHaveMod:
    ///
    ///   - On the server it is required. It does all detection, formatting and broadcasting,
    ///     and it talks to clients over RPCs vanilla already registers, so a fully vanilla
    ///     server population still sees the feed.
    ///   - On a client it is optional and additive. Installed, it reports the victim's real
    ///     HitData -- exact killer, star rank, PvP, and every non-combat cause -- none of
    ///     which a dedicated server can see. Absent, that player's deaths fall back to a
    ///     proximity guess.
    ///
    /// Enforcing it would lock out vanilla players to gain nothing, since the two paths
    /// converge on the same announcement.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
    public class GjallarhornPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.ragemedia.gjallarhorn";
        public const string PluginName = "Gjallarhorn";
        public const string PluginVersion = "0.1.0";

        internal static GjallarhornPlugin Instance;

        private readonly Harmony _harmony = new Harmony(PluginGuid);

        /// <summary>
        /// The ZNet this process last evaluated. ZNet is created per world and destroyed on
        /// returning to the menu, so comparing identity -- rather than latching a bool --
        /// re-evaluates the role when a player hosts a world, quits, then joins someone else's.
        /// </summary>
        private ZNet _knownNet;

        private void Awake()
        {
            Instance = this;

            GjallarhornConfig.Bind(Config);
            GjallarhornRpc.Register();

            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            VerifyPatches();
        }

        private void OnDestroy()
        {
            CancelInvoke(nameof(PollDeaths));
            _harmony?.UnpatchSelf();
        }

        /// <summary>
        /// ZNet does not exist at Awake, so whether this process is the server cannot be known
        /// until a world is up. Checked here rather than assumed, and the poll is started
        /// exactly once, only on the server.
        /// </summary>
        private void Update()
        {
            var net = ZNet.instance;
            if (ReferenceEquals(net, _knownNet))
            {
                return;
            }

            _knownNet = net;
            CancelInvoke(nameof(PollDeaths));

            if (net == null)
            {
                return;
            }

            if (!net.IsServer())
            {
                LogInfo("Running as a client. Deaths here are reported to the server with exact " +
                        "detail; announcements come back from the server.");
                return;
            }

            var interval = Math.Max(0.25f, GjallarhornConfig.PollInterval.Value);
            InvokeRepeating(nameof(PollDeaths), 2f, interval);

            LogInfo($"Running as the server. Watching for deaths every {interval:0.00}s.");
        }

        private void PollDeaths()
        {
            try
            {
                DeathWatcher.Poll();
            }
            catch (Exception e)
            {
                LogError($"Death poll failed: {e}");
            }
        }

        /// <summary>
        /// Reports what Harmony actually bound, and errors loudly on any [HarmonyPatch] class
        /// whose target could not be resolved.
        ///
        /// This exists because a patch aimed at a method that no longer exists compiles
        /// cleanly against publicized assemblies and then silently does nothing at runtime.
        /// Without this check, a broken build and a working build log identically.
        /// </summary>
        private void VerifyPatches()
        {
            var bound = _harmony.GetPatchedMethods().ToList();
            LogInfo($"Harmony bound {bound.Count} target(s):");
            foreach (var m in bound)
            {
                LogInfo($"  bound  {m.DeclaringType?.FullName}.{m.Name}");
            }

            var unresolved = 0;
            var patchClasses = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), true).Any());

            foreach (var type in patchClasses)
            {
                var attrs = type.GetCustomAttributes(typeof(HarmonyPatch), true)
                    .Cast<HarmonyPatch>()
                    .Select(a => a.info)
                    .ToList();

                HarmonyMethod merged;
                try
                {
                    merged = HarmonyMethod.Merge(attrs);
                }
                catch (Exception e)
                {
                    LogError($"  UNRESOLVED  {type.Name}: could not merge patch attributes: {e.Message}");
                    unresolved++;
                    continue;
                }

                if (merged?.declaringType == null || string.IsNullOrEmpty(merged.methodName))
                {
                    continue;
                }

                var target = AccessTools.Method(
                    merged.declaringType, merged.methodName, merged.argumentTypes);

                if (target == null)
                {
                    unresolved++;
                    LogError($"  UNRESOLVED  {type.Name} -> " +
                             $"{merged.declaringType.Name}.{merged.methodName} does not exist. " +
                             "This patch will never run. The game version likely changed; " +
                             "re-check the method against the decompiled source.");
                }
            }

            if (unresolved > 0)
            {
                LogError($"{PluginName}: {unresolved} patch target(s) failed to resolve. " +
                         "This mod is loaded but not fully functional.");
            }
            else
            {
                LogInfo($"{PluginName} v{PluginVersion} ready.");
            }
        }

        internal static void LogInfo(string msg) => Instance.Logger.LogInfo($"[{PluginName}] {msg}");
        internal static void LogWarning(string msg) => Instance.Logger.LogWarning($"[{PluginName}] {msg}");
        internal static void LogError(string msg) => Instance.Logger.LogError($"[{PluginName}] {msg}");

        internal static void LogVerbose(string msg)
        {
            if (GjallarhornConfig.VerboseLogging != null && GjallarhornConfig.VerboseLogging.Value)
            {
                Instance.Logger.LogInfo($"[{PluginName}] {msg}");
            }
        }
    }
}
