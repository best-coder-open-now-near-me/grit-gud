using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    /// <summary>
    /// The single authority for the mode-independent part of actor input.
    /// Controllers and action preparers must ask this before they interpret a
    /// capability's own target, ammunition, or cost rules. Exploration never
    /// disables an immediate action; only actions that require a simulated
    /// interval need turn mode when they cannot begin an encounter.
    /// </summary>
    public static class GameplayActorActionAuthority
    {
        public static bool TryAuthorize(
            GameplaySession gameplay,
            string actorId,
            GameplayActionTiming timing,
            bool startsEncounter,
            bool blocksPinnedActor,
            out GameplayActorSnapshot actor,
            out GameplayActorActionFailure failure)
        {
            if (gameplay == null)
                throw new ArgumentNullException(nameof(gameplay));

            return TryAuthorize(
                GameplayCombatStateCapture.Capture(gameplay).Session,
                actorId,
                timing,
                startsEncounter,
                blocksPinnedActor,
                out actor,
                out failure);
        }

        public static bool TryAuthorize(
            GameplaySessionStateSnapshot session,
            string actorId,
            GameplayActionTiming timing,
            bool startsEncounter,
            bool blocksPinnedActor,
            out GameplayActorSnapshot actor,
            out GameplayActorActionFailure failure)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            actor = default;
            if (session.Operation != GameplaySessionOperation.None)
            {
                failure = GameplayActorActionFailure.OperationInProgress;
                return false;
            }

            try
            {
                actor = session.GetActor(actorId);
            }
            catch (KeyNotFoundException)
            {
                failure = GameplayActorActionFailure.ActorUnavailable;
                return false;
            }

            if (actor.IsIncapacitated)
            {
                failure = GameplayActorActionFailure.ActorIncapacitated;
                return false;
            }

            if (blocksPinnedActor && actor.IsPinned)
            {
                failure = GameplayActorActionFailure.ActorPinned;
                return false;
            }

            if (session.Mode == GameplaySessionMode.TurnBased)
            {
                if (!string.Equals(
                        session.ActiveActorId,
                        actorId,
                        StringComparison.Ordinal))
                {
                    failure = GameplayActorActionFailure.ActorNotActive;
                    return false;
                }
            }
            else if (timing == GameplayActionTiming.RequiresTurnInterval
                && !startsEncounter)
            {
                failure = GameplayActorActionFailure.TurnModeRequired;
                return false;
            }

            failure = GameplayActorActionFailure.None;
            return true;
        }
    }

    public enum GameplayActionTiming
    {
        /// <summary>
        /// The action resolves at the current world instant. It remains usable
        /// in exploration and may open an encounter as a consequence.
        /// </summary>
        Immediate,

        /// <summary>
        /// The action needs a simulated interval (for example projectile or
        /// drone flight). It needs an existing turn interval unless its target
        /// starts the encounter that provides one.
        /// </summary>
        RequiresTurnInterval,
    }

    public enum GameplayActorActionFailure
    {
        None,
        ActorUnavailable,
        ActorNotActive,
        ActorIncapacitated,
        ActorPinned,
        OperationInProgress,
        TurnModeRequired,
    }
}
