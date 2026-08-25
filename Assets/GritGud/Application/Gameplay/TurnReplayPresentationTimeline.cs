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
        ThrownExplosiveRelease = 5,
        ThrownExplosiveImpact = 6,
        Death = 7,
        DroneCrashImpact = 8,
    }

    public enum ReplayCombatPresentationOutcome
    {
        None = 0,
        Hit = 1,
        Miss = 2,
        Blocked = 3,
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
            string presentationId = null,
            ReplayCombatPresentationOutcome outcome =
                ReplayCombatPresentationOutcome.None,
            int eventOrdinal = -1)
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
            if (!Enum.IsDefined(
                    typeof(ReplayCombatPresentationOutcome),
                    outcome))
                throw new ArgumentOutOfRangeException(nameof(outcome));
            if (eventOrdinal < -1)
                throw new ArgumentOutOfRangeException(nameof(eventOrdinal));
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
                    || kind == ReplayCombatPresentationEventKind.ProjectileImpact
                    || kind == ReplayCombatPresentationEventKind
                        .ThrownExplosiveRelease
                    || kind == ReplayCombatPresentationEventKind
                        .ThrownExplosiveImpact)
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
            Outcome = outcome;
            EventOrdinal = eventOrdinal;
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
        public ReplayCombatPresentationOutcome Outcome { get; }
        public int EventOrdinal { get; }

        public string CombatEventId => "replay-combat:"
            + TransitionSequence + ":" + EventOrdinal + ":" + Kind + ":"
            + ShooterKind + ":" + ActorId + ":" + TargetKind + ":"
            + TargetId + ":" + ProjectileId;

        public string StableKey => CombatEventId;
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
            bool contactAttack = false,
            ActorTargetFacingActionPhase targetFacingPhase = null,
            ActorLifeState? resultingLifeState = null)
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
            TargetFacingPhase = targetFacingPhase;
            if (resultingLifeState.HasValue
                && !Enum.IsDefined(
                    typeof(ActorLifeState),
                    resultingLifeState.Value))
                throw new ArgumentOutOfRangeException(
                    nameof(resultingLifeState));
            ResultingLifeState = resultingLifeState;
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
        public ActorTargetFacingActionPhase TargetFacingPhase { get; }
        public ActorLifeState? ResultingLifeState { get; }
    }

    /// <summary>
    /// Canonical evidence for one actor life-state change inside a semantic
    /// replay frame. This is presentation metadata only: it identifies when
    /// the already-verified state change becomes visible and, when an injury
    /// caused it, preserves the localized evidence used to select a pose.
    /// </summary>
    public sealed class ReplayActorLifeStateTransition
    {
        public ReplayActorLifeStateTransition(
            long transitionSequence,
            string actorId,
            ActorLifeState previousLifeState,
            ActorLifeState resultingLifeState,
            float normalizedTime,
            TargetRegionId? hitRegion,
            DamageMechanism? damageMechanism)
        {
            if (transitionSequence <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(transitionSequence));
            ActorId = string.IsNullOrWhiteSpace(actorId)
                ? throw new ArgumentException(
                    "Replay life-state transitions require an actor identifier.",
                    nameof(actorId))
                : actorId;
            if (!Enum.IsDefined(typeof(ActorLifeState), previousLifeState))
                throw new ArgumentOutOfRangeException(
                    nameof(previousLifeState));
            if (!Enum.IsDefined(typeof(ActorLifeState), resultingLifeState))
                throw new ArgumentOutOfRangeException(
                    nameof(resultingLifeState));
            if (previousLifeState == resultingLifeState)
                throw new ArgumentException(
                    "Replay life-state transitions must change state.",
                    nameof(resultingLifeState));
            if (float.IsNaN(normalizedTime)
                || float.IsInfinity(normalizedTime)
                || normalizedTime < 0f
                || normalizedTime > 1f)
                throw new ArgumentOutOfRangeException(nameof(normalizedTime));
            if (hitRegion.HasValue
                && !Enum.IsDefined(typeof(TargetRegionId), hitRegion.Value))
                throw new ArgumentOutOfRangeException(nameof(hitRegion));
            if (damageMechanism.HasValue
                && !Enum.IsDefined(
                    typeof(DamageMechanism),
                    damageMechanism.Value))
                throw new ArgumentOutOfRangeException(
                    nameof(damageMechanism));

            TransitionSequence = transitionSequence;
            PreviousLifeState = previousLifeState;
            ResultingLifeState = resultingLifeState;
            NormalizedTime = normalizedTime;
            HitRegion = hitRegion;
            DamageMechanism = damageMechanism;
        }

        public long TransitionSequence { get; }
        public string ActorId { get; }
        public ActorLifeState PreviousLifeState { get; }
        public ActorLifeState ResultingLifeState { get; }
        public float NormalizedTime { get; }
        public TargetRegionId? HitRegion { get; }
        public DamageMechanism? DamageMechanism { get; }
        public bool EntersTerminalPose =>
            PreviousLifeState == ActorLifeState.Active
            && ResultingLifeState != ActorLifeState.Active;
        public bool Recovers =>
            PreviousLifeState != ActorLifeState.Active
            && ResultingLifeState == ActorLifeState.Active;
        public string StableKey => "replay-life-state:"
            + TransitionSequence + ":" + ActorId + ":"
            + PreviousLifeState + ":" + ResultingLifeState;
    }

    /// <summary>
    /// Absolute-time form of a canonical life-state transition.
    /// </summary>
    public sealed class ReplayActorLifeStateEvent
    {
        internal ReplayActorLifeStateEvent(
            ReplayActorLifeStateTransition transition,
            float timeSeconds)
        {
            Transition = transition ?? throw new ArgumentNullException(
                nameof(transition));
            GameplayNumericPolicy.RequireFinite(timeSeconds, nameof(timeSeconds));
            if (timeSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(timeSeconds));
            TimeSeconds = timeSeconds;
        }

        public ReplayActorLifeStateTransition Transition { get; }
        public float TimeSeconds { get; }
        public long TransitionSequence => Transition.TransitionSequence;
        public string ActorId => Transition.ActorId;
        public ActorLifeState PreviousLifeState =>
            Transition.PreviousLifeState;
        public ActorLifeState ResultingLifeState =>
            Transition.ResultingLifeState;
        public TargetRegionId? HitRegion => Transition.HitRegion;
        public DamageMechanism? DamageMechanism =>
            Transition.DamageMechanism;
        public string StableKey => Transition.StableKey;
    }

    public enum ReplayActorTerminalPoseKind
    {
        FallOver = 0,
        ShoulderFall = 1,
    }

    /// <summary>
    /// A seekable presentation episode for one transition from active to a
    /// non-active life state. Later incapacitated/dead status changes do not
    /// restart the pose. A recovery closes the episode and a later terminal
    /// transition creates a new identity.
    /// </summary>
    public sealed class ReplayActorTerminalPoseEpisode
    {
        internal ReplayActorTerminalPoseEpisode(
            ReplayActorLifeStateEvent source,
            ReplayActorTerminalPoseKind poseKind,
            float animationDurationSeconds,
            float? recoveryTimeSeconds)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            if (!source.Transition.EntersTerminalPose)
                throw new ArgumentException(
                    "Terminal pose episodes require an active-to-terminal transition.",
                    nameof(source));
            if (!Enum.IsDefined(
                    typeof(ReplayActorTerminalPoseKind),
                    poseKind))
                throw new ArgumentOutOfRangeException(nameof(poseKind));
            GameplayNumericPolicy.RequireFinite(
                animationDurationSeconds,
                nameof(animationDurationSeconds));
            if (animationDurationSeconds <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(animationDurationSeconds));
            if (recoveryTimeSeconds.HasValue)
            {
                GameplayNumericPolicy.RequireFinite(
                    recoveryTimeSeconds.Value,
                    nameof(recoveryTimeSeconds));
                if (recoveryTimeSeconds.Value < source.TimeSeconds)
                    throw new ArgumentOutOfRangeException(
                        nameof(recoveryTimeSeconds));
            }

            PoseKind = poseKind;
            AnimationDurationSeconds = animationDurationSeconds;
            RecoveryTimeSeconds = recoveryTimeSeconds;
        }

        public ReplayActorLifeStateEvent Source { get; }
        public string EpisodeId => "terminal:" + ActorId + ":"
            + Source.TransitionSequence;
        public string ActorId => Source.ActorId;
        public long SourceTransitionSequence => Source.TransitionSequence;
        public ActorLifeState EnteredLifeState => Source.ResultingLifeState;
        public ReplayActorTerminalPoseKind PoseKind { get; }
        public TargetRegionId? HitRegion => Source.HitRegion;
        public DamageMechanism? DamageMechanism => Source.DamageMechanism;
        public float StartSeconds => Source.TimeSeconds;
        public float AnimationDurationSeconds { get; }
        public float AnimationEndSeconds =>
            StartSeconds + AnimationDurationSeconds;
        public float? RecoveryTimeSeconds { get; }
        public float PresentationEndSeconds => RecoveryTimeSeconds.HasValue
            ? Math.Min(AnimationEndSeconds, RecoveryTimeSeconds.Value)
            : AnimationEndSeconds;

        public bool Contains(float timeSeconds)
        {
            GameplayNumericPolicy.RequireFinite(timeSeconds, nameof(timeSeconds));
            return timeSeconds >= StartSeconds
                && (!RecoveryTimeSeconds.HasValue
                    || timeSeconds < RecoveryTimeSeconds.Value);
        }
    }

    public sealed class ReplayActorTerminalPoseSample
    {
        internal ReplayActorTerminalPoseSample(
            ReplayActorTerminalPoseEpisode episode,
            float normalizedProgress)
        {
            Episode = episode ?? throw new ArgumentNullException(
                nameof(episode));
            GameplayNumericPolicy.RequireFinite(
                normalizedProgress,
                nameof(normalizedProgress));
            NormalizedProgress = Math.Max(
                0f,
                Math.Min(1f, normalizedProgress));
        }

        public ReplayActorTerminalPoseEpisode Episode { get; }
        public string EpisodeId => Episode.EpisodeId;
        public string ActorId => Episode.ActorId;
        public ReplayActorTerminalPoseKind PoseKind => Episode.PoseKind;
        public float NormalizedProgress { get; }
    }

    /// <summary>
    /// Single canonical projector for replay life-state timing and localized
    /// terminal-pose evidence. Markers, transcript entries, and terminal pose
    /// episodes consume this same projection.
    /// </summary>
    public static class ReplayActorLifeStateEventProjector
    {
        public static IReadOnlyList<ReplayActorLifeStateTransition> Project(
            GameplaySemanticReplayFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            var projected = new List<ReplayActorLifeStateTransition>();
            foreach (GameplayActorSnapshot resulting in
                frame.Resulting.Session.Actors)
            {
                if (!TryFindActor(
                        frame.Previous.Session.Actors,
                        resulting.ActorId,
                        out GameplayActorSnapshot previous)
                    || previous.LifeState == resulting.LifeState)
                    continue;
                InjuryRecord sourceInjury = FindNewestInjury(
                    previous,
                    resulting);
                projected.Add(new ReplayActorLifeStateTransition(
                    frame.Transition.Identity.Sequence,
                    resulting.ActorId,
                    previous.LifeState,
                    resulting.LifeState,
                    sourceInjury == null
                        ? 1f
                        : ResolveInjuryEventTime(frame),
                    sourceInjury?.Region,
                    sourceInjury?.Mechanism));
            }
            return projected.Count == 0
                ? Array.Empty<ReplayActorLifeStateTransition>()
                : projected.AsReadOnly();
        }

        public static IReadOnlyList<ReplayActorLifeStateEvent> Project(
            GameplaySemanticReplayPlaybackFrame playbackFrame)
        {
            if (playbackFrame == null)
                throw new ArgumentNullException(nameof(playbackFrame));
            var projected = new List<ReplayActorLifeStateEvent>();
            foreach (ReplayActorLifeStateTransition transition in
                Project(playbackFrame.Frame))
            {
                projected.Add(new ReplayActorLifeStateEvent(
                    transition,
                    playbackFrame.StartSeconds
                        + playbackFrame.DurationSeconds
                        * transition.NormalizedTime));
            }
            return projected.Count == 0
                ? Array.Empty<ReplayActorLifeStateEvent>()
                : projected.AsReadOnly();
        }

        private static InjuryRecord FindNewestInjury(
            GameplayActorSnapshot previous,
            GameplayActorSnapshot resulting)
        {
            if (resulting.Injuries.Injuries.Count
                <= previous.Injuries.Injuries.Count)
                return null;
            return resulting.Injuries.Injuries[
                resulting.Injuries.Injuries.Count - 1];
        }

        private static float ResolveInjuryEventTime(
            GameplaySemanticReplayFrame frame) =>
            GameplaySemanticReplayPresentationTiming.GetResolutionProgress(
                frame.SemanticRecord);

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
    }

    internal static class ReplayActorTerminalPoseEpisodeProjector
    {
        private sealed class EpisodeBuilder
        {
            public EpisodeBuilder(ReplayActorLifeStateEvent source)
            {
                Source = source;
            }

            public ReplayActorLifeStateEvent Source { get; }
            public float? RecoveryTimeSeconds { get; set; }
        }

        public static IReadOnlyList<ReplayActorTerminalPoseEpisode> Project(
            IReadOnlyList<GameplaySemanticReplayPlaybackFrame> frames,
            out IReadOnlyList<ReplayActorLifeStateEvent> lifeStateEvents)
        {
            if (frames == null) throw new ArgumentNullException(nameof(frames));
            var events = new List<ReplayActorLifeStateEvent>();
            foreach (GameplaySemanticReplayPlaybackFrame frame in frames)
            {
                foreach (ReplayActorLifeStateEvent lifeEvent in
                    ReplayActorLifeStateEventProjector.Project(frame))
                    events.Add(lifeEvent);
            }
            lifeStateEvents = events.Count == 0
                ? Array.Empty<ReplayActorLifeStateEvent>()
                : events.AsReadOnly();
            return Project(lifeStateEvents);
        }

        internal static IReadOnlyList<ReplayActorTerminalPoseEpisode> Project(
            IReadOnlyList<ReplayActorLifeStateEvent> lifeStateEvents)
        {
            if (lifeStateEvents == null)
                throw new ArgumentNullException(nameof(lifeStateEvents));
            var builders = new List<EpisodeBuilder>();
            var active = new Dictionary<string, EpisodeBuilder>(
                StringComparer.Ordinal);
            foreach (ReplayActorLifeStateEvent lifeEvent in lifeStateEvents)
            {
                if (lifeEvent == null)
                    throw new ArgumentException(
                        "Terminal episode evidence cannot contain null events.",
                        nameof(lifeStateEvents));
                if (lifeEvent.Transition.EntersTerminalPose)
                {
                    var builder = new EpisodeBuilder(lifeEvent);
                    builders.Add(builder);
                    active[lifeEvent.ActorId] = builder;
                }
                else if (lifeEvent.Transition.Recovers
                    && active.TryGetValue(
                        lifeEvent.ActorId,
                        out EpisodeBuilder builder))
                {
                    builder.RecoveryTimeSeconds = lifeEvent.TimeSeconds;
                    active.Remove(lifeEvent.ActorId);
                }
            }

            var episodes = new List<ReplayActorTerminalPoseEpisode>(
                builders.Count);
            foreach (EpisodeBuilder builder in builders)
                episodes.Add(new ReplayActorTerminalPoseEpisode(
                    builder.Source,
                    ResolvePoseKind(builder.Source.HitRegion),
                    GameplaySemanticReplayPresentationTiming
                        .TerminalCollapseSeconds,
                    builder.RecoveryTimeSeconds));
            return episodes.Count == 0
                ? Array.Empty<ReplayActorTerminalPoseEpisode>()
                : episodes.AsReadOnly();
        }

        private static ReplayActorTerminalPoseKind ResolvePoseKind(
            TargetRegionId? hitRegion)
        {
            switch (hitRegion)
            {
                case TargetRegionId.Torso:
                case TargetRegionId.LeftArm:
                case TargetRegionId.RightArm:
                    return ReplayActorTerminalPoseKind.ShoulderFall;
                default:
                    return ReplayActorTerminalPoseKind.FallOver;
            }
        }
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
                            .ActionResolutionProgress,
                        outcome: resolved.Attack.Hit
                            ? ReplayCombatPresentationOutcome.Hit
                            : ReplayCombatPresentationOutcome.Miss));
                    break;
                }
            }
            AppendReactionEvents(frame, events);
            for (int index = 0; index < events.Count; index++)
                events[index] = CompleteEventIdentity(
                    frame,
                    events[index],
                    index);
            return events.Count == 0
                ? Array.Empty<ReplayCombatPresentationEvent>()
                : events.AsReadOnly();
        }

        private static ReplayCombatPresentationEvent WithEventOrdinal(
            ReplayCombatPresentationEvent presentationEvent,
            int eventOrdinal) => new ReplayCombatPresentationEvent(
                presentationEvent.TransitionSequence,
                presentationEvent.Kind,
                presentationEvent.ActorId,
                presentationEvent.TargetId,
                presentationEvent.Origin,
                presentationEvent.Destination,
                presentationEvent.NormalizedTime,
                presentationEvent.ProjectileId,
                presentationEvent.ShooterKind,
                presentationEvent.TargetKind,
                presentationEvent.PresentationId,
                presentationEvent.Outcome,
                eventOrdinal);

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
                    SummonedDroneSnapshot target = FindDrone(
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
                            ReplayCombatPresentationSubjectKind.Drone,
                        outcome: attack.Hit
                            ? ReplayCombatPresentationOutcome.Hit
                            : ReplayCombatPresentationOutcome.Miss));
                    break;
                }
                case DroneAttackRecord attack:
                {
                    SummonedDroneSnapshot shooter = FindDrone(
                        frame.Previous.Drones,
                        attack.DroneId);
                    ReplayCombatPresentationSubjectKind targetKind =
                        ResolveSubjectKind(frame, attack.TargetId);
                    events.Add(ProjectDroneDischarge(
                        sequence,
                        attack,
                        shooter,
                        ResolveSubjectPosition(
                            frame,
                            attack.TargetId,
                            targetKind),
                        targetKind));
                    break;
                }
            }
        }

        public static ReplayCombatPresentationEvent ProjectDroneDischarge(
            long transitionSequence,
            DroneAttackRecord attack,
            SummonedDroneSnapshot shooter,
            GameplayPosition destination,
            ReplayCombatPresentationSubjectKind targetKind)
        {
            if (attack == null) throw new ArgumentNullException(nameof(attack));
            if (!string.Equals(
                    attack.DroneId,
                    shooter.DroneId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Drone discharge state must match its semantic record.",
                    nameof(shooter));
            return new ReplayCombatPresentationEvent(
                transitionSequence,
                ReplayCombatPresentationEventKind.WeaponDischarge,
                attack.DroneId,
                attack.TargetId,
                shooter.Position,
                destination,
                GameplaySemanticReplayPresentationTiming
                    .ActionResolutionProgress,
                shooterKind: ReplayCombatPresentationSubjectKind.Drone,
                targetKind: targetKind,
                presentationId: shooter.Definition.Attack.ActionId,
                outcome: ResolveOutcome(attack.Consequence));
        }

        private static ReplayCombatPresentationEvent CompleteEventIdentity(
            GameplaySemanticReplayFrame frame,
            ReplayCombatPresentationEvent presentationEvent,
            int eventOrdinal)
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
                presentationId,
                presentationEvent.Outcome,
                eventOrdinal);
        }

        private static ReplayCombatPresentationOutcome ResolveOutcome(
            object consequence)
        {
            if (consequence is AttackResolutionRecord attack)
                return attack.Hit
                    ? ReplayCombatPresentationOutcome.Hit
                    : ReplayCombatPresentationOutcome.Miss;
            return consequence == null
                ? ReplayCombatPresentationOutcome.None
                : ReplayCombatPresentationOutcome.Hit;
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
            foreach (SummonedDroneSnapshot drone in frame.Previous.Drones)
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

        private static SummonedDroneSnapshot FindDrone(
            IEnumerable<SummonedDroneSnapshot> drones,
            string droneId)
        {
            foreach (SummonedDroneSnapshot drone in drones)
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
                    else if (outcome is ThrownExplosiveActionOutcome thrown)
                    {
                        ThrownExplosiveRecord value = thrown.Record;
                        string projectileId =
                            GameplayThrownExplosivePresentationTiming
                                .GetProjectileId(
                                    value.ThrowerId,
                                    value.Sequence);
                        events.Add(new ReplayCombatPresentationEvent(
                            sequence,
                            ReplayCombatPresentationEventKind
                                .ThrownExplosiveRelease,
                            value.ThrowerId,
                            GameplayTargetIds.WorldAimPoint,
                            value.LaunchOrigin,
                            value.ResolvedLanding,
                            GameplayThrownExplosivePresentationTiming
                                .ReleaseNormalizedTime,
                            projectileId,
                            presentationId: value.Definition.Id));
                        events.Add(new ReplayCombatPresentationEvent(
                            sequence,
                            ReplayCombatPresentationEventKind
                                .ThrownExplosiveImpact,
                            value.ThrowerId,
                            GameplayTargetIds.WorldAimPoint,
                            value.LaunchOrigin,
                            value.ResolvedLanding,
                            GameplayThrownExplosivePresentationTiming
                                .ImpactNormalizedTime,
                            projectileId,
                            presentationId: value.Definition.Id));
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
            else if (semanticRecord is DroneCrashImpactRecord crash)
            {
                events.Add(new ReplayCombatPresentationEvent(
                    sequence,
                    ReplayCombatPresentationEventKind.DroneCrashImpact,
                    crash.DroneId,
                    GameplayTargetIds.WorldAimPoint,
                    crash.Origin,
                    crash.ImpactPosition,
                    crash.ImpactNormalizedTime,
                    shooterKind:
                        ReplayCombatPresentationSubjectKind.Drone,
                    targetKind:
                        ReplayCombatPresentationSubjectKind.World,
                    outcome: ReplayCombatPresentationOutcome.Hit));
            }

            for (int index = 0; index < events.Count; index++)
                events[index] = WithEventOrdinal(events[index], index);
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
            }

            foreach (ReplayActorLifeStateTransition transition in
                ReplayActorLifeStateEventProjector.Project(frame))
            {
                ReplayCombatPresentationEventKind? kind =
                    transition.ResultingLifeState == ActorLifeState.Dead
                        ? ReplayCombatPresentationEventKind.Death
                        : transition.ResultingLifeState
                            == ActorLifeState.Incapacitated
                            ? ReplayCombatPresentationEventKind.Incapacitation
                            : null;
                if (!kind.HasValue) continue;
                GameplayActorSnapshot resulting = frame.Resulting.Session
                    .GetActor(transition.ActorId);
                GameplayPosition position = AddHeight(
                    resulting.Pose.Position,
                    1f);
                events.Add(new ReplayCombatPresentationEvent(
                    transition.TransitionSequence,
                    kind.Value,
                    transition.ActorId,
                    transition.ActorId,
                    position,
                    position,
                    transition.NormalizedTime));
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
                float eventNormalizedTime = -1f,
                ActorLifeState? resultingLifeState = null)
            {
                Kind = kind;
                ContactReaction = contactReaction;
                ResultingWoundCount = resultingWoundCount;
                HitRegion = hitRegion;
                EventNormalizedTime = eventNormalizedTime;
                ResultingLifeState = resultingLifeState;
            }

            public TurnReplayActorActionKind Kind { get; }
            public bool ContactReaction { get; }
            public int ResultingWoundCount { get; }
            public TargetRegionId? HitRegion { get; }
            public float EventNormalizedTime { get; }
            public ActorLifeState? ResultingLifeState { get; }
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
                    SummonedDroneSnapshot target = FindDrone(
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
                case DroneCrashImpactRecord crash:
                {
                    var reacted = new HashSet<string>(StringComparer.Ordinal);
                    foreach (BlastEffectRecord effect in crash.Effects)
                    {
                        if (effect.SubjectKind != BlastSubjectKind.Actor
                            || !reacted.Add(effect.EntityId))
                            continue;
                        AddProjectileReaction(
                            frame.Previous.Session.Actors,
                            frame.Resulting.Session.Actors,
                            effect.EntityId,
                            effect.InjuryRegion,
                            sequence,
                            progress,
                            crash.ImpactNormalizedTime,
                            states);
                    }
                    break;
                }
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

        private static SummonedDroneSnapshot FindDrone(
            IEnumerable<SummonedDroneSnapshot> drones,
            string droneId)
        {
            foreach (SummonedDroneSnapshot drone in drones)
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
            ActorTargetFacingActionPhase primaryTargetFacingPhase = null;
            var reactions = new Dictionary<
                string,
                ReactionProjection>(StringComparer.Ordinal);
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (outcome is ThrownExplosiveActionOutcome thrown)
                {
                    primary = TurnReplayActorActionKind.Throw;
                    ThrownExplosiveRecord record = thrown.Record;
                    GameplayActorSnapshot previous = frame.Previous.Session
                        .GetActor(record.ThrowerId);
                    GameplayActorSnapshot resulting = frame.Resulting.Session
                        .GetActor(record.ThrowerId);
                    primaryEventTime =
                        GameplayThrownExplosivePresentationTiming
                            .ReleaseNormalizedTime;
                    primaryOrigin = record.LaunchOrigin;
                    primaryDestination = record.ResolvedLanding;
                    primaryProjectileId =
                        GameplayThrownExplosivePresentationTiming
                            .GetProjectileId(
                                record.ThrowerId,
                                record.Sequence);
                    primaryTargetFacingPhase =
                        GameplayThrownExplosivePresentationTiming
                            .CreateFacingPhase(
                                previous.Pose.FacingDegrees,
                                resulting.Pose.FacingDegrees);
                    foreach (BlastEffectRecord effect in record.BlastEffects)
                    {
                        if (!effect.IsLocalizedActorInjury) continue;
                        GameplayActorSnapshot previousTarget = frame.Previous
                            .Session.GetActor(effect.EntityId);
                        GameplayActorSnapshot resultingTarget = frame.Resulting
                            .Session.GetActor(effect.EntityId);
                        if (resultingTarget.Injuries.Injuries.Count
                            <= previousTarget.Injuries.Injuries.Count)
                            continue;
                        reactions[effect.EntityId] = new ReactionProjection(
                            TurnReplayActorActionKind.Reaction,
                            contactReaction: false,
                            resultingTarget.Wounds.WoundCount,
                            effect.InjuryRegion,
                            GameplayThrownExplosivePresentationTiming
                                .ImpactNormalizedTime,
                            resultingTarget.LifeState);
                    }
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
                    contactAttack: primaryContactAttack,
                    targetFacingPhase: primaryTargetFacingPhase));
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
                    reaction.Value.EventNormalizedTime,
                    resultingLifeState: reaction.Value.ResultingLifeState));
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
                        .ActionResolutionProgress,
                attack.Injury?.ResultingLifeState);
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
                        .ActionResolutionProgress,
                resultingLifeState: attack.Injury?.ResultingLifeState));
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
                eventTime,
                resultingLifeState: resulting.LifeState));
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
