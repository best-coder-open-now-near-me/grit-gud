using System;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Gameplay;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GritGud.Editor
{
    public static class DefaultWeaponRigAssetGenerator
    {
        public const string RifleRigPath =
            "Assets/GritGud/Content/Resources/Gameplay/WeaponRigs/RifleWeaponRig.prefab";
        public const string LauncherRigPath =
            "Assets/GritGud/Content/Resources/Gameplay/WeaponRigs/LauncherWeaponRig.prefab";
        public const string KnifeRigPath =
            "Assets/GritGud/Content/Resources/Gameplay/WeaponRigs/KnifeWeaponRig.prefab";

        private const string PlayerPrefabPath =
            "Assets/GritGud/Content/Resources/Actors/DefaultPlayerActor.prefab";
        private const string CatalogPath =
            "Assets/GritGud/Content/Resources/Gameplay/WeaponPresentationCatalog.asset";
        private const string RifleSourcePath =
            "Assets/Synty/PolygonBattleRoyale/Prefabs/Weapons/"
            + "SM_Wep_Rifle_Assault_01.prefab";
        private const string LauncherSourcePath =
            "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/"
            + "Human Soldier Animations/Prefabs/Weapons/Human_Bazooka.prefab";
        private const string KnifeSourcePath =
            "Assets/Synty/PolygonBattleRoyale/Prefabs/Weapons/SM_Wep_Machete_01.prefab";
        private const string RifleMountClipPath =
            "Assets/Basic Shooter Pack/rifle aiming idle.fbx";
        private const float CalibrationMuzzleElevationDegrees = 15f;
        private const float CalibrationUpOffsetMeters = 0.35f;
        private static readonly Quaternion RiflePrimaryGripRotation =
            new(-0.002023f, -0.027522f, -0.066818f, 0.997384f);
        private static readonly SupportContactCalibration RifleSupportContact =
            new(
                new Vector3(-0.05839497f, 0.03974687f, 0.1901556f),
                new Quaternion(
                    0.36325172f,
                    -0.8738143f,
                    -0.30538845f,
                    -0.10599399f));

        public static void EnsureGenerated()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RifleRigPath) == null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(LauncherRigPath) == null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(KnifeRigPath) == null ||
                AssetDatabase.LoadAssetAtPath<WeaponPresentationCatalog>(
                    CatalogPath) == null)
            {
                Generate();
                return;
            }

            Validate();
        }

        [MenuItem("Grit Gud/Weapons/Regenerate Authored Weapon Rigs")]
        public static void Generate()
        {
            EnsureFolder(
                "Assets/GritGud/Content/Resources/Gameplay/WeaponRigs");

            GameObject rifle = GenerateRig(
                "Rifle Weapon Rig",
                RifleRigPath,
                RifleSourcePath,
                new Vector3(-0.008f, 0.035f, -0.002f),
                Quaternion.Euler(0f, 90f, 0f)
                    * Quaternion.AngleAxis(
                        90f,
                        new Vector3(0f, 0.96f, -0.09f).normalized),
                new Vector3(0.002f, 0.089f, 0.54f),
                RifleMountClipPath,
                RiflePrimaryGripRotation,
                RifleSupportContact,
                barrelAxisLocal: Vector3.forward,
                clockwiseBarrelRollDegrees: 90f,
                animationSetId: ActorAnimationPoseIds.Rifle,
                topViewCounterclockwiseYawDegrees: 25f,
                topViewCounterclockwisePitchDegrees: 45f,
                supportHand: true,
                supportHintWeight: 0.55f);
            GameObject launcher = GenerateRig(
                "Launcher Weapon Rig",
                LauncherRigPath,
                LauncherSourcePath,
                new Vector3(0.007f, 0.012f, 0f),
                Quaternion.Euler(0f, 90f, 0f),
                new Vector3(0f, 0f, -0.15f),
                mountClipPath: null,
                primaryGripLocalRotation: Quaternion.identity,
                supportContact: null,
                barrelAxisLocal: Vector3.back,
                clockwiseBarrelRollDegrees: 0f,
                animationSetId: ActorAnimationPoseIds.Launcher,
                topViewCounterclockwiseYawDegrees: 0f,
                topViewCounterclockwisePitchDegrees: 0f,
                supportHand: true,
                supportHintWeight: 0.45f);
            GameObject knife = GenerateRig(
                "Knife Weapon Rig",
                KnifeRigPath,
                KnifeSourcePath,
                Vector3.zero,
                Quaternion.identity,
                new Vector3(0f, 0f, 0.7f),
                mountClipPath: null,
                primaryGripLocalRotation: Quaternion.identity,
                supportContact: null,
                barrelAxisLocal: Vector3.forward,
                clockwiseBarrelRollDegrees: 0f,
                animationSetId: ActorAnimationPoseIds.Empty,
                topViewCounterclockwiseYawDegrees: 0f,
                topViewCounterclockwisePitchDegrees: 0f,
                supportHand: false,
                supportHintWeight: 0f);

            UpdateCatalog(rifle, launcher, knife);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log(
                "Generated authored weapon rigs and updated the weapon presentation catalog.");
        }

        [MenuItem("Grit Gud/Weapons/Regenerate Synty Rifle Rig")]
        public static void RegenerateRifle()
        {
            EnsureFolder(
                "Assets/GritGud/Content/Resources/Gameplay/WeaponRigs");
            GameObject rifle = GenerateRig(
                "Rifle Weapon Rig",
                RifleRigPath,
                RifleSourcePath,
                new Vector3(-0.008f, 0.035f, -0.002f),
                Quaternion.Euler(0f, 90f, 0f)
                    * Quaternion.AngleAxis(
                        90f,
                        new Vector3(0f, 0.96f, -0.09f).normalized),
                new Vector3(0.002f, 0.089f, 0.54f),
                RifleMountClipPath,
                RiflePrimaryGripRotation,
                RifleSupportContact,
                barrelAxisLocal: Vector3.forward,
                clockwiseBarrelRollDegrees: 90f,
                animationSetId: ActorAnimationPoseIds.Rifle,
                topViewCounterclockwiseYawDegrees: 25f,
                topViewCounterclockwisePitchDegrees: 45f,
                supportHand: true,
                supportHintWeight: 0.55f);
            GameObject launcher = AssetDatabase.LoadAssetAtPath<GameObject>(
                LauncherRigPath);
            GameObject knife = AssetDatabase.LoadAssetAtPath<GameObject>(
                KnifeRigPath);
            UpdateCatalog(rifle, launcher, knife);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("Regenerated the Synty rifle rig.");
        }

        [MenuItem("Grit Gud/Weapons/Replace Rifle With Calibration Cylinder")]
        public static void GenerateRifleCalibrationRig()
        {
            EnsureFolder(
                "Assets/GritGud/Content/Resources/Gameplay/WeaponRigs");
            GameObject rifle = GenerateCalibrationRifleRig();
            GameObject launcher = AssetDatabase.LoadAssetAtPath<GameObject>(
                LauncherRigPath);
            GameObject knife = AssetDatabase.LoadAssetAtPath<GameObject>(
                KnifeRigPath);
            if (launcher == null || knife == null)
            {
                throw new InvalidOperationException(
                    "Generate the authored weapon rigs before replacing only "
                    + "the rifle with the calibration cylinder.");
            }

            UpdateCatalog(rifle, launcher, knife);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log(
                "Replaced the rifle visual with the calibration cylinder.");
        }

        [MenuItem("Grit Gud/Weapons/Validate Authored Weapon Rigs")]
        public static void Validate()
        {
            ValidateRig(RifleRigPath, requiresSupportHand: true);
            ValidateRig(LauncherRigPath, requiresSupportHand: true);
            ValidateRig(KnifeRigPath, requiresSupportHand: false);

            WeaponPresentationCatalog catalog =
                AssetDatabase.LoadAssetAtPath<WeaponPresentationCatalog>(
                    CatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"Weapon presentation catalog is missing at '{CatalogPath}'.");
            }

            ValidateCatalogEntry(
                catalog,
                "weapon.rifle",
                RifleRigPath,
                ActorAnimationPoseIds.Rifle);
            ValidateCatalogEntry(
                catalog,
                "weapon.rocket-launcher",
                LauncherRigPath,
                ActorAnimationPoseIds.Launcher);
            ValidateCatalogEntry(
                catalog,
                "weapon.combat-knife",
                KnifeRigPath,
                ActorAnimationPoseIds.Melee);
        }

        private static GameObject GenerateRig(
            string rigName,
            string destinationPath,
            string sourcePath,
            Vector3 visualLocalPosition,
            Quaternion visualLocalRotation,
            Vector3 muzzleVisualLocalPosition,
            string mountClipPath,
            Quaternion primaryGripLocalRotation,
            SupportContactCalibration? supportContact,
            Vector3 barrelAxisLocal,
            float clockwiseBarrelRollDegrees,
            string animationSetId,
            float topViewCounterclockwiseYawDegrees,
            float topViewCounterclockwisePitchDegrees,
            bool supportHand,
            float supportHintWeight)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
                sourcePath);
            if (source == null)
            {
                throw new InvalidOperationException(
                    $"Weapon source prefab is missing at '{sourcePath}'.");
            }

            var root = new GameObject(rigName);
            try
            {
                WeaponRigSocketSet sockets =
                    root.AddComponent<WeaponRigSocketSet>();
                GameObject visual = PrefabUtility.InstantiatePrefab(source)
                    as GameObject;
                if (visual == null)
                {
                    throw new InvalidOperationException(
                        $"Could not instantiate weapon source '{sourcePath}'.");
                }

                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.SetLocalPositionAndRotation(
                    visualLocalPosition,
                    visualLocalRotation);
                visual.transform.localScale = Vector3.one;
                bool mountCalibrated = TryCalibrateVisualOrientation(
                    root.transform,
                    visual.transform,
                    mountClipPath,
                    barrelAxisLocal,
                    clockwiseBarrelRollDegrees);
                if (!string.IsNullOrWhiteSpace(mountClipPath) &&
                    !mountCalibrated)
                {
                    throw new InvalidOperationException(
                        $"Could not calibrate weapon mount from '{mountClipPath}'.");
                }

                Transform muzzle = CreateSocket(root.transform, "Muzzle");
                Transform rightGrip = CreateSocket(root.transform, "Right Hand Grip");
                rightGrip.localRotation = primaryGripLocalRotation;
                muzzle.position = visual.transform.TransformPoint(
                    muzzleVisualLocalPosition);
                muzzle.rotation = Quaternion.LookRotation(
                    visual.transform.TransformDirection(
                        barrelAxisLocal.normalized),
                    visual.transform.up);

                Transform hand = null;
                Transform hint = null;
                if (supportHand)
                {
                    hand = CreateSocket(root.transform, "Support Hand");
                    hint = CreateSocket(root.transform, "Support Elbow Hint");
                }

                bool yawCalibrated = TryApplyRuntimePoseYaw(
                    root.transform,
                    visual.transform,
                    muzzle,
                    hand,
                    hint,
                    barrelAxisLocal,
                    animationSetId,
                    topViewCounterclockwiseYawDegrees);
                if (!yawCalibrated)
                {
                    throw new InvalidOperationException(
                        $"Could not apply the {topViewCounterclockwiseYawDegrees:0.###}-degree "
                        + $"top-view yaw in the '{animationSetId}' runtime pose.");
                }

                bool pitchCalibrated = TryApplyRuntimePosePitch(
                    root.transform,
                    visual.transform,
                    muzzle,
                    hand,
                    hint,
                    barrelAxisLocal,
                    animationSetId,
                    topViewCounterclockwisePitchDegrees);
                if (!pitchCalibrated)
                {
                    throw new InvalidOperationException(
                        $"Could not apply the {topViewCounterclockwisePitchDegrees:0.###}-degree "
                        + $"top-view pitch in the '{animationSetId}' runtime pose.");
                }

                bool supportCalibrated = !supportHand
                    || (supportContact.HasValue
                        ? ApplyAuthoredSupportContact(
                            visual.transform,
                            hand,
                            supportContact.Value)
                            && TryCalibrateRuntimeElbowHint(
                                root.transform,
                                rightGrip,
                                hint,
                                animationSetId)
                        : TryCalibrateRuntimeSupportPose(
                            root.transform,
                            hand,
                            hint,
                            animationSetId));
                if (!supportCalibrated)
                {
                    throw new InvalidOperationException(
                        "Could not calibrate the support-hand socket from "
                        + $"the '{animationSetId}' runtime animation set.");
                }

                ConfigureSockets(
                    sockets,
                    visual.transform,
                    muzzle,
                    rightGrip,
                    hand,
                    hint,
                    supportHintWeight);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    destinationPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Could not save weapon rig '{destinationPath}'.");
                }

                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject GenerateCalibrationRifleRig()
        {
            var root = new GameObject("Rifle Calibration Rig");
            try
            {
                WeaponRigSocketSet sockets =
                    root.AddComponent<WeaponRigSocketSet>();
                GameObject barrel = GameObject.CreatePrimitive(
                    PrimitiveType.Cylinder);
                barrel.name = "Calibration Barrel";
                Object.DestroyImmediate(barrel.GetComponent<Collider>());
                barrel.transform.SetParent(root.transform, false);
                barrel.transform.SetLocalPositionAndRotation(
                    new Vector3(0f, 0f, 0.36f),
                    Quaternion.Euler(90f, 0f, 0f));
                barrel.transform.localScale = new Vector3(
                    0.045f,
                    0.38f,
                    0.045f);

                GameObject gripMarker = GameObject.CreatePrimitive(
                    PrimitiveType.Sphere);
                gripMarker.name = "Grip Marker";
                Object.DestroyImmediate(gripMarker.GetComponent<Collider>());
                gripMarker.transform.SetParent(root.transform, false);
                gripMarker.transform.localScale = Vector3.one * 0.07f;

                Transform muzzle = CreateSocket(root.transform, "Muzzle");
                Transform rightGrip = CreateSocket(root.transform, "Right Hand Grip");
                Transform support = CreateSocket(
                    root.transform,
                    "Support Hand");
                Transform hint = CreateSocket(root.transform, "Support Elbow Hint");
                if (!TryCalibrateCylinderAgainstRiflePose(
                        root.transform,
                        barrel.transform,
                        muzzle,
                        support,
                        hint))
                {
                    throw new InvalidOperationException(
                        "Could not calibrate the rifle cylinder against the "
                        + "Synty rifle-hand animation pose.");
                }
                ConfigureSockets(
                    sockets,
                    barrel.transform,
                    muzzle,
                    rightGrip,
                    support,
                    hint,
                    0.55f);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    RifleRigPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        "Could not save the rifle calibration rig.");
                }

                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static bool TryCalibrateCylinderAgainstRiflePose(
            Transform rig,
            Transform barrel,
            Transform muzzle,
            Transform supportHand,
            Transform elbowHint)
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PlayerPrefabPath);
            AnimationClip clip = LoadAnimationClip(RifleMountClipPath);
            if (playerPrefab == null || clip == null)
            {
                return false;
            }

            GameObject player = Object.Instantiate(playerPrefab);
            try
            {
                Animator animator = player.GetComponentInChildren<Animator>(true);
                if (animator == null || !animator.isHuman)
                {
                    return false;
                }

                AnimationMode.StartAnimationMode();
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(
                    animator.gameObject,
                    clip,
                    clip.length * 0.5f);
                AnimationMode.EndSampling();

                Transform rightHand = animator.GetBoneTransform(
                    HumanBodyBones.RightHand);
                Transform leftHand = animator.GetBoneTransform(
                    HumanBodyBones.LeftHand);
                Transform leftLowerArm = animator.GetBoneTransform(
                    HumanBodyBones.LeftLowerArm);
                if (rightHand == null || leftHand == null ||
                    leftLowerArm == null)
                {
                    return false;
                }

                rig.SetParent(rightHand, false);
                rig.SetLocalPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);

                // A Unity cylinder is longitudinal on local Y. Rotate its
                // rendered axis into the actor's sampled facing direction,
                // then retain that calibrated local rotation beneath this
                // skeleton's right-hand coordinate frame.
                barrel.rotation = Quaternion.FromToRotation(
                    barrel.up,
                    player.transform.forward) * barrel.rotation;
                // Preserve the validated ground-plane heading. Add only
                // elevation in the actor's vertical plane, then move the
                // whole rig upward. Neither operation changes top-down yaw.
                Vector3 heading = Vector3.ProjectOnPlane(
                    barrel.up,
                    player.transform.up).normalized;
                Vector3 elevatedAxis = (
                    heading * Mathf.Cos(
                        CalibrationMuzzleElevationDegrees
                        * Mathf.Deg2Rad)
                    + player.transform.up * Mathf.Sin(
                        CalibrationMuzzleElevationDegrees
                        * Mathf.Deg2Rad)).normalized;
                barrel.rotation = Quaternion.FromToRotation(
                    barrel.up,
                    elevatedAxis) * barrel.rotation;
                rig.localPosition += rightHand.InverseTransformVector(
                    player.transform.up * CalibrationUpOffsetMeters);
                muzzle.SetPositionAndRotation(
                    barrel.TransformPoint(new Vector3(0f, 1f, 0f)),
                    barrel.rotation);
                supportHand.SetPositionAndRotation(
                    leftHand.position,
                    leftHand.rotation);
                elbowHint.position = leftLowerArm.position;

                rig.SetParent(null, false);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Could not calibrate the rifle cylinder: "
                    + exception.Message);
                return false;
            }
            finally
            {
                if (AnimationMode.InAnimationMode())
                {
                    AnimationMode.StopAnimationMode();
                }

                Object.DestroyImmediate(player);
            }
        }

        private static bool TryCalibrateRuntimeSupportPose(
            Transform rig,
            Transform supportHand,
            Transform elbowHint,
            string animationSetId)
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PlayerPrefabPath);
            if (playerPrefab == null)
            {
                return false;
            }

            GameObject player = Object.Instantiate(playerPrefab);
            try
            {
                ActorAnimationCoordinator coordinator =
                    player.GetComponent<ActorAnimationCoordinator>();
                Animator animator = coordinator != null
                    ? coordinator.TargetAnimator
                    : null;
                if (animator == null || !animator.isHuman)
                {
                    return false;
                }

                animator.Update(0f);
                coordinator.PresentWeaponPose(animationSetId);
                animator.Update(0.25f);

                Transform rightHand = animator.GetBoneTransform(
                    HumanBodyBones.RightHand);
                Transform leftHand = animator.GetBoneTransform(
                    HumanBodyBones.LeftHand);
                Transform leftLowerArm = animator.GetBoneTransform(
                    HumanBodyBones.LeftLowerArm);
                if (rightHand == null || leftHand == null)
                {
                    return false;
                }

                rig.SetParent(rightHand, false);
                rig.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                supportHand.SetPositionAndRotation(
                    leftHand.position,
                    leftHand.rotation);
                if (leftLowerArm != null)
                {
                    elbowHint.position = leftLowerArm.position;
                }

                rig.SetParent(null, false);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Could not sample the '{animationSetId}' runtime support pose: "
                    + exception.Message);
                return false;
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static bool ApplyAuthoredSupportContact(
            Transform visual,
            Transform supportHand,
            SupportContactCalibration contact)
        {
            if (visual == null || supportHand == null)
            {
                return false;
            }

            supportHand.SetPositionAndRotation(
                visual.TransformPoint(contact.VisualLocalPosition),
                visual.rotation * contact.VisualLocalRotation);
            return true;
        }

        private static bool TryCalibrateRuntimeElbowHint(
            Transform rig,
            Transform rightGrip,
            Transform elbowHint,
            string animationSetId)
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PlayerPrefabPath);
            if (playerPrefab == null || rightGrip == null || elbowHint == null)
            {
                return false;
            }

            Vector3 originalRigPosition = rig.position;
            Quaternion originalRigRotation = rig.rotation;
            GameObject player = Object.Instantiate(playerPrefab);
            try
            {
                ActorAnimationCoordinator coordinator =
                    player.GetComponent<ActorAnimationCoordinator>();
                Animator animator = coordinator != null
                    ? coordinator.TargetAnimator
                    : null;
                if (animator == null || !animator.isHuman)
                {
                    return false;
                }

                animator.Update(0f);
                coordinator.PresentWeaponPose(animationSetId);
                animator.Update(0.25f);
                Transform rightHand = animator.GetBoneTransform(
                    HumanBodyBones.RightHand);
                Transform leftLowerArm = animator.GetBoneTransform(
                    HumanBodyBones.LeftLowerArm);
                if (rightHand == null || leftLowerArm == null)
                {
                    return false;
                }

                Pose gripInRoot = new(
                    rig.InverseTransformPoint(rightGrip.position),
                    Quaternion.Inverse(rig.rotation) * rightGrip.rotation);
                Quaternion rigRotation = rightHand.rotation
                    * Quaternion.Inverse(gripInRoot.rotation);
                rig.SetPositionAndRotation(
                    rightHand.position - rigRotation * gripInRoot.position,
                    rigRotation);
                elbowHint.position = leftLowerArm.position;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Could not calibrate the '{animationSetId}' support elbow hint: "
                    + exception.Message);
                return false;
            }
            finally
            {
                rig.SetPositionAndRotation(
                    originalRigPosition,
                    originalRigRotation);
                Object.DestroyImmediate(player);
            }
        }

        private static bool TryCalibrateVisualOrientation(
            Transform rig,
            Transform visual,
            string clipPath,
            Vector3 barrelAxisLocal,
            float clockwiseBarrelRollDegrees)
        {
            if (string.IsNullOrWhiteSpace(clipPath))
            {
                return false;
            }

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PlayerPrefabPath);
            AnimationClip clip = LoadAnimationClip(clipPath);
            if (playerPrefab == null || clip == null ||
                barrelAxisLocal.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            GameObject player = Object.Instantiate(playerPrefab);
            try
            {
                Animator animator = player.GetComponentInChildren<Animator>(true);
                if (animator == null || !animator.isHuman)
                {
                    return false;
                }

                AnimationMode.StartAnimationMode();
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(
                    animator.gameObject,
                    clip,
                    clip.length * 0.5f);
                AnimationMode.EndSampling();

                Transform rightHand = animator.GetBoneTransform(
                    HumanBodyBones.RightHand);
                if (rightHand == null)
                {
                    return false;
                }

                rig.SetParent(rightHand, false);
                rig.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                Vector3 currentBarrel = visual.TransformDirection(
                    barrelAxisLocal.normalized);
                Vector3 desiredBarrel = player.transform.forward;
                Quaternion roll = Quaternion.AngleAxis(
                    -clockwiseBarrelRollDegrees,
                    currentBarrel);
                Quaternion rolled = roll * visual.rotation;
                Quaternion barrelAlignment = Quaternion.FromToRotation(
                    currentBarrel,
                    desiredBarrel);
                visual.rotation = barrelAlignment * rolled;
                rig.SetParent(null, false);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Could not calibrate weapon mount from '{clipPath}': "
                    + exception.Message);
                return false;
            }
            finally
            {
                if (AnimationMode.InAnimationMode())
                {
                    AnimationMode.StopAnimationMode();
                }

                Object.DestroyImmediate(player);
            }
        }

        private static bool TryApplyRuntimePoseYaw(
            Transform rig,
            Transform visual,
            Transform muzzle,
            Transform supportHand,
            Transform elbowHint,
            Vector3 barrelAxisLocal,
            string animationSetId,
            float topViewCounterclockwiseYawDegrees,
            bool rotateAroundActorRight = false)
        {
            if (Mathf.Abs(topViewCounterclockwiseYawDegrees) <= 0.0001f)
            {
                return true;
            }

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PlayerPrefabPath);
            if (playerPrefab == null ||
                barrelAxisLocal.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            GameObject player = Object.Instantiate(playerPrefab);
            bool mounted = false;
            try
            {
                ActorAnimationCoordinator coordinator =
                    player.GetComponent<ActorAnimationCoordinator>();
                Animator animator = coordinator != null
                    ? coordinator.TargetAnimator
                    : null;
                if (animator == null || !animator.isHuman)
                {
                    return false;
                }

                animator.Update(0f);
                coordinator.PresentWeaponPose(animationSetId);
                animator.Update(0.25f);
                Transform rightHand = animator.GetBoneTransform(
                    HumanBodyBones.RightHand);
                if (rightHand == null)
                {
                    return false;
                }

                rig.SetParent(rightHand, false);
                rig.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                mounted = true;
                Vector3 axis = rotateAroundActorRight
                    ? player.transform.right
                    : player.transform.up;
                Vector3 barrelBefore = visual.TransformDirection(
                    barrelAxisLocal.normalized);
                Vector3 horizontalBefore = Vector3.ProjectOnPlane(
                    barrelBefore,
                    axis);
                if (horizontalBefore.sqrMagnitude <= 0.0001f)
                {
                    return false;
                }

                Quaternion topViewYaw = Quaternion.AngleAxis(
                    -topViewCounterclockwiseYawDegrees,
                    axis);
                Vector3 visualPivot = visual.position;
                visual.rotation = topViewYaw * visual.rotation;
                RotateSocketAround(
                    muzzle,
                    visualPivot,
                    topViewYaw);
                RotateSocketAround(
                    supportHand,
                    visualPivot,
                    topViewYaw);
                RotateSocketAround(
                    elbowHint,
                    visualPivot,
                    topViewYaw);
                Vector3 horizontalAfter = Vector3.ProjectOnPlane(
                    visual.TransformDirection(barrelAxisLocal.normalized),
                    axis);
                float appliedYaw = Vector3.SignedAngle(
                    horizontalBefore,
                    horizontalAfter,
                    axis);
                if (Mathf.Abs(
                        appliedYaw + topViewCounterclockwiseYawDegrees) > 0.05f)
                {
                    return false;
                }

                rig.SetParent(null, false);
                mounted = false;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Could not calibrate runtime weapon yaw for '{animationSetId}': "
                    + exception.Message);
                return false;
            }
            finally
            {
                if (mounted)
                {
                    rig.SetParent(null, false);
                }

                Object.DestroyImmediate(player);
            }
        }

        private static bool TryApplyRuntimePosePitch(
            Transform rig,
            Transform visual,
            Transform muzzle,
            Transform supportHand,
            Transform elbowHint,
            Vector3 barrelAxisLocal,
            string animationSetId,
            float topViewCounterclockwisePitchDegrees)
        {
            return TryApplyRuntimePoseYaw(
                rig,
                visual,
                muzzle,
                supportHand,
                elbowHint,
                barrelAxisLocal,
                animationSetId,
                topViewCounterclockwisePitchDegrees,
                rotateAroundActorRight: true);
        }

        private static void RotateSocketAround(
            Transform socket,
            Vector3 pivot,
            Quaternion rotation)
        {
            if (socket == null)
            {
                return;
            }

            socket.position = pivot + rotation * (socket.position - pivot);
            socket.rotation = rotation * socket.rotation;
        }

        private static void ConfigureSockets(
            WeaponRigSocketSet sockets,
            Transform visual,
            Transform muzzle,
            Transform rightGrip,
            Transform supportHand,
            Transform elbowHint,
            float hintWeight)
        {
            var serialized = new SerializedObject(sockets);
            serialized.FindProperty("visualRoot").objectReferenceValue = visual;
            serialized.FindProperty("muzzle").objectReferenceValue = muzzle;
            serialized.FindProperty("rightHandGrip").objectReferenceValue =
                rightGrip;
            serialized.FindProperty("supportHand").objectReferenceValue =
                supportHand;
            serialized.FindProperty("supportElbowHint").objectReferenceValue =
                elbowHint;
            serialized.FindProperty("supportPositionWeight").floatValue =
                supportHand != null ? 1f : 0f;
            serialized.FindProperty("supportRotationWeight").floatValue =
                supportHand != null ? 1f : 0f;
            serialized.FindProperty("supportElbowHintWeight").floatValue =
                hintWeight;
            serialized.FindProperty("supportBlendSeconds").floatValue = 0.12f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpdateCatalog(
            GameObject rifle,
            GameObject launcher,
            GameObject knife)
        {
            WeaponPresentationCatalog catalog =
                AssetDatabase.LoadAssetAtPath<WeaponPresentationCatalog>(
                    CatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"Weapon presentation catalog is missing at '{CatalogPath}'.");
            }

            var serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("entries");
            SetRig(
                entries,
                "weapon.rifle",
                rifle,
                ActorAnimationPoseIds.Rifle);
            SetRig(
                entries,
                "weapon.rocket-launcher",
                launcher,
                ActorAnimationPoseIds.Launcher);
            SetRig(
                entries,
                "weapon.combat-knife",
                knife,
                ActorAnimationPoseIds.Melee);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void SetRig(
            SerializedProperty entries,
            string itemId,
            GameObject rig,
            string animationSetId)
        {
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                if (entry.FindPropertyRelative("itemId").stringValue == itemId)
                {
                    entry.FindPropertyRelative("prefab").objectReferenceValue = rig;
                    entry.FindPropertyRelative("animationSetId").stringValue =
                        animationSetId;
                    return;
                }
            }

            throw new InvalidOperationException(
                $"Weapon presentation catalog entry '{itemId}' is missing.");
        }

        private static void ValidateRig(
            string path,
            bool requiresSupportHand)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            WeaponRigSocketSet sockets =
                prefab != null ? prefab.GetComponent<WeaponRigSocketSet>() : null;
            if (sockets == null)
            {
                throw new InvalidOperationException(
                    $"Weapon rig is missing its socket contract at '{path}'.");
            }

            sockets.Validate(path);
            if (requiresSupportHand &&
                (sockets.SupportHand == null ||
                 sockets.SupportElbowHint == null))
            {
                throw new InvalidOperationException(
                    $"Two-handed weapon rig '{path}' requires its support-hand "
                    + "and elbow sockets.");
            }
        }

        private static void ValidateCatalogEntry(
            WeaponPresentationCatalog catalog,
            string itemId,
            string expectedPath,
            string expectedAnimationSetId)
        {
            WeaponPresentationDefinition definition = catalog.Get(itemId);
            if (AssetDatabase.GetAssetPath(definition.Prefab) != expectedPath ||
                definition.AnimationSetId != expectedAnimationSetId)
            {
                throw new InvalidOperationException(
                    $"Weapon presentation '{itemId}' must use '{expectedPath}' "
                    + $"and animation set '{expectedAnimationSetId}'.");
            }
        }

        private static Transform CreateSocket(Transform parent, string name)
        {
            var socket = new GameObject(name).transform;
            socket.SetParent(parent, false);
            return socket;
        }

        private readonly struct SupportContactCalibration
        {
            public SupportContactCalibration(
                Vector3 visualLocalPosition,
                Quaternion visualLocalRotation)
            {
                VisualLocalPosition = visualLocalPosition;
                VisualLocalRotation = visualLocalRotation;
            }

            public Vector3 VisualLocalPosition { get; }

            public Quaternion VisualLocalRotation { get; }
        }

        private static AnimationClip LoadAnimationClip(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip &&
                    !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                {
                    return clip;
                }
            }

            return null;
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }
    }
}
