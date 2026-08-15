using System.Linq;
using GritGud.Domain.Levels;
using GritGud.Presentation.Levels.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class LevelDressingProjectorTests
    {
        [Test]
        public void ProjectsPortableDressingAndAuthoringZoneGizmos()
        {
            var parent = new GameObject("Dressing Test Parent");
            var projector = new LevelDressingProjector(
                parent.transform,
                LevelDressingCatalog.LoadDefault());
            try
            {
                projector.Replace(CreateDressing(), showZoneGizmos: true, playAudio: false);

                Transform root = parent.transform.Find("Level Dressing");
                Assert.That(root, Is.Not.Null);
                Assert.That(root.Find("Test Decal"), Is.Not.Null);
                Assert.That(root.Find("Test Dust"), Is.Not.Null);
                Assert.That(root.Find("Test Audio"), Is.Not.Null);
                Assert.That(root.GetComponentsInChildren<LineRenderer>(true), Has.Length.EqualTo(12));
                Assert.That(root.GetComponentsInChildren<AmbientAudioZoneController>(true),
                    Has.Length.EqualTo(1));

                projector.SetEditorPresentation(showZoneGizmos: false, playAudio: false);

                root = parent.transform.Find("Level Dressing");
                Assert.That(root.GetComponentsInChildren<LineRenderer>(true), Is.Empty);
            }
            finally
            {
                projector.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void AudioZoneGainUsesBoxInteriorAndAuthoredFadeDistance()
        {
            Vector3 center = Vector3.zero;
            Vector3 size = new Vector3(10f, 4f, 6f);

            Assert.That(
                AmbientAudioZoneController.CalculateGain(center, size, center, 5f),
                Is.EqualTo(1f));
            Assert.That(
                AmbientAudioZoneController.CalculateGain(
                    center,
                    size,
                    new Vector3(7.5f, 0f, 0f),
                    5f),
                Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(
                AmbientAudioZoneController.CalculateGain(
                    center,
                    size,
                    new Vector3(12f, 0f, 0f),
                    5f),
                Is.Zero);
        }

        [TestCase("industrial-hum")]
        [TestCase("wind")]
        [TestCase("ventilation")]
        public void ProceduralAmbientSoundsCreateLoopReadyClips(string soundId)
        {
            AudioClip clip = ProceduralAmbientAudioFactory.Create(soundId);
            try
            {
                Assert.That(clip.samples, Is.GreaterThan(0));
                Assert.That(clip.frequency, Is.EqualTo(22050));
                Assert.That(clip.channels, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        private static LevelDressingData CreateDressing()
        {
            var dressing = new LevelDressingData();
            dressing.decals.Add(new LevelDecalData
            {
                id = "test-decal",
                displayName = "Test Decal",
                styleId = "hazard",
                position = new Float3Data(1f, 0.02f, 2f),
                size = new Float3Data(2f, 2f, 1f),
                color = new FloatColorData(1f, 0.5f, 0f, 0.6f),
            });
            dressing.ambientVfx.Add(new LevelAmbientVfxData
            {
                id = "test-dust",
                displayName = "Test Dust",
                effectId = "dust-air",
                scale = new Float3Data(1f, 1f, 1f),
            });
            dressing.audioZones.Add(new LevelAudioZoneData
            {
                id = "test-audio",
                displayName = "Test Audio",
                soundId = "wind",
                size = new Float3Data(8f, 4f, 8f),
                volume = 0.2f,
                fadeDistance = 4f,
            });
            return dressing;
        }
    }
}
