using System.Collections;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Application.Levels;
using GritGud.Domain.Gameplay;
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

        [UnityTest]
        public IEnumerator PublishedTopplingFixtureCommitsAndRestoresExactLivePose()
        {
            EnsureBootstrap();
            bootstrap.ReturnToMenu();
            bootstrap.PlayMainLevel();
            yield return WaitForMode(ApplicationMode.Gameplay);

            GameplayController gameplay =
                bootstrap.GetComponent<GameplayController>();
            GameplayDisplacementController displacement =
                bootstrap.GetComponent<GameplayDisplacementController>();
            GameplayDestructibleController destructibles =
                bootstrap.GetComponent<GameplayDestructibleController>();
            const string propId = "barrel-yard-01";
            DestructiblePropSnapshot before =
                destructibles.Session.GetProp(propId);
            Transform prop = gameplay.WorldRegistry
                .GetLevelEntity(propId).transform;
            Vector3 liveDestination = new Vector3(0f, 0f, -8.75f);

            bool resolved = displacement.TryDisplaceSubject(
                gameplay.PartyControl.Snapshot.SelectedActorId,
                "close-quarters.push",
                propId,
                liveDestination,
                out DisplacementRecord record,
                out DisplacementResolutionFailure failure);
            yield return null;

            Assert.That(resolved, Is.True, failure.ToString());
            Assert.That(record.AppliedResults,
                Is.EqualTo(DisplacementResultPolicies.Topple));
            Assert.That(record.ResultingPropState.Posture,
                Is.EqualTo(DestructiblePropPosture.Toppled));
            Assert.That(destructibles.Session.GetProp(propId).Posture,
                Is.EqualTo(DestructiblePropPosture.Toppled));
            Vector3 expectedPosition = new Vector3(
                record.ResultingPosition.X,
                record.ResultingPosition.Y,
                record.ResultingPosition.Z);
            Quaternion expectedRotation = Quaternion.Euler(
                record.ResultingPropState.Pose.PitchDegrees,
                record.ResultingPropState.Pose.YawDegrees,
                record.ResultingPropState.Pose.RollDegrees);
            Assert.That(prop.position, Is.EqualTo(expectedPosition));
            Assert.That(Quaternion.Angle(prop.rotation, expectedRotation),
                Is.LessThan(0.01f));

            destructibles.PresentReplay(
                new List<DestructiblePropSnapshot> { before });
            Physics.SyncTransforms();
            Assert.That(prop.position, Is.Not.EqualTo(expectedPosition));

            destructibles.RestoreAuthoritativePresentation();
            Physics.SyncTransforms();
            Assert.That(prop.position, Is.EqualTo(expectedPosition));
            Assert.That(Quaternion.Angle(prop.rotation, expectedRotation),
                Is.LessThan(0.01f));

            bootstrap.ReturnToMenu();
            yield return null;
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
