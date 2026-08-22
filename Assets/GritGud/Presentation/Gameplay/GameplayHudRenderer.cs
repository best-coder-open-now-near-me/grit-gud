using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed partial class GameplayHudRenderer : IDisposable
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
            GameplayHudStyleSet.PanelStrongColor;
        private static readonly Color BorderColor =
            GameplayHudStyleSet.BorderColor;
        private static readonly Color SignalColor =
            GameplayHudStyleSet.SignalColor;
        private static readonly Color SignalSoftColor =
            GameplayHudStyleSet.SignalSoftColor;
        private static readonly Color EquipmentSignalColor =
            GameplayHudStyleSet.EquipmentSignalColor;
        private static readonly Color ModeButtonEdgeColor =
            GameplayHudStyleSet.ModeButtonEdgeColor;
        private static readonly Color ModeButtonTextColor =
            GameplayHudStyleSet.ModeButtonTextColor;
        private static readonly Color PrimaryTextColor =
            GameplayHudStyleSet.PrimaryTextColor;
        private static readonly Color SecondaryTextColor =
            GameplayHudStyleSet.SecondaryTextColor;

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
        private readonly GameplayHudStyleSet styles =
            new GameplayHudStyleSet();
        private GUIStyle headerStyle => styles.Header;
        private GUIStyle bodyStyle => styles.Body;
        private GUIStyle guidanceStyle => styles.Guidance;
        private GUIStyle controlsStyle => styles.Controls;
        private GUIStyle commandHintsStyle => styles.CommandHints;
        private GUIStyle bodyRegionStyle => styles.BodyRegion;
        private GUIStyle woundedBodyRegionStyle => styles.WoundedBodyRegion;
        private GUIStyle buttonStyle => styles.Button;
        private GUIStyle statusStyle => styles.Status;
        private GUIStyle tabStyle => styles.Tab;
        private GUIStyle resourceLabelStyle => styles.ResourceLabel;
        private GUIStyle resourceValueStyle => styles.ResourceValue;
        private GUIStyle hotbarNumberStyle => styles.HotbarNumber;
        private GUIStyle hotbarItemStyle => styles.HotbarItem;
        private GUIStyle pendingPowerButtonStyle => styles.PendingPowerButton;
        private GUIStyle equipmentButtonStyle => styles.EquipmentButton;
        private GUIStyle equippedButtonStyle => styles.EquippedButton;
        private GUIStyle equipmentConfirmationStyle =>
            styles.EquipmentConfirmation;
        private GUIStyle tooltipStyle => styles.Tooltip;
        private GUIStyle confirmationFlyoutStyle => styles.ConfirmationFlyout;
        private GUIStyle warningHintStyle => styles.WarningHint;
        private GUIStyle encounterNoticeStyle => styles.EncounterNotice;
        private GUIStyle choiceHeaderStyle => styles.ChoiceHeader;
        private GUIStyle tipTitleStyle => styles.TipTitle;
        private GUIStyle tipBodyStyle => styles.TipBody;
        private GUIStyle modeButtonStyle => styles.ModeButton;
        private Texture2D whiteTexture => styles.WhiteTexture;
        private GameplayHudTextureSet textureSet => styles.TextureSet;
        private bool flyoutExpanded;
        private float flyoutReveal;
        private float equipmentFlyoutReveal;
        private float warningHintReveal;
        private string revealingEquipmentItemId;
        private string revealingWarningSignature;
        private string cachedEquipmentFlyoutText = string.Empty;
        private string activeTooltip = string.Empty;
        private readonly GameplayHudHotbarDrawer hotbarDrawer;
        private readonly GameplayHudGuidanceDrawer guidanceDrawer;
        private string playerActorId => bindings.PlayerActorId;
        private bool visible = true;

        internal GameplayHudBindings BindingState => bindings;

        public GameplaySession Session => bindings.Session;

        public TurnMovementController TurnMovement => turnMovement;

        public GameplayActionController ActionController => actionController;

        public GameplayAttackController AttackController => attackController;

        public GameplayEquipmentController EquipmentController =>
            equipmentController;

        public GameplayProjectileController ProjectileController =>
            projectileController;

        public bool IsVisible => visible;

        internal bool IsFlyoutExpanded => flyoutExpanded;

        internal bool IsBugReportNoteOpen => bugReportNoteOpen;

        internal bool IsHotbarChoiceOpen => hotbarDrawer.IsChoiceOpen;

        internal void OpenHotbarChoiceForTesting(
            int slotNumber,
            Rect slotRectangle,
            float height) =>
            hotbarDrawer.OpenChoice(slotNumber, slotRectangle, height);

        internal bool IsCommandBarVisible => Session != null;

        internal bool ContainsInteractiveScreenPoint(Vector2 screenPoint)
            => GameplayHudLayout.ContainsInteractiveScreenPoint(
                screenPoint,
                Screen.width,
                Screen.height,
                bugReportNoteOpen,
                hotbarDrawer.IsChoiceOpen,
                hotbarDrawer.ChoiceRectangle,
                hotbarDrawer.ActorAbilityReveal,
                hotbarDrawer.ActorAbilityRectangle,
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

        public GameplayHudRenderer()
        {
            hotbarDrawer = new GameplayHudHotbarDrawer(this);
            guidanceDrawer = new GameplayHudGuidanceDrawer(this);
            guidanceCatalog = GameplayGuidanceCatalog.LoadDefault();
            tipCatalog = GameplayTipCatalog.LoadDefault();
            flyoutMotion = GameplayFlyoutMotionProfile.LoadDefault();
        }

        public void Show()
        {
            ResetTransientState();
            visible = true;
        }

        public void Hide()
        {
            ResetTransientState();
            visible = false;
        }

        private void ResetTransientState()
        {
            flyoutExpanded = false;
            flyoutReveal = 0f;
            equipmentFlyoutReveal = 0f;
            warningHintReveal = 0f;
            revealingEquipmentItemId = null;
            revealingWarningSignature = null;
            cachedEquipmentFlyoutText = string.Empty;
            hotbarDrawer.Reset();
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

        public void Advance(float unscaledDeltaTime)
        {
            EnsureFlyoutMotion();
            flyoutReveal = flyoutMotion.Advance(
                flyoutReveal,
                flyoutExpanded,
                unscaledDeltaTime);

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
                unscaledDeltaTime);
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
                unscaledDeltaTime);
            if (warningHint == null && warningHintReveal <= 0f)
            {
                revealingWarningSignature = null;
            }

            hotbarDrawer.Advance(unscaledDeltaTime);

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

        public void Render()
        {
            if (!visible)
                return;
            EnsureStyles();

            float uiScale = GameplayHudLayout.CalculateUiScale(Screen.height);
            float canvasWidth = Screen.width / uiScale;
            float canvasHeight = Screen.height / uiScale;
            using (new GameplayGuiMatrixScope(
                Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f))))
            {
                GameplayHudModel model = CurrentModel;
                if (model == null)
                    return;

                if (bugReportNoteOpen)
                {
                    guidanceDrawer.DrawBugReportNoteModal(
                        canvasWidth,
                        canvasHeight);
                    return;
                }

                activeTooltip = string.Empty;
                Rect commandBarRectangle = CalculateCommandBarRectangle(
                    canvasWidth,
                    canvasHeight);
                DrawCommandBar(
                    commandBarRectangle,
                    model.CommandBar);
                hotbarDrawer.DrawActorAbilityFlyout(
                    commandBarRectangle,
                    model.CommandBar.HotbarSlots);
                hotbarDrawer.DrawChoiceMenu(canvasWidth, canvasHeight);
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
                    canvasWidth,
                    model.CommandBar.WarningHint);
                guidanceDrawer.DrawAnimatedFlyout(
                    canvasWidth,
                    canvasHeight,
                    model.ScenarioDisplayName,
                    model.ModeLabel,
                    model.ObjectiveSummary);
                guidanceDrawer.DrawInteractionPrompt(
                    canvasWidth * 0.5f,
                    canvasHeight * 0.5f,
                    model.InteractionAvailable);
                DrawTooltip(canvasWidth, canvasHeight);
            }
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

        internal static Rect CalculateEncounterNoticeRectangle(
            float canvasWidth) =>
            GameplayHudLayout.CalculateEncounterNoticeRectangle(canvasWidth);

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

            hotbarDrawer.DrawHotbar(hotbarRectangle, model.HotbarSlots);
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
            return FormatBodyRegionName(region.Region)
                + "\nSTATE - "
                + (region.IsWounded ? "INJURED" : "CLEAR")
                + "\nCONDITION - " + region.ConditionPercent + "%"
                + "\nSTRUCTURE - " + region.StructuralIntegrity + "%"
                + "\nMOTOR - " + region.MotorFunction + "%"
                + "\nSENSORY - " + region.SensoryFunction + "%"
                + "\nBLEED - " + region.BleedRate
                + "\nLIFE STATE - " + bodyStatus.LifeState
                + "\nSYSTEMIC TRAUMA - " + bodyStatus.SystemicTrauma
                + "\nPHYSIOLOGY - BLOOD "
                + bodyStatus.Physiology.BloodReserve + "% / SHOCK "
                + bodyStatus.Physiology.Shock + "%"
                + "\nCAPABILITY - MOVE "
                + bodyStatus.Capabilities.MovementCapacity + "% / AIM "
                + bodyStatus.Capabilities.AimStability + "%";
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

        private void EnsureStyles()
        {
            styles.Ensure();
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
            float canvasWidth,
            GameplayWarningHintModel warningHint)
        {
            if (warningHint == null || warningHintReveal <= 0f)
            {
                return;
            }

            if (string.Equals(
                    warningHint.SourceId,
                    "encounter.started",
                    StringComparison.Ordinal))
            {
                DrawEncounterNotice(canvasWidth, warningHint);
                return;
            }

            Rect rectangle = CalculateWarningHintRectangle(commandBarRectangle);
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

        private void DrawEncounterNotice(
            float canvasWidth,
            GameplayWarningHintModel warningHint)
        {
            Rect rectangle = CalculateEncounterNoticeRectangle(canvasWidth);
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
            DrawFramedPanel(clippedRectangle, PanelStrongColor);
            GUI.Label(clippedRectangle, warningHint.Text, encounterNoticeStyle);
            DrawGlowFrame(clippedRectangle, EquipmentSignalColor);
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

        public void Dispose()
        {
            styles.Dispose();
        }
    }
}
