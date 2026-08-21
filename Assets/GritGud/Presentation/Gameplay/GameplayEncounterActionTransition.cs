using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal static class GameplayEncounterActionTransition
    {
        public static bool BeginAfterCommittedAction(
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
                return false;
            }

            try
            {
                if (beginEncounter != null && beginEncounter(action))
                    return true;

                Debug.LogWarning(
                    $"Committed {actionKind} requires an encounter, but its presentation start request was not accepted.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Committed {actionKind} could not present its encounter start: {exception.Message}");
            }

            return false;
        }
    }
}
