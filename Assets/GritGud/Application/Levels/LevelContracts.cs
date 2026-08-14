using System;
using GritGud.Domain.Levels;

namespace GritGud.Application.Levels
{
    public sealed class LevelSerializationException : Exception
    {
        public LevelSerializationException(string message)
            : base(message)
        {
        }

        public LevelSerializationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public interface ILevelSerializer
    {
        string Serialize(LevelDocument document, bool prettyPrint = true);

        LevelDocument Deserialize(string text);
    }

    public interface ILevelDraftStore
    {
        bool HasDraft(string slot);

        string LoadDraft(string slot);

        void SaveDraft(string slot, string serializedLevel);

        void DeleteDraft(string slot);
    }
}
