using System;
using System.Collections.Generic;
using Gjallarhorn.Model;
using UnityEngine;

namespace Gjallarhorn.Chronicle
{
    /// <summary>One announced death, kept in memory for the session.</summary>
    public sealed class ChronicleEntry
    {
        public DateTime TimestampUtc;
        public string VictimName;
        public string KillerName;
        public DeathCause Cause;
        public string Biome;
        public Vector3 Position;
        public bool WasExact;
        public string Message;
    }

    /// <summary>
    /// A rolling in-memory record of what has been announced, for console inspection and as a
    /// hook for anything that wants to read the feed back out.
    ///
    /// Deliberately not persisted: a kill feed is not save data, and writing to disk on every
    /// death is a cost a server should not pay for a cosmetic feature.
    /// </summary>
    public static class ChronicleRegistry
    {
        private const int MaxEntries = 200;

        private static readonly List<ChronicleEntry> Entries = new List<ChronicleEntry>();
        private static readonly object Gate = new object();

        public static void Record(DeathReport report, string message)
        {
            var entry = new ChronicleEntry
            {
                TimestampUtc = DateTime.UtcNow,
                VictimName = report.VictimName,
                KillerName = report.KillerName,
                Cause = report.Cause,
                Biome = report.Biome,
                Position = report.Position,
                WasExact = report.Source == ReportSource.Exact,
                Message = message,
            };

            lock (Gate)
            {
                Entries.Add(entry);
                if (Entries.Count > MaxEntries)
                {
                    Entries.RemoveRange(0, Entries.Count - MaxEntries);
                }
            }
        }

        /// <summary>Most recent first.</summary>
        public static List<ChronicleEntry> Recent(int count = 25)
        {
            lock (Gate)
            {
                var take = Math.Min(count, Entries.Count);
                var result = new List<ChronicleEntry>(take);

                for (var i = Entries.Count - 1; i >= Entries.Count - take; i--)
                {
                    result.Add(Entries[i]);
                }

                return result;
            }
        }
    }
}
