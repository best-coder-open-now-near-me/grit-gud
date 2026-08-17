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

    internal readonly struct TargetingPointerFeedback
    {
        public TargetingPointerFeedback(string text, bool isValid)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException(
                    "Targeting feedback requires visible text.",
                    nameof(text));
            }

            Text = text;
            IsValid = isValid;
        }

        public string Text { get; }

        public bool IsValid { get; }
    }

    [DisallowMultipleComponent]
    public sealed class TargetAcquisitionPresenter : MonoBehaviour
    {
        private const float AimPlaneTolerance = 0.01f;
        private const float WorldAimFallbackDistance = 250f;

        internal static readonly Color AcquisitionOutlineColor =
            TargetFeedbackPresenter.AcquisitionOutlineColor;

        internal static readonly Color InvalidOutlineColor =
            TargetFeedbackPresenter.InvalidColor;

        internal static readonly Color FriendlyOutlineColor =
            TargetFeedbackPresenter.FriendlyColor;

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
        private bool hasLockedWeaponAimOrigin;
        private Vector3 lockedWeaponAimOriginLocal;
        private bool hasResolvedWeaponAim;
        private GameplayWeaponAim resolvedWeaponAim;
        private ISightObscuranceQuery sightObscurance;
        private object validationFeedbackOwner;
        private string validationTargetId;
        private Transform validationTargetRoot;
        private string validationText;
        private bool validationIsValid;

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
            validationFeedbackOwner != null
                ? validationTargetRoot != null
                : CurrentPreview != null
                    && feedbackSuppressors.Count == 0;

        internal bool ShouldPresentHitChance =>
            validationFeedbackOwner == null
            && ShouldPresentFeedback
            && session?.GetEquippedAttack(observerId) != null;

        internal bool HasValidationFeedback =>
            validationFeedbackOwner != null;

        internal string CurrentValidationText => validationText;

        internal bool CurrentValidationIsValid => validationIsValid;

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
            hasLockedWeaponAimOrigin = false;
            hasResolvedWeaponAim = false;
            pointerQuery = new UnityPointerTargetQuery(
                observer.Transform,
                registry,
                actorEligibility: CanAcquireActorTarget);
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
            validationFeedbackOwner = null;
            validationTargetId = null;
            validationTargetRoot = null;
            validationText = null;
            validationIsValid = false;
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
            hasLockedWeaponAimOrigin = false;
            lockedWeaponAimOriginLocal = default;
            hasResolvedWeaponAim = false;
            resolvedWeaponAim = default;
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

        internal void PresentValidationFeedback(
            object owner,
            string targetId,
            Transform targetRoot,
            bool isValid,
            string text)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException(
                    "Target validation feedback requires visible text.",
                    nameof(text));
            }
            if (targetRoot != null && string.IsNullOrWhiteSpace(targetId))
            {
                throw new ArgumentException(
                    "Highlighted validation targets require an identifier.",
                    nameof(targetId));
            }

            validationFeedbackOwner = owner;
            validationTargetId = string.IsNullOrWhiteSpace(targetId)
                ? null
                : targetId;
            validationTargetRoot = targetRoot;
            validationText = text;
            validationIsValid = isValid;
            ApplyFeedbackVisibility();
        }

        internal void ClearValidationFeedback(object owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }
            if (!ReferenceEquals(validationFeedbackOwner, owner))
            {
                return;
            }

            validationFeedbackOwner = null;
            validationTargetId = null;
            validationTargetRoot = null;
            validationText = null;
            validationIsValid = false;
            feedback?.ClearTarget();
            ApplyFeedbackVisibility();
        }

        internal bool TryGetPointerFeedback(
            out TargetingPointerFeedback pointerFeedback)
        {
            if (validationFeedbackOwner != null
                && !string.IsNullOrWhiteSpace(validationText))
            {
                pointerFeedback = new TargetingPointerFeedback(
                    validationText,
                    validationIsValid);
                return true;
            }

            AttackDefinition attack = session?.GetEquippedAttack(observerId);
            if (feedbackSuppressors.Count > 0 || attack == null)
            {
                pointerFeedback = default;
                return false;
            }

            if (CurrentPreview != null && ShouldPresentFeedback)
            {
                pointerFeedback = new TargetingPointerFeedback(
                    CurrentPreview.IsWithinReach
                        ? $"CHANCE TO HIT  {CurrentPreview.HitChancePercent}%"
                        : $"OUT OF REACH  {CurrentPreview.Distance:0.#} / "
                            + $"{CurrentPreview.MaximumReach:0.#} M",
                    CurrentPreview.IsWithinReach);
                return true;
            }

            if (!WeaponTargetingActive)
            {
                pointerFeedback = default;
                return false;
            }

            if (attack.Contact != null)
            {
                pointerFeedback = new TargetingPointerFeedback(
                    $"ACTOR TARGET REQUIRED  {attack.Contact.MaximumReach:0.#} M MAX",
                    isValid: false);
                return true;
            }

            bool validWorldAim = TryGetWeaponAim(out _);
            pointerFeedback = new TargetingPointerFeedback(
                validWorldAim
                    ? "VALID AIM POINT"
                    : "NO VALID AIM POINT",
                validWorldAim);
            return true;
        }

        internal void SetPointerBlocker(Func<Vector2, bool> pointerBlocker)
        {
            isPointerBlocked = pointerBlocker;
        }

        internal void SetWeaponTargetingActive(bool active)
        {
            bool wasActive = WeaponTargetingActive;
            WeaponTargetingActive = active;
            if (active && !wasActive)
            {
                LockWeaponAimOrigin();
            }
            else if (!active)
            {
                hasLockedWeaponAimOrigin = false;
                hasResolvedWeaponAim = false;
                resolvedWeaponAim = default;
            }

            if (!active)
            {
                feedback?.ClearTarget();
            }

            if (hasPointerRay)
            {
                RefreshNow(currentPointerRay);
            }
            else
            {
                ApplyFeedbackVisibility();
            }
        }

        internal void SetWeaponAimOriginProvider(Func<Vector3?> originProvider)
        {
            getWeaponAimOrigin = originProvider;
            if (WeaponTargetingActive)
            {
                LockWeaponAimOrigin();
                if (hasPointerRay)
                {
                    RefreshNow(currentPointerRay);
                }
            }
        }

        internal void InvalidateWorldEvidence()
        {
            hasResolvedWeaponAim = false;
            resolvedWeaponAim = default;
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
                hasPointerRay = false;
                hasResolvedWeaponAim = false;
                resolvedWeaponAim = default;
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
                hasResolvedWeaponAim = false;
                resolvedWeaponAim = default;
                ClearAcquisition();
                return;
            }

            RefreshNow(gameplayCamera.ScreenPointToRay(pointerPosition));
        }

        internal void RefreshNow(Ray pointerRay)
        {
            currentPointerRay = pointerRay;
            hasPointerRay = true;
            hasResolvedWeaponAim = false;
            resolvedWeaponAim = default;
            if (!IsBound)
            {
                ClearAcquisition();
                return;
            }

            GameplayActorView target;
            if (UsesAuthoritativeWeaponAim())
            {
                if (!TryGetWeaponAim(out GameplayWeaponAim resolvedAim)
                    || !registry.TryGetActor(resolvedAim.TargetId, out target)
                    || !CanAcquireActorTarget(target)
                    || ReferenceEquals(target.Transform, observer.Transform))
                {
                    ClearAcquisition();
                    return;
                }
            }
            else if (!pointerQuery.TryAcquire(pointerRay, out target))
            {
                ClearAcquisition();
                return;
            }

            if (session.IsActorIncapacitated(target.ActorId))
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
            TryGetResolvedPresentationAimPoint(out aimPoint);

        internal bool TryGetWeaponAim(out GameplayWeaponAim aim)
        {
            if (WeaponTargetingActive && hasResolvedWeaponAim)
            {
                aim = resolvedWeaponAim;
                return true;
            }

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
            if (WeaponTargetingActive)
            {
                resolvedWeaponAim = aim;
                hasResolvedWeaponAim = true;
            }
            return true;
        }

        private bool TryGetResolvedPresentationAimPoint(out Vector3 aimPoint)
        {
            if (WeaponTargetingActive
                && TryGetWeaponAim(out GameplayWeaponAim aim))
            {
                aimPoint = aim.Position;
                return true;
            }

            return TryGetPresentationAimPoint(
                WorldAimFallbackDistance,
                out aimPoint);
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
            if (WeaponTargetingActive && hasLockedWeaponAimOrigin)
            {
                return observer.Transform.TransformPoint(
                    lockedWeaponAimOriginLocal);
            }

            Vector3? presentedOrigin = getWeaponAimOrigin?.Invoke();
            return presentedOrigin.HasValue
                ? presentedOrigin.Value
                : observer.Stance.FirstPersonEyePosition;
        }

        private void LockWeaponAimOrigin()
        {
            hasResolvedWeaponAim = false;
            resolvedWeaponAim = default;
            if (observer == null)
            {
                hasLockedWeaponAimOrigin = false;
                return;
            }

            Vector3? presentedOrigin = getWeaponAimOrigin?.Invoke();
            Vector3 worldOrigin = presentedOrigin.HasValue
                ? presentedOrigin.Value
                : observer.Stance.FirstPersonEyePosition;
            lockedWeaponAimOriginLocal = observer.Transform
                .InverseTransformPoint(worldOrigin);
            hasLockedWeaponAimOrigin = true;
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

        private string ResolveAimTargetId(Transform aimTransform)
        {
            if (registry != null
                && registry.TryGetActorContaining(
                    aimTransform,
                    out GameplayActorView actor))
            {
                return actor.ActorId;
            }

            return registry != null
                && registry.TryGetLevelEntityContaining(
                    aimTransform,
                    out LevelEntityView entity)
                    ? entity.EntityId
                    : GameplayTargetIds.WorldAimPoint;
        }

        private bool UsesAuthoritativeWeaponAim()
        {
            AttackDefinition attack = session?.GetEquippedAttack(observerId);
            return WeaponTargetingActive
                && attack != null
                && attack.Contact == null;
        }

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
            if (feedback == null)
            {
                return;
            }

            if (validationFeedbackOwner != null)
            {
                bool hasValidationTarget = validationTargetRoot != null;
                if (hasValidationTarget)
                {
                    feedback.SetTarget(
                        validationTargetId,
                        validationTargetRoot);
                    feedback.SetColor(
                        validationIsValid
                            ? ResolveValidTargetColor(validationTargetId)
                            : TargetFeedbackPresenter.InvalidColor);
                }

                feedback.SetVisible(
                    hasValidationTarget,
                    hasValidationTarget
                        && validationIsValid
                        && CanShowTurnHalo());
                return;
            }

            bool acquired = currentTarget != null
                && CurrentPreview != null
                && feedbackSuppressors.Count == 0;
            if (acquired)
            {
                feedback.SetTarget(currentTarget);
                feedback.SetColor(
                    CurrentPreview.IsWithinReach
                        ? ResolveValidTargetColor(CurrentPreview.TargetId)
                        : TargetFeedbackPresenter.InvalidColor);
            }
            else if (feedbackSuppressors.Count == 0
                && WeaponTargetingActive
                && TryGetWeaponAim(out GameplayWeaponAim worldAim)
                && registry.TryGetLevelEntity(
                    worldAim.TargetId,
                    out LevelEntityView worldTarget))
            {
                feedback.SetTarget(
                    worldAim.TargetId,
                    worldTarget.transform);
                feedback.SetColor(TargetFeedbackPresenter.ValidColor);
                feedback.SetVisible(
                    outlineVisible: true,
                    turnHaloVisible: false);
                return;
            }

            feedback.SetVisible(
                acquired,
                acquired
                    && CurrentPreview.IsWithinReach
                    && CanShowTurnHalo());
        }

        private bool CanShowTurnHalo() =>
            session != null
            && session.Mode == GameplaySessionMode.TurnBased
            && session.Operation == GameplaySessionOperation.None
            && string.Equals(
                session.ActiveActorId,
                observerId,
                StringComparison.Ordinal);

        private Color ResolveValidTargetColor(string targetId) =>
            IsFriendlyActorTarget(targetId)
                ? TargetFeedbackPresenter.FriendlyColor
                : TargetFeedbackPresenter.ValidColor;

        private bool IsFriendlyActorTarget(string targetId)
        {
            PlayerPartyDefinition party = session?.Scenario?.PlayerParty;
            return party != null
                && party.Contains(observerId)
                && party.Contains(targetId);
        }

        private bool CanAcquireActorTarget(GameplayActorView target) =>
            target != null
            && (target.Targetable
                || IsFriendlyActorTarget(target.ActorId));

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
