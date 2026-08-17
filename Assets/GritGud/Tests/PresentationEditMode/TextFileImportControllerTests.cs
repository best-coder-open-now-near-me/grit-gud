using System;
using System.IO;
using GritGud.Presentation.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class TextFileImportControllerTests
    {
        [Test]
        public void PendingBrowserRequestRoutesOnlyTheMatchingCallbackOnce()
        {
            var platform = new FakePlatform(
                usesBrowserFileDialog: true,
                TextFileImportStartResult.Pending());
            var controller = new TextFileImportController(
                platform,
                "Import Receiver");
            string imported = null;
            int failureCount = 0;

            long requestId = controller.RequestImport(
                "ignored-on-web.json",
                text => imported = text,
                _ => failureCount++);

            Assert.That(requestId, Is.GreaterThan(0));
            Assert.That(controller.HasPendingRequest, Is.True);
            Assert.That(controller.UsesBrowserFileDialog, Is.True);
            Assert.That(platform.RequestId, Is.EqualTo(requestId));
            Assert.That(platform.ReceiverName, Is.EqualTo("Import Receiver"));
            long rejectedId = controller.RequestImport(
                "another.json",
                _ => Assert.Fail("A second import must not replace the pending request."),
                _ => failureCount++);
            Assert.That(rejectedId, Is.Zero);
            Assert.That(controller.Complete(requestId + 1, "stale"), Is.False);
            Assert.That(controller.Complete(requestId, "{\"kind\":\"character\"}"), Is.True);
            Assert.That(controller.Fail(requestId, "late failure"), Is.False);
            Assert.That(imported, Is.EqualTo("{\"kind\":\"character\"}"));
            Assert.That(failureCount, Is.EqualTo(1));
            Assert.That(controller.HasPendingRequest, Is.False);
        }

        [Test]
        public void CancellingPendingRequestCompletesFailureAndIgnoresLateBrowserResult()
        {
            var controller = new TextFileImportController(
                new FakePlatform(
                    usesBrowserFileDialog: true,
                    TextFileImportStartResult.Pending()),
                "Import Receiver");
            int successCount = 0;
            string failure = null;
            long requestId = controller.RequestImport(
                string.Empty,
                _ => successCount++,
                message => failure = message);

            Assert.That(controller.Cancel(requestId), Is.True);
            Assert.That(controller.Complete(requestId, "late"), Is.False);
            Assert.That(successCount, Is.Zero);
            Assert.That(failure, Does.Contain("cancelled"));
        }

        [Test]
        public void SynchronousPlatformCompletionDoesNotLeavePendingRequest()
        {
            var controller = new TextFileImportController(
                new FakePlatform(
                    usesBrowserFileDialog: false,
                    TextFileImportStartResult.Succeeded("level json")),
                "Import Receiver");
            string imported = null;

            long requestId = controller.RequestImport(
                "level.json",
                text => imported = text,
                Assert.Fail);

            Assert.That(requestId, Is.Zero);
            Assert.That(imported, Is.EqualTo("level json"));
            Assert.That(controller.HasPendingRequest, Is.False);
        }

        [Test]
        public void DesktopPlatformReadsConfiguredTextAndReportsMissingFiles()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "portable json");
                var platform = new DesktopTextFileImportPlatform();

                TextFileImportStartResult loaded = platform.Start(
                    requestId: 1,
                    path,
                    "unused");
                TextFileImportStartResult missing = platform.Start(
                    requestId: 2,
                    path + ".missing",
                    "unused");

                Assert.That(loaded.Status,
                    Is.EqualTo(TextFileImportStartStatus.Succeeded));
                Assert.That(loaded.Value, Is.EqualTo("portable json"));
                Assert.That(missing.Status,
                    Is.EqualTo(TextFileImportStartStatus.Failed));
                Assert.That(missing.Value, Does.Contain("does not exist"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void BrowserMessageEnvelopePreservesMultilineJson()
        {
            Assert.That(TextFileImportMessage.TryDecode(
                "42\n{\n  \"name\": \"Depot\"\n}",
                out long requestId,
                out string text),
                Is.True);
            Assert.That(requestId, Is.EqualTo(42));
            Assert.That(text, Is.EqualTo("{\n  \"name\": \"Depot\"\n}"));
            Assert.That(TextFileImportMessage.TryDecode(
                "invalid",
                out _,
                out _),
                Is.False);
        }

        [Test]
        public void WebGlPluginRoutesGenericRequestIdsAndCancellation()
        {
            string plugin = File.ReadAllText(Path.Combine(
                UnityEngine.Application.dataPath,
                "Plugins",
                "WebGL",
                "GritGudTextFileTransfer.jslib"));

            Assert.That(plugin, Does.Contain("requestId + \"\\n\" + value"));
            Assert.That(plugin, Does.Contain("ReceiveImportedText"));
            Assert.That(plugin, Does.Contain("ReceiveTextImportError"));
            Assert.That(plugin, Does.Contain("addEventListener(\"cancel\""));
            Assert.That(plugin, Does.Contain("window.addEventListener(\"focus\""));
            Assert.That(plugin, Does.Not.Contain("ReceiveImportedLevelText"));
        }

        [Test]
        public void LevelExportSlugRetainsTheExistingPortableFileNameBehavior()
        {
            Assert.That(TextFileTransfer.CreateSlugFileName(
                    "Depot / Yard",
                    "level",
                    ".json"),
                Is.EqualTo("depot---yard.json"));
            Assert.That(TextFileTransfer.CreateSlugFileName(
                    "  ",
                    "level",
                    ".json"),
                Is.EqualTo("level.json"));
        }

        private sealed class FakePlatform : ITextFileImportPlatform
        {
            private readonly TextFileImportStartResult result;

            public FakePlatform(
                bool usesBrowserFileDialog,
                TextFileImportStartResult result)
            {
                UsesBrowserFileDialog = usesBrowserFileDialog;
                this.result = result;
            }

            public bool UsesBrowserFileDialog { get; }

            public long RequestId { get; private set; }

            public string ReceiverName { get; private set; }

            public TextFileImportStartResult Start(
                long requestId,
                string desktopImportPath,
                string receiverGameObjectName)
            {
                RequestId = requestId;
                ReceiverName = receiverGameObjectName;
                return result;
            }
        }
    }
}
