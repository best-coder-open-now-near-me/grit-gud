using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayCameraRig : IDisposable
    {
        private Camera sceneCamera;
        private Camera gameplayCamera;
        private GameplayCameraController cameraController;
        private ReplayFreeCameraController freeCamera;
        private GameplayPlayerCutoutPresenter playerCutout;
        private ExplorationMovementInput movementInput;
        private readonly bool sceneCameraWasActive;

        private GameplayCameraRig(
            Camera sourceCamera,
            Camera sessionCamera,
            GameplayCameraController viewController,
            ReplayFreeCameraController replayFreeCamera,
            GameplayPlayerCutoutPresenter cutoutPresenter,
            ExplorationMovementInput input,
            bool sourceCameraWasActive)
        {
            sceneCamera = sourceCamera;
            gameplayCamera = sessionCamera;
            cameraController = viewController;
            freeCamera = replayFreeCamera;
            playerCutout = cutoutPresenter;
            movementInput = input;
            sceneCameraWasActive = sourceCameraWasActive;
            cameraController.ViewChanged += OnViewChanged;
            OnViewChanged(cameraController.View);
        }

        public static GameplayCameraRig Create(
            Transform target,
            ExplorationMovementInput movementInput,
            IGameplayInputSource inputSource,
            IReadOnlyList<Renderer> playerCutoutRenderers)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (movementInput == null)
            {
                throw new ArgumentNullException(nameof(movementInput));
            }

            if (inputSource == null)
            {
                throw new ArgumentNullException(nameof(inputSource));
            }

            Camera sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                throw new InvalidOperationException(
                    "Gameplay requires a camera tagged MainCamera.");
            }

            bool sceneCameraWasActive = sceneCamera.gameObject.activeSelf;
            Camera gameplayCamera = null;
            GameplayCameraController cameraController = null;
            ReplayFreeCameraController freeCamera = null;
            GameplayPlayerCutoutPresenter playerCutout = null;
            try
            {
                gameplayCamera = CreateGameplayCamera(sceneCamera);
                sceneCamera.gameObject.SetActive(false);
                cameraController = gameplayCamera.gameObject
                    .AddComponent<GameplayCameraController>();
                ActorStancePresenter stancePresenter =
                    target.GetComponent<ActorStancePresenter>();
                cameraController.Bind(target, inputSource, stancePresenter);
                freeCamera = gameplayCamera.gameObject
                    .AddComponent<ReplayFreeCameraController>();
                freeCamera.Configure(new ReplaySpectatorInput());
                playerCutout = gameplayCamera.gameObject
                    .AddComponent<GameplayPlayerCutoutPresenter>();
                playerCutout.Bind(
                    gameplayCamera,
                    target,
                    stancePresenter,
                    playerCutoutRenderers);
                movementInput.BindView(gameplayCamera.transform);
                return new GameplayCameraRig(
                    sceneCamera,
                    gameplayCamera,
                    cameraController,
                    freeCamera,
                    playerCutout,
                    movementInput,
                    sceneCameraWasActive);
            }
            catch
            {
                movementInput.BindView(null);
                freeCamera?.EndPresentation();
                cameraController?.Unbind();
                playerCutout?.Unbind();
                GameplayObjectLifecycle.Destroy(
                    gameplayCamera != null
                        ? gameplayCamera.gameObject
                        : null);
                sceneCamera.gameObject.SetActive(sceneCameraWasActive);
                throw;
            }
        }

        public GameplayCameraView ToggleView()
        {
            if (IsReplayFreeCameraActive)
                return cameraController?.View
                    ?? GameplayCameraView.ThirdPerson;
            return cameraController != null
                ? cameraController.ToggleView()
                : GameplayCameraView.ThirdPerson;
        }

        internal Transform Target => cameraController?.Target;

        internal bool IsReplayFreeCameraActive =>
            freeCamera?.IsPresenting == true;

        internal ReplayFreeCameraController FreeCamera => freeCamera;

        internal GameplayReplayCameraSnapshot CaptureReplaySnapshot()
        {
            RequireReplayCamera();
            if (playerCutout.Target != cameraController.Target)
                throw new InvalidOperationException(
                    "Gameplay camera and cutout targets diverged before replay.");
            return cameraController.CaptureReplaySnapshot();
        }

        internal void SetReplayTarget(Transform target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            RequireReplayCamera();
            EndReplayFreeCamera();
            if (cameraController.Target == target
                && playerCutout.Target == target)
            {
                cameraController.RefreshNow();
                playerCutout.RefreshNow();
                return;
            }
            ActorStancePresenter stance = target.GetComponent<
                ActorStancePresenter>();
            cameraController.SetTarget(target, stance);
            playerCutout.SetTarget(target, stance);
            OnViewChanged(cameraController.View);
        }

        internal void BeginReplayFreeCamera()
        {
            RequireReplayCamera();
            if (freeCamera.IsPresenting)
                return;
            playerCutout.SetPresentationEnabled(false);
            cameraController.enabled = false;
            freeCamera.BeginPresentation();
        }

        internal void EndReplayFreeCamera()
        {
            if (freeCamera == null || !freeCamera.IsPresenting)
                return;
            freeCamera.EndPresentation();
            cameraController.enabled = true;
            OnViewChanged(cameraController.View);
            cameraController.RefreshNow();
            playerCutout.RefreshNow();
        }

        internal void RestoreReplaySnapshot(
            GameplayReplayCameraSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(
                nameof(snapshot));
            RequireReplayCamera();
            EndReplayFreeCamera();
            ActorStancePresenter stance = snapshot.Target.GetComponent<
                ActorStancePresenter>();
            playerCutout.SetTarget(snapshot.Target, stance);
            cameraController.RestoreReplaySnapshot(snapshot);
            OnViewChanged(cameraController.View);
        }

        public void SetTarget(
            Transform target,
            ExplorationMovementInput input)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (gameplayCamera == null
                || cameraController == null
                || playerCutout == null)
            {
                throw new ObjectDisposedException(nameof(GameplayCameraRig));
            }

            ActorStancePresenter stance =
                target.GetComponent<ActorStancePresenter>();
            cameraController.SetTarget(target, stance);
            playerCutout.SetTarget(target, stance);
            if (movementInput != input)
            {
                input.BindView(gameplayCamera.transform);
                movementInput?.BindView(null);
                movementInput = input;
            }
            else
            {
                movementInput.BindView(gameplayCamera.transform);
            }
            OnViewChanged(cameraController.View);
        }

        public void Dispose()
        {
            if (movementInput != null)
            {
                movementInput.BindView(null);
                movementInput = null;
            }

            if (cameraController != null)
            {
                cameraController.ViewChanged -= OnViewChanged;
                cameraController.Unbind();
                cameraController = null;
            }

            if (freeCamera != null)
            {
                freeCamera.EndPresentation();
                freeCamera = null;
            }

            if (playerCutout != null)
            {
                playerCutout.Unbind();
                playerCutout = null;
            }

            GameplayObjectLifecycle.Destroy(
                gameplayCamera != null ? gameplayCamera.gameObject : null);
            gameplayCamera = null;
            if (sceneCamera != null)
            {
                sceneCamera.gameObject.SetActive(sceneCameraWasActive);
                sceneCamera = null;
            }
        }

        private void RequireReplayCamera()
        {
            if (gameplayCamera == null
                || cameraController == null
                || freeCamera == null
                || playerCutout == null)
                throw new ObjectDisposedException(nameof(GameplayCameraRig));
        }

        private void OnViewChanged(GameplayCameraView view)
        {
            playerCutout?.SetPresentationEnabled(
                view == GameplayCameraView.ThirdPerson);
        }

        private static Camera CreateGameplayCamera(Camera source)
        {
            var cameraObject = new GameObject("Gameplay Camera");
            try
            {
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.AddComponent<Camera>();
                camera.CopyFrom(source);
                camera.orthographic = false;
                camera.fieldOfView = 60f;
                camera.nearClipPlane = 0.08f;
                camera.farClipPlane = 80f;
                camera.clearFlags = CameraClearFlags.Skybox;
                UniversalAdditionalCameraData cameraData =
                    camera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = true;
                cameraData.antialiasing =
                    AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cameraData.antialiasingQuality = AntialiasingQuality.Medium;
                cameraObject.AddComponent<AudioListener>();
                return camera;
            }
            catch
            {
                GameplayObjectLifecycle.Destroy(cameraObject);
                throw;
            }
        }
    }

    internal readonly struct ReplaySpectatorInputFrame
    {
        public ReplaySpectatorInputFrame(
            Vector3 movement,
            Vector2 lookDelta,
            bool boosted)
        {
            if (!IsFinite(movement.x)
                || !IsFinite(movement.y)
                || !IsFinite(movement.z))
                throw new ArgumentOutOfRangeException(nameof(movement));
            if (!IsFinite(lookDelta.x) || !IsFinite(lookDelta.y))
                throw new ArgumentOutOfRangeException(nameof(lookDelta));
            Movement = Vector3.ClampMagnitude(movement, 1f);
            LookDelta = lookDelta;
            Boosted = boosted;
        }

        public Vector3 Movement { get; }
        public Vector2 LookDelta { get; }
        public bool Boosted { get; }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    internal interface IReplaySpectatorInputSource
    {
        ReplaySpectatorInputFrame ReadFrame();
    }

    internal sealed class ReplaySpectatorInput :
        IReplaySpectatorInputSource
    {
        public ReplaySpectatorInputFrame ReadFrame()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null && mouse == null)
                return default;
            float horizontal = Read(keyboard?.dKey)
                - Read(keyboard?.aKey);
            float vertical = Read(keyboard?.eKey)
                - Read(keyboard?.qKey);
            float forward = Read(keyboard?.wKey)
                - Read(keyboard?.sKey);
            bool boosted = keyboard?.leftShiftKey.isPressed == true
                || keyboard?.rightShiftKey.isPressed == true;
            Vector2 look = mouse?.middleButton.isPressed == true
                ? mouse.delta.ReadValue()
                : Vector2.zero;
            return new ReplaySpectatorInputFrame(
                new Vector3(horizontal, vertical, forward),
                look,
                boosted);
        }

        private static float Read(UnityEngine.InputSystem.Controls.KeyControl key)
            => key?.isPressed == true ? 1f : 0f;
    }

    [DefaultExecutionOrder(1100)]
    internal sealed class ReplayFreeCameraController : MonoBehaviour
    {
        private const float MovementSpeed = 7f;
        private const float BoostMultiplier = 3f;
        private const float LookSensitivity = 0.12f;
        private const float MinimumPitch = -89f;
        private const float MaximumPitch = 89f;

        private IReplaySpectatorInputSource input;
        private float yaw;
        private float pitch;

        internal bool IsPresenting { get; private set; }

        internal void Configure(IReplaySpectatorInputSource inputSource)
        {
            input = inputSource ?? throw new ArgumentNullException(
                nameof(inputSource));
            IsPresenting = false;
            enabled = false;
        }

        internal void BeginPresentation()
        {
            if (input == null)
                throw new InvalidOperationException(
                    "Configure spectator input before free-camera presentation.");
            Vector3 euler = transform.rotation.eulerAngles;
            yaw = euler.y;
            pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            IsPresenting = true;
            enabled = true;
        }

        internal void EndPresentation()
        {
            IsPresenting = false;
            enabled = false;
        }

        internal void Advance(
            ReplaySpectatorInputFrame frame,
            float unscaledDeltaSeconds)
        {
            if (float.IsNaN(unscaledDeltaSeconds)
                || float.IsInfinity(unscaledDeltaSeconds)
                || unscaledDeltaSeconds < 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(unscaledDeltaSeconds));
            if (!IsPresenting)
                return;
            yaw += frame.LookDelta.x * LookSensitivity;
            pitch = Mathf.Clamp(
                pitch - frame.LookDelta.y * LookSensitivity,
                MinimumPitch,
                MaximumPitch);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 movement = (transform.right * frame.Movement.x)
                + (Vector3.up * frame.Movement.y)
                + (transform.forward * frame.Movement.z);
            movement = Vector3.ClampMagnitude(movement, 1f);
            float speed = MovementSpeed
                * (frame.Boosted ? BoostMultiplier : 1f);
            transform.position += movement * speed * unscaledDeltaSeconds;
        }

        private void Update()
        {
            if (input != null)
                Advance(input.ReadFrame(), Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            input = null;
            IsPresenting = false;
        }
    }
}
