using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GritGud.Application.Gameplay;
using GritGud.Application.Levels;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Domain.Turns;

internal static class SimulationChecks
{
    private static int Main()
    {
        try
        {
            var executedFixtureChecks = new HashSet<string>(StringComparer.Ordinal);
            VerifyWalkingSliceAndExactReplay();
            VerifyCapabilityCoverageFailsClosed();
            VerifyTacticalRuleCoverageAndOutcomeProjection();
            VerifyAtomicLiveInstallation();
            VerifyPortableGroundSurfaces();
            VerifyStaticHeadlessSpatialGeometry();
            VerifyConcreteActorAttackCandidateRoute();
            VerifyAllCurrentContentCoverage();
            VerifyTacticalDestructibleSimulation();
            executedFixtureChecks.Add("sim-destructible-cover");
            executedFixtureChecks.Add("sim-target-kind-matrix");
            VerifyHeadlessSmokeExposure();
            executedFixtureChecks.Add("sim-smoke-and-exposure");
            VerifyHeadlessFireHazard();
            executedFixtureChecks.Add("sim-persistent-fire");
            VerifyEncounterAwarenessAndScopedInitiative();
            executedFixtureChecks.Add("sim-awareness-multi-observer");
            executedFixtureChecks.Add("sim-reinforcement-scope");
            VerifyCommittedActionConsequenceTrajectory();
            VerifyBankedActionPointEconomy();
            executedFixtureChecks.Add("sim-ap-banking");
            VerifyDroneHeadlessTrajectory();
            executedFixtureChecks.Add("sim-drone-control");
            SimulationParityChecks.Verify();
            executedFixtureChecks.Add("sim-concussive-ap");
            executedFixtureChecks.Add("sim-pinned-recovery");
            VerifyIntegratedDepotGauntlet();
            executedFixtureChecks.Add("sim-integrated-encounter");
            VerifySimulationFixtureManifest(executedFixtureChecks);
            Console.WriteLine(
                "Simulation checks passed: reducers, exact replay, atomic installation, and all-content capability coverage.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void VerifyDroneHeadlessTrajectory()
    {
        GameplaySession gameplay = CreateGameplay(CreateRifle());
        Require(gameplay.BeginEncounter(),
            "Drone fixture encounter did not begin.");
        GameplayCombatStateSnapshot captured = GameplayCombatStateCapture.Capture(
            gameplay);
        var droneDefinition = new DroneDefinition(
            "drone.fixture",
            "player",
            new GameplayPosition(0f, 2f, 0f),
            0f,
            5f,
            5f,
            new ActionCost(1, 0f, ActionMobility.Mobile),
            new DroneSensorDefinition(16f, 120f),
            CreateRifle());
        Require(DroneSensorRules.CanObserve(
                droneDefinition.CreateInitialSnapshot(),
                new GameplayPosition(0f, 2f, 8f))
            && !DroneSensorRules.CanObserve(
                droneDefinition.CreateInitialSnapshot(),
                new GameplayPosition(0f, 2f, -8f))
            && !DroneSensorRules.CanObserve(
                droneDefinition.CreateInitialSnapshot(),
                new GameplayPosition(0f, 2f, 17f)),
            "Drone perception did not enforce its canonical range and facing cone.");
        var initial = new GameplayCombatStateSnapshot(
            captured.Session,
            coverage: GameplayCombatStateCoverage.Session
                | GameplayCombatStateCoverage.Drones
                | GameplayCombatStateCoverage.Destructibles
                | GameplayCombatStateCoverage.SmokeFields,
            destructibles: Array.Empty<DestructiblePropSnapshot>(),
            smokeFields: Array.Empty<SmokeFieldSnapshot>(),
            drones: new[] { droneDefinition.CreateInitialSnapshot() });
        var sensorLevel = new LevelDocument
        {
            levelId = "drone-sensor-fixture",
            schemaVersion = 1,
        };
        var sensorSpatial = new GameplayHeadlessSpatialEvidence(
            sensorLevel,
            new SpatialContentIdentity(
                sensorLevel.levelId,
                sensorLevel.schemaVersion,
                evidenceAlgorithmVersion: 1,
                new string('d', 64)));
        TargetExposureSnapshot forwardSight =
            GameplayHeadlessEncounterEvidence.CaptureDroneSight(
                initial,
                sensorSpatial,
                droneDefinition.Id,
                "enemy");
        var reversed = new GameplayCombatStateSnapshot(
            captured.Session,
            coverage: GameplayCombatStateCoverage.Session
                | GameplayCombatStateCoverage.Drones
                | GameplayCombatStateCoverage.Destructibles
                | GameplayCombatStateCoverage.SmokeFields,
            destructibles: Array.Empty<DestructiblePropSnapshot>(),
            smokeFields: Array.Empty<SmokeFieldSnapshot>(),
            drones: new[]
            {
                new DroneSnapshot(
                    droneDefinition,
                    droneDefinition.StartingPosition,
                    facingDegrees: 180f,
                    droneDefinition.MaximumIntegrity),
            });
        TargetExposureSnapshot rearSight =
            GameplayHeadlessEncounterEvidence.CaptureDroneSight(
                reversed,
                sensorSpatial,
                droneDefinition.Id,
                "enemy");
        Require(forwardSight.VisibleSampleCount > 0
            && rearSight.VisibleSampleCount == 0,
            "Headless drone exposure diverged from its canonical sensor cone.");
        var reducers = GameplaySimulationReducers.CreateCurrent();
        var droneAttackInput = new GameplayReachableInput(
            GameplayReachableInputKind.CharacterAbility,
            "fixture.drone.attack",
            "player",
            GameplayCapabilityProfiles.DroneAttack(
                droneDefinition.Attack,
                GameplaySemanticSubjectKind.Actor),
            sourceSubjectId: droneDefinition.Id);
        GameplayCapabilityRegistry droneCapabilities =
            GameplayCurrentCapabilityCatalog.Create(
                reducers,
                new[] { droneAttackInput });
        var droneCandidates = new GameplayTacticalCandidateBuilder(
            droneCapabilities);
        Require(droneCandidates.Build(
                    initial,
                    new[] { droneAttackInput }).Count == 1
            && droneCandidates.Build(
                    reversed,
                    new[] { droneAttackInput }).Count == 0,
            "Drone candidate generation ignored its canonical sensor envelope.");
        var trajectory = new List<GameplaySemanticTransition>();
        TurnBudget initialBudget = initial.Session.GetActor("player").TurnBudget;
        var movement = new DroneMoveRecord(
            "player",
            droneDefinition.Id,
            droneDefinition.StartingPosition,
            new GameplayPosition(3f, 2f, 0f),
            90f,
            droneDefinition.MoveCost,
            initialBudget,
            initialBudget.SpendAction(droneDefinition.MoveCost));
        GameplayCombatStateSnapshot moved = Reduce(
            reducers,
            initial,
            new GameplayDroneMoveTransitionPayload(movement),
            trajectory);
        GameplayActorSnapshot target = moved.Session.GetActor("enemy");
        var exposure = new TargetExposureSnapshot(
            droneDefinition.Id,
            target.ActorId,
            new[]
            {
                new TargetRegionExposure(TargetRegionId.Torso, 1, 1),
            });
        AttackResolutionRecord resolution = AttackResolutionRules.Resolve(
            1L,
            7u,
            exposure,
            droneDefinition.Attack.AccuracyDecay,
            1f,
            target.Wounds,
            droneDefinition.Attack.WoundMovementPenalty);
        TurnBudget attackBudget = moved.Session.GetActor("player").TurnBudget;
        var attack = new DroneAttackRecord(
            "player",
            droneDefinition.Id,
            "enemy",
            GameplaySemanticSubjectKind.Actor.ToString(),
            droneDefinition.Attack.TurnCost,
            attackBudget,
            attackBudget.SpendAction(droneDefinition.Attack.TurnCost),
            resolution);
        GameplayCombatStateSnapshot resulting = Reduce(
            reducers,
            moved,
            new GameplayDroneAttackTransitionPayload(
                GameplaySemanticSubjectKind.Actor,
                droneDefinition.Attack,
                attack),
            trajectory);
        Require(resulting.Drones[0].Position.DistanceTo(
                movement.Destination) == 0f
            && resulting.Session.GetActor("player")
                .TurnBudget.ActionPoints == 2
            && resulting.Session.GetActor("enemy").Wounds.TorsoWounds == 1,
            "Drone trajectory did not preserve controller AP, movement, and damage.");
        var branch = new GameplaySimulationBranch("drone", initial, reducers);
        foreach (GameplaySemanticTransition transition in trajectory)
            branch.Apply(transition);
        Require(GameplayExactReplay.Verify(initial, branch.Steps, reducers).IsExact,
            "Drone headless trajectory did not replay exactly.");

        GameplaySession liveGameplay = CreateGameplay(CreateRifle());
        Require(liveGameplay.BeginEncounter(),
            "Live drone parity encounter did not begin.");
        var liveDrones = new GameplayDroneSession(
            liveGameplay,
            new[] { droneDefinition });
        GameplayCombatStateSnapshot liveInitial = GameplayCombatStateCapture.Capture(
            liveGameplay,
            drones: liveDrones);
        DroneMoveRecord liveMove = liveDrones.PrepareMove(
            droneDefinition.Id,
            movement.Destination,
            movement.ResultingFacingDegrees);
        GameplaySemanticTransition liveMoveTransition = CreateTransition(
            liveInitial,
            new GameplayDroneMoveTransitionPayload(liveMove),
            1L);
        GameplayCombatStateSnapshot predictedMove = reducers.Reduce(
            liveInitial,
            liveMoveTransition).Resulting;
        liveDrones.CommitMove(liveMove);
        liveGameplay.RecordSemanticTransition(liveMoveTransition.Identity);
        GameplayCombatStateSnapshot actualMove = GameplayCombatStateCapture.Capture(
            liveGameplay,
            drones: liveDrones);
        IReadOnlyList<GameplayStateDifference> moveDifferences =
            GameplayCombatStateDiffer.Compare(predictedMove, actualMove);
        Require(moveDifferences.Count == 0,
            "Live drone movement diverged from pure reduction at "
                + (moveDifferences.Count == 0
                    ? "unknown"
                    : moveDifferences[0].Path));
        GameplayActorSnapshot liveTarget = actualMove.Session.GetActor("enemy");
        var liveExposure = new TargetExposureSnapshot(
            droneDefinition.Id,
            liveTarget.ActorId,
            new[]
            {
                new TargetRegionExposure(TargetRegionId.Torso, 1, 1),
            });
        AttackResolutionRecord liveResolution = AttackResolutionRules.Resolve(
            1L,
            7u,
            liveExposure,
            droneDefinition.Attack.AccuracyDecay,
            1f,
            liveTarget.Wounds,
            droneDefinition.Attack.WoundMovementPenalty);
        TurnBudget liveBudget = actualMove.Session.GetActor("player").TurnBudget;
        var liveAttack = new DroneAttackRecord(
            "player",
            droneDefinition.Id,
            "enemy",
            GameplaySemanticSubjectKind.Actor.ToString(),
            droneDefinition.Attack.TurnCost,
            liveBudget,
            liveBudget.SpendAction(droneDefinition.Attack.TurnCost),
            liveResolution);
        var liveAttackPayload = new GameplayDroneAttackTransitionPayload(
            GameplaySemanticSubjectKind.Actor,
            droneDefinition.Attack,
            liveAttack);
        GameplaySemanticTransition liveAttackTransition = CreateTransition(
            actualMove,
            liveAttackPayload,
            2L);
        GameplayCombatStateSnapshot predictedAttack = reducers.Reduce(
            actualMove,
            liveAttackTransition).Resulting;
        liveDrones.CommitAttack(liveAttack);
        liveGameplay.RecordSemanticTransition(liveAttackTransition.Identity);
        GameplayCombatStateSnapshot actualAttack = GameplayCombatStateCapture.Capture(
            liveGameplay,
            drones: liveDrones);
        Require(GameplayCombatStateDiffer.Compare(
                predictedAttack,
                actualAttack).Count == 0,
            "Live drone attack diverged from pure reduction.");

        DroneSnapshot liveDroneTarget = actualAttack.Drones[0];
        var droneExposure = new DroneExposureSnapshot(
            "player",
            liveDroneTarget.DroneId,
            visibleSampleCount: 1,
            totalSampleCount: 1);
        ActorDroneAttackRecord incoming = liveDrones.PrepareActorAttack(
            "player",
            liveDroneTarget.DroneId,
            droneExposure,
            distance: 3f,
            resolutionSeed: 9u);
        var incomingPayload = new GameplayActorDroneAttackTransitionPayload(
            CreateRifle(),
            incoming);
        GameplaySemanticTransition incomingTransition = CreateTransition(
            actualAttack,
            incomingPayload,
            3L);
        GameplayCombatStateSnapshot predictedIncoming = reducers.Reduce(
            actualAttack,
            incomingTransition).Resulting;
        liveDrones.CommitActorAttack(incoming);
        liveGameplay.RecordSemanticTransition(incomingTransition.Identity);
        GameplayCombatStateSnapshot actualIncoming =
            GameplayCombatStateCapture.Capture(
                liveGameplay,
                drones: liveDrones);
        Require(GameplayCombatStateDiffer.Compare(
                predictedIncoming,
                actualIncoming).Count == 0
            && actualIncoming.Drones[0].RemainingIntegrity == 4f,
            "Incoming drone damage diverged across live and pure reduction.");
    }

    private static GameplaySemanticTransition CreateTransition(
        GameplayCombatStateSnapshot state,
        GameplayTransitionPayload payload,
        long sequence) => new GameplaySemanticTransition(
            new GameplayTransitionIdentity(
                sequence,
                payload.Profile.Capability.ToString(),
                payload.ActorId,
                payload.SubjectId),
            state.CanonicalHash,
            payload);

    private static void VerifyIntegratedDepotGauntlet()
    {
        LoadDepotContent(
            out GameplayScenarioAssembly assembly,
            out LevelDocument level);
        var gameplay = new GameplaySession(
            assembly.Scenario,
            scenarioSeed: assembly.RandomSeed);
        DestructiblePropSession destructibles =
            DestructiblePropSession.FromLevel(level, gameplay.Journal);
        var drones = new GameplayDroneSession(
            gameplay,
            assembly.Drones,
            destructibles);
        using (var fireFields = new GameplayFireFieldSession(
            gameplay,
            destructibles))
        {
            var evidence = new IntegratedExplosiveEvidence(
                "oren-vale",
                "depot-rifleman");
            var explosives = new GameplayThrownExplosiveSession(
                gameplay,
                evidence,
                evidence,
                new GameplayBlastConsequenceResolver(
                    gameplay,
                    destructibles),
                new IntegratedCenterUncertaintySampler(),
                fireFieldSession: fireFields);
            GameplayTransitionReducerRegistry reducers =
                GameplaySimulationReducers.CreateCurrent();
            var trajectory = new List<GameplaySemanticTransition>();
            GameplayCombatStateSnapshot initial = CaptureIntegratedState(
                gameplay,
                destructibles,
                fireFields,
                drones);
            GameplayCombatStateSnapshot predicted = initial;

            Require(gameplay.BeginEncounter(),
                "Integrated Depot encounter did not begin.");
            predicted = VerifyIntegratedStep(
                "Depot encounter start",
                predicted,
                new GameplaySessionControlTransitionPayload(
                    "player",
                    GameplaySemanticCapability.ChangeEncounter,
                    "begin"),
                gameplay,
                destructibles,
                fireFields,
                drones,
                reducers,
                trajectory);
            int turnGuard = 0;
            while (!string.Equals(
                gameplay.ActiveActorId,
                "player",
                StringComparison.Ordinal))
            {
                string skippedActorId = gameplay.ActiveActorId;
                Require(turnGuard++ < gameplay.InitiativeOrder.Count,
                    "Integrated Depot fixture could not reach the player turn.");
                Require(gameplay.TryEndTurn(
                        skippedActorId,
                        out TurnEndFailure skippedFailure),
                    "Integrated Depot setup turn failed to end: "
                        + skippedFailure);
                predicted = VerifyIntegratedStep(
                    "Depot setup turn " + skippedActorId,
                    predicted,
                    new GameplayEndTurnTransitionPayload(
                        skippedActorId,
                        emergency: false),
                    gameplay,
                    destructibles,
                    fireFields,
                    drones,
                    reducers,
                    trajectory);
            }

            GameplayPosition playerPosition = gameplay.GetActor("player")
                .Pose.Position;
            GameplayPosition fireLanding = new GameplayPosition(
                playerPosition.X,
                playerPosition.Y,
                playerPosition.Z + 2f);
            Require(explosives.TryThrowItem(
                    "player",
                    "item.incendiary-grenade",
                    fireLanding,
                    out GameplayActionRecord fireAction,
                    out ThrownExplosiveFailure fireFailure),
                "Integrated Depot incendiary failed: " + fireFailure);
            predicted = VerifyIntegratedStep(
                "Depot incendiary",
                predicted,
                new GameplayResolvedActionTransitionPayload(
                    GameplayCapabilityProfiles.ThrowExplosive(
                        (ThrownExplosiveDefinition)gameplay.GetInventoryItem(
                            "player",
                            "item.incendiary-grenade").ConsumablePower),
                    fireAction),
                gameplay,
                destructibles,
                fireFields,
                drones,
                reducers,
                trajectory);

            DroneDefinition droneDefinition = assembly.GetDrone(
                "scout-drone-01");
            DroneSnapshot drone = drones.GetDrone(droneDefinition.Id);
            var droneDestination = new GameplayPosition(
                drone.Position.X,
                drone.Position.Y,
                drone.Position.Z + 2f);
            DroneMoveRecord movement = drones.PrepareMove(
                drone.DroneId,
                droneDestination,
                facingDegrees: 0f);
            drones.CommitMove(movement);
            predicted = VerifyIntegratedStep(
                "Depot drone movement",
                predicted,
                new GameplayDroneMoveTransitionPayload(movement),
                gameplay,
                destructibles,
                fireFields,
                drones,
                reducers,
                trajectory);

            GameplayActorSnapshot droneTarget = gameplay.GetActor(
                "depot-rifleman");
            var droneExposure = new TargetExposureSnapshot(
                drone.DroneId,
                droneTarget.ActorId,
                new[]
                {
                    new TargetRegionExposure(TargetRegionId.Torso, 1, 1),
                });
            long attackSequence = gameplay.LastActionSequence + 1L;
            AttackResolutionRecord droneResolution =
                AttackResolutionRules.Resolve(
                    attackSequence,
                    AttackResolutionRules.DeriveResolutionSeed(
                        assembly.RandomSeed,
                        attackSequence),
                    droneExposure,
                    droneDefinition.Attack.AccuracyDecay,
                    droneDestination.DistanceTo(droneTarget.Pose.Position),
                    droneTarget.Wounds,
                    droneDefinition.Attack.WoundMovementPenalty);
            DroneAttackRecord droneAttack = drones.PrepareActorAttack(
                drone.DroneId,
                droneResolution);
            drones.CommitAttack(droneAttack);
            predicted = VerifyIntegratedStep(
                "Depot drone attack",
                predicted,
                new GameplayDroneAttackTransitionPayload(
                    GameplaySemanticSubjectKind.Actor,
                    droneDefinition.Attack,
                    droneAttack),
                gameplay,
                destructibles,
                fireFields,
                drones,
                reducers,
                trajectory);

            Require(gameplay.TryEndTurn(
                    "player",
                    out TurnEndFailure endFailure),
                "Integrated Depot player turn failed to end: " + endFailure);
            predicted = VerifyIntegratedStep(
                "Depot fire advancement",
                predicted,
                new GameplayEndTurnTransitionPayload(
                    "player",
                    emergency: false),
                gameplay,
                destructibles,
                fireFields,
                drones,
                reducers,
                trajectory);
            Require(predicted.FireFields.Count == 1
                && predicted.FireFields[0].CurrentRadius
                    > predicted.FireFields[0].Field.Definition.InitialRadius,
                "Integrated Depot fire did not evolve on canonical turn time.");
            turnGuard = 0;
            while (!string.Equals(
                gameplay.ActiveActorId,
                "oren-vale",
                StringComparison.Ordinal))
            {
                string skippedActorId = gameplay.ActiveActorId;
                Require(turnGuard++ < gameplay.InitiativeOrder.Count,
                    "Integrated Depot fixture could not reach Oren's turn.");
                Require(gameplay.TryEndTurn(
                        skippedActorId,
                        out TurnEndFailure skippedFailure),
                    "Integrated Depot intervening turn failed to end: "
                        + skippedFailure);
                predicted = VerifyIntegratedStep(
                    "Depot intervening turn " + skippedActorId,
                    predicted,
                    new GameplayEndTurnTransitionPayload(
                        skippedActorId,
                        emergency: false),
                    gameplay,
                    destructibles,
                    fireFields,
                    drones,
                    reducers,
                    trajectory);
            }

            GameplayPosition orenPosition = gameplay.GetActor("oren-vale")
                .Pose.Position;
            int riflemanActionPoints = gameplay.GetActor("depot-rifleman")
                .TurnBudget.ActionPoints;
            Require(explosives.TryThrowItem(
                    "oren-vale",
                    "item.concussive-grenade",
                    new GameplayPosition(
                        orenPosition.X,
                        orenPosition.Y,
                        orenPosition.Z + 2f),
                    out GameplayActionRecord concussiveAction,
                    out ThrownExplosiveFailure concussiveFailure),
                "Integrated Depot concussion failed: " + concussiveFailure);
            predicted = VerifyIntegratedStep(
                "Depot concussion",
                predicted,
                new GameplayResolvedActionTransitionPayload(
                    GameplayCapabilityProfiles.ThrowExplosive(
                        (ThrownExplosiveDefinition)gameplay.GetInventoryItem(
                            "oren-vale",
                            "item.concussive-grenade").ConsumablePower),
                    concussiveAction),
                gameplay,
                destructibles,
                fireFields,
                drones,
                reducers,
                trajectory);
            Require(predicted.Session.GetActor("oren-vale")
                    .TurnBudget.ActionPoints
                    == Math.Max(
                        0,
                        concussiveAction.ResultingBudget.ActionPoints - 2)
                && predicted.Session.GetActor("depot-rifleman")
                    .TurnBudget.ActionPoints
                    == Math.Max(0, riflemanActionPoints - 2),
                "Integrated Depot concussion did not reduce frozen current AP after throw cost.");

            Require(gameplay.CompleteEncounter(),
                "Integrated Depot encounter did not complete.");
            predicted = VerifyIntegratedStep(
                "Depot encounter completion",
                predicted,
                new GameplaySessionControlTransitionPayload(
                    "oren-vale",
                    GameplaySemanticCapability.ChangeEncounter,
                    "complete"),
                gameplay,
                destructibles,
                fireFields,
                drones,
                reducers,
                trajectory);
            Require(!predicted.Session.EncounterActive
                && predicted.Session.TurnContext == TurnModeContext.Voluntary,
                "Integrated multi-enemy Depot lifecycle did not terminate.");
            var branch = new GameplaySimulationBranch(
                "integrated-depot",
                initial,
                reducers);
            foreach (GameplaySemanticTransition transition in trajectory)
                branch.Apply(transition);
            Require(string.Equals(
                    branch.CurrentState.CanonicalHash,
                    predicted.CanonicalHash,
                    StringComparison.Ordinal),
                "Detached headless Depot branch diverged from the live trajectory.");
            GameplayExactReplayResult replay = GameplayExactReplay.Verify(
                initial,
                branch.Steps,
                reducers);
            Require(replay.IsExact
                && string.Equals(
                    replay.FinalState.CanonicalHash,
                    predicted.CanonicalHash,
                    StringComparison.Ordinal),
                "Integrated Depot trajectory did not replay exactly.");
        }
    }

    private static GameplayCombatStateSnapshot VerifyIntegratedStep(
        string label,
        GameplayCombatStateSnapshot previous,
        GameplayTransitionPayload payload,
        GameplaySession gameplay,
        DestructiblePropSession destructibles,
        GameplayFireFieldSession fireFields,
        GameplayDroneSession drones,
        GameplayTransitionReducerRegistry reducers,
        ICollection<GameplaySemanticTransition> trajectory)
    {
        GameplaySemanticTransition transition = CreateTransition(
            previous,
            payload,
            previous.Session.LastTransitionSequence + 1L);
        GameplayCombatStateSnapshot predicted = reducers.Reduce(
            previous,
            transition).Resulting;
        gameplay.RecordSemanticTransition(transition.Identity);
        GameplayCombatStateSnapshot actual = CaptureIntegratedState(
            gameplay,
            destructibles,
            fireFields,
            drones);
        IReadOnlyList<GameplayStateDifference> differences =
            GameplayCombatStateDiffer.Compare(predicted, actual);
        Require(differences.Count == 0,
            label + " live/headless parity diverged at "
                + (differences.Count == 0 ? "unknown" : differences[0].Path));
        trajectory.Add(transition);
        return actual;
    }

    private static GameplayCombatStateSnapshot CaptureIntegratedState(
        GameplaySession gameplay,
        DestructiblePropSession destructibles,
        GameplayFireFieldSession fireFields,
        GameplayDroneSession drones) => GameplayCombatStateCapture.Capture(
            gameplay,
            destructibles,
            fireFields: fireFields,
            drones: drones);

    private static void LoadDepotContent(
        out GameplayScenarioAssembly assembly,
        out LevelDocument level)
    {
        string repositoryRoot = FindRepositoryRoot();
        string contentRoot = Path.Combine(
            repositoryRoot,
            "Assets",
            "GritGud",
            "Content",
            "Resources");
        var json = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
        };
        ScenarioContentDocument scenario = ReadJson<ScenarioContentDocument>(
            Path.Combine(contentRoot, "Scenarios", "depot-yard.json"),
            json);
        level = ReadCurrentLevel(
            Path.Combine(
                contentRoot,
                "Levels",
                "Published",
                "main-level.json"),
            json);
        scenario.Normalize();
        level.Normalize();
        assembly = new GameplayScenarioAssembler().Assemble(scenario, level);
    }

    private static void VerifyBankedActionPointEconomy()
    {
        var economy = new TurnActionPointEconomy(4, 4, 6);
        VerifyGrant(economy, 0, granted: 4, waste: 0, resulting: 4);
        VerifyGrant(economy, 1, granted: 4, waste: 0, resulting: 5);
        VerifyGrant(economy, 2, granted: 4, waste: 0, resulting: 6);
        VerifyGrant(economy, 5, granted: 1, waste: 3, resulting: 6);
        VerifyGrant(economy, 6, granted: 0, waste: 4, resulting: 6);

        var legacy = new ScenarioContentDocument
        {
            schemaVersion = 16,
            timing = new ScenarioTimingData
            {
                minimumVoluntaryTurnSeconds = 1f,
            },
            actors = new List<ScenarioActorContentData>
            {
                new ScenarioActorContentData
                {
                    id = "legacy.actor",
                    turnBudget = new ScenarioTurnBudgetData
                    {
                        actionPoints = 3,
                    },
                },
            },
        };
        ScenarioContentMigrator.Migrate(legacy);
        Require(legacy.schemaVersion == ScenarioContentDocument.CurrentSchemaVersion
            && legacy.timing.startingActionPoints == 3
            && legacy.timing.actionPointIncome == 3
            && legacy.timing.maximumHeldActionPoints == 3,
            "Legacy scenario migration did not install explicit equivalent AP semantics.");

        GameplaySession encounter = CreateGameplay(CreateRifle());
        Require(encounter.GetActor("player").TurnBudget.ActionPoints == 4,
            "Scenario starting AP did not use the authored economy.");
        Require(encounter.BeginEncounter(), "AP check encounter did not begin.");
        Require(encounter.TryEndTurn("player", out _),
            "AP check could not complete a personal turn.");
        TurnEndRecord ended = encounter.LastEndedTurn;
        Require(ended.PersonalTurnStart != null
            && ended.PersonalTurnStart.ActionPoints.PreviousActionPoints == 4
            && ended.PersonalTurnStart.ActionPoints.GrantedActionPoints == 2
            && ended.PersonalTurnStart.ActionPoints.CapWaste == 2
            && ended.PersonalTurnStart.ActionPoints.ResultingActionPoints == 6
            && encounter.GetActor("enemy").TurnBudget.ActionPoints == 6,
            "Personal-turn record did not freeze the capped AP grant.");
        Require(encounter.CompleteEncounter(),
            "AP check encounter did not complete.");
        Require(encounter.GetActor("enemy").TurnBudget.ActionPoints == 6,
            "Encounter completion generated or discarded AP.");

        GameplaySession voluntary = CreateGameplay(CreateRifle());
        Require(voluntary.EnterTurnMode(),
            "AP check voluntary interval did not begin.");
        Require(voluntary.TryEndTurn("player", out _),
            "AP check voluntary interval did not end.");
        Require(voluntary.CompleteVoluntaryWorldTurn(),
            "AP check voluntary world cycle did not complete.");
        Require(voluntary.GetActor("player").TurnBudget.ActionPoints == 6
            && voluntary.LastCompletedVoluntaryTurnCycle
                .PersonalTurnStarts.Count == 2,
            "Voluntary world completion did not grant each capable actor once.");
    }

    private static void VerifyGrant(
        TurnActionPointEconomy economy,
        int previous,
        int granted,
        int waste,
        int resulting)
    {
        PersonalTurnActionPointGrant record =
            PersonalTurnActionPointRules.Grant(previous, economy);
        Require(record.PreviousActionPoints == previous
            && record.RequestedIncome == 4
            && record.GrantedActionPoints == granted
            && record.CapWaste == waste
            && record.ResultingActionPoints == resulting,
            $"AP grant mismatch for previous AP {previous}.");
    }

    private static void VerifyTacticalRuleCoverageAndOutcomeProjection()
    {
        AttackDefinition rifle = CreateRifle();
        GameplayCapabilityProfile profile = GameplayCapabilityProfiles.Attack(
            rifle,
            GameplaySemanticSubjectKind.Actor);
        var rule = new TacticalContextRuleDefinition(
            "rule.ambush",
            "Ambush",
            order: 0,
            new[] { profile.Signature },
            new[] { GameplaySemanticSubjectKind.Actor },
            new[]
            {
                new TacticalContextPredicate(
                    TacticalContextFeature.TargetAwareness,
                    TacticalPredicateOperator.Equal,
                    (int)TacticalAwarenessBand.Unaware),
            },
            new TacticalModifierConsequences(accuracyDeltaPercent: 15),
            new[] { "outcome.ambush" });
        var input = new GameplayReachableInput(
            GameplayReachableInputKind.EquippedAttack,
            rifle.ActionId,
            "player",
            profile,
            "enemy");
        GameplayTacticalRuleSupportRegistry registry =
            GameplayCurrentTacticalRuleSupport.Create(
                new[] { rule },
                "UnityTacticalContextQuery");
        GameplayTacticalRuleCoverageReport complete =
            GameplayTacticalRuleCoverageValidator.Validate(
                new[] { rule },
                new[] { input },
                registry);
        Require(complete.IsComplete,
            "Current tactical-rule support did not cover its exact route.");

        GameplayTacticalRuleCoverageReport missing =
            GameplayTacticalRuleCoverageValidator.Validate(
                new[] { rule },
                new[] { input },
                new GameplayTacticalRuleSupportRegistry());
        Require(!missing.IsComplete
            && missing.Issues.Count == 1
            && missing.Issues[0].MissingStages
                == GameplayTacticalRuleSupportStage.Complete,
            "Tactical-rule validation did not fail closed for a missing route.");
    }

    private static void VerifyWalkingSliceAndExactReplay()
    {
        AttackDefinition rifle = CreateRifle();
        GameplaySession gameplay = CreateGameplay(rifle);
        Require(gameplay.BeginEncounter(), "Encounter did not begin.");
        GameplayCombatStateSnapshot initial =
            GameplayCombatStateCapture.Capture(gameplay);
        GameplayTransitionReducerRegistry registry =
            GameplaySimulationReducers.CreateCurrent();
        var transitions = new List<GameplaySemanticTransition>();

        GameplayCombatStateSnapshot state = initial;
        GameplayActorSnapshot player = state.Session.GetActor("player");
        var segment = new MovementRouteSegmentRecord(
            player.Pose.Position,
            new GameplayPosition(1f, 0f, 0f),
            movementCost: 1f,
            playbackDurationSeconds: 0.25f);
        var route = new MovementRouteRecord(
            player.ActorId,
            player.Pose,
            player.TurnBudget,
            new[] { segment });
        state = Reduce(
            registry,
            state,
            new GameplayMoveTransitionPayload(
                GameplayCapabilityProfiles.GroundedMove(),
                route),
            transitions);

        player = state.Session.GetActor("player");
        var crouchedPose = new GameplayActorPose(
            player.Pose.Position,
            player.Pose.FacingDegrees,
            ActorStance.Crouched);
        state = Reduce(
            registry,
            state,
            new GameplayStanceTransitionPayload(new StanceChangeRecord(
                player.ActorId,
                player.Pose,
                crouchedPose)),
            transitions);

        player = state.Session.GetActor("player");
        GameplayActorSnapshot enemy = state.Session.GetActor("enemy");
        long transitionSequence = state.Session.LastTransitionSequence + 1L;
        var attackIdentity = new GameplayTransitionIdentity(
            transitionSequence,
            GameplaySemanticCapability.DirectAttack.ToString(),
            player.ActorId,
            enemy.ActorId);
        var exposure = new TargetExposureSnapshot(
            player.ActorId,
            enemy.ActorId,
            new[]
            {
                new TargetRegionExposure(TargetRegionId.Torso, 5, 5),
            });
        uint seed = GameplayAddressedRandom.SampleUInt32(
            state.Session.RunIdentity,
            attackIdentity,
            "resolution");
        var attack = AttackResolutionRules.Resolve(
            sequence: 1L,
            resolutionSeed: seed,
            exposure,
            rifle.AccuracyDecay,
            player.Pose.Position.DistanceTo(enemy.Pose.Position),
            enemy.Wounds,
            rifle.WoundMovementPenalty,
            rifle.Contact);
        TurnBudget attackBudget = player.TurnBudget.SpendAction(rifle.TurnCost);
        var action = new GameplayActionRecord(
            state.Session.LastActionSequence + 1L,
            new GameplayActionRequest(
                player.ActorId,
                rifle.ActionId,
                enemy.ActorId),
            rifle.TurnCost,
            player.TurnBudget,
            attackBudget,
            new GameplayActionOutcome[]
            {
                new AttackResolvedActionOutcome(attack),
            });
        var attackTransition = new GameplaySemanticTransition(
            attackIdentity,
            state.CanonicalHash,
            new GameplayWeaponTransitionPayload(
                GameplayCapabilityProfiles.Attack(rifle),
                action));
        transitions.Add(attackTransition);
        state = registry.Reduce(state, attackTransition).Resulting;

        state = Reduce(
            registry,
            state,
            new GameplayEndTurnTransitionPayload("player", emergency: false),
            transitions);

        Require(
            state.Session.LastTransitionSequence == 4L,
            "Walking slice did not produce four ordered transitions.");
        Require(
            string.Equals(
                state.Session.ActiveActorId,
                "enemy",
                StringComparison.Ordinal),
            "Normal turn completion did not advance initiative.");
        Require(
            state.Session.GetActor("player").Pose.Stance == ActorStance.Crouched,
            "Stance transition was not reduced.");
        Require(
            state.Session.GetActor("player").Pose.Position.DistanceTo(
                new GameplayPosition(1f, 0f, 0f)) == 0f,
            "Movement transition was not reduced.");

        GameplayCombatStateSnapshot replay = initial;
        foreach (GameplaySemanticTransition transition in transitions)
            replay = registry.Reduce(replay, transition).Resulting;
        Require(
            string.Equals(
                replay.CanonicalHash,
                state.CanonicalHash,
                StringComparison.Ordinal),
            "Exact replay diverged from the source trajectory.");
        Require(
            GameplayCombatStateDiffer.Compare(state, replay).Count == 0,
            "Exact replay has structured state differences.");

        var branch = new GameplaySimulationBranch(
            "walking-source",
            initial,
            registry);
        foreach (GameplaySemanticTransition transition in transitions)
            branch.Apply(transition);
        GameplayExactReplayResult verified = GameplayExactReplay.Verify(
            initial,
            branch.Steps,
            registry);
        Require(verified.IsExact,
            "Recorded headless trajectory did not replay exactly.");
        Require(string.Equals(
                verified.FinalState.CanonicalHash,
                state.CanonicalHash,
                StringComparison.Ordinal),
            "Recorded trajectory ended at the wrong state.");
        GameplaySimulationBranch fork = branch.Fork("walking-branch");
        Require(string.Equals(
                fork.CurrentState.CanonicalHash,
                branch.CurrentState.CanonicalHash,
                StringComparison.Ordinal),
            "Detached fork did not begin from its parent state.");
        Require(fork.Steps.Count == 0,
            "Detached fork inherited parent trajectory mutations.");
    }

    private static GameplayCombatStateSnapshot Reduce(
        GameplayTransitionReducerRegistry registry,
        GameplayCombatStateSnapshot state,
        GameplayTransitionPayload payload,
        ICollection<GameplaySemanticTransition> trajectory)
    {
        var identity = new GameplayTransitionIdentity(
            state.Session.LastTransitionSequence + 1L,
            payload.Profile.Capability.ToString(),
            payload.ActorId,
            payload.SubjectId);
        var transition = new GameplaySemanticTransition(
            identity,
            state.CanonicalHash,
            payload);
        trajectory.Add(transition);
        GameplayReductionResult result = registry.Reduce(state, transition);
        Require(result.DomainEvents.Count > 0, "Reduction produced no domain event.");
        return result.Resulting;
    }

    private static GameplaySession CreateGameplay(AttackDefinition rifle)
    {
        var player = new ScenarioActorDefinition(
            "player",
            10,
            new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
            new TurnBudget(4, 8f),
            rifle);
        var enemy = new ScenarioActorDefinition(
            "enemy",
            0,
            new GameplayActorPose(new GameplayPosition(1f, 0f, 5f), 180f),
            new TurnBudget(4, 8f),
            rifle);
        var scenario = new ScenarioDefinition(
            "simulation-check",
            new ScenarioTimingDefinition(1f),
            new[] { player, enemy },
            Array.Empty<ScenarioObjectiveDefinition>());
        return new GameplaySession(scenario, scenarioSeed: 0xC0FFEEu);
    }

    private static void VerifyEncounterAwarenessAndScopedInitiative()
    {
        GameplaySession live = CreateEncounterGameplay();
        GameplayCombatStateSnapshot initial = GameplayCombatStateCapture.Capture(live);
        GameplayTransitionReducerRegistry reducers =
            GameplaySimulationReducers.CreateCurrent();
        var trajectory = new List<GameplaySemanticTransition>();
        GameplayCombatStateSnapshot reduced = initial;
        EnemyBehaviorDefinition behavior = live.Scenario.GetActor("enemy")
            .Combat.EnemyBehavior;
        GameplayActorSnapshot enemy = live.GetActor("enemy");
        var patrolRoute = new MovementRouteRecord(
            "enemy",
            enemy.Pose,
            new[] { behavior.PatrolRoute.GetWaypoint(1) });
        PatrolAdvanceRecord patrol = live.PreparePatrolAdvance(
            "enemy",
            patrolRoute);
        reduced = Reduce(
            reducers,
            reduced,
            new GameplayPatrolTransitionPayload("enemy", behavior, patrol),
            trajectory);
        live.CommitPatrolAdvance(patrol);
        live.RecordSemanticTransition(trajectory[trajectory.Count - 1].Identity);
        Require(GameplayCombatStateDiffer.Compare(
                reduced,
                GameplayCombatStateCapture.Capture(live)).Count == 0,
            "Live patrol advance diverged from its pure reducer.");

        GameplayPosition playerPosition = live.GetActor("player").Pose.Position;
        var soundObservation = new EncounterObservation(
            "enemy",
            sound: new EncounterSoundEvidence(
                "player",
                playerPosition,
                audibility: 1f));
        EnemyAwarenessTransitionRecord sound = live.PrepareAwarenessTransition(
            "enemy",
            soundObservation);
        reduced = Reduce(
            reducers,
            reduced,
            new GameplayEncounterObservationTransitionPayload(
                "enemy",
                behavior,
                soundObservation),
            trajectory);
        live.CommitAwarenessTransition(sound);
        live.RecordSemanticTransition(trajectory[trajectory.Count - 1].Identity);
        Require(live.EncounterState.GetAwareness("enemy").State
                == EncounterAwarenessState.Suspicious,
            "Audible sound did not raise suspicion.");
        Require(live.EncounterState.GetAwareness("support").State
                == EncounterAwarenessState.Unaware,
            "One observer's sound evidence leaked into another enemy's awareness.");
        Require(GameplayCombatStateDiffer.Compare(
                reduced,
                GameplayCombatStateCapture.Capture(live)).Count == 0,
            "Live sound awareness diverged from its pure reducer.");

        var sight = new TargetExposureSnapshot(
            "enemy",
            "player",
            new[]
            {
                new TargetRegionExposure(TargetRegionId.Torso, 1, 1),
            });
        var sightObservation = new EncounterObservation(
            "enemy",
            sight,
            playerPosition);
        EnemyAwarenessTransitionRecord detection =
            live.PrepareAwarenessTransition("enemy", sightObservation);
        reduced = Reduce(
            reducers,
            reduced,
            new GameplayEncounterObservationTransitionPayload(
                "enemy",
                behavior,
                sightObservation),
            trajectory);
        live.CommitAwarenessTransition(detection);
        live.RecordSemanticTransition(trajectory[trajectory.Count - 1].Identity);
        Require(live.EncounterState.GetAwareness("enemy").State
                == EncounterAwarenessState.Alert,
            "Visible hostile did not escalate awareness to alert.");
        Require(GameplayCombatStateDiffer.Compare(
                reduced,
                GameplayCombatStateCapture.Capture(live)).Count == 0,
            "Live sight awareness diverged from its pure reducer.");

        IReadOnlyList<string> scope = live.CreateEncounterScope("enemy", "player");
        var begin = new GameplaySessionControlTransitionPayload(
            "player",
            GameplaySemanticCapability.ChangeEncounter,
            "begin",
            encounterParticipantIds: scope);
        reduced = Reduce(reducers, reduced, begin, trajectory);
        Require(live.BeginEncounter(scope), "Scoped encounter did not begin.");
        live.RecordSemanticTransition(trajectory[trajectory.Count - 1].Identity);
        GameplayCombatStateSnapshot liveState = GameplayCombatStateCapture.Capture(live);
        Require(GameplayCombatStateDiffer.Compare(reduced, liveState).Count == 0,
            "Live scoped encounter diverged from its pure reducer.");
        Require(live.InitiativeOrder.Count == 3
                && ContainsActor(live.InitiativeOrder, "support")
                && !ContainsActor(live.InitiativeOrder, "bystander"),
            "Scoped encounter omitted a declared reinforcement or included a nonparticipant.");

        GameplayCombatStateSnapshot replay = initial;
        foreach (GameplaySemanticTransition transition in trajectory)
            replay = reducers.Reduce(replay, transition).Resulting;
        Require(string.Equals(replay.CanonicalHash, reduced.CanonicalHash,
                StringComparison.Ordinal),
            "Encounter trajectory did not replay exactly.");
        var branch = new GameplaySimulationBranch(
            "encounter",
            initial,
            reducers);
        foreach (GameplaySemanticTransition transition in trajectory)
            branch.Apply(transition);
        Require(GameplayExactReplay.Verify(initial, branch.Steps, reducers).IsExact,
            "Headless encounter trajectory did not replay exactly.");
    }

    private static void VerifyCommittedActionConsequenceTrajectory()
    {
        GameplaySession live = CreateEncounterGameplay();
        GameplayCombatStateSnapshot initial = GameplayCombatStateCapture.Capture(
            live);
        initial = new GameplayCombatStateSnapshot(
            initial.Session,
            destructibles: Array.Empty<DestructiblePropSnapshot>(),
            coverage: GameplayCombatStateCoverage.Session
                | GameplayCombatStateCoverage.Destructibles);
        var attacks = new GameplayAttackSession(live);
        var exposure = new TargetExposureSnapshot(
            "player",
            "enemy",
            new[]
            {
                new TargetRegionExposure(TargetRegionId.Torso, 1, 1),
            });
        Require(attacks.TryPrepareResolve(
                "player",
                exposure,
                out GameplayPreparedTransition<GameplayActionRecord> prepared,
                out AttackResolutionFailure failure)
            && failure == AttackResolutionFailure.None,
            "Could not prepare the committed-action trajectory attack.");
        AttackDefinition attack = live.Scenario.GetActor("player").Attack;
        var identity = new GameplayTransitionIdentity(
            initial.Session.LastTransitionSequence + 1L,
            GameplaySemanticCapability.DirectAttack.ToString(),
            "player",
            "enemy");
        var transition = new GameplaySemanticTransition(
            identity,
            initial.CanonicalHash,
            new GameplayWeaponTransitionPayload(
                GameplayCapabilityProfiles.Attack(attack),
                prepared.Record));
        var level = new LevelDocument
        {
            levelId = "committed-action-empty-space",
            schemaVersion = 1,
        };
        var spatial = new GameplayHeadlessSpatialEvidence(
            level,
            new SpatialContentIdentity(
                level.levelId,
                level.schemaVersion,
                evidenceAlgorithmVersion: 1,
                new string('b', 64)));
        GameplayTransitionReducerRegistry reducers =
            GameplaySimulationReducers.CreateCurrent();

        GameplayCommittedActionConsequencePlan plan =
            GameplayCommittedActionConsequencePlanner.Execute(
                initial,
                transition,
                live.Scenario,
                spatial,
                reducers,
                authoredActionStartsEncounter: true);

        Require(plan.Steps.Count == 4,
            "Committed action did not reduce as action, per-observer sound awareness, then encounter; got "
                + plan.Steps.Count + " steps.");
        Require(plan.Steps[0].Transition.Payload
                is GameplayWeaponTransitionPayload
            && plan.Steps[1].Transition.Payload
                is GameplayEncounterObservationTransitionPayload
            && plan.Steps[2].Transition.Payload
                is GameplayEncounterObservationTransitionPayload
            && plan.Steps[3].Transition.Payload
                is GameplaySessionControlTransitionPayload,
            "Committed action consequence ordering is not canonical.");
        Require(plan.ResultingState.Session.EncounterState
                .GetAwareness("enemy").State
                == EncounterAwarenessState.Suspicious,
            "Post-action sound did not update headless awareness.");
        Require(plan.ResultingState.Session.EncounterState
                .GetAwareness("support").State
                == EncounterAwarenessState.Unaware,
            "Out-of-range sound incorrectly leaked into reinforcement awareness.");
        Require(plan.ResultingState.Session.EncounterActive,
            "Committed action did not begin its scoped encounter.");
        Require(GameplayExactReplay.Verify(
                initial,
                plan.Steps,
                reducers).IsExact,
            "Committed action consequence trajectory did not replay exactly.");
    }

    private static GameplaySession CreateEncounterGameplay()
    {
        AttackDefinition rifle = CreateRifle();
        var playerProfile = new CharacterProfileDefinition(
            "character.player",
            "Player",
            "Test Operative",
            new[]
            {
                new CharacterRating(CoreAttributeIds.Strength, 3),
                new CharacterRating(CoreAttributeIds.Dexterity, 3),
                new CharacterRating(CoreAttributeIds.Grit, 3),
                new CharacterRating(CoreAttributeIds.Charisma, 3),
            },
            Array.Empty<CharacterRating>(),
            Array.Empty<string>());
        var player = new ScenarioActorDefinition(
            "player",
            5,
            new GameplayActorPose(new GameplayPosition(0f, 0f, 6f), 180f),
            new TurnBudget(4, 8f),
            rifle,
            combat: new ActorCombatDefinition("player", new[] { "raider" }, 2),
            characterProfile: playerProfile);
        var behavior = new EnemyBehaviorDefinition(
            "behavior.encounter-check",
            perceptionRange: 20f,
            viewAngleDegrees: 120f,
            preferredEngagementRange: 12f,
            movementSearchRadius: 6f,
            maximumAttacksPerTurn: 1,
            awarenessPolicy: new EncounterAwarenessPolicyDefinition(
                hearingRange: 12f,
                sightSuspicionGain: 100,
                soundSuspicionGain: 60,
                suspicionDecayPerTick: 10,
                alertThreshold: 100),
            patrolRoute: new PatrolRouteDefinition(
                new[]
                {
                    new GameplayPosition(0f, 0f, 0f),
                    new GameplayPosition(0f, 0f, 3f),
                },
                loops: true),
            reinforcementActorIds: new[] { "support" });
        var enemy = new ScenarioActorDefinition(
            "enemy",
            3,
            new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
            new TurnBudget(4, 8f),
            rifle,
            combat: new ActorCombatDefinition(
                "raider",
                new[] { "player" },
                2,
                behavior));
        var bystander = new ScenarioActorDefinition(
            "bystander",
            1,
            new GameplayActorPose(new GameplayPosition(30f, 0f, 0f), 0f),
            new TurnBudget(4, 8f),
            rifle,
            combat: new ActorCombatDefinition("neutral", new string[0], 1));
        var supportBehavior = new EnemyBehaviorDefinition(
            "behavior.encounter-support",
            perceptionRange: 16f,
            viewAngleDegrees: 90f,
            preferredEngagementRange: 10f,
            movementSearchRadius: 4f,
            maximumAttacksPerTurn: 1,
            awarenessPolicy: new EncounterAwarenessPolicyDefinition(
                hearingRange: 8f,
                sightSuspicionGain: 100,
                soundSuspicionGain: 40,
                suspicionDecayPerTick: 10,
                alertThreshold: 100));
        var support = new ScenarioActorDefinition(
            "support",
            2,
            new GameplayActorPose(new GameplayPosition(12f, 0f, 0f), 270f),
            new TurnBudget(4, 8f),
            rifle,
            combat: new ActorCombatDefinition(
                "raider",
                new[] { "player" },
                2,
                supportBehavior));
        var scenario = new ScenarioDefinition(
            "encounter-check",
            new ScenarioTimingDefinition(1f),
            new[] { player, enemy, support, bystander },
            Array.Empty<ScenarioObjectiveDefinition>(),
            playerParty: new PlayerPartyDefinition(
                new[] { "player" },
                "player"));
        return new GameplaySession(scenario, scenarioSeed: 0xE11Cu);
    }

    private static GameplayCombatStateSnapshot CreateHeadlessEncounterState(
        AttackDefinition rifle,
        LevelDocument level)
    {
        var behavior = new EnemyBehaviorDefinition(
            "behavior.headless-sight",
            perceptionRange: 20f,
            viewAngleDegrees: 120f,
            preferredEngagementRange: 12f,
            movementSearchRadius: 6f,
            maximumAttacksPerTurn: 1,
            awarenessPolicy: new EncounterAwarenessPolicyDefinition(
                hearingRange: 12f,
                sightSuspicionGain: 100,
                soundSuspicionGain: 50,
                suspicionDecayPerTick: 10,
                alertThreshold: 100));
        var observer = new ScenarioActorDefinition(
            "observer",
            2,
            new GameplayActorPose(new GameplayPosition(-2f, 0f, 0f), 90f),
            new TurnBudget(4, 8f),
            rifle,
            combat: new ActorCombatDefinition(
                "raider",
                new[] { "player" },
                2,
                behavior));
        var target = new ScenarioActorDefinition(
            "target",
            1,
            new GameplayActorPose(new GameplayPosition(2f, 0f, 0f), 270f),
            new TurnBudget(4, 8f),
            rifle,
            combat: new ActorCombatDefinition(
                "player",
                new[] { "raider" },
                2));
        var gameplay = new GameplaySession(new ScenarioDefinition(
            "headless-encounter-evidence",
            new ScenarioTimingDefinition(1f),
            new[] { observer, target },
            Array.Empty<ScenarioObjectiveDefinition>()));
        DestructiblePropSession destructibles =
            DestructiblePropSession.FromLevel(level, gameplay.Journal);
        return GameplayCombatStateCapture.Capture(gameplay, destructibles);
    }

    private static void VerifyCapabilityCoverageFailsClosed()
    {
        AttackDefinition rifle = CreateRifle();
        GameplaySession gameplay = CreateGameplay(rifle);
        GameplayTransitionReducerRegistry reducers =
            GameplaySimulationReducers.CreateCurrent();
        var capabilities = new GameplayCapabilityRegistry(reducers);
        GameplayCapabilityProfile movement =
            GameplayCapabilityProfiles.GroundedMove();
        capabilities.RegisterStage(
            movement,
            GameplayCapabilitySupportStage.CandidateConstruction,
            "checks.move.candidates");
        capabilities.RegisterStage(
            movement,
            GameplayCapabilitySupportStage.LegalityAndEvidence,
            "checks.move.legality");
        capabilities.RegisterStage(
            movement,
            GameplayCapabilitySupportStage.PureStateReduction,
            "checks.move.reducer");
        capabilities.RegisterStage(
            GameplayCapabilityProfiles.Equip(),
            GameplayCapabilitySupportStage.CandidateConstruction,
            "checks.unreachable.equip");

        GameplayCapabilityCoverageReport report =
            GameplayCapabilityCoverageValidator.Validate(
                gameplay.Scenario,
                new LevelDocument(),
                capabilities);
        Require(!report.IsComplete,
            "Partial capability support was incorrectly accepted.");
        Require(HasIssue(report, "capability.incomplete-route"),
            "Incomplete reachable routes were not reported.");
        Require(HasIssue(report, "capability.unreachable-implementation"),
            "Unreachable registered capabilities were not reported.");

        var candidate = new GameplayCandidate(
            "candidate.move.player",
            movement,
            "player",
            "player",
            new object());
        bool rejected = false;
        try
        {
            capabilities.RequireCandidateRoute(candidate);
        }
        catch (NotSupportedException)
        {
            rejected = true;
        }
        Require(rejected,
            "Candidate generation did not fail closed on an incomplete route.");
    }

    private static void VerifyAtomicLiveInstallation()
    {
        AttackDefinition rifle = CreateRifle();
        GameplaySession gameplay = CreateGameplay(rifle);
        Require(gameplay.BeginEncounter(), "Encounter did not begin.");
        GameplayCombatStateSnapshot initial =
            GameplayCombatStateCapture.Capture(gameplay);
        GameplayTransitionReducerRegistry reducers =
            GameplaySimulationReducers.CreateCurrent();
        var input = new GameplayReachableInput(
            GameplayReachableInputKind.MovementControl,
            "control.move",
            "player",
            GameplayCapabilityProfiles.GroundedMove());
        GameplayCapabilityRegistry capabilities =
            GameplayCurrentCapabilityCatalog.Create(
                reducers,
                new[] { input });
        GameplayCandidate candidate = new GameplayReachableCandidateBuilder(
            capabilities).Build(input);
        Require(candidate.Profile.Equals(input.Profile),
            "Reachable input did not build its exact capability candidate.");

        GameplayActorSnapshot player = initial.Session.GetActor("player");
        var route = new MovementRouteRecord(
            player.ActorId,
            player.Pose,
            player.TurnBudget,
            new[]
            {
                new MovementRouteSegmentRecord(
                    player.Pose.Position,
                    new GameplayPosition(1f, 0f, 0f),
                    movementCost: 1f,
                    playbackDurationSeconds: 0.25f),
            });
        var payload = new GameplayMoveTransitionPayload(
            GameplayCapabilityProfiles.GroundedMove(),
            route);
        var transition = new GameplaySemanticTransition(
            new GameplayTransitionIdentity(
                1L,
                GameplaySemanticCapability.Move.ToString(),
                payload.ActorId,
                payload.SubjectId),
            initial.CanonicalHash,
            payload);
        var store = new GameplayAtomicCombatStateStore(initial);
        bool eventObservedInstalledState = false;
        store.DomainEventPublished += _ =>
            eventObservedInstalledState = store.Current.Session
                .LastTransitionSequence == 1L;
        var pipeline = new GameplaySemanticExecutionPipeline(
            reducers,
            capabilities,
            store);
        GameplayReductionResult result = pipeline.Execute(transition);
        Require(ReferenceEquals(store.Current, result.Resulting),
            "Live installation did not swap the authoritative root.");
        Require(eventObservedInstalledState,
            "Domain events were published before authoritative installation.");

        var executionIdentity = new GameplayExecutionIdentity(
            new GameplayContentIdentity(
                initial.Session.ScenarioId,
                scenarioSchemaVersion: 1,
                rulesSchemaVersion: 1,
                new string('a', 64)),
            new SpatialContentIdentity(
                "simulation-check-level",
                levelSchemaVersion: 1,
                evidenceAlgorithmVersion: 1,
                new string('b', 64)),
            initial.Session.RunIdentity);
        var runtime = new GameplaySimulationRuntime(
            executionIdentity,
            initial,
            reducers,
            capabilities);
        bool runtimeEventSawInstalledTrajectory = false;
        runtime.DomainEventPublished += domainEvent =>
            runtimeEventSawInstalledTrajectory = runtime.CurrentState.Session
                    .LastTransitionSequence == 1L
                && runtime.Trajectory.Count == 1
                && string.Equals(
                    domainEvent.EventType,
                    "transition-reduced.Move",
                    StringComparison.Ordinal);
        runtime.Execute(transition);
        Require(runtimeEventSawInstalledTrajectory,
            "Live runtime published before state and trajectory installation.");
        GameplayReproBundle repro = runtime.CreateRepro(
            "atomic movement check");
        Require(repro.Trajectory.Count == 1
            && string.Equals(
                repro.FinalStateHash,
                runtime.CurrentState.CanonicalHash,
                StringComparison.Ordinal),
            "Live runtime did not create a contiguous semantic repro bundle.");
        string portable = repro.ToPortableJson();
        using (JsonDocument document = JsonDocument.Parse(portable))
        {
            Require(string.Equals(
                    document.RootElement.GetProperty("format").GetString(),
                    "grit-gud-semantic-repro",
                    StringComparison.Ordinal),
                "Portable repro document has the wrong format marker.");
        }
        Require(portable.Contains(initial.CanonicalHash)
            && portable.Contains(runtime.CurrentState.CanonicalHash)
            && portable.Contains(typeof(GameplayMoveTransitionPayload).FullName),
            "Portable repro omitted canonical endpoints or semantic payload data.");
        GameplayExactReplayResult runtimeReplay = GameplayExactReplay.Verify(
            initial,
            runtime.Trajectory,
            reducers);
        Require(runtimeReplay.IsExact,
            "Live runtime trajectory did not replay exactly.");

        var wrongEvents = new GameplayTrajectoryStep(
            transition,
            runtime.CurrentState.CanonicalHash,
            new[] { "wrong-event" });
        GameplayExactReplayResult eventDivergence = GameplayExactReplay.Verify(
            initial,
            new[] { wrongEvents },
            reducers);
        Require(!eventDivergence.IsExact
            && string.Equals(
                eventDivergence.DivergenceReason,
                "domain-events",
                StringComparison.Ordinal),
            "Exact replay did not detect domain-event divergence.");

        var wrongPayload = new GameplayTrajectoryStep(
            transition,
            runtime.CurrentState.CanonicalHash,
            runtime.Trajectory[0].DomainEventTypes,
            new string('0', 64));
        GameplayExactReplayResult payloadDivergence = GameplayExactReplay.Verify(
            initial,
            new[] { wrongPayload },
            reducers);
        Require(!payloadDivergence.IsExact
            && string.Equals(
                payloadDivergence.DivergenceReason,
                "transition-payload",
                StringComparison.Ordinal),
            "Exact replay did not detect semantic payload divergence.");

        var failingPresentationRuntime = new GameplaySimulationRuntime(
            executionIdentity,
            initial,
            reducers,
            capabilities);
        failingPresentationRuntime.DomainEventPublished += _ =>
            throw new InvalidOperationException("presentation check failure");
        bool presentationFailed = false;
        try
        {
            failingPresentationRuntime.Execute(transition);
        }
        catch (AggregateException)
        {
            presentationFailed = true;
        }
        Require(presentationFailed
            && failingPresentationRuntime.CurrentState.Session
                .LastTransitionSequence == 1L
            && failingPresentationRuntime.Trajectory.Count == 1,
            "Presentation failure rolled back or orphaned authoritative installation.");
    }

    private static void VerifyConcreteActorAttackCandidateRoute()
    {
        GameplaySession gameplay = CreateEncounterGameplay();
        var level = new LevelDocument
        {
            levelId = "candidate-route-check",
            schemaVersion = 1,
        };
        level.Normalize();
        DestructiblePropSession destructibles =
            DestructiblePropSession.FromLevel(level, gameplay.Journal);
        Require(gameplay.BeginEncounter(),
            "Concrete candidate route encounter did not begin.");
        GameplayCombatStateSnapshot initial = GameplayCombatStateCapture.Capture(
            gameplay,
            destructibles);
        var spatialIdentity = new SpatialContentIdentity(
            level.levelId,
            level.schemaVersion,
            evidenceAlgorithmVersion: 1,
            new string('c', 64));
        var spatial = new GameplayHeadlessSpatialEvidence(
            level,
            spatialIdentity);
        IReadOnlyList<GameplayReachableInput> inputs =
            GameplayReachableInputEnumerator.Enumerate(
                gameplay.Scenario,
                level);
        GameplayTransitionReducerRegistry reducers =
            GameplaySimulationReducers.CreateCurrent();
        GameplayCapabilityRegistry capabilities =
            GameplayCurrentCapabilityCatalog.Create(reducers, inputs);
        var routes = new GameplayCandidateExecutionRouteRegistry(capabilities);
        routes.Register(new GameplayActorAttackCandidateExecutionRoute(
            gameplay.Scenario,
            Array.Empty<TacticalContextRuleDefinition>(),
            spatial));
        routes.Register(new GameplayEndTurnCandidateExecutionRoute(
            gameplay.Scenario));

        GameplayCandidate selected = null;
        foreach (GameplayCandidate candidate in
            new GameplayTacticalCandidateBuilder(capabilities).Build(
                initial,
                inputs))
        {
            if (candidate.Profile.Capability
                    == GameplaySemanticCapability.DirectAttack
                && string.Equals(
                    candidate.ActorId,
                    "player",
                    StringComparison.Ordinal)
                && string.Equals(
                    candidate.SubjectId,
                    "enemy",
                    StringComparison.Ordinal)
                && routes.Supports(candidate.Profile))
            {
                selected = candidate;
                break;
            }
        }
        Require(selected != null,
            "Concrete route fixture did not construct the player attack candidate.");
        var context = new GameplayDecisionContext(
            initial,
            GameplayObservationSnapshot.FullState("player", initial));
        GameplayExecutableCandidateEvaluation first = routes.Evaluate(
            context,
            selected);
        GameplayExecutableCandidateEvaluation repeated = routes.Evaluate(
            context,
            selected);
        Require(first.IsLegal
            && repeated.IsLegal
            && first.ExpectedOutcome.GetValue("attack.hit-probability") > 0f
            && string.Equals(
                GameplayCanonicalValueDigest.Calculate(first.Evidence),
                GameplayCanonicalValueDigest.Calculate(repeated.Evidence),
                StringComparison.Ordinal),
            "Policy-neutral attack evaluation was not stable and legal.");

        GameplaySemanticTransition transition = routes.Prepare(context, first);
        var semanticPayload = (GameplayWeaponTransitionPayload)
            transition.Payload;
        var liveAttacks = new GameplayAttackSession(
            gameplay,
            destructibles,
            new GameplayHeadlessTacticalContextQuery(spatial),
            new GameplayTacticalContextEvaluator(
                Array.Empty<TacticalContextRuleDefinition>()));
        TargetExposureSnapshot exposure =
            GameplayHeadlessEncounterEvidence.CaptureSight(
                initial,
                spatial,
                "player",
                "enemy");
        Require(liveAttacks.TryPrepareResolve(
                "player",
                exposure,
                out GameplayPreparedTransition<GameplayActionRecord>
                    livePrepared,
                out AttackResolutionFailure liveFailure),
            "Live adapter rejected the shared attack preparation: "
                + liveFailure);
        Require(string.Equals(
                GameplayCanonicalValueDigest.Calculate(
                    semanticPayload.Action),
                GameplayCanonicalValueDigest.Calculate(livePrepared.Record),
                StringComparison.Ordinal)
            && string.Equals(
                GameplayWeaponActionStateProjector.Project(
                    initial,
                    semanticPayload.Action).CanonicalHash,
                livePrepared.Predicted.CanonicalHash,
                StringComparison.Ordinal),
            "Live and headless actor attack preparation diverged.");

        var executionIdentity = new GameplayExecutionIdentity(
            new GameplayContentIdentity(
                gameplay.Scenario.Id,
                scenarioSchemaVersion: 1,
                rulesSchemaVersion: 1,
                new string('a', 64)),
            spatialIdentity,
            gameplay.RunIdentity);
        var runtime = new GameplaySimulationRuntime(
            executionIdentity,
            initial,
            reducers,
            capabilities);
        runtime.Execute(transition);
        Require(runtime.Trajectory.Count == 1
            && GameplayExactReplay.Verify(
                initial,
                runtime.Trajectory,
                reducers).IsExact,
            "Concrete actor attack route did not execute and replay exactly.");
    }

    private static void VerifyStaticHeadlessSpatialGeometry()
    {
        var level = new LevelDocument
        {
            levelId = "static-spatial-check",
            schemaVersion = 1,
            entities = new List<LevelEntity>
            {
                new LevelEntity
                {
                    id = "static-wall",
                    archetypeId = "structure.wall.standard",
                    transform = new LevelTransformData(
                        new Float3Data(0f, 0f, 0f),
                        yawDegrees: 90f),
                    coverVolumes = new List<CoverVolumeData>
                    {
                        new CoverVolumeData
                        {
                            id = "primary",
                            localCenter = new Float3Data(0f, 1f, 0f),
                            size = new Float3Data(4f, 2f, 0.25f),
                        },
                    },
                },
            },
        };
        level.Normalize();
        GameplayCombatStateSnapshot state = GameplayCombatStateCapture.Capture(
            CreateGameplay(CreateRifle()));
        var spatial = new GameplayHeadlessSpatialEvidence(
            level,
            new SpatialContentIdentity(
                level.levelId,
                level.schemaVersion,
                evidenceAlgorithmVersion: 1,
                new string('e', 64)));
        var origin = new GameplayPosition(0f, 1f, -2f);
        var destination = new GameplayPosition(0f, 1f, 2f);
        Require(spatial.BlocksLineOfSight(state, origin, destination)
            && spatial.BlocksPath(
                state,
                origin,
                destination,
                clearanceRadius: 0.35f),
            "Static authored cover was absent from headless sight or path evidence.");
        Require(!spatial.BlocksLineOfSight(
                state,
                new GameplayPosition(3f, 1f, -2f),
                new GameplayPosition(3f, 1f, 2f)),
            "Static headless obstruction extended beyond its authored volume.");
    }

    private static void VerifyPortableGroundSurfaces()
    {
        var legacy = new LevelDocument
        {
            schemaVersion = 15,
            levelId = "portable-ground-check",
            entities = new List<LevelEntity>
            {
                new LevelEntity
                {
                    id = "floor",
                    archetypeId = "structure.floor.standard",
                    transform = new LevelTransformData(
                        new Float3Data(0f, 0f, 0f),
                        yawDegrees: 0f),
                },
                new LevelEntity
                {
                    id = "stairs",
                    archetypeId = "structure.stairs.standard",
                    transform = new LevelTransformData(
                        new Float3Data(0f, 0f, 0f),
                        yawDegrees: 0f),
                },
            },
            terrainSurfaces = new List<TerrainSurfaceData>
            {
                new TerrainSurfaceData
                {
                    id = "ground",
                    origin = new Float3Data(-2f, -0.15f, -2f),
                    sampleCountX = 3,
                    sampleCountZ = 3,
                    sampleSpacing = 2f,
                    minimumElevation = 0f,
                    elevationIncrement = 0.1f,
                    heightSamples = new List<int>
                    {
                        0, 0, 0,
                        0, 0, 0,
                        0, 0, 0,
                    },
                },
            },
        };
        LevelDocument level = new LevelDocumentMigrator().MigrateToCurrent(
            legacy);
        Require(level.schemaVersion == LevelDocument.CurrentSchemaVersion
            && level.entities[0].placementSurface != null
            && level.entities[1].placementSurface != null,
            "Level migration did not materialize portable placement surfaces.");
        var spatial = new GameplayHeadlessSpatialEvidence(
            level,
            new SpatialContentIdentity(
                level.levelId,
                level.schemaVersion,
                evidenceAlgorithmVersion: 1,
                new string('f', 64)));
        GameplayPosition floor = spatial.ResolveSpawnPosition(
            new GameplayPosition(-1f, 2f, 1f));
        GameplayPosition stairTop = spatial.ResolveSpawnPosition(
            new GameplayPosition(-1f, 2f, -2.5f));
        Require(Math.Abs(floor.Y - 0.07f) <= 0.0001f
            && Math.Abs(stairTop.Y - 1.52f) <= 0.0001f,
            "Portable spawn grounding did not reproduce floor and ramp heights.");
        Require(spatial.TryResolveMovementPosition(
                new GameplayPosition(-1f, 0.02f, 0f),
                new GameplayPosition(-1f, 0.02f, -0.5f),
                maximumVerticalReach: 0.5f,
                out GameplayPosition stairStep)
            && Math.Abs(stairStep.Y - 0.32f) <= 0.0001f,
            "Portable ramp grounding did not resolve a reachable stair step.");

        LoadDepotContent(
            out GameplayScenarioAssembly depot,
            out LevelDocument depotLevel);
        var depotSpatial = new GameplayHeadlessSpatialEvidence(
            depotLevel,
            new SpatialContentIdentity(
                depotLevel.levelId,
                depotLevel.schemaVersion,
                evidenceAlgorithmVersion: 1,
                new string('1', 64)));
        GameplayScenarioAssembly groundedDepot =
            GameplayHeadlessScenarioGrounding.Resolve(depot, depotSpatial);
        float playerHeight = groundedDepot.GetActorDefinition("player")
            .StartingPose.Position.Y;
        float supportHeight = groundedDepot.GetActorDefinition(
            "depot-yard-support").StartingPose.Position.Y;
        Require(playerHeight < 0.2f
            && supportHeight > playerHeight + 2.5f,
            "Portable Depot grounding did not distinguish yard and raised-deck spawns.");
        var depotGameplay = new GameplaySession(
            groundedDepot.Scenario,
            scenarioSeed: groundedDepot.RandomSeed);
        DestructiblePropSession depotDestructibles =
            DestructiblePropSession.FromLevel(
                depotLevel,
                depotGameplay.Journal);
        GameplayCombatStateSnapshot depotState =
            GameplayCombatStateCapture.Capture(
                depotGameplay,
                depotDestructibles);
        TargetExposureSnapshot startingExposure =
            GameplayHeadlessEncounterEvidence.CaptureSight(
                depotState,
                depotSpatial,
                "player",
                "depot-rifleman");
        Require(startingExposure.VisibleSampleCount == 0,
            "Portable Depot evidence did not preserve the authored hidden starting positions.");
    }

    private static void VerifyAllCurrentContentCoverage()
    {
        string repositoryRoot = FindRepositoryRoot();
        string contentRoot = Path.Combine(
            repositoryRoot,
            "Assets",
            "GritGud",
            "Content",
            "Resources");
        JsonSerializerOptions json = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
        };
        var levels = new Dictionary<string, LevelDocument>(
            StringComparer.Ordinal);
        foreach (string path in Directory.GetFiles(
            Path.Combine(contentRoot, "Levels"),
            "*.json",
            SearchOption.AllDirectories))
        {
            LevelDocument level = ReadCurrentLevel(path, json);
            if (string.IsNullOrWhiteSpace(level.levelId)) continue;
            if (!levels.TryAdd(level.levelId, level))
                throw new InvalidOperationException(
                    $"Current content defines level '{level.levelId}' more than once.");
        }

        var allInputs = new List<GameplayReachableInput>();
        int scenarioCount = 0;
        foreach (string path in Directory.GetFiles(
            Path.Combine(contentRoot, "Scenarios"),
            "*.json",
            SearchOption.AllDirectories))
        {
            ScenarioContentDocument scenario =
                ReadJson<ScenarioContentDocument>(path, json);
            scenario.Normalize();
            if (!levels.TryGetValue(
                scenario.levelId ?? string.Empty,
                out LevelDocument level))
                throw new InvalidOperationException(
                    $"Scenario '{scenario.scenarioId}' references missing level '{scenario.levelId}'.");
            GameplayScenarioAssembly assembly =
                new GameplayScenarioAssembler().Assemble(scenario, level);
            GameplayCapabilityCoverageReport report =
                GameplayCapabilityCoverageGate.ValidateCurrent(assembly, level);
            report.RequireComplete(assembly.Scenario.Id);
            allInputs.AddRange(
                GameplayReachableInputEnumerator.Enumerate(assembly, level));
            scenarioCount++;
        }
        Require(scenarioCount > 0,
            "The all-content coverage gate found no scenarios.");

        GameplayTransitionReducerRegistry reducers =
            GameplaySimulationReducers.CreateCurrent();
        GameplayCapabilityRegistry capabilities =
            GameplayCurrentCapabilityCatalog.Create(reducers, allInputs);
        GameplayCapabilityCoverageReport aggregate =
            GameplayCapabilityCoverageValidator.Validate(
                allInputs,
                capabilities);
        Require(aggregate.IsComplete,
            "Current content contains an incomplete semantic capability route.");
        IReadOnlyList<GameplayCandidate> candidates =
            new GameplayReachableCandidateBuilder(capabilities).BuildAll(
                allInputs);
        Require(candidates.Count == allInputs.Count,
            "Reachable candidate construction omitted current content inputs.");

        int unreachable = 0;
        foreach (GameplayCapabilityCoverageIssue issue in aggregate.Issues)
            if (!issue.IsBlocking) unreachable++;
        Console.WriteLine(
            $"Capability coverage: {scenarioCount} scenario(s), "
            + $"{allInputs.Count} reachable inputs, "
            + $"{unreachable} implemented-but-unreachable profile(s).");
    }

    private static void VerifySimulationFixtureManifest(
        ISet<string> executedBehaviorChecks)
    {
        string repositoryRoot = FindRepositoryRoot();
        string contentRoot = Path.Combine(
            repositoryRoot,
            "Assets",
            "GritGud",
            "Content");
        var json = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
        };
        SimulationFixtureManifest manifest = ReadJson<
            SimulationFixtureManifest>(
            Path.Combine(
                contentRoot,
                "SimulationFixtures",
                "simulator-foundation-fixtures.json"),
            json);
        ScenarioContentDocument scenario = ReadJson<ScenarioContentDocument>(
            Path.Combine(
                contentRoot,
                "Resources",
                "Scenarios",
                "depot-yard.json"),
            json);
        LevelDocument level = ReadCurrentLevel(
            Path.Combine(
                contentRoot,
                "Resources",
                "Levels",
                "Published",
                "main-level.json"),
            json);
        scenario.Normalize();
        level.Normalize();
        Require(manifest.schemaVersion == 1,
            "Simulation fixture manifest schema is unsupported.");
        Require(string.Equals(
                manifest.scenarioId,
                scenario.scenarioId,
                StringComparison.Ordinal)
            && string.Equals(
                manifest.levelId,
                level.levelId,
                StringComparison.Ordinal),
            "Simulation fixture manifest does not target the playable Depot content.");

        var actorIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (ScenarioActorContentData actor in scenario.actors)
            actorIds.Add(actor.id);
        var entityIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (LevelEntity entity in level.entities)
            entityIds.Add(entity.id);
        var required = new HashSet<string>(new[]
        {
            "sim-awareness-multi-observer",
            "sim-reinforcement-scope",
            "sim-destructible-cover",
            "sim-target-kind-matrix",
            "sim-ap-banking",
            "sim-smoke-and-exposure",
            "sim-persistent-fire",
            "sim-concussive-ap",
            "sim-drone-control",
            "sim-pinned-recovery",
            "sim-integrated-encounter",
        }, StringComparer.Ordinal);
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (SimulationFixtureDefinition fixture in manifest.fixtures)
        {
            Require(fixture != null
                    && !string.IsNullOrWhiteSpace(fixture.id)
                    && found.Add(fixture.id),
                "Simulation fixtures require unique non-empty IDs.");
            Require(fixture.assertions != null
                    && fixture.assertions.Count > 0,
                $"Simulation fixture '{fixture.id}' has no behavioral assertions.");
            Require(string.Equals(
                    fixture.behaviorCheckId,
                    fixture.id,
                    StringComparison.Ordinal)
                && executedBehaviorChecks.Contains(fixture.behaviorCheckId),
                $"Simulation fixture '{fixture.id}' is not backed by an executed behavior check.");
            foreach (string actorId in fixture.actorIds)
                Require(actorIds.Contains(actorId),
                    $"Simulation fixture '{fixture.id}' references missing actor '{actorId}'.");
            foreach (string entityId in fixture.entityIds)
                Require(entityIds.Contains(entityId),
                    $"Simulation fixture '{fixture.id}' references missing entity '{entityId}'.");
        }
        Require(found.SetEquals(required),
            "Simulation fixture manifest does not contain the complete foundation matrix.");
        Console.WriteLine(
            $"Simulation fixtures: {found.Count} content-loaded cases target "
            + $"{actorIds.Count} Depot actors.");
    }

    private static void VerifyTacticalDestructibleSimulation()
    {
        LevelDocument level = CreateTacticalDestructibleLevel();
        AttackDefinition rifle = new AttackDefinition(
            "attack.cover-breaker",
            "Cover breaker",
            new ActionCost(1, 0f, ActionMobility.Set),
            woundMovementPenalty: 2f,
            accuracyDecay: AccuracyDecayDefinition.None,
            directFireDamage: new DirectFireDamageDefinition(
                "damage.ballistic.cover-breaker",
                baseIntegrityDamage: 2f));
        GameplaySession gameplay = CreateGameplay(rifle);
        Require(gameplay.BeginEncounter(), "Encounter did not begin.");
        DestructiblePropSession destructibles =
            DestructiblePropSession.FromLevel(level, gameplay.Journal);
        GameplayCombatStateSnapshot initial =
            GameplayCombatStateCapture.Capture(gameplay, destructibles);
        GameplayCapabilityProfile actorRoute =
            GameplayCapabilityProfiles.Attack(
                rifle,
                GameplaySemanticSubjectKind.Actor);
        GameplayCapabilityProfile propRoute =
            GameplayCapabilityProfiles.Attack(
                rifle,
                GameplaySemanticSubjectKind.DestructibleProp);
        Require(!actorRoute.Equals(propRoute),
            "Attack routes do not distinguish actor and destructible subjects.");

        var actorInput = new GameplayReachableInput(
            GameplayReachableInputKind.InventoryWeapon,
            "weapon.cover-breaker->Actor",
            "player",
            actorRoute);
        var propInput = new GameplayReachableInput(
            GameplayReachableInputKind.InventoryWeapon,
            "weapon.cover-breaker->DestructibleProp",
            "player",
            propRoute);
        GameplayTransitionReducerRegistry reducers =
            GameplaySimulationReducers.CreateCurrent();
        var incomplete = new GameplayCapabilityRegistry(reducers);
        RegisterComplete(incomplete, actorRoute, "checks.actor-only");
        GameplayCapabilityCoverageReport missingSubject =
            GameplayCapabilityCoverageValidator.Validate(
                new[] { actorInput, propInput },
                incomplete);
        Require(!missingSubject.IsComplete
            && HasMissingProfile(missingSubject, propRoute.Signature),
            "Coverage did not fail when destructible targeting was omitted.");

        GameplayCapabilityRegistry capabilities =
            GameplayCurrentCapabilityCatalog.Create(
                reducers,
                new[] { actorInput, propInput });
        IReadOnlyList<GameplayCandidate> tactical =
            new GameplayTacticalCandidateBuilder(capabilities).Build(
                initial,
                new[] { propInput });
        Require(tactical.Count == 1
            && tactical[0].SubjectKind
                == GameplaySemanticSubjectKind.DestructibleProp
            && string.Equals(
                tactical[0].SubjectId,
                "cover-wall",
                StringComparison.Ordinal),
            "Registered tactical destructible did not become an attack candidate.");
        var decisionContext = new GameplayDecisionContext(
            initial,
            GameplayObservationSnapshot.FullState("player", initial));
        GameplayCandidateEvaluation evaluation =
            new GameplayCandidateRouteEvaluator(capabilities).Evaluate(
                decisionContext,
                tactical[0]);
        Require(evaluation.RequiredEvidenceTypes.Count == 1
            && string.Equals(
                evaluation.RequiredEvidenceTypes[0],
                "target-exposure",
                StringComparison.Ordinal),
            "Destructible attack candidate did not reach its evidence contract.");

        var spatialIdentity = new SpatialContentIdentity(
            level.levelId,
            level.schemaVersion,
            evidenceAlgorithmVersion: 1,
            new string('a', 64));
        var spatial = new GameplayHeadlessSpatialEvidence(
            level,
            spatialIdentity);
        var sightOrigin = new GameplayPosition(-2f, 1f, 0f);
        var sightDestination = new GameplayPosition(2f, 1f, 0f);
        Require(spatial.BlocksLineOfSight(
                initial,
                sightOrigin,
                sightDestination),
            "Intact cover did not block headless line of sight.");
        Require(spatial.BlocksPath(
                initial,
                sightOrigin,
                sightDestination,
                clearanceRadius: 0.25f),
            "Intact cover did not block the corresponding headless route.");
        var toppledProp = new DestructiblePropSnapshot(
            "cover-wall",
            DestructiblePropState.Intact,
            maximumIntegrity: 2f,
            remainingIntegrity: 2f,
            new GameplayPropPose(
                new GameplayPosition(0f, 0f, 0f),
                pitchDegrees: 0f,
                yawDegrees: 0f,
                rollDegrees: 90f),
            DestructiblePropPosture.Toppled);
        GameplayCombatStateSnapshot toppled = WithDestructible(
            initial,
            toppledProp);
        Require(!spatial.BlocksLineOfSight(
                toppled,
                sightOrigin,
                sightDestination),
            "Toppled cover retained its obsolete upright obstruction.");
        GameplayCombatStateSnapshot sensingState =
            CreateHeadlessEncounterState(rifle, level);
        TargetExposureSnapshot blockedSight =
            GameplayHeadlessEncounterEvidence.CaptureSight(
                sensingState,
                spatial,
                "observer",
                "target");
        Require(blockedSight.VisibleSampleCount == 0,
            "Headless encounter sight ignored intact tactical cover.");
        TargetExposureSnapshot openedSight =
            GameplayHeadlessEncounterEvidence.CaptureSight(
                WithDestructible(sensingState, toppledProp),
                spatial,
                "observer",
                "target");
        Require(openedSight.VisibleSampleCount == openedSight.TotalSampleCount
            && openedSight.TotalSampleCount == 6,
            "Headless encounter sight retained a toppled obstruction.");
        EncounterSoundEvidence muffledSound =
            GameplayHeadlessEncounterEvidence.CaptureSound(
                sensingState,
                spatial,
                "observer",
                "target",
                loudness: 1f);
        Require(muffledSound.Audibility == 0.5f,
            "Headless encounter sound did not account for tactical obstruction.");
        var pitchedProp = new DestructiblePropSnapshot(
            "cover-wall",
            DestructiblePropState.Intact,
            maximumIntegrity: 2f,
            remainingIntegrity: 2f,
            new GameplayPropPose(
                new GameplayPosition(0f, 0f, 0f),
                pitchDegrees: 90f,
                yawDegrees: 0f,
                rollDegrees: 0f),
            DestructiblePropPosture.Toppled);
        Require(!spatial.BlocksLineOfSight(
                WithDestructible(initial, pitchedProp),
                sightOrigin,
                sightDestination),
            "Pitched cover retained its obsolete upright obstruction.");
        var movedProp = new DestructiblePropSnapshot(
            "cover-wall",
            DestructiblePropState.Intact,
            maximumIntegrity: 2f,
            remainingIntegrity: 2f,
            new GameplayPropPose(
                new GameplayPosition(0f, 0f, 4f),
                pitchDegrees: 0f,
                yawDegrees: 35f,
                rollDegrees: 0f),
            DestructiblePropPosture.Upright);
        Require(!spatial.BlocksLineOfSight(
                WithDestructible(initial, movedProp),
                sightOrigin,
                sightDestination),
            "Moved cover remained at its authored starting obstruction.");
        var highOrigin = new GameplayPosition(-2f, 2.6f, 0f);
        var highDestination = new GameplayPosition(2f, 2.6f, 0f);
        Require(!spatial.BlocksLineOfSight(
                initial,
                highOrigin,
                highDestination)
            && spatial.BlocksPath(
                initial,
                highOrigin,
                highDestination,
                clearanceRadius: 0.75f),
            "Path clearance did not conservatively expand tactical cover.");

        var fractureProfile = new GameplayFractureSpatialProfile(
            "fracture.cover.wall",
            new[]
            {
                new GameplayLocalSpatialVolume(
                    new GameplayPosition(0f, 1f, 0f),
                    new GameplayPosition(1f, 2f, 1f)),
                new GameplayLocalSpatialVolume(
                    new GameplayPosition(0f, 1f, 2f),
                    new GameplayPosition(1f, 2f, 1f)),
            });
        var fractureProfiles = new Dictionary<
            string,
            GameplayFractureSpatialProfile>(StringComparer.Ordinal)
        {
            ["cover.wall"] = fractureProfile,
        };
        var fractureSpatial = new GameplayHeadlessSpatialEvidence(
            level,
            spatialIdentity,
            fractureProfiles);
        var detachedBlockingChunk = new DestructiblePropSnapshot(
            "cover-wall",
            DestructiblePropState.Damaged,
            maximumIntegrity: 2f,
            remainingIntegrity: 1f,
            new GameplayPropPose(
                new GameplayPosition(0f, 0f, 0f),
                pitchDegrees: 0f,
                yawDegrees: 0f,
                rollDegrees: 0f),
            DestructiblePropPosture.Upright,
            fractureChunkCount: 2,
            detachedFractureChunks: 1UL);
        Require(!fractureSpatial.BlocksLineOfSight(
                WithDestructible(initial, detachedBlockingChunk),
                sightOrigin,
                sightDestination),
            "Detached fracture chunk remained in headless obstruction evidence.");
        var retainedBlockingChunk = new DestructiblePropSnapshot(
            "cover-wall",
            DestructiblePropState.Damaged,
            maximumIntegrity: 2f,
            remainingIntegrity: 1f,
            new GameplayPropPose(
                new GameplayPosition(0f, 0f, 0f),
                pitchDegrees: 0f,
                yawDegrees: 0f,
                rollDegrees: 0f),
            DestructiblePropPosture.Upright,
            fractureChunkCount: 2,
            detachedFractureChunks: 2UL);
        Require(fractureSpatial.BlocksLineOfSight(
                WithDestructible(initial, retainedBlockingChunk),
                sightOrigin,
                sightDestination),
            "Attached fracture chunk was absent from headless obstruction evidence.");
        bool missingFractureRejected = false;
        try
        {
            spatial.BlocksLineOfSight(
                WithDestructible(initial, detachedBlockingChunk),
                sightOrigin,
                sightDestination);
        }
        catch (InvalidOperationException)
        {
            missingFractureRejected = true;
        }
        Require(missingFractureRejected,
            "Detached fracture evidence did not fail closed without a profile.");

        GameplayEvidenceRecord beforeEvidence = spatial.CaptureEvidence(
            "line-of-sight",
            initial,
            sightOrigin,
            sightDestination);

        Require(destructibles.TryPrepareDamage(
                "cover-wall",
                requestedDamage: 2f,
                out DestructibleDamageRecord damage),
            "Destructible damage was not prepared.");
        GameplayActorSnapshot player = initial.Session.GetActor("player");
        var impact = new DirectFireImpactRecord(
            "cover-wall",
            "surface.wood",
            new GameplayPosition(0f, 1f, 0f),
            normalX: -1f,
            normalY: 0f,
            normalZ: 0f,
            initial.Session.Revision);
        var discharge = new WeaponDischargeRecord(
            sequence: 1L,
            attackerId: "player",
            actionId: rifle.ActionId,
            targetId: "cover-wall",
            origin: player.Pose.Position,
            aimPoint: impact.Point,
            impact,
            damage);
        var action = new GameplayActionRecord(
            sequence: 1L,
            new GameplayActionRequest(
                "player",
                rifle.ActionId,
                "cover-wall"),
            rifle.TurnCost,
            player.TurnBudget,
            player.TurnBudget.SpendAction(rifle.TurnCost),
            new GameplayActionOutcome[]
            {
                new WeaponDischargedActionOutcome(discharge),
            });
        var payload = new GameplayWeaponTransitionPayload(propRoute, action);
        var transition = new GameplaySemanticTransition(
            new GameplayTransitionIdentity(
                1L,
                GameplaySemanticCapability.DirectAttack.ToString(),
                "player",
                "cover-wall"),
            initial.CanonicalHash,
            payload,
            new[] { beforeEvidence });
        GameplayCombatStateSnapshot resulting = reducers.Reduce(
            initial,
            transition).Resulting;
        Require(resulting.Destructibles[0].State
                == DestructiblePropState.Destroyed,
            "Shooting cover did not reduce its canonical destructible state.");
        Require(!spatial.BlocksLineOfSight(
                resulting,
                sightOrigin,
                sightDestination),
            "Destroyed cover remained in headless line-of-sight evidence.");
        Require(!spatial.BlocksPath(
                resulting,
                sightOrigin,
                sightDestination,
                clearanceRadius: 0.25f),
            "Destroyed cover remained in headless route evidence.");
        GameplayEvidenceRecord afterEvidence = spatial.CaptureEvidence(
            "line-of-sight",
            resulting,
            sightOrigin,
            sightDestination);
        Require(!string.Equals(
                beforeEvidence.EvidenceDigest,
                afterEvidence.EvidenceDigest,
                StringComparison.Ordinal),
            "Destructible reduction did not invalidate spatial evidence.");
    }

    private static void VerifyHeadlessSmokeExposure()
    {
        GameplaySession gameplay = CreateEncounterGameplay();
        GameplayCombatStateSnapshot sessionOnly =
            GameplayCombatStateCapture.Capture(gameplay);
        var level = new LevelDocument
        {
            levelId = "headless-smoke-check",
            schemaVersion = 1,
        };
        var spatial = new GameplayHeadlessSpatialEvidence(
            level,
            new SpatialContentIdentity(
                level.levelId,
                level.schemaVersion,
                evidenceAlgorithmVersion: 1,
                new string('c', 64)));
        var smoke = new SmokeFieldRecord(
            "smoke.headless-check",
            "player",
            "item.smoke",
            new GameplayPosition(0f, 0f, 3f),
            new SmokeFieldDefinition(
                radius: 2f,
                height: 3f,
                explorationDurationSeconds: 20f,
                durationTurnEnds: 4,
                minimumObscuredPath: 0.5f));
        var clear = new GameplayCombatStateSnapshot(
            sessionOnly.Session,
            destructibles: Array.Empty<DestructiblePropSnapshot>(),
            smokeFields: Array.Empty<SmokeFieldSnapshot>(),
            coverage: GameplayCombatStateCoverage.Session
                | GameplayCombatStateCoverage.Destructibles
                | GameplayCombatStateCoverage.SmokeFields);
        var obscured = new GameplayCombatStateSnapshot(
            sessionOnly.Session,
            destructibles: Array.Empty<DestructiblePropSnapshot>(),
            smokeFields: new[] { new SmokeFieldSnapshot(smoke, 1f) },
            coverage: GameplayCombatStateCoverage.Session
                | GameplayCombatStateCoverage.Destructibles
                | GameplayCombatStateCoverage.SmokeFields);

        Require(GameplayHeadlessEncounterEvidence.CaptureSight(
                    clear,
                    spatial,
                    "enemy",
                    "player").VisibleSampleCount > 0,
            "Clear headless encounter sight was unexpectedly blocked.");
        Require(GameplayHeadlessEncounterEvidence.CaptureSight(
                    obscured,
                    spatial,
                    "enemy",
                    "player").VisibleSampleCount == 0,
            "Canonical smoke did not obscure headless encounter sight.");
        Require(!spatial.BlocksPath(
                obscured,
                gameplay.GetActor("enemy").Pose.Position,
                gameplay.GetActor("player").Pose.Position,
                clearanceRadius: 0.5f),
            "Smoke incorrectly became a solid headless path obstacle.");
    }

    private static void VerifyHeadlessFireHazard()
    {
        GameplaySession gameplay = CreateEncounterGameplay();
        GameplayCombatStateSnapshot sessionOnly =
            GameplayCombatStateCapture.Capture(gameplay);
        var level = new LevelDocument
        {
            levelId = "headless-fire-check",
            schemaVersion = 1,
        };
        var spatial = new GameplayHeadlessSpatialEvidence(
            level,
            new SpatialContentIdentity(
                level.levelId,
                level.schemaVersion,
                evidenceAlgorithmVersion: 1,
                new string('d', 64)));
        var field = new FireFieldRecord(
            "fire.headless-check",
            "player",
            "item.incendiary",
            new GameplayPosition(0f, 0f, 3f),
            new FireFieldDefinition(
                initialRadius: 1f,
                maximumRadius: 3f,
                height: 2f,
                explorationDurationSeconds: 12f,
                durationTurnEnds: 6,
                explorationPulseSeconds: 2f,
                actorWoundMovementPenalty: 1f,
                destructibleIntegrityDamage: 1f,
                minimumHazardPath: 0.5f));
        GameplayCombatStateCoverage coverage =
            GameplayCombatStateCoverage.Session
            | GameplayCombatStateCoverage.Destructibles
            | GameplayCombatStateCoverage.FireFields;
        var initial = new GameplayCombatStateSnapshot(
            sessionOnly.Session,
            destructibles: Array.Empty<DestructiblePropSnapshot>(),
            coverage: coverage,
            fireFields: new[] { new FireFieldSnapshot(field, 1f) });
        var expanded = new GameplayCombatStateSnapshot(
            sessionOnly.Session,
            destructibles: Array.Empty<DestructiblePropSnapshot>(),
            coverage: coverage,
            fireFields: new[] { new FireFieldSnapshot(field, 0.25f) });
        GameplayPosition start = new GameplayPosition(1.75f, 0.5f, 0f);
        GameplayPosition end = new GameplayPosition(1.75f, 0.5f, 6f);

        Require(spatial.EvaluateFireHazardTraversal(initial, start, end) == 0f,
            "Initial fire radius incorrectly reached a clear route.");
        Require(spatial.EvaluateFireHazardTraversal(expanded, start, end)
                >= field.Definition.MinimumHazardPath,
            "Expanded fire radius did not alter headless route hazard evidence.");
        Require(!spatial.BlocksPath(
                expanded,
                start,
                end,
                clearanceRadius: 0.5f),
            "Fire incorrectly became a solid headless path obstacle.");
    }

    private static GameplayCombatStateSnapshot WithDestructible(
        GameplayCombatStateSnapshot source,
        DestructiblePropSnapshot prop) => new GameplayCombatStateSnapshot(
            source.Session,
            new[] { prop },
            source.Vehicles,
            source.Projectiles,
            source.SmokeFields,
            source.Coverage,
            source.FireFields);

    private static LevelDocument CreateTacticalDestructibleLevel()
    {
        var level = new LevelDocument
        {
            levelId = "tactical-prop-check",
            displayName = "Tactical prop check",
        };
        var wall = new LevelEntity
        {
            id = "cover-wall",
            archetypeId = "cover.wall",
            transform = new LevelTransformData(
                new Float3Data(0f, 0f, 0f),
                yawDegrees: 0f),
            destructible = new DestructibleInstanceData
            {
                enabled = true,
                initialState = "intact",
                integrity = 2f,
            },
        };
        wall.coverVolumes.Add(new CoverVolumeData
        {
            id = "cover-wall.volume",
            localCenter = new Float3Data(0f, 1f, 0f),
            size = new Float3Data(1f, 2f, 1f),
        });
        level.entities.Add(wall);
        level.Normalize();
        return level;
    }

    private static void RegisterComplete(
        GameplayCapabilityRegistry registry,
        GameplayCapabilityProfile profile,
        string prefix)
    {
        foreach (GameplayCapabilitySupportStage stage in new[]
        {
            GameplayCapabilitySupportStage.CandidateConstruction,
            GameplayCapabilitySupportStage.LegalityAndEvidence,
            GameplayCapabilitySupportStage.PureStateReduction,
            GameplayCapabilitySupportStage.DomainEventProduction,
            GameplayCapabilitySupportStage.ReplayEncodingAndReduction,
            GameplayCapabilitySupportStage.HeadlessExecution,
            GameplayCapabilitySupportStage.LiveInstallation,
        })
            registry.RegisterStage(profile, stage, prefix + "." + stage);
    }

    private static bool HasMissingProfile(
        GameplayCapabilityCoverageReport report,
        string signature)
    {
        foreach (GameplayCapabilityCoverageIssue issue in report.Issues)
            if (issue.IsBlocking && string.Equals(
                issue.ProfileSignature,
                signature,
                StringComparison.Ordinal))
                return true;
        return false;
    }

    private static LevelDocument ReadCurrentLevel(
        string path,
        JsonSerializerOptions options) => new LevelDocumentMigrator()
            .MigrateToCurrent(ReadJson<LevelDocument>(path, options));

    private static T ReadJson<T>(
        string path,
        JsonSerializerOptions options)
    {
        T value = JsonSerializer.Deserialize<T>(
            File.ReadAllText(path),
            options);
        return value == null
            ? throw new InvalidOperationException(
                $"Content file '{path}' did not deserialize.")
            : value;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo directory = new DirectoryInfo(
            Directory.GetCurrentDirectory());
        while (directory != null)
        {
            if (File.Exists(Path.Combine(
                directory.FullName,
                "ProjectSettings",
                "ProjectVersion.txt")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException(
            "The simulation checks could not locate the repository root.");
    }

    private static bool HasIssue(
        GameplayCapabilityCoverageReport report,
        string code)
    {
        foreach (GameplayCapabilityCoverageIssue issue in report.Issues)
            if (string.Equals(issue.Code, code, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static AttackDefinition CreateRifle() => new AttackDefinition(
        "attack.rifle",
        "Fire rifle",
        new ActionCost(1, 0f, ActionMobility.Set),
        woundMovementPenalty: 2f,
        accuracyDecay: AccuracyDecayDefinition.None,
        directVehicleIntegrityDamage: 1f);

    private static bool ContainsActor(
        IReadOnlyList<string> actorIds,
        string actorId)
    {
        foreach (string candidate in actorIds)
            if (string.Equals(candidate, actorId, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class IntegratedExplosiveEvidence :
        IThrownExplosiveLandingQuery,
        IBlastWorldQuery
    {
        private readonly string firstActorId;
        private readonly string secondActorId;

        public IntegratedExplosiveEvidence(
            string firstActorId,
            string secondActorId)
        {
            this.firstActorId = firstActorId;
            this.secondActorId = secondActorId;
        }

        public ThrownExplosiveLandingResult Resolve(
            GameplayPosition launchOrigin,
            GameplayPosition sampledLanding) =>
            new ThrownExplosiveLandingResult(
                sampledLanding,
                worldStateRevision: 0L);

        public BlastWorldQueryResult Query(BlastWorldQuery query) =>
            new BlastWorldQueryResult(
                query,
                worldStateRevision: 0L,
                query.Radius <= 0f
                    ? Array.Empty<BlastEffectRecord>()
                    : new[]
                    {
                        new BlastEffectRecord(
                            firstActorId,
                            BlastSubjectKind.Actor,
                            distance: 0f,
                            occlusionExposure: 1f,
                            distanceFalloff: 1f),
                        new BlastEffectRecord(
                            secondActorId,
                            BlastSubjectKind.Actor,
                            distance: 0f,
                            occlusionExposure: 1f,
                            distanceFalloff: 1f),
                    });
    }

    private sealed class IntegratedCenterUncertaintySampler :
        IUncertaintySampler
    {
        public GameplayPosition Sample(
            GameplayPosition center,
            float radius,
            ScenarioRunIdentity run,
            GameplayTransitionIdentity transition,
            string purpose) => center;
    }

    private sealed class SimulationFixtureManifest
    {
        public int schemaVersion = -1;
        public string scenarioId = string.Empty;
        public string levelId = string.Empty;
        public List<SimulationFixtureDefinition> fixtures = new List<
            SimulationFixtureDefinition>();
    }

    private sealed class SimulationFixtureDefinition
    {
        public string id = string.Empty;
        public List<string> actorIds = new List<string>();
        public List<string> entityIds = new List<string>();
        public List<string> assertions = new List<string>();
        public string behaviorCheckId = string.Empty;
    }
}
