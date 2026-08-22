using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayBattleArtifactPresentationTests
    {
        [Test]
        public async Task EmbeddedFirstSimulationPreparesAgainstCurrentContent()
        {
            GameplayContentPackage content = GameplayContentLoader.LoadDefault();

            GameplayBattleReplayPreparationResult<
                GameplayBattleArtifact,
                GameplaySemanticReplayTimeline> result =
                await GameplayFirstSimulationPreparationService.PrepareAsync(
                    content.Assembly,
                    content.SpatialContent,
                    CancellationToken.None);

            Assert.That(result.IsReady, Is.True);
            Assert.That(result.Replay, Is.Not.Null);
            Assert.That(result.Replay.Frames, Is.Not.Empty);
        }

        [Test]
        public async Task EmbeddedTerminalEpisodesPersistAndExtendFinalPlayback()
        {
            GameplayContentPackage content = GameplayContentLoader.LoadDefault();
            GameplayBattleReplayPreparationResult<
                GameplayBattleArtifact,
                GameplaySemanticReplayTimeline> result =
                await GameplayFirstSimulationPreparationService.PrepareAsync(
                    content.Assembly,
                    content.SpatialContent,
                    CancellationToken.None);

            Assert.That(result.IsReady, Is.True);
            var playback = new GameplaySemanticReplayPlaybackTimeline(
                result.Replay);
            ReplayActorTerminalPoseEpisode warehousePatrol = playback
                .TerminalPoseEpisodes.Single(episode =>
                    episode.ActorId == "depot-warehouse-patrol"
                    && episode.SourceTransitionSequence == 7);
            GameplaySemanticReplayPlaybackFrame sourceFrame = playback.Frames
                .Single(frame =>
                    frame.Frame.Transition.Identity.Sequence == 7);
            GameplaySemanticReplayPlaybackFrame laterFrame = playback.Frames
                .First(frame => frame.StartSeconds >= sourceFrame.EndSeconds);

            Assert.That(
                warehousePatrol.EpisodeId,
                Is.EqualTo("terminal:depot-warehouse-patrol:7"));
            Assert.That(
                warehousePatrol.EnteredLifeState,
                Is.EqualTo(ActorLifeState.Dead));
            Assert.That(
                warehousePatrol.HitRegion,
                Is.EqualTo(TargetRegionId.Torso));
            Assert.That(
                warehousePatrol.PoseKind,
                Is.EqualTo(ReplayActorTerminalPoseKind.ShoulderFall));
            Assert.That(
                playback.SampleTerminalPose(
                    warehousePatrol.ActorId,
                    warehousePatrol.StartSeconds - 0.001f),
                Is.Null);
            ReplayActorTerminalPoseSample laterSample = playback
                .SampleTerminalPose(
                    warehousePatrol.ActorId,
                    laterFrame.StartSeconds);
            Assert.That(laterSample, Is.Not.Null);
            Assert.That(
                laterSample.EpisodeId,
                Is.EqualTo(warehousePatrol.EpisodeId));
            Assert.That(laterSample.NormalizedProgress, Is.GreaterThan(0f));

            ReplayCombatTranscriptEntry death = new ReplayCombatTranscript(
                    playback)
                .Entries.Single(entry =>
                    entry.EventKind == ReplayCombatTranscriptEventKind.Death
                    && entry.TargetId == warehousePatrol.ActorId);
            Assert.That(
                death.TimeSeconds,
                Is.EqualTo(warehousePatrol.StartSeconds).Within(0.0001f));

            ReplayActorTerminalPoseEpisode finalEpisode = playback
                .TerminalPoseEpisodes.Single(episode =>
                    episode.ActorId == "oren-vale"
                    && episode.SourceTransitionSequence == 50);
            Assert.That(
                finalEpisode.PoseKind,
                Is.EqualTo(ReplayActorTerminalPoseKind.FallOver));
            Assert.That(
                playback.TotalDurationSeconds,
                Is.EqualTo(finalEpisode.AnimationEndSeconds)
                    .Within(0.0001f));
            Assert.That(
                playback.TotalDurationSeconds,
                Is.GreaterThan(playback.SemanticDurationSeconds));
            Assert.That(
                playback.TurnGroups.Last().EndSeconds,
                Is.EqualTo(playback.TotalDurationSeconds).Within(0.0001f));
            Assert.That(
                playback.Locate(playback.TotalDurationSeconds).Frame,
                Is.SameAs(playback.Frames.Last().Frame));
            Assert.That(
                playback.SampleTerminalPose(
                    finalEpisode.ActorId,
                    playback.TotalDurationSeconds).NormalizedProgress,
                Is.EqualTo(1f));
        }

        [Test]
        public void EmbeddedFirstSimulationIsStrictAndExercisesIntendedSpace()
        {
            TextAsset asset = Resources.Load<TextAsset>(
                GameplayBattleReplayController.ArtifactResource);

            Assert.That(asset, Is.Not.Null);
            GameplayBattleArtifact artifact = GameplayBattleArtifactCodec.Read(
                asset.text);
            GameplayContentPackage content = GameplayContentLoader.LoadDefault();
            GameplayBattleScoreboard score = artifact.Content.Scoreboard;
            Assert.That(
                artifact.SchemaVersion,
                Is.EqualTo(GameplayBattleArtifact.CurrentSchemaVersion));
            Assert.That(
                artifact.Content.Terminal.Kind,
                Is.EqualTo(GameplayBattleTerminalKind.PartyVictory));
            Assert.That(score.Decisions, Is.GreaterThan(0));
            Assert.That(score.Transitions, Is.EqualTo(score.Decisions + 1));
            Assert.That(score.FireDeployments, Is.GreaterThan(0));
            Assert.That(score.ConcussiveTargets, Is.GreaterThan(0));
            Assert.That(score.DroneMoves, Is.GreaterThan(0));
            Assert.That(score.DroneAttacks, Is.GreaterThan(0));
            Assert.That(score.Reloads, Is.EqualTo(1));
            Assert.That(score.RoundsSpent, Is.EqualTo(14));
            Assert.That(score.RoundsReloaded, Is.EqualTo(6));
            Assert.That(score.Ammunition, Has.Count.EqualTo(2));
            Assert.That(score.Ammunition[0].AmmoTypeId,
                Is.EqualTo("ammo.rifle"));
            Assert.That(score.Ammunition[0].RoundsSpent, Is.EqualTo(14));
            Assert.That(score.Ammunition[1].AmmoTypeId,
                Is.EqualTo("ammo.rocket"));
            Assert.That(score.Ammunition[1].RoundsSpent, Is.Zero);
            Assert.That(
                content.SpatialContent.Identity.HasSameIdentity(
                    artifact.Content.ExecutionIdentity.Spatial),
                Is.True,
                "Unity-loaded spatial content must match the embedded sim.");
            Assert.That(
                artifact.ToPortableJson(),
                Is.EqualTo(asset.text));
        }
    }
}
