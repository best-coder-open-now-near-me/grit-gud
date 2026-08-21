using System.Linq;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using GritGud.Presentation.Actors;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace GritGud.Presentation.Tests
{
    public sealed class ActorStancePresenterTests
    {
        [Test]
        public void CrouchingShortensCapsuleWithoutMovingItsBottom()
        {
            var actor = new GameObject("Stance Test Actor");
            try
            {
                CharacterController controller = actor.AddComponent<CharacterController>();
                controller.height = 2f;
                controller.center = new Vector3(0f, 1f, 0f);
                controller.radius = 0.3f;
                var presenter = actor.AddComponent<ActorStancePresenter>();
                float standingBottom = controller.center.y - (controller.height * 0.5f);
                float standingPivot = presenter.CameraPivotHeight;

                presenter.ApplyResolved(ActorStance.Crouched);

                Assert.That(presenter.Stance, Is.EqualTo(ActorStance.Crouched));
                Assert.That(controller.height, Is.LessThan(2f));
                Assert.That(presenter.CameraPivotHeight, Is.LessThan(standingPivot));
                Assert.That(
                    controller.center.y - (controller.height * 0.5f),
                    Is.EqualTo(standingBottom).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void CrouchedMovementUsesSneakSpeedAndIgnoresSprint()
        {
            ActorMotionProfile motion = Resources.Load<GameObject>(
                    "Actors/DefaultPlayerActor")
                .GetComponent<ThirdPersonMotor>()
                .MotionProfile;
            float standingWalk = motion.ResolveMovementSpeed(
                sprint: false,
                ActorStance.Standing);
            float standingSprint = motion.ResolveMovementSpeed(
                sprint: true,
                ActorStance.Standing);
            float crouchedWalk = motion.ResolveMovementSpeed(
                sprint: false,
                ActorStance.Crouched);
            float crouchedSprint = motion.ResolveMovementSpeed(
                sprint: true,
                ActorStance.Crouched);

            Assert.That(standingSprint, Is.GreaterThan(standingWalk));
            Assert.That(crouchedWalk, Is.LessThan(standingWalk));
            Assert.That(crouchedSprint, Is.EqualTo(crouchedWalk));
            Assert.That(
                motion.ResolveMovementSpeed(
                    sprint: false,
                    ActorStance.Standing,
                    movementSpeedMultiplier: 0.75f),
                Is.EqualTo(standingWalk * 0.75f));
        }

        [Test]
        public void StandingIsRejectedWhenOverheadGeometryBlocksCapsule()
        {
            var actor = new GameObject("Blocked Stance Test Actor");
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                CharacterController controller = actor.AddComponent<CharacterController>();
                controller.height = 2f;
                controller.center = new Vector3(0f, 1f, 0f);
                controller.radius = 0.3f;
                var presenter = actor.AddComponent<ActorStancePresenter>();
                presenter.ApplyResolved(ActorStance.Crouched);
                ceiling.transform.position = new Vector3(0f, 1.65f, 0f);
                ceiling.transform.localScale = new Vector3(2f, 0.3f, 2f);
                Physics.SyncTransforms();

                StanceTransitionValidation validation = presenter.Validate(
                    CreateSnapshot(ActorStance.Crouched),
                    ActorStance.Standing);

                Assert.That(validation.Accepted, Is.False);
                Assert.That(validation.FailureCode,
                    Is.EqualTo("stance.overhead-blocked"));
                Assert.That(presenter.Stance, Is.EqualTo(ActorStance.Crouched));
                Assert.That(controller.height, Is.LessThan(2f));
            }
            finally
            {
                Object.DestroyImmediate(ceiling);
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void TargetRegionsAreStableAndMoveWithStance()
        {
            var actor = new GameObject("Target Region Test Actor");
            try
            {
                CharacterController controller = actor.AddComponent<CharacterController>();
                controller.height = 2f;
                controller.center = new Vector3(0f, 1f, 0f);
                var presenter = actor.AddComponent<ActorStancePresenter>();
                var pinState = actor.AddComponent<
                    GameplayTurnReplayActorStateHooks>();
                var targetProfile = actor.AddComponent<
                    ActorTargetProfilePresenter>();
                targetProfile.Bind(presenter, pinState);
                ActorTargetRegionSample[] standing =
                    targetProfile.GetTargetRegionSamples().ToArray();
                Vector3 standingEye = presenter.FirstPersonEyePosition;

                presenter.ApplyResolved(ActorStance.Crouched);
                ActorTargetRegionSample[] crouched =
                    targetProfile.GetTargetRegionSamples().ToArray();
                Vector3 crouchedEye = presenter.FirstPersonEyePosition;

                Assert.That(standing.Select(sample => sample.Id).Distinct().Count(),
                    Is.EqualTo(6));
                Assert.That(crouched.Select(sample => sample.Id),
                    Is.EqualTo(standing.Select(sample => sample.Id)));
                Assert.That(
                    crouched.Single(sample => sample.Id == TargetRegionId.Head)
                        .WorldCenter.y,
                    Is.LessThan(standing.Single(
                        sample => sample.Id == TargetRegionId.Head).WorldCenter.y));
                Assert.That(crouched.All(sample => sample.Radius > 0f), Is.True);
                Assert.That(crouchedEye.y, Is.LessThan(standingEye.y));
                Assert.That(crouchedEye,
                    Is.EqualTo(crouched.Single(
                        sample => sample.Id == TargetRegionId.Head).WorldCenter)
                        .Using(Vector3ComparerWithEqualsOperator.Instance));
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        private static GameplayActorSnapshot CreateSnapshot(ActorStance stance)
        {
            return new GameplayActorSnapshot(
                "actor",
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f,
                    stance),
                new TurnBudget(0, 0f));
        }
    }
}
