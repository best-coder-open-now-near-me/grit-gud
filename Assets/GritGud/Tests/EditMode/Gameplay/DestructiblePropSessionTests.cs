using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class DestructiblePropSessionTests
    {
        [Test]
        public void DamageTransitionsThroughExplicitRecordedStates()
        {
            DestructiblePropSession session = CreateSession();

            Assert.That(session.TryApplyDamage("cover", 3f, out var damaged), Is.True);
            Assert.That(damaged.Sequence, Is.EqualTo(1));
            Assert.That(damaged.Previous.State, Is.EqualTo(DestructiblePropState.Intact));
            Assert.That(damaged.Resulting.State, Is.EqualTo(DestructiblePropState.Damaged));
            Assert.That(damaged.Resulting.RemainingIntegrity, Is.EqualTo(7f));

            Assert.That(session.TryApplyDamage("cover", 20f, out var destroyed), Is.True);
            Assert.That(destroyed.Sequence, Is.EqualTo(2));
            Assert.That(destroyed.AppliedDamage, Is.EqualTo(7f));
            Assert.That(destroyed.Resulting.State,
                Is.EqualTo(DestructiblePropState.Destroyed));
            Assert.That(destroyed.Resulting.RemainingIntegrity, Is.Zero);
            Assert.That(session.TryApplyDamage("cover", 1f, out _), Is.False);
        }

        [Test]
        public void DamageRecordReplaysToTheSameState()
        {
            DestructiblePropSession source = CreateSession();
            source.TryApplyDamage("cover", 4f, out var record);
            DestructiblePropSession replay = CreateSession();

            replay.CommitDamage(record);

            Assert.That(replay.GetProp("cover").State,
                Is.EqualTo(DestructiblePropState.Damaged));
            Assert.That(replay.GetProp("cover").RemainingIntegrity, Is.EqualTo(6f));
            Assert.That(replay.DamageRecords.Count, Is.EqualTo(1));
        }

        [Test]
        public void DamagePreservesAnAuthoredWorldPosition()
        {
            var position = new GameplayPosition(2f, 0f, -3f);
            var session = new DestructiblePropSession(new[]
            {
                new DestructiblePropDefinition(
                    "positioned-cover",
                    10f,
                    DestructiblePropState.Intact,
                    position),
            });

            Assert.That(
                session.TryApplyDamage("positioned-cover", 3f, out var record),
                Is.True);
            Assert.That(record.Resulting.Position.X, Is.EqualTo(2f));
            Assert.That(record.Resulting.Position.Z, Is.EqualTo(-3f));
            Assert.That(session.GetProp("positioned-cover").Position.X, Is.EqualTo(2f));
        }

        [Test]
        public void LevelAssemblyPreservesAuthoredPropYaw()
        {
            var level = new LevelDocument();
            level.entities.Add(new LevelEntity
            {
                id = "rotated-cover",
                transform = new LevelTransformData(
                    new Float3Data(2f, 0f, -3f),
                    37f),
                destructible = new DestructibleInstanceData
                {
                    enabled = true,
                    initialState = "intact",
                    integrity = 10f,
                },
            });

            DestructiblePropSnapshot prop = DestructiblePropSession
                .FromLevel(level)
                .GetProp("rotated-cover");

            Assert.That(prop.Pose.YawDegrees, Is.EqualTo(37f));
            Assert.That(prop.Posture, Is.EqualTo(DestructiblePropPosture.Upright));
        }

        [Test]
        public void DisplacementReplayCommitsAuthoritativePoseAndPosture()
        {
            var initialPose = new GameplayPropPose(
                new GameplayPosition(1f, 0f, 2f),
                0f,
                25f,
                0f);
            var session = new DestructiblePropSession(new[]
            {
                new DestructiblePropDefinition(
                    "cover",
                    10f,
                    DestructiblePropState.Intact,
                    initialPose,
                    DestructiblePropPosture.Upright),
            });
            var destination = new GameplayPosition(2f, 0f, 3f);
            var resultingPose = new GameplayPropPose(
                destination,
                90f,
                25f,
                0f);
            var request = new DisplacementRequest(
                "player",
                "close-quarters.push",
                "cover",
                DisplacementSubjectKind.Prop,
                35f,
                destination,
                DisplacementActionKind.Push);
            var record = new DisplacementRecord(
                1L,
                request,
                new PropDisplacementState(
                    initialPose,
                    DestructiblePropPosture.Upright),
                new PropDisplacementState(
                    resultingPose,
                    DestructiblePropPosture.Toppled),
                DisplacementResultPolicies.Topple);

            session.CommitDisplacement(record);

            DestructiblePropSnapshot prop = session.GetProp("cover");
            Assert.That(prop.Position, Is.EqualTo(destination));
            Assert.That(prop.Pose.PitchDegrees, Is.EqualTo(90f));
            Assert.That(prop.Pose.YawDegrees, Is.EqualTo(25f));
            Assert.That(prop.Posture, Is.EqualTo(DestructiblePropPosture.Toppled));
        }

        [Test]
        public void ReplayRejectsARecordFromDifferentCurrentState()
        {
            DestructiblePropSession source = CreateSession();
            source.TryApplyDamage("cover", 4f, out var record);
            DestructiblePropSession replay = CreateSession();
            replay.TryApplyDamage("cover", 1f, out _);

            Assert.Throws<InvalidOperationException>(() =>
                replay.CommitDamage(record));
        }

        private static DestructiblePropSession CreateSession() =>
            new DestructiblePropSession(new[]
            {
                new DestructiblePropDefinition(
                    "cover",
                    10f,
                    DestructiblePropState.Intact),
            });
    }
}
