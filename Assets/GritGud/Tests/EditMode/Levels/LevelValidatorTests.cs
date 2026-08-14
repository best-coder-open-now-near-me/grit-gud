using System.Linq;
using System.Collections.Generic;
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
        public void ScenarioRequiresExactlyOneSelectedPlayer()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.scenario.actors[0].initiallySelected = false;

            var issues = LevelValidator.Validate(document);

            Assert.That(
                issues.Any(item => item.Code == "scenario.party.selection"),
                Is.True);
        }

        [Test]
        public void ScenarioEntityLinksMustResolve()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.scenario.props.Add(new LevelScenarioPropData
            {
                entityId = "missing-prop",
                mass = 25f,
            });

            var issues = LevelValidator.Validate(document);

            Assert.That(
                issues.Any(item => item.Code == "scenario.prop.entity.unknown"),
                Is.True);
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

        [Test]
        public void UnknownActorTemplateWarnsDuringAuthoringAndBlocksPublish()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.scenario.actors[0].templateId = "missing-template";
            LevelValidationContent content = CreateValidationContent();

            var authoringIssues = LevelValidator.Validate(
                document,
                content,
                LevelValidationProfile.Authoring);
            var publishIssues = LevelValidator.Validate(
                document,
                content,
                LevelValidationProfile.Publish);

            Assert.That(
                authoringIssues.Single(issue =>
                    issue.Code == "scenario.actor.template.unknown").Severity,
                Is.EqualTo(LevelValidationSeverity.Warning));
            Assert.That(LevelValidator.HasErrors(authoringIssues), Is.False);
            Assert.That(
                publishIssues.Single(issue =>
                    issue.Code == "scenario.actor.template.unknown").Severity,
                Is.EqualTo(LevelValidationSeverity.Error));
        }

        [Test]
        public void UnavailableActorPresentationBlocksPublish()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            var content = new LevelValidationContent(
                actorPresentationsByTemplateId: new[]
                {
                    new KeyValuePair<string, string>("player", "presentation.missing"),
                },
                knownActorPresentationIds: new[] { "presentation.available" });

            var issues = LevelValidator.Validate(
                document,
                content,
                LevelValidationProfile.Publish);

            Assert.That(
                issues,
                Has.Some.Matches<LevelValidationIssue>(issue =>
                    issue.Code == "scenario.actor.presentation.unknown"
                    && issue.Severity == LevelValidationSeverity.Error));
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

        private static LevelValidationContent CreateValidationContent()
        {
            return new LevelValidationContent(
                actorPresentationsByTemplateId: new[]
                {
                    new KeyValuePair<string, string>("player", "actor.player.default"),
                },
                knownActorPresentationIds: new[] { "actor.player.default" });
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
