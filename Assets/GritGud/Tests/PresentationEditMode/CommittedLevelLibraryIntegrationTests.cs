using GritGud.Application.Levels;
using GritGud.Presentation.Levels;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class CommittedLevelLibraryIntegrationTests
    {
        [Test]
        public void EveryPublishedLevelIsEditableAndPlayable()
        {
            CommittedLevelLibrary library =
                UnityCommittedLevelLibrary.LoadDefault();

            Assert.That(library.Entries, Is.Not.Empty);
            foreach (CommittedLevelEntry entry in library.Entries)
            {
                Assert.That(
                    entry.CanEdit,
                    Is.True,
                    $"{entry.ResourceKey}: {entry.StatusMessage}");
                Assert.That(
                    entry.CanPlay,
                    Is.True,
                    $"{entry.ResourceKey}: {entry.StatusMessage}");
            }
        }

        [Test]
        public void DefaultPublishedLevelUsesStableIdentityAndDetachedSnapshots()
        {
            CommittedLevelLibrary library =
                UnityCommittedLevelLibrary.LoadDefault();
            CommittedLevelEntry entry = library.Find(
                UnityCommittedLevelLibrary.DefaultResourceKey);

            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.LevelId, Is.EqualTo("main-depot-yard-v1"));
            Assert.That(entry.DisplayName, Is.EqualTo("Depot Yard"));

            var first = library.OpenForEditing(entry.ResourceKey);
            first.displayName = "Changed";

            Assert.That(
                library.OpenForEditing(entry.ResourceKey).displayName,
                Is.EqualTo("Depot Yard"));
        }
    }
}
