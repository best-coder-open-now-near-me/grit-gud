using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class VehicleMomentumEnvelopePresenterTests
    {
        [Test]
        public void PresenterBuildsEnvelopeLinesHiddenUntilRequested()
        {
            var vehicle = new GameObject("Vehicle");
            try
            {
                var session = new VehicleMomentumSession(
                    new VehicleMomentumProfile(
                        10f,
                        4f,
                        2f,
                        75f,
                        25f,
                        0.6f,
                        0.16f),
                    new VehicleMomentumState(
                        "vehicle",
                        new GameplayPosition(0f, 0f, 0f),
                        0f,
                        4f));
                var presenter = vehicle.AddComponent<VehicleMomentumEnvelopePresenter>();

                presenter.Bind(session);

                Assert.That(presenter.IsBound, Is.True);
                Assert.That(presenter.OuterBoundaryPointCount, Is.EqualTo(21));
                LineRenderer[] lines =
                    vehicle.GetComponentsInChildren<LineRenderer>(true);
                Assert.That(lines.Length, Is.EqualTo(3));
                Assert.That(presenter.PresentationEnabled, Is.False);
                foreach (LineRenderer line in lines)
                {
                    Assert.That(line.enabled, Is.False);
                }

                presenter.SetPresentationEnabled(true);

                Assert.That(presenter.PresentationEnabled, Is.True);
                foreach (LineRenderer line in lines)
                {
                    Assert.That(line.enabled, Is.True);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(vehicle);
            }
        }

        [Test]
        public void EnvelopeRequiresTurnModeAndTheOccupantsActiveTurn()
        {
            GameplaySession gameplay = CreateGameplaySession();

            Assert.That(
                GameplayVehicleController.ShouldShowMomentumEnvelope(
                    gameplay,
                    "player"),
                Is.False,
                "Exploration never presents vehicle momentum.");

            gameplay.EnterTurnMode();

            Assert.That(
                GameplayVehicleController.ShouldShowMomentumEnvelope(
                    gameplay,
                    occupantActorId: null),
                Is.False,
                "An unoccupied vehicle has no acting driver.");
            Assert.That(
                GameplayVehicleController.ShouldShowMomentumEnvelope(
                    gameplay,
                    "target"),
                Is.False,
                "A vehicle driven by an inactive actor stays hidden.");
            Assert.That(
                GameplayVehicleController.ShouldShowMomentumEnvelope(
                    gameplay,
                    "player"),
                Is.True,
                "The active driver's vehicle presents its envelope.");
        }

        [Test]
        public void ReplayTransformUsesSampleWithoutChangingMomentumState()
        {
            var vehicle = new GameObject("Replay Vehicle");
            var authoritative = new VehicleMomentumState(
                "vehicle",
                new GameplayPosition(1f, 0f, 2f),
                15f,
                3f);
            var sampled = new VehicleMomentumState(
                "vehicle",
                new GameplayPosition(5f, 0f, 7f),
                90f,
                6f);
            try
            {
                GameplayVehicleController.ApplyReplayTransform(vehicle, sampled);

                Assert.That(vehicle.transform.position,
                    Is.EqualTo(new Vector3(5f, 0f, 7f)));
                Assert.That(vehicle.transform.eulerAngles.y,
                    Is.EqualTo(90f).Within(0.001f));
                Assert.That(authoritative.Position,
                    Is.EqualTo(new GameplayPosition(1f, 0f, 2f)));
                Assert.That(authoritative.Speed, Is.EqualTo(3f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(vehicle);
            }
        }

        private static GameplaySession CreateGameplaySession()
        {
            var player = new ScenarioActorDefinition(
                "player",
                initiative: 10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f));
            var target = new ScenarioActorDefinition(
                "target",
                initiative: 5,
                new GameplayActorPose(
                    new GameplayPosition(2f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f));
            return new GameplaySession(new ScenarioDefinition(
                "vehicle-envelope-test",
                new ScenarioTimingDefinition(1.25f),
                new[] { player, target },
                Array.Empty<ScenarioObjectiveDefinition>()));
        }
    }
}
