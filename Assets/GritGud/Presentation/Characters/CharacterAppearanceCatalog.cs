using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Application.Characters;
using GritGud.Domain.Characters;
using UnityEngine;

namespace GritGud.Presentation.Characters
{
    public enum CharacterAppearanceSocket
    {
        Root,
        Head,
        Neck,
        Chest,
        Hips,
        LeftShoulder,
        RightShoulder,
    }

    public enum CharacterAccessoryProjectionKind
    {
        ToggleRenderer,
        AttachPrefab,
    }

    [Serializable]
    public sealed class CharacterBodyPresentationDefinition
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private string rendererName = string.Empty;
        [SerializeField] private string compatibilityTag = string.Empty;

        public CharacterBodyPresentationDefinition(
            string bodyId,
            string bodyDisplayName,
            string bodyRendererName,
            string bodyCompatibilityTag)
        {
            id = bodyId ?? string.Empty;
            displayName = bodyDisplayName ?? string.Empty;
            rendererName = bodyRendererName ?? string.Empty;
            compatibilityTag = bodyCompatibilityTag ?? string.Empty;
        }

        public string Id => id?.Trim() ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
        public string RendererName => rendererName?.Trim() ?? string.Empty;
        public string CompatibilityTag => compatibilityTag?.Trim() ?? string.Empty;
    }

    [Serializable]
    public sealed class CharacterAccessoryPresentationDefinition
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private string slotId = string.Empty;
        [SerializeField] private string compatibilityTag = string.Empty;
        [SerializeField] private CharacterAccessoryProjectionKind projectionKind;
        [SerializeField] private string rendererName = string.Empty;
        [SerializeField] private GameObject prefab;
        [SerializeField] private CharacterAppearanceSocket socket;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField] private Vector3 localScale = Vector3.one;
        [SerializeField] private bool hidesHair;

        public CharacterAccessoryPresentationDefinition(
            string accessoryId,
            string accessoryDisplayName,
            string accessorySlotId,
            string accessoryCompatibilityTag,
            CharacterAccessoryProjectionKind kind,
            string embeddedRendererName,
            GameObject accessoryPrefab,
            CharacterAppearanceSocket attachmentSocket,
            bool hideHair)
        {
            id = accessoryId ?? string.Empty;
            displayName = accessoryDisplayName ?? string.Empty;
            slotId = accessorySlotId ?? string.Empty;
            compatibilityTag = accessoryCompatibilityTag ?? string.Empty;
            projectionKind = kind;
            rendererName = embeddedRendererName ?? string.Empty;
            prefab = accessoryPrefab;
            socket = attachmentSocket;
            localScale = Vector3.one;
            hidesHair = hideHair;
        }

        public string Id => id?.Trim() ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
        public string SlotId => slotId?.Trim() ?? string.Empty;
        public string CompatibilityTag => compatibilityTag?.Trim() ?? string.Empty;
        public CharacterAccessoryProjectionKind ProjectionKind => projectionKind;
        public string RendererName => rendererName?.Trim() ?? string.Empty;
        public GameObject Prefab => prefab;
        public CharacterAppearanceSocket Socket => socket;
        public Vector3 LocalPosition => localPosition;
        public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);
        public Vector3 LocalScale => localScale == Vector3.zero ? Vector3.one : localScale;
        public bool HidesHair => hidesHair;
    }

    [CreateAssetMenu(
        fileName = "CharacterAppearanceCatalog",
        menuName = "Grit Gud/Character Appearance Catalog")]
    public sealed class CharacterAppearanceCatalog : ScriptableObject
    {
        public const string DefaultResourceName = "Gameplay/CharacterAppearanceCatalog";

        [SerializeField] private CharacterBodyPresentationDefinition[] bodies =
            Array.Empty<CharacterBodyPresentationDefinition>();
        [SerializeField] private CharacterAccessoryPresentationDefinition[] accessories =
            Array.Empty<CharacterAccessoryPresentationDefinition>();
        [SerializeField] private GameObject previewPrefab;

        private Dictionary<string, CharacterBodyPresentationDefinition> bodyIndex;
        private Dictionary<string, CharacterAccessoryPresentationDefinition> accessoryIndex;

        public IReadOnlyList<CharacterBodyPresentationDefinition> Bodies => bodies;
        public IReadOnlyList<CharacterAccessoryPresentationDefinition> Accessories => accessories;
        public GameObject PreviewPrefab => previewPrefab;

        public static CharacterAppearanceCatalog LoadDefault()
        {
            CharacterAppearanceCatalog catalog = Resources.Load<CharacterAppearanceCatalog>(
                DefaultResourceName);
            if (catalog == null)
                throw new InvalidOperationException(
                    $"Character appearance catalog '{DefaultResourceName}' was not found.");
            catalog.Validate();
            return catalog;
        }

        public CharacterBodyPresentationDefinition GetBody(string bodyId)
        {
            EnsureIndex();
            return bodyIndex.TryGetValue(bodyId ?? string.Empty, out var definition)
                ? definition
                : throw new KeyNotFoundException($"Character body '{bodyId}' is unavailable.");
        }

        public CharacterAccessoryPresentationDefinition GetAccessory(string accessoryId)
        {
            EnsureIndex();
            return accessoryIndex.TryGetValue(accessoryId ?? string.Empty, out var definition)
                ? definition
                : throw new KeyNotFoundException(
                    $"Character accessory '{accessoryId}' is unavailable.");
        }

        public IReadOnlyList<CharacterAccessoryPresentationDefinition> GetAccessoriesForSlot(
            string slotId,
            string bodyId)
        {
            string compatibility = GetBody(bodyId).CompatibilityTag;
            return accessories.Where(accessory => accessory != null
                    && string.Equals(accessory.SlotId, slotId, StringComparison.Ordinal)
                    && (string.IsNullOrEmpty(accessory.CompatibilityTag)
                        || string.Equals(
                            accessory.CompatibilityTag,
                            compatibility,
                            StringComparison.Ordinal)))
                .OrderBy(accessory => accessory.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public CharacterAppearanceValidationContent CreateValidationContent() =>
            new CharacterAppearanceValidationContent(
                bodies.Where(body => body != null).Select(body =>
                    new KeyValuePair<string, string>(body.Id, body.CompatibilityTag)),
                accessories.Where(accessory => accessory != null).Select(accessory =>
                    new CharacterAccessoryValidationEntry(
                        accessory.Id,
                        accessory.SlotId,
                        accessory.CompatibilityTag)));

        public IReadOnlyList<CharacterAuthoringOption> CreateBodyOptions() =>
            bodies.Where(body => body != null)
                .Select(body => new CharacterAuthoringOption(
                    body.Id,
                    string.Empty,
                    body.CompatibilityTag))
                .ToArray();

        public IReadOnlyList<CharacterAuthoringOption> CreateAccessoryOptions() =>
            accessories.Where(accessory => accessory != null)
                .Select(accessory => new CharacterAuthoringOption(
                    accessory.Id,
                    accessory.SlotId,
                    accessory.CompatibilityTag))
                .ToArray();

        public void ConfigureForAuthoring(
            GameObject characterPreviewPrefab,
            CharacterBodyPresentationDefinition[] bodyDefinitions,
            CharacterAccessoryPresentationDefinition[] accessoryDefinitions)
        {
            previewPrefab = characterPreviewPrefab;
            bodies = bodyDefinitions ?? Array.Empty<CharacterBodyPresentationDefinition>();
            accessories = accessoryDefinitions
                ?? Array.Empty<CharacterAccessoryPresentationDefinition>();
            bodyIndex = null;
            accessoryIndex = null;
            Validate();
        }

        public void Validate() => EnsureIndex();

        private void OnEnable()
        {
            bodyIndex = null;
            accessoryIndex = null;
        }

        private void EnsureIndex()
        {
            if (bodyIndex != null && accessoryIndex != null)
                return;
            bodyIndex = new Dictionary<string, CharacterBodyPresentationDefinition>(
                StringComparer.Ordinal);
            foreach (CharacterBodyPresentationDefinition body in bodies)
            {
                if (body == null || string.IsNullOrWhiteSpace(body.Id)
                    || string.IsNullOrWhiteSpace(body.RendererName))
                {
                    throw new InvalidOperationException(
                        "Character bodies require an ID and renderer name.");
                }
                if (!bodyIndex.TryAdd(body.Id, body))
                    throw new InvalidOperationException($"Character body '{body.Id}' is duplicated.");
            }
            if (bodyIndex.Count == 0)
                throw new InvalidOperationException("The character catalog needs at least one body.");
            if (previewPrefab == null)
                throw new InvalidOperationException("The character catalog needs a preview prefab.");

            accessoryIndex = new Dictionary<string, CharacterAccessoryPresentationDefinition>(
                StringComparer.Ordinal);
            foreach (CharacterAccessoryPresentationDefinition accessory in accessories)
            {
                if (accessory == null || string.IsNullOrWhiteSpace(accessory.Id)
                    || string.IsNullOrWhiteSpace(accessory.SlotId))
                {
                    throw new InvalidOperationException(
                        "Character accessories require an ID and slot.");
                }
                if (accessory.ProjectionKind == CharacterAccessoryProjectionKind.AttachPrefab
                    && accessory.Prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Character accessory '{accessory.Id}' needs a prefab.");
                }
                if (accessory.ProjectionKind == CharacterAccessoryProjectionKind.ToggleRenderer
                    && string.IsNullOrWhiteSpace(accessory.RendererName))
                {
                    throw new InvalidOperationException(
                        $"Character accessory '{accessory.Id}' needs a renderer name.");
                }
                if (!accessoryIndex.TryAdd(accessory.Id, accessory))
                    throw new InvalidOperationException(
                        $"Character accessory '{accessory.Id}' is duplicated.");
            }
        }
    }
}
