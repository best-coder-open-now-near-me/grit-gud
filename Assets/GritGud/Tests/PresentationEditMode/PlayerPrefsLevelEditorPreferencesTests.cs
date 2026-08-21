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
                gridVisible = false,
                gridSpacing = 1.25f,
                gridElevation = 0.5f,
                camera = new LevelEditorCameraState
                {
                    target = new Vector3(4f, 2f, -8f),
                    yaw = 80f,
                    pitch = 45f,
                    distance = 12f,
                    view = LevelEditorCameraView.Top,
                    orthographicSize = 9f,
                    perspectiveYaw = 80f,
                    perspectivePitch = 45f,
                },
            };

            store.Save(expected);
            LevelEditorLocalPreferences actual = store.Load();

            Assert.That(actual.snapEnabled, Is.False);
            Assert.That(actual.gridVisible, Is.False);
            Assert.That(actual.gridSpacing, Is.EqualTo(1.25f));
            Assert.That(actual.gridElevation, Is.EqualTo(0.5f));
            Assert.That(actual.camera.target, Is.EqualTo(expected.camera.target));
            Assert.That(actual.camera.yaw, Is.EqualTo(expected.camera.yaw));
            Assert.That(actual.camera.pitch, Is.EqualTo(expected.camera.pitch));
            Assert.That(actual.camera.distance, Is.EqualTo(expected.camera.distance));
            Assert.That(actual.camera.view, Is.EqualTo(LevelEditorCameraView.Top));
            Assert.That(actual.camera.orthographicSize, Is.EqualTo(9f));
        }

        [Test]
        public void OlderPreferencesReceiveSafeGridDefaults()
        {
            PlayerPrefs.SetString(
                PlayerPrefsLevelEditorPreferences.StorageKey,
                "{\"snapEnabled\":false}");

            LevelEditorLocalPreferences actual =
                new PlayerPrefsLevelEditorPreferences().Load();

            Assert.That(actual.gridVisible, Is.True);
            Assert.That(actual.gridSpacing, Is.EqualTo(2.5f));
            Assert.That(actual.gridElevation, Is.Zero);
        }
    }
}
