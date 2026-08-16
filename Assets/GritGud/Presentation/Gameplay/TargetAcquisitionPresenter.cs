using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GritGud.Presentation.Gameplay
{
    internal readonly struct GameplayWeaponAim
    {
        public GameplayWeaponAim(Vector3 position, string targetId)
            : this(
                position,
                targetId,
                -Vector3.forward,
                SurfacePresentationCatalog.DefaultSurfaceId,
                worldStateRevision: 0L,
                preferredFractureChunkIndex: -1)
        {
        }

        public GameplayWeaponAim(
            Vector3 position,
            string targetId,
            Vector3 normal,
            string surfaceId,
            long worldStateRevision,
            int preferredFractureChunkIndex)
        {
            Position = position;
            TargetId = string.IsNullOrWhiteSpace(targetId)
                ? GameplayTargetIds.WorldAimPoint
                : targetId;
            Normal = normal.sqrMagnitude > 0.0001f
                ? normal.normalized
                : Vector3.up;
            SurfaceId = string.IsNullOrWhiteSpace(surfaceId)
                ? SurfacePresentationCatalog.DefaultSurfaceId
                : surfaceId;
            WorldStateRevision = Math.Max(0L, worldStateRevision);
            PreferredFractureChunkIndex = preferredFractureChunkIndex;
        }

        public Vector3 Position { get; }

        public string TargetId { get; }

        public Vector3 Normal { get; }

        public string SurfaceId { get; }

        public long WorldStateRevision { get; }

        public int PreferredFractureChunkIndex { get; }
    }

    [DisallowMultipleComponent]
    public sealed class TargetAcquisitionPresenter : MonoBehaviour
    {
        private const float AimPlaneTolerance = 0.01f;
        private const float WorldAimFallbackDistance = 250f;

        internal static readonly Color AcquisitionOutlineColor =
            TargetFeedbackPresenter.AcquisitionOutlineColor;

        private readonly Dictionary<string, UnityTargetExposureQuery> exposureQueries =
            new Dictionary<string, UnityTargetExposureQuery>(StringComparer.Ordinal);
        private readonly HashSet<object> feedbackSuppressors =
            new HashSet<object>();
        private readonly RaycastHit[] aimHitBuffer = new RaycastHit[32];
        private GameplaySession session;
        private GameplayWorldRegistry registry;
        private GameplayActorView observer;
        private GameplayActorView currentTarget;
        private string observerId;
        private UnityPointerTargetQuery pointerQuery;
        private TargetFeedbackPresenter feedback;
        private TargetChancePresenter chancePresenter;
        private Ray currentPointerRay;
        private bool hasPointerRay;
        private Func<Vector2, bool> isPointerBlocked;
        private Func<Vector3?> getWeaponAimOrigin;
        private ISightObscuranceQuery sightObscurance;

        public bool IsBound => pointerQuery != null;

        public bool HasPointerTarget => CurrentPreview != null;

        public bool WeaponTargetingActive { get; private set; }

        public bool GroundHaloVisible =>
            feedback != null && feedback.GroundHaloVisible;

        public bool TargetOutlineVisible =>
            feedback != null && feedback.TargetOutlineVisible;

        public string CurrentTargetActorId => CurrentPreview?.TargetId;

        public int CurrentHitChancePercent =>
            CurrentPreview?.HitChancePercent ?? 0;

        public TargetExposureSnapshot CurrentSnapshot =>
            CurrentPreview?.Exposure;

        public TargetAcquisitionPreview CurrentPreview { get; private set; }

        internal bool ShouldPresentFeedback =>
            CurrentPreview != null
            && feedbackSuppressors.Count == 0;

        internal bool ShouldPresentHitChance =>
            ShouldPresentFeedback
            && session?.GetEquippedAttack(observerId) != null;

        internal void Bind(
            GameplaySession gameplaySession,
            GameplayWorldRegistry worldRegistry,
            string observingActorId,
            ISightObscuranceQuery obscuranceQuery = null)
        {
            Unbind();
            session = gameplaySession ??
                throw new ArgumentNullException(nameof(gameplaySession));
            registry = worldRegistry ??
                throw new ArgumentNullException(nameof(worldRegistry));
            sightObscurance = obscuranceQuery;
            feedback = new TargetFeedbackPresenter();
            chancePresenter = GetComponent<TargetChancePresenter>()
                ?? gameObject.AddComponent<TargetChancePresenter>();
            chancePresenter.Bind(this);
            SetObserver(observingActorId);
            enabled = true;
            RefreshNow();
        }

        internal void SetObserver(string observingActorId)
        {
            if (session == null || registry == null)
            {
                throw new InvalidOperationException(
                    "Bind target acquisition before changing its observer.");
            }

            string nextObserverId = RequireId(
                observingActorId,
                nameof(observingActorId));
            if (!session.TryGetActor(nextObserverId, out _))
            {
                throw new ArgumentException(
                    $"Observer '{nextObserverId}' is not part of the gameplay session.",
                    nameof(observingActorId));
            }

            ClearAcquisition();
            exposureQueries.Clear();
            observerId = nextObserverId;
            observer = registry.GetActor(observerId);
            pointerQuery = new UnityPointerTargetQuery(
                observer.Transform,
                registry);
            InvalidateWorldEvidence();
            RefreshNow();
        }

        public void Unbind()
        {
            CurrentPreview = null;
            currentTarget = null;
            if (chancePresenter != null)
            {
                chancePresenter.Unbind();
            }
            chancePresenter = null;
            feedback?.Dispose();
            feedback = null;
            exposureQueries.Clear();
            feedbackSuppressors.Clear();
            pointerQuery = null;
            observer = null;
            observerId = null;
            session = null;
            registry = null;
            sightObscurance = null;
            hasPointerRay = false;
            WeaponTargetingActive = false;
            isPointerBlocked = null;
            getWeaponAimOrigin = null;
            enabled = false;
        }

        internal void SetFeedbackSuppressed(object owner, bool suppressed)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            if (suppressed)
            {
                feedbackSuppressors.Add(owner);
            }
            else
            {
                feedbackSuppressors.Remove(owner);
            }

            ApplyFeedbackVisibility();
        }

        internal void SetPointerBlocker(Func<Vector2, bool> pointerBlocker)
        {
            isPointerBlocked = pointerBlocker;
        }

        internal void SetWeaponTargetingActive(bool active)
        {
            WeaponTargetingActive = active;
        }

        internal void SetWeaponAimOriginProvider(Func<Vector3?> originProvider)
        {
            getWeaponAimOrigin = originProvider;
        }

        internal void InvalidateWorldEvidence()
        {
            foreach (UnityTargetExposureQuery query in exposureQueries.Values)
            {
                query.Invalidate();
            }
        }

        public void RefreshNow()
        {
            RefreshNow(Camera.main);
        }

        internal void RefreshNow(Camera gameplayCamera)
        {
            if (!IsBound || gameplayCamera == null)
            {
                ClearAcquisition();
                return;
            }

            Vector2 pointerPosition = Mouse.current == null
                ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
                : Mouse.current.position.ReadValue();
            RefreshAtScreenPoint(gameplayCamera, pointerPosition);
        }

        internal void RefreshAtScreenPoint(
            Camera gameplayCamera,
            Vector2 pointerPosition)
        {
            if (!IsBound
                || gameplayCamera == null
                || (isPointerBlocked?.Invoke(pointerPosition) ?? false))
            {
                hasPointerRay = false;
                ClearAcquisition();
                return;
            }

            RefreshNow(gameplayCamera.ScreenPointToRay(pointerPosition));
        }

        internal void RefreshNow(Ray pointerRay)
        {
            currentPointerRay = pointerRay;
            hasPointerRay = true;
            if (!IsBound
                || !pointerQuery.TryAcquire(pointerRay, out GameplayActorView target)
                || session.IsActorIncapacitated(target.ActorId))
            {
                ClearAcquisition();
                return;
            }

            IReadOnlyList<ActorTargetRegionSample> presentedRegions =
                target.Stance.GetTargetRegionSamples();
            var targetRegions = new List<TargetRegionSample>(presentedRegions.Count);
            foreach (ActorTargetRegionSample region in presentedRegions)
            {
                targetRegions.Add(new TargetRegionSample(
                    region.Id,
                    ToGameplayPosition(region.WorldCenter),
                    region.Radius));
            }

            if (!exposureQueries.TryGetValue(
                    target.ActorId,
                    out UnityTargetExposureQuery exposureQuery))
            {
                exposureQuery = new UnityTargetExposureQuery(
                    observer.Transform,
                    target.Transform,
                    Physics.DefaultRaycastLayers,
                    () => session?.Journal.LastEntry?.Sequence ?? 0L,
                    sightObscurance);
                exposureQueries.Add(target.ActorId, exposureQuery);
            }

            TargetExposureSnapshot exposure = exposureQuery.Capture(
                observerId,
                ToGameplayPosition(observer.Stance.FirstPersonEyePosition),
                target.ActorId,
                targetRegions);
            if (exposure.VisibleSampleCount == 0)
            {
                ClearAcquisition();
                return;
            }

            currentTarget = target;
            AttackDefinition attack = session.GetEquippedAttack(observerId);
            AccuracyDecayDefinition accuracyDecay =
                attack?.AccuracyDecay ?? AccuracyDecayDefinition.None;
            float distance = session.GetActor(observerId).Pose.Position.DistanceTo(
                session.GetActor(target.ActorId).Pose.Position);
            CurrentPreview = TargetPreviewCalculator.Calculate(
                exposure,
                accuracyDecay,
                distance,
                attack?.Contact);
            feedback.SetTarget(target);
            ApplyFeedbackVisibility();
        }

        internal bool TryGetPresentationAimPoint(
            float fallbackDistance,
            out Vector3 aimPoint)
        {
            if (!IsBound || observer == null)
            {
                aimPoint = default;
                return false;
            }

            return TryResolvePointerAimPoint(
                ResolveWeaponAimOrigin(),
                fallbackDistance,
                allowRangeFallback: true,
                out aimPoint,
                out _);
        }

        internal bool TryGetPresentationAimPoint(out Vector3 aimPoint) =>
            TryGetPresentationAimPoint(
                WorldAimFallbackDistance,
                out aimPoint);

        internal bool TryGetWeaponAim(out GameplayWeaponAim aim)
        {
            if (!IsBound || observer == null
                || !TryResolvePointerAimPoint(
                    ResolveWeaponAimOrigin(),
                    WorldAimFallbackDistance,
                    allowRangeFallback: true,
                    out Vector3 aimPoint,
                    out string targetId))
            {
                aim = default;
                return false;
            }

            Vector3 origin = ResolveWeaponAimOrigin();
            ResolveWeaponImpactEvidence(
                origin,
                ref aimPoint,
                ref targetId,
                out Vector3 normal,
                out string surfaceId,
                out int preferredFractureChunkIndex);
            aim = new GameplayWeaponAim(
                aimPoint,
                targetId,
                normal,
                surfaceId,
                session.Journal.LastEntry?.Sequence ?? 0L,
                preferredFractureChunkIndex);
            return true;
        }

        private void ResolveWeaponImpactEvidence(
            Vector3 origin,
            ref Vector3 aimPoint,
            ref string targetId,
            out Vector3 normal,
            out string surfaceId,
            out int preferredFractureChunkIndex)
        {
            Vector3 offset = aimPoint - origin;
            normal = offset.sqrMagnitude > 0.0001f
                ? -offset.normalized
                : Vector3.up;
            surfaceId = SurfacePresentationCatalog.DefaultSurfaceId;
            preferredFractureChunkIndex = -1;
            if (offset.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                offset.normalized,
                aimHitBuffer,
                offset.magnitude + 0.15f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            RaycastHit nearest = default;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit candidate = aimHitBuffer[index];
                if (candidate.collider == null
                    || candidate.distance >= nearestDistance
                    || BelongsToObserver(candidate.collider.transform))
                {
                    continue;
                }

                nearest = candidate;
                nearestDistance = candidate.distance;
            }

            if (float.IsPositiveInfinity(nearestDistance))
            {
                return;
            }

            aimPoint = nearest.point;
            normal = nearest.normal.sqrMagnitude > 0.0001f
                ? nearest.normal.normalized
                : normal;
            targetId = ResolveAimTargetId(nearest.collider.transform);
            if (registry.TryGetLevelEntityContaining(
                    nearest.collider.transform,
                    out LevelEntityView entity))
            {
                surfaceId = entity.Archetype.SurfacePresentationId;
                DestructibleFractureProfile fracture =
                    entity.Archetype.FractureProfile;
                if (fracture != null)
                {
                    preferredFractureChunkIndex = fracture.FindClosestChunkIndex(
                        entity.transform.InverseTransformPoint(nearest.point));
                }
            }
        }

        internal bool TryGetPointerSurfacePoint(
            Vector3 characterAimOrigin,
            float maximumRange,
            out Vector3 aimPoint) =>
            TryResolvePointerAimPoint(
                characterAimOrigin,
                maximumRange,
                allowRangeFallback: false,
                out aimPoint,
                out _);

        internal bool TryGetPointerRay(out Ray pointerRay)
        {
            pointerRay = currentPointerRay;
            return IsBound
                && hasPointerRay
                && currentPointerRay.direction.sqrMagnitude > 0.0001f;
        }

        private bool TryResolvePointerAimPoint(
            Vector3 characterAimOrigin,
            float maximumRange,
            bool allowRangeFallback,
            out Vector3 aimPoint,
            out string targetId)
        {
            if (!IsBound
                || !IsFinitePositive(maximumRange))
            {
                aimPoint = default;
                targetId = null;
                return false;
            }

            if (currentTarget != null)
            {
                IReadOnlyList<ActorTargetRegionSample> samples =
                    currentTarget.Stance.GetTargetRegionSamples();
                foreach (ActorTargetRegionSample sample in samples)
                {
                    if (sample.Id == TargetRegionId.Torso)
                    {
                        aimPoint = sample.WorldCenter;
                        targetId = currentTarget.ActorId;
                        return true;
                    }
                }

                if (samples.Count > 0)
                {
                    aimPoint = samples[0].WorldCenter;
                    targetId = currentTarget.ActorId;
                    return true;
                }
            }

            if (!hasPointerRay
                || currentPointerRay.direction.sqrMagnitude <= 0.0001f)
            {
                aimPoint = default;
                targetId = null;
                return false;
            }

            var aimRay = new Ray(
                currentPointerRay.origin,
                currentPointerRay.direction.normalized);
            if (!TryGetRangeEndDistance(
                    aimRay,
                    characterAimOrigin,
                    maximumRange,
                    out float rangeEndDistance))
            {
                aimPoint = default;
                targetId = null;
                return false;
            }

            float characterPlaneDistance = Mathf.Max(
                0f,
                Vector3.Dot(
                    characterAimOrigin - aimRay.origin,
                    aimRay.direction));
            int hitCount = Physics.RaycastNonAlloc(
                aimRay,
                aimHitBuffer,
                rangeEndDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            Vector3 intendedAimPoint;
            Transform intendedTransform;
            bool foundPointerSurface;
            if (hitCount == aimHitBuffer.Length)
            {
                RaycastHit[] allHits = Physics.RaycastAll(
                    aimRay,
                    rangeEndDistance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                foundPointerSurface = TryFindNearestCharacterSideHit(
                    allHits,
                    allHits.Length,
                    characterPlaneDistance,
                    out intendedAimPoint,
                    out intendedTransform);
            }
            else
            {
                foundPointerSurface = TryFindNearestCharacterSideHit(
                    aimHitBuffer,
                    hitCount,
                    characterPlaneDistance,
                    out intendedAimPoint,
                    out intendedTransform);
            }

            if (!foundPointerSurface)
            {
                if (!allowRangeFallback)
                {
                    aimPoint = default;
                    targetId = null;
                    return false;
                }

                intendedAimPoint = aimRay.GetPoint(rangeEndDistance);
                intendedTransform = null;
            }

            return TryResolveCharacterAimPath(
                characterAimOrigin,
                intendedAimPoint,
                intendedTransform,
                maximumRange,
                out aimPoint,
                out targetId);
        }

        private bool TryResolveCharacterAimPath(
            Vector3 characterAimOrigin,
            Vector3 intendedAimPoint,
            Transform intendedTransform,
            float maximumRange,
            out Vector3 aimPoint,
            out string targetId)
        {
            Vector3 offset = intendedAimPoint - characterAimOrigin;
            float intendedDistance = offset.magnitude;
            if (intendedDistance <= 0.0001f)
            {
                aimPoint = default;
                targetId = null;
                return false;
            }

            float pathDistance = Mathf.Min(maximumRange, intendedDistance);
            var characterRay = new Ray(
                characterAimOrigin,
                offset / intendedDistance);
            int hitCount = Physics.RaycastNonAlloc(
                characterRay,
                aimHitBuffer,
                pathDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            if (hitCount == aimHitBuffer.Length)
            {
                RaycastHit[] allHits = Physics.RaycastAll(
                    characterRay,
                    pathDistance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                if (TryFindNearestCharacterSideHit(
                        allHits,
                        allHits.Length,
                        0f,
                        out aimPoint,
                        out Transform hitTransform))
                {
                    targetId = ResolveAimTargetId(hitTransform);
                    return true;
                }
            }
            else if (TryFindNearestCharacterSideHit(
                         aimHitBuffer,
                         hitCount,
                         0f,
                         out aimPoint,
                         out Transform hitTransform))
            {
                targetId = ResolveAimTargetId(hitTransform);
                return true;
            }

            aimPoint = characterRay.GetPoint(pathDistance);
            targetId = pathDistance + 0.0001f < intendedDistance
                ? GameplayTargetIds.WorldAimPoint
                : ResolveAimTargetId(intendedTransform);
            return true;
        }

        private Vector3 ResolveWeaponAimOrigin()
        {
            Vector3? presentedOrigin = getWeaponAimOrigin?.Invoke();
            return presentedOrigin.HasValue
                ? presentedOrigin.Value
                : observer.Stance.FirstPersonEyePosition;
        }

        private bool TryFindNearestCharacterSideHit(
            RaycastHit[] hits,
            int hitCount,
            float characterPlaneDistance,
            out Vector3 aimPoint,
            out Transform aimTransform)
        {
            float nearestDistance = float.PositiveInfinity;
            Vector3 nearestPoint = default;
            Transform nearestTransform = null;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = hits[index];
                if (hit.collider == null
                    || hit.distance + AimPlaneTolerance
                        < characterPlaneDistance
                    || hit.distance >= nearestDistance
                    || BelongsToObserver(hit.collider.transform))
                {
                    continue;
                }

                nearestDistance = hit.distance;
                nearestPoint = hit.point;
                nearestTransform = hit.collider.transform;
            }

            aimPoint = nearestPoint;
            aimTransform = nearestTransform;
            return !float.IsPositiveInfinity(nearestDistance);
        }

        private string ResolveAimTargetId(Transform aimTransform) =>
            registry != null
            && registry.TryGetLevelEntityContaining(
                aimTransform,
                out LevelEntityView entity)
                ? entity.EntityId
                : GameplayTargetIds.WorldAimPoint;

        private bool BelongsToObserver(Transform candidate) =>
            candidate != null
            && observer != null
            && (candidate == observer.Transform
                || candidate.IsChildOf(observer.Transform));

        private static bool TryGetRangeEndDistance(
            Ray ray,
            Vector3 sphereCenter,
            float sphereRadius,
            out float distance)
        {
            Vector3 offset = ray.origin - sphereCenter;
            float projection = Vector3.Dot(offset, ray.direction);
            float discriminant = projection * projection
                - (offset.sqrMagnitude - sphereRadius * sphereRadius);
            if (discriminant < 0f)
            {
                distance = 0f;
                return false;
            }

            distance = -projection + Mathf.Sqrt(discriminant);
            return distance > 0f;
        }

        private void LateUpdate()
        {
            RefreshNow();
        }

        private void ClearAcquisition()
        {
            currentTarget = null;
            CurrentPreview = null;
            ApplyFeedbackVisibility();
        }

        private void ApplyFeedbackVisibility()
        {
            bool acquired = currentTarget != null
                && CurrentPreview != null
                && feedbackSuppressors.Count == 0;
            bool showTurnHalo = acquired
                && session != null
                && session.Mode == GameplaySessionMode.TurnBased
                && session.Operation == GameplaySessionOperation.None
                && string.Equals(
                    session.ActiveActorId,
                    observerId,
                    StringComparison.Ordinal);
            feedback?.SetVisible(acquired, showTurnHalo);
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private static GameplayPosition ToGameplayPosition(Vector3 position) =>
            new GameplayPosition(position.x, position.y, position.z);

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value)
            && !float.IsInfinity(value)
            && value > 0f;

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Target acquisition requires an actor identifier.",
                    parameterName);
            }

            return value;
        }
    }
}
