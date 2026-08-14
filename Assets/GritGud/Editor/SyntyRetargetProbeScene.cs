using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GritGud.Editor
{
    /// <summary>
    /// Creates a deliberately bare retargeting check: a Synty character plus
    /// one Basic Shooter Pack humanoid clip. No controller, weapon, rigging,
    /// or gameplay component participates in the sampled result.
    /// </summary>
    public static class SyntyRetargetProbeScene
    {
        private const string ScenePath =
            "Assets/GritGud/Content/Scenes/SyntyRetargetProbe.unity";
        private const string CharacterPrefabPath =
            "Assets/Synty/PolygonBattleRoyale/Prefabs/Characters/Character_MilitaryMale_01.prefab";
        private const string RifleAimClipPath =
            "Assets/Basic Shooter Pack/rifle aiming idle.fbx";

        [MenuItem("Grit Gud/Diagnostics/Create Synty Retarget Probe")]
        public static void Create()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            GameObject characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                CharacterPrefabPath);
            AnimationClip rifleAim = LoadAnimationClip(RifleAimClipPath);
            if (characterPrefab == null || rifleAim == null)
            {
                throw new InvalidOperationException(
                    "Could not load the Synty character or Basic Shooter rifle-aim clip.");
            }

            GameObject character = PrefabUtility.InstantiatePrefab(characterPrefab)
                as GameObject;
            if (character == null)
            {
                throw new InvalidOperationException("Could not instantiate the Synty character.");
            }

            character.name = "Synty + Basic Shooter Retarget Only";
            Animator animator = character.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
            {
                throw new InvalidOperationException(
                    "The Synty character does not have a valid humanoid Animator.");
            }

            SampleAndFreeze(character, rifleAim, rifleAim.length * 0.5f);
            animator.enabled = false;
            CreateLight();
            CreateCamera(character.transform.position + new Vector3(3f, 1.8f, -4f));

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = character;
            EditorGUIUtility.PingObject(character);
            Debug.Log(
                "Created Synty retarget probe. This scene contains no rifle, IK, "
                + "Animator Controller, or gameplay code: inspect the frozen left arm "
                + "to verify the imported humanoid retargeting.",
                character);
        }

        internal static void RefreshOpenProbe()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded)
            {
                return;
            }

            GameObject character = Array.Find(
                scene.GetRootGameObjects(),
                root => root.name == "Synty + Basic Shooter Retarget Only");
            if (character == null)
            {
                return;
            }

            AnimationClip rifleAim = LoadAnimationClip(RifleAimClipPath);
            if (rifleAim == null)
            {
                return;
            }

            SampleAndFreeze(character, rifleAim, rifleAim.length * 0.5f);
            Animator animator = character.GetComponent<Animator>();
            if (animator != null)
            {
                animator.enabled = false;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Refreshed the open Synty retarget probe after clip import.",
                character);
        }

        private static void SampleAndFreeze(
            GameObject character,
            AnimationClip clip,
            float sampleTime)
        {
            Transform[] transforms = character.GetComponentsInChildren<Transform>(true);
            var positions = new Vector3[transforms.Length];
            var rotations = new Quaternion[transforms.Length];
            var scales = new Vector3[transforms.Length];

            try
            {
                AnimationMode.StartAnimationMode();
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(character, clip, sampleTime);
                AnimationMode.EndSampling();

                for (int index = 0; index < transforms.Length; index++)
                {
                    positions[index] = transforms[index].localPosition;
                    rotations[index] = transforms[index].localRotation;
                    scales[index] = transforms[index].localScale;
                }
            }
            finally
            {
                if (AnimationMode.InAnimationMode())
                {
                    AnimationMode.StopAnimationMode();
                }
            }

            for (int index = 0; index < transforms.Length; index++)
            {
                transforms[index].SetLocalPositionAndRotation(
                    positions[index], rotations[index]);
                transforms[index].localScale = scales[index];
            }
        }

        private static AnimationClip LoadAnimationClip(string path)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip
                    && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                {
                    return clip;
                }
            }

            return null;
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Key Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }

        private static void CreateCamera(Vector3 position)
        {
            var cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.transform.position = position;
            camera.transform.LookAt(new Vector3(0f, 1f, 0f));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.18f, 0.22f);
        }
    }
}
