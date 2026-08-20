using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayEnemyDecisionSessionTests
    {
        [Test]
        public void DetectionRequiresAuthoredHostilityVisibilityAndRange()
        {
            GameplaySession gameplay = CreateSession();
            var decisions = new GameplayEnemyDecisionSession(gameplay);

            EnemyTacticalDecisionRecord detected = decisions.EvaluateDetection(
                "enemy",
                "player",
                CreateExposure(visibleSamples: 6));
            EnemyTacticalDecisionRecord occluded = decisions.EvaluateDetection(
                "enemy",
                "player",
                CreateExposure(visibleSamples: 0));

            Assert.That(detected, Is.Not.Null);
            Assert.That(detected.Kind,
                Is.EqualTo(EnemyTacticalDecisionKind.Detect));
            Assert.That(occluded, Is.Null);
        }

        [Test]
        public void DetectionRespectsAuthoredViewCone()
        {
            GameplaySession gameplay = CreateSession();
            gameplay.UpdateExplorationPose(
                "player",
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 20f),
                    0f));
            var decisions = new GameplayEnemyDecisionSession(gameplay);

            EnemyTacticalDecisionRecord behind = decisions.EvaluateDetection(
                "enemy",
                "player",
                CreateExposure(visibleSamples: 6));

            Assert.That(behind, Is.Null);
        }

        [Test]
        public void PartyDetectionAndTargetSelectionUseTheActualResponsiveActor()
        {
            GameplaySession gameplay = CreateMultiTargetSession();
            var decisions = new GameplayEnemyDecisionSession(gameplay);
            var capturedTargets = new System.Collections.Generic.List<string>();

            EnemyTacticalDecisionRecord detection =
                decisions.EvaluateFirstDetection(
                    "enemy",
                    new[] { "player", "ally" },
                    targetId =>
                    {
                        capturedTargets.Add(targetId);
                        return CreateExposure(
                            visibleSamples: targetId == "ally" ? 6 : 0,
                            targetId: targetId);
                    });

            Assert.That(capturedTargets,
                Is.EqualTo(new[] { "player", "ally" }));
            Assert.That(detection, Is.Not.Null);
            Assert.That(detection.TargetId, Is.EqualTo("ally"));
            Assert.That(
                decisions.SelectNearestCapableHostile("enemy"),
                Is.EqualTo("ally"));
        }

        [Test]
        public void CombatTargetSelectionPrefersTheBestShotOverNearestDistance()
        {
            GameplaySession gameplay = CreateMultiTargetSession();
            var decisions = new GameplayEnemyDecisionSession(gameplay);

            EnemyTargetSelection selection = decisions.SelectBestTarget(
                "enemy",
                new[] { "player", "ally" },
                targetId => CreateRasterExposure(
                    visibleSamples: targetId == "player" ? 9 : 2,
                    totalSamples: 10,
                    targetId: targetId));

            Assert.That(selection.TargetId, Is.EqualTo("player"));
            Assert.That(selection.HitChancePercent, Is.EqualTo(90));
            Assert.That(selection.Exposure.TargetId, Is.EqualTo("player"));
            Assert.That(decisions.SelectNearestCapableHostile("enemy"),
                Is.EqualTo("ally"));
        }

        [Test]
        public void ExplorationDetectionEvaluatesTheWholePartyBeforeChoosingThreat()
        {
            GameplaySession gameplay = CreateMultiTargetSession();
            var decisions = new GameplayEnemyDecisionSession(gameplay);
            var capturedTargets = new System.Collections.Generic.List<string>();

            EnemyTacticalDecisionRecord detection = decisions.EvaluateBestDetection(
                "enemy",
                new[] { "player", "ally" },
                targetId =>
                {
                    capturedTargets.Add(targetId);
                    return CreateRasterExposure(
                        visibleSamples: targetId == "player" ? 2 : 8,
                        totalSamples: 10,
                        targetId: targetId);
                });

            Assert.That(capturedTargets, Is.EqualTo(new[] { "player", "ally" }));
            Assert.That(detection.TargetId, Is.EqualTo("ally"));
            Assert.That(detection.Exposure.VisibleFraction, Is.EqualTo(0.8f));
        }

        [Test]
        public void ExactTargetTiesUseStableActorIdentifiers()
        {
            GameplaySession gameplay = CreateMultiTargetSession();
            gameplay.UpdateExplorationPose(
                "player",
                new GameplayActorPose(
                    new GameplayPosition(-1f, 0f, 0f),
                    0f));
            gameplay.UpdateExplorationPose(
                "ally",
                new GameplayActorPose(
                    new GameplayPosition(1f, 0f, 0f),
                    0f));
            var decisions = new GameplayEnemyDecisionSession(gameplay);

            foreach (string[] candidates in new[]
                {
                    new[] { "player", "ally" },
                    new[] { "ally", "player" },
                })
            {
                EnemyTacticalDecisionRecord detection =
                    decisions.EvaluateBestDetection(
                        "enemy",
                        candidates,
                        targetId => CreateExposure(
                            visibleSamples: 6,
                            targetId: targetId));
                EnemyTargetSelection target = decisions.SelectBestTarget(
                    "enemy",
                    candidates,
                    targetId => CreateExposure(
                        visibleSamples: 6,
                        targetId: targetId));

                Assert.That(detection.TargetId, Is.EqualTo("ally"));
                Assert.That(target.TargetId, Is.EqualTo("ally"));
                Assert.That(
                    decisions.SelectNearestCapableHostile("enemy"),
                    Is.EqualTo("ally"));
            }
        }

        [Test]
        public void ExposedAffordableTargetProducesAttackDecisionAndJournalEntry()
        {
            GameplaySession gameplay = CreateSession();
            Assert.That(gameplay.BeginEncounter(), Is.True);
            var decisions = new GameplayEnemyDecisionSession(gameplay);

            EnemyTacticalDecisionRecord decision = decisions.EvaluateTurn(
                "enemy",
                "player",
                CreateExposure(visibleSamples: 6),
                Array.Empty<EnemyMovementOption>(),
                attacksCommittedThisTurn: 0);
            decisions.Commit(decision);

            Assert.That(decision.Kind,
                Is.EqualTo(EnemyTacticalDecisionKind.Attack));
            Assert.That(decisions.Decisions, Has.Count.EqualTo(1));
            Assert.That(gameplay.Journal.LastEntry,
                Is.TypeOf<EnemyDecisionCommittedJournalEntry>());
            Assert.That(
                ((EnemyDecisionCommittedJournalEntry)gameplay.Journal.LastEntry)
                    .Decision,
                Is.SameAs(decision));
        }

        [Test]
        public void ExposedAffordableProjectileProducesAttackDecision()
        {
            GameplaySession gameplay = CreateSession(
                enemyAttack: CreateRocket());
            Assert.That(gameplay.BeginEncounter(), Is.True);
            var decisions = new GameplayEnemyDecisionSession(gameplay);

            EnemyTacticalDecisionRecord decision = decisions.EvaluateTurn(
                "enemy",
                "player",
                CreateExposure(visibleSamples: 6),
                Array.Empty<EnemyMovementOption>(),
                attacksCommittedThisTurn: 0);

            Assert.That(decision.Kind,
                Is.EqualTo(EnemyTacticalDecisionKind.Attack));
        }

        [Test]
        public void LowConfidenceShotRepositionsForMeaningfullyBetterExposure()
        {
            GameplaySession gameplay = CreateSession();
            Assert.That(gameplay.BeginEncounter(), Is.True);
            var decisions = new GameplayEnemyDecisionSession(gameplay);
            GameplayActorSnapshot enemy = gameplay.GetActor("enemy");
            var betterRoute = new MovementRouteRecord(
                "enemy",
                enemy.Pose,
                new[] { new GameplayPosition(2f, 0f, 6f) });
            TargetExposureSnapshot lowConfidence = CreateRasterExposure(
                visibleSamples: 2,
                totalSamples: 10);

            Assert.That(decisions.RequiresMovementSearch(
                "enemy", "player", lowConfidence), Is.True);
            EnemyTacticalDecisionRecord decision = decisions.EvaluateTurn(
                "enemy",
                "player",
                lowConfidence,
                new[]
                {
                    new EnemyMovementOption(
                        betterRoute,
                        CreateRasterExposure(visibleSamples: 8, totalSamples: 10),
                        resultingTargetDistance: 6f),
                },
                attacksCommittedThisTurn: 0);

            Assert.That(decision.Kind, Is.EqualTo(EnemyTacticalDecisionKind.Move));
            Assert.That(decision.MovementRoute, Is.SameAs(betterRoute));
            Assert.That(decision.Rationale, Does.Contain("improves attack position"));
        }

        [Test]
        public void LowConfidenceShotIsTakenWhenNoRouteImprovesIt()
        {
            GameplaySession gameplay = CreateSession();
            Assert.That(gameplay.BeginEncounter(), Is.True);
            var decisions = new GameplayEnemyDecisionSession(gameplay);
            GameplayActorSnapshot enemy = gameplay.GetActor("enemy");
            var worseRoute = new MovementRouteRecord(
                "enemy",
                enemy.Pose,
                new[] { new GameplayPosition(2f, 0f, 6f) });

            EnemyTacticalDecisionRecord decision = decisions.EvaluateTurn(
                "enemy",
                "player",
                CreateRasterExposure(visibleSamples: 2, totalSamples: 10),
                new[]
                {
                    new EnemyMovementOption(
                        worseRoute,
                        CreateRasterExposure(visibleSamples: 1, totalSamples: 10),
                        resultingTargetDistance: 6f),
                },
                attacksCommittedThisTurn: 0);

            Assert.That(decision.Kind, Is.EqualTo(EnemyTacticalDecisionKind.Attack));
            Assert.That(decision.Rationale, Does.Contain("no better firing position"));
            Assert.That(decision.Rationale, Does.Contain("20%"));
        }

        [Test]
        public void CombatAttackHasNoHardRangeBeyondAccuracyDecay()
        {
            GameplaySession gameplay = CreateSession();
            gameplay.UpdateExplorationPose(
                "player",
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, -100f),
                    0f));
            Assert.That(gameplay.BeginEncounter(), Is.True);
            var decisions = new GameplayEnemyDecisionSession(gameplay);

            EnemyTacticalDecisionRecord decision = decisions.EvaluateTurn(
                "enemy",
                "player",
                CreateExposure(visibleSamples: 6),
                Array.Empty<EnemyMovementOption>(),
                attacksCommittedThisTurn: 0);

            Assert.That(decision.Kind,
                Is.EqualTo(EnemyTacticalDecisionKind.Attack));
            Assert.That(decision.Rationale, Does.Contain("hit chance"));
        }

        [Test]
        public void ContactEnemyMovesIntoAuthoredReachBeforeAttacking()
        {
            GameplaySession gameplay = CreateSession(
                enemyAttack: CreateKnife(),
                enemyPositionZ: 5f);
            Assert.That(gameplay.BeginEncounter(), Is.True);
            var decisions = new GameplayEnemyDecisionSession(gameplay);
            GameplayActorSnapshot enemy = gameplay.GetActor("enemy");
            var route = new MovementRouteRecord(
                "enemy",
                enemy.Pose,
                new[] { new GameplayPosition(0f, 0f, 1.5f) });

            EnemyTacticalDecisionRecord decision = decisions.EvaluateTurn(
                "enemy",
                "player",
                CreateExposure(visibleSamples: 6),
                new[]
                {
                    new EnemyMovementOption(
                        route,
                        CreateExposure(visibleSamples: 6),
                        resultingTargetDistance: 1.5f),
                },
                attacksCommittedThisTurn: 0);

            Assert.That(decision.Kind,
                Is.EqualTo(EnemyTacticalDecisionKind.Move));
            Assert.That(decision.MovementRoute, Is.SameAs(route));
        }

        [Test]
        public void MovementSelectionPrioritizesExposureThenAccuracyAndRange()
        {
            GameplaySession gameplay = CreateSession();
            Assert.That(gameplay.BeginEncounter(), Is.True);
            var decisions = new GameplayEnemyDecisionSession(gameplay);
            GameplayActorSnapshot enemy = gameplay.GetActor("enemy");
            var concealedRoute = new MovementRouteRecord(
                "enemy",
                enemy.Pose,
                new[] { new GameplayPosition(1f, 0f, 10f) });
            var exposedRoute = new MovementRouteRecord(
                "enemy",
                enemy.Pose,
                new[] { new GameplayPosition(2f, 0f, 10f) });
            var options = new[]
            {
                new EnemyMovementOption(
                    concealedRoute,
                    CreateExposure(visibleSamples: 0),
                    resultingTargetDistance: 9f),
                new EnemyMovementOption(
                    exposedRoute,
                    CreateExposure(visibleSamples: 3),
                    resultingTargetDistance: 8f),
            };

            EnemyTacticalDecisionRecord decision = decisions.EvaluateTurn(
                "enemy",
                "player",
                CreateExposure(visibleSamples: 0),
                options,
                attacksCommittedThisTurn: 0);

            Assert.That(decision.Kind,
                Is.EqualTo(EnemyTacticalDecisionKind.Move));
            Assert.That(decision.MovementRoute, Is.SameAs(exposedRoute));
        }

        [Test]
        public void MovementSelectionComparesNormalizedExposureAcrossRasterSizes()
        {
            GameplaySession gameplay = CreateSession();
            Assert.That(gameplay.BeginEncounter(), Is.True);
            var decisions = new GameplayEnemyDecisionSession(gameplay);
            GameplayActorSnapshot enemy = gameplay.GetActor("enemy");
            var largerRawCountRoute = new MovementRouteRecord(
                "enemy",
                enemy.Pose,
                new[] { new GameplayPosition(1f, 0f, 10f) });
            var higherHitChanceRoute = new MovementRouteRecord(
                "enemy",
                enemy.Pose,
                new[] { new GameplayPosition(2f, 0f, 10f) });

            EnemyTacticalDecisionRecord decision = decisions.EvaluateTurn(
                "enemy",
                "player",
                CreateExposure(visibleSamples: 0),
                new[]
                {
                    new EnemyMovementOption(
                        largerRawCountRoute,
                        CreateRasterExposure(visibleSamples: 10, totalSamples: 100),
                        resultingTargetDistance: 8f),
                    new EnemyMovementOption(
                        higherHitChanceRoute,
                        CreateRasterExposure(visibleSamples: 8, totalSamples: 10),
                        resultingTargetDistance: 8f),
                },
                attacksCommittedThisTurn: 0);

            Assert.That(decision.Kind,
                Is.EqualTo(EnemyTacticalDecisionKind.Move));
            Assert.That(decision.MovementRoute, Is.SameAs(higherHitChanceRoute));
            Assert.That(
                TargetExposureRules.CalculateHitChancePercent(decision.Exposure),
                Is.EqualTo(80));
        }

        [Test]
        public void ExactMovementTiesUseStableRouteGeometry()
        {
            GameplaySession gameplay = CreateSession();
            Assert.That(gameplay.BeginEncounter(), Is.True);
            var decisions = new GameplayEnemyDecisionSession(gameplay);
            GameplayActorSnapshot enemy = gameplay.GetActor("enemy");
            var positiveRoute = new MovementRouteRecord(
                "enemy",
                enemy.Pose,
                new[] { new GameplayPosition(1f, 0f, 10f) });
            var negativeRoute = new MovementRouteRecord(
                "enemy",
                enemy.Pose,
                new[] { new GameplayPosition(-1f, 0f, 10f) });

            foreach (MovementRouteRecord[] routes in new[]
                {
                    new[] { positiveRoute, negativeRoute },
                    new[] { negativeRoute, positiveRoute },
                })
            {
                EnemyTacticalDecisionRecord decision = decisions.EvaluateTurn(
                    "enemy",
                    "player",
                    CreateExposure(visibleSamples: 0),
                    new[]
                    {
                        new EnemyMovementOption(
                            routes[0],
                            CreateExposure(visibleSamples: 6),
                            resultingTargetDistance: 8f),
                        new EnemyMovementOption(
                            routes[1],
                            CreateExposure(visibleSamples: 6),
                            resultingTargetDistance: 8f),
                    },
                    attacksCommittedThisTurn: 0);

                Assert.That(decision.Kind,
                    Is.EqualTo(EnemyTacticalDecisionKind.Move));
                Assert.That(decision.MovementRoute,
                    Is.SameAs(negativeRoute));
            }
        }

        [Test]
        public void AuthoredAttackLimitEndsTurnWithoutRepositioning()
        {
            GameplaySession gameplay = CreateSession();
            Assert.That(gameplay.BeginEncounter(), Is.True);
            var decisions = new GameplayEnemyDecisionSession(gameplay);
            GameplayActorSnapshot enemy = gameplay.GetActor("enemy");
            var route = new MovementRouteRecord(
                "enemy",
                enemy.Pose,
                new[] { new GameplayPosition(2f, 0f, 0f) });

            EnemyTacticalDecisionRecord decision = decisions.EvaluateTurn(
                "enemy",
                "player",
                CreateExposure(visibleSamples: 6),
                new[]
                {
                    new EnemyMovementOption(
                        route,
                        CreateExposure(visibleSamples: 6),
                        resultingTargetDistance: 8f),
                },
                attacksCommittedThisTurn: 1);

            Assert.That(decision.Kind,
                Is.EqualTo(EnemyTacticalDecisionKind.EndTurn));
            Assert.That(decision.Rationale,
                Does.Contain("attack limit"));
        }

        [Test]
        public void IncapacitatedActorsAreRejectedAsTargetsAndSkippedInInitiative()
        {
            GameplaySession gameplay = CreateSession(
                playerInitiative: 100,
                enemyInitiative: 0,
                enemyMaximumWounds: 1);
            Assert.That(gameplay.BeginEncounter(), Is.True);
            var attacks = new GameplayAttackSession(gameplay);
            TargetExposureSnapshot exposure = CreateExposure(
                observerId: "player",
                targetId: "enemy",
                visibleSamples: 6);

            Assert.That(attacks.TryResolve(
                "player",
                exposure,
                out _,
                out AttackResolutionFailure failure), Is.True);
            Assert.That(failure, Is.EqualTo(AttackResolutionFailure.None));
            Assert.That(gameplay.IsActorIncapacitated("enemy"), Is.True);
            Assert.That(gameplay.TryEndTurn("player", out _), Is.True);
            Assert.That(gameplay.ActiveActorId, Is.EqualTo("player"));

            Assert.That(attacks.TryResolve(
                "player",
                exposure,
                out _,
                out failure), Is.False);
            Assert.That(failure,
                Is.EqualTo(AttackResolutionFailure.TargetIncapacitated));
        }

        private static GameplaySession CreateSession(
            int playerInitiative = 0,
            int enemyInitiative = 100,
            int enemyMaximumWounds = 2,
            AttackDefinition enemyAttack = null,
            float enemyPositionZ = 10f)
        {
            AttackDefinition rifle = CreateRifle();
            var player = new ScenarioActorDefinition(
                "player",
                playerInitiative,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f),
                rifle,
                combat: new ActorCombatDefinition(
                    "player",
                    new[] { "raider" },
                    maximumWounds: 3));
            var enemy = new ScenarioActorDefinition(
                "enemy",
                enemyInitiative,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, enemyPositionZ),
                    180f),
                new TurnBudget(4, 8f),
                enemyAttack ?? rifle,
                combat: new ActorCombatDefinition(
                    "raider",
                    new[] { "player" },
                    enemyMaximumWounds,
                    new EnemyBehaviorDefinition(
                        "behavior.rifleman",
                        perceptionRange: 30f,
                        viewAngleDegrees: 120f,
                        preferredEngagementRange: 12f,
                        movementSearchRadius: 6f,
                        maximumAttacksPerTurn: 1)));
            return new GameplaySession(new ScenarioDefinition(
                "enemy-decision-test",
                new ScenarioTimingDefinition(1.25f),
                new[] { player, enemy },
                Array.Empty<ScenarioObjectiveDefinition>()),
                scenarioSeed: 3u);
        }

        private static GameplaySession CreateMultiTargetSession()
        {
            AttackDefinition rifle = CreateRifle();
            var player = new ScenarioActorDefinition(
                "player",
                0,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f),
                rifle,
                combat: new ActorCombatDefinition(
                    "party",
                    new[] { "raider" },
                    maximumWounds: 3));
            var ally = new ScenarioActorDefinition(
                "ally",
                1,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 4f),
                    0f),
                new TurnBudget(4, 8f),
                rifle,
                combat: new ActorCombatDefinition(
                    "party",
                    new[] { "raider" },
                    maximumWounds: 3));
            var enemy = new ScenarioActorDefinition(
                "enemy",
                100,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 10f),
                    180f),
                new TurnBudget(4, 8f),
                rifle,
                combat: new ActorCombatDefinition(
                    "raider",
                    new[] { "party" },
                    maximumWounds: 2,
                    enemyBehavior: new EnemyBehaviorDefinition(
                        "behavior.rifleman",
                        perceptionRange: 30f,
                        viewAngleDegrees: 120f,
                        preferredEngagementRange: 12f,
                        movementSearchRadius: 6f,
                        maximumAttacksPerTurn: 1)));
            return new GameplaySession(new ScenarioDefinition(
                "enemy-party-target-test",
                new ScenarioTimingDefinition(1.25f),
                new[] { player, ally, enemy },
                Array.Empty<ScenarioObjectiveDefinition>()));
        }

        private static AttackDefinition CreateRifle() =>
            new AttackDefinition(
                "attack.rifle",
                "Fire rifle",
                new ActionCost(1, 0f, ActionMobility.Set),
                woundMovementPenalty: 2f,
                accuracyDecay: AccuracyDecayDefinition.None);

        private static AttackDefinition CreateKnife() =>
            new AttackDefinition(
                "attack.combat-knife",
                "Knife strike",
                new ActionCost(1, 0f, ActionMobility.Mobile),
                woundMovementPenalty: 2f,
                contact: new ContactAttackDefinition(2f));

        private static AttackDefinition CreateRocket() =>
            new AttackDefinition(
                "attack.rocket",
                "Launch rocket",
                new ActionCost(1, 0f, ActionMobility.Set),
                woundMovementPenalty: 2f,
                projectile: new ProjectileFlightDefinition(
                    "projectile.rocket",
                    speedPerTurn: 12f,
                    radius: 0.1f,
                    maximumRange: 30f,
                    standingLaunchHeight: 1.2f,
                    crouchedLaunchHeight: 0.9f));

        private static TargetExposureSnapshot CreateExposure(
            int visibleSamples,
            string observerId = "enemy",
            string targetId = "player") =>
            new TargetExposureSnapshot(
                observerId,
                targetId,
                new[]
                {
                    new TargetRegionExposure(TargetRegionId.Head,
                        visibleSamples > 0 ? 1 : 0, 1),
                    new TargetRegionExposure(TargetRegionId.Torso,
                        visibleSamples > 1 ? 1 : 0, 1),
                    new TargetRegionExposure(TargetRegionId.LeftArm,
                        visibleSamples > 2 ? 1 : 0, 1),
                    new TargetRegionExposure(TargetRegionId.RightArm,
                        visibleSamples > 3 ? 1 : 0, 1),
                    new TargetRegionExposure(TargetRegionId.LeftLeg,
                        visibleSamples > 4 ? 1 : 0, 1),
                    new TargetRegionExposure(TargetRegionId.RightLeg,
                        visibleSamples > 5 ? 1 : 0, 1),
                });

        private static TargetExposureSnapshot CreateRasterExposure(
            int visibleSamples,
            int totalSamples,
            string targetId = "player") =>
            new TargetExposureSnapshot(
                "enemy",
                targetId,
                new[]
                {
                    new TargetRegionExposure(
                        TargetRegionId.Torso,
                        visibleSamples,
                        totalSamples),
                });
    }
}
