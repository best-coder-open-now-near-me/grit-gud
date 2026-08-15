using System;
using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GritGud.Presentation.Tests
{
    public sealed class TerrainMeshBuilderTests
    {
        [Test]
        public void RuntimeColorShaderCreatesMaterialWithoutNullShader()
        {
            Material material = RuntimeMaterialFactory.CreateColor(Color.magenta, "Test Material");
            try
            {
                Assert.That(Shader.Find("GritGud/RuntimeColor"), Is.Not.Null);
                Assert.That(material, Is.Not.Null);
                Assert.That(material.shader, Is.Not.Null);
            }
            finally
            {
                if (material != null)
                {
                    Object.DestroyImmediate(material);
                }
            }
        }

        [Test]
        public void CelSurfaceShaderCreatesLitTerrainMaterial()
        {
            Material material = RuntimeMaterialFactory.CreateCelColor(
                Color.gray,
                "Test Cel Material");
            try
            {
                Assert.That(Shader.Find("GritGud/CelSurface"), Is.Not.Null);
                Assert.That(Shader.Find("GritGud/RuntimeOutline"), Is.Not.Null);
                Assert.That(Shader.Find("GritGud/EmissiveSurface"), Is.Not.Null);
                Assert.That(material, Is.Not.Null);
                Assert.That(material.shader.name, Is.EqualTo("GritGud/CelSurface"));
            }
            finally
            {
                if (material != null)
                {
                    Object.DestroyImmediate(material);
                }
            }
        }

        [Test]
        public void TacticalWireframeShaderIsAvailableForMovementGhosts()
        {
            Shader shader = Shader.Find(MovementRouteGhostPresenter.GhostShaderName);

            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.name, Is.EqualTo("GritGud/TacticalWireframe"));
        }

        [Test]
        public void BuildChunkMapsQuantizedSamplesToWorldVertices()
        {
            TerrainSurfaceData surface = CreateSurface(3, 3);
            surface.origin = new Float3Data(-2f, 1f, 4f);
            surface.sampleSpacing = 2f;
            surface.minimumElevation = -1f;
            surface.elevationIncrement = 0.5f;
            surface.heightSamples[4] = 6;

            Mesh mesh = TerrainMeshBuilder.BuildChunk(surface, 0, 0, 32);
            try
            {
                Assert.That(mesh.vertexCount, Is.EqualTo(9));
                Assert.That(mesh.triangles, Has.Length.EqualTo(24));
                Assert.That(mesh.vertices[0], Is.EqualTo(new Vector3(-2f, 0f, 4f)));
                Assert.That(mesh.vertices[4], Is.EqualTo(new Vector3(0f, 3f, 6f)));
                Assert.That(mesh.bounds.min.x, Is.EqualTo(-2f).Within(0.001f));
                Assert.That(mesh.bounds.max.z, Is.EqualTo(8f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void BuildChunkMapsPaintedMaterialSamplesToVertexColors()
        {
            TerrainSurfaceData surface = CreateSurface(3, 3);
            surface.materialSamples[4] = 2;

            Mesh mesh = TerrainMeshBuilder.BuildChunk(surface, 0, 0, 32);
            try
            {
                Assert.That(mesh.colors32[0].a, Is.Zero);
                Assert.That(mesh.colors32[4], Is.EqualTo(TerrainMeshBuilder.MaterialColor(2)));
                Assert.That(mesh.colors32[4].a, Is.EqualTo(255));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void CalculateBoundsIncludesSurfaceExtentsAndQuantizedHeights()
        {
            TerrainSurfaceData surface = CreateSurface(3, 2);
            surface.origin = new Float3Data(-2f, 3f, 5f);
            surface.sampleSpacing = 2f;
            surface.minimumElevation = -1f;
            surface.elevationIncrement = 0.5f;
            surface.heightSamples = new[] { -2, 0, 4, 1, 2, 3 }.ToList();

            Bounds bounds = TerrainMeshBuilder.CalculateBounds(surface);

            Assert.That(bounds.min, Is.EqualTo(new Vector3(-2f, 1f, 5f)));
            Assert.That(bounds.max, Is.EqualTo(new Vector3(2f, 4f, 7f)));
        }

        [Test]
        public void AdjacentChunksShareIdenticalBorderVertices()
        {
            TerrainSurfaceData surface = CreateSurface(5, 2);
            surface.heightSamples = new[] { 0, 1, 2, 3, 4, 4, 3, 2, 1, 0 }.ToList();

            Mesh left = TerrainMeshBuilder.BuildChunk(surface, 0, 0, 2);
            Mesh right = TerrainMeshBuilder.BuildChunk(surface, 1, 0, 2);
            try
            {
                Assert.That(left.vertices[2], Is.EqualTo(right.vertices[0]));
                Assert.That(left.vertices[5], Is.EqualTo(right.vertices[3]));
                Assert.That(left.normals[2], Is.EqualTo(right.normals[0]));
                Assert.That(left.normals[5], Is.EqualTo(right.normals[3]));
            }
            finally
            {
                Object.DestroyImmediate(left);
                Object.DestroyImmediate(right);
            }
        }

        [Test]
        public void ProjectorRebuildsOnlyChunkIntersectingPatch()
        {
            var owner = new GameObject("Terrain Projector Test");
            var projector = new TerrainWorldProjector(owner.transform);
            try
            {
                TerrainSurfaceData surface = CreateSurface(66, 2);
                LevelDocument document = LevelDocumentFactory.CreateEmpty("Projection Test");
                document.terrainSurfaces.Add(surface);
                projector.Replace(document);
                MeshFilter[] filters = owner.GetComponentsInChildren<MeshFilter>();
                Assert.That(filters, Has.Length.EqualTo(3));
                Mesh firstBefore = filters.Single(filter => filter.name == "Chunk 0, 0").sharedMesh;
                Mesh lastBefore = filters.Single(filter => filter.name == "Chunk 2, 0").sharedMesh;
                surface.heightSamples[0] = 4;
                var command = new SetTerrainHeightsCommand("test", 0, 0, 1, 1, new[] { 4 });
                var change = new LevelSessionChangedEventArgs(
                    LevelSessionChangeKind.Execute,
                    1,
                    command);

                projector.Apply(document, change);

                filters = owner.GetComponentsInChildren<MeshFilter>();
                Assert.That(filters.Single(filter => filter.name == "Chunk 0, 0").sharedMesh,
                    Is.Not.SameAs(firstBefore));
                Assert.That(filters.Single(filter => filter.name == "Chunk 2, 0").sharedMesh,
                    Is.SameAs(lastBefore));
                MeshCollider firstCollider = filters
                    .Single(filter => filter.name == "Chunk 0, 0")
                    .GetComponent<MeshCollider>();
                Assert.That(firstCollider.sharedMesh,
                    Is.SameAs(filters.Single(filter => filter.name == "Chunk 0, 0").sharedMesh));
            }
            finally
            {
                projector.Dispose();
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void AppearanceAndSlopeDiagnosticsUpdateMaterialWithoutRebuildingMesh()
        {
            var owner = new GameObject("Terrain Appearance Projection Test");
            var projector = new TerrainWorldProjector(owner.transform);
            try
            {
                TerrainSurfaceData surface = CreateSurface(3, 3);
                LevelDocument document = LevelDocumentFactory.CreateEmpty("Appearance Test");
                document.terrainSurfaces.Add(surface);
                projector.Replace(document);
                MeshFilter filter = owner.GetComponentInChildren<MeshFilter>();
                Mesh originalMesh = filter.sharedMesh;
                Material material = owner.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                TerrainNavigationInvalidation? navigation = null;
                projector.NavigationInvalidated += value => navigation = value;
                TerrainAppearanceData before = surface.appearance.DeepCopy();
                TerrainAppearanceData after = before.DeepCopy();
                after.baseColor = new FloatColorData(0.2f, 0.4f, 0.1f);
                after.steepColor = new FloatColorData(0.5f, 0.3f, 0.2f);
                after.slopeBlendStartDegrees = 25f;
                after.slopeBlendEndDegrees = 55f;
                surface.appearance = after.DeepCopy();

                projector.Apply(
                    document,
                    new LevelSessionChangedEventArgs(
                        LevelSessionChangeKind.Execute,
                        1,
                        new SetTerrainAppearanceCommand("test", before, after)));
                projector.SetSlopeDiagnostics(true, 50f);

                Assert.That(filter.sharedMesh, Is.SameAs(originalMesh));
                Assert.That(navigation, Is.Null);
                Assert.That(material.GetColor("_BaseColor").g, Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(material.GetColor("_SteepColor").r, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(material.GetFloat("_TerrainDiagnosticsEnabled"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_DiagnosticSlopeCos"),
                    Is.EqualTo(Mathf.Cos(50f * Mathf.Deg2Rad)).Within(0.001f));
            }
            finally
            {
                projector.Dispose();
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ProjectorInvalidatesNavigationOnlyForCommittedTerrainChanges()
        {
            var owner = new GameObject("Terrain Navigation Invalidation Test");
            var projector = new TerrainWorldProjector(owner.transform);
            try
            {
                TerrainSurfaceData surface = CreateSurface(4, 4);
                LevelDocument document = LevelDocumentFactory.CreateEmpty("Navigation Test");
                document.terrainSurfaces.Add(surface);
                projector.Replace(document);
                TerrainNavigationInvalidation? received = null;
                projector.NavigationInvalidated += invalidation => received = invalidation;

                TerrainSurfaceData preview = surface.DeepCopy();
                preview.heightSamples[5] = 2;
                projector.PreviewPatch(preview, 1, 1, 1, 1);

                Assert.That(received, Is.Null,
                    "Visual brush previews must not request navigation work.");

                surface.heightSamples[5] = 2;
                var command = new SetTerrainHeightsCommand(
                    "test",
                    1,
                    1,
                    1,
                    1,
                    new[] { 2 });
                projector.Apply(
                    document,
                    new LevelSessionChangedEventArgs(
                        LevelSessionChangeKind.Execute,
                        1,
                        command));

                Assert.That(received, Is.Not.Null);
                Assert.That(received.Value.RequiresFullRefresh, Is.False);
                Assert.That(received.Value.SurfaceId, Is.EqualTo("test"));
                Assert.That(received.Value.StartX, Is.EqualTo(1));
                Assert.That(received.Value.StartZ, Is.EqualTo(1));
                Assert.That(received.Value.Width, Is.EqualTo(1));
                Assert.That(received.Value.Depth, Is.EqualTo(1));
            }
            finally
            {
                projector.Dispose();
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ProjectorAppliesTerrainChangesInsideCompositeCommand()
        {
            var owner = new GameObject("Composite Terrain Projection Test");
            var projector = new TerrainWorldProjector(owner.transform);
            try
            {
                LevelDocument document = LevelDocumentFactory.CreateEmpty("Composite Test");
                document.terrainSurfaces.Add(CreateSurface(4, 4));
                var session = new LevelSession(document);
                projector.Replace(session.CreateSnapshot());
                Mesh before = owner.GetComponentInChildren<MeshFilter>().sharedMesh;
                int invalidationCount = 0;
                projector.NavigationInvalidated += _ => invalidationCount++;
                session.Changed += (_, args) =>
                    projector.Apply(session.CreateSnapshot(), args);

                session.ExecuteTransaction(
                    "Edit two terrain regions",
                    new ILevelEditCommand[]
                    {
                        new SetTerrainHeightsCommand("test", 0, 0, 1, 1, new[] { 3 }),
                        new SetTerrainHeightsCommand("test", 3, 3, 1, 1, new[] { 4 }),
                    });

                Mesh after = owner.GetComponentInChildren<MeshFilter>().sharedMesh;
                Assert.That(after, Is.Not.SameAs(before));
                Assert.That(after.vertices[0].y, Is.EqualTo(3f));
                Assert.That(after.vertices[15].y, Is.EqualTo(4f));
                Assert.That(invalidationCount, Is.EqualTo(2));
            }
            finally
            {
                projector.Dispose();
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void FailedReplacementPreservesActiveProjection()
        {
            var owner = new GameObject("Atomic Terrain Replacement Test");
            var projector = new TerrainWorldProjector(owner.transform);
            try
            {
                LevelDocument valid = LevelDocumentFactory.CreateEmpty("Valid Terrain");
                valid.terrainSurfaces.Add(CreateSurface(3, 3));
                projector.Replace(valid);
                GameObject originalRoot = owner.transform.Find("Terrain Surfaces").gameObject;
                Mesh originalMesh = originalRoot.GetComponentInChildren<MeshFilter>().sharedMesh;
                LevelDocument invalid = valid.DeepCopy();
                invalid.terrainSurfaces.Add(valid.terrainSurfaces[0].DeepCopy());

                Assert.Throws<InvalidOperationException>(() => projector.Replace(invalid));

                Assert.That(originalRoot, Is.Not.Null);
                Assert.That(originalRoot.activeSelf, Is.True);
                Assert.That(
                    originalRoot.GetComponentInChildren<MeshFilter>().sharedMesh,
                    Is.SameAs(originalMesh));
                Assert.That(
                    owner.transform.Cast<Transform>()
                        .Count(child => child.name == "Terrain Surfaces"),
                    Is.EqualTo(1));
            }
            finally
            {
                projector.Dispose();
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void PreviewPatchRebuildsProjectionWithoutMutatingAuthoredDocument()
        {
            var owner = new GameObject("Terrain Preview Test");
            var projector = new TerrainWorldProjector(owner.transform);
            try
            {
                TerrainSurfaceData authored = CreateSurface(3, 3);
                LevelDocument document = LevelDocumentFactory.CreateEmpty("Preview Test");
                document.terrainSurfaces.Add(authored);
                projector.Replace(document);
                TerrainSurfaceData preview = authored.DeepCopy();
                preview.heightSamples[4] = 5;

                projector.PreviewPatch(preview, 1, 1, 1, 1);

                Mesh mesh = owner.GetComponentInChildren<MeshFilter>().sharedMesh;
                Assert.That(mesh.vertices[4].y, Is.EqualTo(5f));
                Assert.That(authored.heightSamples[4], Is.Zero);
            }
            finally
            {
                projector.Dispose();
                Object.DestroyImmediate(owner);
            }
        }

        private static TerrainSurfaceData CreateSurface(int countX, int countZ)
        {
            return new TerrainSurfaceData
            {
                id = "test",
                sampleCountX = countX,
                sampleCountZ = countZ,
                sampleSpacing = 1f,
                elevationIncrement = 1f,
                heightSamples = Enumerable.Repeat(0, countX * countZ).ToList(),
                materialSamples = Enumerable.Repeat(0, countX * countZ).ToList(),
            };
        }
    }
}
