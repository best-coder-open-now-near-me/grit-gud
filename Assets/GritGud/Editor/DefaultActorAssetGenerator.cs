using System;
using System.Collections.Generic;
using System.IO;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Actors;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using static GritGud.Editor.DefaultActorAssetRecipe;

namespace GritGud.Editor
{
    [InitializeOnLoad]
    public static class DefaultActorAssetGenerator
    {
        public const string ControllerPath =
            DefaultActorAssetRecipe.ControllerPath;
        public const string ProfilePath =
            DefaultActorAssetRecipe.ProfilePath;
        public const string MotionProfilePath =
            DefaultActorAssetRecipe.MotionProfilePath;
        public const string PrefabPath =
            DefaultActorAssetRecipe.PrefabPath;

        static DefaultActorAssetGenerator()
        {
            EditorApplication.delayCall += GenerateIfMissing;
        }

        [MenuItem("Grit Gud/Regenerate Default Player Actor")]
        public static void Generate()
        {
            GameObject sourceVisual =
                AssetDatabase.LoadAssetAtPath<GameObject>(SourceVisualPath);
            if (sourceVisual == null)
            {
                throw new InvalidOperationException(
                    $"Default actor source visual was not found at "
                    + $"'{SourceVisualPath}'.");
            }

            DefaultActorAssetValidator.ValidateHumanoidVisual(sourceVisual);
            ConfigureShooterAnimationImports();
            Dictionary<DefaultActorClipDefinition, AnimationClip> clips =
                LoadClips();
            Dictionary<DefaultActorClipDefinition, AnimationClip>
                rifleLocomotion = LoadRifleLocomotionClips();
            AnimationClip crouchedIdle = LoadRequiredLoopingClip(
                CrouchedIdlePath,
                "Crouched Idle");
            AnimationClip crouchedWalk = LoadRequiredLoopingClip(
                CrouchedWalkPath,
                "Crouched Walk");
            AnimationClip turnLeft = LoadRequiredLoopingClip(
                TurnLeftPath,
                "Turn Left");
            AnimationClip turnRight = LoadRequiredLoopingClip(
                TurnRightPath,
                "Turn Right");
            AnimationClip rifleFire = LoadRequiredClip(
                RifleFirePath,
                "Rifle Fire",
                mustLoop: false);
            AnimationClip jumpClip = LoadRequiredClip(
                JumpPath,
                "Jump",
                mustLoop: false);
            AnimationClip launcherAim = LoadRequiredClip(
                LauncherAimPath,
                "Launcher Aim",
                mustLoop: true);
            AnimationClip launcherFire = LoadRequiredClip(
                LauncherFirePath,
                "Launcher Fire",
                mustLoop: false);
            AnimationClip throwClip = LoadRequiredClip(
                ThrowPath,
                "Throw",
                mustLoop: false);
            AvatarMask upperBodyMask =
                AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            if (upperBodyMask == null)
            {
                throw new InvalidOperationException(
                    $"Required upper-body mask was not found at "
                    + $"'{UpperBodyMaskPath}'.");
            }

            EnsureFolder("Assets/GritGud/Presentation/Actors/Animation");
            EnsureFolder("Assets/GritGud/Content/Resources/Actors");
            AvatarMask lowerBodyMask = GenerateLowerBodyMask(sourceVisual);
            AnimatorController controller = DefaultActorControllerBuilder.Build(
                clips,
                crouchedIdle,
                crouchedWalk,
                turnLeft,
                turnRight,
                lowerBodyMask,
                rifleLocomotion,
                rifleFire,
                launcherAim,
                launcherFire,
                throwClip,
                jumpClip,
                upperBodyMask);
            ActorAnimationProfile profile =
                DefaultActorProfileBuilder.Build(controller);
            ActorMotionProfile motionProfile =
                DefaultActorMotionProfileBuilder.Build();
            DefaultActorPrefabBuilder.Build(
                sourceVisual,
                profile,
                motionProfile);

            AssetDatabase.SaveAssets();
            NormalizeTextAsset(ControllerPath);
            NormalizeTextAsset(LowerBodyMaskPath);
            AssetDatabase.Refresh();
            DefaultWeaponRigAssetGenerator.EnsureGenerated();
            ValidateGeneratedAssets();
            Debug.Log(
                $"Generated the default player actor at '{PrefabPath}'.");
        }

        public static void EnsureGeneratedAssets()
        {
            if (!DefaultActorAssetValidator.GeneratedAssetsExist())
            {
                Generate();
            }

            ValidateGeneratedAssets();
        }

        [MenuItem("Grit Gud/Validate Default Player Actor")]
        public static void ValidateGeneratedAssets() =>
            DefaultActorAssetValidator.ValidateGeneratedAssets();

        private static void GenerateIfMissing()
        {
            if (DefaultActorAssetValidator.GeneratedAssetsExist())
            {
                if (SourceAssetsExist())
                {
                    ValidateGeneratedAssets();
                }

                return;
            }

            if (!SourceAssetsExist())
            {
                Debug.LogWarning(
                    "Default player assets are missing and their licensed "
                    + "source packages are not installed. Gameplay will use "
                    + "its capsule fallback until the packages are installed "
                    + "and Grit Gud/Regenerate Default Player Actor is run.");
                return;
            }

            Generate();
        }

        private static bool SourceAssetsExist()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(
                    SourceVisualPath) != null &&
                LoadAnimationClip(GetClipPath(ClipDefinitions[0])) != null &&
                LoadAnimationClip(CrouchedIdlePath) != null &&
                LoadAnimationClip(CrouchedWalkPath) != null &&
                LoadAnimationClip(GetRifleClipPath(
                    RifleLocomotionDefinitions[0])) != null &&
                LoadAnimationClip(TurnLeftPath) != null &&
                LoadAnimationClip(TurnRightPath) != null &&
                LoadAnimationClip(RifleFirePath) != null &&
                LoadAnimationClip(JumpPath) != null &&
                LoadAnimationClip(LauncherAimPath) != null &&
                LoadAnimationClip(LauncherFirePath) != null &&
                LoadAnimationClip(ThrowPath) != null;
        }

        private static Dictionary<DefaultActorClipDefinition, AnimationClip>
            LoadClips()
        {
            var clips =
                new Dictionary<DefaultActorClipDefinition, AnimationClip>();
            foreach (DefaultActorClipDefinition definition in ClipDefinitions)
            {
                clips.Add(
                    definition,
                    LoadRequiredLoopingClip(
                        GetClipPath(definition),
                        definition.DisplayName));
            }

            return clips;
        }

        private static Dictionary<DefaultActorClipDefinition, AnimationClip>
            LoadRifleLocomotionClips()
        {
            var clips =
                new Dictionary<DefaultActorClipDefinition, AnimationClip>();
            foreach (DefaultActorClipDefinition definition in
                RifleLocomotionDefinitions)
            {
                clips.Add(
                    definition,
                    LoadRequiredLoopingClip(
                        GetRifleClipPath(definition),
                        definition.DisplayName));
            }

            return clips;
        }

        private static void ConfigureShooterAnimationImports()
        {
            foreach (DefaultActorClipDefinition definition in
                RifleLocomotionDefinitions)
            {
                ConfigureHumanoidClip(
                    GetRifleClipPath(definition),
                    loop: true,
                    additive: false);
            }

            ConfigureHumanoidClip(
                TurnLeftPath,
                loop: true,
                additive: false);
            ConfigureHumanoidClip(
                TurnRightPath,
                loop: true,
                additive: false);
            ConfigureHumanoidClip(
                RifleFirePath,
                loop: false,
                additive: true);
            ConfigureHumanoidClip(
                JumpPath,
                loop: false,
                additive: false);
            ConfigureHumanoidClip(
                LauncherFirePath,
                loop: false,
                additive: true);
        }

        private static void ConfigureHumanoidClip(
            string path,
            bool loop,
            bool additive)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            {
                throw new InvalidOperationException(
                    $"Required shooter animation was not found at '{path}'.");
            }

            bool importerChanged = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importerChanged = true;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
                importerChanged = true;
            }

            foreach (ModelImporterClipAnimation clip in clips)
            {
                if (clip.loopTime == loop && clip.loopPose == loop &&
                    clip.lockRootRotation && clip.lockRootHeightY &&
                    clip.lockRootPositionXZ &&
                    clip.hasAdditiveReferencePose == additive &&
                    (!additive ||
                        Mathf.Abs(
                            clip.additiveReferencePoseFrame
                            - clip.lastFrame) < 0.001f))
                {
                    continue;
                }

                clip.loopTime = loop;
                clip.loopPose = loop;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.hasAdditiveReferencePose = additive;
                clip.additiveReferencePoseFrame = additive
                    ? clip.lastFrame
                    : 0f;
                importerChanged = true;
            }

            if (!importerChanged)
            {
                return;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static AvatarMask GenerateLowerBodyMask(
            GameObject sourceVisual)
        {
            AvatarMask mask =
                AssetDatabase.LoadAssetAtPath<AvatarMask>(LowerBodyMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask
                {
                    name = "Default Player Lower Body",
                };
                AssetDatabase.CreateAsset(mask, LowerBodyMaskPath);
            }

            Animator animator =
                sourceVisual.GetComponentInChildren<Animator>(true);
            BodyRegionMaskBuilder.Configure(
                mask,
                animator,
                BodyRegion.PelvisAndLegs);
            EditorUtility.SetDirty(mask);
            return mask;
        }

        private static AnimationClip LoadRequiredLoopingClip(
            string path,
            string displayName) =>
            LoadRequiredClip(path, displayName, mustLoop: true);

        private static AnimationClip LoadRequiredClip(
            string path,
            string displayName,
            bool mustLoop)
        {
            AnimationClip clip = LoadAnimationClip(path);
            if (clip == null)
            {
                throw new InvalidOperationException(
                    $"Required {displayName} clip was not found at '{path}'.");
            }

            if (clip.legacy || !clip.isHumanMotion ||
                clip.isLooping != mustLoop)
            {
                throw new InvalidOperationException(
                    $"Animation '{path}' must be imported as a "
                    + $"{(mustLoop ? "looping" : "non-looping")}, "
                    + "non-legacy Humanoid clip.");
            }

            return clip;
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

        private static void NormalizeTextAsset(string path)
        {
            string[] lines = File.ReadAllLines(path);
            for (int index = 0; index < lines.Length; index++)
            {
                lines[index] = lines[index].TrimEnd();
            }

            File.WriteAllLines(path, lines);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }
}
