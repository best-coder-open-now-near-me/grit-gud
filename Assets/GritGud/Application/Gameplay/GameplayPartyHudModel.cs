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
            bool selected,
            bool commanding,
            bool incapacitated,
            bool canSelect,
            TurnBudget turnBudget,
            int woundCount,
            int maximumWounds)
        {
            ActorId = actorId ?? throw new ArgumentNullException(nameof(actorId));
            DisplayName = displayName ?? throw new ArgumentNullException(
                nameof(displayName));
            Selected = selected;
            Commanding = commanding;
            Incapacitated = incapacitated;
            CanSelect = canSelect;
            TurnBudget = turnBudget;
            WoundCount = woundCount;
            MaximumWounds = maximumWounds;
        }

        public string ActorId { get; }

        public string DisplayName { get; }

        public bool Selected { get; }

        public bool Commanding { get; }

        public bool Incapacitated { get; }

        public bool CanSelect { get; }

        public TurnBudget TurnBudget { get; }

        public int WoundCount { get; }

        public int MaximumWounds { get; }
    }

    public sealed class GameplayPartyHudModel
    {
        public GameplayPartyHudModel(
            bool initiativeControlsSelection,
            IReadOnlyList<GameplayPartyMemberHudModel> members)
        {
            InitiativeControlsSelection = initiativeControlsSelection;
            Members = members ?? throw new ArgumentNullException(nameof(members));
        }

        public bool InitiativeControlsSelection { get; }

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
            var members = new List<GameplayPartyMemberHudModel>(
                control.ActorIds.Count);
            foreach (string actorId in control.ActorIds)
            {
                GameplayActorSnapshot actor = gameplay.GetActor(actorId);
                CharacterProfileDefinition profile = gameplay.Scenario
                    .GetActor(actorId)
                    .CharacterProfile;
                bool selected = string.Equals(
                    actorId,
                    control.SelectedActorId,
                    StringComparison.Ordinal);
                bool commanding = string.Equals(
                    actorId,
                    control.CommandActorId,
                    StringComparison.Ordinal);
                members.Add(new GameplayPartyMemberHudModel(
                    actorId,
                    profile?.DisplayName ?? actorId,
                    selected,
                    commanding,
                    actor.IsIncapacitated,
                    canSelect: !initiativeControlsSelection
                        && !actor.IsIncapacitated
                        && !selected,
                    actor.TurnBudget,
                    actor.Wounds.WoundCount,
                    actor.MaximumWounds));
            }

            return new GameplayPartyHudModel(
                initiativeControlsSelection,
                members.AsReadOnly());
        }
    }
}
