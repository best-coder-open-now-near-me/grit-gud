using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayBattleArtifactFormatException :
        FormatException
    {
        public GameplayBattleArtifactFormatException(
            string message,
            Exception innerException = null)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Dependency-free, strict reader and deterministic writer for permanent
    /// battle artifacts. The reader allowlists every schema object and rejects
    /// missing, duplicate, unknown, mistyped, or trailing data.
    /// </summary>
    public static class GameplayBattleArtifactCodec
    {
        public static string Format(GameplayBattleArtifact artifact)
        {
            if (artifact == null) throw new ArgumentNullException(
                nameof(artifact));
            return "{\"format\":\"" + GameplayBattleArtifact.FormatId
                + "\",\"artifact\":"
                + GameplayReproBundleFormatter.FormatCanonicalValue(artifact)
                + "}";
        }

        public static GameplayBattleArtifact Read(string json)
        {
            try
            {
                JsonNode root = new Parser(json).Parse();
                var document = new ObjectReader(root, "document");
                document.RequireString(
                    "format",
                    GameplayBattleArtifact.FormatId);
                GameplayBattleArtifact artifact = ReadArtifact(
                    document.Take("artifact"));
                document.Complete();
                string stable = Format(artifact);
                if (!string.Equals(stable, json, StringComparison.Ordinal))
                    throw new GameplayBattleArtifactFormatException(
                        "Battle artifact JSON is valid but not in canonical byte form.");
                return artifact;
            }
            catch (GameplayBattleArtifactFormatException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new GameplayBattleArtifactFormatException(
                    "Battle artifact parsing failed: " + exception.Message,
                    exception);
            }
        }

        private static GameplayBattleArtifact ReadArtifact(JsonNode node)
        {
            var value = Typed(node, typeof(GameplayBattleArtifact));
            string artifactId = value.String("ArtifactId");
            GameplayBattleArtifactContent content = ReadContent(
                value.Take("Content"));
            int schemaVersion = value.Int32("SchemaVersion");
            value.Complete();
            return new GameplayBattleArtifact(
                schemaVersion,
                artifactId,
                content);
        }

        private static GameplayBattleArtifactContent ReadContent(JsonNode node)
        {
            var value = Typed(node, typeof(GameplayBattleArtifactContent));
            IReadOnlyList<GameplayBattleArtifactDecision> decisions = ReadList(
                value.Take("Decisions"),
                ReadDecision,
                "Decisions");
            GameplayExecutionIdentity identity = ReadExecutionIdentity(
                value.Take("ExecutionIdentity"));
            string initialCanonical = value.String("InitialStateCanonical");
            string initialHash = value.String("InitialStateHash");
            int numericPolicy = value.Int32("NumericPolicyVersion");
            GameplayBattleArtifactProvenance provenance = ReadProvenance(
                value.Take("Provenance"));
            GameplayBattleScoreboard scoreboard = ReadScoreboard(
                value.Take("Scoreboard"));
            GameplayBattleArtifactTerminal terminal = ReadTerminal(
                value.Take("Terminal"));
            IReadOnlyList<GameplayBattleArtifactTransition> transitions =
                ReadList(
                    value.Take("Transitions"),
                    ReadTransition,
                    "Transitions");
            value.Complete();
            return new GameplayBattleArtifactContent(
                numericPolicy,
                identity,
                provenance,
                initialHash,
                initialCanonical,
                transitions,
                decisions,
                terminal,
                scoreboard);
        }

        private static GameplayExecutionIdentity ReadExecutionIdentity(
            JsonNode node)
        {
            var value = Typed(node, typeof(GameplayExecutionIdentity));
            GameplayContentIdentity gameplay = ReadGameplayIdentity(
                value.Take("Gameplay"));
            ScenarioRunIdentity run = ReadRunIdentity(value.Take("Run"));
            SpatialContentIdentity spatial = ReadSpatialIdentity(
                value.Take("Spatial"));
            value.Complete();
            return new GameplayExecutionIdentity(gameplay, spatial, run);
        }

        private static GameplayContentIdentity ReadGameplayIdentity(
            JsonNode node)
        {
            var value = Typed(node, typeof(GameplayContentIdentity));
            string digest = value.String("DefinitionDigest");
            int rulesVersion = value.Int32("RulesSchemaVersion");
            string scenarioId = value.String("ScenarioId");
            int scenarioVersion = value.Int32("ScenarioSchemaVersion");
            value.RequireInt32(
                "SchemaVersion",
                GameplayContentIdentity.CurrentSchemaVersion);
            value.Complete();
            return new GameplayContentIdentity(
                scenarioId,
                scenarioVersion,
                rulesVersion,
                digest);
        }

        private static SpatialContentIdentity ReadSpatialIdentity(JsonNode node)
        {
            var value = Typed(node, typeof(SpatialContentIdentity));
            int evidenceVersion = value.Int32("EvidenceAlgorithmVersion");
            string levelId = value.String("LevelId");
            int levelVersion = value.Int32("LevelSchemaVersion");
            value.RequireInt32(
                "SchemaVersion",
                SpatialContentIdentity.CurrentSchemaVersion);
            string digest = value.String("StaticSpatialDigest");
            value.Complete();
            return new SpatialContentIdentity(
                levelId,
                levelVersion,
                evidenceVersion,
                digest);
        }

        private static ScenarioRunIdentity ReadRunIdentity(JsonNode node)
        {
            var value = Typed(node, typeof(ScenarioRunIdentity));
            int randomVersion = value.Int32("RandomSchemaVersion");
            string runId = value.String("RunId");
            value.RequireInt32(
                "SchemaVersion",
                ScenarioRunIdentity.CurrentSchemaVersion);
            uint seed = value.UInt32("ScenarioSeed");
            value.Complete();
            return new ScenarioRunIdentity(runId, seed, randomVersion);
        }

        private static GameplayBattleArtifactProvenance ReadProvenance(
            JsonNode node)
        {
            var value = Typed(
                node,
                typeof(GameplayBattleArtifactProvenance));
            string label = value.String("Label");
            IReadOnlyList<string> parents = Strings(
                value.Take("ParentArtifactIds"),
                "ParentArtifactIds");
            string branch = value.String("SourceBranch");
            string revision = value.String("SourceRevision");
            value.Complete();
            return new GameplayBattleArtifactProvenance(
                revision,
                branch,
                label,
                parents);
        }

        private static GameplayBattleArtifactTransition ReadTransition(
            JsonNode node)
        {
            var value = Typed(
                node,
                typeof(GameplayBattleArtifactTransition));
            string actorId = value.String("ActorId");
            int? decisionIndex = value.NullableInt32("DecisionIndex");
            IReadOnlyList<string> eventDigests = Strings(
                value.Take("DomainEventPayloadDigests"),
                "DomainEventPayloadDigests");
            IReadOnlyList<string> eventPayloads = Strings(
                value.Take("DomainEventPayloadsCanonical"),
                "DomainEventPayloadsCanonical");
            IReadOnlyList<string> eventTypes = Strings(
                value.Take("DomainEventTypes"),
                "DomainEventTypes");
            string kind = value.String("Kind");
            string previousHash = value.String("PreviousStateHash");
            string resultingCanonical = value.String(
                "ResultingStateCanonical");
            string resultingHash = value.String("ResultingStateHash");
            long sequence = value.Int64("Sequence");
            string subjectId = value.String("SubjectId");
            string transitionCanonical = value.String(
                "TransitionCanonical");
            string payloadDigest = value.String("TransitionPayloadDigest");
            value.Complete();
            return new GameplayBattleArtifactTransition(
                sequence,
                kind,
                actorId,
                subjectId,
                previousHash,
                resultingHash,
                payloadDigest,
                transitionCanonical,
                decisionIndex,
                eventTypes,
                eventDigests,
                eventPayloads,
                resultingCanonical);
        }

        private static GameplayBattleArtifactDecision ReadDecision(
            JsonNode node)
        {
            var value = Typed(
                node,
                typeof(GameplayBattleArtifactDecision));
            string actorId = value.String("ActorId");
            string candidateDigest = value.String("CandidateSetDigest");
            IReadOnlyList<string> candidates = Strings(
                value.Take("CandidateIds"),
                "CandidateIds");
            int decisionIndex = value.Int32("DecisionIndex");
            IReadOnlyList<string> legal = Strings(
                value.Take("LegalCandidateIds"),
                "LegalCandidateIds");
            string policyId = value.String("PolicyId");
            int policyVersion = value.Int32("PolicyVersion");
            string priorHash = value.String("PreviousStateHash");
            string resultingHash = value.String("ResultingStateHash");
            float score = value.Single("Score");
            IReadOnlyList<GameplayPolicyScoreComponent> components = ReadList(
                value.Take("ScoreComponents"),
                ReadScoreComponent,
                "ScoreComponents");
            GameplayPolicySelectionReason reason = value.Enum<
                GameplayPolicySelectionReason>("SelectionReason");
            string selectedId = value.String("SelectedCandidateId");
            string payloadDigest = value.String("TransitionPayloadDigest");
            long transitionSequence = value.Int64("TransitionSequence");
            value.Complete();
            return new GameplayBattleArtifactDecision(
                decisionIndex,
                policyId,
                policyVersion,
                actorId,
                priorHash,
                candidateDigest,
                candidates,
                legal,
                selectedId,
                reason,
                score,
                components,
                transitionSequence,
                payloadDigest,
                resultingHash);
        }

        private static GameplayPolicyScoreComponent ReadScoreComponent(
            JsonNode node)
        {
            var value = Typed(node, typeof(GameplayPolicyScoreComponent));
            float contribution = value.Single("Contribution");
            string featureId = value.String("FeatureId");
            float featureValue = value.Single("FeatureValue");
            float weight = value.Single("Weight");
            value.Complete();
            var result = new GameplayPolicyScoreComponent(
                featureId,
                featureValue,
                weight);
            if (!GameplayNumericPolicy.AreEquivalent(
                    result.Contribution,
                    contribution))
                throw new GameplayBattleArtifactFormatException(
                    "Policy score contribution does not match value and weight.");
            return result;
        }

        private static GameplayBattleArtifactTerminal ReadTerminal(JsonNode node)
        {
            var value = Typed(
                node,
                typeof(GameplayBattleArtifactTerminal));
            IReadOnlyList<string> hostiles = Strings(
                value.Take("CapableHostileActorIds"),
                "CapableHostileActorIds");
            IReadOnlyList<string> party = Strings(
                value.Take("CapablePartyActorIds"),
                "CapablePartyActorIds");
            GameplayDecisionFailureKind? failureKind = value.NullableEnum<
                GameplayDecisionFailureKind>("FailureKind");
            string failureMessage = value.String("FailureMessage");
            string hash = value.String("FinalStateHash");
            GameplayBattleTerminalKind kind = value.Enum<
                GameplayBattleTerminalKind>("Kind");
            long sequence = value.Int64("TransitionSequence");
            value.Complete();
            return new GameplayBattleArtifactTerminal(
                kind,
                sequence,
                hash,
                party,
                hostiles,
                failureKind,
                failureMessage);
        }

        private static GameplayBattleScoreboard ReadScoreboard(JsonNode node)
        {
            var value = Typed(node, typeof(GameplayBattleScoreboard));
            IReadOnlyList<GameplayBattleActorScore> actors = ReadList(
                value.Take("Actors"),
                ReadActorScore,
                "Actors");
            IReadOnlyList<GameplayBattleAmmunitionScore> ammunition = ReadList(
                value.Take("Ammunition"),
                ReadAmmunitionScore,
                "Ammunition");
            int attacks = value.Int32("Attacks");
            int concussive = value.Int32("ConcussiveTargets");
            int decisions = value.Int32("Decisions");
            int droneAttacks = value.Int32("DroneAttacks");
            int droneMoves = value.Int32("DroneMoves");
            int throws = value.Int32("ExplosiveThrows");
            int finalLoaded = value.Int32("FinalLoadedRounds");
            int finalReserve = value.Int32("FinalReserveRounds");
            int fires = value.Int32("FireDeployments");
            int hits = value.Int32("Hits");
            int reloads = value.Int32("Reloads");
            int roundsReloaded = value.Int32("RoundsReloaded");
            int roundsSpent = value.Int32("RoundsSpent");
            int transitions = value.Int32("Transitions");
            int turns = value.Int32("TurnsCompleted");
            int wounds = value.Int32("Wounds");
            value.Complete();
            return new GameplayBattleScoreboard(
                decisions,
                transitions,
                turns,
                attacks,
                hits,
                wounds,
                throws,
                concussive,
                fires,
                droneMoves,
                droneAttacks,
                reloads,
                roundsSpent,
                roundsReloaded,
                finalLoaded,
                finalReserve,
                actors,
                ammunition);
        }

        private static GameplayBattleActorScore ReadActorScore(JsonNode node)
        {
            var value = Typed(node, typeof(GameplayBattleActorScore));
            string actorId = value.String("ActorId");
            int attacks = value.Int32("Attacks");
            int concussive = value.Int32("ConcussiveTargets");
            int decisions = value.Int32("Decisions");
            int droneAttacks = value.Int32("DroneAttacks");
            int droneMoves = value.Int32("DroneMoves");
            int throws = value.Int32("ExplosiveThrows");
            int fires = value.Int32("FireDeployments");
            int finalLoaded = value.Int32("FinalLoadedRounds");
            int finalReserve = value.Int32("FinalReserveRounds");
            int finalWounds = value.Int32("FinalWounds");
            int hits = value.Int32("Hits");
            bool incapacitated = value.Boolean("Incapacitated");
            float distance = value.Single("MovementDistance");
            int moves = value.Int32("Moves");
            int reloads = value.Int32("Reloads");
            int roundsReloaded = value.Int32("RoundsReloaded");
            int roundsSpent = value.Int32("RoundsSpent");
            int turns = value.Int32("TurnsCompleted");
            int wounds = value.Int32("WoundsDealt");
            value.Complete();
            return new GameplayBattleActorScore(
                actorId,
                decisions,
                turns,
                moves,
                distance,
                attacks,
                hits,
                wounds,
                throws,
                concussive,
                fires,
                droneMoves,
                droneAttacks,
                reloads,
                roundsSpent,
                roundsReloaded,
                finalLoaded,
                finalReserve,
                finalWounds,
                incapacitated);
        }

        private static GameplayBattleAmmunitionScore ReadAmmunitionScore(
            JsonNode node)
        {
            var value = Typed(node, typeof(GameplayBattleAmmunitionScore));
            string ammoTypeId = value.String("AmmoTypeId");
            int finalLoaded = value.Int32("FinalLoadedRounds");
            int finalReserve = value.Int32("FinalReserveRounds");
            int reloads = value.Int32("Reloads");
            int roundsReloaded = value.Int32("RoundsReloaded");
            int roundsSpent = value.Int32("RoundsSpent");
            value.Complete();
            return new GameplayBattleAmmunitionScore(
                ammoTypeId,
                reloads,
                roundsSpent,
                roundsReloaded,
                finalLoaded,
                finalReserve);
        }

        private static ObjectReader Typed(JsonNode node, Type type)
        {
            var value = new ObjectReader(node, type.Name);
            value.RequireString("$type", type.FullName ?? type.Name);
            return value;
        }

        private static IReadOnlyList<T> ReadList<T>(
            JsonNode node,
            Func<JsonNode, T> reader,
            string name)
        {
            IReadOnlyList<JsonNode> values = node.RequireArray(name);
            var result = new List<T>(values.Count);
            foreach (JsonNode value in values) result.Add(reader(value));
            return result.AsReadOnly();
        }

        private static IReadOnlyList<string> Strings(
            JsonNode node,
            string name)
        {
            IReadOnlyList<JsonNode> values = node.RequireArray(name);
            var result = new List<string>(values.Count);
            foreach (JsonNode value in values)
                result.Add(value.RequireString(name));
            return result.AsReadOnly();
        }

        private sealed class ObjectReader
        {
            private readonly Dictionary<string, JsonNode> values;
            private readonly string name;

            public ObjectReader(JsonNode node, string objectName)
            {
                name = objectName;
                values = new Dictionary<string, JsonNode>(
                    node.RequireObject(objectName),
                    StringComparer.Ordinal);
            }

            public JsonNode Take(string property)
            {
                if (!values.TryGetValue(property, out JsonNode value))
                    throw new GameplayBattleArtifactFormatException(
                        name + " is missing property '" + property + "'.");
                values.Remove(property);
                return value;
            }

            public string String(string property) => Take(property)
                .RequireString(name + "." + property);

            public bool Boolean(string property) => Take(property)
                .RequireBoolean(name + "." + property);

            public int Int32(string property) => Take(property).RequireInt32(
                name + "." + property);

            public uint UInt32(string property) => Take(property).RequireUInt32(
                name + "." + property);

            public long Int64(string property) => Take(property).RequireInt64(
                name + "." + property);

            public float Single(string property) => Take(property)
                .RequireSingle(name + "." + property);

            public int? NullableInt32(string property)
            {
                JsonNode value = Take(property);
                return value.Kind == JsonKind.Null
                    ? (int?)null
                    : value.RequireInt32(name + "." + property);
            }

            public T Enum<T>(string property) where T : struct
            {
                string text = String(property);
                if (!System.Enum.TryParse(text, ignoreCase: false, out T value)
                    || !System.Enum.IsDefined(typeof(T), value))
                    throw new GameplayBattleArtifactFormatException(
                        name + "." + property + " has invalid enum value '"
                            + text + "'.");
                return value;
            }

            public T? NullableEnum<T>(string property) where T : struct
            {
                JsonNode value = Take(property);
                if (value.Kind == JsonKind.Null) return null;
                string text = value.RequireString(name + "." + property);
                if (!System.Enum.TryParse(text, ignoreCase: false, out T parsed)
                    || !System.Enum.IsDefined(typeof(T), parsed))
                    throw new GameplayBattleArtifactFormatException(
                        name + "." + property + " has invalid enum value '"
                            + text + "'.");
                return parsed;
            }

            public void RequireString(string property, string expected)
            {
                string actual = String(property);
                if (!string.Equals(actual, expected, StringComparison.Ordinal))
                    throw new GameplayBattleArtifactFormatException(
                        name + "." + property + " must equal '" + expected
                            + "'.");
            }

            public void RequireInt32(string property, int expected)
            {
                int actual = Int32(property);
                if (actual != expected)
                    throw new GameplayBattleArtifactFormatException(
                        name + "." + property + " must equal " + expected
                            + ".");
            }

            public void Complete()
            {
                if (values.Count == 0) return;
                var unknown = new List<string>(values.Keys);
                unknown.Sort(StringComparer.Ordinal);
                throw new GameplayBattleArtifactFormatException(
                    name + " has unknown property '" + unknown[0] + "'.");
            }
        }

        internal enum JsonKind
        {
            Null,
            Boolean,
            Number,
            String,
            Array,
            Object,
        }

        internal sealed class JsonNode
        {
            private JsonNode(
                JsonKind kind,
                string text = null,
                bool boolean = false,
                IReadOnlyList<JsonNode> array = null,
                IReadOnlyDictionary<string, JsonNode> properties = null)
            {
                Kind = kind;
                Text = text;
                Boolean = boolean;
                Array = array;
                Properties = properties;
            }

            public JsonKind Kind { get; }
            public string Text { get; }
            public bool Boolean { get; }
            public IReadOnlyList<JsonNode> Array { get; }
            public IReadOnlyDictionary<string, JsonNode> Properties { get; }

            public static JsonNode Null() => new JsonNode(JsonKind.Null);
            public static JsonNode Bool(bool value) => new JsonNode(
                JsonKind.Boolean,
                boolean: value);
            public static JsonNode Number(string value) => new JsonNode(
                JsonKind.Number,
                text: value);
            public static JsonNode String(string value) => new JsonNode(
                JsonKind.String,
                text: value);
            public static JsonNode Sequence(IReadOnlyList<JsonNode> value) =>
                new JsonNode(JsonKind.Array, array: value);
            public static JsonNode Object(
                IReadOnlyDictionary<string, JsonNode> value) => new JsonNode(
                    JsonKind.Object,
                    properties: value);

            public IReadOnlyDictionary<string, JsonNode> RequireObject(
                string name) => Kind == JsonKind.Object
                ? Properties
                : throw Wrong(name, "object");

            public IReadOnlyList<JsonNode> RequireArray(string name) =>
                Kind == JsonKind.Array ? Array : throw Wrong(name, "array");

            public string RequireString(string name) => Kind == JsonKind.String
                ? Text
                : throw Wrong(name, "string");

            public bool RequireBoolean(string name) =>
                Kind == JsonKind.Boolean
                    ? Boolean
                    : throw Wrong(name, "boolean");

            public int RequireInt32(string name)
            {
                if (Kind != JsonKind.Number
                    || !int.TryParse(
                        Text,
                        NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture,
                        out int value))
                    throw Wrong(name, "32-bit integer");
                return value;
            }

            public uint RequireUInt32(string name)
            {
                if (Kind != JsonKind.Number
                    || !uint.TryParse(
                        Text,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out uint value))
                    throw Wrong(name, "unsigned 32-bit integer");
                return value;
            }

            public long RequireInt64(string name)
            {
                if (Kind != JsonKind.Number
                    || !long.TryParse(
                        Text,
                        NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture,
                        out long value))
                    throw Wrong(name, "64-bit integer");
                return value;
            }

            public float RequireSingle(string name)
            {
                if (Kind != JsonKind.Number
                    || !float.TryParse(
                        Text,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float value)
                    || float.IsNaN(value)
                    || float.IsInfinity(value))
                    throw Wrong(name, "finite number");
                return value;
            }

            private GameplayBattleArtifactFormatException Wrong(
                string name,
                string expected) => new GameplayBattleArtifactFormatException(
                    name + " must be a JSON " + expected + ".");
        }

        internal sealed class Parser
        {
            private readonly string text;
            private int index;

            public Parser(string json)
            {
                text = json ?? throw new ArgumentNullException(nameof(json));
            }

            public JsonNode Parse()
            {
                SkipWhitespace();
                JsonNode result = Value(depth: 0);
                SkipWhitespace();
                if (index != text.Length)
                    Fail("Trailing content is not allowed.");
                return result;
            }

            private JsonNode Value(int depth)
            {
                if (depth > 256) Fail("JSON nesting exceeds the limit.");
                SkipWhitespace();
                if (index >= text.Length) Fail("Unexpected end of JSON.");
                switch (text[index])
                {
                    case '{': return Object(depth + 1);
                    case '[': return Array(depth + 1);
                    case '"': return JsonNode.String(String());
                    case 't': Literal("true"); return JsonNode.Bool(true);
                    case 'f': Literal("false"); return JsonNode.Bool(false);
                    case 'n': Literal("null"); return JsonNode.Null();
                    default:
                        if (text[index] == '-'
                            || (text[index] >= '0' && text[index] <= '9'))
                            return JsonNode.Number(Number());
                        Fail("Unexpected JSON token.");
                        return null;
                }
            }

            private JsonNode Object(int depth)
            {
                index++;
                SkipWhitespace();
                var values = new Dictionary<string, JsonNode>(
                    StringComparer.Ordinal);
                if (Consume('}')) return JsonNode.Object(values);
                while (true)
                {
                    if (index >= text.Length || text[index] != '"')
                        Fail("Object property names must be strings.");
                    string name = String();
                    SkipWhitespace();
                    Require(':');
                    JsonNode value = Value(depth);
                    if (!values.TryAdd(name, value))
                        Fail("Duplicate object property '" + name + "'.");
                    SkipWhitespace();
                    if (Consume('}')) return JsonNode.Object(values);
                    Require(',');
                    SkipWhitespace();
                }
            }

            private JsonNode Array(int depth)
            {
                index++;
                SkipWhitespace();
                var values = new List<JsonNode>();
                if (Consume(']')) return JsonNode.Sequence(values.AsReadOnly());
                while (true)
                {
                    values.Add(Value(depth));
                    SkipWhitespace();
                    if (Consume(']'))
                        return JsonNode.Sequence(values.AsReadOnly());
                    Require(',');
                    SkipWhitespace();
                }
            }

            private string String()
            {
                Require('"');
                var value = new StringBuilder();
                while (index < text.Length)
                {
                    char character = text[index++];
                    if (character == '"') return value.ToString();
                    if (character < ' ') Fail("Strings cannot contain controls.");
                    if (character != '\\')
                    {
                        value.Append(character);
                        continue;
                    }
                    if (index >= text.Length) Fail("Incomplete string escape.");
                    char escape = text[index++];
                    switch (escape)
                    {
                        case '"': value.Append('"'); break;
                        case '\\': value.Append('\\'); break;
                        case '/': value.Append('/'); break;
                        case 'b': value.Append('\b'); break;
                        case 'f': value.Append('\f'); break;
                        case 'n': value.Append('\n'); break;
                        case 'r': value.Append('\r'); break;
                        case 't': value.Append('\t'); break;
                        case 'u': value.Append(UnicodeEscape()); break;
                        default: Fail("Unsupported string escape."); break;
                    }
                }
                Fail("Unterminated JSON string.");
                return null;
            }

            private char UnicodeEscape()
            {
                if (index + 4 > text.Length)
                    Fail("Incomplete Unicode escape.");
                int value = 0;
                for (int offset = 0; offset < 4; offset++)
                {
                    char character = text[index++];
                    int digit = character >= '0' && character <= '9'
                        ? character - '0'
                        : character >= 'a' && character <= 'f'
                            ? character - 'a' + 10
                            : character >= 'A' && character <= 'F'
                                ? character - 'A' + 10
                                : -1;
                    if (digit < 0) Fail("Invalid Unicode escape.");
                    value = (value * 16) + digit;
                }
                return (char)value;
            }

            private string Number()
            {
                int start = index;
                if (Consume('-'))
                {
                    if (index >= text.Length) Fail("Incomplete number.");
                }
                if (Consume('0'))
                {
                    if (index < text.Length
                        && text[index] >= '0' && text[index] <= '9')
                        Fail("Numbers cannot contain leading zeroes.");
                }
                else
                {
                    Digits(required: true);
                }
                if (Consume('.')) Digits(required: true);
                if (index < text.Length
                    && (text[index] == 'e' || text[index] == 'E'))
                {
                    index++;
                    if (index < text.Length
                        && (text[index] == '+' || text[index] == '-'))
                        index++;
                    Digits(required: true);
                }
                return text.Substring(start, index - start);
            }

            private void Digits(bool required)
            {
                int start = index;
                while (index < text.Length
                    && text[index] >= '0' && text[index] <= '9')
                    index++;
                if (required && start == index) Fail("Expected a digit.");
            }

            private void Literal(string expected)
            {
                if (index + expected.Length > text.Length
                    || !string.Equals(
                        text.Substring(index, expected.Length),
                        expected,
                        StringComparison.Ordinal))
                    Fail("Invalid JSON literal.");
                index += expected.Length;
            }

            private void Require(char expected)
            {
                if (!Consume(expected))
                    Fail("Expected '" + expected + "'.");
            }

            private bool Consume(char expected)
            {
                if (index >= text.Length || text[index] != expected)
                    return false;
                index++;
                return true;
            }

            private void SkipWhitespace()
            {
                while (index < text.Length)
                {
                    char character = text[index];
                    if (character != ' '
                        && character != '\t'
                        && character != '\r'
                        && character != '\n')
                        return;
                    index++;
                }
            }

            private void Fail(string message) =>
                throw new GameplayBattleArtifactFormatException(
                    message + " Offset " + index + ".");
        }
    }
}
