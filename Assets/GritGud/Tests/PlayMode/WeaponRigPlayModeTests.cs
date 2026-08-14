using System;
using System.Collections;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GritGud.PlayMode.Tests
{
    public sealed class WeaponRigPlayModeTests
    {
        [UnityTest]
        public IEnumerator EquipmentAndAttackEventsDrivePoseRecoilAndRecovery()
        {
            GameObject playerPrefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject player = Object.Instantiate(playerPrefab);
            LevelWorld world = null;
            GameplayWorldRegistry registry = null;
            try
            {
                yield return null;

                world = new LevelWorld(
                    new GameObject("Weapon Production Flow World"),
                    new Dictionary<string, LevelEntityView>(),
                    null);
                registry = new GameplayWorldRegistry(world);
                registry.RegisterActor(
                    "player",
                    ActorPresentationIds.DefaultPlayer,
                    targetable: false,
                    player);
                GameplaySession session = CreateRifleSession();
                var acquisition =
                    player.AddComponent<TargetAcquisitionPresenter>();
                var attacks = player.AddComponent<GameplayAttackController>();
                attacks.Bind(
                    session,
                    acquisition,
                    new GameplayDialogueLog(),
                    "player",
                    scenarioSeed: 7u);
                var projectiles =
                    player.AddComponent<GameplayProjectileController>();
                ActorAnimationCoordinator animation =
                    player.GetComponent<ActorAnimationCoordinator>();
                Animator animator = animation.TargetAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var presenter =
                    player.AddComponent<GameplayWeaponPresenter>();
                presenter.Bind(
                    session,
                    registry,
                    attacks,
                    projectiles,
                    animation,
                    "player",
                    presentAsLocalPlayer: false);

                var equipment = new GameplayEquipmentSession(session);
                Assert.That(
                    equipment.TryResolve(
                        "player",
                        "weapon.rifle",
                        equip: false,
                        out _,
                        out EquipmentChangeFailure unequipFailure),
                    Is.True,
                    unequipFailure.ToString());
                Assert.That(presenter.HeldWeapon, Is.Null);
                Assert.That(
                    animation.CurrentWeaponAnimationSetId,
                    Is.EqualTo(ActorAnimationPoseIds.Empty));

                Assert.That(
                    equipment.TryResolve(
                        "player",
                        "weapon.rifle",
                        equip: true,
                        out _,
                        out EquipmentChangeFailure equipFailure),
                    Is.True,
                    equipFailure.ToString());
                Assert.That(presenter.HeldWeapon, Is.Not.Null);
                Assert.That(
                    animation.CurrentWeaponAnimationSetId,
                    Is.EqualTo(ActorAnimationPoseIds.Rifle));

                yield return null;

                WeaponAimRig rig = animator.GetComponent<WeaponAimRig>();
                Assert.That(rig, Is.Not.Null);
                session.EnterTurnMode();
                GameplayPosition aimPoint = new GameplayPosition(
                    player.transform.position.x,
                    player.transform.position.y + 1.4f,
                    player.transform.position.z + 15f);
                Assert.That(attacks.TryDischarge(aimPoint), Is.True);
                Assert.That(
                    animation.LastRequestedAction,
                    Is.EqualTo(ActorAnimationAction.WeaponFire));
                Assert.That(animation.ActionSequence, Is.EqualTo(1));
                Assert.That(rig.IsRecoiling, Is.True);
                Assert.That(rig.RecoilWeight, Is.EqualTo(1f));

                ActorWeaponAnimationSet rifleAnimation =
                    animation.Profile.GetWeaponAnimationSet(
                        ActorAnimationPoseIds.Rifle);
                yield return new WaitForSeconds(
                    rifleAnimation.RecoilHoldSeconds + 0.08f);
                float recoveringWeight = rig.RecoilWeight;
                Assert.That(recoveringWeight, Is.InRange(0f, 0.99f));

                Assert.That(attacks.TryDischarge(aimPoint), Is.True);
                Assert.That(animation.ActionSequence, Is.EqualTo(2));
                Assert.That(rig.RecoilWeight, Is.EqualTo(1f));
                Assert.That(
                    rig.RecoilWeight,
                    Is.GreaterThan(recoveringWeight),
                    "Rapid fire must restart the authored recoil envelope.");

                yield return new WaitForSeconds(
                    rifleAnimation.RecoilHoldSeconds +
                    rifleAnimation.RecoilReturnSeconds + 0.08f);
                Assert.That(rig.IsRecoiling, Is.False);
                Assert.That(rig.RecoilWeight, Is.Zero.Within(0.001f));
                Assert.That(
                    animation.CurrentWeaponAnimationSetId,
                    Is.EqualTo(ActorAnimationPoseIds.Rifle));
            }
            finally
            {
                registry?.Dispose();
                world?.Dispose();
                if (registry == null && player != null)
                {
                    Object.DestroyImmediate(player);
                }
            }
        }

        [UnityTest]
        public IEnumerator SupportHandStaysOnSocketAcrossCursorAimTargets()
        {
            GameObject playerPrefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject player = Object.Instantiate(playerPrefab);
            try
            {
                yield return null;

                ActorAnimationCoordinator presenter =
                    player.GetComponent<ActorAnimationCoordinator>();
                Animator animator = presenter.TargetAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Transform rightHand = animator.GetBoneTransform(
                    HumanBodyBones.RightHand);
                Transform leftHand = animator.GetBoneTransform(
                    HumanBodyBones.LeftHand);
                Transform leftUpper = animator.GetBoneTransform(
                    HumanBodyBones.LeftUpperArm);
                Transform leftLower = animator.GetBoneTransform(
                    HumanBodyBones.LeftLowerArm);
                float upperLength = Vector3.Distance(
                    leftUpper.position,
                    leftLower.position);
                float lowerLength = Vector3.Distance(
                    leftLower.position,
                    leftHand.position);
                WeaponPresentationDefinition rifle =
                    WeaponPresentationCatalog.LoadDefault().Get(
                        "weapon.rifle");

                GameObject weapon = Object.Instantiate(
                    rifle.Prefab,
                    rightHand,
                    false);
                weapon.transform.SetLocalPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);
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
                presenter.PresentWeaponPose(ActorAnimationPoseIds.Rifle);

                yield return new WaitForSeconds(
                    sockets.SupportBlendSeconds + 0.05f);

                Vector3 origin = player.transform.position + Vector3.up * 1.4f;
                driver.SetAimPoint(
                    origin + new Vector3(-3f, 1.5f, 12f));
                for (int frame = 0; frame < 12; frame++)
                {
                    yield return null;
                }

                float leftError = Vector3.Distance(
                    leftHand.position,
                    sockets.SupportHand.position);
                float primaryError = Vector3.Distance(
                    rightHand.position,
                    sockets.RightHandGrip.position);

                driver.SetAimPoint(
                    origin + new Vector3(3f, -1f, 12f));
                for (int frame = 0; frame < 4; frame++)
                {
                    yield return null;
                }

                float rightError = Vector3.Distance(
                    leftHand.position,
                    sockets.SupportHand.position);
                Assert.That(driver.FollowsAnimatedPrimaryGrip, Is.True);
                Assert.That(
                    leftError,
                    Is.LessThan(0.03f),
                    $"Left aim grip error was {leftError:F4} m; right was "
                    + $"{rightError:F4} m.");
                Assert.That(
                    primaryError,
                    Is.LessThan(0.03f),
                    $"Right-hand grip error was {primaryError:F4} m.");
                Assert.That(
                    rightError,
                    Is.LessThan(0.03f),
                    $"Right aim grip error was {rightError:F4} m; left was "
                    + $"{leftError:F4} m.");
                Assert.That(
                    Mathf.Abs(leftError - rightError),
                    Is.LessThan(0.015f),
                    "Moving the cursor must not change support-hand grip "
                    + $"quality (left {leftError:F4} m, right "
                    + $"{rightError:F4} m).");
                Assert.That(
                    Vector3.Distance(leftUpper.position, leftLower.position),
                    Is.EqualTo(upperLength).Within(0.001f),
                    "The support solve must preserve the upper-arm length.");
                Assert.That(
                    Vector3.Distance(leftLower.position, leftHand.position),
                    Is.EqualTo(lowerLength).Within(0.001f),
                    "The support solve must preserve the forearm length.");
                float elbowFlexion = 180f - Vector3.Angle(
                    leftUpper.position - leftLower.position,
                    leftHand.position - leftLower.position);
                Assert.That(
                    elbowFlexion,
                    Is.GreaterThan(5f),
                    "Reachable socket geometry must produce the elbow bend "
                    + "without a forced angle or wrist teleport.");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static GameplaySession CreateRifleSession()
        {
            var cost = new ActionCost(1, 0f, ActionMobility.Set);
            var attack = new AttackDefinition(
                "attack.rifle",
                "Rifle shot",
                cost,
                woundMovementPenalty: 2f,
                accuracyDecay: AccuracyDecayDefinition.None);
            var rifle = new InventoryItemDefinition(
                "weapon.rifle",
                "Rifle",
                1,
                InventoryItemKind.Weapon,
                cost,
                EquipmentEffectSet.None,
                attack);
            var actor = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    facingDegrees: 0f),
                new TurnBudget(4, 8f),
                new[] { rifle },
                initiallyEquippedItemId: rifle.Id);
            return new GameplaySession(new ScenarioDefinition(
                "weapon-production-flow-test",
                new ScenarioTimingDefinition(1f),
                new[] { actor },
                Array.Empty<ScenarioObjectiveDefinition>()));
        }
    }
}
