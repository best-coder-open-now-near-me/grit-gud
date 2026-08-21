using System;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    internal sealed class GameplayObjectiveOutcomeValidator :
        GameplayActionOutcomeValidator<ObjectiveCompletedActionOutcome>
    {
        private readonly GameplaySession session;

        public GameplayObjectiveOutcomeValidator(GameplaySession session)
        {
            this.session = session ?? throw new ArgumentNullException(
                nameof(session));
        }

        protected override void Validate(
            GameplayActionRecord action,
            ObjectiveCompletedActionOutcome outcome)
        {
            if (session.RequireObjective(outcome.ObjectiveId).IsCompleted)
            {
                throw new InvalidOperationException(
                    "The objective is already complete.");
            }
        }
    }
}
