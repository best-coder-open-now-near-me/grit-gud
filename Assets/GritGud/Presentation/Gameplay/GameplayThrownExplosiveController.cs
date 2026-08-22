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
        private const float MinimumAimDistance = 1f;
        private const float AimDistanceMetersPerSecond = 8f;
        private GameplayThrownExplosiveSession throws;
        private GameplayWorldRegistry registry;
        private string actorId;
        private GameplayDialogueLog dialogue;
        private Func<GameplayActionRecord, bool> beginEncounter;
        private TargetAcquisitionPresenter acquisition;
        private Transform actorTransform;
        private ActorAnimationCoordinator animationCoordinator;
        private ConsumablePresentationCatalog presentationCatalog;
        private GameplayInputController gameplayInput;
        private string aimedItemId;
        private ThrownExplosivePresentationDefinition aimedPresentation;
        private float aimDistance;
        private LineRenderer uncertaintyCircle;
        private LineRenderer blastCircle;
        private LineRenderer trajectoryLine;
        private Material uncertaintyMaterial;
        private Material blastMaterial;
        private Material trajectoryMaterial;
        private GameObject armedProjectileRoot;
        private GameObject playbackRoot;
        private GameObject impactRoot;
        private GameObject replayHeldProjectileRoot;
        private GameObject replayFlightProjectileRoot;
        private GameObject replayImpactRoot;
        private string replayPresentationKey;
        private bool replayPresenting;

        public GameplaySession Session { get; private set; }
        public ThrownExplosiveFailure LastFailure { get; private set; }
        public ThrownExplosiveRecord LastThrow { get; private set; }
        public string StatusMessage { get; private set; } = string.Empty;
        public bool IsAiming => aimedItemId != null;

        internal float AimDistance => aimDistance;

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
            GameplaySmokeFieldSession smokeFieldSession = null,
            GameplayFireFieldSession fireFieldSession = null,
            GameplayInputController inputController = null)
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
            gameplayInput = inputController;
            Session.GetActor(authoritativeActorId);
            throws = new GameplayThrownExplosiveSession(
                Session,
                new UnityThrownExplosiveLandingQuery(
                    () => Session.WorldStateRevision),
                blastWorldQuery ?? throw new ArgumentNullException(
                    nameof(blastWorldQuery)),
                consequenceResolver ?? throw new ArgumentNullException(
                    nameof(consequenceResolver)),
                new AddressedUncertaintySampler(),
                smokeFieldSession,
                fireFieldSession);
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
            ClearReplayPresentation();
            GameplayObjectLifecycle.Destroy(armedProjectileRoot);
            armedProjectileRoot = null;
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
            gameplayInput = null;
            aimDistance = 0f;
            LastFailure = ThrownExplosiveFailure.None;
            LastThrow = null;
            StatusMessage = string.Empty;
            enabled = false;
        }

        private void Update()
        {
            if (IsAiming)
            {
                AdjustAimDistance(Time.unscaledDeltaTime);
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
                StatusMessage = "Aim the cursor toward a throw direction.";
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
            float visualStartingFacing = actorTransform != null
                ? actorTransform.eulerAngles.y
                : Session.GetActor(actorId).Pose.FacingDegrees;
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
                    ClearAimingState();
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
                if (failure == ThrownExplosiveFailure.WorldStateChanged)
                {
                    StatusMessage = "World changed. Aim refreshed; throw again.";
                    dialogue.Append(
                        GameplayDialogueChannel.System,
                        "THROW NOT APPLIED",
                        "Spatial state changed before confirmation. "
                            + "No item was spent; aim and confirm again.");
                }
                else
                {
                    StatusMessage = $"Throw unavailable: {failure}.";
                }
                return false;
            }

            GameplayEncounterActionTransition.BeginAfterCommittedAction(
                Session,
                action,
                beginEncounter,
                "throw");

            LastFailure = ThrownExplosiveFailure.None;
            LastThrow = ((ThrownExplosiveActionOutcome)action.Outcomes[0]).Record;
            float authoritativeTargetFacing =
                Session.GetActor(actorId).Pose.FacingDegrees;
            ActorTargetFacingActionPhase facingPhase =
                GameplayThrownExplosivePresentationTiming.CreateFacingPhase(
                    visualStartingFacing,
                    authoritativeTargetFacing);
            if (actorTransform != null)
            {
                actorTransform.rotation = Quaternion.Euler(
                    0f,
                    facingPhase.StartingFacingDegrees,
                    0f);
            }
            animationCoordinator?.TryPresentThrow();
            PresentThrow(LastThrow, facingPhase);
            HideAimPreview(clearArmedProjectile: false);
            ClearAimingState();
            ClearAimFeedback();
            int exposedTargetCount = CountExposedTargets(
                LastThrow.BlastEffects);
            StatusMessage = LastThrow.SmokeField != null
                ? $"{itemId} deployed smoke across "
                    + $"{LastThrow.Definition.AreaRadius:0.0} m."
                : LastThrow.FireField != null
                    ? $"{itemId} ignited a persistent fire field."
                : LastThrow.ConcussiveEffects.Count > 0
                    ? $"{itemId} reduced current AP for "
                        + $"{LastThrow.ConcussiveEffects.Count} target(s)."
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

            ClearAimingState();
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
            var thrownExplosive =
                (ThrownExplosiveDefinition)item.ConsumablePower;
            aimDistance = ResolveInitialAimDistance(
                thrownExplosive.MaximumRange);
            gameplayInput?.SetMovementCaptured(this, true);
            acquisition.SetFeedbackSuppressed(this, true);
            EnsureAimPreview(aimedPresentation);
            PresentArmedProjectile(aimedPresentation);
            RefreshAimPreview();
            StatusMessage = "AIMING " + item.DisplayName.ToUpperInvariant()
                + " - CURSOR AIM; W/S OR UP/DOWN DISTANCE; LMB THROW; "
                + "PRESS ITS BUTTON/HOTKEY AGAIN OR ESC TO CANCEL";
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
                    "INVALID DIRECTION - AIM WITH THE CURSOR");
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
                    + $"{thrownExplosive.MaximumRange:0.#} M  "
                    + "W/S OR UP/DOWN ADJUST");
        }

        private void AdjustAimDistance(float deltaTime)
        {
            if (gameplayInput == null || !IsAiming)
            {
                return;
            }

            ThrownExplosiveDefinition definition =
                (ThrownExplosiveDefinition)Session
                    .GetInventoryItem(actorId, aimedItemId)
                    .ConsumablePower;
            aimDistance = ApplyAimDistanceInput(
                aimDistance,
                gameplayInput.CurrentFrame.ThrowDistanceInput,
                definition.MaximumRange,
                deltaTime);
        }

        internal static float ResolveInitialAimDistance(float maximumRange) =>
            Mathf.Clamp(
                maximumRange * 0.5f,
                Mathf.Min(MinimumAimDistance, maximumRange),
                maximumRange);

        internal static float ApplyAimDistanceInput(
            float currentDistance,
            float input,
            float maximumRange,
            float deltaTime)
        {
            float minimumDistance = Mathf.Min(
                MinimumAimDistance,
                maximumRange);
            return Mathf.Clamp(
                currentDistance
                    + (Mathf.Clamp(input, -1f, 1f)
                        * AimDistanceMetersPerSecond
                        * Mathf.Max(0f, deltaTime)),
                minimumDistance,
                maximumRange);
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
                case ThrownExplosiveFailure.WorldStateChanged:
                    return "WORLD CHANGED - AIM AGAIN";
                case ThrownExplosiveFailure.Depleted:
                    return "THROW UNAVAILABLE - ITEM DEPLETED";
                case ThrownExplosiveFailure.InsufficientActionPoints:
                    return "THROW UNAVAILABLE - INSUFFICIENT AP";
                case ThrownExplosiveFailure.InsufficientMovementOpportunity:
                    return "THROW UNAVAILABLE - INSUFFICIENT MOVEMENT";
                case ThrownExplosiveFailure.InsufficientCapability:
                    return "THROW UNAVAILABLE - THROWING ARM IMPAIRED";
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
            gameplayInput?.SetMovementCaptured(this, false);
            ClearPlayback();
            ClearReplayPresentation();
            GameplayObjectLifecycle.Destroy(armedProjectileRoot);
            armedProjectileRoot = null;
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
            ActorTargetFacingActionPhase facingPhase)
        {
            StopAllCoroutines();
            ClearPlayback();
            ThrownExplosivePresentationDefinition presentation =
                presentationCatalog.GetThrownExplosive(record.Definition.Id);
            StartCoroutine(PlayCommittedThrow(
                record,
                presentation,
                facingPhase));
        }

        private IEnumerator PlayCommittedThrow(
            ThrownExplosiveRecord record,
            ThrownExplosivePresentationDefinition presentation,
            ActorTargetFacingActionPhase facingPhase)
        {
            float elapsedSequence = 0f;
            while (elapsedSequence < presentation.ReleaseDelaySeconds)
            {
                elapsedSequence = Mathf.Min(
                    presentation.ReleaseDelaySeconds,
                    elapsedSequence + Time.unscaledDeltaTime);
                PresentFacing(
                    facingPhase.SampleFacingDegrees(
                        elapsedSequence /
                        GameplayThrownExplosivePresentationTiming
                            .TotalSequenceSeconds));
                yield return null;
            }
            PresentFacing(facingPhase.TargetFacingDegrees);

            Vector3 visualLaunchOrigin;
            if (armedProjectileRoot != null)
            {
                playbackRoot = armedProjectileRoot;
                armedProjectileRoot = null;
                visualLaunchOrigin = playbackRoot.transform.position;
                playbackRoot.transform.SetParent(transform, true);
                playbackRoot.transform.rotation = presentation.VisualRotation;
            }
            else
            {
                visualLaunchOrigin = ToVector3(record.LaunchOrigin);
                playbackRoot = Instantiate(
                    presentation.ProjectilePrefab,
                    visualLaunchOrigin,
                    presentation.VisualRotation,
                    transform);
            }
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

        internal void BeginReplayPresentation()
        {
            if (replayPresenting)
                throw new InvalidOperationException(
                    "Thrown-explosive replay presentation is already active.");
            if (Session == null || registry == null ||
                presentationCatalog == null)
            {
                throw new InvalidOperationException(
                    "Bind thrown-explosive presentation before replay.");
            }
            ClearReplayPresentation();
            replayPresenting = true;
        }

        internal void PresentReplay(
            GameplayPresentationWorldStateSample sample)
        {
            if (!replayPresenting)
                throw new InvalidOperationException(
                    "Begin thrown-explosive replay before presenting it.");
            if (sample == null) throw new ArgumentNullException(nameof(sample));
            if (!(sample.Frame.SemanticRecord is GameplayActionRecord action)
                || !TryGetThrownRecord(action, out ThrownExplosiveRecord record))
            {
                ClearReplayTransients();
                return;
            }

            string key = record.ThrowerId + ":" + record.Sequence;
            if (!string.Equals(
                    replayPresentationKey,
                    key,
                    StringComparison.Ordinal))
            {
                ClearReplayTransients();
                replayPresentationKey = key;
            }
            ThrownExplosivePresentationDefinition presentation =
                presentationCatalog.GetThrownExplosive(record.Definition.Id);
            float progress = Mathf.Clamp01(sample.Progress);
            if (progress <= GameplayThrownExplosivePresentationTiming
                    .ReleaseNormalizedTime)
            {
                GameplayObjectLifecycle.Destroy(replayFlightProjectileRoot);
                GameplayObjectLifecycle.Destroy(replayImpactRoot);
                replayFlightProjectileRoot = null;
                replayImpactRoot = null;
                EnsureReplayHeldProjectile(record, presentation);
                return;
            }

            GameplayObjectLifecycle.Destroy(replayHeldProjectileRoot);
            replayHeldProjectileRoot = null;
            if (progress < GameplayThrownExplosivePresentationTiming
                    .ImpactNormalizedTime)
            {
                GameplayObjectLifecycle.Destroy(replayImpactRoot);
                replayImpactRoot = null;
                EnsureReplayFlightProjectile(record, presentation);
                float flightProgress = Mathf.InverseLerp(
                    GameplayThrownExplosivePresentationTiming
                        .ReleaseNormalizedTime,
                    GameplayThrownExplosivePresentationTiming
                        .ImpactNormalizedTime,
                    progress);
                replayFlightProjectileRoot.transform.position =
                    EvaluateThrowPosition(
                        ToVector3(record.LaunchOrigin),
                        ToVector3(record.ResolvedLanding),
                        flightProgress,
                        presentation);
                float flightSeconds = flightProgress
                    * presentation.FlightSeconds;
                replayFlightProjectileRoot.transform.rotation =
                    presentation.VisualRotation
                    * Quaternion.Euler(
                        presentation.SpinDegreesPerSecond * flightSeconds);
                return;
            }

            GameplayObjectLifecycle.Destroy(replayFlightProjectileRoot);
            replayFlightProjectileRoot = null;
            EnsureReplayImpact(record, presentation, progress);
        }

        internal void ClearReplayTransients()
        {
            GameplayObjectLifecycle.Destroy(replayHeldProjectileRoot);
            GameplayObjectLifecycle.Destroy(replayFlightProjectileRoot);
            GameplayObjectLifecycle.Destroy(replayImpactRoot);
            replayHeldProjectileRoot = null;
            replayFlightProjectileRoot = null;
            replayImpactRoot = null;
            replayPresentationKey = null;
        }

        internal void EndReplayPresentation()
        {
            if (!replayPresenting) return;
            ClearReplayTransients();
            replayPresenting = false;
        }

        private void ClearReplayPresentation()
        {
            ClearReplayTransients();
            replayPresenting = false;
        }

        private void EnsureReplayHeldProjectile(
            ThrownExplosiveRecord record,
            ThrownExplosivePresentationDefinition presentation)
        {
            if (replayHeldProjectileRoot != null) return;
            GameplayActorView actor = registry.GetActor(record.ThrowerId);
            ActorAnimationCoordinator replayAnimation = actor.Root
                .GetComponent<ActorAnimationCoordinator>();
            Transform hand = replayAnimation?.TargetAnimator != null
                ? replayAnimation.TargetAnimator.GetBoneTransform(
                    HumanBodyBones.RightHand)
                : null;
            Transform parent = hand != null ? hand : actor.Transform;
            replayHeldProjectileRoot = Instantiate(
                presentation.ProjectilePrefab,
                parent);
            replayHeldProjectileRoot.name =
                "Replay Held Thrown Explosive";
            replayHeldProjectileRoot.transform.localPosition = hand != null
                ? new Vector3(0.02f, 0.08f, 0.04f)
                : new Vector3(0.32f, 1.35f, -0.18f);
            replayHeldProjectileRoot.transform.localRotation =
                presentation.VisualRotation * Quaternion.Euler(0f, 0f, -35f);
            ConfigureReplayProjectile(
                replayHeldProjectileRoot,
                presentation);
        }

        private void EnsureReplayFlightProjectile(
            ThrownExplosiveRecord record,
            ThrownExplosivePresentationDefinition presentation)
        {
            if (replayFlightProjectileRoot != null) return;
            replayFlightProjectileRoot = Instantiate(
                presentation.ProjectilePrefab,
                ToVector3(record.LaunchOrigin),
                presentation.VisualRotation,
                transform);
            replayFlightProjectileRoot.name =
                "Replay Flying Thrown Explosive";
            ConfigureReplayProjectile(
                replayFlightProjectileRoot,
                presentation);
        }

        private static void ConfigureReplayProjectile(
            GameObject projectile,
            ThrownExplosivePresentationDefinition presentation)
        {
            projectile.transform.localScale = Vector3.Scale(
                presentation.ProjectilePrefab.transform.localScale,
                Vector3.one * presentation.VisualScale);
            foreach (Collider collider in
                projectile.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
        }

        private void EnsureReplayImpact(
            ThrownExplosiveRecord record,
            ThrownExplosivePresentationDefinition presentation,
            float normalizedProgress)
        {
            if (presentation.ImpactEffectPrefab == null) return;
            if (replayImpactRoot == null)
            {
                replayImpactRoot = Instantiate(
                    presentation.ImpactEffectPrefab,
                    ToVector3(record.ResolvedLanding),
                    presentation.ImpactRotation,
                    transform);
                replayImpactRoot.name =
                    "Replay Thrown Explosive Impact";
                float impactScale = Mathf.Max(
                    0.01f,
                    record.Definition.AreaRadius
                        * presentation.ImpactScalePerBlastRadius);
                replayImpactRoot.transform.localScale = Vector3.Scale(
                    presentation.ImpactEffectPrefab.transform.localScale,
                    Vector3.one * impactScale);
            }
            float impactProgress = Mathf.InverseLerp(
                GameplayThrownExplosivePresentationTiming
                    .ImpactNormalizedTime,
                1f,
                normalizedProgress);
            foreach (ParticleSystem particles in
                replayImpactRoot.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Simulate(
                    impactProgress * presentation.ImpactEffectSeconds,
                    withChildren: false,
                    restart: true,
                    fixedTimeStep: true);
                particles.Pause(withChildren: false);
            }
        }

        private static bool TryGetThrownRecord(
            GameplayActionRecord action,
            out ThrownExplosiveRecord record)
        {
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (!(outcome is ThrownExplosiveActionOutcome thrown))
                    continue;
                record = thrown.Record;
                return true;
            }
            record = null;
            return false;
        }

        private void PresentFacing(float facingDegrees)
        {
            if (actorTransform == null) return;
            actorTransform.rotation = Quaternion.Euler(
                0f,
                facingDegrees,
                0f);
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
                    GameplayActorPose actorPose = Session.GetActor(actorId).Pose;
                    Vector3 actorPosition = ToVector3(actorPose.Position);
                    if (acquisition.TryGetPointerRay(out Ray pointerRay)
                        && TryResolveAimDirection(
                            pointerRay,
                            actorPosition,
                            out Vector3 aimDirection))
                    {
                        Vector3 aimPoint = actorPosition
                            + (aimDirection * Mathf.Clamp(
                                aimDistance,
                                Mathf.Min(
                                    MinimumAimDistance,
                                    definition.MaximumRange),
                                definition.MaximumRange));
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

        internal static bool TryResolveAimDirection(
            Ray pointerRay,
            Vector3 actorPosition,
            out Vector3 direction)
        {
            if (!IsFinite(pointerRay.origin)
                || !IsFinite(pointerRay.direction)
                || pointerRay.direction.sqrMagnitude <= 0.0001f
                || !IsFinite(actorPosition))
            {
                direction = default;
                return false;
            }

            var normalizedRay = new Ray(
                pointerRay.origin,
                pointerRay.direction.normalized);
            var actorPlane = new Plane(Vector3.up, actorPosition);
            Vector3 flatDirection = default;
            if (actorPlane.Raycast(normalizedRay, out float distance)
                && distance >= 0f)
            {
                flatDirection = Vector3.ProjectOnPlane(
                    normalizedRay.GetPoint(distance) - actorPosition,
                    Vector3.up);
            }

            if (flatDirection.sqrMagnitude <= 0.0001f)
            {
                flatDirection = Vector3.ProjectOnPlane(
                    normalizedRay.direction,
                    Vector3.up);
            }
            if (flatDirection.sqrMagnitude <= 0.0001f)
            {
                flatDirection = Vector3.ProjectOnPlane(
                    actorPosition - normalizedRay.origin,
                    Vector3.up);
            }
            if (flatDirection.sqrMagnitude <= 0.0001f)
            {
                direction = default;
                return false;
            }

            direction = flatDirection.normalized;
            return true;
        }

        private void ClearAimingState()
        {
            gameplayInput?.SetMovementCaptured(this, false);
            aimedItemId = null;
            aimedPresentation = null;
            aimDistance = 0f;
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x)
            && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y)
            && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z)
            && !float.IsInfinity(value.z);
    }
}
