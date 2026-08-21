using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed partial class GameplayHudRenderer
    {
        private sealed class GameplayHudHotbarDrawer
        {
            private const float PendingPowerPulseCyclesPerSecond =
                GameplayHudRenderer.PendingPowerPulseCyclesPerSecond;
            private const float PendingPowerPulseMinimumAlpha =
                GameplayHudRenderer.PendingPowerPulseMinimumAlpha;
            private static readonly Color PanelStrongColor =
                GameplayHudRenderer.PanelStrongColor;
            private static readonly Color EquipmentSignalColor =
                GameplayHudRenderer.EquipmentSignalColor;
            private static readonly Color ModeButtonEdgeColor =
                GameplayHudRenderer.ModeButtonEdgeColor;
            private static readonly Color SignalColor =
                GameplayHudRenderer.SignalColor;

            private readonly GameplayHudRenderer owner;
            private readonly GameplayHotbarChoiceState hotbarChoice =
                new GameplayHotbarChoiceState();
            private float actorAbilityFlyoutReveal;
            private Rect actorAbilityFlyoutRectangle;
            private int cachedActorAbilitySlotNumber;
            private string cachedActorAbilityId;
            private string cachedActorAbilityLabel;
            private IReadOnlyList<GameplayHotbarAbilityOptionModel>
                cachedActorAbilityOptions =
                    Array.Empty<GameplayHotbarAbilityOptionModel>();

            public GameplayHudHotbarDrawer(GameplayHudRenderer owner)
            {
                this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            }

            public bool IsChoiceOpen => hotbarChoice.IsOpen;
            public Rect ChoiceRectangle => hotbarChoice.Rectangle;
            public float ActorAbilityReveal => actorAbilityFlyoutReveal;
            public Rect ActorAbilityRectangle => actorAbilityFlyoutRectangle;

            public void OpenChoice(
                int slotNumber,
                Rect slotRectangle,
                float height) =>
                hotbarChoice.Open(slotNumber, slotRectangle, height);

            private GameplayHotbarController hotbarController =>
                owner.hotbarController;
            private GameplayEquipmentController equipmentController =>
                owner.equipmentController;
            private GameplaySession Session => owner.Session;
            private string playerActorId => owner.playerActorId;
            private GUIStyle pendingPowerButtonStyle =>
                owner.pendingPowerButtonStyle;
            private GUIStyle hotbarItemStyle => owner.hotbarItemStyle;
            private GUIStyle hotbarNumberStyle => owner.hotbarNumberStyle;
            private GUIStyle equipmentConfirmationStyle =>
                owner.equipmentConfirmationStyle;
            private GUIStyle equippedButtonStyle => owner.equippedButtonStyle;
            private GUIStyle equipmentButtonStyle => owner.equipmentButtonStyle;
            private GUIStyle choiceHeaderStyle => owner.choiceHeaderStyle;

            private string activeTooltip
            {
                set => owner.activeTooltip = value;
            }

            public void Reset()
            {
                actorAbilityFlyoutReveal = 0f;
                hotbarChoice.Close();
                ClearCachedActorAbilityFlyout();
            }

            public void Advance(float unscaledDeltaTime)
            {
                actorAbilityFlyoutReveal = owner.flyoutMotion.Advance(
                    actorAbilityFlyoutReveal,
                    hotbarController?.HasExpandedActorAbility == true,
                    unscaledDeltaTime);
                if (actorAbilityFlyoutReveal <= 0f
                    && hotbarController?.HasExpandedActorAbility != true)
                {
                    ClearCachedActorAbilityFlyout();
                }
            }

            public void DrawChoiceMenu(
                float canvasWidth,
                float canvasHeight) =>
                DrawHotbarChoiceMenu(canvasWidth, canvasHeight);

            private void DrawRectangle(Rect rectangle, Color color) =>
                owner.DrawRectangle(rectangle, color);
            private void DrawGlowLine(Rect rectangle, Color color) =>
                owner.DrawGlowLine(rectangle, color);
            private void DrawGlowFrame(Rect rectangle, Color color) =>
                owner.DrawGlowFrame(rectangle, color);
            private void DrawFramedPanel(Rect rectangle, Color color) =>
                owner.DrawFramedPanel(rectangle, color);
            private float EvaluateFlyoutReveal(float progress) =>
                owner.EvaluateFlyoutReveal(progress);
            private void DrawHorizontalLaserReveal(
                float x,
                float y,
                float width,
                Color color,
                float progress) =>
                owner.DrawHorizontalLaserReveal(
                    x,
                    y,
                    width,
                    color,
                    progress);

            public void DrawHotbar(
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

            public void DrawActorAbilityFlyout(
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

        }
    }
}
