using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Domain.Turns;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayThrownExplosiveControllerTests
    {
        [Test]
        public void AimFailuresUsePointerValidationLanguage()
        {
            Assert.That(
                GameplayThrownExplosiveController.FormatAimFailure(
                    ThrownExplosiveFailure.OutOfRange,
                    12f),
                Is.EqualTo("OUT OF RANGE  12 M MAX"));
            Assert.That(
                GameplayThrownExplosiveController.FormatAimFailure(
                    ThrownExplosiveFailure.InsufficientActionPoints,
                    12f),
                Is.EqualTo("THROW UNAVAILABLE - INSUFFICIENT AP"));
            Assert.That(
                GameplayThrownExplosiveController.FormatAimFailure(
                    ThrownExplosiveFailure.ActorPinned,
                    12f),
                Is.EqualTo("THROW UNAVAILABLE - ACTOR PINNED"));
        }

        [Test]
        public void CursorRayControlsDirectionWithoutRequiringSurfaceGeometry()
        {
            var pointerRay = new Ray(
                new Vector3(0f, 5f, -5f),
                new Vector3(1f, -1f, 1f));

            bool resolved = GameplayThrownExplosiveController
                .TryResolveAimDirection(
                    pointerRay,
                    Vector3.zero,
                    out Vector3 direction);

            Assert.That(resolved, Is.True);
            Assert.That(direction.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(direction.y, Is.Zero.Within(0.001f));
            Assert.That(direction.z, Is.Zero.Within(0.001f));
        }

        [Test]
        public void DistanceInputAdjustsAndClampsWithinThrowRange()
        {
            Assert.That(
                GameplayThrownExplosiveController.ResolveInitialAimDistance(10f),
                Is.EqualTo(5f));
            Assert.That(
                GameplayThrownExplosiveController.ApplyAimDistanceInput(
                    currentDistance: 5f,
                    input: 1f,
                    maximumRange: 10f,
                    deltaTime: 0.25f),
                Is.EqualTo(7f));
            Assert.That(
                GameplayThrownExplosiveController.ApplyAimDistanceInput(
                    currentDistance: 1f,
                    input: -1f,
                    maximumRange: 10f,
                    deltaTime: 1f),
                Is.EqualTo(1f));
            Assert.That(
                GameplayThrownExplosiveController.ApplyAimDistanceInput(
                    currentDistance: 9f,
                    input: 1f,
                    maximumRange: 10f,
                    deltaTime: 1f),
                Is.EqualTo(10f));
        }

        [Test]
        public void DefaultCatalogAuthorsTheProductionGrenadeVisuals()
        {
            ConsumablePresentationCatalog catalog =
                ConsumablePresentationCatalog.LoadDefault();

            ThrownExplosivePresentationDefinition presentation =
                catalog.GetThrownExplosive("item.frag-grenade");

            Assert.That(presentation.ProjectilePrefab, Is.Not.Null);
            Assert.That(presentation.ImpactEffectPrefab, Is.Not.Null);
            Assert.That(presentation.FlightSeconds, Is.EqualTo(0.55f));
            Assert.That(presentation.ReleaseDelaySeconds, Is.EqualTo(0.45f));
            Assert.That(
                presentation.ImpactDelaySeconds,
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(presentation.ImpactEffectSeconds, Is.EqualTo(0.65f));
            Assert.That(
                presentation.FlightSeconds,
                Is.EqualTo(GameplayThrownExplosivePresentationTiming
                    .FlightSeconds));
            Assert.That(
                presentation.ReleaseDelaySeconds,
                Is.EqualTo(GameplayThrownExplosivePresentationTiming
                    .ReleaseSeconds));
            Assert.That(
                presentation.ImpactEffectSeconds,
                Is.EqualTo(GameplayThrownExplosivePresentationTiming
                    .DefaultImpactEffectSeconds));
        }

        [Test]
        public void ConsumableDispatcherRoutesAnyRegisteredPowerType()
        {
            var power = new TestConsumablePowerDefinition(
                "item.medkit",
                new ActionCost(1, 0f, ActionMobility.Mobile));
            GameplaySession session = CreateSession(power);
            var handler = new TestConsumablePowerHandler(power);
            var controller = new GameplayConsumableController(session, handler);

            Assert.That(controller.TryToggle("player", power.Id), Is.True);
            Assert.That(controller.IsPending, Is.True);
            Assert.That(controller.PendingItemId, Is.EqualTo(power.Id));
            Assert.That(handler.ToggleCount, Is.EqualTo(1));
            Assert.That(controller.TryConfirmPending(), Is.True);
            Assert.That(controller.IsPending, Is.False);
            Assert.That(controller.PendingItemId, Is.Null);

            Assert.That(controller.TryToggle("player", power.Id), Is.True);
            Assert.That(controller.CancelPending(), Is.True);
            Assert.That(controller.IsPending, Is.False);
            Assert.That(handler.CancelCount, Is.EqualTo(1));
        }

        [Test]
        public void ExplorationThrowAnimatesWithoutBeginningEncounter()
        {
            var host = new GameObject("Thrown Explosive Controller Test");
            var worldRoot = new GameObject("Thrown Explosive Test World");
            var grenadeVisual = new GameObject("Grenade Visual Test");
            var impactVisual = new GameObject("Grenade Impact Test");
            ConsumablePresentationCatalog presentation =
                ConsumablePresentationCatalog.CreateRuntime(
                    CreatePresentation(
                        "item.grenade",
                        grenadeVisual,
                        impactVisual));
            var world = new LevelWorld(
                worldRoot,
                new Dictionary<string, LevelEntityView>(),
                null);
            var registry = new GameplayWorldRegistry(world);
            GameObject actorRoot = UnityEngine.Object.Instantiate(
                Resources.Load<GameObject>("Actors/DefaultPlayerActor"));
            try
            {
                GameplaySession session = CreateSession();
                registry.RegisterActor(
                    "player",
                    "test",
                    targetable: false,
                    actorRoot);
                TargetAcquisitionPresenter acquisition =
                    host.AddComponent<TargetAcquisitionPresenter>();
                acquisition.Bind(session, registry, "player");
                GameplayThrownExplosiveController controller =
                    host.AddComponent<GameplayThrownExplosiveController>();
                GameplayInputController input =
                    host.AddComponent<GameplayInputController>();
                var destructibles = new DestructiblePropSession(
                    Array.Empty<DestructiblePropDefinition>());
                int encounterStartRequests = 0;
                controller.Bind(
                    session,
                    registry,
                    new UnityBlastWorldQuery(
                        registry,
                        () => session.Journal.LastEntry?.Sequence ?? 0L,
                        _ => false),
                    new GameplayBlastConsequenceResolver(
                        session,
                        destructibles),
                    acquisition,
                    new GameplayDialogueLog(),
                    "player",
                    randomSeed: 17u,
                    onEncounterStartRequested: action =>
                    {
                        encounterStartRequests++;
                        return session.BeginEncounterFromAction(action);
                    },
                    presentation: presentation,
                    inputController: input);
                int journalEntryCount = session.Journal.Entries.Count;

                Assert.That(controller.TryToggleAim("item.grenade"), Is.True);
                Assert.That(controller.IsAiming, Is.True);
                Assert.That(input.IsMovementCaptured, Is.True);
                Assert.That(encounterStartRequests, Is.Zero);
                Assert.That(session.Mode,
                    Is.EqualTo(GameplaySessionMode.Exploration));
                Assert.That(session.EncounterActive, Is.False);
                Assert.That(session.Journal.Entries.Count,
                    Is.EqualTo(journalEntryCount));
                Assert.That(
                    session.GetInventoryQuantity("player", "item.grenade"),
                    Is.EqualTo(3));
                Transform armedProjectile = FindDescendant(
                    actorRoot.transform,
                    "Armed Thrown Explosive");
                Assert.That(armedProjectile, Is.Not.Null);
                foreach (Collider collider in
                    armedProjectile.GetComponentsInChildren<Collider>(true))
                {
                    Assert.That(collider.enabled, Is.False);
                }

                Assert.That(controller.TryToggleAim("item.grenade"), Is.True);
                Assert.That(controller.IsAiming, Is.False);
                Assert.That(input.IsMovementCaptured, Is.False);
                Assert.That(
                    FindDescendant(actorRoot.transform, "Armed Thrown Explosive"),
                    Is.Null);
                Assert.That(controller.StatusMessage, Is.EqualTo("Throw canceled."));
                Assert.That(encounterStartRequests, Is.Zero);
                Assert.That(session.Mode,
                    Is.EqualTo(GameplaySessionMode.Exploration));
                Assert.That(session.Journal.Entries.Count,
                    Is.EqualTo(journalEntryCount));
                Assert.That(
                    session.GetInventoryQuantity("player", "item.grenade"),
                    Is.EqualTo(3));

                Assert.That(controller.TryToggleAim("item.grenade"), Is.True);
                Assert.That(controller.CancelAim(), Is.True);
                Assert.That(controller.IsAiming, Is.False);
                Assert.That(input.IsMovementCaptured, Is.False);
                Assert.That(encounterStartRequests, Is.Zero);
                Assert.That(session.Mode,
                    Is.EqualTo(GameplaySessionMode.Exploration));
                Assert.That(session.Journal.Entries.Count,
                    Is.EqualTo(journalEntryCount));
                Assert.That(
                    session.GetInventoryQuantity("player", "item.grenade"),
                    Is.EqualTo(3));

                Assert.That(
                    controller.TryToggleAim("item.grenade"),
                    Is.True);
                Assert.That(
                    controller.TryConfirmThrow(
                        new GameplayPosition(5f, 0f, 0f)),
                    Is.True);
                Assert.That(controller.IsAiming, Is.False);
                Assert.That(input.IsMovementCaptured, Is.False);
                Assert.That(
                    controller.LastThrow.IntendedLanding.X,
                    Is.EqualTo(5f).Within(0.001f));
                Assert.That(
                    controller.LastThrow.IntendedLanding.Z,
                    Is.Zero.Within(0.001f));
                Assert.That(
                    session.GetActor("player").Pose.FacingDegrees,
                    Is.EqualTo(90f).Within(0.001f),
                    "The committed action owns the authoritative facing.");
                Assert.That(
                    actorRoot.transform.eulerAngles.y,
                    Is.Zero.Within(0.001f),
                    "Presentation must begin at the visual facing instead of "
                    + "snapping to the committed result.");
                Assert.That(
                    FindDescendant(
                        actorRoot.transform,
                        "Armed Thrown Explosive"),
                    Is.Not.Null,
                    "The held visual remains attached throughout wind-up.");
                Assert.That(encounterStartRequests, Is.Zero);
                Assert.That(session.EncounterActive, Is.False);
                Assert.That(session.Mode,
                    Is.EqualTo(GameplaySessionMode.Exploration));
                Assert.That(
                    session.LastResolvedAction.Cost.ActionPoints,
                    Is.Zero);
                Assert.That(
                    session.GetInventoryQuantity("player", "item.grenade"),
                    Is.EqualTo(2));
                ActorAnimationCoordinator animation =
                    actorRoot.GetComponent<ActorAnimationCoordinator>();
                Assert.That(
                    animation.LastRequestedAction,
                    Is.EqualTo(ActorAnimationAction.Throw));
                Assert.That(animation.ActionSequence, Is.EqualTo(1));
            }
            finally
            {
                registry.Dispose();
                world.Dispose();
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(grenadeVisual);
                UnityEngine.Object.DestroyImmediate(impactVisual);
                UnityEngine.Object.DestroyImmediate(presentation);
                UnityEngine.Object.DestroyImmediate(actorRoot);
            }
        }

        [Test]
        public void ThrowArcUsesAuthoredPresentationHeight()
        {
            var grenadeVisual = new GameObject("Grenade Arc Visual Test");
            var impactVisual = new GameObject("Grenade Arc Impact Test");
            try
            {
                ThrownExplosivePresentationDefinition presentation =
                    CreatePresentation(
                        "item.grenade",
                        grenadeVisual,
                        impactVisual,
                        arcHeightPerMeter: 0.5f,
                        minimumArcHeight: 0.1f,
                        maximumArcHeight: 10f);

                Vector3 midpoint =
                    GameplayThrownExplosiveController.EvaluateThrowPosition(
                        Vector3.zero,
                        new Vector3(4f, 0f, 0f),
                        0.5f,
                        presentation);
                Vector3 release =
                    GameplayThrownExplosiveController.EvaluateThrowPosition(
                        new Vector3(1f, 1.2f, 2f),
                        new Vector3(4f, 0f, 0f),
                        0f,
                        presentation);
                Vector3 landing =
                    GameplayThrownExplosiveController.EvaluateThrowPosition(
                        new Vector3(1f, 1.2f, 2f),
                        new Vector3(4f, 0f, 0f),
                        1f,
                        presentation);

                Assert.That(midpoint.x, Is.EqualTo(2f));
                Assert.That(midpoint.y, Is.EqualTo(2f));
                Assert.That(release,
                    Is.EqualTo(new Vector3(1f, 1.2f, 2f)));
                Assert.That(landing,
                    Is.EqualTo(new Vector3(4f, 0f, 0f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(grenadeVisual);
                UnityEngine.Object.DestroyImmediate(impactVisual);
            }
        }

        [Test]
        public void LandingQueryProjectsAtDestinationNotAlongFlightChord()
        {
            var nearBlocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var destinationSurface = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            try
            {
                nearBlocker.name = "Near Flight Chord Blocker";
                nearBlocker.transform.position = new Vector3(1f, 1f, 0f);
                nearBlocker.transform.localScale = new Vector3(1f, 3f, 1f);
                destinationSurface.name = "Destination Surface";
                destinationSurface.transform.position =
                    new Vector3(10f, -0.5f, 0f);
                destinationSurface.transform.localScale =
                    new Vector3(2f, 1f, 2f);
                Physics.SyncTransforms();
                var query = new UnityThrownExplosiveLandingQuery(
                    () => 42L,
                    ~0);

                ThrownExplosiveLandingResult result = query.Resolve(
                    new GameplayPosition(0f, 1.2f, 0f),
                    new GameplayPosition(10f, 0f, 0f));

                Assert.That(result.LandingPosition.X,
                    Is.EqualTo(10f).Within(0.001f));
                Assert.That(result.LandingPosition.Y,
                    Is.Zero.Within(0.001f));
                Assert.That(result.LandingPosition.Z,
                    Is.Zero.Within(0.001f));
                Assert.That(result.WorldStateRevision, Is.EqualTo(42L));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(nearBlocker);
                UnityEngine.Object.DestroyImmediate(destinationSurface);
            }
        }

        [Test]
        public void LiveAndReplayFlightShareRecordedReleaseAndLanding()
        {
            var definition = new ThrownExplosiveDefinition(
                "item.grenade",
                new ActionCost(2, 0f, ActionMobility.Mobile),
                maximumRange: 10f,
                standingLaunchHeight: 1.2f,
                crouchedLaunchHeight: 0.82f,
                baseUncertaintyRadius: 0.5f,
                uncertaintyPerMeter: 0.1f,
                blastRadius: 5f,
                blastWoundMovementPenalty: 1f);
            var record = new ThrownExplosiveRecord(
                1,
                "player",
                definition,
                new GameplayPosition(0f, 0f, 0f),
                new GameplayPosition(0f, 1.2f, 0f),
                new GameplayPosition(10f, 0f, 0f),
                new GameplayPosition(9.5f, 0f, 0f),
                new GameplayPosition(9.5f, 0f, 0f),
                1.5f,
                42,
                Array.Empty<BlastEffectRecord>(),
                requestedLanding: new GameplayPosition(18f, 0f, 0f));

            GameplayThrownExplosiveController.GetVisualFlightEndpoints(
                record,
                out Vector3 liveRelease,
                out Vector3 liveLanding);
            GameplayThrownExplosiveController.GetVisualFlightEndpoints(
                record,
                out Vector3 replayRelease,
                out Vector3 replayLanding);

            Assert.That(liveRelease, Is.EqualTo(replayRelease));
            Assert.That(liveLanding, Is.EqualTo(replayLanding));
            Assert.That(liveRelease,
                Is.EqualTo(new Vector3(0f, 1.2f, 0f)));
            Assert.That(liveLanding,
                Is.EqualTo(new Vector3(9.5f, 0f, 0f)));
        }

        [Test]
        public void BlastQueryIncludesOnlyAuthoredDestructibleProps()
        {
            var worldRoot = new GameObject("Responsive Blast Query World");
            var responsiveRoot = new GameObject("Responsive Blast Target");
            responsiveRoot.transform.SetParent(worldRoot.transform, false);
            LevelEntityView responsive = responsiveRoot.AddComponent<
                LevelEntityView>();
            responsive.Initialize(
                new LevelEntity
                {
                    id = "alarm-panel",
                    archetypeId = "test",
                    transform = new LevelTransformData(
                        new Float3Data(2f, 0f, 0f),
                        0f),
                },
                new LevelArchetypeDefinition());
            var inertRoot = new GameObject("Inert Blast Target");
            inertRoot.transform.SetParent(worldRoot.transform, false);
            LevelEntityView inert = inertRoot.AddComponent<LevelEntityView>();
            inert.Initialize(
                new LevelEntity
                {
                    id = "inert-crate",
                    archetypeId = "test",
                    transform = new LevelTransformData(
                        new Float3Data(3f, 0f, 0f),
                        0f),
                },
                new LevelArchetypeDefinition());
            var entities = new Dictionary<string, LevelEntityView>
            {
                { responsive.EntityId, responsive },
                { inert.EntityId, inert },
            };
            var world = new LevelWorld(worldRoot, entities, null);
            var registry = new GameplayWorldRegistry(world);
            try
            {
                var query = new UnityBlastWorldQuery(
                    registry,
                    () => 7L,
                    entityId => entityId == "alarm-panel");

                BlastWorldQueryResult result = query.Query(
                    new BlastWorldQuery(
                        new GameplayPosition(2f, 0f, 0f),
                        5f));

                Assert.That(result.Effects.Count, Is.EqualTo(1));
                Assert.That(result.Effects[0].EntityId,
                    Is.EqualTo("alarm-panel"));
                Assert.That(result.Effects[0].SubjectKind,
                    Is.EqualTo(BlastSubjectKind.DestructibleProp));
                Assert.That(result.Effects[0].Exposure, Is.EqualTo(1f));
            }
            finally
            {
                registry.Dispose();
                world.Dispose();
            }
        }

        private static GameplaySession CreateSession()
        {
            var grenade = new ThrownExplosiveDefinition(
                "item.grenade",
                new ActionCost(2, 0f, ActionMobility.Mobile),
                maximumRange: 10f,
                standingLaunchHeight: 1.2f,
                crouchedLaunchHeight: 0.82f,
                baseUncertaintyRadius: 1f,
                uncertaintyPerMeter: 0.1f,
                blastRadius: 5f,
                blastWoundMovementPenalty: 2f);
            return CreateSession(grenade);
        }

        private static GameplaySession CreateSession(
            ConsumablePowerDefinition power)
        {
            var player = new ScenarioActorDefinition(
                "player",
                initiative: 10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f),
                new[]
                {
                    new InventoryItemDefinition(
                        power.Id,
                        "Consumable",
                        3,
                        InventoryItemKind.Consumable,
                        new ActionCost(0, 0f, ActionMobility.Mobile),
                        EquipmentEffectSet.None,
                        consumablePower: power,
                        initialQuantity: 3),
                },
                initiallyEquippedItemId: null);
            var target = new ScenarioActorDefinition(
                "target",
                initiative: 0,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 4f),
                    180f),
                new TurnBudget(0, 0f));
            return new GameplaySession(new ScenarioDefinition(
                "thrown-controller-test",
                new ScenarioTimingDefinition(1.25f),
                new[] { player, target },
                Array.Empty<ScenarioObjectiveDefinition>()));
        }

        private static ThrownExplosivePresentationDefinition CreatePresentation(
            string itemId,
            GameObject grenadeVisual,
            GameObject impactVisual,
            float arcHeightPerMeter = 0.2f,
            float minimumArcHeight = 0.8f,
            float maximumArcHeight = 3f) =>
            new ThrownExplosivePresentationDefinition(
                itemId,
                grenadeVisual,
                Vector3.zero,
                1f,
                0.55f,
                0.45f,
                new Vector3(310f, 190f, 240f),
                arcHeightPerMeter,
                minimumArcHeight,
                maximumArcHeight,
                0.035f,
                0.035f,
                0.018f,
                Color.yellow,
                Color.red,
                impactVisual,
                Vector3.zero,
                0.2f,
                0.65f);

        private static Transform FindDescendant(Transform root, string name)
        {
            foreach (Transform descendant in
                root.GetComponentsInChildren<Transform>(true))
            {
                if (descendant.name == name)
                {
                    return descendant;
                }
            }

            return null;
        }

        private sealed class TestConsumablePowerDefinition
            : ConsumablePowerDefinition
        {
            public TestConsumablePowerDefinition(string id, ActionCost turnCost)
                : base(id, turnCost)
            {
            }

            public override string PowerTypeId => "test-consumable";
        }

        private sealed class TestConsumablePowerHandler
            : IGameplayConsumablePowerHandler
        {
            private readonly ConsumablePowerDefinition supportedPower;

            public TestConsumablePowerHandler(
                ConsumablePowerDefinition power)
            {
                supportedPower = power;
            }

            public string PendingItemId { get; private set; }

            public bool IsPending => PendingItemId != null;

            public int ToggleCount { get; private set; }

            public int CancelCount { get; private set; }

            public bool CanHandle(ConsumablePowerDefinition power) =>
                ReferenceEquals(power, supportedPower);

            public bool TryToggle(string itemId)
            {
                ToggleCount++;
                PendingItemId = IsPending ? null : itemId;
                return true;
            }

            public bool TryConfirm()
            {
                if (!IsPending)
                {
                    return false;
                }

                PendingItemId = null;
                return true;
            }

            public bool Cancel()
            {
                if (!IsPending)
                {
                    return false;
                }

                CancelCount++;
                PendingItemId = null;
                return true;
            }
        }
    }
}
