using System.Collections;
using System.Linq;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Bootstrap;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayBootstrapCompositionTests
    {
        [UnityTest]
        public IEnumerator MainLevelComposesAuthoredGameplayServicesAndContent()
        {
            using var runtime = new GameplayRuntimeTestHarness();
            yield return runtime.Start();

            GameplayController gameplay = runtime.Gameplay;
            GameplayInputController inputController = runtime.InputController;
            GameplayHud hud = runtime.Hud;
            GameplayPartyHud partyHud = runtime.PartyHud;
            GameplayTurnReplayHud replayHud = runtime.Bootstrap
                .GetComponent<GameplayTurnReplayHud>();
            GameplayDialogueDrawer dialogueDrawer = runtime.DialogueDrawer;
            GameplaySessionPresenter sessionPresenter = runtime.SessionPresenter;
            TurnMovementController turnMovement = runtime.TurnMovement;
            GameplayActionController actions = runtime.Actions;
            GameplayAttackController attacks = runtime.Attacks;
            GameplayEquipmentController equipment = runtime.Equipment;
            GameplayHotbarController hotbar = runtime.Hotbar;
            GameplayObjectivePresenter objectivePresenter =
                runtime.ObjectivePresenter;

            Assert.That(runtime.Bootstrap.CurrentMode,
                Is.EqualTo(ApplicationMode.Gameplay));
            Assert.That(gameplay, Is.Not.Null);
            Assert.That(gameplay.IsRunning, Is.True);
            Assert.That(gameplay.IsSimulationViewer, Is.False);
            Assert.That(
                runtime.Bootstrap.GetComponent<GameplayBattleReplayController>()
                    .enabled,
                Is.False);
            Assert.That(
                replayHud.Source,
                Is.EqualTo(GameplayReplaySource.LiveEncounter));
            Assert.That(RenderSettings.fog, Is.True);
            Assert.That(GameObject.Find("Gameplay Environment Lighting"), Is.Not.Null);
            Assert.That(GameObject.Find("Gameplay Post Processing"), Is.Not.Null);
            Assert.That(inputController, Is.Not.Null);
            Assert.That(inputController.IsActive, Is.True);
            foreach (GameplayControl control in System.Enum.GetValues(
                typeof(GameplayControl)))
            {
                Assert.That(
                    inputController.GetBindingDisplay(control),
                    Is.Not.Empty,
                    $"{control} requires a displayable authored binding.");
            }
            Assert.That(
                inputController.GetBindingDisplay(GameplayControl.AimLook),
                Is.EqualTo("MMB"));
            Assert.That(
                inputController.GetBindingDisplay(GameplayControl.CameraZoom),
                Is.EqualTo("WHEEL"));
            Assert.That(
                inputController.GetBindingDisplay(GameplayControl.Attack),
                Is.EqualTo("LMB"));
            Assert.That(
                inputController.GetBindingDisplay(GameplayControl.Reload),
                Is.EqualTo("R"));
            Assert.That(
                inputController.GetBindingDisplay(GameplayControl.CancelRoute),
                Is.EqualTo("X"));
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(Cursor.visible, Is.True);
            inputController.HandleEscapePressed();
            Assert.That(runtime.Bootstrap.CurrentMode,
                Is.EqualTo(ApplicationMode.Gameplay));
            Assert.That(gameplay.IsRunning, Is.True);

            Assert.That(hud, Is.Not.Null);
            Assert.That(partyHud, Is.Not.Null);
            Assert.That(
                runtime.Bootstrap.GetComponents<MonoBehaviour>()
                    .Select(component => component.GetType().Name),
                Does.Not.Contain("GameplayAdvancementHud"));
            Assert.That(GameplayHud.HotbarSlotCount, Is.EqualTo(8));
            Assert.That(hud.IsVisible, Is.True);
            Assert.That(hud.IsCommandBarVisible, Is.True);
            Assert.That(hud.AreTurnResourcesVisible, Is.False);
            Assert.That(hud.IsEndTurnAvailable, Is.False);
            Assert.That(hud.IsFlyoutExpanded, Is.False);
            Assert.That(dialogueDrawer, Is.Not.Null);
            Assert.That(dialogueDrawer.IsExpanded, Is.False);
            Assert.That(gameplay.DialogueLog, Is.Not.Null);
            Assert.That(gameplay.DialogueLog.Entries, Is.Not.Empty);
            Assert.That(
                gameplay.DialogueLog.Entries[0].Title,
                Is.EqualTo("Initiative order"));
            StringAssert.Contains(
                "DEX ",
                gameplay.DialogueLog.Entries[0].Message);
            StringAssert.Contains(
                "→ advance ",
                gameplay.DialogueLog.Entries[0].Message);
            StringAssert.Contains(
                "Dexterity affects reaction only",
                gameplay.DialogueLog.Entries[0].Message);
            Assert.That(dialogueDrawer.Log, Is.SameAs(gameplay.DialogueLog));
            Assert.That(
                dialogueDrawer.ActiveFilters,
                Is.EqualTo(GameplayDialogueChannel.All));
            hud.ToggleFlyout();
            Assert.That(hud.IsFlyoutExpanded, Is.True);
            hud.ToggleFlyout();
            Assert.That(hud.IsFlyoutExpanded, Is.False);

            Assert.That(sessionPresenter, Is.Not.Null);
            Assert.That(turnMovement, Is.Not.Null);
            Assert.That(actions, Is.Not.Null);
            Assert.That(attacks, Is.Not.Null);
            Assert.That(equipment, Is.Not.Null);
            Assert.That(hotbar, Is.Not.Null);
            Assert.That(
                hotbar.ActorAbilities.Select(ability => ability.Id),
                Does.Contain(GameplayCoreActorAbilities.StanceId));
            Assert.That(
                hotbar.ActorAbilities.Select(ability => ability.Id),
                Does.Contain(GameplayDroneController.AbilityId));
            var stanceBinding = new GameplayHotbarBinding(
                GameplayHotbarBindingKind.ActorAbility,
                GameplayCoreActorAbilities.StanceId);
            Assert.That(hotbar.TryBindSlot(1, stanceBinding), Is.True);
            Assert.That(hotbar.Bindings[1], Is.EqualTo(stanceBinding));
            Assert.That(
                gameplay.Session.GetActor("player").Pose.Stance,
                Is.EqualTo(ActorStance.Standing));
            Assert.That(
                hotbar.TryActivateSlot(1),
                Is.True);
            Assert.That(
                gameplay.Session.GetActor("player").Pose.Stance,
                Is.EqualTo(ActorStance.Crouched));
            Assert.That(
                hotbar.TryActivateSlot(1),
                Is.True);
            Assert.That(
                gameplay.Session.GetActor("player").Pose.Stance,
                Is.EqualTo(ActorStance.Standing));
            Assert.That(replayHud.LiveTransitionCount, Is.GreaterThan(0));
            Assert.That(
                gameplay.Session.Mode,
                Is.EqualTo(GameplaySessionMode.Exploration));
            Assert.That(
                replayHud.IsAvailable,
                Is.False,
                "Exploration events must not surface as a combat replay.");
            Assert.That(runtime.Projectiles, Is.Not.Null);
            Assert.That(actions.TurnModeExitConstraintCount, Is.EqualTo(1));
            Assert.That(objectivePresenter, Is.Not.Null);
            Assert.That(turnMovement.Session, Is.SameAs(gameplay.Session));
            Assert.That(hud.TurnMovement, Is.SameAs(turnMovement));
            Assert.That(actions.Session, Is.SameAs(gameplay.Session));
            Assert.That(attacks.Session, Is.SameAs(gameplay.Session));
            Assert.That(equipment.Session, Is.SameAs(gameplay.Session));
            Assert.That(hud.ActionController, Is.SameAs(actions));
            Assert.That(hud.AttackController, Is.SameAs(attacks));
            Assert.That(hud.EquipmentController, Is.SameAs(equipment));
            Assert.That(hud.IsInteractionPromptVisible, Is.False);
            Assert.That(objectivePresenter.Session, Is.SameAs(gameplay.Session));
            Assert.That(objectivePresenter.IsPresented, Is.True);
            Assert.That(gameplay.Session, Is.SameAs(sessionPresenter.Session));
            Assert.That(gameplay.Session.Mode,
                Is.EqualTo(GameplaySessionMode.Exploration));
            Assert.That(gameplay.Session.Scenario.Actors.Count, Is.EqualTo(6));
            Assert.That(
                gameplay.Session.Scenario.Actors.Select(actor => actor.Id),
                Is.EquivalentTo(new[]
                {
                    "player",
                    "oren-vale",
                    "depot-rifleman",
                    "depot-yard-support",
                    "depot-warehouse-patrol",
                    "depot-loading-guard",
                }));
            Assert.That(gameplay.Session.Scenario.Objectives.Count, Is.EqualTo(1));
            Assert.That(
                gameplay.Session.Scenario.PlayerParty.ActorIds,
                Is.EqualTo(new[] { "player", "oren-vale" }));
            Assert.That(gameplay.PartyHud, Is.Not.Null);
            Assert.That(gameplay.PartyHud.CurrentModel.Members, Has.Count.EqualTo(2));
            Assert.That(gameplay.PartyHud.CurrentModel.Members[0].Selected, Is.True);
            Assert.That(gameplay.Session.GetInventory("player"), Has.Count.EqualTo(6));
            Assert.That(gameplay.Session.GetInventory("oren-vale"),
                Has.Count.EqualTo(4));
            Assert.That(
                gameplay.Session.GetInventoryItem(
                    "player",
                    "weapon.combat-knife").Attack.Contact.MaximumReach,
                Is.EqualTo(2f));
            Assert.That(
                gameplay.Session.GetInventoryItem(
                    "player",
                    "item.frag-grenade").ConsumablePower,
                Is.Not.Null);
            Assert.That(
                gameplay.Session.GetInventoryQuantity(
                    "player",
                    "item.frag-grenade"),
                Is.EqualTo(3));
            Assert.That(
                gameplay.Session.GetInventoryItem(
                    "player",
                    "item.smoke-grenade").ConsumablePower,
                Is.TypeOf<ThrownExplosiveDefinition>());
            Assert.That(
                gameplay.Session.GetInventoryQuantity(
                    "player",
                    "item.smoke-grenade"),
                Is.EqualTo(2));
            Assert.That(
                ((ThrownExplosiveDefinition)gameplay.Session.GetInventoryItem(
                    "player",
                    "item.incendiary-grenade").ConsumablePower).DeploysFire,
                Is.True);
            Assert.That(
                ((ThrownExplosiveDefinition)gameplay.Session.GetInventoryItem(
                    "oren-vale",
                    "item.concussive-grenade").ConsumablePower)
                        .BlastActionPointReduction,
                Is.EqualTo(2));
            Assert.That(
                gameplay.Session.GetActor("player").EquippedItemId,
                Is.EqualTo("weapon.rifle"));

            GameplayObjectiveSnapshot objective = gameplay.Session.GetObjective(
                "raised-deck");
            Assert.That(objective.Position.X, Is.EqualTo(12.5f));
            Assert.That(objective.Position.Y, Is.EqualTo(3.02f).Within(0.001f));
            Assert.That(objective.Position.Z, Is.EqualTo(5f));
            Assert.That(objective.InteractionRadius, Is.EqualTo(1.5f));
            Assert.That(
                objective.Interaction.Id,
                Is.EqualTo(
                    gameplay.ScenarioAssembly
                        .Scenario.Objectives.Single(definition =>
                            definition.Id == gameplay.ScenarioAssembly
                                .PrimaryObjectiveId)
                        .Interaction.Id));
            Assert.That(objective.Interaction.TurnCost.ActionPoints, Is.EqualTo(1));
            Assert.That(
                objective.Interaction.TurnCost.MovementOpportunity,
                Is.EqualTo(1f));
            Assert.That(objective.IsCompleted, Is.False);
            Assert.That(hud.Session, Is.SameAs(gameplay.Session));
            Assert.That(runtime.Editor == null || !runtime.Editor.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator WatchSimsKeepsTheMenuUntilPlaybackIsReady()
        {
            GameObject ownedApplication = null;
            GameBootstrap bootstrap = GameBootstrap.Instance;
            if (bootstrap == null)
            {
                ownedApplication = new GameObject(
                    "Simulation Menu Preparation Test");
                bootstrap = ownedApplication.AddComponent<GameBootstrap>();
            }

            bootstrap.ReturnToMenu();
            bootstrap.WatchFirstSimulation();

            Assert.That(bootstrap.CurrentMode, Is.EqualTo(ApplicationMode.Menu));
            Assert.That(bootstrap.IsPreparingSimulation, Is.True);
            Assert.That(
                bootstrap.GetComponent<GameplayController>()?.IsRunning
                    ?? false,
                Is.False);
            Assert.That(
                bootstrap.GetComponent<StartMenu>().LaunchStatus,
                Does.StartWith("LOADING DEPOT FIRST SIM"));

            bootstrap.ReturnToMenu();
            Assert.That(bootstrap.IsPreparingSimulation, Is.False);
            Assert.That(
                bootstrap.GetComponent<StartMenu>().LaunchStatus,
                Is.Empty);
            yield return null;

            if (ownedApplication != null)
                Object.DestroyImmediate(ownedApplication);
        }

        [UnityTest]
        public IEnumerator WatchSimsLaunchesAnExclusiveSimulationViewer()
        {
            using var runtime = new GameplayRuntimeTestHarness();
            yield return runtime.StartSimulation();

            GameplayController gameplay = runtime.Gameplay;
            GameplayBattleReplayController battleReplay = runtime.Bootstrap
                .GetComponent<GameplayBattleReplayController>();
            GameplayTurnReplayHud replayHud = runtime.Bootstrap
                .GetComponent<GameplayTurnReplayHud>();
            GameplayDroneController drones = runtime.Bootstrap
                .GetComponent<GameplayDroneController>();
            GameplayCameraController camera = runtime.Bootstrap
                .GetComponent<GameplayCameraController>();

            Assert.That(
                runtime.Bootstrap.CurrentMode,
                Is.EqualTo(ApplicationMode.SimulationViewer));
            Assert.That(gameplay.IsRunning, Is.True);
            Assert.That(gameplay.IsSimulationViewer, Is.True);
            Assert.That(battleReplay.enabled, Is.True);
            Assert.That(
                replayHud.Source,
                Is.EqualTo(GameplayReplaySource.VerifiedSimulation));
            Assert.That(replayHud.IsAvailable, Is.True);
            Assert.That(replayHud.IsOpen, Is.True);
            Assert.That(replayHud.Replay.Frames.Count, Is.EqualTo(74));
            Assert.That(replayHud.ActionLabel, Is.EqualTo("WATCH BATTLE"));
            Assert.That(replayHud.Playback.TurnGroups.Count, Is.GreaterThan(1));
            Assert.That(
                replayHud.Playback.TurnGroups[
                    replayHud.Playback.TurnGroups.Count - 1]
                    .EndsWithTurnRecord,
                Is.False,
                "The lethal terminal tail must remain attached as the final "
                + "battle turn even without a TurnEnd record.");
            Assert.That(
                replayHud.Playback.TurnGroups[
                    replayHud.Playback.TurnGroups.Count - 1]
                    .ClosureReason,
                Is.EqualTo(GameplayReplayWindowClosureReason.ArtifactTerminal));
            Assert.That(runtime.InputController.CameraOnly, Is.True);
            Assert.That(runtime.Hud.IsVisible, Is.False);
            Assert.That(runtime.PartyHud.IsPresentationSuppressed, Is.True);
            Assert.That(Time.timeScale, Is.Zero);

            GameplaySemanticReplayPlaybackFrame movementFrame = replayHud
                .Playback.Frames.First(frame =>
                    frame.Frame.SemanticRecord is MovementRouteRecord);
            var movement = (MovementRouteRecord)movementFrame.Frame
                .SemanticRecord;
            const float movementProgress = 0.5f;
            GameplayPosition sampledPosition = GameplaySemanticReplaySampler
                .Sample(movementFrame.Frame, movementProgress)
                .Actors[movement.ActorId]
                .Pose.Position;
            var expectedPosition = new Vector3(
                sampledPosition.X,
                sampledPosition.Y,
                sampledPosition.Z);
            GameplaySemanticReplayPlaybackFrame droneMovementFrame = replayHud
                .Playback.Frames.First(frame =>
                    frame.Frame.SemanticRecord is DroneMoveRecord
                    && frame.StartSeconds >= movementFrame.EndSeconds);
            var droneMovement = (DroneMoveRecord)droneMovementFrame.Frame
                .SemanticRecord;
            GameplayPosition sampledDronePosition = GameplaySemanticReplaySampler
                .Sample(droneMovementFrame.Frame, movementProgress)
                .Drones.Single(value => value.DroneId == droneMovement.DroneId)
                .Position;
            var expectedDronePosition = new Vector3(
                sampledDronePosition.X,
                sampledDronePosition.Y,
                sampledDronePosition.Z);

            // Reopen the verified replay to prove every world presenter owns a
            // reversible projection boundary, including drones.
            replayHud.Toggle();
            Assert.That(drones.Session, Is.Not.Null);
            Transform cameraTarget = camera.Target;
            Transform replayedActor = gameplay.WorldRegistry
                .GetActor(movement.ActorId).Transform;
            Transform replayedDrone = drones.GetPresentationTransform(
                droneMovement.DroneId);
            replayHud.OpenVerifiedExternalReplay();
            Vector3 replayStart = replayedActor.position;
            Vector3 droneReplayStart = replayedDrone.position;
            replayHud.AdvancePlayback(
                movementFrame.StartSeconds
                + (movementFrame.DurationSeconds * movementProgress));

            Assert.That(replayHud.IsOpen, Is.True);
            Assert.That(replayHud.TimeSeconds, Is.GreaterThan(0f));
            Assert.That(
                Vector3.Distance(replayedActor.position, expectedPosition),
                Is.LessThan(0.001f));
            Assert.That(
                Vector3.Distance(replayedActor.position, replayStart),
                Is.GreaterThan(0.01f));
            Assert.That(camera.Target, Is.SameAs(cameraTarget));

            float droneMovementTime = droneMovementFrame.StartSeconds
                + (droneMovementFrame.DurationSeconds * movementProgress);
            replayHud.AdvancePlayback(
                droneMovementTime - replayHud.TimeSeconds);
            Assert.That(
                Vector3.Distance(replayedDrone.position, expectedDronePosition),
                Is.LessThan(0.001f));
            Assert.That(
                Vector3.Distance(replayedDrone.position, droneReplayStart),
                Is.GreaterThan(0.01f));

            int expectedDischarges = replayHud.Playback.Frames.Sum(frame =>
                ReplayCombatPresentationEventProjector.Project(frame.Frame)
                    .Count(presentationEvent => presentationEvent.Kind ==
                            ReplayCombatPresentationEventKind.WeaponDischarge
                        || presentationEvent.Kind ==
                            ReplayCombatPresentationEventKind.ProjectileLaunch));
            int expectedImpacts = replayHud.Playback.Frames.Sum(frame =>
                ReplayCombatPresentationEventProjector.Project(frame.Frame)
                    .Count(presentationEvent => presentationEvent.Kind ==
                        ReplayCombatPresentationEventKind.ProjectileImpact));
            int expectedReactions = replayHud.Playback.Frames.Sum(frame =>
                ReplayCombatPresentationEventProjector.Project(frame.Frame)
                    .Count(presentationEvent => presentationEvent.Kind ==
                        ReplayCombatPresentationEventKind.Reaction));
            int expectedIncapacitations = replayHud.Playback.Frames.Sum(frame =>
                ReplayCombatPresentationEventProjector.Project(frame.Frame)
                    .Count(presentationEvent => presentationEvent.Kind ==
                        ReplayCombatPresentationEventKind.Incapacitation));
            int expectedDroneDischarges = replayHud.Playback.Frames.Sum(frame =>
                ReplayCombatPresentationEventProjector.Project(frame.Frame)
                    .Count(presentationEvent => presentationEvent.ShooterKind ==
                        ReplayCombatPresentationSubjectKind.Drone));
            int expectedActorDroneDischarges = replayHud.Playback.Frames.Sum(
                frame => ReplayCombatPresentationEventProjector
                    .Project(frame.Frame)
                    .Count(presentationEvent =>
                        presentationEvent.ShooterKind ==
                            ReplayCombatPresentationSubjectKind.Actor
                        && presentationEvent.TargetKind ==
                            ReplayCombatPresentationSubjectKind.Drone
                        && presentationEvent.Kind ==
                            ReplayCombatPresentationEventKind.WeaponDischarge));
            replayHud.AdvancePlayback(replayHud.Playback.TotalDurationSeconds);

            Assert.That(
                gameplay.ReplayPresentedDischargeCount,
                Is.EqualTo(expectedDischarges).And.GreaterThan(0));
            Assert.That(
                gameplay.ReplayPresentedProjectileImpactCount,
                Is.EqualTo(expectedImpacts));
            Assert.That(
                gameplay.ReplayPresentedReactionCount,
                Is.EqualTo(expectedReactions).And.GreaterThan(0));
            Assert.That(
                gameplay.ReplayPresentedIncapacitationCount,
                Is.EqualTo(expectedIncapacitations).And.GreaterThan(0));
            Assert.That(expectedDroneDischarges, Is.GreaterThan(0));
            Assert.That(expectedActorDroneDischarges, Is.GreaterThan(0));
            Assert.That(
                drones.ReplayPresentedDischargeCount,
                Is.EqualTo(expectedDroneDischarges));
            Assert.That(
                drones.ReplayTransientVisualCount,
                Is.EqualTo(expectedDroneDischarges * 2),
                "Every crossed drone discharge must create a visible muzzle "
                + "light and historical tracer.");

            runtime.Bootstrap.PlayMainLevel();
            Assert.That(
                runtime.Bootstrap.CurrentMode,
                Is.EqualTo(ApplicationMode.SimulationViewer));

            runtime.Bootstrap.ReturnToMenu();
            Assert.That(
                runtime.Bootstrap.CurrentMode,
                Is.EqualTo(ApplicationMode.Menu));
            Assert.That(gameplay.IsRunning, Is.False);
            Assert.That(battleReplay.enabled, Is.False);
            Assert.That(runtime.InputController.CameraOnly, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
    }
}
