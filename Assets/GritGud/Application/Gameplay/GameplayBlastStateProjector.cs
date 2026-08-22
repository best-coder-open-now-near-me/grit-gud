using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    internal readonly struct GameplayBlastProjectionCounts
    {
        public GameplayBlastProjectionCounts(
            int actorInjuries,
            int destructibleDamages)
        {
            ActorInjuries = actorInjuries;
            DestructibleDamages = destructibleDamages;
        }

        public int ActorInjuries { get; }
        public int DestructibleDamages { get; }
    }

    internal static class GameplayBlastStateProjector
    {
        public static GameplayBlastProjectionCounts Apply(
            GameplayCanonicalStateMutation mutation,
            IEnumerable<BlastEffectRecord> effects,
            float woundMovementPenalty,
            float integrityDamage,
            string sourceActorId,
            string weaponId,
            long sequence)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            if (effects == null) throw new ArgumentNullException(nameof(effects));
            GameplayNumericPolicy.RequireFinite(
                woundMovementPenalty,
                nameof(woundMovementPenalty));
            GameplayNumericPolicy.RequireFinite(
                integrityDamage,
                nameof(integrityDamage));
            if (woundMovementPenalty < 0f || integrityDamage < 0f)
                throw new ArgumentOutOfRangeException(nameof(woundMovementPenalty));
            if (string.IsNullOrWhiteSpace(sourceActorId)
                || string.IsNullOrWhiteSpace(weaponId))
                throw new ArgumentException(
                    "Blast injury projection requires action provenance.");
            if (sequence <= 0L)
                throw new ArgumentOutOfRangeException(nameof(sequence));

            int actorInjuries = 0;
            int destructibleDamages = 0;
            foreach (BlastEffectRecord effect in effects)
            {
                if (effect == null)
                    throw new ArgumentException(
                        "Blast effects cannot contain null entries.",
                        nameof(effects));
                if (effect.Exposure <= 0f) continue;
                switch (effect.SubjectKind)
                {
                    case BlastSubjectKind.Actor:
                        if (woundMovementPenalty > 0f)
                        {
                            ApplyActor(
                                mutation,
                                effect,
                                woundMovementPenalty * effect.Exposure,
                                sourceActorId,
                                weaponId,
                                sequence);
                            actorInjuries++;
                        }
                        break;
                    case BlastSubjectKind.DestructibleProp:
                        if (integrityDamage > 0f
                            && ApplyDestructible(
                                mutation,
                                effect.EntityId,
                                integrityDamage * effect.Exposure))
                            destructibleDamages++;
                        break;
                    case BlastSubjectKind.Vehicle:
                        throw new NotSupportedException(
                            "Vehicle blast damage has no authored vehicle integrity state.");
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(effect.SubjectKind));
                }
            }
            return new GameplayBlastProjectionCounts(
                actorInjuries,
                destructibleDamages);
        }

        public static int ApplyConcussiveEffects(
            GameplayCanonicalStateMutation mutation,
            IEnumerable<ConcussiveActionPointEffectRecord> effects)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            if (effects == null) throw new ArgumentNullException(nameof(effects));
            int changed = 0;
            foreach (ConcussiveActionPointEffectRecord effect in effects)
            {
                if (effect == null)
                    throw new ArgumentException(
                        "Concussive effects cannot contain null entries.",
                        nameof(effects));
                GameplayActorSnapshot actor = mutation.GetActor(effect.ActorId);
                if (actor.TurnBudget.ActionPoints != effect.PreviousActionPoints)
                    throw new InvalidOperationException(
                        "Concussive AP consequence no longer matches canonical state.");
                mutation.ReplaceActor(GameplayCanonicalStateMutation.CopyActor(
                    actor,
                    budget: new TurnBudget(
                        effect.ResultingActionPoints,
                        actor.TurnBudget.MovementOpportunity)));
                if (effect.RemovedActionPoints > 0) changed++;
            }
            return changed;
        }

        private static void ApplyActor(
            GameplayCanonicalStateMutation mutation,
            BlastEffectRecord effect,
            float movementPenalty,
            string sourceActorId,
            string weaponId,
            long sequence)
        {
            GameplayActorSnapshot actor = mutation.GetActor(effect.EntityId);
            int severity = ActorInjuryRules.CalculateImpactSeverity(
                movementPenalty,
                100f,
                100,
                1,
                100);
            var impact = new LocalizedImpact(
                "blast-impact:" + sequence + ":" + sourceActorId + ":"
                    + effect.EntityId,
                sourceActorId,
                effect.EntityId,
                weaponId,
                effect.InjuryRegion,
                DamageMechanism.Blast,
                severity,
                sequence);
            ActorInjuryState injuries = ActorInjuryRules.ApplyImpact(
                actor.Injuries,
                impact,
                movementPenalty).Resulting;
            ActorWoundSnapshot wounds = LegacyWoundProjection.From(injuries);
            TurnBudget budget = GameplayInjuryCapabilityProjection
                .LimitMovement(
                    actor.TurnBudget,
                    actor.TurnMovementAllowance,
                    injuries.Capabilities);
            mutation.ReplaceActor(GameplayCanonicalStateMutation.CopyActor(
                actor,
                budget: budget,
                wounds: wounds,
                injuries: injuries));
        }

        private static bool ApplyDestructible(
            GameplayCanonicalStateMutation mutation,
            string propId,
            float requestedDamage)
        {
            DestructiblePropSnapshot prop = mutation.GetDestructible(propId);
            if (prop.State == DestructiblePropState.Destroyed) return false;
            float remaining = Math.Max(
                0f,
                prop.RemainingIntegrity - Math.Min(
                    requestedDamage,
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
