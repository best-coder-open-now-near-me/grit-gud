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
    }
}
