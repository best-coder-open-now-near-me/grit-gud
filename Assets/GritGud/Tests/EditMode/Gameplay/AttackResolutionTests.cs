using System;
using System.Linq;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class AttackResolutionTests
    {
        [Test]
        public void SeededHitSelectsOnlyRecordedExposedRegions()
        {
            TargetExposureSnapshot exposure = CreateExposure(
                torsoVisible: 5,
                legsVisible: 0);
            var wounds = new ActorWoundSnapshot("target", 0, 0f);
            uint seed = AttackResolutionRules.DeriveResolutionSeed(3u, 1L);

            AttackResolutionRecord first = AttackResolutionRules.Resolve(
                1L,
                seed,
                exposure,
                AccuracyDecayDefinition.None,
                distance: 5f,
                wounds,
                woundMovementPenalty: 2f);
            AttackResolutionRecord repeated = AttackResolutionRules.Resolve(
                1L,
                seed,
                exposure,
                AccuracyDecayDefinition.None,
                distance: 5f,
                wounds,
                woundMovementPenalty: 2f);

            Assert.That(first.Hit, Is.True);
            Assert.That(first.FinalHitChancePercent, Is.EqualTo(17));
            Assert.That(first.HitRegion, Is.EqualTo(TargetRegionId.Torso));
            Assert.That(first.Wound.Resulting.WoundCount, Is.EqualTo(1));
            Assert.That(first.Wound.Resulting.MovementPenalty, Is.EqualTo(2f));
            Assert.That(first.Wound.Resulting.TorsoWounds, Is.EqualTo(1));
            Assert.That(first.Wound.Resulting.LeftLegWounds, Is.Zero);
            Assert.That(repeated.HitRoll, Is.EqualTo(first.HitRoll));
            Assert.That(repeated.RegionRoll, Is.EqualTo(first.RegionRoll));
            Assert.That(repeated.HitRegion, Is.EqualTo(first.HitRegion));
        }

        [Test]
        public void RecordedAttackReplaysWithoutExposureQuery()
        {
            GameplaySession source = CreateSession();
            source.EnterTurnMode();
            var sourceAttacks = new GameplayAttackSession(source);

            Assert.That(sourceAttacks.TryResolve(
                "player",
                CreateExposure(torsoVisible: 5, legsVisible: 5),
                out GameplayActionRecord recordedAction,
                out AttackResolutionFailure failure), Is.True);
            Assert.That(failure, Is.EqualTo(AttackResolutionFailure.None));

            GameplaySession replay = CreateSession();
            replay.EnterTurnMode();
            var replayAttacks = new GameplayAttackSession(replay);
            replayAttacks.Commit(recordedAction);

            GameplayActorSnapshot sourceTarget = source.GetActor("target");
            GameplayActorSnapshot replayTarget = replay.GetActor("target");
            Assert.That(replayTarget.Wounds.WoundCount,
                Is.EqualTo(sourceTarget.Wounds.WoundCount));
            Assert.That(replayTarget.Wounds.MovementPenalty,
                Is.EqualTo(sourceTarget.Wounds.MovementPenalty));
            Assert.That(
                replayTarget.Wounds.HasSameState(sourceTarget.Wounds),
                Is.True);
            Assert.That(replayTarget.TurnBudget.ActionPoints,
                Is.EqualTo(sourceTarget.TurnBudget.ActionPoints));
            Assert.That(replayTarget.TurnBudget.MovementOpportunity,
                Is.EqualTo(sourceTarget.TurnBudget.MovementOpportunity));
            Assert.That(replay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(3));
            Assert.That(replayAttacks.Records.Single().Exposure,
                Is.SameAs(recordedAction.Outcomes
                    .OfType<AttackResolvedActionOutcome>()
                    .Single().Attack.Exposure));
        }

        [Test]
        public void PreparedAttackIsNonMutatingAndMatchesAuthoritativeCommit()
        {
            GameplaySession session = CreateSession();
            session.EnterTurnMode();
            var attacks = new GameplayAttackSession(session);

            Assert.That(attacks.TryPrepareResolve(
                "player",
                CreateExposure(torsoVisible: 5, legsVisible: 5),
                out GameplayPreparedTransition<GameplayActionRecord> prepared,
                out AttackResolutionFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(AttackResolutionFailure.None));
            Assert.That(session.ResolvedActions, Is.Empty);
            Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(4));
            Assert.That(prepared.Previous.CanonicalHash,
                Is.Not.EqualTo(prepared.Predicted.CanonicalHash));

            GameplayTransitionCommitResult committed =
                attacks.CommitPrepared(prepared);

            Assert.That(committed.MatchesPrediction, Is.True);
            Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(3));
            Assert.That(attacks.Records, Has.Count.EqualTo(1));
        }

        [Test]
        public void PreparedAttackRejectsChangedTurnBeforeCommit()
        {
            GameplaySession session = CreateSession();
            session.EnterTurnMode();
            var attacks = new GameplayAttackSession(session);
            Assert.That(attacks.TryPrepareResolve(
                "player",
                CreateExposure(torsoVisible: 5, legsVisible: 5),
                out GameplayPreparedTransition<GameplayActionRecord> prepared,
                out _), Is.True);
            Assert.That(session.TryEndTurn("player", out _), Is.True);

            Assert.Throws<InvalidOperationException>(
                () => attacks.CommitPrepared(prepared));

            Assert.That(session.ResolvedActions, Is.Empty);
            Assert.That(attacks.Records, Is.Empty);
        }

        [Test]
        public void ObserverFailureCannotInterruptAuthoritativeAttackCommit()
        {
            GameplaySession session = CreateSession();
            session.EnterTurnMode();
            var attacks = new GameplayAttackSession(session);
            int successfulObservers = 0;
            session.ActorCapabilityChanged += _ =>
                throw new InvalidOperationException("observer failed");
            session.ActorCapabilityChanged += _ => successfulObservers++;

            Assert.Throws<InvalidOperationException>(() =>
                attacks.TryResolve(
                    "player",
                    CreateExposure(torsoVisible: 5, legsVisible: 5),
                    out _,
                    out _));

            Assert.That(successfulObservers, Is.EqualTo(1));
            Assert.That(attacks.Records, Has.Count.EqualTo(1));
            Assert.That(session.ResolvedActions, Has.Count.EqualTo(1));
            Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(3));
            Assert.That(session.Journal.LastEntry,
                Is.TypeOf<ActionResolvedJournalEntry>());
        }

        [Test]
        public void WoundSnapshotPreservesLeftAndRightLimbDamageIndependently()
        {
            var clear = new ActorWoundSnapshot("player", 0, 0f);

            ActorWoundSnapshot wounded = clear
                .AddWound(TargetRegionId.LeftArm, 1f)
                .AddWound(TargetRegionId.RightLeg, 2f);

            Assert.That(wounded.WoundCount, Is.EqualTo(2));
            Assert.That(wounded.LeftArmWounds, Is.EqualTo(1));
            Assert.That(wounded.RightArmWounds, Is.Zero);
            Assert.That(wounded.LeftLegWounds, Is.Zero);
            Assert.That(wounded.RightLegWounds, Is.EqualTo(1));
            Assert.That(wounded.MovementPenalty, Is.EqualTo(3f));
        }

        [Test]
        public void MissConsumesAuthoredCostWithoutApplyingWound()
        {
            GameplaySession session = CreateSession();
            session.EnterTurnMode();
            var attacks = new GameplayAttackSession(session);

            Assert.That(attacks.TryResolve(
                "player",
                CreateExposure(torsoVisible: 0, legsVisible: 0),
                out GameplayActionRecord action,
                out _), Is.True);

            AttackResolutionRecord attack = action.Outcomes
                .OfType<AttackResolvedActionOutcome>()
                .Single().Attack;
            Assert.That(attack.Hit, Is.False);
            Assert.That(attack.RegionRoll, Is.Zero);
            Assert.That(attack.Wound, Is.Null);
            Assert.That(session.GetActor("target").Wounds.WoundCount, Is.Zero);
            Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(3));
            Assert.That(session.Journal.LastEntry,
                Is.TypeOf<ActionResolvedJournalEntry>());
            Assert.That(
                ((ActionResolvedJournalEntry)session.Journal.LastEntry)
                    .Action.Outcomes.Single(),
                Is.TypeOf<AttackResolvedActionOutcome>());
        }

        [Test]
        public void CommittedAttackFacesAttackerTowardTarget()
        {
            GameplaySession session = CreateSession(
                new GameplayPosition(5f, 0f, 0f));
            session.EnterTurnMode();
            var attacks = new GameplayAttackSession(session);

            Assert.That(attacks.TryResolve(
                "player",
                CreateExposure(torsoVisible: 5, legsVisible: 5),
                out _,
                out _), Is.True);

            Assert.That(
                session.GetActor("player").Pose.FacingDegrees,
                Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void WorldDischargeSpendsCostFacesAimAndSkipsTargetRolls()
        {
            GameplaySession session = CreateSession();
            session.EnterTurnMode();
            var attacks = new GameplayAttackSession(session);

            Assert.That(attacks.TryDischarge(
                "player",
                new GameplayPosition(5f, 0f, 0f),
                out GameplayActionRecord action,
                out AttackResolutionFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(AttackResolutionFailure.None));
            Assert.That(action.Request.TargetId,
                Is.EqualTo(GameplayTargetIds.WorldAimPoint));
            Assert.That(action.Outcomes.Single(),
                Is.TypeOf<WeaponDischargedActionOutcome>());
            Assert.That(attacks.Records, Is.Empty);
            Assert.That(attacks.Discharges, Has.Count.EqualTo(1));
            Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(3));
            Assert.That(session.GetActor("player").Pose.FacingDegrees,
                Is.EqualTo(90f).Within(0.001f));
            Assert.That(session.GetActor("target").Wounds.WoundCount,
                Is.Zero);

            string diagnostic = string.Join(
                Environment.NewLine,
                AttackDiagnosticFormatter.FormatDischarge(action));
            Assert.That(diagnostic,
                Does.Contain("OUTCOME - WORLD DISCHARGE - NO TARGET HIT ROLL"));
        }

        [Test]
        public void PreparedWorldDischargePredictsBudgetAndFacing()
        {
            GameplaySession session = CreateSession();
            session.EnterTurnMode();
            var attacks = new GameplayAttackSession(session);

            Assert.That(attacks.TryPrepareDischarge(
                "player",
                GameplayTargetIds.WorldAimPoint,
                new GameplayPosition(5f, 0f, 0f),
                out GameplayPreparedTransition<GameplayActionRecord> prepared,
                out AttackResolutionFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(AttackResolutionFailure.None));
            GameplayActorSnapshot predicted =
                prepared.Predicted.Session.GetActor("player");
            Assert.That(predicted.TurnBudget.ActionPoints, Is.EqualTo(3));
            Assert.That(predicted.Pose.FacingDegrees,
                Is.EqualTo(90f).Within(0.001f));
            Assert.That(attacks.CommitPrepared(prepared).MatchesPrediction, Is.True);
        }

        [Test]
        public void ExplorationDischargeRecordsStableTargetAtZeroCost()
        {
            GameplaySession session = CreateSession();
            var attacks = new GameplayAttackSession(session);

            Assert.That(attacks.TryDischarge(
                "player",
                "alarm-panel",
                new GameplayPosition(5f, 0f, 0f),
                out GameplayActionRecord action,
                out AttackResolutionFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(AttackResolutionFailure.None));
            Assert.That(action.Request.TargetId, Is.EqualTo("alarm-panel"));
            Assert.That(action.Cost.ActionPoints, Is.Zero);
            Assert.That(action.ResultingBudget.ActionPoints, Is.EqualTo(4));
            WeaponDischargeRecord discharge =
                ((WeaponDischargedActionOutcome)action.Outcomes.Single())
                .Discharge;
            Assert.That(discharge.TargetId, Is.EqualTo("alarm-panel"));
            Assert.That(session.Mode,
                Is.EqualTo(GameplaySessionMode.Exploration));
            Assert.That(session.GetActor("player").Pose.FacingDegrees,
                Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void ExplorationAttackResolvesAgainstInertActorAtZeroCost()
        {
            GameplaySession session = CreateSession();
            var attacks = new GameplayAttackSession(session);

            Assert.That(attacks.TryResolve(
                "player",
                CreateExposure(torsoVisible: 5, legsVisible: 5),
                out GameplayActionRecord action,
                out AttackResolutionFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(AttackResolutionFailure.None));
            Assert.That(action.Cost.ActionPoints, Is.Zero);
            Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(4));
            Assert.That(session.Mode,
                Is.EqualTo(GameplaySessionMode.Exploration));
        }

        [Test]
        public void RecordedWorldDischargeReplaysWithoutAWorldQuery()
        {
            GameplaySession source = CreateSession();
            source.EnterTurnMode();
            var sourceAttacks = new GameplayAttackSession(source);
            sourceAttacks.TryDischarge(
                "player",
                new GameplayPosition(5f, 0f, 0f),
                out GameplayActionRecord action,
                out _);

            GameplaySession replay = CreateSession();
            replay.EnterTurnMode();
            var replayAttacks = new GameplayAttackSession(replay);
            replayAttacks.Commit(action);

            Assert.That(replayAttacks.Discharges, Has.Count.EqualTo(1));
            Assert.That(replay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(3));
            Assert.That(replay.GetActor("player").Pose.FacingDegrees,
                Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void CombatDiagnosticsReflectResolvedRollsAndOutcome()
        {
            GameplaySession session = CreateSession();
            session.EnterTurnMode();
            var attacks = new GameplayAttackSession(session);
            attacks.TryResolve(
                "player",
                CreateExposure(torsoVisible: 5, legsVisible: 5),
                out GameplayActionRecord action,
                out _);

            string diagnostic = string.Join(
                Environment.NewLine,
                AttackDiagnosticFormatter.Format(action));

            Assert.That(diagnostic, Does.Contain("SEED -"));
            Assert.That(diagnostic, Does.Contain(
                "SILHOUETTE - 30 painted cells - 10 world-visible"
                + " - 33% geometric"));
            Assert.That(diagnostic, Does.Contain(
                "REGION TORSO - 5 painted - 5 world-visible"
                + " - 100% exposed - 50% hit-location share"));
            Assert.That(diagnostic, Does.Contain(
                "ACCURACY - 5 m - 100% - half-life 1 m - floor 100%"));
            Assert.That(diagnostic, Does.Contain(
                "HIT CHANCE - 33% geometric x 100% accuracy = 33%"));
            Assert.That(diagnostic, Does.Contain("HIT ROLL - d100"));
            Assert.That(diagnostic,
                Does.Contain("REGION ROLL - NOT ROLLED ON MISS"));
            Assert.That(diagnostic,
                Does.Contain("OUTCOME - MISS - NO WOUND"));
        }

        [Test]
        public void AccuracyDecayIsSmoothAndNeverCreatesAHardRange()
        {
            var decay = new AccuracyDecayDefinition(
                halfLifeDistance: 20f,
                minimumAccuracyPercent: 5f);

            Assert.That(decay.EvaluatePercent(0f), Is.EqualTo(100f));
            Assert.That(decay.EvaluatePercent(20f), Is.EqualTo(52.5f)
                .Within(0.001f));
            Assert.That(decay.EvaluatePercent(40f), Is.EqualTo(28.75f)
                .Within(0.001f));
            Assert.That(decay.EvaluatePercent(10000f), Is.EqualTo(5f)
                .Within(0.001f));
        }

        [Test]
        public void HitChanceCombinesGeometricExposureWithDistanceAccuracy()
        {
            TargetExposureSnapshot exposure = CreateExposure(
                torsoVisible: 5,
                legsVisible: 5);
            var decay = new AccuracyDecayDefinition(20f, 5f);

            int chance = AttackHitChanceRules.CalculateFinalHitChancePercent(
                exposure,
                decay,
                distance: 20f);

            Assert.That(
                TargetExposureRules.CalculateHitChancePercent(exposure),
                Is.EqualTo(33));
            Assert.That(chance, Is.EqualTo(17));
        }

        [Test]
        public void HitChanceAppliesFrozenContextAfterDistanceBeforeClamping()
        {
            TargetExposureSnapshot exposure = CreateExposure(
                torsoVisible: 5,
                legsVisible: 5);
            var decay = new AccuracyDecayDefinition(20f, 5f);

            int boosted = AttackHitChanceRules.CalculateFinalHitChancePercent(
                exposure,
                decay,
                distance: 20f,
                contextualAccuracyDeltaPercent: 15);
            int penalized = AttackHitChanceRules.CalculateFinalHitChancePercent(
                exposure,
                decay,
                distance: 20f,
                contextualAccuracyDeltaPercent: -30);

            Assert.That(boosted, Is.EqualTo(32));
            Assert.That(penalized, Is.EqualTo(1));
        }

        [Test]
        public void ContactAttackRejectsOutOfReachTargetWithoutMutation()
        {
            GameplaySession session = CreateContactSession(
                new GameplayPosition(0f, 0f, 2.5f));
            session.EnterTurnMode();
            var attacks = new GameplayAttackSession(session);

            AttackResolutionFailure readiness = attacks.EvaluateResolve(
                "player",
                CreateExposure(torsoVisible: 5, legsVisible: 5));
            bool resolved = attacks.TryResolve(
                "player",
                CreateExposure(torsoVisible: 5, legsVisible: 5),
                out GameplayActionRecord action,
                out AttackResolutionFailure failure);

            Assert.That(readiness,
                Is.EqualTo(AttackResolutionFailure.TargetOutOfReach));
            Assert.That(resolved, Is.False);
            Assert.That(action, Is.Null);
            Assert.That(failure,
                Is.EqualTo(AttackResolutionFailure.TargetOutOfReach));
            Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(4));
            Assert.That(session.GetActor("target").Wounds.WoundCount, Is.Zero);
            Assert.That(attacks.Records, Is.Empty);
        }

        [Test]
        public void ContactAttackRecordsReachAndCannotDischargeAtWorld()
        {
            GameplaySession session = CreateContactSession(
                new GameplayPosition(0f, 0f, 1.5f));
            session.EnterTurnMode();
            var attacks = new GameplayAttackSession(session);

            Assert.That(attacks.TryDischarge(
                "player",
                new GameplayPosition(0f, 0f, 1f),
                out _,
                out AttackResolutionFailure dischargeFailure), Is.False);
            Assert.That(dischargeFailure,
                Is.EqualTo(AttackResolutionFailure.TargetRequired));
            Assert.That(attacks.TryResolve(
                "player",
                CreateExposure(torsoVisible: 5, legsVisible: 5),
                out GameplayActionRecord action,
                out AttackResolutionFailure failure), Is.True);

            AttackResolutionRecord attack = action.Outcomes
                .OfType<AttackResolvedActionOutcome>()
                .Single().Attack;
            Assert.That(failure, Is.EqualTo(AttackResolutionFailure.None));
            Assert.That(attack.IsContactAttack, Is.True);
            Assert.That(attack.MaximumReach, Is.EqualTo(2f));
            Assert.That(attack.Distance, Is.EqualTo(1.5f));
            Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(3));
            Assert.That(string.Join(
                    Environment.NewLine,
                    AttackDiagnosticFormatter.Format(action)),
                Does.Contain("CONTACT REACH - 1.5 m <= 2 m - VALID"));
        }

        [Test]
        public void ContactTargetPreviewMarksRangeWithoutSamplingOrMutation()
        {
            TargetExposureSnapshot exposure = CreateExposure(
                torsoVisible: 5,
                legsVisible: 5);
            var contact = new ContactAttackDefinition(2f);

            TargetAcquisitionPreview near = TargetPreviewCalculator.Calculate(
                exposure,
                AccuracyDecayDefinition.None,
                1.5f,
                contact);
            TargetAcquisitionPreview far = TargetPreviewCalculator.Calculate(
                exposure,
                AccuracyDecayDefinition.None,
                2.5f,
                contact);

            Assert.That(near.IsWithinReach, Is.True);
            Assert.That(near.HitChancePercent, Is.EqualTo(33));
            Assert.That(far.IsWithinReach, Is.False);
            Assert.That(far.HitChancePercent, Is.Zero);
            Assert.That(far.MaximumReach, Is.EqualTo(2f));
        }

        private static GameplaySession CreateSession(
            GameplayPosition? targetPosition = null)
        {
            var attack = new AttackDefinition(
                "attack.rifle",
                "Fire rifle",
                new ActionCost(1, 0f, ActionMobility.Set),
                woundMovementPenalty: 2f,
                accuracyDecay: AccuracyDecayDefinition.None);
            var player = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f),
                attack);
            var target = new ScenarioActorDefinition(
                "target",
                0,
                new GameplayActorPose(
                    targetPosition ?? new GameplayPosition(0f, 0f, 5f),
                    0f),
                new TurnBudget(0, 8f));
            return new GameplaySession(new ScenarioDefinition(
                "attack-test",
                new ScenarioTimingDefinition(1.25f),
                new[] { player, target },
                Array.Empty<ScenarioObjectiveDefinition>()),
                scenarioSeed: 3u);
        }

        private static GameplaySession CreateContactSession(
            GameplayPosition targetPosition)
        {
            var knife = new AttackDefinition(
                "attack.combat-knife",
                "Knife strike",
                new ActionCost(1, 0f, ActionMobility.Mobile),
                woundMovementPenalty: 2f,
                contact: new ContactAttackDefinition(2f));
            var player = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f),
                knife);
            var target = new ScenarioActorDefinition(
                "target",
                0,
                new GameplayActorPose(targetPosition, 0f),
                new TurnBudget(0, 8f));
            return new GameplaySession(new ScenarioDefinition(
                "contact-attack-test",
                new ScenarioTimingDefinition(1.25f),
                new[] { player, target },
                Array.Empty<ScenarioObjectiveDefinition>()),
                scenarioSeed: 3u);
        }

        private static TargetExposureSnapshot CreateExposure(
            int torsoVisible,
            int legsVisible)
        {
            return new TargetExposureSnapshot(
                "player",
                "target",
                new[]
                {
                    new TargetRegionExposure(TargetRegionId.Head, 0, 5),
                    new TargetRegionExposure(TargetRegionId.Torso, torsoVisible, 5),
                    new TargetRegionExposure(TargetRegionId.LeftArm, 0, 5),
                    new TargetRegionExposure(TargetRegionId.RightArm, 0, 5),
                    new TargetRegionExposure(TargetRegionId.LeftLeg, legsVisible, 5),
                    new TargetRegionExposure(TargetRegionId.RightLeg, 0, 5),
                });
        }
    }
}
