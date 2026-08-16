using System;
using System.Collections.Generic;
using GritGud.Domain.Turns;

namespace GritGud.Domain.Gameplay
{
    public sealed class ScenarioTimingDefinition
    {
        public ScenarioTimingDefinition(float minimumVoluntaryTurnSeconds)
        {
            if (float.IsNaN(minimumVoluntaryTurnSeconds)
                || float.IsInfinity(minimumVoluntaryTurnSeconds)
                || minimumVoluntaryTurnSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumVoluntaryTurnSeconds));
            }

            MinimumVoluntaryTurnSeconds = minimumVoluntaryTurnSeconds;
        }

        public float MinimumVoluntaryTurnSeconds { get; }
    }

    public sealed class ScenarioActorDefinition
    {
        public ScenarioActorDefinition(
            string id,
            int initiative,
            GameplayActorPose startingPose,
            TurnBudget startingTurnBudget,
            AttackDefinition attack = null,
            DisplacementAbilityDefinition displacementAbility = null,
            ActorCombatDefinition combat = null,
            CharacterProfileDefinition characterProfile = null)
            : this(
                id,
                initiative,
                startingPose,
                startingTurnBudget,
                attack,
                Array.Empty<InventoryItemDefinition>(),
                initiallyEquippedItemId: null,
                characterProfile,
                displacementAbility,
                combat)
        {
        }

        public ScenarioActorDefinition(
            string id,
            int initiative,
            GameplayActorPose startingPose,
            TurnBudget startingTurnBudget,
            IEnumerable<InventoryItemDefinition> inventory,
            string initiallyEquippedItemId,
            CharacterProfileDefinition characterProfile = null,
            DisplacementAbilityDefinition displacementAbility = null,
            ActorCombatDefinition combat = null)
            : this(
                id,
                initiative,
                startingPose,
                startingTurnBudget,
                attack: null,
                inventory,
                initiallyEquippedItemId,
                characterProfile,
                displacementAbility,
                combat)
        {
        }

        private ScenarioActorDefinition(
            string id,
            int initiative,
            GameplayActorPose startingPose,
            TurnBudget startingTurnBudget,
            AttackDefinition attack,
            IEnumerable<InventoryItemDefinition> inventory,
            string initiallyEquippedItemId,
            CharacterProfileDefinition characterProfile = null,
            DisplacementAbilityDefinition displacementAbility = null,
            ActorCombatDefinition combat = null)
        {
            Id = RequireId(id, nameof(id));
            Initiative = initiative;
            StartingPose = startingPose;
            StartingTurnBudget = startingTurnBudget;
            Attack = attack;
            Inventory = CopyInventory(inventory);
            if (initiallyEquippedItemId != null)
            {
                InventoryItemDefinition initiallyEquipped = null;
                foreach (InventoryItemDefinition item in Inventory)
                {
                    if (string.Equals(
                        item.Id,
                        initiallyEquippedItemId,
                        StringComparison.Ordinal))
                    {
                        initiallyEquipped = item;
                        break;
                    }
                }

                if (initiallyEquipped == null || !initiallyEquipped.IsEquippable)
                {
                    throw new ArgumentException(
                        "The initially equipped item must be an equippable inventory item.",
                        nameof(initiallyEquippedItemId));
                }
            }

            InitiallyEquippedItemId = initiallyEquippedItemId;
            CharacterProfile = characterProfile;
            CoreAttributes = characterProfile?.CoreAttributes;
            DerivedStatistics = characterProfile?.DerivedStatistics;
            DisplacementAbility = displacementAbility;
            Combat = combat ?? ActorCombatDefinition.CreateLegacyNeutral();
            ValidateHotbarAssignments(Inventory, DisplacementAbility);
        }

        public string Id { get; }

        public int Initiative { get; }

        public GameplayActorPose StartingPose { get; }

        public TurnBudget StartingTurnBudget { get; }

        public AttackDefinition Attack { get; }

        public IReadOnlyList<InventoryItemDefinition> Inventory { get; }

        public string InitiallyEquippedItemId { get; }

        public CharacterProfileDefinition CharacterProfile { get; }

        public CoreAttributeSet CoreAttributes { get; }

        public CharacterDerivedStatistics? DerivedStatistics { get; }

        public DisplacementAbilityDefinition DisplacementAbility { get; }

        public ActorCombatDefinition Combat { get; }

        public IReadOnlyList<DisplacementActionDefinition>
            DisplacementActions => DisplacementAbility?.Actions
                ?? Array.Empty<DisplacementActionDefinition>();

        public InventoryItemDefinition GetInventoryItem(string itemId)
        {
            foreach (InventoryItemDefinition item in Inventory)
            {
                if (string.Equals(item.Id, itemId, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        public DisplacementActionDefinition GetDisplacementAction(
            string actionId)
        {
            foreach (DisplacementActionDefinition action in
                DisplacementActions)
            {
                if (string.Equals(
                    action.Id,
                    actionId,
                    StringComparison.Ordinal))
                {
                    return action;
                }
            }

            return null;
        }

        public ScenarioActorDefinition WithStartingPose(
            GameplayActorPose startingPose) =>
            Inventory.Count == 0
                ? new ScenarioActorDefinition(
                    Id,
                    Initiative,
                    startingPose,
                    StartingTurnBudget,
                    Attack,
                    DisplacementAbility,
                    Combat,
                    CharacterProfile)
                : new ScenarioActorDefinition(
                    Id,
                    Initiative,
                    startingPose,
                    StartingTurnBudget,
                    Inventory,
                    InitiallyEquippedItemId,
                    CharacterProfile,
                    DisplacementAbility,
                    Combat);

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Actor identifiers cannot be empty.",
                    parameterName);
            }

            return value;
        }

        private static IReadOnlyList<InventoryItemDefinition> CopyInventory(
            IEnumerable<InventoryItemDefinition> inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            var copy = new List<InventoryItemDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var slots = new HashSet<int>();
            foreach (InventoryItemDefinition item in inventory)
            {
                if (item == null)
                {
                    throw new ArgumentException(
                        "Actor inventories cannot contain null entries.",
                        nameof(inventory));
                }

                if (!ids.Add(item.Id) || !slots.Add(item.HotbarSlot))
                {
                    throw new ArgumentException(
                        "Actor inventory identifiers and hotbar slots must be unique.",
                        nameof(inventory));
                }

                copy.Add(item);
            }

            return copy.AsReadOnly();
        }

        private static void ValidateHotbarAssignments(
            IReadOnlyList<InventoryItemDefinition> inventory,
            DisplacementAbilityDefinition displacementAbility)
        {
            var slots = new HashSet<int>();
            foreach (InventoryItemDefinition item in inventory)
            {
                slots.Add(item.HotbarSlot);
            }

            if (displacementAbility != null
                && !slots.Add(displacementAbility.HotbarSlot))
            {
                throw new ArgumentException(
                    $"Hotbar slot {displacementAbility.HotbarSlot} is assigned more than once.",
                    nameof(displacementAbility));
            }
        }
    }

    public sealed class ContactAttackDefinition
    {
        public ContactAttackDefinition(float maximumReach)
        {
            if (float.IsNaN(maximumReach)
                || float.IsInfinity(maximumReach)
                || maximumReach <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumReach));
            }

            MaximumReach = maximumReach;
        }

        public float MaximumReach { get; }
    }

    public sealed class AttackDefinition
    {
        public AttackDefinition(
            string actionId,
            string displayName,
            ActionCost turnCost,
            float woundMovementPenalty,
            ProjectileFlightDefinition projectile = null,
            AccuracyDecayDefinition accuracyDecay = null,
            ContactAttackDefinition contact = null,
            DirectFireDamageDefinition directFireDamage = null)
        {
            if (string.IsNullOrWhiteSpace(actionId))
            {
                throw new ArgumentException(
                    "Attack identifiers cannot be empty.",
                    nameof(actionId));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "Attack display names cannot be empty.",
                    nameof(displayName));
            }

            if (float.IsNaN(woundMovementPenalty)
                || float.IsInfinity(woundMovementPenalty)
                || woundMovementPenalty <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(woundMovementPenalty));
            }

            if (projectile != null && contact != null)
            {
                throw new ArgumentException(
                    "An attack cannot be both a projectile and a contact attack.",
                    nameof(contact));
            }

            if (directFireDamage != null
                && (projectile != null || contact != null))
            {
                throw new ArgumentException(
                    "Only ranged immediate attacks can author direct-fire prop damage.",
                    nameof(directFireDamage));
            }

            if (contact != null && accuracyDecay != null)
            {
                throw new ArgumentException(
                    "Contact attacks use geometric exposure within their authored reach, not ranged accuracy decay.",
                    nameof(accuracyDecay));
            }

            if (projectile == null && contact == null && accuracyDecay == null)
            {
                throw new ArgumentNullException(
                    nameof(accuracyDecay),
                    "Ranged immediate attacks require an authored accuracy-decay function.");
            }

            ActionId = actionId;
            DisplayName = displayName;
            TurnCost = turnCost;
            WoundMovementPenalty = woundMovementPenalty;
            Projectile = projectile;
            Contact = contact;
            DirectFireDamage = directFireDamage;
            AccuracyDecay = contact == null
                ? accuracyDecay
                : AccuracyDecayDefinition.None;
        }

        public string ActionId { get; }

        public string DisplayName { get; }

        public ActionCost TurnCost { get; }

        public float WoundMovementPenalty { get; }

        public ProjectileFlightDefinition Projectile { get; }

        public AccuracyDecayDefinition AccuracyDecay { get; }

        public ContactAttackDefinition Contact { get; }

        public DirectFireDamageDefinition DirectFireDamage { get; }

        public bool CanTargetWorldPoint => Projectile == null && Contact == null;
    }

    public sealed class ScenarioObjectiveDefinition
    {
        public ScenarioObjectiveDefinition(string id, GameplayPosition position)
            : this(
                id,
                position,
                interactionRadius: 1f,
                CreateDefaultInteraction(id))
        {
        }

        public ScenarioObjectiveDefinition(
            string id,
            GameplayPosition position,
            float interactionRadius)
            : this(
                id,
                position,
                interactionRadius,
                CreateDefaultInteraction(id))
        {
        }

        public ScenarioObjectiveDefinition(
            string id,
            GameplayPosition position,
            float interactionRadius,
            GameplayInteractionDefinition interaction)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Objective identifiers cannot be empty.",
                    nameof(id));
            }

            if (float.IsNaN(interactionRadius)
                || float.IsInfinity(interactionRadius)
                || interactionRadius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(interactionRadius));
            }

            Id = id;
            Position = position;
            InteractionRadius = interactionRadius;
            Interaction = interaction ??
                throw new ArgumentNullException(nameof(interaction));
        }

        public string Id { get; }

        public GameplayPosition Position { get; }

        public float InteractionRadius { get; }

        public GameplayInteractionDefinition Interaction { get; }

        private static GameplayInteractionDefinition CreateDefaultInteraction(
            string objectiveId)
        {
            string normalizedId = string.IsNullOrWhiteSpace(objectiveId)
                ? "objective"
                : objectiveId;
            return new GameplayInteractionDefinition(
                normalizedId + ".interact",
                "Interact",
                new ActionCost(0, 0f, ActionMobility.Set));
        }
    }

    public sealed class AttackResponseDefinition
    {
        public AttackResponseDefinition(
            string targetId,
            bool startsEncounter)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                throw new ArgumentException(
                    "Attack responses require a stable target identifier.",
                    nameof(targetId));
            }

            TargetId = targetId;
            StartsEncounter = startsEncounter;
        }

        public string TargetId { get; }

        public bool StartsEncounter { get; }
    }

    public sealed class PlayerPartyDefinition
    {
        private readonly HashSet<string> actorIds;

        public PlayerPartyDefinition(
            IEnumerable<string> controlledActorIds,
            string initiallySelectedActorId)
        {
            if (controlledActorIds == null)
                throw new ArgumentNullException(nameof(controlledActorIds));

            var orderedIds = new List<string>();
            actorIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string actorId in controlledActorIds)
            {
                if (string.IsNullOrWhiteSpace(actorId))
                    throw new ArgumentException(
                        "Player party actor identifiers cannot be empty.",
                        nameof(controlledActorIds));
                if (!actorIds.Add(actorId))
                    throw new ArgumentException(
                        $"Player party actor '{actorId}' is duplicated.",
                        nameof(controlledActorIds));
                orderedIds.Add(actorId);
            }

            if (orderedIds.Count == 0)
                throw new ArgumentException(
                    "A player party requires at least one controlled actor.",
                    nameof(controlledActorIds));
            if (string.IsNullOrWhiteSpace(initiallySelectedActorId)
                || !actorIds.Contains(initiallySelectedActorId))
                throw new ArgumentException(
                    "The initially selected actor must belong to the player party.",
                    nameof(initiallySelectedActorId));

            ActorIds = orderedIds.AsReadOnly();
            InitiallySelectedActorId = initiallySelectedActorId;
        }

        public IReadOnlyList<string> ActorIds { get; }

        public string InitiallySelectedActorId { get; }

        public bool Contains(string actorId) =>
            actorIds.Contains(actorId ?? string.Empty);
    }

    public sealed class ScenarioDefinition
    {
        public ScenarioDefinition(
            string id,
            ScenarioTimingDefinition timing,
            IEnumerable<ScenarioActorDefinition> actors,
            IEnumerable<ScenarioObjectiveDefinition> objectives,
            IEnumerable<AttackResponseDefinition> attackResponses = null,
            PlayerPartyDefinition playerParty = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Scenario identifiers cannot be empty.",
                    nameof(id));
            }

            Id = id;
            Timing = timing ?? throw new ArgumentNullException(nameof(timing));
            Actors = CopyActors(actors);
            Objectives = CopyObjectives(objectives);
            AttackResponses = CopyAttackResponses(attackResponses);
            PlayerParty = playerParty;
            ValidatePlayerParty();
        }

        public string Id { get; }

        public ScenarioTimingDefinition Timing { get; }

        public IReadOnlyList<ScenarioActorDefinition> Actors { get; }

        public IReadOnlyList<ScenarioObjectiveDefinition> Objectives { get; }

        public IReadOnlyList<AttackResponseDefinition> AttackResponses { get; }

        public PlayerPartyDefinition PlayerParty { get; }

        public ScenarioDefinition WithActorStartingPoses(
            IReadOnlyDictionary<string, GameplayActorPose> resolvedPoses)
        {
            if (resolvedPoses == null)
            {
                throw new ArgumentNullException(nameof(resolvedPoses));
            }

            var knownActors = new HashSet<string>(StringComparer.Ordinal);
            var resolvedActors = new List<ScenarioActorDefinition>(
                Actors.Count);
            foreach (ScenarioActorDefinition actor in Actors)
            {
                knownActors.Add(actor.Id);
                resolvedActors.Add(resolvedPoses.TryGetValue(
                        actor.Id,
                        out GameplayActorPose pose)
                    ? actor.WithStartingPose(pose)
                    : actor);
            }

            foreach (string actorId in resolvedPoses.Keys)
            {
                if (!knownActors.Contains(actorId))
                {
                    throw new KeyNotFoundException(
                        $"Resolved actor pose '{actorId}' is not in scenario '{Id}'.");
                }
            }

            return new ScenarioDefinition(
                Id,
                Timing,
                resolvedActors,
                Objectives,
                AttackResponses,
                PlayerParty);
        }

        public bool TryGetAttackResponse(
            string targetId,
            out AttackResponseDefinition response)
        {
            foreach (AttackResponseDefinition candidate in AttackResponses)
            {
                if (string.Equals(
                        candidate.TargetId,
                        targetId,
                        StringComparison.Ordinal))
                {
                    response = candidate;
                    return true;
                }
            }

            response = null;
            return false;
        }

        public ScenarioActorDefinition GetActor(string actorId)
        {
            foreach (ScenarioActorDefinition actor in Actors)
                if (string.Equals(actor.Id, actorId, StringComparison.Ordinal))
                    return actor;
            throw new KeyNotFoundException(
                $"Actor definition '{actorId}' is not part of scenario '{Id}'.");
        }

        private static IReadOnlyList<ScenarioActorDefinition> CopyActors(
            IEnumerable<ScenarioActorDefinition> actors)
        {
            if (actors == null)
            {
                throw new ArgumentNullException(nameof(actors));
            }

            var copy = new List<ScenarioActorDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ScenarioActorDefinition actor in actors)
            {
                if (actor == null)
                {
                    throw new ArgumentException(
                        "Scenario actors cannot contain null entries.",
                        nameof(actors));
                }

                if (!ids.Add(actor.Id))
                {
                    throw new ArgumentException(
                        $"Scenario actor identifier '{actor.Id}' is duplicated.",
                        nameof(actors));
                }

                copy.Add(actor);
            }

            if (copy.Count == 0)
            {
                throw new ArgumentException(
                    "A gameplay scenario requires at least one actor.",
                    nameof(actors));
            }

            return copy.AsReadOnly();
        }

        private static IReadOnlyList<ScenarioObjectiveDefinition> CopyObjectives(
            IEnumerable<ScenarioObjectiveDefinition> objectives)
        {
            if (objectives == null)
            {
                throw new ArgumentNullException(nameof(objectives));
            }

            var copy = new List<ScenarioObjectiveDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ScenarioObjectiveDefinition objective in objectives)
            {
                if (objective == null)
                {
                    throw new ArgumentException(
                        "Scenario objectives cannot contain null entries.",
                        nameof(objectives));
                }

                if (!ids.Add(objective.Id))
                {
                    throw new ArgumentException(
                        $"Scenario objective identifier '{objective.Id}' is duplicated.",
                        nameof(objectives));
                }

                copy.Add(objective);
            }

            return copy.AsReadOnly();
        }

        private static IReadOnlyList<AttackResponseDefinition>
            CopyAttackResponses(
                IEnumerable<AttackResponseDefinition> responses)
        {
            if (responses == null)
            {
                return Array.Empty<AttackResponseDefinition>();
            }

            var copy = new List<AttackResponseDefinition>();
            var targetIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (AttackResponseDefinition response in responses)
            {
                if (response == null)
                {
                    throw new ArgumentException(
                        "Scenario attack responses cannot contain null entries.",
                        nameof(responses));
                }

                if (!targetIds.Add(response.TargetId))
                {
                    throw new ArgumentException(
                        $"Attack response target '{response.TargetId}' is duplicated.",
                        nameof(responses));
                }

                copy.Add(response);
            }

            return copy.AsReadOnly();
        }

        private void ValidatePlayerParty()
        {
            if (PlayerParty == null)
                return;

            var knownActors = new Dictionary<string, ScenarioActorDefinition>(
                StringComparer.Ordinal);
            foreach (ScenarioActorDefinition actor in Actors)
                knownActors.Add(actor.Id, actor);

            var characterIdentities = new HashSet<string>(StringComparer.Ordinal);
            foreach (string actorId in PlayerParty.ActorIds)
            {
                if (!knownActors.TryGetValue(actorId, out ScenarioActorDefinition actor))
                    throw new ArgumentException(
                        $"Player party actor '{actorId}' is not part of scenario '{Id}'.",
                        nameof(PlayerParty));
                if (actor.Combat.EnemyBehavior != null)
                    throw new ArgumentException(
                        $"Player party actor '{actorId}' cannot also own enemy behavior.",
                        nameof(PlayerParty));

                if (actor.CharacterProfile == null)
                    throw new ArgumentException(
                        $"Player party actor '{actorId}' requires a character profile.",
                        nameof(PlayerParty));
                string identityId = actor.CharacterProfile.IdentityId;
                if (!characterIdentities.Add(identityId))
                    throw new ArgumentException(
                        $"Player party character identity '{identityId}' is duplicated.",
                        nameof(PlayerParty));
            }
        }
    }
}
