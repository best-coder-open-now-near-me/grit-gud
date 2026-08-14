using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Levels
{
    public sealed class LevelValidatorTests
    {
        [Test]
        public void EmptyFactoryDocumentIsValid()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty("Test Level");

            var issues = LevelValidator.Validate(document);

            Assert.That(LevelValidator.HasErrors(issues), Is.False);
        }

        [Test]
        public void DuplicateEntityIdsIdentifyTheEntity()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.entities.Add(CreateEntity("duplicate"));
            document.entities.Add(CreateEntity("duplicate"));

            var issues = LevelValidator.Validate(document);

            LevelValidationIssue issue = issues.Single(item => item.Code == "entity.id.duplicate");
            Assert.That(issue.EntityId, Is.EqualTo("duplicate"));
        }

        [Test]
        public void UnknownArchetypeIsRejectedWhenCatalogIsProvided()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.entities.Add(CreateEntity("entity-1"));
            var known = new System.Collections.Generic.HashSet<string> { "structure.floor.standard" };

            var issues = LevelValidator.Validate(document, known);

            Assert.That(issues.Any(item => item.Code == "entity.archetype.unknown"), Is.True);
        }

        [Test]
        public void OutOfBoundsEntityIsAWarningRatherThanAnError()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            LevelEntity entity = CreateEntity("entity-1");
            entity.transform = new LevelTransformData(new Float3Data(100f, 0f, 0f), 0f);
            document.entities.Add(entity);

            var issues = LevelValidator.Validate(document);

            Assert.That(issues.Any(item => item.Code == "entity.outside-bounds"), Is.True);
            Assert.That(LevelValidator.HasErrors(issues), Is.False);
        }

        [Test]
        public void DisabledDestructiblePlaceholderIsIgnored()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            LevelEntity entity = CreateEntity("entity-1");
            entity.destructible = new DestructibleInstanceData();
            document.entities.Add(entity);

            var issues = LevelValidator.Validate(document);

            Assert.That(issues.Any(item => item.Code == "destructible.state"), Is.False);
        }

        [Test]
        public void UnknownDestructibleStateIsRejected()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            LevelEntity entity = CreateEntity("entity-1");
            entity.destructible = new DestructibleInstanceData
            {
                enabled = true,
                initialState = "splintered",
                integrity = 10f,
            };
            document.entities.Add(entity);

            var issues = LevelValidator.Validate(document);

            Assert.That(issues.Any(item => item.Code == "destructible.state"),
                Is.True);
        }

        [Test]
        public void ValidationServiceComposesFeatureRulesAndProfiles()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            var service = new LevelValidationService(new ILevelValidationRule[]
            {
                new PublishMarkerRule(),
            });

            var authoringIssues = service.Validate(
                document,
                profile: LevelValidationProfile.Authoring);
            var publishIssues = service.Validate(
                document,
                profile: LevelValidationProfile.Publish);

            Assert.That(authoringIssues, Is.Empty);
            Assert.That(publishIssues.Single().Code, Is.EqualTo("test.publish"));
        }

        private sealed class PublishMarkerRule : ILevelValidationRule
        {
            public void Evaluate(LevelValidationContext context)
            {
                if (context.Profile == LevelValidationProfile.Publish)
                {
                    context.Warning("test.publish", "Publish validation ran.");
                }
            }
        }

        private static LevelEntity CreateEntity(string id)
        {
            return new LevelEntity
            {
                id = id,
                archetypeId = "prop.crate.standard",
                transform = new LevelTransformData(new Float3Data(0f, 0f, 0f), 0f),
            };
        }
    }
}
