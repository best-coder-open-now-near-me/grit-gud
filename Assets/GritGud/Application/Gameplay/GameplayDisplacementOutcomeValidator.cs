using System;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    internal sealed class GameplayDisplacementOutcomeValidator :
        GameplayActionOutcomeValidator<DisplacementActionOutcome>
    {
        private readonly GameplaySession session;

        public GameplayDisplacementOutcomeValidator(GameplaySession session)
        {
            this.session = session ?? throw new ArgumentNullException(
                nameof(session));
        }

        protected override void Validate(
            GameplayActionRecord action,
            DisplacementActionOutcome outcome)
        {
            DisplacementRecord displacement = outcome.Displacement;
            if (displacement == null)
            {
                DisplacementActionCommitValidator.Validate(
                    action,
                    displacement,
                    definition: null,
                    equippedItem: null,
                    chargesTurnCost:
                        GameplayActionValidationRules.ShouldChargeTurnCost(
                            session,
                            action));
                return;
            }

            DisplacementActionDefinition definition = session
                .RequireActorDefinition(displacement.Request.ActorId)
                .GetDisplacementAction(displacement.Request.ActionId);
            session.RequireActor(displacement.Request.ActorId);
            DisplacementActionCommitValidator.Validate(
                action,
                displacement,
                definition,
                session.GetEquippedItem(displacement.Request.ActorId),
                GameplayActionValidationRules.ShouldChargeTurnCost(
                    session,
                    action));
        }
    }
}
