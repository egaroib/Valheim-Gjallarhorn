using System;
using System.Collections.Generic;
using Gjallarhorn.Config;
using Gjallarhorn.Logic;
using Gjallarhorn.Model;
using UnityEngine;

namespace Gjallarhorn.Server
{
    /// <summary>
    /// Best-effort killer attribution for players who do not have Gjallarhorn installed.
    ///
    /// This is a guess and is documented as one. The server has no HitData, so all it can do
    /// is look at what was standing near the corpse. It gets the common case right -- you
    /// died to the thing that was hitting you, and that thing is still there -- and gets
    /// ranged kills, deaths on a fleeing mob, and anything non-combat wrong.
    /// </summary>
    public static class KillerGuesser
    {
        private readonly struct Candidate
        {
            public readonly string Name;
            public readonly int Level;
            public readonly float Distance;
            public readonly bool IsBoss;

            public Candidate(string name, int level, float distance, bool isBoss)
            {
                Name = name;
                Level = level;
                Distance = distance;
                IsBoss = isBoss;
            }
        }

        /// <summary>
        /// Fills in Cause, KillerName and KillerLevel on a report the watcher has already
        /// populated with victim, position and biome.
        /// </summary>
        public static void Fill(DeathReport report)
        {
            var candidates = Scan(report.Position, GjallarhornConfig.KillerSearchRadius.Value);

            if (candidates.Count == 0)
            {
                report.Cause = DeathCause.Generic;
                report.KillerName = string.Empty;
                report.KillerLevel = 0;
                return;
            }

            var swarmRadius = GjallarhornConfig.SwarmRadius.Value;
            var swarm = 0;
            foreach (var c in candidates)
            {
                if (c.Distance <= swarmRadius)
                {
                    swarm++;
                }
            }

            if (swarm >= GjallarhornConfig.SwarmThreshold.Value)
            {
                report.Cause = DeathCause.Overwhelmed;
                report.KillerName = "a horde";
                report.KillerLevel = 0;
                return;
            }

            var closest = candidates[0];
            foreach (var c in candidates)
            {
                if (c.Distance < closest.Distance)
                {
                    closest = c;
                }
            }

            report.Cause = closest.IsBoss ? DeathCause.Boss : DeathCause.Monster;
            report.KillerName = closest.Name;
            report.KillerLevel = closest.Level;
        }

        /// <summary>
        /// Collects hostile creature ZDOs near a point.
        ///
        /// ZoneSystem.GetZone returns Vector2s and ZDOMan.FindSectorObjects takes a
        /// SimulationDistance -- both changed shape in a game update, and calling the old
        /// forms throws MissingMethodException at runtime while still compiling cleanly
        /// against a stale copy of assembly_valheim. Verified against the current decompile.
        /// </summary>
        private static List<Candidate> Scan(Vector3 position, float radius)
        {
            var found = new List<Candidate>();

            if (ZDOMan.instance == null || ZNetScene.instance == null || ZoneSystem.instance == null)
            {
                return found;
            }

            try
            {
                var zone = ZoneSystem.GetZone(position);
                var nearby = new List<ZDO>();

                // Near 1, far 0: one ring of zones around the death is ample for a 20m radius,
                // and keeps the scan off the whole loaded world.
                ZDOMan.instance.FindSectorObjects(zone, new SimulationDistance(1, 0), nearby);

                GjallarhornPlugin.LogVerbose(
                    $"Killer scan at {position}: {nearby.Count} ZDO(s) in the surrounding zones.");

                foreach (var zdo in nearby)
                {
                    if (zdo == null)
                    {
                        continue;
                    }

                    var distance = Vector3.Distance(zdo.GetPosition(), position);
                    if (distance > radius)
                    {
                        continue;
                    }

                    var prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
                    if (prefab == null)
                    {
                        continue;
                    }

                    var character = prefab.GetComponent<Character>();
                    if (character == null || character.IsPlayer())
                    {
                        // Players are handled by the exact path; a corpse next to another
                        // player is not evidence of PvP.
                        continue;
                    }

                    if (prefab.GetComponent<BaseAI>() == null)
                    {
                        // No AI means it cannot have attacked anyone.
                        continue;
                    }

                    if (zdo.GetBool(ZDOVars.s_tamed, false))
                    {
                        continue;
                    }

                    // Valheim omits the health field entirely while a character is at full
                    // health, so "no field" means unhurt, not dead. Only an explicit zero
                    // rules a candidate out.
                    if (zdo.GetFloat(ZDOVars.s_health, out var health) && health <= 0f)
                    {
                        continue;
                    }

                    var level = zdo.GetInt(ZDOVars.s_level, 1);
                    // Prefer the prefab name: it is the stable identifier NameTable is keyed
                    // on. Character.m_name is a localisation token whose spelling does not
                    // always track the prefab (gd_king vs $enemy_gdking).
                    var name = NameTable.Creature(
                        string.IsNullOrWhiteSpace(prefab.name) ? character.m_name : prefab.name);

                    GjallarhornPlugin.LogVerbose(
                        $"  candidate '{name}' level={level} dist={distance:0.0} boss={character.IsBoss()}");

                    found.Add(new Candidate(name, level, distance, character.IsBoss()));
                }
            }
            catch (Exception e)
            {
                GjallarhornPlugin.LogError($"Killer scan failed: {e}");
            }

            return found;
        }
    }
}
