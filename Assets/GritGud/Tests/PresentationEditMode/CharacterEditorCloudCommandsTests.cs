using System;
using GritGud.Domain.Characters;
using GritGud.Presentation.CharacterEditing;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class CharacterEditorCloudCommandsTests
    {
        [Test]
        public void SuccessfulSaveMarksOnlyTheRevisionThatWasSent()
        {
            var gateway = new StubGateway();
            var host = new StubHost();
            using var commands = new CharacterEditorCloudCommands(gateway, host);

            commands.Save();
            gateway.SaveSucceeded();

            Assert.That(host.MarkSavedCount, Is.EqualTo(1));
            Assert.That(host.Status, Is.EqualTo("Saved the character to cloud."));

            commands.Save();
            host.Revision++;
            gateway.SaveSucceeded();

            Assert.That(host.MarkSavedCount, Is.EqualTo(1));
        }

        [Test]
        public void FailedSaveDoesNotMarkTheCharacterSaved()
        {
            var gateway = new StubGateway();
            var host = new StubHost();
            using var commands = new CharacterEditorCloudCommands(gateway, host);

            commands.Save();
            gateway.SaveFailed("Cloud rejected save.");

            Assert.That(host.MarkSavedCount, Is.Zero);
            Assert.That(host.Status, Is.EqualTo("Cloud rejected save."));
        }

        [Test]
        public void LoadDoesNotOverwriteEditsMadeWhileRequestIsPending()
        {
            var gateway = new StubGateway();
            var host = new StubHost();
            using var commands = new CharacterEditorCloudCommands(gateway, host);

            commands.Load();
            host.Revision++;
            gateway.LoadSucceeded("loaded");

            Assert.That(host.ReplaceCount, Is.Zero);
            Assert.That(host.Status, Does.Contain("changed while loading"));
        }

        [Test]
        public void DisposedCommandIgnoresLateCallbacks()
        {
            var gateway = new StubGateway();
            var host = new StubHost();
            var commands = new CharacterEditorCloudCommands(gateway, host);

            commands.Load();
            commands.Dispose();
            gateway.LoadSucceeded("loaded");

            Assert.That(host.ReplaceCount, Is.Zero);
        }

        private sealed class StubGateway : ICharacterEditorCloudGateway
        {
            private Action saveSucceeded;
            private Action<string> saveFailed;
            private Action<string> loadSucceeded;

            public bool IsAvailable { get; set; } = true;
            public string UnavailableStatus => "Cloud unavailable.";

            public void Save(
                CharacterDocument document,
                string serialized,
                Action succeeded,
                Action<string> failed)
            {
                saveSucceeded = succeeded;
                saveFailed = failed;
            }

            public void Load(string characterId, Action<string> succeeded, Action<string> failed) =>
                loadSucceeded = succeeded;

            public void SaveSucceeded() => saveSucceeded();
            public void SaveFailed(string error) => saveFailed(error);
            public void LoadSucceeded(string text) => loadSucceeded(text);
        }

        private sealed class StubHost : ICharacterEditorCloudHost
        {
            public bool IsReady { get; set; } = true;
            public long Revision { get; set; } = 4;
            public int MarkSavedCount { get; private set; }
            public int ReplaceCount { get; private set; }
            public string Status { get; private set; }

            public CharacterDocument CreateSnapshot() => new CharacterDocument
            {
                characterId = "character.test",
                displayName = "Test",
            };

            public string Serialize(CharacterDocument document) => "serialized";
            public CharacterDocument DeserializeAndValidate(string text) => CreateSnapshot();
            public void ReplaceWithLoaded(CharacterDocument document) => ReplaceCount++;
            public void MarkSaved() => MarkSavedCount++;
            public void SetStatus(string message) => Status = message;
        }
    }
}
