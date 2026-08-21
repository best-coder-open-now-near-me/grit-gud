using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayTacticalContextEvidenceTests
    {
        [TestCase(true, true, TacticalVisibilityRelation.Mutual)]
        [TestCase(true, false, TacticalVisibilityRelation.AttackerOnly)]
        [TestCase(false, true, TacticalVisibilityRelation.TargetOnly)]
        [TestCase(false, false, TacticalVisibilityRelation.Neither)]
        public void VisibilityRelationPreservesBothObservationDirections(
            bool attackerSeesTarget,
            bool targetSeesAttacker,
            TacticalVisibilityRelation expected)
        {
            Assert.That(
                GameplayTacticalContextEvidenceRules.ResolveVisibility(
                    attackerSeesTarget,
                    targetSeesAttacker),
                Is.EqualTo(expected));
        }

        [Test]
        public void MissingTargetAwarenessIsConservativelyUnknown()
        {
            var encounter = new GameplayEncounterStateSnapshot();

            Assert.That(
                GameplayTacticalContextEvidenceRules.ResolveTargetAwareness(
                    encounter,
                    "actor.player"),
                Is.EqualTo(TacticalAwarenessBand.Unknown));
        }

        [TestCase(0f, TacticalRangeBand.Contact)]
        [TestCase(2f, TacticalRangeBand.Contact)]
        [TestCase(2.01f, TacticalRangeBand.Close)]
        [TestCase(8f, TacticalRangeBand.Close)]
        [TestCase(9f, TacticalRangeBand.Effective)]
        [TestCase(30f, TacticalRangeBand.Long)]
        [TestCase(41f, TacticalRangeBand.Extreme)]
        public void RangeBandsUseStableAuthoredBoundaries(
            float distance,
            TacticalRangeBand expected)
        {
            Assert.That(
                new GameplayTacticalContextEvidencePolicy().ClassifyRange(
                    distance),
                Is.EqualTo(expected));
        }

        [Test]
        public void RequestRejectsSubjectKindThatDiffersFromCapability()
        {
            GameplayCapabilityProfile profile = new(
                GameplaySemanticCapability.DirectAttack,
                semanticVersion: 1,
                new[]
                {
                    new GameplayCapabilityTrait("subject", "Actor"),
                });

            Assert.Throws<ArgumentException>(() =>
                new GameplayTacticalContextRequest(
                    profile,
                    "actor.player",
                    new GameplaySubjectReference(
                        GameplaySemanticSubjectKind.DestructibleProp,
                        "prop.wall"),
                    soundSignature: 1f));
        }
    }
}
