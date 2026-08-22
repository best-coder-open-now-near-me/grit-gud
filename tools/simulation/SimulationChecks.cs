using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
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
            VerifyLiveExplorationProjection();
            VerifyLiveSessionReducerProjection();
            VerifyFullLiveCombatProjection();
            VerifySharedPresentationSampling();
            VerifyPortableGroundSurfaces();
            VerifyStaticHeadlessSpatialGeometry();
            VerifyConcreteGroundedMoveCandidateRoute();
            VerifyConcreteTraversalCandidateRoute();
            VerifyCanonicalActionOwnedRecordSequences();
            VerifyConcreteActorAttackCandidateRoute();
            VerifyConcreteDirectFireCandidateRoute();
            VerifyConcreteProjectileCandidateRoutes();
            VerifyConcreteThrownExplosiveCandidateRoutes();
            VerifyConcreteDisplacementCandidateRoute();
            VerifyPermanentPolicyRunner();
            VerifyPermanentBattleRunner();
            VerifyLogicalExecutionGuards();
            VerifyBasicExecutableCandidateRoutes();
            VerifyLifecycleExecutableCandidateRoutes();
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
            VerifyDronePartnerBudgetPooling();
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

    private static void VerifyDronePartnerBudgetPooling()
    {
        GameplayActorSnapshot summoner = CreateDronePartnerActor("summoner");
        GameplayActorSnapshot other = CreateDronePartnerActor("other");
        var session = new GameplaySessionStateSnapshot(
            "drone-partner-budget",
            GameplaySessionMode.TurnBased,
            GameplaySessionOperation.None,
            TurnModeContext.InitiatedEncounter,
            encounterActive: true,
            encounterCompletionRequested: false,
            activeActorId: summoner.ActorId,
            GameplayTurnPhase.Normal,
            new[] { summoner, other },
            new[] { summoner.ActorId, other.ActorId },
            Array.Empty<GameplayObjectiveSnapshot>(),
            Array.Empty<string>(),
            emergencyResponderIndex: -1,
            emergencyResumeActorId: string.Empty,
            lastActionSequence: 0L,
            lastTurnSequence: 0L,
            journalSequence: 0L,
            encounterState: new GameplayEncounterStateSnapshot(
                encounterParticipantIds: new[]
                {
                    summoner.ActorId,
                    other.ActorId,
                }));
        var initial = new GameplayCombatStateSnapshot(
            session,
            coverage: GameplayCombatStateCoverage.Session
                | GameplayCombatStateCoverage.Drones,
            drones: Array.Empty<SummonedDroneSnapshot>());
        DroneArchetypeDefinition archetype = CreateDroneArchetype(
            "drone.partner",
            maximumIntegrity: 6f);
        var ability = new DroneSummonAbilityDefinition(
            "ability.partner-drone",
            archetype.ArchetypeId,
            new ActionCost(1, 0f, ActionMobility.Set),
            maximumSpawnDistance: 5f,
            maximumActiveInstances: 1,
            durationTurns: null,
            spawnHeight: 2f);
        var reducers = GameplaySimulationReducers.CreateCurrent();
        var trajectory = new List<GameplaySemanticTransition>();

        var summon = new SummonDroneRecord(
            sequence: 1L,
            summoner.ActorId,
            ability,
            archetype,
            new GameplayPosition(1f, 2f, 1f),
            spawnFacingDegrees: 0f,
            summoner.TurnBudget,
            summoner.TurnBudget.SpendAction(ability.SummonCost));
        GameplayCombatStateSnapshot summoned = Reduce(
            reducers,
            initial,
            new GameplaySummonDroneTransitionPayload(summon),
            trajectory);
        SummonedDroneSnapshot drone = summoned.Drones[0];
        TurnBudget sharedBudget = summoned.Session.GetActor(
            summoner.ActorId).TurnBudget;
        var movement = new DroneMoveRecord(
            summoner.ActorId,
            drone.DroneId,
            drone.Position,
            new GameplayPosition(2f, 2f, 1f),
            resultingFacingDegrees: 90f,
            drone.Definition.MoveCost,
            sharedBudget,
            sharedBudget.SpendAction(drone.Definition.MoveCost));
        GameplayCombatStateSnapshot moved = Reduce(
            reducers,
            summoned,
            new GameplayDroneMoveTransitionPayload(movement),
            trajectory);

        Require(initial.Drones.Count == 0
            && string.Equals(
                drone.DroneId,
                "drone:summoner:1",
                StringComparison.Ordinal)
            && drone.SummonerActorId == summoner.ActorId
            && moved.Session.GetActor(summoner.ActorId)
                .TurnBudget.ActionPoints == 2
            && moved.Session.InitiativeOrder.Count == 2
            && !ContainsActor(moved.Session.InitiativeOrder, drone.DroneId),
            "Summoner and summoned partner did not share AP without gaining an initiative slot.");

        var branch = new GameplaySimulationBranch(
            "drone-partner-replay",
            initial,
            reducers);
        foreach (GameplaySemanticTransition transition in trajectory)
            branch.Apply(transition);
        Require(GameplayExactReplay.Verify(
                initial,
                branch.Steps,
                reducers).IsExact,
            "Summon and partnered movement did not replay exactly.");
    }

    private static GameplayActorSnapshot CreateDronePartnerActor(
        string actorId) => new GameplayActorSnapshot(
            actorId,
            new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
            new TurnBudget(4, 8f),
            new ActorWoundSnapshot(actorId, 0, 0f),
            equippedItemId: null,
            equipmentEffects: EquipmentEffectSet.None,
            maximumWounds: 3,
            actionPointEconomy: new TurnActionPointEconomy(4, 4, 6),
            turnMovementAllowance: 8f);

    private static DroneArchetypeDefinition CreateDroneArchetype(
        string archetypeId,
        float maximumIntegrity = 5f) => new DroneArchetypeDefinition(
            archetypeId,
            maximumIntegrity,
            maximumMoveDistance: 5f,
            new ActionCost(1, 0f, ActionMobility.Mobile),
            new DroneSensorDefinition(16f, 120f),
            CreateDroneRifle(),
            "presentation.drone.test",
            new DroneCrashDefinition(
                impactRadius: 2.5f,
                injuryMovementPenalty: 0.75f,
                destructibleIntegrityDamage: 1f,
                maximumActionPointReduction: 1,
                maximumDriftDistance: 0.5f,
                impactPlaybackSeconds: 0.7f));

    private static void VerifyDroneHeadlessTrajectory()
    {
        LoadDepotContent(
            out GameplayScenarioAssembly assembly,
            out LevelDocument level,
            out GameplayStaticSpatialContent spatialContent);
        GameplayCombatStateSnapshot repositoryInitial =
            GameplayHeadlessBattleStateFactory.Create(
                assembly,
                spatialContent);
        Require(repositoryInitial.Drones.Count == 0,
            "Depot must begin with no summoned drone instance.");

        var gameplay = new GameplaySession(
            assembly.Scenario,
            scenarioSeed: assembly.RandomSeed);
        Require(gameplay.BeginEncounter(),
            "Drone lifecycle fixture encounter did not begin.");
        int turnGuard = 0;
        while (!string.Equals(
            gameplay.ActiveActorId,
            "player",
            StringComparison.Ordinal))
        {
            Require(turnGuard++ < gameplay.InitiativeOrder.Count,
                "Drone lifecycle fixture could not reach the summoner turn.");
            Require(gameplay.TryEndTurn(
                    gameplay.ActiveActorId,
                    out TurnEndFailure failure),
                "Drone lifecycle setup turn could not end: " + failure);
        }
        GameplaySessionStateSnapshot encounterSession =
            GameplayCombatStateCapture.Capture(gameplay).Session;
        var initial = new GameplayCombatStateSnapshot(
            encounterSession,
            repositoryInitial.Destructibles,
            repositoryInitial.Vehicles,
            repositoryInitial.Projectiles,
            repositoryInitial.SmokeFields,
            GameplayCombatStateSnapshot.AllCoverage,
            repositoryInitial.FireFields,
            Array.Empty<SummonedDroneSnapshot>());

        GameplayTransitionReducerRegistry reducers =
            GameplaySimulationReducers.CreateCurrent();
        IReadOnlyList<GameplayReachableInput> reachable =
            GameplayReachableInputEnumerator.Enumerate(assembly, level);
        GameplayCapabilityRegistry capabilities =
            GameplayCurrentCapabilityCatalog.Create(reducers, reachable);
        GameplayHeadlessSpatialEvidence spatial =
            spatialContent.CreateEvidence();
        GameplayCandidateExecutionRouteRegistry routes =
            GameplayCurrentCandidateExecutionRoutes.Create(
                assembly,
                spatial,
                capabilities);
        var builder = new GameplayHeadlessCandidateBuilder(
            capabilities,
            spatial,
            scenarioDefinition: assembly.Scenario,
            authoredTraversalLinks: level.traversalLinks,
            scenarioAssembly: assembly);
        var trajectory = new List<GameplaySemanticTransition>();

        GameplayCandidate summonCandidate = FindFirstCandidate(
            builder.Build(initial, reachable, "player"),
            GameplayCapabilityProfiles.SummonDrone());
        var summonContext = new GameplayDecisionContext(
            initial,
            GameplayObservationSnapshot.FullState("player", initial));
        GameplayExecutableCandidateEvaluation summonEvaluation =
            routes.Evaluate(summonContext, summonCandidate);
        Require(summonEvaluation.IsLegal,
            "Depot summon candidate was illegal: "
                + summonEvaluation.FailureCode);
        GameplaySemanticTransition summonTransition = routes.Prepare(
            summonContext,
            summonEvaluation);
        GameplayReductionResult summonReduction = reducers.Reduce(
            initial,
            summonTransition);
        trajectory.Add(summonTransition);
        GameplayCombatStateSnapshot summoned = summonReduction.Resulting;
        SummonedDroneSnapshot drone = summoned.Drones[0];
        Require(drone.DroneId == "drone:player:"
                + summonTransition.Identity.Sequence
            && drone.Lifecycle == SummonLifecycleState.Active
            && summoned.Session.GetActor("player").TurnBudget.ActionPoints
                == initial.Session.GetActor("player").TurnBudget.ActionPoints
                    - assembly.GetDroneSummonAbility(
                        drone.SummonAbilityId).Ability.SummonCost.ActionPoints
            && !ContainsActor(
                summoned.Session.InitiativeOrder,
                drone.DroneId),
            "Authoritative summon did not create the deterministic partnered instance.");

        foreach (GameplayCandidate candidate in builder.Build(
            summoned,
            reachable,
            "player"))
        {
            if (!candidate.Profile.Equals(
                    GameplayCapabilityProfiles.SummonDrone()))
                continue;
            Require(!routes.Evaluate(
                    new GameplayDecisionContext(
                        summoned,
                        GameplayObservationSnapshot.FullState(
                            "player",
                            summoned)),
                    candidate).IsLegal,
                "Active-instance limit did not close repeated summon candidates.");
        }

        GameplayCandidate moveCandidate = FindFirstCandidate(
            builder.Build(summoned, reachable, "player"),
            GameplayCapabilityProfiles.AerialDroneMove());
        var moveContext = new GameplayDecisionContext(
            summoned,
            GameplayObservationSnapshot.FullState("player", summoned));
        GameplayExecutableCandidateEvaluation moveEvaluation =
            routes.Evaluate(moveContext, moveCandidate);
        Require(moveEvaluation.IsLegal,
            "Summoned drone movement candidate was illegal: "
                + moveEvaluation.FailureCode);
        GameplaySemanticTransition moveTransition = routes.Prepare(
            moveContext,
            moveEvaluation);
        GameplayReductionResult moveReduction = reducers.Reduce(
            summoned,
            moveTransition);
        trajectory.Add(moveTransition);
        GameplayCombatStateSnapshot moved = moveReduction.Resulting;
        Require(moved.Drones[0].Position.DistanceTo(drone.Position) > 0f
            && moved.Session.GetActor("player").TurnBudget.ActionPoints
                < summoned.Session.GetActor("player").TurnBudget.ActionPoints,
            "Summoned drone movement did not consume the shared AP pool.");

        GameplayCandidate dismissCandidate = FindFirstCandidate(
            builder.Build(moved, reachable, "player"),
            GameplayCapabilityProfiles.DismissDrone());
        var dismissContext = new GameplayDecisionContext(
            moved,
            GameplayObservationSnapshot.FullState("player", moved));
        GameplayExecutableCandidateEvaluation dismissEvaluation =
            routes.Evaluate(dismissContext, dismissCandidate);
        Require(dismissEvaluation.IsLegal,
            "Drone dismissal candidate was illegal: "
                + dismissEvaluation.FailureCode);
        GameplaySemanticTransition dismissTransition = routes.Prepare(
            dismissContext,
            dismissEvaluation);
        GameplayReductionResult dismissReduction = reducers.Reduce(
            moved,
            dismissTransition);
        trajectory.Add(dismissTransition);
        GameplayCombatStateSnapshot dismissed = dismissReduction.Resulting;
        Require(dismissed.Drones[0].Lifecycle
                == SummonLifecycleState.Dismissed
            && !dismissed.Drones[0].IsVisible,
            "Dismissal did not preserve a terminal lifecycle record.");

        var replayBranch = new GameplaySimulationBranch(
            "summon-move-dismiss",
            initial,
            reducers);
        foreach (GameplaySemanticTransition transition in trajectory)
            replayBranch.Apply(transition);
        Require(GameplayExactReplay.Verify(
                initial,
                replayBranch.Steps,
                reducers).IsExact,
            "Summon, movement, and dismissal did not replay exactly.");
        Require(GameplayHeadlessBattleStateFactory.Create(
                assembly,
                spatialContent).Drones.Count == 0,
            "A new Depot session retained a prior summoned instance.");

        VerifyDroneExpiration(reducers);
        VerifyDroneCrashLifecycle(
            assembly,
            spatialContent,
            repositoryInitial,
            reducers);
    }

    private static void VerifyDroneExpiration(
        GameplayTransitionReducerRegistry reducers)
    {
        GameplayActorSnapshot summoner = CreateDronePartnerActor("summoner");
        GameplayActorSnapshot other = CreateDronePartnerActor("other");
        var session = new GameplaySessionStateSnapshot(
            "drone-expiration",
            GameplaySessionMode.TurnBased,
            GameplaySessionOperation.None,
            TurnModeContext.InitiatedEncounter,
            encounterActive: true,
            encounterCompletionRequested: false,
            activeActorId: summoner.ActorId,
            GameplayTurnPhase.Normal,
            new[] { summoner, other },
            new[] { summoner.ActorId, other.ActorId },
            Array.Empty<GameplayObjectiveSnapshot>(),
            Array.Empty<string>(),
            emergencyResponderIndex: -1,
            emergencyResumeActorId: string.Empty,
            lastActionSequence: 0L,
            lastTurnSequence: 0L,
            journalSequence: 0L);
        var initial = new GameplayCombatStateSnapshot(
            session,
            coverage: GameplayCombatStateCoverage.Session
                | GameplayCombatStateCoverage.Drones,
            drones: Array.Empty<SummonedDroneSnapshot>());
        DroneArchetypeDefinition archetype = CreateDroneArchetype(
            "drone.expiring");
        var ability = new DroneSummonAbilityDefinition(
            "ability.expiring-drone",
            archetype.ArchetypeId,
            new ActionCost(1, 0f, ActionMobility.Set),
            maximumSpawnDistance: 5f,
            maximumActiveInstances: 1,
            durationTurns: 1,
            spawnHeight: 2f);
        var summon = new SummonDroneRecord(
            1L,
            summoner.ActorId,
            ability,
            archetype,
            new GameplayPosition(0f, 2f, 0f),
            0f,
            summoner.TurnBudget,
            summoner.TurnBudget.SpendAction(ability.SummonCost));
        GameplayReductionResult afterSummon = reducers.Reduce(
            initial,
            CreateTransition(
                initial,
                new GameplaySummonDroneTransitionPayload(summon),
                1L));
        GameplayReductionResult afterTurn = reducers.Reduce(
            afterSummon.Resulting,
            CreateTransition(
                afterSummon.Resulting,
                new GameplayEndTurnTransitionPayload(
                    summoner.ActorId,
                    emergency: false),
                2L));
        bool hasExpirationEvent = false;
        foreach (GameplayDomainEvent domainEvent in afterTurn.DomainEvents)
            hasExpirationEvent |= domainEvent is GameplayDroneExpiredEvent;
        Require(afterTurn.Resulting.Drones[0].Lifecycle
                == SummonLifecycleState.Expired
            && hasExpirationEvent,
            "Authored summon duration did not expire on its owner's turn end.");
    }

    private static void VerifyDroneCrashLifecycle(
        GameplayScenarioAssembly assembly,
        GameplayStaticSpatialContent spatialContent,
        GameplayCombatStateSnapshot repositoryInitial,
        GameplayTransitionReducerRegistry reducers)
    {
        var gameplay = new GameplaySession(
            assembly.Scenario,
            scenarioSeed: assembly.RandomSeed);
        Require(gameplay.BeginEncounter(),
            "Drone crash fixture encounter did not begin.");
        GameplaySessionStateSnapshot session =
            GameplayCombatStateCapture.Capture(gameplay).Session;
        string actorId = session.ActiveActorId;
        GameplayActorSnapshot actor = session.GetActor(actorId);
        DroneArchetypeDefinition archetype = CreateDroneArchetype(
            "drone.crash-target",
            maximumIntegrity: 1f);
        var ability = new DroneSummonAbilityDefinition(
            "ability.crash-target",
            archetype.ArchetypeId,
            new ActionCost(0, 0f, ActionMobility.Set),
            maximumSpawnDistance: 5f,
            maximumActiveInstances: 1,
            durationTurns: null,
            spawnHeight: 2f);
        GameplayPosition origin = new GameplayPosition(
            actor.Pose.Position.X,
            actor.Pose.Position.Y + 2f,
            actor.Pose.Position.Z);
        SummonedDroneSnapshot target = new SummonDroneRecord(
            1L,
            actorId,
            ability,
            archetype,
            origin,
            actor.Pose.FacingDegrees,
            actor.TurnBudget,
            actor.TurnBudget).Resulting;
        var initial = new GameplayCombatStateSnapshot(
            session,
            repositoryInitial.Destructibles,
            repositoryInitial.Vehicles,
            repositoryInitial.Projectiles,
            repositoryInitial.SmokeFields,
            GameplayCombatStateSnapshot.AllCoverage,
            repositoryInitial.FireFields,
            new[] { target });
        GameplayHeadlessSpatialEvidence spatial =
            spatialContent.CreateEvidence();
        long actionSequence = checked(session.LastActionSequence + 1L);
        long transitionSequence = checked(
            session.LastTransitionSequence + 1L);
        var identity = new GameplayTransitionIdentity(
            actionSequence,
            GameplaySemanticCapability.DirectAttack.ToString(),
            actorId,
            target.DroneId);
        uint seed = GameplayAddressedRandom.SampleUInt32(
            session.RunIdentity,
            identity,
            "resolution");
        ActorDroneAttackRecord attack = DroneDirectAttackRules.Resolve(
            actionSequence,
            seed,
            actorId,
            CreateRifle(),
            actor.TurnBudget,
            new DroneExposureSnapshot(actorId, target.DroneId, 1, 1),
            distance: 0f,
            target,
            crashTrajectory: spatial.ResolveDroneCrashTrajectory(
                target.Position,
                actor.Pose.Position,
                archetype.Crash.MaximumDriftDistance,
                transitionSequence));
        var attackPayload = new GameplayActorDroneAttackTransitionPayload(
            CreateRifle(),
            attack);
        GameplaySemanticTransition attackTransition = CreateTransition(
            initial,
            attackPayload,
            transitionSequence);
        GameplayReductionResult disabled = reducers.Reduce(
            initial,
            attackTransition);
        Require(disabled.Resulting.Drones[0].Lifecycle
                == SummonLifecycleState.Crashing
            && GameplayMandatoryWorkRules.HasPending(disabled.Resulting),
            "Lethal drone integrity damage did not start mandatory crashing.");

        var crashCandidate = new GameplayCandidate(
            "fixture.drone-crash",
            GameplayCapabilityProfiles.AdvanceDroneCrash(),
            actorId,
            target.DroneId,
            new GameplayDroneCrashIntent(target.DroneId));
        var crashRoute = new GameplayDroneCrashCandidateExecutionRoute(spatial);
        var crashContext = new GameplayDecisionContext(
            disabled.Resulting,
            GameplayObservationSnapshot.FullState(
                actorId,
                disabled.Resulting));
        GameplayExecutableCandidateEvaluation crashEvaluation =
            crashRoute.Evaluate(crashContext, crashCandidate);
        Require(crashEvaluation.IsLegal,
            "Drone crash impact candidate was illegal: "
                + crashEvaluation.FailureCode);
        GameplayCapabilityRegistry crashCapabilities =
            GameplayCurrentCapabilityCatalog.Create(
                reducers,
                new[]
                {
                    new GameplayReachableInput(
                        GameplayReachableInputKind.SystemContinuation,
                        "fixture.drone-crash",
                        actorId,
                        GameplayCapabilityProfiles.AdvanceDroneCrash()),
                });
        var crashRoutes = new GameplayCandidateExecutionRouteRegistry(
            crashCapabilities);
        crashRoutes.Register(crashRoute);
        GameplaySemanticTransition crashTransition = crashRoutes.Prepare(
            crashContext,
            crashEvaluation);
        GameplayReductionResult impacted = reducers.Reduce(
            disabled.Resulting,
            crashTransition);
        DroneCrashImpactRecord impact = (DroneCrashImpactRecord)
            ((GameplayTransitionReducedEvent)impacted.DomainEvents[0])
            .SemanticRecord;
        Require(impacted.Resulting.Drones[0].Lifecycle
                == SummonLifecycleState.Destroyed
            && impacted.Resulting.Drones[0].Position.DistanceTo(
                impact.ImpactPosition) == 0f
            && impact.Effects.Count > 0
            && impacted.Resulting.FireFields.Count
                == disabled.Resulting.FireFields.Count
            && !GameplayMandatoryWorkRules.HasPending(impacted.Resulting),
            "Crash impact did not create a modest localized wreck without fire.");

        var branch = new GameplaySimulationBranch(
            "drone-crash-replay",
            initial,
            reducers);
        branch.Apply(attackTransition);
        branch.Apply(crashTransition);
        Require(GameplayExactReplay.Verify(
                initial,
                branch.Steps,
                reducers).IsExact,
            "Drone disable, fall, impact, and reactions did not replay exactly.");
        var replay = new GameplaySemanticReplayTimeline(
            initial,
            branch.Steps,
            reducers);
        GameplaySemanticReplayFrame crashFrame = null;
        foreach (GameplaySemanticReplayFrame frame in replay.Frames)
            if (frame.SemanticRecord is DroneCrashImpactRecord)
            {
                crashFrame = frame;
                break;
            }
        Require(crashFrame != null,
            "Drone crash transition was absent from semantic replay.");
        GameplayPresentationWorldStateSample falling =
            GameplaySemanticReplaySampler.Sample(crashFrame, 0.5f);
        SummonedDroneSnapshot fallingDrone = default;
        foreach (SummonedDroneSnapshot candidate in falling.Drones)
            if (candidate.DroneId == target.DroneId)
            {
                fallingDrone = candidate;
                break;
            }
        bool hasImpactEvent = false;
        int replayReactions = 0;
        foreach (ReplayCombatPresentationEvent presentationEvent in
            ReplayCombatPresentationEventProjector.Project(crashFrame))
        {
            hasImpactEvent |= presentationEvent.Kind ==
                ReplayCombatPresentationEventKind.DroneCrashImpact;
            if (presentationEvent.Kind ==
                ReplayCombatPresentationEventKind.Reaction)
                replayReactions++;
        }
        int injuredActors = 0;
        foreach (BlastEffectRecord effect in impact.Effects)
            if (effect.IsLocalizedActorInjury) injuredActors++;
        Require(fallingDrone.Lifecycle == SummonLifecycleState.Crashing
            && fallingDrone.Position.DistanceTo(impact.Origin) > 0f
            && fallingDrone.Position.DistanceTo(impact.ImpactPosition) > 0f
            && hasImpactEvent
            && replayReactions == injuredActors,
            "Replay did not present the frozen fall, impact, and localized reactions.");
    }

    private static GameplayCandidate FindFirstCandidate(
        IEnumerable<GameplayCandidate> candidates,
        GameplayCapabilityProfile profile)
    {
        foreach (GameplayCandidate candidate in candidates)
            if (candidate.Profile.Equals(profile)) return candidate;
        throw new InvalidOperationException(
            "Expected gameplay candidate was not constructed for '"
            + profile.Signature + "'.");
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
            assembly.DroneArchetypes,
            destructibles);
        using (var fireFields = new GameplayFireFieldSession(
            gameplay,
            destructibles))
        {
            var evidence = new IntegratedExplosiveEvidence(
                "oren-vale",
                "depot-rifleman",
                () => gameplay.Journal.LastEntry?.Sequence ?? 0L);
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

            ScenarioDroneSummonRuntimeDefinition summonRuntime =
                assembly.GetDroneSummonAbility("ability.summon-drone");
            DroneArchetypeDefinition droneDefinition =
                assembly.GetDroneArchetype(
                    summonRuntime.Ability.DroneArchetypeId);
            GameplayActorSnapshot summoner = gameplay.GetActor("player");
            var summon = new SummonDroneRecord(
                checked(predicted.Session.LastTransitionSequence + 1L),
                "player",
                summonRuntime.Ability,
                droneDefinition,
                new GameplayPosition(
                    summoner.Pose.Position.X,
                    summoner.Pose.Position.Y
                        + summonRuntime.Ability.SpawnHeight,
                    summoner.Pose.Position.Z),
                summoner.Pose.FacingDegrees,
                summoner.TurnBudget,
                summoner.TurnBudget.SpendAction(
                    summonRuntime.Ability.SummonCost));
            drones.CommitSummon(summon);
            predicted = VerifyIntegratedStep(
                "Depot drone summon",
                predicted,
                new GameplaySummonDroneTransitionPayload(summon),
                gameplay,
                destructibles,
                fireFields,
                drones,
                reducers,
                trajectory);
            SummonedDroneSnapshot drone = drones.GetDrone(
                summon.DroneInstanceId);
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
            long attackSequence = checked(
                predicted.Session.LastTransitionSequence + 1L);
            AttackResolutionRecord droneResolution =
                AttackResolutionRules.Resolve(
                    attackSequence,
                    GameplayAddressedRandom.SampleUInt32(
                        gameplay.RunIdentity,
                        new GameplayTransitionIdentity(
                            attackSequence,
                            GameplaySemanticCapability.DirectAttack.ToString(),
                            drone.SummonerActorId,
                            droneTarget.ActorId),
                        "resolution"),
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
        out LevelDocument level) => SimulationRepositoryContent.LoadDepot(
            out assembly,
            out level);

    private static void LoadDepotContent(
        out GameplayScenarioAssembly assembly,
        out LevelDocument level,
        out GameplayStaticSpatialContent spatialContent) =>
        SimulationRepositoryContent.LoadDepot(
            out assembly,
            out level,
            out spatialContent);

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

    private static GameplaySession CreateHostileGameplay(
        AttackDefinition rifle)
    {
        var player = new ScenarioActorDefinition(
            "player",
            10,
            new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
            new TurnBudget(4, 8f),
            rifle,
            combat: new ActorCombatDefinition(
                "player",
                new[] { "raider" },
                maximumWounds: 2));
        var enemy = new ScenarioActorDefinition(
            "enemy",
            0,
            new GameplayActorPose(
                new GameplayPosition(1f, 0f, 5f),
                180f),
            new TurnBudget(4, 8f),
            rifle,
            combat: new ActorCombatDefinition(
                "raider",
                new[] { "player" },
                maximumWounds: 2));
        var scenario = new ScenarioDefinition(
            "hostile-simulation-check",
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
        var routeLevel = new LevelDocument
        {
            levelId = "encounter-route-level",
            schemaVersion = LevelDocument.CurrentSchemaVersion,
            entities = new List<LevelEntity>
            {
                new LevelEntity
                {
                    id = "encounter-route-floor",
                    archetypeId = "structure.floor.standard",
                    transform = new LevelTransformData(
                        new Float3Data(0f, 0f, 0f),
                        yawDegrees: 0f),
                    placementSurface = new LevelPlacementSurfaceData
                    {
                        kind = LevelPlacementSurfaceData.FlatKind,
                        size = new Float3Data(40f, 0f, 40f),
                    },
                },
            },
        };
        routeLevel.Normalize();
        var routeSpatialIdentity = new SpatialContentIdentity(
            routeLevel.levelId,
            routeLevel.schemaVersion,
            evidenceAlgorithmVersion: 1,
            new string('2', 64));
        var routeSpatial = new GameplayHeadlessSpatialEvidence(
            routeLevel,
            routeSpatialIdentity);
        IReadOnlyList<GameplayReachableInput> routeInputs =
            GameplayReachableInputEnumerator.Enumerate(
                live.Scenario,
                routeLevel);
        GameplayCapabilityRegistry routeCapabilities =
            GameplayCurrentCapabilityCatalog.Create(reducers, routeInputs);
        var executableRoutes = new GameplayCandidateExecutionRouteRegistry(
            routeCapabilities);
        executableRoutes.Register(
            new GameplayEncounterObservationCandidateExecutionRoute(
                live.Scenario,
                routeSpatial));
        executableRoutes.Register(new GameplayPatrolCandidateExecutionRoute(
            live.Scenario,
            routeSpatial));
        IReadOnlyList<GameplayCandidate> routeCandidates =
            new GameplayTacticalCandidateBuilder(routeCapabilities).Build(
                initial,
                routeInputs);
        GameplayCandidate observationCandidate = FindCandidate(
            routeCandidates,
            "enemy",
            GameplaySemanticCapability.ObserveEncounter,
            "enemy");
        GameplayCandidate patrolCandidate = FindCandidate(
            routeCandidates,
            "enemy",
            GameplaySemanticCapability.Patrol,
            "enemy");
        var observationRuntime = new GameplaySimulationRuntime(
            new GameplayExecutionIdentity(
                new GameplayContentIdentity(
                    live.Scenario.Id,
                    scenarioSchemaVersion: 1,
                    rulesSchemaVersion: 1,
                    new string('1', 64)),
                routeSpatialIdentity,
                live.RunIdentity),
            initial,
            reducers,
            routeCapabilities);
        ExecuteCandidate(
            observationRuntime,
            executableRoutes,
            observationCandidate);
        Require(observationRuntime.Trajectory.Count == 1
            && GameplayExactReplay.Verify(
                initial,
                observationRuntime.Trajectory,
                reducers).IsExact,
            "Encounter observation route did not reduce and replay exactly.");
        var patrolRuntime = new GameplaySimulationRuntime(
            observationRuntime.ExecutionIdentity,
            initial,
            reducers,
            routeCapabilities);
        ExecuteCandidate(patrolRuntime, executableRoutes, patrolCandidate);
        Require(patrolRuntime.CurrentState.Session.GetActor("enemy")
                .Pose.Position.DistanceTo(
                    initial.Session.GetActor("enemy").Pose.Position) > 0f
            && GameplayExactReplay.Verify(
                initial,
                patrolRuntime.Trajectory,
                reducers).IsExact,
            "Patrol route did not reduce and replay exactly.");
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
                payload.Profile.Capability.ToString(),
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

        var staleRuntime = new GameplaySimulationRuntime(
            executionIdentity,
            initial,
            reducers,
            capabilities);
        GameplayReductionResult staleReduction = staleRuntime
            .PrepareReduction(transition);
        staleRuntime.Execute(transition);
        GameplayStaleDecisionStateException staleFailure = null;
        try
        {
            staleRuntime.InstallPreparedReduction(
                transition,
                staleReduction);
        }
        catch (GameplayStaleDecisionStateException exception)
        {
            staleFailure = exception;
        }
        Require(staleFailure != null
            && string.Equals(
                staleFailure.PreparedStateHash,
                initial.CanonicalHash,
                StringComparison.Ordinal)
            && string.Equals(
                staleFailure.CurrentStateHash,
                staleRuntime.CurrentState.CanonicalHash,
                StringComparison.Ordinal),
            "A stale prepared reduction did not fail with typed canonical endpoints.");

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

        var failingProjectionRuntime = new GameplaySimulationRuntime(
            executionIdentity,
            initial,
            reducers,
            capabilities);
        bool domainEventSurvivedProjectionFailure = false;
        failingProjectionRuntime.StateInstalled += _ =>
            throw new InvalidOperationException("projection check failure");
        failingProjectionRuntime.DomainEventPublished += _ =>
            domainEventSurvivedProjectionFailure = true;
        bool projectionFailed = false;
        try
        {
            failingProjectionRuntime.Execute(transition);
        }
        catch (AggregateException)
        {
            projectionFailed = true;
        }
        Require(projectionFailed
            && domainEventSurvivedProjectionFailure
            && failingProjectionRuntime.CurrentState.Session
                .LastTransitionSequence == 1L
            && failingProjectionRuntime.Trajectory.Count == 1,
            "A failed live projection rolled back authority or suppressed reducer domain events.");
    }

    private static void VerifyLiveSessionReducerProjection()
    {
        GameplaySession gameplay = CreateEncounterGameplay();
        Require(gameplay.BeginEncounter(),
            "Live projection fixture encounter did not begin.");
        GameplayCombatStateSnapshot initial =
            GameplayCombatStateCapture.Capture(gameplay);
        GameplayTransitionReducerRegistry reducers =
            GameplaySimulationReducers.CreateCurrent();
        GameplayCapabilityProfile liveAttackProfile =
            GameplayCapabilityProfiles.Attack(
                gameplay.GetEquippedAttack(initial.Session.ActiveActorId),
                GameplaySemanticSubjectKind.Actor);
        GameplayCapabilityRegistry capabilities =
            GameplayCurrentCapabilityCatalog.Create(
                reducers,
                new[]
                {
                    new GameplayReachableInput(
                        GameplayReachableInputKind.EquippedAttack,
                        "live-projection-attack",
                        initial.Session.ActiveActorId,
                        liveAttackProfile),
                });
        var executionIdentity = new GameplayExecutionIdentity(
            new GameplayContentIdentity(
                initial.Session.ScenarioId,
                scenarioSchemaVersion: 1,
                rulesSchemaVersion: 1,
                new string('6', 64)),
            new SpatialContentIdentity(
                "live-projection-check",
                levelSchemaVersion: 1,
                evidenceAlgorithmVersion: 1,
                new string('7', 64)),
            initial.Session.RunIdentity);
        using var live = new GameplayLiveSessionRuntime(
            gameplay,
            executionIdentity,
            initial,
            reducers,
            capabilities);

        int capabilityNotifications = 0;
        int activeActorNotifications = 0;
        int turnNotifications = 0;
        bool domainEventSawProjectedState = false;
        gameplay.ActorCapabilityChanged += _ => capabilityNotifications++;
        gameplay.ActiveActorChanged += _ => activeActorNotifications++;
        gameplay.TurnEnded += _ => turnNotifications++;
        live.DomainEventPublished += _ =>
            domainEventSawProjectedState = string.Equals(
                GameplayCombatStateCapture.Capture(gameplay).CanonicalHash,
                new GameplayCombatStateSnapshot(live.CurrentState.Session)
                    .CanonicalHash,
                StringComparison.Ordinal);

        GameplayActorSnapshot actor = initial.Session.GetActor(
            initial.Session.ActiveActorId);
        var route = new MovementRouteRecord(
            actor.ActorId,
            actor.Pose,
            actor.TurnBudget,
            new[]
            {
                new MovementRouteSegmentRecord(
                    actor.Pose.Position,
                    new GameplayPosition(
                        actor.Pose.Position.X + 1f,
                        actor.Pose.Position.Y,
                        actor.Pose.Position.Z),
                    movementCost: 1f,
                    playbackDurationSeconds: 0.25f),
            });
        gameplay.CommitMovementRoute(route);

        GameplayCombatStateSnapshot projectedMove =
            GameplayCombatStateCapture.Capture(gameplay);
        Require(string.Equals(
                projectedMove.CanonicalHash,
                new GameplayCombatStateSnapshot(live.CurrentState.Session)
                    .CanonicalHash,
                StringComparison.Ordinal)
            && gameplay.Operation == GameplaySessionOperation.None
            && gameplay.WorldStateRevision
                == live.CurrentState.Session.JournalSequence
            && gameplay.WorldStateRevision
                > (gameplay.Journal.LastEntry?.Sequence ?? 0L)
            && capabilityNotifications == 1
            && domainEventSawProjectedState,
            "Reducer-owned movement did not expose its canonical spatial revision to presentation.");

        bool legacyMovementRejected = false;
        try
        {
            gameplay.CommitMovementRoute(route);
        }
        catch (InvalidOperationException)
        {
            legacyMovementRejected = true;
        }
        Require(legacyMovementRejected,
            "A projection-bound live session still allowed duplicate movement mutation.");

        var attacks = new GameplayAttackSession(gameplay);
        var exposure = new TargetExposureSnapshot(
            gameplay.ActiveActorId,
            "enemy",
            new[]
            {
                new TargetRegionExposure(
                    TargetRegionId.Torso,
                    visibleSampleCount: 1,
                    totalSampleCount: 1),
            });
        Require(attacks.TryResolve(
                gameplay.ActiveActorId,
                exposure,
                out GameplayActionRecord liveAction,
                out AttackResolutionFailure liveActionFailure)
            && liveAction.Sequence == 1L
            && gameplay.LastActionSequence == 1L
            && attacks.Records.Count == 1
            && string.Equals(
                GameplayCombatStateCapture.Capture(gameplay).CanonicalHash,
                new GameplayCombatStateSnapshot(live.CurrentState.Session)
                    .CanonicalHash,
                StringComparison.Ordinal),
            "A fresh live action did not execute exactly once through its semantic reducer: "
                + liveActionFailure);

        string endingActorId = gameplay.ActiveActorId;
        Require(gameplay.TryEndTurn(
                endingActorId,
                out TurnEndFailure liveTurnFailure)
            && liveTurnFailure == TurnEndFailure.None,
            "The live end-turn adapter rejected a legal turn: "
                + liveTurnFailure);

        GameplayCombatStateSnapshot projectedTurn =
            GameplayCombatStateCapture.Capture(gameplay);
        Require(string.Equals(
                projectedTurn.CanonicalHash,
                new GameplayCombatStateSnapshot(live.CurrentState.Session)
                    .CanonicalHash,
                StringComparison.Ordinal)
            && !string.Equals(
                gameplay.ActiveActorId,
                endingActorId,
                StringComparison.Ordinal)
            && activeActorNotifications == 1
            && turnNotifications == 1
            && gameplay.LastEndedTurn?.Sequence == 1L,
            "Reducer-owned turn completion did not install state and lifecycle notifications exactly once.");
        Require(GameplayExactReplay.Verify(
                initial,
                live.Trajectory,
                reducers).IsExact,
            "The reducer-owned live projection trajectory did not replay exactly.");

        int trajectoryCount = live.Trajectory.Count;
        Require(!gameplay.TryEndTurn(
                endingActorId,
                out TurnEndFailure staleTurnFailure)
            && staleTurnFailure == TurnEndFailure.ActorNotActive
            && live.Trajectory.Count == trajectoryCount,
            "A stale live turn command mutated canonical state.");
    }

    private static void VerifyLiveExplorationProjection()
    {
        GameplaySession gameplay = CreateEncounterGameplay();
        GameplayCombatStateSnapshot initial =
            GameplayCombatStateCapture.Capture(gameplay);
        GameplayTransitionReducerRegistry reducers =
            GameplaySimulationReducers.CreateCurrent();
        GameplayCapabilityRegistry capabilities =
            GameplayCurrentCapabilityCatalog.Create(
                reducers,
                Array.Empty<GameplayReachableInput>());
        var identity = new GameplayExecutionIdentity(
            new GameplayContentIdentity(
                initial.Session.ScenarioId,
                scenarioSchemaVersion: 1,
                rulesSchemaVersion: 1,
                new string('8', 64)),
            new SpatialContentIdentity(
                "live-exploration-check",
                levelSchemaVersion: 1,
                evidenceAlgorithmVersion: 1,
                new string('9', 64)),
            gameplay.RunIdentity);
        using var live = new GameplayLiveSessionRuntime(
            gameplay,
            identity,
            initial,
            reducers,
            capabilities);
        GameplayActorSnapshot actor = gameplay.GetActor(
            gameplay.Scenario.PlayerParty.InitiallySelectedActorId);
        var resultingPose = new GameplayActorPose(
            new GameplayPosition(
                actor.Pose.Position.X + 0.5f,
                actor.Pose.Position.Y,
                actor.Pose.Position.Z),
            actor.Pose.FacingDegrees + 15f,
            actor.Pose.Stance);
        gameplay.AdvanceExploration(
            actor.ActorId,
            resultingPose,
            elapsedSeconds: 0.1f);
        Require(gameplay.GetActor(actor.ActorId).Pose.Position.DistanceTo(
                    resultingPose.Position) == 0f
                && live.Trajectory.Count == 1
                && string.Equals(
                    GameplayCombatStateCapture.Capture(gameplay).CanonicalHash,
                    live.CurrentState.CanonicalHash,
                    StringComparison.Ordinal),
            "Exploration locomotion was not installed through the canonical world transition.");
        gameplay.AdvanceExploration(
            actor.ActorId,
            resultingPose,
            elapsedSeconds: 0.1f);
        Require(live.Trajectory.Count == 1,
            "Idle exploration emitted a duplicate transition without advancing world state.");
        Require(gameplay.TryEnterTurnMode(out TurnModeEntryFailure failure)
                && failure == TurnModeEntryFailure.None
                && live.Trajectory.Count == 2
                && GameplayExactReplay.Verify(
                    initial,
                    live.Trajectory,
                    reducers).IsExact,
            "Exploration-to-turn transition did not preserve exact semantic replay: "
                + failure);
    }

    private static void VerifyFullLiveCombatProjection()
    {
        GameplaySession gameplay = CreateEncounterGameplay();
        Require(gameplay.BeginEncounter(),
            "Full live projection encounter did not begin.");
        var destructibles = new DestructiblePropSession(
            new[]
            {
                new DestructiblePropDefinition(
                    "projection-crate",
                    maximumIntegrity: 3f,
                    DestructiblePropState.Intact,
                    new GameplayPosition(2f, 0f, 2f)),
            },
            gameplay.Journal);
        using var smokeFields = new GameplaySmokeFieldSession(gameplay);
        using var fireFields = new GameplayFireFieldSession(
            gameplay,
            destructibles);
        smokeFields.Deploy(new SmokeFieldRecord(
            "smoke.projection",
            "player",
            "item.smoke",
            new GameplayPosition(20f, 0f, 20f),
            new SmokeFieldDefinition(
                radius: 2f,
                height: 2f,
                explorationDurationSeconds: 8f,
                durationTurnEnds: 4,
                minimumObscuredPath: 0.5f)));
        fireFields.Deploy(new FireFieldRecord(
            "fire.projection",
            "player",
            "item.incendiary",
            new GameplayPosition(2f, 0f, 2f),
            new FireFieldDefinition(
                initialRadius: 1f,
                maximumRadius: 2f,
                height: 2f,
                explorationDurationSeconds: 12f,
                durationTurnEnds: 6,
                explorationPulseSeconds: 2f,
                actorWoundMovementPenalty: 1f,
                destructibleIntegrityDamage: 1f,
                minimumHazardPath: 0.5f)));

        var projectiles = new GameplayProjectileSession(
            gameplay,
            new AlwaysClearProjectileQuery(),
            new GameplayBlastConsequenceResolver(
                gameplay,
                destructibles));
        var vehicle = new VehicleMomentumSession(
            new VehicleMomentumProfile(
                maximumSpeed: 4f,
                accelerationPerTurn: 1f,
                brakingPerTurn: 1f,
                lowSpeedTurnDegrees: 90f,
                highSpeedTurnDegrees: 30f,
                baseTurningRadius: 0.25f,
                speedTurningRadiusFactor: 0.1f),
            new VehicleMomentumState(
                "vehicle.projection",
                new GameplayPosition(12f, 0f, 12f),
                forwardDegrees: 0f,
                speed: 0f),
            gameplay.Journal);
        string droneControllerId = gameplay.InitiativeOrder[1];
        DroneArchetypeDefinition droneDefinition = CreateDroneArchetype(
            "drone.projection");
        var droneAbility = new DroneSummonAbilityDefinition(
            "ability.projection-drone",
            droneDefinition.ArchetypeId,
            new ActionCost(1, 0f, ActionMobility.Set),
            maximumSpawnDistance: 5f,
            maximumActiveInstances: 1,
            durationTurns: null,
            spawnHeight: 2f);
        var drones = new GameplayDroneSession(
            gameplay,
            new[] { droneDefinition },
            destructibles);
        var projection = new GameplayLiveCombatProjection(
            gameplay,
            destructibles,
            new[] { vehicle },
            projectiles,
            smokeFields,
            fireFields,
            drones);
        GameplayCombatStateSnapshot initial = projection.Capture();
        GameplayTransitionReducerRegistry reducers =
            GameplaySimulationReducers.CreateCurrent();
        var projectileDefinition = new ProjectileFlightDefinition(
            "projectile.projection",
            speedPerTurn: 2f,
            radius: 0.1f,
            maximumRange: 10f,
            standingLaunchHeight: 1f,
            crouchedLaunchHeight: 0.7f);
        var launcher = new AttackDefinition(
            "attack.projection-launcher",
            "Projection launcher",
            new ActionCost(1, 0f, ActionMobility.Set),
            woundMovementPenalty: 1f,
            projectileDefinition);
        GameplayCapabilityProfile launcherProfile =
            GameplayCapabilityProfiles.Attack(
                launcher,
                GameplaySemanticSubjectKind.Actor);
        GameplayCapabilityRegistry capabilities =
            GameplayCurrentCapabilityCatalog.Create(
                reducers,
                new[]
                {
                    new GameplayReachableInput(
                        GameplayReachableInputKind.CharacterAbility,
                        "projection.launcher",
                        droneControllerId,
                        launcherProfile),
                });
        var identity = new GameplayExecutionIdentity(
            new GameplayContentIdentity(
                initial.Session.ScenarioId,
                scenarioSchemaVersion: 1,
                rulesSchemaVersion: 1,
                new string('4', 64)),
            new SpatialContentIdentity(
                "full-live-projection-check",
                levelSchemaVersion: 1,
                evidenceAlgorithmVersion: 1,
                new string('5', 64)),
            initial.Session.RunIdentity);
        using var live = new GameplayLiveSessionRuntime(
            projection,
            identity,
            initial,
            reducers,
            capabilities);

        int damaged = 0;
        int fireChanged = 0;
        int turnEnded = 0;
        destructibles.Damaged += _ => damaged++;
        fireFields.FieldChanged += _ => fireChanged++;
        gameplay.TurnEnded += _ => turnEnded++;
        string actorId = initial.Session.ActiveActorId;
        Require(gameplay.TryEndTurn(
                actorId,
                out TurnEndFailure projectionTurnFailure)
            && projectionTurnFailure == TurnEndFailure.None,
            "Full live projection rejected a legal end-turn command: "
                + projectionTurnFailure);

        GameplayCombatStateSnapshot installed = projection.Capture();
        Require(string.Equals(
                installed.CanonicalHash,
                live.CurrentState.CanonicalHash,
                StringComparison.Ordinal)
            && installed.SmokeFields[0].RemainingFraction == 0.75f
            && installed.FireFields[0].RemainingFraction
                == 1f - (1f / 6f)
            && installed.Destructibles[0].RemainingIntegrity == 2f
            && damaged == 1
            && fireChanged == 1
            && turnEnded == 1,
            "Full live projection did not install fire, smoke, destructible, and turn state exactly once.");

        GameplayActorSnapshot controller = live.CurrentState.Session.GetActor(
            droneControllerId);
        var summon = new SummonDroneRecord(
            checked(live.CurrentState.Session.LastTransitionSequence + 1L),
            droneControllerId,
            droneAbility,
            droneDefinition,
            new GameplayPosition(
                controller.Pose.Position.X,
                controller.Pose.Position.Y + droneAbility.SpawnHeight,
                controller.Pose.Position.Z),
            controller.Pose.FacingDegrees,
            controller.TurnBudget,
            controller.TurnBudget.SpendAction(droneAbility.SummonCost));
        drones.CommitSummon(summon);
        controller = live.CurrentState.Session.GetActor(droneControllerId);
        SummonedDroneSnapshot drone = live.CurrentState.Drones[0];
        var droneMove = new DroneMoveRecord(
            droneControllerId,
            drone.DroneId,
            drone.Position,
            new GameplayPosition(
                drone.Position.X + 1f,
                drone.Position.Y,
                drone.Position.Z),
            resultingFacingDegrees: 90f,
            drone.Definition.MoveCost,
            controller.TurnBudget,
            controller.TurnBudget.SpendAction(
                drone.Definition.MoveCost));
        drones.CommitMove(droneMove);
        Require(drones.GetDrone(drone.DroneId).Position.DistanceTo(
                droneMove.Destination) == 0f,
            "Reducer-owned drone movement was not installed live.");

        VehicleMomentumState previousVehicle = vehicle.State;
        var resultingVehicle = new VehicleMomentumState(
            previousVehicle.VehicleId,
            new GameplayPosition(
                previousVehicle.Position.X,
                previousVehicle.Position.Y,
                previousVehicle.Position.Z + 0.5f),
            forwardDegrees: 0f,
            speed: 1f);
        var vehicleMove = new VehicleMomentumRecord(
            live.CurrentState.Session.LastTransitionSequence + 1L,
            previousVehicle,
            resultingVehicle,
            new[]
            {
                previousVehicle.Position,
                resultingVehicle.Position,
            });
        vehicle.Commit(vehicleMove);
        Require(vehicle.State.Position.DistanceTo(
                resultingVehicle.Position) == 0f
            && vehicle.Records.Count == 1,
            "Reducer-owned vehicle movement was not installed live.");

        controller = live.CurrentState.Session.GetActor(droneControllerId);
        string projectileId = "projectile.projection.1";
        GameplayPosition launchOrigin = projectileDefinition.GetLaunchOrigin(
            controller.Pose);
        GameplayPosition aimPoint = new GameplayPosition(
            launchOrigin.X,
            launchOrigin.Y,
            launchOrigin.Z + 8f);
        TurnBudget launchBudget = controller.TurnBudget.SpendAction(
            launcher.TurnCost);
        var launch = new ProjectileLaunchRecord(
            sequence: 1L,
            projectileId,
            droneControllerId,
            "enemy",
            launcher.ActionId,
            launchOrigin,
            aimPoint,
            projectileDefinition,
            controller.ActionPointEconomy.IncomePerPersonalTurn,
            launchBudget.ActionPoints);
        var launchAction = new GameplayActionRecord(
            sequence: 1L,
            new GameplayActionRequest(
                droneControllerId,
                launcher.ActionId,
                "enemy"),
            launcher.TurnCost,
            controller.TurnBudget,
            launchBudget,
            new[] { new ProjectileLaunchedActionOutcome(launch) });
        var launchPayload = new GameplayWeaponTransitionPayload(
            launcherProfile,
            launchAction);
        live.Execute(new GameplaySemanticTransition(
            new GameplayTransitionIdentity(
                checked(
                    live.CurrentState.Session.LastTransitionSequence + 1L),
                launchPayload.Profile.Capability.ToString(),
                launchPayload.ActorId,
                launchPayload.SubjectId),
            live.CurrentState.CanonicalHash,
            launchPayload));
        Require(projectiles.ProjectileIds.Count == 1
            && projectiles.Launches.Count == 1,
            "Reducer-owned projectile launch was not installed live.");

        ProjectileFlightSnapshot previousFlight =
            projectiles.GetProjectile(projectileId);
        float advanceDistance = projectileDefinition.SpeedPerTurn;
        var resultingFlight = new ProjectileFlightSnapshot(
            launch,
            launch.GetPosition(advanceDistance),
            advanceDistance,
            elapsedTurnTime: 1f,
            ProjectileFlightStatus.InFlight);
        var advance = new ProjectileAdvanceRecord(
            sequence: checked(
                live.CurrentState.Session.LastTransitionSequence + 1L),
            previousFlight,
            resultingFlight,
            requestedTurnTime: 1f,
            segmentEnd: resultingFlight.Position,
            worldStateRevision: live.CurrentState.Session.JournalSequence,
            collisionFraction: null);
        projectiles.CommitAdvance(advance);
        Require(projectiles.Advances.Count == 1
            && projectiles.GetProjectile(projectileId).DistanceTraveled
                == advanceDistance,
            "Reducer-owned projectile advance was not installed live.");
        Require(GameplayExactReplay.Verify(
                initial,
                live.Trajectory,
                reducers).IsExact,
            "Full live combat projection trajectory did not replay exactly.");
        GameplaySemanticReplayTimeline replay =
            live.CreateReplayTimeline();
        var playback = new GameplaySemanticReplayPlaybackTimeline(replay);
        GameplaySemanticReplayPlaybackPosition playbackStart =
            playback.Locate(0f);
        GameplaySemanticReplayPlaybackPosition playbackEnd =
            playback.Locate(playback.TotalDurationSeconds);
        GameplayPresentationWorldStateSample initialSample =
            GameplaySemanticReplaySampler.Sample(
                playbackStart.Frame,
                playbackStart.Progress);
        GameplayPresentationWorldStateSample droneSample =
            GameplaySemanticReplaySampler.Sample(
                replay.Frames[2],
                linearProgress: 0.5f);
        GameplayPresentationWorldStateSample vehicleSample =
            GameplaySemanticReplaySampler.Sample(
                replay.Frames[3],
                linearProgress: 0.5f);
        GameplayPresentationWorldStateSample projectileSample =
            GameplaySemanticReplaySampler.Sample(
                replay.Frames[5],
                linearProgress: 0.5f);
        GameplayPresentationWorldStateSample beforeLaunch =
            GameplaySemanticReplaySampler.Sample(
                replay.Frames[4],
                linearProgress: 0f);
        GameplayPresentationWorldStateSample resolvedLaunch =
            GameplaySemanticReplaySampler.Sample(
                replay.Frames[4],
                GameplaySemanticReplayPresentationTiming
                    .ActionResolutionProgress);
        IReadOnlyList<TurnReplayActorActionState> launchActions =
            TurnReplayActorActionProjector.Project(
                replay.Frames[4],
                normalizedProgress: 0.5f);
        Require(replay.Frames.Count == 6
            && string.Equals(
                replay.FinalState.CanonicalHash,
                live.CurrentState.CanonicalHash,
                StringComparison.Ordinal)
            && droneSample.Drones[0].Position.DistanceTo(
                new GameplayPosition(
                    drone.Position.X + 0.5f,
                    drone.Position.Y,
                    drone.Position.Z)) < 0.001f
            && vehicleSample.Vehicles[0].Position.DistanceTo(
                new GameplayPosition(
                    previousVehicle.Position.X,
                    previousVehicle.Position.Y,
                    previousVehicle.Position.Z + 0.25f)) < 0.001f
            && projectileSample.Projectiles[0].DistanceTraveled > 0f
            && projectileSample.Projectiles[0].DistanceTraveled
                < advanceDistance * 0.5f
            && playback.Frames.Count == replay.Frames.Count
            && playback.TotalDurationSeconds > 0f
            && playbackStart.Frame.Index == 0
            && playbackStart.Progress == 0f
            && playbackEnd.Frame.Index == replay.Frames.Count - 1
            && playbackEnd.Progress == 1f
            && string.Equals(
                initialSample.Session.ActiveActorId,
                initial.Session.ActiveActorId,
                StringComparison.Ordinal)
            && beforeLaunch.Projectiles.Count == 0
            && resolvedLaunch.Projectiles.Count == 1
            && launchActions.Count == 1
            && launchActions[0].Kind
                == TurnReplayActorActionKind.Attack
            && string.Equals(
                launchActions[0].ActorId,
                controller.ActorId,
                StringComparison.Ordinal),
            "Semantic replay did not consume reducer endpoints with shared presentation interpolation.");

        bool duplicateFireAdvanceRejected = false;
        try
        {
            fireFields.AdvanceContinuousTime(1f);
        }
        catch (InvalidOperationException)
        {
            duplicateFireAdvanceRejected = true;
        }
        Require(duplicateFireAdvanceRejected,
            "Projection-bound fire still allowed its independent mutable path.");
    }

    private static void VerifySharedPresentationSampling()
    {
        var route = new MovementRouteRecord(
            "presentation.actor",
            new GameplayActorPose(
                new GameplayPosition(0f, 0f, 0f),
                facingDegrees: 0f),
            new TurnBudget(4, 8f),
            new[]
            {
                new MovementRouteSegmentRecord(
                    new GameplayPosition(0f, 0f, 0f),
                    new GameplayPosition(0f, 0f, 2f),
                    MovementRouteSegmentKind.Jump,
                    "jump.presentation",
                    "traversal.jump",
                    movementCost: 2f,
                    actionPointCost: 0,
                    arcHeight: 1.25f,
                    playbackDurationSeconds: 0.8f),
            });
        Require(GameplayMovementPresentationSampler.TrySample(
                route,
                elapsedSeconds: 0.4f,
                out GameplayMovementPresentationSample movement)
            && movement.SegmentIndex == 0
            && Math.Abs(movement.SegmentProgress - 0.5f) < 0.001f
            && Math.Abs(movement.Position.Y - 1.25f) < 0.001f
            && Math.Abs(movement.Position.Z - 1f) < 0.001f
            && movement.FacingDegrees == 0f,
            "Shared movement presentation sampling lost frozen arc, timing, or facing evidence.");

        var definition = new ProjectileFlightDefinition(
            "projectile.presentation",
            speedPerTurn: 4f,
            radius: 0.1f,
            maximumRange: 12f,
            standingLaunchHeight: 1f,
            crouchedLaunchHeight: 0.7f);
        var launch = new ProjectileLaunchRecord(
            1L,
            "projectile.presentation.1",
            "presentation.actor",
            "target",
            "attack.presentation",
            new GameplayPosition(0f, 1f, 0f),
            new GameplayPosition(0f, 1f, 12f),
            definition,
            turnActionPointTimeScale: 4,
            remainingActionPointsAfterLaunch: 3);
        var previous = new ProjectileFlightSnapshot(
            launch,
            launch.Origin,
            distanceTraveled: 0f,
            elapsedTurnTime: 0f,
            ProjectileFlightStatus.InFlight);
        var resulting = new ProjectileFlightSnapshot(
            launch,
            launch.GetPosition(4f),
            distanceTraveled: 4f,
            elapsedTurnTime: 1f,
            ProjectileFlightStatus.InFlight);
        ProjectileFlightSnapshot early =
            GameplayProjectilePresentationSampler.Sample(
                previous,
                resulting,
                linearProgress: 0.14f);
        ProjectileFlightSnapshot complete =
            GameplayProjectilePresentationSampler.Sample(
                previous,
                resulting,
                linearProgress: 1f);
        Require(early.DistanceTraveled > 0f
            && early.DistanceTraveled < 4f * 0.14f
            && complete.DistanceTraveled == 4f
            && complete.Position.DistanceTo(resulting.Position) == 0f,
            "Shared projectile sampling lost acceleration or exact endpoint installation.");
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

    private static void VerifyPermanentPolicyRunner()
    {
        GameplaySession gameplay = CreateEncounterGameplay();
        var level = new LevelDocument
        {
            levelId = "policy-runner-check",
            schemaVersion = LevelDocument.CurrentSchemaVersion,
        };
        level.Normalize();
        Require(gameplay.BeginEncounter(),
            "Policy runner fixture encounter did not begin.");
        GameplayCombatStateSnapshot initial = GameplayCombatStateCapture.Capture(
            gameplay,
            DestructiblePropSession.FromLevel(level, gameplay.Journal));
        var spatialIdentity = new SpatialContentIdentity(
            level.levelId,
            level.schemaVersion,
            evidenceAlgorithmVersion: 1,
            new string('7', 64));
        var spatial = new GameplayHeadlessSpatialEvidence(
            level,
            spatialIdentity);
        IReadOnlyList<GameplayReachableInput> allInputs =
            GameplayReachableInputEnumerator.Enumerate(
                gameplay.Scenario,
                level);
        GameplayTransitionReducerRegistry reducers =
            GameplaySimulationReducers.CreateCurrent();
        GameplayCapabilityRegistry capabilities =
            GameplayCurrentCapabilityCatalog.Create(reducers, allInputs);
        var routes = new GameplayCandidateExecutionRouteRegistry(capabilities);
        routes.Register(new GameplayActorAttackCandidateExecutionRoute(
            gameplay.Scenario,
            Array.Empty<TacticalContextRuleDefinition>(),
            spatial));
        routes.Register(new GameplayEndTurnCandidateExecutionRoute(
            gameplay.Scenario));

        var decisionInputs = new List<GameplayReachableInput>();
        foreach (GameplayReachableInput input in allInputs)
            if (string.Equals(input.ActorId, "player", StringComparison.Ordinal)
                && routes.Supports(input.Profile))
                decisionInputs.Add(input);
        var candidateSource = new GameplayHeadlessDecisionCandidateSource(
            new GameplayHeadlessCandidateBuilder(capabilities, spatial),
            decisionInputs,
            routes);
        var policy = new GameplayWeightedOutcomePolicy(new[]
        {
            new GameplayOutcomeFeatureWeight(
                "attack.hit-probability",
                10f),
            new GameplayOutcomeFeatureWeight("turn.end", -1f),
        });
        var runner = new GameplayPolicyDecisionRunner(
            candidateSource,
            routes,
            policy);

        GameplayExecutionIdentity executionIdentity =
            new GameplayExecutionIdentity(
                new GameplayContentIdentity(
                    gameplay.Scenario.Id,
                    scenarioSchemaVersion: 1,
                    rulesSchemaVersion: 1,
                    new string('6', 64)),
                spatialIdentity,
                gameplay.RunIdentity);
        var firstRuntime = new GameplaySimulationRuntime(
            executionIdentity,
            initial,
            reducers,
            capabilities);
        var firstScope = new GameplayExecutionDeadlineScope();
        firstScope.BeginTurn();
        GameplayDecisionExecutionResult first = runner.ExecuteAsync(
                firstRuntime,
                GameplayObservationSnapshot.FullState("player", initial),
                firstScope)
            .GetAwaiter().GetResult();
        Require(first.Transition.Profile.Capability
                == GameplaySemanticCapability.DirectAttack
            && firstRuntime.Trajectory.Count == 1
            && firstRuntime.CurrentState.Session.GetActor("player")
                .AttacksCommittedThisTurn == 1
            && first.Diagnostic.Timings.Count == 6,
            "Permanent policy runner did not traverse all stages or record its canonical attack count.");

        var repeatedRuntime = new GameplaySimulationRuntime(
            executionIdentity,
            initial,
            reducers,
            capabilities);
        var repeatedScope = new GameplayExecutionDeadlineScope();
        repeatedScope.BeginTurn();
        GameplayDecisionExecutionResult repeated = runner.ExecuteAsync(
                repeatedRuntime,
                GameplayObservationSnapshot.FullState("player", initial),
                repeatedScope)
            .GetAwaiter().GetResult();
        Require(string.Equals(
                GameplayTransitionPayloadDigest.Calculate(first.Transition),
                GameplayTransitionPayloadDigest.Calculate(repeated.Transition),
                StringComparison.Ordinal)
            && string.Equals(
                firstRuntime.CurrentState.CanonicalHash,
                repeatedRuntime.CurrentState.CanonicalHash,
                StringComparison.Ordinal),
            "Policy timing or worker scheduling changed deterministic selection or reduction.");

        var baselineRunner = new GameplayPolicyDecisionRunner(
            candidateSource,
            routes,
            GameplayBaselineCombatPolicy.Create(gameplay.Scenario));
        var baselineRuntime = new GameplaySimulationRuntime(
            executionIdentity,
            initial,
            reducers,
            capabilities);
        var baselineScope = new GameplayExecutionDeadlineScope();
        baselineScope.BeginTurn();
        GameplayDecisionExecutionResult baseline = baselineRunner.ExecuteAsync(
                baselineRuntime,
                GameplayObservationSnapshot.FullState("player", initial),
                baselineScope)
            .GetAwaiter().GetResult();
        Require(baseline.Transition.Profile.Capability
                == GameplaySemanticCapability.DirectAttack
            && string.Equals(
                GameplayTransitionPayloadDigest.Calculate(baseline.Transition),
                GameplayTransitionPayloadDigest.Calculate(first.Transition),
                StringComparison.Ordinal),
            "Permanent baseline policy did not select the deterministic combat route used by live enemies.");

        var staleRunner = new GameplayPolicyDecisionRunner(
            candidateSource,
            routes,
            policy,
            installationBoundary: new StaleInstallationBoundary());
        var staleScope = new GameplayExecutionDeadlineScope();
        staleScope.BeginTurn();
        GameplayDecisionFailureException staleDecision = null;
        try
        {
            staleRunner.ExecuteAsync(
                    new GameplaySimulationRuntime(
                        executionIdentity,
                        initial,
                        reducers,
                        capabilities),
                    GameplayObservationSnapshot.FullState("player", initial),
                    staleScope)
                .GetAwaiter().GetResult();
        }
        catch (GameplayDecisionFailureException exception)
        {
            staleDecision = exception;
        }
        Require(staleDecision != null
            && staleDecision.Kind
                == GameplayDecisionFailureKind.StaleDecisionState
            && staleDecision.Diagnostic.ActiveStage
                == GameplayDecisionStage.Installation
            && staleDecision.InnerException
                is GameplayStaleDecisionStateException,
            "The policy runner did not preserve typed stale-installation diagnostics.");

        GameplayActorSnapshot enemy = initial.Session.GetActor("enemy");
        EnemyBehaviorDefinition enemyBehavior = gameplay.Scenario.GetActor(
            "enemy").Combat.EnemyBehavior;
        var cappedEnemy = new GameplayActorSnapshot(
            enemy.ActorId,
            enemy.Pose,
            enemy.TurnBudget,
            enemy.Wounds,
            enemy.EquippedItemId,
            enemy.EquipmentEffects,
            enemy.MaximumWounds,
            enemy.Inventory,
            enemy.ActionPointEconomy,
            enemy.TurnMovementAllowance,
            enemy.PinState,
            enemy.EmergencyActionPointAllowance,
            enemy.SuspendedTurnBudget,
            attacksCommittedThisTurn:
                enemyBehavior.MaximumAttacksPerTurn);
        var cappedActors = new List<GameplayActorSnapshot>();
        foreach (GameplayActorSnapshot actor in initial.Session.Actors)
            cappedActors.Add(string.Equals(
                    actor.ActorId,
                    enemy.ActorId,
                    StringComparison.Ordinal)
                ? cappedEnemy
                : actor);
        GameplaySessionStateSnapshot previousSession = initial.Session;
        var cappedSession = new GameplaySessionStateSnapshot(
            previousSession.ScenarioId,
            previousSession.Mode,
            previousSession.Operation,
            previousSession.TurnContext,
            previousSession.EncounterActive,
            previousSession.EncounterCompletionRequested,
            previousSession.ActiveActorId,
            previousSession.TurnPhase,
            cappedActors,
            previousSession.InitiativeOrder,
            previousSession.Objectives,
            previousSession.EmergencyResponders,
            previousSession.EmergencyResponderIndex,
            previousSession.EmergencyResumeActorId,
            previousSession.LastActionSequence,
            previousSession.LastTurnSequence,
            previousSession.JournalSequence,
            previousSession.RunIdentity,
            previousSession.Revision,
            previousSession.VoluntaryTurnReentrySecondsRemaining,
            previousSession.PendingMovementRoute,
            previousSession.PendingVoluntaryTurnCycle,
            previousSession.LastTransitionSequence,
            previousSession.LastVoluntaryTurnCycleSequence,
            previousSession.EncounterState,
            previousSession.AllInitiativeOrder);
        var cappedState = new GameplayCombatStateSnapshot(
            cappedSession,
            initial.Destructibles,
            initial.Vehicles,
            initial.Projectiles,
            initial.SmokeFields,
            initial.Coverage,
            initial.FireFields,
            initial.Drones);
        IReadOnlyList<GameplayCandidate> cappedCandidates =
            new GameplayHeadlessCandidateBuilder(
                capabilities,
                spatial,
                scenarioDefinition: gameplay.Scenario)
            .Build(cappedState, allInputs, "enemy");
        bool retainedCappedWeaponAttack = false;
        foreach (GameplayCandidate candidate in cappedCandidates)
            if (candidate.Profile.Capability
                    == GameplaySemanticCapability.DirectAttack
                || candidate.Profile.Capability
                    == GameplaySemanticCapability.LaunchProjectile)
            {
                try
                {
                    retainedCappedWeaponAttack |= string.Equals(
                        candidate.Profile.GetTrait("resource"),
                        "equipped-weapon",
                        StringComparison.Ordinal);
                }
                catch (KeyNotFoundException)
                {
                }
            }
        Require(!retainedCappedWeaponAttack,
            "Canonical per-turn attack cap did not remove authored enemy weapon candidates.");

        var missingRouteInput = new GameplayReachableInput(
            GameplayReachableInputKind.StanceControl,
            "fixture.stance",
            "player",
            GameplayCapabilityProfiles.ChangeStance());
        GameplayExecutableRouteCoverageReport missingCoverage =
            GameplayExecutableRouteCoverageValidator.Validate(
                new[] { missingRouteInput },
                routes);
        Require(!missingCoverage.IsComplete
            && missingCoverage.Issues.Count == 1,
            "Executable route coverage accepted capability metadata without a concrete route.");

        var hangingRuntime = new GameplaySimulationRuntime(
            executionIdentity,
            initial,
            reducers,
            capabilities);
        var timeoutPolicy = new GameplayExecutionDeadlinePolicy(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(30),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(4));
        var hangingRunner = new GameplayPolicyDecisionRunner(
            candidateSource,
            routes,
            new HangingCandidatePolicy(),
            deadlinePolicy: timeoutPolicy);
        var hangingScope = new GameplayExecutionDeadlineScope();
        hangingScope.BeginTurn();
        GameplayDecisionFailureException timeout = null;
        try
        {
            hangingRunner.ExecuteAsync(
                    hangingRuntime,
                    GameplayObservationSnapshot.FullState("player", initial),
                    hangingScope)
                .GetAwaiter().GetResult();
        }
        catch (GameplayDecisionFailureException exception)
        {
            timeout = exception;
        }
        Require(timeout != null
            && timeout.Kind == GameplayDecisionFailureKind.DeadlineExceeded
            && timeout.Diagnostic.ActiveStage
                == GameplayDecisionStage.Scoring
            && timeout.Diagnostic.CandidateIds.Count > 0
            && timeout.Diagnostic.LegalCandidateIds.Count > 0
            && hangingRuntime.Trajectory.Count == 0
            && string.Equals(
                hangingRuntime.CurrentState.CanonicalHash,
                initial.CanonicalHash,
                StringComparison.Ordinal),
            "Hanging policy did not fail with a typed partial artifact before mutation.");
    }

    private static void VerifyPermanentBattleRunner()
    {
        LoadDepotContent(
            out GameplayScenarioAssembly assembly,
            out LevelDocument level,
            out GameplayStaticSpatialContent spatialContent);
        GameplayCombatStateSnapshot initial =
            GameplayHeadlessBattleStateFactory.Create(assembly, spatialContent);
        var semanticSpatialContent = new GameplayStaticSpatialContent(
            level,
            spatialContent.FractureCatalog);
        GameplayFractureSpatialCatalogDocument changedFractureCatalog =
            spatialContent.FractureCatalog.DeepCopy();
        changedFractureCatalog.profiles[0].chunks[0].center.x += 0.125f;
        var changedFractureContent = new GameplayStaticSpatialContent(
            level,
            changedFractureCatalog);
        Require(!semanticSpatialContent.Identity.HasSameIdentity(
                changedFractureContent.Identity),
            "Static spatial identity ignored fracture topology changes.");
        LevelDocument presentationChangedLevel = level.DeepCopy();
        presentationChangedLevel.displayName += " presentation mutation";
        var presentationChangedContent = new GameplayStaticSpatialContent(
            presentationChangedLevel,
            spatialContent.FractureCatalog);
        Require(semanticSpatialContent.Identity.HasSameIdentity(
                presentationChangedContent.Identity),
            "Static spatial identity included presentation-only level content.");
        LevelDocument geometryChangedLevel = level.DeepCopy();
        geometryChangedLevel.entities[0].transform.position.x += 0.125f;
        var geometryChangedContent = new GameplayStaticSpatialContent(
            geometryChangedLevel,
            spatialContent.FractureCatalog);
        Require(!semanticSpatialContent.Identity.HasSameIdentity(
                geometryChangedContent.Identity),
            "Static spatial identity ignored simulation geometry changes.");
        var identity = new GameplayExecutionIdentity(
            new GameplayContentIdentity(
                assembly.Scenario.Id,
                ScenarioContentDocument.CurrentSchemaVersion,
                GameplayCombatStateSnapshot.CurrentSchemaVersion,
                GameplayCanonicalValueDigest.Calculate(assembly.Scenario)),
            spatialContent.Identity,
            initial.Session.RunIdentity);
        var runner = new GameplayBattleRunner(
            assembly,
            spatialContent,
            identity,
            logicalGuardPolicy: new GameplayExecutionLogicalGuardPolicy(
                maximumTransitions: 2000,
                maximumRepeatedMaterialStates: 4,
                maximumNoProgressTurns: 4),
            workerBoundary:
                new GameplayCooperativeDecisionWorkerBoundary());
        GameplayBattleRunResult result = runner.RunAsync(initial)
            .GetAwaiter().GetResult();
        Console.WriteLine(
            "Permanent Depot battle: " + result.Terminal.Kind
            + ", decisions=" + result.Decisions.Count
            + ", transitions=" + result.Transitions.Count
            + ", failure=" + result.Terminal.FailureKind);
        if (!result.Terminal.IsSuccessful)
        {
            Console.WriteLine("Active actor at failure: "
                + result.FinalState.Session.ActiveActorId);
            foreach (GameplayActorSnapshot actor in
                result.FinalState.Session.Actors)
                Console.WriteLine("  " + actor.ActorId + " life="
                    + actor.LifeState + " injuries="
                    + actor.Injuries.Injuries.Count + " condition="
                    + GameplayInjuryCapabilityProjection
                        .CalculateConditionPercent(actor.Injuries)
                    + " move=" + actor.Capabilities.MovementCapacity
                    + " aim=" + actor.Capabilities.AimStability
                    + " grip=" + actor.Capabilities.GripCapacity);
        }
        Require(result.Terminal.IsSuccessful,
            "Permanent Depot battle failed: "
                + result.Terminal.FailureKind + " "
                + result.Terminal.FailureMessage);
        Require(result.Decisions.Count > 0
            && result.Transitions.Count == result.Decisions.Count + 1,
            "Permanent battle did not retain setup plus policy decisions.");
        Require(result.ReplayWindows.Count > 0,
            "Permanent battle did not close any replay windows.");
        GameplayReplayWindow terminalReplayWindow = result.ReplayWindows[
            result.ReplayWindows.Count - 1];
        Require(terminalReplayWindow.ClosureReason
                == GameplayReplayWindowClosureReason.TerminalCapability
            && terminalReplayWindow.EndTrajectoryIndex
                == result.Transitions.Count - 1
            && terminalReplayWindow.TurnSequence
                == result.Terminal.TransitionSequence,
            "The lethal terminal transition was not retained in the final "
                + "terminal-capability replay window.");
        int portableFractureProps = 0;
        foreach (DestructiblePropSnapshot prop in initial.Destructibles)
        {
            if (prop.FractureChunkCount == 12) portableFractureProps++;
        }
        Require(portableFractureProps == initial.Destructibles.Count
            && portableFractureProps > 0,
            "Permanent battle did not initialize portable fracture topology.");
        int fireDeployments = 0;
        int concussiveThrows = 0;
        int droneMoves = 0;
        int droneAttacks = 0;
        int droneSummons = 0;
        int droneDismissals = 0;
        int droneExpirations = 0;
        int droneCrashes = 0;
        foreach (GameplayBattleTransitionRecord transition in
            result.Transitions)
            foreach (GameplayDomainEvent domainEvent in transition.DomainEvents)
            {
                if (domainEvent is GameplayDroneExpiredEvent)
                {
                    droneExpirations++;
                    continue;
                }
                if (!(domainEvent is GameplayTransitionReducedEvent reduced))
                    continue;
                if (reduced.SemanticRecord is SummonDroneRecord)
                    droneSummons++;
                if (reduced.SemanticRecord is DismissDroneRecord)
                    droneDismissals++;
                if (reduced.SemanticRecord is DroneCrashImpactRecord)
                    droneCrashes++;
                if (reduced.SemanticRecord is DroneMoveRecord) droneMoves++;
                if (reduced.SemanticRecord is DroneAttackRecord
                    || reduced.SemanticRecord is ActorDroneAttackRecord)
                    droneAttacks++;
                if (!(reduced.SemanticRecord is GameplayActionRecord action))
                    continue;
                foreach (GameplayActionOutcome outcome in action.Outcomes)
                    if (outcome is ThrownExplosiveActionOutcome thrown)
                    {
                        if (thrown.Record.FireField != null) fireDeployments++;
                        if (thrown.Record.ConcussiveEffects.Count > 0)
                            concussiveThrows++;
                    }
            }
        Console.WriteLine(
            "First-sim mechanics: fire=" + fireDeployments
            + ", concussive=" + concussiveThrows
            + ", drone-summons=" + droneSummons
            + ", drone-moves=" + droneMoves
            + ", drone-attacks=" + droneAttacks
            + ", drone-dismissals=" + droneDismissals
            + ", drone-expirations=" + droneExpirations
            + ", drone-crashes=" + droneCrashes);
        // The calibrated rifle can now end this seeded battle before policy
        // selects the optional prop shot. DirectFireDestructibleTests owns the
        // deterministic impact-to-fracture vertical slice; this permanent run
        // continues to prove the four policy-driven first-sim mechanics.
        Require(fireDeployments > 0
            && concussiveThrows > 0
            && droneSummons > 0
            && droneMoves > 0
            && droneAttacks > 0,
            "Permanent battle did not exercise every first-sim mechanic.");
        GameplayExactReplayResult replay = GameplayExactReplay.Verify(
            initial,
            result.CreateTrajectory(),
            GameplaySimulationReducers.CreateCurrent());
        Require(replay.IsExact
            && string.Equals(
                replay.FinalState.CanonicalHash,
                result.FinalState.CanonicalHash,
                StringComparison.Ordinal),
            "Permanent battle trajectory did not replay exactly.");
        GameplayBattleArtifact artifact = GameplayBattleArtifactFactory.Create(
            result,
            new GameplayBattleArtifactProvenance(
                new string('a', 40),
                "codex/artifact-contract",
                "permanent-depot-contract"));
        string artifactJson = artifact.ToPortableJson();
        GameplayBattleArtifact decoded = GameplayBattleArtifactCodec.Read(
            artifactJson);
        GameplaySemanticReplayTimeline verifiedArtifactReplay =
            GameplayBattleArtifactVerifier.VerifyRun(result, decoded);
        var verifiedCombatTranscript = new ReplayCombatTranscript(
            new GameplaySemanticReplayPlaybackTimeline(
                verifiedArtifactReplay));
        int verifiedSystemicChanges = 0;
        foreach (ReplayCombatTranscriptEntry entry in
            verifiedCombatTranscript.Entries)
        {
            if (entry.EventKind
                != ReplayCombatTranscriptEventKind.SystemicChange)
                continue;
            verifiedSystemicChanges++;
            Require(entry.Injury == null
                && entry.PhysiologyBefore != null
                && entry.PhysiologyAfter != null
                && (entry.PhysiologyBefore.BloodReserve
                        != entry.PhysiologyAfter.BloodReserve
                    || entry.PhysiologyBefore.Shock
                        != entry.PhysiologyAfter.Shock
                    || entry.PhysiologyBefore.Consciousness
                        != entry.PhysiologyAfter.Consciousness
                    || entry.PhysiologyBefore.Respiration
                        != entry.PhysiologyAfter.Respiration
                    || entry.LifeStateBefore != entry.LifeStateAfter),
                "Replay systemic entry did not preserve a stored state change.");
        }
        Require(verifiedSystemicChanges > 0
            && verifiedSystemicChanges
                == verifiedCombatTranscript.Totals.SystemicChanges,
            "Permanent replay did not expose canonical physiology advances.");
        Stopwatch playbackClock = Stopwatch.StartNew();
        GameplayBattleArtifact playbackArtifact = decoded;
        GameplaySemanticReplayTimeline loadedArtifactReplay =
            GameplayBattleArtifactReplayLoader.Load(playbackArtifact);
        var combatPlayback = new GameplaySemanticReplayPlaybackTimeline(
            loadedArtifactReplay);
        var combatTranscript = new ReplayCombatTranscript(combatPlayback);
        playbackClock.Stop();
        Console.WriteLine(
            "Artifact playback decode + load: "
            + playbackClock.Elapsed.TotalSeconds.ToString("0.000") + "s");
        Console.WriteLine(
            "Replay transcript: attacks="
            + combatTranscript.Totals.AttackExecutions
            + ", hits=" + combatTranscript.Totals.Hits
            + ", misses=" + combatTranscript.Totals.Misses
            + ", injuries=" + combatTranscript.Totals.InjuriesApplied
            + ", discharges=" + combatTranscript.Totals.WeaponDischarges
            + ", reactions=" + combatTranscript.Totals.Reactions
            + ", incapacitations="
            + combatTranscript.Totals.Incapacitations
            + ", systemic=" + combatTranscript.Totals.SystemicChanges
            + ", deaths=" + combatTranscript.Totals.Deaths);
        Require(string.Equals(
                decoded.ToPortableJson(),
                artifactJson,
                StringComparison.Ordinal)
            && string.Equals(
                decoded.ArtifactId,
                artifact.ArtifactId,
                StringComparison.Ordinal)
            && decoded.Content.Scoreboard.FireDeployments
                == fireDeployments
            && decoded.Content.Scoreboard.ConcussiveTargets > 0
            && decoded.Content.Scoreboard.DroneMoves == droneMoves
            && decoded.Content.Scoreboard.DroneAttacks > 0
            && decoded.Content.Scoreboard.DroneSummons == droneSummons
            && decoded.Content.Scoreboard.DroneDismissals == droneDismissals
            && decoded.Content.Scoreboard.DroneExpirations == droneExpirations
            && decoded.Content.Scoreboard.DroneCrashes == droneCrashes
            && string.Equals(
                verifiedArtifactReplay.FinalState.CanonicalHash,
                result.FinalState.CanonicalHash,
                StringComparison.Ordinal)
            && loadedArtifactReplay.Frames.Count
                == playbackArtifact.Content.Transitions.Count
            && string.Equals(
                loadedArtifactReplay.FinalState.CanonicalHash,
                result.FinalState.CanonicalHash,
                StringComparison.Ordinal)
            && combatTranscript.Totals.AttackExecutions
                == playbackArtifact.Content.Scoreboard.Attacks
            && combatTranscript.Totals.Hits
                == playbackArtifact.Content.Scoreboard.Hits
            && combatTranscript.Totals.Misses
                == playbackArtifact.Content.Scoreboard.Attacks
                    - playbackArtifact.Content.Scoreboard.Hits
            && combatTranscript.Totals.WoundsApplied
                >= playbackArtifact.Content.Scoreboard.Wounds,
            "Battle artifact was not byte-stable, replayable, and scoreboard-complete.");

        var transcriptEventIds = new HashSet<string>(StringComparer.Ordinal);
        float previousTranscriptTime = 0f;
        int transcriptInjuryEntries = 0;
        foreach (ReplayCombatTranscriptEntry entry in combatTranscript.Entries)
        {
            Require(transcriptEventIds.Add(entry.CombatEventId),
                "Replay combat transcript contains duplicate event identity.");
            Require(entry.TimeSeconds >= previousTranscriptTime,
                "Replay combat transcript is not chronologically ordered.");
            previousTranscriptTime = entry.TimeSeconds;
            if (entry.EventKind
                != ReplayCombatTranscriptEventKind.InjuryApplied)
                continue;
            transcriptInjuryEntries++;
            Require(entry.Injury != null
                && entry.Injury.Severity > 0
                && entry.CapabilitiesBefore != null
                && entry.CapabilitiesAfter != null
                && entry.PhysiologyBefore != null
                && entry.PhysiologyAfter != null
                && entry.LifeStateBefore.HasValue
                && entry.LifeStateAfter.HasValue,
                "Replay injury entry omitted stored localized consequences.");
        }
        Require(transcriptInjuryEntries
                == combatTranscript.Totals.InjuriesApplied,
            "Replay transcript injury totals diverged.");
        foreach (ReplayActorTerminalPoseEpisode episode in
            combatPlayback.TerminalPoseEpisodes)
        {
            ReplayActorTerminalPoseSample sample =
                combatPlayback.SampleTerminalPose(
                    episode.ActorId,
                    Math.Min(
                        combatPlayback.TotalDurationSeconds,
                        episode.AnimationEndSeconds));
            Require(sample != null
                && string.Equals(
                    sample.EpisodeId,
                    episode.EpisodeId,
                    StringComparison.Ordinal),
                "A replay terminal episode was not seekable.");
        }
        float transcriptMidpoint = combatPlayback.TotalDurationSeconds * 0.5f;
        IReadOnlyList<ReplayCombatTranscriptEntry> midpointEntries =
            combatTranscript.GetEntriesAtOrBefore(transcriptMidpoint);
        foreach (ReplayCombatTranscriptEntry entry in midpointEntries)
            Require(entry.TimeSeconds <= transcriptMidpoint,
                "Replay transcript exposed a future entry at the playhead.");
        GameplayBattleArtifactFormatException unknownFailure = null;
        try
        {
            GameplayBattleArtifactCodec.Read(artifactJson.Replace(
                "\"artifact\":",
                "\"unknown\":0,\"artifact\":"));
        }
        catch (GameplayBattleArtifactFormatException exception)
        {
            unknownFailure = exception;
        }
        GameplayBattleArtifactFormatException digestFailure = null;
        try
        {
            GameplayBattleArtifactCodec.Read(artifactJson.Replace(
                artifact.ArtifactId,
                new string('0', 64)));
        }
        catch (GameplayBattleArtifactFormatException exception)
        {
            digestFailure = exception;
        }
        Require(unknownFailure != null && digestFailure != null,
            "Strict artifact reading accepted unknown or tampered content.");
    }

    private static void VerifyLogicalExecutionGuards()
    {
        GameplaySession gameplay = CreateEncounterGameplay();
        Require(gameplay.BeginEncounter(),
            "Logical guard fixture encounter did not begin.");
        GameplayCombatStateSnapshot state = GameplayCombatStateCapture.Capture(
            gameplay);
        var noProgress = new GameplayExecutionLogicalGuard(
            state,
            new GameplayExecutionLogicalGuardPolicy(
                maximumTransitions: 10,
                maximumRepeatedMaterialStates: 10,
                maximumNoProgressTurns: 2));
        noProgress.BeginTurn(state.Session.ActiveActorId, state);
        noProgress.CompleteTurn(state, mandatoryWorkRemaining: false);
        noProgress.BeginTurn(state.Session.ActiveActorId, state);
        GameplayDecisionFailureException noProgressFailure = null;
        try
        {
            noProgress.CompleteTurn(state, mandatoryWorkRemaining: false);
        }
        catch (GameplayDecisionFailureException exception)
        {
            noProgressFailure = exception;
        }
        Require(noProgressFailure?.Kind
                == GameplayDecisionFailureKind.NoProgressTurn,
            "No-progress turn guard did not return a typed failure.");

        var mandatory = new GameplayExecutionLogicalGuard(state);
        mandatory.BeginTurn(state.Session.ActiveActorId, state);
        GameplayDecisionFailureException mandatoryFailure = null;
        try
        {
            mandatory.CompleteTurn(state, mandatoryWorkRemaining: true);
        }
        catch (GameplayDecisionFailureException exception)
        {
            mandatoryFailure = exception;
        }
        Require(mandatoryFailure?.Kind
                == GameplayDecisionFailureKind.UnresolvedMandatoryWork,
            "Unresolved mandatory work did not prevent turn completion.");

        var projectileDefinition = new ProjectileFlightDefinition(
            "guard.projectile",
            speedPerTurn: 4f,
            radius: 0.1f,
            maximumRange: 12f,
            standingLaunchHeight: 1f,
            crouchedLaunchHeight: 0.7f);
        var launch = new ProjectileLaunchRecord(
            1L,
            "guard.projectile.1",
            state.Session.ActiveActorId,
            "enemy",
            "guard.launch",
            new GameplayPosition(0f, 1f, 0f),
            new GameplayPosition(0f, 1f, 12f),
            projectileDefinition,
            turnActionPointTimeScale: 4,
            remainingActionPointsAfterLaunch: 3);
        var inFlight = new ProjectileFlightSnapshot(
            launch,
            launch.Origin,
            distanceTraveled: 0f,
            elapsedTurnTime: 0f,
            ProjectileFlightStatus.InFlight);
        var projectileState = new GameplayCombatStateSnapshot(
            state.Session,
            projectiles: new[] { inFlight },
            coverage: GameplayCombatStateCoverage.Session
                | GameplayCombatStateCoverage.Projectiles);
        Require(GameplayMandatoryWorkRules.HasPending(projectileState),
            "In-flight projectile was not recognized as mandatory work.");
        var projectileGuard = new GameplayExecutionLogicalGuard(
            projectileState);
        projectileGuard.BeginTurn(
            projectileState.Session.ActiveActorId,
            projectileState);
        GameplayDecisionFailureException projectileFailure = null;
        try
        {
            projectileGuard.CompleteTurn(
                projectileState,
                GameplayMandatoryWorkRules.HasPending(projectileState));
        }
        catch (GameplayDecisionFailureException exception)
        {
            projectileFailure = exception;
        }
        Require(projectileFailure?.Kind
                == GameplayDecisionFailureKind.UnresolvedMandatoryWork,
            "An in-flight projectile did not fail closed at the end-turn guard.");

        var repeated = new GameplayExecutionLogicalGuard(
            state,
            new GameplayExecutionLogicalGuardPolicy(
                maximumTransitions: 10,
                maximumRepeatedMaterialStates: 1,
                maximumNoProgressTurns: 10));
        GameplayDecisionFailureException repeatedFailure = null;
        try
        {
            repeated.ValidatePreparedTransition(new GameplayReductionResult(
                state,
                state,
                Array.Empty<GameplayDomainEvent>()));
        }
        catch (GameplayDecisionFailureException exception)
        {
            repeatedFailure = exception;
        }
        Require(repeatedFailure?.Kind
                == GameplayDecisionFailureKind.RepeatedCanonicalState,
            "Repeated material-state hash did not stop execution.");

        var maximum = new GameplayExecutionLogicalGuard(
            state,
            new GameplayExecutionLogicalGuardPolicy(
                maximumTransitions: 1,
                maximumRepeatedMaterialStates: 10,
                maximumNoProgressTurns: 10));
        var unchanged = new GameplayReductionResult(
            state,
            state,
            Array.Empty<GameplayDomainEvent>());
        maximum.ValidatePreparedTransition(unchanged);
        GameplayDecisionFailureException maximumFailure = null;
        try
        {
            maximum.ValidatePreparedTransition(unchanged);
        }
        catch (GameplayDecisionFailureException exception)
        {
            maximumFailure = exception;
        }
        Require(maximumFailure?.Kind
                == GameplayDecisionFailureKind.MaximumTransitionsExceeded,
            "Maximum transition guard did not stop execution.");
    }

    private static void VerifyBasicExecutableCandidateRoutes()
    {
        AttackDefinition rifle = CreateRifle();
        var firstWeapon = new InventoryItemDefinition(
            "weapon.first",
            "First Weapon",
            hotbarSlot: 1,
            InventoryItemKind.Weapon,
            new ActionCost(1, 0f, ActionMobility.Set),
            new EquipmentEffectSet(0.9f),
            attack: rifle);
        var secondWeapon = new InventoryItemDefinition(
            "weapon.second",
            "Second Weapon",
            hotbarSlot: 2,
            InventoryItemKind.Weapon,
            new ActionCost(1, 0f, ActionMobility.Set),
            new EquipmentEffectSet(0.8f),
            attack: rifle);
        var player = new ScenarioActorDefinition(
            "player",
            initiative: 10,
            new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
            new TurnBudget(4, 8f),
            new[] { firstWeapon, secondWeapon },
            initiallyEquippedItemId: firstWeapon.Id,
            combat: new ActorCombatDefinition(
                "player",
                new[] { "raider" },
                maximumWounds: 2));
        var enemy = new ScenarioActorDefinition(
            "enemy",
            initiative: 0,
            new GameplayActorPose(new GameplayPosition(0f, 0f, 5f), 180f),
            new TurnBudget(4, 8f),
            rifle,
            combat: new ActorCombatDefinition(
                "raider",
                new[] { "player" },
                maximumWounds: 2));
        var objective = new ScenarioObjectiveDefinition(
            "objective",
            new GameplayPosition(0f, 0f, 0f),
            interactionRadius: 1f);
        var scenario = new ScenarioDefinition(
            "basic-route-check",
            new ScenarioTimingDefinition(1f),
            new[] { player, enemy },
            new[] { objective });
        var gameplay = new GameplaySession(scenario, scenarioSeed: 123u);
        Require(gameplay.BeginEncounter(),
            "Basic route fixture encounter did not begin.");
        GameplayCombatStateSnapshot initial = GameplayCombatStateCapture.Capture(
            gameplay);
        var level = new LevelDocument
        {
            levelId = "basic-route-level",
            schemaVersion = LevelDocument.CurrentSchemaVersion,
        };
        level.Normalize();
        IReadOnlyList<GameplayReachableInput> inputs =
            GameplayReachableInputEnumerator.Enumerate(scenario, level);
        GameplayTransitionReducerRegistry reducers =
            GameplaySimulationReducers.CreateCurrent();
        GameplayCapabilityRegistry capabilities =
            GameplayCurrentCapabilityCatalog.Create(reducers, inputs);
        var routes = new GameplayCandidateExecutionRouteRegistry(capabilities);
        routes.Register(new GameplayStanceCandidateExecutionRoute());
        routes.Register(new GameplayEquipmentCandidateExecutionRoute(scenario));
        routes.Register(new GameplayInteractionCandidateExecutionRoute());
        IReadOnlyList<GameplayCandidate> candidates =
            new GameplayTacticalCandidateBuilder(capabilities).Build(
                initial,
                inputs);
        GameplayCandidate stance = FindCandidate(
            candidates,
            "player",
            GameplaySemanticCapability.ChangeStance,
            "player");
        GameplayCandidate unequip = FindCandidate(
            candidates,
            "player",
            GameplaySemanticCapability.Equip,
            firstWeapon.Id);
        GameplayCandidate equip = FindCandidate(
            candidates,
            "player",
            GameplaySemanticCapability.Equip,
            secondWeapon.Id);
        GameplayCandidate interact = FindCandidate(
            candidates,
            "player",
            GameplaySemanticCapability.Interact,
            objective.Id);
        var runtime = new GameplaySimulationRuntime(
            new GameplayExecutionIdentity(
                new GameplayContentIdentity(
                    scenario.Id,
                    scenarioSchemaVersion: 1,
                    rulesSchemaVersion: 1,
                    new string('5', 64)),
                new SpatialContentIdentity(
                    level.levelId,
                    level.schemaVersion,
                    evidenceAlgorithmVersion: 1,
                    new string('4', 64)),
                gameplay.RunIdentity),
            initial,
            reducers,
            capabilities);
        ExecuteCandidate(runtime, routes, stance);
        Require(runtime.CurrentState.Session.GetActor("player").Pose.Stance
                == ActorStance.Crouched,
            "Stance candidate did not install its reducer-owned pose.");
        ExecuteCandidate(runtime, routes, unequip);
        Require(runtime.CurrentState.Session.GetActor("player")
                .EquippedItemId == null,
            "Equipment candidate did not unequip the canonical item.");
        ExecuteCandidate(runtime, routes, equip);
        GameplayActorSnapshot equipped = runtime.CurrentState.Session.GetActor(
            "player");
        Require(string.Equals(
                equipped.EquippedItemId,
                secondWeapon.Id,
                StringComparison.Ordinal)
            && equipped.EquipmentEffects.MovementSpeedMultiplier == 0.8f,
            "Equipment candidate did not install item effects.");
        ExecuteCandidate(runtime, routes, interact);
        bool completed = false;
        foreach (GameplayObjectiveSnapshot value in runtime.CurrentState.Session
            .Objectives)
            if (string.Equals(
                value.ObjectiveId,
                objective.Id,
                StringComparison.Ordinal)) completed = value.IsCompleted;
        Require(completed
            && runtime.Trajectory.Count == 4
            && GameplayExactReplay.Verify(
                initial,
                runtime.Trajectory,
                reducers).IsExact,
            "Basic executable routes did not form one exact semantic trajectory.");
    }

    private static void VerifyLifecycleExecutableCandidateRoutes()
    {
        GameplaySession gameplay = CreateGameplay(CreateRifle());
        GameplayCombatStateSnapshot initial = GameplayCombatStateCapture.Capture(
            gameplay);
        GameplayTransitionReducerRegistry reducers =
            GameplaySimulationReducers.CreateCurrent();
        var inputs = new List<GameplayReachableInput>
        {
            new GameplayReachableInput(
                GameplayReachableInputKind.SystemContinuation,
                "system.world.continuous-time",
                "player",
                GameplayCapabilityProfiles.AdvanceWorld("continuous-time")),
            new GameplayReachableInput(
                GameplayReachableInputKind.SystemContinuation,
                "system.world.voluntary-cycle",
                "player",
                GameplayCapabilityProfiles.AdvanceWorld("voluntary-cycle")),
            new GameplayReachableInput(
                GameplayReachableInputKind.SessionControl,
                "control.turn-mode.enter",
                "player",
                GameplayCapabilityProfiles.ChangeTurnMode("enter")),
            new GameplayReachableInput(
                GameplayReachableInputKind.SessionControl,
                "control.turn-mode.exit",
                "player",
                GameplayCapabilityProfiles.ChangeTurnMode("exit")),
            new GameplayReachableInput(
                GameplayReachableInputKind.SystemContinuation,
                "system.encounter.begin",
                "player",
                GameplayCapabilityProfiles.ChangeEncounter("begin")),
            new GameplayReachableInput(
                GameplayReachableInputKind.SystemContinuation,
                "system.encounter.request-completion",
                "player",
                GameplayCapabilityProfiles.ChangeEncounter(
                    "request-completion")),
            new GameplayReachableInput(
                GameplayReachableInputKind.SystemContinuation,
                "system.emergency.begin",
                "player",
                GameplayCapabilityProfiles.EmergencyReaction("begin")),
            new GameplayReachableInput(
                GameplayReachableInputKind.SystemContinuation,
                "system.emergency.complete",
                "player",
                GameplayCapabilityProfiles.EmergencyReaction("complete")),
            new GameplayReachableInput(
                GameplayReachableInputKind.EndTurnControl,
                "control.end-turn",
                "player",
                GameplayCapabilityProfiles.EndTurn(emergency: false)),
            new GameplayReachableInput(
                GameplayReachableInputKind.EmergencyControl,
                "control.end-emergency-turn",
                "enemy",
                GameplayCapabilityProfiles.EndTurn(emergency: true)),
        };
        GameplayCapabilityRegistry capabilities =
            GameplayCurrentCapabilityCatalog.Create(reducers, inputs);
        var routes = new GameplayCandidateExecutionRouteRegistry(capabilities);
        routes.Register(new GameplayLifecycleCandidateExecutionRoute(
            gameplay.Scenario,
            continuousTimeStepSeconds: 0.25f));
        routes.Register(new GameplayEndTurnCandidateExecutionRoute(
            gameplay.Scenario));
        var candidates = new GameplayReachableCandidateBuilder(capabilities);
        var executionIdentity = new GameplayExecutionIdentity(
            new GameplayContentIdentity(
                gameplay.Scenario.Id,
                scenarioSchemaVersion: 1,
                rulesSchemaVersion: 1,
                new string('0', 64)),
            new SpatialContentIdentity(
                "lifecycle-route-level",
                levelSchemaVersion: 1,
                evidenceAlgorithmVersion: 1,
                new string('f', 64)),
            gameplay.RunIdentity);

        var encounterRuntime = new GameplaySimulationRuntime(
            executionIdentity,
            initial,
            reducers,
            capabilities);
        GameplayCandidate continuous = candidates.Build(inputs[0]);
        ExecuteCandidate(encounterRuntime, routes, continuous);
        GameplayCandidate scopedBegin = candidates.Build(
            inputs[4],
            new GameplaySubjectReference(
                GameplaySemanticSubjectKind.System,
                GameplaySessionControlTransitionPayload.Subject),
            new GameplayLifecycleCandidateIntent(
                inputs[4],
                participantIds: new[] { "player", "enemy" }));
        ExecuteCandidate(encounterRuntime, routes, scopedBegin);
        ExecuteCandidate(
            encounterRuntime,
            routes,
            candidates.Build(inputs[5]));
        ExecuteCandidate(
            encounterRuntime,
            routes,
            candidates.Build(inputs[8]));
        Require(encounterRuntime.CurrentState.Session.Mode
                == GameplaySessionMode.Exploration
            && !encounterRuntime.CurrentState.Session.EncounterActive
            && encounterRuntime.CurrentState.Session
                .VoluntaryTurnReentrySecondsRemaining
                == gameplay.Scenario.Timing.MinimumVoluntaryTurnSeconds
            && GameplayExactReplay.Verify(
                initial,
                encounterRuntime.Trajectory,
                reducers).IsExact,
            "Scoped encounter lifecycle routes did not complete and replay exactly.");

        var modeRuntime = new GameplaySimulationRuntime(
            executionIdentity,
            initial,
            reducers,
            capabilities);
        ExecuteCandidate(modeRuntime, routes, candidates.Build(inputs[2]));
        ExecuteCandidate(modeRuntime, routes, candidates.Build(inputs[3]));
        Require(modeRuntime.CurrentState.Session.Mode
                == GameplaySessionMode.Exploration
            && GameplayExactReplay.Verify(
                initial,
                modeRuntime.Trajectory,
                reducers).IsExact,
            "Voluntary turn-mode enter/exit routes did not replay exactly.");

        var voluntaryRuntime = new GameplaySimulationRuntime(
            executionIdentity,
            initial,
            reducers,
            capabilities);
        ExecuteCandidate(
            voluntaryRuntime,
            routes,
            candidates.Build(inputs[2]));
        ExecuteCandidate(
            voluntaryRuntime,
            routes,
            candidates.Build(inputs[8]));
        ExecuteCandidate(
            voluntaryRuntime,
            routes,
            candidates.Build(inputs[1]));
        Require(voluntaryRuntime.CurrentState.Session.Operation
                == GameplaySessionOperation.None
            && voluntaryRuntime.CurrentState.Session
                .PendingVoluntaryTurnCycle == null
            && GameplayExactReplay.Verify(
                initial,
                voluntaryRuntime.Trajectory,
                reducers).IsExact,
            "Mandatory voluntary world-cycle route did not replay exactly.");

        var emergencyRuntime = new GameplaySimulationRuntime(
            executionIdentity,
            initial,
            reducers,
            capabilities);
        ExecuteCandidate(emergencyRuntime, routes, scopedBegin);
        GameplayCandidate emergencyBegin = candidates.Build(
            inputs[6],
            new GameplaySubjectReference(
                GameplaySemanticSubjectKind.Actor,
                "player"),
            new GameplayLifecycleCandidateIntent(
                inputs[6],
                responderIds: new[] { "enemy" },
                emergencyActionPointAllowance: 1));
        ExecuteCandidate(emergencyRuntime, routes, emergencyBegin);
        ExecuteCandidate(
            emergencyRuntime,
            routes,
            candidates.Build(inputs[9]));
        ExecuteCandidate(
            emergencyRuntime,
            routes,
            candidates.Build(inputs[7]));
        Require(emergencyRuntime.CurrentState.Session.TurnPhase
                == GameplayTurnPhase.Normal
            && string.Equals(
                emergencyRuntime.CurrentState.Session.ActiveActorId,
                "player",
                StringComparison.Ordinal)
            && GameplayExactReplay.Verify(
                initial,
                emergencyRuntime.Trajectory,
                reducers).IsExact,
            "Emergency lifecycle routes did not restore and replay the suspended turn.");
    }

    private static void ExecuteCandidate(
        GameplaySimulationRuntime runtime,
        GameplayCandidateExecutionRouteRegistry routes,
        GameplayCandidate candidate)
    {
        var context = new GameplayDecisionContext(
            runtime.CurrentState,
            GameplayObservationSnapshot.FullState(
                candidate.ActorId,
                runtime.CurrentState));
        GameplayExecutableCandidateEvaluation evaluation = routes.Evaluate(
            context,
            candidate);
        Require(evaluation.IsLegal,
            "Candidate route was unexpectedly illegal: "
                + evaluation.FailureCode);
        runtime.Execute(routes.Prepare(context, evaluation));
    }

    private static GameplayCandidate FindCandidate(
        IEnumerable<GameplayCandidate> candidates,
        string actorId,
        GameplaySemanticCapability capability,
        string subjectId)
    {
        foreach (GameplayCandidate candidate in candidates)
            if (candidate.Profile.Capability == capability
                && string.Equals(
                    candidate.ActorId,
                    actorId,
                    StringComparison.Ordinal)
                && string.Equals(
                    candidate.SubjectId,
                    subjectId,
                    StringComparison.Ordinal)) return candidate;
        throw new InvalidOperationException(
            $"Candidate '{actorId}/{capability}/{subjectId}' was not built.");
    }

    private static void VerifyCanonicalActionOwnedRecordSequences()
    {
        GameplaySession gameplay = CreateEncounterGameplay();
        Require(gameplay.BeginEncounter(),
            "Causal sequence fixture encounter did not begin.");
        var attacks = new GameplayAttackSession(gameplay);
        var exposure = new TargetExposureSnapshot(
            "player",
            "enemy",
            new[]
            {
                new TargetRegionExposure(
                    TargetRegionId.Torso,
                    visibleSampleCount: 1,
                    totalSampleCount: 1),
            });
        Require(attacks.TryResolve(
                "player",
                exposure,
                out GameplayActionRecord firstAction,
                out AttackResolutionFailure firstFailure),
            "Causal sequence setup attack failed: " + firstFailure);
        Require(firstAction.Sequence == 1L,
            "Causal sequence setup did not commit the first action.");
        Require(attacks.TryPrepareDischarge(
                "player",
                GameplayTargetIds.WorldAimPoint,
                new GameplayPosition(0f, 1f, -2f),
                out GameplayPreparedTransition<GameplayActionRecord> prepared,
                out AttackResolutionFailure dischargeFailure),
            "Causal discharge preparation failed: " + dischargeFailure);
        var discharge = (WeaponDischargedActionOutcome)
            prepared.Record.Outcomes[0];
        Require(prepared.Record.Sequence == 2L
            && discharge.Discharge.Sequence == prepared.Record.Sequence,
            "A discharge used its private record count instead of the canonical action sequence.");
    }

    private static void VerifyConcreteDirectFireCandidateRoute()
    {
        LoadDepotContent(
            out GameplayScenarioAssembly authored,
            out LevelDocument level);
        var spatialIdentity = new SpatialContentIdentity(
            level.levelId,
            level.schemaVersion,
            evidenceAlgorithmVersion: 1,
            new string('6', 64));
        var spatial = new GameplayHeadlessSpatialEvidence(
            level,
            spatialIdentity);
        GameplayScenarioAssembly assembly = GameplayHeadlessScenarioGrounding
            .Resolve(authored, spatial);
        var gameplay = new GameplaySession(
            assembly.Scenario,
            scenarioSeed: assembly.RandomSeed);
        var destructibles = DestructiblePropSession.FromLevel(
            level,
            gameplay.Journal);
        Require(gameplay.BeginEncounter(),
            "Direct-fire fixture encounter did not begin.");
        GameplayCombatStateSnapshot initial = GameplayCombatStateCapture.Capture(
            gameplay,
            destructibles);
        IReadOnlyList<GameplayReachableInput> inputs =
            GameplayReachableInputEnumerator.Enumerate(
                assembly.Scenario,
                level);
        GameplayTransitionReducerRegistry reducers =
            GameplaySimulationReducers.CreateCurrent();
        GameplayCapabilityRegistry capabilities =
            GameplayCurrentCapabilityCatalog.Create(reducers, inputs);
        var routes = new GameplayCandidateExecutionRouteRegistry(capabilities);
        routes.Register(new GameplayDirectAttackCandidateExecutionRoute(
            assembly,
            spatial));
        IReadOnlyList<GameplayCandidate> candidates =
            new GameplayTacticalCandidateBuilder(capabilities).Build(
                initial,
                inputs);
        string activeActorId = initial.Session.ActiveActorId;
        var context = new GameplayDecisionContext(
            initial,
            GameplayObservationSnapshot.FullState(activeActorId, initial));
        GameplayExecutableCandidateEvaluation selected = null;
        var failures = new List<string>();
        foreach (GameplayCandidate candidate in candidates)
        {
            if (!string.Equals(
                    candidate.ActorId,
                    activeActorId,
                    StringComparison.Ordinal)
                || candidate.Profile.Capability
                    != GameplaySemanticCapability.DirectAttack
                || candidate.SubjectKind
                    != GameplaySemanticSubjectKind.DestructibleProp)
                continue;
            GameplayExecutableCandidateEvaluation evaluated = routes.Evaluate(
                context,
                candidate);
            if (!evaluated.IsLegal)
            {
                failures.Add(
                    candidate.SubjectId + ":" + evaluated.FailureCode);
                continue;
            }
            selected = evaluated;
            break;
        }
        Require(selected != null,
            "Depot supplied no legal reducer-owned direct-fire prop candidate: "
                + string.Join(", ", failures));
        GameplaySemanticTransition transition = routes.Prepare(
            context,
            selected);
        var weaponPayload = (GameplayWeaponTransitionPayload)
            transition.Payload;
        var discharged = (WeaponDischargedActionOutcome)
            weaponPayload.Action.Outcomes[0];
        LevelEntity targetEntity = null;
        foreach (LevelEntity entity in level.entities)
            if (string.Equals(
                entity.id,
                selected.Candidate.SubjectId,
                StringComparison.Ordinal))
            {
                targetEntity = entity;
                break;
            }
        Require(targetEntity?.destructible?.enabled == true
            && discharged.Discharge.Impact != null
            && string.Equals(
                discharged.Discharge.Impact.SurfaceId,
                targetEntity.destructible.surfaceId,
                StringComparison.Ordinal)
            && discharged.Discharge.Damage != null,
            "Direct-fire candidate did not use authoritative prop surface damage.");
        var liveAdapter = new GameplayAttackSession(gameplay, destructibles);
        Require(liveAdapter.TryPrepareDischarge(
                activeActorId,
                selected.Candidate.SubjectId,
                discharged.Discharge.AimPoint,
                discharged.Discharge.Impact,
                out GameplayPreparedTransition<GameplayActionRecord>
                    livePrepared,
                out AttackResolutionFailure liveFailure)
            && string.Equals(
                GameplayCanonicalValueDigest.Calculate(livePrepared.Record),
                GameplayCanonicalValueDigest.Calculate(weaponPayload.Action),
                StringComparison.Ordinal),
            "Live direct-fire adapter diverged from pure preparation: "
                + liveFailure);

        var runtime = new GameplaySimulationRuntime(
            new GameplayExecutionIdentity(
                new GameplayContentIdentity(
                    assembly.Scenario.Id,
                    scenarioSchemaVersion: 1,
                    rulesSchemaVersion: 1,
                    new string('7', 64)),
                spatialIdentity,
                gameplay.RunIdentity),
            initial,
            reducers,
            capabilities);
        runtime.Execute(transition);
        Require(runtime.CurrentState.Destructibles.Count
                == initial.Destructibles.Count
            && runtime.CurrentState.CanonicalHash
                != initial.CanonicalHash
            && GameplayExactReplay.Verify(
                initial,
                runtime.Trajectory,
                reducers).IsExact,
            "Direct-fire prop candidate did not reduce and replay exactly.");

    }

    private static void VerifyConcreteDisplacementCandidateRoute()
    {
        LoadDepotContent(
            out GameplayScenarioAssembly authored,
            out LevelDocument level);
        var resolvedPoses = new Dictionary<string, GameplayActorPose>(
            StringComparer.Ordinal)
        {
            ["player"] = new GameplayActorPose(
                new GameplayPosition(-7.5f, 2f, 3f),
                90f,
                ActorStance.Standing),
            ["oren-vale"] = new GameplayActorPose(
                new GameplayPosition(-6f, 2f, 3f),
                270f,
                ActorStance.Standing),
        };
        var spatialIdentity = new SpatialContentIdentity(
            level.levelId,
            level.schemaVersion,
            evidenceAlgorithmVersion: 1,
            new string('b', 64));
        var spatial = new GameplayHeadlessSpatialEvidence(
            level,
            spatialIdentity);
        GameplayScenarioAssembly assembly = GameplayHeadlessScenarioGrounding
            .Resolve(authored.WithResolvedActorPoses(resolvedPoses), spatial);
        var gameplay = new GameplaySession(
            assembly.Scenario,
            scenarioSeed: assembly.RandomSeed);
        Require(gameplay.BeginEncounter(),
            "Displacement route fixture encounter did not begin.");
        int turnGuard = 0;
        while (!string.Equals(
            gameplay.ActiveActorId,
            "player",
            StringComparison.Ordinal))
        {
            Require(turnGuard++ < assembly.Scenario.Actors.Count
                && gameplay.TryEndTurn(gameplay.ActiveActorId, out _),
                "Displacement route fixture could not reach the player turn.");
        }
        DestructiblePropSession destructibles =
            DestructiblePropSession.FromLevel(level, gameplay.Journal);
        GameplayCombatStateSnapshot initial = GameplayCombatStateCapture.Capture(
            gameplay,
            destructibles);
        IReadOnlyList<GameplayReachableInput> inputs =
            GameplayReachableInputEnumerator.Enumerate(assembly, level);
        GameplayTransitionReducerRegistry reducers =
            GameplaySimulationReducers.CreateCurrent();
        GameplayCapabilityRegistry capabilities =
            GameplayCurrentCapabilityCatalog.Create(reducers, inputs);
        var routes = new GameplayCandidateExecutionRouteRegistry(capabilities);
        routes.Register(new GameplayDisplacementCandidateExecutionRoute(
            assembly,
            spatial));
        var displacementInputs = new List<GameplayReachableInput>();
        foreach (GameplayReachableInput input in inputs)
            if (input.Profile.Capability
                    == GameplaySemanticCapability.Displace)
                displacementInputs.Add(input);
        GameplayExecutableRouteCoverageValidator.Validate(
            displacementInputs,
            routes).RequireComplete();

        var builder = new GameplayHeadlessCandidateBuilder(
            capabilities,
            spatial,
            scenarioDefinition: assembly.Scenario);
        var context = new GameplayDecisionContext(
            initial,
            GameplayObservationSnapshot.FullState("player", initial));
        GameplayExecutableCandidateEvaluation selected = null;
        var failures = new List<string>();
        foreach (GameplayCandidate candidate in builder.Build(
            initial,
            displacementInputs,
            "player"))
        {
            if (candidate.Profile.Capability
                    != GameplaySemanticCapability.Displace
                || candidate.SubjectKind
                    != GameplaySemanticSubjectKind.Actor
                || !string.Equals(
                    candidate.SubjectId,
                    "oren-vale",
                    StringComparison.Ordinal)
                || candidate.Profile.GetTrait("intent")
                    != DisplacementActionKind.Push.ToString())
                continue;
            GameplayExecutableCandidateEvaluation evaluated = routes.Evaluate(
                context,
                candidate);
            if (!evaluated.IsLegal)
            {
                failures.Add(evaluated.FailureCode);
                continue;
            }
            selected = evaluated;
            break;
        }
        Require(selected != null,
            "Depot supplied no legal reducer-owned actor displacement: "
                + string.Join(", ", failures));
        GameplaySemanticTransition transition = routes.Prepare(
            context,
            selected);
        GameplaySemanticTransition repeated = routes.Prepare(
            context,
            routes.Evaluate(context, selected.Candidate));
        Require(string.Equals(
            GameplayTransitionPayloadDigest.Calculate(transition),
            GameplayTransitionPayloadDigest.Calculate(repeated),
            StringComparison.Ordinal),
            "Displacement contest preparation was not deterministic.");

        var runtime = new GameplaySimulationRuntime(
            new GameplayExecutionIdentity(
                new GameplayContentIdentity(
                    assembly.Scenario.Id,
                    scenarioSchemaVersion: 1,
                    rulesSchemaVersion: 1,
                    new string('a', 64)),
                spatialIdentity,
                gameplay.RunIdentity),
            initial,
            reducers,
            capabilities);
        runtime.Execute(transition);
        Require(runtime.CurrentState.Session.LastActionSequence
                == initial.Session.LastActionSequence + 1L
            && GameplayExactReplay.Verify(
                initial,
                runtime.Trajectory,
                reducers).IsExact,
            "Displacement did not reduce and replay exactly.");

        DisplacementActionDefinition throwDefinition = assembly.GetActor(
            "player").GameplayDefinition.GetDisplacementAction(
                "close-quarters.throw");
        var mismatchedPayload = new GameplayResolvedActionTransitionPayload(
            GameplayCapabilityProfiles.Displace(
                throwDefinition,
                GameplaySemanticSubjectKind.Actor),
            ((GameplayResolvedActionTransitionPayload)transition.Payload)
                .Action);
        var mismatched = new GameplaySemanticTransition(
            transition.Identity,
            initial.CanonicalHash,
            mismatchedPayload,
            transition.Evidence);
        bool rejected = false;
        try
        {
            reducers.Reduce(initial, mismatched);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected,
            "Displacement reducer accepted an action under a different exact semantic profile.");
    }

    private static void VerifyConcreteProjectileCandidateRoutes()
    {
        LoadDepotContent(
            out GameplayScenarioAssembly authored,
            out LevelDocument level);
        var spatialIdentity = new SpatialContentIdentity(
            level.levelId,
            level.schemaVersion,
            evidenceAlgorithmVersion: 1,
            new string('e', 64));
        var spatial = new GameplayHeadlessSpatialEvidence(
            level,
            spatialIdentity);
        GameplayScenarioAssembly assembly = GameplayHeadlessScenarioGrounding
            .Resolve(authored, spatial);
        var gameplay = new GameplaySession(
            assembly.Scenario,
            scenarioSeed: assembly.RandomSeed);
        Require(gameplay.BeginEncounter(),
            "Projectile route fixture encounter did not begin.");
        int turnGuard = 0;
        while (!string.Equals(
            gameplay.ActiveActorId,
            "player",
            StringComparison.Ordinal))
        {
            Require(turnGuard++ < assembly.Scenario.Actors.Count
                && gameplay.TryEndTurn(gameplay.ActiveActorId, out _),
                "Projectile route fixture could not reach the player turn.");
        }
        var equipment = new GameplayEquipmentSession(gameplay);
        Require(equipment.TryResolveSwitch(
                "player",
                "weapon.rocket-launcher",
                out _,
                out _,
                out EquipmentChangeFailure equipmentFailure),
            "Projectile route fixture could not equip the launcher: "
                + equipmentFailure);
        DestructiblePropSession destructibles =
            DestructiblePropSession.FromLevel(level, gameplay.Journal);
        var projectiles = new GameplayProjectileSession(
            gameplay,
            new AlwaysClearProjectileQuery(),
            new GameplayBlastConsequenceResolver(gameplay, destructibles));
        var drones = new GameplayDroneSession(
            gameplay,
            assembly.DroneArchetypes,
            destructibles);
        GameplayCombatStateSnapshot initial = GameplayCombatStateCapture.Capture(
            gameplay,
            destructibles,
            projectiles: projectiles,
            drones: drones);
        IReadOnlyList<GameplayReachableInput> inputs =
            GameplayReachableInputEnumerator.Enumerate(assembly, level);
        GameplayTransitionReducerRegistry reducers =
            GameplaySimulationReducers.CreateCurrent();
        GameplayCapabilityRegistry capabilities =
            GameplayCurrentCapabilityCatalog.Create(reducers, inputs);
        var routes = new GameplayCandidateExecutionRouteRegistry(capabilities);
        var launchRoute = new GameplayProjectileLaunchCandidateExecutionRoute(
            assembly,
            spatial);
        routes.Register(launchRoute);
        routes.Register(new GameplayProjectileAdvanceCandidateExecutionRoute(
            spatial));
        var projectileInputs = new List<GameplayReachableInput>();
        foreach (GameplayReachableInput input in inputs)
            if (input.Profile.Capability
                    == GameplaySemanticCapability.LaunchProjectile
                || input.Profile.Capability
                    == GameplaySemanticCapability.AdvanceProjectile)
                projectileInputs.Add(input);
        GameplayExecutableRouteCoverageValidator.Validate(
            projectileInputs,
            routes).RequireComplete();

        var builder = new GameplayHeadlessCandidateBuilder(
            capabilities,
            spatial,
            scenarioDefinition: assembly.Scenario);
        var context = new GameplayDecisionContext(
            initial,
            GameplayObservationSnapshot.FullState("player", initial));
        GameplayExecutableCandidateEvaluation launchEvaluation = null;
        var launchFailures = new List<string>();
        foreach (GameplayCandidate candidate in builder.Build(
            initial,
            inputs,
            "player"))
        {
            if (candidate.Profile.Capability
                != GameplaySemanticCapability.LaunchProjectile)
                continue;
            GameplayExecutableCandidateEvaluation evaluated = routes.Evaluate(
                context,
                candidate);
            if (!evaluated.IsLegal)
            {
                launchFailures.Add(
                    candidate.SubjectId + ":" + evaluated.FailureCode);
                continue;
            }
            launchEvaluation = evaluated;
            if (candidate.SubjectKind == GameplaySemanticSubjectKind.Actor)
                break;
        }
        Require(launchEvaluation != null,
            "Depot supplied no legal reducer-owned projectile launch: "
                + string.Join(", ", launchFailures));
        VerifyProjectileNearMatchRejection(
            launchRoute,
            context,
            launchEvaluation.Candidate);
        GameplaySemanticTransition launchTransition = routes.Prepare(
            context,
            launchEvaluation);
        GameplayExecutableCandidateEvaluation repeatedLaunch = routes.Evaluate(
            context,
            launchEvaluation.Candidate);
        Require(string.Equals(
            GameplayTransitionPayloadDigest.Calculate(launchTransition),
            GameplayTransitionPayloadDigest.Calculate(routes.Prepare(
                context,
                repeatedLaunch)),
            StringComparison.Ordinal),
            "Projectile launch preparation was not deterministic.");

        var runtime = new GameplaySimulationRuntime(
            new GameplayExecutionIdentity(
                new GameplayContentIdentity(
                    assembly.Scenario.Id,
                    scenarioSchemaVersion: 1,
                    rulesSchemaVersion: 1,
                    new string('f', 64)),
                spatialIdentity,
                gameplay.RunIdentity),
            initial,
            reducers,
            capabilities);
        runtime.Execute(launchTransition);
        Require(runtime.CurrentState.Projectiles.Count == 1
            && runtime.CurrentState.Projectiles[0].Status
                == ProjectileFlightStatus.InFlight,
            "Projectile launch did not install one canonical in-flight state.");

        GameplayCombatStateSnapshot afterLaunch = runtime.CurrentState;
        var advanceContext = new GameplayDecisionContext(
            afterLaunch,
            GameplayObservationSnapshot.FullState("player", afterLaunch));
        GameplayExecutableCandidateEvaluation advanceEvaluation = null;
        foreach (GameplayCandidate candidate in builder.Build(
            afterLaunch,
            inputs,
            "player"))
        {
            if (!candidate.Profile.Equals(
                GameplayCapabilityProfiles.AdvanceProjectile()))
                continue;
            GameplayExecutableCandidateEvaluation evaluated = routes.Evaluate(
                advanceContext,
                candidate);
            if (evaluated.IsLegal)
            {
                advanceEvaluation = evaluated;
                break;
            }
        }
        Require(advanceEvaluation != null,
            "An in-flight projectile produced no mandatory advance candidate.");
        GameplaySemanticTransition advanceTransition = routes.Prepare(
            advanceContext,
            advanceEvaluation);
        var advancePayload = (GameplayProjectileAdvanceTransitionPayload)
            advanceTransition.Payload;
        Require(advancePayload.Advance.WorldStateRevision
                == afterLaunch.Session.JournalSequence
            && advancePayload.Advance.Resulting.DistanceTraveled
                > advancePayload.Advance.Previous.DistanceTraveled,
            "Projectile advance did not freeze current spatial evidence and flight progress.");
        runtime.Execute(advanceTransition);
        Require(GameplayExactReplay.Verify(
                initial,
                runtime.Trajectory,
                reducers).IsExact,
            "Projectile launch and advance did not replay exactly.");
    }

    private static void VerifyProjectileNearMatchRejection(
        GameplayProjectileLaunchCandidateExecutionRoute route,
        GameplayDecisionContext context,
        GameplayCandidate canonical)
    {
        GameplayCapabilityProfile profile = canonical.Profile;
        string consequence = profile.GetTrait("consequence");
        string emergency = profile.GetTrait("emergency");
        GameplayCapabilityProfile[] nearMatches =
        {
            ReplaceCapabilityTrait(
                profile,
                "consequence",
                string.Equals(
                    consequence,
                    "impact",
                    StringComparison.Ordinal)
                        ? "blast-actor-and-destructible"
                        : "impact"),
            ReplaceCapabilityTrait(
                profile,
                "emergency",
                string.Equals(
                    emergency,
                    "opens",
                    StringComparison.Ordinal)
                        ? "none"
                        : "opens"),
            new GameplayCapabilityProfile(
                profile.Capability,
                checked(profile.SemanticVersion + 1),
                profile.Traits),
            AddCapabilityTrait(profile, "near-match", "unsupported"),
        };
        string stateHash = context.State.CanonicalHash;
        for (int index = 0; index < nearMatches.Length; index++)
        {
            GameplayCapabilityProfile nearMatch = nearMatches[index];
            Require(route.Supports(nearMatch),
                "Projectile near-match fixture no longer reaches the broad "
                    + "route predicate: " + nearMatch.Signature);
            var candidate = new GameplayCandidate(
                canonical.CandidateId + ".near-match." + index,
                nearMatch,
                canonical.ActorId,
                canonical.SubjectId,
                canonical.Intent);
            GameplayExecutableCandidateEvaluation evaluation = route.Evaluate(
                context,
                candidate);
            Require(!evaluation.IsLegal
                && string.Equals(
                    evaluation.FailureCode,
                    "equipped-profile-mismatch",
                    StringComparison.Ordinal)
                && string.Equals(
                    context.State.CanonicalHash,
                    stateHash,
                    StringComparison.Ordinal),
                "Projectile route executed a superficially compatible "
                    + "near-match profile: " + nearMatch.Signature);
        }
    }

    private static GameplayCapabilityProfile ReplaceCapabilityTrait(
        GameplayCapabilityProfile source,
        string traitName,
        string value)
    {
        var traits = new List<GameplayCapabilityTrait>(source.Traits.Count);
        bool replaced = false;
        foreach (GameplayCapabilityTrait trait in source.Traits)
        {
            if (string.Equals(
                    trait.Name,
                    traitName,
                    StringComparison.Ordinal))
            {
                traits.Add(new GameplayCapabilityTrait(traitName, value));
                replaced = true;
            }
            else
            {
                traits.Add(trait);
            }
        }
        Require(replaced,
            "Near-match fixture could not replace capability trait '"
                + traitName + "'.");
        return new GameplayCapabilityProfile(
            source.Capability,
            source.SemanticVersion,
            traits);
    }

    private static GameplayCapabilityProfile AddCapabilityTrait(
        GameplayCapabilityProfile source,
        string traitName,
        string value)
    {
        var traits = new List<GameplayCapabilityTrait>(source.Traits)
        {
            new GameplayCapabilityTrait(traitName, value),
        };
        return new GameplayCapabilityProfile(
            source.Capability,
            source.SemanticVersion,
            traits);
    }

    private static void VerifyConcreteThrownExplosiveCandidateRoutes()
    {
        LoadDepotContent(
            out GameplayScenarioAssembly authored,
            out LevelDocument level);
        var spatialIdentity = new SpatialContentIdentity(
            level.levelId,
            level.schemaVersion,
            evidenceAlgorithmVersion: 1,
            new string('c', 64));
        var spatial = new GameplayHeadlessSpatialEvidence(
            level,
            spatialIdentity);
        GameplayScenarioAssembly assembly = GameplayHeadlessScenarioGrounding
            .Resolve(authored, spatial);
        GameplayTransitionReducerRegistry reducers =
            GameplaySimulationReducers.CreateCurrent();
        int executedProfiles = 0;
        var consequences = new HashSet<string>(StringComparer.Ordinal);
        foreach (ScenarioActorDefinition owner in assembly.Scenario.Actors)
        foreach (InventoryItemDefinition item in owner.Inventory)
        {
            if (item.ConsumablePower
                is not ThrownExplosiveDefinition explosive)
                continue;
            var gameplay = new GameplaySession(
                assembly.Scenario,
                scenarioSeed: assembly.RandomSeed);
            Require(gameplay.BeginEncounter(),
                "Explosive route fixture encounter did not begin.");
            int turnGuard = 0;
            while (!string.Equals(
                gameplay.ActiveActorId,
                owner.Id,
                StringComparison.Ordinal))
            {
                Require(turnGuard++ < assembly.Scenario.Actors.Count
                    && gameplay.TryEndTurn(gameplay.ActiveActorId, out _),
                    $"Could not advance to explosive owner '{owner.Id}'.");
            }
            var destructibles = DestructiblePropSession.FromLevel(
                level,
                gameplay.Journal);
            using var smokeFields = new GameplaySmokeFieldSession(gameplay);
            using var fireFields = new GameplayFireFieldSession(
                gameplay,
                destructibles);
            GameplayCombatStateSnapshot initial =
                GameplayCombatStateCapture.Capture(
                    gameplay,
                    destructibles,
                    smokeFields: smokeFields,
                    fireFields: fireFields);
            IReadOnlyList<GameplayReachableInput> inputs =
                GameplayReachableInputEnumerator.Enumerate(
                    assembly.Scenario,
                    level);
            GameplayCapabilityRegistry capabilities =
                GameplayCurrentCapabilityCatalog.Create(reducers, inputs);
            var routes = new GameplayCandidateExecutionRouteRegistry(
                capabilities);
            routes.Register(
                new GameplayThrownExplosiveCandidateExecutionRoute(
                    assembly,
                    spatial));
            IReadOnlyList<GameplayCandidate> candidates =
                new GameplayHeadlessCandidateBuilder(
                    capabilities,
                    spatial,
                    scenarioDefinition: assembly.Scenario).Build(
                        initial,
                        inputs,
                        owner.Id);
            GameplayCapabilityProfile expectedProfile =
                GameplayCapabilityProfiles.ThrowExplosive(explosive);
            var context = new GameplayDecisionContext(
                initial,
                GameplayObservationSnapshot.FullState(owner.Id, initial));
            GameplayExecutableCandidateEvaluation selected = null;
            float selectedValue = float.NegativeInfinity;
            var failures = new List<string>();
            foreach (GameplayCandidate candidate in candidates)
            {
                if (!candidate.Profile.Equals(expectedProfile)) continue;
                GameplayExecutableCandidateEvaluation evaluated =
                    routes.Evaluate(context, candidate);
                if (!evaluated.IsLegal)
                {
                    failures.Add(evaluated.FailureCode);
                    continue;
                }
                float value = evaluated.ExpectedOutcome.GetValue(
                        "blast.affected-actors")
                    + evaluated.ExpectedOutcome.GetValue(
                        "blast.affected-destructibles");
                if (selected == null || value > selectedValue)
                {
                    selected = evaluated;
                    selectedValue = value;
                }
            }
            Require(selected != null,
                $"Explosive '{explosive.Id}' supplied no legal world candidate: "
                    + string.Join(", ", failures));
            if (explosive.IsConcussive)
                Require(selected.ExpectedOutcome.GetValue(
                        "concussive.affected-actors") > 0f,
                    "Concussive candidate did not freeze any authoritative AP effect.");
            GameplaySemanticTransition transition = routes.Prepare(
                context,
                selected);
            GameplayExecutableCandidateEvaluation repeatedEvaluation =
                routes.Evaluate(context, selected.Candidate);
            GameplaySemanticTransition repeatedTransition = routes.Prepare(
                context,
                repeatedEvaluation);
            Require(string.Equals(
                GameplayTransitionPayloadDigest.Calculate(transition),
                GameplayTransitionPayloadDigest.Calculate(repeatedTransition),
                StringComparison.Ordinal),
                $"Explosive '{explosive.Id}' preparation was not deterministic.");
            var runtime = new GameplaySimulationRuntime(
                new GameplayExecutionIdentity(
                    new GameplayContentIdentity(
                        assembly.Scenario.Id,
                        scenarioSchemaVersion: 1,
                        rulesSchemaVersion: 1,
                        new string('d', 64)),
                    spatialIdentity,
                    gameplay.RunIdentity),
                initial,
                reducers,
                capabilities);
            runtime.Execute(transition);
            GameplayActorSnapshot resultingOwner = runtime.CurrentState.Session
                .GetActor(owner.Id);
            Require(resultingOwner.Inventory.GetQuantity(explosive.Id)
                    == initial.Session.GetActor(owner.Id).Inventory
                        .GetQuantity(explosive.Id) - 1
                && GameplayExactReplay.Verify(
                    initial,
                    runtime.Trajectory,
                    reducers).IsExact,
                $"Explosive '{explosive.Id}' did not reduce and replay exactly.");
            if (explosive.DeploysSmoke)
            {
                Require(selected.ExpectedOutcome.GetValue(
                            "blast.hostile-actors") == 0f
                        && selected.ExpectedOutcome.GetValue(
                            "blast.friendly-actors") == 0f,
                    "Smoke field proximity was misreported as a damaging blast.");
                Require(runtime.CurrentState.SmokeFields.Count == 1,
                    "Smoke grenade did not install one canonical smoke field.");
            }
            if (explosive.DeploysFire)
                Require(runtime.CurrentState.FireFields.Count == 1,
                    "Incendiary grenade did not install one canonical fire field.");
            consequences.Add(expectedProfile.GetTrait("consequence"));
            executedProfiles++;
        }
        Require(executedProfiles >= 4
            && consequences.SetEquals(new[]
            {
                "blast-actor-and-destructible",
                "smoke-field",
                "fire-field",
                "concussive-actor-ap",
            }),
            "Depot explosive execution did not cover frag, smoke, fire, and concussive semantics.");
    }

    private static void VerifyConcreteGroundedMoveCandidateRoute()
    {
        GameplaySession gameplay = CreateEncounterGameplay();
        var level = new LevelDocument
        {
            levelId = "grounded-move-route-check",
            schemaVersion = LevelDocument.CurrentSchemaVersion,
            entities = new List<LevelEntity>
            {
                new LevelEntity
                {
                    id = "movement-floor",
                    archetypeId = "structure.floor.standard",
                    transform = new LevelTransformData(
                        new Float3Data(0f, 0f, 0f),
                        yawDegrees: 0f),
                    placementSurface = new LevelPlacementSurfaceData
                    {
                        kind = LevelPlacementSurfaceData.FlatKind,
                        size = new Float3Data(40f, 0f, 40f),
                    },
                },
            },
        };
        level.Normalize();
        var spatialIdentity = new SpatialContentIdentity(
            level.levelId,
            level.schemaVersion,
            evidenceAlgorithmVersion: 1,
            new string('9', 64));
        var spatial = new GameplayHeadlessSpatialEvidence(
            level,
            spatialIdentity);
        Require(gameplay.BeginEncounter(),
            "Concrete movement route encounter did not begin.");
        GameplayCombatStateSnapshot initial = GameplayCombatStateCapture.Capture(
            gameplay,
            DestructiblePropSession.FromLevel(level, gameplay.Journal));
        IReadOnlyList<GameplayReachableInput> inputs =
            GameplayReachableInputEnumerator.Enumerate(
                gameplay.Scenario,
                level);
        GameplayTransitionReducerRegistry reducers =
            GameplaySimulationReducers.CreateCurrent();
        GameplayCapabilityRegistry capabilities =
            GameplayCurrentCapabilityCatalog.Create(reducers, inputs);
        var routes = new GameplayCandidateExecutionRouteRegistry(capabilities);
        routes.Register(new GameplayGroundedMoveCandidateExecutionRoute(
            gameplay.Scenario,
            spatial));
        var builder = new GameplayHeadlessCandidateBuilder(
            capabilities,
            spatial);
        IReadOnlyList<GameplayCandidate> built = builder.Build(
            initial,
            inputs,
            initial.Session.ActiveActorId);
        var moveCandidates = new List<GameplayCandidate>();
        var candidateIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (GameplayCandidate candidate in built)
        {
            if (!candidate.Profile.Equals(
                    GameplayCapabilityProfiles.GroundedMove()))
                continue;
            moveCandidates.Add(candidate);
            candidateIds.Add(candidate.CandidateId);
        }
        Require(moveCandidates.Count > 1
            && candidateIds.Count == moveCandidates.Count,
            "One semantic Move input did not expand into stable distinct grounded routes.");

        var context = new GameplayDecisionContext(
            initial,
            GameplayObservationSnapshot.FullState(
                initial.Session.ActiveActorId,
                initial));
        GameplayExecutableCandidateEvaluation evaluation = routes.Evaluate(
            context,
            moveCandidates[0]);
        Require(evaluation.IsLegal
            && evaluation.ExpectedOutcome.GetValue("move.distance") > 0f
            && evaluation.Evidence.Count == 1,
            "Concrete grounded movement route was not legal and evidence-backed.");
        GameplaySemanticTransition transition = routes.Prepare(
            context,
            evaluation);
        var runtime = new GameplaySimulationRuntime(
            new GameplayExecutionIdentity(
                new GameplayContentIdentity(
                    gameplay.Scenario.Id,
                    scenarioSchemaVersion: 1,
                    rulesSchemaVersion: 1,
                    new string('8', 64)),
                spatialIdentity,
                gameplay.RunIdentity),
            initial,
            reducers,
            capabilities);
        runtime.Execute(transition);
        Require(runtime.CurrentState.Session.GetActor(
                    initial.Session.ActiveActorId).Pose.Position.DistanceTo(
                    initial.Session.GetActor(
                        initial.Session.ActiveActorId).Pose.Position) > 0f
            && GameplayExactReplay.Verify(
                initial,
                runtime.Trajectory,
                reducers).IsExact,
            "Concrete grounded movement route did not execute and replay exactly.");

        var changedContext = new GameplayDecisionContext(
            runtime.CurrentState,
            GameplayObservationSnapshot.FullState(
                runtime.CurrentState.Session.ActiveActorId,
                runtime.CurrentState));
        GameplayExecutableCandidateEvaluation stale = routes.Evaluate(
            changedContext,
            moveCandidates[0]);
        Require(!stale.IsLegal
            && string.Equals(
                stale.FailureCode,
                "movement-evidence-stale",
                StringComparison.Ordinal),
            "Grounded movement accepted evidence frozen against an older state.");
    }

    private static void VerifyConcreteTraversalCandidateRoute()
    {
        LoadDepotContent(
            out GameplayScenarioAssembly authored,
            out LevelDocument level);
        LevelTraversalLinkData link = level.traversalLinks[0];
        var resolvedPoses = new Dictionary<string, GameplayActorPose>(
            StringComparer.Ordinal)
        {
            ["player"] = new GameplayActorPose(
                new GameplayPosition(
                    link.takeoff.x,
                    link.takeoff.y,
                    link.takeoff.z),
                90f,
                ActorStance.Standing),
        };
        var spatialIdentity = new SpatialContentIdentity(
            level.levelId,
            level.schemaVersion,
            evidenceAlgorithmVersion: 1,
            new string('7', 64));
        var spatial = new GameplayHeadlessSpatialEvidence(
            level,
            spatialIdentity);
        GameplayScenarioAssembly assembly = GameplayHeadlessScenarioGrounding
            .Resolve(authored.WithResolvedActorPoses(resolvedPoses), spatial);
        var gameplay = new GameplaySession(
            assembly.Scenario,
            scenarioSeed: assembly.RandomSeed);
        Require(gameplay.BeginEncounter(),
            "Traversal route fixture encounter did not begin.");
        int turnGuard = 0;
        while (!string.Equals(
            gameplay.ActiveActorId,
            "player",
            StringComparison.Ordinal))
        {
            Require(turnGuard++ < assembly.Scenario.Actors.Count
                && gameplay.TryEndTurn(gameplay.ActiveActorId, out _),
                "Traversal route fixture could not reach the player turn.");
        }
        DestructiblePropSession destructibles =
            DestructiblePropSession.FromLevel(level, gameplay.Journal);
        GameplayCombatStateSnapshot initial = GameplayCombatStateCapture.Capture(
            gameplay,
            destructibles);
        var traversalInputs = new List<GameplayReachableInput>();
        foreach (GameplayReachableInput input in
            GameplayReachableInputEnumerator.Enumerate(assembly, level))
            if (string.Equals(
                    input.ActorId,
                    "player",
                    StringComparison.Ordinal)
                && input.Profile.Equals(
                    GameplayCapabilityProfiles.TraversalMove()))
                traversalInputs.Add(input);
        GameplayTransitionReducerRegistry reducers =
            GameplaySimulationReducers.CreateCurrent();
        GameplayCapabilityRegistry capabilities =
            GameplayCurrentCapabilityCatalog.Create(
                reducers,
                traversalInputs);
        var routes = new GameplayCandidateExecutionRouteRegistry(capabilities);
        routes.Register(new GameplayTraversalCandidateExecutionRoute(
            assembly.Scenario,
            spatial));
        var builder = new GameplayHeadlessCandidateBuilder(
            capabilities,
            spatial,
            scenarioDefinition: assembly.Scenario,
            authoredTraversalLinks: level.traversalLinks);
        IReadOnlyList<GameplayCandidate> candidates = builder.Build(
            initial,
            traversalInputs,
            "player");
        Require(candidates.Count > 0,
            "An actor at an authored takeoff produced no traversal candidate.");
        var context = new GameplayDecisionContext(
            initial,
            GameplayObservationSnapshot.FullState("player", initial));
        GameplayExecutableCandidateEvaluation evaluation = routes.Evaluate(
            context,
            candidates[0]);
        Require(evaluation.IsLegal
                && evaluation.ExpectedOutcome.GetValue("move.traversal") == 1f
                && evaluation.Evidence.Count == 1,
            "Authored traversal was not legal and evidence-backed: "
                + evaluation.FailureCode);
        GameplaySemanticTransition transition = routes.Prepare(
            context,
            evaluation);
        var runtime = new GameplaySimulationRuntime(
            new GameplayExecutionIdentity(
                new GameplayContentIdentity(
                    assembly.Scenario.Id,
                    scenarioSchemaVersion: 1,
                    rulesSchemaVersion: 1,
                    new string('5', 64)),
                spatialIdentity,
                gameplay.RunIdentity),
            initial,
            reducers,
            capabilities);
        runtime.Execute(transition);
        Require(runtime.CurrentState.Session.GetActor("player")
                .Pose.Position.DistanceTo(initial.Session.GetActor("player")
                    .Pose.Position) > 1f
            && GameplayExactReplay.Verify(
                initial,
                runtime.Trajectory,
                reducers).IsExact,
            "Authored traversal did not reduce and replay exactly.");
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
                new LevelEntity
                {
                    id = "blocked-prop",
                    archetypeId = "prop.crate.standard",
                    transform = new LevelTransformData(
                        new Float3Data(0f, 0f, 2f),
                        yawDegrees: 0f),
                    coverVolumes = new List<CoverVolumeData>
                    {
                        new CoverVolumeData
                        {
                            id = "primary",
                            localCenter = new Float3Data(0f, 1f, 0f),
                            size = new Float3Data(1f, 2f, 1f),
                        },
                    },
                    destructible = new DestructibleInstanceData
                    {
                        enabled = true,
                        initialState = "intact",
                        integrity = 10f,
                        surfaceId = "surface.wood",
                    },
                },
                new LevelEntity
                {
                    id = "open-prop",
                    archetypeId = "prop.crate.standard",
                    transform = new LevelTransformData(
                        new Float3Data(3f, 0f, 2f),
                        yawDegrees: 0f),
                    coverVolumes = new List<CoverVolumeData>
                    {
                        new CoverVolumeData
                        {
                            id = "primary",
                            localCenter = new Float3Data(0f, 1f, 0f),
                            size = new Float3Data(1f, 2f, 1f),
                        },
                    },
                    destructible = new DestructibleInstanceData
                    {
                        enabled = true,
                        initialState = "intact",
                        integrity = 10f,
                        surfaceId = "surface.wood",
                    },
                },
            },
        };
        level.Normalize();
        GameplaySession gameplay = CreateGameplay(CreateRifle());
        GameplayCombatStateSnapshot state = GameplayCombatStateCapture.Capture(
            gameplay,
            DestructiblePropSession.FromLevel(level, gameplay.Journal));
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
                new GameplayPosition(3f, 1f, 1f)),
            "Static headless obstruction extended beyond its authored volume.");
        Require(!spatial.TryResolveDestructibleDirectFireImpact(
                state,
                origin,
                "blocked-prop",
                out _)
            && spatial.TryResolveDestructibleDirectFireImpact(
                state,
                new GameplayPosition(3f, 1f, -2f),
                "open-prop",
                out DirectFireImpactRecord openImpact)
            && string.Equals(
                openImpact.SurfaceId,
                "surface.wood",
                StringComparison.Ordinal)
            && openImpact.WorldStateRevision
                == state.Session.JournalSequence,
            "Portable direct-fire evidence did not enforce first-hit obstruction and authored material.");
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
        GameplayTransitionReducerRegistry reducers =
            GameplaySimulationReducers.CreateCurrent();
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
            IReadOnlyList<GameplayReachableInput> scenarioInputs =
                GameplayReachableInputEnumerator.Enumerate(assembly, level);
            GameplayCapabilityRegistry scenarioCapabilities =
                GameplayCurrentCapabilityCatalog.Create(
                    reducers,
                    scenarioInputs);
            var spatial = new GameplayHeadlessSpatialEvidence(
                level,
                new SpatialContentIdentity(
                    level.levelId,
                    level.schemaVersion,
                    evidenceAlgorithmVersion: 1,
                    new string('0', 64)));
            GameplayCandidateExecutionRouteRegistry scenarioRoutes =
                GameplayCurrentCandidateExecutionRoutes.Create(
                    assembly,
                    spatial,
                    scenarioCapabilities);
            GameplayExecutableRouteCoverageValidator.Validate(
                scenarioInputs,
                scenarioRoutes).RequireComplete();
            allInputs.AddRange(scenarioInputs);
            scenarioCount++;
        }
        Require(scenarioCount > 0,
            "The all-content coverage gate found no scenarios.");

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
            && openedSight.TotalSampleCount > 6,
            "Headless encounter sight retained a toppled obstruction.");
        TargetExposureSnapshot sharedRaster = GameplayTargetExposureRaster
            .Capture(
                "raster-observer",
                new GameplayPosition(0f, 1.62f, -5f),
                "raster-target",
                ActorTargetProfileCatalog.CreateWorldSamples(
                    new GameplayActorPose(
                        new GameplayPosition(0f, 0f, 0f),
                        0f),
                    pinned: false),
                new PredicateExposureObstruction(
                    (_, destination) => destination.Y < 1f));
        Require(sharedRaster.VisibleSampleCount > 0
            && sharedRaster.VisibleSampleCount < sharedRaster.TotalSampleCount
            && sharedRaster.TotalSampleCount > 6,
            "Portable exposure raster did not preserve partial silhouette cover.");
        EncounterSoundEvidence muffledSound =
            GameplayHeadlessEncounterEvidence.CaptureSound(
                sensingState,
                spatial,
                "observer",
                "target",
                loudness: 1f,
                hearingRange: 12f);
        EncounterSoundEvidence outOfRangeSound =
            GameplayHeadlessEncounterEvidence.CaptureSound(
                sensingState,
                spatial,
                "observer",
                "target",
                loudness: 1f,
                hearingRange: 3f);
        Require(muffledSound.Audibility > 0f
            && muffledSound.Audibility < 0.5f
            && outOfRangeSound.Audibility == 0f,
            "Headless encounter sound diverged from shared range and obstruction attenuation.");
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

    private static AttackDefinition CreateDroneRifle() => new AttackDefinition(
        "attack.drone-rifle",
        "Fire drone rifle",
        new ActionCost(1, 0f, ActionMobility.Set),
        woundMovementPenalty: 1f,
        accuracyDecay: AccuracyDecayDefinition.None,
        directFireDamage: new DirectFireDamageDefinition(
            "damage.drone-ballistic",
            baseIntegrityDamage: 1f),
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

    private sealed class PredicateExposureObstruction :
        ITargetExposureObstructionQuery
    {
        private readonly Func<GameplayPosition, GameplayPosition, bool> blocks;

        public PredicateExposureObstruction(
            Func<GameplayPosition, GameplayPosition, bool> obstruction)
        {
            blocks = obstruction ?? throw new ArgumentNullException(
                nameof(obstruction));
        }

        public bool Blocks(
            GameplayPosition origin,
            GameplayPosition targetSurface) => blocks(origin, targetSurface);
    }

    private sealed class AlwaysClearProjectileQuery :
        IProjectileSegmentQuery
    {
        public ProjectileSegmentQueryResult Query(
            ProjectileSegmentQuery query) =>
            ProjectileSegmentQueryResult.Clear(
                worldStateRevision: 0L);
    }

    private sealed class IntegratedExplosiveEvidence :
        IThrownExplosiveLandingQuery,
        IBlastWorldQuery
    {
        private readonly string firstActorId;
        private readonly string secondActorId;
        private readonly Func<long> worldStateRevision;

        public IntegratedExplosiveEvidence(
            string firstActorId,
            string secondActorId,
            Func<long> worldStateRevision)
        {
            this.firstActorId = firstActorId;
            this.secondActorId = secondActorId;
            this.worldStateRevision = worldStateRevision
                ?? throw new ArgumentNullException(
                    nameof(worldStateRevision));
        }

        public ThrownExplosiveLandingResult Resolve(
            GameplayPosition launchOrigin,
            GameplayPosition sampledLanding) =>
            new ThrownExplosiveLandingResult(
                sampledLanding,
                worldStateRevision());

        public BlastWorldQueryResult Query(BlastWorldQuery query) =>
            new BlastWorldQueryResult(
                query,
                worldStateRevision(),
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

    private sealed class HangingCandidatePolicy : IGameplayCandidatePolicy
    {
        public GameplayPolicyScore Score(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation,
            CancellationToken cancellationToken)
        {
            cancellationToken.WaitHandle.WaitOne();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException(
                "A cancelled hanging policy cannot produce a score.");
        }
    }

    private sealed class StaleInstallationBoundary :
        IGameplayRuntimeInstallationBoundary
    {
        public System.Threading.Tasks.Task<GameplayReductionResult> InstallAsync(
            GameplaySimulationRuntime runtime,
            GameplaySemanticTransition transition,
            GameplayReductionResult reduction,
            CancellationToken cancellationToken) =>
            System.Threading.Tasks.Task.FromException<GameplayReductionResult>(
                new GameplayStaleDecisionStateException(
                    reduction.Previous.CanonicalHash,
                    runtime.CurrentState.CanonicalHash));
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
