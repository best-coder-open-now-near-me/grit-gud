using System;
using System.Collections.Generic;
using System.Globalization;
using GritGud.Application.Levels;
using GritGud.Presentation.Levels;
using UnityEngine;

namespace GritGud.Presentation.Supabase
{
    internal static class SupabaseLevelDraftResponseParser
    {
        public static IReadOnlyList<LevelDraftSummary> ParseSummaries(
            string json)
        {
            SupabaseLevelDraftSummaryRows rows =
                SupabaseResponseParser.DeserializeRpcRows<
                    SupabaseLevelDraftSummaryRows>(
                    json,
                    "level-draft library response");
            if (rows.rows == null)
            {
                throw new InvalidOperationException(
                    "Supabase returned an invalid level-draft library response.");
            }

            var result = new List<LevelDraftSummary>(rows.rows.Length);
            foreach (SupabaseLevelDraftSummaryRow row in rows.rows)
                result.Add(ToSummary(row));
            return result.AsReadOnly();
        }

        public static SupabaseLevelDraftSummaryRow ParseSingleSummary(
            string json)
        {
            SupabaseLevelDraftSummaryRows rows =
                SupabaseResponseParser.DeserializeRpcRows<
                    SupabaseLevelDraftSummaryRows>(
                    json,
                    "level-draft mutation response");
            if (rows.rows == null || rows.rows.Length != 1)
            {
                throw new InvalidOperationException(
                    "Supabase returned an invalid level-draft mutation response.");
            }

            ToSummary(rows.rows[0]);
            return rows.rows[0];
        }

        public static LevelDraftRecord ParseRecord(
            string json,
            UnityLevelJsonSerializer serializer)
        {
            if (serializer == null)
                throw new ArgumentNullException(nameof(serializer));
            SupabaseLevelDraftRecordRows rows =
                SupabaseResponseParser.DeserializeRpcRows<
                    SupabaseLevelDraftRecordRows>(
                    json,
                    "level-draft record response");
            if (rows.rows == null || rows.rows.Length != 1)
            {
                throw new LevelDraftOperationException(
                    LevelDraftFailure.NotFound,
                    "The level draft was not found.");
            }

            SupabaseLevelDraftRecordRow row = rows.rows[0];
            if (string.IsNullOrWhiteSpace(row.document))
            {
                throw new InvalidOperationException(
                    "Supabase returned a level draft without a document.");
            }

            return new LevelDraftRecord(
                ToSummary(row),
                serializer.Deserialize(row.document));
        }

        public static LevelDraftSummary ToSummary(
            SupabaseLevelDraftSummaryRow row)
        {
            if (row == null
                || !DateTimeOffset.TryParse(
                    row.updated_at,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTimeOffset updatedAt))
            {
                throw new InvalidOperationException(
                    "Supabase returned invalid level-draft metadata.");
            }

            return new LevelDraftSummary(
                new LevelDraftId(row.draft_id),
                row.name,
                row.revision,
                updatedAt,
                row.level_id,
                row.display_name,
                row.schema_version);
        }
    }

    [Serializable]
    internal class SupabaseLevelDraftSummaryRow
    {
        public string draft_id;
        public string name;
        public long revision;
        public string updated_at;
        public string level_id;
        public string display_name;
        public int schema_version;
    }

    [Serializable]
    internal sealed class SupabaseLevelDraftRecordRow :
        SupabaseLevelDraftSummaryRow
    {
        public string document;
    }

    [Serializable]
    internal sealed class SupabaseLevelDraftSummaryRows
    {
        public SupabaseLevelDraftSummaryRow[] rows;
    }

    [Serializable]
    internal sealed class SupabaseLevelDraftRecordRows
    {
        public SupabaseLevelDraftRecordRow[] rows;
    }
}
