using System.Collections;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using GritGud.Presentation.Actors;
using GritGud.Presentation.Actors.Animation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GritGud.PlayMode.Tests
{
    public sealed class ActorRagdollPlayModeTests
    {
        [UnityTest]
        public IEnumerator ProductionRigSettlesAndReplayDoesNotRunPhysics()
        {
            GameObject ground = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            ground.name = "Ragdoll Test Ground";
            ground.transform.position = new Vector3(0f, -0.1f, 0f);
            ground.transform.localScale = new Vector3(12f, 0.2f, 12f);
            GameObject actor = Object.Instantiate(Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor"));
            try
            {
                ActorRagdollPresenter ragdoll = actor.GetComponent<
                    ActorRagdollPresenter>();
                ActorAnimationCoordinator animation = actor.GetComponent<
                    ActorAnimationCoordinator>();
                Assert.That(ragdoll, Is.Not.Null);
                Assert.That(ragdoll.RuntimeBoneCount, Is.Zero,
                    "The production ragdoll must remain inert until incapacitation.");
                ragdoll.EnsureRuntimeRig();
                Assert.That(ragdoll.RuntimeBoneCount, Is.EqualTo(12));
                Assert.That(ragdoll.ActivateImmediatelyForTests(
                    journalSequence: 1,
                    hitRegion: TargetRegionId.Torso,
                    impulseDirection: Vector3.forward,
                    handoffEventNormalizedTime: 0.5f), Is.True);

                float timeout = 3f;
                while (!ragdoll.IsSettled && timeout > 0f)
                {
                    yield return new WaitForFixedUpdate();
                    timeout -= Time.fixedDeltaTime;
                }

                Assert.That(ragdoll.IsSettled, Is.True);
                Assert.That(ragdoll.TryGetTrace(1, out var trace), Is.True);
                Assert.That(trace.IsComplete, Is.True);
                Assert.That(trace.SampleCount, Is.GreaterThan(10));
                Transform hips = animation.TargetAnimator.GetBoneTransform(
                    HumanBodyBones.Hips);
                Vector3 frozenPosition = hips.position;
                Quaternion frozenRotation = hips.rotation;
                TurnReplayEventTimeline timeline = CreateTimeline();
                TurnReplayTimedEvent actionEvent = timeline.Events[0];

                ragdoll.BeginReplayPresentation();
                Assert.That(
                    actor.GetComponentsInChildren<Rigidbody>(true),
                    Has.All.Matches<Rigidbody>(body => body.isKinematic));
                Assert.That(ragdoll.PresentReplay(
                    timeline,
                    actionEvent.StartSeconds +
                        (actionEvent.DurationSeconds * 0.5f) + 0.1f),
                    Is.True);
                yield return new WaitForFixedUpdate();
                Vector3 replayPosition = hips.position;
                yield return new WaitForFixedUpdate();
                Assert.That(
                    Vector3.Distance(hips.position, replayPosition),
                    Is.LessThan(0.0001f),
                    "Replay sampling must remain kinematic instead of rerunning PhysX.");

                ragdoll.EndReplayPresentation();

                Assert.That(
                    Vector3.Distance(hips.position, frozenPosition),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(hips.rotation, frozenRotation),
                    Is.LessThan(0.001f));
            }
            finally
            {
                Object.Destroy(actor);
                Object.Destroy(ground);
            }
        }

        private static TurnReplayEventTimeline CreateTimeline()
        {
            var previous = new TurnBudget(4, 8f);
            var resulting = new TurnBudget(3, 8f);
            var action = new GameplayActionRecord(
                1,
                new GameplayActionRequest(
                    "mara",
                    EquipmentActionIds.Equip,
                    "weapon.rifle"),
                new ActionCost(1, 0f, ActionMobility.Set),
                previous,
                resulting,
                new GameplayActionOutcome[]
                {
                    new EquipmentChangedActionOutcome(
                        new EquipmentChangeRecord(
                            "mara",
                            "weapon.rifle",
                            EquipmentChangeKind.Equip,
                            previousEquippedItemId: null,
                            resultingEquippedItemId: "weapon.rifle")),
                });
            return new TurnReplayEventTimeline(new TurnReplayWindow(
                "mara",
                new[]
                {
                    new TurnReplaySegment(
                        1,
                        "mara",
                        new GameplayJournalEntry[]
                        {
                            new ActionResolvedJournalEntry(1, action),
                            new TurnEndedJournalEntry(
                                2,
                                new TurnEndRecord(1, "mara", "raider")),
                        }),
                }));
        }
    }
}
