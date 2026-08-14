using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayProgressionSessionTests
    {
        [Test]
        public void ConstrainedAdvancementSpendsPointWithoutChangingBaselineIdentity()
        {
            CharacterProfileDefinition profile = CreateProfile();
            var progression = new GameplayProgressionSession(profile);

            Assert.That(progression.GetEffectiveSkill("skill.demolitions"), Is.EqualTo(2));
            Assert.That(progression.TryAdvance(
                "advance.demolitions-drill", out CharacterAdvancementFailure failure), Is.True);
            Assert.That(failure, Is.EqualTo(CharacterAdvancementFailure.None));
            Assert.That(progression.GetEffectiveSkill("skill.demolitions"), Is.EqualTo(3));
            Assert.That(progression.Snapshot.UnspentPoints, Is.Zero);
            Assert.That(profile.GetSkill("skill.demolitions").Rating, Is.EqualTo(2));
            Assert.That(profile.IdentityId, Is.EqualTo("character.mara-vance"));
        }

        [Test]
        public void AdvancementCannotExceedAuthoredOption()
        {
            var progression = new GameplayProgressionSession(CreateProfile());
            Assert.That(progression.TryAdvance("advance.demolitions-drill", out _), Is.True);
            Assert.That(progression.TryAdvance(
                "advance.demolitions-drill", out CharacterAdvancementFailure failure), Is.False);
            Assert.That(failure, Is.EqualTo(CharacterAdvancementFailure.MaximumReached));
        }

        [Test]
        public void PersistenceKeepsIdentityProgressionEquipmentAndWoundsSeparate()
        {
            CharacterProfileDefinition profile = CreateProfile();
            var progression = new GameplayProgressionSession(profile);
            progression.TryAdvance("advance.demolitions-drill", out _);
            GameplaySession gameplay = CreateGameplay(profile);

            CharacterPersistenceSnapshot saved = progression.CapturePersistence(gameplay, "player");

            Assert.That(saved.IdentityId, Is.EqualTo(profile.IdentityId));
            Assert.That(saved.Progression.Bonuses["skill.demolitions"], Is.EqualTo(1));
            Assert.That(saved.EquippedItemId, Is.EqualTo("weapon.rifle"));
            Assert.That(saved.Wounds.WoundCount, Is.Zero);
        }

        [Test]
        public void PartyProgressionOwnsIndependentIdentityBoundAggregates()
        {
            CharacterProfileDefinition mara = CreateProfile();
            CharacterProfileDefinition vale = CreateSecondProfile();
            GameplaySession gameplay = CreatePartyGameplay(mara, vale);
            var progression = new GameplayPartyProgressionSession(gameplay);

            Assert.That(progression.TryAdvance(
                "mara",
                "advance.demolitions-drill",
                out CharacterAdvancementFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(CharacterAdvancementFailure.None));
            Assert.That(
                progression.GetProgression("mara")
                    .GetEffectiveSkill("skill.demolitions"),
                Is.EqualTo(3));
            Assert.That(
                progression.GetProgression("vale")
                    .GetEffectiveSkill("skill.demolitions"),
                Is.EqualTo(1));
            var persistence = progression.CapturePersistence();
            Assert.That(persistence, Has.Count.EqualTo(2));
            Assert.That(persistence[0].IdentityId,
                Is.EqualTo("character.mara-vance"));
            Assert.That(persistence[1].IdentityId,
                Is.EqualTo("character.oren-vale"));
        }

        [Test]
        public void PersistenceRejectsCrossCharacterCapture()
        {
            CharacterProfileDefinition mara = CreateProfile();
            CharacterProfileDefinition vale = CreateSecondProfile();
            GameplaySession gameplay = CreatePartyGameplay(mara, vale);
            var progression = new GameplayProgressionSession(mara);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => progression.CapturePersistence(gameplay, "vale"));

            Assert.That(exception.Message, Does.Contain(mara.IdentityId));
        }

        private static CharacterProfileDefinition CreateProfile() =>
            new CharacterProfileDefinition(
                "character.mara-vance", "Mara Vance", "Field Operative",
                new[]
                {
                    new CharacterRating(CoreAttributeIds.Strength, 3),
                    new CharacterRating(CoreAttributeIds.Dexterity, 4),
                    new CharacterRating(CoreAttributeIds.Grit, 4),
                    new CharacterRating(CoreAttributeIds.Charisma, 3),
                },
                new[] { new CharacterRating("skill.demolitions", 2) },
                new[] { "talent.steady-hands" },
                1,
                new[]
                {
                    new CharacterAdvancementOption(
                        "advance.demolitions-drill", "skill.demolitions", 1, 1),
                });

        private static CharacterProfileDefinition CreateSecondProfile() =>
            new CharacterProfileDefinition(
                "character.oren-vale", "Oren Vale", "Scout",
                new[]
                {
                    new CharacterRating(CoreAttributeIds.Strength, 2),
                    new CharacterRating(CoreAttributeIds.Dexterity, 5),
                    new CharacterRating(CoreAttributeIds.Grit, 3),
                    new CharacterRating(CoreAttributeIds.Charisma, 3),
                },
                new[] { new CharacterRating("skill.demolitions", 1) },
                Array.Empty<string>(),
                0,
                Array.Empty<CharacterAdvancementOption>());

        private static GameplaySession CreateGameplay(CharacterProfileDefinition profile)
        {
            var rifle = new InventoryItemDefinition(
                "weapon.rifle", "Rifle", 1, InventoryItemKind.Weapon,
                new ActionCost(0, 0f, ActionMobility.Mobile),
                EquipmentEffectSet.None,
                new AttackDefinition(
                    "attack.rifle", "Fire rifle",
                    new ActionCost(1, 0f, ActionMobility.Mobile), 1f,
                    accuracyDecay: AccuracyDecayDefinition.None));
            var actor = new ScenarioActorDefinition(
                "player", 10,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f),
                new[] { rifle }, "weapon.rifle", profile);
            return new GameplaySession(new ScenarioDefinition(
                "progression-test", new ScenarioTimingDefinition(1f),
                new[] { actor }, Array.Empty<ScenarioObjectiveDefinition>()));
        }

        private static GameplaySession CreatePartyGameplay(
            CharacterProfileDefinition maraProfile,
            CharacterProfileDefinition valeProfile)
        {
            var rifle = new InventoryItemDefinition(
                "weapon.rifle", "Rifle", 1, InventoryItemKind.Weapon,
                new ActionCost(0, 0f, ActionMobility.Mobile),
                EquipmentEffectSet.None,
                new AttackDefinition(
                    "attack.rifle", "Fire rifle",
                    new ActionCost(1, 0f, ActionMobility.Mobile), 1f,
                    accuracyDecay: AccuracyDecayDefinition.None));
            var mara = new ScenarioActorDefinition(
                "mara", 10,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f),
                new[] { rifle }, "weapon.rifle", maraProfile);
            var vale = new ScenarioActorDefinition(
                "vale", 9,
                new GameplayActorPose(new GameplayPosition(2f, 0f, 0f), 0f),
                new TurnBudget(4, 8f),
                new[] { rifle }, "weapon.rifle", valeProfile);
            var party = new PlayerPartyDefinition(
                new[] { "mara", "vale" },
                "mara");
            return new GameplaySession(new ScenarioDefinition(
                "party-progression-test",
                new ScenarioTimingDefinition(1f),
                new[] { mara, vale },
                Array.Empty<ScenarioObjectiveDefinition>(),
                playerParty: party));
        }
    }
}
