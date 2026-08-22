using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayPartyMemberHudModel
    {
        public GameplayPartyMemberHudModel(
            string actorId,
            string displayName,
            bool partyMember,
            bool hostile,
            bool selected,
            bool commanding,
            bool active,
            bool incapacitated,
            bool canSelect,
            TurnBudget turnBudget,
            int woundCount,
            int maximumWounds,
            ActorLifeState lifeState = ActorLifeState.Active,
            int conditionPercent = 100)
        {
            ActorId = actorId ?? throw new ArgumentNullException(nameof(actorId));
            DisplayName = displayName ?? throw new ArgumentNullException(
                nameof(displayName));
            PartyMember = partyMember;
            Hostile = hostile;
            Selected = selected;
            Commanding = commanding;
            Active = active;
            Incapacitated = incapacitated;
            CanSelect = canSelect;
            TurnBudget = turnBudget;
            WoundCount = woundCount;
            MaximumWounds = maximumWounds;
            if (!Enum.IsDefined(typeof(ActorLifeState), lifeState))
                throw new ArgumentOutOfRangeException(nameof(lifeState));
            if (conditionPercent < 0 || conditionPercent > 100)
                throw new ArgumentOutOfRangeException(nameof(conditionPercent));
            LifeState = lifeState;
            ConditionPercent = conditionPercent;
        }

        public string ActorId { get; }

        public string DisplayName { get; }

        public bool PartyMember { get; }

        public bool Hostile { get; }

        public bool Selected { get; }

        public bool Commanding { get; }

        public bool Active { get; }

        public bool Incapacitated { get; }

        public bool CanSelect { get; }

        public TurnBudget TurnBudget { get; }

        public int WoundCount { get; }

        public int MaximumWounds { get; }

        public ActorLifeState LifeState { get; }

        public int ConditionPercent { get; }

        public bool Dead => LifeState == ActorLifeState.Dead;
    }

    public sealed class GameplayPartyHudModel
    {
        public GameplayPartyHudModel(
            bool initiativeControlsSelection,
            bool combatRoster,
            IReadOnlyList<GameplayPartyMemberHudModel> members)
        {
            InitiativeControlsSelection = initiativeControlsSelection;
            CombatRoster = combatRoster;
            Members = members ?? throw new ArgumentNullException(nameof(members));
        }

        public bool InitiativeControlsSelection { get; }

        public bool CombatRoster { get; }

        public IReadOnlyList<GameplayPartyMemberHudModel> Members { get; }
    }

    public static class GameplayPartyHudModelBuilder
    {
        public static GameplayPartyHudModel Build(
            GameplaySession gameplay,
            GameplayPartyControlSession control)
        {
            if (gameplay == null)
                throw new ArgumentNullException(nameof(gameplay));
            if (control == null)
                throw new ArgumentNullException(nameof(control));

            bool initiativeControlsSelection =
                gameplay.Mode == GameplaySessionMode.TurnBased;
            bool combatRoster = gameplay.EncounterActive;
            IReadOnlyList<string> actorIds = combatRoster
                ? gameplay.InitiativeOrder
                : control.ActorIds;
            var partyActorIds = new HashSet<string>(
                control.ActorIds,
                StringComparer.Ordinal);
            var members = new List<GameplayPartyMemberHudModel>(
                actorIds.Count);
            foreach (string actorId in actorIds)
            {
                GameplayActorSnapshot actor = gameplay.GetActor(actorId);
                CharacterProfileDefinition profile = gameplay.Scenario
                    .GetActor(actorId)
                    .CharacterProfile;
                bool partyMember = partyActorIds.Contains(actorId);
                bool hostile = IsHostileToParty(
                    gameplay,
                    control.ActorIds,
                    actorId,
                    partyMember);
                bool selected = string.Equals(
                    actorId,
                    control.SelectedActorId,
                    StringComparison.Ordinal);
                bool commanding = string.Equals(
                    actorId,
                    control.CommandActorId,
                    StringComparison.Ordinal);
                bool active = string.Equals(
                    actorId,
                    gameplay.ActiveActorId,
                    StringComparison.Ordinal);
                members.Add(new GameplayPartyMemberHudModel(
                    actorId,
                    profile?.DisplayName ?? actorId,
                    partyMember,
                    hostile,
                    selected,
                    commanding,
                    active,
                    actor.IsIncapacitated,
                    canSelect: !initiativeControlsSelection
                        && partyMember
                        && !actor.IsIncapacitated
                        && !selected,
                    actor.TurnBudget,
                    actor.Wounds.WoundCount,
                    actor.MaximumWounds,
                    actor.LifeState,
                    GameplayInjuryCapabilityProjection
                        .CalculateConditionPercent(actor.Injuries)));
            }

            return new GameplayPartyHudModel(
                initiativeControlsSelection,
                combatRoster,
                members.AsReadOnly());
        }

        private static bool IsHostileToParty(
            GameplaySession gameplay,
            IReadOnlyList<string> partyActorIds,
            string actorId,
            bool partyMember)
        {
            if (partyMember)
            {
                return false;
            }

            foreach (string partyActorId in partyActorIds)
            {
                if (gameplay.IsHostile(partyActorId, actorId)
                    || gameplay.IsHostile(actorId, partyActorId))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
