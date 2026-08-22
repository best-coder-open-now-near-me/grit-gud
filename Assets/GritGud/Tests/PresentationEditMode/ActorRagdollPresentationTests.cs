using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using GritGud.Presentation.Actors;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class ActorRagdollPresentationTests
    {
        private const string ProfilePath =
            "Assets/GritGud/Presentation/Actors/DefaultActorRagdollProfile.asset";

        [Test]
        public void GeneratedProfileAndPrefabOwnTheVersionedRagdollContract()
        {
            ActorRagdollProfile profile =
                AssetDatabase.LoadAssetAtPath<ActorRagdollProfile>(
                    ProfilePath);
            GameObject prefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");

            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.TraceSchemaId,
                Is.EqualTo("default-humanoid-v1"));
            Assert.That(profile.TraceSchemaVersion, Is.EqualTo(1));
            Assert.That(profile.Bones, Has.Count.EqualTo(12));
            Assert.That(profile.HandoffNormalizedTime,
                Is.EqualTo(0.72f).Within(0.001f));
            Assert.That(profile.MaximumHandoffWaitSeconds,
                Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(profile.MaximumActiveSeconds,
                Is.EqualTo(2.25f).Within(0.001f));
            Assert.That(profile.MaximumStoredTraces, Is.EqualTo(4));
            Assert.That(prefab, Is.Not.Null);
            Assert.That(
                prefab.GetComponent<ActorRagdollPresenter>()?.Profile,
                Is.SameAs(profile));
        }

        [Test]
        public void QuantizedTraceInterpolatesRootRelativeBonePoses()
        {
            var trace = new ActorRagdollPoseTrace(
                journalSequence: 8,
                schemaId: "test-v1",
                schemaVersion: 1,
                boneCount: 2,
                handoffEventNormalizedTime: 0.6f);
            trace.AddSample(
                0f,
                new[] { Vector3.zero, Vector3.up },
                new[] { Quaternion.identity, Quaternion.identity });
            trace.AddSample(
                1f,
                new[] { new Vector3(1f, 0f, 0f), Vector3.up * 2f },
                new[]
                {
                    Quaternion.Euler(0f, 90f, 0f),
                    Quaternion.Euler(90f, 0f, 0f),
                });
            trace.Complete();
            var positions = new Vector3[2];
            var rotations = new Quaternion[2];

            trace.SampleAt(0.5f, positions, rotations);

            Assert.That(positions[0].x, Is.EqualTo(0.5f).Within(0.0015f));
            Assert.That(positions[1].y, Is.EqualTo(1.5f).Within(0.0015f));
            Assert.That(
                Quaternion.Angle(
                    rotations[0],
                    Quaternion.Euler(0f, 45f, 0f)),
                Is.LessThan(0.1f));
            Assert.That(trace.IsComplete, Is.True);
        }

        [Test]
        public void AuthoredFallHandsOffToBoundedRigAndFreezesItsTrace()
        {
            GameObject actor = Object.Instantiate(Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor"));
            try
            {
                ActorRagdollPresenter ragdoll = actor.GetComponent<
                    ActorRagdollPresenter>();
                ActorAnimationCoordinator animation = actor.GetComponent<
                    ActorAnimationCoordinator>();
                Animator animator = animation.TargetAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Update(0f);
                ragdoll.EnsureRuntimeRig();

                Assert.That(ragdoll.RuntimeBoneCount, Is.EqualTo(12));
                Assert.That(
                    actor.GetComponentsInChildren<CharacterJoint>(true),
                    Has.Length.EqualTo(11));
                Assert.That(animation.PresentWoundReaction(
                    TargetRegionId.Torso,
                    incapacitated: true), Is.True);
                Assert.That(ragdoll.ArmIncapacitation(
                    sourceTransitionSequence: 11,
                    hitRegion: TargetRegionId.Torso,
                    impulseDirection: Vector3.forward), Is.True);
                bool activated = false;
                for (int index = 0; index < 120 && !activated; index++)
                {
                    animator.Update(0.05f);
                    activated = ragdoll.TryActivateAtAuthoredHandoff();
                }

                int reactionLayer = animator.GetLayerIndex(
                    ActorAnimationParameters.ReactionLayerName);
                AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(
                    reactionLayer);
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(
                    reactionLayer);
                Assert.That(
                    activated,
                    Is.True,
                    $"action={animation.LastRequestedAction}; " +
                    $"enabled={animator.enabled}; speed={animator.speed}; " +
                    $"currentShoulder={current.IsName(ActorAnimationParameters.ShoulderFallStateName)}; " +
                    $"currentTime={current.normalizedTime}; " +
                    $"transition={animator.IsInTransition(reactionLayer)}; " +
                    $"nextShoulder={next.IsName(ActorAnimationParameters.ShoulderFallStateName)}; " +
                    $"nextTime={next.normalizedTime}");
                Assert.That(ragdoll.IsRagdollActive, Is.True);
                Assert.That(animator.enabled, Is.False);
                Assert.That(
                    actor.GetComponentsInChildren<Rigidbody>(true),
                    Has.All.Matches<Rigidbody>(body => !body.isKinematic));

                ragdoll.TickActiveRagdoll(
                    ragdoll.Profile.MaximumActiveSeconds + 0.1f);

                Assert.That(ragdoll.IsSettled, Is.True);
                Assert.That(
                    actor.GetComponentsInChildren<Rigidbody>(true),
                    Has.All.Matches<Rigidbody>(body => body.isKinematic));
                Assert.That(ragdoll.TryGetTrace(11, out var trace), Is.True);
                Assert.That(trace.IsComplete, Is.True);
                Assert.That(trace.SampleCount, Is.GreaterThanOrEqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void AuthoredHandoffTimeoutActivatesFromCurrentPose()
        {
            GameObject actor = Object.Instantiate(Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor"));
            try
            {
                ActorRagdollPresenter ragdoll = actor.GetComponent<
                    ActorRagdollPresenter>();
                ActorAnimationCoordinator animation = actor.GetComponent<
                    ActorAnimationCoordinator>();
                Animator animator = animation.TargetAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Update(0f);
                ragdoll.EnsureRuntimeRig();
                Assert.That(
                    animation.PresentTerminalCollapse(TargetRegionId.Torso),
                    Is.True);
                Assert.That(ragdoll.ArmIncapacitation(
                    sourceTransitionSequence: 23L,
                    hitRegion: TargetRegionId.Torso,
                    impulseDirection: Vector3.forward), Is.True);

                Assert.That(ragdoll.PendingArmedUnscaledTime,
                    Is.GreaterThanOrEqualTo(0f));
                Assert.That(
                    ragdoll.PendingExpectedAction,
                    Is.EqualTo(
                        ActorAnimationAction.IncapacitateShoulder));
                Assert.That(ragdoll.TickPendingHandoff(
                    ragdoll.Profile.MaximumHandoffWaitSeconds + 0.01f),
                    Is.True);

                Assert.That(ragdoll.HasPendingActivation, Is.False);
                Assert.That(ragdoll.IsRagdollActive, Is.True);
                Assert.That(
                    ragdoll.LastHandoffSourceTransitionSequence,
                    Is.EqualTo(23L));
                Assert.That(
                    ragdoll.LastHandoffFallbackReason,
                    Is.EqualTo(ActorRagdollHandoffFallbackReason
                        .AuthoredHandoffTimedOut));
                Assert.That(animator.enabled, Is.False);
                Assert.That(ragdoll.TryGetTrace(23L, out _), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void ReplaySamplesRecordedTraceAndRestoresFrozenLivePose()
        {
            GameObject actor = Object.Instantiate(Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor"));
            try
            {
                ActorRagdollPresenter ragdoll = actor.GetComponent<
                    ActorRagdollPresenter>();
                ActorAnimationCoordinator animation = actor.GetComponent<
                    ActorAnimationCoordinator>();
                ragdoll.EnsureRuntimeRig();
                Assert.That(ragdoll.ActivateImmediatelyForTests(
                    sourceTransitionSequence: 1,
                    hitRegion: TargetRegionId.Head,
                    impulseDirection: Vector3.forward,
                    handoffEventNormalizedTime: 0.5f), Is.True);
                Transform head = animation.TargetAnimator.GetBoneTransform(
                    HumanBodyBones.Head);
                Vector3 recordedStart = head.position;
                Vector3 recordedEnd = recordedStart +
                    new Vector3(0.2f, 0.1f, 0.05f);
                head.position = recordedEnd;
                head.GetComponent<Rigidbody>().position = recordedEnd;
                ragdoll.TickActiveRagdoll(0.1f);
                ragdoll.TickActiveRagdoll(
                    ragdoll.Profile.MaximumActiveSeconds);
                Vector3 frozenLivePosition = head.position;
                ragdoll.BeginReplayPresentation();
                bool presented = ragdoll.PresentReplay(
                    transitionSequence: 1,
                    normalizedProgress: 0.6f,
                    presentationDurationSeconds: 0.8f);

                Assert.That(presented, Is.True);
                Assert.That(
                    Vector3.Distance(head.position, recordedStart),
                    Is.GreaterThan(0.04f));
                Assert.That(
                    Vector3.Distance(head.position, recordedEnd),
                    Is.GreaterThan(0.04f));

                ragdoll.EndReplayPresentation();

                Assert.That(
                    Vector3.Distance(head.position, frozenLivePosition),
                    Is.LessThan(0.0001f));
                Assert.That(ragdoll.IsSettled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void TerminalGroundingTracksTheAnimatedBodyFootprint()
        {
            var host = new GameObject("Terminal Grounding Host");
            GameObject actor = Object.Instantiate(Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor"));
            LevelWorld world = null;
            GameplayWorldRegistry registry = null;
            try
            {
                world = new LevelWorld(
                    new GameObject("Terminal Grounding World"),
                    new Dictionary<string, LevelEntityView>(),
                    null);
                registry = new GameplayWorldRegistry(world);
                registry.RegisterActor(
                    "actor", "test", targetable: true, actor);
                GameplayCharacterGroundingPresenter grounding =
                    host.AddComponent<GameplayCharacterGroundingPresenter>();
                grounding.Bind(
                    registry,
                    GameplayVisualTheme.LoadDefault(),
                    world.Root.transform);
                ActorAnimationCoordinator animation = actor.GetComponent<
                    ActorAnimationCoordinator>();
                Animator animator = animation.TargetAnimator;
                Assert.That(
                    animation.PresentTerminalCollapse(TargetRegionId.Torso),
                    Is.True);
                animator.enabled = false;
                PositionBone(animator, HumanBodyBones.Hips, 0f, 0.8f);
                PositionBone(animator, HumanBodyBones.Chest, 0f, 1.25f);
                PositionBone(animator, HumanBodyBones.UpperChest, 0f, 1.45f);
                PositionBone(animator, HumanBodyBones.Head, 0f, 1.9f);
                PositionBone(animator, HumanBodyBones.LeftHand, -0.25f, 1.1f);
                PositionBone(animator, HumanBodyBones.RightHand, 0.25f, 1.1f);
                PositionBone(animator, HumanBodyBones.LeftFoot, -0.18f, 0f);
                PositionBone(animator, HumanBodyBones.RightFoot, 0.18f, 0f);

                grounding.RefreshNow();

                Transform visual = world.Root.transform.Find(
                    "Gameplay Character Grounding/actor Contact Grounding");
                Assert.That(visual, Is.Not.Null);
                Assert.That(visual.position.z, Is.GreaterThan(0.7f));
                Assert.That(visual.localScale.y, Is.GreaterThan(1.8f));
                Assert.That(
                    visual.localScale.y,
                    Is.GreaterThan(visual.localScale.x * 2f));
            }
            finally
            {
                Object.DestroyImmediate(host);
                registry?.Dispose();
                world?.Dispose();
            }
        }

        private static void PositionBone(
            Animator animator,
            HumanBodyBones bone,
            float x,
            float z)
        {
            Transform target = animator.GetBoneTransform(bone);
            if (target != null)
                target.position = new Vector3(x, 0.5f, z);
        }

    }
}
