using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;

namespace GritGud.Presentation.Gameplay
{
    internal static class GameplayEncounterActionTransition
    {
        public static void BeginAfterCommittedAction(
            GameplaySession session,
            GameplayActionRecord action,
            Func<GameplayActionRecord, bool> beginEncounter,
            string actionKind)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (session.EncounterActive
                || !session.ActionStartsEncounter(action))
            {
                return;
            }

            if (beginEncounter == null || !beginEncounter(action))
            {
                throw new InvalidOperationException(
                    $"A committed encounter-opening {actionKind} could not start its encounter.");
            }
        }
    }
}
