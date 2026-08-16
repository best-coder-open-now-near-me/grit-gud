using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
            yield return new WaitForFixedUpdate();

            GameplayController gameplay =
                bootstrap.GetComponent<GameplayController>();
            GameplayDisplacementController displacement =
                bootstrap.GetComponent<GameplayDisplacementController>();
            GameplayDestructibleController destructibles =
                bootstrap.GetComponent<GameplayDestructibleController>();
            const string propId = "crate-pin-demo";
            DestructiblePropSnapshot before =
                destructibles.Session.GetProp(propId);
            Transform prop = gameplay.WorldRegistry
                .GetLevelEntity(propId).transform;
            string actingActorId =
                gameplay.PartyControl.Snapshot.SelectedActorId;
            DisplacementDestinationEvaluation destination =
                displacement.Session.EvaluateIntentDestination(
                    actingActorId,
                    "close-quarters.push",
                    propId);
            Assert.That(destination.IsEligible, Is.True,
                destination.Failure.ToString());
            Vector3 liveDestination = new Vector3(
                destination.Destination.X,
                destination.Destination.Y,
                destination.Destination.Z);

            bool resolved = displacement.TryDisplaceSubject(
                actingActorId,
                "close-quarters.push",
                propId,
                liveDestination,
                out DisplacementRecord record,
                out DisplacementResolutionFailure failure);
            yield return null;

            Assert.That(resolved, Is.True, failure.ToString());
            Assert.That(record.AppliedResults.HasFlag(
                DisplacementResultPolicies.Topple), Is.True);
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

        [UnityTest]
        public IEnumerator PublishedPinFixtureEntersDirectionalPushOffAndCommitsChoice()
        {
            EnsureBootstrap();
            bootstrap.ReturnToMenu();
            bootstrap.PlayMainLevel();
            yield return WaitForMode(ApplicationMode.Gameplay);
            yield return new WaitForFixedUpdate();

            GameplayController gameplay =
                bootstrap.GetComponent<GameplayController>();
            GameplayDisplacementController displacement =
                bootstrap.GetComponent<GameplayDisplacementController>();
            const string actorId = "oren-vale";
            const string propId = "crate-pin-demo";
            Transform actor = gameplay.WorldRegistry.GetActor(actorId).Transform;
            Transform prop = gameplay.WorldRegistry.GetLevelEntity(propId)
                .transform;
            GameplayPosition actorPosition = gameplay.Session.GetActor(
                actorId).Pose.Position;
            var pinDestination = new Vector3(
                actorPosition.X + 0.5f,
                actorPosition.Y,
                actorPosition.Z + 0.3f);

            Assert.That(displacement.TryDisplaceSubject(
                "player",
                "close-quarters.push",
                propId,
                pinDestination,
                out DisplacementRecord pinRecord,
                out DisplacementResolutionFailure pinFailure),
                Is.True,
                pinFailure.ToString());
            yield return null;
            Assert.That(pinRecord.PinTransition, Is.Not.Null);
            Assert.That(pinRecord.PinTransition.EstablishesPin, Is.True);
            Assert.That(pinRecord.PinTransition.ActorId, Is.EqualTo(actorId));
            Assert.That(gameplay.Session.GetActor(actorId).IsPinned, Is.True);

            displacement.SetActor(actorId);
            Assert.That(displacement.TryToggleTargeting(
                "close-quarters.push-off"), Is.True);
            Assert.That(displacement.IsChoosingPushOffDirection, Is.True);
            Assert.That(displacement.LockedSubjectId, Is.EqualTo(propId));
            Assert.That(displacement.CurrentWarningHint.Text,
                Does.Contain("AIM PUSH-OFF DIRECTION"));
            Assert.That(displacement.CancelTargeting(), Is.True);

            GameplayPosition propOrigin = new GameplayPosition(
                prop.position.x,
                prop.position.y,
                prop.position.z);
            DisplacementDestinationEvaluation choice =
                displacement.Session.EvaluateDirectionalPushOffDestination(
                    actorId,
                    "close-quarters.push-off",
                    propId,
                    new GameplayPosition(
                        propOrigin.X - 10f,
                        propOrigin.Y,
                        propOrigin.Z));
            Assert.That(choice.IsEligible, Is.True, choice.Failure.ToString());
            Assert.That(choice.Destination.X, Is.LessThan(propOrigin.X));
            Assert.That(displacement.TryDisplaceSubject(
                actorId,
                "close-quarters.push-off",
                propId,
                new Vector3(
                    choice.Destination.X,
                    choice.Destination.Y,
                    choice.Destination.Z),
                out DisplacementRecord releaseRecord,
                out DisplacementResolutionFailure releaseFailure),
                Is.True,
                releaseFailure.ToString());
            yield return null;

            Assert.That(releaseRecord.PinTransition.ReleasesPin, Is.True);
            Assert.That(gameplay.Session.GetActor(actorId).IsPinned, Is.False);
            Assert.That(prop.position.x,
                Is.EqualTo(choice.Destination.X).Within(0.001f));
            Assert.That(prop.position.z,
                Is.EqualTo(choice.Destination.Z).Within(0.001f));

            bootstrap.ReturnToMenu();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PublishedCrateFracturesAndReplayRestoresExactChunkMask()
        {
            EnsureBootstrap();
            bootstrap.ReturnToMenu();
            bootstrap.PlayMainLevel();
            yield return WaitForMode(ApplicationMode.Gameplay);

            GameplayController gameplay =
                bootstrap.GetComponent<GameplayController>();
            GameplayDestructibleController destructibles =
                bootstrap.GetComponent<GameplayDestructibleController>();
            const string propId = "crate-yard-01";
            DestructiblePropSnapshot before =
                destructibles.Session.GetProp(propId);
            GameObject prop = gameplay.WorldRegistry
                .GetLevelEntity(propId).gameObject;
            DestructiblePropPresenter presenter =
                prop.GetComponent<DestructiblePropPresenter>();
            Renderer originalRenderer = prop.GetComponentInChildren<Renderer>();
            Collider originalCollider = prop.GetComponentInChildren<Collider>();

            Assert.That(before.FractureChunkCount, Is.EqualTo(12));
            Assert.That(before.DetachedFractureChunks, Is.Zero);
            Assert.That(originalRenderer, Is.Not.Null);
            Assert.That(originalCollider, Is.Not.Null);
            Assert.That(
                destructibles.TryApplyDamage(propId, 4f, out var record),
                Is.True);
            yield return null;

            DestructiblePropSnapshot damaged =
                destructibles.Session.GetProp(propId);
            DestructibleFractureChunk[] chunks = prop
                .GetComponentsInChildren<DestructibleFractureChunk>(true);
            int detachedCount = DestructibleFracture.CountDetachedChunks(
                damaged.DetachedFractureChunks);
            Assert.That(damaged.State,
                Is.EqualTo(DestructiblePropState.Damaged));
            Assert.That(damaged.DetachedFractureChunks,
                Is.EqualTo(record.Resulting.DetachedFractureChunks));
            Assert.That(detachedCount, Is.EqualTo(5));
            Assert.That(chunks.Length, Is.EqualTo(12));
            Assert.That(originalRenderer.enabled, Is.False);
            Assert.That(originalCollider.enabled, Is.False);
            Assert.That(
                chunks.Count(chunk => chunk.gameObject.activeInHierarchy),
                Is.EqualTo(12 - detachedCount));
            Assert.That(presenter.ActiveTransientDebrisCount,
                Is.EqualTo(detachedCount));

            destructibles.ClearReplayTransients();
            Assert.That(presenter.ActiveTransientDebrisCount, Is.Zero);
            destructibles.PresentReplay(
                new List<DestructiblePropSnapshot> { before });
            Physics.SyncTransforms();
            Assert.That(originalRenderer.enabled, Is.True);
            Assert.That(originalCollider.enabled, Is.True);
            Assert.That(
                chunks.Count(chunk => chunk.gameObject.activeInHierarchy),
                Is.Zero);

            destructibles.RestoreAuthoritativePresentation();
            Physics.SyncTransforms();
            Assert.That(originalRenderer.enabled, Is.False);
            Assert.That(originalCollider.enabled, Is.False);
            Assert.That(
                chunks.Count(chunk => chunk.gameObject.activeInHierarchy),
                Is.EqualTo(12 - detachedCount));
            Assert.That(
                destructibles.Session.GetProp(propId).DetachedFractureChunks,
                Is.EqualTo(damaged.DetachedFractureChunks));

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
