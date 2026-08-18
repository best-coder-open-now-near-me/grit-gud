using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayCameraRig : IDisposable
    {
        private Camera sceneCamera;
        private Camera gameplayCamera;
        private GameplayCameraController cameraController;
        private GameplayPlayerCutoutPresenter playerCutout;
        private ExplorationMovementInput movementInput;

        private GameplayCameraRig(
            Camera sourceCamera,
            Camera sessionCamera,
            GameplayCameraController viewController,
            GameplayPlayerCutoutPresenter cutoutPresenter,
            ExplorationMovementInput input)
        {
            sceneCamera = sourceCamera;
            gameplayCamera = sessionCamera;
            cameraController = viewController;
            playerCutout = cutoutPresenter;
            movementInput = input;
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

            Camera gameplayCamera = CreateGameplayCamera(sceneCamera);
            sceneCamera.gameObject.SetActive(false);
            GameplayCameraController cameraController = gameplayCamera.gameObject
                .AddComponent<GameplayCameraController>();
            ActorStancePresenter stancePresenter =
                target.GetComponent<ActorStancePresenter>();
            cameraController.Bind(target, inputSource, stancePresenter);
            GameplayPlayerCutoutPresenter playerCutout = gameplayCamera.gameObject
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
                playerCutout,
                movementInput);
        }

        public GameplayCameraView ToggleView()
        {
            return cameraController != null
                ? cameraController.ToggleView()
                : GameplayCameraView.ThirdPerson;
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

            movementInput?.BindView(null);
            movementInput = input;
            movementInput.BindView(gameplayCamera.transform);
            ActorStancePresenter stance =
                target.GetComponent<ActorStancePresenter>();
            cameraController.SetTarget(target, stance);
            playerCutout.SetTarget(target, stance);
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
                sceneCamera.gameObject.SetActive(true);
                sceneCamera = null;
            }
        }

        private void OnViewChanged(GameplayCameraView view)
        {
            playerCutout?.SetPresentationEnabled(
                view == GameplayCameraView.ThirdPerson);
        }

        private static Camera CreateGameplayCamera(Camera source)
        {
            var cameraObject = new GameObject("Gameplay Camera");
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
    }
}
