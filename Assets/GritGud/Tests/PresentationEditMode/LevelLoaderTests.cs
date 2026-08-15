using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Presentation.Gameplay;
using GritGud.Presentation.LevelEditing;
using GritGud.Presentation.Levels;
using GritGud.Presentation.Levels.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class LevelLoaderTests
    {
        [Test]
        public void DefaultCatalogUsesStableUniqueArchetypeIds()
        {
            LevelArchetypeCatalog catalog = LevelArchetypeCatalog.LoadDefault();

            string[] ids = catalog.Entries.Select(entry => entry.ArchetypeId).ToArray();

            Assert.That(ids.Length, Is.GreaterThanOrEqualTo(7));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
            Assert.That(ids, Does.Contain("structure.floor.standard"));
            Assert.That(ids, Does.Contain("prop.crate.standard"));
            Assert.That(ids, Does.Contain("vehicle.buggy.standard"));
            Assert.That(ids, Does.Contain("structure.barrier.concrete"));
            Assert.That(ids, Does.Contain("structure.barricade.wood"));
            Assert.That(ids, Does.Contain("prop.pallet-stack.standard"));
            Assert.That(ids, Does.Contain("structure.wire-fence.standard"));
            Assert.That(ids, Does.Contain("structure.container.small"));
            Assert.That(ids, Does.Contain("prop.generator.standard"));
            Assert.That(ids, Does.Contain("prop.tire-stack.standard"));
            Assert.That(ids, Does.Contain("prop.propane.standard"));
            Assert.That(
                catalog.Entries.Single(entry => entry.ArchetypeId == "vehicle.buggy.standard")
                    .Capabilities.HasFlag(LevelArchetypeCapabilities.Vehicle),
                Is.True);
        }

        [Test]
        public void ScenarioAuthoringCatalogExposesPlayerAndOpponentTemplates()
        {
            ScenarioAuthoringCatalog authoring = ScenarioAuthoringCatalog.LoadDefault();

            Assert.That(authoring.ActorTemplates.Select(template => template.TemplateId),
                Does.Contain("player"));
            Assert.That(authoring.ActorTemplates.Select(template => template.TemplateId),
                Does.Contain("depot-rifleman"));
            Assert.That(authoring.GetActor("player").PlayerTemplate, Is.True);
            Assert.That(authoring.GetActor("depot-rifleman").PlayerTemplate, Is.False);
            Assert.That(authoring.TryGetActor("missing-template", out _), Is.False);
        }

        [Test]
        public void FixtureConstructsThroughSharedLoader()
        {
            TextAsset fixture = Resources.Load<TextAsset>("Levels/basic-construction");
            Assert.That(fixture, Is.Not.Null);
            var serializer = new UnityLevelJsonSerializer();
            LevelDocument document = serializer.Deserialize(fixture.text);
            LevelArchetypeCatalog catalog = LevelArchetypeCatalog.LoadDefault();
            var issues = LevelValidator.Validate(document, catalog.CreateKnownIdSet());
            Assert.That(
                LevelValidator.HasErrors(issues),
                Is.False,
                string.Join(" | ", issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            var loader = new LevelLoader(catalog);

            using (LevelWorld world = loader.Load(document))
            {
                Assert.That(world.Entities, Has.Count.EqualTo(document.entities.Count));
                Assert.That(world.TryGetEntity("crate-01", out LevelEntityView crate), Is.True);
                Assert.That(crate.ArchetypeId, Is.EqualTo("prop.crate.standard"));
                TerrainChunkTag terrain = world.Root.GetComponentInChildren<TerrainChunkTag>();
                Assert.That(terrain, Is.Not.Null);
                Assert.That(terrain.SurfaceId, Is.EqualTo("fixture-ground"));
                Assert.That(terrain.GetComponent<MeshCollider>().sharedMesh, Is.Not.Null);
            }
        }

        [Test]
        public void MainLevelIsValidAndSubstantial()
        {
            TextAsset asset = Resources.Load<TextAsset>(
                "Levels/Published/main-level");
            Assert.That(asset, Is.Not.Null);
            var serializer = new UnityLevelJsonSerializer();
            LevelDocument document = serializer.Deserialize(asset.text);
            LevelArchetypeCatalog catalog = LevelArchetypeCatalog.LoadDefault();

            var issues = LevelValidator.Validate(document, catalog.CreateKnownIdSet());

            Assert.That(
                LevelValidator.HasErrors(issues),
                Is.False,
                string.Join(" | ", issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            Assert.That(document.levelId, Is.EqualTo("main-depot-yard-v1"));
            Assert.That(document.entities, Has.Count.GreaterThanOrEqualTo(150));
            Assert.That(document.entities.Any(entity => entity.transform.position.y > 0f), Is.True);
            GameplayContentPackage content = GameplayContentLoader.LoadDefault();
            Assert.That(
                content.Scenario.playerParty.actorIds,
                Is.EqualTo(new[] { "player", "oren-vale" }));
            Assert.That(
                content.Assembly.GetActorDefinition("oren-vale")
                    .CharacterProfile.DisplayName,
                Is.EqualTo("Oren Vale"));
            Assert.That(
                content.Assembly.GetActorDefinition("oren-vale")
                    .GetInventoryItem("item.smoke-grenade")
                    .ConsumablePower,
                Is.TypeOf<ThrownExplosiveDefinition>());
            ScenarioDisplacementActionData push = content.Scenario.actors
                .Single(actor => actor.id
                    == content.Scenario.playerParty.initiallySelectedActorId)
                .displacementAbility.actions
                .Single(action => action.id == "close-quarters.push");
            Assert.That(
                push.acceptedSubjectKinds,
                Is.EquivalentTo(new[] { "prop", "combatant" }));
            Assert.That(
                push.contestPolicy,
                Is.EqualTo("close-quarters-control"));
            Assert.That(
                content.Assembly.GetActorDefinition(
                    content.Scenario.playerParty.initiallySelectedActorId)
                    .GetDisplacementAction("close-quarters.push")
                    .DistanceDecay,
                Is.Null);
            ScenarioDisplacementActionData throwAction = content.Scenario.actors
                .Single(actor => actor.id
                    == content.Scenario.playerParty.initiallySelectedActorId)
                .displacementAbility.actions
                .Single(action => action.id == "close-quarters.throw");
            Assert.That(throwAction.intent, Is.EqualTo("throw"));
            Assert.That(throwAction.maximumSubjectSize, Is.EqualTo("medium"));
            Assert.That(throwAction.distanceDecay.fullDistanceMass,
                Is.EqualTo(15f));
            Assert.That(throwAction.distanceDecay.minimumDistance,
                Is.EqualTo(0.75f));
            Assert.That(
                content.Assembly.GetActorDefinition(
                    content.Scenario.playerParty.initiallySelectedActorId)
                    .GetInventoryItem("weapon.rifle")
                    .OccupiedHands,
                Is.EqualTo(2));
            InventoryItemDefinition knife = content.Assembly
                .GetActorDefinition(
                    content.Scenario.playerParty.initiallySelectedActorId)
                .GetInventoryItem("weapon.combat-knife");
            Assert.That(knife.HotbarSlot, Is.EqualTo(5));
            Assert.That(knife.OccupiedHands, Is.EqualTo(1));
            Assert.That(knife.Attack.Contact.MaximumReach, Is.EqualTo(2f));
            Assert.That(knife.Attack.CanTargetWorldPoint, Is.False);
            InventoryItemDefinition grenade = content.Assembly
                .GetActorDefinition(
                    content.Scenario.playerParty.initiallySelectedActorId)
                .GetInventoryItem("item.frag-grenade");
            Assert.That(grenade.InitialQuantity, Is.EqualTo(3));
            string configuredVehicleId = content.Scenario.vehicles.Single().entityId;
            Assert.That(
                document.entities.Any(entity =>
                    entity.id == configuredVehicleId
                    && entity.archetypeId == "vehicle.buggy.standard"),
                Is.True);
            Assert.That(document.terrainSurfaces, Has.Count.EqualTo(1));
            Assert.That(document.terrainSurfaces[0].id, Is.EqualTo("depot-ground"));
            Assert.That(document.terrainSurfaces[0].heightSamples,
                Has.Count.EqualTo(17 * 15));
        }

        [Test]
        public void SandboxPackageUsesDetachedLevelAndOnlyTheSelectedPlayerActor()
        {
            TextAsset asset = Resources.Load<TextAsset>("Levels/basic-construction");
            LevelDocument source = new UnityLevelJsonSerializer().Deserialize(asset.text);
            source.levelId = "playtest-fixture";
            source.displayName = "Playtest Fixture";

            GameplayContentPackage package = GameplayContentLoader.LoadSandbox(source);

            Assert.That(package.IsSandbox, Is.True);
            Assert.That(package.Level.levelId, Is.EqualTo("playtest-fixture"));
            Assert.That(package.Scenario.levelId, Is.EqualTo("playtest-fixture"));
            Assert.That(package.Scenario.objectives, Is.Empty);
            Assert.That(package.Scenario.props, Is.Empty);
            Assert.That(package.Scenario.vehicles, Is.Empty);
            Assert.That(package.Scenario.playerParty.actorIds, Has.Count.EqualTo(1));
            Assert.That(package.Scenario.playerParty.actorIds.Single(),
                Is.EqualTo(package.Scenario.playerParty.initiallySelectedActorId));
            Assert.That(package.Scenario.actors, Has.Count.EqualTo(1));
            Assert.That(package.Scenario.actors.Single().id,
                Is.EqualTo(package.Scenario.playerParty.initiallySelectedActorId));
            Assert.That(package.Level, Is.Not.SameAs(source));
        }

        [Test]
        public void SandboxPackageUsesOnlyAuthoredScenarioInstances()
        {
            TextAsset asset = Resources.Load<TextAsset>("Levels/basic-construction");
            LevelDocument source = new UnityLevelJsonSerializer().Deserialize(asset.text);
            source.levelId = "authored-scenario";
            source.scenario = new LevelScenarioData
            {
                randomSeed = 42,
                actors =
                {
                    new LevelScenarioActorData
                    {
                        id = "hero",
                        templateId = "player",
                        playerControlled = true,
                        initiallySelected = true,
                        transform = new LevelTransformData(
                            new Float3Data(1f, 2f, 3f),
                            45f),
                    },
                    new LevelScenarioActorData
                    {
                        id = "guard-a",
                        templateId = "depot-rifleman",
                        primaryTarget = true,
                        transform = new LevelTransformData(
                            new Float3Data(-4f, 2f, 6f),
                            180f),
                    },
                },
                props =
                {
                    new LevelScenarioPropData
                    {
                        entityId = "crate-01",
                        mass = 31f,
                        sizeClass = "medium",
                        startsEncounterOnAttack = true,
                    },
                },
            };

            GameplayContentPackage package = GameplayContentLoader.LoadSandbox(source);

            Assert.That(package.Scenario.randomSeed, Is.EqualTo(42));
            Assert.That(package.Scenario.actors.Select(actor => actor.id),
                Is.EquivalentTo(new[] { "hero", "guard-a" }));
            Assert.That(package.Scenario.playerParty.actorIds,
                Is.EqualTo(new[] { "hero" }));
            Assert.That(package.Scenario.playerParty.initiallySelectedActorId,
                Is.EqualTo("hero"));
            Assert.That(package.Scenario.primaryTargetActorId,
                Is.EqualTo("guard-a"));
            Assert.That(package.Scenario.actors.Single(actor => actor.id == "hero")
                .position.x, Is.EqualTo(1f));
            Assert.That(package.Scenario.actors.Single(actor => actor.id == "guard-a")
                .facingDegrees, Is.EqualTo(180f));
            Assert.That(package.Scenario.props.Single().entityId,
                Is.EqualTo("crate-01"));
            Assert.That(package.Scenario.props.Single().mass, Is.EqualTo(31f));
            Assert.That(package.Scenario.props.Single().attackResponse.startsEncounter,
                Is.True);
            Assert.That(package.Scenario.objectives, Is.Empty);
            Assert.That(package.Scenario.vehicles, Is.Empty);
        }

        [Test]
        public void UnknownArchetypeFailsBeforeConstructingAWorld()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.entities.Add(new LevelEntity
            {
                id = "unknown-1",
                archetypeId = "missing.archetype",
            });
            var loader = new LevelLoader(LevelArchetypeCatalog.LoadDefault());

            LevelLoadException exception = Assert.Throws<LevelLoadException>(() => loader.Load(document));

            Assert.That(
                exception.Issues.Any(issue => issue.Code == "entity.archetype.unknown"),
                Is.True);
        }

        [Test]
        public void ProjectorUpdatesTransformWithoutReplacingUnaffectedView()
        {
            TextAsset fixture = Resources.Load<TextAsset>("Levels/basic-construction");
            var serializer = new UnityLevelJsonSerializer();
            LevelDocument document = serializer.Deserialize(fixture.text);
            LevelArchetypeCatalog catalog = LevelArchetypeCatalog.LoadDefault();
            var parent = new GameObject("Projector Test");

            try
            {
                using var workspace = new LevelEditorWorkspace(document, catalog.CreateKnownIdSet());
                using var projector = new LevelWorldProjector(catalog, parent.transform);
                projector.Replace(workspace.CreateSnapshot());
                Assert.That(projector.TryGetEntity("crate-01", out LevelEntityView before), Is.True);
                workspace.Changed += (_, args) =>
                    projector.Apply(workspace.CreateSnapshot(), args.SessionChange);
                LevelEntity entity = workspace.FindEntitySnapshot("crate-01");
                LevelTransformData after = entity.transform;
                after.position.x += 2.5f;

                workspace.Execute(new SetEntityTransformCommand(
                    entity.id,
                    entity.transform,
                    after));

                Assert.That(projector.TryGetEntity("crate-01", out LevelEntityView result), Is.True);
                Assert.That(result, Is.SameAs(before));
                Assert.That(result.transform.localPosition.x, Is.EqualTo(after.position.x));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void CatalogExposesPlacementAndGameplayCapabilitiesSeparately()
        {
            LevelArchetypeCatalog catalog = LevelArchetypeCatalog.LoadDefault();
            LevelArchetypeDefinition crate = catalog.Entries
                .Single(entry => entry.ArchetypeId == "prop.crate.standard");

            Assert.That(crate.Presentation.Prefab, Is.Not.Null);
            Assert.That(crate.PlacementRules.PositionSnap, Is.GreaterThan(0f));
            Assert.That(
                (crate.Capabilities & LevelArchetypeCapabilities.Destructible) != 0,
                Is.True);
        }

        [Test]
        public void CuratedPrivatePropArchetypesResolveProductionPrefabs()
        {
            LevelArchetypeCatalog catalog = LevelArchetypeCatalog.LoadDefault();
            string[] privateArchetypeIds =
            {
                "structure.barrier.concrete",
                "structure.barricade.wood",
                "prop.pallet-stack.standard",
                "structure.wire-fence.standard",
                "structure.container.small",
                "prop.generator.standard",
                "prop.tire-stack.standard",
                "prop.propane.standard",
            };

            foreach (string archetypeId in privateArchetypeIds)
            {
                LevelArchetypeDefinition definition = catalog.Entries.Single(
                    entry => entry.ArchetypeId == archetypeId);
                Assert.That(definition.Prefab, Is.Not.Null, archetypeId);
                Assert.That(definition.LocalBounds.size.x, Is.GreaterThan(0f), archetypeId);
                Assert.That(definition.LocalBounds.size.y, Is.GreaterThan(0f), archetypeId);
                Assert.That(definition.LocalBounds.size.z, Is.GreaterThan(0f), archetypeId);
            }
        }
    }
}
