using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal static class GameplayHudLayout
    {
        public const float ReferenceHeight = 900f;
        public const float CommandBarMargin = 14f;
        public const float CommandBarSideRailWidth = 142f;
        public const int CommandHintRowCapacity = 10;
        public const float CommandHintRowHeight = 16f;
        public const float CommandHintRowGap = 3f;
        public const float SideRailSectionGap = 5f;
        public const float TurnModeButtonTop = 13f;
        public const float TurnModeButtonHeight = 31f;
        public const float TurnResourceTop = 64f;
        public const float EquipmentFlyoutTop = 126f;
        public const float WarningHintHeight = 16f;
        public const float WarningHintGap = 5f;
        public const float EncounterNoticeTop = 38f;
        public const float EncounterNoticeHeight = 74f;
        public const float EncounterNoticeMaximumWidth = 720f;
        public const int HotbarSlotCount =
            GameplayCommandBarModel.HotbarSlotCount;

        public static float CalculateUiScale(float screenHeight) =>
            Mathf.Clamp(screenHeight / ReferenceHeight, 0.75f, 1.35f);

        public static bool ContainsInteractiveScreenPoint(
            Vector2 screenPoint,
            float screenWidth,
            float screenHeight,
            bool modalOpen,
            bool hotbarChoiceOpen,
            Rect hotbarChoiceRectangle,
            float actorAbilityFlyoutReveal,
            Rect actorAbilityFlyoutRectangle,
            bool flyoutExpanded)
        {
            float uiScale = CalculateUiScale(screenHeight);
            float canvasWidth = screenWidth / uiScale;
            float canvasHeight = screenHeight / uiScale;
            var guiPoint = new Vector2(
                screenPoint.x / uiScale,
                (screenHeight - screenPoint.y) / uiScale);
            if (modalOpen)
                return true;

            Rect commandBar = CalculateCommandBarRectangle(
                canvasWidth,
                canvasHeight);
            if (commandBar.Contains(guiPoint)
                || CalculateDialogueButtonRectangle(
                    canvasWidth,
                    commandBar).Contains(guiPoint)
                || CalculateBodyStatusRectangle(
                    canvasWidth,
                    commandBar).Contains(guiPoint))
            {
                return true;
            }

            if (hotbarChoiceOpen && hotbarChoiceRectangle.Contains(guiPoint))
                return true;

            if (actorAbilityFlyoutReveal > 0f
                && actorAbilityFlyoutRectangle.Contains(guiPoint))
            {
                return true;
            }

            Rect flyout = flyoutExpanded
                ? new Rect(
                    0f,
                    18f,
                    Mathf.Min(470f, canvasWidth - 58f),
                    canvasHeight - 36f)
                : new Rect(0f, 36f, 42f, 82f);
            return flyout.Contains(guiPoint);
        }

        public static Rect CalculateCommandBarRectangle(
            float canvasWidth,
            float canvasHeight)
        {
            float reservedWidth = 2f * (
                CommandBarSideRailWidth + (CommandBarMargin * 2f));
            float width = Mathf.Min(
                940f,
                Mathf.Max(0f, canvasWidth - reservedWidth));
            bool compact = width < 680f;
            float height = compact ? 142f : 118f;
            return new Rect(
                (canvasWidth - width) * 0.5f,
                canvasHeight - height - 6f,
                width,
                height);
        }

        public static Rect CalculateDialogueButtonRectangle(
            float canvasWidth,
            Rect commandBarRectangle)
        {
            const float buttonHeight = 31f;
            Rect bodyStatus = CalculateBodyStatusRectangle(
                canvasWidth,
                commandBarRectangle);
            return new Rect(
                bodyStatus.x,
                bodyStatus.y - SideRailSectionGap - buttonHeight,
                CommandBarSideRailWidth,
                buttonHeight);
        }

        public static Rect CalculateHotbarRectangle(
            Rect commandBarRectangle,
            float x,
            float width)
        {
            const float topMargin = 18f;
            return new Rect(
                x,
                commandBarRectangle.y + topMargin,
                width,
                commandBarRectangle.height - topMargin - CommandBarMargin);
        }

        public static Rect CalculateHotbarLayoutRectangle(
            Rect commandBarRectangle)
        {
            bool compact = commandBarRectangle.width < 680f;
            float contentX = commandBarRectangle.x + CommandBarMargin;
            float contentWidth = commandBarRectangle.width
                - (CommandBarMargin * 2f);
            float turnAreaWidth = compact
                ? Mathf.Clamp(contentWidth * 0.36f, 210f, 245f)
                : 320f;
            const float separatorSpacing = 15f;
            float hotbarWidth = contentWidth
                - turnAreaWidth
                - (separatorSpacing * 2f);
            return CalculateHotbarRectangle(
                commandBarRectangle,
                contentX,
                hotbarWidth);
        }

        public static Rect CalculateEquipmentFlyoutRectangle(
            Rect commandBarRectangle)
        {
            Rect hotbarRectangle = CalculateHotbarLayoutRectangle(
                commandBarRectangle);
            Rect hintRectangle = CalculateWarningHintRectangle(
                commandBarRectangle);
            float top = Mathf.Min(
                EquipmentFlyoutTop,
                hotbarRectangle.y - 120f);
            return new Rect(
                CommandBarMargin,
                Mathf.Max(8f, top),
                Mathf.Min(330f, hotbarRectangle.width),
                hintRectangle.y
                    - WarningHintGap
                    - Mathf.Max(8f, top));
        }

        public static Rect CalculateWarningHintRectangle(
            Rect commandBarRectangle)
        {
            Rect hotbarRectangle = CalculateHotbarLayoutRectangle(
                commandBarRectangle);
            return new Rect(
                hotbarRectangle.x,
                commandBarRectangle.y
                    - WarningHintGap
                    - WarningHintHeight,
                hotbarRectangle.width,
                WarningHintHeight);
        }

        /// <summary>
        /// Encounter entry is an interrupting state change, not routine command
        /// guidance. Keep its notice in a dedicated, prominent location rather
        /// than squeezing it into the command-bar hint strip.
        /// </summary>
        public static Rect CalculateEncounterNoticeRectangle(
            float canvasWidth)
        {
            float width = Mathf.Min(
                EncounterNoticeMaximumWidth,
                Mathf.Max(0f, canvasWidth - (CommandBarMargin * 2f)));
            return new Rect(
                (canvasWidth - width) * 0.5f,
                EncounterNoticeTop,
                width,
                EncounterNoticeHeight);
        }

        public static Rect CalculateBodyStatusRectangle(
            float canvasWidth,
            Rect commandBarRectangle)
        {
            float height = Mathf.Max(
                0f,
                commandBarRectangle.height - (CommandBarMargin * 2f));
            return new Rect(
                Mathf.Max(
                    CommandBarMargin,
                    canvasWidth
                        - CommandBarSideRailWidth
                        - CommandBarMargin),
                commandBarRectangle.yMax
                    - CommandBarMargin
                    - height,
                CommandBarSideRailWidth,
                height);
        }

        public static Rect CalculateCommandHintsRectangle(
            Rect commandBarRectangle) =>
            new Rect(
                CommandBarMargin,
                commandBarRectangle.yMax
                    - CommandBarMargin
                    - CalculateCommandHintContentHeight(
                        CommandHintRowCapacity),
                CommandBarSideRailWidth,
                CalculateCommandHintContentHeight(
                    CommandHintRowCapacity));

        public static float CalculateCommandHintContentHeight(int rowCount)
        {
            if (rowCount < 0)
                throw new ArgumentOutOfRangeException(nameof(rowCount));

            return rowCount == 0
                ? 0f
                : (rowCount * CommandHintRowHeight)
                    + ((rowCount - 1) * CommandHintRowGap);
        }

        public static Rect CalculateCommandHintRowRectangle(
            Rect rectangle,
            int rowIndex,
            int rowCount)
        {
            if (rowCount < 1)
                throw new ArgumentOutOfRangeException(nameof(rowCount));
            if (rowIndex < 0 || rowIndex >= rowCount)
                throw new ArgumentOutOfRangeException(nameof(rowIndex));

            return new Rect(
                rectangle.x,
                rectangle.y
                    + (rowIndex
                        * (CommandHintRowHeight + CommandHintRowGap)),
                rectangle.width,
                CommandHintRowHeight);
        }

        public static Rect CalculateBodyRegionRectangle(
            Rect bodyStatusRectangle,
            TargetRegionId region)
        {
            if (!Enum.IsDefined(typeof(TargetRegionId), region))
                throw new ArgumentOutOfRangeException(nameof(region));

            const float silhouetteWidth = 68f;
            const float silhouetteHeight = 95f;
            float scale = Mathf.Max(
                0f,
                Mathf.Min(
                    bodyStatusRectangle.width / silhouetteWidth,
                    bodyStatusRectangle.height / silhouetteHeight));
            float centerX = bodyStatusRectangle.center.x;
            float top = bodyStatusRectangle.center.y
                - ((silhouetteHeight * scale) * 0.5f);
            switch (region)
            {
                case TargetRegionId.Head:
                    return new Rect(
                        centerX - (10f * scale),
                        top,
                        20f * scale,
                        20f * scale);
                case TargetRegionId.LeftArm:
                    return new Rect(
                        centerX - (34f * scale),
                        top + (25f * scale),
                        13f * scale,
                        31f * scale);
                case TargetRegionId.Torso:
                    return new Rect(
                        centerX - (18f * scale),
                        top + (23f * scale),
                        36f * scale,
                        35f * scale);
                case TargetRegionId.RightArm:
                    return new Rect(
                        centerX + (21f * scale),
                        top + (25f * scale),
                        13f * scale,
                        31f * scale);
                case TargetRegionId.LeftLeg:
                    return new Rect(
                        centerX - (17f * scale),
                        top + (61f * scale),
                        15f * scale,
                        34f * scale);
                case TargetRegionId.RightLeg:
                    return new Rect(
                        centerX + (2f * scale),
                        top + (61f * scale),
                        15f * scale,
                        34f * scale);
                default:
                    throw new ArgumentOutOfRangeException(nameof(region));
            }
        }

        public static Rect CalculateHotbarSlotRectangle(
            Rect commandBarRectangle,
            int slotNumber)
        {
            if (slotNumber < 1 || slotNumber > HotbarSlotCount)
                throw new ArgumentOutOfRangeException(nameof(slotNumber));

            Rect hotbar = CalculateHotbarLayoutRectangle(commandBarRectangle);
            const float gap = 5f;
            float slotWidth = (hotbar.width
                    - (gap * (HotbarSlotCount - 1)))
                / HotbarSlotCount;
            return new Rect(
                hotbar.x + ((slotWidth + gap) * (slotNumber - 1)),
                hotbar.y,
                slotWidth,
                hotbar.height);
        }

        public static Rect CalculateActorAbilityFlyoutRectangle(
            Rect slotRectangle,
            int optionCount)
        {
            if (optionCount < 1)
                throw new ArgumentOutOfRangeException(nameof(optionCount));

            const float width = 260f;
            const float padding = 8f;
            const float headingHeight = 28f;
            const float optionHeight = 31f;
            const float optionGap = 5f;
            const float flyoutGap = 7f;
            float height = (padding * 2f)
                + headingHeight
                + (optionCount * optionHeight)
                + ((optionCount - 1) * optionGap);
            return new Rect(
                slotRectangle.x,
                slotRectangle.y - flyoutGap - height,
                width,
                height);
        }

        public static bool IsHotbarChoiceRequest(
            Event current,
            Rect itemRectangle) =>
            current != null
            && current.rawType == EventType.MouseDown
            && current.button == 1
            && itemRectangle.Contains(current.mousePosition);
    }
}
