using System;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using UnityEngine;

namespace GritGud.Presentation.Levels
{
    public sealed class UnityLevelJsonSerializer : ILevelSerializer
    {
        [Serializable]
        private sealed class LegacyPlaytestEnvelope
        {
            public LevelPlaytestData playtest;
        }

        public const int MaximumDocumentCharacters = 2 * 1024 * 1024;

        private readonly LevelDocumentMigrator migrator;

        public UnityLevelJsonSerializer(LevelDocumentMigrator migrator = null)
        {
            this.migrator = migrator ?? new LevelDocumentMigrator();
        }

        public string Serialize(LevelDocument document, bool prettyPrint = true)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            LevelDocument snapshot = document.DeepCopy();
            snapshot.Normalize();
            try
            {
                return JsonUtility.ToJson(snapshot, prettyPrint);
            }
            catch (Exception exception)
            {
                throw new LevelSerializationException("The level could not be serialized.", exception);
            }
        }

        public LevelDocument Deserialize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new LevelSerializationException("The imported level text is empty.");
            }

            if (text.Length > MaximumDocumentCharacters)
            {
                throw new LevelSerializationException(
                    $"The imported level exceeds the {MaximumDocumentCharacters}-character safety limit.");
            }

            try
            {
                // Overwrite an initialized portable document so omitted JSON
                // fields retain the same authored defaults used by the
                // engine-free System.Text.Json content loader.
                var document = new LevelDocument();
                JsonUtility.FromJsonOverwrite(text, document);

                if (document.schemaVersion <= 3)
                {
                    LegacyPlaytestEnvelope legacy =
                        JsonUtility.FromJson<LegacyPlaytestEnvelope>(text);
                    document.legacyPlaytest = legacy?.playtest;
                }

                return migrator.MigrateToCurrent(document);
            }
            catch (LevelSerializationException)
            {
                throw;
            }
            catch (InvalidOperationException exception)
            {
                throw new LevelSerializationException(exception.Message, exception);
            }
            catch (Exception exception)
            {
                throw new LevelSerializationException("The imported level text is not valid JSON.", exception);
            }
        }
    }
}
