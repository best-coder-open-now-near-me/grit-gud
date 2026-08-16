using System;
using System.Linq;
using GritGud.Domain.Characters;
using GritGud.Presentation.Characters;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class CharacterAppearanceTests
    {
        [Test]
        public void DefaultCatalogAndPublishedLibraryAreValid()
        {
            CharacterAppearanceCatalog catalog = CharacterAppearanceCatalog.LoadDefault();
            UnityCharacterLibrary library = UnityCharacterLibrary.LoadDefault(catalog);

            Assert.That(catalog.Bodies.Count, Is.EqualTo(15));
            Assert.That(catalog.Accessories.Count, Is.EqualTo(87));
            Assert.That(catalog.PreviewPrefab, Is.Not.Null);
            Assert.That(library.Find("character.default-operative"), Is.Not.Null);
        }

        [Test]
        public void CatalogReferencesResolveAgainstInstalledPrivateCharacterAssets()
        {
            CharacterAppearanceCatalog catalog = CharacterAppearanceCatalog.LoadDefault();
            GameObject preview = UnityEngine.Object.Instantiate(catalog.PreviewPrefab);
            try
            {
                string[] rendererNames = preview.GetComponentsInChildren<Renderer>(true)
                    .Select(renderer => renderer.name)
                    .ToArray();
                foreach (CharacterBodyPresentationDefinition body in catalog.Bodies)
                {
                    Assert.That(
                        rendererNames,
                        Does.Contain(body.RendererName),
                        $"Private character body renderer '{body.RendererName}' is unavailable.");
                }
                foreach (CharacterAccessoryPresentationDefinition accessory in catalog.Accessories)
                {
                    if (accessory.ProjectionKind == CharacterAccessoryProjectionKind.AttachPrefab)
                    {
                        Assert.That(
                            accessory.Prefab,
                            Is.Not.Null,
                            $"Private character accessory '{accessory.Id}' is unavailable.");
                    }
                    else
                    {
                        Assert.That(
                            rendererNames,
                            Does.Contain(accessory.RendererName),
                            $"Embedded armor renderer '{accessory.RendererName}' is unavailable.");
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preview);
            }
        }

        [Test]
        public void SerializerRoundTripsAppearanceRecipe()
        {
            var source = new CharacterDocument
            {
                characterId = "character.roundtrip",
                displayName = "Round Trip",
            };
            source.appearance.SetAccessory(
                CharacterAppearanceSlotIds.Headwear,
                "accessory.hat-tacticle-01");
            var serializer = new UnityCharacterJsonSerializer();

            CharacterDocument restored = serializer.Deserialize(serializer.Serialize(source));

            Assert.That(restored.characterId, Is.EqualTo(source.characterId));
            Assert.That(
                restored.appearance.GetAccessory(CharacterAppearanceSlotIds.Headwear),
                Is.EqualTo("accessory.hat-tacticle-01"));
        }

        [Test]
        public void SerializerMigratesAppearanceOnlySchemaOneDocuments()
        {
            const string legacy = "{\"schemaVersion\":1,\"characterId\":\"character.legacy\","
                + "\"displayName\":\"Legacy\",\"appearance\":{"
                + "\"bodyId\":\"body.military-male-01\",\"accessories\":[]}}";
            var serializer = new UnityCharacterJsonSerializer();

            CharacterDocument restored = serializer.Deserialize(legacy);

            Assert.That(restored.schemaVersion, Is.EqualTo(CharacterDocument.CurrentSchemaVersion));
            Assert.That(restored.build, Is.Not.Null);
            Assert.That(restored.build.attributes, Has.Count.EqualTo(4));
            Assert.That(restored.startingLoadout, Is.Not.Null);
        }

        [Test]
        public void SerializerRoundTripsBuildAndStartingLoadout()
        {
            var source = new CharacterDocument
            {
                characterId = "character.build-roundtrip",
                displayName = "Build Round Trip",
            };
            source.build.archetype = "scout";
            source.build.talentIds.Add("talent.combat-awareness");
            source.startingLoadout.initiallyEquippedItemId = "weapon.rifle";
            source.startingLoadout.items.Add(new CharacterLoadoutItemData
            {
                itemId = "weapon.rifle",
                quantity = 1,
                hotbarSlot = 2,
            });
            var serializer = new UnityCharacterJsonSerializer();

            CharacterDocument restored = serializer.Deserialize(serializer.Serialize(source));

            Assert.That(restored.build.archetype, Is.EqualTo("scout"));
            Assert.That(restored.build.talentIds, Contains.Item("talent.combat-awareness"));
            Assert.That(restored.startingLoadout.initiallyEquippedItemId,
                Is.EqualTo("weapon.rifle"));
            Assert.That(restored.startingLoadout.items.Single().hotbarSlot, Is.EqualTo(2));
        }

        [Test]
        public void ProjectorSelectsBodyAndParentsAccessoriesToHumanoidSocket()
        {
            CharacterAppearanceCatalog catalog = CharacterAppearanceCatalog.LoadDefault();
            GameObject preview = UnityEngine.Object.Instantiate(catalog.PreviewPrefab);
            preview.name = "Projection Test";
            try
            {
                var appearance = new CharacterAppearanceData
                {
                    bodyId = "body.military-female-01",
                };
                appearance.SetAccessory(
                    CharacterAppearanceSlotIds.Headwear,
                    "accessory.hat-tacticle-01");

                CharacterAppearanceProjector.Apply(preview, appearance, catalog);

                Transform selectedBody = preview.GetComponentsInChildren<Transform>(true)
                    .First(transform => transform.name == "Character_MilitaryFemale_01");
                Transform unselectedBody = preview.GetComponentsInChildren<Transform>(true)
                    .First(transform => transform.name == "Character_MercenaryMale_01");
                Transform accessory = preview.GetComponentsInChildren<Transform>(true)
                    .First(transform => transform.name.StartsWith(
                        "[Character Accessory] ",
                        StringComparison.Ordinal));
                Animator animator = preview.GetComponentInChildren<Animator>(true);

                Assert.That(selectedBody.gameObject.activeSelf, Is.True);
                Assert.That(unselectedBody.gameObject.activeSelf, Is.False);
                Assert.That(accessory.parent, Is.SameAs(
                    animator.GetBoneTransform(HumanBodyBones.Head)));
                Assert.That(
                    accessory.GetComponentsInChildren<Collider>(true)
                        .All(collider => !collider.enabled),
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preview);
            }
        }
    }
}
