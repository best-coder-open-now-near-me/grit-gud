using GritGud.Presentation.LevelEditing;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class LevelEditorOutlinePresenterTests
    {
        private GameObject root;
        private LevelEditorOutlinePresenter presenter;

        [TearDown]
        public void TearDown()
        {
            presenter?.Dispose();
            presenter = null;
            if (root != null)
            {
                Object.DestroyImmediate(root);
                root = null;
            }
        }

        [Test]
        public void PresenterOwnsOutlineVisibilityAndLifecycle()
        {
            root = new GameObject("Outline Presenter Test");
            presenter = new LevelEditorOutlinePresenter(root.transform);

            Assert.That(root.transform.childCount, Is.EqualTo(3));
            Assert.That(root.transform.Find("Selection Outline").gameObject.activeSelf, Is.False);
            Assert.That(root.transform.Find("Hover Outline").gameObject.activeSelf, Is.False);
            Transform placement = root.transform.Find("Placement Outline");
            Assert.That(placement.gameObject.activeSelf, Is.False);

            presenter.PresentPlacement(new Bounds(Vector3.one, Vector3.one * 2f));

            Assert.That(placement.gameObject.activeSelf, Is.True);
            presenter.HideAll();
            Assert.That(placement.gameObject.activeSelf, Is.False);

            presenter.Dispose();
            presenter = null;
            Assert.That(root.transform.childCount, Is.Zero);
        }
    }
}
