using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing;
using GritGud.Presentation.LevelEditing.Core;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class LevelEditorGridPresenterTests
    {
        [Test]
        public void GridBuildsBoundedLineMeshAndHonorsVisibility()
        {
            var parent = new GameObject("Grid Parent");
            var presenter = new LevelEditorGridPresenter(parent.transform);
            try
            {
                var settings = new LevelEditorGridSettings();
                settings.Configure(true, 2.5f, 1f);
                presenter.Refresh(
                    new LevelBoundsData(
                        new Float3Data(0f, 2f, 0f),
                        new Float3Data(10f, 4f, 5f)),
                    settings);

                Mesh mesh = parent.GetComponentInChildren<MeshFilter>().sharedMesh;
                Assert.That(mesh.vertexCount, Is.EqualTo(16));
                Assert.That(mesh.bounds.min.x, Is.EqualTo(-5f).Within(0.001f));
                Assert.That(mesh.bounds.max.z, Is.EqualTo(2.5f).Within(0.001f));

                settings.Configure(false, 2.5f, 1f);
                presenter.Refresh(default, settings);
                Assert.That(parent.transform.GetChild(0).gameObject.activeSelf, Is.False);
            }
            finally
            {
                presenter.Dispose();
                Object.DestroyImmediate(parent);
            }
        }
    }
}
