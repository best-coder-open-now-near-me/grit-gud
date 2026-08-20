using System;
using System.Threading;
using System.Threading.Tasks;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    internal sealed class GameplayBattleReplayController : MonoBehaviour
    {
        internal const string ArtifactResource =
            "SimulationArtifacts/depot-first-sim";

        private CancellationTokenSource cancellation;
        private Task preparation = Task.CompletedTask;
        private GameplayTurnReplayHud hud;
        private string status = string.Empty;
        private GUIStyle statusStyle;

        public void Bind(
            GameplayScenarioAssembly assembly,
            LevelDocument level,
            GameplayLiveSessionRuntime liveRuntime,
            GameplayTurnReplayHud replayHud)
        {
            Unbind();
            if (assembly == null) throw new ArgumentNullException(
                nameof(assembly));
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (liveRuntime == null) throw new ArgumentNullException(
                nameof(liveRuntime));
            hud = replayHud ?? throw new ArgumentNullException(nameof(replayHud));
            cancellation = new CancellationTokenSource();
            status = "PREPARING FIRST SIM…";
            enabled = true;
            preparation = PrepareAsync(
                assembly,
                level,
                liveRuntime,
                cancellation);
        }

        public void Unbind()
        {
            cancellation?.Cancel();
            cancellation = null;
            hud = null;
            status = string.Empty;
            enabled = false;
        }

        private async Task PrepareAsync(
            GameplayScenarioAssembly assembly,
            LevelDocument level,
            GameplayLiveSessionRuntime liveRuntime,
            CancellationTokenSource owner)
        {
            CancellationToken token = owner.Token;
            try
            {
                GameplayBattleReplayPreparationResult<
                    GameplayBattleArtifact,
                    GameplaySemanticReplayTimeline> result =
                    await GameplayBattleReplayPreparationWorkflow
                        .PrepareAsync(
                            LoadArtifact,
                            expected => MatchesLoadedContent(
                                assembly,
                                level,
                                liveRuntime,
                                expected),
                            () => GameplayHeadlessBattleStateFactory.Create(
                                assembly,
                                level),
                            (initial, expected, cancellationToken) =>
                                RunBattleAsync(
                                    assembly,
                                    level,
                                    initial,
                                    expected.Content.ExecutionIdentity,
                                    cancellationToken),
                            GameplayBattleArtifactVerifier.VerifyRun,
                            token);
                if (!result.IsReady)
                {
                    if (ReferenceEquals(cancellation, owner))
                        status = "FIRST SIM UNAVAILABLE FOR THIS SCENARIO";
                    return;
                }
                if (!ReferenceEquals(cancellation, owner))
                    return;
                hud.SetVerifiedExternalReplay(result.Replay, result.Artifact);
                GameplayBattleScoreboard score = result.Artifact.Content
                    .Scoreboard;
                status = "FIRST SIM READY — "
                    + result.Artifact.Content.Terminal.Kind.ToString()
                        .ToUpperInvariant()
                    + " · " + score.TurnsCompleted + " TURNS — CLICK WATCH";
            }
            catch (OperationCanceledException)
            {
                if (ReferenceEquals(cancellation, owner))
                    status = string.Empty;
            }
            catch (Exception exception)
            {
                if (ReferenceEquals(cancellation, owner))
                {
                    status = "FIRST SIM UNAVAILABLE — " + exception.Message;
                    Debug.LogException(exception, this);
                }
            }
            finally
            {
                if (ReferenceEquals(cancellation, owner))
                    cancellation = null;
                owner.Dispose();
            }
        }

        private static GameplayBattleArtifact LoadArtifact()
        {
            TextAsset asset = Resources.Load<TextAsset>(ArtifactResource);
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
            GameplayLiveSessionRuntime liveRuntime,
            GameplayBattleArtifact expected)
        {
            GameplayExecutionIdentity identity = expected.Content
                .ExecutionIdentity;
            GameplayExecutionIdentity liveIdentity = liveRuntime
                .ExecutionIdentity;
            return liveIdentity.Run.HasSameIdentity(identity.Run)
                && string.Equals(
                    assembly.Scenario.Id,
                    identity.Gameplay.ScenarioId,
                    StringComparison.Ordinal)
                && string.Equals(
                    level.levelId,
                    identity.Spatial.LevelId,
                    StringComparison.Ordinal);
        }

        private static Task<GameplayBattleRunResult> RunBattleAsync(
            GameplayScenarioAssembly assembly,
            LevelDocument level,
            GameplayCombatStateSnapshot initial,
            GameplayExecutionIdentity resultIdentity,
            CancellationToken cancellationToken)
        {
            var runner = new GameplayBattleRunner(
                assembly,
                level,
                resultIdentity,
                logicalGuardPolicy:
                    new GameplayExecutionLogicalGuardPolicy(
                        maximumTransitions: 2000,
                        maximumRepeatedMaterialStates: 4,
                        maximumNoProgressTurns: 4),
                workerBoundary:
                    new GameplayCooperativeDecisionWorkerBoundary());
            return runner.RunAsync(initial, cancellationToken);
        }

        private void OnGUI()
        {
            if (string.IsNullOrWhiteSpace(status) || hud?.IsOpen == true)
                return;
            statusStyle ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
            };
            float width = Mathf.Min(420f, Screen.width - 28f);
            GUI.Box(
                new Rect((Screen.width - width) * 0.5f, 14f, width, 28f),
                status,
                statusStyle);
        }

        private void OnDestroy() => Unbind();
    }
}
