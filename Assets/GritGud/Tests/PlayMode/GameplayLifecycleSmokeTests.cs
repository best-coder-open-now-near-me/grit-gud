using System.Collections;
using GritGud.Application.Levels;
using GritGud.Presentation.Bootstrap;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GritGud.PlayMode.Tests
{
    public sealed class GameplayLifecycleSmokeTests
    {
        private GameObject ownedApplication;
        private GameObject ownedCamera;
        private GameBootstrap bootstrap;

        [UnityTest]
        public IEnumerator DefaultGameplaySurvivesSustainedFrameUpdates()
        {
            EnsureBootstrap();

            bootstrap.ReturnToMenu();
            bootstrap.PlayMainLevel();
            yield return null;
            GameplayController gameplay =
                bootstrap.GetComponent<GameplayController>();
            Assert.That(gameplay, Is.Not.Null);
            Assert.That(gameplay.IsRunning, Is.True);
            GameplayAdvancementHud advancement =
                bootstrap.GetComponent<GameplayAdvancementHud>();
            GameplayInputController input =
                bootstrap.GetComponent<GameplayInputController>();
            Assert.That(advancement, Is.Not.Null);
            Assert.That(input, Is.Not.Null);
            Assert.That(advancement.IsOpen, Is.False);
            advancement.Open(gameplay.PartyControl.Snapshot.SelectedActorId);
            yield return null;
            Assert.That(advancement.IsOpen, Is.True);
            Assert.That(input.CameraOnly, Is.True);
            advancement.Close();
            Assert.That(advancement.IsOpen, Is.False);
            Assert.That(input.CameraOnly, Is.False);

            const int sustainedFrameCount = 180;
            for (int frame = 0; frame < sustainedFrameCount; frame++)
            {
                yield return null;
            }

            Assert.That(bootstrap.CurrentMode,
                Is.EqualTo(ApplicationMode.Gameplay));
            Assert.That(gameplay.IsRunning, Is.True);
            Assert.That(gameplay.Session, Is.Not.Null);

            bootstrap.ReturnToMenu();
            yield return null;

            Assert.That(bootstrap.CurrentMode,
                Is.EqualTo(ApplicationMode.Menu));
            Assert.That(gameplay.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator EveryPlayableCommittedLevelBootsAndTearsDown()
        {
            EnsureBootstrap();
            bootstrap.ReturnToMenu();
            int playableLevelCount = 0;

            foreach (CommittedLevelEntry entry in bootstrap.CommittedLevels)
            {
                if (!entry.CanPlay)
                {
                    continue;
                }

                playableLevelCount++;
                bootstrap.PlayCommittedLevel(entry.ResourceKey);
                yield return WaitForMode(ApplicationMode.Gameplay);

                GameplayController gameplay =
                    bootstrap.GetComponent<GameplayController>();
                Assert.That(gameplay, Is.Not.Null, entry.ResourceKey);
                Assert.That(gameplay.IsRunning, Is.True, entry.ResourceKey);
                Assert.That(gameplay.Session, Is.Not.Null, entry.ResourceKey);

                bootstrap.ReturnToMenu();
                yield return null;
                Assert.That(
                    bootstrap.CurrentMode,
                    Is.EqualTo(ApplicationMode.Menu),
                    entry.ResourceKey);
                Assert.That(gameplay.IsRunning, Is.False, entry.ResourceKey);
            }

            Assert.That(playableLevelCount, Is.GreaterThan(0));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            bootstrap?.ReturnToMenu();
            if (ownedCamera != null)
            {
                Object.Destroy(ownedCamera);
            }

            if (ownedApplication != null)
            {
                Object.Destroy(ownedApplication);
            }

            yield return null;
            bootstrap = null;
            ownedCamera = null;
            ownedApplication = null;
        }

        private void EnsureBootstrap()
        {
            bootstrap = GameBootstrap.Instance;
            if (bootstrap == null)
            {
                ownedApplication = new GameObject(
                    "Gameplay Lifecycle Smoke Test");
                bootstrap = ownedApplication.AddComponent<GameBootstrap>();
            }

            if (Camera.main == null)
            {
                ownedCamera = new GameObject("Main Camera");
                ownedCamera.tag = "MainCamera";
                ownedCamera.AddComponent<Camera>();
            }
        }

        private IEnumerator WaitForMode(ApplicationMode expectedMode)
        {
            const int maximumFrames = 30;
            for (int frame = 0;
                frame < maximumFrames && bootstrap.CurrentMode != expectedMode;
                frame++)
            {
                yield return null;
            }

            Assert.That(bootstrap.CurrentMode, Is.EqualTo(expectedMode));
        }
    }
}
