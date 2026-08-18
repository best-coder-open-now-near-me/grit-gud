using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Domain.Turns;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplaySpatialEvidenceParityTests
    {
        private const int TacticalLayer = 31;
        private const int TacticalMask = 1 << TacticalLayer;

        [Test]
        public void TacticalCoverMatchesUnityAcrossResultingPropStates()
        {
            var observer = new GameObject("Parity Observer");
            var target = new GameObject("Parity Target");
            var prop = new GameObject("Parity Cover");
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var origin = new GameplayPosition(-2f, 1f, 0f);
                var destination = new GameplayPosition(2f, 1f, 0f);
                observer.transform.position = ToVector(origin);
                target.transform.position = ToVector(destination);
                visual.layer = TacticalLayer;
                visual.transform.SetParent(prop.transform, false);
                visual.transform.localPosition = new Vector3(0f, 1f, 0f);
                visual.transform.localScale = new Vector3(1f, 2f, 1f);
                var presenter = prop.AddComponent<DestructiblePropPresenter>();
                GameplayCombatStateSnapshot initial = CreateInitialState();
                var spatial = new GameplayHeadlessSpatialEvidence(
                    CreateLevel(),
                    new SpatialContentIdentity(
                        "spatial-parity",
                        levelSchemaVersion: 1,
                        evidenceAlgorithmVersion: 1,
                        new string('a', 64)));
                var region = new TargetRegionSample(
                    TargetRegionId.Torso,
                    destination,
                    radius: 0.08f);

                DestructiblePropSnapshot[] corpus =
                {
                    CreateProp(
                        DestructiblePropState.Intact,
                        new GameplayPropPose(
                            new GameplayPosition(0f, 0f, 0f),
                            0f,
                            0f,
                            0f),
                        DestructiblePropPosture.Upright),
                    CreateProp(
                        DestructiblePropState.Damaged,
                        new GameplayPropPose(
                            new GameplayPosition(0f, 0f, 0f),
                            0f,
                            35f,
                            0f),
                        DestructiblePropPosture.Upright),
                    CreateProp(
                        DestructiblePropState.Intact,
                        new GameplayPropPose(
                            new GameplayPosition(0f, 0f, 0f),
                            0f,
                            0f,
                            90f),
                        DestructiblePropPosture.Toppled),
                    CreateProp(
                        DestructiblePropState.Intact,
                        new GameplayPropPose(
                            new GameplayPosition(0f, 0f, 0f),
                            90f,
                            0f,
                            0f),
                        DestructiblePropPosture.Toppled),
                    CreateProp(
                        DestructiblePropState.Intact,
                        new GameplayPropPose(
                            new GameplayPosition(0f, 0f, 4f),
                            0f,
                            25f,
                            0f),
                        DestructiblePropPosture.Upright),
                    CreateProp(
                        DestructiblePropState.Destroyed,
                        new GameplayPropPose(
                            new GameplayPosition(0f, 0f, 0f),
                            0f,
                            0f,
                            0f),
                        DestructiblePropPosture.Upright),
                };

                presenter.Bind(corpus[0]);
                foreach (DestructiblePropSnapshot snapshot in corpus)
                {
                    presenter.Present(snapshot);
                    Physics.SyncTransforms();
                    GameplayCombatStateSnapshot state = WithProp(
                        initial,
                        snapshot);
                    bool headlessBlocked = spatial.BlocksLineOfSight(
                        state,
                        origin,
                        destination);
                    var unity = new UnityTargetExposureQuery(
                        observer.transform,
                        target.transform,
                        TacticalMask);
                    TargetExposureSnapshot exposure = unity.Capture(
                        "observer",
                        origin,
                        "target",
                        new[] { region });
                    bool unityBlocked = exposure.VisibleSampleCount == 0;
                    Assert.That(
                        headlessBlocked,
                        Is.EqualTo(unityBlocked),
                        $"Spatial parity diverged for {snapshot.State} "
                        + $"at pose {snapshot.Pose.PitchDegrees}/"
                        + $"{snapshot.Pose.YawDegrees}/"
                        + $"{snapshot.Pose.RollDegrees}.");
                    Assert.That(
                        spatial.EvaluateBlastExposure(
                            state,
                            origin,
                            destination),
                        Is.EqualTo(unityBlocked ? 0f : 1f));
                }

                DestructiblePropSnapshot upright = corpus[0];
                presenter.Present(upright);
                Physics.SyncTransforms();
                var highOrigin = new GameplayPosition(-2f, 2.6f, 0f);
                var highDestination = new GameplayPosition(2f, 2.6f, 0f);
                Vector3 direction = ToVector(highDestination)
                    - ToVector(highOrigin);
                bool unityPathBlocked = Physics.SphereCast(
                    ToVector(highOrigin),
                    radius: 0.75f,
                    direction.normalized,
                    out _,
                    direction.magnitude,
                    TacticalMask,
                    QueryTriggerInteraction.Ignore);
                bool headlessPathBlocked = spatial.BlocksPath(
                    WithProp(initial, upright),
                    highOrigin,
                    highDestination,
                    clearanceRadius: 0.75f);
                Assert.That(headlessPathBlocked, Is.EqualTo(unityPathBlocked));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prop);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(observer);
            }
        }

        [Test]
        public void DetachedFractureChunksMatchUnityAndFailClosedWithoutProfile()
        {
            var observer = new GameObject("Fracture Parity Observer");
            var target = new GameObject("Fracture Parity Target");
            var prop = new GameObject("Fracture Parity Cover");
            GameObject original = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var fracturePrefab = new GameObject("Fracture Parity Prefab");
            var profile = ScriptableObject.CreateInstance<
                DestructibleFractureProfile>();
            try
            {
                original.layer = TacticalLayer;
                original.transform.SetParent(prop.transform, false);
                original.transform.localPosition = new Vector3(0f, 1f, 0f);
                original.transform.localScale = new Vector3(1f, 2f, 2f);
                Vector3[] centers =
                {
                    new Vector3(0f, 1f, -0.75f),
                    new Vector3(0f, 1f, 0.75f),
                };
                for (int index = 0; index < centers.Length; index++)
                {
                    GameObject chunk = GameObject.CreatePrimitive(
                        PrimitiveType.Cube);
                    chunk.name = $"Chunk {index}";
                    chunk.layer = TacticalLayer;
                    chunk.transform.SetParent(fracturePrefab.transform, false);
                    chunk.transform.localPosition = centers[index];
                    chunk.transform.localScale = new Vector3(1f, 2f, 0.5f);
                    chunk.AddComponent<DestructibleFractureChunk>()
                        .Configure(index);
                }
                profile.Configure(
                    "fracture.parity",
                    fracturePrefab,
                    centers,
                    impulse: 1f,
                    lifetime: 1f);

                var profiles = new Dictionary<
                    string,
                    GameplayFractureSpatialProfile>(StringComparer.Ordinal)
                {
                    ["cover.parity"] = profile.CreateSpatialProfile(),
                };
                var spatial = new GameplayHeadlessSpatialEvidence(
                    CreateLevel(),
                    new SpatialContentIdentity(
                        "spatial-parity",
                        levelSchemaVersion: 1,
                        evidenceAlgorithmVersion: 1,
                        new string('b', 64)),
                    profiles);
                GameplayCombatStateSnapshot initial = CreateInitialState();
                var presenter = prop.AddComponent<DestructiblePropPresenter>();
                presenter.Bind(CreateFracturedProp(
                    DestructiblePropState.Intact,
                    detachedMask: 0UL), profile);
                var origin = new GameplayPosition(-2f, 1f, -0.75f);
                var destination = new GameplayPosition(2f, 1f, -0.75f);
                observer.transform.position = ToVector(origin);
                target.transform.position = ToVector(destination);
                var region = new TargetRegionSample(
                    TargetRegionId.Torso,
                    destination,
                    radius: 0.08f);

                foreach (DestructiblePropSnapshot snapshot in new[]
                {
                    CreateFracturedProp(
                        DestructiblePropState.Damaged,
                        detachedMask: 1UL),
                    CreateFracturedProp(
                        DestructiblePropState.Damaged,
                        detachedMask: 2UL),
                    CreateFracturedProp(
                        DestructiblePropState.Destroyed,
                        detachedMask: 3UL),
                })
                {
                    presenter.Present(snapshot);
                    Physics.SyncTransforms();
                    GameplayCombatStateSnapshot state = WithProp(
                        initial,
                        snapshot);
                    bool headlessBlocked = spatial.BlocksLineOfSight(
                        state,
                        origin,
                        destination);
                    var unity = new UnityTargetExposureQuery(
                        observer.transform,
                        target.transform,
                        TacticalMask);
                    bool unityBlocked = unity.Capture(
                        "observer",
                        origin,
                        "target",
                        new[] { region }).VisibleSampleCount == 0;
                    Assert.That(
                        headlessBlocked,
                        Is.EqualTo(unityBlocked),
                        $"Fracture parity diverged for detached mask "
                        + $"{snapshot.DetachedFractureChunks}.");
                }

                var missingProfile = new GameplayHeadlessSpatialEvidence(
                    CreateLevel(),
                    new SpatialContentIdentity(
                        "spatial-parity",
                        levelSchemaVersion: 1,
                        evidenceAlgorithmVersion: 1,
                        new string('c', 64)));
                Assert.Throws<InvalidOperationException>(() =>
                    missingProfile.BlocksLineOfSight(
                        WithProp(
                            initial,
                            CreateFracturedProp(
                                DestructiblePropState.Damaged,
                                detachedMask: 1UL)),
                        origin,
                        destination));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(fracturePrefab);
                UnityEngine.Object.DestroyImmediate(prop);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(observer);
            }
        }

        private static GameplayCombatStateSnapshot CreateInitialState()
        {
            var scenario = new ScenarioDefinition(
                "spatial-parity",
                new ScenarioTimingDefinition(1f),
                new[]
                {
                    new ScenarioActorDefinition(
                        "observer",
                        10,
                        new GameplayActorPose(
                            new GameplayPosition(-2f, 0f, 0f),
                            90f),
                        new TurnBudget(4, 8f)),
                    new ScenarioActorDefinition(
                        "target",
                        0,
                        new GameplayActorPose(
                            new GameplayPosition(2f, 0f, 0f),
                            270f),
                        new TurnBudget(4, 8f)),
                },
                Array.Empty<ScenarioObjectiveDefinition>());
            var gameplay = new GameplaySession(scenario, scenarioSeed: 7u);
            GameplayCombatStateSnapshot session =
                GameplayCombatStateCapture.Capture(gameplay);
            return WithProp(
                session,
                CreateProp(
                    DestructiblePropState.Intact,
                    new GameplayPropPose(
                        new GameplayPosition(0f, 0f, 0f),
                        0f,
                        0f,
                        0f),
                    DestructiblePropPosture.Upright));
        }

        private static GameplayCombatStateSnapshot WithProp(
            GameplayCombatStateSnapshot source,
            DestructiblePropSnapshot prop) => new GameplayCombatStateSnapshot(
                source.Session,
                new[] { prop },
                source.Vehicles,
                source.Projectiles,
                source.SmokeFields,
                source.Coverage | GameplayCombatStateCoverage.Destructibles);

        private static DestructiblePropSnapshot CreateProp(
            DestructiblePropState state,
            GameplayPropPose pose,
            DestructiblePropPosture posture) => new DestructiblePropSnapshot(
                "cover",
                state,
                maximumIntegrity: 10f,
                remainingIntegrity: state == DestructiblePropState.Destroyed
                    ? 0f
                    : state == DestructiblePropState.Damaged ? 5f : 10f,
                pose: pose,
                posture: posture);

        private static DestructiblePropSnapshot CreateFracturedProp(
            DestructiblePropState state,
            ulong detachedMask) => new DestructiblePropSnapshot(
                "cover",
                state,
                maximumIntegrity: 10f,
                remainingIntegrity: state == DestructiblePropState.Destroyed
                    ? 0f
                    : state == DestructiblePropState.Damaged ? 5f : 10f,
                new GameplayPropPose(
                    new GameplayPosition(0f, 0f, 0f),
                    pitchDegrees: 0f,
                    yawDegrees: 0f,
                    rollDegrees: 0f),
                DestructiblePropPosture.Upright,
                fractureChunkCount: 2,
                detachedFractureChunks: detachedMask);

        private static LevelDocument CreateLevel()
        {
            var level = new LevelDocument
            {
                levelId = "spatial-parity",
                displayName = "Spatial parity",
            };
            var cover = new LevelEntity
            {
                id = "cover",
                archetypeId = "cover.parity",
                destructible = new DestructibleInstanceData
                {
                    enabled = true,
                    initialState = "intact",
                    integrity = 10f,
                },
            };
            cover.coverVolumes.Add(new CoverVolumeData
            {
                id = "cover.volume",
                localCenter = new Float3Data(0f, 1f, 0f),
                size = new Float3Data(1f, 2f, 1f),
            });
            level.entities.Add(cover);
            level.Normalize();
            return level;
        }

        private static Vector3 ToVector(GameplayPosition position) =>
            new Vector3(position.X, position.Y, position.Z);
    }
}
