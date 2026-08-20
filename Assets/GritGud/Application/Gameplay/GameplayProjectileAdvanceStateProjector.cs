using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public static class GameplayProjectileAdvanceStateProjector
    {
        private const float ValueTolerance = 0.0001f;

        public static GameplayCombatStateSnapshot Project(
            GameplayCombatStateSnapshot previous,
            ProjectileAdvanceRecord advance,
            bool destructiblesShareGameplayJournal)
        {
            if (previous == null) throw new ArgumentNullException(nameof(previous));
            if (advance == null) throw new ArgumentNullException(nameof(advance));
            ProjectileFlightSnapshot current = FindProjectile(
                previous.Projectiles,
                advance.ProjectileId);
            if (!FlightsMatch(current, advance.Previous))
                throw new InvalidOperationException(
                    "Projectile advance no longer starts at canonical flight state.");

            IReadOnlyList<BlastEffectRecord> effects = advance.Resulting.Impact
                ?.BlastEffects ?? Array.Empty<BlastEffectRecord>();
            float woundPenalty = advance.Resulting.Launch.Definition
                .BlastWoundMovementPenalty;
            float integrityDamage = advance.Resulting.Launch.Definition
                .BlastIntegrityDamage;
            var actors = new List<GameplayActorSnapshot>(previous.Session.Actors);
            var destructibles = new List<DestructiblePropSnapshot>(
                previous.Destructibles);
            int destructibleJournalEntries = 0;
            long gameplayRevisionIncrement = 0L;
            foreach (BlastEffectRecord effect in effects)
            {
                if (effect.Exposure <= 0f) continue;
                switch (effect.SubjectKind)
                {
                    case BlastSubjectKind.Actor:
                        if (woundPenalty > 0f)
                        {
                            ApplyActorEffect(actors, effect, woundPenalty);
                            gameplayRevisionIncrement++;
                        }
                        break;
                    case BlastSubjectKind.DestructibleProp:
                        if (integrityDamage > 0f
                            && ApplyDestructibleEffect(
                                destructibles,
                                effect,
                                integrityDamage))
                            destructibleJournalEntries++;
                        break;
                    case BlastSubjectKind.Vehicle:
                        throw new NotSupportedException(
                            "Vehicle blast consequences require vehicle damage state.");
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(effect.SubjectKind));
                }
            }

            var projectiles = new List<ProjectileFlightSnapshot>(
                previous.Projectiles.Count);
            foreach (ProjectileFlightSnapshot flight in previous.Projectiles)
                projectiles.Add(string.Equals(
                        flight.ProjectileId,
                        advance.ProjectileId,
                        StringComparison.Ordinal)
                    ? advance.Resulting
                    : flight);

            GameplaySessionStateSnapshot session = previous.Session;
            long journalIncrement = 1L;
            if (destructiblesShareGameplayJournal)
                journalIncrement = checked(
                    journalIncrement + destructibleJournalEntries);
            var resultingSession = new GameplaySessionStateSnapshot(
                session.ScenarioId,
                session.Mode,
                session.Operation,
                session.TurnContext,
                session.EncounterActive,
                session.EncounterCompletionRequested,
                session.ActiveActorId,
                session.TurnPhase,
                actors,
                session.InitiativeOrder,
                session.Objectives,
                session.EmergencyResponders,
                session.EmergencyResponderIndex,
                session.EmergencyResumeActorId,
                session.LastActionSequence,
                session.LastTurnSequence,
                checked(session.JournalSequence + journalIncrement),
                session.RunIdentity,
                checked(session.Revision + gameplayRevisionIncrement),
                session.VoluntaryTurnReentrySecondsRemaining,
                session.PendingMovementRoute,
                session.PendingVoluntaryTurnCycle,
                session.LastTransitionSequence,
                session.LastVoluntaryTurnCycleSequence,
                session.EncounterState,
                session.AllInitiativeOrder);
            return new GameplayCombatStateSnapshot(
                resultingSession,
                destructibles,
                previous.Vehicles,
                projectiles,
                previous.SmokeFields,
                previous.Coverage,
                previous.FireFields,
                previous.Drones);
        }

        private static void ApplyActorEffect(
            IList<GameplayActorSnapshot> actors,
            BlastEffectRecord effect,
            float woundPenalty)
        {
            int index = FindActorIndex(actors, effect.EntityId);
            GameplayActorSnapshot actor = actors[index];
            float appliedPenalty = woundPenalty * effect.Exposure;
            ActorWoundSnapshot wounds = effect.InjuryRegion.HasValue
                ? actor.Wounds.AddWound(
                    effect.InjuryRegion.Value,
                    appliedPenalty)
                : actor.Wounds.AddUnlocalizedWound(appliedPenalty);
            float woundedAllowance = Math.Max(
                0f,
                actor.TurnMovementAllowance - wounds.MovementPenalty);
            var budget = new TurnBudget(
                actor.TurnBudget.ActionPoints,
                Math.Min(
                    actor.TurnBudget.MovementOpportunity,
                    woundedAllowance));
            actors[index] = CopyActor(actor, budget, wounds);
        }

        private static bool ApplyDestructibleEffect(
            IList<DestructiblePropSnapshot> destructibles,
            BlastEffectRecord effect,
            float integrityDamage)
        {
            int index = FindDestructibleIndex(destructibles, effect.EntityId);
            DestructiblePropSnapshot prop = destructibles[index];
            if (prop.State == DestructiblePropState.Destroyed) return false;
            float remaining = Math.Max(
                0f,
                prop.RemainingIntegrity - (integrityDamage * effect.Exposure));
            DestructiblePropState state = remaining <= 0f
                ? DestructiblePropState.Destroyed
                : DestructiblePropState.Damaged;
            ulong detachedChunks = DestructibleFracture.CreateResultingMask(
                prop.PropId,
                prop.FractureChunkCount,
                prop.DetachedFractureChunks,
                prop.MaximumIntegrity,
                remaining);
            destructibles[index] = new DestructiblePropSnapshot(
                prop.PropId,
                state,
                prop.MaximumIntegrity,
                remaining,
                prop.Pose,
                prop.Posture,
                prop.FractureChunkCount,
                detachedChunks);
            return true;
        }

        private static GameplayActorSnapshot CopyActor(
            GameplayActorSnapshot actor,
            TurnBudget budget,
            ActorWoundSnapshot wounds) =>
            new GameplayActorSnapshot(
                actor.ActorId,
                actor.Pose,
                budget,
                wounds,
                actor.EquippedItemId,
                actor.EquipmentEffects,
                actor.MaximumWounds,
                actor.Inventory,
                actor.ActionPointEconomy,
                actor.TurnMovementAllowance,
                actor.PinState,
                actor.EmergencyActionPointAllowance,
                actor.SuspendedTurnBudget,
                actor.AttacksCommittedThisTurn,
                actor.Ammunition);

        private static int FindActorIndex(
            IList<GameplayActorSnapshot> actors,
            string actorId)
        {
            for (int index = 0; index < actors.Count; index++)
                if (string.Equals(
                    actors[index].ActorId,
                    actorId,
                    StringComparison.Ordinal))
                    return index;
            throw new InvalidOperationException(
                $"Blast actor '{actorId}' is not in canonical state.");
        }

        private static int FindDestructibleIndex(
            IList<DestructiblePropSnapshot> destructibles,
            string propId)
        {
            for (int index = 0; index < destructibles.Count; index++)
                if (string.Equals(
                    destructibles[index].PropId,
                    propId,
                    StringComparison.Ordinal))
                    return index;
            throw new InvalidOperationException(
                $"Blast prop '{propId}' is not in canonical state.");
        }

        private static ProjectileFlightSnapshot FindProjectile(
            IReadOnlyList<ProjectileFlightSnapshot> projectiles,
            string projectileId)
        {
            foreach (ProjectileFlightSnapshot projectile in projectiles)
                if (string.Equals(
                    projectile.ProjectileId,
                    projectileId,
                    StringComparison.Ordinal))
                    return projectile;
            throw new InvalidOperationException(
                $"Projectile '{projectileId}' is not in canonical state.");
        }

        private static bool FlightsMatch(
            ProjectileFlightSnapshot left,
            ProjectileFlightSnapshot right) =>
            left.Launch != null
            && right.Launch != null
            && left.Launch.Sequence == right.Launch.Sequence
            && string.Equals(
                left.ProjectileId,
                right.ProjectileId,
                StringComparison.Ordinal)
            && left.Position.DistanceTo(right.Position) <= ValueTolerance
            && Math.Abs(left.DistanceTraveled - right.DistanceTraveled)
                <= ValueTolerance
            && Math.Abs(left.ElapsedTurnTime - right.ElapsedTurnTime)
                <= ValueTolerance
            && left.Status == right.Status;
    }
}
