using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Presentation.Gameplay;
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
                Assert.DoesNotThrow(() => GameplayContentLoader.LoadCommitted(
                    library.OpenForPlay(entry.ResourceKey)));
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
            Assert.That(first.schemaVersion, Is.EqualTo(LevelDocument.CurrentSchemaVersion));
            Assert.That(first.environment.practicalLights, Has.Count.EqualTo(5));
            Assert.That(first.dressing.decals, Has.Count.EqualTo(4));
            Assert.That(first.dressing.ambientVfx, Has.Count.EqualTo(3));
            Assert.That(first.dressing.audioZones, Has.Count.EqualTo(3));
            first.displayName = "Changed";

            Assert.That(
                library.OpenForEditing(entry.ResourceKey).displayName,
                Is.EqualTo("Depot Yard"));
        }

        [Test]
        public void DepotPublishesTopplingVerificationGroupAndSettledPile()
        {
            CommittedLevelLibrary library =
                UnityCommittedLevelLibrary.LoadDefault();
            LevelDocument level = library.OpenForEditing(
                UnityCommittedLevelLibrary.DefaultResourceKey);
            GameplayContentPackage content =
                GameplayContentLoader.LoadCommitted(level);

            Assert.That(level.groups.Any(group =>
                group.id == "toppling-verification"), Is.True);
            Assert.That(level.scenario.props.Select(prop => prop.entityId),
                Does.Contain("crate-exposure-demo"));
            Assert.That(level.scenario.props.Select(prop => prop.entityId),
                Does.Contain("barrel-yard-01"));
            Assert.That(level.scenario.props.Select(prop => prop.entityId),
                Does.Contain("crate-warehouse-03"));
            LevelEntity settledTop = level.entities.Single(entity =>
                entity.id == "crate-warehouse-03");
            Assert.That(settledTop.groupId,
                Is.EqualTo("toppling-verification"));
            Assert.That(settledTop.transform.position.y, Is.GreaterThan(0f));
            Assert.That(
                settledTop.transform.pitchDegrees != 0f
                    || settledTop.transform.rollDegrees != 0f,
                Is.True);
            Assert.That(content.Assembly.TryGetDisplacementSubject(
                "crate-warehouse-03",
                out DisplacementSubjectDefinition pileSubject), Is.True);
            Assert.That(pileSubject.Toppling, Is.Not.Null);
            Assert.That(content.FractureSpatialProfiles.Keys,
                Does.Contain("prop.crate.standard"));
            Assert.That(content.FractureSpatialProfiles.Keys,
                Does.Contain("prop.barrel.metal"));
            Assert.That(
                content.FractureSpatialProfiles.Values.All(profile =>
                    profile.ChunkCount == 12),
                Is.True);

            Assert.That(content.Assembly.TryGetDisplacementSubject(
                "barrel-yard-01",
                out DisplacementSubjectDefinition pinSubject), Is.True);
            Assert.That(pinSubject.Toppling, Is.Not.Null);
            Assert.That(pinSubject.Pinning, Is.Not.Null);
            Assert.That(pinSubject.Pinning.MaximumActorMass,
                Is.EqualTo(90f));
            Assert.That(
                content.Assembly.GetActorDefinition("player")
                    .GetDisplacementAction("close-quarters.push-off")
                    .AllowedResults,
                Is.EqualTo(DisplacementResultPolicies.Release));
            Assert.That(
                content.Assembly.GetActorDefinition("oren-vale")
                    .GetDisplacementAction("close-quarters.push-off")
                    .AllowedResults,
                Is.EqualTo(DisplacementResultPolicies.Release));
            Assert.That(
                content.Assembly.GetActorDefinition("depot-rifleman")
                    .GetDisplacementAction("close-quarters.push-off")
                    .AllowedResults,
                Is.EqualTo(DisplacementResultPolicies.Release));
        }
    }
}
