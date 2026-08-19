using System;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed partial class GameplaySession
    {
        internal void CommitDroneMoveBudget(DroneMoveRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            GameplayActorState actor = RequireActiveActor(
                record.ControllerActorId);
            RequireDroneControllerBudget(
                actor,
                record.PreviousBudget,
                "movement");
            actor.TurnBudget = record.ResultingBudget;
            Journal.RecordDroneMoved(record);
            MarkStateChanged();
        }

        internal void CommitDroneAttackBudget(DroneAttackRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            GameplayActorState actor = RequireActiveActor(
                record.ControllerActorId);
            RequireDroneControllerBudget(
                actor,
                record.PreviousBudget,
                "attack");
            actor.TurnBudget = record.ResultingBudget;
            Journal.RecordDroneAttackResolved(record);
            MarkStateChanged();
        }

        internal void CommitDroneActorAttack(
            DroneAttackRecord record,
            AttackResolutionRecord resolution)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (resolution == null) throw new ArgumentNullException(
                nameof(resolution));
            GameplayActorState controller = RequireActiveActor(
                record.ControllerActorId);
            GameplayActorState target = RequireActor(resolution.TargetId);
            RequireDroneControllerBudget(
                controller,
                record.PreviousBudget,
                "actor attack");
            if (!target.Wounds.HasSameState(resolution.TargetWoundsBefore))
                throw new InvalidOperationException(
                    "Drone actor attack starts from stale target state.");
            controller.TurnBudget = record.ResultingBudget;
            if (resolution.Hit)
                target.ApplyBlast(
                    resolution.Wound.Region,
                    resolution.Wound.AppliedMovementPenalty);
            Journal.RecordDroneAttackResolved(record);
            var notifications = new GameplayNotificationBatch();
            if (resolution.Hit)
                notifications.Add(ActorCapabilityChanged, resolution.TargetId);
            MarkStateChanged();
            notifications.Publish();
        }

        private static void RequireDroneControllerBudget(
            GameplayActorState actor,
            GritGud.Domain.Turns.TurnBudget expected,
            string actionLabel)
        {
            if (actor.IsIncapacitated)
                throw new InvalidOperationException(
                    "An incapacitated actor cannot control a drone.");
            if (actor.TurnBudget.ActionPoints != expected.ActionPoints
                || actor.TurnBudget.MovementOpportunity
                    != expected.MovementOpportunity)
                throw new InvalidOperationException(
                    $"Drone {actionLabel} was prepared against a stale controller budget.");
        }

        internal void CommitActorDroneAttack(ActorDroneAttackRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            GameplayActorState actor = RequireActiveActor(record.AttackerId);
            if (record.Sequence != NextActionSequence)
                throw new InvalidOperationException(
                    "Actor-drone attack is not the next action sequence.");
            RequireDroneControllerBudget(
                actor,
                record.PreviousBudget,
                "integrity attack");
            actor.TurnBudget = record.ResultingBudget;
            lastAuxiliaryActionSequence = record.Sequence;
            Journal.RecordActorDroneAttackResolved(record);
            MarkStateChanged();
        }
    }
}
