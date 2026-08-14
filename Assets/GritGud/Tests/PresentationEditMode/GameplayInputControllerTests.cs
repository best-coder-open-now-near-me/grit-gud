using System.Linq;
using GritGud.Application.Gameplay;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayInputControllerTests
    {
        [Test]
        public void AuthoredInputActionsDefineEveryGameplaySemantic()
        {
            InputActionAsset asset = GameplayInputController.CreateInputAsset();
            try
            {
                InputActionMap gameplay = asset.FindActionMap(
                    "Gameplay",
                    throwIfNotFound: true);
                Assert.That(gameplay.FindAction("Move", true), Is.Not.Null);
                Assert.That(gameplay.FindAction("Sprint", true), Is.Not.Null);
                Assert.That(gameplay.FindAction("Look", true), Is.Not.Null);
                Assert.That(gameplay.FindAction("Aim", true), Is.Not.Null);
                Assert.That(gameplay.FindAction("CameraZoom", true), Is.Not.Null);
                Assert.That(gameplay.FindAction("Attack", true), Is.Not.Null);
                Assert.That(gameplay.FindAction("ToggleTurnMode", true), Is.Not.Null);
                Assert.That(gameplay.FindAction("ToggleStance", true), Is.Not.Null);
                Assert.That(gameplay.FindAction("ToggleCameraView", true), Is.Not.Null);
                Assert.That(gameplay.FindAction("ExportBugReport", true), Is.Not.Null);
                Assert.That(gameplay.FindAction("Interact", true), Is.Not.Null);
                Assert.That(gameplay.FindAction("EndTurn", true), Is.Not.Null);
                Assert.That(gameplay.FindAction("CancelRoute", true), Is.Not.Null);
                Assert.That(gameplay.FindAction("UndoRoute", true), Is.Not.Null);
                Assert.That(gameplay.FindAction("ConfirmRoute", true), Is.Not.Null);
                Assert.That(gameplay.FindAction("CyclePartyMember", true), Is.Not.Null);
                Assert.That(gameplay.FindAction("Escape", true), Is.Not.Null);
                for (int slot = 1; slot <= 8; slot++)
                {
                    Assert.That(
                        gameplay.FindAction("Hotbar" + slot, true),
                        Is.Not.Null);
                }
                Assert.That(
                    gameplay.FindAction("CancelPendingAction", true),
                    Is.Not.Null);
                Assert.That(
                    gameplay.FindAction("Move", true).bindings.Any(
                        binding => binding.isComposite),
                    Is.True);
                Assert.That(
                    gameplay.FindAction("CameraZoom", true).bindings
                        .Select(binding => binding.path),
                    Is.EquivalentTo(new[] { "<Mouse>/scroll/y" }));
                Assert.That(
                    gameplay.FindAction("CyclePartyMember", true).bindings
                        .Select(binding => binding.path),
                    Is.EquivalentTo(new[] { "<Keyboard>/tab" }));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void MovementConsumesASemanticFrameWithoutReadingHardware()
        {
            var actor = new GameObject("Semantic Input Actor");
            var view = new GameObject("Semantic Input View");
            try
            {
                ExplorationMovementInput movement =
                    actor.AddComponent<ExplorationMovementInput>();
                movement.BindView(view.transform);
                movement.BindInputSource(new FixedInputSource(
                    new GameplayInputFrame(
                        Vector2.right,
                        Vector2.zero,
                        sprintHeld: true,
                        aimHeld: false,
                        cancelRoutePressed: false,
                        undoRoutePressed: false,
                        confirmRoutePressed: false)));

                ActorMovementCommand command =
                    movement.ReadCameraRelativeCommand();

                Assert.That(command.WorldDirection.x, Is.EqualTo(1f));
                Assert.That(command.WorldDirection.z, Is.Zero);
                Assert.That(command.Sprint, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(view);
            }
        }

        private sealed class FixedInputSource : IGameplayInputSource
        {
            public FixedInputSource(GameplayInputFrame frame)
            {
                CurrentFrame = frame;
            }

            public GameplayInputFrame CurrentFrame { get; }

            public string GetBindingDisplay(GameplayControl control) =>
                control.ToString();
        }
    }
}
