using System;
using HarmonyLib;
using Gjallarhorn.Logic;
using Gjallarhorn.Model;
using Gjallarhorn.Net;
using Gjallarhorn.Server;

namespace Gjallarhorn.Patches
{
    /// <summary>
    /// Client side. Reads the victim's own HitData at the moment of death and ships it to the
    /// server, which is the only way any of it can be known -- Character.m_lastHit is set in
    /// Character.Damage, executed by whoever owns the ZDO, which is the victim's machine.
    ///
    /// Prefix rather than postfix: OnDeath clears status effects and requests a respawn, and
    /// there is no reason to read state after that has run.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
    public static class PlayerDeathPatch
    {
        private static void Prefix(Player __instance)
        {
            try
            {
                if (__instance == null || __instance.m_nview == null || !__instance.m_nview.IsValid())
                {
                    return;
                }

                // Every client runs OnDeath for every player it can see, but only the owner
                // holds the real m_lastHit. Vanilla's own OnDeath bails on the same check.
                if (!__instance.m_nview.IsOwner())
                {
                    return;
                }

                var report = Build(__instance);
                if (report == null)
                {
                    return;
                }

                // Hosting locally means we are the server; go straight to the service rather
                // than round-tripping a packet to ourselves.
                if (ZNet.instance != null && ZNet.instance.IsServer())
                {
                    AnnouncementService.SubmitExact(report);
                }
                else
                {
                    GjallarhornRpc.SendDeath(report);
                }
            }
            catch (Exception e)
            {
                // Never let this throw: it would abort Player.OnDeath and leave the player in
                // a broken half-dead state.
                GjallarhornPlugin.LogError($"Failed to report own death: {e}");
            }
        }

        private static DeathReport Build(Player player)
        {
            var position = player.transform.position;

            var report = new DeathReport
            {
                VictimZdoId = player.GetZDOID(),
                VictimName = player.GetPlayerName(),
                Position = position,
                Biome = AnnouncementService.ResolveBiome(position),
                Source = ReportSource.Exact,
            };

            var hit = player.m_lastHit;
            if (hit == null)
            {
                report.Cause = DeathCause.Generic;
                return report;
            }

            var attacker = hit.GetAttacker();
            if (attacker != null)
            {
                report.KillerLevel = attacker.GetLevel();

                // Character.m_name on a player prefab is a placeholder, not the chosen name;
                // the real one lives in the ZDO and GetPlayerName reads it.
                report.KillerName = attacker is Player killer
                    ? killer.GetPlayerName()
                    : NameTable.Creature(attacker.gameObject.name);
            }

            report.Cause = Classify(hit, attacker);
            return report;
        }

        /// <summary>
        /// Collapses HitData.HitType into a message pool. The enum was read off the current
        /// decompile; an unrecognised value from a future update lands on Generic rather than
        /// being mislabelled.
        /// </summary>
        private static DeathCause Classify(HitData hit, Character attacker)
        {
            switch (hit.m_hitType)
            {
                case HitData.HitType.PlayerHit:
                    return DeathCause.Pvp;

                case HitData.HitType.EnemyHit:
                    return attacker != null && attacker.IsBoss()
                        ? DeathCause.Boss
                        : DeathCause.Monster;

                case HitData.HitType.Fall:
                    return DeathCause.Fall;

                case HitData.HitType.Drowning:
                case HitData.HitType.Water:
                case HitData.HitType.AshlandsOcean:
                    return DeathCause.Drowning;

                case HitData.HitType.Burning:
                case HitData.HitType.CinderFire:
                case HitData.HitType.AshlandsLava:
                case HitData.HitType.Incinerator:
                    return DeathCause.Fire;

                case HitData.HitType.Freezing:
                    return DeathCause.Freezing;

                case HitData.HitType.Poisoned:
                    return DeathCause.Poison;

                case HitData.HitType.Tree:
                    return DeathCause.Tree;

                case HitData.HitType.Self:
                    return DeathCause.Self;

                case HitData.HitType.Smoke:
                case HitData.HitType.EdgeOfWorld:
                case HitData.HitType.Impact:
                case HitData.HitType.Cart:
                case HitData.HitType.Structural:
                case HitData.HitType.Turret:
                case HitData.HitType.Boat:
                case HitData.HitType.Stalagtite:
                case HitData.HitType.Catapult:
                case HitData.HitType.DrawBridge:
                    return DeathCause.Environment;

                default:
                    return DeathCause.Generic;
            }
        }
    }
}
