using System.Collections.Generic;
using GritGud.Application.Characters;
using GritGud.Domain.Characters;
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
    }
}
