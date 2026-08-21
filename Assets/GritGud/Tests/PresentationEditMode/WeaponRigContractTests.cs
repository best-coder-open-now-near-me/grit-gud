using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GritGud.Presentation.Tests
{
    public sealed class WeaponRigContractTests
    {
        private const float PositionTolerance = 0.001f;

        [TestCase("weapon.rifle")]
        [TestCase("weapon.rocket-launcher")]
        [TestCase("weapon.combat-knife")]
        public void AuthoredMuzzleFacesAwayFromPrimaryGrip(string weaponId)
        {
            WeaponRigSocketSet sockets = WeaponPresentationCatalog
                .LoadDefault()
                .Get(weaponId)
                .RigSockets;
            Vector3 gripToMuzzle =
                sockets.Muzzle.position - sockets.RightHandGrip.position;

            Assert.That(
                Vector3.Angle(sockets.Muzzle.forward, gripToMuzzle),
                Is.LessThan(35f),
                $"{weaponId} must author Muzzle.forward along its barrel.");
        }

        [Test]
        public void RiflePrimaryGripStaysAtItsAuthoredMountPose()
        {
            WeaponRigSocketSet sockets = WeaponPresentationCatalog
                .LoadDefault()
                .Get("weapon.rifle")
                .RigSockets;
            Transform root = sockets.transform;
            Pose gripInRoot = LocalPose(root, sockets.RightHandGrip);

            Assert.That(
                gripInRoot.position.magnitude,
                Is.LessThan(PositionTolerance),
                "The rifle prefab root mounts on the primary wrist, so its "
                + "primary-grip socket must close at that same origin.");
            Assert.That(
                Quaternion.Angle(
                    new Quaternion(
                        -0.002023f,
                        -0.027522f,
                        -0.066818f,
                        0.997384f),
                    gripInRoot.rotation),
                Is.LessThan(0.1f),
                "The rifle primary grip must retain its authored wrist "
                + "orientation calibration.");
        }

        [Test]
        public void RifleSupportSocketStaysOnTheAuthoredForegrip()
        {
            GameObject player = InstantiatePlayer(
                out ActorAnimationCoordinator presenter);
            GameObject weapon = null;
            try
            {
                Animator animator = presenter.TargetAnimator;
                presenter.PresentWeaponPose(ActorAnimationPoseIds.Rifle);
                animator.Update(0.25f);

                Transform rightHand = animator.GetBoneTransform(
                    HumanBodyBones.RightHand);
                Transform leftUpper = animator.GetBoneTransform(
                    HumanBodyBones.LeftUpperArm);
                Transform leftLower = animator.GetBoneTransform(
                    HumanBodyBones.LeftLowerArm);
                Transform leftHand = animator.GetBoneTransform(
                    HumanBodyBones.LeftHand);
                WeaponPresentationDefinition rifle = WeaponPresentationCatalog
                    .LoadDefault()
                    .Get("weapon.rifle");
                weapon = Object.Instantiate(rifle.Prefab);
                WeaponRigSocketSet sockets =
                    weapon.GetComponent<WeaponRigSocketSet>();
                AlignGripToHand(
                    weapon.transform,
                    sockets.RightHandGrip,
                    rightHand);

                float reach = Vector3.Distance(
                        leftUpper.position,
                        leftLower.position)
                    + Vector3.Distance(
                        leftLower.position,
                        leftHand.position);
                float targetDistance = Vector3.Distance(
                    leftUpper.position,
                    sockets.SupportHand.position);
                float supportError = Vector3.Distance(
                    leftHand.position,
                    sockets.SupportHand.position);
                Vector3 visualLocalSupport = sockets.VisualRoot
                    .InverseTransformPoint(sockets.SupportHand.position);
                Quaternion visualLocalSupportRotation = Quaternion.Inverse(
                    sockets.VisualRoot.rotation) * sockets.SupportHand.rotation;
                float sourceElbowFlexion = 180f - Vector3.Angle(
                    leftUpper.position - leftLower.position,
                    leftHand.position - leftLower.position);

                TestContext.WriteLine(
                    $"supportError={supportError:F6}; reach={reach:F6}; "
                    + $"targetDistance={targetDistance:F6}; "
                    + $"reachMargin={reach - targetDistance:F6}; "
                    + $"sourceElbowFlexion={sourceElbowFlexion:F3}; "
                    + $"currentSupportLocal={sockets.SupportHand.localPosition:F6}; "
                    + $"visualLocalSupport={visualLocalSupport:F6}; "
                    + $"visualLocalSupportRotation={visualLocalSupportRotation:F6}; "
                    + $"supportToAnimatedHand={supportError:F6}");

                Assert.That(
                    Vector3.Distance(
                        visualLocalSupport,
                        new Vector3(-0.058395f, 0.039747f, 0.190156f)),
                    Is.LessThan(PositionTolerance),
                    "The support socket must remain on the authored rifle "
                    + "foregrip instead of following the animated hand.");
                Assert.That(
                    Quaternion.Angle(
                        visualLocalSupportRotation,
                        new Quaternion(
                            0.36325172f,
                            -0.8738143f,
                            -0.30538845f,
                            -0.10599399f)),
                    Is.LessThan(0.1f),
                    "The support palm orientation recovered from the frozen "
                    + "calibration pose must not be overwritten during rig "
                    + "regeneration.");
                Assert.That(
                    reach - targetDistance,
                    Is.GreaterThan(0.01f),
                    "The mounted foregrip must remain physically reachable. "
                    + $"Reach margin was {reach - targetDistance:F6} m.");
            }
            finally
            {
                Object.DestroyImmediate(weapon);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void SolverTracksPoseTransitionsWithoutStretchingEitherArmBone()
        {
            GameObject player = InstantiatePlayer(
                out ActorAnimationCoordinator presenter);
            GameObject weapon = null;
            try
            {
                Animator animator = presenter.TargetAnimator;
                Transform rightHand = animator.GetBoneTransform(
                    HumanBodyBones.RightHand);
                Transform leftUpper = animator.GetBoneTransform(
                    HumanBodyBones.LeftUpperArm);
                Transform leftLower = animator.GetBoneTransform(
                    HumanBodyBones.LeftLowerArm);
                Transform leftHand = animator.GetBoneTransform(
                    HumanBodyBones.LeftHand);
                WeaponPresentationDefinition rifle = WeaponPresentationCatalog
                    .LoadDefault()
                    .Get("weapon.rifle");
                weapon = Object.Instantiate(rifle.Prefab, rightHand, false);
                WeaponRigSocketSet sockets =
                    weapon.GetComponent<WeaponRigSocketSet>();
                WeaponAimRig driver = animator.gameObject
                    .AddComponent<WeaponAimRig>();
                driver.Bind(
                    player.transform,
                    weapon.transform,
                    sockets.Muzzle,
                    sockets.SupportHand,
                    sockets.SupportElbowHint,
                    sockets.SupportPositionWeight,
                    sockets.SupportRotationWeight,
                    sockets.SupportElbowHintWeight,
                    sockets.SupportBlendSeconds,
                    rifle.MaximumAimCorrectionDegrees,
                    presenter.Profile.MaximumBodyAimCorrectionDegrees,
                    presenter.Profile.BodyAimDegreesPerSecond,
                    presenter.Profile.WeaponAimDegreesPerSecond);

                presenter.PresentWeaponPose(ActorAnimationPoseIds.Empty);
                animator.Update(0.25f);
                driver.SynchronizeAfterAnimation(0f);
                AssertPrimaryGripClosed(rightHand, sockets.RightHandGrip);

                presenter.PresentWeaponPose(ActorAnimationPoseIds.Rifle);
                animator.Update(0.25f);
                float upperLength = Vector3.Distance(
                    leftUpper.position,
                    leftLower.position);
                float lowerLength = Vector3.Distance(
                    leftLower.position,
                    leftHand.position);
                float sourceElbowFlexion = 180f - Vector3.Angle(
                    leftUpper.position - leftLower.position,
                    leftHand.position - leftLower.position);
                driver.TickSupportBlend(sockets.SupportBlendSeconds);
                driver.SynchronizeAfterAnimation(0f);

                AssertPrimaryGripClosed(rightHand, sockets.RightHandGrip);
                Assert.That(
                    Vector3.Distance(leftUpper.position, leftLower.position),
                    Is.EqualTo(upperLength).Within(PositionTolerance));
                Assert.That(
                    Vector3.Distance(leftLower.position, leftHand.position),
                    Is.EqualTo(lowerLength).Within(PositionTolerance));
                Assert.That(
                    Vector3.Distance(
                        leftHand.position,
                        sockets.SupportHand.position),
                    Is.LessThan(0.015f),
                    "A reachable support socket must close without moving the wrist.");
                float elbowFlexion = 180f - Vector3.Angle(
                    leftUpper.position - leftLower.position,
                    leftHand.position - leftLower.position);
                TestContext.WriteLine(
                    $"sourceElbowFlexion={sourceElbowFlexion:F3}; "
                    + $"solvedElbowFlexion={elbowFlexion:F3}; "
                    + $"solvedSupportError="
                    + $"{Vector3.Distance(leftHand.position, sockets.SupportHand.position):F6}");
                Assert.That(
                    elbowFlexion,
                    Is.GreaterThan(5f),
                    "Elbow bend must result from reachable authored geometry, "
                    + "not a forced minimum angle.");

                Assert.That(
                    presenter.TryPresentThrow(),
                    Is.True);
                animator.Update(0.1f);
                Vector3 animatedThrowHand = leftHand.position;
                Vector3 authoredSupportPosition =
                    sockets.SupportHand.localPosition;
                Quaternion authoredSupportRotation =
                    sockets.SupportHand.localRotation;
                sockets.SupportHand.localPosition = new Vector3(0f, 2f, 0f);
                driver.SynchronizeAfterAnimation(0f);
                Assert.That(driver.SupportBlendWeight, Is.Zero);
                Assert.That(
                    Vector3.Distance(leftHand.position, animatedThrowHand),
                    Is.LessThan(PositionTolerance),
                    "The post-animation support solve must yield while the "
                    + "action channel owns the throwing arm.");

                sockets.SupportHand.SetLocalPositionAndRotation(
                    authoredSupportPosition,
                    authoredSupportRotation);
                for (int frame = 0; frame < 100; frame++)
                {
                    animator.Update(0.05f);
                }

                int actionLayer = animator.GetLayerIndex(
                    ActorAnimationParameters.ActionLayerName);
                Assert.That(animator.GetLayerWeight(actionLayer), Is.Zero);
                driver.TickSupportBlend(sockets.SupportBlendSeconds);
                driver.SynchronizeAfterAnimation(0f);
                Assert.That(driver.SupportBlendWeight, Is.EqualTo(1f));
                Assert.That(
                    Vector3.Distance(
                        leftHand.position,
                        sockets.SupportHand.position),
                    Is.LessThan(0.015f),
                    "The support hand must reacquire the rifle after Throw.");
                Assert.That(
                    Quaternion.Angle(
                        leftHand.rotation,
                        sockets.SupportHand.rotation),
                    Is.LessThan(1f),
                    "The support palm must recover its authored rifle grip "
                    + "instead of retaining Throw's palm-down rotation.");
            }
            finally
            {
                Object.DestroyImmediate(weapon);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void ReplaySamplesCloseBothGripsAtZeroDeltaBeforeAndAfterRebind()
        {
            GameObject player = InstantiatePlayer(
                out ActorAnimationCoordinator presenter);
            GameObject rifleObject = null;
            GameObject launcherObject = null;
            try
            {
                Animator animator = presenter.TargetAnimator;
                Transform rightHand = animator.GetBoneTransform(
                    HumanBodyBones.RightHand);
                Transform leftHand = animator.GetBoneTransform(
                    HumanBodyBones.LeftHand);
                WeaponAimRig rig = animator.gameObject
                    .AddComponent<WeaponAimRig>();
                WeaponPresentationCatalog catalog =
                    WeaponPresentationCatalog.LoadDefault();

                presenter.BeginReplayPresentation();
                rig.BeginReplayPresentation();

                WeaponPresentationDefinition rifle = catalog.Get(
                    "weapon.rifle");
                rifleObject = Object.Instantiate(rifle.Prefab);
                WeaponRigSocketSet rifleSockets = rifleObject.GetComponent<
                    WeaponRigSocketSet>();
                Bind(rig, player, rifleObject, rifleSockets, rifle, presenter);
                presenter.PresentWeaponPose(ActorAnimationPoseIds.Rifle);
                presenter.PresentReplayAction(
                    GritGud.Domain.Gameplay.ActorStance.Standing,
                    ActorAnimationAction.WeaponFire,
                    0.5f);
                rig.SetReplaySupportWeightImmediate();
                rig.SynchronizeAfterAnimation(0f);

                AssertPrimaryGripClosed(
                    rightHand,
                    rifleSockets.RightHandGrip);
                Assert.That(
                    Vector3.Distance(
                        leftHand.position,
                        rifleSockets.SupportHand.position),
                    Is.LessThan(0.015f),
                    "Paused replay must solve the support hand without "
                    + "waiting for scaled time.");
                Assert.That(rig.SupportBlendWeight, Is.EqualTo(1f));

                WeaponPresentationDefinition launcher = catalog.Get(
                    "weapon.rocket-launcher");
                launcherObject = Object.Instantiate(launcher.Prefab);
                WeaponRigSocketSet launcherSockets = launcherObject.GetComponent<
                    WeaponRigSocketSet>();
                Bind(
                    rig,
                    player,
                    launcherObject,
                    launcherSockets,
                    launcher,
                    presenter);
                presenter.PresentWeaponPose(ActorAnimationPoseIds.Launcher);
                presenter.PresentReplayAction(
                    GritGud.Domain.Gameplay.ActorStance.Standing,
                    ActorAnimationAction.WeaponFire,
                    0.75f);
                rig.SetReplaySupportWeightImmediate();
                rig.SynchronizeAfterAnimation(0f);

                AssertPrimaryGripClosed(
                    rightHand,
                    launcherSockets.RightHandGrip);
                Assert.That(
                    Vector3.Distance(
                        leftHand.position,
                        launcherSockets.SupportHand.position),
                    Is.LessThan(0.015f),
                    "Rebinding historical equipment must use the same replay "
                    + "grip solve as unchanged equipment.");
            }
            finally
            {
                Object.DestroyImmediate(launcherObject);
                Object.DestroyImmediate(rifleObject);
                Object.DestroyImmediate(player);
            }
        }

        private static void Bind(
            WeaponAimRig rig,
            GameObject player,
            GameObject weapon,
            WeaponRigSocketSet sockets,
            WeaponPresentationDefinition definition,
            ActorAnimationCoordinator presenter)
        {
            rig.Bind(
                player.transform,
                weapon.transform,
                sockets.Muzzle,
                sockets.SupportHand,
                sockets.SupportElbowHint,
                sockets.SupportPositionWeight,
                sockets.SupportRotationWeight,
                sockets.SupportElbowHintWeight,
                sockets.SupportBlendSeconds,
                definition.MaximumAimCorrectionDegrees,
                presenter.Profile.MaximumBodyAimCorrectionDegrees,
                presenter.Profile.BodyAimDegreesPerSecond,
                presenter.Profile.WeaponAimDegreesPerSecond);
        }

        private static GameObject InstantiatePlayer(
            out ActorAnimationCoordinator presenter)
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject player = Object.Instantiate(prefab);
            presenter = player.GetComponent<ActorAnimationCoordinator>();
            presenter.TargetAnimator.cullingMode =
                AnimatorCullingMode.AlwaysAnimate;
            presenter.TargetAnimator.Update(0f);
            return player;
        }

        private static void AlignGripToHand(
            Transform weapon,
            Transform grip,
            Transform hand)
        {
            Pose gripInRoot = LocalPose(weapon, grip);
            Quaternion rotation = hand.rotation
                * Quaternion.Inverse(gripInRoot.rotation);
            weapon.SetPositionAndRotation(
                hand.position - rotation * gripInRoot.position,
                rotation);
        }

        private static void AssertPrimaryGripClosed(
            Transform hand,
            Transform grip)
        {
            Assert.That(
                Vector3.Distance(hand.position, grip.position),
                Is.LessThan(PositionTolerance));
            Assert.That(
                Quaternion.Angle(hand.rotation, grip.rotation),
                Is.LessThan(0.1f));
        }

        private static Pose LocalPose(Transform root, Transform child) =>
            new(
                root.InverseTransformPoint(child.position),
                Quaternion.Inverse(root.rotation) * child.rotation);
    }
}
