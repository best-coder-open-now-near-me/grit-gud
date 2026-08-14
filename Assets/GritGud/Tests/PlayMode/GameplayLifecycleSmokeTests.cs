using System.Collections;
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

            bootstrap.ReturnToMenu();
            bootstrap.PlayMainLevel();
            yield return null;
            GameplayController gameplay =
                bootstrap.GetComponent<GameplayController>();
            Assert.That(gameplay, Is.Not.Null);
            Assert.That(gameplay.IsRunning, Is.True);

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
    }
}
