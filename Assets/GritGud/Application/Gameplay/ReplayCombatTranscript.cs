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
        InjuryApplied = 4,
        WoundApplied = InjuryApplied,
        Incapacitation = 5,
        ExplosiveThrow = 6,
        SystemicChange = 7,
        Death = 8,
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
            IEnumerable<string> lines,
            InjuryRecord injury = null,
            ActorCapabilityState capabilitiesBefore = null,
            ActorCapabilityState capabilitiesAfter = null,
            ActorPhysiologyState physiologyBefore = null,
            ActorPhysiologyState physiologyAfter = null,
            ActorLifeState? lifeStateBefore = null,
            ActorLifeState? lifeStateAfter = null)
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
            if ((capabilitiesBefore == null) != (capabilitiesAfter == null))
                throw new ArgumentException(
                    "Replay capability changes require before and after state.");
            if ((physiologyBefore == null) != (physiologyAfter == null))
                throw new ArgumentException(
                    "Replay physiology changes require before and after state.");
            if (lifeStateBefore.HasValue != lifeStateAfter.HasValue)
                throw new ArgumentException(
                    "Replay life-state changes require before and after state.");
            if ((lifeStateBefore.HasValue
                    && !Enum.IsDefined(
                        typeof(ActorLifeState),
                        lifeStateBefore.Value))
                || (lifeStateAfter.HasValue
                    && !Enum.IsDefined(
                        typeof(ActorLifeState),
                        lifeStateAfter.Value)))
                throw new ArgumentOutOfRangeException(nameof(lifeStateBefore));
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
            Injury = injury;
            CapabilitiesBefore = capabilitiesBefore;
            CapabilitiesAfter = capabilitiesAfter;
            PhysiologyBefore = physiologyBefore;
            PhysiologyAfter = physiologyAfter;
            LifeStateBefore = lifeStateBefore;
            LifeStateAfter = lifeStateAfter;
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
        public InjuryRecord Injury { get; }
        public string InjuryId => Injury?.InjuryId ?? string.Empty;
        public string ImpactCombatEventId =>
            Injury?.CombatEventId ?? string.Empty;
        public ActorCapabilityState CapabilitiesBefore { get; }
        public ActorCapabilityState CapabilitiesAfter { get; }
        public ActorPhysiologyState PhysiologyBefore { get; }
        public ActorPhysiologyState PhysiologyAfter { get; }
        public ActorLifeState? LifeStateBefore { get; }
        public ActorLifeState? LifeStateAfter { get; }
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
            int incapacitations,
            int systemicChanges,
            int deaths)
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
            SystemicChanges = systemicChanges;
            Deaths = deaths;
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
        public int InjuriesApplied => WoundsApplied;
        public int Incapacitations { get; }
        public int SystemicChanges { get; }
        public int Deaths { get; }
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
            int systemicChanges = 0;
            int deaths = 0;

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
                        case ReplayCombatPresentationEventKind.DroneCrashImpact:
                            impacts++;
                            break;
                        case ReplayCombatPresentationEventKind.Reaction:
                            reactions++;
                            break;
                        case ReplayCombatPresentationEventKind.Incapacitation:
                            incapacitations++;
                            break;
                        case ReplayCombatPresentationEventKind.Death:
                            deaths++;
                            break;
                        case ReplayCombatPresentationEventKind
                                .ThrownExplosiveRelease:
                            launches++;
                            break;
                        case ReplayCombatPresentationEventKind
                                .ThrownExplosiveImpact:
                            impacts++;
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

                wounds += AppendInjuryEntries(
                    frame,
                    playbackFrame,
                    playback.TotalDurationSeconds,
                    presentationEvents.Count,
                    attackExecutionId,
                    projected,
                    identities,
                    out int frameSystemicChanges);
                systemicChanges += frameSystemicChanges;
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
                incapacitations,
                systemicChanges,
                deaths);
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
                case ReplayCombatPresentationEventKind.DroneCrashImpact:
                    return ReplayCombatTranscriptEventKind.ProjectileImpact;
                case ReplayCombatPresentationEventKind.Reaction:
                    return ReplayCombatTranscriptEventKind.Reaction;
                case ReplayCombatPresentationEventKind.Incapacitation:
                    return ReplayCombatTranscriptEventKind.Incapacitation;
                case ReplayCombatPresentationEventKind.ThrownExplosiveRelease:
                    return ReplayCombatTranscriptEventKind.ExplosiveThrow;
                case ReplayCombatPresentationEventKind.ThrownExplosiveImpact:
                    return ReplayCombatTranscriptEventKind.ProjectileImpact;
                case ReplayCombatPresentationEventKind.Death:
                    return ReplayCombatTranscriptEventKind.Death;
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

        private static int AppendInjuryEntries(
            GameplaySemanticReplayFrame frame,
            GameplaySemanticReplayPlaybackFrame playbackFrame,
            float totalDurationSeconds,
            int firstEventOrdinal,
            string attackExecutionId,
            ICollection<ReplayCombatTranscriptEntry> transcriptEntries,
            ISet<string> identities,
            out int systemicChanges)
        {
            int injuryCount = 0;
            int eventOffset = 0;
            systemicChanges = 0;
            var actorsWithNewInjuries = new HashSet<string>(
                StringComparer.Ordinal);
            foreach (GameplayActorSnapshot resulting in
                frame.Resulting.Session.Actors)
            {
                if (!TryFindActor(
                    frame.Previous.Session.Actors,
                    resulting.ActorId,
                    out GameplayActorSnapshot previous))
                    continue;
                var previousInjuries = new Dictionary<string, int>(
                    StringComparer.Ordinal);
                foreach (InjuryRecord injury in previous.Injuries.Injuries)
                {
                    string key = GetInjuryComparisonKey(injury);
                    previousInjuries.TryGetValue(key, out int existing);
                    previousInjuries[key] = existing + 1;
                }
                int represented = 0;
                foreach (InjuryRecord injury in resulting.Injuries.Injuries)
                {
                    string key = GetInjuryComparisonKey(injury);
                    if (previousInjuries.TryGetValue(key, out int existing)
                        && existing > 0)
                    {
                        previousInjuries[key] = existing - 1;
                        continue;
                    }
                    AppendInjuryEntry(
                        frame,
                        playbackFrame,
                        totalDurationSeconds,
                        firstEventOrdinal + eventOffset,
                        attackExecutionId,
                        previous.Wounds.WoundCount + represented,
                        Math.Min(
                            resulting.Wounds.WoundCount,
                            previous.Wounds.WoundCount + represented + 1),
                        injury,
                        previous,
                        resulting,
                        transcriptEntries,
                        identities);
                    represented++;
                    injuryCount++;
                    eventOffset++;
                }
                if (represented > 0)
                    actorsWithNewInjuries.Add(resulting.ActorId);
            }
            foreach (GameplayActorSnapshot resulting in
                frame.Resulting.Session.Actors)
            {
                if (actorsWithNewInjuries.Contains(resulting.ActorId)
                    || !TryFindActor(
                        frame.Previous.Session.Actors,
                        resulting.ActorId,
                        out GameplayActorSnapshot previous)
                    || (PhysiologyMatches(
                            previous.Physiology,
                            resulting.Physiology)
                        && previous.LifeState == resulting.LifeState))
                    continue;
                AppendSystemicEntry(
                    frame,
                    playbackFrame,
                    totalDurationSeconds,
                    firstEventOrdinal + eventOffset,
                    previous,
                    resulting,
                    transcriptEntries,
                    identities);
                systemicChanges++;
                eventOffset++;
            }
            return injuryCount;
        }

        private static string GetInjuryComparisonKey(InjuryRecord injury)
        {
            if (!injury.InjuryId.StartsWith(
                    "legacy-injury:",
                    StringComparison.Ordinal))
                return injury.InjuryId;
            return "legacy:" + injury.Region + ":" + injury.Mechanism + ":"
                + injury.Severity + ":" + injury.StructuralDamage + ":"
                + injury.SystemicTraumaContribution + ":"
                + injury.MotorLoss + ":" + injury.SensoryLoss + ":"
                + injury.BleedRate + ":" + injury.VitalDamage;
        }

        private static void AppendInjuryEntry(
            GameplaySemanticReplayFrame frame,
            GameplaySemanticReplayPlaybackFrame playbackFrame,
            float totalDurationSeconds,
            int eventOrdinal,
            string attackExecutionId,
            int woundsBefore,
            int woundsAfter,
            InjuryRecord injury,
            GameplayActorSnapshot previous,
            GameplayActorSnapshot resulting,
            ICollection<ReplayCombatTranscriptEntry> transcriptEntries,
            ISet<string> identities)
        {
            long transitionSequence = frame.Transition.Identity.Sequence;
            ResolveInjuryContext(
                frame,
                injury,
                out string shooterId,
                out string presentationId);
            ReplayCombatPresentationSubjectKind shooterKind =
                ResolveSubjectKind(frame, shooterId);
            string combatEventId = "replay-injury:" + transitionSequence + ":"
                + eventOrdinal + ":" + injury.InjuryId;
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
                ReplayCombatTranscriptEventKind.InjuryApplied,
                shooterKind,
                shooterId,
                ReplayCombatPresentationSubjectKind.Actor,
                resulting.ActorId,
                presentationId,
                projectileId: string.Empty,
                ReplayCombatPresentationOutcome.Hit,
                injury.Region,
                woundsBefore,
                woundsAfter,
                resulting.ActorId + " INJURED",
                FormatInjuryLines(injury, previous, resulting),
                injury,
                previous.Capabilities,
                resulting.Capabilities,
                previous.Physiology,
                resulting.Physiology,
                previous.LifeState,
                resulting.LifeState));
        }

        private static void AppendSystemicEntry(
            GameplaySemanticReplayFrame frame,
            GameplaySemanticReplayPlaybackFrame playbackFrame,
            float totalDurationSeconds,
            int eventOrdinal,
            GameplayActorSnapshot previous,
            GameplayActorSnapshot resulting,
            ICollection<ReplayCombatTranscriptEntry> transcriptEntries,
            ISet<string> identities)
        {
            long transitionSequence = frame.Transition.Identity.Sequence;
            string combatEventId = "replay-systemic:" + transitionSequence
                + ":" + eventOrdinal + ":" + resulting.ActorId;
            if (!identities.Add(combatEventId))
                throw new InvalidOperationException(
                    "Replay combat event identity collision: " + combatEventId);
            float eventSeconds = playbackFrame.StartSeconds
                + playbackFrame.DurationSeconds;
            float normalizedReplayTime = totalDurationSeconds <= 0f
                ? 0f
                : eventSeconds / totalDurationSeconds;
            transcriptEntries.Add(new ReplayCombatTranscriptEntry(
                transcriptEntries.Count + 1L,
                combatEventId,
                attackExecutionId: string.Empty,
                transitionSequence,
                eventOrdinal,
                eventSeconds,
                normalizedReplayTime,
                ReplayCombatTranscriptEventKind.SystemicChange,
                ReplayCombatPresentationSubjectKind.Actor,
                resulting.ActorId,
                ReplayCombatPresentationSubjectKind.Actor,
                resulting.ActorId,
                presentationId: string.Empty,
                projectileId: string.Empty,
                ReplayCombatPresentationOutcome.None,
                hitRegion: null,
                woundsBefore: previous.Wounds.WoundCount,
                woundsAfter: resulting.Wounds.WoundCount,
                resulting.ActorId + " SYSTEMIC CONDITION",
                FormatSystemicLines(previous, resulting),
                injury: null,
                previous.Capabilities,
                resulting.Capabilities,
                previous.Physiology,
                resulting.Physiology,
                previous.LifeState,
                resulting.LifeState));
        }

        private static IReadOnlyList<string> FormatInjuryLines(
            InjuryRecord injury,
            GameplayActorSnapshot previous,
            GameplayActorSnapshot resulting)
        {
            var lines = new List<string>
            {
                "REGION - " + (injury.Region.HasValue
                    ? injury.Region.Value.ToString()
                    : "UNLOCALIZED"),
                "INJURY - " + injury.Mechanism.ToString().ToUpperInvariant()
                    + " - SEVERITY " + injury.Severity,
                "TISSUE - STRUCTURAL " + injury.StructuralDamage
                    + " - MOTOR " + injury.MotorLoss
                    + " - SENSORY " + injury.SensoryLoss
                    + " - BLEED " + injury.BleedRate,
                "SYSTEMIC TRAUMA - " + previous.Injuries.SystemicTrauma
                    + " + " + injury.SystemicTraumaContribution + " -> "
                    + resulting.Injuries.SystemicTrauma,
                FormatCapabilityChange(
                    previous.Capabilities,
                    resulting.Capabilities),
                FormatPhysiologyChange(
                    previous.Physiology,
                    resulting.Physiology),
            };
            if (previous.LifeState != resulting.LifeState)
                lines.Add("LIFE STATE - " + previous.LifeState + " -> "
                    + resulting.LifeState);
            lines.Add("COMPATIBILITY WOUNDS - "
                + previous.Wounds.WoundCount + " -> "
                + resulting.Wounds.WoundCount);
            return lines.AsReadOnly();
        }

        private static IReadOnlyList<string> FormatSystemicLines(
            GameplayActorSnapshot previous,
            GameplayActorSnapshot resulting)
        {
            var lines = new List<string>
            {
                "SYSTEMIC TRAUMA - " + previous.Injuries.SystemicTrauma
                    + " -> " + resulting.Injuries.SystemicTrauma,
                FormatPhysiologyChange(
                    previous.Physiology,
                    resulting.Physiology),
                FormatCapabilityChange(
                    previous.Capabilities,
                    resulting.Capabilities),
            };
            if (previous.LifeState != resulting.LifeState)
                lines.Add("LIFE STATE - " + previous.LifeState + " -> "
                    + resulting.LifeState);
            return lines.AsReadOnly();
        }

        private static string FormatCapabilityChange(
            ActorCapabilityState previous,
            ActorCapabilityState resulting) =>
            "FUNCTION - MOVE " + previous.MovementCapacity + " -> "
            + resulting.MovementCapacity + " - STAND "
            + previous.StandingCapacity + " -> "
            + resulting.StandingCapacity + " - AIM "
            + previous.AimStability + " -> " + resulting.AimStability
            + " - GRIP " + previous.GripCapacity + " -> "
            + resulting.GripCapacity + " - RELOAD "
            + previous.ReloadCapacity + " -> "
            + resulting.ReloadCapacity + " - THROW "
            + previous.ThrowCapacity + " -> "
            + resulting.ThrowCapacity + " - GAIT "
            + previous.Mobility.Gait + " -> "
            + resulting.Mobility.Gait + " ("
            + resulting.Mobility.ImpairedSide + ")";

        private static string FormatPhysiologyChange(
            ActorPhysiologyState previous,
            ActorPhysiologyState resulting) =>
            "SYSTEMIC - BLOOD " + previous.BloodReserve + " -> "
            + resulting.BloodReserve + " - SHOCK " + previous.Shock
            + " -> " + resulting.Shock + " - CONSCIOUSNESS "
            + previous.Consciousness + " -> " + resulting.Consciousness
            + " - RESPIRATION " + previous.Respiration + " -> "
            + resulting.Respiration;

        private static bool PhysiologyMatches(
            ActorPhysiologyState left,
            ActorPhysiologyState right) =>
            left.BloodReserve == right.BloodReserve
            && left.Shock == right.Shock
            && left.Consciousness == right.Consciousness
            && left.Respiration == right.Respiration;

        private static void ResolveInjuryContext(
            GameplaySemanticReplayFrame frame,
            InjuryRecord injury,
            out string shooterId,
            out string presentationId)
        {
            AttackResolutionRecord attack = ResolveActorAttack(
                frame.SemanticRecord);
            if (attack?.Injury?.Injury != null
                && string.Equals(
                    attack.Injury.Injury.InjuryId,
                    injury.InjuryId,
                    StringComparison.Ordinal))
            {
                shooterId = attack.Injury.Impact.SourceActorId;
                presentationId = attack.Injury.Impact.WeaponId;
                return;
            }
            if (frame.SemanticRecord is ProjectileAdvanceRecord projectile)
            {
                shooterId = projectile.Resulting.Launch.AttackerId;
                presentationId = projectile.Resulting.Launch.ActionId;
                return;
            }
            if (frame.SemanticRecord is GameplayActionRecord action)
                foreach (GameplayActionOutcome outcome in action.Outcomes)
                    if (outcome is ThrownExplosiveActionOutcome thrown)
                    {
                        shooterId = thrown.Record.ThrowerId;
                        presentationId = thrown.Record.Definition.Id;
                        return;
                    }
            foreach (FireFieldSnapshot fire in frame.Previous.FireFields)
                if (injury.CombatEventId.IndexOf(
                        ":" + fire.Field.Id + ":",
                        StringComparison.Ordinal) >= 0)
                {
                    shooterId = fire.Field.SourceActorId;
                    presentationId = fire.Field.SourceItemId;
                    return;
                }
            shooterId = ResolveWoundSource(frame);
            presentationId = string.Empty;
        }

        private static float ResolveWoundEventTime(
            GameplaySemanticReplayFrame frame)
        {
            if (frame.SemanticRecord is ProjectileAdvanceRecord advance
                && advance.Resulting.Impact != null)
                return GameplaySemanticReplayPresentationTiming
                    .GetProjectileImpactProgress(advance);
            if (frame.SemanticRecord is GameplayActionRecord action)
            {
                foreach (GameplayActionOutcome outcome in action.Outcomes)
                    if (outcome is ThrownExplosiveActionOutcome)
                        return GameplayThrownExplosivePresentationTiming
                            .ImpactNormalizedTime;
                return GameplaySemanticReplayPresentationTiming
                    .GetActionResolutionProgress(action);
            }
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
            foreach (SummonedDroneSnapshot drone in frame.Previous.Drones)
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
                    ReplayCombatPresentationEventKind.ProjectileImpact
                || presentationEvent.Kind == ReplayCombatPresentationEventKind
                    .ThrownExplosiveRelease
                || presentationEvent.Kind == ReplayCombatPresentationEventKind
                    .ThrownExplosiveImpact)
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
                case ReplayCombatPresentationEventKind.Death:
                    title = presentationEvent.TargetId + " DEAD";
                    lines = new[] { "LIFE STATE - DEAD" };
                    return;
                case ReplayCombatPresentationEventKind.ProjectileImpact:
                    title = presentationEvent.ProjectileId + " IMPACTS";
                    lines = new[] { "TARGET - " + presentationEvent.TargetId };
                    return;
                case ReplayCombatPresentationEventKind.DroneCrashImpact:
                    title = presentationEvent.ShooterId + " CRASHES";
                    lines = new[]
                    {
                        "IMPACT - " + presentationEvent.Destination,
                    };
                    return;
                case ReplayCombatPresentationEventKind.ThrownExplosiveRelease:
                    title = presentationEvent.ShooterId + " THROWS "
                        + presentationEvent.PresentationId;
                    lines = new[]
                    {
                        "PROJECTILE - " + presentationEvent.ProjectileId,
                    };
                    return;
                case ReplayCombatPresentationEventKind.ThrownExplosiveImpact:
                    title = presentationEvent.PresentationId + " IMPACTS";
                    lines = new[]
                    {
                        "LANDING - " + presentationEvent.Destination,
                    };
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
