using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Domain.Turns;

internal static class SimulationChecks
{
    private static int Main()
    {
        try
        {
            VerifyWalkingSliceAndExactReplay();
            VerifyCapabilityCoverageFailsClosed();
            VerifyTacticalRuleCoverageAndOutcomeProjection();
            VerifyAtomicLiveInstallation();
            VerifyAllCurrentContentCoverage();
            VerifyTacticalDestructibleSimulation();
            VerifyEncounterAwarenessAndScopedInitiative();
            VerifyBankedActionPointEconomy();
            SimulationParityChecks.Verify();
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
        Require(live.InitiativeOrder.Count == 2
                && !ContainsActor(live.InitiativeOrder, "bystander"),
            "Scoped encounter incorrectly included a nonparticipant.");

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
                loops: true));
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
        var scenario = new ScenarioDefinition(
            "encounter-check",
            new ScenarioTimingDefinition(1f),
            new[] { player, enemy, bystander },
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
            LevelDocument level = ReadJson<LevelDocument>(path, json);
            level.Normalize();
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

    private static GameplayCombatStateSnapshot WithDestructible(
        GameplayCombatStateSnapshot source,
        DestructiblePropSnapshot prop) => new GameplayCombatStateSnapshot(
            source.Session,
            new[] { prop },
            source.Vehicles,
            source.Projectiles,
            source.SmokeFields,
            source.Coverage);

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
        accuracyDecay: AccuracyDecayDefinition.None);

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
}
