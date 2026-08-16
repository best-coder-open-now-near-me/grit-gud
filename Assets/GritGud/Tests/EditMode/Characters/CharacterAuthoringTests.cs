using System.Collections.Generic;
using GritGud.Application.Characters;
using GritGud.Domain.Characters;
using GritGud.Domain.Gameplay;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Characters
{
    public sealed class CharacterAuthoringTests
    {
        [Test]
        public void AppearanceDeepCopyDoesNotShareAccessorySelections()
        {
            var source = new CharacterAppearanceData { bodyId = "body.male" };
            source.SetAccessory(CharacterAppearanceSlotIds.Hair, "hair.short");

            CharacterAppearanceData copy = source.DeepCopy();
            copy.SetAccessory(CharacterAppearanceSlotIds.Hair, "hair.long");

            Assert.That(source.GetAccessory(CharacterAppearanceSlotIds.Hair),
                Is.EqualTo("hair.short"));
            Assert.That(copy.GetAccessory(CharacterAppearanceSlotIds.Hair),
                Is.EqualTo("hair.long"));
        }

        [Test]
        public void GeneratorIsDeterministicAndRespectsCompatibility()
        {
            var bodies = new[]
            {
                new CharacterAuthoringOption("body.male", string.Empty, "male"),
            };
            var accessories = new[]
            {
                new CharacterAuthoringOption(
                    "hair.male",
                    CharacterAppearanceSlotIds.Hair,
                    "male"),
                new CharacterAuthoringOption(
                    "hair.female",
                    CharacterAppearanceSlotIds.Hair,
                    "female"),
            };

            CharacterAppearanceData first = CharacterAppearanceGenerator.Generate(
                7421,
                bodies,
                accessories);
            CharacterAppearanceData second = CharacterAppearanceGenerator.Generate(
                7421,
                bodies,
                accessories);

            Assert.That(second.bodyId, Is.EqualTo(first.bodyId));
            Assert.That(
                second.GetAccessory(CharacterAppearanceSlotIds.Hair),
                Is.EqualTo(first.GetAccessory(CharacterAppearanceSlotIds.Hair)));
            Assert.That(
                first.GetAccessory(CharacterAppearanceSlotIds.Hair),
                Is.Not.EqualTo("hair.female"));
        }

        [Test]
        public void ValidatorRejectsAccessoryInWrongSlot()
        {
            var document = new CharacterDocument
            {
                characterId = "character.test",
                displayName = "Test",
                appearance = new CharacterAppearanceData { bodyId = "body.male" },
            };
            document.appearance.SetAccessory(
                CharacterAppearanceSlotIds.Headwear,
                "hair.short");
            var content = new CharacterAppearanceValidationContent(
                new[] { new KeyValuePair<string, string>("body.male", "male") },
                new[]
                {
                    new CharacterAccessoryValidationEntry(
                        "hair.short",
                        CharacterAppearanceSlotIds.Hair,
                        "male"),
                });

            IReadOnlyList<string> issues = CharacterValidator.Validate(document, content);

            Assert.That(issues, Has.Some.Contains("does not belong"));
        }

        [Test]
        public void SessionTracksUndoRedoAndSavedState()
        {
            var original = new CharacterDocument
            {
                characterId = "character.test",
                displayName = "Before",
            };
            var session = new CharacterAuthoringSession(original);
            CharacterDocument changed = session.CreateSnapshot();
            changed.displayName = "After";

            session.Apply("Rename", changed);
            Assert.That(session.IsDirty, Is.True);
            Assert.That(session.Undo(), Is.True);
            Assert.That(session.CreateSnapshot().displayName, Is.EqualTo("Before"));
            Assert.That(session.IsDirty, Is.False);
            Assert.That(session.Redo(), Is.True);
            Assert.That(session.CreateSnapshot().displayName, Is.EqualTo("After"));
        }

        [Test]
        public void DocumentDeepCopyKeepsBuildAndLoadoutDetached()
        {
            var source = new CharacterDocument();
            source.build.SetRating(source.build.attributes, CoreAttributeIds.Strength, 4);
            source.startingLoadout.items.Add(new CharacterLoadoutItemData
            {
                itemId = "weapon.rifle",
                quantity = 1,
                hotbarSlot = 1,
            });

            CharacterDocument copy = source.DeepCopy();
            copy.build.SetRating(copy.build.attributes, CoreAttributeIds.Strength, 2);
            copy.startingLoadout.items[0].quantity = 3;

            Assert.That(source.build.GetRating(source.build.attributes, CoreAttributeIds.Strength),
                Is.EqualTo(4));
            Assert.That(source.startingLoadout.items[0].quantity, Is.EqualTo(1));
        }

        [Test]
        public void ValidatorRejectsIncompleteBuildAndConflictingLoadout()
        {
            var document = new CharacterDocument
            {
                characterId = "character.invalid-build",
                displayName = "Invalid Build",
                appearance = new CharacterAppearanceData { bodyId = "body.male" },
            };
            document.build.attributes.RemoveAll(
                value => value.id == CoreAttributeIds.Charisma);
            document.startingLoadout.items.Add(new CharacterLoadoutItemData
            {
                itemId = "weapon.rifle",
                hotbarSlot = 1,
            });
            document.startingLoadout.items.Add(new CharacterLoadoutItemData
            {
                itemId = "weapon.knife",
                hotbarSlot = 1,
            });
            var content = new CharacterAppearanceValidationContent(
                new[] { new KeyValuePair<string, string>("body.male", "male") },
                null);

            IReadOnlyList<string> issues = CharacterValidator.Validate(document, content);

            Assert.That(issues, Has.Some.Contains(CoreAttributeIds.Charisma));
            Assert.That(issues, Has.Some.Contains("duplicates hotbar slot 1"));
        }
    }
}
