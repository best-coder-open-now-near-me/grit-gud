using GritGud.Presentation.Actors;
using UnityEditor;
using UnityEngine;
using static GritGud.Editor.DefaultActorAssetRecipe;

namespace GritGud.Editor
{
    internal static class DefaultActorRagdollProfileBuilder
    {
        internal static ActorRagdollProfile Build()
        {
            ActorRagdollProfile profile =
                AssetDatabase.LoadAssetAtPath<ActorRagdollProfile>(
                    RagdollProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<
                    ActorRagdollProfile>();
                AssetDatabase.CreateAsset(profile, RagdollProfilePath);
            }

            var serialized = new SerializedObject(profile);
            serialized.FindProperty("traceSchemaId").stringValue =
                RagdollTraceSchemaId;
            serialized.FindProperty("traceSchemaVersion").intValue =
                RagdollTraceSchemaVersion;
            serialized.FindProperty("totalMass").floatValue =
                RagdollTotalMass;
            serialized.FindProperty("handoffNormalizedTime").floatValue =
                RagdollHandoffNormalizedTime;
            serialized.FindProperty("sampleIntervalSeconds").floatValue =
                RagdollSampleIntervalSeconds;
            serialized.FindProperty("minimumActiveSeconds").floatValue =
                RagdollMinimumActiveSeconds;
            serialized.FindProperty("settleHoldSeconds").floatValue =
                RagdollSettleHoldSeconds;
            serialized.FindProperty("maximumActiveSeconds").floatValue =
                RagdollMaximumActiveSeconds;
            serialized.FindProperty("settleLinearSpeed").floatValue =
                RagdollSettleLinearSpeed;
            serialized.FindProperty("settleAngularSpeed").floatValue =
                RagdollSettleAngularSpeed;
            serialized.FindProperty("maximumImpulseSpeed").floatValue =
                RagdollMaximumImpulseSpeed;
            serialized.FindProperty("upwardImpulseFraction").floatValue =
                RagdollUpwardImpulseFraction;
            serialized.FindProperty("maximumStoredTraces").intValue =
                RagdollMaximumStoredTraces;
            serialized.FindProperty("linearDamping").floatValue =
                RagdollLinearDamping;
            serialized.FindProperty("angularDamping").floatValue =
                RagdollAngularDamping;

            SerializedProperty bones = serialized.FindProperty("bones");
            bones.arraySize = 12;
            ConfigureBone(bones, 0,
                HumanBodyBones.Hips, HumanBodyBones.LastBone,
                HumanBodyBones.Spine, ActorRagdollColliderShape.Capsule,
                0.14f, 0.55f, 0.9f, -18f, 18f, 18f, 18f);
            ConfigureBone(bones, 1,
                HumanBodyBones.Spine, HumanBodyBones.Hips,
                HumanBodyBones.Chest, ActorRagdollColliderShape.Capsule,
                0.12f, 0.42f, 0.95f, -18f, 18f, 18f, 18f);
            ConfigureBone(bones, 2,
                HumanBodyBones.Chest, HumanBodyBones.Spine,
                HumanBodyBones.Neck, ActorRagdollColliderShape.Capsule,
                0.16f, 0.36f, 0.95f, -22f, 22f, 24f, 20f);
            ConfigureBone(bones, 3,
                HumanBodyBones.Head, HumanBodyBones.Chest,
                HumanBodyBones.Neck, ActorRagdollColliderShape.Sphere,
                0.08f, 0.82f, 1f, -35f, 35f, 35f, 35f);
            ConfigureArm(bones, 4, true);
            ConfigureArm(bones, 6, false);
            ConfigureLeg(bones, 8, true);
            ConfigureLeg(bones, 10, false);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void ConfigureArm(
            SerializedProperty bones,
            int startIndex,
            bool left)
        {
            HumanBodyBones upper = left
                ? HumanBodyBones.LeftUpperArm
                : HumanBodyBones.RightUpperArm;
            HumanBodyBones lower = left
                ? HumanBodyBones.LeftLowerArm
                : HumanBodyBones.RightLowerArm;
            HumanBodyBones hand = left
                ? HumanBodyBones.LeftHand
                : HumanBodyBones.RightHand;
            ConfigureBone(bones, startIndex,
                upper, HumanBodyBones.Chest, lower,
                ActorRagdollColliderShape.Capsule,
                0.055f, 0.27f, 0.9f, -55f, 55f, 65f, 45f);
            ConfigureBone(bones, startIndex + 1,
                lower, upper, hand,
                ActorRagdollColliderShape.Capsule,
                0.035f, 0.24f, 0.9f, -8f, 75f, 12f, 12f);
        }

        private static void ConfigureLeg(
            SerializedProperty bones,
            int startIndex,
            bool left)
        {
            HumanBodyBones upper = left
                ? HumanBodyBones.LeftUpperLeg
                : HumanBodyBones.RightUpperLeg;
            HumanBodyBones lower = left
                ? HumanBodyBones.LeftLowerLeg
                : HumanBodyBones.RightLowerLeg;
            HumanBodyBones foot = left
                ? HumanBodyBones.LeftFoot
                : HumanBodyBones.RightFoot;
            ConfigureBone(bones, startIndex,
                upper, HumanBodyBones.Hips, lower,
                ActorRagdollColliderShape.Capsule,
                0.09f, 0.25f, 0.92f, -35f, 45f, 40f, 28f);
            ConfigureBone(bones, startIndex + 1,
                lower, upper, foot,
                ActorRagdollColliderShape.Capsule,
                0.06f, 0.22f, 0.92f, -8f, 80f, 12f, 12f);
        }

        private static void ConfigureBone(
            SerializedProperty bones,
            int index,
            HumanBodyBones bone,
            HumanBodyBones connected,
            HumanBodyBones end,
            ActorRagdollColliderShape shape,
            float massFraction,
            float radiusScale,
            float lengthScale,
            float lowTwist,
            float highTwist,
            float swingOne,
            float swingTwo)
        {
            SerializedProperty entry = bones.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("bone").enumValueIndex = (int)bone;
            entry.FindPropertyRelative("connectedBone").enumValueIndex =
                (int)connected;
            entry.FindPropertyRelative("endBone").enumValueIndex = (int)end;
            entry.FindPropertyRelative("colliderShape").enumValueIndex =
                (int)shape;
            entry.FindPropertyRelative("massFraction").floatValue =
                massFraction;
            entry.FindPropertyRelative("radiusScale").floatValue = radiusScale;
            entry.FindPropertyRelative("lengthScale").floatValue = lengthScale;
            entry.FindPropertyRelative("lowTwistDegrees").floatValue = lowTwist;
            entry.FindPropertyRelative("highTwistDegrees").floatValue =
                highTwist;
            entry.FindPropertyRelative("swingOneDegrees").floatValue = swingOne;
            entry.FindPropertyRelative("swingTwoDegrees").floatValue = swingTwo;
        }
    }
}
