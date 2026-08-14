using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Presentation.Gameplay;
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

            Assert.That(ids, Has.Length.EqualTo(7));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
            Assert.That(ids, Does.Contain("structure.floor.standard"));
            Assert.That(ids, Does.Contain("prop.crate.standard"));
            Assert.That(ids, Does.Contain("vehicle.buggy.standard"));
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
            TextAsset asset = Resources.Load<TextAsset>("Levels/main-level");
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
            Assert.That(
                document.entities.Select(entity => entity.archetypeId).Distinct(),
                Is.EquivalentTo(catalog.Entries.Select(entry => entry.ArchetypeId)));
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
    }
}
