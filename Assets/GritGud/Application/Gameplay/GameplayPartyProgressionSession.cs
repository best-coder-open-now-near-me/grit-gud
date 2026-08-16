using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayPartyProgressionSession
    {
        private readonly GameplaySession gameplay;
        private readonly PlayerPartyDefinition party;
        private readonly Dictionary<string, GameplayProgressionSession>
            progressionByActor =
                new Dictionary<string, GameplayProgressionSession>(
                    StringComparer.Ordinal);

        public GameplayPartyProgressionSession(
            GameplaySession gameplaySession,
            GameplayPartySave restoredParty = null)
        {
            gameplay = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            party = gameplay.Scenario.PlayerParty ?? throw new ArgumentException(
                "Party progression requires an authored player party.",
                nameof(gameplaySession));
            if (restoredParty != null)
                GameplayPartySaveValidator.Validate(
                    restoredParty,
                    gameplay.Scenario);
            foreach (string actorId in party.ActorIds)
            {
                ScenarioActorDefinition actor = gameplay.Scenario.GetActor(actorId);
                CharacterProfileDefinition profile = actor.CharacterProfile
                    ?? throw new InvalidOperationException(
                        $"Party actor '{actorId}' requires a character profile.");
                progressionByActor.Add(
                    actorId,
                    new GameplayProgressionSession(
                        profile,
                        restoredParty != null
                            && restoredParty.TryGetCharacter(
                                profile.IdentityId,
                                out CharacterPersistenceSnapshot restored)
                            ? restored.Progression
                            : null));
            }
        }

        public IReadOnlyList<string> ActorIds => party.ActorIds;

        public GameplayProgressionSession GetProgression(string actorId)
        {
            if (!progressionByActor.TryGetValue(
                    actorId ?? string.Empty,
                    out GameplayProgressionSession progression))
            {
                throw new ArgumentException(
                    $"Actor '{actorId}' is not part of the player party.",
                    nameof(actorId));
            }

            return progression;
        }

        public CharacterProgressionSnapshot GetSnapshot(string actorId) =>
            GetProgression(actorId).Snapshot;

        public CharacterAdvancementAvailability EvaluateAdvancement(
            string actorId,
            string optionId)
        {
            CharacterAdvancementAvailability availability =
                GetProgression(actorId).EvaluateAdvancement(optionId);
            if (availability == null)
                return null;
            return gameplay.Mode == GameplaySessionMode.TurnBased
                ? availability.WithFailure(
                    CharacterAdvancementFailure.TurnBasedModeActive)
                : availability;
        }

        public bool TryAdvance(
            string actorId,
            string optionId,
            out CharacterAdvancementFailure failure)
        {
            CharacterAdvancementAvailability availability =
                EvaluateAdvancement(actorId, optionId);
            if (availability == null)
            {
                failure = CharacterAdvancementFailure.UnknownOption;
                return false;
            }
            if (!availability.CanAdvance)
            {
                failure = availability.Failure;
                return false;
            }
            return GetProgression(actorId).TryAdvance(optionId, out failure);
        }

        public IReadOnlyList<CharacterPersistenceSnapshot> CapturePersistence()
        {
            var snapshots = new List<CharacterPersistenceSnapshot>(
                party.ActorIds.Count);
            foreach (string actorId in party.ActorIds)
            {
                snapshots.Add(
                    GetProgression(actorId).CapturePersistence(
                        gameplay,
                        actorId));
            }

            return snapshots.AsReadOnly();
        }
    }
}
