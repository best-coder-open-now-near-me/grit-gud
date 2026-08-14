using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public enum CharacterAdvancementFailure
    {
        None,
        UnknownOption,
        InsufficientPoints,
        MaximumReached,
    }

    public sealed class GameplayProgressionSession
    {
        private readonly CharacterProfileDefinition profile;
        private readonly Dictionary<string, int> bonuses = new Dictionary<string, int>(StringComparer.Ordinal);
        private int unspentPoints;

        public GameplayProgressionSession(CharacterProfileDefinition characterProfile)
        {
            profile = characterProfile ?? throw new ArgumentNullException(nameof(characterProfile));
            unspentPoints = profile.StartingProgressionPoints;
        }

        public CharacterProfileDefinition Profile => profile;
        public CharacterProgressionSnapshot Snapshot =>
            new CharacterProgressionSnapshot(profile.IdentityId, unspentPoints, bonuses);

        public int GetEffectiveSkill(string skillId)
        {
            CharacterRating baseline = profile.GetSkill(skillId)
                ?? throw new ArgumentException($"Unknown skill '{skillId}'.", nameof(skillId));
            bonuses.TryGetValue(skillId, out int bonus);
            return baseline.Rating + bonus;
        }

        public bool TryAdvance(string optionId, out CharacterAdvancementFailure failure)
        {
            CharacterAdvancementOption option = profile.GetAdvancement(optionId);
            if (option == null) return Fail(CharacterAdvancementFailure.UnknownOption, out failure);
            bonuses.TryGetValue(option.SkillId, out int bonus);
            if (bonus >= option.MaximumBonus) return Fail(CharacterAdvancementFailure.MaximumReached, out failure);
            if (unspentPoints < option.PointCost) return Fail(CharacterAdvancementFailure.InsufficientPoints, out failure);
            unspentPoints -= option.PointCost;
            bonuses[option.SkillId] = bonus + 1;
            failure = CharacterAdvancementFailure.None;
            return true;
        }

        public CharacterPersistenceSnapshot CapturePersistence(GameplaySession gameplay, string actorId)
        {
            if (gameplay == null) throw new ArgumentNullException(nameof(gameplay));
            ScenarioActorDefinition definition = gameplay.Scenario.GetActor(actorId);
            if (!string.Equals(
                    definition.CharacterProfile?.IdentityId,
                    profile.IdentityId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Actor '{actorId}' does not own progression identity '{profile.IdentityId}'.");
            }

            GameplayActorSnapshot actor = gameplay.GetActor(actorId);
            return new CharacterPersistenceSnapshot(
                profile.IdentityId, Snapshot, actor.EquippedItemId, actor.Wounds);
        }

        private static bool Fail(CharacterAdvancementFailure value, out CharacterAdvancementFailure failure)
        {
            failure = value;
            return false;
        }
    }
}
