using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class UnityTacticalContextQueryTests
    {
        [Test]
        public void LiveAdapterCapturesBidirectionalVisibilityAtStateRevision()
        {
            GameplayCombatStateSnapshot state = CreateState();
            var query = new UnityTacticalContextQuery(
                (observer, target) => new FixedExposureQuery(
                    observer == "actor.player"
                    && target == "actor.rifleman"),
                () => state.Session.Revision);

            TacticalContextSnapshot context = query.Capture(
                state,
                new GameplayTacticalContextRequest(
                    ActorAttackProfile(),
                    "actor.player",
                    new GameplaySubjectReference(
                        GameplaySemanticSubjectKind.Actor,
                        "actor.rifleman"),
                    soundSignature: 0.8f));

            Assert.That(
                context.Visibility,
                Is.EqualTo(TacticalVisibilityRelation.AttackerOnly));
            Assert.That(
                context.TargetAwareness,
                Is.EqualTo(TacticalAwarenessBand.Unaware));
            Assert.That(context.StateRevision, Is.EqualTo(7));
            Assert.That(
                context.ExposureBand,
                Is.EqualTo(TacticalExposureBand.Exposed));
        }

        [Test]
        public void LiveAdapterRejectsEvidenceFromAnotherStateRevision()
        {
            GameplayCombatStateSnapshot state = CreateState();
            var query = new UnityTacticalContextQuery(
                (_, _) => new FixedExposureQuery(visible: true),
                () => state.Session.Revision + 1L);

            Assert.Throws<InvalidOperationException>(() => query.Capture(
                state,
                new GameplayTacticalContextRequest(
                    ActorAttackProfile(),
                    "actor.player",
                    new GameplaySubjectReference(
                        GameplaySemanticSubjectKind.Actor,
                        "actor.rifleman"),
                    soundSignature: 0.8f)));
        }

        private static GameplayCapabilityProfile ActorAttackProfile() => new(
            GameplaySemanticCapability.DirectAttack,
            semanticVersion: 1,
            new[]
            {
                new GameplayCapabilityTrait("subject", "Actor"),
            });

        private static GameplayCombatStateSnapshot CreateState()
        {
            GameplayActorSnapshot player = CreateActor(
                "actor.player",
                new GameplayPosition(0f, 0f, 0f),
                ActorStance.Crouched);
            GameplayActorSnapshot rifleman = CreateActor(
                "actor.rifleman",
                new GameplayPosition(10f, 0f, 0f),
                ActorStance.Standing);
            var actors = new[] { player, rifleman };
            var ids = new[] { "actor.player", "actor.rifleman" };
            var awareness = new[]
            {
                new EnemyAwarenessSnapshot(
                    "actor.rifleman",
                    EncounterAwarenessState.Unaware,
                    suspicion: 0),
            };
            var session = new GameplaySessionStateSnapshot(
                "tactical-context-live-test",
                GameplaySessionMode.TurnBased,
                GameplaySessionOperation.None,
                TurnModeContext.InitiatedEncounter,
                encounterActive: true,
                encounterCompletionRequested: false,
                activeActorId: "actor.player",
                turnPhase: GameplayTurnPhase.Normal,
                actors,
                initiativeOrder: ids,
                objectives: Array.Empty<GameplayObjectiveSnapshot>(),
                emergencyResponders: Array.Empty<string>(),
                emergencyResponderIndex: -1,
                emergencyResumeActorId: string.Empty,
                lastActionSequence: 0,
                lastTurnSequence: 0,
                journalSequence: 0,
                revision: 7,
                encounterState: new GameplayEncounterStateSnapshot(
                    awareness,
                    ids));
            return new GameplayCombatStateSnapshot(session);
        }

        private static GameplayActorSnapshot CreateActor(
            string actorId,
            GameplayPosition position,
            ActorStance stance) => new(
                actorId,
                new GameplayActorPose(position, 0f, stance),
                new TurnBudget(4, 8f),
                new ActorWoundSnapshot(actorId, 0, 0f),
                equippedItemId: null,
                equipmentEffects: EquipmentEffectSet.None,
                maximumWounds: int.MaxValue,
                inventory: null,
                actionPointEconomy: new TurnActionPointEconomy(4, 4, 6),
                turnMovementAllowance: 8f);

        private sealed class FixedExposureQuery : ITargetExposureQuery
        {
            private readonly bool visible;

            public FixedExposureQuery(bool visible)
            {
                this.visible = visible;
            }

            public TargetExposureSnapshot Capture(
                string observerId,
                GameplayPosition observerOrigin,
                string targetId,
                IReadOnlyList<TargetRegionSample> targetRegions) => new(
                    observerId,
                    targetId,
                    new[]
                    {
                        new TargetRegionExposure(
                            TargetRegionId.Head,
                            visibleSampleCount: visible ? 1 : 0,
                            totalSampleCount: 1),
                    });
        }
    }
}
