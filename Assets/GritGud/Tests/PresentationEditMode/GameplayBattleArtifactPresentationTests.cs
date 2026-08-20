using GritGud.Application.Gameplay;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayBattleArtifactPresentationTests
    {
        [Test]
        public void EmbeddedFirstSimulationIsStrictAndExercisesIntendedSpace()
        {
            TextAsset asset = Resources.Load<TextAsset>(
                GameplayBattleReplayController.ArtifactResource);

            Assert.That(asset, Is.Not.Null);
            GameplayBattleArtifact artifact = GameplayBattleArtifactCodec.Read(
                asset.text);
            GameplayBattleScoreboard score = artifact.Content.Scoreboard;
            Assert.That(
                artifact.Content.Terminal.Kind,
                Is.EqualTo(GameplayBattleTerminalKind.PartyVictory));
            Assert.That(score.Decisions, Is.GreaterThan(0));
            Assert.That(score.Transitions, Is.EqualTo(score.Decisions + 1));
            Assert.That(score.FireDeployments, Is.GreaterThan(0));
            Assert.That(score.ConcussiveTargets, Is.GreaterThan(0));
            Assert.That(score.DroneMoves, Is.GreaterThan(0));
            Assert.That(score.DroneAttacks, Is.GreaterThan(0));
            Assert.That(
                artifact.ToPortableJson(),
                Is.EqualTo(asset.text));
        }
    }
}
