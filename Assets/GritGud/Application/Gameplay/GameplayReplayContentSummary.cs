using System;
using System.Collections.Generic;
using System.Globalization;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayReplayPresentationCompatibility
    {
        public GameplayReplayPresentationCompatibility(
            IEnumerable<string> matchedActorIds,
            IEnumerable<string> missingActorIds)
        {
            MatchedActorIds = CopyIds(
                matchedActorIds,
                nameof(matchedActorIds));
            MissingActorIds = CopyIds(
                missingActorIds,
                nameof(missingActorIds));
        }

        public IReadOnlyList<string> MatchedActorIds { get; }

        public IReadOnlyList<string> MissingActorIds { get; }

        public bool IsCompatible => MissingActorIds.Count == 0;

        private static IReadOnlyList<string> CopyIds(
            IEnumerable<string> values,
            string parameterName)
        {
            if (values == null)
                throw new ArgumentNullException(parameterName);
            var copied = new List<string>();
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException(
                        "Replay presentation actor identifiers cannot be blank.",
                        parameterName);
                if (!copied.Contains(value)) copied.Add(value);
            }
            return copied.AsReadOnly();
        }
    }

    /// <summary>
    /// Proof that a replay contains semantic actions which survive projection
    /// into visible state, timed events, and combat transcript entries.
    /// </summary>
    public sealed class GameplayReplayContentSummary
    {
        private readonly IReadOnlyList<string> replayActorIds;

        public GameplayReplayContentSummary(
            string sourceLabel,
            GameplaySemanticReplayPlaybackTimeline playback)
        {
            SourceLabel = GameplayContentIdentity.RequireText(
                sourceLabel,
                nameof(sourceLabel));
            Playback = playback ?? throw new ArgumentNullException(
                nameof(playback));

            var actorIds = new List<string>();
            var seenActors = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameplayActorSnapshot actor in playback.Replay.InitialState
                .Session.Actors)
            {
                AddActor(actor.ActorId, actorIds, seenActors);
            }

            foreach (GameplaySemanticReplayPlaybackFrame playbackFrame in
                playback.Frames)
            {
                GameplaySemanticReplayFrame frame = playbackFrame.Frame;
                GameplaySemanticCapability capability = frame.Transition
                    .Profile.Capability;
                switch (capability)
                {
                    case GameplaySemanticCapability.Move:
                        MovementFrames++;
                        break;
                    case GameplaySemanticCapability.DirectAttack:
                        DirectAttackFrames++;
                        break;
                    case GameplaySemanticCapability.LaunchProjectile:
                        ProjectileLaunchFrames++;
                        break;
                    case GameplaySemanticCapability.AdvanceProjectile:
                        ProjectileAdvanceFrames++;
                        break;
                    case GameplaySemanticCapability.ThrowExplosive:
                        GrenadeThrowFrames++;
                        break;
                    case GameplaySemanticCapability.Equip:
                    case GameplaySemanticCapability.Reload:
                        EquipmentFrames++;
                        break;
                    case GameplaySemanticCapability.EndTurn:
                        EndTurnFrames++;
                        break;
                }
                if (frame.SemanticRecord is DroneMoveRecord
                    || frame.SemanticRecord is DroneAttackRecord
                    || frame.SemanticRecord is ActorDroneAttackRecord)
                    DroneActionFrames++;
                if (HasActorPoseDelta(frame)) ActorPoseDeltaFrames++;
                if (TurnReplayActorActionProjector.Project(frame, 0.5f).Count
                    > 0)
                    ActorActionFrames++;

                IReadOnlyList<ReplayCombatPresentationEvent> events =
                    ReplayCombatPresentationEventProjector.Project(frame);
                TimedCombatEvents += events.Count;
                foreach (ReplayCombatPresentationEvent presentationEvent in
                    events)
                {
                    if (presentationEvent.Kind ==
                            ReplayCombatPresentationEventKind
                                .ThrownExplosiveRelease
                        || presentationEvent.Kind ==
                            ReplayCombatPresentationEventKind
                                .ThrownExplosiveImpact)
                        GrenadeFlightEvents++;
                    if (presentationEvent.Kind ==
                            ReplayCombatPresentationEventKind.ProjectileImpact
                        || presentationEvent.Kind ==
                            ReplayCombatPresentationEventKind
                                .ThrownExplosiveImpact)
                        ProjectileImpactEvents++;
                }

                foreach (GameplayActorSnapshot actor in frame.Resulting.Session
                    .Actors)
                {
                    AddActor(actor.ActorId, actorIds, seenActors);
                }
            }

            replayActorIds = actorIds.AsReadOnly();
            Transcript = new ReplayCombatTranscript(playback);
        }

        public string SourceLabel { get; }

        public GameplaySemanticReplayPlaybackTimeline Playback { get; }

        public int SemanticFrames => Playback.Frames.Count;

        public int MovementFrames { get; private set; }

        public int DirectAttackFrames { get; private set; }

        public int ProjectileLaunchFrames { get; private set; }

        public int ProjectileAdvanceFrames { get; private set; }

        public int GrenadeThrowFrames { get; private set; }

        public int DroneActionFrames { get; private set; }

        public int EquipmentFrames { get; private set; }

        public int EndTurnFrames { get; private set; }

        public int ActorPoseDeltaFrames { get; private set; }

        public int ActorActionFrames { get; private set; }

        public int TimedCombatEvents { get; private set; }

        public int GrenadeFlightEvents { get; private set; }

        public int ProjectileImpactEvents { get; private set; }

        public IReadOnlyList<string> ReplayActorIds => replayActorIds;

        public ReplayCombatTranscript Transcript { get; }

        public GameplayReplayPresentationCompatibility PresentationCompatibility
        {
            get;
            private set;
        }

        public int ReplayableSemanticFrames => SemanticFrames - EndTurnFrames;

        public bool IsSemanticallyReplayable => SemanticFrames > 0
            && ReplayableSemanticFrames > 0
            && (MovementFrames == 0 || ActorPoseDeltaFrames > 0)
            && (!ContainsCombatFrames || Transcript.Entries.Count > 0);

        public string ValidationMessage
        {
            get
            {
                if (SemanticFrames == 0)
                    return "REPLAY SOURCE CONTAINS NO SEMANTIC FRAMES";
                if (ReplayableSemanticFrames == 0)
                    return "LATEST PLAYER-AWAY INTERVAL CONTAINS NO REPLAYABLE ACTIONS";
                if (MovementFrames > 0 && ActorPoseDeltaFrames == 0)
                    return "REPLAY SAMPLER LOST RECORDED MOVEMENT";
                if (ContainsCombatFrames && Transcript.Entries.Count == 0)
                    return "REPLAY COMBAT EVENTS DID NOT REACH THE TRANSCRIPT";
                if (PresentationCompatibility != null
                    && !PresentationCompatibility.IsCompatible)
                    return "REPLAY ACTOR IDENTITIES DO NOT MATCH THE SCENE: "
                        + string.Join(", ",
                            PresentationCompatibility.MissingActorIds);
                return string.Empty;
            }
        }

        public bool IsReadyToOpen => IsSemanticallyReplayable
            && (PresentationCompatibility == null
                || PresentationCompatibility.IsCompatible);

        public void SetPresentationCompatibility(
            GameplayReplayPresentationCompatibility compatibility)
        {
            PresentationCompatibility = compatibility
                ?? throw new ArgumentNullException(nameof(compatibility));
        }

        public string ToDisplayText()
        {
            string presenterText = PresentationCompatibility == null
                ? "presenters pending"
                : PresentationCompatibility.MatchedActorIds.Count.ToString(
                        CultureInfo.InvariantCulture)
                    + "/" + ReplayActorIds.Count.ToString(
                        CultureInfo.InvariantCulture)
                    + " actors matched";
            ReplayCombatDiagnosticTotals totals = Transcript.Totals;
            return SourceLabel.ToUpperInvariant()
                + Environment.NewLine
                + $"Frames {SemanticFrames}: Move {MovementFrames}, Attack "
                + $"{DirectAttackFrames + ProjectileLaunchFrames}, Grenade "
                + $"{GrenadeThrowFrames}, Drone {DroneActionFrames}, Equip "
                + $"{EquipmentFrames}, EndTurn {EndTurnFrames}"
                + Environment.NewLine
                + $"Projection: pose {ActorPoseDeltaFrames}, action "
                + $"{ActorActionFrames}, timed {TimedCombatEvents}; "
                + presenterText
                + Environment.NewLine
                + $"Transcript {Transcript.Entries.Count}: attacks "
                + $"{totals.AttackExecutions}, hits {totals.Hits}, misses "
                + $"{totals.Misses}, impacts {totals.ProjectileImpacts}, "
                + $"incapacitations {totals.Incapacitations}";
        }

        private bool ContainsCombatFrames => DirectAttackFrames > 0
            || ProjectileLaunchFrames > 0
            || GrenadeThrowFrames > 0
            || DroneActionFrames > 0;

        private static void AddActor(
            string actorId,
            ICollection<string> actorIds,
            ISet<string> seenActors)
        {
            if (seenActors.Add(actorId)) actorIds.Add(actorId);
        }

        private static bool HasActorPoseDelta(
            GameplaySemanticReplayFrame frame)
        {
            foreach (GameplayActorSnapshot resulting in frame.Resulting.Session
                .Actors)
            {
                GameplayActorSnapshot previous = frame.Previous.Session
                    .GetActor(resulting.ActorId);
                if (previous.Pose.Position.DistanceTo(resulting.Pose.Position)
                        > 0f
                    || previous.Pose.FacingDegrees
                        != resulting.Pose.FacingDegrees
                    || previous.Pose.Stance != resulting.Pose.Stance)
                    return true;
            }
            return false;
        }
    }
}
