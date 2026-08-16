using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GritGud.Domain.Characters;
using GritGud.Presentation.Characters;
using UnityEditor;
using UnityEngine;

namespace GritGud.Editor
{
    public static class CharacterAppearanceCatalogBuilder
    {
        private const string CatalogPath =
            "Assets/GritGud/Content/Resources/Gameplay/CharacterAppearanceCatalog.asset";
        private const string AttachmentFolder =
            "Assets/Synty/PolygonBattleRoyale/Prefabs/Characters/Attachments";
        private const string PreviewPrefabPath =
            "Assets/Synty/PolygonBattleRoyale/Prefabs/Characters/Character_MilitaryMale_01.prefab";

        private static readonly (string Id, string Name, string Renderer, string Tag)[] Bodies =
        {
            ("body.military-male-01", "Military Male", "Character_MilitaryMale_01", "male"),
            ("body.military-female-01", "Military Female", "Character_MilitaryFemale_01", "female"),
            ("body.mercenary-male-01", "Mercenary Male", "Character_MercenaryMale_01", "male"),
            ("body.mercenary-female-01", "Mercenary Female", "Character_MercenaryFemale_01", "female"),
            ("body.sporty-male-01", "Sporty Male 1", "Character_SportyMale_01", "male"),
            ("body.sporty-male-02", "Sporty Male 2", "Character_SportyMale_02", "male"),
            ("body.sporty-female-01", "Sporty Female 1", "Character_SportyFemale_01", "female"),
            ("body.sporty-female-02", "Sporty Female 2", "Character_SportyFemale_02", "female"),
            ("body.business-male-01", "Business Male", "Character_BusinessMale_01", "male"),
            ("body.redneck-male-01", "Redneck Male", "Character_RedneckMale_01", "male"),
            ("body.topless-male-01", "Topless Male", "Character_ToplessMale_01", "male"),
            ("body.70s-female-01", "70s Female", "Character_70sFemale_01", "female"),
            ("body.goth-female-01", "Goth Female", "Character_GothFemale_01", "female"),
            ("body.sports-bra-female-01", "Sports Bra Female", "Character_SportsBraFemale_01", "female"),
            ("body.ghillie-suit-01", "Ghillie Suit", "Character_GhillieSuit_01", string.Empty),
        };

        [MenuItem("Grit Gud/Content/Rebuild Character Appearance Catalog")]
        public static void Rebuild()
        {
            GameObject previewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PreviewPrefabPath);
            if (previewPrefab == null || !AssetDatabase.IsValidFolder(AttachmentFolder))
            {
                throw new InvalidOperationException(
                    "The installed private-assets overlay is missing the POLYGON Battle Royale "
                    + "character prefabs required to build the character appearance catalog.");
            }

            CharacterBodyPresentationDefinition[] bodies = Bodies.Select(body =>
                    new CharacterBodyPresentationDefinition(
                        body.Id,
                        body.Name,
                        body.Renderer,
                        body.Tag))
                .ToArray();
            var embeddedRendererNames = new HashSet<string>(
                previewPrefab.GetComponentsInChildren<Renderer>(true)
                    .Select(renderer => renderer.name),
                StringComparer.Ordinal);
            foreach (CharacterBodyPresentationDefinition body in bodies)
            {
                if (!embeddedRendererNames.Contains(body.RendererName))
                {
                    throw new InvalidOperationException(
                        $"Private character source prefab '{PreviewPrefabPath}' does not contain "
                        + $"body renderer '{body.RendererName}'.");
                }
            }

            var accessories = new List<CharacterAccessoryPresentationDefinition>();
            string[] attachmentPaths = AssetDatabase.FindAssets(
                    "t:Prefab",
                new[] { AttachmentFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (attachmentPaths.Length == 0)
            {
                throw new InvalidOperationException(
                    $"The private character attachment folder '{AttachmentFolder}' is empty.");
            }

            foreach (string path in attachmentPaths)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (!TryClassify(name, out string slotId, out CharacterAppearanceSocket socket))
                    continue;
                string compatibility = name.Contains("_Male_", StringComparison.Ordinal)
                    ? "male"
                    : name.Contains("_Female_", StringComparison.Ordinal)
                        ? "female"
                        : string.Empty;
                bool armor = string.Equals(slotId, CharacterAppearanceSlotIds.Armor, StringComparison.Ordinal);
                string rendererName = armor
                    ? name.Replace("SM_Chr_", "SM_Char_", StringComparison.Ordinal)
                    : string.Empty;
                if (armor && !embeddedRendererNames.Contains(rendererName))
                {
                    throw new InvalidOperationException(
                        $"Private character source prefab '{PreviewPrefabPath}' does not contain "
                        + $"armor renderer '{rendererName}'.");
                }
                GameObject prefab = armor ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
                accessories.Add(new CharacterAccessoryPresentationDefinition(
                    ToId(name),
                    ToDisplayName(name),
                    slotId,
                    compatibility,
                    armor
                        ? CharacterAccessoryProjectionKind.ToggleRenderer
                        : CharacterAccessoryProjectionKind.AttachPrefab,
                    rendererName,
                    prefab,
                    socket,
                    string.Equals(slotId, CharacterAppearanceSlotIds.Headwear, StringComparison.Ordinal)));
            }

            CharacterAppearanceCatalog catalog = AssetDatabase.LoadAssetAtPath<
                CharacterAppearanceCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterAppearanceCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.ConfigureForAuthoring(
                previewPrefab,
                bodies,
                accessories.ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Rebuilt character appearance catalog with {bodies.Length} bodies and "
                + $"{accessories.Count} accessories.");
        }

        private static bool TryClassify(
            string name,
            out string slotId,
            out CharacterAppearanceSocket socket)
        {
            slotId = string.Empty;
            socket = CharacterAppearanceSocket.Root;
            if (name.Contains("_Armor_", StringComparison.Ordinal))
            {
                slotId = CharacterAppearanceSlotIds.Armor;
                return true;
            }
            if (name.Contains("_Hair_", StringComparison.Ordinal)
                || name.Contains("_Default_Hair_", StringComparison.Ordinal))
            {
                slotId = CharacterAppearanceSlotIds.Hair;
                socket = CharacterAppearanceSocket.Head;
                return true;
            }
            if (name.Contains("_Beard_", StringComparison.Ordinal))
            {
                slotId = CharacterAppearanceSlotIds.FacialHair;
                socket = CharacterAppearanceSocket.Head;
                return true;
            }
            if (name.Contains("_Hat_", StringComparison.Ordinal)
                || name.Contains("_Helmet_", StringComparison.Ordinal))
            {
                slotId = CharacterAppearanceSlotIds.Headwear;
                socket = CharacterAppearanceSocket.Head;
                return true;
            }
            if (ContainsAny(name, "_Earmuffs_", "_Eyepatch_", "_Facemask_", "_GasMask_", "_Glasses_"))
            {
                slotId = CharacterAppearanceSlotIds.Face;
                socket = CharacterAppearanceSocket.Head;
                return true;
            }
            if (name.Contains("_Scarf_", StringComparison.Ordinal))
            {
                slotId = CharacterAppearanceSlotIds.Neck;
                socket = CharacterAppearanceSocket.Neck;
                return true;
            }
            if (ContainsAny(name, "_Bag_", "_Bedroll_"))
            {
                slotId = CharacterAppearanceSlotIds.Back;
                socket = CharacterAppearanceSocket.Chest;
                return true;
            }
            if (ContainsAny(name, "_Ammo_", "_Pouch_"))
            {
                slotId = CharacterAppearanceSlotIds.Waist;
                socket = CharacterAppearanceSocket.Hips;
                return true;
            }
            if (name.Contains("_Patch_", StringComparison.Ordinal))
            {
                slotId = CharacterAppearanceSlotIds.Patch;
                socket = CharacterAppearanceSocket.RightShoulder;
                return true;
            }
            return false;
        }

        private static bool ContainsAny(string value, params string[] tokens) =>
            tokens.Any(token => value.Contains(token, StringComparison.Ordinal));

        private static string ToId(string name) => "accessory."
            + name.Replace("SM_Chr_Attach_", string.Empty, StringComparison.Ordinal)
                .Replace("SM_Chr_", string.Empty, StringComparison.Ordinal)
                .Replace('_', '-')
                .ToLowerInvariant();

        private static string ToDisplayName(string name)
        {
            string value = name.Replace("SM_Chr_Attach_", string.Empty, StringComparison.Ordinal)
                .Replace("SM_Chr_", string.Empty, StringComparison.Ordinal)
                .Replace('_', ' ');
            return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                value.ToLowerInvariant());
        }
    }
}
