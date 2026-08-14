using System;
using System.IO;
using System.Runtime.InteropServices;
using GritGud.Presentation.Persistence;
using UnityEngine;

namespace GritGud.Presentation.Levels.Persistence
{
    public sealed class LevelTextTransfer : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void GritGud_RequestTextFile(string gameObjectName);
#endif

        public event Action<string> ImportCompleted;

        public event Action<string> ImportFailed;

        public string DesktopImportPath { get; set; }

        public bool UsesBrowserFileDialog
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        private void Awake()
        {
            DesktopImportPath = Path.Combine(
                UnityEngine.Application.persistentDataPath,
                "Imports",
                "level.json");
        }

        public string Export(string displayName, string serializedLevel)
        {
            if (serializedLevel == null)
            {
                throw new ArgumentNullException(nameof(serializedLevel));
            }

            string fileName = SanitizeFileName(displayName) + ".json";
            return TextFileTransfer.Export(
                fileName,
                serializedLevel,
                "application/json;charset=utf-8");
        }

        public void RequestImport()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            GritGud_RequestTextFile(gameObject.name);
#else
            try
            {
                if (string.IsNullOrWhiteSpace(DesktopImportPath))
                {
                    throw new InvalidOperationException("Enter a desktop import path first.");
                }

                if (!File.Exists(DesktopImportPath))
                {
                    throw new FileNotFoundException("The import file does not exist.", DesktopImportPath);
                }

                ImportCompleted?.Invoke(File.ReadAllText(DesktopImportPath));
            }
            catch (Exception exception)
            {
                ImportFailed?.Invoke(exception.Message);
            }
#endif
        }

        public void ReceiveImportedLevelText(string text)
        {
            ImportCompleted?.Invoke(text);
        }

        public void ReceiveLevelImportError(string message)
        {
            ImportFailed?.Invoke(string.IsNullOrWhiteSpace(message) ? "Browser import failed." : message);
        }

        private static string SanitizeFileName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "level" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(invalid, '-');
            }

            return result.Replace(' ', '-').ToLowerInvariant();
        }
    }
}
