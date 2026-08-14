using NUnit.Framework;
using UnityEditor;

namespace GritGud.Presentation.Tests
{
    public sealed class WebPreviewBuildSettingsTests
    {
        [Test]
        public void CompressedPreviewsSupportStaticServersWithoutEncodingHeaders()
        {
            Assert.That(
                PlayerSettings.WebGL.compressionFormat,
                Is.EqualTo(WebGLCompressionFormat.Brotli));
            Assert.That(PlayerSettings.WebGL.decompressionFallback, Is.True);
        }
    }
}
