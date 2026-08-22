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
        public void SummonCreatesRuntimePartnerAndKeepsSummonerInitiativeSlot()
        {
            GameplayCombatStateSnapshot initial = CreateState(
                activeActorId: "controller",
                includeDrone: false);
            DroneArchetypeDefinition archetype =
                CreateDroneArchetypeDefinition();
            var ability = new DroneSummonAbilityDefinition(
                "ability.summon-drone",
                archetype.ArchetypeId,
                new ActionCost(1, 0f, ActionMobility.Mobile),
                maximumSpawnDistance: 6f,
                maximumActiveInstances: 1,
                durationTurns: null,
                spawnHeight: 2f);
            GameplayActorSnapshot summoner = initial.Session.GetActor(
                "controller");
            var summon = new SummonDroneRecord(
                sequence: 1L,
                summoner.ActorId,
                ability,
                archetype,
                new GameplayPosition(2f, 2f, 0f),
                spawnFacingDegrees: 90f,
                summoner.TurnBudget,
                summoner.TurnBudget.SpendAction(ability.SummonCost));
            var transition = new GameplaySemanticTransition(
                new GameplayTransitionIdentity(
                    1L,
                    GameplaySemanticCapability.SummonDrone.ToString(),
                    summoner.ActorId,
                    ability.AbilityId),
                initial.CanonicalHash,
                new GameplaySummonDroneTransitionPayload(summon));

            GameplayReductionResult result =
                new GameplayDroneLifecycleTransitionReducer().Reduce(
                    initial,
                    transition);

            Assert.That(initial.Drones, Is.Empty);
            Assert.That(result.Resulting.Drones, Has.Count.EqualTo(1));
            Assert.That(result.Resulting.Drones[0].DroneId,
                Is.EqualTo("drone:controller:1"));
            Assert.That(result.Resulting.Drones[0].SummonerActorId,
                Is.EqualTo(summoner.ActorId));
            Assert.That(result.Resulting.Session.GetActor(summoner.ActorId)
                .TurnBudget.ActionPoints, Is.EqualTo(3));
            Assert.That(result.Resulting.Session.InitiativeOrder,
                Is.EqualTo(new[] { "controller", "other" }));
        }

        [Test]
        public void DroneMoveSpendsSummonerApAndReplaysDeterministically()
        {
            GameplayCombatStateSnapshot initial = CreateState(
                activeActorId: "controller");
            var movement = new DroneMoveRecord(
                "controller",
                "drone:controller:1",
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
        public void SummonerAndDroneSpendOneSharedTurnBudget()
        {
            GameplayCombatStateSnapshot initial = CreateState(
                activeActorId: "controller");
            GameplayActorSnapshot summoner = initial.Session.GetActor(
                "controller");
            var actorRoute = new MovementRouteRecord(
                summoner.ActorId,
                summoner.Pose,
                summoner.TurnBudget,
                new[]
                {
                    new MovementRouteSegmentRecord(
                        summoner.Pose.Position,
                        new GameplayPosition(1f, 0f, 0f),
                        movementCost: 1f,
                        actionPointCost: 1),
                });
            var actorTransition = new GameplaySemanticTransition(
                new GameplayTransitionIdentity(
                    1L,
                    GameplaySemanticCapability.Move.ToString(),
                    summoner.ActorId,
                    summoner.ActorId),
                initial.CanonicalHash,
                new GameplayMoveTransitionPayload(
                    GameplayCapabilityProfiles.GroundedMove(),
                    actorRoute));
            GameplayReductionResult afterSummoner =
                new GameplayCoreTransitionReducer().Reduce(
                    initial,
                    actorTransition);
            TurnBudget sharedBudget = afterSummoner.Resulting.Session
                .GetActor(summoner.ActorId)
                .TurnBudget;
            SummonedDroneSnapshot drone = afterSummoner.Resulting.Drones[0];
            var droneMove = new DroneMoveRecord(
                summoner.ActorId,
                drone.DroneId,
                drone.Position,
                new GameplayPosition(2f, 2f, 1f),
                0f,
                drone.Definition.MoveCost,
                sharedBudget,
                sharedBudget.SpendAction(drone.Definition.MoveCost));
            var droneTransition = new GameplaySemanticTransition(
                new GameplayTransitionIdentity(
                    2L,
                    GameplaySemanticCapability.Move.ToString(),
                    summoner.ActorId,
                    drone.DroneId),
                afterSummoner.Resulting.CanonicalHash,
                new GameplayDroneMoveTransitionPayload(droneMove));

            GameplayReductionResult afterDrone =
                new GameplayWorldTransitionReducer().Reduce(
                    afterSummoner.Resulting,
                    droneTransition);

            DroneTurnPartnership partnership = drone.TurnPartnership;
            Assert.That(partnership.SharedBudgetActorId,
                Is.EqualTo(summoner.ActorId));
            Assert.That(partnership.PoolingPolicy,
                Is.EqualTo(DroneTurnPoolingPolicy.SharedSummonerBudget));
            Assert.That(sharedBudget.ActionPoints, Is.EqualTo(3));
            Assert.That(afterDrone.Resulting.Session
                .GetActor(summoner.ActorId)
                .TurnBudget.ActionPoints, Is.EqualTo(2));
            Assert.That(afterDrone.Resulting.Session.InitiativeOrder,
                Is.EqualTo(new[] { "controller", "other" }));
        }

        [Test]
        public void ActorDroneAttackProjectsDestinationAndTimedDischarge()
        {
            GameplayCombatStateSnapshot initial = CreateState(
                "controller",
                controllerEquippedItemId: "weapon.rifle");
            SummonedDroneSnapshot drone = initial.Drones[0];
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
        public void DroneMoveRejectsInactiveSummonerAndDestroyedDrone()
        {
            var movement = new DroneMoveRecord(
                "controller",
                "drone:controller:1",
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
            GameplayCombatStateSnapshot incapacitated = CreateState(
                "controller",
                controllerIncapacitated: true);
            var reducer = new GameplayWorldTransitionReducer();

            Assert.Throws<InvalidOperationException>(() => reducer.Reduce(
                inactive,
                CreateTransition(inactive, movement)));
            Assert.Throws<InvalidOperationException>(() => reducer.Reduce(
                destroyed,
                CreateTransition(destroyed, movement)));
            Assert.Throws<InvalidOperationException>(() => reducer.Reduce(
                incapacitated,
                CreateTransition(incapacitated, movement)));
            Assert.That(incapacitated.Drones[0].IsVisible, Is.True,
                "An incapacitated summoner leaves the drone in the world.");
            Assert.That(incapacitated.Session.InitiativeOrder,
                Does.Not.Contain(incapacitated.Drones[0].DroneId));
        }

        [Test]
        public void DroneAttackSpendsSummonerApAndAppliesFrozenActorWound()
        {
            GameplayCombatStateSnapshot initial = CreateState("controller");
            GameplayActorSnapshot target = initial.Session.GetActor("other");
            DroneArchetypeDefinition drone = initial.Drones[0].Definition;
            var exposure = new TargetExposureSnapshot(
                initial.Drones[0].DroneId,
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
                initial.Drones[0].DroneId,
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
            Assert.That(presentationEvent.ShooterId,
                Is.EqualTo(initial.Drones[0].DroneId));
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
                    movement.SummonerActorId,
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
            string controllerEquippedItemId = null,
            bool includeDrone = true,
            bool controllerIncapacitated = false)
        {
            GameplayActorSnapshot controller = CreateActor(
                "controller",
                controllerEquippedItemId,
                controllerIncapacitated
                    ? ActorLifeState.Incapacitated
                    : ActorLifeState.Active);
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
            DroneArchetypeDefinition definition = CreateDroneArchetypeDefinition();
            var trajectory = remainingIntegrity > 0f
                ? null
                : new DroneCrashTrajectoryRecord(
                    new GameplayPosition(1f, 2f, 1f),
                    new GameplayPosition(1f, 0f, 1f),
                    disabledTransitionSequence: 1L);
            SummonedDroneSnapshot[] snapshots = includeDrone
                ? new[]
                {
                    new SummonedDroneSnapshot(
                        definition,
                        "drone:controller:1",
                        "ability.summon-drone",
                        new DroneTurnPartnership("controller"),
                        new GameplayPosition(1f, 2f, 1f),
                        0f,
                        remainingIntegrity,
                        remainingIntegrity > 0f
                            ? SummonLifecycleState.Active
                            : SummonLifecycleState.Destroyed,
                        crashTrajectory: trajectory),
                }
                : Array.Empty<SummonedDroneSnapshot>();
            return new GameplayCombatStateSnapshot(
                session,
                coverage: GameplayCombatStateCoverage.Session
                    | GameplayCombatStateCoverage.Drones,
                drones: snapshots);
        }

        private static DroneArchetypeDefinition
            CreateDroneArchetypeDefinition() => new DroneArchetypeDefinition(
                "drone.scout",
                maximumIntegrity: 6f,
                maximumMoveDistance: 5f,
                moveCost: new ActionCost(1, 0f, ActionMobility.Mobile),
                sensor: new DroneSensorDefinition(14f, 120f),
                attack: new AttackDefinition(
                    "attack.drone.light",
                    "Drone light weapon",
                    new ActionCost(1, 0f, ActionMobility.Set),
                    woundMovementPenalty: 1f,
                    accuracyDecay: AccuracyDecayDefinition.None),
                presentationId: "presentation.drone.scout",
                crash: new DroneCrashDefinition(
                    impactRadius: 2f,
                    injuryMovementPenalty: 0.5f,
                    destructibleIntegrityDamage: 1f,
                    maximumActionPointReduction: 1,
                    maximumDriftDistance: 2f,
                    impactPlaybackSeconds: 0.75f));

        private static GameplayActorSnapshot CreateActor(
            string actorId,
            string equippedItemId = null,
            ActorLifeState lifeState = ActorLifeState.Active) =>
            new GameplayActorSnapshot(
                actorId,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f),
                new ActorWoundSnapshot(actorId, 0, 0f),
                equippedItemId,
                equipmentEffects: EquipmentEffectSet.None,
                maximumWounds: 3,
                actionPointEconomy: new TurnActionPointEconomy(4, 4, 6),
                turnMovementAllowance: 8f,
                injuries: new ActorInjuryState(
                    actorId,
                    Array.Empty<InjuryRecord>(),
                    ActorPhysiologyState.Healthy,
                    lifeState));
    }
}
