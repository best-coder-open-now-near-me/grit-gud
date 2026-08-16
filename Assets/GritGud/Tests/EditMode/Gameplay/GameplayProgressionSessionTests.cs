using System;
using System.Collections.Generic;
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

        [Test]
        public void ValidPartySaveRestoresIdentityBoundProgressionEquipmentAndWounds()
        {
            CharacterProfileDefinition mara = CreateProfile();
            CharacterProfileDefinition vale = CreateSecondProfile();
            GameplaySession authored = CreatePartyGameplay(mara, vale);
            var savedWounds = new ActorWoundSnapshot(
                "previous-scenario-mara",
                headWounds: 0,
                torsoWounds: 1,
                leftArmWounds: 0,
                rightArmWounds: 0,
                leftLegWounds: 0,
                rightLegWounds: 0,
                unlocalizedWounds: 0,
                movementPenalty: 1.5f);
            var save = new GameplayPartySave(
                GameplayPartySave.CurrentSchemaVersion,
                new[]
                {
                    new CharacterPersistenceSnapshot(
                        mara.IdentityId,
                        new CharacterProgressionSnapshot(
                            mara.IdentityId,
                            0,
                            new Dictionary<string, int>
                            {
                                ["skill.demolitions"] = 1,
                            }),
                        equippedItemId: null,
                        wounds: savedWounds),
                    new CharacterPersistenceSnapshot(
                        vale.IdentityId,
                        new CharacterProgressionSnapshot(
                            vale.IdentityId,
                            0,
                            new Dictionary<string, int>()),
                        "weapon.rifle",
                        new ActorWoundSnapshot("previous-vale", 0, 0f)),
                });

            var restoredGameplay = new GameplaySession(
                authored.Scenario,
                restoredParty: save);
            var restoredProgression = new GameplayPartyProgressionSession(
                restoredGameplay,
                save);

            GameplayActorSnapshot restoredMara = restoredGameplay.GetActor("mara");
            Assert.That(restoredMara.EquippedItemId, Is.Null);
            Assert.That(restoredMara.Wounds.ActorId, Is.EqualTo("mara"));
            Assert.That(restoredMara.Wounds.TorsoWounds, Is.EqualTo(1));
            Assert.That(restoredMara.TurnBudget.MovementOpportunity,
                Is.EqualTo(6.5f));
            Assert.That(
                restoredProgression.GetProgression("mara")
                    .GetEffectiveSkill("skill.demolitions"),
                Is.EqualTo(3));
            Assert.That(restoredGameplay.GetActor("vale").EquippedItemId,
                Is.EqualTo("weapon.rifle"));
        }

        [Test]
        public void SaveValidationRejectsProgressionThatCreatesPoints()
        {
            CharacterProfileDefinition mara = CreateProfile();
            CharacterProfileDefinition vale = CreateSecondProfile();
            GameplaySession gameplay = CreatePartyGameplay(mara, vale);
            var invalid = new GameplayPartySave(
                GameplayPartySave.CurrentSchemaVersion,
                new[]
                {
                    new CharacterPersistenceSnapshot(
                        mara.IdentityId,
                        new CharacterProgressionSnapshot(
                            mara.IdentityId,
                            1,
                            new Dictionary<string, int>
                            {
                                ["skill.demolitions"] = 1,
                            }),
                        "weapon.rifle",
                        new ActorWoundSnapshot("mara", 0, 0f)),
                    new CharacterPersistenceSnapshot(
                        vale.IdentityId,
                        new CharacterProgressionSnapshot(
                            vale.IdentityId,
                            0,
                            new Dictionary<string, int>()),
                        "weapon.rifle",
                        new ActorWoundSnapshot("vale", 0, 0f)),
                });

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    GameplayPartySaveValidator.Validate(
                        invalid,
                        gameplay.Scenario));

            Assert.That(exception.Message, Does.Contain("point budget"));
        }

        [Test]
        public void AdvancementIsExplorationOnlyAndPersistsImmediately()
        {
            CharacterProfileDefinition mara = CreateProfile();
            CharacterProfileDefinition vale = CreateSecondProfile();
            GameplaySession gameplay = CreatePartyGameplay(mara, vale);
            var progression = new GameplayPartyProgressionSession(gameplay);
            var store = new MemoryPartySaveStore();
            using var persistence = new GameplayPartyPersistenceSession(store);
            persistence.Bind(gameplay, progression);

            Assert.That(persistence.TryAdvance(
                "mara",
                "advance.demolitions-drill",
                out CharacterAdvancementFailure failure), Is.True);
            Assert.That(failure, Is.EqualTo(CharacterAdvancementFailure.None));
            Assert.That(store.SaveCount, Is.EqualTo(1));
            Assert.That(store.Saved.TryGetCharacter(
                mara.IdentityId,
                out CharacterPersistenceSnapshot saved), Is.True);
            Assert.That(saved.Progression.Bonuses["skill.demolitions"],
                Is.EqualTo(1));

            var equipment = new GameplayEquipmentSession(gameplay);
            Assert.That(equipment.TryResolve(
                "mara",
                "weapon.rifle",
                equip: false,
                out _,
                out EquipmentChangeFailure equipmentFailure), Is.True);
            Assert.That(equipmentFailure,
                Is.EqualTo(EquipmentChangeFailure.None));
            Assert.That(store.SaveCount, Is.EqualTo(2));
            Assert.That(store.Saved.TryGetCharacter(
                mara.IdentityId,
                out saved), Is.True);
            Assert.That(saved.EquippedItemId, Is.Null);

            Assert.That(gameplay.EnterTurnMode(), Is.True);
            CharacterAdvancementAvailability availability =
                persistence.EvaluateAdvancement(
                    "mara",
                    "advance.demolitions-drill");
            Assert.That(availability.CanAdvance, Is.False);
            Assert.That(availability.Failure,
                Is.EqualTo(CharacterAdvancementFailure.TurnBasedModeActive));
        }

        private sealed class MemoryPartySaveStore : IGameplayPartySaveStore
        {
            public GameplayPartySave Saved { get; private set; }

            public int SaveCount { get; private set; }

            public bool TryLoad(out GameplayPartySave save)
            {
                save = Saved;
                return save != null;
            }

            public void Save(GameplayPartySave save)
            {
                Saved = save;
                SaveCount++;
            }

            public void Delete()
            {
                Saved = null;
            }
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
