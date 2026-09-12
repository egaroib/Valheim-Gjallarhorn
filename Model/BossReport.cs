using UnityEngine;

namespace Gjallarhorn.Model
{
    /// <summary>
    /// A boss defeat as seen by the client that owned the boss. Separate wire format from
    /// <see cref="DeathReport"/> because it carries a different set of facts.
    /// </summary>
    public class BossReport
    {
        public const int WireVersion = 1;

        public string BossName = string.Empty;

        /// <summary>Player who dealt the final blow. Empty when it cannot be determined.</summary>
        public string KillerName = string.Empty;

        public Vector3 Position = Vector3.zero;

        public ZPackage Write()
        {
            var pkg = new ZPackage();
            pkg.Write(WireVersion);
            pkg.Write(BossName ?? string.Empty);
            pkg.Write(KillerName ?? string.Empty);
            pkg.Write(Position);
            return pkg;
        }

        public static BossReport Read(ZPackage pkg)
        {
            try
            {
                if (pkg.ReadInt() != WireVersion)
                {
                    return null;
                }

                return new BossReport
                {
                    BossName = pkg.ReadString(),
                    KillerName = pkg.ReadString(),
                    Position = pkg.ReadVector3(),
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
