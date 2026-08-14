using System;
using GritGud.Domain.Levels;

namespace GritGud.Application.Levels
{
    public static class LevelDocumentFactory
    {
        public static LevelDocument CreateEmpty(string displayName = "Untitled Level")
        {
            return new LevelDocument
            {
                schemaVersion = LevelDocument.CurrentSchemaVersion,
                levelId = NewStableId(),
                displayName = displayName,
                bounds = new LevelBoundsData(
                    new Float3Data(0f, 2.5f, 0f),
                    new Float3Data(50f, 10f, 50f)),
                playtest = new LevelPlaytestData
                {
                    playerStart = new LevelTransformData(
                        new Float3Data(0f, 7.5f, 0f),
                        0f),
                },
            };
        }

        public static string NewStableId()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
