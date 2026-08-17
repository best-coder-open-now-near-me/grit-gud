using GritGud.Domain.Gameplay;
using GritGud.Application.Gameplay;
using GritGud.Presentation.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayDisplacementTargetingTests
    {
        [Test]
        public void SelectingIntentEngagesBeforePointerHasAValidTarget()
        {
            var root = new GameObject("Displacement Targeting Test");
            try
            {
                var controller = root.AddComponent<GameplayDisplacementController>();

                controller.BeginTargeting("close-quarters.push");

                Assert.That(controller.IsTargeting, Is.True);
                Assert.That(controller.IsPointerTargetValid, Is.False);
                Assert.That(controller.PointerTooltip, Is.EqualTo("INVALID TARGET"));
                Assert.That(controller.CancelTargeting(), Is.True);
                Assert.That(controller.IsTargeting, Is.False);
                Assert.That(controller.PointerTooltip, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StructuredTargetFailuresProduceSpecificPointerDetails()
        {
            Assert.That(
                GameplayDisplacementController.FormatTargetFailure(
                    DisplacementTargetFailure.SubjectKindNotAccepted),
                Is.EqualTo(
                    "INVALID TARGET - SUBJECT TYPE NOT ACCEPTED"));
            Assert.That(
                GameplayDisplacementController.FormatTargetFailure(
                    DisplacementTargetFailure.SubjectTooHeavy,
                    subjectMass: 35f,
                    maximumSubjectMass: 30f),
                Is.EqualTo("INVALID TARGET - TOO HEAVY (35 / 30 KG)"));
            Assert.That(
                GameplayDisplacementController.FormatTargetFailure(
                    DisplacementTargetFailure.SubjectOutOfReach,
                    distance: 3.2f,
                    reach: 2f),
                Is.EqualTo(
                    "INVALID TARGET - OUT OF REACH (3.2 / 2 M)"));
        }

        [Test]
        public void StructuredDestinationFailuresProduceSpecificPointerDetails()
        {
            Assert.That(
                GameplayDisplacementController.FormatDestinationFailure(
                    DisplacementResolutionFailure.DestinationUnchanged),
                Is.EqualTo("INVALID DESTINATION - MOVE THE SUBJECT"));
            Assert.That(
                GameplayDisplacementController.FormatDestinationFailure(
                    DisplacementResolutionFailure.DestinationTooFar),
                Is.EqualTo("INVALID DESTINATION - TOO FAR"));
            Assert.That(
                GameplayDisplacementController.FormatDestinationFailure(
                    DisplacementResolutionFailure.DestinationBlocked),
                Is.EqualTo("INVALID DESTINATION - PATH BLOCKED"));
            Assert.That(
                GameplayDisplacementController.FormatDestinationFailure(
                    DisplacementResolutionFailure.GetUpSpaceBlocked),
                Is.EqualTo(
                    "INVALID DESTINATION - GET-UP SPACE BLOCKED"));
        }

        [Test]
        public void ActionTooltipUsesAuthoredCostAndExplainsAvailability()
        {
            var action = new DisplacementActionDefinition(
                "close-quarters.push",
                "Push",
                DisplacementActionKind.Push,
                new ActionCost(2, 0f, ActionMobility.Mobile),
                DisplacementSubjectKinds.Prop,
                reach: 1.5f,
                maximumDistance: 3f,
                maximumSubjectMass: 40f,
                DisplacementHandRequirement.OneHandFree,
                DisplacementAutoStowPolicy.Allowed,
                DisplacementContestPolicy.None,
                DisplacementResultPolicies.Topple);

            string tooltip = DisplacementActionTooltipFormatter.Format(
                action,
                DisplacementActionAvailabilityFailure.InsufficientTurnBudget,
                turnBased: true);

            Assert.That(tooltip, Does.Contain("COST - 2 AP"));
            Assert.That(tooltip, Does.Contain("TARGETS - PROPS"));
            Assert.That(tooltip, Does.Contain("REACH - 1.5 M"));
            Assert.That(tooltip, Does.Contain("REQUIRES ONE FREE HAND"));
            Assert.That(tooltip, Does.Contain("UNAVAILABLE - INSUFFICIENT AP"));

            string explorationTooltip =
                DisplacementActionTooltipFormatter.Format(
                    action,
                    DisplacementActionAvailabilityFailure.None,
                    turnBased: false);
            Assert.That(explorationTooltip,
                Does.Contain("COST - FREE OUT OF TURN MODE"));
        }

        [Test]
        public void CombatantDisplacementTooltipShowsStrengthFormula()
        {
            var action = new DisplacementActionDefinition(
                "close-quarters.throw",
                "Throw",
                DisplacementActionKind.Throw,
                new ActionCost(2, 0f, ActionMobility.Set),
                DisplacementSubjectKinds.Combatant,
                reach: 2f,
                maximumDistance: 6f,
                maximumSubjectMass: 90f,
                DisplacementHandRequirement.BothHandsFree,
                DisplacementAutoStowPolicy.Allowed,
                DisplacementContestPolicy.CloseQuartersControl,
                DisplacementResultPolicies.None);

            string tooltip = DisplacementActionTooltipFormatter.Format(
                action,
                DisplacementActionAvailabilityFailure.None,
                turnBased: true,
                controlProfile: new CloseQuartersControlProfile(
                    strengthRating: 4,
                    skillRating: 3,
                    talentId: "talent.leverage",
                    talentModifier: 2));

            Assert.That(
                tooltip,
                Does.Contain(
                    "CONTEST - D20 + STR 4 + CONTROL 3 + TALENT 2"));
        }
    }
}
