using System;
using System.Collections.Generic;
using Gjallarhorn.Logic;
using Gjallarhorn.Model;

namespace Gjallarhorn.Server
{
    /// <summary>
    /// Server-side death detection for players who do not have Gjallarhorn installed.
    ///
    /// A dedicated server never instantiates a Player component for a remote client, so
    /// nothing in Player or Character is reachable here. What the server does have is every
    /// connected peer's character ZDO, replicated from that peer.
    ///
    /// It watches ZDOVars.s_dead, which Player.OnDeath sets explicitly, rather than health.
    /// Health is the wrong signal twice over: Valheim strips the health field from the ZDO
    /// entirely while a character is at full health, so a player killed outright never shows
    /// a positive reading to compare against, and a poll can straddle the moment health
    /// passes through zero.
    /// </summary>
    public static class DeathWatcher
    {
        /// <summary>Last known death flag per character, so only the transition fires.</summary>
        private static readonly Dictionary<ZDOID, bool> LastSeenDead = new Dictionary<ZDOID, bool>();

        private static readonly List<ZDOID> Seen = new List<ZDOID>();
        private static readonly List<ZDOID> Gone = new List<ZDOID>();
        private static readonly List<ZNetPeer> EmptyPeers = new List<ZNetPeer>();

        public static void Poll()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null)
            {
                return;
            }

            Seen.Clear();

            // Pending guesses have to be released even with nobody connected: the last player
            // on the server can die and disconnect inside the grace window.
            var peers = ZNet.instance.GetPeers() ?? EmptyPeers;

            foreach (var peer in peers)
            {
                if (peer == null || !peer.IsReady() || peer.m_characterID.IsNone())
                {
                    continue;
                }

                var zdo = ZDOMan.instance.GetZDO(peer.m_characterID);
                if (zdo == null)
                {
                    continue;
                }

                var id = peer.m_characterID;
                Seen.Add(id);

                var dead = zdo.GetBool(ZDOVars.s_dead, false);

                // First sighting establishes a baseline without announcing. Without this, a
                // server restart while someone lies dead would replay their death.
                if (!LastSeenDead.TryGetValue(id, out var wasDead))
                {
                    LastSeenDead[id] = dead;
                    continue;
                }

                LastSeenDead[id] = dead;

                if (wasDead || !dead)
                {
                    continue;
                }

                var name = zdo.GetString(ZDOVars.s_playerName, string.Empty);
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = peer.m_playerName;
                }

                var position = zdo.GetPosition();

                // Read the victim's details now. The character ZDO is destroyed when they
                // respawn, roughly ten seconds later, and the announcement may still be
                // inside its grace window at that point.
                AnnouncementService.SubmitObserved(new DeathReport
                {
                    VictimZdoId = id,
                    VictimName = name,
                    Position = position,
                    Biome = AnnouncementService.ResolveBiome(position),
                    Source = ReportSource.Guessed,
                });
            }

            ForgetDeparted();
            AnnouncementService.Tick();
        }

        /// <summary>
        /// Drops characters that are no longer among the connected peers -- respawns and
        /// disconnects both. Each life gets a new ZDOID, so without this the map grows by one
        /// entry per death for the lifetime of the process.
        /// </summary>
        private static void ForgetDeparted()
        {
            if (LastSeenDead.Count == 0)
            {
                return;
            }

            Gone.Clear();

            foreach (var known in LastSeenDead.Keys)
            {
                if (!Seen.Contains(known))
                {
                    Gone.Add(known);
                }
            }

            foreach (var id in Gone)
            {
                LastSeenDead.Remove(id);
            }
        }
    }
}
