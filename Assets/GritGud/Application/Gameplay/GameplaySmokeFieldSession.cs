using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplaySmokeFieldSession :
        ISightObscuranceQuery,
        IDisposable
    {
        private const float IntersectionTolerance = 0.000001f;

        private sealed class ActiveField
        {
            public ActiveField(SmokeFieldRecord field)
                : this(field, 1f)
            {
            }

            public ActiveField(
                SmokeFieldRecord field,
                float remainingFraction)
            {
                Field = field;
                RemainingFraction = remainingFraction;
            }

            public SmokeFieldRecord Field { get; }

            public float RemainingFraction { get; set; }
        }

        private readonly GameplaySession gameplay;
        private readonly Dictionary<string, ActiveField> active =
            new Dictionary<string, ActiveField>(StringComparer.Ordinal);
        private readonly List<string> expiredIds = new List<string>();
        private bool disposed;
        private bool canonicalProjectionBound;

        public GameplaySmokeFieldSession(GameplaySession gameplaySession)
        {
            gameplay = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            gameplay.TurnEnded += HandleTurnEnded;
        }

        public long Revision { get; private set; }

        public int ActiveCount => active.Count;

        public event Action<SmokeFieldRecord> FieldDeployed;

        public event Action<SmokeFieldRecord> FieldExpired;

        internal void BindCanonicalProjection(
            IReadOnlyList<SmokeFieldSnapshot> snapshots)
        {
            if (canonicalProjectionBound)
                throw new InvalidOperationException(
                    "Smoke fields already have a canonical runtime projection.");
            ValidateCanonicalProjection(snapshots);
            if (!Matches(snapshots))
                throw new InvalidOperationException(
                    "Smoke field session does not match the initial canonical state.");
            canonicalProjectionBound = true;
        }

        internal void ValidateCanonicalProjection(
            IReadOnlyList<SmokeFieldSnapshot> snapshots)
        {
            ThrowIfDisposed();
            if (snapshots == null)
                throw new ArgumentNullException(nameof(snapshots));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (SmokeFieldSnapshot snapshot in snapshots)
            {
                if (!ids.Add(snapshot.Field.Id))
                    throw new InvalidOperationException(
                        $"Canonical smoke field '{snapshot.Field.Id}' is duplicated.");
                if (active.TryGetValue(
                        snapshot.Field.Id,
                        out ActiveField current)
                    && !string.Equals(
                        GameplayCanonicalValueDigest.Calculate(current.Field),
                        GameplayCanonicalValueDigest.Calculate(snapshot.Field),
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Canonical smoke field '{snapshot.Field.Id}' changed its definition.");
                }
            }
        }

        internal void InstallCanonicalProjection(
            IReadOnlyList<SmokeFieldSnapshot> snapshots,
            GameplayNotificationBatch notifications)
        {
            if (!canonicalProjectionBound)
                throw new InvalidOperationException(
                    "Smoke fields are not bound to a canonical runtime.");
            if (notifications == null)
                throw new ArgumentNullException(nameof(notifications));
            ValidateCanonicalProjection(snapshots);
            if (Matches(snapshots)) return;

            var next = new Dictionary<string, ActiveField>(
                StringComparer.Ordinal);
            foreach (SmokeFieldSnapshot snapshot in snapshots)
            {
                next.Add(
                    snapshot.Field.Id,
                    new ActiveField(
                        snapshot.Field,
                        snapshot.RemainingFraction));
                if (!active.ContainsKey(snapshot.Field.Id))
                    notifications.Add(FieldDeployed, snapshot.Field);
            }
            foreach (KeyValuePair<string, ActiveField> entry in active)
                if (!next.ContainsKey(entry.Key))
                    notifications.Add(FieldExpired, entry.Value.Field);
            active.Clear();
            foreach (KeyValuePair<string, ActiveField> entry in next)
                active.Add(entry.Key, entry.Value);
            Revision++;
        }

        public IReadOnlyList<SmokeFieldSnapshot> CaptureActiveFields()
        {
            ThrowIfDisposed();
            var snapshots = new List<SmokeFieldSnapshot>(active.Count);
            foreach (ActiveField state in active.Values)
            {
                snapshots.Add(new SmokeFieldSnapshot(
                    state.Field,
                    state.RemainingFraction));
            }

            snapshots.Sort((left, right) => string.CompareOrdinal(
                left.Field.Id,
                right.Field.Id));
            return snapshots.AsReadOnly();
        }

        public bool TryGetField(
            string fieldId,
            out SmokeFieldSnapshot snapshot)
        {
            ThrowIfDisposed();
            if (fieldId != null
                && active.TryGetValue(fieldId, out ActiveField state))
            {
                snapshot = new SmokeFieldSnapshot(
                    state.Field,
                    state.RemainingFraction);
                return true;
            }

            snapshot = default;
            return false;
        }

        public void Deploy(SmokeFieldRecord field)
        {
            var notifications = new GameplayNotificationBatch();
            Deploy(field, notifications);
            notifications.Publish();
        }

        internal void Deploy(
            SmokeFieldRecord field,
            GameplayNotificationBatch notifications)
        {
            RequireLegacyMutationAllowed(nameof(Deploy));
            if (notifications == null)
                throw new ArgumentNullException(nameof(notifications));
            ThrowIfDisposed();
            if (field == null)
                throw new ArgumentNullException(nameof(field));
            if (!active.TryAdd(field.Id, new ActiveField(field)))
                throw new InvalidOperationException(
                    $"Smoke field '{field.Id}' is already active.");

            Revision++;
            notifications.Add(FieldDeployed, field);
        }

        public void AdvanceContinuousTime(float deltaTime)
        {
            RequireLegacyMutationAllowed(nameof(AdvanceContinuousTime));
            ThrowIfDisposed();
            if (float.IsNaN(deltaTime)
                || float.IsInfinity(deltaTime)
                || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (deltaTime <= 0f
                || gameplay.Mode != GameplaySessionMode.Exploration)
                return;

            AdvanceFields(state => deltaTime
                / state.Field.Definition.ExplorationDurationSeconds);
        }

        public bool BlocksSight(
            GameplayPosition origin,
            GameplayPosition destination)
        {
            ThrowIfDisposed();
            foreach (ActiveField state in active.Values)
            {
                SmokeFieldRecord field = state.Field;
                if (CalculateTraversalLength(
                        origin,
                        destination,
                        field.Origin,
                        field.Definition.Radius,
                        field.Definition.Height)
                    >= field.Definition.MinimumObscuredPath)
                {
                    return true;
                }
            }

            return false;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            gameplay.TurnEnded -= HandleTurnEnded;
            active.Clear();
            expiredIds.Clear();
            FieldDeployed = null;
            FieldExpired = null;
            disposed = true;
        }

        internal static float CalculateTraversalLength(
            GameplayPosition origin,
            GameplayPosition destination,
            GameplayPosition fieldOrigin,
            float radius,
            float height)
        {
            float dx = destination.X - origin.X;
            float dy = destination.Y - origin.Y;
            float dz = destination.Z - origin.Z;
            float segmentLength = (float)Math.Sqrt(
                (dx * dx) + (dy * dy) + (dz * dz));
            if (segmentLength <= IntersectionTolerance)
                return 0f;

            float radialStartX = origin.X - fieldOrigin.X;
            float radialStartZ = origin.Z - fieldOrigin.Z;
            if (!TryGetRadialInterval(
                    radialStartX,
                    radialStartZ,
                    dx,
                    dz,
                    radius,
                    out float radialMinimum,
                    out float radialMaximum)
                || !TryGetAxisInterval(
                    origin.Y,
                    dy,
                    fieldOrigin.Y,
                    fieldOrigin.Y + height,
                    out float verticalMinimum,
                    out float verticalMaximum))
                return 0f;

            float minimum = Math.Max(
                0f,
                Math.Max(radialMinimum, verticalMinimum));
            float maximum = Math.Min(
                1f,
                Math.Min(radialMaximum, verticalMaximum));
            return maximum > minimum
                ? segmentLength * (maximum - minimum)
                : 0f;
        }

        private void HandleTurnEnded(TurnEndRecord _)
        {
            if (canonicalProjectionBound) return;
            AdvanceFields(state => 1f
                / state.Field.Definition.DurationTurnEnds);
        }

        private bool Matches(IReadOnlyList<SmokeFieldSnapshot> snapshots)
        {
            if (snapshots.Count != active.Count) return false;
            foreach (SmokeFieldSnapshot snapshot in snapshots)
                if (!active.TryGetValue(
                        snapshot.Field.Id,
                        out ActiveField current)
                    || current.RemainingFraction
                        != snapshot.RemainingFraction
                    || !string.Equals(
                        GameplayCanonicalValueDigest.Calculate(current.Field),
                        GameplayCanonicalValueDigest.Calculate(snapshot.Field),
                        StringComparison.Ordinal))
                    return false;
            return true;
        }

        private void AdvanceFields(Func<ActiveField, float> getStep)
        {
            expiredIds.Clear();
            foreach (KeyValuePair<string, ActiveField> entry in active)
            {
                entry.Value.RemainingFraction = Math.Max(
                    0f,
                    entry.Value.RemainingFraction - getStep(entry.Value));
                if (entry.Value.RemainingFraction <= 0f)
                    expiredIds.Add(entry.Key);
            }

            foreach (string id in expiredIds)
            {
                SmokeFieldRecord field = active[id].Field;
                active.Remove(id);
                Revision++;
                FieldExpired?.Invoke(field);
            }
            expiredIds.Clear();
        }

        private static bool TryGetRadialInterval(
            float startX,
            float startZ,
            float deltaX,
            float deltaZ,
            float radius,
            out float minimum,
            out float maximum)
        {
            float a = (deltaX * deltaX) + (deltaZ * deltaZ);
            float c = (startX * startX) + (startZ * startZ)
                - (radius * radius);
            if (a <= IntersectionTolerance)
            {
                minimum = 0f;
                maximum = 1f;
                return c <= 0f;
            }

            float b = 2f * ((startX * deltaX) + (startZ * deltaZ));
            float discriminant = (b * b) - (4f * a * c);
            if (discriminant < 0f)
            {
                minimum = 0f;
                maximum = 0f;
                return false;
            }

            float root = (float)Math.Sqrt(discriminant);
            minimum = (-b - root) / (2f * a);
            maximum = (-b + root) / (2f * a);
            return maximum >= 0f && minimum <= 1f;
        }

        private static bool TryGetAxisInterval(
            float start,
            float delta,
            float lower,
            float upper,
            out float minimum,
            out float maximum)
        {
            if (Math.Abs(delta) <= IntersectionTolerance)
            {
                minimum = 0f;
                maximum = 1f;
                return start >= lower && start <= upper;
            }

            float first = (lower - start) / delta;
            float second = (upper - start) / delta;
            minimum = Math.Min(first, second);
            maximum = Math.Max(first, second);
            return maximum >= 0f && minimum <= 1f;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(
                    nameof(GameplaySmokeFieldSession));
        }

        private void RequireLegacyMutationAllowed(string operation)
        {
            if (canonicalProjectionBound)
                throw new InvalidOperationException(
                    $"Legacy smoke mutation '{operation}' is disabled while the semantic runtime owns state.");
        }
    }
}
