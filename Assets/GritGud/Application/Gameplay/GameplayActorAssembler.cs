using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    internal static class GameplayActorAssembler
    {
        internal static PlayerPartyDefinition CreatePlayerParty(
            ScenarioPlayerPartyData data,
            IReadOnlyDictionary<string, ScenarioActorContentData> actors)
        {
            Require(data != null, "Scenario requires a player party.");
            Require(
                data.actorIds != null && data.actorIds.Count > 0,
                "Player party requires at least one controlled actor.");
            RequireText(
                data.initiallySelectedActorId,
                "Player party initially selected actor ID");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var characterIdentities = new HashSet<string>(StringComparer.Ordinal);
            foreach (string actorId in data.actorIds)
            {
                RequireText(actorId, "Player party actor ID");
                Require(
                    ids.Add(actorId),
                    $"Player party actor '{actorId}' is listed more than once.");
                Require(
                    actors.TryGetValue(actorId, out ScenarioActorContentData actor),
                    $"Player party actor '{actorId}' is not defined.");
                Require(
                    !GameplayActorCombatAssembler.HasAuthoredEnemyBehavior(
                        actor.combat?.enemyBehavior),
                    $"Player party actor '{actorId}' cannot own enemy behavior.");
                string identityId = actor.characterProfile?.identityId;
                RequireText(
                    identityId,
                    $"Player party actor '{actorId}' character identity ID");
                Require(
                    characterIdentities.Add(identityId),
                    $"Player party character identity '{identityId}' is used more than once.");
            }

            Require(
                ids.Contains(data.initiallySelectedActorId),
                $"Initially selected actor '{data.initiallySelectedActorId}' is not in the player party.");
            return new PlayerPartyDefinition(
                data.actorIds,
                data.initiallySelectedActorId);
        }

        internal static Dictionary<string, ScenarioActorContentData> IndexActors(
            IReadOnlyList<ScenarioActorContentData> actors)
        {
            Require(actors.Count > 0, "A scenario requires at least one actor.");
            var index = new Dictionary<string, ScenarioActorContentData>(
                StringComparer.Ordinal);
            foreach (ScenarioActorContentData actor in actors)
            {
                Require(actor != null, "Scenario actors cannot contain null entries.");
                RequireText(actor.id, "Actor ID");
                RequireText(actor.displayName, $"Actor '{actor.id}' display name");
                RequireText(actor.presentationId, $"Actor '{actor.id}' presentation ID");
                Require(
                    index.TryAdd(actor.id, actor),
                    $"Actor '{actor.id}' is defined more than once.");
                RequireFinitePositive(actor.mass, $"Actor '{actor.id}' mass");
                GameplayDisplacementAssembler.ParseSize(actor.sizeClass);
                ValidateCharacterProfile(actor);
                ValidateControl(actor);
                GameplayDisplacementAssembler.ValidateActor(actor);

                GameplayActorCombatAssembler.ValidateAttack(
                    actor.id,
                    actor.attackCapability);
                GameplayInventoryAssembler.Validate(actor);
                GameplayActorCombatAssembler.ValidateCombat(actor);
            }

            foreach (ScenarioActorContentData actor in actors)
            {
                GameplayActorCombatAssembler.ValidateEncounterReferences(
                    actor,
                    index);
            }

            return index;
        }

        internal static ScenarioActorDefinition CreateActorDefinition(
            ScenarioActorContentData actor)
        {
            ScenarioTurnBudgetData budget = actor.turnBudget ??
                throw new InvalidOperationException(
                    $"Actor '{actor.id}' does not define a turn budget.");
            CharacterProfileDefinition characterProfile =
                CreateCharacterProfile(actor.characterProfile);
            CharacterDerivedStatistics derived =
                characterProfile.DerivedStatistics;
            var pose = new GameplayActorPose(
                ToPosition(actor.position),
                actor.facingDegrees,
                ParseStance(actor.stance));
            var startingBudget = new TurnBudget(
                budget.actionPoints,
                derived.MovementOpportunity);
            IReadOnlyList<InventoryItemDefinition> inventory =
                GameplayInventoryAssembler.CreateDefinitions(actor);
            return inventory.Count == 0
                ? new ScenarioActorDefinition(
                    actor.id,
                    derived.Initiative,
                    pose,
                    startingBudget,
                    GameplayActorCombatAssembler.CreateAttackDefinition(
                        actor.id,
                        actor.attackCapability),
                    GameplayDisplacementAssembler.CreateAbility(actor),
                    GameplayActorCombatAssembler.CreateCombatDefinition(
                        actor.combat),
                    characterProfile)
                : new ScenarioActorDefinition(
                    actor.id,
                    derived.Initiative,
                    pose,
                    startingBudget,
                    inventory,
                    GameplayInventoryAssembler.NormalizeOptionalId(
                        actor.initiallyEquippedItemId),
                    characterProfile,
                    GameplayDisplacementAssembler.CreateAbility(actor),
                    GameplayActorCombatAssembler.CreateCombatDefinition(
                        actor.combat));
        }

        private static CharacterProfileDefinition CreateCharacterProfile(
            ScenarioCharacterProfileData data)
        {
            if (!HasAuthoredCharacterProfile(data))
            {
                throw new InvalidOperationException(
                    "Scenario actors require an authored character profile.");
            }
            var attributes = new List<CharacterRating>();
            foreach (ScenarioCharacterRatingData value in
                data.attributes ?? new List<ScenarioCharacterRatingData>())
                attributes.Add(new CharacterRating(value.id, value.rating));
            var skills = new List<CharacterRating>();
            foreach (ScenarioCharacterRatingData value in
                data.skills ?? new List<ScenarioCharacterRatingData>())
                skills.Add(new CharacterRating(value.id, value.rating));
            return new CharacterProfileDefinition(
                data.identityId, data.displayName, data.archetype,
                attributes, skills, data.talentIds ?? new List<string>());
        }

        private static void ValidateCharacterProfile(ScenarioActorContentData actor)
        {
            ScenarioCharacterProfileData data = actor.characterProfile;
            Require(
                HasAuthoredCharacterProfile(data),
                $"Actor '{actor.id}' requires a character profile.");
            RequireText(data.identityId, $"Actor '{actor.id}' character identity");
            RequireText(data.displayName, $"Actor '{actor.id}' character display name");
            RequireText(data.archetype, $"Actor '{actor.id}' archetype");
            try
            {
                _ = CreateCharacterProfile(data);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(
                    $"Actor '{actor.id}' character profile is invalid: "
                    + exception.Message,
                    exception);
            }
        }

        private static bool HasAuthoredCharacterProfile(
            ScenarioCharacterProfileData data) =>
            data != null
            && (!string.IsNullOrWhiteSpace(data.identityId)
                || !string.IsNullOrWhiteSpace(data.displayName)
                || !string.IsNullOrWhiteSpace(data.archetype)
                || (data.attributes != null && data.attributes.Count > 0)
                || (data.skills != null && data.skills.Count > 0)
                || (data.talentIds != null && data.talentIds.Count > 0));

        internal static CloseQuartersControlProfile CreateControlProfile(
            ScenarioActorContentData actor)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            CharacterProfileDefinition characterProfile =
                CreateCharacterProfile(actor.characterProfile);
            ValidateControl(actor);
            CharacterRating controlSkill = characterProfile.GetSkill(
                CharacterSkillIds.CloseQuarters);
            return new CloseQuartersControlProfile(
                characterProfile.CoreAttributes.Strength,
                controlSkill.Rating,
                string.IsNullOrWhiteSpace(actor.control.talentId)
                    ? null
                    : actor.control.talentId,
                actor.control.talentModifier);
        }

        private static void ValidateControl(ScenarioActorContentData actor)
        {
            ScenarioControlProfileData control = actor.control;
            Require(control != null, $"Actor '{actor.id}' has no control profile.");
            CharacterProfileDefinition characterProfile =
                CreateCharacterProfile(actor.characterProfile);
            Require(
                characterProfile.GetSkill(CharacterSkillIds.CloseQuarters)
                    != null,
                $"Actor '{actor.id}' requires skill '{CharacterSkillIds.CloseQuarters}'.");
            Require(
                control.talentModifier == 0
                || !string.IsNullOrWhiteSpace(control.talentId),
                $"Actor '{actor.id}' cannot have a talent modifier without a talent ID.");
            Require(
                string.IsNullOrWhiteSpace(control.talentId)
                || ContainsId(
                    characterProfile.TalentIds,
                    control.talentId),
                $"Actor '{actor.id}' control talent '{control.talentId}' is not owned by its character profile.");
        }

        private static bool ContainsId(
            IReadOnlyList<string> values,
            string expected)
        {
            foreach (string value in values)
            {
                if (string.Equals(value, expected, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static GameplayPosition ToPosition(Float3Data position)
        {
            return new GameplayPosition(position.x, position.y, position.z);
        }

        private static ActorStance ParseStance(string value)
        {
            if (string.Equals(value, "standing", StringComparison.OrdinalIgnoreCase))
            {
                return ActorStance.Standing;
            }

            if (string.Equals(value, "crouched", StringComparison.OrdinalIgnoreCase))
            {
                return ActorStance.Crouched;
            }

            throw new InvalidOperationException($"Unknown actor stance '{value}'.");
        }

        private static ActionMobility ParseMobility(string value) =>
            GameplayScenarioAssemblyValidation.ParseMobility(value);

        private static void RequireText(string value, string label) =>
            GameplayScenarioAssemblyValidation.RequireText(value, label);

        private static void RequireFinitePositive(float value, string label) =>
            GameplayScenarioAssemblyValidation.RequireFinitePositive(
                value,
                label);

        private static void Require(bool condition, string message) =>
            GameplayScenarioAssemblyValidation.Require(condition, message);
    }
}
