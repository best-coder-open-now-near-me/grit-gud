using System;
using System.Collections.Generic;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    public enum GameplayCameraView
    {
        ThirdPerson = 0,
        FirstPerson = 1,
    }

    [RequireComponent(typeof(Camera))]
    public sealed class GameplayCameraController : MonoBehaviour
    {
        public const string LocalPlayerLayerName = "LocalPlayer";

        private static readonly Vector3 ShoulderOffset =
            new Vector3(-0.82f, 0.45f, -2.35f);
        private const float DefaultPivotHeight = 1.3f;
        private const float DefaultEyeHeight = 1.62f;
        private const float Sensitivity = 0.12f;
        private const float ZoomStepPerWheelTick = 0.25f;
        private const float ZoomInputThreshold = 0.01f;
        private const float CollisionRadius = 0.2f;
        private const float ThirdPersonMinimumPitch = -35f;
        private const float ThirdPersonMaximumPitch = 70f;
        private const float FirstPersonMinimumPitch = -80f;
        private const float FirstPersonMaximumPitch = 80f;

        private Transform target;
        private IGameplayInputSource inputSource;
        private ActorStancePresenter stancePresenter;
        private LocalPlayerCameraVisibility playerVisibility;
        private float yaw;
        private float pitch = 12f;
        private float thirdPersonZoom = 1f;

        public event Action<GameplayCameraView> ViewChanged;

        public GameplayCameraView View { get; private set; } =
            GameplayCameraView.ThirdPerson;

        public Transform Target => target;

        internal float ThirdPersonZoom => thirdPersonZoom;

        public void Bind(
            Transform followTarget,
            IGameplayInputSource gameplayInput,
            ActorStancePresenter actorStancePresenter = null)
        {
            Unbind();
            if (followTarget == null)
                throw new ArgumentNullException(nameof(followTarget));
            inputSource = gameplayInput ??
                throw new ArgumentNullException(nameof(gameplayInput));
            View = GameplayCameraView.ThirdPerson;
            thirdPersonZoom = 1f;
            SetTarget(followTarget, actorStancePresenter);
            enabled = true;
            RefreshNow();
        }

        public void SetTarget(
            Transform followTarget,
            ActorStancePresenter actorStancePresenter = null)
        {
            if (inputSource == null)
            {
                throw new InvalidOperationException(
                    "Bind the gameplay camera before changing its target.");
            }

            Transform nextTarget = followTarget != null
                ? followTarget
                : throw new ArgumentNullException(nameof(followTarget));
            bool targetChanged = target != nextTarget;
            target = nextTarget;
            stancePresenter = actorStancePresenter;
            if (targetChanged)
                yaw = followTarget.eulerAngles.y;
            playerVisibility?.Dispose();
            playerVisibility = new LocalPlayerCameraVisibility(
                GetComponent<Camera>(),
                followTarget,
                LocalPlayerLayerName);
            playerVisibility.SetVisible(
                View == GameplayCameraView.ThirdPerson);
            RefreshNow();
        }

        public void Unbind()
        {
            playerVisibility?.Dispose();
            playerVisibility = null;
            target = null;
            inputSource = null;
            stancePresenter = null;
            thirdPersonZoom = 1f;
            enabled = false;
        }

        public GameplayCameraView ToggleView()
        {
            GameplayCameraView next = View == GameplayCameraView.ThirdPerson
                ? GameplayCameraView.FirstPerson
                : GameplayCameraView.ThirdPerson;
            SetView(next);
            return View;
        }

        public void SetView(GameplayCameraView view)
        {
            if (!Enum.IsDefined(typeof(GameplayCameraView), view))
            {
                throw new ArgumentOutOfRangeException(nameof(view));
            }

            thirdPersonZoom = view == GameplayCameraView.ThirdPerson ? 1f : 0f;
            ApplyView(view);
        }

        internal void ApplyZoomInput(float scrollDelta)
        {
            if (float.IsNaN(scrollDelta)
                || float.IsInfinity(scrollDelta)
                || Mathf.Abs(scrollDelta) < ZoomInputThreshold)
            {
                return;
            }

            if (scrollDelta > 0f)
            {
                if (View == GameplayCameraView.FirstPerson)
                {
                    return;
                }

                thirdPersonZoom = Mathf.Max(
                    0f,
                    thirdPersonZoom - ZoomStepPerWheelTick);
                if (thirdPersonZoom <= 0f)
                {
                    ApplyView(GameplayCameraView.FirstPerson);
                }
                else
                {
                    RefreshNow();
                }

                return;
            }

            if (View == GameplayCameraView.FirstPerson)
            {
                thirdPersonZoom = ZoomStepPerWheelTick;
                ApplyView(GameplayCameraView.ThirdPerson);
                return;
            }

            thirdPersonZoom = Mathf.Min(
                1f,
                thirdPersonZoom + ZoomStepPerWheelTick);
            RefreshNow();
        }

        private void ApplyView(GameplayCameraView view)
        {
            bool changed = View != view;
            View = view;
            pitch = Mathf.Clamp(
                pitch,
                GetMinimumPitch(view),
                GetMaximumPitch(view));
            playerVisibility?.SetVisible(view == GameplayCameraView.ThirdPerson);
            RefreshNow();
            if (changed)
            {
                ViewChanged?.Invoke(view);
            }
        }

        public void RefreshNow()
        {
            if (target == null)
            {
                return;
            }

            if (View == GameplayCameraView.FirstPerson)
            {
                SnapToFirstPerson();
                return;
            }

            SnapToThirdPerson();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            GameplayInputFrame input = inputSource?.CurrentFrame ?? default;
            if (input.AimHeld)
            {
                Vector2 delta = input.LookDelta;
                yaw += delta.x * Sensitivity;
                pitch = Mathf.Clamp(
                    pitch - (delta.y * Sensitivity),
                    GetMinimumPitch(View),
                    GetMaximumPitch(View));
            }

            ApplyZoomInput(input.CameraZoomDelta);

            RefreshNow();
        }

        private void SnapToFirstPerson()
        {
            Vector3 eyePosition = stancePresenter != null
                ? stancePresenter.FirstPersonEyePosition
                : target.position + (Vector3.up * DefaultEyeHeight);
            Quaternion lookRotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(eyePosition, lookRotation);
        }

        private void SnapToThirdPerson()
        {
            Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
            float pivotHeight = stancePresenter != null
                ? stancePresenter.ThirdPersonCameraPivotHeight
                : DefaultPivotHeight;
            Vector3 pivot = target.position + (Vector3.up * pivotHeight);
            Vector3 eyePosition = stancePresenter != null
                ? stancePresenter.FirstPersonEyePosition
                : target.position + (Vector3.up * DefaultEyeHeight);
            Vector3 fullThirdPerson = pivot + (orbit * ShoulderOffset);
            Vector3 desired = Vector3.Lerp(
                eyePosition,
                fullThirdPerson,
                thirdPersonZoom);
            Vector3 ray = desired - pivot;
            float distance = ray.magnitude;
            RaycastHit[] hits = Physics.SphereCastAll(
                pivot,
                CollisionRadius,
                ray.normalized,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            float obstructionDistance = distance;
            bool obstructed = false;
            foreach (RaycastHit hit in hits)
            {
                if (!GameplayCameraOcclusionRules.ShouldMoveCamera(
                    hit.collider,
                    target))
                {
                    continue;
                }

                obstructed = true;
                obstructionDistance = Mathf.Min(obstructionDistance, hit.distance);
            }

            if (obstructed)
            {
                desired = pivot + (ray.normalized
                    * Mathf.Max(0.15f, obstructionDistance - CollisionRadius));
            }

            transform.SetPositionAndRotation(desired, orbit);
        }

        private static float GetMinimumPitch(GameplayCameraView view) =>
            view == GameplayCameraView.FirstPerson
                ? FirstPersonMinimumPitch
                : ThirdPersonMinimumPitch;

        private static float GetMaximumPitch(GameplayCameraView view) =>
            view == GameplayCameraView.FirstPerson
                ? FirstPersonMaximumPitch
                : ThirdPersonMaximumPitch;

        private void OnDestroy()
        {
            playerVisibility?.Dispose();
        }
    }

    internal sealed class LocalPlayerCameraVisibility : IDisposable
    {
        private readonly Camera gameplayCamera;
        private readonly Dictionary<GameObject, int> originalLayers =
            new Dictionary<GameObject, int>();
        private readonly int originalCullingMask;
        private readonly int localPlayerLayer;
        private bool disposed;

        public LocalPlayerCameraVisibility(
            Camera camera,
            Transform player,
            string layerName)
        {
            gameplayCamera = camera != null
                ? camera
                : throw new ArgumentNullException(nameof(camera));
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            localPlayerLayer = LayerMask.NameToLayer(layerName);
            if (localPlayerLayer < 0)
            {
                throw new InvalidOperationException(
                    $"Gameplay camera requires the '{layerName}' project layer.");
            }

            originalCullingMask = gameplayCamera.cullingMask;
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true))
            {
                GameObject rendererObject = renderer.gameObject;
                if (originalLayers.ContainsKey(rendererObject))
                {
                    continue;
                }

                originalLayers.Add(rendererObject, rendererObject.layer);
                rendererObject.layer = localPlayerLayer;
            }
        }

        public void SetVisible(bool visible)
        {
            if (disposed)
            {
                return;
            }

            int localPlayerMask = 1 << localPlayerLayer;
            gameplayCamera.cullingMask = visible
                ? gameplayCamera.cullingMask | localPlayerMask
                : gameplayCamera.cullingMask & ~localPlayerMask;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            foreach (KeyValuePair<GameObject, int> entry in originalLayers)
            {
                if (entry.Key != null)
                {
                    entry.Key.layer = entry.Value;
                }
            }

            gameplayCamera.cullingMask = originalCullingMask;
            originalLayers.Clear();
            disposed = true;
        }
    }
}
