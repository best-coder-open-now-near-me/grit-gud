using System;
using GritGud.Application.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GritGud.Presentation.Gameplay
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class GameplayInputController : MonoBehaviour, IGameplayInputSource
    {
        internal const string InputActionsResource =
            "Input/gameplay-input-actions";

        private Action<GameplayControl> commandRequested;
        private InputActionAsset inputActions;
        private InputAction move;
        private InputAction sprint;
        private InputAction look;
        private InputAction aim;
        private InputAction cameraZoom;
        private InputAction attack;
        private InputAction toggleTurnMode;
        private InputAction toggleStance;
        private InputAction toggleCameraView;
        private InputAction exportBugReport;
        private InputAction interact;
        private InputAction endTurn;
        private InputAction cancelRoute;
        private InputAction undoRoute;
        private InputAction confirmRoute;
        private InputAction cyclePartyMember;
        private InputAction escape;
        private InputAction cancelPendingAction;
        private readonly InputAction[] hotbar = new InputAction[8];

        public bool IsActive => enabled && inputActions != null;

        public GameplayInputFrame CurrentFrame { get; private set; }

        public void Begin(Action<GameplayControl> onCommandRequested)
        {
            End();
            Action<GameplayControl> requested = onCommandRequested ??
                throw new ArgumentNullException(nameof(onCommandRequested));
            try
            {
                inputActions = CreateInputAsset();
                move = RequireAction("Move");
                sprint = RequireAction("Sprint");
                look = RequireAction("Look");
                aim = RequireAction("Aim");
                cameraZoom = RequireAction("CameraZoom");
                attack = RequireAction("Attack");
                toggleTurnMode = RequireAction("ToggleTurnMode");
                toggleStance = RequireAction("ToggleStance");
                toggleCameraView = RequireAction("ToggleCameraView");
                exportBugReport = RequireAction("ExportBugReport");
                interact = RequireAction("Interact");
                endTurn = RequireAction("EndTurn");
                cancelRoute = RequireAction("CancelRoute");
                undoRoute = RequireAction("UndoRoute");
                confirmRoute = RequireAction("ConfirmRoute");
                cyclePartyMember = RequireAction("CyclePartyMember");
                escape = RequireAction("Escape");
                cancelPendingAction = RequireAction("CancelPendingAction");
                for (int index = 0; index < hotbar.Length; index++)
                {
                    hotbar[index] = RequireAction("Hotbar" + (index + 1));
                }
                inputActions.Enable();
                commandRequested = requested;
                ReleaseCursor();
                enabled = true;
            }
            catch
            {
                End();
                throw;
            }
        }

        public void End()
        {
            CurrentFrame = default;
            commandRequested = null;
            if (inputActions != null)
            {
                inputActions.Disable();
                GameplayObjectLifecycle.Destroy(inputActions);
            }

            inputActions = null;
            move = null;
            sprint = null;
            look = null;
            aim = null;
            cameraZoom = null;
            attack = null;
            toggleTurnMode = null;
            toggleStance = null;
            toggleCameraView = null;
            exportBugReport = null;
            interact = null;
            endTurn = null;
            cancelRoute = null;
            undoRoute = null;
            confirmRoute = null;
            cyclePartyMember = null;
            escape = null;
            cancelPendingAction = null;
            for (int index = 0; index < hotbar.Length; index++)
            {
                hotbar[index] = null;
            }
            ReleaseCursor();
            enabled = false;
        }

        public string GetBindingDisplay(GameplayControl control)
        {
            InputAction action = GetAction(control);
            if (action == null)
            {
                return string.Empty;
            }

            string display = action.GetBindingDisplayString();
            return NormalizeBindingDisplay(display);
        }

        private void Update()
        {
            ReleaseCursor();
            if (inputActions == null)
            {
                CurrentFrame = default;
                return;
            }

            CurrentFrame = new GameplayInputFrame(
                move.ReadValue<Vector2>(),
                look.ReadValue<Vector2>(),
                sprint.IsPressed(),
                aim.IsPressed(),
                cancelRoute.WasPressedThisFrame(),
                undoRoute.WasPressedThisFrame(),
                confirmRoute.WasPressedThisFrame(),
                cameraZoom.ReadValue<float>());
            if (escape.WasPressedThisFrame())
            {
                HandleEscapePressed();
            }

            DispatchIfPressed(toggleTurnMode, GameplayControl.ToggleTurnMode);
            DispatchIfPressed(attack, GameplayControl.Attack);
            DispatchIfPressed(toggleStance, GameplayControl.ToggleStance);
            DispatchIfPressed(toggleCameraView, GameplayControl.ToggleCameraView);
            DispatchIfPressed(exportBugReport, GameplayControl.ExportBugReport);
            DispatchIfPressed(interact, GameplayControl.Interact);
            DispatchIfPressed(endTurn, GameplayControl.EndTurn);
            DispatchIfPressed(
                cyclePartyMember,
                GameplayControl.CyclePartyMember);
            for (int index = 0; index < hotbar.Length; index++)
            {
                DispatchIfPressed(
                    hotbar[index],
                    (GameplayControl)((int)GameplayControl.Hotbar1 + index));
            }
            DispatchIfPressed(
                cancelPendingAction,
                GameplayControl.CancelPendingAction);
        }

        internal void HandleEscapePressed()
        {
            ReleaseCursor();
        }

        internal static InputActionAsset CreateInputAsset()
        {
            TextAsset source = Resources.Load<TextAsset>(InputActionsResource);
            if (source == null)
            {
                throw new InvalidOperationException(
                    $"Gameplay input actions '{InputActionsResource}' were not found.");
            }

            InputActionAsset asset = InputActionAsset.FromJson(source.text);
            return asset ?? throw new InvalidOperationException(
                "Gameplay input actions could not be parsed.");
        }

        private void DispatchIfPressed(InputAction action, GameplayControl control)
        {
            // Handling one command can end or replace the input session in
            // the same Update (for example when changing modes). The rest of
            // this frame must not dereference actions that teardown cleared.
            if (action != null && action.WasPressedThisFrame())
            {
                commandRequested?.Invoke(control);
            }
        }

        private InputAction GetAction(GameplayControl control)
        {
            if (inputActions == null)
            {
                return null;
            }

            switch (control)
            {
                case GameplayControl.Move:
                    return move;
                case GameplayControl.Sprint:
                    return sprint;
                case GameplayControl.AimLook:
                    return aim;
                case GameplayControl.CameraZoom:
                    return cameraZoom;
                case GameplayControl.Attack:
                    return attack;
                case GameplayControl.ToggleTurnMode:
                    return toggleTurnMode;
                case GameplayControl.ToggleStance:
                    return toggleStance;
                case GameplayControl.ToggleCameraView:
                    return toggleCameraView;
                case GameplayControl.ExportBugReport:
                    return exportBugReport;
                case GameplayControl.Interact:
                    return interact;
                case GameplayControl.EndTurn:
                    return endTurn;
                case GameplayControl.CancelRoute:
                    return cancelRoute;
                case GameplayControl.UndoRoute:
                    return undoRoute;
                case GameplayControl.ConfirmRoute:
                    return confirmRoute;
                case GameplayControl.CyclePartyMember:
                    return cyclePartyMember;
                case GameplayControl.Hotbar1:
                    return hotbar[0];
                case GameplayControl.Hotbar2:
                    return hotbar[1];
                case GameplayControl.Hotbar3:
                    return hotbar[2];
                case GameplayControl.Hotbar4:
                    return hotbar[3];
                case GameplayControl.Hotbar5:
                    return hotbar[4];
                case GameplayControl.Hotbar6:
                    return hotbar[5];
                case GameplayControl.Hotbar7:
                    return hotbar[6];
                case GameplayControl.Hotbar8:
                    return hotbar[7];
                case GameplayControl.CancelPendingAction:
                    return cancelPendingAction;
                default:
                    throw new ArgumentOutOfRangeException(nameof(control));
            }
        }

        private InputAction RequireAction(string actionName) =>
            inputActions.FindAction(actionName, throwIfNotFound: true);

        private static string NormalizeBindingDisplay(string display)
        {
            return (display ?? string.Empty)
                .Replace("Left Shift", "Shift")
                .Replace("Middle Button", "MMB")
                .Replace("Scroll/Y", "Wheel")
                .Replace("Scroll Y", "Wheel")
                .Replace("Right Button", "RMB")
                .Replace("Left Button", "LMB")
                .ToUpperInvariant();
        }

        private static void ReleaseCursor()
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
            }

            Cursor.visible = true;
        }

        private void OnDestroy()
        {
            End();
        }
    }
}
