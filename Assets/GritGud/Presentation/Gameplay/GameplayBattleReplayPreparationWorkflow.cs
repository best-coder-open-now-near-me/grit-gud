using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayBattleReplayPreparationResult<
        TArtifact,
        TReplay>
    {
        private GameplayBattleReplayPreparationResult(
            bool isReady,
            TArtifact artifact,
            TReplay replay)
        {
            IsReady = isReady;
            Artifact = artifact;
            Replay = replay;
        }

        public bool IsReady { get; }
        public TArtifact Artifact { get; }
        public TReplay Replay { get; }

        public static GameplayBattleReplayPreparationResult<
            TArtifact,
            TReplay> Ready(TArtifact artifact, TReplay replay) =>
            new GameplayBattleReplayPreparationResult<TArtifact, TReplay>(
                true,
                artifact,
                replay);

        public static GameplayBattleReplayPreparationResult<
            TArtifact,
            TReplay> ContentMismatch(TArtifact artifact) =>
            new GameplayBattleReplayPreparationResult<TArtifact, TReplay>(
                false,
                artifact,
                default);
    }

    /// <summary>
    /// Owns the cancellable trust sequence between loading an expected battle
    /// artifact and exposing a verified semantic replay to presentation.
    /// Every completed stage is followed by a cancellation checkpoint so a
    /// retired controller owner cannot advance into the next stage.
    /// </summary>
    internal static class GameplayBattleReplayPreparationWorkflow
    {
        public static async Task<GameplayBattleReplayPreparationResult<
            TArtifact,
            TReplay>> PrepareAsync<TArtifact, TInitialState, TRun, TReplay>(
            Func<TArtifact> loadArtifact,
            Func<TArtifact, bool> matchesLoadedContent,
            Func<TInitialState> createInitialState,
            Func<TInitialState, TArtifact, CancellationToken, Task<TRun>>
                runSimulation,
            Func<TRun, TArtifact, TReplay> verifyRun,
            CancellationToken cancellationToken)
        {
            if (loadArtifact == null)
                throw new ArgumentNullException(nameof(loadArtifact));
            if (matchesLoadedContent == null)
                throw new ArgumentNullException(nameof(matchesLoadedContent));
            if (createInitialState == null)
                throw new ArgumentNullException(nameof(createInitialState));
            if (runSimulation == null)
                throw new ArgumentNullException(nameof(runSimulation));
            if (verifyRun == null)
                throw new ArgumentNullException(nameof(verifyRun));

            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            TArtifact artifact = loadArtifact();
            cancellationToken.ThrowIfCancellationRequested();

            bool contentMatches = matchesLoadedContent(artifact);
            cancellationToken.ThrowIfCancellationRequested();
            if (!contentMatches)
                return GameplayBattleReplayPreparationResult<
                    TArtifact,
                    TReplay>.ContentMismatch(artifact);

            TInitialState initialState = createInitialState();
            cancellationToken.ThrowIfCancellationRequested();

            TRun run = await runSimulation(
                initialState,
                artifact,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            TReplay replay = verifyRun(run, artifact);
            cancellationToken.ThrowIfCancellationRequested();

            return GameplayBattleReplayPreparationResult<
                TArtifact,
                TReplay>.Ready(artifact, replay);
        }
    }

    /// <summary>
    /// Loads the embedded authored simulation before presentation constructs a
    /// playable world. Runtime playback rehydrates the stored trajectory;
    /// regenerating the tactical policy run remains an offline
    /// verification concern.
    /// </summary>
    internal static class GameplayFirstSimulationPreparationService
    {
        public static async Task<GameplayBattleReplayPreparationResult<
            GameplayBattleArtifact,
            GameplaySemanticReplayTimeline>> PrepareAsync(
            GameplayScenarioAssembly assembly,
            LevelDocument level,
            CancellationToken cancellationToken)
        {
            if (assembly == null) throw new ArgumentNullException(
                nameof(assembly));
            if (level == null) throw new ArgumentNullException(nameof(level));
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            GameplayBattleArtifact artifact = LoadArtifact();
            cancellationToken.ThrowIfCancellationRequested();
            GameplayExecutionIdentity loadedIdentity =
                CreateLoadedIdentity(assembly, level);
            GameplayExecutionIdentity artifactIdentity = artifact.Content
                .ExecutionIdentity;
            RequireViewerIdentityCompatibility(
                artifactIdentity,
                loadedIdentity);

            GameplaySemanticReplayTimeline replay =
                GameplayBattleArtifactReplayLoader.Load(artifact);
            cancellationToken.ThrowIfCancellationRequested();
            RequireViewerEntityCompatibility(assembly, replay.InitialState);
            return GameplayBattleReplayPreparationResult<
                GameplayBattleArtifact,
                GameplaySemanticReplayTimeline>.Ready(artifact, replay);
        }

        private static GameplayBattleArtifact LoadArtifact()
        {
            TextAsset asset = Resources.Load<TextAsset>(
                GameplayBattleReplayController.ArtifactResource);
            if (asset == null)
                throw new InvalidOperationException(
                    "First-sim artifact resource was not found.");
            try
            {
                return GameplayBattleArtifactCodec.Read(asset.text);
            }
            finally
            {
                Resources.UnloadAsset(asset);
            }
        }

        private static GameplayExecutionIdentity CreateLoadedIdentity(
            GameplayScenarioAssembly assembly,
            LevelDocument level)
        {
            var spatial = new SpatialContentIdentity(
                level.levelId,
                level.schemaVersion,
                evidenceAlgorithmVersion: 1,
                GameplayCanonicalValueDigest.Calculate(level));
            GameplayScenarioAssembly grounded =
                GameplayHeadlessScenarioGrounding.Resolve(
                    assembly,
                    new GameplayHeadlessSpatialEvidence(level, spatial));
            return new GameplayExecutionIdentity(
                new GameplayContentIdentity(
                    grounded.Scenario.Id,
                    ScenarioContentDocument.CurrentSchemaVersion,
                    GameplayCombatStateSnapshot.CurrentSchemaVersion,
                    GameplayCanonicalValueDigest.Calculate(
                        grounded.Scenario)),
                spatial,
                new ScenarioRunIdentity(
                    grounded.Scenario.Id + ".run",
                    grounded.RandomSeed));
        }

        private static string DescribeIdentityMismatch(
            GameplayExecutionIdentity expected,
            GameplayExecutionIdentity actual)
        {
            if (!actual.Run.HasSameIdentity(expected.Run))
            {
                return "run identity mismatch (expected "
                    + expected.Run.RunId + "/" + expected.Run.ScenarioSeed
                    + "/random-v" + expected.Run.RandomSchemaVersion
                    + ", loaded " + actual.Run.RunId + "/"
                    + actual.Run.ScenarioSeed + "/random-v"
                    + actual.Run.RandomSchemaVersion + ").";
            }
            if (!actual.Spatial.HasSameIdentity(expected.Spatial))
            {
                return "spatial identity mismatch (expected "
                    + expected.Spatial.LevelId + "/"
                    + expected.Spatial.LevelSchemaVersion + "/evidence-v"
                    + expected.Spatial.EvidenceAlgorithmVersion + "/"
                    + ShortDigest(expected.Spatial.StaticSpatialDigest)
                    + ", loaded " + actual.Spatial.LevelId + "/"
                    + actual.Spatial.LevelSchemaVersion + "/evidence-v"
                    + actual.Spatial.EvidenceAlgorithmVersion + "/"
                    + ShortDigest(actual.Spatial.StaticSpatialDigest) + ").";
            }
            return "gameplay identity mismatch (expected "
                + expected.Gameplay.ScenarioId + "/"
                + expected.Gameplay.ScenarioSchemaVersion + "/rules-v"
                + expected.Gameplay.RulesSchemaVersion + "/"
                + ShortDigest(expected.Gameplay.DefinitionDigest)
                + ", loaded " + actual.Gameplay.ScenarioId + "/"
                + actual.Gameplay.ScenarioSchemaVersion + "/rules-v"
                + actual.Gameplay.RulesSchemaVersion + "/"
                + ShortDigest(actual.Gameplay.DefinitionDigest) + ").";
        }

        private static void RequireViewerIdentityCompatibility(
            GameplayExecutionIdentity expected,
            GameplayExecutionIdentity actual)
        {
            if (!actual.Run.HasSameIdentity(expected.Run)
                || !actual.Spatial.HasSameIdentity(expected.Spatial)
                || actual.Gameplay.SchemaVersion
                    != expected.Gameplay.SchemaVersion
                || actual.Gameplay.ScenarioSchemaVersion
                    != expected.Gameplay.ScenarioSchemaVersion
                || actual.Gameplay.RulesSchemaVersion
                    != expected.Gameplay.RulesSchemaVersion
                || !string.Equals(
                    actual.Gameplay.ScenarioId,
                    expected.Gameplay.ScenarioId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "First-sim " + DescribeIdentityMismatch(expected, actual));
            }

            // Full definition-digest equality remains mandatory when the
            // artifact is generated or re-simulated. This viewer executes no
            // gameplay: it presents the artifact's independently verified
            // states and events against the exact spatial content, so its
            // remaining dependency is the presentation entity graph below.
        }

        private static void RequireViewerEntityCompatibility(
            GameplayScenarioAssembly assembly,
            GameplayCombatStateSnapshot initial)
        {
            RequireSameIds(
                "actor",
                initial.Session.Actors.Select(value => value.ActorId),
                assembly.Actors.Select(value => value.Id));
            RequireSameIds(
                "objective",
                initial.Session.Objectives.Select(value => value.ObjectiveId),
                assembly.Scenario.Objectives.Select(value => value.Id));
            RequireSameIds(
                "vehicle",
                initial.Vehicles.Select(value => value.VehicleId),
                assembly.Vehicles.Select(value => value.EntityId));
            RequireSameIds(
                "drone",
                initial.Drones.Select(value => value.DroneId),
                assembly.Drones.Select(value => value.Id));
        }

        private static void RequireSameIds(
            string label,
            IEnumerable<string> expected,
            IEnumerable<string> actual)
        {
            var expectedIds = new HashSet<string>(
                expected,
                StringComparer.Ordinal);
            var actualIds = new HashSet<string>(
                actual,
                StringComparer.Ordinal);
            if (expectedIds.SetEquals(actualIds)) return;
            string missing = string.Join(
                ",",
                expectedIds.Except(actualIds).OrderBy(value => value));
            string extra = string.Join(
                ",",
                actualIds.Except(expectedIds).OrderBy(value => value));
            throw new InvalidOperationException(
                "First-sim " + label + " roster mismatch (missing loaded: "
                + (string.IsNullOrEmpty(missing) ? "none" : missing)
                + "; extra loaded: "
                + (string.IsNullOrEmpty(extra) ? "none" : extra) + ").");
        }

        private static string ShortDigest(string value) =>
            value.Substring(0, 12);
    }
}
