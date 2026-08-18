using System;
using System.Collections;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayThrownExplosiveController : MonoBehaviour,
        IGameplayConsumablePowerHandler
    {
        private const int CircleSegments = 64;
        private const int TrajectorySegments = 32;
        private GameplayThrownExplosiveSession throws;
        private GameplayWorldRegistry registry;
        private string actorId;
        private GameplayDialogueLog dialogue;
        private Func<GameplayActionRecord, bool> beginEncounter;
        private TargetAcquisitionPresenter acquisition;
        private Transform actorTransform;
        private ActorAnimationCoordinator animationCoordinator;
        private ConsumablePresentationCatalog presentationCatalog;
        private string aimedItemId;
        private ThrownExplosivePresentationDefinition aimedPresentation;
        private LineRenderer uncertaintyCircle;
        private LineRenderer blastCircle;
        private LineRenderer trajectoryLine;
        private Material uncertaintyMaterial;
        private Material blastMaterial;
        private Material trajectoryMaterial;
        private GameObject armedProjectileRoot;
        private GameObject playbackRoot;
        private GameObject impactRoot;

        public GameplaySession Session { get; private set; }
        public ThrownExplosiveFailure LastFailure { get; private set; }
        public ThrownExplosiveRecord LastThrow { get; private set; }
        public string StatusMessage { get; private set; } = string.Empty;
        public bool IsAiming => aimedItemId != null;

        bool IGameplayConsumablePowerHandler.IsPending => IsAiming;

        string IGameplayConsumablePowerHandler.PendingItemId => aimedItemId;

        bool IGameplayConsumablePowerHandler.CanHandle(
            ConsumablePowerDefinition power) =>
            power is ThrownExplosiveDefinition;

        bool IGameplayConsumablePowerHandler.TryToggle(string itemId) =>
            TryToggleAim(itemId);

        bool IGameplayConsumablePowerHandler.TryConfirm() =>
            TryConfirmThrow();

        bool IGameplayConsumablePowerHandler.Cancel() => CancelAim();

        internal void Bind(
            GameplaySession session,
            GameplayWorldRegistry registry,
            IBlastWorldQuery blastWorldQuery,
            GameplayBlastConsequenceResolver consequenceResolver,
            TargetAcquisitionPresenter targetAcquisition,
            GameplayDialogueLog dialogueLog,
            string authoritativeActorId,
            uint randomSeed,
            Func<GameplayActionRecord, bool> onEncounterStartRequested = null,
            ConsumablePresentationCatalog presentation = null,
            GameplaySmokeFieldSession smokeFieldSession = null)
        {
            Unbind();
            Session = session ?? throw new ArgumentNullException(nameof(session));
            dialogue = dialogueLog ?? throw new ArgumentNullException(nameof(dialogueLog));
            beginEncounter = onEncounterStartRequested
                ?? Session.BeginEncounterFromAction;
            acquisition = targetAcquisition
                ?? throw new ArgumentNullException(nameof(targetAcquisition));
            this.registry = registry
                ?? throw new ArgumentNullException(nameof(registry));
            presentationCatalog = presentation
                ?? ConsumablePresentationCatalog.LoadDefault();
            Session.GetActor(authoritativeActorId);
            throws = new GameplayThrownExplosiveSession(
                Session,
                new UnityThrownExplosiveLandingQuery(
                    () => Session.Journal.LastEntry?.Sequence ?? 0L),
                blastWorldQuery ?? throw new ArgumentNullException(
                    nameof(blastWorldQuery)),
                consequenceResolver ?? throw new ArgumentNullException(
                    nameof(consequenceResolver)),
                new AddressedUncertaintySampler(),
                smokeFieldSession);
            enabled = true;
            SetActor(authoritativeActorId);
        }

        public void SetActor(string authoritativeActorId)
        {
            if (Session == null || throws == null || registry == null)
            {
                throw new InvalidOperationException(
                    "Bind thrown explosives before changing actors.");
            }
            if (string.IsNullOrWhiteSpace(authoritativeActorId))
            {
                throw new ArgumentException(
                    "Thrown-explosive actor identifiers cannot be empty.",
                    nameof(authoritativeActorId));
            }

            Session.GetActor(authoritativeActorId);
            CancelAim();
            actorId = authoritativeActorId;
            actorTransform = registry.GetActor(actorId).Transform;
            animationCoordinator = actorTransform.GetComponent<
                ActorAnimationCoordinator>();
            LastFailure = ThrownExplosiveFailure.None;
            LastThrow = null;
            StatusMessage = string.Empty;
        }

        public void Unbind()
        {
            StopAllCoroutines();
            ClearPlayback();
            CancelAim();
            ClearAimFeedback();
            Session = null;
            throws = null;
            registry = null;
            dialogue = null;
            actorId = null;
            beginEncounter = null;
            acquisition = null;
            actorTransform = null;
            animationCoordinator = null;
            presentationCatalog = null;
            LastFailure = ThrownExplosiveFailure.None;
            LastThrow = null;
            StatusMessage = string.Empty;
            enabled = false;
        }

        private void Update()
        {
            if (IsAiming)
            {
                RefreshAimPreview();
            }
        }

        public bool TryToggleAim(string itemId)
        {
            if (throws == null)
            {
                return false;
            }

            if (string.Equals(aimedItemId, itemId, StringComparison.Ordinal))
            {
                return CancelAim();
            }

            return BeginAim(itemId);
        }

        public bool TryConfirmThrow()
        {
            if (throws == null || !IsAiming)
            {
                return false;
            }

            string itemId = aimedItemId;
            if (!TryGetAimPoint(out GameplayPosition aimPoint))
            {
                StatusMessage = "Aim at reachable ground before throwing.";
                return false;
            }

            return TryConfirmThrow(itemId, aimPoint);
        }

        internal bool TryConfirmThrow(GameplayPosition aimPoint)
        {
            if (throws == null || !IsAiming)
            {
                return false;
            }

            return TryConfirmThrow(aimedItemId, aimPoint);
        }

        private bool TryConfirmThrow(
            string itemId,
            GameplayPosition aimPoint)
        {
            if (!throws.TryPrepareThrowItem(
                    actorId,
                    itemId,
                    aimPoint,
                    out ThrownExplosiveRecord prepared,
                    out ThrownExplosiveFailure failure))
            {
                LastFailure = failure;
                StatusMessage = $"Throw unavailable: {failure}.";
                if (failure == ThrownExplosiveFailure.Depleted)
                {
                    aimedItemId = null;
                    aimedPresentation = null;
                    HideAimPreview();
                    ClearAimFeedback();
                }
                return false;
            }

            if (!throws.TryCommitPreparedThrow(
                    prepared,
                    out GameplayActionRecord action,
                    out failure))
            {
                LastFailure = failure;
                StatusMessage = $"Throw unavailable: {failure}.";
                return false;
            }

            GameplayEncounterActionTransition.BeginAfterCommittedAction(
                Session,
                action,
                beginEncounter,
                "throw");

            LastFailure = ThrownExplosiveFailure.None;
            LastThrow = ((ThrownExplosiveActionOutcome)action.Outcomes[0]).Record;
            SynchronizeAuthoritativeFacing();
            animationCoordinator?.TryPresentThrow();
            Vector3 visualLaunchOrigin = armedProjectileRoot != null
                ? armedProjectileRoot.transform.position
                : ToVector3(LastThrow.LaunchOrigin);
            PresentThrow(LastThrow, visualLaunchOrigin);
            HideAimPreview();
            aimedItemId = null;
            aimedPresentation = null;
            ClearAimFeedback();
            int exposedTargetCount = CountExposedTargets(
                LastThrow.BlastEffects);
            StatusMessage = LastThrow.SmokeField != null
                ? $"{itemId} deployed smoke across "
                    + $"{LastThrow.Definition.AreaRadius:0.0} m."
                : $"{itemId} landed with {exposedTargetCount} exposed targets.";
            if (GameplayCombatDiagnosticFormatter.TryFormatAction(
                    action,
                    out GameplayDiagnosticProjection diagnostic))
            {
                dialogue.AppendCombatDiagnostic(diagnostic);
            }
            return true;
        }

        private static int CountExposedTargets(
            System.Collections.Generic.IReadOnlyList<BlastEffectRecord> effects)
        {
            int count = 0;
            foreach (BlastEffectRecord effect in effects)
            {
                if (effect.Exposure > 0f)
                {
                    count++;
                }
            }

            return count;
        }

        public bool CancelAim()
        {
            if (!IsAiming)
            {
                return false;
            }

            aimedItemId = null;
            aimedPresentation = null;
            HideAimPreview();
            ClearAimFeedback();
            StatusMessage = "Throw canceled.";
            return true;
        }

        private bool BeginAim(string itemId)
        {
            InventoryItemDefinition item = Session.GetInventoryItem(actorId, itemId);
            if (!(item?.ConsumablePower is ThrownExplosiveDefinition))
            {
                return false;
            }
            InventoryPowerAvailability availability =
                new GameplayInventoryAvailabilitySession(Session)
                    .EvaluatePower(actorId, itemId);
            if (!availability.IsAvailable)
            {
                StatusMessage = "Throw unavailable: "
                    + availability.Requirement + ".";
                return false;
            }

            aimedPresentation = presentationCatalog.GetThrownExplosive(itemId);
            aimedItemId = itemId;
            acquisition.SetFeedbackSuppressed(this, true);
            EnsureAimPreview(aimedPresentation);
            PresentArmedProjectile(aimedPresentation);
            RefreshAimPreview();
            StatusMessage = "AIMING " + item.DisplayName.ToUpperInvariant()
                + " - LMB THROW; PRESS ITS BUTTON/HOTKEY AGAIN OR ESC TO CANCEL";
            return true;
        }

        private void RefreshAimPreview()
        {
            InventoryItemDefinition item = Session.GetInventoryItem(
                actorId,
                aimedItemId);
            var thrownExplosive =
                (ThrownExplosiveDefinition)item.ConsumablePower;
            if (!TryGetAimPoint(out GameplayPosition aimPoint))
            {
                HideAimPreview(clearArmedProjectile: false);
                acquisition.PresentValidationFeedback(
                    this,
                    targetId: null,
                    targetRoot: null,
                    isValid: false,
                    $"INVALID LANDING - AIM AT GROUND WITHIN "
                        + $"{thrownExplosive.MaximumRange:0.#} M");
                return;
            }

            if (!throws.TryPreview(
                    actorId,
                    thrownExplosive,
                    aimPoint,
                    out float uncertaintyRadius,
                    out ThrownExplosiveFailure failure))
            {
                LastFailure = failure;
                HideAimPreview(clearArmedProjectile: false);
                StatusMessage = "Throw unavailable: " + failure + ".";
                acquisition.PresentValidationFeedback(
                    this,
                    targetId: null,
                    targetRoot: null,
                    isValid: false,
                    FormatAimFailure(
                        failure,
                        thrownExplosive.MaximumRange));
                return;
            }

            ThrownExplosivePresentationDefinition presentation =
                aimedPresentation
                ?? presentationCatalog.GetThrownExplosive(aimedItemId);
            EnsureAimPreview(presentation);
            Vector3 center = ToVector3(aimPoint)
                + Vector3.up * presentation.AimPreviewHeight;
            DrawCircle(uncertaintyCircle, center, uncertaintyRadius);
            DrawCircle(blastCircle, center, thrownExplosive.AreaRadius);
            DrawTrajectory(
                trajectoryLine,
                GetPresentationLaunchOrigin(thrownExplosive),
                ToVector3(aimPoint),
                presentation);
            uncertaintyCircle.enabled = true;
            blastCircle.enabled = true;
            trajectoryLine.enabled = true;
            LastFailure = ThrownExplosiveFailure.None;
            float distance = Session.GetActor(actorId).Pose.Position
                .DistanceTo(aimPoint);
            acquisition.PresentValidationFeedback(
                this,
                targetId: null,
                targetRoot: null,
                isValid: true,
                $"VALID LANDING  {distance:0.#} / "
                    + $"{thrownExplosive.MaximumRange:0.#} M");
        }

        internal static string FormatAimFailure(
            ThrownExplosiveFailure failure,
            float maximumRange)
        {
            switch (failure)
            {
                case ThrownExplosiveFailure.OutOfRange:
                    return $"OUT OF RANGE  {maximumRange:0.#} M MAX";
                case ThrownExplosiveFailure.ActorNotActive:
                    return "THROW UNAVAILABLE - NOT YOUR TURN";
                case ThrownExplosiveFailure.ActorIncapacitated:
                    return "THROW UNAVAILABLE - ACTOR INCAPACITATED";
                case ThrownExplosiveFailure.ActorPinned:
                    return "THROW UNAVAILABLE - ACTOR PINNED";
                case ThrownExplosiveFailure.OperationInProgress:
                    return "THROW UNAVAILABLE - ACTION IN PROGRESS";
                case ThrownExplosiveFailure.Depleted:
                    return "THROW UNAVAILABLE - ITEM DEPLETED";
                case ThrownExplosiveFailure.InsufficientActionPoints:
                    return "THROW UNAVAILABLE - INSUFFICIENT AP";
                case ThrownExplosiveFailure.InsufficientMovementOpportunity:
                    return "THROW UNAVAILABLE - INSUFFICIENT MOVEMENT";
                case ThrownExplosiveFailure.TurnModeRequired:
                    return "THROW UNAVAILABLE - ENTER TURN MODE";
                case ThrownExplosiveFailure.None:
                    return "VALID LANDING";
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
        }

        private void ClearAimFeedback()
        {
            acquisition?.ClearValidationFeedback(this);
            acquisition?.SetFeedbackSuppressed(this, false);
        }

        private void EnsureAimPreview(
            ThrownExplosivePresentationDefinition presentation)
        {
            if (presentation == null)
            {
                throw new ArgumentNullException(nameof(presentation));
            }

            if (uncertaintyCircle == null)
            {
                uncertaintyMaterial = RuntimeMaterialFactory.CreateColor(
                    presentation.UncertaintyColor,
                    "Thrown Explosive Uncertainty Material");
                blastMaterial = RuntimeMaterialFactory.CreateColor(
                    presentation.BlastColor,
                    "Thrown Explosive Blast Material");
                uncertaintyCircle = CreateCircle(
                    "Thrown Explosive Uncertainty Region",
                    uncertaintyMaterial,
                    presentation.UncertaintyRingWidth);
                blastCircle = CreateCircle(
                    "Thrown Explosive Blast Radius",
                    blastMaterial,
                    presentation.BlastRingWidth);
                trajectoryMaterial = RuntimeMaterialFactory.CreateColor(
                    presentation.UncertaintyColor,
                    "Thrown Explosive Trajectory Material");
                trajectoryLine = CreateLine(
                    "Thrown Explosive Trajectory",
                    trajectoryMaterial,
                    presentation.UncertaintyRingWidth);
            }

            uncertaintyMaterial.color = presentation.UncertaintyColor;
            blastMaterial.color = presentation.BlastColor;
            trajectoryMaterial.color = presentation.UncertaintyColor;
            uncertaintyCircle.startWidth = presentation.UncertaintyRingWidth;
            uncertaintyCircle.endWidth = presentation.UncertaintyRingWidth;
            blastCircle.startWidth = presentation.BlastRingWidth;
            blastCircle.endWidth = presentation.BlastRingWidth;
            trajectoryLine.startWidth = presentation.UncertaintyRingWidth;
            trajectoryLine.endWidth = presentation.UncertaintyRingWidth * 0.35f;
        }

        private LineRenderer CreateLine(
            string objectName,
            Material material,
            float width)
        {
            LineRenderer line = CreateCircle(objectName, material, width);
            line.loop = false;
            line.positionCount = TrajectorySegments;
            return line;
        }

        private LineRenderer CreateCircle(string objectName, Material material, float width)
        {
            var root = new GameObject(objectName);
            root.transform.SetParent(transform, false);
            LineRenderer line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = CircleSegments;
            line.startWidth = width;
            line.endWidth = width;
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static void DrawCircle(LineRenderer line, Vector3 center, float radius)
        {
            for (int index = 0; index < CircleSegments; index++)
            {
                float angle = index * Mathf.PI * 2f / CircleSegments;
                line.SetPosition(index, center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius));
            }
        }

        private static void DrawTrajectory(
            LineRenderer line,
            Vector3 origin,
            Vector3 landing,
            ThrownExplosivePresentationDefinition presentation)
        {
            for (int index = 0; index < TrajectorySegments; index++)
            {
                float progress = index / (TrajectorySegments - 1f);
                line.SetPosition(
                    index,
                    EvaluateThrowPosition(origin, landing, progress, presentation));
            }
        }

        private void HideAimPreview(bool clearArmedProjectile = true)
        {
            if (uncertaintyCircle != null)
            {
                uncertaintyCircle.enabled = false;
            }
            if (blastCircle != null)
            {
                blastCircle.enabled = false;
            }
            if (trajectoryLine != null)
            {
                trajectoryLine.enabled = false;
            }
            if (clearArmedProjectile)
            {
                GameplayObjectLifecycle.Destroy(armedProjectileRoot);
                armedProjectileRoot = null;
            }
        }

        private void OnDestroy()
        {
            ClearPlayback();
            GameplayObjectLifecycle.Destroy(uncertaintyMaterial);
            GameplayObjectLifecycle.Destroy(blastMaterial);
            GameplayObjectLifecycle.Destroy(trajectoryMaterial);
        }

        private void PresentArmedProjectile(
            ThrownExplosivePresentationDefinition presentation)
        {
            GameplayObjectLifecycle.Destroy(armedProjectileRoot);
            Transform hand = animationCoordinator?.TargetAnimator != null
                ? animationCoordinator.TargetAnimator.GetBoneTransform(
                    HumanBodyBones.RightHand)
                : null;
            Transform parent = hand != null ? hand : actorTransform;
            if (parent == null || presentation.ProjectilePrefab == null)
            {
                return;
            }

            armedProjectileRoot = Instantiate(
                presentation.ProjectilePrefab,
                parent);
            armedProjectileRoot.name = "Armed Thrown Explosive";
            armedProjectileRoot.transform.localPosition = hand != null
                ? new Vector3(0.02f, 0.08f, 0.04f)
                : new Vector3(0.32f, 1.35f, -0.18f);
            armedProjectileRoot.transform.localRotation =
                presentation.VisualRotation * Quaternion.Euler(0f, 0f, -35f);
            armedProjectileRoot.transform.localScale = Vector3.Scale(
                presentation.ProjectilePrefab.transform.localScale,
                Vector3.one * presentation.VisualScale);
            foreach (Collider collider in
                armedProjectileRoot.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
        }

        private void PresentThrow(
            ThrownExplosiveRecord record,
            Vector3 visualLaunchOrigin)
        {
            StopAllCoroutines();
            ClearPlayback();
            ThrownExplosivePresentationDefinition presentation =
                presentationCatalog.GetThrownExplosive(record.Definition.Id);
            StartCoroutine(PlayCommittedThrow(
                record,
                presentation,
                visualLaunchOrigin));
        }

        private IEnumerator PlayCommittedThrow(
            ThrownExplosiveRecord record,
            ThrownExplosivePresentationDefinition presentation,
            Vector3 visualLaunchOrigin)
        {
            if (presentation.ReleaseDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    presentation.ReleaseDelaySeconds);
            }

            playbackRoot = Instantiate(
                presentation.ProjectilePrefab,
                visualLaunchOrigin,
                presentation.VisualRotation,
                transform);
            playbackRoot.name = "Committed Thrown Explosive";
            playbackRoot.transform.localScale = Vector3.Scale(
                presentation.ProjectilePrefab.transform.localScale,
                Vector3.one * presentation.VisualScale);
            foreach (Collider collider
                in playbackRoot.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
            Vector3 origin = visualLaunchOrigin;
            Vector3 landing = ToVector3(record.ResolvedLanding);
            float elapsed = 0f;
            while (elapsed < presentation.FlightSeconds && playbackRoot != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(
                    elapsed / presentation.FlightSeconds);
                playbackRoot.transform.position = EvaluateThrowPosition(
                    origin,
                    landing,
                    progress,
                    presentation);
                playbackRoot.transform.Rotate(
                    presentation.SpinDegreesPerSecond * Time.unscaledDeltaTime,
                    Space.Self);
                yield return null;
            }

            if (playbackRoot == null) yield break;
            GameplayObjectLifecycle.Destroy(playbackRoot);
            playbackRoot = null;
            if (presentation.ImpactEffectPrefab != null)
            {
                impactRoot = Instantiate(
                    presentation.ImpactEffectPrefab,
                    landing,
                    presentation.ImpactRotation,
                    transform);
                impactRoot.name = "Committed Thrown Explosive Impact";
                float impactScale = Mathf.Max(
                    0.01f,
                    record.Definition.AreaRadius
                        * presentation.ImpactScalePerBlastRadius);
                impactRoot.transform.localScale = Vector3.Scale(
                    presentation.ImpactEffectPrefab.transform.localScale,
                    Vector3.one * impactScale);
                foreach (ParticleSystem particles
                    in impactRoot.GetComponentsInChildren<ParticleSystem>(true))
                {
                    particles.Play(true);
                }

                yield return new WaitForSecondsRealtime(
                    presentation.ImpactEffectSeconds);
            }
            ClearPlayback();
        }

        internal static Vector3 EvaluateThrowPosition(
            Vector3 origin,
            Vector3 landing,
            float progress,
            ThrownExplosivePresentationDefinition presentation)
        {
            if (presentation == null)
            {
                throw new ArgumentNullException(nameof(presentation));
            }

            float clamped = Mathf.Clamp01(progress);
            Vector3 position = Vector3.Lerp(origin, landing, clamped);
            float distance = Vector3.Distance(origin, landing);
            position.y += 4f * clamped * (1f - clamped)
                * Mathf.Clamp(
                    distance * presentation.ArcHeightPerMeter,
                    presentation.MinimumArcHeight,
                    presentation.MaximumArcHeight);
            return position;
        }

        private void ClearPlayback()
        {
            GameplayObjectLifecycle.Destroy(playbackRoot);
            GameplayObjectLifecycle.Destroy(impactRoot);
            playbackRoot = null;
            impactRoot = null;
        }

        private void SynchronizeAuthoritativeFacing()
        {
            if (Session == null || actorTransform == null || actorId == null)
            {
                return;
            }

            GameplayActorPose pose = Session.GetActor(actorId).Pose;
            actorTransform.rotation = Quaternion.Euler(
                0f,
                pose.FacingDegrees,
                0f);
        }

        private static Vector3 ToVector3(GameplayPosition position) =>
            new Vector3(position.X, position.Y, position.Z);

        private Vector3 GetPresentationLaunchOrigin(
            ThrownExplosiveDefinition definition)
        {
            if (armedProjectileRoot != null)
            {
                return armedProjectileRoot.transform.position;
            }

            return ToVector3(definition.GetLaunchOrigin(
                Session.GetActor(actorId).Pose));
        }

        private static string FormatPosition(GameplayPosition position) =>
            $"({position.X:0.00}, {position.Y:0.00}, {position.Z:0.00})";

        private bool TryGetAimPoint(out GameplayPosition point)
        {
            if (Session != null
                && acquisition != null
                && aimedItemId != null)
            {
                InventoryItemDefinition item = Session.GetInventoryItem(
                    actorId,
                    aimedItemId);
                if (item.ConsumablePower
                        is ThrownExplosiveDefinition definition)
                {
                    GameplayPosition launchOrigin = definition.GetLaunchOrigin(
                        Session.GetActor(actorId).Pose);
                    if (acquisition.TryGetPointerSurfacePoint(
                            ToVector3(launchOrigin),
                            definition.MaximumRange,
                            out Vector3 aimPoint))
                    {
                        point = new GameplayPosition(
                            aimPoint.x,
                            aimPoint.y,
                            aimPoint.z);
                        return true;
                    }
                }
            }

            point = default;
            return false;
        }
    }
}
