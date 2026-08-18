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
            VerifyAtomicLiveInstallation();
            VerifyAllCurrentContentCoverage();
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

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
