using GritGud.Domain.Gameplay;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class ProjectileFlightPresenterTests
    {
        [Test]
        public void DefaultCatalogResolvesTheAuthoredRocketModel()
        {
            ProjectilePresentationCatalog catalog =
                ProjectilePresentationCatalog.LoadDefault();

            ProjectilePresentationDefinition rocket = catalog.Get(
                "projectile.rocket.synty");

            Assert.That(rocket.Prefab, Is.Not.Null);
            Assert.That(
                rocket.Prefab.name,
                Is.EqualTo("SM_Wep_RPG_Rocket_Active_01"));
            Assert.That(rocket.SpinDegreesPerSecond, Is.EqualTo(420f));
            Assert.That(rocket.VisualRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(rocket.PlaybackAccelerationFraction, Is.EqualTo(0.28f));
            Assert.That(rocket.TrailEffectPrefab, Is.Not.Null);
            Assert.That(
                rocket.TrailEffectPrefab.name,
                Is.EqualTo("Smoke_Small_Light_FX"));
            Assert.That(rocket.TrailScale, Is.EqualTo(0.15f));
            Assert.That(rocket.EmitsTrailWhileHolding, Is.True);
            Assert.That(rocket.ImpactEffectPrefab, Is.Not.Null);
            Assert.That(
                rocket.ImpactEffectPrefab.name,
                Is.EqualTo("Explosion_Large_FX"));
            Assert.That(rocket.ImpactEffectSeconds, Is.EqualTo(1.1f));
            Assert.That(rocket.GhostEndpointHoldSeconds, Is.EqualTo(0.45f));
            Assert.That(rocket.EncounterPlaybackSeconds, Is.EqualTo(0.45f));
        }

        [Test]
        public void PresenterHoldsRecordedRocketAndPreviewsNextSegmentInOrange()
        {
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab.name = "Rocket Presenter Test Model";
            var definition = new ProjectilePresentationDefinition(
                "projectile.test",
                prefab,
                Vector3.zero,
                modelScale: 0.25f,
                spinSpeed: 360f);
            ProjectileFlightSnapshot initial = CreateInitialFlight();
            ProjectileFlightPresenter presenter = null;
            try
            {
                presenter = new ProjectileFlightPresenter(initial, definition);
                Quaternion initialSpin = presenter.SolidSpinPivot.localRotation;

                presenter.Tick(0.25f);

                Assert.That(presenter.SolidVisible, Is.True);
                Assert.That(
                    presenter.SolidPosition.z,
                    Is.EqualTo(0f).Within(0.001f));
                Assert.That(presenter.GhostVisible, Is.True);
                Assert.That(presenter.GhostPosition.z, Is.EqualTo(1f).Within(0.001f));
                Assert.That(
                    presenter.GhostMaterial.shader.name,
                    Is.EqualTo(MovementRouteGhostPresenter.GhostShaderName));
                Assert.That(
                    presenter.GhostMaterial.GetColor("_LineColor"),
                    Is.EqualTo(GameplayVisualPalette.ProjectileGhostLine));
                Assert.That(
                    presenter.SolidSpinPivot.localRotation,
                    Is.Not.EqualTo(initialSpin),
                    "The mid-flight rocket should keep spinning while the reaction cycle waits.");
            }
            finally
            {
                presenter?.Dispose();
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void RecordedAdvanceAcceleratesSolidRocketAndKeepsHoldingSmoke()
        {
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var definition = new ProjectilePresentationDefinition(
                "projectile.test",
                prefab,
                Vector3.zero,
                modelScale: 0.25f,
                spinSpeed: 360f);
            ProjectileFlightSnapshot initial = CreateInitialFlight();
            ProjectileFlightSnapshot resulting = new ProjectileFlightSnapshot(
                initial.Launch,
                initial.Launch.GetPosition(4f),
                distanceTraveled: 4f,
                elapsedTurnTime: 1f,
                status: ProjectileFlightStatus.InFlight);
            var advance = new ProjectileAdvanceRecord(
                sequence: 1,
                previous: initial,
                resulting: resulting,
                requestedTurnTime: 1f,
                segmentEnd: resulting.Position,
                worldStateRevision: 4,
                collisionFraction: null);
            ProjectileFlightPresenter presenter = null;
            try
            {
                presenter = new ProjectileFlightPresenter(initial, definition);
                Quaternion initialSpin = presenter.SolidSpinPivot.localRotation;
                Assert.That(presenter.IsAdvancePlaying, Is.False);

                presenter.PlayAdvance(advance, durationSeconds: 0.5f);
                Assert.That(presenter.IsAdvancePlaying, Is.True);
                Assert.That(presenter.GhostVisible, Is.False);

                presenter.Tick(0.25f);
                float halfwayProgress =
                    ProjectileFlightPresenter.CalculateAcceleratedProgress(
                        0.5f,
                        definition.PlaybackAccelerationFraction);
                Assert.That(
                    presenter.SolidPosition.z,
                    Is.EqualTo(Mathf.Lerp(0f, 4f, halfwayProgress)).Within(0.001f));
                Assert.That(
                    presenter.SolidSpinPivot.localRotation,
                    Is.Not.EqualTo(initialSpin));

                presenter.Tick(0.25f);
                Assert.That(presenter.SolidPosition.z, Is.EqualTo(4f).Within(0.001f));
                Assert.That(presenter.IsAdvancePlaying, Is.False);
                Assert.That(presenter.GhostVisible, Is.True);
            }
            finally
            {
                presenter?.Dispose();
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void BackToBackRecordedAdvancesPlayOnceInOrder()
        {
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var definition = new ProjectilePresentationDefinition(
                "projectile.test",
                prefab,
                Vector3.zero,
                modelScale: 0.25f,
                spinSpeed: 360f);
            ProjectileFlightSnapshot initial = CreateInitialFlight();
            ProjectileFlightSnapshot afterFirst = new ProjectileFlightSnapshot(
                initial.Launch,
                initial.Launch.GetPosition(4f),
                distanceTraveled: 4f,
                elapsedTurnTime: 1f,
                status: ProjectileFlightStatus.InFlight);
            ProjectileFlightSnapshot afterSecond = new ProjectileFlightSnapshot(
                initial.Launch,
                initial.Launch.GetPosition(8f),
                distanceTraveled: 8f,
                elapsedTurnTime: 2f,
                status: ProjectileFlightStatus.InFlight);
            var first = new ProjectileAdvanceRecord(
                sequence: 1,
                previous: initial,
                resulting: afterFirst,
                requestedTurnTime: 1f,
                segmentEnd: afterFirst.Position,
                worldStateRevision: 1,
                collisionFraction: null);
            var second = new ProjectileAdvanceRecord(
                sequence: 2,
                previous: afterFirst,
                resulting: afterSecond,
                requestedTurnTime: 1f,
                segmentEnd: afterSecond.Position,
                worldStateRevision: 2,
                collisionFraction: null);
            ProjectileFlightPresenter presenter = null;
            try
            {
                presenter = new ProjectileFlightPresenter(initial, definition);

                presenter.PlayAdvance(first, durationSeconds: 0.5f);
                presenter.PlayAdvance(second, durationSeconds: 0.5f);

                presenter.Tick(0.5f);
                Assert.That(presenter.SolidPosition.z, Is.EqualTo(4f).Within(0.001f));
                Assert.That(presenter.GhostVisible, Is.False);

                presenter.Tick(0.25f);
                float halfwayProgress =
                    ProjectileFlightPresenter.CalculateAcceleratedProgress(
                        0.5f,
                        definition.PlaybackAccelerationFraction);
                Assert.That(
                    presenter.SolidPosition.z,
                    Is.EqualTo(Mathf.Lerp(4f, 8f, halfwayProgress)).Within(0.001f));
                presenter.Tick(0.25f);
                Assert.That(presenter.SolidPosition.z, Is.EqualTo(8f).Within(0.001f));
                Assert.That(presenter.GhostVisible, Is.True);
            }
            finally
            {
                presenter?.Dispose();
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void SolidRocketLaunchesFromMuzzleBeforeJoiningRecordedFlight()
        {
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var definition = new ProjectilePresentationDefinition(
                "projectile.test",
                prefab,
                Vector3.zero,
                modelScale: 0.25f,
                spinSpeed: 360f);
            ProjectileFlightSnapshot initial = CreateInitialFlight();
            ProjectileFlightSnapshot resulting = new ProjectileFlightSnapshot(
                initial.Launch,
                initial.Launch.GetPosition(4f),
                distanceTraveled: 4f,
                elapsedTurnTime: 1f,
                status: ProjectileFlightStatus.InFlight);
            var advance = new ProjectileAdvanceRecord(
                sequence: 1,
                previous: initial,
                resulting: resulting,
                requestedTurnTime: 1f,
                segmentEnd: resulting.Position,
                worldStateRevision: 1,
                collisionFraction: null);
            Vector3 muzzle = new Vector3(0.45f, 1.35f, 0.6f);
            ProjectileFlightPresenter presenter = null;
            try
            {
                presenter = new ProjectileFlightPresenter(
                    initial,
                    definition,
                    visualLaunchOrigin: muzzle);

                Assert.That(presenter.SolidPosition, Is.EqualTo(muzzle));
                Assert.That(presenter.GhostPosition, Is.EqualTo(muzzle));

                presenter.PlayAdvance(advance, durationSeconds: 0.5f);
                presenter.Tick(0.25f);
                float halfwayProgress =
                    ProjectileFlightPresenter.CalculateAcceleratedProgress(
                        0.5f,
                        definition.PlaybackAccelerationFraction);
                Assert.That(
                    presenter.SolidPosition,
                    Is.EqualTo(Vector3.Lerp(
                        muzzle,
                        Vector3.forward * 4f,
                        halfwayProgress)));
                presenter.Tick(0.25f);
                Assert.That(
                    presenter.SolidPosition,
                    Is.EqualTo(Vector3.forward * 4f));
            }
            finally
            {
                presenter?.Dispose();
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void PlaybackAccelerationRampsQuicklyThenMaintainsTravelSpeed()
        {
            const float accelerationFraction = 0.25f;

            float early = ProjectileFlightPresenter.CalculateAcceleratedProgress(
                0.1f,
                accelerationFraction);
            float afterRamp = ProjectileFlightPresenter.CalculateAcceleratedProgress(
                0.5f,
                accelerationFraction);

            Assert.That(early, Is.LessThan(0.1f));
            Assert.That(afterRamp, Is.GreaterThan(early));
            Assert.That(
                ProjectileFlightPresenter.CalculateAcceleratedProgress(
                    1f,
                    accelerationFraction),
                Is.EqualTo(1f));
        }

        [Test]
        public void ReactionPredictionLimitsGhostToThePredictedEndpoint()
        {
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var definition = new ProjectilePresentationDefinition(
                "projectile.test",
                prefab,
                Vector3.zero,
                modelScale: 0.25f,
                spinSpeed: 360f);
            ProjectileFlightPresenter presenter = null;
            try
            {
                presenter = new ProjectileFlightPresenter(
                    CreateInitialFlight(),
                    definition);
                presenter.SetPreviewEndpoint(
                    new GameplayPosition(0f, 0f, 2f));

                presenter.Tick(0.5f);

                Assert.That(presenter.GhostVisible, Is.True);
                Assert.That(
                    presenter.GhostPosition,
                    Is.EqualTo(Vector3.forward * 2f));
            }
            finally
            {
                presenter?.Dispose();
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void AuthoredTrailPrefabControlsHoldingEmission()
        {
            GameObject projectilePrefab = GameObject.CreatePrimitive(
                PrimitiveType.Capsule);
            var trailPrefab = new GameObject("Authored Trail");
            trailPrefab.AddComponent<ParticleSystem>();
            var definition = new ProjectilePresentationDefinition(
                "projectile.test",
                projectilePrefab,
                Vector3.zero,
                modelScale: 0.25f,
                spinSpeed: 360f,
                trailPrefab: trailPrefab,
                trailVisualScale: 0.2f,
                trailWhileHolding: true);
            ProjectileFlightPresenter presenter = null;
            try
            {
                presenter = new ProjectileFlightPresenter(
                    CreateInitialFlight(),
                    definition);

                Assert.That(presenter.TrailEmitting, Is.True);
            }
            finally
            {
                presenter?.Dispose();
                Object.DestroyImmediate(projectilePrefab);
                Object.DestroyImmediate(trailPrefab);
            }
        }

        private static ProjectileFlightSnapshot CreateInitialFlight()
        {
            var flight = new ProjectileFlightDefinition(
                "projectile.test",
                speedPerTurn: 4f,
                radius: 0.1f,
                maximumRange: 12f);
            var launch = new ProjectileLaunchRecord(
                sequence: 1,
                projectileId: "projectile.1",
                attackerId: "player",
                intendedTargetId: "target",
                actionId: "attack.rocket",
                origin: new GameplayPosition(0f, 0f, 0f),
                aimPoint: new GameplayPosition(0f, 0f, 10f),
                definition: flight,
                turnActionPointAllowance: 4,
                remainingActionPointsAfterLaunch: 2);
            return new ProjectileFlightSnapshot(
                launch,
                launch.Origin,
                distanceTraveled: 0f,
                elapsedTurnTime: 0f,
                status: ProjectileFlightStatus.InFlight);
        }
    }
}
