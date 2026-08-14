using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Levels
{
    public sealed class CommittedLevelLibraryTests
    {
        [Test]
        public void LibrarySortsLevelsAndReturnsDetachedSnapshots()
        {
            var serializer = new StubSerializer(
                ("bravo", CreateLevel("bravo", "Bravo")),
                ("alpha", CreateLevel("alpha", "Alpha")));
            var library = new CommittedLevelLibrary(
                new[]
                {
                    new CommittedLevelSource("levels/bravo", "bravo", "bravo"),
                    new CommittedLevelSource("levels/alpha", "alpha", "alpha"),
                },
                serializer,
                CreateValidationContent());

            Assert.That(library.Entries[0].DisplayName, Is.EqualTo("Alpha"));
            Assert.That(library.Entries[1].DisplayName, Is.EqualTo("Bravo"));
            Assert.That(library.Entries[0].CanPlay, Is.True);

            LevelDocument first = library.OpenForEditing("levels/alpha");
            first.displayName = "Changed";
            LevelDocument second = library.OpenForEditing("levels/alpha");

            Assert.That(second.displayName, Is.EqualTo("Alpha"));
        }

        [Test]
        public void InvalidSourceDoesNotHideValidCommittedLevels()
        {
            var serializer = new StubSerializer(
                ("valid", CreateLevel("valid", "Valid")));
            var library = new CommittedLevelLibrary(
                new[]
                {
                    new CommittedLevelSource("levels/broken", "Broken", "broken"),
                    new CommittedLevelSource("levels/valid", "Valid", "valid"),
                },
                serializer,
                CreateValidationContent());

            CommittedLevelEntry broken = library.Find("levels/broken");
            Assert.That(broken, Is.Not.Null);
            Assert.That(broken.CanEdit, Is.False);
            Assert.That(broken.CanPlay, Is.False);
            Assert.That(broken.StatusMessage, Does.Contain("could not be read"));
            Assert.That(library.Find("levels/valid").CanPlay, Is.True);
        }

        [Test]
        public void DuplicateStableIdsRejectEveryConflictingEntry()
        {
            var serializer = new StubSerializer(
                ("one", CreateLevel("same-id", "One")),
                ("two", CreateLevel("same-id", "Two")));
            var library = new CommittedLevelLibrary(
                new[]
                {
                    new CommittedLevelSource("levels/one", "One", "one"),
                    new CommittedLevelSource("levels/two", "Two", "two"),
                },
                serializer,
                CreateValidationContent());

            Assert.That(library.Entries.Count, Is.EqualTo(2));
            Assert.That(library.Entries[0].CanEdit, Is.False);
            Assert.That(library.Entries[1].CanEdit, Is.False);
            Assert.That(library.Entries[0].StatusMessage, Does.Contain("duplicated"));
            Assert.Throws<InvalidOperationException>(
                () => library.OpenForPlay("levels/one"));
        }

        [Test]
        public void UnknownActorTemplateRemainsEditableButCannotPlay()
        {
            LevelDocument document = CreateLevel("unknown-template", "Unknown Template");
            document.scenario.actors[0].templateId = "missing-template";
            var serializer = new StubSerializer(("level", document));
            var library = new CommittedLevelLibrary(
                new[]
                {
                    new CommittedLevelSource("levels/unknown", "Unknown", "level"),
                },
                serializer,
                CreateValidationContent());

            CommittedLevelEntry entry = library.Entries.Single();

            Assert.That(entry.CanEdit, Is.True);
            Assert.That(entry.CanPlay, Is.False);
            Assert.That(
                entry.PublishIssues,
                Has.Some.Matches<LevelValidationIssue>(issue =>
                    issue.Code == "scenario.actor.template.unknown"));
        }

        private static LevelDocument CreateLevel(string id, string displayName)
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty(displayName);
            document.levelId = id;
            return document;
        }

        private static LevelValidationContent CreateValidationContent()
        {
            return new LevelValidationContent(
                actorPresentationsByTemplateId: new[]
                {
                    new KeyValuePair<string, string>("player", "actor.player.default"),
                },
                knownActorPresentationIds: new[] { "actor.player.default" });
        }

        private sealed class StubSerializer : ILevelSerializer
        {
            private readonly Dictionary<string, LevelDocument> documents =
                new Dictionary<string, LevelDocument>(StringComparer.Ordinal);

            public StubSerializer(params (string Key, LevelDocument Document)[] values)
            {
                foreach ((string key, LevelDocument document) in values)
                {
                    documents.Add(key, document);
                }
            }

            public string Serialize(LevelDocument document, bool prettyPrint = true)
            {
                throw new NotSupportedException();
            }

            public LevelDocument Deserialize(string text)
            {
                return documents.TryGetValue(text, out LevelDocument document)
                    ? document.DeepCopy()
                    : throw new LevelSerializationException(
                        "The committed level could not be read.");
            }
        }
    }
}
