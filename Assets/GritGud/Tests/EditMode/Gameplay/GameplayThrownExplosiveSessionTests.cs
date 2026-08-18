using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayThrownExplosiveSessionTests
    {
        [Test]
        public void ThrowRecordsSampledLandingObstructionAndFriendlyFire()
        {
            GameplaySession gameplay = CreateGameplay();
            gameplay.BeginEncounter();
            var sampler = new FixedSampler(new GameplayPosition(4f, 0f, 1f));
            var world = new FixedWorldQuery();
            var session = CreateThrownSession(
                gameplay, world, sampler);
            var definition = CreateDefinition();

            Assert.That(session.TryThrowItem(
                "player", definition.Id, new GameplayPosition(4f, 0f, 0f),
                out GameplayActionRecord action, out _), Is.True);

            ThrownExplosiveRecord record =
                ((ThrownExplosiveActionOutcome)action.Outcomes[0]).Record;
            InventoryQuantityChangeRecord quantity =
                ((InventoryQuantityChangedActionOutcome)action.Outcomes[1])
                    .Change;
            var expectedLaunch = new GameplayPosition(0f, 1.2f, 0f);
            Assert.That(record.LaunchOrigin, Is.EqualTo(expectedLaunch));
            Assert.That(world.LastLaunchOrigin, Is.EqualTo(expectedLaunch));
            Assert.That(
                gameplay.GetActor("player").Pose.FacingDegrees,
                Is.EqualTo(90f).Within(0.001f));
            Assert.That(record.SampledLanding, Is.EqualTo(new GameplayPosition(4f, 0f, 1f)));
            Assert.That(record.ResolvedLanding, Is.EqualTo(new GameplayPosition(3f, 0f, 1f)));
            Assert.That(record.UncertaintyRadius, Is.EqualTo(1.4f).Within(0.001f));
            Assert.That(record.WorldStateRevision, Is.EqualTo(42));
            Assert.That(record.BlastEffects.Count, Is.EqualTo(2));
            Assert.That(record.BlastEffects[0].EntityId, Is.EqualTo("enemy"));
            Assert.That(record.BlastEffects[1].EntityId, Is.EqualTo("player"));
            Assert.That(action.Outcomes, Has.Count.EqualTo(2));
            Assert.That(quantity.PreviousQuantity, Is.EqualTo(3));
            Assert.That(quantity.ConsumedQuantity, Is.EqualTo(1));
            Assert.That(quantity.ResultingQuantity, Is.EqualTo(2));
            Assert.That(
                gameplay.GetInventoryQuantity("player", "item.grenade"),
                Is.EqualTo(2));
            Assert.That(
                gameplay.GetActor("player").Inventory.GetQuantity(
                    "item.grenade"),
                Is.EqualTo(2));
            Assert.That(gameplay.GetActor("player").TurnBudget.ActionPoints, Is.EqualTo(2));
            Assert.That(gameplay.GetActor("enemy").Wounds.WoundCount, Is.EqualTo(1));
            Assert.That(gameplay.GetActor("enemy").Wounds.MovementPenalty,
                Is.EqualTo(1.6f).Within(0.001f));
            Assert.That(gameplay.GetActor("enemy").Wounds.LeftArmWounds,
                Is.EqualTo(1));
            Assert.That(gameplay.GetActor("enemy").Wounds.TorsoWounds,
                Is.Zero);
        }

        [Test]
        public void OutOfRangeThrowDoesNotSampleOrSpendBudget()
        {
            GameplaySession gameplay = CreateGameplay();
            gameplay.BeginEncounter();
            var sampler = new FixedSampler(new GameplayPosition(0f, 0f, 0f));
            var session = CreateThrownSession(
                gameplay, new FixedWorldQuery(), sampler);

            Assert.That(session.TryThrow(
                "player", CreateDefinition(), new GameplayPosition(20f, 0f, 0f),
                out _, out ThrownExplosiveFailure failure), Is.False);
            Assert.That(failure, Is.EqualTo(ThrownExplosiveFailure.OutOfRange));
            Assert.That(sampler.CallCount, Is.Zero);
            Assert.That(gameplay.GetActor("player").TurnBudget.ActionPoints, Is.EqualTo(4));
        }

        [Test]
        public void ReplayCommitDoesNotRepeatSamplingOrWorldQueries()
        {
            GameplaySession source = CreateGameplay();
            source.BeginEncounter();
            var sampler = new FixedSampler(new GameplayPosition(4f, 0f, 1f));
            var sourceSession = CreateThrownSession(
                source, new FixedWorldQuery(), sampler);
            sourceSession.TryThrow(
                "player", CreateDefinition(), new GameplayPosition(4f, 0f, 0f),
                out GameplayActionRecord recordedAction, out _);

            GameplaySession replay = CreateGameplay();
            replay.BeginEncounter();
            var forbiddenSampler = new FixedSampler(new GameplayPosition(99f, 0f, 99f));
            var replaySession = CreateThrownSession(
                replay, new ThrowingWorldQuery(), forbiddenSampler);
            replaySession.CommitThrow(recordedAction);

            Assert.That(forbiddenSampler.CallCount, Is.Zero);
            Assert.That(replaySession.Throws[0].ResolvedLanding,
                Is.EqualTo(new GameplayPosition(3f, 0f, 1f)));
            Assert.That(
                replay.GetInventoryQuantity("player", "item.grenade"),
                Is.EqualTo(2));
        }

        [Test]
        public void PreviewReportsUncertaintyWithoutSamplingOrQueryingWorld()
        {
            GameplaySession gameplay = CreateGameplay();
            var sampler = new FixedSampler(new GameplayPosition(99f, 0f, 99f));
            var session = CreateThrownSession(
                gameplay, new ThrowingWorldQuery(), sampler);
            int journalEntryCount = gameplay.Journal.Entries.Count;

            Assert.That(session.TryPreview(
                "player", CreateDefinition(), new GameplayPosition(4f, 0f, 0f),
                out float radius, out _), Is.True);
            Assert.That(radius, Is.EqualTo(1.4f).Within(0.001f));
            Assert.That(sampler.CallCount, Is.Zero);
            Assert.That(gameplay.GetActor("player").TurnBudget.ActionPoints, Is.EqualTo(4));
            Assert.That(
                gameplay.GetInventoryQuantity("player", "item.grenade"),
                Is.EqualTo(3));
            Assert.That(gameplay.Mode, Is.EqualTo(GameplaySessionMode.Exploration));
            Assert.That(gameplay.EncounterActive, Is.False);
            Assert.That(gameplay.Journal.Entries.Count, Is.EqualTo(journalEntryCount));
        }

        [Test]
        public void ExplorationThrowWithoutResponsiveBlastStaysContinuous()
        {
            GameplaySession gameplay = CreateGameplay();
            var session = CreateThrownSession(
                gameplay,
                new FixedWorldQuery(),
                new FixedSampler(new GameplayPosition(4f, 0f, 1f)));

            Assert.That(session.TryThrowItem(
                "player",
                "item.grenade",
                new GameplayPosition(4f, 0f, 0f),
                out GameplayActionRecord action,
                out _), Is.True);

            Assert.That(gameplay.Mode,
                Is.EqualTo(GameplaySessionMode.Exploration));
            Assert.That(gameplay.EncounterActive, Is.False);
            Assert.That(action.Cost.ActionPoints, Is.Zero);
            Assert.That(
                gameplay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(4));
            Assert.That(
                gameplay.ThrownExplosiveStartsEncounter(
                    ((ThrownExplosiveActionOutcome)action.Outcomes[0]).Record),
                Is.False);

            Assert.That(gameplay.EnterTurnMode(), Is.True);
            Assert.That(session.TryThrowItem(
                "player",
                "item.grenade",
                new GameplayPosition(4f, 0f, 0f),
                out GameplayActionRecord voluntaryAction,
                out _), Is.True);
            Assert.That(voluntaryAction.Cost.ActionPoints, Is.EqualTo(2));
            Assert.That(gameplay.EncounterActive, Is.False);
            Assert.That(gameplay.TryExitTurnMode(out _), Is.True);
        }

        [Test]
        public void SmokeThrowDeploysAuthoritativeFieldWithoutBlastDamage()
        {
            ThrownExplosiveDefinition definition = CreateSmokeDefinition();
            GameplaySession gameplay = CreateGameplay(
                definition: definition);
            using var smoke = new GameplaySmokeFieldSession(gameplay);
            var session = CreateThrownSession(
                gameplay,
                new FixedWorldQuery(),
                new FixedSampler(new GameplayPosition(4f, 0f, 0.5f)),
                smoke);

            Assert.That(session.TryThrowItem(
                "player",
                definition.Id,
                new GameplayPosition(4f, 0f, 0f),
                out GameplayActionRecord action,
                out _), Is.True);

            ThrownExplosiveRecord record =
                ((ThrownExplosiveActionOutcome)action.Outcomes[0]).Record;
            Assert.That(record.SmokeField, Is.Not.Null);
            Assert.That(smoke.ActiveCount, Is.EqualTo(1));
            Assert.That(gameplay.GetActor("enemy").Wounds.WoundCount, Is.Zero);
            Assert.That(smoke.BlocksSight(
                new GameplayPosition(-2f, 1f, 0.5f),
                new GameplayPosition(6f, 1f, 0.5f)), Is.True);
        }

        [Test]
        public void ObserverFailuresRunAfterTheWholeThrowIsCommitted()
        {
            GameplaySession gameplay = CreateGameplay();
            gameplay.BeginEncounter();
            var session = CreateThrownSession(
                gameplay,
                new FixedWorldQuery(),
                new FixedSampler(new GameplayPosition(4f, 0f, 1f)));
            int successfulObservers = 0;
            gameplay.ActorCapabilityChanged += _ =>
                throw new InvalidOperationException("observer failed");
            gameplay.ActorCapabilityChanged += _ => successfulObservers++;

            Assert.Throws<AggregateException>(() =>
                session.TryThrowItem(
                    "player",
                    "item.grenade",
                    new GameplayPosition(4f, 0f, 0f),
                    out _,
                    out _));

            Assert.That(successfulObservers, Is.EqualTo(2));
            Assert.That(session.Throws, Has.Count.EqualTo(1));
            Assert.That(gameplay.ResolvedActions, Has.Count.EqualTo(1));
            Assert.That(
                gameplay.GetInventoryQuantity("player", "item.grenade"),
                Is.EqualTo(2));
            Assert.That(gameplay.GetActor("enemy").Wounds.WoundCount,
                Is.EqualTo(1));
            Assert.That(gameplay.GetActor("player").Wounds.WoundCount,
                Is.EqualTo(1));
        }

        [Test]
        public void PreparedResponsiveBlastCommitsBeforeBeginningEncounter()
        {
            GameplaySession gameplay = CreateGameplay(
                responsiveEnemy: true);
            var session = CreateThrownSession(
                gameplay,
                new FixedWorldQuery(),
                new FixedSampler(new GameplayPosition(4f, 0f, 1f)));

            Assert.That(session.TryPrepareThrowItem(
                "player",
                "item.grenade",
                new GameplayPosition(4f, 0f, 0f),
                out ThrownExplosiveRecord prepared,
                out _), Is.True);

            Assert.That(
                gameplay.ThrownExplosiveStartsEncounter(prepared),
                Is.True);
            Assert.That(gameplay.Mode,
                Is.EqualTo(GameplaySessionMode.Exploration));
            Assert.That(gameplay.ResolvedActions, Is.Empty);
            Assert.That(session.Throws, Is.Empty);
            Assert.That(gameplay.GetActor("enemy").Wounds.WoundCount,
                Is.Zero);
            Assert.That(
                gameplay.GetInventoryQuantity("player", "item.grenade"),
                Is.EqualTo(3));

            Assert.That(session.TryCommitPreparedThrow(
                prepared,
                out GameplayActionRecord action,
                out _), Is.True);
            Assert.That(action.Cost.ActionPoints, Is.EqualTo(2));
            Assert.That(gameplay.Mode,
                Is.EqualTo(GameplaySessionMode.Exploration));
            Assert.That(gameplay.ActionStartsEncounter(action), Is.True);
            Assert.That(gameplay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(2));
            Assert.That(gameplay.GetActor("enemy").Wounds.WoundCount,
                Is.EqualTo(1));
            Assert.That(
                gameplay.GetInventoryQuantity("player", "item.grenade"),
                Is.EqualTo(2));
            Assert.That(gameplay.BeginEncounterFromAction(action), Is.True);
            Assert.That(gameplay.EncounterActive, Is.True);
            Assert.That(gameplay.Mode,
                Is.EqualTo(GameplaySessionMode.TurnBased));
        }

        [Test]
        public void RepeatedPreparationUsesTheSameAddressedLanding()
        {
            GameplaySession gameplay = CreateGameplay();
            var session = CreateThrownSession(
                gameplay,
                new FixedWorldQuery(),
                new AddressedUncertaintySampler());
            var intended = new GameplayPosition(4f, 0f, 0f);

            Assert.That(session.TryPrepareThrowItem(
                "player",
                "item.grenade",
                intended,
                out ThrownExplosiveRecord first,
                out _), Is.True);
            Assert.That(session.TryPrepareThrowItem(
                "player",
                "item.grenade",
                intended,
                out ThrownExplosiveRecord repeated,
                out _), Is.True);

            Assert.That(repeated.Sequence, Is.EqualTo(first.Sequence));
            Assert.That(repeated.SampledLanding, Is.EqualTo(first.SampledLanding));
            Assert.That(repeated.ResolvedLanding, Is.EqualTo(first.ResolvedLanding));
            Assert.That(gameplay.ResolvedActions, Is.Empty);
            Assert.That(session.Throws, Is.Empty);
        }

        [Test]
        public void DepletedConsumableCannotSampleOrCommitAnotherThrow()
        {
            GameplaySession gameplay = CreateGameplay(initialQuantity: 1);
            var sampler = new FixedSampler(new GameplayPosition(4f, 0f, 1f));
            var session = CreateThrownSession(
                gameplay,
                new FixedWorldQuery(),
                sampler);

            Assert.That(session.TryThrowItem(
                "player",
                "item.grenade",
                new GameplayPosition(4f, 0f, 0f),
                out _,
                out _), Is.True);
            int journalCount = gameplay.Journal.Entries.Count;
            int sampleCount = sampler.CallCount;

            Assert.That(session.TryThrowItem(
                "player",
                "item.grenade",
                new GameplayPosition(4f, 0f, 0f),
                out _,
                out ThrownExplosiveFailure failure), Is.False);

            Assert.That(failure, Is.EqualTo(ThrownExplosiveFailure.Depleted));
            Assert.That(sampler.CallCount, Is.EqualTo(sampleCount));
            Assert.That(gameplay.ResolvedActions, Has.Count.EqualTo(1));
            Assert.That(gameplay.Journal.Entries.Count, Is.EqualTo(journalCount));
            Assert.That(
                gameplay.GetInventoryQuantity("player", "item.grenade"),
                Is.Zero);
            InventoryPowerAvailability availability =
                new GameplayInventoryAvailabilitySession(gameplay)
                    .EvaluatePower("player", "item.grenade");
            Assert.That(availability.IsAvailable, Is.False);
            Assert.That(availability.Failure,
                Is.EqualTo(InventoryPowerAvailabilityFailure.Depleted));
            Assert.That(availability.Requirement,
                Is.EqualTo("NO QUANTITY REMAINING"));
        }

        [Test]
        public void ReplayRejectsAStaleQuantityTransitionWithoutMutation()
        {
            GameplaySession source = CreateGameplay(initialQuantity: 3);
            source.BeginEncounter();
            var sourceSession = CreateThrownSession(
                source,
                new FixedWorldQuery(),
                new FixedSampler(new GameplayPosition(4f, 0f, 1f)));
            Assert.That(sourceSession.TryThrowItem(
                "player",
                "item.grenade",
                new GameplayPosition(4f, 0f, 0f),
                out GameplayActionRecord recordedAction,
                out _), Is.True);

            GameplaySession replay = CreateGameplay(initialQuantity: 2);
            replay.BeginEncounter();
            var replaySession = CreateThrownSession(
                replay,
                new ThrowingWorldQuery(),
                new FixedSampler(new GameplayPosition(99f, 0f, 99f)));

            Assert.Throws<InvalidOperationException>(() =>
                replaySession.CommitThrow(recordedAction));
            Assert.That(replay.ResolvedActions, Is.Empty);
            Assert.That(replaySession.Throws, Is.Empty);
            Assert.That(
                replay.GetInventoryQuantity("player", "item.grenade"),
                Is.EqualTo(2));
            Assert.That(
                replay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(4));
        }

        [Test]
        public void ActionBoundaryRejectsDuplicateQuantityConsumption()
        {
            GameplaySession source = CreateGameplay();
            source.BeginEncounter();
            var sourceSession = CreateThrownSession(
                source,
                new FixedWorldQuery(),
                new FixedSampler(new GameplayPosition(4f, 0f, 1f)));
            Assert.That(sourceSession.TryThrowItem(
                "player",
                "item.grenade",
                new GameplayPosition(4f, 0f, 0f),
                out GameplayActionRecord recorded,
                out _), Is.True);
            var thrown = (ThrownExplosiveActionOutcome)recorded.Outcomes[0];
            var quantity = (InventoryQuantityChangedActionOutcome)
                recorded.Outcomes[1];
            var duplicated = new GameplayActionRecord(
                recorded.Sequence,
                recorded.Request,
                recorded.Cost,
                recorded.PreviousBudget,
                recorded.ResultingBudget,
                new GameplayActionOutcome[]
                {
                    thrown,
                    quantity,
                    new InventoryQuantityChangedActionOutcome(
                        quantity.Change),
                });
            GameplaySession replay = CreateGameplay();
            replay.BeginEncounter();

            Assert.Throws<InvalidOperationException>(() =>
                replay.CommitAction(duplicated));
            Assert.That(replay.ResolvedActions, Is.Empty);
            Assert.That(
                replay.GetInventoryQuantity("player", "item.grenade"),
                Is.EqualTo(3));
        }

        [Test]
        public void AddressedSamplerIsDeterministicAndStaysInsideRegion()
        {
            var first = new AddressedUncertaintySampler();
            var second = new AddressedUncertaintySampler();
            var center = new GameplayPosition(5f, 2f, -3f);
            var run = new ScenarioRunIdentity("test", 123u);
            var transition = new GameplayTransitionIdentity(
                1L,
                "thrown-explosive",
                "player",
                "item.grenade");

            GameplayPosition firstPoint = first.Sample(
                center, 2f, run, transition, "landing-error");
            GameplayPosition secondPoint = second.Sample(
                center, 2f, run, transition, "landing-error");

            Assert.That(firstPoint, Is.EqualTo(secondPoint));
            Assert.That(firstPoint.DistanceTo(center), Is.LessThanOrEqualTo(2f));
            Assert.That(firstPoint.Y, Is.EqualTo(center.Y));
        }

        [Test]
        public void AddressedSamplerWeightsLandingErrorTowardAimPoint()
        {
            var sampler = new AddressedUncertaintySampler();
            var center = new GameplayPosition(5f, 2f, -3f);
            var run = new ScenarioRunIdentity("test", 123u);
            const int sampleCount = 4096;
            float totalDistance = 0f;

            for (int index = 0; index < sampleCount; index++)
            {
                var transition = new GameplayTransitionIdentity(
                    index + 1L,
                    "thrown-explosive",
                    "player",
                    "item.grenade");
                GameplayPosition point = sampler.Sample(
                    center,
                    1f,
                    run,
                    transition,
                    "landing-error");
                float distance = point.DistanceTo(center);
                Assert.That(distance, Is.LessThanOrEqualTo(1f));
                totalDistance += distance;
            }

            float meanDistance = totalDistance / sampleCount;
            Assert.That(meanDistance, Is.InRange(0.31f, 0.35f));
        }

        [Test]
        public void AddressedSamplerDoesNotLandOutsideUncertaintyEdge()
        {
            var sampler = new AddressedUncertaintySampler();
            var center = new GameplayPosition(5f, 0f, 5f);
            var run = new ScenarioRunIdentity("depot", 12648430u);
            var transition = new GameplayTransitionIdentity(
                1L,
                "thrown-explosive",
                "player",
                "item.grenade");

            GameplayPosition point = sampler.Sample(
                center, 1f, run, transition, "landing-error");

            Assert.That(point.DistanceTo(center), Is.LessThanOrEqualTo(1f));
        }

        private static ThrownExplosiveDefinition CreateDefinition() =>
            new ThrownExplosiveDefinition(
                "item.grenade", new ActionCost(2, 0f, ActionMobility.Mobile),
                10f, 1.2f, 0.82f, 1f, 0.1f, 5f,
                blastWoundMovementPenalty: 2f);

        private static ThrownExplosiveDefinition CreateSmokeDefinition() =>
            new ThrownExplosiveDefinition(
                "item.smoke-grenade",
                new ActionCost(2, 0f, ActionMobility.Mobile),
                10f,
                1.2f,
                0.82f,
                0.5f,
                0.1f,
                0f,
                0f,
                0f,
                new SmokeFieldDefinition(4f, 2.8f, 24f, 4, 0.75f));

        private static GameplaySession CreateGameplay(
            bool responsiveEnemy = false,
            int initialQuantity = 3,
            ThrownExplosiveDefinition definition = null)
        {
            definition = definition ?? CreateDefinition();
            var player = new ScenarioActorDefinition(
                "player", 10, new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f),
                new[]
                {
                    new InventoryItemDefinition(
                        definition.Id,
                        "Grenade",
                        3,
                        InventoryItemKind.Consumable,
                        new ActionCost(0, 0f, ActionMobility.Mobile),
                        EquipmentEffectSet.None,
                        consumablePower: definition,
                        initialQuantity: initialQuantity),
                },
                initiallyEquippedItemId: null);
            var enemy = new ScenarioActorDefinition(
                "enemy", 0, new GameplayActorPose(new GameplayPosition(3f, 0f, 0f), 180f),
                new TurnBudget(4, 8f));
            var responses = responsiveEnemy
                ? new[]
                {
                    new AttackResponseDefinition(
                        "enemy",
                        startsEncounter: true),
                }
                : Array.Empty<AttackResponseDefinition>();
            return new GameplaySession(new ScenarioDefinition(
                "grenade-test", new ScenarioTimingDefinition(1f),
                new[] { player, enemy },
                Array.Empty<ScenarioObjectiveDefinition>(),
                responses));
        }

        private sealed class FixedSampler : IUncertaintySampler
        {
            private readonly GameplayPosition result;
            public FixedSampler(GameplayPosition result) => this.result = result;
            public int CallCount { get; private set; }
            public GameplayPosition Sample(
                GameplayPosition center,
                float radius,
                ScenarioRunIdentity run,
                GameplayTransitionIdentity transition,
                string purpose)
            {
                CallCount++;
                return result;
            }
        }

        private interface ITestExplosiveWorld :
            IThrownExplosiveLandingQuery,
            IBlastWorldQuery
        {
        }

        private static GameplayThrownExplosiveSession CreateThrownSession(
            GameplaySession gameplay,
            ITestExplosiveWorld world,
            IUncertaintySampler sampler,
            GameplaySmokeFieldSession smokeFields = null)
        {
            var destructibles = new DestructiblePropSession(
                Array.Empty<DestructiblePropDefinition>());
            return new GameplayThrownExplosiveSession(
                gameplay,
                world,
                world,
                new GameplayBlastConsequenceResolver(
                    gameplay,
                    destructibles),
                sampler,
                smokeFields);
        }

        private sealed class FixedWorldQuery : ITestExplosiveWorld
        {
            public GameplayPosition LastLaunchOrigin { get; private set; }

            public ThrownExplosiveLandingResult Resolve(
                GameplayPosition launchOrigin,
                GameplayPosition sampledLanding)
            {
                LastLaunchOrigin = launchOrigin;
                return new ThrownExplosiveLandingResult(
                    new GameplayPosition(3f, 0f, 1f),
                    42);
            }

            public BlastWorldQueryResult Query(BlastWorldQuery query) =>
                new BlastWorldQueryResult(
                    query,
                    42,
                    new[]
                    {
                        new BlastEffectRecord(
                            "enemy",
                            BlastSubjectKind.Actor,
                            1f,
                            1f,
                            0.8f,
                            TargetRegionId.LeftArm),
                        new BlastEffectRecord(
                            "player",
                            BlastSubjectKind.Actor,
                            4f,
                            1f,
                            0.2f,
                            TargetRegionId.RightLeg),
                    });
        }

        private sealed class ThrowingWorldQuery : ITestExplosiveWorld
        {
            public ThrownExplosiveLandingResult Resolve(
                GameplayPosition launchOrigin,
                GameplayPosition sampledLanding) =>
                throw new AssertionException("Replay must not query the world.");

            public BlastWorldQueryResult Query(BlastWorldQuery query) =>
                throw new AssertionException("Replay must not query the world.");
        }
    }
}
