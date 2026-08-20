using System;
using System.Collections.Generic;
using System.Threading;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayAmmunitionReloadTests
    {
        [Test]
        public void TurnReloadTransfersExactDeficitAndConsumesAuthoredBudget()
        {
            GameplaySession gameplay = CreateGameplay(
                loaded: 1,
                reserve: 10);
            gameplay.EnterTurnMode();
            var reloads = new GameplayReloadSession(gameplay);
            WeaponAmmunitionDelta observed = null;
            gameplay.AmmunitionChanged += change => observed = change;

            Assert.That(reloads.TryResolve(
                "player",
                out GameplayActionRecord action,
                out GameplayReloadFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(GameplayReloadFailure.None));
            Assert.That(action.Request.ActionId,
                Is.EqualTo(AmmunitionActionIds.Reload));
            Assert.That(action.Cost.ActionPoints, Is.EqualTo(2));
            Assert.That(action.ResultingBudget.ActionPoints, Is.EqualTo(2));
            Assert.That(action.ResultingBudget.MovementOpportunity,
                Is.EqualTo(0f));
            var outcome = (WeaponReloadedActionOutcome)action.Outcomes[0];
            Assert.That(outcome.Change.ChangedRounds, Is.EqualTo(5));
            Assert.That(observed, Is.SameAs(outcome.Change));
            AssertAmmunition(gameplay, loaded: 6, reserve: 5);
        }

        [Test]
        public void ReloadIsBoundedByRemainingReserve()
        {
            GameplaySession gameplay = CreateGameplay(
                loaded: 4,
                reserve: 1);
            gameplay.EnterTurnMode();

            Assert.That(new GameplayReloadSession(gameplay).TryResolve(
                "player",
                out GameplayActionRecord action,
                out GameplayReloadFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(GameplayReloadFailure.None));
            Assert.That(((WeaponReloadedActionOutcome)action.Outcomes[0])
                .Change.ChangedRounds, Is.EqualTo(1));
            AssertAmmunition(gameplay, loaded: 5, reserve: 0);
        }

        [Test]
        public void ExplorationReloadIsFreeAndPreservesMovement()
        {
            GameplaySession gameplay = CreateGameplay(
                loaded: 1,
                reserve: 10);

            Assert.That(new GameplayReloadSession(gameplay).TryResolve(
                "player",
                out GameplayActionRecord action,
                out GameplayReloadFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(GameplayReloadFailure.None));
            Assert.That(action.Cost.ActionPoints, Is.Zero);
            Assert.That(action.Cost.MovementOpportunity, Is.Zero);
            Assert.That(action.ResultingBudget.ActionPoints, Is.EqualTo(4));
            Assert.That(action.ResultingBudget.MovementOpportunity,
                Is.EqualTo(8f));
            AssertAmmunition(gameplay, loaded: 6, reserve: 5);
        }

        [TestCase(6, 10, true, 4, GameplayReloadFailure.MagazineFull)]
        [TestCase(1, 0, true, 4, GameplayReloadFailure.ReserveEmpty)]
        [TestCase(1, 10, false, 4, GameplayReloadFailure.WeaponNotEquipped)]
        [TestCase(1, 10, true, 1,
            GameplayReloadFailure.InsufficientActionPoints)]
        public void IllegalReloadLeavesBudgetAndAmmunitionUnchanged(
            int loaded,
            int reserve,
            bool equipped,
            int actionPoints,
            GameplayReloadFailure expected)
        {
            GameplaySession gameplay = CreateGameplay(
                loaded,
                reserve,
                equipped,
                actionPoints);
            gameplay.EnterTurnMode();
            GameplayActorSnapshot before = gameplay.GetActor("player");

            Assert.That(new GameplayReloadSession(gameplay).TryResolve(
                "player",
                out _,
                out GameplayReloadFailure failure), Is.False);

            Assert.That(failure, Is.EqualTo(expected));
            GameplayActorSnapshot after = gameplay.GetActor("player");
            Assert.That(after.TurnBudget.ActionPoints,
                Is.EqualTo(before.TurnBudget.ActionPoints));
            Assert.That(after.TurnBudget.MovementOpportunity,
                Is.EqualTo(before.TurnBudget.MovementOpportunity));
            AssertAmmunition(gameplay, loaded, reserve);
            Assert.That(gameplay.ResolvedActions, Is.Empty);
        }

        [Test]
        public void SemanticReloadRouteReducesAndReplaysExactly()
        {
            GameplaySession gameplay = CreateGameplay(
                loaded: 0,
                reserve: 10);
            gameplay.EnterTurnMode();
            GameplayCombatStateSnapshot initial =
                GameplayCombatStateCapture.Capture(gameplay);
            InventoryItemDefinition weapon = gameplay.Scenario.GetActor(
                "player").GetInventoryItem("weapon.rifle");
            var input = new GameplayReachableInput(
                GameplayReachableInputKind.ReloadControl,
                "control.reload.weapon.rifle",
                "player",
                GameplayCapabilityProfiles.Reload(weapon.Ammunition),
                weapon.Id);
            GameplayTransitionReducerRegistry reducers =
                GameplaySimulationReducers.CreateCurrent();
            GameplayCapabilityRegistry capabilities =
                GameplayCurrentCapabilityCatalog.Create(
                    reducers,
                    new[] { input });
            var routes = new GameplayCandidateExecutionRouteRegistry(
                capabilities);
            routes.Register(new GameplayReloadCandidateExecutionRoute(
                gameplay.Scenario));
            GameplayCandidate candidate = new GameplayReachableCandidateBuilder(
                capabilities).Build(input);
            var runtime = new GameplaySimulationRuntime(
                CreateExecutionIdentity(gameplay),
                initial,
                reducers,
                capabilities);
            var context = new GameplayDecisionContext(
                initial,
                GameplayObservationSnapshot.FullState("player", initial));

            GameplayExecutableCandidateEvaluation evaluation = routes.Evaluate(
                context,
                candidate);
            Assert.That(evaluation.IsLegal, Is.True);
            Assert.That(evaluation.ExpectedOutcome.GetValue(
                "ammunition.reload-readiness"), Is.EqualTo(1f));
            runtime.Execute(routes.Prepare(context, evaluation));

            GameplayActorSnapshot resulting = runtime.CurrentState.Session
                .GetActor("player");
            Assert.That(resulting.Ammunition.GetMagazine(weapon.Id)
                .LoadedRounds, Is.EqualTo(6));
            Assert.That(resulting.Ammunition.GetReserve("ammo.rifle"),
                Is.EqualTo(4));
            Assert.That(resulting.TurnBudget.ActionPoints, Is.EqualTo(2));
            Assert.That(resulting.TurnBudget.MovementOpportunity,
                Is.EqualTo(0f));
            Assert.That(GameplayExactReplay.Verify(
                initial,
                runtime.Trajectory,
                reducers).IsExact, Is.True);
        }

        [Test]
        public void LiveCanonicalReloadProjectsTheSameStateAndEvent()
        {
            GameplaySession gameplay = CreateGameplay(
                loaded: 0,
                reserve: 10);
            gameplay.EnterTurnMode();
            GameplayCombatStateSnapshot initial =
                GameplayCombatStateCapture.Capture(gameplay);
            InventoryItemDefinition weapon = gameplay.Scenario.GetActor(
                "player").GetInventoryItem("weapon.rifle");
            var input = new GameplayReachableInput(
                GameplayReachableInputKind.ReloadControl,
                "control.reload.weapon.rifle",
                "player",
                GameplayCapabilityProfiles.Reload(weapon.Ammunition),
                weapon.Id);
            GameplayTransitionReducerRegistry reducers =
                GameplaySimulationReducers.CreateCurrent();
            GameplayCapabilityRegistry capabilities =
                GameplayCurrentCapabilityCatalog.Create(
                    reducers,
                    new[] { input });
            WeaponAmmunitionDelta observed = null;
            gameplay.AmmunitionChanged += change => observed = change;

            using (var live = new GameplayLiveSessionRuntime(
                gameplay,
                CreateExecutionIdentity(gameplay),
                initial,
                reducers,
                capabilities))
            {
                Assert.That(new GameplayReloadSession(gameplay).TryResolve(
                    "player",
                    out GameplayActionRecord action,
                    out GameplayReloadFailure failure), Is.True);

                Assert.That(failure, Is.EqualTo(GameplayReloadFailure.None));
                Assert.That(observed, Is.SameAs(
                    ((WeaponReloadedActionOutcome)action.Outcomes[0]).Change));
                Assert.That(GameplayCombatStateCapture.Capture(gameplay)
                    .CanonicalHash, Is.EqualTo(live.CurrentState.CanonicalHash));
                Assert.That(live.Trajectory, Has.Count.EqualTo(1));
            }
            AssertAmmunition(gameplay, loaded: 6, reserve: 4);
        }

        [Test]
        public void StaleReloadCannotTransferTheSameReserveTwice()
        {
            GameplaySession gameplay = CreateGameplay(
                loaded: 1,
                reserve: 10);
            gameplay.EnterTurnMode();
            var reloads = new GameplayReloadSession(gameplay);

            Assert.That(reloads.TryResolve(
                "player",
                out GameplayActionRecord committed,
                out _), Is.True);
            Assert.Throws<InvalidOperationException>(() =>
                reloads.Commit(committed));

            AssertAmmunition(gameplay, loaded: 6, reserve: 5);
            Assert.That(gameplay.ResolvedActions, Has.Count.EqualTo(1));
        }

        [Test]
        public void BaselinePolicyPrefersReloadToEndingAnEmptyWeaponTurn()
        {
            GameplaySession gameplay = CreateGameplay(
                loaded: 0,
                reserve: 10);
            gameplay.EnterTurnMode();
            GameplayCombatStateSnapshot state =
                GameplayCombatStateCapture.Capture(gameplay);
            WeaponAmmunitionDefinition ammunition = gameplay.Scenario
                .GetActor("player")
                .GetInventoryItem("weapon.rifle")
                .Ammunition;
            var reloadInput = new GameplayReachableInput(
                GameplayReachableInputKind.ReloadControl,
                "control.reload.weapon.rifle",
                "player",
                GameplayCapabilityProfiles.Reload(ammunition),
                "weapon.rifle");
            var endInput = new GameplayReachableInput(
                GameplayReachableInputKind.EndTurnControl,
                "control.end-turn",
                "player",
                GameplayCapabilityProfiles.EndTurn(emergency: false));
            GameplayTransitionReducerRegistry reducers =
                GameplaySimulationReducers.CreateCurrent();
            GameplayCapabilityRegistry capabilities =
                GameplayCurrentCapabilityCatalog.Create(
                    reducers,
                    new[] { reloadInput, endInput });
            var routes = new GameplayCandidateExecutionRouteRegistry(
                capabilities);
            routes.Register(new GameplayReloadCandidateExecutionRoute(
                gameplay.Scenario));
            routes.Register(new GameplayEndTurnCandidateExecutionRoute(
                gameplay.Scenario));
            var candidates = new GameplayReachableCandidateBuilder(
                capabilities);
            var context = new GameplayDecisionContext(
                state,
                GameplayObservationSnapshot.FullState("player", state));
            GameplayExecutableCandidateEvaluation reload = routes.Evaluate(
                context,
                candidates.Build(reloadInput));
            GameplayExecutableCandidateEvaluation end = routes.Evaluate(
                context,
                candidates.Build(endInput));
            IGameplayCandidatePolicy policy = GameplayBaselineCombatPolicy
                .Create(gameplay.Scenario);

            Assert.That(reload.IsLegal, Is.True);
            Assert.That(end.IsLegal, Is.True);
            Assert.That(policy.Score(
                    context,
                    reload,
                    CancellationToken.None).Value,
                Is.GreaterThan(policy.Score(
                    context,
                    end,
                    CancellationToken.None).Value));
        }

        [Test]
        public void ReducerRejectsPartialReloadAgainstCanonicalTransfer()
        {
            GameplaySession gameplay = CreateGameplay(
                loaded: 1,
                reserve: 10);
            gameplay.EnterTurnMode();
            GameplayCombatStateSnapshot initial =
                GameplayCombatStateCapture.Capture(gameplay);
            WeaponAmmunitionDefinition definition = gameplay.Scenario
                .GetActor("player")
                .GetInventoryItem("weapon.rifle")
                .Ammunition;
            Assert.That(GameplayReloadPreparation.TryPrepare(
                gameplay.Scenario,
                initial.Session,
                "player",
                "weapon.rifle",
                out GameplayResolvedActionTransitionPayload legal,
                out _), Is.True);
            GameplayActionRecord source = legal.Action;
            var partial = new WeaponReloadedActionOutcome(
                new WeaponAmmunitionDelta(
                    source.Sequence,
                    "player",
                    "weapon.rifle",
                    "ammo.rifle",
                    WeaponAmmunitionChangeKind.Reload,
                    6,
                    1,
                    4,
                    5,
                    10,
                    6));
            var malformedAction = new GameplayActionRecord(
                source.Sequence,
                source.Request,
                source.Cost,
                source.PreviousBudget,
                source.ResultingBudget,
                new GameplayActionOutcome[] { partial });
            var payload = new GameplayResolvedActionTransitionPayload(
                GameplayCapabilityProfiles.Reload(definition),
                malformedAction);
            var transition = new GameplaySemanticTransition(
                new GameplayTransitionIdentity(
                    1L,
                    GameplaySemanticCapability.Reload.ToString(),
                    "player",
                    "weapon.rifle"),
                initial.CanonicalHash,
                payload);

            Assert.Throws<InvalidOperationException>(() =>
                GameplaySimulationReducers.CreateCurrent().Reduce(
                    initial,
                    transition));
            Assert.That(initial.Session.GetActor("player").Ammunition
                .GetMagazine("weapon.rifle").LoadedRounds, Is.EqualTo(1));
            Assert.That(initial.Session.GetActor("player").Ammunition
                .GetReserve("ammo.rifle"), Is.EqualTo(10));
        }

        [Test]
        public void ReloadRouteRejectsNearMatchCapabilityProfile()
        {
            GameplaySession gameplay = CreateGameplay(
                loaded: 0,
                reserve: 10);
            WeaponAmmunitionDefinition definition = gameplay.Scenario
                .GetActor("player")
                .GetInventoryItem("weapon.rifle")
                .Ammunition;
            GameplayCapabilityProfile exact =
                GameplayCapabilityProfiles.Reload(definition);
            var traits = new List<GameplayCapabilityTrait>(exact.Traits)
            {
                new GameplayCapabilityTrait("near-match", "true"),
            };
            var nearMatch = new GameplayCapabilityProfile(
                GameplaySemanticCapability.Reload,
                exact.SemanticVersion,
                traits);

            Assert.That(new GameplayReloadCandidateExecutionRoute(
                gameplay.Scenario).Supports(exact), Is.True);
            Assert.That(new GameplayReloadCandidateExecutionRoute(
                gameplay.Scenario).Supports(nearMatch), Is.False);
        }

        private static GameplaySession CreateGameplay(
            int loaded,
            int reserve,
            bool equipped = true,
            int actionPoints = 4)
        {
            var attack = new AttackDefinition(
                "attack.rifle",
                "Fire",
                new ActionCost(1, 0f, ActionMobility.Set),
                2f,
                accuracyDecay: AccuracyDecayDefinition.None);
            var weapon = new InventoryItemDefinition(
                "weapon.rifle",
                "Rifle",
                1,
                InventoryItemKind.Weapon,
                new ActionCost(1, 0f, ActionMobility.Set),
                EquipmentEffectSet.None,
                attack,
                ammunition: new WeaponAmmunitionDefinition(
                    "ammo.rifle",
                    magazineCapacity: 6,
                    initialLoadedRounds: loaded,
                    roundsPerUse: 1,
                    reloadTurnCost: new ActionCost(
                        2,
                        0f,
                        ActionMobility.Set),
                    consumesRemainingMovement: true,
                    reloadPolicyVersion: 1));
            var player = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(actionPoints, 8f),
                new[] { weapon },
                equipped ? weapon.Id : null,
                ammunitionReserves: new[]
                {
                    new AmmunitionReserveDefinition("ammo.rifle", reserve),
                });
            var target = new ScenarioActorDefinition(
                "target",
                0,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 10f),
                    180f),
                new TurnBudget(0, 8f));
            return new GameplaySession(new ScenarioDefinition(
                "reload-test",
                new ScenarioTimingDefinition(1f),
                new[] { player, target },
                Array.Empty<ScenarioObjectiveDefinition>()),
                scenarioSeed: 11u);
        }

        private static GameplayExecutionIdentity CreateExecutionIdentity(
            GameplaySession gameplay) => new GameplayExecutionIdentity(
            new GameplayContentIdentity(
                gameplay.Scenario.Id,
                scenarioSchemaVersion: 1,
                rulesSchemaVersion: 1,
                new string('a', 64)),
            new SpatialContentIdentity(
                "reload-level",
                levelSchemaVersion: 1,
                evidenceAlgorithmVersion: 1,
                new string('b', 64)),
            gameplay.RunIdentity);

        private static void AssertAmmunition(
            GameplaySession gameplay,
            int loaded,
            int reserve)
        {
            ActorAmmunitionSnapshot ammunition = gameplay.GetActor("player")
                .Ammunition;
            Assert.That(ammunition.GetMagazine("weapon.rifle").LoadedRounds,
                Is.EqualTo(loaded));
            Assert.That(ammunition.GetReserve("ammo.rifle"),
                Is.EqualTo(reserve));
        }
    }
}
