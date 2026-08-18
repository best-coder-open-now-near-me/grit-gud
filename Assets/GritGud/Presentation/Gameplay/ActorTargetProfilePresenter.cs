using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors.Animation;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    /// <summary>
    /// Projects the portable actor target profile into Unity target regions,
    /// a query-only acquisition volume, and the matching pinned pose.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActorTargetProfilePresenter : MonoBehaviour
    {
        private const string AcquisitionObjectName = "Target Acquisition Volume";

        private readonly List<ActorTargetRegionSample> samples = new();
        private ActorStancePresenter stance;
        private GameplayTurnReplayActorStateHooks pinState;
        private ActorAnimationCoordinator animationCoordinator;
        private CapsuleCollider acquisitionCollider;
        private bool bound;

        public ActorTargetProfileKind ProfileKind { get; private set; } =
            ActorTargetProfileKind.Standing;

        internal Collider AcquisitionCollider => acquisitionCollider;

        internal void Bind(
            ActorStancePresenter stancePresenter,
            GameplayTurnReplayActorStateHooks pinStateHooks)
        {
            if (stancePresenter == null)
                throw new ArgumentNullException(nameof(stancePresenter));
            if (pinStateHooks == null)
                throw new ArgumentNullException(nameof(pinStateHooks));
            if (bound)
                Unsubscribe();

            stance = stancePresenter;
            pinState = pinStateHooks;
            animationCoordinator = GetComponent<ActorAnimationCoordinator>();
            stance.StanceChanged += HandleStanceChanged;
            pinState.PinStatePresented += HandlePinStatePresented;
            bound = true;
            EnsureAcquisitionCollider();
            ApplyResolvedProfile(presentPoseTransition: false);
        }

        public IReadOnlyList<ActorTargetRegionSample> GetTargetRegionSamples()
        {
            RequireBound();
            ActorTargetProfile profile = ActorTargetProfileCatalog.Resolve(
                stance.Stance,
                pinState.CurrentPinState != null);
            samples.Clear();
            Vector3 scale = transform.lossyScale;
            float radiusScale = Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.y),
                Mathf.Abs(scale.z));
            foreach (ActorTargetRegionProfile region in profile.Regions)
            {
                ActorLocalPoint center = region.LocalCenter;
                samples.Add(new ActorTargetRegionSample(
                    region.Id,
                    transform.TransformPoint(
                        new Vector3(center.X, center.Y, center.Z)),
                    region.Radius * radiusScale));
            }
            return samples.AsReadOnly();
        }

        internal static bool IsAcquisitionCollider(Collider candidate)
        {
            if (candidate == null)
                return false;
            ActorTargetProfilePresenter owner = candidate
                .GetComponentInParent<ActorTargetProfilePresenter>();
            return owner != null
                && ReferenceEquals(owner.acquisitionCollider, candidate);
        }

        private void HandleStanceChanged(ActorStance _) =>
            ApplyResolvedProfile(presentPoseTransition: false);

        private void HandlePinStatePresented(ActorPinState _) =>
            ApplyResolvedProfile(presentPoseTransition: true);

        private void ApplyResolvedProfile(bool presentPoseTransition)
        {
            RequireBound();
            ActorTargetProfile next = ActorTargetProfileCatalog.Resolve(
                stance.Stance,
                pinState.CurrentPinState != null);
            ActorTargetProfileKind previousKind = ProfileKind;
            ProfileKind = next.Kind;
            ApplyAcquisitionVolume(next.AcquisitionVolume);
            Physics.SyncTransforms();

            if (!presentPoseTransition || previousKind == next.Kind)
                return;
            if (next.Kind == ActorTargetProfileKind.PinnedDown)
            {
                animationCoordinator?.TryRequestAction(
                    ActorAnimationAction.Incapacitate);
                return;
            }

            // Fall Over deliberately has no automatic exit. Push Off must
            // release that held reaction layer before asking for its get-up
            // presentation, or the actor remains visually pinned forever.
            animationCoordinator?.InterruptAction(
                ActorAnimationAction.Incapacitate);
            animationCoordinator?.InterruptAction(
                ActorAnimationAction.IncapacitateShoulder);
            animationCoordinator?.TryRequestAction(
                ActorAnimationAction.Interact);
        }

        private void EnsureAcquisitionCollider()
        {
            if (acquisitionCollider != null)
                return;
            Transform existing = transform.Find(AcquisitionObjectName);
            GameObject acquisitionObject;
            if (existing != null)
            {
                acquisitionObject = existing.gameObject;
            }
            else
            {
                acquisitionObject = new GameObject(AcquisitionObjectName);
                acquisitionObject.transform.SetParent(transform, false);
            }
            acquisitionCollider = acquisitionObject.GetComponent<
                CapsuleCollider>();
            if (acquisitionCollider == null)
                acquisitionCollider = acquisitionObject.AddComponent<
                    CapsuleCollider>();
            acquisitionCollider.isTrigger = true;
        }

        private void ApplyAcquisitionVolume(
            ActorTargetAcquisitionVolume volume)
        {
            EnsureAcquisitionCollider();
            ActorLocalPoint center = volume.LocalCenter;
            acquisitionCollider.center = new Vector3(
                center.X,
                center.Y,
                center.Z);
            acquisitionCollider.radius = volume.Radius;
            acquisitionCollider.height = volume.Height;
            acquisitionCollider.direction = (int)volume.Axis;
            acquisitionCollider.enabled = true;
        }

        private void RequireBound()
        {
            if (!bound || stance == null || pinState == null)
            {
                throw new InvalidOperationException(
                    "Actor target profiles must be bound to stance and pin state.");
            }
        }

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (!bound)
                return;
            if (stance != null)
                stance.StanceChanged -= HandleStanceChanged;
            if (pinState != null)
                pinState.PinStatePresented -= HandlePinStatePresented;
            bound = false;
        }
    }
}
