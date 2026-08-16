using System;
using UnityEditor;
using UnityEngine;

namespace GritGud.Editor
{
    internal sealed class DefaultActorClipDefinition
    {
        internal DefaultActorClipDefinition(
            string displayName,
            string relativePath,
            Vector2 blendPosition)
        {
            DisplayName = displayName;
            RelativePath = relativePath;
            BlendPosition = blendPosition;
        }

        internal string DisplayName { get; }

        internal string RelativePath { get; }

        internal Vector2 BlendPosition { get; }
    }

    internal static class DefaultActorAssetRecipe
    {
        internal const string ControllerPath =
            "Assets/GritGud/Presentation/Actors/Animation/DefaultPlayerLocomotion.controller";
        internal const string ProfilePath =
            "Assets/GritGud/Presentation/Actors/Animation/DefaultPlayerAnimationProfile.asset";
        internal const string MotionProfilePath =
            "Assets/GritGud/Presentation/Actors/DefaultActorMotionProfile.asset";
        internal const string RagdollProfilePath =
            "Assets/GritGud/Presentation/Actors/DefaultActorRagdollProfile.asset";
        internal const string PrefabPath =
            "Assets/GritGud/Content/Resources/Actors/DefaultPlayerActor.prefab";
        internal const string SourceVisualPath =
            "Assets/Synty/PolygonBattleRoyale/Prefabs/Characters/Character_MilitaryMale_01.prefab";
        internal const string SourceAnimationRoot =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male";
        internal const string CrouchedIdlePath =
            "Assets/DoubleL/FBX Unity/One Hand Up/Movement/Crouch/Idle/Idle/1Hand_Up_Crouch_Idle_1.fbx";
        internal const string CrouchedWalkPath =
            "Assets/DoubleL/FBX Unity/One Hand Up/Movement/Crouch/Base/InPlace/1Hand_Up_Crouch_F_InPlace.fbx";
        internal const string ShooterAnimationRoot = "Assets/Basic Shooter Pack";
        internal const string RifleFirePath =
            ShooterAnimationRoot + "/firing rifle.fbx";
        internal const string JumpPath =
            ShooterAnimationRoot + "/rifle jump.fbx";
        internal const string TurnLeftPath =
            ShooterAnimationRoot + "/turn left.fbx";
        internal const string TurnRightPath =
            ShooterAnimationRoot + "/turning right 45 degrees.fbx";
        internal const string LauncherAimPath =
            SourceAnimationRoot + "/Combat/Bazooka/HumanM@Bazooka_Aim01.fbx";
        internal const string LauncherFirePath =
            SourceAnimationRoot + "/Combat/Bazooka/HumanM@Bazooka_Aim01_Shoot01.fbx";
        internal const string ThrowPath =
            SourceAnimationRoot + "/Combat/Grenade/HumanM@ThrowGrenade01_L.fbx";
        internal const string KnifeIdlePath = "Assets/Mixamo/Knife Idle.fbx";
        internal const string KnifeStrikePath = "Assets/Mixamo/Stabbing.fbx";
        internal const string ShoulderFallPath =
            "Assets/Mixamo/Shoulder Hit And Fall.fbx";
        internal const string FallOverPath = "Assets/Mixamo/Fall Over.fbx";
        internal const string UpperBodyMaskPath =
            "Assets/Kevin Iglesias/Human Animations/Models/Avatar Masks/Human Body Upper Mask.mask";
        internal const string LowerBodyMaskPath =
            "Assets/GritGud/Presentation/Actors/Animation/DefaultPlayerLowerBody.mask";
        internal const string StandingStateName = "Standing Locomotion";
        internal const string CrouchedStateName = "Crouched Locomotion";
        internal const string TurnInPlaceBlendName = "Lower Body Turn Blend";
        internal const float CrouchedWalkBlendSpeed = 2.5f;
        internal const float StanceTransitionDuration = 0.12f;
        internal const int EmptyPoseValue = 0;
        internal const int RiflePoseValue = 1;
        internal const int LauncherPoseValue = 2;
        internal const int MeleePoseValue = 3;
        internal const float WalkSpeed = 4f;
        internal const float SprintSpeed = 6.5f;
        internal const float CrouchedSpeed = 2.5f;
        internal const float MovementAcceleration = 24f;
        internal const float GravityMagnitude = 25f;
        internal const float GroundedDownwardSpeed = 2f;
        internal const float MovementTurnSharpness = 18f;
        internal const float FallResetDistance = 10f;
        internal const float LocomotionReferenceSpeed = 6.5f;
        internal const float TurnReferenceDegreesPerSecond = 540f;
        internal const float ParameterDampTime = 0.08f;
        internal const float MaximumBodyAimCorrectionDegrees = 48f;
        internal const float BodyAimDegreesPerSecond = 300f;
        internal const float ActorAimTurnDegreesPerSecond = 300f;
        internal const float WeaponAimDegreesPerSecond = 240f;
        internal const float ShotAlignmentToleranceDegrees = 1f;
        internal const float TurnActivationDegreesPerSecond = 18f;
        internal const float TurnSustainDegreesPerSecond = 6f;
        internal const float TurnMinimumActiveBlend = 0.65f;
        internal const float TurnReleaseDelaySeconds = 0.12f;
        internal const float TurnReleaseSeconds = 0.16f;
        internal const float TurnMaximumMovementSpeed = 0.1f;
        internal const float TurnMaximumLayerWeight = 1f;
        internal const float TurnMaximumPoseBlend = 0.75f;
        internal const float TurnPlaybackSpeed = 0.65f;
        internal const float WeaponPoseLayerWeight = 0.68f;
        internal const float WeaponPoseTransitionSeconds = 0.12f;
        internal const float RifleRecoilPlaybackSpeed = 0.6f;
        internal const float LauncherRecoilPlaybackSpeed = 1f;
        internal const float RecoilExitNormalizedTime = 0.9f;
        internal const float RecoilReturnTransitionSeconds = 0.12f;
        internal const float ActionTransitionSeconds = 0.08f;
        internal const float ActionExitNormalizedTime = 0.92f;
        internal const float ActionReturnTransitionSeconds = 0.1f;
        internal const float ContactStrikeSeconds =
            GritGud.Presentation.Gameplay
                .GameplayCloseQuartersPresentationTiming
                .ContactStrikeSeconds;
        internal const float HitReactionExitNormalizedTime = 0.3f;
        internal const string RagdollTraceSchemaId = "default-humanoid-v1";
        internal const int RagdollTraceSchemaVersion = 1;
        internal const float RagdollTotalMass = 72f;
        internal const float RagdollHandoffNormalizedTime = 0.72f;
        internal const float RagdollSampleIntervalSeconds = 0.05f;
        internal const float RagdollMinimumActiveSeconds = 0.45f;
        internal const float RagdollSettleHoldSeconds = 0.35f;
        internal const float RagdollMaximumActiveSeconds = 2.25f;
        internal const float RagdollSettleLinearSpeed = 0.12f;
        internal const float RagdollSettleAngularSpeed = 0.3f;
        internal const float RagdollMaximumImpulseSpeed = 2.4f;
        internal const float RagdollUpwardImpulseFraction = 0.22f;
        internal const int RagdollMaximumStoredTraces = 4;
        internal const float RagdollLinearDamping = 0.08f;
        internal const float RagdollAngularDamping = 0.12f;

        internal static readonly DefaultActorClipDefinition[] ClipDefinitions =
        {
            new("Idle", "Idles/HumanM@Idle01.fbx", Vector2.zero),
            new("Walk Forward", "Movement/Walk/HumanM@Walk01_Forward.fbx", new Vector2(0f, 0.6f)),
            new("Walk Backward", "Movement/Walk/HumanM@Walk01_Backward.fbx", new Vector2(0f, -0.6f)),
            new("Walk Left", "Movement/Walk/HumanM@Walk01_Left.fbx", new Vector2(-0.6f, 0f)),
            new("Walk Right", "Movement/Walk/HumanM@Walk01_Right.fbx", new Vector2(0.6f, 0f)),
            new("Walk Forward Left", "Movement/Walk/HumanM@Walk01_ForwardLeft.fbx", new Vector2(-0.42f, 0.42f)),
            new("Walk Forward Right", "Movement/Walk/HumanM@Walk01_ForwardRight.fbx", new Vector2(0.42f, 0.42f)),
            new("Walk Backward Left", "Movement/Walk/HumanM@Walk01_BackwardLeft.fbx", new Vector2(-0.42f, -0.42f)),
            new("Walk Backward Right", "Movement/Walk/HumanM@Walk01_BackwardRight.fbx", new Vector2(0.42f, -0.42f)),
            new("Run Forward", "Movement/Run/HumanM@Run01_Forward.fbx", new Vector2(0f, 1f)),
            new("Run Backward", "Movement/Run/HumanM@Run01_Backward.fbx", new Vector2(0f, -1f)),
            new("Run Left", "Movement/Run/HumanM@Run01_Left.fbx", new Vector2(-1f, 0f)),
            new("Run Right", "Movement/Run/HumanM@Run01_Right.fbx", new Vector2(1f, 0f)),
            new("Run Forward Left", "Movement/Run/HumanM@Run01_ForwardLeft.fbx", new Vector2(-0.71f, 0.71f)),
            new("Run Forward Right", "Movement/Run/HumanM@Run01_ForwardRight.fbx", new Vector2(0.71f, 0.71f)),
            new("Run Backward Left", "Movement/Run/HumanM@Run01_BackwardLeft.fbx", new Vector2(-0.71f, -0.71f)),
            new("Run Backward Right", "Movement/Run/HumanM@Run01_BackwardRight.fbx", new Vector2(0.71f, -0.71f)),
        };

        internal static readonly DefaultActorClipDefinition[]
            RifleLocomotionDefinitions =
        {
            new("Rifle Aim Idle", "rifle aiming idle.fbx", Vector2.zero),
            new("Rifle Walk Forward", "walking.fbx", new Vector2(0f, 0.6f)),
            new("Rifle Walk Backward", "walking backwards.fbx", new Vector2(0f, -0.6f)),
            new("Rifle Strafe Left", "strafe left.fbx", new Vector2(-0.6f, 0f)),
            new("Rifle Strafe Right", "strafe right.fbx", new Vector2(0.6f, 0f)),
            new("Rifle Run Forward", "rifle run.fbx", new Vector2(0f, 1f)),
            new("Rifle Run Backward", "run backwards.fbx", new Vector2(0f, -1f)),
        };

        internal static AnimationClip LoadAnimationClip(string path)
        {
            foreach (UnityEngine.Object asset in
                AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip &&
                    !clip.name.StartsWith(
                        "__preview__",
                        StringComparison.Ordinal))
                {
                    return clip;
                }
            }

            return null;
        }

        internal static string GetClipPath(
            DefaultActorClipDefinition definition) =>
            $"{SourceAnimationRoot}/{definition.RelativePath}";

        internal static string GetRifleClipPath(
            DefaultActorClipDefinition definition) =>
            $"{ShooterAnimationRoot}/{definition.RelativePath}";
    }
}
