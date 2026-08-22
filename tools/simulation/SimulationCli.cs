using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;

internal static class SimulationCli
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0) return Usage();
            switch (args[0])
            {
                case "run-depot":
                    return RunDepot(ParseOptions(args, 1));
                case "verify":
                    return Verify(ParseOptions(args, 1));
                default:
                    return Usage();
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                exception.GetType().Name + ": " + exception.Message);
            return 1;
        }
    }

    private static int RunDepot(IReadOnlyDictionary<string, string> options)
    {
        string output = Required(options, "output");
        string revision = Required(options, "source-revision");
        string branch = Required(options, "source-branch");
        string label = Optional(options, "label", "depot-first-sim");
        SimulationRepositoryContent.LoadDepot(
            out GameplayScenarioAssembly assembly,
            out LevelDocument level,
            out GameplayStaticSpatialContent spatialContent);
        GameplayCombatStateSnapshot initial =
            GameplayHeadlessBattleStateFactory.Create(assembly, spatialContent);
        GameplayExecutionIdentity identity = CreateIdentity(
            assembly,
            spatialContent,
            initial.Session.RunIdentity);
        var runner = new GameplayBattleRunner(
            assembly,
            spatialContent,
            identity,
            deadlinePolicy: ArtifactDeadlinePolicy(),
            logicalGuardPolicy: new GameplayExecutionLogicalGuardPolicy(
                maximumTransitions: 2000,
                maximumRepeatedMaterialStates: 4,
                maximumNoProgressTurns: 4));
        var executionClock = Stopwatch.StartNew();
        GameplayBattleRunResult result = runner.RunAsync(initial)
            .GetAwaiter().GetResult();
        executionClock.Stop();
        if (!result.Terminal.IsSuccessful)
            throw new InvalidOperationException(
                "Battle execution failed with " + result.Terminal.FailureKind
                    + ": " + result.Terminal.FailureMessage);
        GameplayBattleArtifact artifact = GameplayBattleArtifactFactory.Create(
            result,
            new GameplayBattleArtifactProvenance(
                revision,
                branch,
                label));
        string canonical = artifact.ToPortableJson();
        string fullPath = Path.GetFullPath(output);
        string directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException(
                "Artifact output path has no parent directory.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, canonical);
        GameplayBattleArtifact persisted = GameplayBattleArtifactCodec.Read(
            File.ReadAllText(fullPath));
        if (!string.Equals(
                persisted.ArtifactId,
                artifact.ArtifactId,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Persisted artifact identity changed after strict reading.");
        PrintSummary(persisted, fullPath, result, executionClock.Elapsed);
        return 0;
    }

    private static int Verify(IReadOnlyDictionary<string, string> options)
    {
        string input = Path.GetFullPath(Required(options, "input"));
        string canonical = File.ReadAllText(input);
        GameplayBattleArtifact expected = GameplayBattleArtifactCodec.Read(
            canonical);
        SimulationRepositoryContent.LoadDepot(
            out GameplayScenarioAssembly assembly,
            out LevelDocument level,
            out GameplayStaticSpatialContent spatialContent);
        GameplayCombatStateSnapshot initial =
            GameplayHeadlessBattleStateFactory.Create(assembly, spatialContent);
        GameplayExecutionIdentity identity = CreateIdentity(
            assembly,
            spatialContent,
            initial.Session.RunIdentity);
        if (!identity.HasSameIdentity(expected.Content.ExecutionIdentity))
            throw new InvalidOperationException(
                "Current content/run identity does not match the artifact.");
        if (!string.Equals(
                initial.CanonicalHash,
                expected.Content.InitialStateHash,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Current initial state does not match the artifact.");
        var runner = new GameplayBattleRunner(
            assembly,
            spatialContent,
            identity,
            deadlinePolicy: ArtifactDeadlinePolicy(),
            logicalGuardPolicy: new GameplayExecutionLogicalGuardPolicy(
                maximumTransitions: 2000,
                maximumRepeatedMaterialStates: 4,
                maximumNoProgressTurns: 4));
        var executionClock = Stopwatch.StartNew();
        GameplayBattleRunResult rerun = runner.RunAsync(initial)
            .GetAwaiter().GetResult();
        executionClock.Stop();
        GameplayBattleArtifact actual = GameplayBattleArtifactFactory.Create(
            rerun,
            expected.Content.Provenance);
        if (!string.Equals(
                actual.ArtifactId,
                expected.ArtifactId,
                StringComparison.Ordinal)
            || !string.Equals(
                actual.ToPortableJson(),
                canonical,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Fresh execution diverged from the battle artifact.");
        PrintSummary(actual, input, rerun, executionClock.Elapsed);
        Console.WriteLine("verification=exact");
        return 0;
    }

    private static GameplayExecutionIdentity CreateIdentity(
        GameplayScenarioAssembly assembly,
        GameplayStaticSpatialContent spatialContent,
        ScenarioRunIdentity run) => new GameplayExecutionIdentity(
            new GameplayContentIdentity(
                assembly.Scenario.Id,
                ScenarioContentDocument.CurrentSchemaVersion,
                GameplayCombatStateSnapshot.CurrentSchemaVersion,
                GameplayCanonicalValueDigest.Calculate(assembly.Scenario)),
            spatialContent.Identity,
            run);

    private static void PrintSummary(
        GameplayBattleArtifact artifact,
        string path,
        GameplayBattleRunResult run,
        TimeSpan executionElapsed)
    {
        GameplayBattleScoreboard score = artifact.Content.Scoreboard;
        Console.WriteLine("artifact=" + artifact.ArtifactId);
        Console.WriteLine("path=" + path);
        Console.WriteLine("terminal=" + artifact.Content.Terminal.Kind);
        Console.WriteLine(
            "decisions=" + score.Decisions
            + " transitions=" + score.Transitions
            + " turns=" + score.TurnsCompleted);
        Console.WriteLine(
            "attacks=" + score.Attacks
            + " hits=" + score.Hits
            + " wounds=" + score.Wounds
            + " explosives=" + score.ExplosiveThrows
            + " concussive-targets=" + score.ConcussiveTargets
            + " fire=" + score.FireDeployments
            + " drone-summons=" + score.DroneSummons
            + " drone-moves=" + score.DroneMoves
            + " drone-attacks=" + score.DroneAttacks
            + " drone-dismissals=" + score.DroneDismissals
            + " drone-expirations=" + score.DroneExpirations
            + " drone-crashes=" + score.DroneCrashes);
        Console.WriteLine(
            "reloads=" + score.Reloads
            + " rounds-spent=" + score.RoundsSpent
            + " rounds-reloaded=" + score.RoundsReloaded
            + " final-loaded=" + score.FinalLoadedRounds
            + " final-reserve=" + score.FinalReserveRounds);
        foreach (GameplayBattleAmmunitionScore ammunition in score.Ammunition)
            Console.WriteLine(
                "ammo=" + ammunition.AmmoTypeId
                + " reloads=" + ammunition.Reloads
                + " spent=" + ammunition.RoundsSpent
                + " reloaded=" + ammunition.RoundsReloaded
                + " final-loaded=" + ammunition.FinalLoadedRounds
                + " final-reserve=" + ammunition.FinalReserveRounds);
        var replay = new GameplaySemanticReplayTimeline(
            run.InitialState,
            run.CreateTrajectory(),
            GameplaySimulationReducers.CreateCurrent());
        var playback = new GameplaySemanticReplayPlaybackTimeline(replay);
        Console.WriteLine(
            "presentation-seconds="
            + playback.TotalDurationSeconds.ToString(
                "0.###",
                CultureInfo.InvariantCulture));
        PrintPerformanceProfile(
            artifact,
            run,
            replay,
            executionElapsed);
    }

    private static void PrintPerformanceProfile(
        GameplayBattleArtifact artifact,
        GameplayBattleRunResult run,
        GameplaySemanticReplayTimeline replay,
        TimeSpan executionElapsed)
    {
        var candidateCounts = new List<int>(run.Decisions.Count);
        long totalCandidates = 0L;
        long totalLegal = 0L;
        int slowestDecision = -1;
        double slowestDecisionMilliseconds = 0d;
        var stages = new Dictionary<
            GameplayDecisionStage,
            StageAggregate>();
        foreach (GameplayBattleDecisionRecord decision in run.Decisions)
        {
            int candidateCount = decision.CandidateIds.Count;
            candidateCounts.Add(candidateCount);
            totalCandidates += candidateCount;
            totalLegal += decision.LegalCandidateIds.Count;
            double decisionMilliseconds = 0d;
            foreach (GameplayDecisionStageTiming timing in decision.Diagnostic
                .Timings)
            {
                if (!stages.TryGetValue(
                        timing.Stage,
                        out StageAggregate aggregate))
                {
                    aggregate = new StageAggregate();
                    stages.Add(timing.Stage, aggregate);
                }
                aggregate.Add(timing.Elapsed.TotalMilliseconds);
                decisionMilliseconds += timing.Elapsed.TotalMilliseconds;
            }
            if (decisionMilliseconds > slowestDecisionMilliseconds)
            {
                slowestDecisionMilliseconds = decisionMilliseconds;
                slowestDecision = decision.DecisionIndex;
            }
        }
        candidateCounts.Sort();
        Console.WriteLine(
            "execution-seconds=" + executionElapsed.TotalSeconds.ToString(
                "0.###",
                CultureInfo.InvariantCulture));
        if (candidateCounts.Count > 0)
        {
            Console.WriteLine(
                "candidates min=" + candidateCounts[0]
                + " avg=" + ((double)totalCandidates
                    / candidateCounts.Count).ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                + " p95=" + Percentile(candidateCounts, 0.95d)
                + " max=" + candidateCounts[candidateCounts.Count - 1]
                + " total=" + totalCandidates);
            Console.WriteLine(
                "legal-candidates=" + totalLegal
                + " legal-percent=" + (totalCandidates == 0L
                    ? "0"
                    : (100d * totalLegal / totalCandidates).ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)));
            Console.WriteLine(
                "slowest-decision=" + slowestDecision
                + " milliseconds=" + slowestDecisionMilliseconds.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture));
        }
        foreach (GameplayDecisionStage stage in Enum.GetValues(
            typeof(GameplayDecisionStage)))
        {
            if (!stages.TryGetValue(stage, out StageAggregate aggregate))
                continue;
            Console.WriteLine(
                "stage=" + stage
                + " avg-ms=" + aggregate.Average.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                + " max-ms=" + aggregate.Maximum.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                + " total-ms=" + aggregate.Total.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture));
        }

        var materialVisits = new Dictionary<string, int>(
            StringComparer.Ordinal);
        AddMaterialVisit(materialVisits, run.InitialState);
        foreach (GameplaySemanticReplayFrame frame in replay.Frames)
            AddMaterialVisit(materialVisits, frame.Resulting);
        int repeatedObservations = 0;
        int maximumVisits = 0;
        int repeatedGuardHits = 0;
        foreach (int visits in materialVisits.Values)
        {
            if (visits > 1) repeatedObservations += visits - 1;
            if (visits > maximumVisits) maximumVisits = visits;
            if (visits > 4) repeatedGuardHits += visits - 4;
        }
        Console.WriteLine(
            "material-states unique=" + materialVisits.Count
            + " observations=" + (replay.Frames.Count + 1)
            + " repeats=" + repeatedObservations
            + " max-visits=" + maximumVisits
            + " repeated-guard-hits=" + repeatedGuardHits);

        GameplayBattleScoreboard score = artifact.Content.Scoreboard;
        Console.WriteLine(
            "rounds-per-wound=" + (score.Wounds == 0
                ? "n/a"
                : ((double)score.RoundsSpent / score.Wounds).ToString(
                    "0.###",
                    CultureInfo.InvariantCulture))
            + " rockets-per-victory=" + RoundsSpent(
                score,
                "ammo.rocket")
            + " artifact-bytes=" + Encoding.UTF8.GetByteCount(
                artifact.ToPortableJson()));
    }

    private static int Percentile(IReadOnlyList<int> sorted, double value)
    {
        int index = (int)Math.Ceiling(sorted.Count * value) - 1;
        if (index < 0) index = 0;
        if (index >= sorted.Count) index = sorted.Count - 1;
        return sorted[index];
    }

    private static void AddMaterialVisit(
        IDictionary<string, int> visits,
        GameplayCombatStateSnapshot state)
    {
        string digest = GameplayMaterialStateDigest.Calculate(state);
        visits.TryGetValue(digest, out int count);
        visits[digest] = count + 1;
    }

    private static int RoundsSpent(
        GameplayBattleScoreboard scoreboard,
        string ammoTypeId)
    {
        foreach (GameplayBattleAmmunitionScore score in scoreboard.Ammunition)
            if (string.Equals(
                    score.AmmoTypeId,
                    ammoTypeId,
                    StringComparison.Ordinal))
                return score.RoundsSpent;
        return 0;
    }

    private sealed class StageAggregate
    {
        public int Count { get; private set; }
        public double Total { get; private set; }
        public double Maximum { get; private set; }
        public double Average => Count == 0 ? 0d : Total / Count;

        public void Add(double milliseconds)
        {
            Count++;
            Total += milliseconds;
            if (milliseconds > Maximum) Maximum = milliseconds;
        }
    }

    private static GameplayExecutionDeadlinePolicy ArtifactDeadlinePolicy() =>
        new GameplayExecutionDeadlinePolicy(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(10),
            TimeSpan.FromMinutes(30));

    private static IReadOnlyDictionary<string, string> ParseOptions(
        IReadOnlyList<string> args,
        int start)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = start; index < args.Count; index += 2)
        {
            string key = args[index];
            if (!key.StartsWith("--", StringComparison.Ordinal)
                || index + 1 >= args.Count)
                throw new ArgumentException(
                    "Options require --name value pairs.");
            key = key.Substring(2);
            if (!result.TryAdd(key, args[index + 1]))
                throw new ArgumentException(
                    "Option '--" + key + "' was repeated.");
        }
        return result;
    }

    private static string Required(
        IReadOnlyDictionary<string, string> options,
        string key) => options.TryGetValue(key, out string value)
            && !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new ArgumentException(
                    "Missing required option '--" + key + "'.");

    private static string Optional(
        IReadOnlyDictionary<string, string> options,
        string key,
        string fallback) => options.TryGetValue(key, out string value)
            ? value
            : fallback;

    private static int Usage()
    {
        Console.Error.WriteLine(
            "Usage:\n"
            + "  run-depot --output <path> --source-revision <sha> "
            + "--source-branch <branch> [--label <label>]\n"
            + "  verify --input <artifact-path>");
        return 2;
    }
}
