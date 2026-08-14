using UnityEngine;
using UnityEngine.InputSystem;

namespace GritGud.Presentation.LevelEditing.Core
{
    public readonly struct LevelEditorPointerState
    {
        public LevelEditorPointerState(
            Vector2 position,
            Vector2 delta,
            bool pressed,
            bool held,
            bool released)
        {
            Position = position;
            Delta = delta;
            Pressed = pressed;
            Held = held;
            Released = released;
        }

        public Vector2 Position { get; }
        public Vector2 Delta { get; }
        public bool Pressed { get; }
        public bool Held { get; }
        public bool Released { get; }
    }

    public struct LevelEditorInputState
    {
        public Vector2 PointerPosition { get; set; }
        public Vector2 PointerDelta { get; set; }
        public float MoveForward { get; set; }
        public float MoveRight { get; set; }
        public float CameraRotation { get; set; }
        public float ZoomDelta { get; set; }
        public bool PointerBlocked { get; set; }
        public bool PrimaryPressed { get; set; }
        public bool PrimaryHeld { get; set; }
        public bool PrimaryReleased { get; set; }
        public bool MiddleHeld { get; set; }
        public bool SecondaryHeld { get; set; }
        public bool FastCameraMovement { get; set; }
        public bool AdditiveSelection { get; set; }
        public bool RotatePressed { get; set; }
        public bool DeletePressed { get; set; }
        public bool CancelPressed { get; set; }
        public bool UndoPressed { get; set; }
        public bool RedoPressed { get; set; }
        public bool CopyPressed { get; set; }
        public bool PastePressed { get; set; }
        public bool DuplicatePressed { get; set; }
        public bool FrameSelectionPressed { get; set; }
        public bool FrameLevelPressed { get; set; }
    }

    public sealed class LevelEditorInputRouter
    {
        public Vector2 PointerPosition => ReadPointer().Position;

        public LevelEditorInputState Capture(bool pointerBlocked, bool keyboardCaptured)
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            LevelEditorPointerState pointer = ReadPointer();
            bool control = keyboard != null
                && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);

            return new LevelEditorInputState
            {
                PointerPosition = pointer.Position,
                PointerDelta = pointer.Delta,
                MoveForward = keyboardCaptured || keyboard == null
                    ? 0f
                    : (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                        - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f),
                MoveRight = keyboardCaptured || keyboard == null
                    ? 0f
                    : (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                        - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f),
                CameraRotation = keyboardCaptured || keyboard == null
                    ? 0f
                    : (keyboard.eKey.isPressed ? 1f : 0f) - (keyboard.qKey.isPressed ? 1f : 0f),
                ZoomDelta = mouse?.scroll.ReadValue().y ?? 0f,
                PointerBlocked = pointerBlocked,
                PrimaryPressed = pointer.Pressed,
                PrimaryHeld = pointer.Held,
                PrimaryReleased = pointer.Released,
                MiddleHeld = mouse != null && mouse.middleButton.isPressed,
                SecondaryHeld = mouse != null && mouse.rightButton.isPressed,
                FastCameraMovement = !keyboardCaptured && keyboard != null
                    && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed),
                AdditiveSelection = !keyboardCaptured && control,
                RotatePressed = !keyboardCaptured && keyboard != null && keyboard.rKey.wasPressedThisFrame,
                DeletePressed = !keyboardCaptured && keyboard != null && keyboard.deleteKey.wasPressedThisFrame,
                CancelPressed = !keyboardCaptured && keyboard != null && keyboard.escapeKey.wasPressedThisFrame,
                UndoPressed = !keyboardCaptured && control && keyboard.zKey.wasPressedThisFrame,
                RedoPressed = !keyboardCaptured && control && keyboard.yKey.wasPressedThisFrame,
                CopyPressed = !keyboardCaptured && control && keyboard.cKey.wasPressedThisFrame,
                PastePressed = !keyboardCaptured && control && keyboard.vKey.wasPressedThisFrame,
                DuplicatePressed = !keyboardCaptured && control && keyboard.dKey.wasPressedThisFrame,
                FrameSelectionPressed = !keyboardCaptured && keyboard != null
                    && keyboard.fKey.wasPressedThisFrame,
                FrameLevelPressed = !keyboardCaptured && keyboard != null
                    && keyboard.homeKey.wasPressedThisFrame,
            };
        }

        public static LevelEditorPointerState SelectPointer(
            bool touchActive,
            LevelEditorPointerState touch,
            LevelEditorPointerState mouse)
        {
            return touchActive ? touch : mouse;
        }

        private static LevelEditorPointerState ReadPointer()
        {
            Mouse mouse = Mouse.current;
            var mouseState = new LevelEditorPointerState(
                mouse?.position.ReadValue() ?? Vector2.zero,
                mouse?.delta.ReadValue() ?? Vector2.zero,
                mouse != null && mouse.leftButton.wasPressedThisFrame,
                mouse != null && mouse.leftButton.isPressed,
                mouse != null && mouse.leftButton.wasReleasedThisFrame);
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                return mouseState;
            }

            var touch = touchscreen.primaryTouch;
            bool touchActive = touch.press.isPressed
                || touch.press.wasPressedThisFrame
                || touch.press.wasReleasedThisFrame;
            var touchState = new LevelEditorPointerState(
                touch.position.ReadValue(),
                touch.delta.ReadValue(),
                touch.press.wasPressedThisFrame,
                touch.press.isPressed,
                touch.press.wasReleasedThisFrame);
            return SelectPointer(touchActive, touchState, mouseState);
        }
    }
}
