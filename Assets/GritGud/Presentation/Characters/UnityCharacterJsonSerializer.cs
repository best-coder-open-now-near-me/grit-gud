using System;
using GritGud.Application.Characters;
using GritGud.Domain.Characters;
using UnityEngine;

namespace GritGud.Presentation.Characters
{
    public sealed class UnityCharacterJsonSerializer : ICharacterSerializer
    {
        public const int MaximumDocumentCharacters = 256 * 1024;

        public string Serialize(CharacterDocument document, bool prettyPrint = true)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            CharacterDocument copy = document.DeepCopy();
            copy.Normalize();
            return JsonUtility.ToJson(copy, prettyPrint);
        }

        public CharacterDocument Deserialize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new CharacterSerializationException("The imported character text is empty.");
            if (text.Length > MaximumDocumentCharacters)
                throw new CharacterSerializationException("The imported character document is too large.");
            try
            {
                CharacterDocument document = JsonUtility.FromJson<CharacterDocument>(text);
                if (document == null)
                    throw new CharacterSerializationException(
                        "The imported text did not contain a character document.");
                if (document.schemaVersion == 1)
                {
                    document.schemaVersion = CharacterDocument.CurrentSchemaVersion;
                    document.build = new CharacterBuildData();
                    document.startingLoadout = new CharacterLoadoutData();
                }
                else if (document.schemaVersion != CharacterDocument.CurrentSchemaVersion)
                    throw new CharacterSerializationException(
                        $"Character schema {document.schemaVersion} is not supported.");
                document.Normalize();
                return document;
            }
            catch (CharacterSerializationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new CharacterSerializationException(
                    "The imported character text is not valid JSON.",
                    exception);
            }
        }
    }
}
