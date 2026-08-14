using System;
using System.Collections.Generic;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GritGud.Editor
{
    /// <summary>
    /// Builds a frozen copy of the real player in its actual rifle pose.  This
    /// is the one place where a Synty prop is positioned by eye; it is then
    /// baked to the production weapon prefab.
    /// </summary>
    public static class SyntyRifleCalibrationScene
    {
        private const float RootPositionTolerance = 0.0001f;
        private const float RootRotationToleranceDegrees = 0.05f;
        private const string ScenePath =
            "Assets/GritGud/Content/Scenes/SyntyRifleCalibration.unity";
        private const string PlayerPrefabPath =
            "Assets/GritGud/Content/Resources/Actors/DefaultPlayerActor.prefab";
        private const string RifleRigPath =
            "Assets/GritGud/Content/Resources/Gameplay/WeaponRigs/RifleWeaponRig.prefab";

        [MenuItem("Grit Gud/Weapons/Create Synty Rifle Calibration Scene")]
        public static void Create()
        {
            EnsureFolder("Assets/GritGud/Content/Scenes");
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PlayerPrefabPath);
            GameObject riflePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                RifleRigPath);
            if (playerPrefab == null || riflePrefab == null)
            {
                throw new InvalidOperationException(
                    "The default player and rifle rig must exist before creating the calibration scene.");
            }

            GameObject player = PrefabUtility.InstantiatePrefab(playerPrefab)
                as GameObject;
            player.name = "Calibration Player (Frozen Rifle Pose)";
            ActorAnimationCoordinator coordinator =
                player.GetComponent<ActorAnimationCoordinator>();
            Animator animator = coordinator?.TargetAnimator;
            if (animator == null || !animator.isHuman)
            {
                throw new InvalidOperationException(
                    "The calibration player requires a humanoid Animator.");
            }

            FreezeRifleAimPose(coordinator);
            animator.enabled = false;

            Transform chest = animator.GetBoneTransform(HumanBodyBones.UpperChest)
                ?? animator.GetBoneTransform(HumanBodyBones.Chest);
            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform leftUpperArm = animator.GetBoneTransform(
                HumanBodyBones.LeftUpperArm);
            Transform leftLowerArm = animator.GetBoneTransform(
                HumanBodyBones.LeftLowerArm);
            if (chest == null || rightHand == null || leftHand == null
                || leftUpperArm == null || leftLowerArm == null)
            {
                throw new InvalidOperationException(
                    "The calibration player is missing chest or hand bones.");
            }

            Transform anchor = CreateReference(chest, "Weapon Anchor");
            anchor.SetPositionAndRotation(rightHand.position, rightHand.rotation);
            CreateReference(rightHand, "Right Wrist Reference");
            CreateReference(leftHand, "Left Palm Reference");
            Transform elbowHint = CreateReference(
                player.transform,
                "Calibration Left Elbow Hint");
            elbowHint.position = leftLowerArm.position + player.transform.forward
                * 0.25f;

            GameObject rifle = PrefabUtility.InstantiatePrefab(riflePrefab)
                as GameObject;
            rifle.name = "Calibrate This Synty Rifle";
            rifle.transform.SetParent(anchor, false);
            rifle.transform.SetLocalPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);

            WeaponRigSocketSet sockets =
                rifle.GetComponent<WeaponRigSocketSet>();
            if (sockets == null)
            {
                throw new InvalidOperationException(
                    "The rifle prefab requires WeaponRigSocketSet.");
            }

            sockets.SupportHand.SetPositionAndRotation(
                leftHand.position,
                leftHand.rotation);
            PlaceMuzzleAtNativeForwardExtent(sockets);
            LiveWeaponCalibrationPreview preview =
                player.AddComponent<LiveWeaponCalibrationPreview>();
            preview.Configure(
                leftUpperArm,
                leftLowerArm,
                leftHand,
                sockets.SupportHand,
                elbowHint);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = rifle;
            EditorGUIUtility.PingObject(rifle);
            Debug.Log(
                "Created Synty rifle calibration scene. Position the Visual and "
                + "its sockets against the frozen hands, then use "
                + "'Grit Gud/Weapons/Bake Selected Synty Rifle Calibration'.",
                rifle);
        }

        [MenuItem("Grit Gud/Weapons/Bake Selected Synty Rifle Calibration")]
        public static void BakeSelected()
        {
            WeaponRigSocketSet sockets = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<WeaponRigSocketSet>()
                : null;
            if (sockets == null)
            {
                throw new InvalidOperationException(
                    "Select 'Calibrate This Synty Rifle' before baking.");
            }

            Transform anchor = sockets.transform.parent;
            if (anchor == null || anchor.name != "Weapon Anchor")
            {
                throw new InvalidOperationException(
                    "The selected rifle must be parented to Weapon Anchor.");
            }

            if (sockets.transform.localPosition.sqrMagnitude >
                    RootPositionTolerance * RootPositionTolerance ||
                Quaternion.Angle(
                    sockets.transform.localRotation,
                    Quaternion.identity) > RootRotationToleranceDegrees)
            {
                throw new InvalidOperationException(
                    "Keep the rifle rig root at the Weapon Anchor origin. "
                    + "Calibrate the Visual and socket children instead; "
                    + "runtime resolves the root from Right Hand Grip.");
            }

            Undo.RecordObject(sockets, "Bake Synty Rifle Calibration");
            sockets.SetAnchorCalibration(
                anchor.InverseTransformPoint(sockets.transform.position),
                Quaternion.Inverse(anchor.rotation) * sockets.transform.rotation);
            EditorUtility.SetDirty(sockets);
            var serializedSockets = new SerializedObject(sockets);
            ApplyOverride(
                serializedSockets.FindProperty("anchorLocalPosition"),
                RifleRigPath);
            ApplyOverride(
                serializedSockets.FindProperty("anchorLocalEulerAngles"),
                RifleRigPath);
            ApplyTransformPose(sockets.VisualRoot);
            ApplyTransformPose(sockets.RightHandGrip);
            ApplyTransformPose(sockets.SupportHand);
            ApplyTransformPose(sockets.SupportElbowHint);
            ApplyTransformPose(sockets.Muzzle);
            AssetDatabase.SaveAssets();
            Debug.Log("Baked Synty rifle calibration into RifleWeaponRig.", sockets);
        }

        private static void ApplyTransformPose(Transform transform)
        {
            if (transform == null)
            {
                return;
            }

            var serializedTransform = new SerializedObject(transform);
            ApplyOverride(
                serializedTransform.FindProperty("m_LocalPosition"),
                RifleRigPath);
            ApplyOverride(
                serializedTransform.FindProperty("m_LocalRotation"),
                RifleRigPath);
            serializedTransform.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyOverride(
            SerializedProperty property,
            string destinationPrefabPath)
        {
            if (property != null
                && PrefabUtility.IsPartOfPrefabInstance(property.serializedObject.targetObject))
            {
                PrefabUtility.ApplyPropertyOverride(
                    property,
                    destinationPrefabPath,
                    InteractionMode.UserAction);
            }
        }

        internal static void RefreshOpenCalibrationPose()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded)
            {
                return;
            }

            GameObject player = Array.Find(
                scene.GetRootGameObjects(),
                root => root.name == "Calibration Player (Frozen Rifle Pose)");
            if (player == null)
            {
                return;
            }

            ActorAnimationCoordinator coordinator =
                player.GetComponent<ActorAnimationCoordinator>();
            Animator animator = coordinator?.TargetAnimator;
            if (animator == null || !animator.isHuman)
            {
                return;
            }

            animator.enabled = true;
            FreezeRifleAimPose(coordinator);
            animator.enabled = false;

            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform chest = animator.GetBoneTransform(HumanBodyBones.UpperChest)
                ?? animator.GetBoneTransform(HumanBodyBones.Chest);
            Transform anchor = chest?.Find("Weapon Anchor");
            if (rightHand != null && anchor != null)
            {
                // Keep the user's rifle offsets beneath the anchor intact,
                // while moving that whole calibrated setup to the corrected
                // retargeted right-hand pose.
                anchor.SetPositionAndRotation(rightHand.position, rightHand.rotation);
            }

            WeaponRigSocketSet sockets = player.GetComponentInChildren<
                WeaponRigSocketSet>(true);
            SyncVisualFromPrefab(sockets);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(
                "Refreshed the open frozen rifle calibration pose after humanoid import. "
                + "Existing rifle offsets were preserved.",
                player);
        }

        private static void SyncVisualFromPrefab(WeaponRigSocketSet sockets)
        {
            if (sockets?.VisualRoot == null)
            {
                return;
            }

            Transform sourceVisual =
                PrefabUtility.GetCorrespondingObjectFromSource(sockets.VisualRoot);
            if (sourceVisual == null)
            {
                return;
            }

            sockets.VisualRoot.SetLocalPositionAndRotation(
                sourceVisual.localPosition,
                sourceVisual.localRotation);
        }

        private static Transform CreateReference(Transform parent, string name)
        {
            var reference = new GameObject(name).transform;
            reference.SetParent(parent, false);
            return reference;
        }

        private static void FreezeRifleAimPose(
            ActorAnimationCoordinator coordinator)
        {
            Animator animator = coordinator?.TargetAnimator;
            if (animator == null || !animator.isHuman)
            {
                throw new InvalidOperationException(
                    "The calibration player requires a humanoid Animator.");
            }

            animator.Rebind();
            animator.Update(0f);
            coordinator.PresentWeaponPose(ActorAnimationPoseIds.Rifle);
            animator.Update(0.25f);
        }

        private static void PlaceMuzzleAtNativeForwardExtent(
            WeaponRigSocketSet sockets)
        {
            Renderer[] renderers = sockets.VisualRoot.GetComponentsInChildren<Renderer>(
                true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            Vector3 forward = sockets.VisualRoot.forward;
            Vector3 extent = bounds.extents;
            float distance = Mathf.Abs(forward.x) * extent.x
                + Mathf.Abs(forward.y) * extent.y
                + Mathf.Abs(forward.z) * extent.z;
            sockets.Muzzle.SetPositionAndRotation(
                bounds.center + forward * distance,
                Quaternion.LookRotation(forward, sockets.VisualRoot.up));
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
