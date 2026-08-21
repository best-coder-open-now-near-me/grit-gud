using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public enum VehiclePathFailure
    {
        None,
        MissingDestination,
        OriginMismatch,
        StationarySegment,
        TooShortForBraking,
        TooLongForAcceleration,
        OutsideForwardEnvelope,
        CurvatureTooTight,
    }

    public sealed class VehicleMomentumSession
    {
        private const float DistanceTolerance = 0.0001f;

        private readonly List<VehicleMomentumRecord> records =
            new List<VehicleMomentumRecord>();
        private readonly IReadOnlyList<VehicleMomentumRecord> readOnlyRecords;
        private bool canonicalProjectionBound;
        private Func<VehicleMomentumRecord, GameplayReductionResult>
            canonicalExecutor;
        private Func<long> canonicalSequence;

        public VehicleMomentumSession(
            VehicleMomentumProfile profile,
            VehicleMomentumState initialState,
            GameplayJournal journal = null)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (initialState.Speed > profile.MaximumSpeed)
            {
                throw new ArgumentOutOfRangeException(nameof(initialState));
            }

            State = initialState;
            Journal = journal ?? new GameplayJournal();
            readOnlyRecords = records.AsReadOnly();
        }

        public VehicleMomentumProfile Profile { get; }

        public VehicleMomentumState State { get; private set; }

        public GameplayJournal Journal { get; }

        public IReadOnlyList<VehicleMomentumRecord> Records => readOnlyRecords;

        internal void BindCanonicalExecutor(
            Func<VehicleMomentumRecord, GameplayReductionResult> executor,
            Func<long> nextSequence)
        {
            if (executor == null) throw new ArgumentNullException(nameof(executor));
            if (nextSequence == null)
                throw new ArgumentNullException(nameof(nextSequence));
            if (canonicalExecutor != null || canonicalProjectionBound)
                throw new InvalidOperationException(
                    "Vehicle semantic executor is already bound or projection binding has started.");
            canonicalExecutor = executor;
            canonicalSequence = nextSequence;
        }

        internal void BindCanonicalProjection(VehicleMomentumState snapshot)
        {
            if (canonicalProjectionBound)
                throw new InvalidOperationException(
                    "Vehicle already has a canonical runtime projection.");
            if (!StatesMatch(State, snapshot))
                throw new InvalidOperationException(
                    "Vehicle session does not match the initial canonical state.");
            canonicalProjectionBound = true;
        }

        internal void ValidateCanonicalProjection(
            VehicleMomentumState snapshot)
        {
            if (!string.Equals(
                    State.VehicleId,
                    snapshot.VehicleId,
                    StringComparison.Ordinal)
                || snapshot.Speed > Profile.MaximumSpeed)
                throw new InvalidOperationException(
                    "Canonical vehicle projection changed identity or exceeded its authored speed.");
        }

        internal void ValidateCanonicalProjection(
            VehicleMomentumState snapshot,
            object semanticRecord)
        {
            ValidateCanonicalProjection(snapshot);
            if (StatesMatch(State, snapshot)) return;
            if (!(semanticRecord is VehicleMomentumRecord movement)
                || !StatesMatch(movement.Previous, State)
                || !StatesMatch(movement.Resulting, snapshot))
                throw new InvalidOperationException(
                    "A changed canonical vehicle requires its exact movement record.");
        }

        internal void InstallCanonicalProjection(
            VehicleMomentumState snapshot,
            object semanticRecord)
        {
            if (!canonicalProjectionBound)
                throw new InvalidOperationException(
                    "Vehicle is not bound to a canonical runtime.");
            ValidateCanonicalProjection(snapshot, semanticRecord);
            if (StatesMatch(State, snapshot)) return;
            var movement = (VehicleMomentumRecord)semanticRecord;
            State = snapshot;
            records.Add(movement);
        }

        public VehicleMovementEnvelope CreateEnvelope()
        {
            float minimumEndSpeed = Math.Max(
                0f,
                State.Speed - Profile.BrakingPerTurn);
            float maximumEndSpeed = Math.Min(
                Profile.MaximumSpeed,
                State.Speed + Profile.AccelerationPerTurn);
            float minimumDistance = (State.Speed + minimumEndSpeed) * 0.5f;
            float maximumDistance = (State.Speed + maximumEndSpeed) * 0.5f;
            float speedFraction = State.Speed / Profile.MaximumSpeed;
            float turnDegrees = Profile.LowSpeedTurnDegrees
                + ((Profile.HighSpeedTurnDegrees - Profile.LowSpeedTurnDegrees)
                    * speedFraction);
            return new VehicleMovementEnvelope(
                State,
                minimumDistance,
                maximumDistance,
                turnDegrees);
        }

        public bool TryResolvePath(
            IEnumerable<GameplayPosition> requestedPath,
            out VehicleMomentumRecord record,
            out VehiclePathFailure failure) => TryResolvePath(
                requestedPath,
                canonicalProjectionBound
                    ? canonicalSequence()
                    : records.Count + 1L,
                out record,
                out failure);

        public bool TryResolvePath(
            IEnumerable<GameplayPosition> requestedPath,
            long transitionSequence,
            out VehicleMomentumRecord record,
            out VehiclePathFailure failure)
        {
            if (requestedPath == null)
            {
                throw new ArgumentNullException(nameof(requestedPath));
            }
            if (transitionSequence <= 0L)
                throw new ArgumentOutOfRangeException(
                    nameof(transitionSequence));

            var path = new List<GameplayPosition>(requestedPath);
            if (path.Count < 2)
            {
                record = null;
                failure = VehiclePathFailure.MissingDestination;
                return false;
            }

            if (path[0].DistanceTo(State.Position) > DistanceTolerance)
            {
                record = null;
                failure = VehiclePathFailure.OriginMismatch;
                return false;
            }

            var segmentLengths = new List<float>(path.Count - 1);
            var headings = new List<float>(path.Count - 1);
            float totalDistance = 0f;
            for (int index = 1; index < path.Count; index++)
            {
                float length = path[index - 1].DistanceTo(path[index]);
                if (length <= DistanceTolerance)
                {
                    record = null;
                    failure = VehiclePathFailure.StationarySegment;
                    return false;
                }

                segmentLengths.Add(length);
                headings.Add(GetHeading(path[index - 1], path[index]));
                totalDistance += length;
            }

            VehicleMovementEnvelope envelope = CreateEnvelope();
            if (totalDistance + DistanceTolerance < envelope.MinimumDistance)
            {
                record = null;
                failure = VehiclePathFailure.TooShortForBraking;
                return false;
            }

            if (totalDistance - DistanceTolerance > envelope.MaximumDistance)
            {
                record = null;
                failure = VehiclePathFailure.TooLongForAcceleration;
                return false;
            }

            float initialTurn = DeltaDegrees(
                State.ForwardDegrees,
                headings[0]);
            if (Math.Abs(initialTurn) > envelope.MaximumTurnDegrees)
            {
                record = null;
                failure = VehiclePathFailure.OutsideForwardEnvelope;
                return false;
            }

            if (!CurvatureIsValid(
                    headings,
                    segmentLengths,
                    totalDistance,
                    out failure))
            {
                record = null;
                return false;
            }

            float minimumEnd = Math.Max(0f, State.Speed - Profile.BrakingPerTurn);
            float maximumEnd = Math.Min(
                Profile.MaximumSpeed,
                State.Speed + Profile.AccelerationPerTurn);
            float finalSpeed = Clamp(
                (totalDistance * 2f) - State.Speed,
                minimumEnd,
                maximumEnd);
            var resulting = new VehicleMomentumState(
                State.VehicleId,
                path[path.Count - 1],
                headings[headings.Count - 1],
                finalSpeed);
            record = new VehicleMomentumRecord(
                transitionSequence,
                State,
                resulting,
                path);
            Commit(record);
            failure = VehiclePathFailure.None;
            return true;
        }

        public void Commit(VehicleMomentumRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }
            if (canonicalProjectionBound)
            {
                canonicalExecutor(record);
                return;
            }
            RequireLegacyMutationAllowed(nameof(Commit));

            if (records.Count > 0
                && record.Sequence <= records[records.Count - 1].Sequence)
            {
                throw new InvalidOperationException(
                    "Vehicle movement must advance its canonical transition sequence.");
            }

            if (!StatesMatch(State, record.Previous))
            {
                throw new InvalidOperationException(
                    "The vehicle record no longer starts at authoritative momentum state.");
            }

            State = record.Resulting;
            records.Add(record);
            Journal.RecordVehicleMomentumResolved(record);
        }

        private bool CurvatureIsValid(
            IReadOnlyList<float> headings,
            IReadOnlyList<float> segmentLengths,
            float totalDistance,
            out VehiclePathFailure failure)
        {
            float traveled = 0f;
            float previousHeading = State.ForwardDegrees;
            for (int index = 0; index < headings.Count; index++)
            {
                float turnDegrees = Math.Abs(DeltaDegrees(
                    previousHeading,
                    headings[index]));
                if (turnDegrees > 0.001f)
                {
                    float turnRadians = turnDegrees * ((float)Math.PI / 180f);
                    float availableLength = index == 0
                        ? segmentLengths[index]
                        : Math.Min(segmentLengths[index - 1], segmentLengths[index]);
                    float impliedRadius = availableLength
                        / (2f * (float)Math.Sin(turnRadians * 0.5f));
                    float progress = totalDistance > 0f
                        ? traveled / totalDistance
                        : 0f;
                    float estimatedSpeed = State.Speed
                        + (((totalDistance * 2f) - (State.Speed * 2f)) * progress);
                    float minimumRadius = Profile.GetMinimumTurningRadius(
                        Math.Max(0f, estimatedSpeed));
                    if (impliedRadius + DistanceTolerance < minimumRadius)
                    {
                        failure = VehiclePathFailure.CurvatureTooTight;
                        return false;
                    }
                }

                traveled += segmentLengths[index];
                previousHeading = headings[index];
            }

            failure = VehiclePathFailure.None;
            return true;
        }

        private static float GetHeading(
            GameplayPosition from,
            GameplayPosition to)
        {
            double radians = Math.Atan2(to.X - from.X, to.Z - from.Z);
            float degrees = (float)(radians * (180d / Math.PI));
            return degrees < 0f ? degrees + 360f : degrees;
        }

        private static float DeltaDegrees(float from, float to)
        {
            float delta = (to - from) % 360f;
            if (delta > 180f)
            {
                delta -= 360f;
            }
            else if (delta < -180f)
            {
                delta += 360f;
            }

            return delta;
        }

        private static float Clamp(float value, float minimum, float maximum) =>
            Math.Max(minimum, Math.Min(maximum, value));

        private static bool StatesMatch(
            VehicleMomentumState left,
            VehicleMomentumState right) =>
            string.Equals(left.VehicleId, right.VehicleId, StringComparison.Ordinal)
            && left.Position.DistanceTo(right.Position) <= DistanceTolerance
            && Math.Abs(left.ForwardDegrees - right.ForwardDegrees) <= DistanceTolerance
            && Math.Abs(left.Speed - right.Speed) <= DistanceTolerance;

        private void RequireLegacyMutationAllowed(string operation)
        {
            if (canonicalProjectionBound)
                throw new InvalidOperationException(
                    $"Legacy vehicle mutation '{operation}' is disabled while the semantic runtime owns state.");
        }
    }
}
