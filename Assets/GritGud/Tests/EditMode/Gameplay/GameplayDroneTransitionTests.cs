using System;
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

        private static GameplayCombatStateSnapshot CreateState(
            string activeActorId,
            float remainingIntegrity = 6f)
        {
            GameplayActorSnapshot controller = CreateActor("controller");
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

        private static GameplayActorSnapshot CreateActor(string actorId) =>
            new GameplayActorSnapshot(
                actorId,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f),
                new ActorWoundSnapshot(actorId, 0, 0f),
                equippedItemId: null,
                equipmentEffects: EquipmentEffectSet.None,
                maximumWounds: 3,
                actionPointEconomy: new TurnActionPointEconomy(4, 4, 6),
                turnMovementAllowance: 8f);
    }
}
