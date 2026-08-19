using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Bootstrap;
using GritGud.Presentation.Levels;
using GritGud.Presentation.Levels.Runtime;
using GritGud.Presentation.Persistence;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GritGud.Presentation.Gameplay
{
    public sealed partial class GameplayController : MonoBehaviour
    {
        private GameplayContentPackage content;
        private GameplayScenarioAssembly scenarioAssembly;
        private LevelWorld levelWorld;
        private GameplayWorldRegistry worldRegistry;
        private ThirdPersonMotor player;
        private GameplayEnvironmentLighting environmentLighting;
        private LevelDressingProjector dressingProjector;
        private GameplayEnvironmentStyle environmentStyle;
        private GameplayPostProcessing postProcessing;
        private GameplayVisualTheme visualTheme;
        private SurfacePresentationCatalog surfacePresentationCatalog;
        private GameplayCameraRig cameraRig;
        private GameplayInputController inputController;
        private GameplayHud hud;
        private GameplayPartyHud partyHud;
        private GameplayTurnReplayHud turnReplayHud;
        private GameplayTurnReplayWorldPresenter turnReplayWorldPresenter;
        private GameplayCombatStateTimeline turnReplayStateTimeline;
        private GameplayDialogueDrawer dialogueDrawer;
        private GameplaySessionPresenter sessionPresenter;
        private TurnMovementController turnMovementController;
        private GameplayActionController actionController;
        private GameplayAttackController attackController;
        private GameplayEnemyController enemyController;
        private GameplayEquipmentController equipmentController;
        private GameplayHotbarController hotbarController;
        private GameplayProjectileController projectileController;
        private GameplayThrownExplosiveController thrownExplosiveController;
        private GameplaySmokeFieldController smokeFieldController;
        private GameplaySmokeFieldSession smokeFieldSession;
        private GameplayFireFieldController fireFieldController;
        private GameplayFireFieldSession fireFieldSession;
        private GameplayConsumableController consumableController;
        private GameplayPartyControlSession partyControl;
        private GameplayPartyPersistenceSession partyPersistence;
        private GameplayPartyPresentationSession partyPresentation;
        private GameplayWeaponTargetingController weaponTargetingController;
        private GameplayTargetingCursorPresenter targetingCursorPresenter;
        private GameplayObjectivePresenter objectivePresenter;
        private TargetAcquisitionPresenter targetAcquisitionPresenter;
        private GameplayDestructibleController destructibleController;
        private GameplayDisplacementController displacementController;
        private GameplayVehicleController vehicleController;
        private GameplayDroneController droneController;
        private GameplayDialogueLog dialogueLog;
        private GameplayCharacterGroundingPresenter characterGroundingPresenter;
        private GameplayTacticalTransitionPresenter tacticalTransitionPresenter;
        private GameplaySurfaceImpactPresenter surfaceImpactPresenter;
        private GameplayCombatReactionPresenter combatReactionPresenter;
        private GameplayControlRouter controlRouter;

        public bool IsRunning => levelWorld != null && player != null;

        public GameplaySession Session => sessionPresenter?.Session;

        public GameplayDialogueLog DialogueLog => dialogueLog;

        internal GameplayPartyControlSession PartyControl => partyControl;

        internal GameplayPartyHud PartyHud => partyHud;

        internal GameplayScenarioAssembly ScenarioAssembly => scenarioAssembly;

        internal GameplayWorldRegistry WorldRegistry => worldRegistry;

        private void Awake()
        {
            EnsureDependencies();
        }

        private void EnsureDependencies()
        {
            inputController = GetOrAddComponent<GameplayInputController>();
            hud = GetOrAddComponent<GameplayHud>();
            partyHud = GetOrAddComponent<GameplayPartyHud>();
            turnReplayHud = GetOrAddComponent<GameplayTurnReplayHud>();
            turnReplayWorldPresenter ??= new GameplayTurnReplayWorldPresenter();
            dialogueDrawer = GetOrAddComponent<GameplayDialogueDrawer>();
            sessionPresenter = GetOrAddComponent<GameplaySessionPresenter>();
            turnMovementController = GetOrAddComponent<TurnMovementController>();
            actionController = GetOrAddComponent<GameplayActionController>();
            attackController = GetOrAddComponent<GameplayAttackController>();
            enemyController = GetOrAddComponent<GameplayEnemyController>();
            equipmentController = GetOrAddComponent<GameplayEquipmentController>();
            hotbarController = GetOrAddComponent<GameplayHotbarController>();
            projectileController = GetOrAddComponent<GameplayProjectileController>();
            thrownExplosiveController =
                GetOrAddComponent<GameplayThrownExplosiveController>();
            smokeFieldController = GetOrAddComponent<GameplaySmokeFieldController>();
            fireFieldController = GetOrAddComponent<GameplayFireFieldController>();
            weaponTargetingController =
                GetOrAddComponent<GameplayWeaponTargetingController>();
            targetingCursorPresenter =
                GetOrAddComponent<GameplayTargetingCursorPresenter>();
            objectivePresenter = GetOrAddComponent<GameplayObjectivePresenter>();
            targetAcquisitionPresenter =
                GetOrAddComponent<TargetAcquisitionPresenter>();
            destructibleController =
                GetOrAddComponent<GameplayDestructibleController>();
            displacementController =
                GetOrAddComponent<GameplayDisplacementController>();
            vehicleController = GetOrAddComponent<GameplayVehicleController>();
            droneController = GetOrAddComponent<GameplayDroneController>();
            characterGroundingPresenter =
                GetOrAddComponent<GameplayCharacterGroundingPresenter>();
            tacticalTransitionPresenter =
                GetOrAddComponent<GameplayTacticalTransitionPresenter>();
            surfaceImpactPresenter =
                GetOrAddComponent<GameplaySurfaceImpactPresenter>();
            combatReactionPresenter =
                GetOrAddComponent<GameplayCombatReactionPresenter>();
            ResetPresentationBindings();
        }

        private T GetOrAddComponent<T>() where T : Component
        {
            T component = GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private void ResetPresentationBindings()
        {
            inputController?.End();
            controlRouter = null;
            hud?.Hide();
            partyHud?.Unbind();
            turnReplayHud?.Unbind();
            turnReplayWorldPresenter?.Dispose();
            turnReplayStateTimeline?.Dispose();
            turnReplayStateTimeline = null;
            hud?.UnbindSession();
            hud?.UnbindTurnMovement();
            hud?.UnbindGameplayActions();
            hud?.UnbindGameplayAttack();
            hud?.UnbindGameplayEquipment();
            hud?.UnbindGameplayHotbar();
            hud?.UnbindGameplayConsumables();
            hud?.UnbindGameplayWeaponTargeting();
            hud?.UnbindGameplayProjectile();
            hud?.UnbindGameplayDisplacement();
            hud?.UnbindInputSource();
            hud?.UnbindTurnModeToggle();
            hud?.UnbindBugReportExport();
            dialogueDrawer?.Hide();
            dialogueDrawer?.Unbind();
            turnMovementController?.Unbind();
            actionController?.Unbind();
            enemyController?.Unbind();
            combatReactionPresenter?.Unbind();
            surfaceImpactPresenter?.Unbind();
            tacticalTransitionPresenter?.Unbind();
            characterGroundingPresenter?.Unbind();
            weaponTargetingController?.Unbind();
            targetingCursorPresenter?.Unbind();
            attackController?.Unbind();
            equipmentController?.Unbind();
            hotbarController?.Unbind();
            projectileController?.Unbind();
            thrownExplosiveController?.Unbind();
            smokeFieldController?.Unbind();
            fireFieldController?.Unbind();
            objectivePresenter?.Unbind();
            targetAcquisitionPresenter?.Unbind();
            destructibleController?.Unbind();
            displacementController?.Unbind();
            vehicleController?.Unbind();
            droneController?.Unbind();
            sessionPresenter?.Unbind();
        }

        public void Begin()
        {
            Begin(GameplayContentLoader.LoadDefault());
        }

        public void BeginCommitted(LevelDocument level)
        {
            Begin(GameplayContentLoader.LoadCommitted(level));
        }

        public void BeginSandbox(LevelDocument level)
        {
            Begin(GameplayContentLoader.LoadSandbox(level));
        }

        private void Begin(GameplayContentPackage initialContent)
        {
            EndSession();
            try
            {
                EnsureDependencies();
                RequireBootstrap();
                enabled = true;
                content = initialContent
                    ?? throw new ArgumentNullException(nameof(initialContent));
                GameplayWorldStart worldStart = BuildWorld();
                GameplaySession session = BuildSession(worldStart);
                InstallGameplayFeatures(session, worldStart);
            }
            catch
            {
                EndSession();
                throw;
            }
        }

        private void RequireBootstrap()
        {
            GameBootstrap bootstrap = GameBootstrap.Instance;
            if (bootstrap == null)
            {
                bootstrap = GetComponent<GameBootstrap>();
            }

            if (bootstrap == null)
            {
                throw new InvalidOperationException(
                    "Gameplay must be hosted by the application bootstrap.");
            }
        }

        private GameplayWorldStart BuildWorld()
        {
            levelWorld = new LevelLoader(content.Archetypes).Load(content.Level);
            visualTheme = GameplayVisualTheme.LoadDefault();
            surfacePresentationCatalog = SurfacePresentationCatalog.LoadDefault();
            environmentLighting = GameplayEnvironmentLighting.Create(
                levelWorld.Root.transform,
                content.Level.environment);
            dressingProjector = new LevelDressingProjector(
                levelWorld.Root.transform,
                LevelDressingCatalog.LoadDefault());
            dressingProjector.Replace(
                content.Level.dressing,
                showZoneGizmos: false,
                playAudio: true);
            environmentStyle = GameplayEnvironmentStyle.Create(
                levelWorld.Root.transform,
                visualTheme,
                surfacePresentationCatalog);
            postProcessing = GameplayPostProcessing.Create(
                levelWorld.Root.transform,
                visualTheme);
            var journal = new GameplayJournal();
            destructibleController.Bind(levelWorld, content.Level, journal);
            worldRegistry = new GameplayWorldRegistry(levelWorld);
            var resolvedActorPoses = new Dictionary<string, GameplayActorPose>(
                StringComparer.Ordinal);
            foreach (ScenarioActorRuntimeDefinition actorDefinition in
                content.Assembly.Actors)
            {
                GameObject actorRoot = GameplayActorFactory.CreateActor(
                    actorDefinition,
                    content.ActorPresentations,
                    content.CharacterAppearances,
                    content.Characters);
                Vector3 authoredPosition = actorRoot.transform.position;
                GameplayGroundPlacement.PlaceOnGround(
                    actorRoot.transform,
                    authoredPosition);
                ActorStance stance =
                    actorDefinition.GameplayDefinition.StartingPose.Stance;
                actorRoot.GetComponent<ActorStancePresenter>()
                    .ApplyResolved(stance);
                worldRegistry.RegisterActor(actorDefinition, actorRoot);
                resolvedActorPoses.Add(
                    actorDefinition.Id,
                    GameplayPoseAdapter.FromTransform(actorRoot.transform, stance));
            }

            scenarioAssembly = content.Assembly.WithResolvedActorPoses(
                resolvedActorPoses);
            string initiallySelectedActorId =
                scenarioAssembly.PlayerParty.InitiallySelectedActorId;
            GameplayActorView playerView = worldRegistry.GetActor(
                initiallySelectedActorId);
            player = playerView.Motor ?? throw new InvalidOperationException(
                $"Initially selected party actor '{initiallySelectedActorId}' requires "
                + $"{nameof(ThirdPersonMotor)}.");
            ExplorationMovementInput movementInput = playerView.MovementInput ??
                throw new InvalidOperationException(
                    $"Initially selected party actor '{initiallySelectedActorId}' requires "
                    + $"{nameof(ExplorationMovementInput)}.");
            movementInput.BindInputSource(inputController);

            Physics.SyncTransforms();
            foreach (string partyActorId in scenarioAssembly.PlayerParty.ActorIds)
            {
                GameplayActorView partyActor = worldRegistry.GetActor(partyActorId);
                partyActor.Motor.SetRespawnPoint(partyActor.Transform.position);
            }

            return new GameplayWorldStart(
                journal,
                initiallySelectedActorId,
                movementInput);
        }

        private GameplaySession BuildSession(GameplayWorldStart worldStart)
        {
            partyPersistence = new GameplayPartyPersistenceSession(
                new PlayerPrefsGameplayPartySaveStore());
            GameplayPartySave restoredParty = partyPersistence.Load(
                scenarioAssembly.Scenario);
            var session = new GameplaySession(
                scenarioAssembly.Scenario,
                worldStart.Journal,
                scenarioAssembly.RandomSeed,
                restoredParty);
            dialogueLog = new GameplayDialogueLog();
            partyControl = new GameplayPartyControlSession(session);
            partyPersistence.Bind(session);
            smokeFieldSession = new GameplaySmokeFieldSession(session);
            smokeFieldController.Bind(smokeFieldSession);
            fireFieldSession = new GameplayFireFieldSession(
                session,
                destructibleController.Session);
            fireFieldController.Bind(fireFieldSession);
            tacticalTransitionPresenter.Bind(
                session,
                visualTheme,
                inputController,
                hud,
                partyHud);
            characterGroundingPresenter.Bind(
                worldRegistry,
                visualTheme,
                levelWorld.Root.transform);
            if (scenarioAssembly.Vehicles.Count > 0)
            {
                vehicleController.Bind(
                    levelWorld,
                    session,
                    scenarioAssembly.Vehicles);
            }
            if (scenarioAssembly.Drones.Count > 0)
            {
                droneController.Bind(
                    levelWorld,
                    session,
                    worldRegistry,
                    scenarioAssembly.Drones,
                    destructibleController.Session,
                    smokeFieldSession,
                    dialogueLog,
                    scenarioAssembly.RandomSeed,
                    IsPointerOverGameplayInterface);
            }
            sessionPresenter.Bind(
                session,
                worldStart.MovementInput,
                player.transform,
                worldStart.InitiallySelectedActorId);
            hud.BindSession(
                session,
                worldStart.InitiallySelectedActorId,
                scenarioAssembly);
            dialogueLog.AppendCombatDiagnostic(
                GameplayCombatDiagnosticFormatter.FormatInitiative(session));
            dialogueDrawer.Bind(dialogueLog, ExportDialogue);
            dialogueDrawer.Show();

            cameraRig = GameplayCameraRig.Create(
                player.transform,
                worldStart.MovementInput,
                inputController,
                environmentStyle.PlayerCutoutRenderers);

            return session;
        }

        private void InstallGameplayFeatures(
            GameplaySession session,
            GameplayWorldStart worldStart)
        {
            string actorId = worldStart.InitiallySelectedActorId;
            GameplayEmergencyCycleSession emergencyCycle = null;
            var deliveryInstaller =
                new GameplayProjectileDeliveryFeatureInstaller(
                    session,
                    worldRegistry,
                    destructibleController,
                    targetAcquisitionPresenter,
                    dialogueLog,
                    actionController,
                    sessionPresenter,
                    projectileController,
                    thrownExplosiveController,
                    smokeFieldSession,
                    fireFieldSession,
                    actorId,
                    scenarioAssembly.RandomSeed,
                    (installedEmergencyCycle, installedConsumables) =>
                    {
                        emergencyCycle = installedEmergencyCycle;
                        consumableController = installedConsumables;
                    });

            IGameplayFeatureInstaller[] installers =
            {
                new GameplayTargetingFeatureInstaller(
                    session,
                    worldRegistry,
                    smokeFieldSession,
                    targetAcquisitionPresenter,
                    displacementController,
                    destructibleController,
                    levelWorld,
                    scenarioAssembly,
                    dialogueLog,
                    sessionPresenter,
                    turnMovementController,
                    worldStart.MovementInput,
                    inputController,
                    player,
                    hud,
                    actorId,
                    content.Level.traversalLinks,
                    scenarioAssembly.RandomSeed,
                    IsPointerOverGameplayInterface),
                new GameplayActorActionsFeatureInstaller(
                    session,
                    player,
                    actionController,
                    sessionPresenter,
                    attackController,
                    targetAcquisitionPresenter,
                    dialogueLog,
                    destructibleController,
                    surfaceImpactPresenter,
                    worldRegistry,
                    surfacePresentationCatalog,
                    levelWorld.Root.transform,
                    equipmentController,
                    scenarioAssembly,
                    smokeFieldSession,
                    actorId,
                    scenarioAssembly.PrimaryObjectiveId,
                    TryUseEquippedItemPower,
                    CanRequestHotbarPower),
                deliveryInstaller,
                new GameplayHotbarFeatureInstaller(
                    hotbarController,
                    session,
                    actorId,
                    CreateActorAbilityHotbarDefinitions(
                        scenarioAssembly.GetActorDefinition(actorId)
                            .DisplacementAbility,
                        HasControlledDrone(actorId)),
                    equipmentController.TryActivateItem,
                    TryActivateActorAbility,
                    CanActivateHotbarBinding,
                    CancelPendingHotbarActions),
                new GameplayEncounterActorsFeatureInstaller(
                    session,
                    () => emergencyCycle,
                    actionController,
                    projectileController,
                    scenarioAssembly.PlayerParty,
                    worldRegistry,
                    attackController,
                    targetAcquisitionPresenter,
                    enemyController,
                    sessionPresenter,
                    displacementController,
                    partyControl,
                    droneController,
                    dialogueLog,
                    smokeFieldSession,
                    content.Level.traversalLinks,
                    combatReactionPresenter,
                    tacticalTransitionPresenter,
                    installed => partyPresentation = installed),
                new GameplayAimingFeatureInstaller(
                    session,
                    actorId,
                    targetAcquisitionPresenter,
                    projectileController,
                    weaponTargetingController,
                    targetingCursorPresenter,
                    () => partyPresentation?.SelectedWeapon?.Muzzle,
                    ConfirmArmedWeaponFire,
                    ShouldShowTargetingCursor,
                    ResolveTargetingCursorValidity),
                new GameplayObjectiveFeatureInstaller(
                    session,
                    objectivePresenter,
                    scenarioAssembly.PrimaryObjectiveId),
                new GameplayControlRoutingFeatureInstaller(
                    session,
                    hud,
                    partyHud,
                    inputController,
                    actionController,
                    attackController,
                    projectileController,
                    equipmentController,
                    hotbarController,
                    () => consumableController,
                    sessionPresenter,
                    cameraRig,
                    partyControl,
                    targetAcquisitionPresenter,
                    displacementController,
                    weaponTargetingController,
                    IsPointerOverGameplayInterface,
                    ReadPointer,
                    () => Camera.main,
                    HandlePartyControlChanged,
                    ApplyPartyControl,
                    installed => controlRouter = installed),
                new GameplayReplayFeatureInstaller(
                    session,
                    turnReplayHud,
                    turnReplayWorldPresenter,
                    inputController,
                    partyControl,
                    destructibleController,
                    GetVehicleMomentumSessions,
                    projectileController,
                    smokeFieldSession,
                    worldRegistry,
                    vehicleController,
                    smokeFieldController,
                    partyHud,
                    installed => turnReplayStateTimeline = installed),
                new GameplayHudFeatureInstaller(
                    hud,
                    inputController,
                    () => controlRouter,
                    ExportBugReport),
            };

            new GameplayFeatureInstallationPipeline(
                    installers,
                    ResetPresentationBindings)
                .InstallAll();
        }
        private IReadOnlyList<VehicleMomentumSession> GetVehicleMomentumSessions()
        {
            var sessions = new List<VehicleMomentumSession>(
                scenarioAssembly.Vehicles.Count);
            foreach (ScenarioVehicleRuntimeDefinition vehicle in
                scenarioAssembly.Vehicles)
                sessions.Add(vehicleController.GetSession(vehicle.EntityId));
            return sessions.AsReadOnly();
        }

        private readonly struct GameplayWorldStart
        {
            public GameplayWorldStart(
                GameplayJournal journal,
                string initiallySelectedActorId,
                ExplorationMovementInput movementInput)
            {
                Journal = journal ?? throw new ArgumentNullException(nameof(journal));
                InitiallySelectedActorId = string.IsNullOrWhiteSpace(initiallySelectedActorId)
                    ? throw new ArgumentException(
                        "An initially selected actor is required.",
                        nameof(initiallySelectedActorId))
                    : initiallySelectedActorId;
                MovementInput = movementInput
                    ?? throw new ArgumentNullException(nameof(movementInput));
            }

            public GameplayJournal Journal { get; }

            public string InitiallySelectedActorId { get; }

            public ExplorationMovementInput MovementInput { get; }
        }

        public void EndSession()
        {
            ResetPresentationBindings();
            if (partyControl != null)
            {
                partyControl.ControlChanged -= HandlePartyControlChanged;
            }
            partyPresentation?.Dispose();
            partyPresentation = null;
            partyControl?.Dispose();
            partyControl = null;
            partyPersistence?.Dispose();
            partyPersistence = null;
            consumableController?.CancelPending();
            consumableController = null;
            smokeFieldSession?.Dispose();
            smokeFieldSession = null;
            fireFieldSession?.Dispose();
            fireFieldSession = null;
            postProcessing?.Dispose();
            postProcessing = null;
            environmentStyle?.Dispose();
            environmentStyle = null;
            environmentLighting?.Dispose();
            environmentLighting = null;
            dressingProjector?.Dispose();
            dressingProjector = null;
            surfacePresentationCatalog = null;
            visualTheme = null;
            levelWorld?.Dispose();
            levelWorld = null;
            cameraRig?.Dispose();
            cameraRig = null;
            worldRegistry?.Dispose();
            worldRegistry = null;
            player = null;
            scenarioAssembly = null;
            content = null;
            dialogueLog = null;
            enabled = false;
        }

        internal bool TrySelectPartyActor(
            string actorId,
            out GameplayPartySelectionFailure failure)
        {
            if (partyControl == null)
            {
                failure = GameplayPartySelectionFailure.NotPartyMember;
                return false;
            }

            return partyControl.TrySelectActor(actorId, out failure);
        }

        private void HandlePartyControlChanged(
            GameplayPartyControlSnapshot control) =>
            ApplyPartyControl(control);

        private void ApplyPartyControl(GameplayPartyControlSnapshot control)
        {
            if (partyPresentation == null
                || string.IsNullOrWhiteSpace(control.SelectedActorId))
            {
                return;
            }

            CancelPendingHotbarActions();
            GameplayActorView previousView = partyPresentation.SelectedView;
            GameplayActorView selectedView = worldRegistry.GetActor(
                control.SelectedActorId);
            if (selectedView.Motor == null
                || selectedView.MovementInput == null)
            {
                throw new InvalidOperationException(
                    $"Party actor '{control.SelectedActorId}' requires movement components.");
            }

            previousView.MovementInput?.BindInputSource(null);
            cameraRig.SetTarget(
                selectedView.Transform,
                selectedView.MovementInput);
            targetAcquisitionPresenter.SetObserver(control.SelectedActorId);
            partyPresentation.SetSelectedActor(control.SelectedActorId);
            selectedView.MovementInput.BindInputSource(inputController);
            sessionPresenter.SetActor(
                selectedView.MovementInput,
                selectedView.Transform,
                control.SelectedActorId);
            turnMovementController.SetActor(
                selectedView.MovementInput,
                selectedView.Motor,
                control.SelectedActorId);
            actionController.SetActor(
                partyPresentation.SelectedAnimationCoordinator,
                control.SelectedActorId);
            attackController.SetActor(control.SelectedActorId);
            equipmentController.SetActor(control.SelectedActorId);
            projectileController.SetActor(control.SelectedActorId);
            thrownExplosiveController.SetActor(control.SelectedActorId);
            displacementController.SetActor(control.SelectedActorId);
            DisplacementAbilityDefinition ability = scenarioAssembly
                .GetActorDefinition(control.SelectedActorId)
                .DisplacementAbility;
            hotbarController.SetActor(
                control.SelectedActorId,
                CreateActorAbilityHotbarDefinitions(
                    ability,
                    HasControlledDrone(control.SelectedActorId)));
            weaponTargetingController.SetActor(control.SelectedActorId);
            hud.SetActor(control.SelectedActorId);
            player = selectedView.Motor;
        }

        private static Vector2 ReadPointer() => Mouse.current == null
            ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
            : Mouse.current.position.ReadValue();

        private bool IsPointerOverGameplayInterface(Vector2 pointer) =>
            (hud?.ContainsInteractiveScreenPoint(pointer) ?? false)
            || (partyHud?.ContainsInteractiveScreenPoint(pointer) ?? false)
            || (turnReplayHud?.ContainsInteractiveScreenPoint(pointer) ?? false)
            || (dialogueDrawer?.ContainsInteractiveScreenPoint(pointer)
                ?? false);

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                partyPersistence?.Flush();
        }

        private void OnApplicationQuit()
        {
            partyPersistence?.Flush();
        }

        private void TryUseEquippedItemPower(string itemId)
        {
            string actorId = partyControl?.CommandActorId;
            if (string.IsNullOrWhiteSpace(actorId))
                return;

            InventoryItemDefinition item = Session.GetInventoryItem(
                actorId,
                itemId);
            if (item.ConsumablePower != null)
            {
                consumableController.TryToggle(
                    actorId,
                    itemId);
                return;
            }

            if (!string.Equals(
                    Session.GetActor(actorId).EquippedItemId,
                    itemId,
                    StringComparison.Ordinal))
            {
                return;
            }

            weaponTargetingController?.ToggleTargeting();
        }

        private bool CanRequestHotbarPower(string itemId)
        {
            string actorId = partyControl?.CommandActorId;
            if (string.IsNullOrWhiteSpace(actorId))
                return false;

            if (consumableController == null
                || !consumableController.IsPending)
            {
                return true;
            }

            InventoryItemDefinition item = Session.GetInventoryItem(
                actorId,
                itemId);
            return item.ConsumablePower != null;
        }

        private bool CanActivateHotbarBinding(GameplayHotbarBinding binding)
        {
            string actorId = partyControl?.CommandActorId;
            if (string.IsNullOrWhiteSpace(actorId))
                return false;

            if (displacementController != null
                && displacementController.IsTargeting)
            {
                return binding.Kind == GameplayHotbarBindingKind.ActorAbility;
            }

            if (droneController != null && droneController.IsTargeting)
                return binding.Kind == GameplayHotbarBindingKind.ActorAbility;

            if (weaponTargetingController != null
                && weaponTargetingController.IsTargeting)
            {
                return binding.Kind == GameplayHotbarBindingKind.InventoryItem
                    && string.Equals(
                        binding.ContentId,
                        Session.GetActor(actorId).EquippedItemId,
                        StringComparison.Ordinal);
            }

            if (consumableController == null
                || !consumableController.IsPending)
            {
                return true;
            }

            if (binding.Kind != GameplayHotbarBindingKind.InventoryItem)
            {
                return false;
            }

            InventoryItemDefinition item = Session.GetInventoryItem(
                actorId,
                binding.ContentId);
            return item?.ConsumablePower != null;
        }

        private bool TryActivateActorAbility(
            string abilityId,
            string optionId)
        {
            string actorId = partyControl?.CommandActorId;
            if (string.IsNullOrWhiteSpace(actorId))
                return false;

            if (string.Equals(
                    abilityId,
                    GameplayCoreActorAbilities.StanceId,
                    StringComparison.Ordinal)
                && optionId == null)
            {
                CancelPendingHotbarActions();
                return sessionPresenter.ToggleStance();
            }

            if (string.Equals(
                    abilityId,
                    GameplayDroneController.AbilityId,
                    StringComparison.Ordinal))
            {
                equipmentController?.CancelPending();
                displacementController?.CancelTargeting();
                weaponTargetingController?.CancelTargeting();
                return droneController != null
                    && droneController.TryToggle(actorId, optionId);
            }

            DisplacementAbilityDefinition displacementAbility =
                scenarioAssembly.GetActorDefinition(
                    actorId)
                    .DisplacementAbility;
            if (displacementAbility == null
                || !string.Equals(
                    displacementAbility.Id,
                    abilityId,
                    StringComparison.Ordinal)
                || !Session.TryGetDisplacementAction(
                    actorId,
                    optionId,
                    out _))
            {
                return false;
            }

            equipmentController?.CancelPending();
            return displacementController.TryToggleTargeting(optionId);
        }

        private void CancelPendingHotbarActions()
        {
            displacementController?.CancelTargeting();
            droneController?.CancelTargeting();
            weaponTargetingController?.CancelTargeting();
            hotbarController?.CloseActorAbilityFlyout();
            consumableController?.CancelPending();
            equipmentController?.CancelPending();
        }

        private bool ConfirmArmedWeaponFire()
        {
            Vector2 pointer = Mouse.current == null
                ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
                : Mouse.current.position.ReadValue();
            if (IsPointerOverGameplayInterface(pointer))
                return false;
            targetAcquisitionPresenter?.RefreshAtScreenPoint(
                Camera.main,
                pointer);

            if (projectileController != null
                && projectileController.HasProjectileWeapon)
            {
                return projectileController.TryLaunch();
            }

            if (attackController != null && attackController.TryAttack())
                return true;
            return droneController != null
                && !string.IsNullOrWhiteSpace(partyControl?.CommandActorId)
                && Camera.main != null
                && droneController.TryAttackDroneAtPointer(
                    partyControl.CommandActorId,
                    Camera.main.ScreenPointToRay(pointer));
        }

        private bool ShouldShowTargetingCursor()
        {
            Vector2 pointer = Mouse.current == null
                ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
                : Mouse.current.position.ReadValue();
            if (IsPointerOverGameplayInterface(pointer))
                return false;
            return CanHoverAttackPointerTarget()
                || weaponTargetingController?.IsTargeting == true
                || displacementController?.IsTargeting == true
                || consumableController?.IsPending == true;
        }

        private bool? ResolveTargetingCursorValidity()
        {
            if (targetAcquisitionPresenter != null
                && targetAcquisitionPresenter.TryGetPointerFeedback(
                    out TargetingPointerFeedback feedback))
            {
                return feedback.IsValid;
            }

            return null;
        }

        private bool CanHoverAttackPointerTarget() =>
            targetAcquisitionPresenter?.HasPointerTarget == true
            && Session != null
            && scenarioAssembly != null
            && !string.IsNullOrWhiteSpace(partyControl?.CommandActorId)
            && Session.GetEquippedAttack(partyControl.CommandActorId)
                != null;

        private void ExportBugReport(string playerNote)
        {
            try
            {
                string status = GameplayBugReportExporter.Export(
                    Session,
                    turnMovementController,
                    hud.CurrentGuidanceEntry,
                    playerNote,
                    partyControl?.Snapshot);
                hud.SetBugReportStatus(status);
            }
            catch (Exception exception)
            {
                string message = "Bug report export failed: " + exception.Message;
                hud.SetBugReportStatus(message);
                Debug.LogException(exception);
            }
        }

        private void ExportDialogue()
        {
            try
            {
                string status = GameplayDialogueExporter.Export(dialogueLog);
                dialogueDrawer.SetExportStatus(status);
            }
            catch (Exception exception)
            {
                dialogueDrawer.SetExportStatus(
                    "Dialogue export failed: " + exception.Message);
                Debug.LogException(exception);
            }
        }

    }
}
