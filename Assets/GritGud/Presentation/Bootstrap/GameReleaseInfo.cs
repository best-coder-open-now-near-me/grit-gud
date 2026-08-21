using System;
using System.Globalization;

namespace GritGud.Presentation.Bootstrap
{
    /// <summary>
    /// Identifies the reviewed release represented by this branch. Keep the
    /// timestamp in UTC so every platform displays the same build identity.
    /// </summary>
    public static class GameReleaseInfo
    {
        public const string ReleasedAtUtc = "2026-08-15T06:09:00Z";

        public static string Format(string version)
        {
            DateTimeOffset releasedAt = DateTimeOffset.Parse(
                ReleasedAtUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            string displayVersion = string.IsNullOrWhiteSpace(version)
                ? "UNKNOWN"
                : version.Trim();
            return $"VERSION {displayVersion}  •  RELEASED {releasedAt:yyyy-MM-dd HH:mm} UTC";
        }
    }
}
