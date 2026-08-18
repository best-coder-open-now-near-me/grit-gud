using System.Linq;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

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
                    System.Array.Empty<ScenarioObjectiveDefinition>()),
                    scenarioSeed: 3u);
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
                    session);
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

        [Test]
        public void HotbarClassificationExcludesAdjacentControls()
        {
            foreach (GameplayControl control in System.Enum.GetValues(
                typeof(GameplayControl)))
            {
                bool expected = control == GameplayControl.Hotbar1
                    || control == GameplayControl.Hotbar2
                    || control == GameplayControl.Hotbar3
                    || control == GameplayControl.Hotbar4
                    || control == GameplayControl.Hotbar5
                    || control == GameplayControl.Hotbar6
                    || control == GameplayControl.Hotbar7
                    || control == GameplayControl.Hotbar8;
                Assert.That(
                    GameplayControlRouter.IsHotbarControl(control),
                    Is.EqualTo(expected),
                    control.ToString());
            }

            Assert.That(
                GameplayControlRouter.IsHotbarControl(
                    GameplayControl.CancelPendingAction),
                Is.False);
        }

        [TestCase(GameplayControl.Hotbar1, 1)]
        [TestCase(GameplayControl.Hotbar2, 2)]
        [TestCase(GameplayControl.Hotbar3, 3)]
        [TestCase(GameplayControl.Hotbar4, 4)]
        [TestCase(GameplayControl.Hotbar5, 5)]
        [TestCase(GameplayControl.Hotbar6, 6)]
        [TestCase(GameplayControl.Hotbar7, 7)]
        [TestCase(GameplayControl.Hotbar8, 8)]
        public void HotbarNumbersUseExplicitControlMapping(
            GameplayControl control,
            int expectedNumber)
        {
            Assert.That(
                GameplayControlRouter.ResolveHotbarNumber(control),
                Is.EqualTo(expectedNumber));
        }

        [Test]
        public void NonHotbarControlHasNoSlotNumber()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                GameplayControlRouter.ResolveHotbarNumber(
                    GameplayControl.CancelPendingAction));
        }

        private sealed class EmptyGameplayInputSource : IGameplayInputSource
        {
            public GameplayInputFrame CurrentFrame => default;

            public string GetBindingDisplay(GameplayControl control) =>
                string.Empty;
        }
    }
}
