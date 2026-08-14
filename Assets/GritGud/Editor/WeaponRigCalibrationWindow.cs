using GritGud.Presentation.Gameplay;
using UnityEditor;
using UnityEngine;

namespace GritGud.Editor
{
    /// <summary>
    /// A deliberately small bake tool: set up a side/top calibration scene,
    /// parent the weapon instance to the intended upper-body anchor, position
    /// it visually, then bake that exact relative pose into its prefab.
    /// </summary>
    public sealed class WeaponRigCalibrationWindow : EditorWindow
    {
        private Transform anchor;
        private WeaponRigSocketSet rig;

        [MenuItem("Grit Gud/Weapons/Calibrate Weapon Pose")]
        private static void Open() => GetWindow<WeaponRigCalibrationWindow>(
            "Weapon Calibration");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "In a side/top calibration scene, parent the weapon rig to an "
                + "upper-body anchor and place it once. Put both grip sockets at "
                + "the palms and align Muzzle.forward with the barrel. Bake stores "
                + "the exact anchor-relative pose; it never infers local axes.",
                MessageType.Info);
            anchor = (Transform)EditorGUILayout.ObjectField(
                "Upper-Body Anchor", anchor, typeof(Transform), true);
            rig = (WeaponRigSocketSet)EditorGUILayout.ObjectField(
                "Weapon Rig Instance", rig, typeof(WeaponRigSocketSet), true);

            using (new EditorGUI.DisabledScope(anchor == null || rig == null))
            {
                if (GUILayout.Button("Bake Current Pose Into Prefab"))
                {
                    Bake();
                }
            }
        }

        private void Bake()
        {
            if (!rig.transform.IsChildOf(anchor))
            {
                Debug.LogError(
                    "Weapon rig must be parented beneath the selected upper-body anchor before baking.",
                    rig);
                return;
            }

            Vector3 localPosition = anchor.InverseTransformPoint(
                rig.transform.position);
            Quaternion localRotation = Quaternion.Inverse(anchor.rotation)
                * rig.transform.rotation;
            Undo.RecordObject(rig, "Bake Weapon Anchor Pose");
            rig.SetAnchorCalibration(localPosition, localRotation);
            EditorUtility.SetDirty(rig);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig);
            PrefabUtility.ApplyPrefabInstance(rig.gameObject,
                InteractionMode.UserAction);
            Debug.Log($"Baked weapon anchor pose for '{rig.name}'.", rig);
        }
    }
}
