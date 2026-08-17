using System.Collections;
using System.Linq;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using GritGud.Editor;
using GritGud.Presentation.Bootstrap;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Gameplay;
using GritGud.Presentation.LevelEditing;
using GritGud.Presentation.Levels.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using UnityEditor.Animations;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayRoutingTests
    {
        [Test]
        public void MovementPlannerRefreshesWhenTurnReturnsToSameActor()
        {
            var host = new GameObject("Turn Movement Refresh Test");
            try
            {
                var actor = new ScenarioActorDefinition(
                    "player",
                    10,
                    new GameplayActorPose(
                        new GameplayPosition(0f, 0f, 0f),
                        facingDegrees: 0f),
                    new TurnBudget(actionPoints: 4, movementOpportunity: 8f));
                var scenario = new ScenarioDefinition(
                    "turn-movement-refresh",
                    new ScenarioTimingDefinition(1.25f),
                    new[] { actor },
                    System.Array.Empty<ScenarioObjectiveDefinition>());
                var session = new GameplaySession(scenario);
                Assert.That(session.BeginEncounter(), Is.True);
                session.SpendMovement("player", 3f);

                ExplorationMovementInput movementInput =
                    host.AddComponent<ExplorationMovementInput>();
                ThirdPersonMotor motor = host.AddComponent<ThirdPersonMotor>();
                TurnMovementController controller =
                    host.AddComponent<TurnMovementController>();
                controller.Bind(
                    session,
                    movementInput,
                    new EmptyGameplayInputSource(),
                    motor,
                    "player");

                Assert.That(controller.SynchronizePlanningState(), Is.True);
                Assert.That(controller.PlanningMaximumCost, Is.EqualTo(5f));
                Assert.That(session.TryEndTurn("player", out _), Is.True);

                Assert.That(controller.SynchronizePlanningState(), Is.True);
                Assert.That(controller.PlanningMaximumCost, Is.EqualTo(8f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void MovementPlannerRefreshesAfterRifleConsumesActionPoint()
        {
            var host = new GameObject("Post-Rifle Turn Movement Refresh Test");
            try
            {
                var player = new ScenarioActorDefinition(
                    "player",
                    10,
                    new GameplayActorPose(
                        new GameplayPosition(0f, 0f, 0f),
                        facingDegrees: 0f),
                    new TurnBudget(actionPoints: 4, movementOpportunity: 8f),
                    new AttackDefinition(
                        "attack.rifle",
                        "Fire rifle",
                        new ActionCost(1, 0f, ActionMobility.Set),
                        woundMovementPenalty: 2f,
                        accuracyDecay: AccuracyDecayDefinition.None));
                var target = new ScenarioActorDefinition(
                    "target",
                    0,
                    new GameplayActorPose(
                        new GameplayPosition(0f, 0f, 5f),
                        facingDegrees: 180f),
                    new TurnBudget(actionPoints: 4, movementOpportunity: 8f));
                var session = new GameplaySession(new ScenarioDefinition(
                    "post-rifle-movement-refresh",
                    new ScenarioTimingDefinition(1.25f),
                    new[] { player, target },
                    System.Array.Empty<ScenarioObjectiveDefinition>()));
                Assert.That(session.EnterTurnMode(), Is.True);

                ExplorationMovementInput movementInput =
                    host.AddComponent<ExplorationMovementInput>();
                ThirdPersonMotor motor = host.AddComponent<ThirdPersonMotor>();
                TurnMovementController controller =
                    host.AddComponent<TurnMovementController>();
                controller.Bind(
                    session,
                    movementInput,
                    new EmptyGameplayInputSource(),
                    motor,
                    "player");

                Assert.That(controller.SynchronizePlanningState(), Is.True);
                Assert.That(controller.PlanningMaximumActionPoints, Is.EqualTo(4));

                var exposure = new TargetExposureSnapshot(
                    "player",
                    "target",
                    new[]
                    {
                        new TargetRegionExposure(TargetRegionId.Torso, 5, 5),
                    });
                var attacks = new GameplayAttackSession(
                    session,
                    authoredScenarioSeed: 3u);
                Assert.That(
                    attacks.TryResolve(
                        "player",
                        exposure,
                        out _,
                        out AttackResolutionFailure failure),
                    Is.True,
                    failure.ToString());

                Assert.That(controller.SynchronizePlanningState(), Is.True);
                Assert.That(controller.PlanningMaximumActionPoints, Is.EqualTo(3));
                Assert.That(controller.PlanningMaximumCost, Is.EqualTo(8f));
                Assert.That(controller.PlanPointCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CameraOrbitUsesMiddleMouseAndLeavesRightMouseContextual()
        {
            InputActionAsset inputActions = GameplayInputController.CreateInputAsset();
            try
            {
                InputAction cancel = inputActions.FindAction(
                    "CancelPendingAction",
                    throwIfNotFound: true);
                Assert.That(
                    cancel.bindings.Select(binding => binding.path),
                    Is.EquivalentTo(new[] { "<Keyboard>/escape" }));

                InputAction aim = inputActions.FindAction(
                    "Aim",
                    throwIfNotFound: true);
                Assert.That(
                    aim.bindings.Select(binding => binding.path),
                    Is.EquivalentTo(new[] { "<Mouse>/middleButton" }));
            }
            finally
            {
                Object.DestroyImmediate(inputActions);
            }
        }

        [Test]
        public void HotbarReassignmentRecognizesOnlyRightClickInsideSlot()
        {
            var slot = new Rect(10f, 20f, 80f, 40f);
            var rightClick = new Event
            {
                type = EventType.MouseDown,
                button = 1,
                mousePosition = new Vector2(25f, 35f),
            };
            var leftClick = new Event(rightClick)
            {
                button = 0,
            };
            var outsideRightClick = new Event(rightClick)
            {
                mousePosition = new Vector2(100f, 35f),
            };

            Assert.That(
                GameplayHud.IsHotbarChoiceRequest(rightClick, slot),
                Is.True);
            Assert.That(
                GameplayHud.IsHotbarChoiceRequest(leftClick, slot),
                Is.False);
            Assert.That(
                GameplayHud.IsHotbarChoiceRequest(outsideRightClick, slot),
                Is.False);
        }

        [Test]
        public void PendingPowerPulseOscillatesWithinAuthoredAlphaRange()
        {
            float midpoint = GameplayHud.CalculatePendingPowerPulse(0f);
            float peak = GameplayHud.CalculatePendingPowerPulse(
                1f / (4f * GameplayHud.PendingPowerPulseCyclesPerSecond));

            Assert.That(
                midpoint,
                Is.InRange(
                    GameplayHud.PendingPowerPulseMinimumAlpha,
                    1f));
            Assert.That(peak, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(peak, Is.GreaterThan(midpoint));
        }

        [Test]
        public void AbilityOptionHotkeysSkipTheParentAbilitySlot()
        {
            Assert.That(
                GameplayHotbarController.ResolveOptionHotbarNumber(
                    parentSlot: 4,
                    optionIndex: 0),
                Is.EqualTo(1));
            Assert.That(
                GameplayHotbarController.ResolveOptionHotbarNumber(
                    parentSlot: 1,
                    optionIndex: 0),
                Is.EqualTo(2));
            Assert.That(
                GameplayHud.FormatActorAbilityOptionLabel(
                    parentSlot: 1,
                    optionIndex: 0,
                    label: "PUSH"),
                Is.EqualTo("[2]  PUSH"));
        }

        [UnityTest]
        public IEnumerator PlayMainLevelStartsGameplayInsteadOfEditorPreview()
        {
            GameObject ownedApplication = null;
            GameObject ownedCamera = null;
            GameBootstrap bootstrap = GameBootstrap.Instance;
            if (bootstrap == null)
            {
                ownedApplication = new GameObject("Gameplay Routing Test");
                bootstrap = ownedApplication.AddComponent<GameBootstrap>();
            }

            bootstrap.ReturnToMenu();
            bool originalFog = RenderSettings.fog;
            Camera sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                ownedCamera = new GameObject("Main Camera");
                ownedCamera.tag = "MainCamera";
                sceneCamera = ownedCamera.AddComponent<Camera>();
            }

            try
            {
                bootstrap.PlayMainLevel();
                yield return null;

                GameplayController gameplay = bootstrap.GetComponent<GameplayController>();
                GameplayInputController inputController =
                    bootstrap.GetComponent<GameplayInputController>();
                GameplayHud hud = bootstrap.GetComponent<GameplayHud>();
                GameplayPartyHud partyHud =
                    bootstrap.GetComponent<GameplayPartyHud>();
                GameplayDialogueDrawer dialogueDrawer =
                    bootstrap.GetComponent<GameplayDialogueDrawer>();
                GameplaySessionPresenter sessionPresenter =
                    bootstrap.GetComponent<GameplaySessionPresenter>();
                TurnMovementController turnMovement =
                    bootstrap.GetComponent<TurnMovementController>();
                GameplayActionController actions =
                    bootstrap.GetComponent<GameplayActionController>();
                GameplayAttackController attacks =
                    bootstrap.GetComponent<GameplayAttackController>();
                GameplayEquipmentController equipment =
                    bootstrap.GetComponent<GameplayEquipmentController>();
                GameplayHotbarController hotbar =
                    bootstrap.GetComponent<GameplayHotbarController>();
                GameplayProjectileController projectiles =
                    bootstrap.GetComponent<GameplayProjectileController>();
                GameplayObjectivePresenter objectivePresenter =
                    bootstrap.GetComponent<GameplayObjectivePresenter>();
                LevelEditorController editor = bootstrap.GetComponent<LevelEditorController>();
                Assert.That(bootstrap.CurrentMode, Is.EqualTo(ApplicationMode.Gameplay));
                Assert.That(gameplay, Is.Not.Null);
                Assert.That(gameplay.IsRunning, Is.True);
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
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
                Assert.That(Cursor.visible, Is.True);
                inputController.HandleEscapePressed();
                Assert.That(bootstrap.CurrentMode,
                    Is.EqualTo(ApplicationMode.Gameplay));
                Assert.That(gameplay.IsRunning, Is.True);
                Assert.That(hud, Is.Not.Null);
                Assert.That(partyHud, Is.Not.Null);
                Assert.That(
                    bootstrap.GetComponents<MonoBehaviour>()
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
                Assert.That(
                    dialogueDrawer.Log,
                    Is.SameAs(gameplay.DialogueLog));
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
                    hotbar.Bindings[GameplayCoreActorAbilities.StanceHotbarSlot],
                    Is.EqualTo(new GameplayHotbarBinding(
                        GameplayHotbarBindingKind.ActorAbility,
                        GameplayCoreActorAbilities.StanceId)));
                Assert.That(
                    gameplay.Session.GetActor("player").Pose.Stance,
                    Is.EqualTo(ActorStance.Standing));
                Assert.That(
                    hotbar.TryActivateSlot(
                        GameplayCoreActorAbilities.StanceHotbarSlot),
                    Is.True);
                Assert.That(
                    gameplay.Session.GetActor("player").Pose.Stance,
                    Is.EqualTo(ActorStance.Crouched));
                Assert.That(
                    hotbar.TryActivateSlot(
                        GameplayCoreActorAbilities.StanceHotbarSlot),
                    Is.True);
                Assert.That(
                    gameplay.Session.GetActor("player").Pose.Stance,
                    Is.EqualTo(ActorStance.Standing));
                Assert.That(projectiles, Is.Not.Null);
                Assert.That(actions.TurnModeExitConstraintCount,
                    Is.EqualTo(1));
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
                Assert.That(gameplay.Session.Scenario.Actors.Count, Is.EqualTo(3));
                Assert.That(gameplay.Session.Scenario.Objectives.Count, Is.EqualTo(1));
                Assert.That(
                    gameplay.Session.Scenario.PlayerParty.ActorIds,
                    Is.EqualTo(new[] { "player", "oren-vale" }));
                Assert.That(gameplay.PartyHud, Is.Not.Null);
                Assert.That(gameplay.PartyHud.CurrentModel.Members,
                    Has.Count.EqualTo(2));
                Assert.That(gameplay.PartyHud.CurrentModel.Members[0].Selected,
                    Is.True);
                Assert.That(gameplay.Session.GetInventory("player"),
                    Has.Count.EqualTo(5));
                Assert.That(gameplay.Session.GetInventory("oren-vale"),
                    Has.Count.EqualTo(3));
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
                Assert.That(gameplay.Session.GetActor("player").EquippedItemId,
                    Is.EqualTo("weapon.rifle"));
                GameplayObjectiveSnapshot objective = gameplay.Session.GetObjective(
                    "raised-deck");
                Assert.That(objective.Position.X, Is.EqualTo(12.5f));
                Assert.That(objective.Position.Y, Is.EqualTo(3.02f).Within(0.001f));
                Assert.That(objective.Position.Z, Is.EqualTo(5f));
                Assert.That(objective.InteractionRadius, Is.EqualTo(1.5f));
                Assert.That(objective.Interaction.Id,
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
                Assert.That(editor == null || !editor.enabled, Is.True);
                GameObject player = GameObject.Find("Player Actor");
                GameObject target = GameObject.Find("Depot Rifleman");
                Assert.That(player, Is.Not.Null);
                Assert.That(player.transform.position.z, Is.GreaterThan(-10f));
                Assert.That(player.transform.position.y, Is.GreaterThanOrEqualTo(0f));
                ThirdPersonMotor motor = player.GetComponent<ThirdPersonMotor>();
                ExplorationMovementInput input = player.GetComponent<ExplorationMovementInput>();
                ActorAnimationCoordinator presenter =
                    player.GetComponent<ActorAnimationCoordinator>();
                ActorLocomotionAnimationPresenter locomotionPresenter =
                    player.GetComponent<ActorLocomotionAnimationPresenter>();
                ActorCelShadingPresenter celShadingPresenter =
                    player.GetComponent<ActorCelShadingPresenter>();
                Animator animator = player.GetComponentInChildren<Animator>();
                Assert.That(motor, Is.Not.Null);
                Assert.That(motor.MovementSpeedMultiplier,
                    Is.EqualTo(0.9f));
                Assert.That(input, Is.Not.Null);
                Assert.That(presenter, Is.Not.Null);
                Assert.That(locomotionPresenter, Is.Not.Null);
                Assert.That(celShadingPresenter, Is.Not.Null);
                Assert.That(celShadingPresenter.IsApplied, Is.True);
                Assert.That(celShadingPresenter.IsOutlineApplied, Is.True);
                Assert.That(animator, Is.Not.Null);
                Material[] playerMaterials = player
                    .GetComponentsInChildren<Renderer>()
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null)
                    .ToArray();
                Assert.That(playerMaterials, Is.Not.Empty);
                Material[] playerCelMaterials = playerMaterials
                    .Where(material =>
                        material.shader.name == GameplayCelMaterialStyle.ShaderName)
                    .ToArray();
                Assert.That(playerCelMaterials, Is.Not.Empty);
                Assert.That(playerCelMaterials.All(material =>
                    material.GetTexture("_BaseMap") != null), Is.True);
                Assert.That(playerCelMaterials.All(material =>
                    material.GetFloat("_AmbientStrength") > 1f), Is.True);
                Assert.That(playerMaterials.Any(material =>
                    material.shader.name == ActorCelShadingPresenter.OutlineShaderName),
                    Is.True);
                Assert.That(motor.MovementCommandSource, Is.SameAs(input));
                Assert.That(input.ViewTransform, Is.Not.Null);
                Assert.That(input.InputEnabled, Is.True);
                Assert.That(locomotionPresenter.Motor, Is.SameAs(motor));
                Assert.That(
                    locomotionPresenter.AnimationCoordinator,
                    Is.SameAs(presenter));
                Assert.That(presenter.Profile, Is.Not.Null);
                Assert.That(presenter.Profile.AnimatorController, Is.Not.Null);
                Assert.That(animator.runtimeAnimatorController,
                    Is.SameAs(presenter.Profile.AnimatorController));
                AnimatorController generatedController =
                    presenter.Profile.AnimatorController as AnimatorController;
                Assert.That(generatedController, Is.Not.Null);
                AnimatorStateMachine locomotionStateMachine =
                    generatedController.layers[0].stateMachine;
                AnimatorState standingLocomotion = locomotionStateMachine.states
                    .Single(state => state.state.name == "Standing Locomotion")
                    .state;
                AnimatorState crouchedLocomotion = locomotionStateMachine.states
                    .Single(state => state.state.name == "Crouched Locomotion")
                    .state;
                Assert.That(locomotionStateMachine.defaultState,
                    Is.SameAs(standingLocomotion));
                Assert.That(standingLocomotion.motion, Is.TypeOf<BlendTree>());
                var standingBlendTree = (BlendTree)standingLocomotion.motion;
                Assert.That(
                    standingBlendTree.children
                        .Single(child => child.position == Vector2.zero)
                        .motion,
                    Is.TypeOf<AnimationClip>());
                AnimatorControllerLayer turnLayer = generatedController.layers
                    .Single(layer =>
                        layer.name == ActorAnimationParameters.TurnLayerName);
                Assert.That(turnLayer.avatarMask, Is.Not.Null);
                Assert.That(turnLayer.defaultWeight, Is.Zero);
                Assert.That(turnLayer.iKPass, Is.False);
                Assert.That(
                    turnLayer.avatarMask.GetHumanoidBodyPartActive(
                        AvatarMaskBodyPart.LeftLeg),
                    Is.True);
                Assert.That(
                    turnLayer.avatarMask.GetHumanoidBodyPartActive(
                        AvatarMaskBodyPart.RightLeg),
                    Is.True);
                Assert.That(
                    turnLayer.avatarMask.GetHumanoidBodyPartActive(
                        AvatarMaskBodyPart.Body),
                    Is.False);
                Assert.That(
                    turnLayer.avatarMask.GetHumanoidBodyPartActive(
                        AvatarMaskBodyPart.LeftArm),
                    Is.False);
                Assert.That(
                    turnLayer.avatarMask.GetHumanoidBodyPartActive(
                        AvatarMaskBodyPart.RightArm),
                    Is.False);
                string hipsPath = AnimationUtility.CalculateTransformPath(
                    animator.GetBoneTransform(HumanBodyBones.Hips),
                    animator.transform);
                string spinePath = AnimationUtility.CalculateTransformPath(
                    animator.GetBoneTransform(HumanBodyBones.Spine),
                    animator.transform);
                int hipsMaskIndex = Enumerable.Range(
                        0,
                        turnLayer.avatarMask.transformCount)
                    .Single(index =>
                        turnLayer.avatarMask.GetTransformPath(index) ==
                        hipsPath);
                int spineMaskIndex = Enumerable.Range(
                        0,
                        turnLayer.avatarMask.transformCount)
                    .Single(index =>
                        turnLayer.avatarMask.GetTransformPath(index) ==
                        spinePath);
                Assert.That(
                    turnLayer.avatarMask.GetTransformActive(hipsMaskIndex),
                    Is.True);
                Assert.That(
                    turnLayer.avatarMask.GetTransformActive(spineMaskIndex),
                    Is.False);
                AnimatorState turnState = turnLayer.stateMachine.defaultState;
                ActorAnimationProfile animationProfile =
                    AssetDatabase.LoadAssetAtPath<ActorAnimationProfile>(
                        DefaultActorAssetGenerator.ProfilePath);
                Assert.That(
                    turnState.name,
                    Is.EqualTo(ActorAnimationParameters.TurnInPlaceStateName));
                Assert.That(
                    turnState.speed,
                    Is.EqualTo(
                        animationProfile.TurnInPlace.PlaybackSpeed)
                        .Within(0.001f));
                BlendTree turnInPlaceBlendTree = turnState.motion as BlendTree;
                Assert.That(turnInPlaceBlendTree, Is.Not.Null);
                Assert.That(turnInPlaceBlendTree.blendType,
                    Is.EqualTo(BlendTreeType.Simple1D));
                Assert.That(turnInPlaceBlendTree.blendParameter,
                    Is.EqualTo(ActorAnimationParameters.TurnRateName));
                Assert.That(turnInPlaceBlendTree.children.Select(
                        child => child.threshold),
                    Is.EqualTo(new[]
                    {
                        -1f,
                        0f,
                        1f,
                    }));
                Assert.That(crouchedLocomotion.motion, Is.TypeOf<BlendTree>());
                var crouchedBlendTree = (BlendTree)crouchedLocomotion.motion;
                Assert.That(crouchedBlendTree.blendType,
                    Is.EqualTo(BlendTreeType.Simple1D));
                Assert.That(crouchedBlendTree.blendParameter,
                    Is.EqualTo(ActorAnimationParameters.SpeedName));
                Assert.That(crouchedBlendTree.children.Select(
                        child => child.motion.name),
                    Is.EqualTo(new[]
                    {
                        "1Hand_Up_Crouch_Idle_1",
                        "1Hand_Up_Crouch_F_InPlace",
                    }));
                Assert.That(standingLocomotion.transitions.Any(transition =>
                    transition.destinationState == crouchedLocomotion &&
                    transition.conditions.Any(condition =>
                        condition.parameter == ActorAnimationParameters.StanceName &&
                        condition.mode == AnimatorConditionMode.Equals &&
                        condition.threshold == (int)ActorStance.Crouched)), Is.True);
                Assert.That(animator.applyRootMotion, Is.False);
                Assert.That(animator.avatar, Is.Not.Null);
                Assert.That(animator.avatar.isValid, Is.True);
                Assert.That(animator.avatar.isHuman, Is.True);
                Assert.That(target, Is.Not.Null);
                ActorCelShadingPresenter targetCelShading =
                    target.GetComponent<ActorCelShadingPresenter>();
                Assert.That(targetCelShading, Is.Not.Null);
                Assert.That(targetCelShading.IsApplied, Is.True);
                Material[] targetMaterials = target
                    .GetComponentsInChildren<Renderer>()
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null)
                    .ToArray();
                Assert.That(targetMaterials, Is.Not.Empty);
                Assert.That(targetMaterials.Any(material =>
                    material.shader.name == GameplayCelMaterialStyle.ShaderName),
                    Is.True);
                GameplayEnemyController enemies =
                    bootstrap.GetComponent<GameplayEnemyController>();
                Assert.That(enemies, Is.Not.Null);
                Assert.That(enemies.EnemyCount, Is.EqualTo(1));
                TargetAcquisitionPresenter targetAcquisition =
                    bootstrap.GetComponent<TargetAcquisitionPresenter>();
                Assert.That(targetAcquisition, Is.Not.Null);
                Assert.That(targetAcquisition.IsBound, Is.True);
                Assert.That(targetAcquisition.HasPointerTarget, Is.False);
                Assert.That(targetAcquisition.TargetOutlineVisible, Is.False);
                Assert.That(targetAcquisition.GroundHaloVisible, Is.False);
                Camera gameplayCamera = GameObject.Find("Gameplay Camera")?.GetComponent<Camera>();
                Assert.That(gameplayCamera, Is.Not.Null);
                Assert.That(gameplayCamera.orthographic, Is.False);
                GameplayCameraController cameraController = gameplayCamera
                    .GetComponent<GameplayCameraController>();
                Assert.That(cameraController, Is.Not.Null);
                Assert.That(cameraController.View,
                    Is.EqualTo(GameplayCameraView.ThirdPerson));
                GameplayPlayerCutoutPresenter playerCutout = gameplayCamera
                    .GetComponent<GameplayPlayerCutoutPresenter>();
                Assert.That(playerCutout, Is.Not.Null);
                Assert.That(playerCutout.IsBound, Is.True);
                Assert.That(playerCutout.Target, Is.SameAs(player.transform));
                playerCutout.RefreshNow();
                Assert.That(playerCutout.CurrentShaderData.z,
                    Is.EqualTo(GameplayPlayerCutoutPresenter.ViewportRadius));
                Assert.That(playerCutout.CurrentShaderData.w, Is.GreaterThan(0f));
                Assert.That(
                    playerCutout.CurrentLeftExtension,
                    Is.EqualTo(
                        GameplayPlayerCutoutPresenter.LeftViewportExtension));
                Assert.That(playerCutout.PresentationEnabled, Is.True);
                Assert.That(Vector3.Distance(
                    gameplayCamera.transform.position,
                    player.transform.position), Is.InRange(2.75f, 3.75f));
                Vector3 playerViewport = gameplayCamera.WorldToViewportPoint(
                    player.transform.position + Vector3.up);
                Assert.That(playerViewport.z, Is.GreaterThan(0f));
                Assert.That(playerViewport.x, Is.InRange(0f, 1f));
                Assert.That(playerViewport.x, Is.GreaterThan(0.5f));
                Assert.That(playerViewport.y, Is.InRange(0f, 1f));
                int localPlayerLayer = LayerMask.NameToLayer(
                    GameplayCameraController.LocalPlayerLayerName);
                int localPlayerMask = 1 << localPlayerLayer;
                Assert.That(localPlayerLayer, Is.GreaterThanOrEqualTo(0));
                Assert.That(player.GetComponentsInChildren<Renderer>()
                    .All(renderer => renderer.gameObject.layer == localPlayerLayer),
                    Is.True);
                Assert.That(gameplayCamera.cullingMask & localPlayerMask,
                    Is.EqualTo(localPlayerMask));

                cameraController.ToggleView();
                cameraController.RefreshNow();

                ActorStancePresenter stancePresenter =
                    player.GetComponent<ActorStancePresenter>();
                Assert.That(cameraController.View,
                    Is.EqualTo(GameplayCameraView.FirstPerson));
                Assert.That(gameplayCamera.transform.position,
                    Is.EqualTo(stancePresenter.FirstPersonEyePosition)
                        .Using(Vector3ComparerWithEqualsOperator.Instance));
                Assert.That(gameplayCamera.cullingMask & localPlayerMask, Is.Zero);
                Assert.That(playerCutout.PresentationEnabled, Is.False);
                Assert.That(playerCutout.CurrentShaderData, Is.EqualTo(Vector4.zero));
                Assert.That(playerCutout.CurrentLeftExtension, Is.Zero);

                cameraController.ToggleView();
                cameraController.RefreshNow();

                Assert.That(cameraController.View,
                    Is.EqualTo(GameplayCameraView.ThirdPerson));
                Assert.That(gameplayCamera.cullingMask & localPlayerMask,
                    Is.EqualTo(localPlayerMask));
                Assert.That(playerCutout.PresentationEnabled, Is.True);
                Assert.That(Vector3.Distance(
                    gameplayCamera.transform.position,
                    player.transform.position), Is.InRange(2.75f, 3.75f));
                Assert.That(sceneCamera.gameObject.activeSelf, Is.False);
                LevelEntityView[] levelEntities = Object.FindObjectsByType<LevelEntityView>(
                    FindObjectsInactive.Exclude);
                Renderer wallRenderer = levelEntities
                    .Single(entity => entity.EntityId == "wall-south-01")
                    .GetComponentsInChildren<Renderer>()
                    .First(renderer => renderer.sharedMaterial.shader.name
                        == "GritGud/CelSurface");
                Renderer floorRenderer = levelEntities
                    .Single(entity => entity.EntityId == "floor-r01-c01")
                    .GetComponentsInChildren<Renderer>()
                    .First(renderer => renderer.sharedMaterial.shader.name
                        == "GritGud/CelSurface");
                Assert.That(
                    wallRenderer.sharedMaterial.GetFloat("_PlayerCutoutEnabled"),
                    Is.EqualTo(1f));
                Assert.That(
                    floorRenderer.sharedMaterial.GetFloat("_PlayerCutoutEnabled"),
                    Is.EqualTo(0f));

                hud.RequestTurnModeToggle();
                Assert.That(gameplay.Session.Mode,
                    Is.EqualTo(GameplaySessionMode.TurnBased));
                Assert.That(hud.IsCommandBarVisible, Is.True);
                Assert.That(hud.AreTurnResourcesVisible, Is.True);
                Assert.That(hud.IsEndTurnAvailable, Is.True);
                Assert.That(gameplay.Session.ActiveActorId,
                    Is.EqualTo("oren-vale"));
                Assert.That(input.InputEnabled, Is.False);
                GameplayActorView activePartyView = gameplay.WorldRegistry
                    .GetActor("oren-vale");
                ExplorationMovementInput activePartyInput =
                    activePartyView.MovementInput;
                ActorAnimationCoordinator activePartyPresenter =
                    activePartyView.Root.GetComponent<ActorAnimationCoordinator>();
                Animator activePartyAnimator =
                    activePartyView.Transform.GetComponentInChildren<Animator>();
                Assert.That(activePartyInput.InputEnabled, Is.False);
                Assert.That(activePartyInput.InputSource,
                    Is.SameAs(inputController));
                Assert.That(input.InputSource, Is.Null);
                Assert.That(gameplay.PartyControl.SelectedActorId,
                    Is.EqualTo("oren-vale"));
                Assert.That(gameplay.PartyControl.CommandActorId,
                    Is.EqualTo("oren-vale"));
                Assert.That(gameplay.PartyHud.CurrentModel.Members[1].Selected,
                    Is.True);
                GameplayActorSnapshot playerState =
                    gameplay.Session.GetActor("oren-vale");
                Assert.That(playerState.Pose.Position.X,
                    Is.EqualTo(activePartyView.Transform.position.x).Within(0.001f));
                Assert.That(playerState.Pose.Position.Y,
                    Is.EqualTo(activePartyView.Transform.position.y).Within(0.001f));
                Assert.That(playerState.Pose.Position.Z,
                    Is.EqualTo(activePartyView.Transform.position.z).Within(0.001f));
                CharacterController characterController =
                    activePartyView.Transform.GetComponent<CharacterController>();
                float standingHeight = characterController.height;
                Assert.That(activePartyAnimator.GetInteger(
                        ActorAnimationParameters.Stance),
                    Is.EqualTo((int)ActorStance.Standing));
                Assert.That(sessionPresenter.ToggleStance(), Is.True);
                Assert.That(
                    gameplay.Session.GetActor("oren-vale").Pose.Stance,
                    Is.EqualTo(ActorStance.Crouched));
                Assert.That(characterController.height, Is.LessThan(standingHeight));
                Assert.That(activePartyAnimator.GetInteger(
                        ActorAnimationParameters.Stance),
                    Is.EqualTo((int)ActorStance.Crouched));
                Assert.That(sessionPresenter.ToggleStance(), Is.True);
                Assert.That(
                    gameplay.Session.GetActor("oren-vale").Pose.Stance,
                    Is.EqualTo(ActorStance.Standing));
                Assert.That(characterController.height,
                    Is.EqualTo(standingHeight).Within(0.001f));
                Assert.That(activePartyAnimator.GetInteger(
                        ActorAnimationParameters.Stance),
                    Is.EqualTo((int)ActorStance.Standing));
                Assert.That(activePartyPresenter, Is.Not.Null);
                Assert.That(actions.EvaluateInteraction(),
                    Is.EqualTo(GameplayActionFailure.TargetOutOfRange));
                float activePartyMovementOpportunity = gameplay.Session
                    .GetActor("oren-vale")
                    .TurnBudget.MovementOpportunity;
                gameplay.Session.SpendMovement(
                    "oren-vale",
                    activePartyMovementOpportunity);
                Assert.That(turnMovement.SynchronizePlanningState(), Is.True);
                Assert.That(turnMovement.PlanningMaximumCost, Is.Zero);
                Assert.That(turnMovement.PlanPointCount, Is.EqualTo(1));
                Assert.That(actions.TryEndTurn(), Is.True);
                Assert.That(actions.LastTurnEndFailure,
                    Is.EqualTo(TurnEndFailure.None));
                Assert.That(gameplay.Session.Mode,
                    Is.EqualTo(GameplaySessionMode.TurnBased));
                Assert.That(gameplay.Session.Operation,
                    Is.EqualTo(GameplaySessionOperation.ResolvingWorldTurn));
                Assert.That(actions.StatusMessage,
                    Is.EqualTo("World turn resolving..."));
                Assert.That(hud.IsEndTurnAvailable, Is.False);
                Assert.That(gameplay.Session.LastCompletedVoluntaryTurnCycle,
                    Is.Null);
                Assert.That(turnMovement.SynchronizePlanningState(), Is.False);
                Assert.That(turnMovement.PlanPointCount, Is.Zero);
                Assert.That(gameplay.Session.CompleteVoluntaryWorldTurn(), Is.True);
                Assert.That(turnMovement.SynchronizePlanningState(), Is.True);
                Assert.That(turnMovement.PlanningMaximumCost,
                    Is.EqualTo(activePartyMovementOpportunity));
                Assert.That(turnMovement.PlanPointCount, Is.EqualTo(1));
                Assert.That(hud.IsEndTurnAvailable, Is.True);
                Assert.That(gameplay.Session.LastCompletedVoluntaryTurnCycle,
                    Is.Not.Null);
                hud.RequestTurnModeToggle();
                Assert.That(gameplay.Session.Mode,
                    Is.EqualTo(GameplaySessionMode.Exploration));
                Assert.That(hud.IsCommandBarVisible, Is.True);
                Assert.That(hud.AreTurnResourcesVisible, Is.False);
                Assert.That(activePartyInput.InputEnabled, Is.True);
                Assert.That(input.InputEnabled, Is.False);
                Assert.That(gameplay.Session.ResolvedActions, Is.Empty);
                Assert.That(gameplay.Session.LastCompletedVoluntaryTurnCycle,
                    Is.Not.Null);
                hud.RequestTurnModeToggle();
                Assert.That(gameplay.Session.Mode,
                    Is.EqualTo(GameplaySessionMode.Exploration));
                Assert.That(actions.LastTurnModeEntryFailure,
                    Is.EqualTo(TurnModeEntryFailure.VoluntaryReentryLocked));

                Assert.That(gameplay.Session.BeginEncounter(), Is.True);
                sessionPresenter.RefreshModePresentation();
                Assert.That(gameplay.Session.ActiveActorId,
                    Is.EqualTo("oren-vale"));
                Assert.That(actions.CanExitTurnMode, Is.False);
                Assert.That(
                    hud.CurrentModel.CommandBar.FindCommand(
                        GameplayControl.ToggleTurnMode).Enabled,
                    Is.False);
                var activeFlightConstraint =
                    new MutableTurnModeExitConstraint
                    {
                        BlocksTurnModeExit = true,
                    };
                actions.RegisterTurnModeExitConstraint(activeFlightConstraint);
                Assert.That(actions.CanExitTurnMode, Is.False);
                Assert.That(actions.TryExitTurnMode(), Is.False);
                Assert.That(gameplay.Session.EncounterActive, Is.True);
                Assert.That(actions.StatusMessage,
                    Is.EqualTo(activeFlightConstraint.TurnModeExitBlockedMessage));
                activeFlightConstraint.BlocksTurnModeExit = false;
                Assert.That(actions.CanExitTurnMode, Is.False);
                Assert.That(actions.TryExitTurnMode(), Is.False);
                Assert.That(actions.StatusMessage,
                    Is.EqualTo(
                        "Hostile actors are still capable of responding."));
                Assert.That(actions.TryEndTurn(), Is.True);
                Assert.That(gameplay.Session.Mode,
                    Is.EqualTo(GameplaySessionMode.TurnBased));
                Assert.That(gameplay.Session.ActiveActorId,
                    Is.EqualTo("player"));
                Assert.That(hud.IsEndTurnAvailable, Is.True);
                Assert.That(gameplay.Session.LastEndedTurn.EndingActorId,
                    Is.EqualTo("oren-vale"));
                Assert.That(actions.TryEndTurn(), Is.True);
                Assert.That(gameplay.Session.ActiveActorId,
                    Is.EqualTo("depot-rifleman"));
                Assert.That(hud.IsEndTurnAvailable, Is.False);
                Assert.That(gameplay.Session.LastEndedTurn.EndingActorId,
                    Is.EqualTo("player"));
                Assert.That(gameplay.Session.CompleteEncounter(), Is.True);
                sessionPresenter.RefreshModePresentation();
                Assert.That(actions.CanExitTurnMode, Is.True);
                Assert.That(actions.TryExitTurnMode(), Is.True);
                Assert.That(gameplay.Session.EncounterActive, Is.False);
                Assert.That(gameplay.Session.Mode,
                    Is.EqualTo(GameplaySessionMode.Exploration));

                bootstrap.ReturnToMenu();
                Assert.That(bootstrap.CurrentMode, Is.EqualTo(ApplicationMode.Menu));
                Assert.That(gameplay.IsRunning, Is.False);
                Assert.That(RenderSettings.fog, Is.EqualTo(originalFog));
                Assert.That(GameObject.Find("Gameplay Environment Lighting"), Is.Null);
                Assert.That(GameObject.Find("Gameplay Post Processing"), Is.Null);
                Assert.That(inputController.IsActive, Is.False);
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
                Assert.That(Cursor.visible, Is.True);
                Assert.That(hud.IsVisible, Is.False);
                Assert.That(hud.IsFlyoutExpanded, Is.False);
                Assert.That(hud.Session, Is.Null);
                Assert.That(hud.IsCommandBarVisible, Is.False);
                Assert.That(sessionPresenter.Session, Is.Null);
                Assert.That(turnMovement.Session, Is.Null);
                Assert.That(hud.TurnMovement, Is.Null);
                Assert.That(actions.Session, Is.Null);
                Assert.That(attacks.Session, Is.Null);
                Assert.That(hud.ActionController, Is.Null);
                Assert.That(hud.AttackController, Is.Null);
                Assert.That(objectivePresenter.Session, Is.Null);
                Assert.That(objectivePresenter.IsPresented, Is.False);
                Assert.That(sceneCamera.gameObject.activeSelf, Is.True);
                Assert.That(
                    Shader.GetGlobalVector("_GritGudPlayerCutout"),
                    Is.EqualTo(Vector4.zero));
                Assert.That(
                    Shader.GetGlobalFloat("_GritGudPlayerCutoutLeftExtension"),
                    Is.Zero);
            }
            finally
            {
                bootstrap.ReturnToMenu();
                if (ownedApplication != null)
                {
                    Object.DestroyImmediate(ownedApplication);
                }

                if (ownedCamera != null)
                {
                    Object.DestroyImmediate(ownedCamera);
                }
            }
        }

        private sealed class MutableTurnModeExitConstraint :
            IGameplayTurnModeExitConstraint
        {
            public bool BlocksTurnModeExit { get; set; }

            public string TurnModeExitBlockedMessage =>
                "The test flight is still active.";
        }

        private sealed class EmptyGameplayInputSource : IGameplayInputSource
        {
            public GameplayInputFrame CurrentFrame => default;

            public string GetBindingDisplay(GameplayControl control) =>
                string.Empty;
        }
    }
}
