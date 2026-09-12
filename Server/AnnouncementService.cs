using System;
using System.Collections.Generic;
using Gjallarhorn.Chronicle;
using Gjallarhorn.Config;
using Gjallarhorn.Logic;
using Gjallarhorn.Model;
using Gjallarhorn.Net;
using UnityEngine;

namespace Gjallarhorn.Server
{
    /// <summary>
    /// The single place a death turns into a broadcast.
    ///
    /// Two detectors feed this, and they race by design: the victim's client sends exact
    /// details the instant it dies, while the server's own poll notices the death flag up to
    /// a second later. Everything is keyed on the victim's ZDOID, which is unique per life --
    /// Valheim destroys the player object on death and instantiates a fresh one on respawn --
    /// so it is a perfect dedupe key with no expiry needed for correctness.
    /// </summary>
    public static class AnnouncementService
    {
        private sealed class Pending
        {
            public DeathReport Report;
            public DateTime GuessDueAt;
        }

        private static readonly object Gate = new object();

        /// <summary>Victims already broadcast, with the time, so the set can be pruned.</summary>
        private static readonly Dictionary<ZDOID, DateTime> Announced =
            new Dictionary<ZDOID, DateTime>();

        /// <summary>Deaths the poll has spotted, waiting out the grace window for exact details.</summary>
        private static readonly Dictionary<ZDOID, Pending> Waiting =
            new Dictionary<ZDOID, Pending>();

        /// <summary>Recent boss announcements, to dedupe the client report against the global key.</summary>
        private static readonly Dictionary<string, DateTime> RecentBosses =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        private static readonly TimeSpan AnnouncedRetention = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan BossDedupeWindow = TimeSpan.FromSeconds(15);

        /// <summary>
        /// An exact report from the victim's client. Always wins: it cancels any pending
        /// guess for the same victim and goes out immediately.
        /// </summary>
        public static void SubmitExact(DeathReport report)
        {
            if (report == null || !GjallarhornConfig.AnnounceDeaths.Value)
            {
                return;
            }

            lock (Gate)
            {
                if (Announced.ContainsKey(report.VictimZdoId))
                {
                    GjallarhornPlugin.LogVerbose(
                        $"Exact report for '{report.VictimName}' arrived after the guess went " +
                        "out. Raise ExactReportGrace if this keeps happening.");
                    return;
                }

                Waiting.Remove(report.VictimZdoId);
                Announced[report.VictimZdoId] = DateTime.UtcNow;
            }

            Dispatch(report);
        }

        /// <summary>
        /// The server poll noticed a death. Held for the grace window so the victim's client,
        /// if it has Gjallarhorn, can supply the real killer first.
        /// </summary>
        public static void SubmitObserved(DeathReport report)
        {
            if (report == null || !GjallarhornConfig.AnnounceDeaths.Value)
            {
                return;
            }

            lock (Gate)
            {
                if (Announced.ContainsKey(report.VictimZdoId) ||
                    Waiting.ContainsKey(report.VictimZdoId))
                {
                    return;
                }

                Waiting[report.VictimZdoId] = new Pending
                {
                    Report = report,
                    GuessDueAt = DateTime.UtcNow.AddSeconds(
                        Math.Max(0f, GjallarhornConfig.ExactReportGrace.Value)),
                };
            }

            GjallarhornPlugin.LogVerbose(
                $"Observed death of '{report.VictimName}'; waiting " +
                $"{GjallarhornConfig.ExactReportGrace.Value:0.0}s for exact details.");
        }

        /// <summary>
        /// Releases any pending guesses whose grace window has expired. Called from the same
        /// poll that feeds <see cref="SubmitObserved"/>.
        /// </summary>
        public static void Tick()
        {
            List<DeathReport> due = null;
            var now = DateTime.UtcNow;

            lock (Gate)
            {
                if (Waiting.Count > 0)
                {
                    List<ZDOID> released = null;

                    foreach (var entry in Waiting)
                    {
                        if (entry.Value.GuessDueAt > now)
                        {
                            continue;
                        }

                        (released ?? (released = new List<ZDOID>())).Add(entry.Key);
                        (due ?? (due = new List<DeathReport>())).Add(entry.Value.Report);
                    }

                    if (released != null)
                    {
                        foreach (var id in released)
                        {
                            Waiting.Remove(id);
                            Announced[id] = now;
                        }
                    }
                }

                Prune(now);
            }

            if (due == null)
            {
                return;
            }

            foreach (var report in due)
            {
                // Only guess at the killer now, not when the death was spotted: the scan wants
                // the corpse's surroundings, and waiting a moment does not make it worse.
                KillerGuesser.Fill(report);
                Dispatch(report);
            }
        }

        /// <summary>
        /// A boss went down.
        ///
        /// <paramref name="killerName"/> is empty, and <paramref name="position"/> null, when
        /// this came from the global-key fallback -- that path knows a boss died and nothing
        /// else. Passing Vector3.zero instead of null would have the announcement claim the
        /// biome at the world origin, which is worse than saying nothing.
        /// </summary>
        public static void BossDefeated(string bossName, string killerName, Vector3? position)
        {
            if (!GjallarhornConfig.AnnounceBossDefeats.Value || string.IsNullOrWhiteSpace(bossName))
            {
                return;
            }

            lock (Gate)
            {
                var now = DateTime.UtcNow;

                if (RecentBosses.TryGetValue(bossName, out var last) &&
                    now - last < BossDedupeWindow)
                {
                    GjallarhornPlugin.LogVerbose($"Boss '{bossName}' already announced; skipping.");
                    return;
                }

                RecentBosses[bossName] = now;
            }

            var biome = position.HasValue ? ResolveBiome(position.Value) : string.Empty;
            var message = MessageFormatter.BossDefeat(bossName, killerName, biome);

            if (GjallarhornConfig.LogToConsole.Value)
            {
                GjallarhornPlugin.LogInfo($"[boss] {message}");
            }

            Broadcast.Hud(message, GjallarhornConfig.BossUsesCenterBanner.Value);

            if (GjallarhornConfig.UseChatLine.Value)
            {
                // Chat carries a world position for its floating text. Without a real one,
                // fall back to the listener's own position by sending the origin -- vanilla
                // only uses it for proximity, and the chat-window line is unaffected.
                Broadcast.Chat(message, position ?? Vector3.zero);
            }
        }

        private static void Dispatch(DeathReport report)
        {
            var message = MessageFormatter.Death(report);

            if (GjallarhornConfig.LogToConsole.Value)
            {
                GjallarhornPlugin.LogInfo(
                    $"[{(report.Source == ReportSource.Exact ? "exact" : "guess")}] {message} " +
                    $"(cause={report.Cause}, pos={report.Position})");
            }

            ChronicleRegistry.Record(report, message);

            // Player deaths never take the centre banner -- that is reserved for boss defeats,
            // so the two are distinguishable at a glance.
            if (GjallarhornConfig.UseTopLeftFeed.Value)
            {
                Broadcast.Hud(message, center: false);
            }

            if (GjallarhornConfig.UseChatLine.Value)
            {
                Broadcast.Chat(message, report.Position);
            }
        }

        public static string ResolveBiome(Vector3 position)
        {
            try
            {
                if (WorldGenerator.instance != null)
                {
                    return WorldGenerator.instance.GetBiome(position.x, position.z).ToString();
                }
            }
            catch (Exception e)
            {
                GjallarhornPlugin.LogVerbose($"Biome lookup failed at {position}: {e.Message}");
            }

            return string.Empty;
        }

        /// <summary>
        /// Drops old entries. Purely a memory concern: every life gets a fresh ZDOID, so
        /// without this the map grows by one entry per death for the life of the process.
        /// </summary>
        private static void Prune(DateTime now)
        {
            if (Announced.Count > 0)
            {
                List<ZDOID> stale = null;
                foreach (var entry in Announced)
                {
                    if (now - entry.Value > AnnouncedRetention)
                    {
                        (stale ?? (stale = new List<ZDOID>())).Add(entry.Key);
                    }
                }

                if (stale != null)
                {
                    foreach (var id in stale)
                    {
                        Announced.Remove(id);
                    }
                }
            }

            if (RecentBosses.Count > 0)
            {
                List<string> stale = null;
                foreach (var entry in RecentBosses)
                {
                    if (now - entry.Value > BossDedupeWindow)
                    {
                        (stale ?? (stale = new List<string>())).Add(entry.Key);
                    }
                }

                if (stale != null)
                {
                    foreach (var name in stale)
                    {
                        RecentBosses.Remove(name);
                    }
                }
            }
        }
    }
}
