using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    /// <summary>
    /// Live authoritative owner for persistent-fire snapshots. Evolution is
    /// installed from the same pure transition reductions used by headless
    /// simulation; this class never derives consequences from presentation.
    /// </summary>
    public sealed class GameplayFireFieldSession : IDisposable
    {
        private readonly GameplaySession gameplay;
        private readonly DestructiblePropSession destructibles;
        private readonly Dictionary<string, FireFieldSnapshot> active =
            new Dictionary<string, FireFieldSnapshot>(StringComparer.Ordinal);
        private bool disposed;
        private bool canonicalProjectionBound;

        public GameplayFireFieldSession(
            GameplaySession gameplaySession,
            DestructiblePropSession destructibleSession)
        {
            gameplay = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            destructibles = destructibleSession ?? throw new ArgumentNullException(
                nameof(destructibleSession));
            gameplay.TurnEnded += HandleTurnEnded;
        }

        public long Revision { get; private set; }
        public int ActiveCount => active.Count;

        public event Action<FireFieldSnapshot> FieldDeployed;
        public event Action<FireFieldSnapshot> FieldChanged;
        public event Action<FireFieldRecord> FieldExpired;

        internal void BindCanonicalProjection(
            IReadOnlyList<FireFieldSnapshot> snapshots)
        {
            if (canonicalProjectionBound)
                throw new InvalidOperationException(
                    "Fire fields already have a canonical runtime projection.");
            ValidateCanonicalProjection(snapshots);
            if (!Matches(snapshots))
                throw new InvalidOperationException(
                    "Fire field session does not match the initial canonical state.");
            canonicalProjectionBound = true;
        }

        internal void ValidateCanonicalProjection(
            IReadOnlyList<FireFieldSnapshot> snapshots)
        {
            ThrowIfDisposed();
            if (snapshots == null)
                throw new ArgumentNullException(nameof(snapshots));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (FireFieldSnapshot snapshot in snapshots)
            {
                if (!ids.Add(snapshot.Field.Id))
                    throw new InvalidOperationException(
                        $"Canonical fire field '{snapshot.Field.Id}' is duplicated.");
                if (active.TryGetValue(
                        snapshot.Field.Id,
                        out FireFieldSnapshot current)
                    && !string.Equals(
                        GameplayCanonicalValueDigest.Calculate(current.Field),
                        GameplayCanonicalValueDigest.Calculate(snapshot.Field),
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Canonical fire field '{snapshot.Field.Id}' changed its definition.");
                }
            }
        }

        public IReadOnlyList<FireFieldSnapshot> CaptureActiveFields()
        {
            ThrowIfDisposed();
            var snapshots = new List<FireFieldSnapshot>(active.Values);
            snapshots.Sort((left, right) => string.CompareOrdinal(
                left.Field.Id,
                right.Field.Id));
            return snapshots.AsReadOnly();
        }

        public bool TryGetField(
            string fieldId,
            out FireFieldSnapshot snapshot)
        {
            ThrowIfDisposed();
            return active.TryGetValue(fieldId ?? string.Empty, out snapshot);
        }

        public void Deploy(FireFieldRecord field)
        {
            var notifications = new GameplayNotificationBatch();
            Deploy(field, notifications);
            notifications.Publish();
        }

        internal void Deploy(
            FireFieldRecord field,
            GameplayNotificationBatch notifications)
        {
            RequireLegacyMutationAllowed(nameof(Deploy));
            ThrowIfDisposed();
            if (field == null) throw new ArgumentNullException(nameof(field));
            if (notifications == null)
                throw new ArgumentNullException(nameof(notifications));
            var snapshot = new FireFieldSnapshot(
                field,
                remainingFraction: 1f);
            if (!active.TryAdd(field.Id, snapshot))
                throw new InvalidOperationException(
                    $"Fire field '{field.Id}' is already active.");
            Revision++;
            notifications.Add(FieldDeployed, snapshot);
        }

        public void Install(IReadOnlyList<FireFieldSnapshot> snapshots)
        {
            RequireLegacyMutationAllowed(nameof(Install));
            var notifications = new GameplayNotificationBatch();
            InstallSnapshots(snapshots, notifications);
            notifications.Publish();
        }

        internal void InstallCanonicalProjection(
            IReadOnlyList<FireFieldSnapshot> snapshots,
            GameplayNotificationBatch notifications)
        {
            if (!canonicalProjectionBound)
                throw new InvalidOperationException(
                    "Fire fields are not bound to a canonical runtime.");
            InstallSnapshots(snapshots, notifications);
        }

        private void InstallSnapshots(
            IReadOnlyList<FireFieldSnapshot> snapshots,
            GameplayNotificationBatch notifications)
        {
            ValidateCanonicalProjection(snapshots);
            if (notifications == null)
                throw new ArgumentNullException(nameof(notifications));
            if (Matches(snapshots)) return;
            var next = new Dictionary<string, FireFieldSnapshot>(
                StringComparer.Ordinal);
            foreach (FireFieldSnapshot snapshot in snapshots)
            {
                if (!next.TryAdd(snapshot.Field.Id, snapshot))
                    throw new ArgumentException(
                        $"Duplicate fire field '{snapshot.Field.Id}'.",
                        nameof(snapshots));
            }

            foreach (KeyValuePair<string, FireFieldSnapshot> entry in active)
                if (!next.ContainsKey(entry.Key))
                    notifications.Add(FieldExpired, entry.Value.Field);
            foreach (KeyValuePair<string, FireFieldSnapshot> entry in next)
                if (!active.TryGetValue(entry.Key, out FireFieldSnapshot previous))
                    notifications.Add(FieldDeployed, entry.Value);
                else if (previous.RemainingFraction
                        != entry.Value.RemainingFraction
                    || previous.PulseProgress != entry.Value.PulseProgress)
                    notifications.Add(FieldChanged, entry.Value);

            active.Clear();
            foreach (KeyValuePair<string, FireFieldSnapshot> entry in next)
                active.Add(entry.Key, entry.Value);
            Revision++;
        }

        public void AdvanceContinuousTime(float elapsedSeconds)
        {
            RequireLegacyMutationAllowed(nameof(AdvanceContinuousTime));
            ThrowIfDisposed();
            GameplayNumericPolicy.RequireFinite(
                elapsedSeconds,
                nameof(elapsedSeconds));
            if (elapsedSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (elapsedSeconds <= 0f
                || gameplay.Mode != GameplaySessionMode.Exploration)
                return;
            Advance(
                (state, fire) =>
                    GameplayFireFieldEvolution.AdvanceContinuous(
                        state,
                        fire,
                        elapsedSeconds));
        }

        public float CalculateHazardTraversal(
            GameplayPosition origin,
            GameplayPosition destination)
        {
            ThrowIfDisposed();
            float total = 0f;
            foreach (FireFieldSnapshot fire in active.Values)
                total += CalculateHazardTraversal(
                    origin,
                    destination,
                    fire);
            return total;
        }

        internal static float CalculateHazardTraversal(
            GameplayPosition origin,
            GameplayPosition destination,
            FireFieldSnapshot fire) =>
            GameplaySmokeFieldSession.CalculateTraversalLength(
                origin,
                destination,
                fire.Field.Origin,
                fire.CurrentRadius,
                fire.Field.Definition.Height);

        public void Dispose()
        {
            if (disposed) return;
            gameplay.TurnEnded -= HandleTurnEnded;
            active.Clear();
            FieldDeployed = null;
            FieldChanged = null;
            FieldExpired = null;
            disposed = true;
        }

        private void HandleTurnEnded(TurnEndRecord _)
        {
            if (canonicalProjectionBound) return;
            Advance(GameplayFireFieldEvolution.AdvanceTurnEnd);
        }

        private bool Matches(IReadOnlyList<FireFieldSnapshot> snapshots)
        {
            if (snapshots.Count != active.Count) return false;
            foreach (FireFieldSnapshot snapshot in snapshots)
                if (!active.TryGetValue(
                        snapshot.Field.Id,
                        out FireFieldSnapshot current)
                    || !string.Equals(
                        GameplayCanonicalValueDigest.Calculate(current),
                        GameplayCanonicalValueDigest.Calculate(snapshot),
                        StringComparison.Ordinal))
                    return false;
            return true;
        }

        private void Advance(
            Func<GameplayCombatStateSnapshot, FireFieldSnapshot,
                GameplayFireFieldAdvanceRecord> evolve)
        {
            if (active.Count == 0) return;
            GameplayCombatStateSnapshot state = GameplayCombatStateCapture.Capture(
                gameplay,
                destructibles,
                fireFields: this);
            var ids = new List<string>(active.Keys);
            ids.Sort(StringComparer.Ordinal);
            var advances = new List<GameplayFireFieldAdvanceRecord>(ids.Count);
            foreach (string id in ids)
                advances.Add(evolve(state, active[id]));

            var notifications = new GameplayNotificationBatch();
            long sequence = gameplay.LastTransitionSequence + 1L;
            foreach (GameplayFireFieldAdvanceRecord advance in advances)
            {
                ApplyConsequences(
                    advance.Previous.Field,
                    advance.Pulses,
                    sequence,
                    notifications);
                if (advance.Resulting.HasValue)
                {
                    FireFieldSnapshot resulting = advance.Resulting.Value;
                    active[advance.FieldId] = resulting;
                    notifications.Add(FieldChanged, resulting);
                }
                else
                {
                    active.Remove(advance.FieldId);
                    notifications.Add(
                        FieldExpired,
                        advance.Previous.Field);
                }
            }
            Revision++;
            notifications.Publish();
        }

        private void ApplyConsequences(
            FireFieldRecord field,
            IEnumerable<FireFieldPulseRecord> pulses,
            long sequence,
            GameplayNotificationBatch notifications)
        {
            FireFieldDefinition definition = field.Definition;
            int pulseIndex = 0;
            foreach (FireFieldPulseRecord pulse in pulses)
            {
                foreach (FireFieldEffectRecord effect in pulse.Effects)
                    switch (effect.SubjectKind)
                    {
                        case FireFieldSubjectKind.Actor:
                            if (definition.ActorWoundMovementPenalty > 0f)
                                gameplay.ApplyEnvironmentalInjury(
                                    effect.EntityId,
                                    TargetRegionId.Torso,
                                    definition.ActorWoundMovementPenalty,
                                    field.SourceActorId,
                                    field.SourceItemId,
                                    sequence,
                                    "fire-impact:" + sequence + ":" + field.Id
                                        + ":" + pulseIndex + ":"
                                        + effect.EntityId,
                                    notifications);
                            break;
                        case FireFieldSubjectKind.DestructibleProp:
                            if (definition.DestructibleIntegrityDamage > 0f)
                                destructibles.TryApplyDamage(
                                    effect.EntityId,
                                    definition.DestructibleIntegrityDamage,
                                    out _,
                                    notifications);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                pulseIndex++;
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(
                    nameof(GameplayFireFieldSession));
        }

        private void RequireLegacyMutationAllowed(string operation)
        {
            if (canonicalProjectionBound)
                throw new InvalidOperationException(
                    $"Legacy fire mutation '{operation}' is disabled while the semantic runtime owns state.");
        }
    }
}
