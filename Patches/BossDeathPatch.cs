using System;
using HarmonyLib;
using Gjallarhorn.Config;
using Gjallarhorn.Logic;
using Gjallarhorn.Server;

namespace Gjallarhorn.Patches
{
    /// <summary>
    /// Client side. Catches a boss dying on the machine that owns it, which is the only place
    /// the kill is unambiguous.
    ///
    /// Character.OnDeath, not Player.OnDeath: Player is the only subclass that overrides it,
    /// so this target sees every creature and nothing else.
    ///
    /// This exists alongside <see cref="GlobalKeyPatch"/> because the global key only fires on
    /// a world's <em>first</em> kill of each boss. Repeat kills have no key change at all, and
    /// on a server that has already cleared the game that would be every kill.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
    public static class BossDeathPatch
    {
        private static void Prefix(Character __instance)
        {
            try
            {
                // Cheapest test first: this prefix runs on every creature death on every
                // client, so the common case must fall out on a single field read.
                if (__instance == null || !__instance.IsBoss())
                {
                    return;
                }

                if (!GjallarhornConfig.AnnounceBossDefeats.Value)
                {
                    return;
                }

                if (__instance.m_nview == null || !__instance.m_nview.IsValid() ||
                    !__instance.m_nview.IsOwner())
                {
                    return;
                }

                var bossName = NameTable.Creature(
                    string.IsNullOrWhiteSpace(__instance.m_name)
                        ? __instance.gameObject.name
                        : __instance.m_name);

                // The boss's own last hit names who actually finished it. Owning the ZDO
                // usually means being nearby, but it is not the same as landing the blow, so
                // the local player is only a fallback.
                var killer = string.Empty;
                if (__instance.m_lastHit?.GetAttacker() is Player attacker)
                {
                    killer = attacker.GetPlayerName();
                }
                else if (Player.m_localPlayer != null)
                {
                    killer = Player.m_localPlayer.GetPlayerName();
                }

                var position = __instance.transform.position;

                if (ZNet.instance != null && ZNet.instance.IsServer())
                {
                    AnnouncementService.BossDefeated(bossName, killer, position);
                }
                else
                {
                    // Reuse the death channel: a boss kill is rare enough that a dedicated RPC
                    // would not earn its keep. The victim ID is None, which marks it as a boss
                    // report on the far side.
                    Net.GjallarhornRpc.SendBoss(bossName, killer, position);
                }
            }
            catch (Exception e)
            {
                GjallarhornPlugin.LogError($"Failed to report a boss defeat: {e}");
            }
        }
    }
}
