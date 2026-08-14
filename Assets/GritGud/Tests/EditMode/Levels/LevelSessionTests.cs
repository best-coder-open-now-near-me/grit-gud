using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using NUnit.Framework;
using System.Linq;

namespace GritGud.Domain.Tests.Levels
{
    public sealed class LevelSessionTests
    {
        [Test]
        public void PlaceUndoAndRedoPreserveEntityIdentity()
        {
            var session = new LevelSession(LevelDocumentFactory.CreateEmpty());
            var entity = new LevelEntity
            {
                id = "stable-id",
                archetypeId = "prop.crate.standard",
            };

            session.Execute(new AddEntityCommand(entity));
            bool undone = session.Undo();
            bool redone = session.Redo();
            LevelDocument snapshot = session.CreateSnapshot();

            Assert.That(undone, Is.True);
            Assert.That(redone, Is.True);
            Assert.That(snapshot.entities, Has.Count.EqualTo(1));
            Assert.That(snapshot.entities[0].id, Is.EqualTo("stable-id"));
        }

        [Test]
        public void TransformCommandCreatesOneReversibleEdit()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.entities.Add(new LevelEntity
            {
                id = "entity-1",
                archetypeId = "structure.floor.standard",
                transform = new LevelTransformData(new Float3Data(0f, 0f, 0f), 0f),
            });
            var session = new LevelSession(document);
            var after = new LevelTransformData(new Float3Data(2.5f, 0f, 5f), 90f);
            LevelEntity before = session.FindEntitySnapshot("entity-1");

            session.Execute(new SetEntityTransformCommand(
                "entity-1",
                before.transform,
                after));
            session.Undo();
            LevelEntity result = session.FindEntitySnapshot("entity-1");

            Assert.That(result.transform.position.x, Is.EqualTo(0f));
            Assert.That(result.transform.yawDegrees, Is.EqualTo(0f));
            Assert.That(session.CanUndo, Is.False);
            Assert.That(session.CanRedo, Is.True);
        }

        [Test]
        public void SnapshotDoesNotShareMutableEntityLists()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.entities.Add(new LevelEntity
            {
                id = "entity-1",
                archetypeId = "prop.crate.standard",
            });
            var session = new LevelSession(document);

            LevelDocument snapshot = session.CreateSnapshot();
            snapshot.entities[0].archetypeId = "changed";

            Assert.That(
                session.FindEntitySnapshot("entity-1").archetypeId,
                Is.EqualTo("prop.crate.standard"));
        }

        [Test]
        public void UndoingToSavedPositionClearsDirtyState()
        {
            var session = new LevelSession(LevelDocumentFactory.CreateEmpty());
            session.Execute(new AddEntityCommand(new LevelEntity
            {
                id = "entity-1",
                archetypeId = "prop.crate.standard",
            }));
            session.MarkSaved();

            session.Execute(new DeleteEntityCommand("entity-1"));
            Assert.That(session.IsDirty, Is.True);

            session.Undo();
            Assert.That(session.IsDirty, Is.False);
        }

        [Test]
        public void EditingAfterUndoInvalidatesUnreachableSavepoint()
        {
            var session = new LevelSession(LevelDocumentFactory.CreateEmpty());
            session.Execute(new AddEntityCommand(new LevelEntity
            {
                id = "entity-1",
                archetypeId = "prop.crate.standard",
            }));
            session.MarkSaved();
            session.Undo();

            session.Execute(new AddEntityCommand(new LevelEntity
            {
                id = "entity-2",
                archetypeId = "prop.barrel.metal",
            }));

            Assert.That(session.IsDirty, Is.True);
            Assert.That(session.CanRedo, Is.False);
        }

        [Test]
        public void TransactionUndoesAsOneHistoryEntry()
        {
            var session = new LevelSession(LevelDocumentFactory.CreateEmpty());
            session.ExecuteTransaction("Place pair", new ILevelEditCommand[]
            {
                new AddEntityCommand(new LevelEntity
                {
                    id = "entity-1",
                    archetypeId = "prop.crate.standard",
                }),
                new AddEntityCommand(new LevelEntity
                {
                    id = "entity-2",
                    archetypeId = "prop.barrel.metal",
                }),
            });

            Assert.That(session.CreateSnapshot().entities, Has.Count.EqualTo(2));
            Assert.That(session.HistoryPosition, Is.EqualTo(1));

            session.Undo();

            Assert.That(session.CreateSnapshot().entities, Is.Empty);
            Assert.That(session.CanRedo, Is.True);
        }

        [Test]
        public void InteractionPointCommandsAreReversibleAndPreserveIdentity()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.entities.Add(new LevelEntity
            {
                id = "door",
                archetypeId = "structure.wall.doorway",
            });
            var session = new LevelSession(document);
            var initial = new InteractionPointData
            {
                id = "passage",
                type = "doorway",
                localPosition = new Float3Data(1f, 0f, 0f),
                radius = 1f,
            };

            session.Execute(new AddInteractionPointCommand("door", initial));
            InteractionPointData edited = initial.DeepCopy();
            edited.radius = 2f;
            session.Execute(new SetInteractionPointCommand("door", "passage", initial, edited));
            session.Execute(new DeleteInteractionPointCommand("door", "passage"));
            session.Undo();
            session.Undo();

            InteractionPointData restored = session.FindEntitySnapshot("door").interactionPoints[0];
            Assert.That(restored.id, Is.EqualTo("passage"));
            Assert.That(restored.radius, Is.EqualTo(1f));

            session.Undo();
            Assert.That(session.FindEntitySnapshot("door").interactionPoints, Is.Empty);
        }

        [Test]
        public void DestructibleDefaultsCommandRestoresNullOrConfiguredValues()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.entities.Add(new LevelEntity
            {
                id = "crate",
                archetypeId = "prop.crate.standard",
            });
            var session = new LevelSession(document);
            var configured = new DestructibleInstanceData
            {
                enabled = true,
                initialState = "intact",
                integrity = 25f,
            };

            session.Execute(new SetDestructibleInstanceCommand("crate", null, configured));
            Assert.That(session.FindEntitySnapshot("crate").destructible.integrity, Is.EqualTo(25f));

            session.Undo();
            Assert.That(session.FindEntitySnapshot("crate").destructible, Is.Null);
        }

        [Test]
        public void CompositeTransformTransactionMovesMultipleEntitiesAsOneUndoableGesture()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.entities.Add(new LevelEntity { id = "left", archetypeId = "prop.crate.standard" });
            document.entities.Add(new LevelEntity { id = "right", archetypeId = "prop.crate.standard" });
            var session = new LevelSession(document);
            var moved = new LevelTransformData(new Float3Data(3f, 0f, 2f), 0f);

            session.ExecuteTransaction("Move entities", new ILevelEditCommand[]
            {
                new SetEntityTransformCommand("left", default, moved),
                new SetEntityTransformCommand("right", default, moved),
            });

            Assert.That(session.HistoryPosition, Is.EqualTo(1));
            session.Undo();
            Assert.That(session.FindEntitySnapshot("left").transform.position.x, Is.EqualTo(0f));
            Assert.That(session.FindEntitySnapshot("right").transform.position.z, Is.EqualTo(0f));
        }

        [Test]
        public void PasteTransactionAddsClonedEntitiesAsOneUndoableEdit()
        {
            LevelDocument document = LevelDocumentFactory.CreateEmpty();
            document.entities.Add(new LevelEntity { id = "source", archetypeId = "prop.crate.standard" });
            var session = new LevelSession(document);
            LevelEntity firstCopy = document.entities[0].DeepCopy();
            firstCopy.id = "copy.one";
            LevelEntity secondCopy = document.entities[0].DeepCopy();
            secondCopy.id = "copy.two";

            session.ExecuteTransaction("Paste entities", new ILevelEditCommand[]
            {
                new AddEntityCommand(firstCopy),
                new AddEntityCommand(secondCopy),
            });

            Assert.That(session.CreateSnapshot().entities.Select(entity => entity.id),
                Is.EquivalentTo(new[] { "source", "copy.one", "copy.two" }));
            session.Undo();
            Assert.That(session.CreateSnapshot().entities.Select(entity => entity.id),
                Is.EquivalentTo(new[] { "source" }));
        }

        [Test]
        public void SessionChangeReportsIncrementalEntityImpact()
        {
            var session = new LevelSession(LevelDocumentFactory.CreateEmpty());
            LevelSessionChangedEventArgs observed = null;
            session.Changed += (_, args) => observed = args;

            session.Execute(new AddEntityCommand(new LevelEntity
            {
                id = "entity-1",
                archetypeId = "prop.crate.standard",
            }));

            Assert.That(observed, Is.Not.Null);
            Assert.That(observed.Kind, Is.EqualTo(LevelSessionChangeKind.Execute));
            Assert.That(observed.AffectedEntityIds, Is.EquivalentTo(new[] { "entity-1" }));
            Assert.That(observed.RequiresFullProjection, Is.False);
        }
    }
}
