using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    [Flags]
    public enum GameplayTacticalAffordance
    {
        None = 0,
        Damage = 1 << 0,
        Destroy = 1 << 1,
        Displace = 1 << 2,
        Interact = 1 << 3,
        UseAsCover = 1 << 4,
        AffectsSight = 1 << 5,
        AffectsRouting = 1 << 6,
        AffectsBlast = 1 << 7,
    }

    public sealed class GameplayTacticalSubject
    {
        public GameplayTacticalSubject(
            GameplaySubjectReference subject,
            GameplayPosition position,
            GameplayTacticalAffordance affordances,
            float remainingIntegrity = 0f)
        {
            GameplayNumericPolicy.RequireFinite(
                remainingIntegrity,
                nameof(remainingIntegrity));
            if (remainingIntegrity < 0f)
                throw new ArgumentOutOfRangeException(nameof(remainingIntegrity));
            Subject = subject;
            Position = position;
            Affordances = affordances;
            RemainingIntegrity = remainingIntegrity;
        }

        public GameplaySubjectReference Subject { get; }
        public GameplayPosition Position { get; }
        public GameplayTacticalAffordance Affordances { get; }
        public float RemainingIntegrity { get; }

        public bool Affords(GameplayTacticalAffordance required) =>
            (Affordances & required) == required;
    }

    public static class GameplayTacticalSubjectCatalog
    {
        public static IReadOnlyList<GameplayTacticalSubject> Discover(
            GameplayCombatStateSnapshot state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var result = new List<GameplayTacticalSubject>();
            foreach (GameplayActorSnapshot actor in state.Session.Actors)
                result.Add(new GameplayTacticalSubject(
                    new GameplaySubjectReference(
                        GameplaySemanticSubjectKind.Actor,
                        actor.ActorId),
                    actor.Pose.Position,
                    GameplayTacticalAffordance.Damage
                        | GameplayTacticalAffordance.Displace));
            foreach (GameplayObjectiveSnapshot objective in
                state.Session.Objectives)
            {
                if (objective.IsCompleted) continue;
                result.Add(new GameplayTacticalSubject(
                    new GameplaySubjectReference(
                        GameplaySemanticSubjectKind.Objective,
                        objective.ObjectiveId),
                    objective.Position,
                    GameplayTacticalAffordance.Interact));
            }
            foreach (DestructiblePropSnapshot prop in state.Destructibles)
            {
                if (prop.State == DestructiblePropState.Destroyed) continue;
                result.Add(new GameplayTacticalSubject(
                    new GameplaySubjectReference(
                        GameplaySemanticSubjectKind.DestructibleProp,
                        prop.PropId),
                    prop.Pose.Position,
                    GameplayTacticalAffordance.Damage
                        | GameplayTacticalAffordance.Destroy
                        | GameplayTacticalAffordance.Displace
                        | GameplayTacticalAffordance.UseAsCover
                        | GameplayTacticalAffordance.AffectsSight
                        | GameplayTacticalAffordance.AffectsRouting
                        | GameplayTacticalAffordance.AffectsBlast,
                    prop.RemainingIntegrity));
            }
            foreach (VehicleMomentumState vehicle in state.Vehicles)
                result.Add(new GameplayTacticalSubject(
                    new GameplaySubjectReference(
                        GameplaySemanticSubjectKind.Vehicle,
                        vehicle.VehicleId),
                    vehicle.Position,
                    GameplayTacticalAffordance.UseAsCover
                        | GameplayTacticalAffordance.AffectsSight
                        | GameplayTacticalAffordance.AffectsRouting
                        | GameplayTacticalAffordance.AffectsBlast));
            foreach (DroneSnapshot drone in state.Drones)
            {
                if (!drone.IsOperational) continue;
                result.Add(new GameplayTacticalSubject(
                    new GameplaySubjectReference(
                        GameplaySemanticSubjectKind.Vehicle,
                        drone.DroneId),
                    drone.Position,
                    GameplayTacticalAffordance.Damage,
                    drone.RemainingIntegrity));
            }
            result.Sort((left, right) =>
            {
                int comparison = left.Subject.Kind.CompareTo(
                    right.Subject.Kind);
                return comparison != 0
                    ? comparison
                    : StringComparer.Ordinal.Compare(
                        left.Subject.Id,
                        right.Subject.Id);
            });
            return result.AsReadOnly();
        }
    }

    public sealed class GameplayTacticalCandidateOptions
    {
        public GameplayTacticalCandidateOptions(
            float maximumSubjectDistance = float.MaxValue)
        {
            GameplayNumericPolicy.RequireFinite(
                maximumSubjectDistance,
                nameof(maximumSubjectDistance));
            if (maximumSubjectDistance <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(maximumSubjectDistance));
            MaximumSubjectDistance = maximumSubjectDistance;
        }

        public float MaximumSubjectDistance { get; }
    }

    public sealed class GameplayTacticalIntent
    {
        public GameplayTacticalIntent(
            GameplayReachableInput input,
            GameplayTacticalSubject subject)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Subject = subject ?? throw new ArgumentNullException(nameof(subject));
        }

        public GameplayReachableInput Input { get; }
        public GameplayTacticalSubject Subject { get; }
    }

    public sealed class GameplayTacticalCandidateBuilder
    {
        private readonly GameplayReachableCandidateBuilder candidates;

        public GameplayTacticalCandidateBuilder(
            GameplayCapabilityRegistry capabilities)
        {
            candidates = new GameplayReachableCandidateBuilder(
                capabilities ?? throw new ArgumentNullException(
                    nameof(capabilities)));
        }

        public IReadOnlyList<GameplayCandidate> Build(
            GameplayCombatStateSnapshot state,
            IEnumerable<GameplayReachableInput> reachableInputs,
            GameplayTacticalCandidateOptions options = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (reachableInputs == null)
                throw new ArgumentNullException(nameof(reachableInputs));
            options ??= new GameplayTacticalCandidateOptions();
            IReadOnlyList<GameplayTacticalSubject> subjects =
                GameplayTacticalSubjectCatalog.Discover(state);
            var result = new List<GameplayCandidate>();
            foreach (GameplayReachableInput input in reachableInputs)
            {
                bool matched = false;
                foreach (GameplayTacticalSubject subject in subjects)
                {
                    if (!Matches(state, input, subject, options)) continue;
                    result.Add(candidates.Build(
                        input,
                        subject.Subject,
                        new GameplayTacticalIntent(input, subject)));
                    matched = true;
                }
                if (!matched && IsSkeletonSubject(input.SubjectKind))
                    result.Add(candidates.Build(input));
            }
            return result.AsReadOnly();
        }

        private static bool Matches(
            GameplayCombatStateSnapshot state,
            GameplayReachableInput input,
            GameplayTacticalSubject subject,
            GameplayTacticalCandidateOptions options)
        {
            if (subject.Subject.Kind != input.SubjectKind) return false;
            if (input.SubjectIdHint != null
                && !string.Equals(
                    input.SubjectIdHint,
                    subject.Subject.Id,
                    StringComparison.Ordinal))
                return false;
            GameplayPosition sourcePosition = input.SourceSubjectId == null
                ? state.Session.GetActor(input.ActorId).Pose.Position
                : FindDrone(state.Drones, input.SourceSubjectId).Position;
            if (sourcePosition.DistanceTo(subject.Position)
                > options.MaximumSubjectDistance)
                return false;
            switch (input.Profile.Capability)
            {
                case GameplaySemanticCapability.Move:
                    if (input.Profile.Equals(
                            GameplayCapabilityProfiles.AerialDroneMove()))
                        return input.SubjectIdHint != null;
                    return string.Equals(
                        input.ActorId,
                        subject.Subject.Id,
                        StringComparison.Ordinal);
                case GameplaySemanticCapability.ChangeStance:
                case GameplaySemanticCapability.EndTurn:
                case GameplaySemanticCapability.EmergencyReaction:
                    return string.Equals(
                        input.ActorId,
                        subject.Subject.Id,
                        StringComparison.Ordinal);
                case GameplaySemanticCapability.DirectAttack:
                case GameplaySemanticCapability.LaunchProjectile:
                    return !string.Equals(
                            input.ActorId,
                            subject.Subject.Id,
                            StringComparison.Ordinal)
                        && (subject.Subject.Kind
                                != GameplaySemanticSubjectKind.DestructibleProp
                            || subject.Affords(
                                GameplayTacticalAffordance.Damage));
                case GameplaySemanticCapability.Displace:
                    return !string.Equals(
                            input.ActorId,
                            subject.Subject.Id,
                            StringComparison.Ordinal)
                        && subject.Affords(GameplayTacticalAffordance.Displace);
                case GameplaySemanticCapability.Interact:
                    return subject.Affords(GameplayTacticalAffordance.Interact);
                default:
                    return true;
            }
        }

        private static bool IsSkeletonSubject(
            GameplaySemanticSubjectKind subjectKind) =>
            subjectKind == GameplaySemanticSubjectKind.WorldPosition
            || subjectKind == GameplaySemanticSubjectKind.InventoryItem
            || subjectKind == GameplaySemanticSubjectKind.Projectile
            || subjectKind == GameplaySemanticSubjectKind.System;

        private static DroneSnapshot FindDrone(
            IReadOnlyList<DroneSnapshot> drones,
            string droneId)
        {
            foreach (DroneSnapshot drone in drones)
                if (string.Equals(drone.DroneId, droneId,
                    StringComparison.Ordinal)) return drone;
            throw new KeyNotFoundException(
                $"Reachable input source drone '{droneId}' is absent from canonical state.");
        }
    }
}
