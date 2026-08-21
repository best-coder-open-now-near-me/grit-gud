using System.IO;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class RuntimePrimitiveBuildSettingsTests
    {
        private const string LinkerConfigPath =
            "Assets/GritGud/Presentation/link.xml";

        private static readonly string[] RuntimeColliderTypes =
        {
            "UnityEngine.BoxCollider",
            "UnityEngine.CapsuleCollider",
            "UnityEngine.SphereCollider",
        };

        [Test]
        public void PrimitiveCollidersAddedByNativeCodeAreRetainedInPlayerBuilds()
        {
            Assert.That(File.Exists(LinkerConfigPath), Is.True);
            string linkerConfig = File.ReadAllText(LinkerConfigPath);
            Assert.That(
                linkerConfig,
                Does.Contain("UnityEngine.PhysicsModule"));
            foreach (string colliderType in RuntimeColliderTypes)
            {
                Assert.That(
                    linkerConfig,
                    Does.Contain($"fullname=\"{colliderType}\" preserve=\"all\""),
                    $"{colliderType} must survive managed stripping because " +
                    "GameObject.CreatePrimitive adds it through native code.");
            }
        }

        [Test]
        public void CanonicalReplayTypesAreRetainedInPlayerBuilds()
        {
            string linkerConfig = File.ReadAllText(LinkerConfigPath);
            Assert.That(
                linkerConfig,
                Does.Contain(
                    "fullname=\"GritGud.Application\" preserve=\"all\""));
            Assert.That(
                linkerConfig,
                Does.Contain(
                    "fullname=\"GritGud.Domain\" preserve=\"all\""));
        }
    }
}
