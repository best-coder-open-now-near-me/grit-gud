using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public enum ReplayCombatTranscriptEventKind
    {
        WeaponDischarge = 0,
        ProjectileLaunch = 1,
        ProjectileImpact = 2,
        Reaction = 3,
        WoundApplied = 4,
        Incapacitation = 5,
        ExplosiveThrow = 6,
    }

    public sealed class ReplayCombatTranscriptEntry
    {
        private readonly IReadOnlyList<string> displayLines;

        internal ReplayCombatTranscriptEntry(
            long sequence,
            string combatEventId,
            string attackExecutionId,
            long transitionSequence,
            int eventOrdinal,
            float timeSeconds,
            float normalizedReplayTime,
            ReplayCombatTranscriptEventKind eventKind,
            ReplayCombatPresentationSubjectKind shooterKind,
            string shooterId,
            ReplayCombatPresentationSubjectKind targetKind,
            string targetId,
            string presentationId,
            string projectileId,
            ReplayCombatPresentationOutcome outcome,
            TargetRegionId? hitRegion,
            int woundsBefore,
            int woundsAfter,
            string displayTitle,
            IEnumerable<string> lines)
        {
            if (sequence <= 0) throw new ArgumentOutOfRangeException(
                nameof(sequence));
            if (string.IsNullOrWhiteSpace(combatEventId))
                throw new ArgumentException(
                    "Replay transcript entries require combat event identity.",
                    nameof(combatEventId));
            if (transitionSequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(transitionSequence));
            if (eventOrdinal < 0)
                throw new ArgumentOutOfRangeException(nameof(eventOrdinal));
            if (!Enum.IsDefined(typeof(ReplayCombatTranscriptEventKind),
                    eventKind))
                throw new ArgumentOutOfRangeException(nameof(eventKind));
            if (!Enum.IsDefined(typeof(ReplayCombatPresentationSubjectKind),
                    shooterKind))
                throw new ArgumentOutOfRangeException(nameof(shooterKind));
            if (!Enum.IsDefined(typeof(ReplayCombatPresentationSubjectKind),
                    targetKind))
                throw new ArgumentOutOfRangeException(nameof(targetKind));
            if (!Enum.IsDefined(typeof(ReplayCombatPresentationOutcome),
                    outcome))
                throw new ArgumentOutOfRangeException(nameof(outcome));
            if (float.IsNaN(timeSeconds)
                || float.IsInfinity(timeSeconds)
                || timeSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(timeSeconds));
            if (float.IsNaN(normalizedReplayTime)
                || float.IsInfinity(normalizedReplayTime)
                || normalizedReplayTime < 0f
                || normalizedReplayTime > 1f)
                throw new ArgumentOutOfRangeException(
                    nameof(normalizedReplayTime));
            if (woundsBefore < -1 || woundsAfter < -1)
                throw new ArgumentOutOfRangeException(nameof(woundsBefore));
            if ((woundsBefore < 0) != (woundsAfter < 0)
                || (woundsBefore >= 0 && woundsAfter < woundsBefore))
                throw new ArgumentException(
                    "Replay wound deltas must be absent or monotonic.");
            if (string.IsNullOrWhiteSpace(displayTitle))
                throw new ArgumentException(
                    "Replay transcript entries require display text.",
                    nameof(displayTitle));

            var copiedLines = new List<string>();
            foreach (string line in lines ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(line)) copiedLines.Add(line.Trim());
            }
            if (copiedLines.Count == 0)
                throw new ArgumentException(
                    "Replay transcript entries require at least one display line.",
                    nameof(lines));

            Sequence = sequence;
            CombatEventId = combatEventId.Trim();
            AttackExecutionId = attackExecutionId ?? string.Empty;
            TransitionSequence = transitionSequence;
            EventOrdinal = eventOrdinal;
            TimeSeconds = timeSeconds;
            NormalizedReplayTime = normalizedReplayTime;
            EventKind = eventKind;
            ShooterKind = shooterKind;
            ShooterId = shooterId ?? string.Empty;
            TargetKind = targetKind;
            TargetId = targetId ?? string.Empty;
            PresentationId = presentationId ?? string.Empty;
            ProjectileId = projectileId ?? string.Empty;
            Outcome = outcome;
            HitRegion = hitRegion;
            WoundsBefore = woundsBefore;
            WoundsAfter = woundsAfter;
            DisplayTitle = displayTitle.Trim();
            displayLines = copiedLines.AsReadOnly();
        }

        public long Sequence { get; }
        public string CombatEventId { get; }
        public string AttackExecutionId { get; }
        public long TransitionSequence { get; }
        public int EventOrdinal { get; }
        public float TimeSeconds { get; }
        public float NormalizedReplayTime { get; }
        public ReplayCombatTranscriptEventKind EventKind { get; }
        public ReplayCombatPresentationSubjectKind ShooterKind { get; }
        public string ShooterId { get; }
        public ReplayCombatPresentationSubjectKind TargetKind { get; }
        public string TargetId { get; }
        public string PresentationId { get; }
        public string ProjectileId { get; }
        public ReplayCombatPresentationOutcome Outcome { get; }
        public TargetRegionId? HitRegion { get; }
        public int WoundsBefore { get; }
        public int WoundsAfter { get; }
        public string DisplayTitle { get; }
        public IReadOnlyList<string> DisplayLines => displayLines;
    }

    public sealed class ReplayCombatDiagnosticTotals
    {
        internal ReplayCombatDiagnosticTotals(
            int attackExecutions,
            int weaponDischarges,
            int projectileLaunches,
            int projectileImpacts,
            int hits,
            int misses,
            int blockedAttacks,
            int reactions,
            int woundsApplied,
            int incapacitations)
        {
            AttackExecutions = attackExecutions;
            WeaponDischarges = weaponDischarges;
            ProjectileLaunches = projectileLaunches;
            ProjectileImpacts = projectileImpacts;
            Hits = hits;
            Misses = misses;
            BlockedAttacks = blockedAttacks;
            Reactions = reactions;
            WoundsApplied = woundsApplied;
            Incapacitations = incapacitations;
        }

        public int AttackExecutions { get; }
        public int WeaponDischarges { get; }
        public int ProjectileLaunches { get; }
        public int ProjectileImpacts { get; }
        public int Hits { get; }
        public int Misses { get; }
        public int BlockedAttacks { get; }
        public int Reactions { get; }
        public int WoundsApplied { get; }
        public int Incapacitations { get; }
    }

    public sealed class ReplayCombatTranscript
    {
        private readonly IReadOnlyList<ReplayCombatTranscriptEntry> entries;

        public ReplayCombatTranscript(
            GameplaySemanticReplayPlaybackTimeline playback)
        {
            Playback = playback ?? throw new ArgumentNullException(
                nameof(playback));
            var projected = new List<ReplayCombatTranscriptEntry>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            int attacks = 0;
            int discharges = 0;
            int launches = 0;
            int impacts = 0;
            int hits = 0;
            int misses = 0;
            int blocked = 0;
            int reactions = 0;
            int wounds = 0;
            int incapacitations = 0;

            foreach (GameplaySemanticReplayPlaybackFrame playbackFrame in
                playback.Frames)
            {
                GameplaySemanticReplayFrame frame = playbackFrame.Frame;
                AttackResolutionRecord actorAttack = ResolveActorAttack(
                    frame.SemanticRecord);
                ActorDroneAttackRecord droneTargetAttack =
                    frame.SemanticRecord as ActorDroneAttackRecord;
                IReadOnlyList<ReplayCombatPresentationEvent> presentationEvents =
                    ReplayCombatPresentationEventProjector.Project(frame);
                bool hasAttackExecution = actorAttack != null
                    || droneTargetAttack != null
                    || ContainsDischarge(presentationEvents);
                if (hasAttackExecution) attacks++;
                if (actorAttack != null)
                {
                    if (actorAttack.Hit) hits++;
                    else misses++;
                }
                else if (droneTargetAttack != null)
                {
                    if (droneTargetAttack.Hit) hits++;
                    else misses++;
                }

                GameplayCombatDiagnosticFormatter.TryFormatSemanticRecord(
                    frame.SemanticRecord,
                    out GameplayDiagnosticProjection diagnostic);
                string attackExecutionId = hasAttackExecution
                    ? "attack-execution:" + frame.Transition.Identity.Sequence
                    : string.Empty;

                foreach (ReplayCombatPresentationEvent presentationEvent in
                    presentationEvents)
                {
                    if (!identities.Add(presentationEvent.CombatEventId))
                        throw new InvalidOperationException(
                            "Replay combat event identity collision: "
                            + presentationEvent.CombatEventId);
                    switch (presentationEvent.Kind)
                    {
                        case ReplayCombatPresentationEventKind.WeaponDischarge:
                            discharges++;
                            break;
                        case ReplayCombatPresentationEventKind.ProjectileLaunch:
                            launches++;
                            break;
                        case ReplayCombatPresentationEventKind.ProjectileImpact:
                            impacts++;
                            break;
                        case ReplayCombatPresentationEventKind.Reaction:
                            reactions++;
                            break;
                        case ReplayCombatPresentationEventKind.Incapacitation:
                            incapacitations++;
                            break;
                    }
                    if (presentationEvent.Outcome ==
                        ReplayCombatPresentationOutcome.Blocked)
                        blocked++;

                    ResolveDisplay(
                        presentationEvent,
                        diagnostic,
                        actorAttack,
                        droneTargetAttack,
                        out string title,
                        out IReadOnlyList<string> lines);
                    float eventSeconds = playbackFrame.StartSeconds
                        + playbackFrame.DurationSeconds
                        * presentationEvent.NormalizedTime;
                    float normalizedTime = playback.TotalDurationSeconds <= 0f
                        ? 0f
                        : eventSeconds / playback.TotalDurationSeconds;
                    projected.Add(new ReplayCombatTranscriptEntry(
                        projected.Count + 1L,
                        presentationEvent.CombatEventId,
                        attackExecutionId,
                        presentationEvent.TransitionSequence,
                        presentationEvent.EventOrdinal,
                        eventSeconds,
                        normalizedTime,
                        MapEventKind(presentationEvent.Kind),
                        presentationEvent.ShooterKind,
                        presentationEvent.ShooterId,
                        presentationEvent.TargetKind,
                        presentationEvent.TargetId,
                        presentationEvent.PresentationId,
                        presentationEvent.ProjectileId,
                        presentationEvent.Outcome,
                        actorAttack?.HitRegion,
                        actorAttack?.TargetWoundsBefore.WoundCount ?? -1,
                        actorAttack?.TargetWoundsAfter.WoundCount ?? -1,
                        title,
                        lines));
                }

                wounds += AppendWoundEntries(
                    frame,
                    playbackFrame,
                    playback.TotalDurationSeconds,
                    presentationEvents.Count,
                    attackExecutionId,
                    projected,
                    identities);
            }

            entries = projected.AsReadOnly();
            Totals = new ReplayCombatDiagnosticTotals(
                attacks,
                discharges,
                launches,
                impacts,
                hits,
                misses,
                blocked,
                reactions,
                wounds,
                incapacitations);
        }

        public GameplaySemanticReplayPlaybackTimeline Playback { get; }
        public IReadOnlyList<ReplayCombatTranscriptEntry> Entries => entries;
        public ReplayCombatDiagnosticTotals Totals { get; }

        public IReadOnlyList<ReplayCombatTranscriptEntry> GetEntriesAtOrBefore(
            float timeSeconds)
        {
            if (float.IsNaN(timeSeconds) || float.IsInfinity(timeSeconds))
                throw new ArgumentOutOfRangeException(nameof(timeSeconds));
            var visible = new List<ReplayCombatTranscriptEntry>();
            foreach (ReplayCombatTranscriptEntry entry in entries)
            {
                if (entry.TimeSeconds > timeSeconds) break;
                visible.Add(entry);
            }
            return visible.Count == 0
                ? Array.Empty<ReplayCombatTranscriptEntry>()
                : visible.AsReadOnly();
        }

        private static bool ContainsDischarge(
            IReadOnlyList<ReplayCombatPresentationEvent> events)
        {
            foreach (ReplayCombatPresentationEvent presentationEvent in events)
                if (presentationEvent.Kind ==
                    ReplayCombatPresentationEventKind.WeaponDischarge)
                    return true;
            return false;
        }

        private static ReplayCombatTranscriptEventKind MapEventKind(
            ReplayCombatPresentationEventKind kind)
        {
            switch (kind)
            {
                case ReplayCombatPresentationEventKind.WeaponDischarge:
                    return ReplayCombatTranscriptEventKind.WeaponDischarge;
                case ReplayCombatPresentationEventKind.ProjectileLaunch:
                    return ReplayCombatTranscriptEventKind.ProjectileLaunch;
                case ReplayCombatPresentationEventKind.ProjectileImpact:
                    return ReplayCombatTranscriptEventKind.ProjectileImpact;
                case ReplayCombatPresentationEventKind.Reaction:
                    return ReplayCombatTranscriptEventKind.Reaction;
                case ReplayCombatPresentationEventKind.Incapacitation:
                    return ReplayCombatTranscriptEventKind.Incapacitation;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static AttackResolutionRecord ResolveActorAttack(
            object semanticRecord)
        {
            if (semanticRecord is DroneAttackRecord drone
                && drone.Consequence is AttackResolutionRecord droneAttack)
                return droneAttack;
            if (!(semanticRecord is GameplayActionRecord action)) return null;
            foreach (GameplayActionOutcome outcome in action.Outcomes)
                if (outcome is AttackResolvedActionOutcome resolved)
                    return resolved.Attack;
            return null;
        }

        private static int AppendWoundEntries(
            GameplaySemanticReplayFrame frame,
            GameplaySemanticReplayPlaybackFrame playbackFrame,
            float totalDurationSeconds,
            int firstEventOrdinal,
            string attackExecutionId,
            ICollection<ReplayCombatTranscriptEntry> transcriptEntries,
            ISet<string> identities)
        {
            int count = 0;
            foreach (GameplayActorSnapshot resulting in
                frame.Resulting.Session.Actors)
            {
                if (!TryFindActor(
                    frame.Previous.Session.Actors,
                    resulting.ActorId,
                    out GameplayActorSnapshot previous))
                    continue;
                int totalDelta = Math.Max(
                    0,
                    resulting.Wounds.WoundCount
                        - previous.Wounds.WoundCount);
                if (totalDelta == 0) continue;
                int represented = 0;
                foreach (TargetRegionId region in Enum.GetValues(
                    typeof(TargetRegionId)))
                {
                    int regionDelta = Math.Max(
                        0,
                        resulting.Wounds.GetWoundCount(region)
                            - previous.Wounds.GetWoundCount(region));
                    for (int index = 0; index < regionDelta; index++)
                    {
                        AppendWoundEntry(
                            frame,
                            playbackFrame,
                            totalDurationSeconds,
                            firstEventOrdinal + count,
                            attackExecutionId,
                            previous.Wounds.WoundCount + represented,
                            previous.Wounds.WoundCount + represented + 1,
                            region,
                            resulting.ActorId,
                            transcriptEntries,
                            identities);
                        represented++;
                        count++;
                    }
                }
                int unlocalizedDelta = Math.Max(
                    0,
                    resulting.Wounds.UnlocalizedWounds
                        - previous.Wounds.UnlocalizedWounds);
                for (int index = 0; index < unlocalizedDelta; index++)
                {
                    AppendWoundEntry(
                        frame,
                        playbackFrame,
                        totalDurationSeconds,
                        firstEventOrdinal + count,
                        attackExecutionId,
                        previous.Wounds.WoundCount + represented,
                        previous.Wounds.WoundCount + represented + 1,
                        region: null,
                        resulting.ActorId,
                        transcriptEntries,
                        identities);
                    represented++;
                    count++;
                }
                if (represented != totalDelta)
                    throw new InvalidOperationException(
                        $"Replay transition {frame.Transition.Identity.Sequence} "
                        + $"cannot classify {totalDelta - represented} wound(s) "
                        + $"for actor '{resulting.ActorId}'.");
            }
            return count;
        }

        private static void AppendWoundEntry(
            GameplaySemanticReplayFrame frame,
            GameplaySemanticReplayPlaybackFrame playbackFrame,
            float totalDurationSeconds,
            int eventOrdinal,
            string attackExecutionId,
            int woundsBefore,
            int woundsAfter,
            TargetRegionId? region,
            string targetId,
            ICollection<ReplayCombatTranscriptEntry> transcriptEntries,
            ISet<string> identities)
        {
            long transitionSequence = frame.Transition.Identity.Sequence;
            string shooterId = ResolveWoundSource(frame);
            ReplayCombatPresentationSubjectKind shooterKind =
                ResolveSubjectKind(frame, shooterId);
            string combatEventId = "replay-combat:" + transitionSequence + ":"
                + eventOrdinal + ":WoundApplied:" + shooterKind + ":"
                + shooterId + ":Actor:" + targetId + ":";
            if (!identities.Add(combatEventId))
                throw new InvalidOperationException(
                    "Replay combat event identity collision: " + combatEventId);
            float normalizedFrameTime = ResolveWoundEventTime(frame);
            float eventSeconds = playbackFrame.StartSeconds
                + playbackFrame.DurationSeconds * normalizedFrameTime;
            float normalizedReplayTime = totalDurationSeconds <= 0f
                ? 0f
                : eventSeconds / totalDurationSeconds;
            transcriptEntries.Add(new ReplayCombatTranscriptEntry(
                transcriptEntries.Count + 1L,
                combatEventId,
                attackExecutionId,
                transitionSequence,
                eventOrdinal,
                eventSeconds,
                normalizedReplayTime,
                ReplayCombatTranscriptEventKind.WoundApplied,
                shooterKind,
                shooterId,
                ReplayCombatPresentationSubjectKind.Actor,
                targetId,
                presentationId: string.Empty,
                projectileId: string.Empty,
                ReplayCombatPresentationOutcome.Hit,
                region,
                woundsBefore,
                woundsAfter,
                targetId + " WOUNDED",
                new[]
                {
                    "REGION - " + (region.HasValue
                        ? region.Value.ToString()
                        : "UNLOCALIZED"),
                    "WOUNDS - " + woundsBefore + " -> " + woundsAfter,
                }));
        }

        private static float ResolveWoundEventTime(
            GameplaySemanticReplayFrame frame)
        {
            if (frame.SemanticRecord is ProjectileAdvanceRecord advance
                && advance.Resulting.Impact != null)
                return GameplaySemanticReplayPresentationTiming
                    .GetProjectileImpactProgress(advance);
            if (frame.SemanticRecord is GameplayActionRecord action)
                return GameplaySemanticReplayPresentationTiming
                    .GetActionResolutionProgress(action);
            return GameplaySemanticReplayPresentationTiming
                .ActionResolutionProgress;
        }

        private static string ResolveWoundSource(
            GameplaySemanticReplayFrame frame)
        {
            AttackResolutionRecord attack = ResolveActorAttack(
                frame.SemanticRecord);
            if (attack != null) return attack.AttackerId;
            return frame.Transition.Identity.ActorId;
        }

        private static ReplayCombatPresentationSubjectKind ResolveSubjectKind(
            GameplaySemanticReplayFrame frame,
            string subjectId)
        {
            foreach (GameplayActorSnapshot actor in frame.Previous.Session.Actors)
                if (string.Equals(actor.ActorId, subjectId,
                    StringComparison.Ordinal))
                    return ReplayCombatPresentationSubjectKind.Actor;
            foreach (DroneSnapshot drone in frame.Previous.Drones)
                if (string.Equals(drone.DroneId, subjectId,
                    StringComparison.Ordinal))
                    return ReplayCombatPresentationSubjectKind.Drone;
            return ReplayCombatPresentationSubjectKind.World;
        }

        private static bool TryFindActor(
            IEnumerable<GameplayActorSnapshot> actors,
            string actorId,
            out GameplayActorSnapshot found)
        {
            foreach (GameplayActorSnapshot actor in actors)
                if (string.Equals(actor.ActorId, actorId,
                    StringComparison.Ordinal))
                {
                    found = actor;
                    return true;
                }
            found = default;
            return false;
        }

        private static void ResolveDisplay(
            ReplayCombatPresentationEvent presentationEvent,
            GameplayDiagnosticProjection diagnostic,
            AttackResolutionRecord actorAttack,
            ActorDroneAttackRecord droneTargetAttack,
            out string title,
            out IReadOnlyList<string> lines)
        {
            if ((presentationEvent.Kind ==
                    ReplayCombatPresentationEventKind.WeaponDischarge
                || presentationEvent.Kind ==
                    ReplayCombatPresentationEventKind.ProjectileLaunch
                || presentationEvent.Kind ==
                    ReplayCombatPresentationEventKind.ProjectileImpact)
                && diagnostic != null)
            {
                title = diagnostic.Title;
                lines = diagnostic.Lines;
                return;
            }

            switch (presentationEvent.Kind)
            {
                case ReplayCombatPresentationEventKind.Reaction:
                    title = presentationEvent.TargetId + " REACTS";
                    lines = new[] { FormatOutcome(actorAttack, droneTargetAttack) };
                    return;
                case ReplayCombatPresentationEventKind.Incapacitation:
                    title = presentationEvent.TargetId + " INCAPACITATED";
                    lines = new[] { "LIFE STATE - INCAPACITATED" };
                    return;
                case ReplayCombatPresentationEventKind.ProjectileImpact:
                    title = presentationEvent.ProjectileId + " IMPACTS";
                    lines = new[] { "TARGET - " + presentationEvent.TargetId };
                    return;
                default:
                    title = presentationEvent.ShooterId + " "
                        + presentationEvent.Kind;
                    lines = new[] { FormatOutcome(actorAttack, droneTargetAttack) };
                    return;
            }
        }

        private static string FormatOutcome(
            AttackResolutionRecord actorAttack,
            ActorDroneAttackRecord droneTargetAttack)
        {
            if (actorAttack != null)
                return actorAttack.Hit
                    ? "RESULT - HIT " + actorAttack.HitRegion
                        + " - WOUNDS "
                        + actorAttack.TargetWoundsBefore.WoundCount + " -> "
                        + actorAttack.TargetWoundsAfter.WoundCount
                    : "RESULT - MISS";
            if (droneTargetAttack != null)
                return droneTargetAttack.Hit
                    ? "RESULT - HIT"
                    : "RESULT - MISS";
            return "RESULT - PRESENTED";
        }
    }
}
