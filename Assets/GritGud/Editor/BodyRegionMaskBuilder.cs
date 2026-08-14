using System;
using System.Collections.Generic;
using GritGud.Presentation.Actors.Animation;
using UnityEngine;

namespace GritGud.Editor
{
    public static class BodyRegionMaskBuilder
    {
        public static void Configure(
            AvatarMask mask,
            Animator animator,
            BodyRegion region)
        {
            if (mask == null)
            {
                throw new ArgumentNullException(nameof(mask));
            }

            ValidateHumanoidAnimator(animator);
            HashSet<string> activePaths = ResolveActivePaths(animator, region);
            mask.transformCount = 0;
            mask.AddTransformPath(animator.transform, true);
            for (int index = 0; index < mask.transformCount; index++)
            {
                mask.SetTransformActive(
                    index,
                    activePaths.Contains(mask.GetTransformPath(index)));
            }

            for (AvatarMaskBodyPart part = AvatarMaskBodyPart.Root;
                 part < AvatarMaskBodyPart.LastBodyPart;
                 part++)
            {
                mask.SetHumanoidBodyPartActive(
                    part,
                    IncludesHumanoidBodyPart(region, part));
            }
        }

        public static bool Matches(
            AvatarMask mask,
            Animator animator,
            BodyRegion region)
        {
            if (mask == null || animator == null || animator.avatar == null ||
                !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                return false;
            }

            HashSet<string> activePaths;
            try
            {
                activePaths = ResolveActivePaths(animator, region);
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            Transform[] transforms = animator.GetComponentsInChildren<Transform>(true);
            if (mask.transformCount != transforms.Length)
            {
                return false;
            }

            for (int index = 0; index < mask.transformCount; index++)
            {
                bool expected = activePaths.Contains(mask.GetTransformPath(index));
                if (mask.GetTransformActive(index) != expected)
                {
                    return false;
                }
            }

            for (AvatarMaskBodyPart part = AvatarMaskBodyPart.Root;
                 part < AvatarMaskBodyPart.LastBodyPart;
                 part++)
            {
                if (mask.GetHumanoidBodyPartActive(part) !=
                    IncludesHumanoidBodyPart(region, part))
                {
                    return false;
                }
            }

            return true;
        }

        private static HashSet<string> ResolveActivePaths(
            Animator animator,
            BodyRegion region)
        {
            var activeTransforms = new HashSet<Transform>
            {
                animator.transform,
            };

            switch (region)
            {
                case BodyRegion.PelvisAndLegs:
                    Transform hips = RequireBone(animator, HumanBodyBones.Hips);
                    AddAncestors(hips, animator.transform, activeTransforms);
                    AddBoneHierarchy(
                        animator,
                        HumanBodyBones.LeftUpperLeg,
                        activeTransforms);
                    AddBoneHierarchy(
                        animator,
                        HumanBodyBones.RightUpperLeg,
                        activeTransforms);
                    break;

                case BodyRegion.TorsoAndArms:
                    AddBone(animator, HumanBodyBones.Spine, activeTransforms);
                    AddOptionalBone(animator, HumanBodyBones.Chest, activeTransforms);
                    AddOptionalBone(
                        animator,
                        HumanBodyBones.UpperChest,
                        activeTransforms);
                    AddOptionalBone(
                        animator,
                        HumanBodyBones.LeftShoulder,
                        activeTransforms);
                    AddBoneHierarchy(
                        animator,
                        HumanBodyBones.LeftUpperArm,
                        activeTransforms);
                    AddOptionalBone(
                        animator,
                        HumanBodyBones.RightShoulder,
                        activeTransforms);
                    AddBoneHierarchy(
                        animator,
                        HumanBodyBones.RightUpperArm,
                        activeTransforms);
                    break;

                case BodyRegion.HeadAndNeck:
                    AddOptionalBone(animator, HumanBodyBones.Neck, activeTransforms);
                    AddBoneHierarchy(
                        animator,
                        HumanBodyBones.Head,
                        activeTransforms);
                    break;

                case BodyRegion.LeftArm:
                    AddOptionalBone(
                        animator,
                        HumanBodyBones.LeftShoulder,
                        activeTransforms);
                    AddBoneHierarchy(
                        animator,
                        HumanBodyBones.LeftUpperArm,
                        activeTransforms);
                    break;

                case BodyRegion.RightArm:
                    AddOptionalBone(
                        animator,
                        HumanBodyBones.RightShoulder,
                        activeTransforms);
                    AddBoneHierarchy(
                        animator,
                        HumanBodyBones.RightUpperArm,
                        activeTransforms);
                    break;

                case BodyRegion.Hands:
                    AddBoneHierarchy(
                        animator,
                        HumanBodyBones.LeftHand,
                        activeTransforms);
                    AddBoneHierarchy(
                        animator,
                        HumanBodyBones.RightHand,
                        activeTransforms);
                    break;

                case BodyRegion.WholeBody:
                    activeTransforms.UnionWith(
                        animator.GetComponentsInChildren<Transform>(true));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(region), region, null);
            }

            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (Transform transform in activeTransforms)
            {
                paths.Add(GetRelativePath(animator.transform, transform));
            }

            return paths;
        }

        private static bool IncludesHumanoidBodyPart(
            BodyRegion region,
            AvatarMaskBodyPart part)
        {
            switch (region)
            {
                case BodyRegion.PelvisAndLegs:
                    return part == AvatarMaskBodyPart.LeftLeg ||
                        part == AvatarMaskBodyPart.RightLeg ||
                        part == AvatarMaskBodyPart.LeftFootIK ||
                        part == AvatarMaskBodyPart.RightFootIK;

                case BodyRegion.TorsoAndArms:
                    return part == AvatarMaskBodyPart.Body ||
                        part == AvatarMaskBodyPart.LeftArm ||
                        part == AvatarMaskBodyPart.RightArm ||
                        part == AvatarMaskBodyPart.LeftFingers ||
                        part == AvatarMaskBodyPart.RightFingers ||
                        part == AvatarMaskBodyPart.LeftHandIK ||
                        part == AvatarMaskBodyPart.RightHandIK;

                case BodyRegion.HeadAndNeck:
                    return part == AvatarMaskBodyPart.Head;

                case BodyRegion.LeftArm:
                    return part == AvatarMaskBodyPart.LeftArm ||
                        part == AvatarMaskBodyPart.LeftFingers ||
                        part == AvatarMaskBodyPart.LeftHandIK;

                case BodyRegion.RightArm:
                    return part == AvatarMaskBodyPart.RightArm ||
                        part == AvatarMaskBodyPart.RightFingers ||
                        part == AvatarMaskBodyPart.RightHandIK;

                case BodyRegion.Hands:
                    return part == AvatarMaskBodyPart.LeftFingers ||
                        part == AvatarMaskBodyPart.RightFingers ||
                        part == AvatarMaskBodyPart.LeftHandIK ||
                        part == AvatarMaskBodyPart.RightHandIK;

                case BodyRegion.WholeBody:
                    return true;

                default:
                    throw new ArgumentOutOfRangeException(nameof(region), region, null);
            }
        }

        private static void AddBone(
            Animator animator,
            HumanBodyBones bone,
            ISet<Transform> transforms)
        {
            transforms.Add(RequireBone(animator, bone));
        }

        private static void AddOptionalBone(
            Animator animator,
            HumanBodyBones bone,
            ISet<Transform> transforms)
        {
            Transform transform = animator.GetBoneTransform(bone);
            if (transform != null)
            {
                transforms.Add(transform);
            }
        }

        private static void AddBoneHierarchy(
            Animator animator,
            HumanBodyBones bone,
            ISet<Transform> transforms)
        {
            Transform root = RequireBone(animator, bone);
            transforms.UnionWith(root.GetComponentsInChildren<Transform>(true));
        }

        private static void AddAncestors(
            Transform transform,
            Transform root,
            ISet<Transform> transforms)
        {
            Transform current = transform;
            while (current != null)
            {
                transforms.Add(current);
                if (current == root)
                {
                    return;
                }

                current = current.parent;
            }

            throw new InvalidOperationException(
                $"Humanoid bone '{transform.name}' is not below Animator '{root.name}'.");
        }

        private static Transform RequireBone(Animator animator, HumanBodyBones bone)
        {
            Transform transform = animator.GetBoneTransform(bone);
            if (transform == null)
            {
                throw new InvalidOperationException(
                    $"Humanoid Animator '{animator.name}' does not resolve required bone '{bone}'.");
            }

            return transform;
        }

        private static string GetRelativePath(Transform root, Transform transform)
        {
            if (transform == root)
            {
                return string.Empty;
            }

            var names = new Stack<string>();
            Transform current = transform;
            while (current != null && current != root)
            {
                names.Push(current.name);
                current = current.parent;
            }

            if (current != root)
            {
                throw new InvalidOperationException(
                    $"Transform '{transform.name}' is not below Animator '{root.name}'.");
            }

            return string.Join("/", names);
        }

        private static void ValidateHumanoidAnimator(Animator animator)
        {
            if (animator == null)
            {
                throw new ArgumentNullException(nameof(animator));
            }

            if (animator.avatar == null || !animator.avatar.isValid ||
                !animator.avatar.isHuman)
            {
                throw new ArgumentException(
                    $"Animator '{animator.name}' requires a valid Humanoid avatar.",
                    nameof(animator));
            }
        }
    }
}
