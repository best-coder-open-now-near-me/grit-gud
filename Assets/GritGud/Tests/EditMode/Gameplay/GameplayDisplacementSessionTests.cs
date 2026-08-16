using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayDisplacementSessionTests
    {
        private static readonly DisplacementActionDefinition PropThrow =
            CreateDisplacementAction(
                "close-quarters.throw-prop",
                "Throw",
                DisplacementActionKind.Throw,
                DisplacementSubjectKinds.Prop);
        private static readonly DisplacementActionDefinition PropPush =
            CreateDisplacementAction(
                "close-quarters.push",
                "Push",
                DisplacementActionKind.Push,
                DisplacementSubjectKinds.Prop);
        private static readonly DisplacementActionDefinition TopplingPush =
            CreateDisplacementAction(
                "close-quarters.toppling-push",
                "Toppling Push",
                DisplacementActionKind.Push,
                DisplacementSubjectKinds.Prop,
                allowedResults: DisplacementResultPolicies.Topple);
        private static readonly DisplacementActionDefinition CombatantThrow =
            CreateDisplacementAction(
                "close-quarters.throw-combatant",
                "Throw",
                DisplacementActionKind.Throw,
                DisplacementSubjectKinds.Combatant,
                DisplacementContestPolicy.CloseQuartersControl);
        private static readonly DisplacementActionDefinition CombatantPush =
            CreateDisplacementAction(
                "close-quarters.push-combatant",
                "Push",
                DisplacementActionKind.Push,
                DisplacementSubjectKinds.Prop
                | DisplacementSubjectKinds.Combatant,
                DisplacementContestPolicy.CloseQuartersControl);

        [Test]
        public void PropThrowUsesSharedRecordAndMovesToContinuousDestination()
        {
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls());
            var destination = new GameplayPosition(1.25f, 0f, 1.75f);

            bool resolved = session.TryDisplaceAction(
                "player",
                PropThrow.Id,
                "crate",
                destination,
                out _,
                out var record,
                out var failure);

            Assert.That(resolved, Is.True);
            Assert.That(failure, Is.EqualTo(DisplacementResolutionFailure.None));
            Assert.That(record.Succeeded, Is.True);
            Assert.That(record.ControlContest, Is.Null);
            Assert.That(record.ResultingPosition.X, Is.EqualTo(1.25f));
            Assert.That(record.ResultingPosition.Z, Is.EqualTo(1.75f));
            Assert.That(session.Records.Count, Is.EqualTo(1));
            Assert.That(
                session.Records[0],
                Is.SameAs(
                    ((DisplacementResolvedJournalEntry)
                        session.Journal.Entries[1]).Displacement));
        }

        [Test]
        public void PropPushUsesTheSharedDisplacementPathAndRecordsItsActionKind()
        {
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls());
            var destination = new GameplayPosition(0f, 0f, 2.25f);

            bool resolved = session.TryDisplaceAction(
                "player",
                PropPush.Id,
                "crate",
                destination,
                out _,
                out DisplacementRecord record,
                out DisplacementResolutionFailure failure);

            Assert.That(resolved, Is.True);
            Assert.That(failure, Is.EqualTo(DisplacementResolutionFailure.None));
            Assert.That(record.Request.ActionKind,
                Is.EqualTo(DisplacementActionKind.Push));
            Assert.That(record.ResultingPosition, Is.EqualTo(destination));
            Assert.That(record.PreviousPropState, Is.Not.Null);
            Assert.That(record.ResultingPropState, Is.Not.Null);
            Assert.That(record.ResultingPropState.Pose.YawDegrees, Is.Zero);
            Assert.That(
                record.ResultingPropState.Posture,
                Is.EqualTo(DestructiblePropPosture.Upright));
            Assert.That(session.Journal.Entries.Count, Is.EqualTo(2));
        }

        [Test]
        public void EligiblePropAndActionCommitDeterministicToppledState()
        {
            var paths = new CapturePaths();
            GameplayDisplacementSession session = CreateSession(
                paths,
                new FixedRolls(),
                out _,
                out DestructiblePropSession destructibles,
                playerPush: TopplingPush,
                propToppling: new PropTopplingDefinition(0f, 90f, 0.5f));

            bool resolved = session.TryDisplaceAction(
                "player",
                TopplingPush.Id,
                "crate",
                new GameplayPosition(0f, 0f, 2f),
                out _,
                out DisplacementRecord record,
                out DisplacementResolutionFailure failure);

            Assert.That(resolved, Is.True);
            Assert.That(failure, Is.EqualTo(DisplacementResolutionFailure.None));
            Assert.That(record.AppliedResults,
                Is.EqualTo(DisplacementResultPolicies.Topple));
            Assert.That(record.ResultingPropState.Posture,
                Is.EqualTo(DestructiblePropPosture.Toppled));
            Assert.That(record.ResultingPropState.Pose.Position.Y,
                Is.EqualTo(0.5f));
            Assert.That(record.ResultingPropState.Pose.RollDegrees,
                Is.EqualTo(90f));
            Assert.That(paths.ResultingPropState,
                Is.SameAs(record.ResultingPropState));
            Assert.That(destructibles.GetProp("crate").Posture,
                Is.EqualTo(DestructiblePropPosture.Toppled));
        }

        [Test]
        public void PreviewValidatesTheSameToppledPoseThatCommitWouldFreeze()
        {
            var paths = new CapturePaths(block: true);
            GameplayDisplacementSession session = CreateSession(
                paths,
                new FixedRolls(),
                out _,
                out _,
                playerPush: TopplingPush,
                propToppling: new PropTopplingDefinition(90f, 0f, 0.4f));

            DisplacementDestinationEvaluation result =
                session.EvaluateDestination(
                    "player",
                    TopplingPush.Id,
                    "crate",
                    new GameplayPosition(0f, 0f, 2f));

            Assert.That(result.Failure,
                Is.EqualTo(DisplacementResolutionFailure.DestinationBlocked));
            Assert.That(result.Destination.Y, Is.Zero);
            Assert.That(paths.ResultingPropState.Posture,
                Is.EqualTo(DestructiblePropPosture.Toppled));
            Assert.That(paths.ResultingPropState.Pose.PitchDegrees,
                Is.EqualTo(90f));
            Assert.That(session.Records, Is.Empty);
        }

        [Test]
        public void AlreadyToppledPropMovesWithoutApplyingToppleTwice()
        {
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls(),
                out _,
                out _,
                playerPush: TopplingPush,
                propToppling: new PropTopplingDefinition(0f, 90f, 0.5f));
            Assert.That(session.TryDisplaceAction(
                "player",
                TopplingPush.Id,
                "crate",
                new GameplayPosition(0f, 0f, 2f),
                out _,
                out _,
                out _), Is.True);

            bool moved = session.TryDisplaceAction(
                "player",
                TopplingPush.Id,
                "crate",
                new GameplayPosition(0.5f, 0.5f, 2f),
                out _,
                out DisplacementRecord second,
                out _);

            Assert.That(moved, Is.True);
            Assert.That(second.AppliedResults,
                Is.EqualTo(DisplacementResultPolicies.None));
            Assert.That(second.ResultingPropState.Posture,
                Is.EqualTo(DestructiblePropPosture.Toppled));
            Assert.That(second.ResultingPropState.Pose.RollDegrees,
                Is.EqualTo(90f));
        }

        [Test]
        public void PropPushActionSpendsAuthoredApBeforeCommittingDisplacement()
        {
            GameplaySession gameplay;
            DestructiblePropSession destructibles;
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls(),
                out gameplay,
                out destructibles);
            gameplay.BeginEncounter();
            int journalEntryCount = session.Journal.Entries.Count;

            bool resolved = session.TryDisplaceAction(
                "player",
                PropPush.Id,
                "crate",
                new GameplayPosition(0f, 0f, 2f),
                out GameplayActionRecord action,
                out DisplacementRecord displacement,
                out DisplacementResolutionFailure failure);

            Assert.That(resolved, Is.True);
            Assert.That(failure, Is.EqualTo(DisplacementResolutionFailure.None));
            Assert.That(action.Request.ActionId, Is.EqualTo("close-quarters.push"));
            Assert.That(gameplay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(3));
            Assert.That(displacement.Request.ActionKind,
                Is.EqualTo(DisplacementActionKind.Push));
            Assert.That(session.Journal.Entries[journalEntryCount],
                Is.TypeOf<ActionResolvedJournalEntry>());
            Assert.That(session.Journal.Entries[journalEntryCount + 1],
                Is.TypeOf<DisplacementResolvedJournalEntry>());
        }

        [Test]
        public void ActionAvailabilityAllowsExplorationAndUsesTurnBudgetInCombat()
        {
            GameplaySession gameplay;
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls(),
                out gameplay,
                out _);

            DisplacementActionAvailability exploration =
                session.EvaluateActionAvailability("player", PropPush.Id);

            Assert.That(exploration.IsAvailable, Is.True);
            Assert.That(
                exploration.Failure,
                Is.EqualTo(DisplacementActionAvailabilityFailure.None));
            Assert.That(exploration.Action, Is.SameAs(PropPush));

            gameplay.BeginEncounter();
            DisplacementActionAvailability activeTurn =
                session.EvaluateActionAvailability("player", PropPush.Id);

            Assert.That(activeTurn.IsAvailable, Is.True);
            Assert.That(
                activeTurn.Failure,
                Is.EqualTo(DisplacementActionAvailabilityFailure.None));
        }

        [Test]
        public void ExplorationPushCommitsRecordedDisplacementWithoutSpendingAp()
        {
            GameplaySession gameplay;
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls(),
                out gameplay,
                out _);

            bool resolved = session.TryDisplaceAction(
                "player",
                PropPush.Id,
                "crate",
                new GameplayPosition(0f, 0f, 2f),
                out GameplayActionRecord action,
                out DisplacementRecord displacement,
                out DisplacementResolutionFailure failure);

            Assert.That(resolved, Is.True);
            Assert.That(failure, Is.EqualTo(DisplacementResolutionFailure.None));
            Assert.That(action.Cost.ActionPoints, Is.Zero);
            Assert.That(action.PreviousBudget.ActionPoints, Is.EqualTo(4));
            Assert.That(action.ResultingBudget.ActionPoints, Is.EqualTo(4));
            Assert.That(gameplay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(4));
            Assert.That(displacement.ResultingPosition,
                Is.EqualTo(new GameplayPosition(0f, 0f, 2f)));
            Assert.That(gameplay.ResolvedActions, Has.Count.EqualTo(1));
            Assert.That(session.Records, Has.Count.EqualTo(1));
        }

        [Test]
        public void CommittedPushFacesTheActorTowardItsSubject()
        {
            GameplaySession gameplay;
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls(),
                out gameplay,
                out _,
                playerFacingDegrees: 180f);

            bool resolved = session.TryDisplaceAction(
                "player",
                PropPush.Id,
                "crate",
                new GameplayPosition(0f, 0f, 2f),
                out _,
                out _,
                out DisplacementResolutionFailure failure);

            Assert.That(resolved, Is.True);
            Assert.That(failure, Is.EqualTo(DisplacementResolutionFailure.None));
            Assert.That(gameplay.GetActor("player").Pose.FacingDegrees,
                Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ExplorationCombatantPushUsesAuthoredControlAndOrdinaryActionPath()
        {
            GameplaySession gameplay;
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls(8, 10),
                out gameplay,
                out _,
                CombatantPush);

            bool resolved = session.TryDisplaceAction(
                "player",
                CombatantPush.Id,
                "target",
                new GameplayPosition(2f, 0f, 0f),
                out GameplayActionRecord action,
                out DisplacementRecord displacement,
                out DisplacementResolutionFailure failure);

            Assert.That(resolved, Is.True);
            Assert.That(failure, Is.EqualTo(DisplacementResolutionFailure.None));
            Assert.That(displacement.Succeeded, Is.True);
            Assert.That(displacement.Request.SubjectKind,
                Is.EqualTo(DisplacementSubjectKind.Combatant));
            Assert.That(displacement.ControlContest.Attacker.TalentId,
                Is.EqualTo("talent.leverage"));
            Assert.That(action.Cost.ActionPoints, Is.Zero);
            Assert.That(gameplay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(4));
            Assert.That(gameplay.GetActor("target").Pose.Position,
                Is.EqualTo(new GameplayPosition(2f, 0f, 0f)));
            Assert.That(gameplay.ResolvedActions, Has.Count.EqualTo(1));
            Assert.That(session.Records, Has.Count.EqualTo(1));
        }

        [Test]
        public void ResponsiveCombatantDisplacementCommitsTurnCostBeforeEncounter()
        {
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls(8, 10),
                out GameplaySession gameplay,
                out _,
                CombatantPush,
                responsiveTarget: true);

            Assert.That(session.TryDisplaceAction(
                "player",
                CombatantPush.Id,
                "target",
                new GameplayPosition(2f, 0f, 0f),
                out GameplayActionRecord action,
                out _,
                out DisplacementResolutionFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(DisplacementResolutionFailure.None));
            Assert.That(action.Cost.ActionPoints,
                Is.EqualTo(CombatantPush.Cost.ActionPoints));
            Assert.That(gameplay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(4 - CombatantPush.Cost.ActionPoints));
            Assert.That(gameplay.Mode,
                Is.EqualTo(GameplaySessionMode.Exploration));
            Assert.That(gameplay.ActionStartsEncounter(action), Is.True);
            Assert.That(gameplay.BeginEncounterFromAction(action), Is.True);
            Assert.That(gameplay.EncounterActive, Is.True);
        }

        [Test]
        public void CombatantDestinationPreviewDoesNotRollOrMutate()
        {
            GameplaySession gameplay;
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new ThrowIfRolled(),
                out gameplay,
                out _,
                CombatantPush);

            DisplacementDestinationEvaluation evaluation =
                session.EvaluateDestination(
                    "player",
                    CombatantPush.Id,
                    "target",
                    new GameplayPosition(2f, 0f, 0f));

            Assert.That(evaluation.IsEligible, Is.True);
            Assert.That(evaluation.Origin,
                Is.EqualTo(new GameplayPosition(1f, 0f, 0f)));
            Assert.That(gameplay.GetActor("target").Pose.Position,
                Is.EqualTo(new GameplayPosition(1f, 0f, 0f)));
            Assert.That(gameplay.ResolvedActions, Is.Empty);
            Assert.That(session.Records, Is.Empty);
        }

        [Test]
        public void PushIntentDerivesMaximumDestinationAwayFromActor()
        {
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls());

            DisplacementDestinationEvaluation evaluation =
                session.EvaluateIntentDestination(
                    "player",
                    PropPush.Id,
                    "crate");

            Assert.That(evaluation.IsEligible, Is.True);
            Assert.That(evaluation.Origin,
                Is.EqualTo(new GameplayPosition(0f, 0f, 1f)));
            Assert.That(evaluation.Destination,
                Is.EqualTo(new GameplayPosition(0f, 0f, 4f)));
            Assert.That(evaluation.Distance, Is.EqualTo(3f));
            Assert.That(session.Records, Is.Empty);
        }

        [Test]
        public void PushIntentStopsAtFarthestValidatedDistanceBeforeObstacle()
        {
            GameplayDisplacementSession session = CreateSession(
                new BlockBeyondDistance(1.25f),
                new FixedRolls());

            DisplacementDestinationEvaluation evaluation =
                session.EvaluateIntentDestination(
                    "player",
                    PropPush.Id,
                    "crate");

            Assert.That(evaluation.IsEligible, Is.True);
            Assert.That(evaluation.Distance,
                Is.EqualTo(1.25f).Within(0.01f));
            Assert.That(evaluation.Destination.Z,
                Is.EqualTo(2.25f).Within(0.01f));
            Assert.That(session.Records, Is.Empty);
        }

        [Test]
        public void DestinationPreviewUsesTheSameValidationWithoutMutatingState()
        {
            GameplaySession gameplay;
            DestructiblePropSession destructibles;
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls(),
                out gameplay,
                out destructibles);
            GameplayPosition destination = new GameplayPosition(1f, 0f, 2f);
            int journalEntryCount = session.Journal.Entries.Count;

            DisplacementDestinationEvaluation evaluation =
                session.EvaluateDestination(
                    "player",
                    PropPush.Id,
                    "crate",
                    destination);

            Assert.That(evaluation.IsEligible, Is.True);
            Assert.That(evaluation.Failure,
                Is.EqualTo(DisplacementResolutionFailure.None));
            Assert.That(evaluation.Origin,
                Is.EqualTo(new GameplayPosition(0f, 0f, 1f)));
            Assert.That(evaluation.Destination, Is.EqualTo(destination));
            Assert.That(evaluation.Distance,
                Is.EqualTo((float)Math.Sqrt(2f)).Within(0.001f));
            Assert.That(destructibles.GetProp("crate").Position,
                Is.EqualTo(new GameplayPosition(0f, 0f, 1f)));
            Assert.That(gameplay.ResolvedActions, Is.Empty);
            Assert.That(session.Records, Is.Empty);
            Assert.That(session.Journal.Entries.Count,
                Is.EqualTo(journalEntryCount));
        }

        [TestCase(0f, 1f, DisplacementResolutionFailure.DestinationUnchanged)]
        [TestCase(0f, 5f, DisplacementResolutionFailure.DestinationTooFar)]
        public void DestinationPreviewReportsStructuredGeometryFailures(
            float destinationX,
            float destinationZ,
            DisplacementResolutionFailure expectedFailure)
        {
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls());

            DisplacementDestinationEvaluation evaluation =
                session.EvaluateDestination(
                    "player",
                    PropPush.Id,
                    "crate",
                    new GameplayPosition(
                        destinationX,
                        0f,
                        destinationZ));

            Assert.That(evaluation.IsEligible, Is.False);
            Assert.That(evaluation.Failure, Is.EqualTo(expectedFailure));
            Assert.That(session.Records, Is.Empty);
        }

        [Test]
        public void DestinationPreviewReportsBlockedAuthoritativePath()
        {
            GameplayDisplacementSession session = CreateSession(
                new BlockPaths(),
                new FixedRolls());

            DisplacementDestinationEvaluation evaluation =
                session.EvaluateDestination(
                    "player",
                    PropPush.Id,
                    "crate",
                    new GameplayPosition(0f, 0f, 2f));

            Assert.That(evaluation.IsEligible, Is.False);
            Assert.That(evaluation.Failure,
                Is.EqualTo(DisplacementResolutionFailure.DestinationBlocked));
            Assert.That(session.Records, Is.Empty);
        }

        [Test]
        public void ThrowDistanceDecaysContinuouslyWithSubjectMass()
        {
            DisplacementActionDefinition action = CreateDisplacementAction(
                "close-quarters.weighted-throw",
                "Throw",
                DisplacementActionKind.Throw,
                DisplacementSubjectKinds.Prop,
                maximumSubjectMass: 90f,
                maximumDistance: 6f,
                distanceDecay: new DisplacementDistanceDecayDefinition(
                    fullDistanceMass: 15f,
                    minimumDistance: 0.75f,
                    exponent: 1f));

            Assert.That(
                action.GetMaximumDistance(
                    15f,
                    DisplacementSizeClass.Small),
                Is.EqualTo(6f));
            Assert.That(
                action.GetMaximumDistance(
                    35f,
                    DisplacementSizeClass.Medium),
                Is.EqualTo(4.6f).Within(0.001f));
            Assert.That(
                action.GetMaximumDistance(
                    90f,
                    DisplacementSizeClass.Medium),
                Is.EqualTo(0.75f));
        }

        [Test]
        public void ThrowRejectsSubjectBeyondAuthoredSizeLimit()
        {
            DisplacementActionDefinition action = CreateDisplacementAction(
                "close-quarters.small-throw",
                "Throw",
                DisplacementActionKind.Throw,
                DisplacementSubjectKinds.Prop,
                maximumSubjectSize: DisplacementSizeClass.Small);
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls(),
                out _,
                out _,
                action);

            DisplacementTargetEvaluation evaluation = session.EvaluateTarget(
                "player",
                action.Id,
                "crate");

            Assert.That(evaluation.IsEligible, Is.False);
            Assert.That(evaluation.Failure,
                Is.EqualTo(DisplacementTargetFailure.SubjectTooLarge));
        }

        [Test]
        public void ThrowAtomicallyStowsTwoHandedWeaponAndChargesCombinedCost()
        {
            DisplacementActionDefinition action = CreateDisplacementAction(
                "close-quarters.weighted-throw",
                "Throw",
                DisplacementActionKind.Throw,
                DisplacementSubjectKinds.Prop,
                actionPoints: 2,
                maximumSubjectMass: 90f,
                maximumDistance: 6f,
                handRequirement: DisplacementHandRequirement.BothHandsFree,
                autoStowPolicy: DisplacementAutoStowPolicy.Allowed,
                distanceDecay: new DisplacementDistanceDecayDefinition(
                    15f,
                    0.75f,
                    1f));
            var rifle = new InventoryItemDefinition(
                "weapon.rifle",
                "Rifle",
                1,
                InventoryItemKind.Weapon,
                new ActionCost(1, 0f, ActionMobility.Set),
                EquipmentEffectSet.None,
                new AttackDefinition(
                    "attack.rifle",
                    "Fire rifle",
                    new ActionCost(1, 0f, ActionMobility.Set),
                    2f,
                    projectile: null,
                    accuracyDecay: AccuracyDecayDefinition.None),
                occupiedHands: 2);
            GameplaySession gameplay;
            DestructiblePropSession destructibles;
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls(),
                out gameplay,
                out destructibles,
                action,
                equippedItem: rifle);
            gameplay.BeginEncounter();

            DisplacementActionAvailability availability =
                session.EvaluateActionAvailability("player", action.Id);
            bool resolved = session.TryDisplaceAction(
                "player",
                action.Id,
                "crate",
                new GameplayPosition(0f, 0f, 5f),
                out GameplayActionRecord committed,
                out _,
                out DisplacementResolutionFailure failure);

            Assert.That(availability.IsAvailable, Is.True);
            Assert.That(availability.RequiresAutoStow, Is.True);
            Assert.That(availability.AutoStowItemId,
                Is.EqualTo("weapon.rifle"));
            Assert.That(availability.ResolvedCost.ActionPoints, Is.EqualTo(3));
            Assert.That(resolved, Is.True);
            Assert.That(failure, Is.EqualTo(DisplacementResolutionFailure.None));
            Assert.That(committed.Outcomes, Has.Count.EqualTo(2));
            Assert.That(committed.Outcomes[0],
                Is.TypeOf<EquipmentChangedActionOutcome>());
            Assert.That(committed.Outcomes[1],
                Is.TypeOf<DisplacementActionOutcome>());
            Assert.That(gameplay.GetActor("player").EquippedItemId, Is.Null);
            Assert.That(gameplay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(1));
            Assert.That(destructibles.GetProp("crate").Position,
                Is.EqualTo(new GameplayPosition(0f, 0f, 5f)));
        }

        [Test]
        public void FailedThrowLeavesWeaponBudgetAndWorldUnchanged()
        {
            DisplacementActionDefinition action = CreateDisplacementAction(
                "close-quarters.blocked-throw",
                "Throw",
                DisplacementActionKind.Throw,
                DisplacementSubjectKinds.Prop,
                actionPoints: 2,
                maximumSubjectMass: 90f,
                maximumDistance: 6f,
                handRequirement: DisplacementHandRequirement.BothHandsFree,
                autoStowPolicy: DisplacementAutoStowPolicy.Allowed,
                distanceDecay: new DisplacementDistanceDecayDefinition(
                    15f,
                    0.75f,
                    1f));
            InventoryItemDefinition rifle = CreateTwoHandedRifle();
            GameplaySession gameplay;
            DestructiblePropSession destructibles;
            GameplayDisplacementSession session = CreateSession(
                new BlockPaths(),
                new FixedRolls(),
                out gameplay,
                out destructibles,
                action,
                equippedItem: rifle);
            gameplay.BeginEncounter();

            bool resolved = session.TryDisplaceAction(
                "player",
                action.Id,
                "crate",
                new GameplayPosition(0f, 0f, 4f),
                out _,
                out _,
                out DisplacementResolutionFailure failure);

            Assert.That(resolved, Is.False);
            Assert.That(failure,
                Is.EqualTo(DisplacementResolutionFailure.DestinationBlocked));
            Assert.That(gameplay.GetActor("player").EquippedItemId,
                Is.EqualTo("weapon.rifle"));
            Assert.That(gameplay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(4));
            Assert.That(gameplay.ResolvedActions, Is.Empty);
            Assert.That(destructibles.GetProp("crate").Position,
                Is.EqualTo(new GameplayPosition(0f, 0f, 1f)));
        }

        [Test]
        public void UnaffordablePushDoesNotMovePropOrCreateJournalEntries()
        {
            GameplaySession gameplay;
            DestructiblePropSession destructibles;
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls(),
                out gameplay,
                out destructibles,
                CreateDisplacementAction(
                    PropPush.Id,
                    PropPush.DisplayName,
                    PropPush.Intent,
                    PropPush.AcceptedSubjects,
                    actionPoints: 5));
            gameplay.BeginEncounter();
            int journalEntryCount = session.Journal.Entries.Count;

            DisplacementActionAvailability availability =
                session.EvaluateActionAvailability("player", PropPush.Id);

            bool resolved = session.TryDisplaceAction(
                "player",
                PropPush.Id,
                "crate",
                new GameplayPosition(0f, 0f, 2f),
                out _,
                out _,
                out DisplacementResolutionFailure failure);

            Assert.That(availability.IsAvailable, Is.False);
            Assert.That(
                availability.Failure,
                Is.EqualTo(
                    DisplacementActionAvailabilityFailure.InsufficientTurnBudget));
            Assert.That(resolved, Is.False);
            Assert.That(failure,
                Is.EqualTo(DisplacementResolutionFailure.InsufficientTurnBudget));
            Assert.That(destructibles.GetProp("crate").Position,
                Is.EqualTo(new GameplayPosition(0f, 0f, 1f)));
            Assert.That(session.Journal.Entries.Count,
                Is.EqualTo(journalEntryCount));
        }

        [Test]
        public void UnknownActionDoesNotMovePropOrSpendActionPoints()
        {
            GameplaySession gameplay;
            DestructiblePropSession destructibles;
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls(),
                out gameplay,
                out destructibles);
            gameplay.BeginEncounter();
            int journalEntryCount = session.Journal.Entries.Count;

            bool resolved = session.TryDisplaceAction(
                "player",
                "close-quarters.unknown",
                "crate",
                new GameplayPosition(0f, 0f, 2f),
                out _,
                out _,
                out DisplacementResolutionFailure failure);

            Assert.That(resolved, Is.False);
            Assert.That(failure,
                Is.EqualTo(DisplacementResolutionFailure.ActionUnavailable));
            Assert.That(gameplay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(4));
            Assert.That(destructibles.GetProp("crate").Position,
                Is.EqualTo(new GameplayPosition(0f, 0f, 1f)));
            Assert.That(session.Journal.Entries.Count,
                Is.EqualTo(journalEntryCount));
        }

        [Test]
        public void TargetEvaluationAcceptsAuthoredPropByStableId()
        {
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls());

            DisplacementTargetEvaluation evaluation = session.EvaluateTarget(
                "player",
                PropPush.Id,
                "crate");

            Assert.That(evaluation.IsEligible, Is.True);
            Assert.That(evaluation.Failure,
                Is.EqualTo(DisplacementTargetFailure.None));
            Assert.That(evaluation.Subject.Kind,
                Is.EqualTo(DisplacementSubjectKind.Prop));
            Assert.That(evaluation.Subject.Mass, Is.EqualTo(35f));
            Assert.That(evaluation.Distance, Is.EqualTo(1f));
        }

        [Test]
        public void TargetEvaluationRejectsCandidateKindNotAcceptedByAction()
        {
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls());

            DisplacementTargetEvaluation evaluation = session.EvaluateTarget(
                "player",
                PropPush.Id,
                "target");

            Assert.That(evaluation.IsEligible, Is.False);
            Assert.That(evaluation.Failure,
                Is.EqualTo(
                    DisplacementTargetFailure.SubjectKindNotAccepted));
            Assert.That(evaluation.Subject.Kind,
                Is.EqualTo(DisplacementSubjectKind.Combatant));
        }

        [Test]
        public void TargetEvaluationRejectsActingCombatantAsItsOwnSubject()
        {
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls());

            DisplacementTargetEvaluation evaluation = session.EvaluateTarget(
                "player",
                CombatantThrow.Id,
                "player");

            Assert.That(evaluation.IsEligible, Is.False);
            Assert.That(evaluation.Failure,
                Is.EqualTo(DisplacementTargetFailure.SelfTarget));
        }

        [Test]
        public void TargetEvaluationRejectsMassFromAuthoritativeSubjectProfile()
        {
            DisplacementActionDefinition lightPush =
                CreateDisplacementAction(
                    "close-quarters.light-push",
                    "Light push",
                    DisplacementActionKind.Push,
                    DisplacementSubjectKinds.Prop,
                    maximumSubjectMass: 30f);
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls(),
                out _,
                out _,
                lightPush);

            DisplacementTargetEvaluation evaluation = session.EvaluateTarget(
                "player",
                lightPush.Id,
                "crate");

            Assert.That(evaluation.IsEligible, Is.False);
            Assert.That(evaluation.Failure,
                Is.EqualTo(DisplacementTargetFailure.SubjectTooHeavy));
            Assert.That(evaluation.Subject.Mass, Is.EqualTo(35f));
            Assert.That(evaluation.Action.MaximumSubjectMass,
                Is.EqualTo(30f));
        }

        [Test]
        public void TargetEvaluationRejectsUnknownCandidateWithoutGuessingKind()
        {
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls());

            DisplacementTargetEvaluation evaluation = session.EvaluateTarget(
                "player",
                PropPush.Id,
                "crate.not-authored");

            Assert.That(evaluation.IsEligible, Is.False);
            Assert.That(evaluation.Failure,
                Is.EqualTo(DisplacementTargetFailure.CandidateUnavailable));
            Assert.That(evaluation.Subject, Is.Null);
        }

        [Test]
        public void TargetEvaluationRejectsSubjectBeyondAuthoredReach()
        {
            DisplacementActionDefinition shortReachPush =
                CreateDisplacementAction(
                    "close-quarters.short-push",
                    "Short push",
                    DisplacementActionKind.Push,
                    DisplacementSubjectKinds.Prop,
                    reach: 0.5f);
            GameplayDisplacementSession session = CreateSession(
                new ThrowIfPathValidated(),
                new FixedRolls(),
                out _,
                out _,
                shortReachPush);

            DisplacementTargetEvaluation evaluation = session.EvaluateTarget(
                "player",
                shortReachPush.Id,
                "crate");

            Assert.That(evaluation.IsEligible, Is.False);
            Assert.That(evaluation.Failure,
                Is.EqualTo(DisplacementTargetFailure.SubjectOutOfReach));
            Assert.That(evaluation.Distance, Is.EqualTo(1f));
        }

        [Test]
        public void StaleWorldStateRejectsPushBeforeSpendingActionPoints()
        {
            var journal = new GameplayJournal();
            var gameplay = new GameplaySession(new ScenarioDefinition(
                "atomic-displacement-test",
                new ScenarioTimingDefinition(1.25f),
                new[]
                {
                    CreateActor("player", new GameplayPosition(0f, 0f, 0f)),
                    CreateActor("target", new GameplayPosition(1f, 0f, 0f)),
                },
                new ScenarioObjectiveDefinition[0]),
                journal);
            var destructibles = new DestructiblePropSession(new[]
            {
                new DestructiblePropDefinition(
                    "crate",
                    10f,
                    DestructiblePropState.Intact,
                    new GameplayPosition(0f, 0f, 1f)),
            }, journal);
            var session = new GameplayDisplacementSession(
                gameplay,
                destructibles,
                CreateSubjects(),
                new MovePropDuringValidation(destructibles),
                new FixedRolls());
            gameplay.BeginEncounter();
            int journalEntryCount = journal.Entries.Count;

            Assert.Throws<InvalidOperationException>(() =>
                session.TryDisplaceAction(
                    "player",
                    PropPush.Id,
                    "crate",
                    new GameplayPosition(0f, 0f, 2f),
                    out _,
                    out _,
                    out _));

            Assert.That(
                gameplay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(4));
            Assert.That(gameplay.LastResolvedAction, Is.Null);
            Assert.That(session.Records, Is.Empty);
            Assert.That(journal.Entries.Count, Is.EqualTo(journalEntryCount));
            Assert.That(destructibles.GetProp("crate").Position,
                Is.EqualTo(new GameplayPosition(0f, 0f, 1.25f)));
        }

        [Test]
        public void LeverageTalentCanWinRecordedOpposedControlCheck()
        {
            GameplaySession gameplay;
            DestructiblePropSession destructibles;
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls(8, 10),
                out gameplay,
                out destructibles);
            bool resolved = session.TryDisplaceAction(
                "player",
                CombatantThrow.Id,
                "target",
                new GameplayPosition(2f, 0f, 1f),
                out _,
                out var record,
                out var failure);

            Assert.That(resolved, Is.True);
            Assert.That(failure, Is.EqualTo(DisplacementResolutionFailure.None));
            Assert.That(record.Succeeded, Is.True);
            Assert.That(record.ControlContest.AttackerTotal, Is.EqualTo(18));
            Assert.That(record.ControlContest.DefenderTotal, Is.EqualTo(17));
            Assert.That(record.ControlContest.Attacker.TalentId,
                Is.EqualTo("talent.leverage"));
            Assert.That(record.ControlContest.Attacker.TalentModifier, Is.EqualTo(2));
            Assert.That(gameplay.GetActor("target").Pose.Position.X, Is.EqualTo(2f));
        }

        [Test]
        public void FailedControlCheckIsRecordedWithoutMovingCombatant()
        {
            GameplaySession gameplay;
            DestructiblePropSession destructibles;
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new FixedRolls(5, 10),
                out gameplay,
                out destructibles);

            bool resolved = session.TryDisplaceAction(
                "player",
                CombatantThrow.Id,
                "target",
                new GameplayPosition(2f, 0f, 1f),
                out _,
                out var record,
                out var failure);

            Assert.That(resolved, Is.True);
            Assert.That(failure, Is.EqualTo(DisplacementResolutionFailure.None));
            Assert.That(record.Succeeded, Is.False);
            Assert.That(record.PreviousPosition.X, Is.EqualTo(1f));
            Assert.That(record.ResultingPosition.X, Is.EqualTo(1f));
            Assert.That(gameplay.GetActor("target").Pose.Position.X, Is.EqualTo(1f));
            Assert.That(session.Records.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReplayCommitsRecordedRollOutcomeWithoutRollingAgain()
        {
            GameplayDisplacementSession source = CreateSession(
                new AllowPaths(),
                new FixedRolls(8, 10));
            source.TryDisplaceAction(
                "player",
                CombatantThrow.Id,
                "target",
                new GameplayPosition(2f, 0f, 1f),
                out _,
                out var record,
                out _);
            GameplaySession replayGameplay;
            DestructiblePropSession replayDestructibles;
            GameplayDisplacementSession replay = CreateSession(
                new AllowPaths(),
                new ThrowIfRolled(),
                out replayGameplay,
                out replayDestructibles);

            replay.Commit(record);

            Assert.That(replayGameplay.GetActor("target").Pose.Position.X,
                Is.EqualTo(2f));
            Assert.That(replay.Records.Count, Is.EqualTo(1));
        }

        [Test]
        public void BlockedDestinationIsRejectedBeforeRollOrRecord()
        {
            GameplayDisplacementSession session = CreateSession(
                new BlockPaths(),
                new ThrowIfRolled());

            bool resolved = session.TryDisplaceAction(
                "player",
                CombatantThrow.Id,
                "target",
                new GameplayPosition(2f, 0f, 1f),
                out _,
                out var record,
                out var failure);

            Assert.That(resolved, Is.False);
            Assert.That(record, Is.Null);
            Assert.That(failure, Is.EqualTo(DisplacementResolutionFailure.DestinationBlocked));
            Assert.That(session.Records, Is.Empty);
        }

        [Test]
        public void UnacceptedCombatantSubjectIsRejectedBeforeRolling()
        {
            GameplayDisplacementSession session = CreateSession(
                new AllowPaths(),
                new ThrowIfRolled());

            bool resolved = session.TryDisplaceAction(
                "player",
                PropThrow.Id,
                "target",
                new GameplayPosition(2f, 0f, 1f),
                out _,
                out _,
                out DisplacementResolutionFailure failure);

            Assert.That(resolved, Is.False);
            Assert.That(failure,
                Is.EqualTo(DisplacementResolutionFailure.SubjectKindNotAccepted));
            Assert.That(session.Records, Is.Empty);
        }

        private static GameplayDisplacementSession CreateSession(
            IDisplacementPathValidator validator,
            ID20RollSource rolls)
        {
            return CreateSession(
                validator,
                rolls,
                out _,
                out _);
        }

        private static GameplayDisplacementSession CreateSession(
            IDisplacementPathValidator validator,
            ID20RollSource rolls,
            out GameplaySession gameplay,
            out DestructiblePropSession destructibles,
            DisplacementActionDefinition playerPush = null,
            float playerFacingDegrees = 0f,
            InventoryItemDefinition equippedItem = null,
            bool responsiveTarget = false,
            PropTopplingDefinition propToppling = null)
        {
            var journal = new GameplayJournal();
            gameplay = new GameplaySession(new ScenarioDefinition(
                "displacement-test",
                new ScenarioTimingDefinition(1.25f),
                new[]
                {
                    CreateActor(
                        "player",
                        new GameplayPosition(0f, 0f, 0f),
                        playerPush == null
                            ? CreateDefaultActions()
                            : new[] { playerPush },
                        playerFacingDegrees,
                        equippedItem),
                    CreateActor("target", new GameplayPosition(1f, 0f, 0f)),
                },
                new ScenarioObjectiveDefinition[0],
                responsiveTarget
                    ? new[]
                    {
                        new AttackResponseDefinition(
                            "target",
                            startsEncounter: true),
                    }
                    : Array.Empty<AttackResponseDefinition>()),
                journal);
            destructibles = new DestructiblePropSession(new[]
            {
                new DestructiblePropDefinition(
                    "crate",
                    10f,
                    DestructiblePropState.Intact,
                    new GameplayPosition(0f, 0f, 1f)),
            }, journal);
            return new GameplayDisplacementSession(
                gameplay,
                destructibles,
                CreateSubjects(propToppling),
                validator,
                rolls,
                CreateControlProfiles());
        }

        private static ScenarioActorDefinition CreateActor(
            string id,
            GameplayPosition position,
            IEnumerable<DisplacementActionDefinition> displacementActions = null,
            float facingDegrees = 0f,
            InventoryItemDefinition equippedItem = null)
        {
            IEnumerable<DisplacementActionDefinition> resolvedActions =
                displacementActions
                ?? (string.Equals(id, "player", StringComparison.Ordinal)
                    ? CreateDefaultActions()
                    : Array.Empty<DisplacementActionDefinition>());
            var actions = new List<DisplacementActionDefinition>(
                resolvedActions);
            DisplacementAbilityDefinition ability = actions.Count == 0
                ? null
                : new DisplacementAbilityDefinition(
                    "ability.displace",
                    "Displace",
                    hotbarSlot: 4,
                    actions);
            return equippedItem == null
                ? new ScenarioActorDefinition(
                    id,
                    initiative: 0,
                    new GameplayActorPose(position, facingDegrees),
                    new TurnBudget(4, 8f),
                    attack: null,
                    displacementAbility: ability)
                : new ScenarioActorDefinition(
                    id,
                    initiative: 0,
                    new GameplayActorPose(position, facingDegrees),
                    new TurnBudget(4, 8f),
                    new[] { equippedItem },
                    equippedItem.Id,
                    displacementAbility: ability);
        }

        private static IReadOnlyList<DisplacementActionDefinition>
            CreateDefaultActions() =>
            new[] { PropPush, PropThrow, CombatantThrow };

        private static IReadOnlyList<DisplacementSubjectDefinition>
            CreateSubjects(PropTopplingDefinition propToppling = null) =>
            new[]
            {
                new DisplacementSubjectDefinition(
                    "player",
                    DisplacementSubjectKind.Combatant,
                    80f),
                new DisplacementSubjectDefinition(
                    "target",
                    DisplacementSubjectKind.Combatant,
                    80f),
                new DisplacementSubjectDefinition(
                    "crate",
                    DisplacementSubjectKind.Prop,
                    35f,
                    toppling: propToppling),
            };

        private static IReadOnlyDictionary<string, CloseQuartersControlProfile>
            CreateControlProfiles() =>
            new Dictionary<string, CloseQuartersControlProfile>(
                StringComparer.Ordinal)
            {
                ["player"] = new CloseQuartersControlProfile(
                    3,
                    5,
                    "talent.leverage",
                    2),
                ["target"] = new CloseQuartersControlProfile(3, 4),
            };

        private static InventoryItemDefinition CreateTwoHandedRifle() =>
            new InventoryItemDefinition(
                "weapon.rifle",
                "Rifle",
                1,
                InventoryItemKind.Weapon,
                new ActionCost(1, 0f, ActionMobility.Set),
                EquipmentEffectSet.None,
                new AttackDefinition(
                    "attack.rifle",
                    "Fire rifle",
                    new ActionCost(1, 0f, ActionMobility.Set),
                    2f,
                    projectile: null,
                    accuracyDecay: AccuracyDecayDefinition.None),
                occupiedHands: 2);

        private static DisplacementActionDefinition CreateDisplacementAction(
            string id,
            string displayName,
            DisplacementActionKind intent,
            DisplacementSubjectKinds acceptedSubjects,
            DisplacementContestPolicy contestPolicy =
                DisplacementContestPolicy.None,
            int actionPoints = 1,
            float reach = 10f,
            float maximumSubjectMass = 100f,
            float maximumDistance = 3f,
            DisplacementHandRequirement handRequirement =
                DisplacementHandRequirement.None,
            DisplacementAutoStowPolicy autoStowPolicy =
                DisplacementAutoStowPolicy.Never,
            DisplacementSizeClass maximumSubjectSize =
                DisplacementSizeClass.Huge,
            DisplacementDistanceDecayDefinition distanceDecay = null,
            DisplacementResultPolicies allowedResults =
                DisplacementResultPolicies.None) =>
            new DisplacementActionDefinition(
                id,
                displayName,
                intent,
                new ActionCost(
                    actionPoints,
                    0f,
                    ActionMobility.Mobile),
                acceptedSubjects,
                reach,
                maximumDistance,
                maximumSubjectMass,
                handRequirement,
                autoStowPolicy,
                contestPolicy,
                allowedResults,
                maximumSubjectSize,
                distanceDecay);

        private sealed class AllowPaths : IDisplacementPathValidator
        {
            public DisplacementPathValidation Validate(
                DisplacementRequest request,
                GameplayPosition origin,
                PropDisplacementState resultingPropState) =>
                DisplacementPathValidation.Allowed();
        }

        private sealed class BlockPaths : IDisplacementPathValidator
        {
            public DisplacementPathValidation Validate(
                DisplacementRequest request,
                GameplayPosition origin,
                PropDisplacementState resultingPropState) =>
                DisplacementPathValidation.Blocked("test.blocked");
        }

        private sealed class CapturePaths : IDisplacementPathValidator
        {
            private readonly bool block;

            public CapturePaths(bool block = false)
            {
                this.block = block;
            }

            public PropDisplacementState ResultingPropState { get; private set; }

            public DisplacementPathValidation Validate(
                DisplacementRequest request,
                GameplayPosition origin,
                PropDisplacementState resultingPropState)
            {
                ResultingPropState = resultingPropState;
                return block
                    ? DisplacementPathValidation.Blocked("test.blocked")
                    : DisplacementPathValidation.Allowed();
            }
        }

        private sealed class BlockBeyondDistance : IDisplacementPathValidator
        {
            private readonly float maximumDistance;

            public BlockBeyondDistance(float acceptedDistance)
            {
                maximumDistance = acceptedDistance;
            }

            public DisplacementPathValidation Validate(
                DisplacementRequest request,
                GameplayPosition origin,
                PropDisplacementState resultingPropState) =>
                origin.DistanceTo(request.Destination) <= maximumDistance
                    ? DisplacementPathValidation.Allowed()
                    : DisplacementPathValidation.Blocked("test.blocked");
        }

        private sealed class ThrowIfPathValidated : IDisplacementPathValidator
        {
            public DisplacementPathValidation Validate(
                DisplacementRequest request,
                GameplayPosition origin,
                PropDisplacementState resultingPropState)
            {
                throw new AssertionException(
                    "Reach rejection must happen before the world query.");
            }
        }

        private sealed class MovePropDuringValidation : IDisplacementPathValidator
        {
            private readonly DestructiblePropSession destructibles;

            public MovePropDuringValidation(
                DestructiblePropSession destructibleSession)
            {
                destructibles = destructibleSession;
            }

            public DisplacementPathValidation Validate(
                DisplacementRequest request,
                GameplayPosition origin,
                PropDisplacementState resultingPropState)
            {
                var movedPosition = new GameplayPosition(
                    origin.X,
                    origin.Y,
                    origin.Z + 0.25f);
                var interveningRequest = new DisplacementRequest(
                    request.ActorId,
                    request.ActionId,
                    request.SubjectId,
                    request.SubjectKind,
                    request.SubjectMass,
                    movedPosition,
                    request.ActionKind);
                destructibles.CommitDisplacement(
                    new DisplacementRecord(
                        1L,
                        interveningRequest,
                        new PropDisplacementState(
                            destructibles.GetProp(request.SubjectId).Pose,
                            DestructiblePropPosture.Upright),
                        new PropDisplacementState(
                            destructibles.GetProp(request.SubjectId).Pose
                                .WithPosition(movedPosition),
                            DestructiblePropPosture.Upright)));
                return DisplacementPathValidation.Allowed();
            }
        }

        private sealed class FixedRolls : ID20RollSource
        {
            private readonly Queue<int> rolls;

            public FixedRolls(params int[] values)
            {
                rolls = new Queue<int>(values);
            }

            public int RollD20() => rolls.Dequeue();
        }

        private sealed class ThrowIfRolled : ID20RollSource
        {
            public int RollD20()
            {
                throw new AssertionException("Replay or rejection must not reroll.");
            }
        }
    }
}
