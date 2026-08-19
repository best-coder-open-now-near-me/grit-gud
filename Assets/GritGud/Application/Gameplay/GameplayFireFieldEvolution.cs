using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayFireFieldAdvanceRecord
    {
        public GameplayFireFieldAdvanceRecord(
            FireFieldSnapshot previous,
            FireFieldSnapshot? resulting,
            IEnumerable<FireFieldPulseRecord> pulses)
        {
            Previous = previous;
            var copy = new List<FireFieldPulseRecord>(
                pulses ?? throw new ArgumentNullException(nameof(pulses)));
            foreach (FireFieldPulseRecord pulse in copy)
                if (pulse == null
                    || !string.Equals(
                        pulse.FieldId,
                        previous.Field.Id,
                        StringComparison.Ordinal))
                    throw new ArgumentException(
                        "Fire advances require pulses from the same field.",
                        nameof(pulses));
            if (resulting.HasValue
                && !string.Equals(
                    resulting.Value.Field.Id,
                    previous.Field.Id,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Fire advances cannot change field identity.",
                    nameof(resulting));
            Resulting = resulting;
            Pulses = copy.AsReadOnly();
        }

        public string FieldId => Previous.Field.Id;
        public FireFieldSnapshot Previous { get; }
        public FireFieldSnapshot? Resulting { get; }
        public IReadOnlyList<FireFieldPulseRecord> Pulses { get; }
    }

    public sealed class GameplayFireFieldsAdvancedEvent : GameplayDomainEvent
    {
        public GameplayFireFieldsAdvancedEvent(
            GameplayTransitionIdentity transition,
            IEnumerable<GameplayFireFieldAdvanceRecord> advances)
            : base(transition, "fire-fields-advanced", "world")
        {
            var copy = new List<GameplayFireFieldAdvanceRecord>(
                advances ?? throw new ArgumentNullException(nameof(advances)));
            foreach (GameplayFireFieldAdvanceRecord advance in copy)
                if (advance == null)
                    throw new ArgumentException(
                        "Fire field advances cannot contain null entries.",
                        nameof(advances));
            Advances = copy.AsReadOnly();
        }

        public IReadOnlyList<GameplayFireFieldAdvanceRecord> Advances { get; }
    }

    internal static class GameplayFireFieldEvolution
    {
        private const float PulseTolerance = 0.000001f;

        public static GameplayFireFieldAdvanceRecord AdvanceTurnEnd(
            GameplayCombatStateSnapshot state,
            FireFieldSnapshot fire)
        {
            FireFieldPulseRecord pulse = CreatePulse(state, fire);
            float remaining = Math.Max(
                0f,
                fire.RemainingFraction
                    - (1f / fire.Field.Definition.DurationTurnEnds));
            FireFieldSnapshot? resulting = remaining > 0f
                ? new FireFieldSnapshot(
                    fire.Field,
                    remaining,
                    fire.PulseProgress)
                : (FireFieldSnapshot?)null;
            return new GameplayFireFieldAdvanceRecord(
                fire,
                resulting,
                new[] { pulse });
        }

        public static GameplayFireFieldAdvanceRecord AdvanceContinuous(
            GameplayCombatStateSnapshot state,
            FireFieldSnapshot fire,
            float elapsedSeconds)
        {
            GameplayNumericPolicy.RequireFinite(
                elapsedSeconds,
                nameof(elapsedSeconds));
            if (elapsedSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            FireFieldDefinition definition = fire.Field.Definition;
            float remainingSeconds = fire.RemainingFraction
                * definition.ExplorationDurationSeconds;
            float activeSeconds = Math.Min(elapsedSeconds, remainingSeconds);
            float pulseUnits = fire.PulseProgress
                + (activeSeconds / definition.ExplorationPulseSeconds);
            int pulseCount = (int)Math.Floor(pulseUnits + PulseTolerance);
            var pulses = new List<FireFieldPulseRecord>(pulseCount);
            for (int index = 0; index < pulseCount; index++)
            {
                float secondsToPulse =
                    ((1f - fire.PulseProgress) + index)
                    * definition.ExplorationPulseSeconds;
                float remainingAtPulse = Math.Max(
                    0f,
                    fire.RemainingFraction
                        - (secondsToPulse
                            / definition.ExplorationDurationSeconds));
                pulses.Add(CreatePulse(
                    state,
                    new FireFieldSnapshot(
                        fire.Field,
                        remainingAtPulse,
                        pulseProgress: 0f)));
            }

            float remaining = Math.Max(
                0f,
                fire.RemainingFraction
                    - (activeSeconds
                        / definition.ExplorationDurationSeconds));
            float progress = pulseUnits - pulseCount;
            if (progress < 0f || progress >= 1f)
                progress = 0f;
            FireFieldSnapshot? resulting = remaining > 0f
                ? new FireFieldSnapshot(fire.Field, remaining, progress)
                : (FireFieldSnapshot?)null;
            return new GameplayFireFieldAdvanceRecord(
                fire,
                resulting,
                pulses);
        }

        private static FireFieldPulseRecord CreatePulse(
            GameplayCombatStateSnapshot state,
            FireFieldSnapshot fire)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            FireFieldDefinition definition = fire.Field.Definition;
            float radius = fire.CurrentRadius;
            var effects = new List<FireFieldEffectRecord>();
            if (definition.ActorWoundMovementPenalty > 0f)
            {
                foreach (GameplayActorSnapshot actor in state.Session.Actors)
                    AddEffect(
                        effects,
                        FireFieldSubjectKind.Actor,
                        actor.ActorId,
                        actor.Pose.Position,
                        fire.Field.Origin,
                        radius,
                        definition.Height);
            }
            if (definition.DestructibleIntegrityDamage > 0f)
            {
                state.RequireCoverage(
                    GameplayCombatStateCoverage.Destructibles);
                foreach (DestructiblePropSnapshot prop in state.Destructibles)
                    if (prop.State != DestructiblePropState.Destroyed)
                        AddEffect(
                            effects,
                            FireFieldSubjectKind.DestructibleProp,
                            prop.PropId,
                            prop.Pose.Position,
                            fire.Field.Origin,
                            radius,
                            definition.Height);
            }
            return new FireFieldPulseRecord(
                fire.Field.Id,
                radius,
                effects);
        }

        private static void AddEffect(
            ICollection<FireFieldEffectRecord> effects,
            FireFieldSubjectKind kind,
            string entityId,
            GameplayPosition position,
            GameplayPosition origin,
            float radius,
            float height)
        {
            float vertical = position.Y - origin.Y;
            if (vertical < 0f || vertical > height) return;
            float deltaX = position.X - origin.X;
            float deltaZ = position.Z - origin.Z;
            float distance = (float)Math.Sqrt(
                (deltaX * deltaX) + (deltaZ * deltaZ));
            if (distance > radius) return;
            effects.Add(new FireFieldEffectRecord(kind, entityId, distance));
        }
    }

    internal readonly struct GameplayFireProjectionCounts
    {
        public GameplayFireProjectionCounts(
            int actorInjuries,
            int destructibleDamages)
        {
            ActorInjuries = actorInjuries;
            DestructibleDamages = destructibleDamages;
        }

        public int ActorInjuries { get; }
        public int DestructibleDamages { get; }
    }

    internal static class GameplayFireStateProjector
    {
        public static GameplayFireProjectionCounts Apply(
            GameplayCanonicalStateMutation mutation,
            FireFieldDefinition definition,
            IEnumerable<FireFieldPulseRecord> pulses)
        {
            int actorInjuries = 0;
            int destructibleDamages = 0;
            foreach (FireFieldPulseRecord pulse in pulses)
                foreach (FireFieldEffectRecord effect in pulse.Effects)
                    switch (effect.SubjectKind)
                    {
                        case FireFieldSubjectKind.Actor:
                            if (definition.ActorWoundMovementPenalty > 0f
                                && ApplyActor(
                                    mutation,
                                    effect.EntityId,
                                    definition.ActorWoundMovementPenalty))
                                actorInjuries++;
                            break;
                        case FireFieldSubjectKind.DestructibleProp:
                            if (definition.DestructibleIntegrityDamage > 0f
                                && ApplyDestructible(
                                    mutation,
                                    effect.EntityId,
                                    definition.DestructibleIntegrityDamage))
                                destructibleDamages++;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
            return new GameplayFireProjectionCounts(
                actorInjuries,
                destructibleDamages);
        }

        private static bool ApplyActor(
            GameplayCanonicalStateMutation mutation,
            string actorId,
            float movementPenalty)
        {
            GameplayActorSnapshot actor = mutation.GetActor(actorId);
            if (actor.Wounds.WoundCount >= actor.MaximumWounds) return false;
            ActorWoundSnapshot wounds = actor.Wounds.AddWound(
                TargetRegionId.Torso,
                movementPenalty);
            float allowance = Math.Max(
                0f,
                actor.TurnMovementAllowance - wounds.MovementPenalty);
            mutation.ReplaceActor(GameplayCanonicalStateMutation.CopyActor(
                actor,
                budget: new TurnBudget(
                    actor.TurnBudget.ActionPoints,
                    Math.Min(
                        actor.TurnBudget.MovementOpportunity,
                        allowance)),
                wounds: wounds));
            return true;
        }

        private static bool ApplyDestructible(
            GameplayCanonicalStateMutation mutation,
            string propId,
            float damage)
        {
            DestructiblePropSnapshot prop = mutation.GetDestructible(propId);
            if (prop.State == DestructiblePropState.Destroyed) return false;
            float remaining = Math.Max(
                0f,
                prop.RemainingIntegrity - Math.Min(
                    damage,
                    prop.RemainingIntegrity));
            DestructiblePropState state = remaining <= 0f
                ? DestructiblePropState.Destroyed
                : DestructiblePropState.Damaged;
            ulong detached = DestructibleFracture.CreateResultingMask(
                prop.PropId,
                prop.FractureChunkCount,
                prop.DetachedFractureChunks,
                prop.MaximumIntegrity,
                remaining,
                preferredChunkIndex: -1);
            mutation.ReplaceDestructible(new DestructiblePropSnapshot(
                prop.PropId,
                state,
                prop.MaximumIntegrity,
                remaining,
                prop.Pose,
                prop.Posture,
                prop.FractureChunkCount,
                detached));
            return true;
        }
    }
}
