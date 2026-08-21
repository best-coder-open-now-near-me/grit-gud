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
            Assert.That(score.RoundsSpent, Is.EqualTo(12));
            Assert.That(score.RoundsReloaded, Is.EqualTo(6));
            Assert.That(score.Ammunition, Has.Count.EqualTo(2));
            Assert.That(score.Ammunition[0].AmmoTypeId,
                Is.EqualTo("ammo.rifle"));
            Assert.That(score.Ammunition[0].RoundsSpent, Is.EqualTo(12));
            Assert.That(score.Ammunition[1].AmmoTypeId,
                Is.EqualTo("ammo.rocket"));
            Assert.That(score.Ammunition[1].RoundsSpent, Is.Zero);
            Assert.That(
                artifact.ToPortableJson(),
                Is.EqualTo(asset.text));
        }
    }
}
