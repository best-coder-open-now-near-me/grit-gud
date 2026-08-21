using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class GameplayWeaponPresenter : MonoBehaviour
    {
        private GameplaySession session;
        private GameplayWorldRegistry registry;
        private GameplayAttackController attackController;
        private GameplayProjectileController projectileController;
        private ActorAnimationCoordinator animationCoordinator;
        private WeaponPresentationCatalog catalog;
        private string actorId;
        private WeaponPresentationDefinition currentDefinition;
        private WeaponMountPresenter mountPresenter;
        private WeaponActionEffectsPresenter effectsPresenter;
        private WeaponAimPresenter aimPresenter;
        private readonly UnityWeaponDischargeOriginResolver
            weaponClearanceResolver =
                new UnityWeaponDischargeOriginResolver();
        private bool localPlayerPresentation;
        private bool replayPresentation;
        private string replayOriginalItemId;
        private float contactStrikeElapsed = -1f;

        internal string CurrentItemId { get; private set; }

        internal GameObject HeldWeapon => mountPresenter?.HeldWeapon;

        internal Transform Muzzle => mountPresenter?.Muzzle;

        internal int TransientVisualCount =>
            effectsPresenter?.TransientVisualCount ?? 0;

        internal bool ContactStrikeActive =>
            contactStrikeElapsed >= 0f;

        internal float LastShotAimErrorDegrees { get; private set; }

        internal void SetLocalControl(
            bool controlledLocally,
            TargetAcquisitionPresenter targetAcquisition)
        {
            localPlayerPresentation = controlledLocally;
            aimPresenter?.SetLocalControl(
                controlledLocally,
                targetAcquisition);
            mountPresenter?.SetLocalControl(controlledLocally);
        }

        internal void Bind(
            GameplaySession gameplaySession,
            GameplayWorldRegistry worldRegistry,
            GameplayAttackController attacks,
            GameplayProjectileController projectiles,
            ActorAnimationCoordinator actorAnimationCoordinator,
            string authoritativeActorId,
            WeaponPresentationCatalog presentationCatalog = null,
            Transform gripOverride = null,
            TargetAcquisitionPresenter targetAcquisition = null,
            bool presentAsLocalPlayer = true)
        {
            Unbind();
            session = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            registry = worldRegistry ?? throw new ArgumentNullException(
                nameof(worldRegistry));
            attackController = attacks ?? throw new ArgumentNullException(
                nameof(attacks));
            projectileController = projectiles ?? throw new ArgumentNullException(
                nameof(projectiles));
            animationCoordinator = actorAnimationCoordinator ??
                throw new ArgumentNullException(nameof(actorAnimationCoordinator));
            localPlayerPresentation = presentAsLocalPlayer;
            if (string.IsNullOrWhiteSpace(authoritativeActorId))
            {
                throw new ArgumentException(
                    "Weapon-presenter actor identifiers cannot be empty.",
                    nameof(authoritativeActorId));
            }

            actorId = authoritativeActorId;
            session.GetActor(actorId);
            Transform actorTransform = registry.GetActor(actorId).Transform;
            catalog = presentationCatalog ?? WeaponPresentationCatalog.LoadDefault();
            Transform grip = gripOverride
                ?? animationCoordinator.TargetAnimator?.GetBoneTransform(
                HumanBodyBones.RightHand);
            if (grip == null)
            {
                throw new InvalidOperationException(
                    $"Actor '{actorId}' requires a humanoid right-hand weapon grip.");
            }

            Animator animator = animationCoordinator.TargetAnimator;
            aimPresenter = GetComponent<WeaponAimPresenter>()
                ?? gameObject.AddComponent<WeaponAimPresenter>();
            aimPresenter.Bind(
                session,
                actorTransform,
                actorId,
                animator,
                presentAsLocalPlayer ? targetAcquisition : null,
                animationCoordinator.Profile);

            mountPresenter = GetComponent<WeaponMountPresenter>()
                ?? gameObject.AddComponent<WeaponMountPresenter>();
            mountPresenter.Bind(grip, localPlayerPresentation);
            effectsPresenter = GetComponent<WeaponActionEffectsPresenter>()
                ?? gameObject.AddComponent<WeaponActionEffectsPresenter>();
            effectsPresenter.Bind(mountPresenter);

            session.EquipmentChanged += HandleEquipmentChanged;
            attackController.AttackResolved += HandleAttackResolved;
            attackController.WeaponDischarged += HandleWeaponDischarged;
            projectileController.ProjectileLaunched += HandleProjectileLaunched;
            SyncEquippedWeapon();
            enabled = true;
        }

        public void Unbind()
        {
            if (session != null)
            {
                session.EquipmentChanged -= HandleEquipmentChanged;
            }

            if (attackController != null)
            {
                attackController.AttackResolved -= HandleAttackResolved;
                attackController.WeaponDischarged -= HandleWeaponDischarged;
            }

            if (projectileController != null)
            {
                projectileController.ProjectileLaunched -=
                    HandleProjectileLaunched;
            }

            ClearHeldWeapon();
            aimPresenter?.Unbind();
            effectsPresenter?.Clear();
            mountPresenter?.Unbind();
            session = null;
            registry = null;
            attackController = null;
            projectileController = null;
            animationCoordinator = null;
            catalog = null;
            aimPresenter = null;
            actorId = null;
            localPlayerPresentation = false;
            replayPresentation = false;
            replayOriginalItemId = null;
            CurrentItemId = null;
            LastShotAimErrorDegrees = 0f;
            contactStrikeElapsed = -1f;
            enabled = false;
        }

        private void Update()
        {
            effectsPresenter?.Tick(Time.unscaledDeltaTime);
            TickContactStrike(Time.unscaledDeltaTime);
        }

        internal void TickTransientVisuals(float deltaTime)
        {
            effectsPresenter?.TickTransientVisuals(deltaTime);
        }

        private void HandleEquipmentChanged(EquipmentChangeRecord change)
        {
            if (!replayPresentation
                && change != null
                && string.Equals(change.ActorId, actorId, StringComparison.Ordinal))
            {
                SyncEquippedWeapon();
            }
        }

        private void HandleAttackResolved(GameplayActionRecord action)
        {
            if (replayPresentation
                || !TryGetAttackResolution(
                    action,
                    out AttackResolutionRecord resolution)
                || !string.Equals(
                    resolution.AttackerId,
                    actorId,
                    StringComparison.Ordinal))
            {
                return;
            }

            if (currentDefinition?.AttackPresentation
                == WeaponAttackPresentationKind.ContactStrike)
            {
                PresentContactStrike();
            }
            else
            {
                PresentFire(
                    ResolveAttackDestination(resolution),
                    drawTracer: true);
            }
        }

        private void HandleProjectileLaunched(GameplayActionRecord action)
        {
            if (replayPresentation
                || !TryGetProjectileLaunch(
                    action,
                    out ProjectileLaunchRecord launch)
                || !string.Equals(
                    launch.AttackerId,
                    actorId,
                    StringComparison.Ordinal))
            {
                return;
            }

            PresentFire(ToVector3(launch.AimPoint), drawTracer: false);
        }

        private void HandleWeaponDischarged(GameplayActionRecord action)
        {
            if (replayPresentation
                || !TryGetWeaponDischarge(
                    action,
                    out WeaponDischargeRecord discharge)
                || !string.Equals(
                    discharge.AttackerId,
                    actorId,
                    StringComparison.Ordinal))
            {
                return;
            }

            PresentFire(ToVector3(discharge.AimPoint), drawTracer: true);
        }

        private void SyncEquippedWeapon()
        {
            PresentEquippedWeapon(session.GetActor(actorId).EquippedItemId);
        }

        private void PresentEquippedWeapon(string equippedItemId)
        {
            if (!replayPresentation && contactStrikeElapsed >= 0f)
            {
                animationCoordinator?.InterruptAction(
                    ActorAnimationAction.ContactStrike);
                contactStrikeElapsed = -1f;
            }
            if (string.Equals(
                    equippedItemId,
                    CurrentItemId,
                    StringComparison.Ordinal)
                && HeldWeapon != null)
            {
                return;
            }

            ClearHeldWeapon();
            CurrentItemId = equippedItemId;
            if (CurrentItemId == null)
            {
                PresentWeaponPoseIfAvailable(ActorAnimationPoseIds.Empty);
                return;
            }

            currentDefinition = catalog.Get(CurrentItemId);
            AttackDefinition equippedAttack = session
                .GetInventoryItem(actorId, CurrentItemId)
                .Attack;
            bool contactAttack = equippedAttack?.Contact != null;
            bool contactPresentation = currentDefinition.AttackPresentation
                == WeaponAttackPresentationKind.ContactStrike;
            if (contactAttack != contactPresentation)
            {
                throw new InvalidOperationException(
                    $"Weapon presentation '{CurrentItemId}' must match its authored contact-attack delivery.");
            }
            WeaponRigSocketSet weaponSockets = mountPresenter.Mount(
                currentDefinition);
            aimPresenter.BindWeapon(
                currentDefinition,
                HeldWeapon.transform,
                weaponSockets);
            if (aimPresenter.HasRig)
            {
                mountPresenter.CaptureBaseLocalPose();
            }
            PresentWeaponPoseIfAvailable(currentDefinition.AnimationSetId);
        }

        internal void BeginReplayPresentation()
        {
            if (replayPresentation)
            {
                throw new InvalidOperationException(
                    "Weapon replay presentation is already active.");
            }
            replayOriginalItemId = CurrentItemId;
            replayPresentation = true;
            ClearReplayTransients();
        }

        internal void PresentReplayEquipment(string equippedItemId)
        {
            if (!replayPresentation)
            {
                throw new InvalidOperationException(
                    "Begin weapon replay presentation before projecting equipment.");
            }
            PresentEquippedWeapon(equippedItemId);
        }

        internal void PresentReplayAction(
            TurnReplayActorActionState action)
        {
            if (!replayPresentation)
                return;
            if (action != null
                && action.Kind == TurnReplayActorActionKind.Attack
                && currentDefinition?.AttackPresentation
                    == WeaponAttackPresentationKind.ContactStrike)
            {
                return;
            }
        }

        internal void ClearReplayTransients()
        {
            effectsPresenter?.ClearTransientVisuals();
            contactStrikeElapsed = -1f;
        }

        internal ActorAnimationAction ResolveReplayAttackAnimation() =>
            currentDefinition?.AttackPresentation ==
                WeaponAttackPresentationKind.ContactStrike
                ? ActorAnimationAction.ContactStrike
                : ActorAnimationAction.WeaponFire;

        internal void EndReplayPresentation()
        {
            if (!replayPresentation)
                return;
            ClearReplayTransients();
            string originalItemId = replayOriginalItemId;
            replayOriginalItemId = null;
            replayPresentation = false;
            PresentEquippedWeapon(originalItemId);
        }

        private void PresentWeaponPoseIfAvailable(string animationSetId)
        {
            if (animationCoordinator.CanPresentWeaponPose(animationSetId))
            {
                animationCoordinator.PresentWeaponPose(animationSetId);
            }
        }

        private void PresentFire(Vector3 destination, bool drawTracer)
        {
            if (currentDefinition == null)
            {
                return;
            }

            LastShotAimErrorDegrees =
                aimPresenter.SynchronizeForShot(destination);
            if (LastShotAimErrorDegrees >
                animationCoordinator.Profile.ShotAlignmentToleranceDegrees)
            {
                Debug.LogWarning(
                    $"Weapon '{currentDefinition.ItemId}' retained "
                    + $"{LastShotAimErrorDegrees:0.##} degrees of barrel "
                    + "error after its shot aim solve.",
                    this);
            }
            ActorWeaponAnimationSet animationSet =
                animationCoordinator.Profile.GetWeaponAnimationSet(
                    currentDefinition.AnimationSetId);
            animationCoordinator.TryPresentWeaponFire();
            GameplayActorView actor = registry.GetActor(actorId);
            Vector3 origin = Muzzle != null
                ? Muzzle.position
                : actor.Stance.FirstPersonEyePosition;
            if (Muzzle != null
                && weaponClearanceResolver.TryResolve(
                    actor,
                    Muzzle,
                    out RaycastHit obstruction))
            {
                destination = obstruction.point;
                drawTracer = false;
            }
            effectsPresenter.PresentShot(
                currentDefinition,
                origin,
                destination,
                drawTracer);
            aimPresenter.PresentRecoil(animationSet);
        }

        private void PresentContactStrike()
        {
            if (currentDefinition == null)
            {
                return;
            }

            aimPresenter.SynchronizeAuthoritativeFacing();
            contactStrikeElapsed = 0f;
            animationCoordinator.TryRequestAction(
                ActorAnimationAction.ContactStrike);
        }

        internal void TickContactStrike(float deltaTime)
        {
            if (contactStrikeElapsed < 0f || currentDefinition == null)
                return;
            contactStrikeElapsed += Mathf.Max(0f, deltaTime);
            if (contactStrikeElapsed >= currentDefinition.ContactStrikeSeconds)
                contactStrikeElapsed = -1f;
        }

        private Vector3 ResolveAttackDestination(AttackResolutionRecord resolution)
        {
            if (!registry.TryGetActor(
                    resolution.TargetId,
                    out GameplayActorView target))
            {
                GameplayPosition position = session.GetActor(
                    resolution.TargetId).Pose.Position;
                return ToVector3(position) + Vector3.up;
            }

            TargetRegionId preferredRegion = resolution.HitRegion
                ?? TargetRegionId.Torso;
            IReadOnlyList<ActorTargetRegionSample> samples =
                target.TargetProfile.GetTargetRegionSamples();
            foreach (ActorTargetRegionSample sample in samples)
            {
                if (sample.Id == preferredRegion)
                {
                    return sample.WorldCenter;
                }
            }

            foreach (ActorTargetRegionSample sample in samples)
            {
                if (sample.Id == TargetRegionId.Torso)
                {
                    return sample.WorldCenter;
                }
            }

            return target.Transform.position + Vector3.up;
        }

        private void ClearHeldWeapon()
        {
            aimPresenter?.ClearWeapon();

            contactStrikeElapsed = -1f;

            if (animationCoordinator != null
                && animationCoordinator.CanPresentWeaponPose(
                    ActorAnimationPoseIds.Empty))
            {
                animationCoordinator.PresentWeaponPose(
                    ActorAnimationPoseIds.Empty);
            }

            mountPresenter?.Clear();
            currentDefinition = null;
        }

        private static bool TryGetAttackResolution(
            GameplayActionRecord action,
            out AttackResolutionRecord resolution)
        {
            if (GameplayWeaponActionOutcomes.TryGetPrimary(
                    action,
                    out AttackResolvedActionOutcome outcome))
            {
                resolution = outcome.Attack;
                return true;
            }

            resolution = null;
            return false;
        }

        private static bool TryGetProjectileLaunch(
            GameplayActionRecord action,
            out ProjectileLaunchRecord launch)
        {
            if (GameplayWeaponActionOutcomes.TryGetPrimary(
                    action,
                    out ProjectileLaunchedActionOutcome outcome))
            {
                launch = outcome.Launch;
                return true;
            }

            launch = null;
            return false;
        }

        private static bool TryGetWeaponDischarge(
            GameplayActionRecord action,
            out WeaponDischargeRecord discharge)
        {
            if (GameplayWeaponActionOutcomes.TryGetPrimary(
                    action,
                    out WeaponDischargedActionOutcome outcome))
            {
                discharge = outcome.Discharge;
                return true;
            }

            discharge = null;
            return false;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private static Vector3 ToVector3(GameplayPosition position) =>
            new Vector3(position.X, position.Y, position.Z);
    }
}
