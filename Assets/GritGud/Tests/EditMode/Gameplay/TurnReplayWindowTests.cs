using System.Linq;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class TurnReplayWindowTests
    {
        [Test]
        public void EventTimelineUsesRecordedDurationsAndRoundTripsBoundaries()
        {
            var route = new MovementRouteRecord(
                "mara",
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new[] { new GameplayPosition(10f, 0f, 0f) });
            var window = new TurnReplayWindow(
                "mara",
                new[]
                {
                    new TurnReplaySegment(
                        1,
                        "mara",
                        new GameplayJournalEntry[]
                        {
                            new MovementRouteCommittedJournalEntry(1, route),
                            new MovementRouteCompletedJournalEntry(2, route),
                            new TurnEndedJournalEntry(
                                3,
                                new TurnEndRecord(1, "mara", "raider")),
                        }),
                    new TurnReplaySegment(
                        2,
                        "raider",
                        new GameplayJournalEntry[]
                        {
                            new TurnEndedJournalEntry(
                                4,
                                new TurnEndRecord(2, "raider", "mara")),
                        }),
                });

            var timeline = new TurnReplayEventTimeline(window);

            Assert.That(timeline.SegmentDurations[0], Is.GreaterThan(
                timeline.SegmentDurations[1]));
            Assert.That(timeline.DefaultTimeSeconds,
                Is.EqualTo(timeline.GetSegmentEndSeconds(0)));
            Assert.That(timeline.ToSegmentPlayhead(0f), Is.EqualTo(0f));
            Assert.That(
                timeline.ToSegmentPlayhead(timeline.GetSegmentEndSeconds(0)),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                timeline.ToSegmentPlayhead(timeline.TotalDurationSeconds),
                Is.EqualTo(2f));
            Assert.That(timeline.GetActiveEvent(0.1f).Entry,
                Is.TypeOf<MovementRouteCommittedJournalEntry>());
        }

        [Test]
        public void CrossingDetectorEmitsOnlyContinuousForwardBoundaries()
        {
            var window = new TurnReplayWindow(
                "mara",
                new[]
                {
                    new TurnReplaySegment(
                        1,
                        "mara",
                        new GameplayJournalEntry[]
                        {
                            new TurnEndedJournalEntry(
                                1,
                                new TurnEndRecord(1, "mara", "raider")),
                        }),
                });
            var timeline = new TurnReplayEventTimeline(window);
            var detector = new TurnReplayEventCrossingDetector(timeline);

            IReadOnlyList<TurnReplayEventCrossing> first = detector.Advance(
                timeline.TotalDurationSeconds);

            Assert.That(first, Has.Count.EqualTo(2));
            Assert.That(first[0].Boundary, Is.EqualTo(
                TurnReplayEventBoundary.Start));
            Assert.That(first[1].Boundary, Is.EqualTo(
                TurnReplayEventBoundary.End));

            detector.Seek(0f);
            Assert.That(detector.PreviousSeconds, Is.Zero);
            detector.Seek(timeline.TotalDurationSeconds);
            Assert.That(
                detector.Advance(timeline.TotalDurationSeconds),
                Is.Empty,
                "Direct forward seeks must not emit one-shot effects.");

            detector.Seek(timeline.TotalDurationSeconds);
            Assert.That(detector.Advance(0f), Is.Empty);
            Assert.That(
                detector.Advance(timeline.TotalDurationSeconds),
                Has.Count.EqualTo(2),
                "Playback may emit boundaries again after a backward seek.");
        }

        [Test]
        public void ActorActionProjectionSeeksEquipmentStateWithoutEffects()
        {
            var previous = new TurnBudget(4, 8f);
            var resulting = new TurnBudget(3, 8f);
            var action = new GameplayActionRecord(
                1,
                new GameplayActionRequest(
                    "mara",
                    EquipmentActionIds.Equip,
                    "weapon.rifle"),
                new ActionCost(1, 0f),
                previous,
                resulting,
                new GameplayActionOutcome[]
                {
                    new EquipmentChangedActionOutcome(
                        new EquipmentChangeRecord(
                            "mara",
                            "weapon.rifle",
                            EquipmentChangeKind.Equip,
                            previousEquippedItemId: null,
                            resultingEquippedItemId: "weapon.rifle")),
                });
            var window = new TurnReplayWindow(
                "mara",
                new[]
                {
                    new TurnReplaySegment(
                        1,
                        "mara",
                        new GameplayJournalEntry[]
                        {
                            new ActionResolvedJournalEntry(1, action),
                            new TurnEndedJournalEntry(
                                2,
                                new TurnEndRecord(1, "mara", "raider")),
                        }),
                });
            var timeline = new TurnReplayEventTimeline(window);
            TurnReplayTimedEvent actionEvent = timeline.Events[0];

            IReadOnlyList<TurnReplayActorActionState> states =
                TurnReplayActorActionProjector.Project(
                    timeline,
                    actionEvent.StartSeconds
                        + (actionEvent.DurationSeconds * 0.5f));

            Assert.That(states, Has.Count.EqualTo(1));
            Assert.That(states[0].ActorId, Is.EqualTo("mara"));
            Assert.That(states[0].Kind, Is.EqualTo(
                TurnReplayActorActionKind.Equipment));
            Assert.That(states[0].NormalizedProgress, Is.EqualTo(0.5f)
                .Within(0.001f));
            Assert.That(
                TurnReplayActorActionProjector.Project(
                    timeline,
                    actionEvent.EndSeconds),
                Is.Empty);
        }

        [Test]
        public void WindowBeginsWithActiveActorsPriorTurnAndNestsReactions()
        {
            GameplaySession session = CreateSession();
            Assert.That(session.BeginEncounter(), Is.True);
            string anchorActorId = session.ActiveActorId;

            Assert.That(TurnReplayWindowProjector.TryProject(
                session.Journal,
                anchorActorId,
                out _), Is.False);

            Assert.That(session.TryEndTurn(anchorActorId, out _), Is.True);
            string interruptedActorId = session.ActiveActorId;
            string responderId = session.InitiativeOrder.First(
                actorId => actorId != anchorActorId
                    && actorId != interruptedActorId);
            session.BeginEmergencyReaction(
                interruptedActorId,
                new[] { responderId },
                actionPointAllowance: 1);
            Assert.That(session.TryEndEmergencyTurn(
                responderId,
                out bool passCompleted,
                out _), Is.True);
            Assert.That(passCompleted, Is.True);
            session.CompleteEmergencyReaction(interruptedActorId);

            while (!string.Equals(
                session.ActiveActorId,
                anchorActorId,
                System.StringComparison.Ordinal))
            {
                Assert.That(session.TryEndTurn(
                    session.ActiveActorId,
                    out _), Is.True);
            }

            Assert.That(TurnReplayWindowProjector.TryProject(
                session.Journal,
                anchorActorId,
                out TurnReplayWindow window), Is.True);
            Assert.That(window.ActorId, Is.EqualTo(anchorActorId));
            Assert.That(window.DefaultPlayheadBoundary, Is.EqualTo(1));
            Assert.That(
                window.Segments.Select(segment => segment.ActorId),
                Is.EqualTo(session.InitiativeOrder));
            TurnReplaySegment interruptedSegment = window.Segments.Single(
                segment => segment.ActorId == interruptedActorId);
            Assert.That(interruptedSegment.Entries
                .OfType<TurnEndedJournalEntry>()
                .Count(entry =>
                    entry.Turn.Kind == GameplayTurnKind.EmergencyReaction),
                Is.EqualTo(1));
        }

        [Test]
        public void ProjectedWindowDoesNotChangeWhenJournalAdvances()
        {
            GameplaySession session = CreateSession();
            Assert.That(session.BeginEncounter(), Is.True);
            string anchorActorId = session.ActiveActorId;
            do
            {
                Assert.That(session.TryEndTurn(
                    session.ActiveActorId,
                    out _), Is.True);
            }
            while (!string.Equals(
                session.ActiveActorId,
                anchorActorId,
                System.StringComparison.Ordinal));

            Assert.That(TurnReplayWindowProjector.TryProject(
                session.Journal,
                anchorActorId,
                out TurnReplayWindow window), Is.True);
            int segmentCount = window.Segments.Count;
            int entryCount = window.Segments.Sum(segment => segment.Entries.Count);
            long endSequence = window.EndJournalSequence;
            Assert.That(window.IsAtJournalTip(session.Journal), Is.True);

            Assert.That(session.TryEndTurn(anchorActorId, out _), Is.True);

            Assert.That(window.Segments, Has.Count.EqualTo(segmentCount));
            Assert.That(
                window.Segments.Sum(segment => segment.Entries.Count),
                Is.EqualTo(entryCount));
            Assert.That(window.EndJournalSequence, Is.EqualTo(endSequence));
            Assert.That(
                window.EndJournalSequence,
                Is.LessThan(session.Journal.LastEntry.Sequence));
            Assert.That(window.IsAtJournalTip(session.Journal), Is.False);
        }

        [Test]
        public void PoseProjectionSeeksAlongRecordedMovementWithoutLiveMutation()
        {
            var origin = new GameplayActorPose(
                new GameplayPosition(0f, 0f, 0f),
                0f);
            var route = new MovementRouteRecord(
                "mara",
                origin,
                new[] { new GameplayPosition(10f, 0f, 0f) });
            var window = new TurnReplayWindow(
                "mara",
                new[]
                {
                    new TurnReplaySegment(
                        1,
                        "mara",
                        new GameplayJournalEntry[]
                        {
                            new MovementRouteCommittedJournalEntry(1, route),
                            new TurnEndedJournalEntry(
                                2,
                                new TurnEndRecord(1, "mara", "raider")),
                        }),
                });
            var finalPoses = new Dictionary<string, GameplayActorPose>
            {
                ["mara"] = new GameplayActorPose(
                    route.Destination,
                    route.FinalFacingDegrees),
            };

            GameplayActorPose start = TurnReplayPoseProjector.Project(
                window,
                finalPoses,
                0f)["mara"];
            GameplayActorPose middle = TurnReplayPoseProjector.Project(
                window,
                finalPoses,
                0.5f)["mara"];
            GameplayActorPose end = TurnReplayPoseProjector.Project(
                window,
                finalPoses,
                1f)["mara"];

            Assert.That(start.Position.X, Is.EqualTo(0f));
            Assert.That(middle.Position.X, Is.EqualTo(5f).Within(0.001f));
            Assert.That(end.Position.X, Is.EqualTo(10f));
            Assert.That(finalPoses["mara"].Position.X, Is.EqualTo(10f));
        }

        [Test]
        public void StateTimelineProjectsCanonicalSegmentBoundariesAndEndpoint()
        {
            GameplaySession session = CreateSession();
            using (var timeline = new GameplayCombatStateTimeline(
                session,
                () => GameplayCombatStateCapture.Capture(session)))
            {
                Assert.That(session.BeginEncounter(), Is.True);
                string anchorActorId = session.ActiveActorId;
                do
                {
                    Assert.That(session.TryEndTurn(
                        session.ActiveActorId,
                        out _), Is.True);
                }
                while (!string.Equals(
                    session.ActiveActorId,
                    anchorActorId,
                    System.StringComparison.Ordinal));

                Assert.That(TurnReplayWindowProjector.TryProject(
                    session.Journal,
                    anchorActorId,
                    out TurnReplayWindow replay), Is.True);
                Assert.That(TurnReplayStateWindowProjector.TryProject(
                    replay,
                    timeline,
                    out TurnReplayStateWindow states), Is.True);

                Assert.That(states.Start.JournalSequence, Is.Zero);
                Assert.That(states.SegmentEnds, Has.Count.EqualTo(
                    replay.Segments.Count));
                Assert.That(states.End.JournalSequence,
                    Is.EqualTo(replay.EndJournalSequence));
                Assert.That(TurnReplayStateWindowProjector
                    .VerifyCurrentEndpoint(states, timeline).IsVerified,
                    Is.True);
                session.SpendMovement(anchorActorId, 1f);
                Assert.That(TurnReplayStateWindowProjector
                    .VerifyCurrentEndpoint(states, timeline).IsVerified,
                    Is.False);
            }
        }

        [Test]
        public void StateTimelineIsBoundedAndStopsCapturingAfterDisposal()
        {
            GameplaySession session = CreateSession();
            var timeline = new GameplayCombatStateTimeline(
                session,
                () => GameplayCombatStateCapture.Capture(session),
                checkpointCapacity: 2);
            Assert.That(session.BeginEncounter(), Is.True);
            Assert.That(session.TryEndTurn(session.ActiveActorId, out _), Is.True);
            Assert.That(session.TryEndTurn(session.ActiveActorId, out _), Is.True);
            Assert.That(session.TryEndTurn(session.ActiveActorId, out _), Is.True);

            Assert.That(timeline.Checkpoints, Has.Count.EqualTo(2));
            long lastSequence = timeline.Checkpoints.Last().JournalSequence;
            timeline.Dispose();
            Assert.That(session.TryEndTurn(session.ActiveActorId, out _), Is.True);

            Assert.That(timeline.Checkpoints.Last().JournalSequence,
                Is.EqualTo(lastSequence));
        }

        [Test]
        public void WorldStateSamplerUsesCanonicalPersistentSegmentState()
        {
            GameplaySession session = CreateSession();
            using (var timeline = new GameplayCombatStateTimeline(
                session,
                () => GameplayCombatStateCapture.Capture(session)))
            {
                Assert.That(session.BeginEncounter(), Is.True);
                string anchorActorId = session.ActiveActorId;
                session.SpendMovement(anchorActorId, 2f);
                do
                {
                    Assert.That(session.TryEndTurn(
                        session.ActiveActorId,
                        out _), Is.True);
                }
                while (!string.Equals(
                    session.ActiveActorId,
                    anchorActorId,
                    System.StringComparison.Ordinal));
                Assert.That(TurnReplayWindowProjector.TryProject(
                    session.Journal,
                    anchorActorId,
                    out TurnReplayWindow replay), Is.True);
                Assert.That(TurnReplayStateWindowProjector.TryProject(
                    replay,
                    timeline,
                    out TurnReplayStateWindow states), Is.True);

                TurnReplayWorldStateSample during =
                    TurnReplayWorldStateSampler.Sample(states, 0.5f);
                TurnReplayWorldStateSample afterFirstTurn =
                    TurnReplayWorldStateSampler.Sample(states, 1f);

                Assert.That(
                    during.Actors[anchorActorId]
                        .TurnBudget.MovementOpportunity,
                    Is.EqualTo(8f));
                Assert.That(
                    afterFirstTurn.Actors[anchorActorId]
                        .TurnBudget.MovementOpportunity,
                    Is.EqualTo(6f));
                Assert.That(
                    session.GetActor(anchorActorId)
                        .TurnBudget.MovementOpportunity,
                    Is.EqualTo(8f));
            }
        }

        [Test]
        public void TimedSamplerAppliesAndReversesConsequencesAtEventBoundary()
        {
            GameplaySession session = CreateSession();
            using (var timeline = new GameplayCombatStateTimeline(
                session,
                () => GameplayCombatStateCapture.Capture(session)))
            {
                Assert.That(session.BeginEncounter(), Is.True);
                string anchorActorId = session.ActiveActorId;
                session.SpendMovement(anchorActorId, 2f);
                do
                {
                    Assert.That(session.TryEndTurn(
                        session.ActiveActorId,
                        out _), Is.True);
                }
                while (!string.Equals(
                    session.ActiveActorId,
                    anchorActorId,
                    System.StringComparison.Ordinal));
                Assert.That(TurnReplayWindowProjector.TryProject(
                    session.Journal,
                    anchorActorId,
                    out TurnReplayWindow replay), Is.True);
                Assert.That(TurnReplayStateWindowProjector.TryProject(
                    replay,
                    timeline,
                    out TurnReplayStateWindow states), Is.True);
                var events = new TurnReplayEventTimeline(replay);
                TurnReplayTimedEvent movement = events.Events.Single(value =>
                    value.Entry is MovementBudgetSpentJournalEntry spent
                    && spent.ActorId == anchorActorId);

                TurnReplayWorldStateSample before =
                    TurnReplayWorldStateSampler.SampleAtTime(
                        states,
                        events,
                        movement.StartSeconds);
                TurnReplayWorldStateSample after =
                    TurnReplayWorldStateSampler.SampleAtTime(
                        states,
                        events,
                        movement.EndSeconds);
                TurnReplayWorldStateSample rewound =
                    TurnReplayWorldStateSampler.SampleAtTime(
                        states,
                        events,
                        movement.StartSeconds);

                Assert.That(before.Actors[anchorActorId]
                    .TurnBudget.MovementOpportunity, Is.EqualTo(8f));
                Assert.That(after.Actors[anchorActorId]
                    .TurnBudget.MovementOpportunity, Is.EqualTo(6f));
                Assert.That(rewound.Actors[anchorActorId]
                    .TurnBudget.MovementOpportunity, Is.EqualTo(8f));
                Assert.That(session.GetActor(anchorActorId)
                    .TurnBudget.MovementOpportunity, Is.EqualTo(8f));
            }
        }

        [Test]
        public void WorldStateSamplerInterpolatesRecordedProjectileFlight()
        {
            GameplaySession session = CreateProjectileSession();
            var destructibles = new DestructiblePropSession(
                System.Array.Empty<DestructiblePropDefinition>());
            var projectiles = new GameplayProjectileSession(
                session,
                new ClearProjectileQuery(),
                new GameplayBlastConsequenceResolver(session, destructibles));
            using (var timeline = new GameplayCombatStateTimeline(
                session,
                () => GameplayCombatStateCapture.Capture(
                    session,
                    destructibles,
                    projectiles: projectiles)))
            {
                Assert.That(session.BeginEncounter(), Is.True);
                string anchorActorId = session.ActiveActorId;
                Assert.That(projectiles.TryLaunch(
                    anchorActorId,
                    "target",
                    new GameplayPosition(0f, 0f, 10f),
                    out _,
                    out _), Is.True);
                projectiles.Advance("projectile.1", 1f);
                do
                {
                    Assert.That(session.TryEndTurn(
                        session.ActiveActorId,
                        out _), Is.True);
                }
                while (!string.Equals(
                    session.ActiveActorId,
                    anchorActorId,
                    System.StringComparison.Ordinal));
                Assert.That(TurnReplayWindowProjector.TryProject(
                    session.Journal,
                    anchorActorId,
                    out TurnReplayWindow replay), Is.True);
                Assert.That(TurnReplayStateWindowProjector.TryProject(
                    replay,
                    timeline,
                    out TurnReplayStateWindow states), Is.True);

                TurnReplayWorldStateSample sample =
                    TurnReplayWorldStateSampler.Sample(states, 0.5f);

                Assert.That(sample.Projectiles, Has.Count.EqualTo(1));
                Assert.That(sample.Projectiles[0].Status,
                    Is.EqualTo(ProjectileFlightStatus.InFlight));
                Assert.That(sample.Projectiles[0].Position.Z,
                    Is.EqualTo(2f).Within(0.001f));
                Assert.That(projectiles.GetProjectile("projectile.1").Position.Z,
                    Is.EqualTo(4f).Within(0.001f));
            }
        }

        private static GameplaySession CreateSession()
        {
            return new GameplaySession(new ScenarioDefinition(
                "turn-replay-test",
                new ScenarioTimingDefinition(1f),
                new[]
                {
                    CreateActor("mara", 14),
                    CreateActor("raider", 10),
                    CreateActor("guard", 6),
                },
                System.Array.Empty<ScenarioObjectiveDefinition>()));
        }

        private static ScenarioActorDefinition CreateActor(
            string actorId,
            int initiative)
        {
            return new ScenarioActorDefinition(
                actorId,
                initiative,
                new GameplayActorPose(
                    new GameplayPosition(initiative, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f));
        }

        private static GameplaySession CreateProjectileSession()
        {
            var projectile = new ProjectileFlightDefinition(
                "projectile.replay",
                speedPerTurn: 4f,
                radius: 0.1f,
                maximumRange: 12f);
            var player = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f),
                new AttackDefinition(
                    "attack.replay-projectile",
                    "Replay projectile",
                    new ActionCost(2, 0f, ActionMobility.Set),
                    woundMovementPenalty: 2f,
                    projectile: projectile));
            var target = new ScenarioActorDefinition(
                "target",
                0,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 10f), 180f),
                new TurnBudget(0, 8f));
            return new GameplaySession(new ScenarioDefinition(
                "projectile-replay-test",
                new ScenarioTimingDefinition(1f),
                new[] { player, target },
                System.Array.Empty<ScenarioObjectiveDefinition>()));
        }

        private sealed class ClearProjectileQuery : IProjectileSegmentQuery
        {
            public ProjectileSegmentQueryResult Query(ProjectileSegmentQuery query) =>
                ProjectileSegmentQueryResult.Clear(query.Flight.Launch.Sequence);
        }
    }
}
