using System;
using System.Threading;
using System.Threading.Tasks;

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
}
