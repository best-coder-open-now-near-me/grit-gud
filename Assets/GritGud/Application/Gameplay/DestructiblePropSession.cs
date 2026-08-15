using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;

namespace GritGud.Application.Gameplay
{
    public sealed class DestructiblePropSession
    {
        private const float IntegrityTolerance = 0.0001f;

        private readonly Dictionary<string, DestructiblePropSnapshot> props;
        private readonly IReadOnlyList<string> propIds;
        private readonly List<DestructibleDamageRecord> damageRecords =
            new List<DestructibleDamageRecord>();
        private readonly IReadOnlyList<DestructibleDamageRecord> readOnlyDamageRecords;

        public DestructiblePropSession(
            IEnumerable<DestructiblePropDefinition> definitions,
            GameplayJournal journal = null)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            props = new Dictionary<string, DestructiblePropSnapshot>(
                StringComparer.Ordinal);
            Journal = journal ?? new GameplayJournal();
            var ids = new List<string>();
            foreach (DestructiblePropDefinition definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException(
                        "Destructible definitions cannot contain null entries.",
                        nameof(definitions));
                }

                if (!props.TryAdd(definition.Id, definition.CreateInitialSnapshot()))
                {
                    throw new ArgumentException(
                        $"Destructible prop '{definition.Id}' is defined more than once.",
                        nameof(definitions));
                }

                ids.Add(definition.Id);
            }

            ids.Sort(StringComparer.Ordinal);
            propIds = ids.AsReadOnly();
            readOnlyDamageRecords = damageRecords.AsReadOnly();
        }

        public IReadOnlyList<string> PropIds => propIds;

        public GameplayJournal Journal { get; }

        public IReadOnlyList<DestructibleDamageRecord> DamageRecords =>
            readOnlyDamageRecords;

        public event Action<DestructibleDamageRecord> Damaged;

        public static DestructiblePropSession FromLevel(
            LevelDocument level,
            GameplayJournal journal = null)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            var definitions = new List<DestructiblePropDefinition>();
            foreach (LevelEntity entity in level.entities)
            {
                DestructibleInstanceData destructible = entity.destructible;
                if (destructible == null || !destructible.enabled)
                {
                    continue;
                }

                definitions.Add(new DestructiblePropDefinition(
                    entity.id,
                    destructible.integrity,
                    ParseState(destructible.initialState),
                    new GameplayPropPose(
                        new GameplayPosition(
                            entity.transform.position.x,
                            entity.transform.position.y,
                            entity.transform.position.z),
                        pitchDegrees: 0f,
                        yawDegrees: entity.transform.yawDegrees,
                        rollDegrees: 0f),
                    DestructiblePropPosture.Upright));
            }

            return new DestructiblePropSession(definitions, journal);
        }

        public DestructiblePropSnapshot GetProp(string propId)
        {
            if (!props.TryGetValue(propId ?? string.Empty, out var prop))
            {
                throw new KeyNotFoundException(
                    $"Destructible prop '{propId}' is not in this session.");
            }

            return prop;
        }

        public bool TryGetProp(
            string propId,
            out DestructiblePropSnapshot prop) =>
            props.TryGetValue(propId ?? string.Empty, out prop);

        public bool TryApplyDamage(
            string propId,
            float requestedDamage,
            out DestructibleDamageRecord record)
        {
            var notifications = new GameplayNotificationBatch();
            bool applied = TryApplyDamage(
                propId,
                requestedDamage,
                out record,
                notifications);
            notifications.Publish();
            return applied;
        }

        internal bool TryApplyDamage(
            string propId,
            float requestedDamage,
            out DestructibleDamageRecord record,
            GameplayNotificationBatch notifications)
        {
            if (notifications == null)
            {
                throw new ArgumentNullException(nameof(notifications));
            }

            if (float.IsNaN(requestedDamage)
                || float.IsInfinity(requestedDamage)
                || requestedDamage <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(requestedDamage));
            }

            DestructiblePropSnapshot previous = GetProp(propId);
            if (previous.State == DestructiblePropState.Destroyed)
            {
                record = null;
                return false;
            }

            float appliedDamage = Math.Min(
                requestedDamage,
                previous.RemainingIntegrity);
            float remainingIntegrity = Math.Max(
                0f,
                previous.RemainingIntegrity - appliedDamage);
            DestructiblePropState resultingState = remainingIntegrity <= 0f
                ? DestructiblePropState.Destroyed
                : DestructiblePropState.Damaged;
            var resulting = new DestructiblePropSnapshot(
                previous.PropId,
                resultingState,
                previous.MaximumIntegrity,
                remainingIntegrity,
                previous.Pose,
                previous.Posture);
            record = new DestructibleDamageRecord(
                damageRecords.Count + 1L,
                appliedDamage,
                previous,
                resulting);
            CommitDamage(record, notifications);
            return true;
        }

        public void CommitDamage(DestructibleDamageRecord record)
        {
            var notifications = new GameplayNotificationBatch();
            CommitDamage(record, notifications);
            notifications.Publish();
        }

        internal void CommitDamage(
            DestructibleDamageRecord record,
            GameplayNotificationBatch notifications)
        {
            if (notifications == null)
            {
                throw new ArgumentNullException(nameof(notifications));
            }

            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            long expectedSequence = damageRecords.Count + 1L;
            if (record.Sequence != expectedSequence)
            {
                throw new InvalidOperationException(
                    "The damage record is not the next authoritative sequence.");
            }

            DestructiblePropSnapshot current = GetProp(record.PropId);
            if (!SnapshotsMatch(current, record.Previous))
            {
                throw new InvalidOperationException(
                    "The damage record does not start from current prop state.");
            }

            float expectedRemaining = Math.Max(
                0f,
                current.RemainingIntegrity - record.AppliedDamage);
            DestructiblePropState expectedState = expectedRemaining <= 0f
                ? DestructiblePropState.Destroyed
                : DestructiblePropState.Damaged;
            if (!Approximately(expectedRemaining, record.Resulting.RemainingIntegrity)
                || record.Resulting.State != expectedState
                || !Approximately(
                    current.MaximumIntegrity,
                    record.Resulting.MaximumIntegrity)
                || !PropStatesMatch(current, record.Resulting))
            {
                throw new InvalidOperationException(
                    "The damage record's resulting prop state is inconsistent.");
            }

            props[record.PropId] = record.Resulting;
            damageRecords.Add(record);
            Journal.RecordDestructibleDamaged(record);
            notifications.Add(Damaged, record);
        }

        public void CommitDisplacement(DisplacementRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            if (!record.Succeeded
                || record.Request.SubjectKind != DisplacementSubjectKind.Prop)
            {
                throw new ArgumentException(
                    "Only successful prop displacement can update destructible state.",
                    nameof(record));
            }

            DestructiblePropSnapshot current = GetProp(record.Request.SubjectId);
            PropDisplacementState previous = record.PreviousPropState;
            PropDisplacementState resulting = record.ResultingPropState;
            if (previous == null
                || resulting == null
                || !PropStatesMatch(current, previous))
            {
                throw new InvalidOperationException(
                    "Prop displacement no longer starts from authoritative state.");
            }

            props[current.PropId] = new DestructiblePropSnapshot(
                current.PropId,
                current.State,
                current.MaximumIntegrity,
                current.RemainingIntegrity,
                resulting.Pose,
                resulting.Posture);
        }

        public static DestructiblePropState ParseState(string value)
        {
            if (string.Equals(value, "intact", StringComparison.OrdinalIgnoreCase))
            {
                return DestructiblePropState.Intact;
            }

            if (string.Equals(value, "damaged", StringComparison.OrdinalIgnoreCase))
            {
                return DestructiblePropState.Damaged;
            }

            if (string.Equals(value, "destroyed", StringComparison.OrdinalIgnoreCase))
            {
                return DestructiblePropState.Destroyed;
            }

            throw new ArgumentException(
                $"Unknown destructible prop state '{value}'.",
                nameof(value));
        }

        private static bool SnapshotsMatch(
            DestructiblePropSnapshot left,
            DestructiblePropSnapshot right) =>
            string.Equals(left.PropId, right.PropId, StringComparison.Ordinal)
            && left.State == right.State
            && Approximately(left.MaximumIntegrity, right.MaximumIntegrity)
            && Approximately(left.RemainingIntegrity, right.RemainingIntegrity)
            && PropStatesMatch(left, right);

        private static bool PropStatesMatch(
            DestructiblePropSnapshot snapshot,
            PropDisplacementState state) =>
            snapshot.Posture == state.Posture
            && PosesMatch(snapshot.Pose, state.Pose);

        private static bool PropStatesMatch(
            DestructiblePropSnapshot left,
            DestructiblePropSnapshot right) =>
            left.Posture == right.Posture
            && PosesMatch(left.Pose, right.Pose);

        private static bool PosesMatch(
            GameplayPropPose left,
            GameplayPropPose right) =>
            PositionsMatch(left.Position, right.Position)
            && Approximately(left.PitchDegrees, right.PitchDegrees)
            && Approximately(left.YawDegrees, right.YawDegrees)
            && Approximately(left.RollDegrees, right.RollDegrees);

        private static bool PositionsMatch(
            GameplayPosition left,
            GameplayPosition right) =>
            Approximately(left.X, right.X)
            && Approximately(left.Y, right.Y)
            && Approximately(left.Z, right.Z);

        private static bool Approximately(float left, float right) =>
            Math.Abs(left - right) <= IntegrityTolerance;
    }
}
