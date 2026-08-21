using System;
using System.Collections.Generic;
using GritGud.Domain.Characters;
using UnityEngine;

namespace GritGud.Presentation.Characters
{
    public static class CharacterAppearanceProjector
    {
        private const string GeneratedAccessoryPrefix = "[Character Accessory] ";

        public static void Apply(
            GameObject actor,
            CharacterAppearanceData appearance,
            CharacterAppearanceCatalog catalog)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));
            if (appearance == null)
                throw new ArgumentNullException(nameof(appearance));
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            CharacterBodyPresentationDefinition body = catalog.GetBody(appearance.bodyId);
            SetExclusiveRenderer(actor, catalog.Bodies, body.RendererName);
            var selected = new Dictionary<string, CharacterAccessoryPresentationDefinition>(
                StringComparer.Ordinal);
            foreach (CharacterAccessorySelectionData selection in appearance.accessories
                ?? new List<CharacterAccessorySelectionData>())
            {
                if (selection == null || string.IsNullOrWhiteSpace(selection.accessoryId))
                    continue;
                CharacterAccessoryPresentationDefinition accessory =
                    catalog.GetAccessory(selection.accessoryId);
                selected[accessory.SlotId] = accessory;
            }

            foreach (CharacterAccessoryPresentationDefinition accessory in catalog.Accessories)
            {
                if (accessory?.ProjectionKind
                    != CharacterAccessoryProjectionKind.ToggleRenderer)
                {
                    continue;
                }
                bool enabled = selected.TryGetValue(accessory.SlotId, out var chosen)
                    && ReferenceEquals(chosen, accessory);
                SetNamedObjectsActive(actor, accessory.RendererName, enabled);
            }

            foreach (Transform existing in actor.GetComponentsInChildren<Transform>(true))
            {
                if (existing != actor.transform
                    && existing.name.StartsWith(
                        GeneratedAccessoryPrefix,
                        StringComparison.Ordinal))
                {
                    Destroy(existing.gameObject);
                }
            }

            Animator animator = actor.GetComponentInChildren<Animator>(true);
            bool hideHair = selected.TryGetValue(
                CharacterAppearanceSlotIds.Headwear,
                out var headwear) && headwear.HidesHair;
            foreach (CharacterAccessoryPresentationDefinition accessory in selected.Values)
            {
                if (accessory.ProjectionKind != CharacterAccessoryProjectionKind.AttachPrefab)
                    continue;
                if (hideHair && string.Equals(
                        accessory.SlotId,
                        CharacterAppearanceSlotIds.Hair,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                Transform socket = ResolveSocket(actor.transform, animator, accessory.Socket);
                GameObject instance = UnityEngine.Object.Instantiate(accessory.Prefab, socket, false);
                instance.name = GeneratedAccessoryPrefix + accessory.Id;
                instance.transform.localPosition = accessory.LocalPosition;
                instance.transform.localRotation = accessory.LocalRotation;
                instance.transform.localScale = accessory.LocalScale;
                foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
            }
        }

        private static void SetExclusiveRenderer(
            GameObject actor,
            IReadOnlyList<CharacterBodyPresentationDefinition> bodies,
            string selectedRenderer)
        {
            foreach (CharacterBodyPresentationDefinition body in bodies)
            {
                if (body != null)
                {
                    SetNamedObjectsActive(
                        actor,
                        body.RendererName,
                        string.Equals(
                            body.RendererName,
                            selectedRenderer,
                            StringComparison.Ordinal));
                }
            }
        }

        private static void SetNamedObjectsActive(GameObject actor, string name, bool active)
        {
            foreach (Transform child in actor.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(child.name, name, StringComparison.Ordinal))
                    child.gameObject.SetActive(active);
            }
        }

        private static Transform ResolveSocket(
            Transform fallback,
            Animator animator,
            CharacterAppearanceSocket socket)
        {
            if (animator == null || !animator.isHuman || socket == CharacterAppearanceSocket.Root)
                return fallback;
            HumanBodyBones bone = socket switch
            {
                CharacterAppearanceSocket.Head => HumanBodyBones.Head,
                CharacterAppearanceSocket.Neck => HumanBodyBones.Neck,
                CharacterAppearanceSocket.Chest => HumanBodyBones.UpperChest,
                CharacterAppearanceSocket.Hips => HumanBodyBones.Hips,
                CharacterAppearanceSocket.LeftShoulder => HumanBodyBones.LeftShoulder,
                CharacterAppearanceSocket.RightShoulder => HumanBodyBones.RightShoulder,
                _ => HumanBodyBones.Hips,
            };
            Transform resolved = animator.GetBoneTransform(bone);
            if (resolved == null && socket == CharacterAppearanceSocket.Chest)
                resolved = animator.GetBoneTransform(HumanBodyBones.Chest);
            return resolved != null ? resolved : fallback;
        }

        private static void Destroy(GameObject value)
        {
            value.SetActive(false);
            if (UnityEngine.Application.isPlaying)
                UnityEngine.Object.Destroy(value);
            else
                UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
