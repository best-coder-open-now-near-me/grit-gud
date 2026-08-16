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
        TurnBasedModeActive,
    }

    public sealed class CharacterAdvancementAvailability
    {
        public CharacterAdvancementAvailability(
            CharacterAdvancementOption option,
            int baselineRating,
            int currentBonus,
            CharacterAdvancementFailure failure)
        {
            Option = option ?? throw new ArgumentNullException(nameof(option));
            if (baselineRating < 0)
                throw new ArgumentOutOfRangeException(nameof(baselineRating));
            if (currentBonus < 0)
                throw new ArgumentOutOfRangeException(nameof(currentBonus));
            BaselineRating = baselineRating;
            CurrentBonus = currentBonus;
            Failure = failure;
        }

        public CharacterAdvancementOption Option { get; }

        public int BaselineRating { get; }

        public int CurrentBonus { get; }

        public int EffectiveRating => BaselineRating + CurrentBonus;

        public CharacterAdvancementFailure Failure { get; }

        public bool CanAdvance => Failure == CharacterAdvancementFailure.None;

        internal CharacterAdvancementAvailability WithFailure(
            CharacterAdvancementFailure failure) =>
            new CharacterAdvancementAvailability(
                Option,
                BaselineRating,
                CurrentBonus,
                failure);
    }

    public sealed class GameplayProgressionSession
    {
        private readonly CharacterProfileDefinition profile;
        private readonly Dictionary<string, int> bonuses = new Dictionary<string, int>(StringComparer.Ordinal);
        private int unspentPoints;

        public GameplayProgressionSession(
            CharacterProfileDefinition characterProfile,
            CharacterProgressionSnapshot restoredProgression = null)
        {
            profile = characterProfile ?? throw new ArgumentNullException(nameof(characterProfile));
            if (restoredProgression == null)
            {
                unspentPoints = profile.StartingProgressionPoints;
                return;
            }

            GameplayPartySaveValidator.ValidateProgression(
                profile,
                restoredProgression);
            unspentPoints = restoredProgression.UnspentPoints;
            foreach (KeyValuePair<string, int> bonus in
                restoredProgression.Bonuses)
            {
                if (bonus.Value > 0)
                    bonuses.Add(bonus.Key, bonus.Value);
            }
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

        public CharacterAdvancementAvailability EvaluateAdvancement(
            string optionId)
        {
            CharacterAdvancementOption option = profile.GetAdvancement(optionId);
            if (option == null)
                return null;

            CharacterRating baseline = profile.GetSkill(option.SkillId);
            bonuses.TryGetValue(option.SkillId, out int bonus);
            CharacterAdvancementFailure failure =
                bonus >= option.MaximumBonus
                    ? CharacterAdvancementFailure.MaximumReached
                    : unspentPoints < option.PointCost
                        ? CharacterAdvancementFailure.InsufficientPoints
                        : CharacterAdvancementFailure.None;
            return new CharacterAdvancementAvailability(
                option,
                baseline.Rating,
                bonus,
                failure);
        }

        public bool TryAdvance(string optionId, out CharacterAdvancementFailure failure)
        {
            CharacterAdvancementAvailability availability =
                EvaluateAdvancement(optionId);
            if (availability == null)
                return Fail(CharacterAdvancementFailure.UnknownOption, out failure);
            if (!availability.CanAdvance)
                return Fail(availability.Failure, out failure);
            CharacterAdvancementOption option = availability.Option;
            unspentPoints -= option.PointCost;
            bonuses[option.SkillId] = availability.CurrentBonus + 1;
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
