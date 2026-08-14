using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayInventoryAvailabilitySessionTests
    {
        [Test]
        public void EquipmentSwitchProjectsItsCompleteAtomicCost()
        {
            GameplaySession gameplay = CreateSession(actionPoints: 4);
            Assert.That(gameplay.EnterTurnMode(), Is.True);
            var availability = new GameplayInventoryAvailabilitySession(
                gameplay);

            InventoryEquipmentAvailability result =
                availability.EvaluateEquipment("player", "launcher");

            Assert.That(result.IsAvailable, Is.True);
            Assert.That(result.IsSwitch, Is.True);
            Assert.That(result.ResolvedCost.ActionPoints, Is.EqualTo(2));
            Assert.That(result.ResolvedCost.Mobility,
                Is.EqualTo(ActionMobility.Set));
        }

        [Test]
        public void EquipmentSwitchProjectsTheAuthoritativeFailureReason()
        {
            GameplaySession gameplay = CreateSession(actionPoints: 1);
            Assert.That(gameplay.EnterTurnMode(), Is.True);
            var availability = new GameplayInventoryAvailabilitySession(
                gameplay);

            InventoryEquipmentAvailability result =
                availability.EvaluateEquipment("player", "launcher");

            Assert.That(result.IsAvailable, Is.False);
            Assert.That(result.ResolvedCost.ActionPoints, Is.EqualTo(2));
            Assert.That(result.Failure,
                Is.EqualTo(EquipmentChangeFailure.InsufficientActionPoints));
            Assert.That(result.Requirement, Is.EqualTo("INSUFFICIENT AP"));
        }

        [Test]
        public void WeaponPowerRequiresTheItemThatAuthorsIt()
        {
            GameplaySession gameplay = CreateSession(actionPoints: 4);
            var availability = new GameplayInventoryAvailabilitySession(
                gameplay);

            InventoryPowerAvailability result =
                availability.EvaluatePower("player", "launcher");

            Assert.That(result.IsAvailable, Is.False);
            Assert.That(result.Failure, Is.EqualTo(
                InventoryPowerAvailabilityFailure.RequiresEquippedItem));
            Assert.That(result.Requirement,
                Is.EqualTo("REQUIRES EQUIPPED ITEM"));
        }

        [Test]
        public void ConsumableAimRemainsAvailableDuringExplorationReentryLock()
        {
            GameplaySession gameplay = CreateConsumableSession();
            Assert.That(gameplay.EnterTurnMode(), Is.True);
            Assert.That(gameplay.TryExitTurnMode(out _), Is.True);
            Assert.That(gameplay.CanEnterTurnMode, Is.False);
            var availability = new GameplayInventoryAvailabilitySession(
                gameplay);

            InventoryPowerAvailability result =
                availability.EvaluatePower("player", "item.frag");

            Assert.That(result.IsAvailable, Is.True);
            Assert.That(result.ConditionalTurnCost, Is.True);
            Assert.That(result.ResolvedCost.ActionPoints, Is.EqualTo(2));
        }

        private static GameplaySession CreateSession(int actionPoints)
        {
            var equipmentCost = new ActionCost(1, 0f, ActionMobility.Set);
            var rifle = new InventoryItemDefinition(
                "rifle",
                "Rifle",
                1,
                InventoryItemKind.Weapon,
                equipmentCost,
                new EquipmentEffectSet(0.9f),
                CreateAttack("attack.rifle", projectile: null));
            var launcher = new InventoryItemDefinition(
                "launcher",
                "Launcher",
                2,
                InventoryItemKind.Weapon,
                equipmentCost,
                new EquipmentEffectSet(0.75f),
                CreateAttack(
                    "attack.rocket",
                    new ProjectileFlightDefinition(
                        "projectile.rocket",
                        4f,
                        0.12f,
                        24f,
                        1.35f,
                        0.9f,
                        opensEmergencyReactionWindow: true)));
            var player = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(actionPoints, 8f),
                new[] { rifle, launcher },
                "rifle");
            return new GameplaySession(new ScenarioDefinition(
                "inventory-availability-test",
                new ScenarioTimingDefinition(1f),
                new[] { player },
                new ScenarioObjectiveDefinition[0]));
        }

        private static GameplaySession CreateConsumableSession()
        {
            var frag = new InventoryItemDefinition(
                "item.frag",
                "Frag Grenade",
                1,
                InventoryItemKind.Consumable,
                new ActionCost(0, 0f, ActionMobility.Mobile),
                EquipmentEffectSet.None,
                consumablePower: new ThrownExplosiveDefinition(
                    "item.frag",
                    new ActionCost(2, 0f, ActionMobility.Mobile),
                    12f,
                    1.2f,
                    0.8f,
                    0.5f,
                    0.1f,
                    5f,
                    blastWoundMovementPenalty: 2f),
                initialQuantity: 2);
            var player = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f),
                new[] { frag },
                initiallyEquippedItemId: null);
            return new GameplaySession(new ScenarioDefinition(
                "consumable-availability-test",
                new ScenarioTimingDefinition(1f),
                new[] { player },
                new ScenarioObjectiveDefinition[0]));
        }

        private static AttackDefinition CreateAttack(
            string actionId,
            ProjectileFlightDefinition projectile) =>
            new AttackDefinition(
                actionId,
                actionId,
                new ActionCost(1, 0f, ActionMobility.Set),
                2f,
                projectile,
                projectile == null ? AccuracyDecayDefinition.None : null);
    }
}
