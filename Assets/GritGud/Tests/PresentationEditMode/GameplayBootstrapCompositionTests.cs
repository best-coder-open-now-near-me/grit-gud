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
            GameplayDroneController droneController = runtime.Bootstrap
                .GetComponent<GameplayDroneController>();
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
            GameplayActorAbilityHotbarDefinition droneAbility = hotbar
                .ActorAbilities.Single(ability => ability.Id ==
                    GameplayDroneController.AbilityId);
            Assert.That(droneAbility.DisplayName, Is.EqualTo("Summon Drone"));
            Assert.That(droneAbility.Options, Is.Empty);
            Assert.That(droneController.Session.CaptureDrones(), Is.Empty,
                "The depot must begin without a preplaced drone instance.");
            Assert.That(GameObject.Find("scout-drone-01"), Is.Null,
                "Drone presentation must be created only by a summon transition.");
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
        public IEnumerator MainLevelOpeningShotCompletesReactionBeforeRosterSwap()
        {
            using var runtime = new GameplayRuntimeTestHarness();
            yield return runtime.Start();

            GameplaySession session = runtime.Gameplay.Session;
            Assert.That(session.Mode,
                Is.EqualTo(GameplaySessionMode.Exploration));
            Assert.That(session.EncounterActive, Is.False);
            Assert.That(runtime.PartyHud.CurrentModel.Members,
                Has.Count.EqualTo(2));

            var exposure = new TargetExposureSnapshot(
                "player",
                "depot-rifleman",
                new[]
                {
                    new TargetRegionExposure(TargetRegionId.Torso, 5, 5),
                });

            Assert.That(runtime.Attacks.TryAttack(exposure), Is.True);
            Assert.That(runtime.Attacks.LastResolution.Hit, Is.True);
            Assert.That(runtime.SessionPresenter.EncounterStartPending,
                Is.True);
            Assert.That(session.EncounterActive, Is.False);
            Assert.That(runtime.PartyHud.CurrentModel.CombatRoster, Is.False);
            Assert.That(runtime.PartyHud.CurrentModel.Members,
                Has.Count.EqualTo(2));
            Assert.That(
                runtime.Gameplay.DialogueLog.Entries.Select(entry => entry.Title),
                Does.Not.Contain("COMBAT"));

            runtime.SessionPresenter.Tick(
                ActorInjuryAnimationOverlayProjector.HitReactionSeconds
                + 0.15f);
            Assert.That(session.EncounterActive, Is.False);

            runtime.SessionPresenter.Tick(0.02f);
            Assert.That(session.EncounterActive, Is.True);
            runtime.PartyHud.TickRosterReveal(0f);
            Assert.That(runtime.PartyHud.CurrentModel.CombatRoster, Is.True);
            Assert.That(runtime.PartyHud.RevealingRosterMemberCount,
                Is.EqualTo(2));
            Assert.That(runtime.PartyHud.RosterRevealActive, Is.True);
            Assert.That(
                runtime.PartyHud.GetRosterMemberRevealProgress(
                    "depot-rifleman"),
                Is.Zero);

            int playerFinalIndex = runtime.PartyHud.CurrentModel.Members
                .Select((member, index) => new { member.ActorId, Index = index })
                .Single(item => item.ActorId == "player")
                .Index;
            Assert.That(
                runtime.PartyHud.GetRosterMemberVerticalPosition(
                    "player",
                    playerFinalIndex),
                Is.Zero,
                "The existing party row must not jump to its initiative slot.");
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
        public IEnumerator ReplayScrubberCapturePausesAndRestoresPlayState()
        {
            using var runtime = new GameplayRuntimeTestHarness();
            yield return runtime.StartSimulation();

            GameplayTurnReplayHud replayHud = runtime.Bootstrap
                .GetComponent<GameplayTurnReplayHud>();
            float total = replayHud.Playback.TotalDurationSeconds;

            Assert.That(replayHud.IsOpen, Is.True);
            Assert.That(replayHud.IsPlaying, Is.True);
            replayHud.BeginScrubCapture();
            Assert.That(replayHud.IsScrubCaptured, Is.True);
            Assert.That(replayHud.IsPlaying, Is.False);
            replayHud.SetScrubPlayhead(total * 0.75f);
            float heldTime = replayHud.TimeSeconds;
            replayHud.AdvancePlayback(1f);
            Assert.That(replayHud.TimeSeconds, Is.EqualTo(heldTime));
            replayHud.EndScrubCapture();
            Assert.That(replayHud.IsScrubCaptured, Is.False);
            Assert.That(replayHud.IsPlaying, Is.True);

            replayHud.BeginScrubCapture();
            replayHud.SetScrubPlayhead(total * 0.25f);
            Assert.That(replayHud.TimeSeconds, Is.LessThan(heldTime));
            replayHud.EndScrubCapture();
            Assert.That(replayHud.IsPlaying, Is.True);

            replayHud.BeginScrubCapture();
            replayHud.SetScrubPlayhead(total);
            replayHud.EndScrubCapture();
            Assert.That(replayHud.IsPlaying, Is.False,
                "Releasing at the replay end must not resume playback.");

            replayHud.SetScrubPlayhead(total * 0.5f);
            replayHud.BeginScrubCapture();
            replayHud.SetScrubPlayhead(total * 0.4f);
            replayHud.EndScrubCapture();
            Assert.That(replayHud.IsPlaying, Is.False,
                "A scrub that began paused must remain paused on release.");
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
            GameplayDialogueDrawer replayDialogueDrawer =
                runtime.DialogueDrawer;
            GameplayDroneController drones = runtime.Bootstrap
                .GetComponent<GameplayDroneController>();
            GameplayCameraController camera = Camera.main
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
            Assert.That(replayHud.Replay.Frames.Count, Is.EqualTo(90));
            Assert.That(replayHud.ActionLabel, Is.EqualTo("WATCH BATTLE"));
            Assert.That(replayHud.Playback.TurnGroups.Count, Is.GreaterThan(1));
            Assert.That(replayHud.ContentSummary, Is.Not.Null);
            Assert.That(replayHud.ContentSummary.IsReadyToOpen, Is.True);
            Assert.That(replayDialogueDrawer.IsExpanded, Is.True);
            Assert.That(replayDialogueDrawer.HeaderLabel,
                Is.EqualTo("REPLAY COMBAT TRANSCRIPT"));
            StringAssert.Contains(
                "REPLAY SOURCE: ARTIFACT",
                replayDialogueDrawer.ContextStatus);
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
            GameplayPosition sampledPosition = SampleExpectedMovement(
                movement,
                movementProgress);
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
            GameplayPosition sampledDronePosition = LerpExpected(
                droneMovement.Origin,
                droneMovement.Destination,
                movementProgress);
            var expectedDronePosition = new Vector3(
                sampledDronePosition.X,
                sampledDronePosition.Y,
                sampledDronePosition.Z);
            var droneReplayStart = new Vector3(
                droneMovement.Origin.X,
                droneMovement.Origin.Y,
                droneMovement.Origin.Z);

            // Reopen the verified replay to prove every world presenter owns a
            // reversible projection boundary, including drones.
            replayHud.Toggle();
            Assert.That(replayDialogueDrawer.IsExpanded, Is.False);
            Assert.That(replayDialogueDrawer.HeaderLabel,
                Is.EqualTo("DIALOGUE - TRANSCRIPT"));
            Assert.That(drones.Session, Is.Not.Null);
            Transform cameraTarget = camera.Target;
            GameplayCameraView cameraView = camera.View;
            Vector3 cameraPosition = camera.transform.position;
            Quaternion cameraRotation = camera.transform.rotation;
            float cameraZoom = camera.ThirdPersonZoom;
            Transform replayedActor = gameplay.WorldRegistry
                .GetActor(movement.ActorId).Transform;
            replayHud.OpenVerifiedExternalReplay();

            GameplaySemanticReplayPlaybackFrame thrownFrame = replayHud
                .Playback.Frames.First(frame =>
                    frame.Frame.SemanticRecord is GameplayActionRecord action
                    && action.Outcomes.Any(outcome =>
                        outcome is ThrownExplosiveActionOutcome));
            float thrownFlightProgress =
                (GameplayThrownExplosivePresentationTiming
                    .ReleaseNormalizedTime
                + GameplayThrownExplosivePresentationTiming
                    .ImpactNormalizedTime) * 0.5f;
            replayHud.AdvancePlayback(
                thrownFrame.StartSeconds
                + (thrownFrame.DurationSeconds * thrownFlightProgress));
            Transform replayGrenade = gameplay.transform.Find(
                "Replay Flying Thrown Explosive");
            Assert.That(
                replayGrenade,
                Is.Not.Null,
                "Replay must project the grenade between release and impact.");
            ReplayFreeCameraController freeCamera = camera.GetComponent<
                ReplayFreeCameraController>();
            replayHud.RequestCameraCommand(GameplayReplayCameraCommand.Free);
            Assert.That(
                replayHud.ReplayCameraMode,
                Is.EqualTo(GameplayReplayCameraMode.Free));
            Assert.That(freeCamera.IsPresenting, Is.True);
            Assert.That(camera.enabled, Is.False);
            Vector3 freeStart = camera.transform.position;
            Quaternion freeRotation = camera.transform.rotation;
            freeCamera.Advance(
                new ReplaySpectatorInputFrame(
                    new Vector3(1f, 0.5f, 1f),
                    new Vector2(20f, -10f),
                    boosted: true),
                0.5f);
            Assert.That(
                Vector3.Distance(camera.transform.position, freeStart),
                Is.GreaterThan(1f));
            Assert.That(
                Quaternion.Angle(camera.transform.rotation, freeRotation),
                Is.GreaterThan(0.1f));
            Assert.That(
                runtime.InputController.CurrentFrame.Movement,
                Is.EqualTo(Vector2.zero),
                "Spectator movement must not enter the actor input frame.");
            replayHud.Toggle();
            Assert.That(
                gameplay.transform.Find("Replay Flying Thrown Explosive"),
                Is.Null,
                "Closing replay must clear the projected grenade.");
            Assert.That(camera.Target, Is.SameAs(cameraTarget));
            Assert.That(freeCamera.IsPresenting, Is.False);
            Assert.That(camera.enabled, Is.True);
            Assert.That(camera.View, Is.EqualTo(cameraView));
            Assert.That(camera.ThirdPersonZoom, Is.EqualTo(cameraZoom));
            Assert.That(
                camera.transform.position,
                Is.EqualTo(cameraPosition)
                    .Using(UnityEngine.TestTools.Utils
                        .Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(
                Quaternion.Angle(camera.transform.rotation, cameraRotation),
                Is.LessThan(0.001f));
            replayHud.OpenVerifiedExternalReplay();

            Vector3 replayStart = replayedActor.position;
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
            Assert.That(camera.Target, Is.SameAs(replayedActor));
            Assert.That(
                replayHud.ReplayCameraMode,
                Is.EqualTo(GameplayReplayCameraMode.Auto));

            Transform automaticTarget = camera.Target;
            replayHud.RequestCameraCommand(
                GameplayReplayCameraCommand.NextSubject);
            Assert.That(
                replayHud.ReplayCameraMode,
                Is.EqualTo(GameplayReplayCameraMode.Subject));
            Assert.That(camera.Target, Is.Not.SameAs(automaticTarget));
            Assert.That(replayHud.ReplayCameraLabel, Is.Not.EqualTo("AUTO"));
            replayHud.RequestCameraCommand(
                GameplayReplayCameraCommand.PreviousSubject);
            Assert.That(camera.Target, Is.SameAs(automaticTarget));
            replayHud.RequestCameraCommand(GameplayReplayCameraCommand.Auto);
            Assert.That(
                replayHud.ReplayCameraMode,
                Is.EqualTo(GameplayReplayCameraMode.Auto));
            Assert.That(camera.Target, Is.SameAs(replayedActor));

            float droneMovementTime = droneMovementFrame.StartSeconds
                + (droneMovementFrame.DurationSeconds * movementProgress);
            replayHud.AdvancePlayback(
                droneMovementTime - replayHud.TimeSeconds);
            Transform replayedDrone = drones.GetPresentationTransform(
                droneMovement.DroneId);
            Assert.That(
                Vector3.Distance(replayedDrone.position, expectedDronePosition),
                Is.LessThan(0.001f));
            Assert.That(
                Vector3.Distance(replayedDrone.position, droneReplayStart),
                Is.GreaterThan(0.01f));
            bool focusedDrone = false;
            int maximumSubjectCycles = replayHud.Replay.InitialState.Session
                .Actors.Count + 1;
            for (int index = 0; index < maximumSubjectCycles; index++)
            {
                replayHud.RequestCameraCommand(
                    GameplayReplayCameraCommand.NextSubject);
                if (camera.Target != replayedDrone) continue;
                focusedDrone = true;
                break;
            }
            Assert.That(focusedDrone, Is.True,
                "Manual replay-subject cycling must include active drones.");
            Assert.That(
                replayHud.ReplayCameraLabel,
                Is.EqualTo(droneMovement.DroneId.ToUpperInvariant()));
            replayHud.RequestCameraCommand(GameplayReplayCameraCommand.Auto);

            int expectedDischarges = replayHud.Playback.Frames.Sum(frame =>
                CountExpectedDischarges(frame.Frame));
            int expectedImpacts = replayHud.Playback.Frames.Sum(frame =>
                CountExpectedImpacts(frame.Frame));
            int expectedReactions = replayHud.Playback.Frames.Sum(frame =>
                CountExpectedInjuryReactions(frame.Frame));
            int expectedIncapacitations = replayHud.Playback.Frames.Sum(frame =>
                CountExpectedTerminalTransitions(frame.Frame));
            int expectedDroneDischarges = replayHud.Playback.Frames.Sum(frame =>
                frame.Frame.SemanticRecord is DroneAttackRecord ? 1 : 0);
            int expectedDroneTransientVisuals = replayHud.Playback.Frames.Sum(
                frame => CountExpectedDroneTransientVisuals(frame.Frame));
            int expectedActorDroneDischarges = replayHud.Playback.Frames.Sum(
                frame => frame.Frame.SemanticRecord is ActorDroneAttackRecord
                    ? 1
                    : 0);
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
                Is.EqualTo(expectedDroneTransientVisuals),
                "Every crossed drone discharge must create a visible muzzle "
                + "light, while successful shots also create a historical "
                + "tracer.");

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

        private static int CountExpectedDischarges(
            GameplaySemanticReplayFrame frame)
        {
            switch (frame.SemanticRecord)
            {
                case DroneAttackRecord _:
                case ActorDroneAttackRecord _:
                    return 1;
                case GameplayActionRecord action:
                {
                    int count = action.Outcomes.Count(outcome =>
                        outcome is WeaponDischargedActionOutcome
                        || outcome is ProjectileLaunchedActionOutcome
                        || outcome is ThrownExplosiveActionOutcome);
                    if (count > 0) return count;
                    return action.Outcomes.Any(outcome =>
                            outcome is AttackResolvedActionOutcome resolved
                            && !resolved.Attack.IsContactAttack)
                        ? 1
                        : 0;
                }
                default:
                    return 0;
            }
        }

        private static int CountExpectedImpacts(
            GameplaySemanticReplayFrame frame)
        {
            if (frame.SemanticRecord is ProjectileAdvanceRecord advance)
                return advance.Resulting.Impact == null ? 0 : 1;
            if (frame.SemanticRecord is DroneCrashImpactRecord)
                return 1;
            if (frame.SemanticRecord is GameplayActionRecord action)
                return action.Outcomes.Count(outcome =>
                    outcome is ThrownExplosiveActionOutcome);
            return 0;
        }

        private static int CountExpectedInjuryReactions(
            GameplaySemanticReplayFrame frame)
        {
            if (!(frame.SemanticRecord is GameplayActionRecord)
                && !(frame.SemanticRecord is DroneAttackRecord)
                && !(frame.SemanticRecord is ProjectileAdvanceRecord)
                && !(frame.SemanticRecord is DroneCrashImpactRecord))
                return 0;
            return frame.Resulting.Session.Actors.Count(resulting =>
                resulting.Wounds.WoundCount > frame.Previous.Session
                    .GetActor(resulting.ActorId).Wounds.WoundCount);
        }

        private static int CountExpectedTerminalTransitions(
            GameplaySemanticReplayFrame frame) =>
            frame.Resulting.Session.Actors.Count(resulting =>
            {
                ActorLifeState previous = frame.Previous.Session
                    .GetActor(resulting.ActorId).LifeState;
                return previous != resulting.LifeState
                    && (resulting.LifeState == ActorLifeState.Incapacitated
                        || resulting.LifeState == ActorLifeState.Dead);
            });

        private static int CountExpectedDroneTransientVisuals(
            GameplaySemanticReplayFrame frame)
        {
            if (!(frame.SemanticRecord is DroneAttackRecord attack)) return 0;
            bool hit = !(attack.Consequence is AttackResolutionRecord resolved)
                || resolved.Hit;
            return hit ? 2 : 1;
        }

        private static GameplayPosition SampleExpectedMovement(
            MovementRouteRecord route,
            float normalizedProgress)
        {
            float remaining = route.TotalPlaybackDurationSeconds
                * Mathf.Clamp01(normalizedProgress);
            for (int index = 0; index < route.Segments.Count; index++)
            {
                MovementRouteSegmentRecord segment = route.Segments[index];
                if (remaining >= segment.PlaybackDurationSeconds
                    && index < route.Segments.Count - 1)
                {
                    remaining -= segment.PlaybackDurationSeconds;
                    continue;
                }
                float progress = Mathf.Clamp01(
                    remaining / segment.PlaybackDurationSeconds);
                GameplayPosition position = LerpExpected(
                    segment.From,
                    segment.To,
                    progress);
                if (!segment.IsTraversal) return position;
                float lift = 4f * segment.ArcHeight * progress
                    * (1f - progress);
                return new GameplayPosition(
                    position.X,
                    position.Y + lift,
                    position.Z);
            }
            return route.Destination;
        }

        private static GameplayPosition LerpExpected(
            GameplayPosition origin,
            GameplayPosition destination,
            float progress) => new GameplayPosition(
            origin.X + ((destination.X - origin.X) * progress),
            origin.Y + ((destination.Y - origin.Y) * progress),
            origin.Z + ((destination.Z - origin.Z) * progress));
    }
}
