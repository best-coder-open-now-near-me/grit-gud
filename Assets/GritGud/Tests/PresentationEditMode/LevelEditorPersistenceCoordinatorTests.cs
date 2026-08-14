using System;
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

        private sealed class MemoryDraftStore : ILevelDraftStore
        {
            private string serialized;

            public bool HasDraft(string slot) => serialized != null;

            public string LoadDraft(string slot)
            {
                return serialized
                    ?? throw new InvalidOperationException("The draft is missing.");
            }

            public void SaveDraft(string slot, string serializedLevel)
            {
                serialized = serializedLevel;
            }

            public void DeleteDraft(string slot)
            {
                serialized = null;
            }
        }
    }
}
