using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Domain.Turns;
using GritGud.Presentation.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using Object = UnityEngine.Object;

namespace GritGud.Presentation.Tests
{
    public sealed class TargetAcquisitionPresenterTests
    {
        [Test]
        public void TargetingCursorTracksSharedValidationState()
        {
            var host = new GameObject("Targeting Cursor Validation Test");
            try
            {
                bool visible = true;
                bool? valid = false;
                GameplayTargetingCursorPresenter cursor =
                    host.AddComponent<GameplayTargetingCursorPresenter>();
                cursor.Bind(() => visible, () => valid);

                Assert.That(cursor.IsTargetingVisible, Is.True);
                Assert.That(cursor.IsTargetingValid, Is.False);

                valid = true;
                cursor.RefreshNow();

                Assert.That(cursor.IsTargetingVisible, Is.True);
                Assert.That(cursor.IsTargetingValid, Is.True);

                visible = false;
                cursor.RefreshNow();

                Assert.That(cursor.IsTargetingVisible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CharacterSurfaceAimIgnoresCameraSideWallButKeepsForwardWall()
        {
            var host = new GameObject("Character Surface Aim Test");
            var observer = CreateActorObject(
                "Surface Aim Observer",
                Vector3.zero,
                withVisual: false);
            var cameraWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var forwardSurface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            LevelWorld world = null;
            GameplayWorldRegistry registry = null;
            try
            {
                cameraWall.name = "Camera-Side Obstruction";
                cameraWall.transform.position = new Vector3(0f, 1.2f, -2f);
                cameraWall.transform.localScale = new Vector3(4f, 4f, 0.2f);
                forwardSurface.name = "Character-Side Aim Surface";
                forwardSurface.transform.position = new Vector3(0f, 1.2f, 6f);
                forwardSurface.transform.localScale = new Vector3(4f, 4f, 0.2f);

                world = new LevelWorld(
                    new GameObject("Character Surface Aim World"),
                    new Dictionary<string, LevelEntityView>(),
                    null);
                registry = new GameplayWorldRegistry(world);
                registry.RegisterActor(
                    "observer",
                    "test",
                    targetable: false,
                    observer);
                TargetAcquisitionPresenter presenter =
                    host.AddComponent<TargetAcquisitionPresenter>();
                presenter.Bind(CreateSession(), registry, "observer");
                var crosshairRay = new Ray(
                    new Vector3(0f, 1.2f, -4f),
                    Vector3.forward);
                Physics.SyncTransforms();
                presenter.RefreshNow(crosshairRay);

                Assert.That(
                    presenter.TryGetPointerSurfacePoint(
                        new Vector3(0f, 1.2f, 0f),
                        12f,
                        out Vector3 aimPoint),
                    Is.True);
                Assert.That(aimPoint.z, Is.EqualTo(5.9f).Within(0.02f));

                cameraWall.transform.position = new Vector3(0f, 1.2f, 2f);
                Physics.SyncTransforms();
                presenter.RefreshNow(crosshairRay);

                Assert.That(
                    presenter.TryGetPointerSurfacePoint(
                        new Vector3(0f, 1.2f, 0f),
                        12f,
                        out aimPoint),
                    Is.True);
                Assert.That(aimPoint.z, Is.EqualTo(1.9f).Within(0.02f));
            }
            finally
            {
                Object.DestroyImmediate(host);
                registry?.Dispose();
                world?.Dispose();
                Object.DestroyImmediate(observer);
                Object.DestroyImmediate(cameraWall);
                Object.DestroyImmediate(forwardSurface);
            }
        }

        [Test]
        public void WeaponAimRetainsConfiguredLevelEntityIdentifier()
        {
            var host = new GameObject("Stable Weapon Aim Test");
            var observer = CreateActorObject(
                "Stable Aim Observer",
                Vector3.zero,
                withVisual: false);
            var worldRoot = new GameObject("Stable Weapon Aim World");
            var entityRoot = new GameObject("Alarm Panel Root");
            var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            LevelWorld world = null;
            GameplayWorldRegistry registry = null;
            try
            {
                entityRoot.transform.SetParent(worldRoot.transform, false);
                LevelEntityView entity =
                    entityRoot.AddComponent<LevelEntityView>();
                entity.Initialize(
                    new LevelEntity
                    {
                        id = "alarm-panel",
                        archetypeId = "prop.alarm-panel",
                        transform = new LevelTransformData(
                            new Float3Data(0f, 0f, 5f),
                            0f),
                    },
                    new LevelArchetypeDefinition());
                surface.transform.SetParent(entityRoot.transform, false);
                surface.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                surface.transform.localScale = new Vector3(2f, 2f, 0.2f);

                world = new LevelWorld(
                    worldRoot,
                    new Dictionary<string, LevelEntityView>
                    {
                        ["alarm-panel"] = entity,
                    },
                    null);
                registry = new GameplayWorldRegistry(world);
                registry.RegisterActor(
                    "observer",
                    "test",
                    targetable: false,
                    observer);
                TargetAcquisitionPresenter presenter =
                    host.AddComponent<TargetAcquisitionPresenter>();
                presenter.Bind(CreateSession(), registry, "observer");
                Physics.SyncTransforms();
                presenter.RefreshNow(new Ray(
                    new Vector3(0f, 1.2f, 0f),
                    Vector3.forward));

                Assert.That(
                    presenter.TryGetWeaponAim(out GameplayWeaponAim aim),
                    Is.True);
                Assert.That(aim.TargetId, Is.EqualTo("alarm-panel"));
                Assert.That(aim.Position.z, Is.EqualTo(4.9f).Within(0.02f));

                presenter.SetWeaponTargetingActive(true);

                Assert.That(presenter.TargetOutlineVisible, Is.True);
                AssertOutlineColor(
                    surface,
                    TargetAcquisitionPresenter.AcquisitionOutlineColor);

                presenter.SetWeaponTargetingActive(false);

                Assert.That(presenter.TargetOutlineVisible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
                registry?.Dispose();
                world?.Dispose();
                Object.DestroyImmediate(observer);
                Object.DestroyImmediate(surface);
                Object.DestroyImmediate(entityRoot);
                Object.DestroyImmediate(worldRoot);
            }
        }

        [Test]
        public void WeaponAimSelectsNearestStableFractureChunk()
        {
            var host = new GameObject("Fracture Weapon Aim Test");
            var observer = CreateActorObject(
                "Fracture Aim Observer",
                Vector3.zero,
                withVisual: false);
            var worldRoot = new GameObject("Fracture Weapon Aim World");
            var entityRoot = new GameObject("Crate Root");
            LevelWorld world = null;
            GameplayWorldRegistry registry = null;
            try
            {
                LevelArchetypeCatalog catalog = LevelArchetypeCatalog.LoadDefault();
                Assert.That(
                    catalog.TryGet("prop.crate.standard", out var archetype),
                    Is.True);
                entityRoot.transform.SetParent(worldRoot.transform, false);
                LevelEntityView entity =
                    entityRoot.AddComponent<LevelEntityView>();
                entity.Initialize(
                    new LevelEntity
                    {
                        id = "fracture-crate",
                        archetypeId = "prop.crate.standard",
                        transform = new LevelTransformData(
                            new Float3Data(0f, 0f, 5f),
                            0f),
                    },
                    archetype);
                Object.Instantiate(
                    archetype.Prefab,
                    entityRoot.transform,
                    worldPositionStays: false);
                world = new LevelWorld(
                    worldRoot,
                    new Dictionary<string, LevelEntityView>
                    {
                        ["fracture-crate"] = entity,
                    },
                    null);
                registry = new GameplayWorldRegistry(world);
                registry.RegisterActor(
                    "observer",
                    "test",
                    targetable: false,
                    observer);
                TargetAcquisitionPresenter presenter =
                    host.AddComponent<TargetAcquisitionPresenter>();
                presenter.Bind(CreateSession(), registry, "observer");
                presenter.SetWeaponAimOriginProvider(
                    () => new Vector3(0f, 0.5f, 0f));
                Physics.SyncTransforms();
                presenter.RefreshNow(new Ray(
                    new Vector3(0f, 0.5f, 0f),
                    Vector3.forward));

                Assert.That(
                    presenter.TryGetWeaponAim(out GameplayWeaponAim aim),
                    Is.True);
                int expectedIndex = archetype.FractureProfile
                    .FindClosestChunkIndex(
                        entity.transform.InverseTransformPoint(aim.Position));
                Assert.That(aim.TargetId, Is.EqualTo("fracture-crate"));
                Assert.That(aim.PreferredFractureChunkIndex,
                    Is.EqualTo(expectedIndex));
                Assert.That(aim.PreferredFractureChunkIndex,
                    Is.InRange(0, archetype.FractureProfile.ChunkCount - 1));
            }
            finally
            {
                Object.DestroyImmediate(host);
                registry?.Dispose();
                world?.Dispose();
                Object.DestroyImmediate(observer);
                Object.DestroyImmediate(entityRoot);
                Object.DestroyImmediate(worldRoot);
            }
        }

        [Test]
        public void WeaponAimUsesMuzzlePathAfterPointerSelectsWorldPoint()
        {
            var host = new GameObject("Muzzle Path Aim Test");
            var observer = CreateActorObject(
                "Muzzle Path Observer",
                Vector3.zero,
                withVisual: false);
            var pointerSurface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var muzzleObstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
            LevelWorld world = null;
            GameplayWorldRegistry registry = null;
            try
            {
                pointerSurface.transform.position = new Vector3(2f, 1.2f, 6f);
                pointerSurface.transform.localScale = new Vector3(1f, 2f, 0.2f);
                muzzleObstruction.transform.position = new Vector3(
                    0.75f,
                    1.2f,
                    2.25f);
                muzzleObstruction.transform.localScale = new Vector3(
                    0.5f,
                    2f,
                    0.5f);
                world = new LevelWorld(
                    new GameObject("Muzzle Path Aim World"),
                    new Dictionary<string, LevelEntityView>(),
                    null);
                registry = new GameplayWorldRegistry(world);
                registry.RegisterActor(
                    "observer",
                    "test",
                    targetable: false,
                    observer);
                TargetAcquisitionPresenter presenter =
                    host.AddComponent<TargetAcquisitionPresenter>();
                presenter.Bind(CreateSession(), registry, "observer");
                presenter.SetWeaponAimOriginProvider(
                    () => new Vector3(0f, 1.2f, 0f));
                Physics.SyncTransforms();
                presenter.RefreshNow(new Ray(
                    new Vector3(2f, 1.2f, 0f),
                    Vector3.forward));

                Assert.That(
                    presenter.TryGetWeaponAim(out GameplayWeaponAim aim),
                    Is.True);
                Assert.That(aim.Position.z, Is.LessThan(3f));
                Assert.That(aim.Position.x, Is.LessThan(1.2f));
            }
            finally
            {
                Object.DestroyImmediate(host);
                registry?.Dispose();
                world?.Dispose();
                Object.DestroyImmediate(observer);
                Object.DestroyImmediate(pointerSurface);
                Object.DestroyImmediate(muzzleObstruction);
            }
        }

        [Test]
        public void WeaponTargetingHighlightsStableCharacterPathObstruction()
        {
            var host = new GameObject("Stable Character Path Target Test");
            var observer = CreateActorObject(
                "Stable Character Path Observer",
                Vector3.zero,
                withVisual: false);
            var worldRoot = new GameObject("Stable Character Path World");
            var pointerRoot = new GameObject("Pointer Target Root");
            var obstructionRoot = new GameObject("Obstruction Root");
            var pointerSurface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var obstructionSurface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            LevelWorld world = null;
            GameplayWorldRegistry registry = null;
            try
            {
                pointerRoot.transform.SetParent(worldRoot.transform, false);
                LevelEntityView pointerEntity =
                    pointerRoot.AddComponent<LevelEntityView>();
                pointerEntity.Initialize(
                    new LevelEntity
                    {
                        id = "pointer-target",
                        archetypeId = "prop.pointer-target",
                        transform = new LevelTransformData(
                            new Float3Data(2f, 0f, 6f),
                            0f),
                    },
                    new LevelArchetypeDefinition());
                pointerSurface.transform.SetParent(pointerRoot.transform, false);
                pointerSurface.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                pointerSurface.transform.localScale = new Vector3(1f, 2f, 0.2f);

                obstructionRoot.transform.SetParent(worldRoot.transform, false);
                LevelEntityView obstructionEntity =
                    obstructionRoot.AddComponent<LevelEntityView>();
                obstructionEntity.Initialize(
                    new LevelEntity
                    {
                        id = "character-path-obstruction",
                        archetypeId = "prop.character-path-obstruction",
                        transform = new LevelTransformData(
                            new Float3Data(0.75f, 0f, 2.25f),
                            0f),
                    },
                    new LevelArchetypeDefinition());
                obstructionSurface.transform.SetParent(
                    obstructionRoot.transform,
                    false);
                obstructionSurface.transform.localPosition =
                    new Vector3(0f, 1.2f, 0f);
                obstructionSurface.transform.localScale =
                    new Vector3(0.5f, 2f, 0.5f);

                world = new LevelWorld(
                    worldRoot,
                    new Dictionary<string, LevelEntityView>
                    {
                        ["pointer-target"] = pointerEntity,
                        ["character-path-obstruction"] = obstructionEntity,
                    },
                    null);
                registry = new GameplayWorldRegistry(world);
                registry.RegisterActor(
                    "observer",
                    "test",
                    targetable: false,
                    observer);
                TargetAcquisitionPresenter presenter =
                    host.AddComponent<TargetAcquisitionPresenter>();
                presenter.Bind(
                    CreateSession(includeRangedAttack: true),
                    registry,
                    "observer");
                Vector3 presentedOrigin = new Vector3(0f, 1.2f, 0f);
                presenter.SetWeaponAimOriginProvider(() => presentedOrigin);
                Physics.SyncTransforms();
                var pointerRay = new Ray(
                    new Vector3(2f, 1.2f, 0f),
                    Vector3.forward);
                presenter.RefreshNow(pointerRay);
                presenter.SetWeaponTargetingActive(true);

                Assert.That(
                    presenter.TryGetWeaponAim(out GameplayWeaponAim weaponAim),
                    Is.True);
                Assert.That(
                    weaponAim.TargetId,
                    Is.EqualTo("character-path-obstruction"));
                Assert.That(
                    presenter.TryGetPresentationAimPoint(
                        out Vector3 presentationAim),
                    Is.True);
                Assert.That(
                    presentationAim,
                    Is.EqualTo(weaponAim.Position)
                        .Using(Vector3ComparerWithEqualsOperator.Instance));
                AssertOutlineColor(
                    obstructionSurface,
                    TargetAcquisitionPresenter.AcquisitionOutlineColor);
                Assert.That(
                    pointerSurface.GetComponent<Renderer>().sharedMaterials.Any(
                        material => material.shader.name ==
                            "GritGud/RuntimeOutline"),
                    Is.False);

                presentedOrigin = new Vector3(-2f, 1.2f, 0f);
                presenter.RefreshNow(pointerRay);

                Assert.That(
                    presenter.TryGetWeaponAim(out GameplayWeaponAim stableAim),
                    Is.True);
                Assert.That(
                    stableAim.TargetId,
                    Is.EqualTo("character-path-obstruction"));
                Assert.That(
                    stableAim.Position,
                    Is.EqualTo(weaponAim.Position)
                        .Using(Vector3ComparerWithEqualsOperator.Instance));
            }
            finally
            {
                Object.DestroyImmediate(host);
                registry?.Dispose();
                world?.Dispose();
                Object.DestroyImmediate(observer);
                Object.DestroyImmediate(pointerSurface);
                Object.DestroyImmediate(obstructionSurface);
                Object.DestroyImmediate(pointerRoot);
                Object.DestroyImmediate(obstructionRoot);
                Object.DestroyImmediate(worldRoot);
            }
        }

        [Test]
        public void PointerOutlineWorksInBothModesButGroundHaloRequiresPlayerTurn()
        {
            var host = new GameObject("Target Acquisition Test");
            var observer = new GameObject("Observer");
            var target = new GameObject("Target");
            var cameraObject = new GameObject("Target Camera");
            GameObject wall = null;
            LevelWorld world = null;
            GameplayWorldRegistry registry = null;
            try
            {
                CharacterController observerController =
                    observer.AddComponent<CharacterController>();
                observerController.center = new Vector3(0f, 0.9f, 0f);
                observerController.height = 1.8f;
                observer.AddComponent<ActorStancePresenter>();

                target.transform.position = new Vector3(0f, 0f, 5f);
                CharacterController targetController =
                    target.AddComponent<CharacterController>();
                targetController.center = new Vector3(0f, 0.9f, 0f);
                targetController.height = 1.8f;
                CapsuleCollider targetQueryCollider =
                    target.AddComponent<CapsuleCollider>();
                targetQueryCollider.center = targetController.center;
                targetQueryCollider.height = targetController.height;
                targetQueryCollider.radius = targetController.radius;
                target.AddComponent<ActorStancePresenter>();
                GameObject targetVisual =
                    GameObject.CreatePrimitive(PrimitiveType.Capsule);
                targetVisual.transform.SetParent(target.transform, false);
                targetVisual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                Object.DestroyImmediate(targetVisual.GetComponent<Collider>());

                Camera camera = cameraObject.AddComponent<Camera>();
                cameraObject.transform.position = new Vector3(0f, 1.25f, 0f);
                cameraObject.transform.rotation = Quaternion.identity;

                TargetAcquisitionPresenter presenter =
                    host.AddComponent<TargetAcquisitionPresenter>();
                GameplaySession session = CreateSession();
                world = new LevelWorld(
                    new GameObject("Target Test World"),
                    new Dictionary<string, LevelEntityView>(),
                    null);
                registry = new GameplayWorldRegistry(world);
                registry.RegisterActor(
                    "observer",
                    "test",
                    targetable: false,
                    observer);
                registry.RegisterActor(
                    "target",
                    "test",
                    targetable: true,
                    target);
                Physics.SyncTransforms();
                presenter.Bind(
                    session,
                    registry,
                    "observer");
                var crosshairRay = new Ray(
                    cameraObject.transform.position,
                    cameraObject.transform.forward);
                presenter.RefreshNow(crosshairRay);
                TargetAcquisitionPreview initialPreview =
                    presenter.CurrentPreview;
                presenter.RefreshNow(crosshairRay);

                Assert.That(presenter.HasPointerTarget, Is.True);
                Assert.That(presenter.CurrentPreview, Is.SameAs(initialPreview));
                Assert.That(presenter.TargetOutlineVisible, Is.True);
                Assert.That(presenter.GroundHaloVisible, Is.False);
                Assert.That(presenter.CurrentSnapshot, Is.Not.Null);
                Assert.That(presenter.CurrentHitChancePercent, Is.EqualTo(100));
                var feedbackOwner = new object();
                presenter.SetFeedbackSuppressed(feedbackOwner, true);
                Assert.That(presenter.ShouldPresentFeedback, Is.False);
                Assert.That(presenter.TargetOutlineVisible, Is.False);
                Assert.That(presenter.GroundHaloVisible, Is.False);
                presenter.SetFeedbackSuppressed(feedbackOwner, false);
                Assert.That(presenter.ShouldPresentFeedback, Is.True);
                Assert.That(presenter.TargetOutlineVisible, Is.True);
                Material targetOutline = targetVisual
                    .GetComponent<Renderer>()
                    .sharedMaterials
                    .Single(material =>
                        material.shader.name == "GritGud/RuntimeOutline");
                Color outlineColor = targetOutline.GetColor("_OutlineColor");
                Assert.That(
                    outlineColor.r,
                    Is.EqualTo(
                        TargetAcquisitionPresenter.AcquisitionOutlineColor.r)
                        .Within(0.001f));
                Assert.That(
                    outlineColor.g,
                    Is.EqualTo(
                        TargetAcquisitionPresenter.AcquisitionOutlineColor.g)
                        .Within(0.001f));
                Assert.That(
                    outlineColor.b,
                    Is.EqualTo(
                        TargetAcquisitionPresenter.AcquisitionOutlineColor.b)
                        .Within(0.001f));

                Assert.That(session.EnterTurnMode(), Is.True);
                presenter.RefreshNow(crosshairRay);

                Assert.That(presenter.TargetOutlineVisible, Is.True);
                Assert.That(presenter.GroundHaloVisible, Is.True);

                Assert.That(
                    session.TryEndTurn("observer", out TurnEndFailure failure),
                    Is.True);
                Assert.That(failure, Is.EqualTo(TurnEndFailure.None));
                presenter.RefreshNow(crosshairRay);

                Assert.That(presenter.TargetOutlineVisible, Is.True);
                Assert.That(presenter.GroundHaloVisible, Is.False);

                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = new Vector3(1f, 1.1f, 2.5f);
                wall.transform.localScale = new Vector3(0.6f, 3f, 0.2f);
                cameraObject.transform.position = new Vector3(2f, 1.25f, 0f);
                crosshairRay = new Ray(
                    cameraObject.transform.position,
                    (target.transform.position
                        + targetController.center
                        - cameraObject.transform.position).normalized);
                Physics.SyncTransforms();
                presenter.InvalidateWorldEvidence();
                Assert.That(
                    Physics.RaycastAll(
                        crosshairRay,
                        10f,
                        Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore)
                        .Any(hit => hit.collider.transform == wall.transform),
                    Is.True,
                    "The regression wall must occlude the camera ray.");
                presenter.RefreshNow(crosshairRay);

                Assert.That(presenter.HasPointerTarget, Is.True);
                Assert.That(presenter.TargetOutlineVisible, Is.True);
                Assert.That(presenter.CurrentSnapshot.VisibleSampleCount,
                    Is.GreaterThan(0));

                Object.DestroyImmediate(wall);
                wall = null;
                cameraObject.transform.position = new Vector3(0f, 1.25f, 0f);
                crosshairRay = new Ray(
                    cameraObject.transform.position,
                    cameraObject.transform.forward);
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = new Vector3(0f, 1f, 2.5f);
                wall.transform.localScale = new Vector3(3f, 3f, 0.2f);
                Physics.SyncTransforms();
                presenter.InvalidateWorldEvidence();
                presenter.RefreshNow(crosshairRay);

                Assert.That(presenter.HasPointerTarget, Is.False);
                Assert.That(presenter.TargetOutlineVisible, Is.False);
                Assert.That(presenter.GroundHaloVisible, Is.False);
                Assert.That(presenter.CurrentSnapshot, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(wall);
                Object.DestroyImmediate(host);
                registry?.Dispose();
                world?.Dispose();
                Object.DestroyImmediate(observer);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void ContextualValidationHighlightsGenericTargetsWithSemanticColor()
        {
            var host = new GameObject("Contextual Target Feedback Test");
            var observer = CreateActorObject(
                "Contextual Feedback Observer",
                Vector3.zero,
                withVisual: false);
            GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            LevelWorld world = null;
            GameplayWorldRegistry registry = null;
            try
            {
                prop.name = "Contextual Feedback Prop";
                world = new LevelWorld(
                    new GameObject("Contextual Feedback World"),
                    new Dictionary<string, LevelEntityView>(),
                    null);
                registry = new GameplayWorldRegistry(world);
                registry.RegisterActor(
                    "observer",
                    "test",
                    targetable: false,
                    observer);
                TargetAcquisitionPresenter presenter =
                    host.AddComponent<TargetAcquisitionPresenter>();
                presenter.Bind(CreateSession(), registry, "observer");
                var feedbackOwner = new object();

                presenter.PresentValidationFeedback(
                    feedbackOwner,
                    "crate",
                    prop.transform,
                    isValid: false,
                    "INVALID TARGET - OUT OF REACH");

                Assert.That(presenter.HasValidationFeedback, Is.True);
                Assert.That(presenter.TargetOutlineVisible, Is.True);
                Assert.That(presenter.CurrentValidationIsValid, Is.False);
                Assert.That(
                    presenter.TryGetPointerFeedback(
                        out TargetingPointerFeedback pointerFeedback),
                    Is.True);
                Assert.That(pointerFeedback.Text,
                    Is.EqualTo("INVALID TARGET - OUT OF REACH"));
                Assert.That(pointerFeedback.IsValid, Is.False);
                AssertOutlineColor(
                    prop,
                    TargetAcquisitionPresenter.InvalidOutlineColor);

                presenter.PresentValidationFeedback(
                    feedbackOwner,
                    "crate",
                    prop.transform,
                    isValid: true,
                    "VALID TARGET - PUSH");

                Assert.That(presenter.CurrentValidationIsValid, Is.True);
                AssertOutlineColor(
                    prop,
                    TargetAcquisitionPresenter.AcquisitionOutlineColor);

                presenter.ClearValidationFeedback(feedbackOwner);

                Assert.That(presenter.HasValidationFeedback, Is.False);
                Assert.That(presenter.TargetOutlineVisible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
                registry?.Dispose();
                world?.Dispose();
                Object.DestroyImmediate(observer);
                Object.DestroyImmediate(prop);
            }
        }

        [Test]
        public void ValidPartyTargetUsesFriendlyGreenButInvalidRemainsOrange()
        {
            var host = new GameObject("Friendly Target Feedback Test");
            var observer = CreateActorObject(
                "Friendly Feedback Observer",
                Vector3.zero,
                withVisual: false);
            GameObject friendly = GameObject.CreatePrimitive(PrimitiveType.Cube);
            LevelWorld world = null;
            GameplayWorldRegistry registry = null;
            try
            {
                friendly.name = "Friendly Feedback Target";
                world = new LevelWorld(
                    new GameObject("Friendly Feedback World"),
                    new Dictionary<string, LevelEntityView>(),
                    null);
                registry = new GameplayWorldRegistry(world);
                registry.RegisterActor(
                    "observer",
                    "test",
                    targetable: false,
                    observer);
                TargetAcquisitionPresenter presenter =
                    host.AddComponent<TargetAcquisitionPresenter>();
                presenter.Bind(
                    CreateSession(includePlayerParty: true),
                    registry,
                    "observer");
                var feedbackOwner = new object();

                presenter.PresentValidationFeedback(
                    feedbackOwner,
                    "target",
                    friendly.transform,
                    isValid: true,
                    "VALID FRIENDLY TARGET");

                AssertOutlineColor(
                    friendly,
                    TargetAcquisitionPresenter.FriendlyOutlineColor);

                presenter.PresentValidationFeedback(
                    feedbackOwner,
                    "target",
                    friendly.transform,
                    isValid: false,
                    "INVALID TARGET - OUT OF REACH");

                AssertOutlineColor(
                    friendly,
                    TargetAcquisitionPresenter.InvalidOutlineColor);
            }
            finally
            {
                Object.DestroyImmediate(host);
                registry?.Dispose();
                world?.Dispose();
                Object.DestroyImmediate(observer);
                Object.DestroyImmediate(friendly);
            }
        }

        [Test]
        public void NonTargetablePartyCompanionIsAcknowledgedAsFriendlyAim()
        {
            var host = new GameObject("Friendly Companion Aim Test");
            var observer = CreateActorObject(
                "Friendly Companion Observer",
                Vector3.zero,
                withVisual: false);
            var companion = CreateActorObject(
                "Friendly Companion Target",
                new Vector3(0f, 0f, 5f),
                withVisual: true);
            LevelWorld world = null;
            GameplayWorldRegistry registry = null;
            try
            {
                world = new LevelWorld(
                    new GameObject("Friendly Companion Aim World"),
                    new Dictionary<string, LevelEntityView>(),
                    null);
                registry = new GameplayWorldRegistry(world);
                registry.RegisterActor(
                    "observer",
                    "test",
                    targetable: false,
                    observer);
                registry.RegisterActor(
                    "target",
                    "test",
                    targetable: false,
                    companion);
                TargetAcquisitionPresenter presenter =
                    host.AddComponent<TargetAcquisitionPresenter>();
                presenter.Bind(
                    CreateSession(
                        includePlayerParty: true,
                        includeRangedAttack: true),
                    registry,
                    "observer");
                presenter.SetWeaponAimOriginProvider(
                    () => new Vector3(0f, 1.2f, 0f));
                Physics.SyncTransforms();
                presenter.RefreshNow(new Ray(
                    new Vector3(0f, 1.2f, 0f),
                    Vector3.forward));
                presenter.SetWeaponTargetingActive(true);

                Assert.That(
                    presenter.CurrentTargetActorId,
                    Is.EqualTo("target"));
                Assert.That(presenter.TargetOutlineVisible, Is.True);
                AssertOutlineColor(
                    companion,
                    TargetAcquisitionPresenter.FriendlyOutlineColor);
                Material outline = FindOutlineMaterial(companion);
                Assert.That(
                    outline.GetFloat("_OutlineScreenSpace"),
                    Is.EqualTo(1f));
                Assert.That(
                    outline.GetFloat("_OutlineScreenWidth"),
                    Is.EqualTo(
                        TargetFeedbackPresenter.TargetOutlineScreenWidthPixels));
                Assert.That(
                    presenter.TryGetPointerFeedback(
                        out TargetingPointerFeedback pointerFeedback),
                    Is.True);
                Assert.That(pointerFeedback.IsValid, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
                registry?.Dispose();
                world?.Dispose();
                Object.DestroyImmediate(observer);
                Object.DestroyImmediate(companion);
            }
        }

        [Test]
        public void GeometricChanceRoundsVisibleRegionFraction()
        {
            var snapshot = new TargetExposureSnapshot(
                "observer",
                "target",
                new[]
                {
                    new TargetRegionExposure(TargetRegionId.Torso, 3, 5),
                });

            Assert.That(
                TargetPreviewCalculator.Calculate(
                    snapshot,
                    AccuracyDecayDefinition.None,
                    distance: 5f).HitChancePercent,
                Is.EqualTo(60));
        }

        [Test]
        public void PointerCanAcquireEveryTargetableRegisteredActor()
        {
            var host = new GameObject("Multi Target Acquisition Test");
            var observer = CreateActorObject("Observer", Vector3.zero, false);
            var first = CreateActorObject("First", new Vector3(-1f, 0f, 5f), true);
            var second = CreateActorObject("Second", new Vector3(1f, 0f, 5f), true);
            LevelWorld world = null;
            GameplayWorldRegistry registry = null;
            try
            {
                world = new LevelWorld(
                    new GameObject("Multi Target World"),
                    new Dictionary<string, LevelEntityView>(),
                    null);
                registry = new GameplayWorldRegistry(world);
                registry.RegisterActor(
                    "observer",
                    "test",
                    targetable: false,
                    observer);
                registry.RegisterActor(
                    "first",
                    "test",
                    targetable: true,
                    first);
                registry.RegisterActor(
                    "second",
                    "test",
                    targetable: true,
                    second);
                var session = new GameplaySession(new ScenarioDefinition(
                    "multi-target-test",
                    new ScenarioTimingDefinition(1.25f),
                    new[]
                    {
                        CreateActorDefinition("observer", 10, Vector3.zero),
                        CreateActorDefinition("first", 5, first.transform.position),
                        CreateActorDefinition("second", 4, second.transform.position),
                    },
                    Array.Empty<ScenarioObjectiveDefinition>()));
                TargetAcquisitionPresenter presenter =
                    host.AddComponent<TargetAcquisitionPresenter>();
                presenter.Bind(session, registry, "observer");
                Physics.SyncTransforms();
                Vector3 origin = new Vector3(0f, 1.25f, 0f);

                presenter.RefreshNow(new Ray(
                    origin,
                    (new Vector3(-1f, 0.9f, 5f) - origin).normalized));
                Assert.That(presenter.CurrentTargetActorId, Is.EqualTo("first"));

                presenter.RefreshNow(new Ray(
                    origin,
                    (new Vector3(1f, 0.9f, 5f) - origin).normalized));
                Assert.That(presenter.CurrentTargetActorId, Is.EqualTo("second"));
                Assert.That(presenter.TargetOutlineVisible, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
                registry?.Dispose();
                world?.Dispose();
                Object.DestroyImmediate(observer);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void PinnedBodyIsAcquiredAwayFromFeetButMovementColliderIsNotAimable()
        {
            var observer = CreateActorObject(
                "Pinned Target Observer",
                new Vector3(0f, 0f, -4f),
                withVisual: false);
            var target = CreateActorObject(
                "Pinned Target",
                Vector3.zero,
                withVisual: true);
            LevelWorld world = null;
            GameplayWorldRegistry registry = null;
            try
            {
                world = new LevelWorld(
                    new GameObject("Pinned Target World"),
                    new Dictionary<string, LevelEntityView>(),
                    null);
                registry = new GameplayWorldRegistry(world);
                registry.RegisterActor(
                    "observer",
                    "test",
                    targetable: false,
                    observer);
                registry.RegisterActor(
                    "target",
                    "test",
                    targetable: true,
                    target);
                GameplayActorView targetView = registry.GetActor("target");
                targetView.ReplayActions.PresentPinState(new ActorPinState(
                    "target",
                    "prop",
                    displacementSequence: 1,
                    new DisplacementContactEvidence(
                        "target",
                        new GameplayPosition(0f, 0f, 0f),
                        new GameplayPosition(0f, 1f, 0f),
                        overlapDepth: 0.2f)));
                Physics.SyncTransforms();
                var query = new UnityPointerTargetQuery(
                    observer.transform,
                    registry);

                Assert.That(
                    query.TryAcquire(
                        new Ray(
                            new Vector3(0f, 0.3f, -4f),
                            Vector3.forward),
                        out GameplayActorView acquired),
                    Is.True);
                Assert.That(acquired.ActorId, Is.EqualTo("target"));
                Assert.That(
                    targetView.TargetProfile.ProfileKind,
                    Is.EqualTo(ActorTargetProfileKind.PinnedDown));

                Assert.That(
                    query.TryAcquire(
                        new Ray(
                            new Vector3(0f, 1.2f, -4f),
                            Vector3.forward),
                        out _),
                    Is.False,
                    "The upright movement collider must not remain an aiming bound.");
            }
            finally
            {
                registry?.Dispose();
                world?.Dispose();
                if (registry == null)
                {
                    Object.DestroyImmediate(observer);
                    Object.DestroyImmediate(target);
                }
            }
        }

        private static GameplaySession CreateSession(
            bool includePlayerParty = false,
            bool includeRangedAttack = false)
        {
            AttackDefinition attack = includeRangedAttack
                ? new AttackDefinition(
                    "attack.test-rifle",
                    "Test rifle",
                    new ActionCost(1, 0f, ActionMobility.Set),
                    2f,
                    accuracyDecay: AccuracyDecayDefinition.None)
                : null;
            CharacterProfileDefinition CreateProfile(string actorId) =>
                new CharacterProfileDefinition(
                    "character." + actorId,
                    actorId,
                    "Test Operative",
                    new[]
                    {
                        new CharacterRating(CoreAttributeIds.Strength, 3),
                        new CharacterRating(CoreAttributeIds.Dexterity, 3),
                        new CharacterRating(CoreAttributeIds.Grit, 3),
                        new CharacterRating(CoreAttributeIds.Charisma, 3),
                    },
                    Array.Empty<CharacterRating>(),
                    Array.Empty<string>());
            var observer = new ScenarioActorDefinition(
                "observer",
                initiative: 10,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f),
                attack,
                characterProfile: includePlayerParty
                    ? CreateProfile("observer")
                    : null);
            var target = new ScenarioActorDefinition(
                "target",
                initiative: 5,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 5f), 0f),
                new TurnBudget(4, 8f),
                characterProfile: includePlayerParty
                    ? CreateProfile("target")
                    : null);
            PlayerPartyDefinition playerParty = includePlayerParty
                ? new PlayerPartyDefinition(
                    new[] { "observer", "target" },
                    "observer")
                : null;
            return new GameplaySession(new ScenarioDefinition(
                "target-acquisition-test",
                new ScenarioTimingDefinition(1.25f),
                new[] { observer, target },
                Array.Empty<ScenarioObjectiveDefinition>(),
                playerParty: playerParty));
        }

        private static GameObject CreateActorObject(
            string name,
            Vector3 position,
            bool withVisual)
        {
            var actor = new GameObject(name);
            actor.transform.position = position;
            CharacterController controller = actor.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.height = 1.8f;
            actor.AddComponent<ActorStancePresenter>();
            if (withVisual)
            {
                CapsuleCollider queryCollider = actor.AddComponent<CapsuleCollider>();
                queryCollider.center = controller.center;
                queryCollider.height = controller.height;
                queryCollider.radius = controller.radius;
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.transform.SetParent(actor.transform, false);
                visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                Object.DestroyImmediate(visual.GetComponent<Collider>());
            }

            return actor;
        }

        private static void AssertOutlineColor(
            GameObject target,
            Color expected)
        {
            Material outline = FindOutlineMaterial(target);
            Color actual = outline.GetColor("_OutlineColor");
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f));
        }

        private static Material FindOutlineMaterial(GameObject target) =>
            target.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Distinct()
                .Single(material =>
                    material.shader.name == "GritGud/RuntimeOutline");

        private static ScenarioActorDefinition CreateActorDefinition(
            string id,
            int initiative,
            Vector3 position) =>
            new ScenarioActorDefinition(
                id,
                initiative,
                new GameplayActorPose(
                    new GameplayPosition(position.x, position.y, position.z),
                    0f),
                new TurnBudget(4, 8f));
    }
}
