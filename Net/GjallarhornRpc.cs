using System.Collections;
using Jotunn.Entities;
using Jotunn.Managers;
using Gjallarhorn.Model;
using Gjallarhorn.Server;
using UnityEngine;

namespace Gjallarhorn.Net
{
    /// <summary>
    /// The optional client -> server channel.
    ///
    /// Everything worth knowing about a death -- who swung, what kind of damage, whether it
    /// was another player -- lives in Character.m_lastHit, which only ever exists on the
    /// victim's own machine. A dedicated server has no Player components at all, so it cannot
    /// read it. These carry that up to the server, where announcements are actually built.
    ///
    /// Clients without Gjallarhorn simply never send, and the server falls back to its own
    /// detection. That is why the mod is not version-enforced.
    /// </summary>
    public static class GjallarhornRpc
    {
        private const string DeathRpcName = "GjallarhornDeath";
        private const string BossRpcName = "GjallarhornBoss";

        private static CustomRPC _deathRpc;
        private static CustomRPC _bossRpc;

        public static void Register()
        {
            _deathRpc = NetworkManager.Instance.AddRPC(DeathRpcName, OnDeathReceived, NoOp);
            _bossRpc = NetworkManager.Instance.AddRPC(BossRpcName, OnBossReceived, NoOp);

            GjallarhornPlugin.LogInfo($"Registered RPCs '{DeathRpcName}' and '{BossRpcName}'.");
        }

        /// <summary>Client side: hand an exact death report to the server.</summary>
        public static void SendDeath(DeathReport report)
        {
            if (_deathRpc == null || ZRoutedRpc.instance == null)
            {
                return;
            }

            _deathRpc.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), report.Write());
            GjallarhornPlugin.LogVerbose($"Sent exact death report for '{report.VictimName}'.");
        }

        /// <summary>Client side: tell the server a boss went down, and who did it.</summary>
        public static void SendBoss(string bossName, string killerName, Vector3 position)
        {
            if (_bossRpc == null || ZRoutedRpc.instance == null)
            {
                return;
            }

            var report = new BossReport
            {
                BossName = bossName,
                KillerName = killerName,
                Position = position,
            };

            _bossRpc.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), report.Write());
            GjallarhornPlugin.LogVerbose($"Sent boss defeat report for '{bossName}'.");
        }

        private static IEnumerator OnDeathReceived(long sender, ZPackage package)
        {
            // Jotunn's Initiate() sends a one-byte handshake. Anything that short is not a
            // report, and reading it would throw inside the coroutine.
            if (package != null && package.Size() > 1)
            {
                var report = DeathReport.Read(package);
                if (report == null)
                {
                    GjallarhornPlugin.LogWarning(
                        $"Discarded an unreadable death report from peer {sender}. That client " +
                        "is most likely running a different version of Gjallarhorn.");
                }
                else
                {
                    AnnouncementService.SubmitExact(report);
                }
            }

            yield break;
        }

        private static IEnumerator OnBossReceived(long sender, ZPackage package)
        {
            if (package != null && package.Size() > 1)
            {
                var report = BossReport.Read(package);
                if (report == null)
                {
                    GjallarhornPlugin.LogWarning(
                        $"Discarded an unreadable boss report from peer {sender}.");
                }
                else
                {
                    AnnouncementService.BossDefeated(
                        report.BossName, report.KillerName, report.Position);
                }
            }

            yield break;
        }

        /// <summary>
        /// Nothing travels server -> client over these RPCs; announcements ride on Valheim's
        /// own ShowMessage and ChatMessage so vanilla clients see them too. The handler still
        /// has to exist -- Jotunn invokes it unconditionally on the client branch.
        /// </summary>
        private static IEnumerator NoOp(long sender, ZPackage package)
        {
            yield break;
        }
    }
}
