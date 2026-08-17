using System;
using System.Collections.Generic;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.Levels;
using GritGud.Presentation.Supabase;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class SupabaseBoundaryTests
    {
        [Test]
        public void SessionParserAcceptsValidResponseWithDeterministicExpiry()
        {
            DateTimeOffset receivedAt = DateTimeOffset.Parse(
                "2026-08-17T12:00:00Z");

            bool parsed = SupabaseResponseParser.TryParseSession(
                "{\"access_token\":\"access\",\"refresh_token\":\"refresh\","
                    + "\"expires_in\":3600,\"user\":{\"id\":\"user\"}}",
                "session response",
                receivedAt,
                out SupabaseSession session,
                out string error);

            Assert.That(parsed, Is.True, error);
            Assert.That(session.AccessToken, Is.EqualTo("access"));
            Assert.That(session.RefreshToken, Is.EqualTo("refresh"));
            Assert.That(session.UserId, Is.EqualTo("user"));
            Assert.That(session.ExpiresAt,
                Is.EqualTo(receivedAt.AddHours(1)));
        }

        [TestCase("")]
        [TestCase("not-json")]
        [TestCase("{}")]
        [TestCase("{\"access_token\":\"access\",\"user\":{}}")]
        public void SessionParserRejectsMalformedOrIncompleteResponses(
            string json)
        {
            Assert.That(SupabaseResponseParser.TryParseSession(
                    json,
                    "refreshed-session response",
                    DateTimeOffset.UtcNow,
                    out SupabaseSession session,
                    out string error),
                Is.False);
            Assert.That(session, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void DocumentParserRequiresAValidDocumentRow()
        {
            Assert.That(SupabaseResponseParser.TryParseDocument(
                    "[{\"document\":\"{\\\"schemaVersion\\\":1}\"}]",
                    out string document,
                    out string error),
                Is.True,
                error);
            Assert.That(document, Is.EqualTo("{\"schemaVersion\":1}"));

            Assert.That(SupabaseResponseParser.TryParseDocument(
                    "[{\"wrong\":\"shape\"}]",
                    out _,
                    out error),
                Is.False);
            Assert.That(error, Is.EqualTo("No cloud document was found."));
        }

        [Test]
        public void CompletionGateInvokesExactlyOneTerminalCallback()
        {
            int successCount = 0;
            int failureCount = 0;
            var completion = new SupabaseCallbackCompletion<string, string>(
                _ => successCount++,
                _ => failureCount++);

            completion.Fail("malformed");
            completion.Succeed("late success");
            completion.Fail("duplicate failure");

            Assert.That(completion.IsCompleted, Is.True);
            Assert.That(successCount, Is.Zero);
            Assert.That(failureCount, Is.EqualTo(1));
        }

        [Test]
        public void InvalidRefreshTokenFallsBackToAnonymousImmediately()
        {
            var state = new SupabaseAuthenticationState();
            state.Initialize("expired-refresh");
            Assert.That(state.TryBegin(), Is.True);

            bool retryAnonymously = state.FailRefresh(
                "invalid refresh token",
                now: 12f);

            Assert.That(retryAnonymously, Is.True);
            Assert.That(state.RequestRunning, Is.False);
            Assert.That(state.AnonymousSignInRequired, Is.True);
            Assert.That(state.PendingRefreshToken, Is.Empty);
            Assert.That(state.NextAttemptAt, Is.EqualTo(12f));
            Assert.That(state.TryBegin(), Is.True);
        }

        [Test]
        public void MalformedRefreshResponseReleasesRequestAndSchedulesRetry()
        {
            var state = new SupabaseAuthenticationState();
            state.Initialize("refresh");
            Assert.That(state.TryBegin(), Is.True);

            bool retryAnonymously = state.FailRefresh(
                "Supabase returned an invalid refreshed-session response.",
                now: 20f);

            Assert.That(retryAnonymously, Is.False);
            Assert.That(state.RequestRunning, Is.False);
            Assert.That(state.AnonymousSignInRequired, Is.False);
            Assert.That(state.PendingRefreshToken, Is.EqualTo("refresh"));
            Assert.That(state.NextAttemptAt, Is.EqualTo(35f));
        }

        [Test]
        public void AuthenticationCompletionRetainsRotatedRefreshToken()
        {
            var state = new SupabaseAuthenticationState();
            state.Initialize("old-refresh");
            Assert.That(state.TryBegin(), Is.True);
            var session = new SupabaseSession(
                "access",
                "new-refresh",
                "user");

            state.Complete(session);

            Assert.That(state.RequestRunning, Is.False);
            Assert.That(state.ShouldRefresh, Is.True);
            Assert.That(state.PendingRefreshToken,
                Is.EqualTo("new-refresh"));
        }

        [Test]
        public void DraftLibraryParserAcceptsTheRpcColumnContract()
        {
            IReadOnlyList<LevelDraftSummary> drafts =
                SupabaseLevelDraftResponseParser.ParseSummaries(
                    "[{\"draft_id\":\"d8a2ad2a-2787-4b38-a8b0-0ced12ebee58\","
                    + "\"name\":\"Depot\",\"revision\":3,"
                    + "\"updated_at\":\"2026-08-17T12:00:00Z\","
                    + "\"level_id\":\"depot\",\"display_name\":\"Depot\","
                    + "\"schema_version\":7}]");

            Assert.That(drafts, Has.Count.EqualTo(1));
            Assert.That(drafts[0].Name, Is.EqualTo("Depot"));
            Assert.That(drafts[0].Revision, Is.EqualTo(3));
            Assert.That(drafts[0].LevelId, Is.EqualTo("depot"));
            Assert.That(drafts[0].SchemaVersion, Is.EqualTo(7));
        }

        [Test]
        public void DraftMutationParserRequiresExactlyOneValidRow()
        {
            Assert.Throws<InvalidOperationException>(() =>
                SupabaseLevelDraftResponseParser.ParseSingleSummary("[]"));
            Assert.Throws<InvalidOperationException>(() =>
                SupabaseLevelDraftResponseParser.ParseSingleSummary(
                    "[{\"draft_id\":\"draft\"}]"));
        }

        [Test]
        public void DraftRecordParserRequiresDocumentPayload()
        {
            var serializer = new UnityLevelJsonSerializer();
            Assert.Throws<InvalidOperationException>(() =>
                SupabaseLevelDraftResponseParser.ParseRecord(
                    "[{\"draft_id\":\"d8a2ad2a-2787-4b38-a8b0-0ced12ebee58\","
                        + "\"name\":\"Depot\",\"revision\":1,"
                        + "\"updated_at\":\"2026-08-17T12:00:00Z\","
                        + "\"document\":\"\"}]",
                    serializer));
        }

        [Test]
        public void DraftRecordParserAcceptsTheLoadRpcContract()
        {
            var serializer = new UnityLevelJsonSerializer();
            string serialized = serializer.Serialize(new LevelDocument
            {
                levelId = "depot",
                displayName = "Depot",
            }, prettyPrint: false);
            string response = "[{\"draft_id\":"
                + "\"d8a2ad2a-2787-4b38-a8b0-0ced12ebee58\","
                + "\"name\":\"Depot\",\"revision\":2,"
                + "\"updated_at\":\"2026-08-17T12:00:00Z\","
                + "\"level_id\":\"depot\",\"display_name\":\"Depot\","
                + "\"schema_version\":7,\"document\":\""
                + EscapeJsonString(serialized)
                + "\"}]";

            LevelDraftRecord record =
                SupabaseLevelDraftResponseParser.ParseRecord(
                    response,
                    serializer);

            Assert.That(record.Summary.Revision, Is.EqualTo(2));
            Assert.That(record.Summary.LevelId, Is.EqualTo("depot"));
            Assert.That(record.Summary.DisplayName, Is.EqualTo("Depot"));
            Assert.That(record.Summary.SchemaVersion, Is.EqualTo(7));
            Assert.That(record.CreateDocumentSnapshot().levelId,
                Is.EqualTo("depot"));
        }

        [TestCase(401, "", LevelDraftFailure.Unauthenticated)]
        [TestCase(409, "23505", LevelDraftFailure.NameConflict)]
        [TestCase(409, "40001", LevelDraftFailure.RevisionConflict)]
        [TestCase(404, "P0002", LevelDraftFailure.NotFound)]
        [TestCase(503, "", LevelDraftFailure.Unavailable)]
        public void DraftFailureMappingPreservesRpcContract(
            long statusCode,
            string code,
            LevelDraftFailure expected)
        {
            LevelDraftOperationException mapped =
                SupabaseLevelDraftRepository.MapFailure(
                    new SupabaseRequestFailure(
                        code,
                        "failure",
                        statusCode));

            Assert.That(mapped.Failure, Is.EqualTo(expected));
        }

        [Test]
        public void CancellationFailureIsExplicit()
        {
            SupabaseRequestFailure failure =
                SupabaseRequestFailure.Cancelled();

            Assert.That(failure.IsCancelled, Is.True);
            Assert.That(failure.Code, Is.EqualTo("cancelled"));
            Assert.That(failure.Message, Does.Contain("cancelled"));
        }

        private static string EscapeJsonString(string value) =>
            value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
