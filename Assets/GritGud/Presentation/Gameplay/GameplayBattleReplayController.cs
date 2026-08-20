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
            PrepareAsync(
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

        private async void PrepareAsync(
            GameplayScenarioAssembly assembly,
            LevelDocument level,
            GameplayLiveSessionRuntime liveRuntime,
            CancellationTokenSource owner)
        {
            CancellationToken token = owner.Token;
            try
            {
                await Task.Yield();
                token.ThrowIfCancellationRequested();
                TextAsset asset = Resources.Load<TextAsset>(ArtifactResource);
                if (asset == null)
                    throw new InvalidOperationException(
                        "First-sim artifact resource was not found.");
                GameplayBattleArtifact expected;
                try
                {
                    expected = GameplayBattleArtifactCodec.Read(asset.text);
                }
                finally
                {
                    Resources.UnloadAsset(asset);
                }
                GameplayCombatStateSnapshot initial =
                    GameplayHeadlessBattleStateFactory.Create(assembly, level);
                GameplayExecutionIdentity identity = liveRuntime
                    .ExecutionIdentity;
                if (!identity.HasSameIdentity(
                        expected.Content.ExecutionIdentity)
                    || !string.Equals(
                        initial.CanonicalHash,
                        expected.Content.InitialStateHash,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "First-sim artifact does not match loaded content.");
                var runner = new GameplayBattleRunner(
                    assembly,
                    level,
                    identity,
                    logicalGuardPolicy:
                        new GameplayExecutionLogicalGuardPolicy(
                            maximumTransitions: 2000,
                            maximumRepeatedMaterialStates: 4,
                            maximumNoProgressTurns: 4),
                    workerBoundary:
                        new GameplayCooperativeDecisionWorkerBoundary());
                GameplayBattleRunResult run = await runner.RunAsync(
                    initial,
                    token);
                token.ThrowIfCancellationRequested();
                GameplaySemanticReplayTimeline timeline =
                    GameplayBattleArtifactVerifier.VerifyRun(run, expected);
                if (!ReferenceEquals(cancellation, owner))
                    return;
                hud.SetExternalReplay(timeline, expected);
                GameplayBattleScoreboard score = expected.Content.Scoreboard;
                status = "FIRST SIM READY — "
                    + expected.Content.Terminal.Kind.ToString().ToUpperInvariant()
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
