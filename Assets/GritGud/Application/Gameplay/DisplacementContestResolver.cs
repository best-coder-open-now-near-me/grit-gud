using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    internal sealed class DisplacementContestResolver
    {
        private readonly GameplaySession gameplay;
        private readonly IReadOnlyDictionary<string, CloseQuartersControlProfile>
            controlProfiles;
        private readonly ID20RollSource rollSource;
        private readonly DisplacementActionEvaluator actionEvaluator;
        private readonly DisplacementDestinationEvaluator destinationEvaluator;

        public DisplacementContestResolver(
            GameplaySession gameplaySession,
            IReadOnlyDictionary<string, CloseQuartersControlProfile>
                authoredControlProfiles,
            ID20RollSource rolls,
            DisplacementActionEvaluator actions,
            DisplacementDestinationEvaluator destinations)
        {
            gameplay = gameplaySession ??
                throw new ArgumentNullException(nameof(gameplaySession));
            controlProfiles = authoredControlProfiles ??
                throw new ArgumentNullException(
                    nameof(authoredControlProfiles));
            rollSource = rolls ??
                throw new ArgumentNullException(nameof(rolls));
            actionEvaluator = actions ??
                throw new ArgumentNullException(nameof(actions));
            destinationEvaluator = destinations ??
                throw new ArgumentNullException(nameof(destinations));
        }

        public bool TryResolve(
            string actorId,
            string targetActorId,
            float targetMass,
            GameplayPosition destination,
            DisplacementActionDefinition definition,
            long displacementSequence,
            out DisplacementRecord record,
            out DisplacementResolutionFailure failure)
        {
            if (!controlProfiles.TryGetValue(
                    actorId,
                    out CloseQuartersControlProfile attacker)
                || !controlProfiles.TryGetValue(
                    targetActorId,
                    out CloseQuartersControlProfile defender))
            {
                record = null;
                failure = DisplacementResolutionFailure.SubjectUnavailable;
                return false;
            }

            gameplay.GetActor(actorId);
            GameplayActorSnapshot target = gameplay.GetActor(targetActorId);
            var request = new DisplacementRequest(
                actorId,
                definition.Id,
                targetActorId,
                DisplacementSubjectKind.Combatant,
                targetMass,
                actionEvaluator.GetSubjectSize(targetActorId),
                destination,
                definition.Intent);
            if (!destinationEvaluator.TryValidateRequest(
                    request,
                    target.Pose.Position,
                    destination,
                    definition,
                    resultingPropState: null,
                    out _,
                    out failure))
            {
                record = null;
                return false;
            }

            var transition = new GameplayTransitionIdentity(
                gameplay.NextActionSequence,
                GameplaySemanticCapability.Displace.ToString(),
                actorId,
                targetActorId);
            var contest = new CloseQuartersControlRecord(
                rollSource.RollD20(transition, "attacker-roll"),
                attacker,
                rollSource.RollD20(transition, "defender-roll"),
                defender);
            GameplayPosition result = contest.AttackerSucceeded
                ? destination
                : target.Pose.Position;
            record = new DisplacementRecord(
                displacementSequence,
                request,
                target.Pose.Position,
                result,
                contest);
            failure = DisplacementResolutionFailure.None;
            return true;
        }
    }
}
