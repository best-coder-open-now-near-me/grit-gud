using System;
using System.Collections.Generic;
using GritGud.Presentation.Actors;
using UnityEngine;
using static GritGud.Editor.DefaultActorAssetRecipe;

namespace GritGud.Editor
{
    internal static class DefaultActorRagdollProfileValidator
    {
        internal static void Validate(ActorRagdollProfile profile)
        {
            if (profile == null ||
                profile.TraceSchemaId != RagdollTraceSchemaId ||
                profile.TraceSchemaVersion != RagdollTraceSchemaVersion ||
                profile.Bones.Count != 12 ||
                Mathf.Abs(profile.TotalMass - RagdollTotalMass) > 0.001f ||
                Mathf.Abs(
                    profile.HandoffNormalizedTime -
                    RagdollHandoffNormalizedTime) > 0.001f ||
                Mathf.Abs(
                    profile.SampleIntervalSeconds -
                    RagdollSampleIntervalSeconds) > 0.001f ||
                Mathf.Abs(
                    profile.MinimumActiveSeconds -
                    RagdollMinimumActiveSeconds) > 0.001f ||
                Mathf.Abs(
                    profile.SettleHoldSeconds -
                    RagdollSettleHoldSeconds) > 0.001f ||
                Mathf.Abs(
                    profile.MaximumActiveSeconds -
                    RagdollMaximumActiveSeconds) > 0.001f ||
                Mathf.Abs(
                    profile.SettleLinearSpeed -
                    RagdollSettleLinearSpeed) > 0.001f ||
                Mathf.Abs(
                    profile.SettleAngularSpeed -
                    RagdollSettleAngularSpeed) > 0.001f ||
                Mathf.Abs(
                    profile.MaximumImpulseSpeed -
                    RagdollMaximumImpulseSpeed) > 0.001f ||
                Mathf.Abs(
                    profile.UpwardImpulseFraction -
                    RagdollUpwardImpulseFraction) > 0.001f ||
                profile.MaximumStoredTraces != RagdollMaximumStoredTraces ||
                Mathf.Abs(
                    profile.LinearDamping -
                    RagdollLinearDamping) > 0.001f ||
                Mathf.Abs(
                    profile.AngularDamping -
                    RagdollAngularDamping) > 0.001f)
            {
                throw new InvalidOperationException(
                    "The default actor ragdoll profile does not match the authored recipe.");
            }

            var bones = new HashSet<HumanBodyBones>();
            foreach (ActorRagdollBoneDefinition definition in profile.Bones)
            {
                if (definition == null ||
                    definition.Bone == HumanBodyBones.LastBone ||
                    definition.EndBone == HumanBodyBones.LastBone ||
                    !bones.Add(definition.Bone))
                {
                    throw new InvalidOperationException(
                        "The default actor ragdoll profile requires twelve unique, connected bones.");
                }
            }
            foreach (ActorRagdollBoneDefinition definition in profile.Bones)
            {
                if (definition.ConnectedBone != HumanBodyBones.LastBone &&
                    !bones.Contains(definition.ConnectedBone))
                {
                    throw new InvalidOperationException(
                        $"Ragdoll bone '{definition.Bone}' has no connected body.");
                }
            }
        }
    }
}
