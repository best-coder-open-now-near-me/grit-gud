using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayBlastConsequenceResolver
    {
        private readonly GameplaySession gameplay;
        private readonly DestructiblePropSession destructibles;

        public GameplayBlastConsequenceResolver(
            GameplaySession gameplaySession,
            DestructiblePropSession destructibleSession)
        {
            gameplay = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            destructibles = destructibleSession ??
                throw new ArgumentNullException(nameof(destructibleSession));
        }

        public void Validate(
            IReadOnlyList<BlastEffectRecord> effects,
            float woundMovementPenalty,
            float integrityDamage)
        {
            ValidateMagnitude(
                woundMovementPenalty,
                nameof(woundMovementPenalty));
            ValidateMagnitude(integrityDamage, nameof(integrityDamage));
            if (effects == null)
            {
                throw new ArgumentNullException(nameof(effects));
            }

            foreach (BlastEffectRecord effect in effects)
            {
                if (effect == null)
                {
                    throw new ArgumentException(
                        "Blast effects cannot contain null entries.",
                        nameof(effects));
                }

                switch (effect.SubjectKind)
                {
                    case BlastSubjectKind.Actor:
                        if (!gameplay.TryGetActor(effect.EntityId, out _))
                        {
                            throw new InvalidOperationException(
                                $"Blast actor '{effect.EntityId}' is not authoritative.");
                        }
                        break;

                    case BlastSubjectKind.DestructibleProp:
                        if (!destructibles.TryGetProp(effect.EntityId, out _))
                        {
                            throw new InvalidOperationException(
                                $"Blast prop '{effect.EntityId}' is not authoritative.");
                        }
                        break;

                    case BlastSubjectKind.Vehicle:
                        throw new NotSupportedException(
                            "Vehicle blast consequences require the vehicle damage system.");

                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(effect.SubjectKind));
                }
            }
        }

        public void Apply(
            IReadOnlyList<BlastEffectRecord> effects,
            float woundMovementPenalty,
            float integrityDamage)
        {
            var notifications = new GameplayNotificationBatch();
            Apply(
                effects,
                woundMovementPenalty,
                integrityDamage,
                notifications);
            notifications.Publish();
        }

        internal void Apply(
            IReadOnlyList<BlastEffectRecord> effects,
            float woundMovementPenalty,
            float integrityDamage,
            GameplayNotificationBatch notifications)
        {
            if (notifications == null)
            {
                throw new ArgumentNullException(nameof(notifications));
            }

            Validate(effects, woundMovementPenalty, integrityDamage);
            foreach (BlastEffectRecord effect in effects)
            {
                if (effect.Exposure <= 0f)
                {
                    continue;
                }

                switch (effect.SubjectKind)
                {
                    case BlastSubjectKind.Actor:
                        if (woundMovementPenalty > 0f)
                        {
                            gameplay.ApplyBlastInjury(
                                effect.EntityId,
                                effect.InjuryRegion,
                                woundMovementPenalty * effect.Exposure,
                                notifications);
                        }
                        break;

                    case BlastSubjectKind.DestructibleProp:
                        if (integrityDamage > 0f)
                        {
                            destructibles.TryApplyDamage(
                                effect.EntityId,
                                integrityDamage * effect.Exposure,
                                out _,
                                notifications);
                        }
                        break;
                }
            }
        }

        private static void ValidateMagnitude(float value, string name)
        {
            if (float.IsNaN(value)
                || float.IsInfinity(value)
                || value < 0f)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
