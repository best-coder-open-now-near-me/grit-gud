using GritGud.Application.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    public readonly struct GameplayInputFrame
    {
        public GameplayInputFrame(
            Vector2 movement,
            Vector2 lookDelta,
            bool sprintHeld,
            bool aimHeld,
            bool cancelRoutePressed,
            bool undoRoutePressed,
            bool confirmRoutePressed,
            float cameraZoomDelta = 0f)
        {
            Movement = Vector2.ClampMagnitude(movement, 1f);
            LookDelta = lookDelta;
            SprintHeld = sprintHeld;
            AimHeld = aimHeld;
            CancelRoutePressed = cancelRoutePressed;
            UndoRoutePressed = undoRoutePressed;
            ConfirmRoutePressed = confirmRoutePressed;
            CameraZoomDelta = cameraZoomDelta;
        }

        public Vector2 Movement { get; }

        public Vector2 LookDelta { get; }

        public bool SprintHeld { get; }

        public bool AimHeld { get; }

        public float CameraZoomDelta { get; }

        public bool CancelRoutePressed { get; }

        public bool UndoRoutePressed { get; }

        public bool ConfirmRoutePressed { get; }

        public bool WasPressed(GameplayControl control)
        {
            switch (control)
            {
                case GameplayControl.CancelRoute:
                    return CancelRoutePressed;
                case GameplayControl.UndoRoute:
                    return UndoRoutePressed;
                case GameplayControl.ConfirmRoute:
                    return ConfirmRoutePressed;
                default:
                    return false;
            }
        }
    }

    public interface IGameplayInputSource
    {
        GameplayInputFrame CurrentFrame { get; }

        string GetBindingDisplay(GameplayControl control);
    }
}
