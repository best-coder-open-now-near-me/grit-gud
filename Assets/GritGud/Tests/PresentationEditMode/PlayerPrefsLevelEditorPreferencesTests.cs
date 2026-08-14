using GritGud.Presentation.LevelEditing.Core;
using GritGud.Presentation.LevelEditing.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class PlayerPrefsLevelEditorPreferencesTests
    {
        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsLevelEditorPreferences.StorageKey);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsLevelEditorPreferences.StorageKey);
        }

        [Test]
        public void PreferencesRoundTripWithoutEnteringTheLevelDocument()
        {
            var store = new PlayerPrefsLevelEditorPreferences();
            var expected = new LevelEditorLocalPreferences
            {
                snapEnabled = false,
                camera = new LevelEditorCameraState
                {
                    target = new Vector3(4f, 2f, -8f),
                    yaw = 80f,
                    pitch = 45f,
                    distance = 12f,
                },
            };

            store.Save(expected);
            LevelEditorLocalPreferences actual = store.Load();

            Assert.That(actual.snapEnabled, Is.False);
            Assert.That(actual.camera.target, Is.EqualTo(expected.camera.target));
            Assert.That(actual.camera.yaw, Is.EqualTo(expected.camera.yaw));
            Assert.That(actual.camera.pitch, Is.EqualTo(expected.camera.pitch));
            Assert.That(actual.camera.distance, Is.EqualTo(expected.camera.distance));
        }
    }
}
