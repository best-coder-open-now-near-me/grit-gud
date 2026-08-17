using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayCombatStateTests
    {
        [Test]
        public void CanonicalHashIgnoresInputCollectionOrder()
        {
            GameplayActorSnapshot alpha = CreateActor("alpha", 1f, 4);
            GameplayActorSnapshot bravo = CreateActor("bravo", 2f, 3);

            GameplayCombatStateSnapshot first = CreateState(alpha, bravo);
            GameplayCombatStateSnapshot second = CreateState(bravo, alpha);

            Assert.That(second.CanonicalHash, Is.EqualTo(first.CanonicalHash));
            Assert.That(GameplayCombatStateDiffer.Compare(first, second), Is.Empty);
        }

        [Test]
        public void StructuredDifferenceNamesChangedAuthoritativeField()
        {
            GameplayCombatStateSnapshot expected = CreateState(
                CreateActor("alpha", 1f, 4));
            GameplayCombatStateSnapshot actual = CreateState(
                CreateActor("alpha", 1f, 3));

            IReadOnlyList<GameplayStateDifference> differences =
                GameplayCombatStateDiffer.Compare(expected, actual);

            Assert.That(differences, Has.Count.EqualTo(1));
            Assert.That(differences[0].Path, Is.EqualTo("actor.alpha.ap"));
            Assert.That(differences[0].Expected, Is.EqualTo("4"));
            Assert.That(differences[0].Actual, Is.EqualTo("3"));
        }

        [Test]
        public void CanonicalStateIncludesExactActorPinEvidence()
        {
            GameplayActorSnapshot unpinned = CreateActor("alpha", 1f, 4);
            var contact = new DisplacementContactEvidence(
                "alpha",
                new GameplayPosition(1f, 0.5f, 0f),
                new GameplayPosition(0f, 1f, 0f),
                0.1f);
            GameplayActorSnapshot pinned = CreateActor(
                "alpha",
                1f,
                4,
                new ActorPinState("alpha", "crate", 7, contact));

            GameplayCombatStateSnapshot before = CreateState(unpinned);
            GameplayCombatStateSnapshot after = CreateState(pinned);
            IReadOnlyList<GameplayStateDifference> differences =
                GameplayCombatStateDiffer.Compare(before, after);

            Assert.That(after.CanonicalHash, Is.Not.EqualTo(before.CanonicalHash));
            Assert.That(differences,
                Has.Some.Property(nameof(GameplayStateDifference.Path))
                    .EqualTo("actor.alpha.pin.active"));
            Assert.That(differences,
                Has.Some.Property(nameof(GameplayStateDifference.Path))
                    .EqualTo("actor.alpha.pin.prop"));
            Assert.That(differences,
                Has.Some.Property(nameof(GameplayStateDifference.Path))
                    .EqualTo("actor.alpha.pin.contact.depth"));
        }

        [Test]
        public void CommitRejectsStalePreparedStateBeforeMutation()
        {
            GameplayCombatStateSnapshot previous = CreateState(
                CreateActor("alpha", 1f, 4));
            GameplayCombatStateSnapshot predicted = CreateState(
                CreateActor("alpha", 1f, 3));
            GameplayCombatStateSnapshot current = CreateState(
                CreateActor("alpha", 2f, 4));
            var prepared = new GameplayPreparedTransition<string>(
                "spend-action", previous, predicted);
            bool committed = false;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => GameplayTransitionCoordinator.Commit(
                    prepared,
                    () => current,
                    _ => committed = true));

            Assert.That(exception.Message, Does.Contain("actor.alpha.position"));
            Assert.That(committed, Is.False);
        }

        [Test]
        public void CommitReportsPredictionDivergenceWithoutHidingActualState()
        {
            GameplayCombatStateSnapshot previous = CreateState(
                CreateActor("alpha", 1f, 4));
            GameplayCombatStateSnapshot predicted = CreateState(
                CreateActor("alpha", 1f, 3));
            GameplayCombatStateSnapshot actual = CreateState(
                CreateActor("alpha", 1f, 2));
            GameplayCombatStateSnapshot current = previous;
            var prepared = new GameplayPreparedTransition<string>(
                "spend-action", previous, predicted);

            GameplayTransitionCommitResult result =
                GameplayTransitionCoordinator.Commit(
                    prepared,
                    () => current,
                    _ => current = actual);

            Assert.That(result.MatchesPrediction, Is.False);
            Assert.That(result.Actual, Is.SameAs(actual));
            Assert.That(result.Differences, Has.Count.EqualTo(1));
            Assert.That(result.Differences[0].Path, Is.EqualTo("actor.alpha.ap"));
        }

        [Test]
        public void ReplayVerificationRequiresMatchingStateAndValidInvariants()
        {
            GameplayCombatStateSnapshot expected = CreateState(
                CreateActor("alpha", 1f, 4));

            var result = new GameplayReplayVerificationResult(expected, expected);

            Assert.That(result.IsVerified, Is.True);
            Assert.That(result.Differences, Is.Empty);
            Assert.That(result.InvariantViolations, Is.Empty);
        }

        private static GameplayCombatStateSnapshot CreateState(
            params GameplayActorSnapshot[] actors)
        {
            var session = new GameplaySessionStateSnapshot(
                "combat-state-test",
                GameplaySessionMode.TurnBased,
                GameplaySessionOperation.None,
                TurnModeContext.InitiatedEncounter,
                encounterActive: true,
                encounterCompletionRequested: false,
                activeActorId: "alpha",
                turnPhase: GameplayTurnPhase.Normal,
                actors: actors,
                initiativeOrder: ActorIds(actors),
                objectives: Array.Empty<GameplayObjectiveSnapshot>(),
                emergencyResponders: Array.Empty<string>(),
                emergencyResponderIndex: -1,
                emergencyResumeActorId: string.Empty,
                lastActionSequence: 0,
                lastTurnSequence: 0,
                journalSequence: 0);
            return new GameplayCombatStateSnapshot(session);
        }

        private static IEnumerable<string> ActorIds(
            IEnumerable<GameplayActorSnapshot> actors)
        {
            var ids = new List<string>();
            foreach (GameplayActorSnapshot actor in actors)
                ids.Add(actor.ActorId);
            ids.Sort(StringComparer.Ordinal);
            return ids;
        }

        private static GameplayActorSnapshot CreateActor(
            string actorId,
            float positionX,
            int actionPoints,
            ActorPinState pinState = null)
        {
            return new GameplayActorSnapshot(
                actorId,
                new GameplayActorPose(
                    new GameplayPosition(positionX, 0f, 0f),
                    0f),
                new TurnBudget(actionPoints, 8f),
                new ActorWoundSnapshot(actorId, 0, 0f),
                equippedItemId: null,
                equipmentEffects: EquipmentEffectSet.None,
                maximumWounds: int.MaxValue,
                inventory: null,
                turnActionPointAllowance: 4,
                turnMovementAllowance: 8f,
                pinState: pinState);
        }
    }
}
