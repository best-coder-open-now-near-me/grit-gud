using GritGud.Presentation.Actors;
using UnityEditor;
using UnityEngine;
using static GritGud.Editor.DefaultActorAssetRecipe;

namespace GritGud.Editor
{
    internal static class DefaultActorMotionProfileBuilder
    {
        internal static ActorMotionProfile Build()
        {
            ActorMotionProfile profile =
                AssetDatabase.LoadAssetAtPath<ActorMotionProfile>(
                    MotionProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<ActorMotionProfile>();
                AssetDatabase.CreateAsset(profile, MotionProfilePath);
            }

            var serialized = new SerializedObject(profile);
            serialized.FindProperty("walkSpeed").floatValue = WalkSpeed;
            serialized.FindProperty("sprintSpeed").floatValue = SprintSpeed;
            serialized.FindProperty("crouchedSpeed").floatValue =
                CrouchedSpeed;
            serialized.FindProperty("acceleration").floatValue =
                MovementAcceleration;
            serialized.FindProperty("gravityMagnitude").floatValue =
                GravityMagnitude;
            serialized.FindProperty("groundedDownwardSpeed").floatValue =
                GroundedDownwardSpeed;
            serialized.FindProperty("turnSharpness").floatValue =
                MovementTurnSharpness;
            serialized.FindProperty("fallResetDistance").floatValue =
                FallResetDistance;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return profile;
        }
    }
}
