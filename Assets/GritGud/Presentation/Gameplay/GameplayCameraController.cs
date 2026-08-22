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

    internal sealed class GameplayReplayCameraSnapshot
    {
        public GameplayReplayCameraSnapshot(
            Transform target,
            GameplayCameraView view,
            Vector3 position,
            Quaternion rotation,
            float yaw,
            float pitch,
            float thirdPersonZoom,
            bool targetRenderersHidden)
        {
            Target = target != null
                ? target
                : throw new ArgumentNullException(nameof(target));
            if (!Enum.IsDefined(typeof(GameplayCameraView), view))
                throw new ArgumentOutOfRangeException(nameof(view));
            if (float.IsNaN(yaw) || float.IsInfinity(yaw))
                throw new ArgumentOutOfRangeException(nameof(yaw));
            if (float.IsNaN(pitch) || float.IsInfinity(pitch))
                throw new ArgumentOutOfRangeException(nameof(pitch));
            if (float.IsNaN(thirdPersonZoom)
                || float.IsInfinity(thirdPersonZoom)
                || thirdPersonZoom < 0f
                || thirdPersonZoom > 1f)
                throw new ArgumentOutOfRangeException(
                    nameof(thirdPersonZoom));
            View = view;
            Position = position;
            Rotation = rotation;
            Yaw = yaw;
            Pitch = pitch;
            ThirdPersonZoom = thirdPersonZoom;
            TargetRenderersHidden = targetRenderersHidden;
        }

        public Transform Target { get; }
        public GameplayCameraView View { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public float Yaw { get; }
        public float Pitch { get; }
        public float ThirdPersonZoom { get; }
        public bool TargetRenderersHidden { get; }
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

        internal float Yaw => yaw;

        internal float Pitch => pitch;

        public void Bind(
            Transform followTarget,
            IGameplayInputSource gameplayInput,
            ActorStancePresenter actorStancePresenter = null)
        {
            if (followTarget == null)
                throw new ArgumentNullException(nameof(followTarget));
            if (gameplayInput == null)
                throw new ArgumentNullException(nameof(gameplayInput));

            Unbind();
            inputSource = gameplayInput;
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
            if (!targetChanged)
            {
                stancePresenter = actorStancePresenter;
                playerVisibility?.SetVisible(
                    View == GameplayCameraView.ThirdPerson);
                RefreshNow();
                return;
            }

            var nextVisibility = new LocalPlayerCameraVisibility(nextTarget);
            LocalPlayerCameraVisibility previousVisibility = playerVisibility;
            target = nextTarget;
            stancePresenter = actorStancePresenter;
            yaw = followTarget.eulerAngles.y;
            playerVisibility = nextVisibility;
            playerVisibility.SetVisible(
                View == GameplayCameraView.ThirdPerson);
            previousVisibility?.Dispose();
            RefreshNow();
        }

        internal GameplayReplayCameraSnapshot CaptureReplaySnapshot()
        {
            if (target == null || inputSource == null)
                throw new InvalidOperationException(
                    "Bind the gameplay camera before replay capture.");
            return new GameplayReplayCameraSnapshot(
                target,
                View,
                transform.position,
                transform.rotation,
                yaw,
                pitch,
                thirdPersonZoom,
                playerVisibility?.Visible == false);
        }

        internal void RestoreReplaySnapshot(
            GameplayReplayCameraSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(
                nameof(snapshot));
            if (inputSource == null)
                throw new InvalidOperationException(
                    "Bind the gameplay camera before replay restoration.");
            ActorStancePresenter restoredStance = snapshot.Target
                .GetComponent<ActorStancePresenter>();
            SetTarget(snapshot.Target, restoredStance);
            bool viewChanged = View != snapshot.View;
            View = snapshot.View;
            yaw = snapshot.Yaw;
            pitch = snapshot.Pitch;
            thirdPersonZoom = snapshot.ThirdPersonZoom;
            playerVisibility?.SetVisible(
                !snapshot.TargetRenderersHidden);
            transform.SetPositionAndRotation(
                snapshot.Position,
                snapshot.Rotation);
            if (viewChanged)
                ViewChanged?.Invoke(View);
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
            playerVisibility?.Refresh();
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
            playerVisibility = null;
        }

        private void OnDisable()
        {
            playerVisibility?.SetVisible(true);
        }

        private void OnEnable()
        {
            playerVisibility?.SetVisible(
                View == GameplayCameraView.ThirdPerson);
        }
    }

    internal sealed class LocalPlayerCameraVisibility : IDisposable
    {
        private readonly object owner = new object();
        private readonly Transform player;
        private readonly HashSet<Renderer> renderers =
            new HashSet<Renderer>();
        private bool visible = true;
        private bool disposed;

        public LocalPlayerCameraVisibility(Transform playerTransform)
        {
            player = playerTransform != null
                ? playerTransform
                : throw new ArgumentNullException(nameof(playerTransform));
            Refresh();
        }

        public bool Visible => visible;

        public void SetVisible(bool visible)
        {
            if (disposed)
            {
                return;
            }

            Refresh();
            this.visible = visible;
            foreach (Renderer renderer in renderers)
            {
                LocalPlayerRendererVisibilityOverrides.SetHidden(
                    renderer,
                    owner,
                    !visible);
            }
        }

        public void Refresh()
        {
            if (disposed)
                return;

            var current = new HashSet<Renderer>(
                player.GetComponentsInChildren<Renderer>(true));
            foreach (Renderer renderer in current)
            {
                if (renderer != null && renderers.Add(renderer) && !visible)
                {
                    LocalPlayerRendererVisibilityOverrides.SetHidden(
                        renderer,
                        owner,
                        hidden: true);
                }
            }

            renderers.RemoveWhere(renderer =>
            {
                if (renderer != null && current.Contains(renderer))
                    return false;

                LocalPlayerRendererVisibilityOverrides.SetHidden(
                    renderer,
                    owner,
                    hidden: false);
                return true;
            });
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            LocalPlayerRendererVisibilityOverrides.Release(owner);
            renderers.Clear();
            disposed = true;
        }
    }

    internal static class LocalPlayerRendererVisibilityOverrides
    {
        private sealed class RendererOverride
        {
            public RendererOverride(Renderer renderer)
            {
                Renderer = renderer;
                OriginalForceRenderingOff = renderer.forceRenderingOff;
            }

            public Renderer Renderer { get; }

            public bool OriginalForceRenderingOff { get; }

            public HashSet<object> HiddenOwners { get; } =
                new HashSet<object>();
        }

        private static readonly Dictionary<Renderer, RendererOverride>
            Overrides = new Dictionary<Renderer, RendererOverride>();

        public static void SetHidden(
            Renderer renderer,
            object owner,
            bool hidden)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (renderer == null)
                return;

            if (!Overrides.TryGetValue(
                    renderer,
                    out RendererOverride entry))
            {
                if (!hidden)
                    return;
                entry = new RendererOverride(renderer);
                Overrides.Add(renderer, entry);
            }

            if (hidden)
                entry.HiddenOwners.Add(owner);
            else
                entry.HiddenOwners.Remove(owner);
            ApplyOrRelease(renderer, entry);
        }

        public static void Release(object owner)
        {
            if (owner == null)
                return;

            var renderers = new List<Renderer>(Overrides.Keys);
            foreach (Renderer renderer in renderers)
            {
                RendererOverride entry = Overrides[renderer];
                entry.HiddenOwners.Remove(owner);
                ApplyOrRelease(renderer, entry);
            }
        }

        private static void ApplyOrRelease(
            Renderer renderer,
            RendererOverride entry)
        {
            if (entry.HiddenOwners.Count > 0)
            {
                if (entry.Renderer != null)
                    entry.Renderer.forceRenderingOff = true;
                return;
            }

            if (entry.Renderer != null)
            {
                entry.Renderer.forceRenderingOff =
                    entry.OriginalForceRenderingOff;
            }
            Overrides.Remove(renderer);
        }
    }
}
