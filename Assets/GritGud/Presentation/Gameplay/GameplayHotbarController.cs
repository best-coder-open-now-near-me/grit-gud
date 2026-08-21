using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayHotbarController : MonoBehaviour
    {
        private readonly Dictionary<int, GameplayHotbarBinding> bindings =
            new Dictionary<int, GameplayHotbarBinding>();
        private readonly Dictionary<
            string,
            GameplayActorAbilityHotbarDefinition> actorAbilityIndex =
                new Dictionary<
                    string,
                    GameplayActorAbilityHotbarDefinition>(StringComparer.Ordinal);
        private readonly List<GameplayActorAbilityHotbarDefinition>
            actorAbilities =
                new List<GameplayActorAbilityHotbarDefinition>();
        private readonly Dictionary<
            string,
            Dictionary<int, GameplayHotbarBinding>> actorBindingLayouts =
                new Dictionary<
                    string,
                    Dictionary<int, GameplayHotbarBinding>>(
                        StringComparer.Ordinal);
        private string actorId;
        private string expandedActorAbilityId;
        private Func<string, int, bool> inventoryItemActivationRequested;
        private Func<string, string, bool> actorAbilityActivationRequested;
        private Func<GameplayHotbarBinding, bool> activationAllowed;
        private Action assignmentChanged;

        public GameplaySession Session { get; private set; }

        public IReadOnlyDictionary<int, GameplayHotbarBinding> Bindings =>
            bindings;

        public IReadOnlyList<GameplayActorAbilityHotbarDefinition>
            ActorAbilities => actorAbilities;

        public string ExpandedActorAbilityId => expandedActorAbilityId;

        public bool HasExpandedActorAbility => expandedActorAbilityId != null;

        public string StatusMessage { get; private set; } = string.Empty;

        public void Bind(
            GameplaySession session,
            string authoritativeActorId,
            IReadOnlyList<GameplayActorAbilityHotbarDefinition>
                authoredActorAbilities,
            Func<string, int, bool> onInventoryItemActivationRequested,
            Func<string, string, bool> onActorAbilityActivationRequested,
            Func<GameplayHotbarBinding, bool> canActivate = null,
            Action onAssignmentChanged = null)
        {
            Unbind();
            Session = session ?? throw new ArgumentNullException(nameof(session));
            inventoryItemActivationRequested =
                onInventoryItemActivationRequested
                ?? throw new ArgumentNullException(
                    nameof(onInventoryItemActivationRequested));
            actorAbilityActivationRequested =
                onActorAbilityActivationRequested
                ?? throw new ArgumentNullException(
                    nameof(onActorAbilityActivationRequested));
            activationAllowed = canActivate ?? (_ => true);
            assignmentChanged = onAssignmentChanged;
            enabled = true;
            SetActor(authoritativeActorId, authoredActorAbilities);
        }

        public void SetActor(
            string authoritativeActorId,
            IReadOnlyList<GameplayActorAbilityHotbarDefinition>
                authoredActorAbilities)
        {
            if (Session == null)
            {
                throw new InvalidOperationException(
                    "Bind the gameplay hotbar before changing actors.");
            }
            if (string.IsNullOrWhiteSpace(authoritativeActorId))
            {
                throw new ArgumentException(
                    "Hotbar actor identifiers cannot be empty.",
                    nameof(authoritativeActorId));
            }
            if (authoredActorAbilities == null)
                throw new ArgumentNullException(nameof(authoredActorAbilities));

            Session.GetActor(authoritativeActorId);
            SaveCurrentActorLayout();
            actorId = authoritativeActorId;
            expandedActorAbilityId = null;
            bindings.Clear();
            actorAbilityIndex.Clear();
            actorAbilities.Clear();

            foreach (GameplayActorAbilityHotbarDefinition ability in
                authoredActorAbilities)
            {
                if (ability == null)
                {
                    throw new ArgumentException(
                        "Actor ability hotbar definitions cannot contain null entries.",
                        nameof(authoredActorAbilities));
                }

                if (!actorAbilityIndex.TryAdd(ability.Id, ability))
                {
                    throw new InvalidOperationException(
                        $"Actor ability '{ability.Id}' is registered more than once.");
                }

                actorAbilities.Add(ability);
            }

            if (actorBindingLayouts.TryGetValue(
                    actorId,
                    out Dictionary<int, GameplayHotbarBinding> savedLayout))
            {
                foreach (KeyValuePair<int, GameplayHotbarBinding> entry in
                    savedLayout)
                {
                    bindings.Add(entry.Key, entry.Value);
                }
            }
            else
            {
                foreach (InventoryItemDefinition item in
                    Session.GetInventory(actorId))
                {
                    AddDefaultBinding(
                        item.HotbarSlot,
                        new GameplayHotbarBinding(
                            GameplayHotbarBindingKind.InventoryItem,
                            item.Id));
                }

                foreach (GameplayActorAbilityHotbarDefinition ability in
                    actorAbilities)
                {
                    if (ability.AuthoredSlot == 0)
                        continue;
                    AddDefaultBinding(
                        ability.AuthoredSlot,
                        new GameplayHotbarBinding(
                            GameplayHotbarBindingKind.ActorAbility,
                            ability.Id));
                }
            }

            StatusMessage = string.Empty;
        }

        public void Unbind()
        {
            Session = null;
            actorId = null;
            expandedActorAbilityId = null;
            inventoryItemActivationRequested = null;
            actorAbilityActivationRequested = null;
            activationAllowed = null;
            assignmentChanged = null;
            bindings.Clear();
            actorAbilityIndex.Clear();
            actorAbilities.Clear();
            actorBindingLayouts.Clear();
            StatusMessage = string.Empty;
            enabled = false;
        }

        private void SaveCurrentActorLayout()
        {
            if (actorId == null)
                return;

            actorBindingLayouts[actorId] =
                new Dictionary<int, GameplayHotbarBinding>(bindings);
        }

        public bool TryActivateSlot(int slotNumber)
        {
            if (!bindings.TryGetValue(
                    slotNumber,
                    out GameplayHotbarBinding binding)
                || activationAllowed == null
                || !activationAllowed(binding))
            {
                return false;
            }

            StatusMessage = string.Empty;
            switch (binding.Kind)
            {
                case GameplayHotbarBindingKind.InventoryItem:
                    return inventoryItemActivationRequested(
                        binding.ContentId,
                        slotNumber);
                case GameplayHotbarBindingKind.ActorAbility:
                    return TryActivateActorAbility(binding.ContentId);
                default:
                    throw new ArgumentOutOfRangeException(nameof(binding.Kind));
            }
        }

        public bool TryBindSlot(
            int slotNumber,
            GameplayHotbarBinding binding)
        {
            if (slotNumber <= 0
                || slotNumber > GameplayHotbarRules.SlotCount
                || !TryResolveDisplayName(binding, out string displayName))
            {
                return false;
            }

            int previousSlot = 0;
            foreach (KeyValuePair<int, GameplayHotbarBinding> assignment in
                bindings)
            {
                if (assignment.Value.Equals(binding))
                {
                    previousSlot = assignment.Key;
                    break;
                }
            }

            if (previousSlot != 0 && previousSlot != slotNumber)
            {
                bindings.Remove(previousSlot);
            }

            bindings[slotNumber] = binding;
            CloseActorAbilityFlyout();
            assignmentChanged?.Invoke();
            StatusMessage = displayName + " assigned to hotkey "
                + slotNumber + ".";
            return true;
        }

        public void ClearStatus()
        {
            StatusMessage = string.Empty;
        }

        public bool TryActivateActorAbilityOption(
            string abilityId,
            string optionId)
        {
            if (!actorAbilityIndex.TryGetValue(
                    abilityId ?? string.Empty,
                    out GameplayActorAbilityHotbarDefinition ability)
                || activationAllowed == null
                || !activationAllowed(new GameplayHotbarBinding(
                    GameplayHotbarBindingKind.ActorAbility,
                    ability.Id))
                || !ContainsOption(ability, optionId))
            {
                return false;
            }

            bool activated = actorAbilityActivationRequested(
                ability.Id,
                optionId);
            if (activated)
            {
                expandedActorAbilityId = null;
                StatusMessage = string.Empty;
            }

            return activated;
        }

        public bool TryActivateExpandedActorAbilityOption(int optionNumber)
        {
            if (optionNumber <= 0
                || expandedActorAbilityId == null
                || !actorAbilityIndex.TryGetValue(
                    expandedActorAbilityId,
                    out GameplayActorAbilityHotbarDefinition ability)
                || optionNumber > ability.Options.Count)
            {
                return false;
            }

            return TryActivateActorAbilityOption(
                ability.Id,
                ability.Options[optionNumber - 1].Id);
        }

        public bool TryHandleExpandedActorAbilityHotkey(int hotbarNumber)
        {
            if (expandedActorAbilityId == null)
            {
                return false;
            }

            int parentSlot = 0;
            foreach (KeyValuePair<int, GameplayHotbarBinding> binding in
                bindings)
            {
                if (binding.Value.Kind
                        == GameplayHotbarBindingKind.ActorAbility
                    && string.Equals(
                        binding.Value.ContentId,
                        expandedActorAbilityId,
                        StringComparison.Ordinal))
                {
                    parentSlot = binding.Key;
                    break;
                }
            }

            if (parentSlot == 0)
            {
                return false;
            }

            if (hotbarNumber == parentSlot)
            {
                return CloseActorAbilityFlyout();
            }

            GameplayActorAbilityHotbarDefinition ability =
                actorAbilityIndex[expandedActorAbilityId];
            for (int optionIndex = 0;
                optionIndex < ability.Options.Count;
                optionIndex++)
            {
                if (ResolveOptionHotbarNumber(
                        parentSlot,
                        optionIndex) == hotbarNumber)
                {
                    return TryActivateActorAbilityOption(
                        ability.Id,
                        ability.Options[optionIndex].Id);
                }
            }

            return false;
        }

        internal static int ResolveOptionHotbarNumber(
            int parentSlot,
            int optionIndex)
        {
            if (parentSlot < 1 || parentSlot > GameplayHotbarRules.SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(parentSlot));
            }

            if (optionIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(optionIndex));
            }

            int hotbarNumber = optionIndex + 1;
            if (hotbarNumber >= parentSlot)
            {
                hotbarNumber++;
            }

            return hotbarNumber <= GameplayHotbarRules.SlotCount
                ? hotbarNumber
                : 0;
        }

        public bool CloseActorAbilityFlyout()
        {
            if (expandedActorAbilityId == null)
            {
                return false;
            }

            expandedActorAbilityId = null;
            StatusMessage = string.Empty;
            return true;
        }

        private bool TryActivateActorAbility(string abilityId)
        {
            if (!actorAbilityIndex.TryGetValue(
                    abilityId,
                    out GameplayActorAbilityHotbarDefinition ability))
            {
                return false;
            }

            if (ability.Options.Count == 0)
            {
                return actorAbilityActivationRequested(ability.Id, null);
            }

            if (string.Equals(
                    expandedActorAbilityId,
                    ability.Id,
                    StringComparison.Ordinal))
            {
                return CloseActorAbilityFlyout();
            }

            expandedActorAbilityId = ability.Id;
            StatusMessage = "Select a " + ability.DisplayName + " option.";
            return true;
        }

        private static bool ContainsOption(
            GameplayActorAbilityHotbarDefinition ability,
            string optionId)
        {
            foreach (GameplayActorAbilityOptionDefinition option in
                ability.Options)
            {
                if (string.Equals(
                        option.Id,
                        optionId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void AddDefaultBinding(
            int slotNumber,
            GameplayHotbarBinding binding)
        {
            if (slotNumber <= 0
                || slotNumber > GameplayHotbarRules.SlotCount)
            {
                throw new InvalidOperationException(
                    $"Authored hotbar slot '{slotNumber}' is invalid.");
            }

            if (bindings.ContainsKey(slotNumber))
            {
                // Default slots are preferences assembled from independent
                // content sources. The earlier binding stays selected while
                // this action remains available in the assignment chooser.
                return;
            }

            bindings.Add(slotNumber, binding);
        }

        private bool TryResolveDisplayName(
            GameplayHotbarBinding binding,
            out string displayName)
        {
            displayName = null;
            if (Session == null)
            {
                return false;
            }

            switch (binding.Kind)
            {
                case GameplayHotbarBindingKind.InventoryItem:
                    InventoryItemDefinition item = Session.GetInventoryItem(
                        actorId,
                        binding.ContentId);
                    displayName = item?.DisplayName;
                    break;
                case GameplayHotbarBindingKind.ActorAbility:
                    if (actorAbilityIndex.TryGetValue(
                            binding.ContentId,
                            out GameplayActorAbilityHotbarDefinition ability))
                    {
                        displayName = ability.DisplayName;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(binding.Kind));
            }

            return !string.IsNullOrWhiteSpace(displayName);
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
