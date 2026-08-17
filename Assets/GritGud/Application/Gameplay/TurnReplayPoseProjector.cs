using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public static class TurnReplayPoseProjector
    {
        public static IReadOnlyDictionary<string, GameplayActorPose> Project(
            TurnReplayWindow window,
            IReadOnlyDictionary<string, GameplayActorPose> finalPoses,
            float playhead)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));
            if (finalPoses == null)
                throw new ArgumentNullException(nameof(finalPoses));
            var poses = new Dictionary<string, GameplayActorPose>(
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, GameplayActorPose> entry in finalPoses)
                poses.Add(entry.Key, entry.Value);

            for (int segmentIndex = window.Segments.Count - 1;
                segmentIndex >= 0;
                segmentIndex--)
            {
                IReadOnlyList<GameplayJournalEntry> entries =
                    window.Segments[segmentIndex].Entries;
                for (int entryIndex = entries.Count - 1;
                    entryIndex >= 0;
                    entryIndex--)
                {
                    Reverse(entries[entryIndex], poses);
                }
            }

            float clamped = Math.Max(0f, Math.Min(window.Segments.Count, playhead));
            int completeSegments = Math.Min(
                window.Segments.Count,
                (int)Math.Floor(clamped));
            for (int index = 0; index < completeSegments; index++)
                ApplyAll(window.Segments[index].Entries, poses);

            if (completeSegments < window.Segments.Count)
            {
                ApplyPartial(
                    window.Segments[completeSegments].Entries,
                    poses,
                    clamped - completeSegments);
            }
            return poses;
        }

        private static void ApplyPartial(
            IReadOnlyList<GameplayJournalEntry> entries,
            IDictionary<string, GameplayActorPose> poses,
            float progress)
        {
            var poseEntries = new List<GameplayJournalEntry>();
            foreach (GameplayJournalEntry entry in entries)
                if (IsPoseEntry(entry))
                    poseEntries.Add(entry);
            if (poseEntries.Count == 0)
                return;
            float eventPlayhead = Math.Max(0f, Math.Min(1f, progress))
                * poseEntries.Count;
            int completeEvents = Math.Min(
                poseEntries.Count,
                (int)Math.Floor(eventPlayhead));
            for (int index = 0; index < completeEvents; index++)
                Apply(poseEntries[index], poses);
            if (completeEvents < poseEntries.Count)
                ApplyInterpolated(
                    poseEntries[completeEvents],
                    poses,
                    eventPlayhead - completeEvents);
        }

        private static void ApplyAll(
            IReadOnlyList<GameplayJournalEntry> entries,
            IDictionary<string, GameplayActorPose> poses)
        {
            foreach (GameplayJournalEntry entry in entries)
                Apply(entry, poses);
        }

        private static bool IsPoseEntry(GameplayJournalEntry entry) =>
            entry is MovementRouteCommittedJournalEntry
            || entry is StanceChangedJournalEntry
            || entry is DisplacementResolvedJournalEntry;

        private static void Reverse(
            GameplayJournalEntry entry,
            IDictionary<string, GameplayActorPose> poses)
        {
            if (entry is MovementRouteCommittedJournalEntry movement)
            {
                poses[movement.Route.ActorId] = movement.Route.OriginPose;
            }
            else if (entry is StanceChangedJournalEntry stance)
            {
                poses[stance.StanceChange.ActorId] =
                    stance.StanceChange.PreviousPose;
            }
            else if (entry is DisplacementResolvedJournalEntry displacement
                && displacement.Displacement.Request.SubjectKind
                    == DisplacementSubjectKind.Combatant
                && poses.TryGetValue(
                    displacement.Displacement.Request.SubjectId,
                    out GameplayActorPose pose))
            {
                poses[displacement.Displacement.Request.SubjectId] =
                    WithPosition(
                        pose,
                        displacement.Displacement.PreviousPosition);
            }
            else if (entry is DisplacementResolvedJournalEntry pinning
                && pinning.Displacement.PinTransition != null)
            {
                ActorPinTransition transition =
                    pinning.Displacement.PinTransition;
                poses[transition.ActorId] = transition.PreviousPose;
            }
        }

        private static void Apply(
            GameplayJournalEntry entry,
            IDictionary<string, GameplayActorPose> poses)
        {
            ApplyInterpolated(entry, poses, 1f);
        }

        private static void ApplyInterpolated(
            GameplayJournalEntry entry,
            IDictionary<string, GameplayActorPose> poses,
            float progress)
        {
            float clamped = Math.Max(0f, Math.Min(1f, progress));
            if (entry is MovementRouteCommittedJournalEntry movement)
            {
                poses[movement.Route.ActorId] = SampleRoute(
                    movement.Route,
                    clamped);
            }
            else if (entry is StanceChangedJournalEntry stance)
            {
                poses[stance.StanceChange.ActorId] = clamped < 1f
                    ? stance.StanceChange.PreviousPose
                    : stance.StanceChange.ResultingPose;
            }
            else if (entry is DisplacementResolvedJournalEntry displacement
                && displacement.Displacement.Request.SubjectKind
                    == DisplacementSubjectKind.Combatant
                && poses.TryGetValue(
                    displacement.Displacement.Request.SubjectId,
                    out GameplayActorPose pose))
            {
                GameplayPosition from = displacement.Displacement.PreviousPosition;
                GameplayPosition to = displacement.Displacement.ResultingPosition;
                poses[displacement.Displacement.Request.SubjectId] = WithPosition(
                    pose,
                    Lerp(from, to, clamped));
            }
            else if (entry is DisplacementResolvedJournalEntry pinning
                && pinning.Displacement.PinTransition != null)
            {
                ActorPinTransition transition =
                    pinning.Displacement.PinTransition;
                poses[transition.ActorId] = clamped < 1f
                    ? transition.PreviousPose
                    : transition.ResultingPose;
            }
        }

        private static GameplayActorPose SampleRoute(
            MovementRouteRecord route,
            float progress)
        {
            float targetSeconds = route.TotalPlaybackDurationSeconds
                * progress;
            float traversedSeconds = 0f;
            foreach (MovementRouteSegmentRecord segment in route.Segments)
            {
                float duration = segment.PlaybackDurationSeconds;
                if (traversedSeconds + duration <= targetSeconds
                    && !ReferenceEquals(segment, route.Segments[
                        route.Segments.Count - 1]))
                {
                    traversedSeconds += duration;
                    continue;
                }
                float segmentProgress = duration <= 0f
                    ? 1f
                    : (targetSeconds - traversedSeconds) / duration;
                float facing = CalculateFacing(
                    segment.From,
                    segment.To,
                    route.OriginPose.FacingDegrees);
                return new GameplayActorPose(
                    segment.Sample(segmentProgress),
                    facing,
                    route.OriginPose.Stance);
            }
            return new GameplayActorPose(
                route.Destination,
                route.FinalFacingDegrees,
                route.OriginPose.Stance);
        }

        private static GameplayActorPose WithPosition(
            GameplayActorPose pose,
            GameplayPosition position) =>
            new GameplayActorPose(position, pose.FacingDegrees, pose.Stance);

        private static GameplayPosition Lerp(
            GameplayPosition from,
            GameplayPosition to,
            float progress) =>
            new GameplayPosition(
                from.X + ((to.X - from.X) * progress),
                from.Y + ((to.Y - from.Y) * progress),
                from.Z + ((to.Z - from.Z) * progress));

        private static float CalculateFacing(
            GameplayPosition from,
            GameplayPosition to,
            float fallback)
        {
            double x = to.X - from.X;
            double z = to.Z - from.Z;
            if (Math.Abs(x) <= 0.0001 && Math.Abs(z) <= 0.0001)
                return fallback;
            float degrees = (float)(Math.Atan2(x, z) * (180d / Math.PI));
            return degrees < 0f ? degrees + 360f : degrees;
        }
    }
}
