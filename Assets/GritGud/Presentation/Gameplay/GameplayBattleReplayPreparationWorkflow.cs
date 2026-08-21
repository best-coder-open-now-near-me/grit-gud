using System;
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
            if (!MatchesLoadedContent(assembly, level, artifact))
            {
                return GameplayBattleReplayPreparationResult<
                    GameplayBattleArtifact,
                    GameplaySemanticReplayTimeline>.ContentMismatch(artifact);
            }

            GameplaySemanticReplayTimeline replay =
                GameplayBattleArtifactReplayLoader.Load(artifact);
            cancellationToken.ThrowIfCancellationRequested();
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

        private static bool MatchesLoadedContent(
            GameplayScenarioAssembly assembly,
            LevelDocument level,
            GameplayBattleArtifact expected)
        {
            GameplayExecutionIdentity identity = expected.Content
                .ExecutionIdentity;
            var actual = new GameplayExecutionIdentity(
                new GameplayContentIdentity(
                    assembly.Scenario.Id,
                    ScenarioContentDocument.CurrentSchemaVersion,
                    GameplayCombatStateSnapshot.CurrentSchemaVersion,
                    GameplayCanonicalValueDigest.Calculate(
                        assembly.Scenario)),
                new SpatialContentIdentity(
                    level.levelId,
                    level.schemaVersion,
                    evidenceAlgorithmVersion: 1,
                    GameplayCanonicalValueDigest.Calculate(level)),
                new ScenarioRunIdentity(
                    assembly.Scenario.Id + ".run",
                    assembly.RandomSeed));
            return actual.HasSameIdentity(identity);
        }

    }
}
