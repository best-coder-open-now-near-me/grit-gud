using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.Levels;
using UnityEngine;

namespace GritGud.Presentation.Supabase
{
    public sealed class SupabaseLevelDraftRepository : ILevelDraftRepository
    {
        private readonly MonoBehaviour coroutineHost;
        private readonly SupabaseClient client;
        private readonly Func<SupabaseSession> getSession;
        private readonly UnityLevelJsonSerializer serializer;

        public SupabaseLevelDraftRepository(
            MonoBehaviour coroutineHost,
            SupabaseClient client,
            Func<SupabaseSession> getSession,
            UnityLevelJsonSerializer serializer = null)
        {
            this.coroutineHost = coroutineHost ?? throw new ArgumentNullException(nameof(coroutineHost));
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
            this.serializer = serializer ?? new UnityLevelJsonSerializer();
        }

        public Task<IReadOnlyList<LevelDraftSummary>> ListAsync(CancellationToken cancellationToken) =>
            Invoke("list_level_draft_library", "{}", cancellationToken, ParseSummaries);

        public async Task<LevelDraftRecord> CreateAsync(string name, LevelDocument document, CancellationToken cancellationToken)
        {
            SummaryRow row = await Invoke("create_level_draft", JsonUtility.ToJson(new CreateRequest { requested_name = name, requested_document = document.DeepCopy() }), cancellationToken, ParseSingleSummary);
            return new LevelDraftRecord(ToSummary(row), document);
        }

        public Task<LevelDraftRecord> LoadAsync(LevelDraftId id, CancellationToken cancellationToken) =>
            Invoke("load_level_draft_by_id", JsonUtility.ToJson(new IdRequest { requested_id = id.Value }), cancellationToken, ParseRecord);

        public async Task<LevelDraftSummary> SaveAsync(LevelDraftId id, long expectedRevision, LevelDocument document, CancellationToken cancellationToken)
        {
            SummaryRow row = await Invoke("save_level_draft", JsonUtility.ToJson(new SaveRequest { requested_id = id.Value, expected_revision = expectedRevision, requested_document = document.DeepCopy() }), cancellationToken, ParseSingleSummary);
            return ToSummary(row);
        }

        public async Task<LevelDraftSummary> RenameAsync(LevelDraftId id, string name, CancellationToken cancellationToken)
        {
            SummaryRow row = await Invoke("rename_level_draft_by_id", JsonUtility.ToJson(new RenameRequest { requested_id = id.Value, requested_name = name }), cancellationToken, ParseSingleSummary);
            return ToSummary(row);
        }

        public async Task<LevelDraftRecord> DuplicateAsync(LevelDraftId id, string name, CancellationToken cancellationToken)
        {
            SummaryRow row = await Invoke("duplicate_level_draft", JsonUtility.ToJson(new RenameRequest { requested_id = id.Value, requested_name = name }), cancellationToken, ParseSingleSummary);
            return await LoadAsync(new LevelDraftId(row.draft_id), cancellationToken);
        }

        public Task DeleteAsync(LevelDraftId id, CancellationToken cancellationToken) =>
            Invoke<object>("archive_level_draft", JsonUtility.ToJson(new IdRequest { requested_id = id.Value }), cancellationToken, _ => null);

        private Task<T> Invoke<T>(string function, string body, CancellationToken cancellationToken, Func<string, T> parse)
        {
            if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<T>(cancellationToken);
            SupabaseSession session = getSession();
            if (session == null)
                return Task.FromException<T>(new LevelDraftOperationException(LevelDraftFailure.Unauthenticated, "Cloud saves are not signed in."));

            var completion = new TaskCompletionSource<T>();
            CancellationTokenRegistration registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
            coroutineHost.StartCoroutine(client.InvokeRpc(function, body, session, cancellationToken, response =>
            {
                registration.Dispose();
                try { completion.TrySetResult(parse(response)); }
                catch (Exception exception) { completion.TrySetException(exception); }
            }, failure =>
            {
                registration.Dispose();
                completion.TrySetException(MapFailure(failure));
            }));
            return completion.Task;
        }

        private static IReadOnlyList<LevelDraftSummary> ParseSummaries(string json)
        {
            SummaryRows rows = JsonUtility.FromJson<SummaryRows>("{\"rows\":" + json + "}");
            var result = new List<LevelDraftSummary>();
            if (rows?.rows != null)
                foreach (SummaryRow row in rows.rows) result.Add(ToSummary(row));
            return result;
        }

        private LevelDraftRecord ParseRecord(string json)
        {
            RecordRows rows = JsonUtility.FromJson<RecordRows>("{\"rows\":" + json + "}");
            if (rows?.rows == null || rows.rows.Length != 1)
                throw new LevelDraftOperationException(LevelDraftFailure.NotFound, "The level draft was not found.");
            RecordRow row = rows.rows[0];
            return new LevelDraftRecord(ToSummary(row), serializer.Deserialize(row.document));
        }

        private static SummaryRow ParseSingleSummary(string json)
        {
            SummaryRows rows = JsonUtility.FromJson<SummaryRows>("{\"rows\":" + json + "}");
            if (rows?.rows == null || rows.rows.Length != 1)
                throw new InvalidOperationException("Supabase returned an invalid draft result.");
            return rows.rows[0];
        }

        private static LevelDraftSummary ToSummary(SummaryRow row)
        {
            if (row == null || !DateTimeOffset.TryParse(row.updated_at, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset updatedAt))
                throw new InvalidOperationException("Supabase returned invalid draft metadata.");
            return new LevelDraftSummary(new LevelDraftId(row.draft_id), row.name, row.revision, updatedAt, row.level_id, row.display_name, row.schema_version);
        }

        private static LevelDraftOperationException MapFailure(SupabaseRequestFailure failure)
        {
            LevelDraftFailure kind = failure.StatusCode == 401 ? LevelDraftFailure.Unauthenticated
                : failure.Code == "23505" ? LevelDraftFailure.NameConflict
                : failure.Code == "40001" ? LevelDraftFailure.RevisionConflict
                : failure.Code == "P0002" ? LevelDraftFailure.NotFound
                : failure.StatusCode == 0 || failure.StatusCode >= 500 ? LevelDraftFailure.Unavailable
                : LevelDraftFailure.Unknown;
            return new LevelDraftOperationException(kind, failure.Message);
        }

        [Serializable] private class IdRequest { public string requested_id; }
        [Serializable] private sealed class RenameRequest : IdRequest { public string requested_name; }
        [Serializable] private sealed class CreateRequest { public string requested_name; public LevelDocument requested_document; }
        [Serializable] private sealed class SaveRequest : IdRequest { public long expected_revision; public LevelDocument requested_document; }
        [Serializable] private class SummaryRow { public string draft_id; public string name; public long revision; public string updated_at; public string level_id; public string display_name; public int schema_version; }
        [Serializable] private sealed class RecordRow : SummaryRow { public string document; }
        [Serializable] private sealed class SummaryRows { public SummaryRow[] rows; }
        [Serializable] private sealed class RecordRows { public RecordRow[] rows; }
    }
}
