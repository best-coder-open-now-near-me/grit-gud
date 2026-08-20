using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public enum TurnReplayActorActionKind
    {
        Attack = 0,
        Equipment = 1,
        Throw = 2,
        Displacement = 3,
        Reaction = 4,
        Pinned = 5,
        GetUp = 6,
        Jump = 7,
        Vault = 8,
        Mantle = 9,
        Push = 10,
    }

    public sealed class TurnReplayActorActionState
    {
        public TurnReplayActorActionState(
            string actorId,
            TurnReplayActorActionKind kind,
            long journalSequence,
            float normalizedProgress,
            bool contactReaction = false,
            int resultingWoundCount = -1,
            TargetRegionId? hitRegion = null)
        {
            ActorId = string.IsNullOrWhiteSpace(actorId)
                ? throw new ArgumentException(
                    "Replay actor-action states require an actor identifier.",
                    nameof(actorId))
                : actorId;
            if (!Enum.IsDefined(typeof(TurnReplayActorActionKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (journalSequence <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(journalSequence));
            if (float.IsNaN(normalizedProgress)
                || float.IsInfinity(normalizedProgress))
                throw new ArgumentOutOfRangeException(nameof(normalizedProgress));
            Kind = kind;
            TransitionSequence = journalSequence;
            NormalizedProgress = Math.Max(
                0f,
                Math.Min(1f, normalizedProgress));
            if (resultingWoundCount < -1)
                throw new ArgumentOutOfRangeException(
                    nameof(resultingWoundCount));
            IsContactReaction = contactReaction;
            ResultingWoundCount = resultingWoundCount;
            HitRegion = hitRegion;
        }

        public string ActorId { get; }
        public TurnReplayActorActionKind Kind { get; }
        public long TransitionSequence { get; }
        public float NormalizedProgress { get; }
        public bool IsContactReaction { get; }
        public int ResultingWoundCount { get; }
        public TargetRegionId? HitRegion { get; }
    }

    /// <summary>
    /// Projects seekable actor animation intent directly from a verified
    /// semantic replay frame. It never reads or interprets the mutable journal.
    /// </summary>
    public static class TurnReplayActorActionProjector
    {
        private sealed class ReactionProjection
        {
            public ReactionProjection(
                TurnReplayActorActionKind kind,
                bool contactReaction = false,
                int resultingWoundCount = -1,
                TargetRegionId? hitRegion = null)
            {
                Kind = kind;
                ContactReaction = contactReaction;
                ResultingWoundCount = resultingWoundCount;
                HitRegion = hitRegion;
            }

            public TurnReplayActorActionKind Kind { get; }
            public bool ContactReaction { get; }
            public int ResultingWoundCount { get; }
            public TargetRegionId? HitRegion { get; }
        }

        public static IReadOnlyList<TurnReplayActorActionState> Project(
            GameplaySemanticReplayFrame frame,
            float normalizedProgress)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (float.IsNaN(normalizedProgress)
                || float.IsInfinity(normalizedProgress))
                throw new ArgumentOutOfRangeException(nameof(normalizedProgress));
            float progress = Math.Max(
                0f,
                Math.Min(1f, normalizedProgress));
            var states = new List<TurnReplayActorActionState>();
            long sequence = frame.Transition.Identity.Sequence;
            switch (frame.SemanticRecord)
            {
                case GameplayActionRecord action:
                    ProjectAction(action, sequence, progress, states);
                    break;
                case MovementRouteRecord movement:
                    ProjectTraversal(movement, sequence, progress, states);
                    break;
                case DroneAttackRecord attack:
                    Add(
                        states,
                        attack.DroneId,
                        TurnReplayActorActionKind.Attack,
                        sequence,
                        progress);
                    if (attack.Consequence is AttackResolutionRecord resolved)
                        ProjectAttackReaction(
                            resolved,
                            sequence,
                            progress,
                            states);
                    break;
                case ActorDroneAttackRecord attack:
                    Add(
                        states,
                        attack.AttackerId,
                        TurnReplayActorActionKind.Attack,
                        sequence,
                        progress);
                    break;
                case GameplayEmergencyReactionTransitionPayload emergency
                    when emergency.Phase == "begin":
                    foreach (string responderId in emergency.Responders)
                        Add(
                            states,
                            responderId,
                            TurnReplayActorActionKind.Reaction,
                            sequence,
                            progress);
                    break;
            }
            return states.Count == 0
                ? Array.Empty<TurnReplayActorActionState>()
                : states.AsReadOnly();
        }

        private static void ProjectTraversal(
            MovementRouteRecord route,
            long sequence,
            float progress,
            ICollection<TurnReplayActorActionState> states)
        {
            float targetSeconds = route.TotalPlaybackDurationSeconds
                * progress;
            float elapsed = 0f;
            foreach (MovementRouteSegmentRecord segment in route.Segments)
            {
                float duration = segment.PlaybackDurationSeconds;
                if (elapsed + duration <= targetSeconds
                    && !ReferenceEquals(segment, route.Segments[
                        route.Segments.Count - 1]))
                {
                    elapsed += duration;
                    continue;
                }
                if (!segment.IsTraversal)
                    return;
                float segmentProgress = duration <= 0f
                    ? 1f
                    : (targetSeconds - elapsed) / duration;
                Add(
                    states,
                    route.ActorId,
                    MapTraversalKind(segment.Kind),
                    sequence,
                    segmentProgress);
                return;
            }
        }

        private static TurnReplayActorActionKind MapTraversalKind(
            MovementRouteSegmentKind kind)
        {
            switch (kind)
            {
                case MovementRouteSegmentKind.Vault:
                    return TurnReplayActorActionKind.Vault;
                case MovementRouteSegmentKind.Mantle:
                    return TurnReplayActorActionKind.Mantle;
                default:
                    return TurnReplayActorActionKind.Jump;
            }
        }

        private static void ProjectAction(
            GameplayActionRecord action,
            long sequence,
            float progress,
            ICollection<TurnReplayActorActionState> states)
        {
            TurnReplayActorActionKind? primary = null;
            var reactions = new Dictionary<
                string,
                ReactionProjection>(StringComparer.Ordinal);
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (outcome is ThrownExplosiveActionOutcome)
                {
                    primary = TurnReplayActorActionKind.Throw;
                }
                else if (outcome is DisplacementActionOutcome displacement)
                {
                    ActorPinTransition pin =
                        displacement.Displacement.PinTransition;
                    primary = pin != null && pin.ReleasesPin
                        ? TurnReplayActorActionKind.GetUp
                        : MapDisplacementKind(displacement.Displacement);
                    if (displacement.Displacement.Succeeded
                        && displacement.Displacement.Request.SubjectKind
                            == DisplacementSubjectKind.Combatant)
                    {
                        reactions[
                            displacement.Displacement.Request.SubjectId] =
                            new ReactionProjection(
                                TurnReplayActorActionKind.Reaction);
                    }
                    if (pin != null && pin.EstablishesPin)
                        reactions[pin.ActorId] = new ReactionProjection(
                            TurnReplayActorActionKind.Pinned);
                }
                else if (outcome is AttackResolvedActionOutcome attack)
                {
                    primary ??= TurnReplayActorActionKind.Attack;
                    AddAttackReaction(reactions, attack.Attack);
                }
                else if (outcome is WeaponDischargedActionOutcome
                    || outcome is ProjectileLaunchedActionOutcome)
                {
                    primary ??= TurnReplayActorActionKind.Attack;
                }
                else if (outcome is EquipmentChangedActionOutcome)
                {
                    primary ??= TurnReplayActorActionKind.Equipment;
                }
            }

            if (primary.HasValue)
                Add(
                    states,
                    action.Request.ActorId,
                    primary.Value,
                    sequence,
                    progress);
            foreach (KeyValuePair<string, ReactionProjection> reaction
                in reactions)
            {
                states.Add(new TurnReplayActorActionState(
                    reaction.Key,
                    reaction.Value.Kind,
                    sequence,
                    progress,
                    reaction.Value.ContactReaction,
                    reaction.Value.ResultingWoundCount,
                    reaction.Value.HitRegion));
            }
        }

        private static void AddAttackReaction(
            IDictionary<string, ReactionProjection> reactions,
            AttackResolutionRecord attack)
        {
            if (attack?.Wound == null) return;
            reactions[attack.TargetId] = new ReactionProjection(
                TurnReplayActorActionKind.Reaction,
                attack.IsContactAttack,
                attack.TargetWoundsAfter.WoundCount,
                attack.HitRegion);
        }

        private static void ProjectAttackReaction(
            AttackResolutionRecord attack,
            long sequence,
            float progress,
            ICollection<TurnReplayActorActionState> states)
        {
            if (attack?.Wound == null) return;
            states.Add(new TurnReplayActorActionState(
                attack.TargetId,
                TurnReplayActorActionKind.Reaction,
                sequence,
                progress,
                attack.IsContactAttack,
                attack.TargetWoundsAfter.WoundCount,
                attack.HitRegion));
        }

        private static TurnReplayActorActionKind MapDisplacementKind(
            DisplacementRecord record)
        {
            if (record.PinTransition?.ReleasesPin == true
                || record.Request.ActionKind == DisplacementActionKind.PushOff)
                return TurnReplayActorActionKind.GetUp;
            switch (record.Request.ActionKind)
            {
                case DisplacementActionKind.Push:
                    return TurnReplayActorActionKind.Push;
                case DisplacementActionKind.Throw:
                    return TurnReplayActorActionKind.Throw;
                default:
                    return TurnReplayActorActionKind.Displacement;
            }
        }

        private static void Add(
            ICollection<TurnReplayActorActionState> states,
            string actorId,
            TurnReplayActorActionKind kind,
            long sequence,
            float progress) => states.Add(new TurnReplayActorActionState(
                actorId,
                kind,
                sequence,
                progress));
    }
}
