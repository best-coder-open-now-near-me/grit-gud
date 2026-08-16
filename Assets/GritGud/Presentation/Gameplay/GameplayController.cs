using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Bootstrap;
using GritGud.Presentation.Levels;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GritGud.Presentation.Gameplay
{
    public sealed class GameplayController : MonoBehaviour
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
        private GameplayConsumableController consumableController;
        private GameplayPartyControlSession partyControl;
        private GameplayPartyProgressionSession partyProgression;
        private GameplayPartyPresentationSession partyPresentation;
        private GameplayWeaponTargetingController weaponTargetingController;
        private GameplayTargetingCursorPresenter targetingCursorPresenter;
        private GameplayObjectivePresenter objectivePresenter;
        private TargetAcquisitionPresenter targetAcquisitionPresenter;
        private GameplayDestructibleController destructibleController;
        private GameplayDisplacementController displacementController;
        private GameplayVehicleController vehicleController;
        private GameplayDialogueLog dialogueLog;
        private GameplayCharacterGroundingPresenter characterGroundingPresenter;
        private GameplayTacticalTransitionPresenter tacticalTransitionPresenter;
        private GameplaySurfaceImpactPresenter surfaceImpactPresenter;

        public bool IsRunning => levelWorld != null && player != null;

        public GameplaySession Session => sessionPresenter?.Session;

        public GameplayDialogueLog DialogueLog => dialogueLog;

        internal GameplayPartyControlSession PartyControl => partyControl;

        internal GameplayPartyProgressionSession PartyProgression =>
            partyProgression;

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
            characterGroundingPresenter =
                GetOrAddComponent<GameplayCharacterGroundingPresenter>();
            tacticalTransitionPresenter =
                GetOrAddComponent<GameplayTacticalTransitionPresenter>();
            surfaceImpactPresenter =
                GetOrAddComponent<GameplaySurfaceImpactPresenter>();
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
            hud?.Hide();
            partyHud?.Unbind();
            turnReplayHud?.Unbind();
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
            objectivePresenter?.Unbind();
            targetAcquisitionPresenter?.Unbind();
            destructibleController?.Unbind();
            displacementController?.Unbind();
            vehicleController?.Unbind();
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
                BindGameplaySystems(session, worldStart);
                ShowGameplayInterface(session);
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
            var session = new GameplaySession(
                scenarioAssembly.Scenario,
                worldStart.Journal,
                scenarioAssembly.RandomSeed);
            partyControl = new GameplayPartyControlSession(session);
            partyProgression = new GameplayPartyProgressionSession(session);
            smokeFieldSession = new GameplaySmokeFieldSession(session);
            smokeFieldController.Bind(smokeFieldSession);
            tacticalTransitionPresenter.Bind(session, visualTheme);
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
            sessionPresenter.Bind(
                session,
                worldStart.MovementInput,
                player.transform,
                worldStart.InitiallySelectedActorId);
            hud.BindSession(
                session,
                worldStart.InitiallySelectedActorId,
                scenarioAssembly);
            dialogueLog = new GameplayDialogueLog();
            dialogueLog.AppendCombatDiagnostic(
                GameplayCombatDiagnosticFormatter.FormatInitiative(session));
            dialogueDrawer.Bind(dialogueLog, ExportDialogue);
            dialogueDrawer.Show();

            cameraRig = GameplayCameraRig.Create(
                player.transform,
                worldStart.MovementInput,
                inputController);

            return session;
        }

        private void BindGameplaySystems(
            GameplaySession session,
            GameplayWorldStart worldStart)
        {
            string initiallySelectedActorId = worldStart.InitiallySelectedActorId;
            uint attackRandomSeed = GameplayRandomStreams.DeriveSeed(
                scenarioAssembly.RandomSeed,
                GameplayRandomStreams.AttackResolution);
            uint displacementRandomSeed = GameplayRandomStreams.DeriveSeed(
                scenarioAssembly.RandomSeed,
                GameplayRandomStreams.DisplacementControl);
            uint thrownExplosiveRandomSeed = GameplayRandomStreams.DeriveSeed(
                scenarioAssembly.RandomSeed,
                GameplayRandomStreams.ThrownExplosiveUncertainty);
            targetAcquisitionPresenter.Bind(
                session,
                worldRegistry,
                initiallySelectedActorId,
                smokeFieldSession);
            targetAcquisitionPresenter.SetPointerBlocker(
                IsPointerOverGameplayInterface);
            displacementController.Bind(
                session,
                destructibleController,
                levelWorld,
                worldRegistry,
                scenarioAssembly,
                displacementRandomSeed,
                targetAcquisitionPresenter,
                dialogueLog,
                sessionPresenter.TryBeginEncounterFromAction);
            turnMovementController.Bind(
                session,
                worldStart.MovementInput,
                inputController,
                player,
                initiallySelectedActorId);
            hud.BindTurnMovement(turnMovementController);
            ActorAnimationCoordinator animationCoordinator =
                player.GetComponent<ActorAnimationCoordinator>();
            actionController.Bind(
                session,
                sessionPresenter,
                animationCoordinator,
                initiallySelectedActorId,
                scenarioAssembly.PrimaryObjectiveId);
            attackController.Bind(
                session,
                targetAcquisitionPresenter,
                dialogueLog,
                initiallySelectedActorId,
                attackRandomSeed,
                sessionPresenter.TryBeginEncounterFromAction);
            surfaceImpactPresenter.Bind(
                attackController,
                worldRegistry,
                surfacePresentationCatalog,
                levelWorld.Root.transform);
            equipmentController.Bind(
                session,
                initiallySelectedActorId,
                TryUseEquippedItemPower,
                CanRequestHotbarPower);
            var blastWorldQuery = new UnityBlastWorldQuery(
                worldRegistry,
                () => session.Journal.LastEntry?.Sequence ?? 0L,
                propId => destructibleController.Session.TryGetProp(
                    propId,
                    out _));
            var blastConsequences = new GameplayBlastConsequenceResolver(
                session,
                destructibleController.Session);
            var emergencyCycle = new GameplayEmergencyCycleSession(session);
            projectileController.Bind(
                session,
                worldRegistry,
                blastWorldQuery,
                blastConsequences,
                targetAcquisitionPresenter,
                dialogueLog,
                initiallySelectedActorId,
                onTurnModeStartRequested: actionController.TryEnterTurnMode,
                onEncounterStartRequested:
                    sessionPresenter.TryBeginEncounterFromAction,
                emergencyCycle: emergencyCycle);
            thrownExplosiveController.Bind(
                session,
                worldRegistry,
                blastWorldQuery,
                blastConsequences,
                targetAcquisitionPresenter,
                dialogueLog,
                initiallySelectedActorId,
                thrownExplosiveRandomSeed,
                sessionPresenter.TryBeginEncounterFromAction,
                smokeFieldSession: smokeFieldSession);
            consumableController = new GameplayConsumableController(
                session,
                thrownExplosiveController);
            hotbarController.Bind(
                session,
                initiallySelectedActorId,
                CreateActorAbilityHotbarDefinitions(
                    scenarioAssembly.GetActorDefinition(
                        initiallySelectedActorId)
                        .DisplacementAbility),
                equipmentController.TryActivateItem,
                TryActivateActorAbility,
                CanActivateHotbarBinding,
                CancelPendingHotbarActions);
            actionController.BindEmergencyCycle(emergencyCycle);
            actionController.RegisterTurnModeExitConstraint(
                projectileController);
            partyPresentation = new GameplayPartyPresentationSession(
                session,
                scenarioAssembly.PlayerParty,
                worldRegistry,
                attackController,
                projectileController,
                targetAcquisitionPresenter);
            enemyController.Bind(
                session,
                worldRegistry,
                sessionPresenter,
                actionController,
                attackController,
                projectileController,
                emergencyCycle,
                partyControl,
                dialogueLog,
                sessionPresenter.TryBeginEncounter,
                obscuranceQuery: smokeFieldSession);
            targetAcquisitionPresenter.SetWeaponAimOriginProvider(
                () => partyPresentation?.SelectedWeapon?.Muzzle != null
                    ? partyPresentation.SelectedWeapon.Muzzle.position
                    : (Vector3?)null);
            projectileController.BindVisualLaunchOrigin(
                () => partyPresentation?.SelectedWeapon?.Muzzle != null
                    ? partyPresentation.SelectedWeapon.Muzzle.position
                    : (Vector3?)null);
            weaponTargetingController.Bind(
                session,
                initiallySelectedActorId,
                ConfirmArmedWeaponFire,
                active =>
                {
                    if (targetAcquisitionPresenter != null)
                    {
                        targetAcquisitionPresenter.SetWeaponTargetingActive(
                            active);
                    }
                });
            targetingCursorPresenter.Bind(ShouldShowTargetingCursor);
            if (!string.IsNullOrWhiteSpace(scenarioAssembly.PrimaryObjectiveId))
            {
                objectivePresenter.Bind(
                    session,
                    scenarioAssembly.PrimaryObjectiveId);
            }
        }

        private void ShowGameplayInterface(GameplaySession session)
        {
            hud.BindGameplayActions(actionController);
            hud.BindGameplayAttack(attackController);
            hud.BindGameplayEquipment(equipmentController);
            hud.BindGameplayHotbar(hotbarController);
            hud.BindGameplayConsumables(consumableController);
            hud.BindGameplayProjectile(projectileController);
            hud.BindGameplayDisplacement(displacementController);
            hud.BindGameplayWeaponTargeting(weaponTargetingController);
            partyControl.ControlChanged += HandlePartyControlChanged;
            ApplyPartyControl(partyControl.Snapshot);
            Action toggleTurnMode = () =>
                HandleGameplayControl(GameplayControl.ToggleTurnMode);
            inputController.Begin(HandleGameplayControl);
            turnReplayHud.Bind(session, partyControl);
            partyHud.Bind(
                session,
                partyControl,
                inputController,
                () => turnReplayHud.IsAvailable,
                turnReplayHud.Toggle);
            hud.BindInputSource(inputController);
            hud.BindTurnModeToggle(toggleTurnMode);
            hud.BindBugReportExport(ExportBugReport);
            hud.Show();
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
            partyProgression = null;
            consumableController?.CancelPending();
            consumableController = null;
            smokeFieldSession?.Dispose();
            smokeFieldSession = null;
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
                CreateActorAbilityHotbarDefinitions(ability));
            weaponTargetingController.SetActor(control.SelectedActorId);
            hud.SetActor(control.SelectedActorId);
            player = selectedView.Motor;
        }

        private void HandleGameplayControl(GameplayControl control)
        {
            if (hud != null && hud.IsBugReportNoteOpen)
            {
                if (control == GameplayControl.CancelPendingAction)
                    hud.CancelBugReportNote();
                return;
            }

            bool hotbarControl = control >= GameplayControl.Hotbar1
                && control <= GameplayControl.CancelPendingAction;
            if (!hotbarControl)
            {
                equipmentController?.ClearStatus();
                hotbarController?.ClearStatus();
            }

            if (control != GameplayControl.Attack)
            {
                attackController?.ClearStatus();
                projectileController?.ClearStatus();
            }

            switch (control)
            {
                case GameplayControl.ToggleTurnMode:
                    if (Session.Mode == GameplaySessionMode.Exploration)
                    {
                        actionController.TryEnterTurnMode();
                    }
                    else
                    {
                        actionController.TryExitTurnMode();
                    }

                    break;
                case GameplayControl.Attack:
                {
                    Vector2 pointer = Mouse.current == null
                        ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
                        : Mouse.current.position.ReadValue();
                    if (IsPointerOverGameplayInterface(pointer))
                    {
                        break;
                    }

                    targetAcquisitionPresenter?.RefreshAtScreenPoint(
                        Camera.main,
                        pointer);

                    if (displacementController != null
                        && displacementController.IsTargeting)
                    {
                        displacementController.TryConfirmTargeting();
                    }
                    else if (consumableController != null
                        && consumableController.IsPending)
                    {
                        consumableController.TryConfirmPending();
                    }
                    else if (weaponTargetingController != null
                        && weaponTargetingController.IsTargeting)
                    {
                        weaponTargetingController.ConfirmTargeting();
                    }
                    else
                    {
                        weaponTargetingController?.BeginTargeting();
                    }
                    break;
                }
                case GameplayControl.ToggleStance:
                    sessionPresenter.ToggleStance();
                    break;
                case GameplayControl.ToggleCameraView:
                    cameraRig.ToggleView();
                    break;
                case GameplayControl.ExportBugReport:
                    hud.OpenBugReportNote();
                    break;
                case GameplayControl.Interact:
                    actionController.TryInteract();
                    break;
                case GameplayControl.EndTurn:
                    actionController.TryEndTurn();
                    break;
                case GameplayControl.CyclePartyMember:
                    if (!partyControl.TrySelectNextActor(
                            out GameplayPartySelectionFailure selectionFailure))
                    {
                        partyHud.PresentSelectionFailure(selectionFailure);
                    }
                    break;
                case GameplayControl.Hotbar1:
                case GameplayControl.Hotbar2:
                case GameplayControl.Hotbar3:
                case GameplayControl.Hotbar4:
                case GameplayControl.Hotbar5:
                case GameplayControl.Hotbar6:
                case GameplayControl.Hotbar7:
                case GameplayControl.Hotbar8:
                    int hotbarNumber =
                        ((int)control - (int)GameplayControl.Hotbar1) + 1;
                    if (hotbarController.HasExpandedActorAbility)
                    {
                        hotbarController
                            .TryHandleExpandedActorAbilityHotkey(
                                hotbarNumber);
                    }
                    else
                    {
                        hotbarController.TryActivateSlot(hotbarNumber);
                    }
                    break;
                case GameplayControl.CancelPendingAction:
                    if (hotbarController != null
                        && hotbarController.CloseActorAbilityFlyout())
                    {
                        break;
                    }
                    if (displacementController != null
                        && displacementController.CancelTargeting())
                    {
                        break;
                    }
                    if (weaponTargetingController != null
                        && weaponTargetingController.CancelTargeting())
                    {
                        break;
                    }
                    if (consumableController == null
                        || !consumableController.CancelPending())
                    {
                        equipmentController.CancelPending();
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(control));
            }
        }

        private bool IsPointerOverGameplayInterface(Vector2 pointer) =>
            (hud?.ContainsInteractiveScreenPoint(pointer) ?? false)
            || (partyHud?.ContainsInteractiveScreenPoint(pointer) ?? false)
            || (turnReplayHud?.ContainsInteractiveScreenPoint(pointer) ?? false)
            || (dialogueDrawer?.ContainsInteractiveScreenPoint(pointer)
                ?? false);

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

        private static IReadOnlyList<GameplayActorAbilityHotbarDefinition>
            CreateActorAbilityHotbarDefinitions(
                DisplacementAbilityDefinition displacementAbility)
        {
            if (displacementAbility == null)
            {
                return Array.Empty<GameplayActorAbilityHotbarDefinition>();
            }

            var options = new List<GameplayActorAbilityOptionDefinition>(
                displacementAbility.Actions.Count);
            foreach (DisplacementActionDefinition action in
                displacementAbility.Actions)
            {
                options.Add(new GameplayActorAbilityOptionDefinition(
                    action.Id,
                    action.DisplayName));
            }

            return new[]
            {
                new GameplayActorAbilityHotbarDefinition(
                    displacementAbility.Id,
                    displacementAbility.DisplayName,
                    displacementAbility.HotbarSlot,
                    options),
            };
        }

        private void CancelPendingHotbarActions()
        {
            displacementController?.CancelTargeting();
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

            return attackController != null && attackController.TryAttack();
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
                    partyControl?.Snapshot,
                    partyProgression);
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
