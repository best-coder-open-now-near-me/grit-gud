using System;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed partial class GameplaySession
    {
        internal void CommitDroneSummon(SummonDroneRecord record)
        {
            RequireLegacyMutationAllowed(nameof(CommitDroneSummon));
            if (record == null) throw new ArgumentNullException(nameof(record));
            GameplayActorState actor = RequireActiveActor(
                record.SummonerActorId);
            RequireCurrentBudget(actor, record.PreviousBudget, "summon");
            actor.TurnBudget = record.ResultingBudget;
            Journal.RecordDroneSummoned(record);
            MarkStateChanged();
        }

        internal void CommitDroneDismiss(DismissDroneRecord record)
        {
            RequireLegacyMutationAllowed(nameof(CommitDroneDismiss));
            if (record == null) throw new ArgumentNullException(nameof(record));
            GameplayActorState actor = RequireActiveActor(
                record.SummonerActorId);
            RequireCurrentBudget(actor, record.PreviousBudget, "dismissal");
            actor.TurnBudget = record.ResultingBudget;
            Journal.RecordDroneDismissed(record);
            MarkStateChanged();
        }

        internal void CommitDroneMoveBudget(DroneMoveRecord record)
        {
            RequireLegacyMutationAllowed(nameof(CommitDroneMoveBudget));
            if (record == null) throw new ArgumentNullException(nameof(record));
            GameplayActorState actor = RequireActiveActor(
                record.SummonerActorId);
            RequireCurrentBudget(
                actor,
                record.PreviousBudget,
                "movement");
            actor.TurnBudget = record.ResultingBudget;
            Journal.RecordDroneMoved(record);
            MarkStateChanged();
        }

        internal void CommitDroneAttackBudget(DroneAttackRecord record)
        {
            RequireLegacyMutationAllowed(nameof(CommitDroneAttackBudget));
            if (record == null) throw new ArgumentNullException(nameof(record));
            GameplayActorState actor = RequireActiveActor(
                record.SummonerActorId);
            RequireCurrentBudget(
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
            RequireLegacyMutationAllowed(nameof(CommitDroneActorAttack));
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (resolution == null) throw new ArgumentNullException(
                nameof(resolution));
            GameplayActorState summoner = RequireActiveActor(
                record.SummonerActorId);
            GameplayActorState target = RequireActor(resolution.TargetId);
            RequireCurrentBudget(
                summoner,
                record.PreviousBudget,
                "actor attack");
            if (!target.Wounds.HasSameState(resolution.TargetWoundsBefore))
                throw new InvalidOperationException(
                    "Drone actor attack starts from stale target state.");
            summoner.TurnBudget = record.ResultingBudget;
            if (resolution.Hit)
                target.ApplyAttack(resolution);
            Journal.RecordDroneAttackResolved(record);
            var notifications = new GameplayNotificationBatch();
            if (resolution.Hit)
                notifications.Add(ActorCapabilityChanged, resolution.TargetId);
            MarkStateChanged();
            notifications.Publish();
        }

        private static void RequireCurrentBudget(
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
                    $"Drone {actionLabel} was prepared against a stale shared partner budget.");
        }

        internal void CommitActorDroneAttack(ActorDroneAttackRecord record)
        {
            RequireLegacyMutationAllowed(nameof(CommitActorDroneAttack));
            if (record == null) throw new ArgumentNullException(nameof(record));
            GameplayActorState actor = RequireActiveActor(record.AttackerId);
            if (record.Sequence != NextActionSequence)
                throw new InvalidOperationException(
                    "Actor-drone attack is not the next action sequence.");
            RequireCurrentBudget(
                actor,
                record.PreviousBudget,
                "integrity attack");
            actor.TurnBudget = record.ResultingBudget;
            actor.CommitWeaponAttack();
            lastAuxiliaryActionSequence = record.Sequence;
            Journal.RecordActorDroneAttackResolved(record);
            MarkStateChanged();
        }
    }
}
