using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class TurnReplayWorldStateSample
    {
        public TurnReplayWorldStateSample(
            IReadOnlyDictionary<string, GameplayActorSnapshot> actors,
            IReadOnlyList<DestructiblePropSnapshot> destructibles,
            IReadOnlyList<VehicleMomentumState> vehicles,
            IReadOnlyList<ProjectileFlightSnapshot> projectiles,
            IReadOnlyList<SmokeFieldSnapshot> smokeFields)
        {
            Actors = actors ?? throw new ArgumentNullException(nameof(actors));
            Destructibles = destructibles ?? throw new ArgumentNullException(
                nameof(destructibles));
            Vehicles = vehicles ?? throw new ArgumentNullException(nameof(vehicles));
            Projectiles = projectiles ?? throw new ArgumentNullException(
                nameof(projectiles));
            SmokeFields = smokeFields ?? throw new ArgumentNullException(
                nameof(smokeFields));
        }

        public IReadOnlyDictionary<string, GameplayActorSnapshot> Actors { get; }
        public IReadOnlyList<DestructiblePropSnapshot> Destructibles { get; }
        public IReadOnlyList<VehicleMomentumState> Vehicles { get; }
        public IReadOnlyList<ProjectileFlightSnapshot> Projectiles { get; }
        public IReadOnlyList<SmokeFieldSnapshot> SmokeFields { get; }
    }

    public static class TurnReplayWorldStateSampler
    {
        public static TurnReplayWorldStateSample Sample(
            TurnReplayStateWindow window,
            float playhead)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            float clamped = Math.Max(
                0f,
                Math.Min(window.Replay.Segments.Count, playhead));
            int segmentIndex = Math.Min(
                window.Replay.Segments.Count - 1,
                (int)Math.Floor(clamped));
            float progress = clamped >= window.Replay.Segments.Count
                ? 1f
                : clamped - segmentIndex;
            GameplayCombatStateSnapshot before = segmentIndex == 0
                ? window.Start.State
                : window.SegmentEnds[segmentIndex - 1].State;
            GameplayCombatStateSnapshot after =
                window.SegmentEnds[segmentIndex].State;
            GameplayCombatStateSnapshot persistent = progress >= 1f
                ? after
                : before;

            var finalPoses = new Dictionary<string, GameplayActorPose>(
                StringComparer.Ordinal);
            foreach (GameplayActorSnapshot actor in window.End.State.Session.Actors)
                finalPoses.Add(actor.ActorId, actor.Pose);
            IReadOnlyDictionary<string, GameplayActorPose> poses =
                TurnReplayPoseProjector.Project(
                    window.Replay,
                    finalPoses,
                    clamped);
            var actors = new Dictionary<string, GameplayActorSnapshot>(
                StringComparer.Ordinal);
            foreach (GameplayActorSnapshot actor in persistent.Session.Actors)
                actors.Add(actor.ActorId, CopyActor(actor, poses[actor.ActorId]));

            return new TurnReplayWorldStateSample(
                actors,
                persistent.Destructibles,
                SampleVehicles(before.Vehicles, after.Vehicles, progress),
                SampleProjectiles(before.Projectiles, after.Projectiles, progress),
                persistent.SmokeFields);
        }

        public static TurnReplayWorldStateSample SampleAtTime(
            TurnReplayStateWindow window,
            TurnReplayEventTimeline timeline,
            float timeSeconds)
        {
            if (timeline == null) throw new ArgumentNullException(nameof(timeline));
            if (!ReferenceEquals(window?.Replay, timeline.Replay))
                throw new ArgumentException(
                    "The event timeline must describe the sampled replay window.",
                    nameof(timeline));
            float time = Math.Max(
                0f,
                Math.Min(timeline.TotalDurationSeconds, timeSeconds));
            TurnReplayWorldStateSample boundary = Sample(
                window,
                timeline.ToSegmentPlayhead(time));
            var actors = IndexActors(window.Start.State.Session.Actors);
            var destructibles = IndexDestructibles(window.Start.State.Destructibles);
            var vehicles = IndexVehicles(window.Start.State.Vehicles);
            var projectiles = IndexProjectiles(window.Start.State.Projectiles);
            var smokeFields = IndexSmoke(window.Start.State.SmokeFields);
            TurnReplayTimedEvent activeEvent = null;

            foreach (TurnReplayTimedEvent timedEvent in timeline.Events)
            {
                if (timedEvent.EndSeconds > time)
                {
                    if (timedEvent.DurationSeconds > 0f &&
                        timedEvent.StartSeconds <= time)
                    {
                        activeEvent = timedEvent;
                    }
                    break;
                }
                ApplyEntry(
                    timedEvent.Entry,
                    window,
                    actors,
                    destructibles,
                    vehicles,
                    projectiles,
                    smokeFields);
            }

            if (activeEvent?.Entry is DisplacementResolvedJournalEntry active)
            {
                float normalized =
                    (time - activeEvent.StartSeconds)
                    / activeEvent.DurationSeconds;
                ApplyInterpolatedDisplacement(
                    active.Displacement,
                    normalized,
                    destructibles);
            }

            foreach (KeyValuePair<string, GameplayActorSnapshot> entry in
                new List<KeyValuePair<string, GameplayActorSnapshot>>(actors))
            {
                if (boundary.Actors.TryGetValue(
                    entry.Key,
                    out GameplayActorSnapshot sampled))
                    actors[entry.Key] = CopyActor(entry.Value, sampled.Pose);
            }
            return new TurnReplayWorldStateSample(
                actors,
                SortedValues(destructibles),
                SortedValues(vehicles),
                SortedValues(projectiles),
                SortedSmoke(smokeFields));
        }

        private static void ApplyEntry(
            GameplayJournalEntry entry,
            TurnReplayStateWindow window,
            IDictionary<string, GameplayActorSnapshot> actors,
            IDictionary<string, DestructiblePropSnapshot> destructibles,
            IDictionary<string, VehicleMomentumState> vehicles,
            IDictionary<string, ProjectileFlightSnapshot> projectiles,
            IDictionary<string, SmokeFieldSnapshot> smokeFields)
        {
            if (entry is TurnEndedJournalEntry ended
                && ended.Turn.Kind == GameplayTurnKind.Normal)
            {
                for (int index = 0; index < window.Replay.Segments.Count; index++)
                {
                    TurnReplaySegment segment = window.Replay.Segments[index];
                    if (segment.Entries[segment.Entries.Count - 1].Sequence
                        != entry.Sequence)
                        continue;
                    GameplayCombatStateSnapshot endpoint =
                        window.SegmentEnds[index].State;
                    ReplaceValues(actors, IndexActors(endpoint.Session.Actors));
                    ReplaceValues(
                        destructibles,
                        IndexDestructibles(endpoint.Destructibles));
                    ReplaceValues(vehicles, IndexVehicles(endpoint.Vehicles));
                    ReplaceValues(
                        projectiles,
                        IndexProjectiles(endpoint.Projectiles));
                    ReplaceValues(smokeFields, IndexSmoke(endpoint.SmokeFields));
                    return;
                }
            }
            if (entry is MovementBudgetSpentJournalEntry movement)
            {
                ReplaceActorBudget(actors, movement.ActorId, movement.ResultingBudget);
                return;
            }
            if (entry is ActionResolvedJournalEntry resolved)
            {
                ApplyAction(resolved.Action, window, actors, projectiles, smokeFields);
                return;
            }
            if (entry is DestructibleDamagedJournalEntry damage)
            {
                destructibles[damage.Damage.PropId] = damage.Damage.Resulting;
                return;
            }
            if (entry is DisplacementResolvedJournalEntry displacement)
            {
                ApplyDisplacement(
                    displacement.Displacement,
                    actors,
                    destructibles);
                return;
            }
            if (entry is VehicleMomentumResolvedJournalEntry momentum)
            {
                vehicles[momentum.Momentum.Resulting.VehicleId] =
                    momentum.Momentum.Resulting;
                return;
            }
            if (entry is ProjectileAdvancedJournalEntry advance)
                projectiles[advance.Advance.ProjectileId] = advance.Advance.Resulting;
        }

        private static void ApplyDisplacement(
            DisplacementRecord record,
            IDictionary<string, GameplayActorSnapshot> actors,
            IDictionary<string, DestructiblePropSnapshot> destructibles)
        {
            if (!record.Succeeded)
                return;
            if (record.Request.SubjectKind == DisplacementSubjectKind.Prop)
            {
                DestructiblePropSnapshot prop = destructibles[
                    record.Request.SubjectId];
                destructibles[record.Request.SubjectId] =
                    new DestructiblePropSnapshot(
                        prop.PropId,
                        prop.State,
                        prop.MaximumIntegrity,
                        prop.RemainingIntegrity,
                        record.ResultingPropState.Pose,
                        record.ResultingPropState.Posture,
                        prop.FractureChunkCount,
                        prop.DetachedFractureChunks);
                ActorPinTransition pin = record.PinTransition;
                if (pin != null)
                {
                    GameplayActorSnapshot actor = actors[pin.ActorId];
                    actors[pin.ActorId] = CopyActor(
                        actor,
                        pin.ResultingPose,
                        pinState: pin.ResultingState,
                        replacePin: true);
                }
                return;
            }

            GameplayActorSnapshot displaced = actors[
                record.Request.SubjectId];
            actors[record.Request.SubjectId] = CopyActor(
                displaced,
                new GameplayActorPose(
                    record.ResultingPosition,
                    displaced.Pose.FacingDegrees,
                    displaced.Pose.Stance));
        }

        private static void ApplyInterpolatedDisplacement(
            DisplacementRecord record,
            float normalizedProgress,
            IDictionary<string, DestructiblePropSnapshot> destructibles)
        {
            if (!record.Succeeded ||
                record.Request.SubjectKind != DisplacementSubjectKind.Prop ||
                record.PreviousPropState == null ||
                record.ResultingPropState == null ||
                !destructibles.TryGetValue(
                    record.Request.SubjectId,
                    out DestructiblePropSnapshot prop))
            {
                return;
            }

            float progress = GameplayDisplacementPresentationTiming
                .EvaluateSubjectProgress(
                    record.Request.ActionKind,
                    normalizedProgress);
            GameplayPropPose previous = record.PreviousPropState.Pose;
            GameplayPropPose resulting = record.ResultingPropState.Pose;
            var pose = new GameplayPropPose(
                Lerp(previous.Position, resulting.Position, progress),
                LerpAngle(
                    previous.PitchDegrees,
                    resulting.PitchDegrees,
                    progress),
                LerpAngle(
                    previous.YawDegrees,
                    resulting.YawDegrees,
                    progress),
                LerpAngle(
                    previous.RollDegrees,
                    resulting.RollDegrees,
                    progress));
            destructibles[record.Request.SubjectId] =
                new DestructiblePropSnapshot(
                    prop.PropId,
                    prop.State,
                    prop.MaximumIntegrity,
                    prop.RemainingIntegrity,
                    pose,
                    record.PreviousPropState.Posture,
                    prop.FractureChunkCount,
                    prop.DetachedFractureChunks);
        }

        private static void ApplyAction(
            GameplayActionRecord action,
            TurnReplayStateWindow window,
            IDictionary<string, GameplayActorSnapshot> actors,
            IDictionary<string, ProjectileFlightSnapshot> projectiles,
            IDictionary<string, SmokeFieldSnapshot> smokeFields)
        {
            ReplaceActorBudget(actors, action.Request.ActorId, action.ResultingBudget);
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (outcome is AttackResolvedActionOutcome attack)
                {
                    ReplaceActorWounds(
                        actors,
                        attack.Attack.TargetId,
                        attack.Attack.TargetWoundsAfter);
                }
                else if (outcome is EquipmentChangedActionOutcome equipment)
                {
                    ReplaceActorEquipment(
                        actors,
                        window,
                        equipment.Change.ActorId,
                        equipment.Change.ResultingEquippedItemId);
                }
                else if (outcome is InventoryQuantityChangedActionOutcome quantity)
                {
                    ReplaceInventoryQuantity(actors, quantity.Change);
                }
                else if (outcome is ProjectileLaunchedActionOutcome launched)
                {
                    ProjectileLaunchRecord launch = launched.Launch;
                    projectiles[launch.ProjectileId] = new ProjectileFlightSnapshot(
                        launch,
                        launch.Origin,
                        0f,
                        0f,
                        ProjectileFlightStatus.InFlight);
                }
                else if (outcome is ThrownExplosiveActionOutcome thrown
                    && thrown.Record.SmokeField != null)
                {
                    SmokeFieldRecord field = thrown.Record.SmokeField;
                    smokeFields[field.Id] = new SmokeFieldSnapshot(field, 1f);
                }
            }
        }

        private static IReadOnlyList<ProjectileFlightSnapshot> SampleProjectiles(
            IReadOnlyList<ProjectileFlightSnapshot> before,
            IReadOnlyList<ProjectileFlightSnapshot> after,
            float progress)
        {
            if (progress >= 1f) return after;
            var beforeIndex = new Dictionary<string, ProjectileFlightSnapshot>(
                StringComparer.Ordinal);
            foreach (ProjectileFlightSnapshot flight in before)
                beforeIndex.Add(flight.ProjectileId, flight);
            var result = new List<ProjectileFlightSnapshot>();
            foreach (ProjectileFlightSnapshot target in after)
            {
                ProjectileFlightSnapshot source;
                if (!beforeIndex.TryGetValue(target.ProjectileId, out source))
                {
                    if (progress <= 0f) continue;
                    source = new ProjectileFlightSnapshot(
                        target.Launch,
                        target.Launch.Origin,
                        0f,
                        0f,
                        ProjectileFlightStatus.InFlight);
                }
                else if (source.Status != ProjectileFlightStatus.InFlight)
                {
                    result.Add(source);
                    beforeIndex.Remove(target.ProjectileId);
                    continue;
                }
                result.Add(GameplayProjectilePresentationSampler.Sample(
                    source,
                    target,
                    progress));
                beforeIndex.Remove(target.ProjectileId);
            }
            foreach (ProjectileFlightSnapshot unchanged in beforeIndex.Values)
                result.Add(unchanged);
            result.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.ProjectileId,
                right.ProjectileId));
            return result.AsReadOnly();
        }

        private static Dictionary<string, GameplayActorSnapshot> IndexActors(
            IReadOnlyList<GameplayActorSnapshot> values)
        {
            var result = new Dictionary<string, GameplayActorSnapshot>(
                StringComparer.Ordinal);
            foreach (GameplayActorSnapshot value in values)
                result.Add(value.ActorId, value);
            return result;
        }

        private static Dictionary<string, DestructiblePropSnapshot>
            IndexDestructibles(IReadOnlyList<DestructiblePropSnapshot> values)
        {
            var result = new Dictionary<string, DestructiblePropSnapshot>(
                StringComparer.Ordinal);
            foreach (DestructiblePropSnapshot value in values)
                result.Add(value.PropId, value);
            return result;
        }

        private static Dictionary<string, VehicleMomentumState> IndexVehicles(
            IReadOnlyList<VehicleMomentumState> values)
        {
            var result = new Dictionary<string, VehicleMomentumState>(
                StringComparer.Ordinal);
            foreach (VehicleMomentumState value in values)
                result.Add(value.VehicleId, value);
            return result;
        }

        private static Dictionary<string, ProjectileFlightSnapshot>
            IndexProjectiles(IReadOnlyList<ProjectileFlightSnapshot> values)
        {
            var result = new Dictionary<string, ProjectileFlightSnapshot>(
                StringComparer.Ordinal);
            foreach (ProjectileFlightSnapshot value in values)
                result.Add(value.ProjectileId, value);
            return result;
        }

        private static Dictionary<string, SmokeFieldSnapshot> IndexSmoke(
            IReadOnlyList<SmokeFieldSnapshot> values)
        {
            var result = new Dictionary<string, SmokeFieldSnapshot>(
                StringComparer.Ordinal);
            foreach (SmokeFieldSnapshot value in values)
                result.Add(value.Field.Id, value);
            return result;
        }

        private static void ReplaceActorBudget(
            IDictionary<string, GameplayActorSnapshot> actors,
            string actorId,
            TurnBudget budget)
        {
            GameplayActorSnapshot actor = actors[actorId];
            actors[actorId] = CopyActor(actor, actor.Pose, turnBudget: budget);
        }

        private static void ReplaceActorWounds(
            IDictionary<string, GameplayActorSnapshot> actors,
            string actorId,
            ActorWoundSnapshot wounds)
        {
            GameplayActorSnapshot actor = actors[actorId];
            actors[actorId] = CopyActor(actor, actor.Pose, wounds: wounds);
        }

        private static void ReplaceActorEquipment(
            IDictionary<string, GameplayActorSnapshot> actors,
            TurnReplayStateWindow window,
            string actorId,
            string equippedItemId)
        {
            GameplayActorSnapshot actor = actors[actorId];
            EquipmentEffectSet effects = EquipmentEffectSet.None;
            if (!string.IsNullOrWhiteSpace(equippedItemId))
            {
                GameplayActorSnapshot endpoint = window.End.State.Session
                    .GetActor(actorId);
                if (string.Equals(
                    endpoint.EquippedItemId,
                    equippedItemId,
                    StringComparison.Ordinal))
                    effects = endpoint.EquipmentEffects;
            }
            actors[actorId] = CopyActor(
                actor,
                actor.Pose,
                equippedItemId: equippedItemId,
                equipmentEffects: effects,
                replaceEquipment: true);
        }

        private static void ReplaceInventoryQuantity(
            IDictionary<string, GameplayActorSnapshot> actors,
            InventoryQuantityChangeRecord change)
        {
            GameplayActorSnapshot actor = actors[change.ActorId];
            var quantities = new List<InventoryQuantitySnapshot>();
            bool replaced = false;
            foreach (InventoryQuantitySnapshot quantity in
                actor.Inventory.Quantities)
            {
                if (string.Equals(
                    quantity.ItemId,
                    change.ItemId,
                    StringComparison.Ordinal))
                {
                    quantities.Add(new InventoryQuantitySnapshot(
                        change.ItemId,
                        change.ResultingQuantity));
                    replaced = true;
                }
                else
                    quantities.Add(quantity);
            }
            if (!replaced)
                quantities.Add(new InventoryQuantitySnapshot(
                    change.ItemId,
                    change.ResultingQuantity));
            actors[change.ActorId] = CopyActor(
                actor,
                actor.Pose,
                inventory: new ActorInventorySnapshot(change.ActorId, quantities));
        }

        private static IReadOnlyList<T> SortedValues<T>(
            IDictionary<string, T> values)
        {
            var keys = new List<string>(values.Keys);
            keys.Sort(StringComparer.Ordinal);
            var result = new List<T>(keys.Count);
            foreach (string key in keys) result.Add(values[key]);
            return result.AsReadOnly();
        }

        private static void ReplaceValues<T>(
            IDictionary<string, T> destination,
            IDictionary<string, T> source)
        {
            destination.Clear();
            foreach (KeyValuePair<string, T> entry in source)
                destination.Add(entry.Key, entry.Value);
        }

        private static IReadOnlyList<SmokeFieldSnapshot> SortedSmoke(
            IDictionary<string, SmokeFieldSnapshot> values) =>
            SortedValues(values);

        private static IReadOnlyList<VehicleMomentumState> SampleVehicles(
            IReadOnlyList<VehicleMomentumState> before,
            IReadOnlyList<VehicleMomentumState> after,
            float progress)
        {
            if (progress <= 0f) return before;
            if (progress >= 1f) return after;
            var previous = new Dictionary<string, VehicleMomentumState>(
                StringComparer.Ordinal);
            foreach (VehicleMomentumState vehicle in before)
                previous.Add(vehicle.VehicleId, vehicle);
            var result = new List<VehicleMomentumState>(after.Count);
            foreach (VehicleMomentumState target in after)
            {
                if (!previous.TryGetValue(target.VehicleId, out VehicleMomentumState source))
                {
                    result.Add(target);
                    continue;
                }
                result.Add(new VehicleMomentumState(
                    target.VehicleId,
                    Lerp(source.Position, target.Position, progress),
                    LerpAngle(source.ForwardDegrees, target.ForwardDegrees, progress),
                    source.Speed + ((target.Speed - source.Speed) * progress)));
            }
            return result.AsReadOnly();
        }

        private static GameplayActorSnapshot CopyActor(
            GameplayActorSnapshot actor,
            GameplayActorPose pose,
            TurnBudget? turnBudget = null,
            ActorWoundSnapshot? wounds = null,
            string equippedItemId = null,
            EquipmentEffectSet? equipmentEffects = null,
            ActorInventorySnapshot inventory = null,
            bool replaceEquipment = false,
            ActorPinState pinState = null,
            bool replacePin = false) => new GameplayActorSnapshot(
                actor.ActorId,
                pose,
                turnBudget ?? actor.TurnBudget,
                wounds ?? actor.Wounds,
                replaceEquipment ? equippedItemId : actor.EquippedItemId,
                replaceEquipment
                    ? equipmentEffects ?? EquipmentEffectSet.None
                    : actor.EquipmentEffects,
                actor.MaximumWounds,
                inventory ?? actor.Inventory,
                actor.ActionPointEconomy,
                actor.TurnMovementAllowance,
                replacePin ? pinState : actor.PinState,
                actor.EmergencyActionPointAllowance,
                actor.SuspendedTurnBudget);

        private static GameplayPosition Lerp(
            GameplayPosition from,
            GameplayPosition to,
            float progress) => new GameplayPosition(
                from.X + ((to.X - from.X) * progress),
                from.Y + ((to.Y - from.Y) * progress),
                from.Z + ((to.Z - from.Z) * progress));

        private static float LerpAngle(float from, float to, float progress)
        {
            float delta = ((to - from + 540f) % 360f) - 180f;
            return from + (delta * progress);
        }
    }
}
