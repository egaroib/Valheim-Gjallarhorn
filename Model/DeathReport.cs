using UnityEngine;

namespace Gjallarhorn.Model
{
    /// <summary>
    /// How a player died, collapsed from Valheim's 24-value <see cref="HitData.HitType"/>
    /// into the categories that deserve their own message pool.
    /// </summary>
    public enum DeathCause
    {
        Generic,
        Monster,
        Boss,
        Pvp,
        Overwhelmed,
        Fall,
        Drowning,
        Fire,
        Freezing,
        Poison,
        Tree,
        Environment,
        Self,
    }

    /// <summary>
    /// Where the facts in a report came from. Drives dedupe: an <see cref="Exact"/> report
    /// always wins over a <see cref="Guessed"/> one for the same victim.
    /// </summary>
    public enum ReportSource
    {
        /// <summary>Server inferred it from replicated ZDO state. Killer is a proximity guess.</summary>
        Guessed,

        /// <summary>Victim's client sent its real HitData. Killer and cause are facts.</summary>
        Exact,
    }

    /// <summary>
    /// One player death, in the form the server broadcasts it.
    ///
    /// Serialised into a ZPackage for the client -> server hop, so field order here is the
    /// wire format. Adding a field means bumping <see cref="WireVersion"/> and handling the
    /// older shape in <see cref="Read"/>, because a server and a client can be on different
    /// mod versions -- this mod is deliberately not version-enforced.
    /// </summary>
    public class DeathReport
    {
        public const int WireVersion = 1;

        /// <summary>ZDOID of the character that died. The dedupe key: each life gets a fresh one.</summary>
        public ZDOID VictimZdoId = ZDOID.None;

        public string VictimName = string.Empty;

        /// <summary>Display name of what killed them, already resolved. Empty when unknown.</summary>
        public string KillerName = string.Empty;

        /// <summary>Creature level: 1 is no stars, 2 is one star. 0 when not applicable.</summary>
        public int KillerLevel;

        public DeathCause Cause = DeathCause.Generic;

        public Vector3 Position = Vector3.zero;

        /// <summary>Biome name at the death position, already resolved to English.</summary>
        public string Biome = string.Empty;

        public ReportSource Source = ReportSource.Guessed;

        public ZPackage Write()
        {
            var pkg = new ZPackage();
            pkg.Write(WireVersion);
            pkg.Write(VictimZdoId);
            pkg.Write(VictimName ?? string.Empty);
            pkg.Write(KillerName ?? string.Empty);
            pkg.Write(KillerLevel);
            pkg.Write((int)Cause);
            pkg.Write(Position);
            pkg.Write(Biome ?? string.Empty);
            return pkg;
        }

        /// <summary>
        /// Reads a report off the wire. Returns null on anything malformed or from a wire
        /// version this build does not understand, rather than throwing into Jotunn's
        /// coroutine and killing the RPC.
        /// </summary>
        public static DeathReport Read(ZPackage pkg)
        {
            try
            {
                var version = pkg.ReadInt();
                if (version != WireVersion)
                {
                    return null;
                }

                return new DeathReport
                {
                    VictimZdoId = pkg.ReadZDOID(),
                    VictimName = pkg.ReadString(),
                    KillerName = pkg.ReadString(),
                    KillerLevel = pkg.ReadInt(),
                    Cause = (DeathCause)pkg.ReadInt(),
                    Position = pkg.ReadVector3(),
                    Biome = pkg.ReadString(),
                    Source = ReportSource.Exact,
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
