using GritGud.Presentation.LevelEditing.Tools;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class SpatialRecordPlacementToolTests
    {
        [Test]
        public void QueueSelectsRequestedKindAndCancelClearsPlacement()
        {
            var tool = new SpatialRecordPlacementTool((_, _) => { });

            tool.Queue(LevelSpatialPlacementKind.AudioZone);

            Assert.That(tool.IsQueued, Is.True);
            Assert.That(tool.Kind, Is.EqualTo(LevelSpatialPlacementKind.AudioZone));
            Assert.That(tool.GetPreviewBounds().size, Is.EqualTo(new Vector3(4f, 2f, 4f)));

            Assert.That(tool.Cancel(), Is.False);
            Assert.That(tool.IsQueued, Is.False);
            Assert.That(tool.HasPreview, Is.False);
        }
    }
}
