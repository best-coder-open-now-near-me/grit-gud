using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayHud : MonoBehaviour
    {
        internal const float CommandBarMargin =
            GameplayHudLayout.CommandBarMargin;
        internal const float CommandBarSideRailWidth =
            GameplayHudLayout.CommandBarSideRailWidth;
        internal const int CommandHintRowCapacity =
            GameplayHudLayout.CommandHintRowCapacity;
        internal const float CommandHintRowHeight =
            GameplayHudLayout.CommandHintRowHeight;
        internal const float CommandHintRowGap =
            GameplayHudLayout.CommandHintRowGap;
        internal const float SideRailSectionGap =
            GameplayHudLayout.SideRailSectionGap;
        internal const float TurnModeButtonTop =
            GameplayHudLayout.TurnModeButtonTop;
        internal const float TurnModeButtonHeight =
            GameplayHudLayout.TurnModeButtonHeight;
        internal const float TurnResourceTop =
            GameplayHudLayout.TurnResourceTop;
        internal const float EquipmentFlyoutTop =
            GameplayHudLayout.EquipmentFlyoutTop;
        internal const float WarningHintHeight =
            GameplayHudLayout.WarningHintHeight;
        internal const float WarningHintGap = GameplayHudLayout.WarningHintGap;
        internal const float PendingPowerPulseCyclesPerSecond = 1.25f;
        internal const float PendingPowerPulseMinimumAlpha = 0.48f;
        internal const int HotbarSlotCount = GameplayHudLayout.HotbarSlotCount;
        private static readonly Color PanelStrongColor =
            GameplayVisualPalette.HudPanel;
        private static readonly Color BorderColor =
            GameplayVisualPalette.HudBorder;
        private static readonly Color SignalColor =
            GameplayVisualPalette.HudPrimarySignal;
        private static readonly Color SignalSoftColor = GameplayVisualPalette.WithAlpha(
            GameplayVisualPalette.HudPrimarySignal,
            0.16f);
        private static readonly Color EquipmentSignalColor =
            GameplayVisualPalette.HudSecondarySignal;
        private static readonly Color ModeButtonEdgeColor = GameplayVisualPalette.WithAlpha(
            GameplayVisualPalette.HudPrimarySignal,
            0.48f);
        private static readonly Color ModeButtonTextColor =
            GameplayVisualPalette.HudTextBright;
        private static readonly Color PrimaryTextColor =
            GameplayVisualPalette.HudTextPrimary;
        private static readonly Color SecondaryTextColor =
            GameplayVisualPalette.HudTextSecondary;

        private readonly GameplayHudBindings bindings =
            new GameplayHudBindings();
        private GameplayHudModelProjector modelProjector;
        private GameplayHudModelProjector ModelProjector =>
            modelProjector ??= new GameplayHudModelProjector(bindings);
        private TurnMovementController turnMovement => bindings.TurnMovement;
        private GameplayActionController actionController =>
            bindings.ActionController;
        private GameplayAttackController attackController =>
            bindings.AttackController;
        private GameplayEquipmentController equipmentController =>
            bindings.EquipmentController;
        private GameplayHotbarController hotbarController =>
            bindings.HotbarController;
        private GameplayProjectileController projectileController =>
            bindings.ProjectileController;
        private GameplayGuidanceCatalog guidanceCatalog;
        private GameplayTipCatalog tipCatalog;
        private GameplayFlyoutMotionProfile flyoutMotion;
        private string bugReportStatus => bindings.BugReportStatus;
        private bool bugReportNoteOpen => bindings.BugReportNoteOpen;
        private string bugReportNote
        {
            get => bindings.BugReportNote;
            set => bindings.BugReportNote = value;
        }
        private GUIStyle headerStyle;
        private GUIStyle bodyStyle;
        private GUIStyle guidanceStyle;
        private GUIStyle controlsStyle;
        private GUIStyle commandHintsStyle;
        private GUIStyle bodyRegionStyle;
        private GUIStyle woundedBodyRegionStyle;
        private GUIStyle buttonStyle;
        private GUIStyle statusStyle;
        private GUIStyle tabStyle;
        private GUIStyle resourceLabelStyle;
        private GUIStyle resourceValueStyle;
        private GUIStyle hotbarNumberStyle;
        private GUIStyle hotbarItemStyle;
        private GUIStyle pendingPowerButtonStyle;
        private GUIStyle equipmentButtonStyle;
        private GUIStyle equippedButtonStyle;
        private GUIStyle equipmentConfirmationStyle;
        private GUIStyle tooltipStyle;
        private GUIStyle confirmationFlyoutStyle;
        private GUIStyle warningHintStyle;
        private GUIStyle choiceHeaderStyle;
        private GUIStyle tipTitleStyle;
        private GUIStyle tipBodyStyle;
        private GUIStyle modeButtonStyle;
        private Texture2D whiteTexture;
        private Texture2D buttonNormalTexture;
        private Texture2D buttonHoverTexture;
        private Texture2D buttonActiveTexture;
        private Texture2D equipmentConfirmationTexture;
        private GameplayHudTextureSet textureSet;
        private bool flyoutExpanded;
        private float flyoutReveal;
        private float equipmentFlyoutReveal;
        private float warningHintReveal;
        private float actorAbilityFlyoutReveal;
        private string revealingEquipmentItemId;
        private string revealingWarningSignature;
        private string cachedEquipmentFlyoutText = string.Empty;
        private string activeTooltip = string.Empty;
        private readonly GameplayHotbarChoiceState hotbarChoice =
            new GameplayHotbarChoiceState();
        private Rect actorAbilityFlyoutRectangle;
        private int cachedActorAbilitySlotNumber;
        private string cachedActorAbilityId;
        private string cachedActorAbilityLabel;
        private IReadOnlyList<GameplayHotbarAbilityOptionModel>
            cachedActorAbilityOptions =
                Array.Empty<GameplayHotbarAbilityOptionModel>();
        private Vector2 tipsScrollPosition;
        private string playerActorId => bindings.PlayerActorId;

        public GameplaySession Session => bindings.Session;

        public TurnMovementController TurnMovement => turnMovement;

        public GameplayActionController ActionController => actionController;

        public GameplayAttackController AttackController => attackController;

        public GameplayEquipmentController EquipmentController =>
            equipmentController;

        public GameplayProjectileController ProjectileController =>
            projectileController;

        public bool IsVisible => enabled;

        internal bool IsFlyoutExpanded => flyoutExpanded;

        internal bool IsBugReportNoteOpen => bugReportNoteOpen;

        internal bool IsHotbarChoiceOpen => hotbarChoice.IsOpen;

        internal bool IsCommandBarVisible => Session != null;

        internal bool ContainsInteractiveScreenPoint(Vector2 screenPoint)
            => GameplayHudLayout.ContainsInteractiveScreenPoint(
                screenPoint,
                Screen.width,
                Screen.height,
                bugReportNoteOpen,
                hotbarChoice.IsOpen,
                hotbarChoice.Rectangle,
                actorAbilityFlyoutReveal,
                actorAbilityFlyoutRectangle,
                flyoutExpanded);

        internal bool AreTurnResourcesVisible =>
            Session?.Mode == GameplaySessionMode.TurnBased;

        internal bool IsInteractionPromptVisible =>
            CurrentModel?.InteractionAvailable == true;

        internal bool IsEndTurnAvailable =>
            CurrentModel?.CommandBar.FindCommand(GameplayControl.EndTurn)?.Enabled
            == true;

        internal GameplayHudModel CurrentModel =>
            ModelProjector.Build();

        internal string CurrentGuidanceId =>
            GameplayGuidanceSelector.Select(Session, turnMovement);

        internal GameplayGuidanceEntry CurrentGuidanceEntry
        {
            get
            {
                string guidanceId = CurrentGuidanceId;
                return guidanceId == null || guidanceCatalog == null
                    ? null
                    : guidanceCatalog.Require(guidanceId);
            }
        }

        private void Awake()
        {
            guidanceCatalog = GameplayGuidanceCatalog.LoadDefault();
            tipCatalog = GameplayTipCatalog.LoadDefault();
            flyoutMotion = GameplayFlyoutMotionProfile.LoadDefault();
        }

        public void BindSession(
            GameplaySession session,
            string authoritativePlayerActorId,
            GameplayScenarioAssembly scenarioAssembly) =>
            bindings.BindSession(
                session,
                authoritativePlayerActorId,
                scenarioAssembly);

        public void SetActor(string authoritativePlayerActorId) =>
            bindings.SetActor(authoritativePlayerActorId);

        public void UnbindSession() => bindings.UnbindSession();

        public void BindTurnMovement(TurnMovementController controller) =>
            bindings.BindTurnMovement(controller);

        public void UnbindTurnMovement() => bindings.UnbindTurnMovement();

        public void BindGameplayActions(GameplayActionController controller) =>
            bindings.BindGameplayActions(controller);

        public void UnbindGameplayActions() =>
            bindings.UnbindGameplayActions();

        public void BindGameplayAttack(GameplayAttackController controller) =>
            bindings.BindGameplayAttack(controller);

        public void UnbindGameplayAttack() => bindings.UnbindGameplayAttack();

        public void BindGameplayEquipment(GameplayEquipmentController controller)
            => bindings.BindGameplayEquipment(controller);

        public void UnbindGameplayEquipment() =>
            bindings.UnbindGameplayEquipment();

        public void BindGameplayHotbar(GameplayHotbarController controller) =>
            bindings.BindGameplayHotbar(controller);

        public void UnbindGameplayHotbar() => bindings.UnbindGameplayHotbar();

        internal void BindGameplayConsumables(
            GameplayConsumableController controller) =>
            bindings.BindGameplayConsumables(controller);

        internal void UnbindGameplayConsumables() =>
            bindings.UnbindGameplayConsumables();

        internal void BindGameplayWeaponTargeting(
            GameplayWeaponTargetingController controller) =>
            bindings.BindGameplayWeaponTargeting(controller);

        internal void UnbindGameplayWeaponTargeting() =>
            bindings.UnbindGameplayWeaponTargeting();

        public void BindWarningHintSource(IGameplayWarningHintSource source) =>
            bindings.BindWarningHintSource(source);

        public void UnbindWarningHintSource(
            IGameplayWarningHintSource source) =>
            bindings.UnbindWarningHintSource(source);

        public void BindGameplayProjectile(GameplayProjectileController controller)
            => bindings.BindGameplayProjectile(controller);

        public void BindGameplayDisplacement(
            GameplayDisplacementController controller) =>
            bindings.BindGameplayDisplacement(controller);

        public void UnbindGameplayDisplacement() =>
            bindings.UnbindGameplayDisplacement();

        public void UnbindGameplayProjectile() =>
            bindings.UnbindGameplayProjectile();

        public void BindInputSource(IGameplayInputSource source) =>
            bindings.BindInputSource(source);

        public void UnbindInputSource() => bindings.UnbindInputSource();

        public void BindTurnModeToggle(Action toggleRequested) =>
            bindings.BindTurnModeToggle(toggleRequested);

        public void UnbindTurnModeToggle() =>
            bindings.UnbindTurnModeToggle();

        public void BindBugReportExport(Action<string> exportRequested) =>
            bindings.BindBugReportExport(exportRequested);

        public void UnbindBugReportExport() =>
            bindings.UnbindBugReportExport();

        public void SetBugReportStatus(string status) =>
            bindings.SetBugReportStatus(status);

        public void OpenBugReportNote() => bindings.OpenBugReportNote();

        internal void SubmitBugReportNote(string note) =>
            bindings.SubmitBugReportNote(note);

        public void CancelBugReportNote() => bindings.CancelBugReportNote();

        public void Show()
        {
            ResetTransientState();
            enabled = true;
        }

        public void Hide()
        {
            ResetTransientState();
            enabled = false;
        }

        private void ResetTransientState()
        {
            flyoutExpanded = false;
            flyoutReveal = 0f;
            equipmentFlyoutReveal = 0f;
            warningHintReveal = 0f;
            actorAbilityFlyoutReveal = 0f;
            revealingEquipmentItemId = null;
            revealingWarningSignature = null;
            cachedEquipmentFlyoutText = string.Empty;
            hotbarChoice.Close();
            ClearCachedActorAbilityFlyout();
        }

        internal void ToggleFlyout()
        {
            flyoutExpanded = !flyoutExpanded;
        }

        internal void RequestTurnModeToggle()
        {
            bindings.RequestTurnModeToggle();
        }

        internal void RequestEndTurn()
        {
            bindings.RequestEndTurn();
        }

        private void Update()
        {
            EnsureFlyoutMotion();
            flyoutReveal = flyoutMotion.Advance(
                flyoutReveal,
                flyoutExpanded,
                Time.unscaledDeltaTime);

            string pendingItemId = equipmentController?.PendingItemId;
            if (pendingItemId != null
                && !string.Equals(
                    pendingItemId,
                    revealingEquipmentItemId,
                    StringComparison.Ordinal))
            {
                revealingEquipmentItemId = pendingItemId;
                equipmentFlyoutReveal = 0f;
            }

            equipmentFlyoutReveal = flyoutMotion.Advance(
                equipmentFlyoutReveal,
                pendingItemId != null,
                Time.unscaledDeltaTime);
            if (pendingItemId == null && equipmentFlyoutReveal <= 0f)
            {
                revealingEquipmentItemId = null;
                cachedEquipmentFlyoutText = string.Empty;
            }

            GameplayWarningHintModel warningHint =
                ModelProjector.ResolveWarningHint();
            string warningSignature = warningHint == null
                ? null
                : warningHint.SourceId + "\n" + warningHint.Text;
            if (warningSignature != null
                && !string.Equals(
                    warningSignature,
                    revealingWarningSignature,
                    StringComparison.Ordinal))
            {
                revealingWarningSignature = warningSignature;
                warningHintReveal = 0f;
            }

            warningHintReveal = flyoutMotion.Advance(
                warningHintReveal,
                warningHint != null,
                Time.unscaledDeltaTime);
            if (warningHint == null && warningHintReveal <= 0f)
            {
                revealingWarningSignature = null;
            }

            actorAbilityFlyoutReveal = flyoutMotion.Advance(
                actorAbilityFlyoutReveal,
                hotbarController?.HasExpandedActorAbility == true,
                Time.unscaledDeltaTime);
            if (actorAbilityFlyoutReveal <= 0f
                && hotbarController?.HasExpandedActorAbility != true)
            {
                ClearCachedActorAbilityFlyout();
            }

        }

        private void RequestControl(GameplayControl control)
        {
            switch (control)
            {
                case GameplayControl.ToggleTurnMode:
                    RequestTurnModeToggle();
                    break;
                case GameplayControl.EndTurn:
                    RequestEndTurn();
                    break;
                default:
                    throw new InvalidOperationException(
                        $"HUD control '{control}' is not a primary command.");
            }
        }

        private string FormatHint(GameplayCommandHintModel hint) =>
            ModelProjector.FormatCommandHint(hint);

        private string GetBindingDisplay(GameplayControl control) =>
            bindings.GetBindingDisplay(control);

        private void OnGUI()
        {
            EnsureStyles();

            float uiScale = GameplayHudLayout.CalculateUiScale(Screen.height);
            float canvasWidth = Screen.width / uiScale;
            float canvasHeight = Screen.height / uiScale;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));

            GameplayHudModel model = CurrentModel;
            if (model == null)
            {
                GUI.matrix = previousMatrix;
                return;
            }

            if (bugReportNoteOpen)
            {
                DrawBugReportNoteModal(canvasWidth, canvasHeight);
                GUI.matrix = previousMatrix;
                return;
            }

            activeTooltip = string.Empty;
            Rect commandBarRectangle = CalculateCommandBarRectangle(
                canvasWidth,
                canvasHeight);
            DrawCommandBar(
                commandBarRectangle,
                model.CommandBar);
            DrawActorAbilityFlyout(
                commandBarRectangle,
                model.CommandBar.HotbarSlots);
            DrawHotbarChoiceMenu(canvasWidth, canvasHeight);
            DrawBodyStatus(
                CalculateBodyStatusRectangle(
                    canvasWidth,
                    commandBarRectangle),
                model.CommandBar.BodyStatus);
            DrawCommandHints(
                CalculateCommandHintsRectangle(
                    commandBarRectangle),
                model.CommandBar.Hints);
            DrawEquipmentConfirmationFlyout(
                commandBarRectangle,
                model.CommandBar.HotbarSlots,
                canvasWidth);
            DrawWarningHint(
                commandBarRectangle,
                model.CommandBar.WarningHint);
            DrawAnimatedFlyout(
                canvasWidth,
                canvasHeight,
                model.ScenarioDisplayName,
                model.ModeLabel,
                model.ObjectiveSummary);
            DrawInteractionPrompt(
                canvasWidth * 0.5f,
                canvasHeight * 0.5f,
                model.InteractionAvailable);
            DrawTooltip(canvasWidth, canvasHeight);

            GUI.matrix = previousMatrix;
        }

        internal static Rect CalculateCommandBarRectangle(
            float canvasWidth,
            float canvasHeight) =>
            GameplayHudLayout.CalculateCommandBarRectangle(
                canvasWidth,
                canvasHeight);

        internal static Rect CalculateDialogueButtonRectangle(
            float canvasWidth,
            Rect commandBarRectangle) =>
            GameplayHudLayout.CalculateDialogueButtonRectangle(
                canvasWidth,
                commandBarRectangle);

        internal static Rect CalculateHotbarRectangle(
            Rect commandBarRectangle,
            float x,
            float width) =>
            GameplayHudLayout.CalculateHotbarRectangle(
                commandBarRectangle,
                x,
                width);

        internal static Rect CalculateHotbarLayoutRectangle(
            Rect commandBarRectangle) =>
            GameplayHudLayout.CalculateHotbarLayoutRectangle(
                commandBarRectangle);

        internal static Rect CalculateEquipmentFlyoutRectangle(
            Rect commandBarRectangle) =>
            GameplayHudLayout.CalculateEquipmentFlyoutRectangle(
                commandBarRectangle);

        internal static Rect CalculateWarningHintRectangle(
            Rect commandBarRectangle) =>
            GameplayHudLayout.CalculateWarningHintRectangle(
                commandBarRectangle);

        internal static Rect CalculateBodyStatusRectangle(
            float canvasWidth,
            Rect commandBarRectangle) =>
            GameplayHudLayout.CalculateBodyStatusRectangle(
                canvasWidth,
                commandBarRectangle);

        internal static Rect CalculateCommandHintsRectangle(
            Rect commandBarRectangle) =>
            GameplayHudLayout.CalculateCommandHintsRectangle(
                commandBarRectangle);

        internal static float CalculateCommandHintContentHeight(int rowCount) =>
            GameplayHudLayout.CalculateCommandHintContentHeight(rowCount);

        internal static Rect CalculateCommandHintRowRectangle(
            Rect rectangle,
            int rowIndex,
            int rowCount) =>
            GameplayHudLayout.CalculateCommandHintRowRectangle(
                rectangle,
                rowIndex,
                rowCount);

        internal static Rect CalculateBodyRegionRectangle(
            Rect bodyStatusRectangle,
            TargetRegionId region) =>
            GameplayHudLayout.CalculateBodyRegionRectangle(
                bodyStatusRectangle,
                region);

        private void DrawCommandBar(
            Rect rectangle,
            GameplayCommandBarModel model)
        {
            bool compact = rectangle.width < 680f;
            DrawFramedPanel(rectangle, PanelStrongColor);
            DrawGlowLine(new Rect(rectangle.x, rectangle.y, rectangle.width, 2f), SignalColor);

            const float padding = CommandBarMargin;
            float contentWidth = rectangle.width - (padding * 2f);
            float turnAreaWidth = compact
                ? Mathf.Clamp(contentWidth * 0.36f, 210f, 245f)
                : 320f;
            const float separatorSpacing = 15f;
            Rect hotbarRectangle = CalculateHotbarLayoutRectangle(rectangle);
            float separatorX = hotbarRectangle.xMax + separatorSpacing;
            float turnAreaX = separatorX + separatorSpacing;

            DrawHotbar(hotbarRectangle, model.HotbarSlots);
            DrawGlowLine(
                new Rect(
                    separatorX,
                    rectangle.y + 12f,
                    1f,
                    rectangle.height - 38f),
                SignalSoftColor);
            DrawTurnModeButtons(
                turnAreaX,
                rectangle.y + TurnModeButtonTop,
                turnAreaWidth,
                TurnModeButtonHeight,
                model.PrimaryCommands);
            if (model.Resources != null)
            {
                DrawTurnResources(
                    turnAreaX,
                    rectangle.y + TurnResourceTop,
                    turnAreaWidth,
                    model.Resources);
            }
        }

        private void DrawCommandHints(
            Rect rectangle,
            IReadOnlyList<GameplayCommandHintModel> hints)
        {
            if (hints.Count == 0)
            {
                return;
            }

            for (int index = 0; index < hints.Count; index++)
            {
                GUI.Label(
                    CalculateCommandHintRowRectangle(
                        rectangle,
                        index,
                        hints.Count),
                    FormatHint(hints[index]),
                    commandHintsStyle);
            }
        }

        private void DrawBodyStatus(
            Rect rectangle,
            GameplayBodyStatusModel bodyStatus)
        {
            foreach (GameplayBodyRegionModel region in bodyStatus.Regions)
            {
                Rect regionRectangle = CalculateBodyRegionRectangle(
                    rectangle,
                    region.Region);
                Color frameColor = region.IsWounded
                    ? EquipmentSignalColor
                    : ModeButtonEdgeColor;
                if (region.Region == TargetRegionId.Head)
                {
                    DrawCircularBodyRegion(
                        regionRectangle,
                        region.IsWounded,
                        frameColor);
                }
                else
                {
                    GUI.Label(
                        regionRectangle,
                        GUIContent.none,
                        region.IsWounded
                            ? woundedBodyRegionStyle
                            : bodyRegionStyle);
                    DrawGlowFrame(regionRectangle, frameColor);
                }

                if (ContainsBodyRegionPoint(
                        regionRectangle,
                        region.Region,
                        Event.current.mousePosition))
                {
                    activeTooltip = BuildBodyRegionTooltip(
                        bodyStatus,
                        region);
                }
            }
        }

        private void DrawCircularBodyRegion(
            Rect rectangle,
            bool wounded,
            Color frameColor)
        {
            DrawTintedTexture(
                ExpandRectangle(rectangle, 4f),
                textureSet.BodyRegionCircleMask,
                GameplayVisualPalette.WithAlpha(
                    frameColor,
                    frameColor.a * 0.06f));
            DrawTintedTexture(
                ExpandRectangle(rectangle, 2f),
                textureSet.BodyRegionCircleMask,
                GameplayVisualPalette.WithAlpha(
                    frameColor,
                    frameColor.a * 0.14f));
            DrawTintedTexture(
                rectangle,
                textureSet.BodyRegionCircleMask,
                frameColor);
            float inset = Mathf.Min(
                1f,
                Mathf.Min(rectangle.width, rectangle.height) * 0.08f);
            Rect fillRectangle = new Rect(
                rectangle.x + inset,
                rectangle.y + inset,
                Mathf.Max(0f, rectangle.width - (inset * 2f)),
                Mathf.Max(0f, rectangle.height - (inset * 2f)));
            DrawTintedTexture(
                fillRectangle,
                textureSet.BodyRegionCircleMask,
                wounded
                    ? GameplayVisualPalette.WithAlpha(
                        GameplayVisualPalette.SignalOrange,
                        0.42f)
                    : GameplayVisualPalette.ButtonNormal);
        }

        private static Rect ExpandRectangle(Rect rectangle, float amount) =>
            new Rect(
                rectangle.x - amount,
                rectangle.y - amount,
                rectangle.width + (amount * 2f),
                rectangle.height + (amount * 2f));

        private static bool ContainsBodyRegionPoint(
            Rect rectangle,
            TargetRegionId region,
            Vector2 point)
        {
            if (region != TargetRegionId.Head)
                return rectangle.Contains(point);
            if (rectangle.width <= 0f || rectangle.height <= 0f)
                return false;
            float normalizedX = (point.x - rectangle.center.x)
                / (rectangle.width * 0.5f);
            float normalizedY = (point.y - rectangle.center.y)
                / (rectangle.height * 0.5f);
            return (normalizedX * normalizedX)
                + (normalizedY * normalizedY) <= 1f;
        }

        private static string BuildBodyRegionTooltip(
            GameplayBodyStatusModel bodyStatus,
            GameplayBodyRegionModel region)
        {
            string maximumWounds = bodyStatus.MaximumWounds == int.MaxValue
                ? "-"
                : bodyStatus.MaximumWounds.ToString();
            return FormatBodyRegionName(region.Region)
                + "\nSTATE - "
                + (region.IsWounded ? "WOUNDED" : "CLEAR")
                + "\nREGION WOUNDS - "
                + region.WoundCount
                + "\nTOTAL WOUNDS - "
                + bodyStatus.TotalWounds
                + " / "
                + maximumWounds
                + "\nMOVE PENALTY - "
                + bodyStatus.MovementPenalty.ToString("0.##");
        }

        private static string FormatBodyRegionName(TargetRegionId region)
        {
            switch (region)
            {
                case TargetRegionId.Head:
                    return "HEAD";
                case TargetRegionId.Torso:
                    return "TORSO";
                case TargetRegionId.LeftArm:
                    return "LEFT ARM";
                case TargetRegionId.RightArm:
                    return "RIGHT ARM";
                case TargetRegionId.LeftLeg:
                    return "LEFT LEG";
                case TargetRegionId.RightLeg:
                    return "RIGHT LEG";
                default:
                    throw new ArgumentOutOfRangeException(nameof(region));
            }
        }

        private void DrawTurnResources(
            float x,
            float y,
            float width,
            GameplayTurnResourceModel resources)
        {
            DrawResourceMeter(
                x,
                y,
                width,
                "AP",
                $"{resources.ActionPoints} / {resources.MaximumActionPoints}",
                resources.MaximumActionPoints > 0
                    ? resources.ActionPoints
                        / (float)resources.MaximumActionPoints
                    : 0f);
            DrawResourceMeter(
                x,
                y + 20f,
                width,
                "MOVE",
                $"{resources.MovementOpportunity:0.##} / "
                    + $"{resources.MaximumMovementOpportunity:0.##}",
                resources.MaximumMovementOpportunity > 0f
                    ? resources.MovementOpportunity
                        / resources.MaximumMovementOpportunity
                    : 0f);
        }

        private void DrawResourceMeter(
            float x,
            float y,
            float width,
            string label,
            string value,
            float fill)
        {
            GUI.Label(
                new Rect(x, y, width * 0.42f, 11f),
                label,
                resourceLabelStyle);
            GUI.Label(
                new Rect(x, y, width, 11f),
                value,
                resourceValueStyle);

            var track = new Rect(x, y + 12f, width, 5f);
            DrawRectangle(track, GameplayVisualPalette.MeterTrack);
            float fillWidth = Mathf.Max(0f, (track.width - 2f) * Mathf.Clamp01(fill));
            if (fillWidth > 0f)
            {
                DrawGlowLine(
                    new Rect(track.x + 1f, track.y + 1f, fillWidth, track.height - 2f),
                    SignalColor);
            }
        }

        private void DrawHotbar(
            Rect rectangle,
            IReadOnlyList<GameplayHotbarSlotModel> slots)
        {
            const float gap = 5f;
            float availableWidth = rectangle.width - (gap * (slots.Count - 1));
            float slotWidth = availableWidth / slots.Count;
            for (int index = 0; index < slots.Count; index++)
            {
                GameplayHotbarSlotModel slot = slots[index];
                var slotRectangle = new Rect(
                    rectangle.x + ((slotWidth + gap) * index),
                    rectangle.y,
                    slotWidth,
                    rectangle.height);
                bool hasEquipmentButton = !string.IsNullOrWhiteSpace(
                    slot.EquipmentLabel);
                const float equipmentHeight = 22f;
                const float innerGap = 4f;
                var itemRectangle = new Rect(
                    slotRectangle.x,
                    slotRectangle.y,
                    slotRectangle.width,
                    hasEquipmentButton
                        ? slotRectangle.height - equipmentHeight - innerGap
                        : slotRectangle.height);
                Event current = Event.current;
                if (IsHotbarChoiceRequest(current, itemRectangle))
                {
                    OpenHotbarChoiceMenu(slot.SlotNumber, itemRectangle);
                    current.Use();
                }
                bool previousEnabled = GUI.enabled;
                GUI.enabled = slot.Enabled;
                bool powerClicked = GUI.Button(
                    itemRectangle,
                    new GUIContent(slot.Label, slot.PowerTooltip),
                    slot.IsPowerPending
                        ? pendingPowerButtonStyle
                        : hotbarItemStyle);
                if (itemRectangle.Contains(Event.current.mousePosition))
                {
                    activeTooltip = slot.PowerTooltip;
                }
                GUI.enabled = previousEnabled;
                GUI.Label(
                    new Rect(
                        itemRectangle.x + 7f,
                        itemRectangle.y + 5f,
                        itemRectangle.width - 14f,
                        16f),
                    slot.SlotNumber.ToString(),
                    hotbarNumberStyle);
                Color itemEdge = slot.IsPowerPending
                    ? GameplayVisualPalette.WithAlpha(
                        EquipmentSignalColor,
                        CalculatePendingPowerPulse(Time.unscaledTime))
                    : slot.IsEquipped
                        ? EquipmentSignalColor
                        : ModeButtonEdgeColor;
                DrawGlowFrame(itemRectangle, itemEdge);
                if (powerClicked && slot.PrimaryClickRequestsPower)
                {
                    hotbarController?.TryActivateSlot(slot.SlotNumber);
                }

                if (!hasEquipmentButton)
                {
                    continue;
                }

                var equipmentRectangle = new Rect(
                    slotRectangle.x,
                    itemRectangle.yMax + innerGap,
                    slotRectangle.width,
                    equipmentHeight);
                GUIStyle equipmentStyle = slot.AwaitingConfirmation
                    ? equipmentConfirmationStyle
                    : slot.IsEquipped
                        ? equippedButtonStyle
                        : equipmentButtonStyle;
                GUI.enabled = slot.EquipmentEnabled;
                bool equipmentClicked = GUI.Button(
                    equipmentRectangle,
                    new GUIContent(
                        slot.EquipmentLabel,
                        slot.EquipmentTooltip),
                    equipmentStyle);
                if (equipmentRectangle.Contains(Event.current.mousePosition))
                {
                    activeTooltip = slot.EquipmentTooltip;
                }
                GUI.enabled = previousEnabled;
                Color equipmentEdge = slot.AwaitingConfirmation
                    ? EquipmentSignalColor
                    : slot.IsEquipped
                        ? SignalColor
                        : ModeButtonEdgeColor;
                DrawGlowFrame(equipmentRectangle, equipmentEdge);
                if (slot.IsEquipped && !slot.AwaitingConfirmation)
                {
                    DrawRectangle(
                        new Rect(
                            equipmentRectangle.x + 1f,
                            equipmentRectangle.y + 1f,
                            equipmentRectangle.width - 2f,
                            1f),
                        GameplayVisualPalette.WithAlpha(
                            GameplayVisualPalette.Border,
                            0.5f));
                    DrawGlowLine(
                        new Rect(
                            equipmentRectangle.x + 1f,
                            equipmentRectangle.yMax - 2f,
                            equipmentRectangle.width - 2f,
                            1f),
                        SignalColor);
                }

                if (equipmentClicked)
                {
                    hotbarController?.ClearStatus();
                    equipmentController?.TryToggleEquipment(
                        slot.ContentId,
                        slot.SlotNumber);
                }
            }
        }

        private void DrawActorAbilityFlyout(
            Rect commandBarRectangle,
            IReadOnlyList<GameplayHotbarSlotModel> slots)
        {
            GameplayHotbarSlotModel expanded = null;
            string expandedId = hotbarController?.ExpandedActorAbilityId;
            if (expandedId != null)
            {
                foreach (GameplayHotbarSlotModel slot in slots)
                {
                    if (slot.BindingKind
                            == GameplayHotbarBindingKind.ActorAbility
                        && string.Equals(
                            slot.ContentId,
                            expandedId,
                            StringComparison.Ordinal))
                    {
                        expanded = slot;
                        break;
                    }
                }
            }

            if (expanded != null)
            {
                cachedActorAbilitySlotNumber = expanded.SlotNumber;
                cachedActorAbilityId = expanded.ContentId;
                cachedActorAbilityLabel = expanded.Label;
                cachedActorAbilityOptions = expanded.AbilityOptions;
            }

            if (cachedActorAbilitySlotNumber == 0
                || cachedActorAbilityOptions.Count == 0
                || (expanded == null && actorAbilityFlyoutReveal <= 0f))
            {
                return;
            }

            Rect slotRectangle = CalculateHotbarSlotRectangle(
                commandBarRectangle,
                cachedActorAbilitySlotNumber);
            actorAbilityFlyoutRectangle =
                CalculateActorAbilityFlyoutRectangle(
                    slotRectangle,
                    cachedActorAbilityOptions.Count);
            float revealHeight = actorAbilityFlyoutRectangle.height
                * EvaluateFlyoutReveal(actorAbilityFlyoutReveal);
            float revealTop = actorAbilityFlyoutRectangle.yMax - revealHeight;
            var clipRectangle = new Rect(
                actorAbilityFlyoutRectangle.x,
                revealTop,
                actorAbilityFlyoutRectangle.width,
                revealHeight);
            GUI.BeginClip(clipRectangle);
            var panelRectangle = new Rect(
                0f,
                actorAbilityFlyoutRectangle.y - clipRectangle.y,
                actorAbilityFlyoutRectangle.width,
                actorAbilityFlyoutRectangle.height);
            DrawFramedPanel(panelRectangle, PanelStrongColor);
            DrawGlowLine(
                new Rect(
                    panelRectangle.x,
                    panelRectangle.yMax - 2f,
                    panelRectangle.width,
                    2f),
                SignalColor);

            const float padding = 8f;
            const float headingHeight = 28f;
            const float optionHeight = 31f;
            const float optionGap = 5f;
            GUI.Label(
                new Rect(
                    padding,
                    panelRectangle.y + padding,
                    panelRectangle.width - (padding * 2f),
                    headingHeight),
                cachedActorAbilityLabel + " OPTIONS",
                choiceHeaderStyle);

            bool previousEnabled = GUI.enabled;
            for (int index = 0;
                index < cachedActorAbilityOptions.Count;
                index++)
            {
                GameplayHotbarAbilityOptionModel option =
                    cachedActorAbilityOptions[index];
                var optionRectangle = new Rect(
                    padding,
                    panelRectangle.y + padding + headingHeight
                        + ((optionHeight + optionGap) * index),
                    panelRectangle.width - (padding * 2f),
                    optionHeight);
                if (optionRectangle.Contains(Event.current.mousePosition))
                {
                    activeTooltip = option.Tooltip;
                }

                GUI.enabled = expanded != null && option.Enabled;
                bool clicked = GUI.Button(
                    optionRectangle,
                    FormatActorAbilityOptionLabel(
                        cachedActorAbilitySlotNumber,
                        index,
                        option.Label),
                    option.Pending
                        ? pendingPowerButtonStyle
                        : hotbarItemStyle);
                GUI.enabled = previousEnabled;
                DrawGlowFrame(
                    optionRectangle,
                    option.Pending
                        ? EquipmentSignalColor
                        : ModeButtonEdgeColor);
                if (clicked)
                {
                    hotbarController?.TryActivateActorAbilityOption(
                        cachedActorAbilityId,
                        option.Id);
                }
            }

            GUI.EndClip();
            DrawHorizontalLaserReveal(
                actorAbilityFlyoutRectangle.x,
                revealTop,
                actorAbilityFlyoutRectangle.width,
                EquipmentSignalColor,
                actorAbilityFlyoutReveal);

            Event current = Event.current;
            if (expanded != null
                && current.type == EventType.MouseDown
                && current.button == 0
                && !slotRectangle.Contains(current.mousePosition)
                && !actorAbilityFlyoutRectangle.Contains(
                    current.mousePosition))
            {
                hotbarController.CloseActorAbilityFlyout();
                current.Use();
            }
        }

        internal static Rect CalculateHotbarSlotRectangle(
            Rect commandBarRectangle,
            int slotNumber) =>
            GameplayHudLayout.CalculateHotbarSlotRectangle(
                commandBarRectangle,
                slotNumber);

        internal static string FormatActorAbilityOptionLabel(
            int parentSlot,
            int optionIndex,
            string label) =>
            GameplayHudModelProjector.FormatActorAbilityOptionLabel(
                parentSlot,
                optionIndex,
                label);

        internal static Rect CalculateActorAbilityFlyoutRectangle(
            Rect slotRectangle,
            int optionCount) =>
            GameplayHudLayout.CalculateActorAbilityFlyoutRectangle(
                slotRectangle,
                optionCount);

        private void ClearCachedActorAbilityFlyout()
        {
            cachedActorAbilitySlotNumber = 0;
            cachedActorAbilityId = null;
            cachedActorAbilityLabel = null;
            cachedActorAbilityOptions =
                Array.Empty<GameplayHotbarAbilityOptionModel>();
            actorAbilityFlyoutRectangle = default;
        }

        internal static bool IsHotbarChoiceRequest(
            Event current,
            Rect itemRectangle) =>
            GameplayHudLayout.IsHotbarChoiceRequest(
                current,
                itemRectangle);

        internal static float CalculatePendingPowerPulse(float unscaledTime)
        {
            float phase = 0.5f + (0.5f * Mathf.Sin(
                unscaledTime
                * Mathf.PI
                * 2f
                * PendingPowerPulseCyclesPerSecond));
            return Mathf.Lerp(PendingPowerPulseMinimumAlpha, 1f, phase);
        }

        private void OpenHotbarChoiceMenu(int slotNumber, Rect slotRectangle)
        {
            hotbarController?.CloseActorAbilityFlyout();
            float height = CalculateHotbarChoiceHeight();
            hotbarChoice.Open(slotNumber, slotRectangle, height);
        }

        private float CalculateHotbarChoiceHeight()
        {
            int abilities = hotbarController?.ActorAbilities.Count ?? 0;
            int items = 0;
            int equipment = 0;
            foreach (InventoryItemDefinition item in Session.GetInventory(playerActorId))
            {
                if (item.IsEquippable)
                {
                    equipment++;
                }
                else
                {
                    items++;
                }
            }
            return 18f
                + (3f * 25f)
                + ((abilities + items + equipment) * 27f)
                + 10f;
        }

        private void DrawHotbarChoiceMenu(float canvasWidth, float canvasHeight)
        {
            if (!hotbarChoice.IsOpen)
            {
                return;
            }

            hotbarChoice.ClampToCanvas(canvasWidth, canvasHeight);
            Rect hotbarChoiceRectangle = hotbarChoice.Rectangle;
            DrawFramedPanel(hotbarChoiceRectangle, PanelStrongColor);
            DrawGlowLine(new Rect(
                hotbarChoiceRectangle.x,
                hotbarChoiceRectangle.y,
                hotbarChoiceRectangle.width,
                2f), SignalColor);
            float x = hotbarChoiceRectangle.x + 9f;
            float y = hotbarChoiceRectangle.y + 8f;
            float width = hotbarChoiceRectangle.width - 18f;
            GUI.Label(new Rect(x, y, width, 20f),
                "ABILITIES", choiceHeaderStyle);
            y += 24f;
            IReadOnlyList<GameplayActorAbilityHotbarDefinition> abilities =
                hotbarController?.ActorAbilities;
            if (abilities != null)
            {
                foreach (GameplayActorAbilityHotbarDefinition ability in abilities)
                {
                    if (GUI.Button(new Rect(x, y, width, 23f),
                        ability.DisplayName.ToUpperInvariant(), hotbarItemStyle))
                    {
                        hotbarController?.TryBindSlot(
                            hotbarChoice.SlotNumber,
                            new GameplayHotbarBinding(
                                GameplayHotbarBindingKind.ActorAbility,
                                ability.Id));
                        hotbarChoice.Close();
                    }
                    y += 27f;
                }
            }

            GUI.Label(new Rect(x, y, width, 20f), "ITEMS", choiceHeaderStyle);
            y += 24f;
            foreach (InventoryItemDefinition item in Session.GetInventory(playerActorId))
            {
                if (item.IsEquippable)
                {
                    continue;
                }

                string quantity = item.ConsumablePower == null
                    ? string.Empty
                    : "  x" + Session.GetInventoryQuantity(
                        playerActorId,
                        item.Id);
                if (GUI.Button(new Rect(x, y, width, 23f),
                    item.DisplayName.ToUpperInvariant() + quantity,
                    hotbarItemStyle))
                {
                    hotbarController?.TryBindSlot(
                        hotbarChoice.SlotNumber,
                        new GameplayHotbarBinding(
                            GameplayHotbarBindingKind.InventoryItem,
                            item.Id));
                    hotbarChoice.Close();
                }
                y += 27f;
            }

            GUI.Label(
                new Rect(x, y, width, 20f),
                "EQUIPMENT",
                choiceHeaderStyle);
            y += 24f;
            foreach (InventoryItemDefinition item in Session.GetInventory(playerActorId))
            {
                if (!item.IsEquippable)
                {
                    continue;
                }
                string suffix = string.Equals(
                    Session.GetActor(playerActorId).EquippedItemId,
                    item.Id,
                    StringComparison.Ordinal) ? "  [EQUIPPED]" : string.Empty;
                if (GUI.Button(new Rect(x, y, width, 23f),
                    item.DisplayName.ToUpperInvariant() + suffix, hotbarItemStyle))
                {
                    hotbarController?.TryBindSlot(
                        hotbarChoice.SlotNumber,
                        new GameplayHotbarBinding(
                            GameplayHotbarBindingKind.InventoryItem,
                            item.Id));
                    hotbarChoice.Close();
                }
                y += 27f;
            }

            Event current = Event.current;
            if (current.type == EventType.MouseDown
                && current.button == 0
                && !hotbarChoiceRectangle.Contains(current.mousePosition))
            {
                hotbarChoice.Close();
                current.Use();
            }
        }

        private void DrawTurnModeButtons(
            float x,
            float y,
            float width,
            float height,
            IReadOnlyList<GameplayCommandButtonModel> commands)
        {
            const float gap = 6f;
            float buttonWidth = commands.Count == 1
                ? width
                : (width - (gap * (commands.Count - 1))) / commands.Count;
            bool previousEnabled = GUI.enabled;
            for (int index = 0; index < commands.Count; index++)
            {
                GameplayCommandButtonModel command = commands[index];
                var rectangle = new Rect(
                    x + ((buttonWidth + gap) * index),
                    y,
                    buttonWidth,
                    height);
                GUI.enabled = command.Enabled;
                string binding = GetBindingDisplay(command.Control);
                string label = string.IsNullOrWhiteSpace(binding)
                    ? command.Label
                    : $"{command.Label}  [{binding}]";
                bool clicked = GUI.Button(rectangle, label, modeButtonStyle);
                GUI.enabled = previousEnabled;
                DrawGlowFrame(rectangle, ModeButtonEdgeColor);
                if (clicked)
                {
                    RequestControl(command.Control);
                }
            }
        }

        private void DrawAnimatedFlyout(
            float canvasWidth,
            float canvasHeight,
            string scenarioDisplayName,
            string mode,
            string resources)
        {
            if (!flyoutExpanded && flyoutReveal <= 0f)
            {
                DrawFlyout(
                    canvasWidth,
                    canvasHeight,
                    scenarioDisplayName,
                    mode,
                    resources,
                    expanded: false);
                return;
            }

            float width = Mathf.Min(470f, canvasWidth - 58f);
            float eased = EvaluateFlyoutReveal(flyoutReveal);
            const float expandedTabWidth = 38f;
            float revealEdge = (width + expandedTabWidth) * eased;
            GUI.BeginClip(new Rect(0f, 0f, revealEdge, canvasHeight));
            DrawFlyout(
                canvasWidth,
                canvasHeight,
                scenarioDisplayName,
                mode,
                resources,
                expanded: true);
            GUI.EndClip();
            DrawLaserReveal(
                revealEdge,
                18f,
                canvasHeight - 36f,
                SignalColor,
                flyoutReveal);
        }

        private void DrawFlyout(
            float canvasWidth,
            float canvasHeight,
            string scenarioDisplayName,
            string mode,
            string resources,
            bool expanded)
        {
            float width = Mathf.Min(470f, canvasWidth - 58f);
            if (!expanded)
            {
                DrawFlyoutTab(new Rect(0f, 36f, 42f, 82f), expanded: false);
                return;
            }

            var rectangle = new Rect(0f, 18f, width, canvasHeight - 36f);
            DrawFramedPanel(rectangle, PanelStrongColor);
            DrawGlowLine(
                new Rect(rectangle.xMax - 2f, rectangle.y, 2f, rectangle.height),
                SignalColor);

            var tabRectangle = new Rect(rectangle.xMax, rectangle.y + 18f, 38f, 72f);
            DrawFlyoutTab(tabRectangle, expanded: true);

            float x = rectangle.x + 18f;
            float innerWidth = rectangle.width - 38f;
            float y = rectangle.y + 17f;
            GUI.Label(
                new Rect(x, y, innerWidth, 22f),
                $"{scenarioDisplayName.ToUpperInvariant()} - {mode}",
                headerStyle);
            y += 30f;
            DrawSectionRule(x, y, innerWidth);
            y += 10f;

            float resourceHeight = Mathf.Max(
                42f,
                bodyStyle.CalcHeight(new GUIContent(resources), innerWidth));
            GUI.Label(
                new Rect(x, y, innerWidth, resourceHeight),
                resources,
                bodyStyle);
            y += resourceHeight + 10f;
            DrawSectionRule(x, y, innerWidth);
            y += 10f;

            float guidanceHeight = DrawGuidance(x, y, innerWidth);
            y += guidanceHeight + 12f;
            float tipsBottom = rectangle.yMax - 58f;
            if (tipsBottom > y + 72f)
            {
                DrawTips(x, y, innerWidth, tipsBottom - y);
            }

            DrawBugReportExport(x, rectangle.yMax - 48f, innerWidth);
        }

        private void DrawFlyoutTab(Rect rectangle, bool expanded)
        {
            DrawRectangle(rectangle, BorderColor);
            var buttonRectangle = new Rect(
                rectangle.x + 1f,
                rectangle.y + 1f,
                rectangle.width - 2f,
                rectangle.height - 2f);
            string label = expanded ? "<<" : ">>";
            if (GUI.Button(buttonRectangle, label, tabStyle))
            {
                ToggleFlyout();
            }
        }

        private float DrawGuidance(float x, float y, float width)
        {
            string guidanceId = CurrentGuidanceId;
            if (guidanceId == null || guidanceCatalog == null)
            {
                return 0f;
            }

            GameplayGuidanceEntry guidance = guidanceCatalog.Require(guidanceId);
            string text = $"EXPECTED  {guidance.ExpectedBehavior}\n" +
                $"WHY  {guidance.Rationale}\n" +
                $"TIP  {guidance.PlayerTip}";
            float contentHeight = guidanceStyle.CalcHeight(
                new GUIContent(text),
                width);
            float height = contentHeight + 31f;
            GUI.Label(
                new Rect(x, y, width, 21f),
                $"FIELD GUIDE - {guidance.Title.ToUpperInvariant()}",
                headerStyle);
            GUI.Label(
                new Rect(x, y + 28f, width, contentHeight),
                new GUIContent(text, guidance.PlayerTip),
                guidanceStyle);
            return height;
        }

        private void DrawTips(float x, float y, float width, float height)
        {
            const float headerHeight = 24f;
            GUI.Label(
                new Rect(x, y, width, headerHeight),
                "TIPS - ALWAYS AVAILABLE",
                headerStyle);
            DrawGlowLine(
                new Rect(x, y + headerHeight - 2f, width, 1f),
                SignalSoftColor);

            Rect viewport = new Rect(
                x,
                y + headerHeight + 3f,
                width,
                Mathf.Max(36f, height - headerHeight - 3f));
            float contentWidth = Mathf.Max(60f, viewport.width - 22f);
            float contentHeight = CalculateTipsContentHeight(contentWidth);
            var content = new Rect(0f, 0f, contentWidth, contentHeight);
            tipsScrollPosition = GUI.BeginScrollView(
                viewport,
                tipsScrollPosition,
                content,
                alwaysShowHorizontal: false,
                alwaysShowVertical: true);
            float contentY = 3f;
            if (tipCatalog != null)
            {
                foreach (GameplayTipEntry tip in tipCatalog.Entries)
                {
                    GUI.Label(
                        new Rect(2f, contentY, contentWidth - 4f, 18f),
                        tip.Category + " / " + tip.Title.ToUpperInvariant(),
                        tipTitleStyle);
                    contentY += 20f;
                    float bodyHeight = tipBodyStyle.CalcHeight(
                        new GUIContent(tip.Text),
                        contentWidth - 4f);
                    GUI.Label(
                        new Rect(2f, contentY, contentWidth - 4f, bodyHeight),
                        tip.Text,
                        tipBodyStyle);
                    contentY += bodyHeight + 11f;
                }
            }
            GUI.EndScrollView();
        }

        private float CalculateTipsContentHeight(float width)
        {
            float height = 6f;
            if (tipCatalog == null || tipBodyStyle == null)
            {
                return height;
            }
            foreach (GameplayTipEntry tip in tipCatalog.Entries)
            {
                height += 31f + tipBodyStyle.CalcHeight(
                    new GUIContent(tip.Text),
                    width - 4f);
            }
            return height;
        }

        private void DrawBugReportExport(float x, float y, float width)
        {
            const float buttonWidth = 224f;
            string exportBinding = GetBindingDisplay(
                GameplayControl.ExportBugReport);
            if (GUI.Button(
                new Rect(x, y, buttonWidth, 30f),
                "EXPORT BUG REPORT - " + exportBinding,
                buttonStyle))
            {
                OpenBugReportNote();
            }

            if (!string.IsNullOrWhiteSpace(bugReportStatus))
            {
                GUI.Label(
                    new Rect(
                        x + buttonWidth + 10f,
                        y + 2f,
                        width - buttonWidth - 10f,
                        28f),
                    bugReportStatus.ToUpperInvariant(),
                    statusStyle);
            }
        }

        private void DrawBugReportNoteModal(float canvasWidth, float canvasHeight)
        {
            const float width = 560f;
            const float height = 310f;
            var rectangle = new Rect((canvasWidth - width) * 0.5f,
                (canvasHeight - height) * 0.5f, width, height);
            DrawRectangle(new Rect(0f, 0f, canvasWidth, canvasHeight),
                new Color(0f, 0f, 0f, 0.72f));
            DrawFramedPanel(rectangle, PanelStrongColor);
            GUI.Label(new Rect(rectangle.x + 20f, rectangle.y + 18f,
                width - 40f, 26f), "EXPORT BUG REPORT", headerStyle);
            GUI.Label(new Rect(rectangle.x + 20f, rectangle.y + 50f,
                width - 40f, 42f),
                "Add what you observed, what you expected, and steps to reproduce. "
                + "This note is prepended to the diagnostic report.", bodyStyle);
            bugReportNote = GUI.TextArea(new Rect(rectangle.x + 20f,
                rectangle.y + 98f, width - 40f, 142f),
                bugReportNote ?? string.Empty, 2000);
            if (GUI.Button(new Rect(rectangle.x + 20f, rectangle.yMax - 50f,
                    140f, 30f), "CANCEL", buttonStyle))
            {
                CancelBugReportNote();
            }
            if (GUI.Button(new Rect(rectangle.xMax - 210f, rectangle.yMax - 50f,
                    190f, 30f), "EXPORT REPORT", buttonStyle))
            {
                SubmitBugReportNote(bugReportNote);
            }
        }

        private void DrawSectionRule(float x, float y, float width)
        {
            DrawGlowLine(new Rect(x, y, 54f, 1f), SignalColor);
            DrawGlowLine(new Rect(x + 58f, y, width - 58f, 1f), SignalSoftColor);
        }

        private void DrawInteractionPrompt(
            float centerX,
            float centerY,
            bool interactionAvailable)
        {
            if (!interactionAvailable)
            {
                return;
            }

            const float width = 280f;
            string binding = GetBindingDisplay(GameplayControl.Interact);
            GUI.Label(
                new Rect(centerX - (width * 0.5f), centerY + 34f, width, 30f),
                binding + "  "
                    + actionController.InteractionDisplayName.ToUpperInvariant(),
                modeButtonStyle);
        }

        private void EnsureStyles()
        {
            if (textureSet != null)
            {
                return;
            }

            textureSet = new GameplayHudTextureSet();
            whiteTexture = textureSet.White;
            buttonNormalTexture = textureSet.ButtonNormal;
            buttonHoverTexture = textureSet.ButtonHover;
            buttonActiveTexture = textureSet.ButtonActive;
            equipmentConfirmationTexture = textureSet.EquipmentConfirmation;

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = SignalColor },
            };
            choiceHeaderStyle = new GUIStyle(headerStyle)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
            };
            tipTitleStyle = new GUIStyle(headerStyle)
            {
                fontSize = 10,
                clipping = TextClipping.Clip,
            };
            tipBodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = PrimaryTextColor },
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = PrimaryTextColor },
            };
            guidanceStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = PrimaryTextColor },
            };
            controlsStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = SecondaryTextColor },
            };
            commandHintsStyle = new GUIStyle(controlsStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                clipping = TextClipping.Clip,
                wordWrap = false,
                normal = { textColor = PrimaryTextColor },
            };
            statusStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
                normal = { textColor = SecondaryTextColor },
            };
            tabStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                border = new RectOffset(0, 0, 0, 0),
                normal =
                {
                    background = buttonNormalTexture,
                    textColor = SignalColor,
                },
                hover =
                {
                    background = buttonHoverTexture,
                    textColor = PrimaryTextColor,
                },
                active =
                {
                    background = buttonActiveTexture,
                    textColor = PrimaryTextColor,
                },
            };
            resourceLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
                normal = { textColor = SignalColor },
            };
            resourceValueStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
                normal = { textColor = PrimaryTextColor },
            };
            hotbarNumberStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0),
                normal = { textColor = ModeButtonTextColor },
            };
            modeButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset(8, 8, 0, 0),
                border = new RectOffset(0, 0, 0, 0),
                normal =
                {
                    background = buttonHoverTexture,
                    textColor = ModeButtonTextColor,
                },
                hover =
                {
                    background = buttonActiveTexture,
                    textColor = ModeButtonTextColor,
                },
                active =
                {
                    background = buttonActiveTexture,
                    textColor = ModeButtonTextColor,
                },
                focused =
                {
                    background = buttonHoverTexture,
                    textColor = ModeButtonTextColor,
                },
            };
            bodyRegionStyle = new GUIStyle(modeButtonStyle)
            {
                fontSize = 8,
                padding = new RectOffset(2, 2, 0, 0),
                normal =
                {
                    background = buttonNormalTexture,
                    textColor = PrimaryTextColor,
                },
            };
            woundedBodyRegionStyle = new GUIStyle(bodyRegionStyle)
            {
                normal =
                {
                    background = equipmentConfirmationTexture,
                    textColor = EquipmentSignalColor,
                },
            };
            hotbarItemStyle = new GUIStyle(modeButtonStyle)
            {
                fontSize = 10,
                padding = new RectOffset(5, 5, 10, 0),
            };
            pendingPowerButtonStyle = new GUIStyle(hotbarItemStyle)
            {
                normal =
                {
                    background = equipmentConfirmationTexture,
                    textColor = EquipmentSignalColor,
                },
                hover =
                {
                    background = equipmentConfirmationTexture,
                    textColor = GameplayVisualPalette.TextBright,
                },
                active =
                {
                    background = equipmentConfirmationTexture,
                    textColor = GameplayVisualPalette.TextBright,
                },
                focused =
                {
                    background = equipmentConfirmationTexture,
                    textColor = EquipmentSignalColor,
                },
            };
            equipmentButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 8,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Clip,
                border = new RectOffset(0, 0, 0, 0),
                normal =
                {
                    background = buttonNormalTexture,
                    textColor = PrimaryTextColor,
                },
                hover =
                {
                    background = buttonHoverTexture,
                    textColor = ModeButtonTextColor,
                },
                active =
                {
                    background = buttonActiveTexture,
                    textColor = ModeButtonTextColor,
                },
            };
            equippedButtonStyle = new GUIStyle(equipmentButtonStyle)
            {
                contentOffset = new Vector2(0f, 1f),
                normal =
                {
                    background = buttonActiveTexture,
                    textColor = ModeButtonTextColor,
                },
                hover =
                {
                    background = buttonActiveTexture,
                    textColor = ModeButtonTextColor,
                },
            };
            equipmentConfirmationStyle = new GUIStyle(equipmentButtonStyle)
            {
                normal =
                {
                    background = equipmentConfirmationTexture,
                    textColor = EquipmentSignalColor,
                },
                hover =
                {
                    background = equipmentConfirmationTexture,
                    textColor = GameplayVisualPalette.TextBright,
                },
                active =
                {
                    background = equipmentConfirmationTexture,
                    textColor = GameplayVisualPalette.TextBright,
                },
            };
            tooltipStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                padding = new RectOffset(10, 10, 8, 8),
                border = new RectOffset(0, 0, 0, 0),
                normal =
                {
                    background = buttonNormalTexture,
                    textColor = PrimaryTextColor,
                },
            };
            confirmationFlyoutStyle = new GUIStyle(tooltipStyle)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 10,
                wordWrap = true,
                normal =
                {
                    background = buttonNormalTexture,
                    textColor = PrimaryTextColor,
                },
            };
            warningHintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 0),
                normal =
                {
                    background = null,
                    textColor = EquipmentSignalColor,
                },
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(14, 10, 0, 0),
                border = new RectOffset(0, 0, 0, 0),
                normal =
                {
                    background = buttonNormalTexture,
                    textColor = PrimaryTextColor,
                },
                hover =
                {
                    background = buttonHoverTexture,
                    textColor = SignalColor,
                },
                active =
                {
                    background = buttonActiveTexture,
                    textColor = SignalColor,
                },
            };
        }

        private void DrawFramedPanel(Rect rectangle, Color fillColor)
        {
            DrawRectangle(rectangle, BorderColor);
            DrawRectangle(
                new Rect(
                    rectangle.x + 1f,
                    rectangle.y + 1f,
                    rectangle.width - 2f,
                    rectangle.height - 2f),
                fillColor);
        }

        private void DrawTooltip(float canvasWidth, float canvasHeight)
        {
            if (string.IsNullOrWhiteSpace(activeTooltip))
            {
                return;
            }

            var content = new GUIContent(activeTooltip);
            Vector2 size = tooltipStyle.CalcSize(content);
            size.x = Mathf.Min(size.x, canvasWidth - 20f);
            Vector2 pointer = Event.current.mousePosition;
            float x = Mathf.Min(pointer.x + 14f, canvasWidth - size.x - 10f);
            float y = Mathf.Max(
                10f,
                Mathf.Min(
                    pointer.y - size.y - 12f,
                    canvasHeight - size.y - 10f));
            var rectangle = new Rect(x, y, size.x, size.y);
            GUI.Box(rectangle, content, tooltipStyle);
            DrawGlowFrame(rectangle, ModeButtonEdgeColor);
        }

        private void DrawEquipmentConfirmationFlyout(
            Rect commandBarRectangle,
            IReadOnlyList<GameplayHotbarSlotModel> slots,
            float canvasWidth)
        {
            GameplayHotbarSlotModel pending = null;
            foreach (GameplayHotbarSlotModel slot in slots)
            {
                if (slot.AwaitingConfirmation)
                {
                    pending = slot;
                    break;
                }
            }

            if (pending != null)
            {
                cachedEquipmentFlyoutText = "EQUIPMENT CONFIRMATION - "
                    + pending.Label
                    + "\n\n"
                    + pending.PowerTooltip
                    + "\n\n"
                    + pending.EquipmentTooltip;
            }

            if (string.IsNullOrWhiteSpace(cachedEquipmentFlyoutText)
                || equipmentFlyoutReveal <= 0f)
            {
                return;
            }

            string text = cachedEquipmentFlyoutText;
            Rect rectangle = CalculateEquipmentFlyoutRectangle(
                commandBarRectangle);
            float revealHeight = rectangle.height
                * EvaluateFlyoutReveal(equipmentFlyoutReveal);
            float revealTop = rectangle.yMax - revealHeight;
            var clipRectangle = new Rect(
                0f,
                revealTop,
                canvasWidth,
                revealHeight);
            GUI.BeginClip(clipRectangle);
            var clippedRectangle = rectangle;
            clippedRectangle.y -= clipRectangle.y;
            DrawFramedPanel(clippedRectangle, PanelStrongColor);
            GUI.Label(clippedRectangle, text, confirmationFlyoutStyle);
            DrawGlowFrame(clippedRectangle, EquipmentSignalColor);
            GUI.EndClip();
            DrawHorizontalLaserReveal(
                rectangle.x,
                revealTop,
                rectangle.width,
                EquipmentSignalColor,
                equipmentFlyoutReveal);
        }

        private void DrawWarningHint(
            Rect commandBarRectangle,
            GameplayWarningHintModel warningHint)
        {
            if (warningHint == null || warningHintReveal <= 0f)
            {
                return;
            }

            Rect rectangle = CalculateWarningHintRectangle(
                commandBarRectangle);
            float eased = EvaluateFlyoutReveal(warningHintReveal);
            float revealHeight = rectangle.height * eased;
            float revealTop = rectangle.yMax - revealHeight;
            var clipRectangle = new Rect(
                rectangle.x,
                revealTop,
                rectangle.width,
                revealHeight);
            GUI.BeginClip(clipRectangle);
            var clippedRectangle = new Rect(
                0f,
                rectangle.y - clipRectangle.y,
                rectangle.width,
                rectangle.height);
            GUI.Label(clippedRectangle, warningHint.Text, warningHintStyle);
            GUI.EndClip();
            DrawHorizontalLaserReveal(
                rectangle.x,
                revealTop,
                rectangle.width,
                EquipmentSignalColor,
                warningHintReveal);
        }

        private float EvaluateFlyoutReveal(float progress)
        {
            EnsureFlyoutMotion();
            return flyoutMotion.Evaluate(progress);
        }

        private void DrawLaserReveal(
            float x,
            float y,
            float height,
            Color color,
            float progress)
        {
            if (progress <= 0f || progress >= 1f)
            {
                return;
            }

            DrawGlowLine(
                new Rect(
                    x - (flyoutMotion.LaserOuterWidth * 0.5f),
                    y,
                    flyoutMotion.LaserOuterWidth,
                    height),
                GameplayVisualPalette.WithAlpha(
                    color,
                    flyoutMotion.LaserOuterAlpha));
            DrawGlowLine(
                new Rect(
                    x - (flyoutMotion.LaserInnerWidth * 0.5f),
                    y,
                    flyoutMotion.LaserInnerWidth,
                    height),
                GameplayVisualPalette.WithAlpha(
                    color,
                    flyoutMotion.LaserInnerAlpha));
            DrawGlowLine(
                new Rect(
                    x - (flyoutMotion.LaserCoreWidth * 0.5f),
                    y,
                    flyoutMotion.LaserCoreWidth,
                    height),
                color);
        }

        private void DrawHorizontalLaserReveal(
            float x,
            float y,
            float width,
            Color color,
            float progress)
        {
            if (progress <= 0f || progress >= 1f)
            {
                return;
            }

            DrawGlowLine(
                new Rect(
                    x,
                    y - (flyoutMotion.LaserOuterWidth * 0.5f),
                    width,
                    flyoutMotion.LaserOuterWidth),
                GameplayVisualPalette.WithAlpha(
                    color,
                    flyoutMotion.LaserOuterAlpha));
            DrawGlowLine(
                new Rect(
                    x,
                    y - (flyoutMotion.LaserInnerWidth * 0.5f),
                    width,
                    flyoutMotion.LaserInnerWidth),
                GameplayVisualPalette.WithAlpha(
                    color,
                    flyoutMotion.LaserInnerAlpha));
            DrawGlowLine(
                new Rect(
                    x,
                    y - (flyoutMotion.LaserCoreWidth * 0.5f),
                    width,
                    flyoutMotion.LaserCoreWidth),
                color);
        }

        private void EnsureFlyoutMotion()
        {
            if (flyoutMotion == null)
            {
                flyoutMotion = GameplayFlyoutMotionProfile.LoadDefault();
            }
        }

        private void DrawGlowFrame(Rect rectangle, Color color)
        {
            DrawGlowLine(new Rect(rectangle.x, rectangle.y, rectangle.width, 1f), color);
            DrawGlowLine(new Rect(rectangle.x, rectangle.yMax - 1f, rectangle.width, 1f), color);
            DrawGlowLine(new Rect(rectangle.x, rectangle.y, 1f, rectangle.height), color);
            DrawGlowLine(new Rect(rectangle.xMax - 1f, rectangle.y, 1f, rectangle.height), color);
        }

        private void DrawGlowLine(Rect rectangle, Color color)
        {
            bool horizontal = rectangle.width >= rectangle.height;
            Rect outerGlow = horizontal
                ? new Rect(rectangle.x, rectangle.y - 4f, rectangle.width, rectangle.height + 8f)
                : new Rect(rectangle.x - 4f, rectangle.y, rectangle.width + 8f, rectangle.height);
            Rect innerGlow = horizontal
                ? new Rect(rectangle.x, rectangle.y - 2f, rectangle.width, rectangle.height + 4f)
                : new Rect(rectangle.x - 2f, rectangle.y, rectangle.width + 4f, rectangle.height);

            DrawRectangle(
                outerGlow,
                new Color(color.r, color.g, color.b, color.a * 0.06f));
            DrawRectangle(
                innerGlow,
                new Color(color.r, color.g, color.b, color.a * 0.14f));
            DrawRectangle(rectangle, color);
        }

        private void DrawRectangle(Rect rectangle, Color color)
        {
            Color previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rectangle, whiteTexture);
            GUI.color = previousColor;
        }

        private static void DrawTintedTexture(
            Rect rectangle,
            Texture texture,
            Color color)
        {
            Color previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rectangle, texture);
            GUI.color = previousColor;
        }

        private void OnDestroy()
        {
            textureSet?.Dispose();
            textureSet = null;
            buttonNormalTexture = null;
            buttonHoverTexture = null;
            buttonActiveTexture = null;
            equipmentConfirmationTexture = null;
        }
    }
}
