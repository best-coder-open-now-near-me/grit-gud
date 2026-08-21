using System;
using System.Linq;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayDroneTransitionTests
    {
        [Test]
        public void DroneMoveSpendsControllerApAndReplaysDeterministically()
        {
            GameplayCombatStateSnapshot initial = CreateState(
                activeActorId: "controller");
            var movement = new DroneMoveRecord(
                "controller",
                "drone.scout",
                new GameplayPosition(1f, 2f, 1f),
                new GameplayPosition(4f, 3f, 1f),
                90f,
                new ActionCost(1, 0f, ActionMobility.Mobile),
                new TurnBudget(4, 8f),
                new TurnBudget(3, 8f));
            GameplaySemanticTransition transition = CreateTransition(
                initial,
                movement);
            var reducer = new GameplayWorldTransitionReducer();

            GameplayReductionResult first = reducer.Reduce(initial, transition);
            GameplayReductionResult replay = reducer.Reduce(initial, transition);

            Assert.That(first.Resulting.Session.GetActor("controller")
                .TurnBudget.ActionPoints, Is.EqualTo(3));
            Assert.That(first.Resulting.Drones[0].Position,
                Is.EqualTo(new GameplayPosition(4f, 3f, 1f)));
            Assert.That(first.Resulting.Drones[0].FacingDegrees, Is.EqualTo(90f));
            Assert.That(replay.Resulting.CanonicalHash,
                Is.EqualTo(first.Resulting.CanonicalHash));

        }

        [Test]
        public void ActorDroneAttackProjectsDestinationAndTimedDischarge()
        {
            GameplayCombatStateSnapshot initial = CreateState(
                "controller",
                controllerEquippedItemId: "weapon.rifle");
            DroneSnapshot drone = initial.Drones[0];
            var attack = new AttackDefinition(
                "attack.rifle",
                "Rifle",
                new ActionCost(1, 0f, ActionMobility.Set),
                woundMovementPenalty: 1f,
                accuracyDecay: AccuracyDecayDefinition.None,
                directVehicleIntegrityDamage: 1f);
            var exposure = new DroneExposureSnapshot(
                "controller",
                drone.DroneId,
                visibleSampleCount: 0,
                totalSampleCount: 1);
            ActorDroneAttackRecord action = DroneDirectAttackRules.Resolve(
                sequence: 1L,
                resolutionSeed: 1u,
                attackerId: "controller",
                attack: attack,
                previousBudget: new TurnBudget(4, 8f),
                exposure: exposure,
                distance: 1f,
                target: drone);
            var payload = new GameplayActorDroneAttackTransitionPayload(
                attack,
                action);
            var identity = new GameplayTransitionIdentity(
                1L,
                GameplaySemanticCapability.DirectAttack.ToString(),
                "controller",
                drone.DroneId);
            var transition = new GameplaySemanticTransition(
                identity,
                initial.CanonicalHash,
                payload);
            var reduction = new GameplayReductionResult(
                initial,
                initial,
                new GameplayDomainEvent[]
                {
                    new GameplayTransitionReducedEvent(
                        identity,
                        drone.DroneId,
                        action),
                });
            GameplaySemanticReplayFrame frame = CreateFrame(
                transition,
                reduction,
                action);

            TurnReplayActorActionState actionState =
                TurnReplayActorActionProjector.Project(frame, 0.5f)
                    .Single(value => value.ActorId == "controller");
            ReplayCombatPresentationEvent presentationEvent =
                ReplayCombatPresentationEventProjector.Project(frame)
                    .Single(value => value.Kind ==
                        ReplayCombatPresentationEventKind.WeaponDischarge);

            Assert.That(actionState.Destination, Is.EqualTo(drone.Position));
            Assert.That(
                actionState.EventNormalizedTime,
                Is.EqualTo(GameplaySemanticReplayPresentationTiming
                    .ActionResolutionProgress));
            Assert.That(
                presentationEvent.ShooterKind,
                Is.EqualTo(ReplayCombatPresentationSubjectKind.Actor));
            Assert.That(
                presentationEvent.TargetKind,
                Is.EqualTo(ReplayCombatPresentationSubjectKind.Drone));
            Assert.That(
                presentationEvent.PresentationId,
                Is.EqualTo("weapon.rifle"));
            Assert.That(presentationEvent.Destination, Is.EqualTo(drone.Position));
        }

        [Test]
        public void DroneMoveRejectsInactiveControllerAndDestroyedDrone()
        {
            var movement = new DroneMoveRecord(
                "controller",
                "drone.scout",
                new GameplayPosition(1f, 2f, 1f),
                new GameplayPosition(2f, 2f, 1f),
                0f,
                new ActionCost(1, 0f, ActionMobility.Mobile),
                new TurnBudget(4, 8f),
                new TurnBudget(3, 8f));
            GameplayCombatStateSnapshot inactive = CreateState("other");
            GameplayCombatStateSnapshot destroyed = CreateState(
                "controller",
                remainingIntegrity: 0f);
            var reducer = new GameplayWorldTransitionReducer();

            Assert.Throws<InvalidOperationException>(() => reducer.Reduce(
                inactive,
                CreateTransition(inactive, movement)));
            Assert.Throws<InvalidOperationException>(() => reducer.Reduce(
                destroyed,
                CreateTransition(destroyed, movement)));
        }

        [Test]
        public void DroneAttackSpendsControllerApAndAppliesFrozenActorWound()
        {
            GameplayCombatStateSnapshot initial = CreateState("controller");
            GameplayActorSnapshot target = initial.Session.GetActor("other");
            DroneDefinition drone = initial.Drones[0].Definition;
            var exposure = new TargetExposureSnapshot(
                drone.Id,
                target.ActorId,
                new[]
                {
                    new TargetRegionExposure(TargetRegionId.Torso, 1, 1),
                });
            AttackResolutionRecord resolution = AttackResolutionRules.Resolve(
                1L,
                7u,
                exposure,
                drone.Attack.AccuracyDecay,
                1f,
                target.Wounds,
                drone.Attack.WoundMovementPenalty);
            var action = new DroneAttackRecord(
                "controller",
                drone.Id,
                "other",
                GameplaySemanticSubjectKind.Actor.ToString(),
                drone.Attack.TurnCost,
                new TurnBudget(4, 8f),
                new TurnBudget(3, 8f),
                resolution);
            var payload = new GameplayDroneAttackTransitionPayload(
                GameplaySemanticSubjectKind.Actor,
                drone.Attack,
                action);
            var transition = new GameplaySemanticTransition(
                new GameplayTransitionIdentity(
                    1L,
                    GameplaySemanticCapability.DirectAttack.ToString(),
                    "controller",
                    "other"),
                initial.CanonicalHash,
                payload);
            var reducer = new GameplayDroneAttackTransitionReducer();

            GameplayReductionResult first = reducer.Reduce(initial, transition);
            GameplayReductionResult replay = reducer.Reduce(initial, transition);

            Assert.That(first.Resulting.Session.GetActor("controller")
                .TurnBudget.ActionPoints, Is.EqualTo(3));
            Assert.That(first.Resulting.Session.GetActor("other")
                .Wounds.TorsoWounds, Is.EqualTo(1));
            Assert.That(replay.Resulting.CanonicalHash,
                Is.EqualTo(first.Resulting.CanonicalHash));

            GameplaySemanticReplayFrame frame = CreateFrame(
                transition,
                first,
                action);
            ReplayCombatPresentationEvent presentationEvent =
                ReplayCombatPresentationEventProjector.Project(frame)
                    .Single(value => value.Kind ==
                        ReplayCombatPresentationEventKind.WeaponDischarge);
            Assert.That(
                presentationEvent.ShooterKind,
                Is.EqualTo(ReplayCombatPresentationSubjectKind.Drone));
            Assert.That(
                presentationEvent.TargetKind,
                Is.EqualTo(ReplayCombatPresentationSubjectKind.Actor));
            Assert.That(presentationEvent.ShooterId, Is.EqualTo(drone.Id));
            Assert.That(presentationEvent.TargetId, Is.EqualTo("other"));
            Assert.That(
                presentationEvent.PresentationId,
                Is.EqualTo(drone.Attack.ActionId));
            Assert.That(presentationEvent.Origin, Is.EqualTo(
                initial.Drones[0].Position));
            Assert.That(
                presentationEvent.Destination,
                Is.EqualTo(new GameplayPosition(0f, 1f, 0f)));
        }

        private static GameplaySemanticTransition CreateTransition(
            GameplayCombatStateSnapshot state,
            DroneMoveRecord movement)
        {
            var payload = new GameplayDroneMoveTransitionPayload(movement);
            return new GameplaySemanticTransition(
                new GameplayTransitionIdentity(
                    1L,
                    GameplaySemanticCapability.Move.ToString(),
                    movement.ControllerActorId,
                    movement.DroneId),
                state.CanonicalHash,
                payload);
        }

        private static GameplaySemanticReplayFrame CreateFrame(
            GameplaySemanticTransition transition,
            GameplayReductionResult reduction,
            object semanticRecord)
        {
            var step = new GameplayTrajectoryStep(
                transition,
                reduction.Resulting.CanonicalHash,
                reduction.DomainEvents.Select(value => value.EventType));
            return new GameplaySemanticReplayFrame(
                index: 0,
                step: step,
                reduction: reduction,
                semanticRecord: semanticRecord);
        }

        private static GameplayCombatStateSnapshot CreateState(
            string activeActorId,
            float remainingIntegrity = 6f,
            string controllerEquippedItemId = null)
        {
            GameplayActorSnapshot controller = CreateActor(
                "controller",
                controllerEquippedItemId);
            GameplayActorSnapshot other = CreateActor("other");
            var session = new GameplaySessionStateSnapshot(
                "drone-transition-test",
                GameplaySessionMode.TurnBased,
                GameplaySessionOperation.None,
                TurnModeContext.InitiatedEncounter,
                encounterActive: true,
                encounterCompletionRequested: false,
                activeActorId,
                GameplayTurnPhase.Normal,
                new[] { controller, other },
                new[] { "controller", "other" },
                Array.Empty<GameplayObjectiveSnapshot>(),
                Array.Empty<string>(),
                -1,
                string.Empty,
                0L,
                0L,
                0L,
                encounterState: new GameplayEncounterStateSnapshot(
                    encounterParticipantIds: new[] { "controller", "other" }));
            DroneDefinition definition = CreateDroneDefinition();
            return new GameplayCombatStateSnapshot(
                session,
                coverage: GameplayCombatStateCoverage.Session
                    | GameplayCombatStateCoverage.Drones,
                drones: new[]
                {
                    new DroneSnapshot(
                        definition,
                        definition.StartingPosition,
                        definition.StartingFacingDegrees,
                        remainingIntegrity),
                });
        }

        private static DroneDefinition CreateDroneDefinition() => new DroneDefinition(
            "drone.scout",
            "controller",
            new GameplayPosition(1f, 2f, 1f),
            0f,
            maximumIntegrity: 6f,
            maximumMoveDistance: 5f,
            moveCost: new ActionCost(1, 0f, ActionMobility.Mobile),
            sensor: new DroneSensorDefinition(14f, 120f),
            attack: new AttackDefinition(
                "attack.drone.light",
                "Drone light weapon",
                new ActionCost(1, 0f, ActionMobility.Set),
                woundMovementPenalty: 1f,
                accuracyDecay: AccuracyDecayDefinition.None));

        private static GameplayActorSnapshot CreateActor(
            string actorId,
            string equippedItemId = null) =>
            new GameplayActorSnapshot(
                actorId,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f),
                new ActorWoundSnapshot(actorId, 0, 0f),
                equippedItemId,
                equipmentEffects: EquipmentEffectSet.None,
                maximumWounds: 3,
                actionPointEconomy: new TurnActionPointEconomy(4, 4, 6),
                turnMovementAllowance: 8f);
    }
}
