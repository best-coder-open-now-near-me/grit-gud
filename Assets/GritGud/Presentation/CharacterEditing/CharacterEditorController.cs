using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GritGud.Application.Characters;
using GritGud.Domain.Characters;
using GritGud.Presentation.Bootstrap;
using GritGud.Presentation.Characters;
using GritGud.Presentation.LevelEditing;
using GritGud.Presentation.LevelEditing.UI;
using GritGud.Presentation.Persistence;
using UnityEngine;

namespace GritGud.Presentation.CharacterEditing
{
    public sealed class CharacterEditorController : MonoBehaviour
    {
        private const float ToolbarHeight = 72f;
        private const float StatusHeight = 32f;
        private const float LeftWidth = 310f;
        private const float RightWidth = 410f;
        private const string DraftKey = "grit-gud.character-editor.draft";

        private static readonly (string Id, string Label)[] Slots =
        {
            (CharacterAppearanceSlotIds.Armor, "ARMOR"),
            (CharacterAppearanceSlotIds.Hair, "HAIR"),
            (CharacterAppearanceSlotIds.FacialHair, "FACIAL HAIR"),
            (CharacterAppearanceSlotIds.Face, "FACE ACCESSORY"),
            (CharacterAppearanceSlotIds.Headwear, "HEADWEAR"),
            (CharacterAppearanceSlotIds.Neck, "NECK"),
            (CharacterAppearanceSlotIds.Back, "BACK"),
            (CharacterAppearanceSlotIds.Waist, "WAIST"),
            (CharacterAppearanceSlotIds.Patch, "PATCH"),
        };

        private readonly LevelEditorGuiStyles styles = new LevelEditorGuiStyles();
        private CharacterAppearanceCatalog catalog;
        private UnityCharacterLibrary library;
        private UnityCharacterJsonSerializer serializer;
        private CharacterAuthoringSession session;
        private GameObject stage;
        private GameObject preview;
        private Camera sceneCamera;
        private Vector3 savedCameraPosition;
        private Quaternion savedCameraRotation;
        private CameraClearFlags savedClearFlags;
        private Color savedBackground;
        private Vector2 bodyScroll;
        private Vector2 accessoryScroll;
        private string displayNameText = string.Empty;
        private string importPath = string.Empty;
        private string status = "Ready.";
        private float previewYaw = 180f;
        private bool autoRotate;
        private Action pendingDestructiveAction;
        private string pendingDestructivePrompt = string.Empty;

        public void Begin(CharacterDocument initial = null)
        {
            EndSession();
            catalog = CharacterAppearanceCatalog.LoadDefault();
            library = UnityCharacterLibrary.LoadDefault(catalog);
            serializer = new UnityCharacterJsonSerializer();
            CharacterDocument document = initial?.DeepCopy()
                ?? library.Entries.FirstOrDefault()?.CreateSnapshot()
                ?? CreateNewDocument();
            session = new CharacterAuthoringSession(document, initial != null || library.Entries.Count > 0);
            session.Changed += HandleChanged;
            displayNameText = document.displayName;
            importPath = Path.Combine(
                UnityEngine.Application.persistentDataPath,
                "Imports",
                "character.json");
            CreateStage();
            RefreshPreview();
            enabled = true;
        }

        public void EndSession()
        {
            if (session != null)
                session.Changed -= HandleChanged;
            session = null;
            CancelDestructiveAction();
            DestroyObject(stage);
            stage = null;
            preview = null;
            if (sceneCamera != null)
            {
                sceneCamera.transform.SetPositionAndRotation(
                    savedCameraPosition,
                    savedCameraRotation);
                sceneCamera.clearFlags = savedClearFlags;
                sceneCamera.backgroundColor = savedBackground;
            }
            sceneCamera = null;
            enabled = false;
        }

        private void Update()
        {
            if (preview == null || !autoRotate)
                return;
            previewYaw = Mathf.Repeat(previewYaw + (Time.unscaledDeltaTime * 18f), 360f);
            preview.transform.localRotation = Quaternion.Euler(0f, previewYaw, 0f);
        }

        private void OnGUI()
        {
            if (session == null)
                return;
            GUISkin previousSkin = GUI.skin;
            GUI.skin = styles.ResolveSkin(previousSkin);
            try
            {
                DrawToolbar();
                GUI.enabled = pendingDestructiveAction == null;
                DrawIdentityAndBodies();
                DrawAccessories();
                GUI.enabled = true;
                DrawStatus();
                if (pendingDestructiveAction != null)
                    DrawDestructiveConfirmation();
            }
            finally
            {
                GUI.enabled = true;
                GUI.skin = previousSkin;
            }
        }

        private void DrawToolbar()
        {
            GUILayout.BeginArea(new Rect(0f, 0f, Screen.width, ToolbarHeight), styles.Toolbar);
            GUILayout.BeginHorizontal();
            bool interactionsEnabled = pendingDestructiveAction == null;
            GUI.enabled = interactionsEnabled;
            if (GUILayout.Button("BACK", GUILayout.Width(74f), GUILayout.Height(34f)))
            {
                RequestDestructiveAction(
                    "Return to the main menu and discard unsaved character changes?",
                    GameBootstrap.Instance.ReturnToMenu);
            }
            if (GUILayout.Button("NEW", GUILayout.Width(66f), GUILayout.Height(34f)))
            {
                RequestDestructiveAction(
                    "Create a new character and discard unsaved character changes?",
                    () => Replace(CreateNewDocument(), false, "Created a new character."));
            }
            if (GUILayout.Button("RANDOMIZE", GUILayout.Width(108f), GUILayout.Height(34f)))
                Randomize();
            GUI.enabled = interactionsEnabled && session.CanUndo;
            if (GUILayout.Button("UNDO", GUILayout.Width(70f), GUILayout.Height(34f)))
                session.Undo();
            GUI.enabled = interactionsEnabled && session.CanRedo;
            if (GUILayout.Button("REDO", GUILayout.Width(70f), GUILayout.Height(34f)))
                session.Redo();
            GUI.enabled = interactionsEnabled;
            if (GUILayout.Button("SAVE DRAFT", GUILayout.Width(102f), GUILayout.Height(34f)))
                SaveDraft();
            GUI.enabled = interactionsEnabled && PlayerPrefs.HasKey(DraftKey);
            if (GUILayout.Button("LOAD DRAFT", GUILayout.Width(102f), GUILayout.Height(34f)))
            {
                RequestDestructiveAction(
                    "Load the character draft and discard unsaved character changes?",
                    LoadDraft);
            }
            GUI.enabled = interactionsEnabled;
            if (GUILayout.Button("EXPORT", GUILayout.Width(78f), GUILayout.Height(34f)))
                Export();
            GUILayout.FlexibleSpace();
            autoRotate = GUILayout.Toggle(
                autoRotate,
                "AUTO ROTATE",
                GUI.skin.button,
                GUILayout.Width(112f),
                GUILayout.Height(34f));
            GUILayout.Label(
                session.IsDirty ? "UNSAVED CHARACTER" : "SAVED",
                styles.MutedLabel,
                GUILayout.Height(34f));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.enabled = true;
        }

        private void DrawIdentityAndBodies()
        {
            GUILayout.BeginArea(
                new Rect(0f, ToolbarHeight, LeftWidth, Screen.height - ToolbarHeight - StatusHeight),
                styles.Panel);
            GUILayout.Label("CHARACTER", styles.SectionHeader);
            CharacterDocument document = session.CreateSnapshot();
            GUILayout.Label("ID", styles.MutedLabel);
            GUILayout.Label(document.characterId);
            GUILayout.Label("DISPLAY NAME", styles.MutedLabel);
            displayNameText = GUILayout.TextField(displayNameText);
            if (GUILayout.Button("APPLY NAME", GUILayout.Height(30f)))
            {
                if (string.IsNullOrWhiteSpace(displayNameText))
                    status = "A character needs a display name.";
                else
                {
                    document.displayName = displayNameText.Trim();
                    session.Apply("Rename character", document);
                    status = "Updated the character name.";
                }
            }

            GUILayout.Space(10f);
            GUILayout.Label("PUBLISHED CHARACTERS", styles.SectionHeader);
            foreach (PublishedCharacterEntry entry in library.Entries)
            {
                if (GUILayout.Button(entry.DisplayName, GUILayout.Height(28f)))
                {
                    RequestDestructiveAction(
                        $"Open {entry.DisplayName} and discard unsaved character changes?",
                        () => Replace(
                            entry.CreateSnapshot(),
                            true,
                            $"Opened {entry.DisplayName}."));
                }
            }

            GUILayout.Space(10f);
            GUILayout.Label("BODY", styles.SectionHeader);
            bodyScroll = GUILayout.BeginScrollView(bodyScroll);
            foreach (CharacterBodyPresentationDefinition body in catalog.Bodies)
            {
                Color previous = GUI.backgroundColor;
                if (string.Equals(document.appearance.bodyId, body.Id, StringComparison.Ordinal))
                    GUI.backgroundColor = LevelEditorTheme.Active;
                if (GUILayout.Button(body.DisplayName, GUILayout.Height(30f)))
                    SelectBody(body);
                GUI.backgroundColor = previous;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawAccessories()
        {
            float left = Screen.width - RightWidth;
            GUILayout.BeginArea(
                new Rect(left, ToolbarHeight, RightWidth, Screen.height - ToolbarHeight - StatusHeight),
                styles.Panel);
            GUILayout.Label("APPEARANCE", styles.SectionHeader);
            GUILayout.Label(
                "Visual choices only. Gameplay stats and equipment are unchanged.",
                styles.MutedLabel);
            accessoryScroll = GUILayout.BeginScrollView(accessoryScroll);
            CharacterDocument document = session.CreateSnapshot();
            foreach ((string slotId, string label) in Slots)
            {
                GUILayout.Space(8f);
                GUILayout.Label(label, styles.SectionHeader);
                IReadOnlyList<CharacterAccessoryPresentationDefinition> options =
                    catalog.GetAccessoriesForSlot(slotId, document.appearance.bodyId);
                string selectedId = document.appearance.GetAccessory(slotId);
                CharacterAccessoryPresentationDefinition selected = options.FirstOrDefault(
                    option => string.Equals(option.Id, selectedId, StringComparison.Ordinal));
                GUILayout.Label(selected?.DisplayName ?? "None");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("<", GUILayout.Width(42f), GUILayout.Height(30f)))
                    CycleAccessory(slotId, options, selectedId, -1);
                if (GUILayout.Button("CLEAR", GUILayout.Height(30f)))
                    SetAccessory(slotId, string.Empty);
                if (GUILayout.Button(">", GUILayout.Width(42f), GUILayout.Height(30f)))
                    CycleAccessory(slotId, options, selectedId, 1);
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(12f);
            GUILayout.Label("IMPORT", styles.SectionHeader);
            importPath = GUILayout.TextField(importPath);
            if (GUILayout.Button("IMPORT JSON", GUILayout.Height(32f)))
                Import();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawStatus()
        {
            GUILayout.BeginArea(
                new Rect(LeftWidth, Screen.height - StatusHeight,
                    Screen.width - LeftWidth - RightWidth, StatusHeight),
                styles.StatusBar);
            GUILayout.Label(status ?? string.Empty, styles.MutedLabel);
            GUILayout.EndArea();
        }

        private void DrawDestructiveConfirmation()
        {
            const float width = 500f;
            const float height = 170f;
            Rect panel = new Rect(
                Mathf.Max(8f, (Screen.width - width) * 0.5f),
                Mathf.Max(8f, (Screen.height - height) * 0.5f),
                Mathf.Min(width, Screen.width - 16f),
                height);
            GUILayout.BeginArea(panel, "UNSAVED CHARACTER", styles.FloatingPanel);
            GUILayout.Space(10f);
            GUILayout.Label(pendingDestructivePrompt);
            GUILayout.Label("Save a draft or export first if you want to keep this work.");
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("KEEP EDITING", GUILayout.Height(36f)))
                CancelDestructiveAction();
            if (GUILayout.Button("DISCARD & CONTINUE", GUILayout.Height(36f)))
            {
                Action action = pendingDestructiveAction;
                CancelDestructiveAction();
                action();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void RequestDestructiveAction(string prompt, Action action)
        {
            if (!session.IsDirty)
            {
                action();
                return;
            }
            pendingDestructivePrompt = prompt;
            pendingDestructiveAction = action;
        }

        private void CancelDestructiveAction()
        {
            pendingDestructiveAction = null;
            pendingDestructivePrompt = string.Empty;
        }

        private void SelectBody(CharacterBodyPresentationDefinition body)
        {
            CharacterDocument document = session.CreateSnapshot();
            document.appearance.bodyId = body.Id;
            foreach (CharacterAccessorySelectionData selection in
                document.appearance.accessories.ToArray())
            {
                if (!catalog.CreateValidationContent().IsCompatible(
                        body.Id,
                        selection.accessoryId))
                {
                    document.appearance.SetAccessory(selection.slotId, string.Empty);
                }
            }
            session.Apply("Change character body", document);
            status = $"Selected {body.DisplayName}.";
        }

        private void CycleAccessory(
            string slotId,
            IReadOnlyList<CharacterAccessoryPresentationDefinition> options,
            string selectedId,
            int direction)
        {
            if (options.Count == 0)
            {
                SetAccessory(slotId, string.Empty);
                return;
            }
            int current = -1;
            for (int index = 0; index < options.Count; index++)
            {
                if (string.Equals(options[index].Id, selectedId, StringComparison.Ordinal))
                {
                    current = index;
                    break;
                }
            }
            int next = current < 0
                ? (direction > 0 ? 0 : options.Count - 1)
                : (current + direction + options.Count) % options.Count;
            SetAccessory(slotId, options[next].Id);
        }

        private void SetAccessory(string slotId, string accessoryId)
        {
            CharacterDocument document = session.CreateSnapshot();
            document.appearance.SetAccessory(slotId, accessoryId);
            session.Apply("Change character accessory", document);
            status = string.IsNullOrEmpty(accessoryId)
                ? $"Cleared {slotId}."
                : $"Selected {catalog.GetAccessory(accessoryId).DisplayName}.";
        }

        private void Randomize()
        {
            CharacterDocument document = session.CreateSnapshot();
            document.appearance = CharacterAppearanceGenerator.Generate(
                Environment.TickCount,
                catalog.CreateBodyOptions(),
                catalog.CreateAccessoryOptions());
            session.Apply("Randomize character appearance", document);
            status = "Generated a new appearance.";
        }

        private void SaveDraft()
        {
            string json = serializer.Serialize(session.CreateSnapshot());
            PlayerPrefs.SetString(DraftKey, json);
            PlayerPrefs.Save();
            session.MarkSaved();
            status = "Saved the character draft.";
        }

        private void LoadDraft()
        {
            try
            {
                CharacterDocument document = serializer.Deserialize(PlayerPrefs.GetString(DraftKey));
                RequireValid(document);
                Replace(document, true, "Loaded the character draft.");
            }
            catch (Exception exception)
            {
                status = exception.Message;
            }
        }

        private void Export()
        {
            try
            {
                CharacterDocument document = session.CreateSnapshot();
                RequireValid(document);
                status = TextFileTransfer.Export(
                    document.displayName + ".character.json",
                    serializer.Serialize(document),
                    "application/json;charset=utf-8");
                session.MarkSaved();
            }
            catch (Exception exception)
            {
                status = exception.Message;
            }
        }

        private void Import()
        {
            try
            {
                if (!File.Exists(importPath))
                    throw new FileNotFoundException("The character import file does not exist.", importPath);
                CharacterDocument document = serializer.Deserialize(File.ReadAllText(importPath));
                RequireValid(document);
                Replace(document, false, "Imported the character document.");
            }
            catch (Exception exception)
            {
                status = exception.Message;
            }
        }

        private void RequireValid(CharacterDocument document)
        {
            IReadOnlyList<string> issues = CharacterValidator.Validate(
                document,
                catalog.CreateValidationContent());
            if (issues.Count > 0)
                throw new InvalidOperationException(issues[0]);
        }

        private void Replace(CharacterDocument document, bool saved, string message)
        {
            session.Replace(document, saved);
            displayNameText = document.displayName;
            status = message;
        }

        private void HandleChanged()
        {
            CharacterDocument document = session.CreateSnapshot();
            displayNameText = document.displayName;
            RefreshPreview();
        }

        private void CreateStage()
        {
            sceneCamera = Camera.main
                ?? throw new InvalidOperationException("The character editor needs a Main Camera.");
            savedCameraPosition = sceneCamera.transform.position;
            savedCameraRotation = sceneCamera.transform.rotation;
            savedClearFlags = sceneCamera.clearFlags;
            savedBackground = sceneCamera.backgroundColor;
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = new Color(0.025f, 0.03f, 0.04f, 1f);
            sceneCamera.transform.position = new Vector3(0f, 1.25f, 3.2f);
            sceneCamera.transform.rotation = Quaternion.LookRotation(
                new Vector3(0f, 1.05f, 0f) - sceneCamera.transform.position);

            stage = new GameObject("Character Editor Stage");
            var lightObject = new GameObject("Character Editor Key Light");
            lightObject.transform.SetParent(stage.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.82f, 0.9f, 1f);
            light.intensity = 1.15f;
        }

        private void RefreshPreview()
        {
            if (stage == null || catalog == null || session == null)
                return;
            DestroyObject(preview);
            preview = Instantiate(catalog.PreviewPrefab, stage.transform, false);
            preview.name = "Character Preview";
            preview.transform.localRotation = Quaternion.Euler(0f, previewYaw, 0f);
            foreach (Collider collider in preview.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            CharacterAppearanceProjector.Apply(
                preview,
                session.CreateSnapshot().appearance,
                catalog);
        }

        private static CharacterDocument CreateNewDocument() => new CharacterDocument
        {
            characterId = "character." + Guid.NewGuid().ToString("N"),
            displayName = "New Character",
            appearance = new CharacterAppearanceData(),
        };

        private static void DestroyObject(GameObject value)
        {
            if (value == null)
                return;
            value.SetActive(false);
            if (UnityEngine.Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }
    }
}
