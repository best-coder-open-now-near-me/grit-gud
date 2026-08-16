using System;
using System.Collections.Generic;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.LevelEditing.Persistence;
using GritGud.Presentation.Levels;
using GritGud.Presentation.Levels.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class LevelEditorPersistenceCoordinatorTests
    {
        [Test]
        public void InvalidAuthoringStateCanBeSavedAndRecoveredAsDraft()
        {
            var transferRoot = new GameObject("Level text transfer test");
            var store = new MemoryDraftStore();
            var serializer = new UnityLevelJsonSerializer();
            try
            {
                LevelTextTransfer transfer = transferRoot.AddComponent<LevelTextTransfer>();
                using var coordinator = new LevelEditorPersistenceCoordinator(
                    serializer,
                    store,
                    transfer,
                    new LevelValidationContent());
                LevelDocument document = LevelDocumentFactory.CreateEmpty();
                document.displayName = string.Empty;
                using var workspace = new LevelEditorWorkspace(document);
                LevelDocument recovered = null;
                coordinator.DocumentLoaded += (_, args) => recovered = args.Document;

                coordinator.SaveDraft(workspace);
                coordinator.LoadDraft();

                Assert.That(store.HasDraft("active"), Is.True);
                Assert.That(recovered, Is.Not.Null);
                Assert.That(recovered.displayName, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(transferRoot);
            }
        }

        [Test]
        public void AutosaveKeepsThreeRollingDirtyRecoveryGenerations()
        {
            var transferRoot = new GameObject("Level autosave test");
            var store = new MemoryDraftStore();
            try
            {
                LevelTextTransfer transfer = transferRoot.AddComponent<LevelTextTransfer>();
                using var coordinator = new LevelEditorPersistenceCoordinator(
                    new UnityLevelJsonSerializer(),
                    store,
                    transfer,
                    new LevelValidationContent());
                using var workspace = new LevelEditorWorkspace(LevelDocumentFactory.CreateEmpty());

                for (int index = 1; index <= 4; index++)
                {
                    LevelDocument before = workspace.CreateSnapshot();
                    workspace.Execute(new SetLevelDisplayNameCommand(
                        before.displayName,
                        $"Autosave {index}"));
                    double scheduledAt = index * 100d;
                    coordinator.ScheduleAutosave(workspace.Revision, scheduledAt);
                    Assert.That(coordinator.TickAutosave(
                        workspace,
                        scheduledAt + LevelEditorPersistenceCoordinator.AutosaveDelaySeconds - 0.01d),
                        Is.False);
                    Assert.That(coordinator.TickAutosave(
                        workspace,
                        scheduledAt + LevelEditorPersistenceCoordinator.AutosaveDelaySeconds),
                        Is.True);
                }

                Assert.That(workspace.IsDirty, Is.True);
                Assert.That(coordinator.HasRecovery(0), Is.True);
                Assert.That(coordinator.HasRecovery(1), Is.True);
                Assert.That(coordinator.HasRecovery(2), Is.True);
                var serializer = new UnityLevelJsonSerializer();
                Assert.That(serializer.Deserialize(store.LoadDraft("recovery.0")).displayName,
                    Is.EqualTo("Autosave 4"));
                Assert.That(serializer.Deserialize(store.LoadDraft("recovery.1")).displayName,
                    Is.EqualTo("Autosave 3"));
                Assert.That(serializer.Deserialize(store.LoadDraft("recovery.2")).displayName,
                    Is.EqualTo("Autosave 2"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(transferRoot);
            }
        }

        [Test]
        public void LoadedRecoveryRemainsUnsaved()
        {
            var transferRoot = new GameObject("Level recovery load test");
            var store = new MemoryDraftStore();
            try
            {
                var serializer = new UnityLevelJsonSerializer();
                LevelDocument document = LevelDocumentFactory.CreateEmpty("Recovered Level");
                store.SaveDraft("recovery.0", serializer.Serialize(document));
                LevelTextTransfer transfer = transferRoot.AddComponent<LevelTextTransfer>();
                using var coordinator = new LevelEditorPersistenceCoordinator(
                    serializer,
                    store,
                    transfer,
                    new LevelValidationContent());
                LevelDocumentLoadedEventArgs loaded = null;
                coordinator.DocumentLoaded += (_, args) => loaded = args;

                coordinator.LoadRecovery(0);

                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.Document.displayName, Is.EqualTo("Recovered Level"));
                Assert.That(loaded.SourceLabel, Is.EqualTo("recovery 1"));
                Assert.That(loaded.IsSaved, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(transferRoot);
            }
        }

        private sealed class MemoryDraftStore : ILevelDraftStore
        {
            private readonly Dictionary<string, string> drafts =
                new Dictionary<string, string>(StringComparer.Ordinal);

            public bool HasDraft(string slot) => drafts.ContainsKey(slot);

            public string LoadDraft(string slot)
            {
                return drafts.TryGetValue(slot, out string serialized)
                    ? serialized
                    : throw new InvalidOperationException("The draft is missing.");
            }

            public void SaveDraft(string slot, string serializedLevel)
            {
                drafts[slot] = serializedLevel;
            }

            public void DeleteDraft(string slot)
            {
                drafts.Remove(slot);
            }
        }
    }
}
