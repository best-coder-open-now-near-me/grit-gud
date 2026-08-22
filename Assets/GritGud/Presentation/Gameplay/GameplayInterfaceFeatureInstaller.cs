using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Levels;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayControlRoutingFeatureInstaller :
        IGameplayFeatureInstaller
    {
        private readonly GameplaySession session;
        private readonly GameplayHud hud;
        private readonly GameplayPartyHud partyHud;
        private readonly GameplayInputController input;
        private readonly GameplayActionController actions;
        private readonly GameplayAttackController attacks;
        private readonly GameplayProjectileController projectiles;
        private readonly GameplayEquipmentController equipment;
        private readonly GameplayHotbarController hotbar;
        private readonly Func<GameplayConsumableController> resolveConsumables;
        private readonly GameplaySessionPresenter sessionPresenter;
        private readonly GameplayCameraRig cameraRig;
        private readonly GameplayPartyControlSession partyControl;
        private readonly TargetAcquisitionPresenter targets;
        private readonly GameplayDisplacementController displacement;
        private readonly GameplayWeaponTargetingController weaponTargeting;
        private readonly Func<Vector2, bool> pointerBlocker;
        private readonly Func<Vector2> readPointer;
        private readonly Func<Camera> resolveCamera;
        private readonly Action<GameplayPartyControlSnapshot> partyControlChanged;
        private readonly Action<GameplayPartyControlSnapshot> applyPartyControl;
        private readonly Action<GameplayControlRouter> captureControlRouter;

        public GameplayControlRoutingFeatureInstaller(
            GameplaySession session,
            GameplayHud hud,
            GameplayPartyHud partyHud,
            GameplayInputController input,
            GameplayActionController actions,
            GameplayAttackController attacks,
            GameplayProjectileController projectiles,
            GameplayEquipmentController equipment,
            GameplayHotbarController hotbar,
            Func<GameplayConsumableController> resolveConsumables,
            GameplaySessionPresenter sessionPresenter,
            GameplayCameraRig cameraRig,
            GameplayPartyControlSession partyControl,
            TargetAcquisitionPresenter targets,
            GameplayDisplacementController displacement,
            GameplayWeaponTargetingController weaponTargeting,
            Func<Vector2, bool> pointerBlocker,
            Func<Vector2> readPointer,
            Func<Camera> resolveCamera,
            Action<GameplayPartyControlSnapshot> partyControlChanged,
            Action<GameplayPartyControlSnapshot> applyPartyControl,
            Action<GameplayControlRouter> captureControlRouter)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.hud = hud ?? throw new ArgumentNullException(nameof(hud));
            this.partyHud = partyHud ?? throw new ArgumentNullException(nameof(partyHud));
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
            this.attacks = attacks ?? throw new ArgumentNullException(nameof(attacks));
            this.projectiles = projectiles ?? throw new ArgumentNullException(
                nameof(projectiles));
            this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            this.hotbar = hotbar ?? throw new ArgumentNullException(nameof(hotbar));
            this.resolveConsumables = resolveConsumables
                ?? throw new ArgumentNullException(nameof(resolveConsumables));
            this.sessionPresenter = sessionPresenter
                ?? throw new ArgumentNullException(nameof(sessionPresenter));
            this.cameraRig = cameraRig ?? throw new ArgumentNullException(nameof(cameraRig));
            this.partyControl = partyControl ?? throw new ArgumentNullException(
                nameof(partyControl));
            this.targets = targets ?? throw new ArgumentNullException(nameof(targets));
            this.displacement = displacement ?? throw new ArgumentNullException(
                nameof(displacement));
            this.weaponTargeting = weaponTargeting
                ?? throw new ArgumentNullException(nameof(weaponTargeting));
            this.pointerBlocker = pointerBlocker ?? throw new ArgumentNullException(
                nameof(pointerBlocker));
            this.readPointer = readPointer ?? throw new ArgumentNullException(
                nameof(readPointer));
            this.resolveCamera = resolveCamera ?? throw new ArgumentNullException(
                nameof(resolveCamera));
            this.partyControlChanged = partyControlChanged
                ?? throw new ArgumentNullException(nameof(partyControlChanged));
            this.applyPartyControl = applyPartyControl
                ?? throw new ArgumentNullException(nameof(applyPartyControl));
            this.captureControlRouter = captureControlRouter
                ?? throw new ArgumentNullException(nameof(captureControlRouter));
        }

        public GameplayFeatureStage Stage => GameplayFeatureStage.ControlRouting;

        public void Install()
        {
            GameplayConsumableController consumables = resolveConsumables()
                ?? throw new InvalidOperationException(
                    "Consumable delivery must install before control routing.");
            hud.BindGameplayActions(actions);
            hud.BindGameplayAttack(attacks);
            hud.BindGameplayEquipment(equipment);
            hud.BindGameplayHotbar(hotbar);
            hud.BindGameplayConsumables(consumables);
            hud.BindGameplayProjectile(projectiles);
            hud.BindGameplayDisplacement(displacement);
            hud.BindGameplayWeaponTargeting(weaponTargeting);
            partyControl.ControlChanged += partyControlChanged;
            applyPartyControl(partyControl.Snapshot);
            var controlRouter = new GameplayControlRouter(
                session,
                hud,
                partyHud,
                actions,
                attacks,
                projectiles,
                equipment,
                hotbar,
                consumables,
                sessionPresenter,
                cameraRig,
                partyControl,
                targets,
                displacement,
                weaponTargeting,
                pointerBlocker,
                readPointer,
                resolveCamera);
            captureControlRouter(controlRouter);
            input.Begin(controlRouter.Handle);
        }
    }

    internal sealed class GameplaySemanticRuntimeFeatureInstaller :
        IGameplayFeatureInstaller
    {
        private readonly Action install;

        public GameplaySemanticRuntimeFeatureInstaller(Action installRuntime)
        {
            install = installRuntime ?? throw new ArgumentNullException(
                nameof(installRuntime));
        }

        public GameplayFeatureStage Stage =>
            GameplayFeatureStage.SemanticRuntime;

        public void Install() => install();
    }

    internal sealed class GameplayReplayFeatureInstaller :
        IGameplayFeatureInstaller
    {
        private readonly GameplaySession session;
        private readonly GameplayHud gameplayHud;
        private readonly GameplayTurnReplayHud replayHud;
        private readonly GameplayTurnReplayWorldPresenter replayWorld;
        private readonly GameplayReplayTranscriptPresenter replayTranscript;
        private readonly GameplayDialogueDrawer dialogueDrawer;
        private readonly GameplayDialogueLog liveDialogue;
        private readonly Action exportLiveDialogue;
        private readonly GameplayInputController input;
        private readonly GameplayPartyControlSession partyControl;
        private readonly GameplayDestructibleController destructibles;
        private readonly GameplayProjectileController projectiles;
        private readonly GameplayThrownExplosiveController thrownExplosives;
        private readonly GameplayWorldRegistry worldRegistry;
        private readonly GameplayVehicleController vehicles;
        private readonly GameplayDroneController drones;
        private readonly GameplaySmokeFieldController smokeFieldPresenter;
        private readonly GameplayFireFieldController fireFieldPresenter;
        private readonly GameplayPartyHud partyHud;
        private readonly GameplayBattleReplayController battleReplay;
        private readonly bool simulationViewer;
        private readonly GameplayBattleReplayPreparationResult<
            GameplayBattleArtifact,
            GameplaySemanticReplayTimeline> preparedSimulation;
        private readonly IReadOnlyList<Behaviour> liveBehaviours;
        private readonly GameplayEnemyController enemies;
        private readonly Func<GameplayLiveSessionRuntime> resolveRuntime;

        public GameplayReplayFeatureInstaller(
            GameplaySession session,
            GameplayHud gameplayHud,
            GameplayTurnReplayHud replayHud,
            GameplayTurnReplayWorldPresenter replayWorld,
            GameplayReplayTranscriptPresenter replayTranscriptPresenter,
            GameplayDialogueDrawer gameplayDialogueDrawer,
            GameplayDialogueLog liveDialogueLog,
            Action onExportLiveDialogue,
            GameplayInputController input,
            GameplayPartyControlSession partyControl,
            GameplayDestructibleController destructibles,
            GameplayProjectileController projectiles,
            GameplayThrownExplosiveController thrownExplosives,
            GameplayWorldRegistry worldRegistry,
            GameplayVehicleController vehicles,
            GameplayDroneController drones,
            GameplaySmokeFieldController smokeFieldPresenter,
            GameplayFireFieldController fireFieldPresenter,
            GameplayPartyHud partyHud,
            GameplayBattleReplayController battleReplayController,
            bool asSimulationViewer,
            GameplayBattleReplayPreparationResult<
                GameplayBattleArtifact,
                GameplaySemanticReplayTimeline> preparedReplay,
            GameplayEnemyController enemyController,
            IEnumerable<Behaviour> behavioursToSuspend,
            Func<GameplayLiveSessionRuntime> resolveRuntime)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.gameplayHud = gameplayHud ?? throw new ArgumentNullException(
                nameof(gameplayHud));
            this.replayHud = replayHud ?? throw new ArgumentNullException(nameof(replayHud));
            this.replayWorld = replayWorld ?? throw new ArgumentNullException(
                nameof(replayWorld));
            replayTranscript = replayTranscriptPresenter
                ?? throw new ArgumentNullException(
                    nameof(replayTranscriptPresenter));
            dialogueDrawer = gameplayDialogueDrawer
                ?? throw new ArgumentNullException(
                    nameof(gameplayDialogueDrawer));
            liveDialogue = liveDialogueLog ?? throw new ArgumentNullException(
                nameof(liveDialogueLog));
            exportLiveDialogue = onExportLiveDialogue;
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.partyControl = partyControl ?? throw new ArgumentNullException(
                nameof(partyControl));
            this.destructibles = destructibles;
            this.projectiles = projectiles;
            this.thrownExplosives = thrownExplosives;
            this.worldRegistry = worldRegistry ?? throw new ArgumentNullException(
                nameof(worldRegistry));
            this.vehicles = vehicles;
            this.drones = drones;
            this.smokeFieldPresenter = smokeFieldPresenter;
            this.fireFieldPresenter = fireFieldPresenter;
            this.partyHud = partyHud ?? throw new ArgumentNullException(nameof(partyHud));
            battleReplay = battleReplayController
                ?? throw new ArgumentNullException(
                    nameof(battleReplayController));
            simulationViewer = asSimulationViewer;
            preparedSimulation = preparedReplay;
            if (simulationViewer
                && (preparedSimulation == null
                    || !preparedSimulation.IsReady))
            {
                throw new ArgumentException(
                    "Simulation replay installation requires prepared content.",
                    nameof(preparedReplay));
            }
            enemies = enemyController ?? throw new ArgumentNullException(
                nameof(enemyController));
            liveBehaviours = new List<Behaviour>(behavioursToSuspend
                ?? throw new ArgumentNullException(
                    nameof(behavioursToSuspend))).AsReadOnly();
            this.resolveRuntime = resolveRuntime
                ?? throw new ArgumentNullException(nameof(resolveRuntime));
        }

        public GameplayFeatureStage Stage =>
            GameplayFeatureStage.ReplayPresentation;

        public void Install()
        {
            GameplayLiveSessionRuntime runtime = resolveRuntime()
                ?? throw new InvalidOperationException(
                    "Semantic runtime must install before replay presentation.");
            replayHud.Bind(
                session,
                runtime,
                simulationViewer
                    ? GameplayReplaySource.VerifiedSimulation
                    : GameplayReplaySource.LiveEncounter);
            replayTranscript.Bind(
                replayHud,
                dialogueDrawer,
                liveDialogue,
                exportLiveDialogue);
            replayWorld.Bind(
                worldRegistry,
                input,
                replayHud,
                projectiles,
                thrownExplosives,
                destructibles,
                vehicles,
                drones,
                smokeFieldPresenter,
                fireFieldPresenter,
                gameplayHud,
                partyHud,
                enemies,
                liveBehaviours);
            if (simulationViewer)
            {
                partyHud.Bind(session, partyControl, input);
                battleReplay.Bind(
                    preparedSimulation,
                    replayHud,
                    input,
                    gameplayHud,
                    partyHud);
            }
            else
            {
                partyHud.Bind(
                    session,
                    partyControl,
                    input,
                    () => replayHud.IsAvailable,
                    replayHud.Toggle,
                    () => replayHud.ActionLabel);
            }
        }
    }

    internal sealed class GameplayHudFeatureInstaller :
        IGameplayFeatureInstaller
    {
        private readonly GameplayHud hud;
        private readonly GameplayInputController input;
        private readonly GameplaySessionPresenter sessionPresenter;
        private readonly Func<GameplayControlRouter> resolveControlRouter;
        private readonly Action<string> exportBugReport;
        private readonly bool showHud;

        public GameplayHudFeatureInstaller(
            GameplayHud hud,
            GameplayInputController input,
            GameplaySessionPresenter presenter,
            Func<GameplayControlRouter> resolveControlRouter,
            Action<string> exportBugReport,
            bool showHud = true)
        {
            this.hud = hud ?? throw new ArgumentNullException(nameof(hud));
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            sessionPresenter = presenter ?? throw new ArgumentNullException(
                nameof(presenter));
            this.resolveControlRouter = resolveControlRouter
                ?? throw new ArgumentNullException(nameof(resolveControlRouter));
            this.exportBugReport = exportBugReport
                ?? throw new ArgumentNullException(nameof(exportBugReport));
            this.showHud = showHud;
        }

        public GameplayFeatureStage Stage => GameplayFeatureStage.HudPresentation;

        public void Install()
        {
            GameplayControlRouter controlRouter = resolveControlRouter()
                ?? throw new InvalidOperationException(
                    "Control routing must install before the gameplay HUD.");
            hud.BindInputSource(input);
            hud.BindWarningHintSource(sessionPresenter);
            hud.BindTurnModeToggle(() =>
                controlRouter.Handle(GameplayControl.ToggleTurnMode));
            hud.BindBugReportExport(exportBugReport);
            if (showHud)
                hud.Show();
            else
                hud.Hide();
        }
    }
}
