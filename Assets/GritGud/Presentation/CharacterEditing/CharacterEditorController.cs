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
        private const float ToolbarHeight = 52f;
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
        private CharacterPreviewCameraController previewCamera;
        private Vector3 savedCameraPosition;
        private Quaternion savedCameraRotation;
        private Rect savedCameraRect;
        private CameraClearFlags savedClearFlags;
        private Color savedBackground;
        private Vector2 bodyScroll;
        private Vector2 accessoryScroll;
        private string displayNameText = string.Empty;
        private string importPath = string.Empty;
        private string status = "Ready.";
        private float previewYaw = 180f;
        private bool autoRotate;
        private bool hasFramedPreview;
        private ToolbarMenu activeMenu;
        private Action pendingDestructiveAction;
        private string pendingDestructivePrompt = string.Empty;

        private enum ToolbarMenu
        {
            None,
            File,
            Edit,
            View,
        }

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
            previewCamera = null;
            hasFramedPreview = false;
            if (sceneCamera != null)
            {
                sceneCamera.transform.SetPositionAndRotation(
                    savedCameraPosition,
                    savedCameraRotation);
                sceneCamera.clearFlags = savedClearFlags;
                sceneCamera.backgroundColor = savedBackground;
                sceneCamera.rect = savedCameraRect;
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
                UpdateCameraViewport();
                HandleInput(Event.current);
                DrawToolbar();
                GUI.enabled = pendingDestructiveAction == null;
                DrawIdentityAndBodies();
                DrawAccessories();
                GUI.enabled = true;
                DrawViewportHelp();
                DrawStatus();
                DrawActiveMenu();
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
            if (GUILayout.Button(new GUIContent("< MENU", "Return to the main menu"), ToolbarButton(76f)))
            {
                RequestDestructiveAction(
                    "Return to the main menu and discard unsaved character changes?",
                    GameBootstrap.Instance.ReturnToMenu);
            }
            GUI.enabled = interactionsEnabled;
            DrawMenuButton("FILE", ToolbarMenu.File, 56f);
            DrawMenuButton("EDIT", ToolbarMenu.Edit, 56f);
            DrawMenuButton("VIEW", ToolbarMenu.View, 60f);
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                $"{session.CreateSnapshot().displayName}  ·  {(session.IsDirty ? "Unsaved" : "Saved")}",
                styles.ToolbarTitle,
                GUILayout.Height(32f));
            GUI.enabled = interactionsEnabled;
            if (GUILayout.Button(new GUIContent("RANDOMIZE", "Generate a compatible appearance"), ToolbarButton(100f)))
                Randomize();
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = LevelEditorTheme.PrimaryAction;
            if (GUILayout.Button(new GUIContent("SAVE", "Save local draft (Ctrl+S)"), ToolbarButton(68f)))
                SaveDraft();
            GUI.backgroundColor = previous;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.enabled = true;
        }

        private static GUILayoutOption[] ToolbarButton(float width) => new[]
        {
            GUILayout.Width(width),
            GUILayout.Height(32f),
        };

        private void DrawMenuButton(string label, ToolbarMenu menu, float width)
        {
            if (GUILayout.Button(label + " ▾", ToolbarButton(width)))
                activeMenu = activeMenu == menu ? ToolbarMenu.None : menu;
        }

        private void DrawActiveMenu()
        {
            if (activeMenu == ToolbarMenu.None || pendingDestructiveAction != null)
                return;
            float x = activeMenu == ToolbarMenu.File ? 84f : activeMenu == ToolbarMenu.Edit ? 140f : 196f;
            float height = activeMenu == ToolbarMenu.File ? 286f : 148f;
            GUILayout.BeginArea(new Rect(x, ToolbarHeight + 4f, 224f, height), styles.FloatingPanel);
            if (activeMenu == ToolbarMenu.File)
            {
                MenuAction("NEW CHARACTER", true, NewCharacter);
                MenuAction("LOAD DRAFT", PlayerPrefs.HasKey(DraftKey), () => RequestDestructiveAction(
                    "Load the character draft and discard unsaved character changes?", LoadDraft));
                MenuAction("SAVE DRAFT", true, SaveDraft);
                GUILayout.Space(6f);
                bool cloudAvailable = GameBootstrap.Instance.Supabase != null;
                MenuAction("LOAD FROM CLOUD", cloudAvailable, () => RequestDestructiveAction(
                    "Load the cloud character and discard unsaved changes?", LoadFromCloud));
                MenuAction("SAVE TO CLOUD", cloudAvailable, SaveToCloud);
                GUILayout.Space(6f);
                MenuAction("IMPORT JSON", true, Import);
                MenuAction("EXPORT JSON", true, Export);
            }
            else if (activeMenu == ToolbarMenu.Edit)
            {
                MenuAction("UNDO        Ctrl+Z", session.CanUndo, session.Undo);
                MenuAction("REDO        Ctrl+Y", session.CanRedo, session.Redo);
                MenuAction("RANDOMIZE", true, Randomize);
            }
            else
            {
                MenuAction(autoRotate ? "✓ AUTO ROTATE" : "AUTO ROTATE", true, () => autoRotate = !autoRotate);
                MenuAction("RESET VIEW        F", true, ResetPreviewView);
                MenuAction("FRAME CHARACTER", true, FramePreview);
            }
            GUILayout.EndArea();
        }

        private void MenuAction(string label, bool enabled, Action action)
        {
            GUI.enabled = enabled;
            if (GUILayout.Button(label, GUILayout.Height(32f)))
            {
                activeMenu = ToolbarMenu.None;
                action();
            }
            GUI.enabled = true;
        }

        private void NewCharacter() => RequestDestructiveAction(
            "Create a new character and discard unsaved character changes?",
            () => Replace(CreateNewDocument(), false, "Created a new character."));

        private void SaveToCloud()
        {
            CharacterDocument document = session.CreateSnapshot();
            GameBootstrap.Instance.Supabase.SaveCharacter(
                document,
                serializer.Serialize(document),
                message => status = message);
        }

        private void LoadFromCloud()
        {
            string characterId = session.CreateSnapshot().characterId;
            GameBootstrap.Instance.Supabase.LoadCharacter(characterId, text =>
            {
                try
                {
                    CharacterDocument document = serializer.Deserialize(text);
                    RequireValid(document);
                    Replace(document, true, "Loaded the character from cloud.");
                }
                catch (Exception exception) { status = exception.Message; }
            }, error => status = error);
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

        private void DrawViewportHelp()
        {
            Rect viewport = PreviewViewport;
            if (viewport.width < 220f || viewport.height < 80f)
                return;
            GUILayout.BeginArea(
                new Rect(viewport.x + 10f, viewport.y + 10f, 280f, 48f),
                styles.FloatingPanel);
            GUILayout.Label("DRAG TO ORBIT  ·  WHEEL TO ZOOM", styles.MutedLabel);
            GUILayout.Label("F resets and frames the view", styles.MutedLabel);
            GUILayout.EndArea();
        }

        private Rect PreviewViewport => new Rect(
            LeftWidth,
            ToolbarHeight,
            Mathf.Max(0f, Screen.width - LeftWidth - RightWidth),
            Mathf.Max(0f, Screen.height - ToolbarHeight - StatusHeight));

        private void UpdateCameraViewport()
        {
            if (sceneCamera == null || Screen.width <= 0 || Screen.height <= 0)
                return;
            sceneCamera.rect = new Rect(
                LeftWidth / Screen.width,
                StatusHeight / Screen.height,
                Mathf.Max(0.01f, (Screen.width - LeftWidth - RightWidth) / Screen.width),
                Mathf.Max(0.01f, (Screen.height - ToolbarHeight - StatusHeight) / Screen.height));
        }

        private void HandleInput(Event current)
        {
            if (current == null || pendingDestructiveAction != null)
                return;

            if (current.type == EventType.KeyDown)
            {
                if (current.keyCode == KeyCode.Escape && activeMenu != ToolbarMenu.None)
                {
                    activeMenu = ToolbarMenu.None;
                    current.Use();
                }
                else if (current.keyCode == KeyCode.F)
                {
                    ResetPreviewView();
                    current.Use();
                }
                else if ((current.control || current.command) && current.keyCode == KeyCode.S)
                {
                    SaveDraft();
                    current.Use();
                }
                else if ((current.control || current.command) && current.keyCode == KeyCode.Z)
                {
                    if (session.CanUndo)
                        session.Undo();
                    current.Use();
                }
                else if ((current.control || current.command) && current.keyCode == KeyCode.Y)
                {
                    if (session.CanRedo)
                        session.Redo();
                    current.Use();
                }
                return;
            }

            if (!PreviewViewport.Contains(current.mousePosition) || previewCamera == null)
                return;
            if (current.type == EventType.MouseDrag && (current.button == 0 || current.button == 1))
            {
                autoRotate = false;
                previewCamera.Orbit(current.delta);
                current.Use();
            }
            else if (current.type == EventType.ScrollWheel)
            {
                previewCamera.Zoom(-current.delta.y);
                current.Use();
            }
        }

        private void ResetPreviewView()
        {
            autoRotate = false;
            previewYaw = 180f;
            if (preview != null)
                preview.transform.localRotation = Quaternion.Euler(0f, previewYaw, 0f);
            previewCamera?.ResetView();
            status = "Reset the character view.";
        }

        private void FramePreview()
        {
            if (preview == null || previewCamera == null)
                return;
            previewCamera.Frame(preview.GetComponentsInChildren<Renderer>(true));
            status = "Framed the character.";
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
            savedCameraRect = sceneCamera.rect;
            savedClearFlags = sceneCamera.clearFlags;
            savedBackground = sceneCamera.backgroundColor;
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = new Color(0.025f, 0.03f, 0.04f, 1f);
            previewCamera = new CharacterPreviewCameraController(sceneCamera);
            UpdateCameraViewport();

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
            if (!hasFramedPreview)
            {
                previewCamera.Frame(preview.GetComponentsInChildren<Renderer>(true));
                hasFramedPreview = true;
            }
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
