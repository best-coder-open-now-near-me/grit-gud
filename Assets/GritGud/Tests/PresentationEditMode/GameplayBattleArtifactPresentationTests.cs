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
            ReplayActorTerminalPoseEpisode persistedEpisode = playback
                .TerminalPoseEpisodes
                .OrderBy(episode => episode.StartSeconds)
                .First();
            GameplaySemanticReplayPlaybackFrame sourceFrame = playback.Frames
                .Single(frame =>
                    frame.Frame.Transition.Identity.Sequence ==
                        persistedEpisode.SourceTransitionSequence);
            GameplaySemanticReplayPlaybackFrame laterFrame = playback.Frames
                .First(frame => frame.StartSeconds >= sourceFrame.EndSeconds);

            Assert.That(
                persistedEpisode.EpisodeId,
                Is.EqualTo("terminal:" + persistedEpisode.ActorId + ":"
                    + persistedEpisode.SourceTransitionSequence));
            Assert.That(
                persistedEpisode.EnteredLifeState,
                Is.EqualTo(ActorLifeState.Incapacitated)
                    .Or.EqualTo(ActorLifeState.Dead));
            Assert.That(persistedEpisode.HitRegion, Is.Not.Null);
            Assert.That(
                playback.SampleTerminalPose(
                    persistedEpisode.ActorId,
                    persistedEpisode.StartSeconds - 0.001f),
                Is.Null);
            ReplayActorTerminalPoseSample laterSample = playback
                .SampleTerminalPose(
                    persistedEpisode.ActorId,
                    laterFrame.StartSeconds);
            Assert.That(laterSample, Is.Not.Null);
            Assert.That(
                laterSample.EpisodeId,
                Is.EqualTo(persistedEpisode.EpisodeId));
            Assert.That(laterSample.NormalizedProgress, Is.GreaterThan(0f));

            ReplayCombatTranscriptEventKind terminalEventKind =
                persistedEpisode.EnteredLifeState == ActorLifeState.Dead
                    ? ReplayCombatTranscriptEventKind.Death
                    : ReplayCombatTranscriptEventKind.Incapacitation;
            ReplayCombatTranscriptEntry terminalEntry = new ReplayCombatTranscript(
                    playback)
                .Entries.Single(entry =>
                    entry.EventKind == terminalEventKind
                    && entry.TargetId == persistedEpisode.ActorId
                    && entry.TransitionSequence ==
                        persistedEpisode.SourceTransitionSequence);
            Assert.That(
                terminalEntry.TimeSeconds,
                Is.EqualTo(persistedEpisode.StartSeconds).Within(0.0001f));

            ReplayActorTerminalPoseEpisode finalEpisode = playback
                .TerminalPoseEpisodes
                .OrderBy(episode => episode.AnimationEndSeconds)
                .Last();
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
            Assert.That(score.DroneSummons, Is.EqualTo(1));
            Assert.That(score.DroneDismissals, Is.Zero);
            Assert.That(score.DroneExpirations, Is.Zero);
            Assert.That(score.DroneCrashes, Is.Zero);
            Assert.That(score.Reloads, Is.Zero);
            Assert.That(score.RoundsSpent, Is.EqualTo(11));
            Assert.That(score.RoundsReloaded, Is.Zero);
            Assert.That(score.Ammunition, Has.Count.EqualTo(2));
            Assert.That(score.Ammunition[0].AmmoTypeId,
                Is.EqualTo("ammo.rifle"));
            Assert.That(score.Ammunition[0].RoundsSpent, Is.EqualTo(11));
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
