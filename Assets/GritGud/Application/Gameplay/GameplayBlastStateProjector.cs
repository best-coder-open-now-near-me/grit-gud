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
            float integrityDamage)
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
                                woundMovementPenalty * effect.Exposure);
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
            float movementPenalty)
        {
            GameplayActorSnapshot actor = mutation.GetActor(effect.EntityId);
            ActorWoundSnapshot wounds = effect.InjuryRegion.HasValue
                ? actor.Wounds.AddWound(
                    effect.InjuryRegion.Value,
                    movementPenalty)
                : actor.Wounds.AddUnlocalizedWound(movementPenalty);
            float allowance = Math.Max(
                0f,
                actor.TurnMovementAllowance - wounds.MovementPenalty);
            var budget = new TurnBudget(
                actor.TurnBudget.ActionPoints,
                Math.Min(actor.TurnBudget.MovementOpportunity, allowance));
            mutation.ReplaceActor(GameplayCanonicalStateMutation.CopyActor(
                actor,
                budget: budget,
                wounds: wounds));
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
