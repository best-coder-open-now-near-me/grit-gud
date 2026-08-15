using GritGud.Presentation.Bootstrap;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class GameReleaseInfoTests
    {
        [Test]
        public void FormatIncludesVersionAndUtcReleaseTimestamp()
        {
            Assert.That(
                GameReleaseInfo.Format("0.1.0"),
                Is.EqualTo("VERSION 0.1.0  •  RELEASED 2026-08-15 06:09 UTC"));
        }

        [Test]
        public void FormatUsesExplicitFallbackForMissingVersion()
        {
            Assert.That(GameReleaseInfo.Format(" "), Does.StartWith("VERSION UNKNOWN"));
        }
    }
}
