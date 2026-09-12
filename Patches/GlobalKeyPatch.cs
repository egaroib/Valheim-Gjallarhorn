using System;
using HarmonyLib;
using Gjallarhorn.Config;
using Gjallarhorn.Logic;
using Gjallarhorn.Server;
using UnityEngine;

namespace Gjallarhorn.Patches
{
    /// <summary>
    /// Server side. The vanilla-client fallback for boss defeats.
    ///
    /// ZoneSystem.SetGlobalKey routes to the server, and RPC_SetGlobalKey is registered only
    /// when IsServer() -- so this target exists exactly where we need it and nowhere else.
    /// Every boss prefab carries an m_defeatSetGlobalKey ("defeated_bonemass" and friends),
    /// set from Character.OnDeath.
    ///
    /// Prefix, because vanilla only adds a key it does not already hold: reading
    /// m_globalKeys after the fact cannot distinguish a fresh kill from a repeat.
    ///
    /// Caveat, and it is inherent: a key is added once per world. The second Bonemass kill
    /// changes nothing here. <see cref="BossDeathPatch"/> covers repeat kills for players who
    /// have Gjallarhorn installed; for a fully vanilla server, first kills are all that can be seen.
    /// </summary>
    [HarmonyPatch(typeof(ZoneSystem), "RPC_SetGlobalKey")]
    public static class GlobalKeyPatch
    {
        private static void Prefix(ZoneSystem __instance, string name)
        {
            try
            {
                if (!GjallarhornConfig.AnnounceBossDefeats.Value || !NameTable.IsBossDefeatKey(name))
                {
                    return;
                }

                if (__instance.m_globalKeys.Contains(name))
                {
                    return;
                }

                // No position and no killer are available from a global key. The dedupe window
                // in AnnouncementService means an exact report from a modded client, which
                // arrives first, suppresses this one.
                AnnouncementService.BossDefeated(
                    NameTable.BossFromDefeatKey(name), string.Empty, Vector3.zero);
            }
            catch (Exception e)
            {
                GjallarhornPlugin.LogError($"Failed to handle global key '{name}': {e}");
            }
        }
    }
}
