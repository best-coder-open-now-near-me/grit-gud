using System;

namespace GritGud.Domain.Gameplay
{
    public static class GameplayTargetIds
    {
        public const string WorldAimPoint = "world.aim-point";
    }

    public sealed class WeaponDischargeRecord
    {
        public WeaponDischargeRecord(
            long sequence,
            string attackerId,
            string actionId,
            GameplayPosition origin,
            GameplayPosition aimPoint)
            : this(
                sequence,
                attackerId,
                actionId,
                GameplayTargetIds.WorldAimPoint,
                origin,
                aimPoint)
        {
        }

        public WeaponDischargeRecord(
            long sequence,
            string attackerId,
            string actionId,
            string targetId,
            GameplayPosition origin,
            GameplayPosition aimPoint)
            : this(
                sequence,
                attackerId,
                actionId,
                targetId,
                origin,
                aimPoint,
                impact: null,
                damage: null)
        {
        }

        public WeaponDischargeRecord(
            long sequence,
            string attackerId,
            string actionId,
            string targetId,
            GameplayPosition origin,
            GameplayPosition aimPoint,
            DirectFireImpactRecord impact,
            DestructibleDamageRecord damage)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            if (string.IsNullOrWhiteSpace(attackerId))
            {
                throw new ArgumentException(
                    "Weapon discharges require an attacker identifier.",
                    nameof(attackerId));
            }

            if (string.IsNullOrWhiteSpace(actionId))
            {
                throw new ArgumentException(
                    "Weapon discharges require an action identifier.",
                    nameof(actionId));
            }

            if (string.IsNullOrWhiteSpace(targetId))
            {
                throw new ArgumentException(
                    "Weapon discharges require a stable target identifier.",
                    nameof(targetId));
            }

            if (origin.DistanceTo(aimPoint) <= 0f)
            {
                throw new ArgumentException(
                    "Weapon discharges require an aim point distinct from their origin.",
                    nameof(aimPoint));
            }

            if (impact != null
                && (!string.Equals(
                        impact.TargetId,
                        targetId,
                        StringComparison.Ordinal)
                    || impact.Point.DistanceTo(aimPoint) > 0.0001f))
            {
                throw new ArgumentException(
                    "Direct-fire impact evidence must match the discharge target and aim point.",
                    nameof(impact));
            }

            if (damage != null
                && (impact == null
                    || !string.Equals(
                        damage.PropId,
                        targetId,
                        StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    "Direct-fire prop damage requires matching impact evidence.",
                    nameof(damage));
            }

            Sequence = sequence;
            AttackerId = attackerId;
            ActionId = actionId;
            TargetId = targetId;
            Origin = origin;
            AimPoint = aimPoint;
            Impact = impact;
            Damage = damage;
        }

        public long Sequence { get; }

        public string AttackerId { get; }

        public string ActionId { get; }

        public string TargetId { get; }

        public GameplayPosition Origin { get; }

        public GameplayPosition AimPoint { get; }

        public DirectFireImpactRecord Impact { get; }

        public DestructibleDamageRecord Damage { get; }

        public float Distance => Origin.DistanceTo(AimPoint);
    }
}
