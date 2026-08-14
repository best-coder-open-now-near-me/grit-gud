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

            Sequence = sequence;
            AttackerId = attackerId;
            ActionId = actionId;
            TargetId = targetId;
            Origin = origin;
            AimPoint = aimPoint;
        }

        public long Sequence { get; }

        public string AttackerId { get; }

        public string ActionId { get; }

        public string TargetId { get; }

        public GameplayPosition Origin { get; }

        public GameplayPosition AimPoint { get; }

        public float Distance => Origin.DistanceTo(AimPoint);
    }
}
