using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace GritGud.Presentation.Persistence
{
    internal static class TextFileTransfer
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void GritGud_DownloadTextFile(
            string fileName,
            string content,
            string mediaType);
#endif

        public static string Export(
            string fileName,
            string content,
            string mediaType)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException(
                    "Export file names cannot be empty.",
                    nameof(fileName));
            }

            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (string.IsNullOrWhiteSpace(mediaType))
            {
                throw new ArgumentException(
                    "Export media types cannot be empty.",
                    nameof(mediaType));
            }

            string safeFileName = SanitizeFileName(fileName);
#if UNITY_WEBGL && !UNITY_EDITOR
            GritGud_DownloadTextFile(safeFileName, content, mediaType);
            return $"Downloaded {safeFileName}";
#else
            string directory = Path.Combine(
                UnityEngine.Application.persistentDataPath,
                "Exports");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, safeFileName);
            File.WriteAllText(path, content);
            return $"Exported report to {path}";
#endif
        }

        private static string SanitizeFileName(string value)
        {
            string result = Path.GetFileName(value.Trim());
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(invalid, '-');
            }

            if (string.IsNullOrWhiteSpace(result))
            {
                throw new ArgumentException(
                    "Export file names must contain a valid name.",
                    nameof(value));
            }

            return result.Replace(' ', '-').ToLowerInvariant();
        }
    }
}
