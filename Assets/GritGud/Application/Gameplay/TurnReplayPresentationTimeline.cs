using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public enum ReplayCombatPresentationEventKind
    {
        WeaponDischarge = 0,
        ProjectileLaunch = 1,
        ProjectileImpact = 2,
        Reaction = 3,
        Incapacitation = 4,
    }

    public enum ReplayCombatPresentationSubjectKind
    {
        Actor = 0,
        Drone = 1,
        Destructible = 2,
        World = 3,
    }

    public sealed class ReplayCombatPresentationEvent
    {
        public ReplayCombatPresentationEvent(
            long transitionSequence,
            ReplayCombatPresentationEventKind kind,
            string actorId,
            string targetId,
            GameplayPosition origin,
            GameplayPosition destination,
            float normalizedTime,
            string projectileId = null,
            ReplayCombatPresentationSubjectKind shooterKind =
                ReplayCombatPresentationSubjectKind.Actor,
            ReplayCombatPresentationSubjectKind targetKind =
                ReplayCombatPresentationSubjectKind.Actor,
            string presentationId = null)
        {
            if (transitionSequence <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(transitionSequence));
            if (!Enum.IsDefined(
                    typeof(ReplayCombatPresentationEventKind),
                    kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Enum.IsDefined(
                    typeof(ReplayCombatPresentationSubjectKind),
                    shooterKind))
                throw new ArgumentOutOfRangeException(nameof(shooterKind));
            if (!Enum.IsDefined(
                    typeof(ReplayCombatPresentationSubjectKind),
                    targetKind))
                throw new ArgumentOutOfRangeException(nameof(targetKind));
            if (string.IsNullOrWhiteSpace(actorId)
                && kind != ReplayCombatPresentationEventKind.ProjectileImpact)
                throw new ArgumentException(
                    "Replay combat events require an actor identifier.",
                    nameof(actorId));
            if (float.IsNaN(normalizedTime)
                || float.IsInfinity(normalizedTime)
                || normalizedTime < 0f
                || normalizedTime > 1f)
                throw new ArgumentOutOfRangeException(nameof(normalizedTime));
            if ((kind == ReplayCombatPresentationEventKind.ProjectileLaunch
                    || kind == ReplayCombatPresentationEventKind.ProjectileImpact)
                && string.IsNullOrWhiteSpace(projectileId))
                throw new ArgumentException(
                    "Projectile replay events require a projectile identifier.",
                    nameof(projectileId));

            TransitionSequence = transitionSequence;
            Kind = kind;
            ActorId = actorId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            Origin = origin;
            Destination = destination;
            NormalizedTime = normalizedTime;
            ProjectileId = projectileId ?? string.Empty;
            ShooterKind = shooterKind;
            TargetKind = targetKind;
            PresentationId = presentationId ?? string.Empty;
        }

        public long TransitionSequence { get; }
        public ReplayCombatPresentationEventKind Kind { get; }
        public string ActorId { get; }
        public string TargetId { get; }
        public GameplayPosition Origin { get; }
        public GameplayPosition Destination { get; }
        public float NormalizedTime { get; }
        public string ProjectileId { get; }
        public ReplayCombatPresentationSubjectKind ShooterKind { get; }
        public string ShooterId => ActorId;
        public ReplayCombatPresentationSubjectKind TargetKind { get; }
        public string PresentationId { get; }

        public string StableKey => TransitionSequence + ":"
            + Kind + ":" + ActorId + ":" + ProjectileId;
    }

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
            TargetRegionId? hitRegion = null,
            float eventNormalizedTime = -1f,
            GameplayPosition? origin = null,
            GameplayPosition? destination = null,
            string projectileId = null,
            bool contactAttack = false)
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
            float resolvedEventTime = eventNormalizedTime < 0f
                ? kind == TurnReplayActorActionKind.Reaction
                    ? contactReaction
                        ? GameplaySemanticReplayPresentationTiming
                            .ContactResolutionProgress
                        : GameplaySemanticReplayPresentationTiming
                            .ActionResolutionProgress
                    : 0f
                : eventNormalizedTime;
            if (float.IsNaN(resolvedEventTime)
                || float.IsInfinity(resolvedEventTime)
                || resolvedEventTime < 0f
                || resolvedEventTime > 1f)
                throw new ArgumentOutOfRangeException(
                    nameof(eventNormalizedTime));
            EventNormalizedTime = resolvedEventTime;
            Origin = origin;
            Destination = destination;
            ProjectileId = projectileId ?? string.Empty;
            IsContactAttack = contactAttack;
        }

        public string ActorId { get; }
        public TurnReplayActorActionKind Kind { get; }
        public long TransitionSequence { get; }
        public float NormalizedProgress { get; }
        public bool IsContactReaction { get; }
        public int ResultingWoundCount { get; }
        public TargetRegionId? HitRegion { get; }
        public float EventNormalizedTime { get; }
        public GameplayPosition? Origin { get; }
        public GameplayPosition? Destination { get; }
        public string ProjectileId { get; }
        public bool IsContactAttack { get; }
    }

    public static class ReplayCombatPresentationEventProjector
    {
        public static IReadOnlyList<ReplayCombatPresentationEvent> Project(
            GameplaySemanticReplayFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            var events = new List<ReplayCombatPresentationEvent>(Project(
                frame.Transition.Identity.Sequence,
                frame.SemanticRecord));
            AppendSpecialSubjectDischarges(frame, events);
            if (frame.SemanticRecord is GameplayActionRecord action
                && !ContainsWeaponEvent(events))
            {
                foreach (GameplayActionOutcome outcome in action.Outcomes)
                {
                    if (!(outcome is AttackResolvedActionOutcome resolved)
                        || resolved.Attack.IsContactAttack)
                        continue;
                    GameplayActorSnapshot attacker = frame.Previous.Session
                        .GetActor(resolved.Attack.AttackerId);
                    GameplayActorSnapshot target = frame.Previous.Session
                        .GetActor(resolved.Attack.TargetId);
                    events.Add(new ReplayCombatPresentationEvent(
                        frame.Transition.Identity.Sequence,
                        ReplayCombatPresentationEventKind.WeaponDischarge,
                        resolved.Attack.AttackerId,
                        resolved.Attack.TargetId,
                        AddHeight(attacker.Pose.Position, 1f),
                        AddHeight(target.Pose.Position, 1f),
                        GameplaySemanticReplayPresentationTiming
                            .ActionResolutionProgress));
                    break;
                }
            }
            AppendReactionEvents(frame, events);
            for (int index = 0; index < events.Count; index++)
                events[index] = CompleteEventIdentity(frame, events[index]);
            return events.Count == 0
                ? Array.Empty<ReplayCombatPresentationEvent>()
                : events.AsReadOnly();
        }

        private static void AppendSpecialSubjectDischarges(
            GameplaySemanticReplayFrame frame,
            ICollection<ReplayCombatPresentationEvent> events)
        {
            long sequence = frame.Transition.Identity.Sequence;
            switch (frame.SemanticRecord)
            {
                case ActorDroneAttackRecord attack:
                {
                    GameplayActorSnapshot attacker = frame.Previous.Session
                        .GetActor(attack.AttackerId);
                    DroneSnapshot target = FindDrone(
                        frame.Previous.Drones,
                        attack.DroneId);
                    events.Add(new ReplayCombatPresentationEvent(
                        sequence,
                        ReplayCombatPresentationEventKind.WeaponDischarge,
                        attack.AttackerId,
                        attack.DroneId,
                        AddHeight(attacker.Pose.Position, 1f),
                        target.Position,
                        GameplaySemanticReplayPresentationTiming
                            .ActionResolutionProgress,
                        shooterKind:
                            ReplayCombatPresentationSubjectKind.Actor,
                        targetKind:
                            ReplayCombatPresentationSubjectKind.Drone));
                    break;
                }
                case DroneAttackRecord attack:
                {
                    DroneSnapshot shooter = FindDrone(
                        frame.Previous.Drones,
                        attack.DroneId);
                    ReplayCombatPresentationSubjectKind targetKind =
                        ResolveSubjectKind(frame, attack.TargetId);
                    events.Add(new ReplayCombatPresentationEvent(
                        sequence,
                        ReplayCombatPresentationEventKind.WeaponDischarge,
                        attack.DroneId,
                        attack.TargetId,
                        shooter.Position,
                        ResolveSubjectPosition(
                            frame,
                            attack.TargetId,
                            targetKind),
                        GameplaySemanticReplayPresentationTiming
                            .ActionResolutionProgress,
                        shooterKind:
                            ReplayCombatPresentationSubjectKind.Drone,
                        targetKind: targetKind,
                        presentationId:
                            shooter.Definition.Attack.ActionId));
                    break;
                }
            }
        }

        private static ReplayCombatPresentationEvent CompleteEventIdentity(
            GameplaySemanticReplayFrame frame,
            ReplayCombatPresentationEvent presentationEvent)
        {
            ReplayCombatPresentationSubjectKind shooterKind =
                ResolveSubjectKind(frame, presentationEvent.ShooterId);
            ReplayCombatPresentationSubjectKind targetKind =
                ResolveSubjectKind(frame, presentationEvent.TargetId);
            string presentationId = presentationEvent.PresentationId;
            if ((presentationEvent.Kind ==
                    ReplayCombatPresentationEventKind.WeaponDischarge
                    || presentationEvent.Kind ==
                    ReplayCombatPresentationEventKind.ProjectileLaunch)
                && shooterKind == ReplayCombatPresentationSubjectKind.Actor)
            {
                presentationId = frame.Previous.Session.GetActor(
                    presentationEvent.ShooterId).EquippedItemId;
            }
            return new ReplayCombatPresentationEvent(
                presentationEvent.TransitionSequence,
                presentationEvent.Kind,
                presentationEvent.ShooterId,
                presentationEvent.TargetId,
                presentationEvent.Origin,
                presentationEvent.Destination,
                presentationEvent.NormalizedTime,
                presentationEvent.ProjectileId,
                shooterKind,
                targetKind,
                presentationId);
        }

        private static ReplayCombatPresentationSubjectKind ResolveSubjectKind(
            GameplaySemanticReplayFrame frame,
            string subjectId)
        {
            if (string.IsNullOrWhiteSpace(subjectId)
                || string.Equals(
                    subjectId,
                    GameplayTargetIds.WorldAimPoint,
                    StringComparison.Ordinal))
                return ReplayCombatPresentationSubjectKind.World;
            foreach (GameplayActorSnapshot actor in frame.Previous.Session.Actors)
                if (string.Equals(actor.ActorId, subjectId, StringComparison.Ordinal))
                    return ReplayCombatPresentationSubjectKind.Actor;
            foreach (DroneSnapshot drone in frame.Previous.Drones)
                if (string.Equals(drone.DroneId, subjectId, StringComparison.Ordinal))
                    return ReplayCombatPresentationSubjectKind.Drone;
            foreach (DestructiblePropSnapshot prop in frame.Previous.Destructibles)
                if (string.Equals(prop.PropId, subjectId, StringComparison.Ordinal))
                    return ReplayCombatPresentationSubjectKind.Destructible;
            return ReplayCombatPresentationSubjectKind.World;
        }

        private static GameplayPosition ResolveSubjectPosition(
            GameplaySemanticReplayFrame frame,
            string subjectId,
            ReplayCombatPresentationSubjectKind subjectKind)
        {
            switch (subjectKind)
            {
                case ReplayCombatPresentationSubjectKind.Actor:
                    return AddHeight(
                        frame.Previous.Session.GetActor(subjectId).Pose.Position,
                        1f);
                case ReplayCombatPresentationSubjectKind.Drone:
                    return FindDrone(frame.Previous.Drones, subjectId).Position;
                case ReplayCombatPresentationSubjectKind.Destructible:
                    foreach (DestructiblePropSnapshot prop in
                        frame.Previous.Destructibles)
                        if (string.Equals(
                                prop.PropId,
                                subjectId,
                                StringComparison.Ordinal))
                            return prop.Pose.Position;
                    break;
            }
            return new GameplayPosition(0f, 0f, 0f);
        }

        private static DroneSnapshot FindDrone(
            IEnumerable<DroneSnapshot> drones,
            string droneId)
        {
            foreach (DroneSnapshot drone in drones)
                if (string.Equals(
                        drone.DroneId,
                        droneId,
                        StringComparison.Ordinal))
                    return drone;
            throw new KeyNotFoundException(
                $"Replay drone '{droneId}' is absent from the previous state.");
        }

        public static IReadOnlyList<ReplayCombatPresentationEvent> Project(
            long transitionSequence,
            object semanticRecord)
        {
            if (transitionSequence <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(transitionSequence));
            if (semanticRecord == null)
                throw new ArgumentNullException(nameof(semanticRecord));
            long sequence = transitionSequence;
            var events = new List<ReplayCombatPresentationEvent>();
            if (semanticRecord is GameplayActionRecord action)
            {
                foreach (GameplayActionOutcome outcome in action.Outcomes)
                {
                    if (outcome is WeaponDischargedActionOutcome discharged)
                    {
                        WeaponDischargeRecord value = discharged.Discharge;
                        events.Add(new ReplayCombatPresentationEvent(
                            sequence,
                            ReplayCombatPresentationEventKind.WeaponDischarge,
                            value.AttackerId,
                            value.TargetId,
                            value.Origin,
                            value.AimPoint,
                            GameplaySemanticReplayPresentationTiming
                                .ActionResolutionProgress));
                    }
                    else if (outcome is ProjectileLaunchedActionOutcome launched)
                    {
                        ProjectileLaunchRecord value = launched.Launch;
                        events.Add(new ReplayCombatPresentationEvent(
                            sequence,
                            ReplayCombatPresentationEventKind.ProjectileLaunch,
                            value.AttackerId,
                            value.IntendedTargetId,
                            value.Origin,
                            value.AimPoint,
                            GameplaySemanticReplayPresentationTiming
                                .ActionResolutionProgress,
                            value.ProjectileId));
                    }
                }
            }
            else if (semanticRecord is ProjectileAdvanceRecord advance
                && advance.Resulting.Impact != null)
            {
                ProjectileImpactRecord impact = advance.Resulting.Impact;
                events.Add(new ReplayCombatPresentationEvent(
                    sequence,
                    ReplayCombatPresentationEventKind.ProjectileImpact,
                    advance.Resulting.Launch.AttackerId,
                    impact.HitEntityId,
                    advance.SegmentStart,
                    impact.Position,
                    GameplaySemanticReplayPresentationTiming
                        .GetProjectileImpactProgress(advance),
                    advance.ProjectileId));
            }

            return events.Count == 0
                ? Array.Empty<ReplayCombatPresentationEvent>()
                : events.AsReadOnly();
        }

        private static bool ContainsWeaponEvent(
            IReadOnlyList<ReplayCombatPresentationEvent> events)
        {
            foreach (ReplayCombatPresentationEvent presentationEvent in events)
                if (presentationEvent.Kind ==
                        ReplayCombatPresentationEventKind.WeaponDischarge
                    || presentationEvent.Kind ==
                        ReplayCombatPresentationEventKind.ProjectileLaunch)
                    return true;
            return false;
        }

        private static void AppendReactionEvents(
            GameplaySemanticReplayFrame frame,
            ICollection<ReplayCombatPresentationEvent> events)
        {
            foreach (TurnReplayActorActionState state in
                TurnReplayActorActionProjector.Project(frame, 1f))
            {
                if (state.Kind != TurnReplayActorActionKind.Reaction
                    || state.ResultingWoundCount < 0)
                    continue;
                GameplayActorSnapshot previous = frame.Previous.Session
                    .GetActor(state.ActorId);
                GameplayActorSnapshot resulting = frame.Resulting.Session
                    .GetActor(state.ActorId);
                GameplayPosition position = AddHeight(
                    resulting.Pose.Position,
                    1f);
                events.Add(new ReplayCombatPresentationEvent(
                    frame.Transition.Identity.Sequence,
                    ReplayCombatPresentationEventKind.Reaction,
                    state.ActorId,
                    state.ActorId,
                    position,
                    position,
                    state.EventNormalizedTime));
                if (!previous.IsIncapacitated && resulting.IsIncapacitated)
                    events.Add(new ReplayCombatPresentationEvent(
                        frame.Transition.Identity.Sequence,
                        ReplayCombatPresentationEventKind.Incapacitation,
                        state.ActorId,
                        state.ActorId,
                        position,
                        position,
                        state.EventNormalizedTime));
            }
        }

        private static GameplayPosition AddHeight(
            GameplayPosition position,
            float height) => new GameplayPosition(
                position.X,
                position.Y + height,
                position.Z);
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
                TargetRegionId? hitRegion = null,
                float eventNormalizedTime = -1f)
            {
                Kind = kind;
                ContactReaction = contactReaction;
                ResultingWoundCount = resultingWoundCount;
                HitRegion = hitRegion;
                EventNormalizedTime = eventNormalizedTime;
            }

            public TurnReplayActorActionKind Kind { get; }
            public bool ContactReaction { get; }
            public int ResultingWoundCount { get; }
            public TargetRegionId? HitRegion { get; }
            public float EventNormalizedTime { get; }
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
                    ProjectAction(
                        frame,
                        action,
                        sequence,
                        progress,
                        states);
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
                {
                    GameplayActorSnapshot attacker = frame.Previous.Session
                        .GetActor(attack.AttackerId);
                    DroneSnapshot target = FindDrone(
                        frame.Previous.Drones,
                        attack.DroneId);
                    states.Add(new TurnReplayActorActionState(
                        attack.AttackerId,
                        TurnReplayActorActionKind.Attack,
                        sequence,
                        progress,
                        eventNormalizedTime:
                            GameplaySemanticReplayPresentationTiming
                                .ActionResolutionProgress,
                        origin: AddHeight(attacker.Pose.Position, 1f),
                        destination: target.Position));
                    break;
                }
                case ProjectileAdvanceRecord projectile:
                    ProjectProjectileImpactReactions(
                        frame,
                        projectile,
                        sequence,
                        progress,
                        states);
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

        private static DroneSnapshot FindDrone(
            IEnumerable<DroneSnapshot> drones,
            string droneId)
        {
            foreach (DroneSnapshot drone in drones)
                if (string.Equals(
                        drone.DroneId,
                        droneId,
                        StringComparison.Ordinal))
                    return drone;
            throw new KeyNotFoundException(
                $"Replay drone '{droneId}' is absent from the previous state.");
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
            GameplaySemanticReplayFrame frame,
            GameplayActionRecord action,
            long sequence,
            float progress,
            ICollection<TurnReplayActorActionState> states)
        {
            TurnReplayActorActionKind? primary = null;
            float primaryEventTime = 0f;
            GameplayPosition? primaryOrigin = null;
            GameplayPosition? primaryDestination = null;
            string primaryProjectileId = null;
            bool primaryContactAttack = false;
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
                    primaryContactAttack = attack.Attack.IsContactAttack;
                    if (!primaryContactAttack)
                    {
                        GameplayActorSnapshot attacker = frame.Previous.Session
                            .GetActor(attack.Attack.AttackerId);
                        GameplayActorSnapshot target = frame.Previous.Session
                            .GetActor(attack.Attack.TargetId);
                        primaryEventTime =
                            GameplaySemanticReplayPresentationTiming
                                .ActionResolutionProgress;
                        primaryOrigin = AddHeight(
                            attacker.Pose.Position,
                            1f);
                        primaryDestination = AddHeight(
                            target.Pose.Position,
                            1f);
                    }
                    AddAttackReaction(reactions, attack.Attack);
                }
                else if (outcome is WeaponDischargedActionOutcome discharged)
                {
                    primary ??= TurnReplayActorActionKind.Attack;
                    primaryEventTime = GameplaySemanticReplayPresentationTiming
                        .ActionResolutionProgress;
                    primaryOrigin = discharged.Discharge.Origin;
                    primaryDestination = discharged.Discharge.AimPoint;
                }
                else if (outcome is ProjectileLaunchedActionOutcome launched)
                {
                    primary ??= TurnReplayActorActionKind.Attack;
                    primaryEventTime = GameplaySemanticReplayPresentationTiming
                        .ActionResolutionProgress;
                    primaryOrigin = launched.Launch.Origin;
                    primaryDestination = launched.Launch.AimPoint;
                    primaryProjectileId = launched.Launch.ProjectileId;
                }
                else if (outcome is EquipmentChangedActionOutcome)
                {
                    primary ??= TurnReplayActorActionKind.Equipment;
                }
            }

            if (primary.HasValue)
            {
                if (primary.Value == TurnReplayActorActionKind.Attack
                    && !primaryContactAttack
                    && !primaryDestination.HasValue)
                {
                    GameplayActorSnapshot attacker = frame.Previous.Session
                        .GetActor(action.Request.ActorId);
                    GameplayActorSnapshot target = frame.Previous.Session
                        .GetActor(action.Request.TargetId);
                    primaryEventTime = GameplaySemanticReplayPresentationTiming
                        .ActionResolutionProgress;
                    primaryOrigin = AddHeight(attacker.Pose.Position, 1f);
                    primaryDestination = AddHeight(target.Pose.Position, 1f);
                }
                states.Add(new TurnReplayActorActionState(
                    action.Request.ActorId,
                    primary.Value,
                    sequence,
                    progress,
                    eventNormalizedTime: primaryEventTime,
                    origin: primaryOrigin,
                    destination: primaryDestination,
                    projectileId: primaryProjectileId,
                    contactAttack: primaryContactAttack));
            }
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
                    reaction.Value.HitRegion,
                    reaction.Value.EventNormalizedTime));
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
                attack.HitRegion,
                attack.IsContactAttack
                    ? GameplaySemanticReplayPresentationTiming
                        .ContactResolutionProgress
                    : GameplaySemanticReplayPresentationTiming
                        .ActionResolutionProgress);
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
                attack.HitRegion,
                attack.IsContactAttack
                    ? GameplaySemanticReplayPresentationTiming
                        .ContactResolutionProgress
                    : GameplaySemanticReplayPresentationTiming
                        .ActionResolutionProgress));
        }

        private static void ProjectProjectileImpactReactions(
            GameplaySemanticReplayFrame frame,
            ProjectileAdvanceRecord advance,
            long sequence,
            float progress,
            ICollection<TurnReplayActorActionState> states)
        {
            foreach (TurnReplayActorActionState state in
                ProjectProjectileImpactReactions(
                    advance,
                    sequence,
                    progress,
                    frame.Previous.Session.Actors,
                    frame.Resulting.Session.Actors))
                states.Add(state);
        }

        internal static IReadOnlyList<TurnReplayActorActionState>
            ProjectProjectileImpactReactions(
                ProjectileAdvanceRecord advance,
                long sequence,
                float progress,
                IReadOnlyList<GameplayActorSnapshot> previousActors,
                IReadOnlyList<GameplayActorSnapshot> resultingActors)
        {
            if (advance == null) throw new ArgumentNullException(nameof(advance));
            if (sequence <= 0) throw new ArgumentOutOfRangeException(
                nameof(sequence));
            if (previousActors == null) throw new ArgumentNullException(
                nameof(previousActors));
            if (resultingActors == null) throw new ArgumentNullException(
                nameof(resultingActors));
            ProjectileImpactRecord impact = advance.Resulting.Impact;
            if (impact == null)
                return Array.Empty<TurnReplayActorActionState>();
            float eventTime = GameplaySemanticReplayPresentationTiming
                .GetProjectileImpactProgress(advance);
            var states = new List<TurnReplayActorActionState>();
            var reacted = new HashSet<string>(StringComparer.Ordinal);
            foreach (BlastEffectRecord effect in impact.BlastEffects)
            {
                if (effect.SubjectKind != BlastSubjectKind.Actor
                    || !reacted.Add(effect.EntityId))
                    continue;
                AddProjectileReaction(
                    previousActors,
                    resultingActors,
                    effect.EntityId,
                    effect.InjuryRegion,
                    sequence,
                    progress,
                    eventTime,
                    states);
            }

            if (reacted.Add(impact.HitEntityId))
            {
                AddProjectileReaction(
                    previousActors,
                    resultingActors,
                    impact.HitEntityId,
                    hitRegion: null,
                    sequence,
                    progress,
                    eventTime,
                    states);
            }
            return states.Count == 0
                ? Array.Empty<TurnReplayActorActionState>()
                : states.AsReadOnly();
        }

        private static void AddProjectileReaction(
            IReadOnlyList<GameplayActorSnapshot> previousActors,
            IReadOnlyList<GameplayActorSnapshot> resultingActors,
            string actorId,
            TargetRegionId? hitRegion,
            long sequence,
            float progress,
            float eventTime,
            ICollection<TurnReplayActorActionState> states)
        {
            if (!TryFindActor(
                resultingActors,
                actorId,
                out GameplayActorSnapshot resulting)
                || !TryFindActor(
                previousActors,
                actorId,
                out GameplayActorSnapshot previous)
                || resulting.Wounds.WoundCount
                    <= previous.Wounds.WoundCount)
                return;
            states.Add(new TurnReplayActorActionState(
                actorId,
                TurnReplayActorActionKind.Reaction,
                sequence,
                progress,
                contactReaction: false,
                resulting.Wounds.WoundCount,
                hitRegion,
                eventTime));
        }

        private static bool TryFindActor(
            IReadOnlyList<GameplayActorSnapshot> actors,
            string actorId,
            out GameplayActorSnapshot result)
        {
            foreach (GameplayActorSnapshot actor in actors)
                if (string.Equals(
                        actor.ActorId,
                        actorId,
                        StringComparison.Ordinal))
                {
                    result = actor;
                    return true;
                }
            result = default;
            return false;
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

        private static GameplayPosition AddHeight(
            GameplayPosition position,
            float height) => new GameplayPosition(
                position.X,
                position.Y + height,
                position.Z);

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
