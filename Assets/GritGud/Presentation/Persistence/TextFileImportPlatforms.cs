using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace GritGud.Presentation.Persistence
{
    internal interface ITextFileImportPlatform
    {
        bool UsesBrowserFileDialog { get; }

        TextFileImportStartResult Start(
            long requestId,
            string desktopImportPath,
            string receiverGameObjectName);
    }

    internal sealed class DesktopTextFileImportPlatform : ITextFileImportPlatform
    {
        public bool UsesBrowserFileDialog => false;

        public TextFileImportStartResult Start(
            long requestId,
            string desktopImportPath,
            string receiverGameObjectName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(desktopImportPath))
                {
                    throw new InvalidOperationException(
                        "Enter a desktop import path first.");
                }

                if (!File.Exists(desktopImportPath))
                {
                    throw new FileNotFoundException(
                        "The import file does not exist.",
                        desktopImportPath);
                }

                return TextFileImportStartResult.Succeeded(
                    File.ReadAllText(desktopImportPath));
            }
            catch (Exception exception)
            {
                return TextFileImportStartResult.Failed(exception.Message);
            }
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    internal sealed class BrowserTextFileImportPlatform : ITextFileImportPlatform
    {
        [DllImport("__Internal")]
        private static extern void GritGud_RequestTextFile(
            string receiverGameObjectName,
            string requestId);

        public bool UsesBrowserFileDialog => true;

        public TextFileImportStartResult Start(
            long requestId,
            string desktopImportPath,
            string receiverGameObjectName)
        {
            GritGud_RequestTextFile(
                receiverGameObjectName,
                requestId.ToString(CultureInfo.InvariantCulture));
            return TextFileImportStartResult.Pending();
        }
    }
#endif

    internal static class TextFileImportPlatformFactory
    {
        public static ITextFileImportPlatform Create()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new BrowserTextFileImportPlatform();
#else
            return new DesktopTextFileImportPlatform();
#endif
        }
    }
}
