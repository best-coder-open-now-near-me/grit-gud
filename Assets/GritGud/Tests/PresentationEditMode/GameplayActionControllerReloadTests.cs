using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayActionControllerReloadTests
    {
        [Test]
        public void ActionControllerCommitsReloadAndPresentsCanonicalCounts()
        {
            var root = new GameObject("Reload Controller Test");
            try
            {
                GameplaySession gameplay = CreateGameplay(
                    loaded: 1,
                    reserve: 10);
                GameplayActionController controller = root.AddComponent<
                    GameplayActionController>();
                GameplaySessionPresenter presenter = root.AddComponent<
                    GameplaySessionPresenter>();
                ActorAnimationCoordinator animation = root.AddComponent<
                    ActorAnimationCoordinator>();
                controller.Bind(
                    gameplay,
                    presenter,
                    animation,
                    "player",
                    primaryObjectiveId: null);
                GameplayActionRecord observed = null;
                controller.ActionResolved += action => observed = action;

                Assert.That(controller.TryReload(), Is.True);

                Assert.That(controller.LastReloadFailure,
                    Is.EqualTo(GameplayReloadFailure.None));
                Assert.That(controller.LastResolvedAction,
                    Is.SameAs(observed));
                Assert.That(controller.StatusMessage,
                    Is.EqualTo("Rifle reloaded: 6 / 5."));
                Assert.That(gameplay.GetActor("player").Ammunition
                    .GetMagazine("weapon.rifle").LoadedRounds,
                    Is.EqualTo(6));
                Assert.That(gameplay.GetActor("player").Ammunition
                    .GetReserve("ammo.rifle"), Is.EqualTo(5));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ActionControllerExplainsRejectedReloadWithoutMutation()
        {
            var root = new GameObject("Rejected Reload Controller Test");
            try
            {
                GameplaySession gameplay = CreateGameplay(
                    loaded: 6,
                    reserve: 10);
                GameplayActionController controller = root.AddComponent<
                    GameplayActionController>();
                controller.Bind(
                    gameplay,
                    root.AddComponent<GameplaySessionPresenter>(),
                    root.AddComponent<ActorAnimationCoordinator>(),
                    "player",
                    primaryObjectiveId: null);

                Assert.That(controller.TryReload(), Is.False);

                Assert.That(controller.LastReloadFailure,
                    Is.EqualTo(GameplayReloadFailure.MagazineFull));
                Assert.That(controller.StatusMessage,
                    Is.EqualTo(
                        "The equipped weapon is already fully loaded."));
                Assert.That(gameplay.ResolvedActions, Is.Empty);
                Assert.That(gameplay.GetActor("player").Ammunition
                    .GetMagazine("weapon.rifle").LoadedRounds,
                    Is.EqualTo(6));
                Assert.That(gameplay.GetActor("player").Ammunition
                    .GetReserve("ammo.rifle"), Is.EqualTo(10));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameplaySession CreateGameplay(
            int loaded,
            int reserve)
        {
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
                    accuracyDecay: AccuracyDecayDefinition.None),
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
                new TurnBudget(4, 8f),
                new[] { rifle },
                rifle.Id,
                ammunitionReserves: new[]
                {
                    new AmmunitionReserveDefinition("ammo.rifle", reserve),
                });
            return new GameplaySession(new ScenarioDefinition(
                "reload-controller-test",
                new ScenarioTimingDefinition(1f),
                new[] { player },
                new ScenarioObjectiveDefinition[0]));
        }
    }
}
