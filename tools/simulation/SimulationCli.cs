using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
            out LevelDocument level);
        GameplayCombatStateSnapshot initial =
            GameplayHeadlessBattleStateFactory.Create(assembly, level);
        GameplayExecutionIdentity identity = CreateIdentity(
            assembly,
            level,
            initial.Session.RunIdentity);
        var runner = new GameplayBattleRunner(
            assembly,
            level,
            identity,
            logicalGuardPolicy: new GameplayExecutionLogicalGuardPolicy(
                maximumTransitions: 2000,
                maximumRepeatedMaterialStates: 4,
                maximumNoProgressTurns: 4));
        GameplayBattleRunResult result = runner.RunAsync(initial)
            .GetAwaiter().GetResult();
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
        PrintSummary(persisted, fullPath, result);
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
            out LevelDocument level);
        GameplayCombatStateSnapshot initial =
            GameplayHeadlessBattleStateFactory.Create(assembly, level);
        GameplayExecutionIdentity identity = CreateIdentity(
            assembly,
            level,
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
            level,
            identity,
            logicalGuardPolicy: new GameplayExecutionLogicalGuardPolicy(
                maximumTransitions: 2000,
                maximumRepeatedMaterialStates: 4,
                maximumNoProgressTurns: 4));
        GameplayBattleRunResult rerun = runner.RunAsync(initial)
            .GetAwaiter().GetResult();
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
        PrintSummary(actual, input, rerun);
        Console.WriteLine("verification=exact");
        return 0;
    }

    private static GameplayExecutionIdentity CreateIdentity(
        GameplayScenarioAssembly assembly,
        LevelDocument level,
        ScenarioRunIdentity run) => new GameplayExecutionIdentity(
            new GameplayContentIdentity(
                assembly.Scenario.Id,
                ScenarioContentDocument.CurrentSchemaVersion,
                GameplayCombatStateSnapshot.CurrentSchemaVersion,
                GameplayCanonicalValueDigest.Calculate(assembly.Scenario)),
            new SpatialContentIdentity(
                level.levelId,
                level.schemaVersion,
                evidenceAlgorithmVersion: 1,
                GameplayCanonicalValueDigest.Calculate(level)),
            run);

    private static void PrintSummary(
        GameplayBattleArtifact artifact,
        string path,
        GameplayBattleRunResult run)
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
            + " drone-moves=" + score.DroneMoves
            + " drone-attacks=" + score.DroneAttacks);
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
    }

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
