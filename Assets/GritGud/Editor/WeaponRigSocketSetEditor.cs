using GritGud.Presentation.Gameplay;
using UnityEditor;
using UnityEngine;

namespace GritGud.Editor
{
    [CustomEditor(typeof(WeaponRigSocketSet))]
    public sealed class WeaponRigSocketSetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var sockets = (WeaponRigSocketSet)target;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "The weapon is mounted to an upper-body anchor. Place both grip "
                + "sockets exactly at their palms in the calibration pose; runtime "
                + "Two Bone IK consumes those world poses directly. The muzzle "
                + "forward axis is the sole firing and VFX direction.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSelectButton("Right Grip", sockets.RightHandGrip);
                DrawSelectButton("Left Grip", sockets.SupportHand);
                DrawSelectButton("Muzzle", sockets.Muzzle);
                DrawSelectButton("Elbow Hint", sockets.SupportElbowHint);
            }

            if (GUILayout.Button("Validate Rig Sockets"))
            {
                sockets.Validate(AssetDatabase.GetAssetPath(sockets.gameObject));
                Debug.Log($"Weapon rig '{sockets.name}' sockets are valid.", sockets);
            }

            if (sockets.transform.parent != null
                && sockets.transform.parent.name == "Weapon Anchor")
            {
                if (GUILayout.Button("Preview Support Hand"))
                {
                    LiveWeaponCalibrationPreview preview =
                        Object.FindFirstObjectByType<LiveWeaponCalibrationPreview>(
                            FindObjectsInactive.Include);
                    if (preview == null)
                    {
                        Debug.LogError(
                            "The calibration scene has no support-hand preview.",
                            sockets);
                    }
                    else
                    {
                        Undo.RecordObject(preview.transform,
                            "Preview Support Hand");
                        preview.PreviewNow();
                        SceneView.RepaintAll();
                    }
                }

                if (GUILayout.Button("Bake Selected Synty Rifle Calibration"))
                {
                    Selection.activeGameObject = sockets.gameObject;
                    SyntyRifleCalibrationScene.BakeSelected();
                }
            }
        }

        private static void DrawSelectButton(string label, Transform socket)
        {
            using (new EditorGUI.DisabledScope(socket == null))
            {
                if (GUILayout.Button(label))
                {
                    Selection.activeTransform = socket;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
            }
        }

    }
}
