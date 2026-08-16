using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GritGud.Application.Characters;
using GritGud.Domain.Characters;
using GritGud.Domain.Gameplay;
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

        private static readonly (string Id, string Label, string Help)[] Attributes =
        {
            (CoreAttributeIds.Strength, "STRENGTH", "Melee and opposed displacement"),
            (CoreAttributeIds.Dexterity, "DEXTERITY", "Initiative and movement allowance"),
            (CoreAttributeIds.Grit, "GRIT", "Resistance to tactical consequences"),
            (CoreAttributeIds.Charisma, "CHARISMA", "Social resolution"),
        };

        private static readonly (string Id, string Label)[] Skills =
        {
            ("skill.firearms", "FIREARMS"),
            ("skill.demolitions", "DEMOLITIONS"),
            ("skill.fieldcraft", "FIELDCRAFT"),
            (CharacterSkillIds.CloseQuarters, "CLOSE-QUARTERS CONTROL"),
        };

        private static readonly (string Id, string Label)[] Talents =
        {
            ("talent.steady-hands", "STEADY HANDS"),
            ("talent.combat-awareness", "COMBAT AWARENESS"),
            ("talent.leverage", "LEVERAGE"),
        };

        private static readonly (string Id, string Label, bool Stackable)[] LoadoutItems =
        {
            ("weapon.rifle", "RIFLE", false),
            ("weapon.rocket-launcher", "LAUNCHER", false),
            ("weapon.combat-knife", "COMBAT KNIFE", false),
            ("item.frag-grenade", "FRAG GRENADE", true),
            ("item.smoke-grenade", "SMOKE GRENADE", true),
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
        private CharacterWorkspace workspace;
        private Action pendingDestructiveAction;
        private string pendingDestructivePrompt = string.Empty;

        private enum ToolbarMenu
        {
            None,
            File,
            Edit,
            View,
        }

        private enum CharacterWorkspace
        {
            Appearance,
            Attributes,
            Skills,
            Loadout,
            Review,
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
                MenuAction("UNDO        Ctrl+Z", session.CanUndo, () => session.Undo());
                MenuAction("REDO        Ctrl+Y", session.CanRedo, () => session.Redo());
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
            DrawWorkspaceNavigation();
            CharacterDocument document = session.CreateSnapshot();
            if (workspace == CharacterWorkspace.Attributes)
            {
                DrawAttributes(document);
                GUILayout.EndArea();
                return;
            }
            if (workspace == CharacterWorkspace.Skills)
            {
                DrawTalents(document);
                GUILayout.EndArea();
                return;
            }
            if (workspace == CharacterWorkspace.Loadout)
            {
                DrawLoadoutLibrary(document);
                GUILayout.EndArea();
                return;
            }
            if (workspace == CharacterWorkspace.Review)
            {
                DrawReviewSummary(document);
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label("CHARACTER", styles.SectionHeader);
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
            CharacterDocument document = session.CreateSnapshot();
            if (workspace == CharacterWorkspace.Attributes)
            {
                DrawDerivedStatistics(document);
                GUILayout.EndArea();
                return;
            }
            if (workspace == CharacterWorkspace.Skills)
            {
                DrawSkills(document);
                GUILayout.EndArea();
                return;
            }
            if (workspace == CharacterWorkspace.Loadout)
            {
                DrawStartingLoadout(document);
                GUILayout.EndArea();
                return;
            }
            if (workspace == CharacterWorkspace.Review)
            {
                DrawValidationReview(document);
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label("APPEARANCE", styles.SectionHeader);
            GUILayout.Label(
                "Visual choices only. Gameplay stats and equipment are unchanged.",
                styles.MutedLabel);
            accessoryScroll = GUILayout.BeginScrollView(accessoryScroll);
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

        private void DrawWorkspaceNavigation()
        {
            GUILayout.Label("CHARACTER CREATOR", styles.SectionHeader);
            string[] labels = { "APPEARANCE", "ATTRIBUTES", "SKILLS", "LOADOUT", "REVIEW" };
            int selected = GUILayout.Toolbar((int)workspace, labels, GUILayout.Height(30f));
            workspace = (CharacterWorkspace)selected;
            GUILayout.Space(10f);
        }

        private void DrawAttributes(CharacterDocument document)
        {
            GUILayout.Label("IDENTITY & ATTRIBUTES", styles.SectionHeader);
            GUILayout.Label("ARCHETYPE", styles.MutedLabel);
            string archetype = GUILayout.TextField(document.build.archetype);
            if (!string.Equals(archetype, document.build.archetype, StringComparison.Ordinal))
            {
                document.build.archetype = archetype;
                session.Apply("Change character archetype", document);
            }
            GUILayout.Space(8f);
            foreach ((string id, string label, string help) in Attributes)
            {
                GUILayout.Label(label, styles.SectionHeader);
                GUILayout.Label(help, styles.MutedLabel);
                DrawRatingStepper(document, document.build.attributes, id, 1, 5);
            }
        }

        private void DrawDerivedStatistics(CharacterDocument document)
        {
            GUILayout.Label("DERIVED STATISTICS", styles.SectionHeader);
            GUILayout.Label("Calculated from authoritative attributes.", styles.MutedLabel);
            int strength = document.build.GetRating(document.build.attributes, CoreAttributeIds.Strength);
            int dexterity = document.build.GetRating(document.build.attributes, CoreAttributeIds.Dexterity);
            int grit = document.build.GetRating(document.build.attributes, CoreAttributeIds.Grit);
            int charisma = document.build.GetRating(document.build.attributes, CoreAttributeIds.Charisma);
            DrawReadOnlyStat("MOVEMENT ALLOWANCE", $"{4 + dexterity} units");
            DrawReadOnlyStat("REACTION", $"Dexterity {dexterity} determines initiative advance");
            DrawReadOnlyStat("PHYSICAL CONTROL", $"Strength {strength} + Close-Quarters skill");
            DrawReadOnlyStat("RESISTANCE", $"Grit modifier {grit}");
            DrawReadOnlyStat("SOCIAL", $"Charisma modifier {charisma}");
            GUILayout.Space(12f);
            GUILayout.Label("Core ratings are constrained to 1–5. Derived values cannot be edited directly.", styles.MutedLabel);
        }

        private void DrawTalents(CharacterDocument document)
        {
            GUILayout.Label("TALENTS", styles.SectionHeader);
            GUILayout.Label("Authored capabilities that preserve the character's role.", styles.MutedLabel);
            foreach ((string id, string label) in Talents)
            {
                bool selected = document.build.talentIds.Contains(id);
                bool next = GUILayout.Toggle(selected, label, GUI.skin.button, GUILayout.Height(32f));
                if (next == selected)
                    continue;
                if (next)
                    document.build.talentIds.Add(id);
                else
                    document.build.talentIds.Remove(id);
                session.Apply("Change character talent", document);
            }
            GUILayout.Space(12f);
            GUILayout.Label("STARTING PROGRESSION POINTS", styles.SectionHeader);
            DrawIntegerStepper(
                document.build.startingProgressionPoints,
                0,
                20,
                value =>
                {
                    document.build.startingProgressionPoints = value;
                    session.Apply("Change starting progression", document);
                });
        }

        private void DrawSkills(CharacterDocument document)
        {
            GUILayout.Label("STARTING SKILLS", styles.SectionHeader);
            GUILayout.Label("Ratings define the character's recognizable baseline role.", styles.MutedLabel);
            foreach ((string id, string label) in Skills)
            {
                GUILayout.Label(label, styles.SectionHeader);
                DrawRatingStepper(document, document.build.skills, id, 0, 5);
            }
            GUILayout.Space(12f);
            GUILayout.Label("ADVANCEMENT", styles.SectionHeader);
            GUILayout.Label("Constrained advancement options are preserved in the document. Detailed option authoring is the next catalog pass.", styles.MutedLabel);
        }

        private void DrawLoadoutLibrary(CharacterDocument document)
        {
            GUILayout.Label("EQUIPMENT CATALOG", styles.SectionHeader);
            GUILayout.Label("Add reusable item references to the starting loadout.", styles.MutedLabel);
            foreach ((string id, string label, bool stackable) in LoadoutItems)
            {
                bool owned = document.startingLoadout.items.Any(
                    item => item != null && string.Equals(item.itemId, id, StringComparison.Ordinal));
                GUI.enabled = !owned;
                if (GUILayout.Button(owned ? label + "  ·  ADDED" : "+ " + label, GUILayout.Height(32f)))
                {
                    document.startingLoadout.items.Add(new CharacterLoadoutItemData
                    {
                        itemId = id,
                        quantity = stackable ? 1 : 0,
                    });
                    session.Apply("Add starting equipment", document);
                }
                GUI.enabled = true;
            }
            GUILayout.Space(12f);
            GUILayout.Label("Loadout entries reference canonical item IDs; combat rules remain owned by the equipment catalog and scenario assembly.", styles.MutedLabel);
        }

        private void DrawStartingLoadout(CharacterDocument document)
        {
            GUILayout.Label("STARTING LOADOUT", styles.SectionHeader);
            GUILayout.Label("Starting choices only—live quantities, wounds, and equipment state are saved separately.", styles.MutedLabel);
            accessoryScroll = GUILayout.BeginScrollView(accessoryScroll);
            foreach (CharacterLoadoutItemData item in document.startingLoadout.items.ToArray())
            {
                if (item == null)
                    continue;
                (string Id, string Label, bool Stackable) definition =
                    LoadoutItems.FirstOrDefault(value => value.Id == item.itemId);
                string label = definition.Label
                    ?? item.itemId;
                GUILayout.Space(8f);
                GUILayout.Label(label, styles.SectionHeader);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{(definition.Stackable ? "QTY " + item.quantity + "  ·  " : string.Empty)}SLOT {(item.hotbarSlot == 0 ? "—" : item.hotbarSlot.ToString())}");
                if (definition.Stackable && GUILayout.Button("QTY +", GUILayout.Width(58f)))
                {
                    item.quantity++;
                    session.Apply("Change starting quantity", document);
                }
                if (GUILayout.Button("SLOT +", GUILayout.Width(64f)))
                {
                    item.hotbarSlot = (item.hotbarSlot + 1) % (GameplayHotbarRules.SlotCount + 1);
                    session.Apply("Change starting hotbar slot", document);
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                bool equipped = string.Equals(
                    document.startingLoadout.initiallyEquippedItemId,
                    item.itemId,
                    StringComparison.Ordinal);
                if (GUILayout.Button(equipped ? "EQUIPPED" : "EQUIP", GUILayout.Height(28f)))
                {
                    document.startingLoadout.initiallyEquippedItemId = equipped
                        ? string.Empty
                        : item.itemId;
                    session.Apply("Change initially equipped item", document);
                }
                if (GUILayout.Button("REMOVE", GUILayout.Height(28f)))
                {
                    document.startingLoadout.items.Remove(item);
                    if (equipped)
                        document.startingLoadout.initiallyEquippedItemId = string.Empty;
                    session.Apply("Remove starting equipment", document);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        private void DrawReviewSummary(CharacterDocument document)
        {
            GUILayout.Label("BUILD REVIEW", styles.SectionHeader);
            DrawReadOnlyStat("NAME", document.displayName);
            DrawReadOnlyStat("ARCHETYPE", document.build.archetype);
            DrawReadOnlyStat("ATTRIBUTES", string.Join("  ·  ", Attributes.Select(value =>
                value.Label + " " + document.build.GetRating(document.build.attributes, value.Id))));
            DrawReadOnlyStat("SKILLS", document.build.skills.Count.ToString());
            DrawReadOnlyStat("TALENTS", document.build.talentIds.Count.ToString());
            DrawReadOnlyStat("STARTING ITEMS", document.startingLoadout.items.Count.ToString());
            DrawReadOnlyStat("EQUIPPED", string.IsNullOrWhiteSpace(document.startingLoadout.initiallyEquippedItemId)
                ? "None"
                : document.startingLoadout.initiallyEquippedItemId);
        }

        private void DrawValidationReview(CharacterDocument document)
        {
            GUILayout.Label("PUBLISH READINESS", styles.SectionHeader);
            IReadOnlyList<string> issues = CharacterValidator.Validate(
                document,
                catalog.CreateValidationContent());
            if (issues.Count == 0)
            {
                Color previous = GUI.color;
                GUI.color = LevelEditorTheme.Active;
                GUILayout.Label("READY TO PUBLISH", styles.SectionHeader);
                GUI.color = previous;
                GUILayout.Label("Appearance, build, and starting loadout are valid.", styles.MutedLabel);
            }
            else
            {
                GUILayout.Label($"{issues.Count} ISSUE{(issues.Count == 1 ? string.Empty : "S")}", styles.SectionHeader);
                foreach (string issue in issues)
                    GUILayout.Label("• " + issue);
            }
            GUILayout.Space(12f);
            if (GUILayout.Button("EXPORT VALID CHARACTER", GUILayout.Height(36f)))
                Export();
        }

        private void DrawRatingStepper(
            CharacterDocument document,
            List<CharacterRatingData> ratings,
            string id,
            int minimum,
            int maximum)
        {
            int current = document.build.GetRating(ratings, id);
            DrawIntegerStepper(current, minimum, maximum, value =>
            {
                document.build.SetRating(ratings, id, value);
                session.Apply("Change character rating", document);
            });
        }

        private static void DrawIntegerStepper(int current, int minimum, int maximum, Action<int> apply)
        {
            GUILayout.BeginHorizontal();
            GUI.enabled = current > minimum;
            if (GUILayout.Button("−", GUILayout.Width(42f), GUILayout.Height(30f)))
                apply(current - 1);
            GUI.enabled = true;
            GUILayout.Label(current.ToString(), GUILayout.Width(34f));
            GUI.enabled = current < maximum;
            if (GUILayout.Button("+", GUILayout.Width(42f), GUILayout.Height(30f)))
                apply(current + 1);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawReadOnlyStat(string label, string value)
        {
            GUILayout.Space(8f);
            GUILayout.Label(label, styles.MutedLabel);
            GUILayout.Label(value ?? string.Empty);
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
