using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayAmmunitionFiringTests
    {
        [Test]
        public void ActorAttackAtomicallyConsumesItsAuthoredRounds()
        {
            GameplaySession gameplay = CreateGameplay(projectile: false);
            gameplay.EnterTurnMode();
            var attacks = new GameplayAttackSession(gameplay);
            WeaponAmmunitionDelta observed = null;
            gameplay.AmmunitionChanged += change => observed = change;

            Assert.That(attacks.TryResolve(
                "player",
                CreateExposure(),
                out GameplayActionRecord action,
                out AttackResolutionFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(AttackResolutionFailure.None));
            Assert.That(action.Outcomes, Has.Count.EqualTo(2));
            Assert.That(action.Outcomes[0],
                Is.TypeOf<AttackResolvedActionOutcome>());
            Assert.That(action.Outcomes[1],
                Is.TypeOf<AmmunitionSpentActionOutcome>());
            Assert.That(observed, Is.SameAs(
                ((AmmunitionSpentActionOutcome)action.Outcomes[1]).Change));
            AssertAmmunition(gameplay, loaded: 2, reserve: 18);
            Assert.That(gameplay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(3));
        }

        [Test]
        public void DirectWorldDischargeConsumesTheSameAmmoLedger()
        {
            GameplaySession gameplay = CreateGameplay(projectile: false);
            gameplay.EnterTurnMode();
            var attacks = new GameplayAttackSession(gameplay);

            Assert.That(attacks.TryDischarge(
                "player",
                GameplayTargetIds.WorldAimPoint,
                new GameplayPosition(0f, 0f, 10f),
                out GameplayActionRecord action,
                out AttackResolutionFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(AttackResolutionFailure.None));
            Assert.That(
                GameplayWeaponActionOutcomes
                    .RequirePrimary<WeaponDischargedActionOutcome>(action),
                Is.Not.Null);
            AssertAmmunition(gameplay, loaded: 2, reserve: 18);
        }

        [Test]
        public void ProjectileLaunchConsumesTheSameAmmoLedger()
        {
            GameplaySession gameplay = CreateGameplay(projectile: true);
            gameplay.EnterTurnMode();
            GameplayProjectileSession projectiles = CreateProjectileSession(
                gameplay);

            Assert.That(projectiles.TryLaunch(
                "player",
                "target",
                new GameplayPosition(0f, 0f, 10f),
                out GameplayActionRecord action,
                out ProjectileLaunchFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(ProjectileLaunchFailure.None));
            Assert.That(
                GameplayWeaponActionOutcomes
                    .RequirePrimary<ProjectileLaunchedActionOutcome>(action),
                Is.Not.Null);
            AssertAmmunition(gameplay, loaded: 2, reserve: 18);
            Assert.That(projectiles.Launches, Has.Count.EqualTo(1));
        }

        [Test]
        public void EmptyMagazineRejectsEveryFiringPathWithoutSpendingAp()
        {
            GameplaySession directGameplay = CreateGameplay(
                projectile: false,
                loaded: 0);
            directGameplay.EnterTurnMode();
            var attacks = new GameplayAttackSession(directGameplay);

            Assert.That(attacks.TryResolve(
                "player",
                CreateExposure(),
                out _,
                out AttackResolutionFailure actorFailure), Is.False);
            Assert.That(actorFailure, Is.EqualTo(
                AttackResolutionFailure.InsufficientLoadedAmmunition));
            Assert.That(attacks.TryDischarge(
                "player",
                GameplayTargetIds.WorldAimPoint,
                new GameplayPosition(0f, 0f, 10f),
                out _,
                out AttackResolutionFailure directFailure), Is.False);
            Assert.That(directFailure, Is.EqualTo(
                AttackResolutionFailure.InsufficientLoadedAmmunition));
            Assert.That(directGameplay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(4));
            AssertAmmunition(directGameplay, loaded: 0, reserve: 18);

            GameplaySession projectileGameplay = CreateGameplay(
                projectile: true,
                loaded: 0);
            projectileGameplay.EnterTurnMode();
            GameplayProjectileSession projectiles = CreateProjectileSession(
                projectileGameplay);
            Assert.That(projectiles.TryLaunch(
                "player",
                "target",
                new GameplayPosition(0f, 0f, 10f),
                out _,
                out ProjectileLaunchFailure projectileFailure), Is.False);
            Assert.That(projectileFailure, Is.EqualTo(
                ProjectileLaunchFailure.InsufficientLoadedAmmunition));
            Assert.That(
                projectileGameplay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(4));
            AssertAmmunition(projectileGameplay, loaded: 0, reserve: 18);
        }

        [Test]
        public void StalePreparedAttackCannotSpendAmmoTwice()
        {
            GameplaySession gameplay = CreateGameplay(projectile: false);
            gameplay.EnterTurnMode();
            var attacks = new GameplayAttackSession(gameplay);
            Assert.That(attacks.TryPrepareResolve(
                "player",
                CreateExposure(),
                out GameplayPreparedTransition<GameplayActionRecord> first,
                out _), Is.True);
            Assert.That(attacks.TryPrepareResolve(
                "player",
                CreateExposure(),
                out GameplayPreparedTransition<GameplayActionRecord> stale,
                out _), Is.True);

            Assert.That(attacks.CommitPrepared(first).MatchesPrediction,
                Is.True);
            Assert.Throws<InvalidOperationException>(
                () => attacks.CommitPrepared(stale));

            AssertAmmunition(gameplay, loaded: 2, reserve: 18);
            Assert.That(gameplay.ResolvedActions, Has.Count.EqualTo(1));
        }

        [Test]
        public void OmittedAmmoOutcomeIsRejectedBeforeAnyMutation()
        {
            GameplaySession gameplay = CreateGameplay(projectile: false);
            gameplay.EnterTurnMode();
            var attacks = new GameplayAttackSession(gameplay);
            Assert.That(attacks.TryPrepareResolve(
                "player",
                CreateExposure(),
                out GameplayPreparedTransition<GameplayActionRecord> prepared,
                out _), Is.True);
            GameplayActionRecord source = prepared.Record;
            var malformed = new GameplayActionRecord(
                source.Sequence,
                source.Request,
                source.Cost,
                source.PreviousBudget,
                source.ResultingBudget,
                new[] { source.Outcomes[0] },
                source.Context);

            Assert.Throws<InvalidOperationException>(
                () => attacks.Commit(malformed));

            AssertAmmunition(gameplay, loaded: 3, reserve: 18);
            Assert.That(gameplay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(4));
            Assert.That(gameplay.ResolvedActions, Is.Empty);
        }

        [Test]
        public void CanonicalProjectionRejectsOmittedAmmoOutcome()
        {
            GameplaySession gameplay = CreateGameplay(projectile: false);
            gameplay.EnterTurnMode();
            var attacks = new GameplayAttackSession(gameplay);
            Assert.That(attacks.TryPrepareResolve(
                "player",
                CreateExposure(),
                out GameplayPreparedTransition<GameplayActionRecord> prepared,
                out _), Is.True);
            GameplayActionRecord source = prepared.Record;
            var malformed = new GameplayActionRecord(
                source.Sequence,
                source.Request,
                source.Cost,
                source.PreviousBudget,
                source.ResultingBudget,
                new[] { source.Outcomes[0] },
                source.Context);

            Assert.Throws<InvalidOperationException>(() =>
                GameplayWeaponActionStateProjector.Project(
                    prepared.Previous,
                    malformed));

            AssertAmmunition(gameplay, loaded: 3, reserve: 18);
        }

        [Test]
        public void CanonicalProjectionRejectsWrongRoundsPerUse()
        {
            GameplaySession gameplay = CreateGameplay(projectile: false);
            gameplay.EnterTurnMode();
            var attacks = new GameplayAttackSession(gameplay);
            Assert.That(attacks.TryPrepareResolve(
                "player",
                CreateExposure(),
                out GameplayPreparedTransition<GameplayActionRecord> prepared,
                out _), Is.True);
            GameplayActionRecord source = prepared.Record;
            var wrongSpend = new AmmunitionSpentActionOutcome(
                new WeaponAmmunitionDelta(
                    source.Sequence,
                    "player",
                    "weapon.rifle",
                    "ammo.rifle",
                    WeaponAmmunitionChangeKind.Spend,
                    3,
                    3,
                    2,
                    1,
                    18,
                    18));
            var malformed = new GameplayActionRecord(
                source.Sequence,
                source.Request,
                source.Cost,
                source.PreviousBudget,
                source.ResultingBudget,
                new GameplayActionOutcome[]
                {
                    source.Outcomes[0],
                    wrongSpend,
                },
                source.Context);

            Assert.Throws<InvalidOperationException>(() =>
                GameplayWeaponActionStateProjector.Project(
                    prepared.Previous,
                    malformed));

            AssertAmmunition(gameplay, loaded: 3, reserve: 18);
        }

        private static GameplaySession CreateGameplay(
            bool projectile,
            int loaded = 3)
        {
            AttackDefinition attack = projectile
                ? new AttackDefinition(
                    "attack.launcher",
                    "Launch",
                    new ActionCost(2, 0f, ActionMobility.Set),
                    2f,
                    projectile: new ProjectileFlightDefinition(
                        "projectile.test",
                        4f,
                        0.1f,
                        12f))
                : new AttackDefinition(
                    "attack.rifle",
                    "Fire",
                    new ActionCost(1, 0f, ActionMobility.Set),
                    2f,
                    accuracyDecay: AccuracyDecayDefinition.None);
            var weapon = new InventoryItemDefinition(
                projectile ? "weapon.launcher" : "weapon.rifle",
                projectile ? "Launcher" : "Rifle",
                1,
                InventoryItemKind.Weapon,
                new ActionCost(0, 0f, ActionMobility.Set),
                EquipmentEffectSet.None,
                attack,
                ammunition: new WeaponAmmunitionDefinition(
                    projectile ? "ammo.rocket" : "ammo.rifle",
                    3,
                    loaded,
                    1,
                    new ActionCost(2, 0f, ActionMobility.Set),
                    consumesRemainingMovement: true,
                    reloadPolicyVersion: 1));
            string ammoTypeId = weapon.Ammunition.AmmoTypeId;
            var player = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f),
                new[] { weapon },
                weapon.Id,
                ammunitionReserves: new[]
                {
                    new AmmunitionReserveDefinition(ammoTypeId, 18),
                });
            var target = new ScenarioActorDefinition(
                "target",
                0,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 10f),
                    180f),
                new TurnBudget(0, 8f));
            return new GameplaySession(new ScenarioDefinition(
                projectile ? "ammo-projectile-test" : "ammo-direct-test",
                new ScenarioTimingDefinition(1f),
                new[] { player, target },
                Array.Empty<ScenarioObjectiveDefinition>()),
                scenarioSeed: 7u);
        }

        private static TargetExposureSnapshot CreateExposure() =>
            new TargetExposureSnapshot(
                "player",
                "target",
                new[]
                {
                    new TargetRegionExposure(TargetRegionId.Head, 0, 5),
                    new TargetRegionExposure(TargetRegionId.Torso, 5, 5),
                    new TargetRegionExposure(TargetRegionId.LeftArm, 0, 5),
                    new TargetRegionExposure(TargetRegionId.RightArm, 0, 5),
                    new TargetRegionExposure(TargetRegionId.LeftLeg, 0, 5),
                    new TargetRegionExposure(TargetRegionId.RightLeg, 0, 5),
                });

        private static GameplayProjectileSession CreateProjectileSession(
            GameplaySession gameplay)
        {
            var destructibles = new DestructiblePropSession(
                Array.Empty<DestructiblePropDefinition>());
            return new GameplayProjectileSession(
                gameplay,
                new NeverQueriedSegmentQuery(),
                new GameplayBlastConsequenceResolver(
                    gameplay,
                    destructibles));
        }

        private static void AssertAmmunition(
            GameplaySession gameplay,
            int loaded,
            int reserve)
        {
            GameplayActorSnapshot actor = gameplay.GetActor("player");
            WeaponMagazineSnapshot magazine =
                actor.Ammunition.Magazines[0];
            Assert.That(magazine.LoadedRounds, Is.EqualTo(loaded));
            Assert.That(
                actor.Ammunition.GetReserve(magazine.AmmoTypeId),
                Is.EqualTo(reserve));
        }

        private sealed class NeverQueriedSegmentQuery : IProjectileSegmentQuery
        {
            public ProjectileSegmentQueryResult Query(
                ProjectileSegmentQuery query) =>
                throw new AssertionException(
                    "Launch tests must not advance projectile flight.");
        }
    }
}
