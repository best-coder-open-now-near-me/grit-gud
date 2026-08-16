using System;
using System.Linq;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayEquipmentSessionTests
    {
        [Test]
        public void ConsumableAcceptsAnyAuthoredPowerDefinition()
        {
            var power = new TestConsumablePowerDefinition(
                "item.medkit",
                new ActionCost(1, 0f, ActionMobility.Mobile));
            var item = new InventoryItemDefinition(
                power.Id,
                "Medkit",
                4,
                InventoryItemKind.Consumable,
                new ActionCost(0, 0f, ActionMobility.Mobile),
                EquipmentEffectSet.None,
                consumablePower: power,
                initialQuantity: 2);

            Assert.That(item.ConsumablePower, Is.SameAs(power));
            Assert.That(item.HasPower, Is.True);
            Assert.That(item.IsEquippable, Is.False);
        }

        [Test]
        public void DirectSwitchRecordsSymmetricUnequipAndEquipActions()
        {
            GameplaySession session = CreateSession(actionPoints: 4);
            session.EnterTurnMode();
            var equipment = new GameplayEquipmentSession(session);
            int changeEvents = 0;
            session.EquipmentChanged += _ => changeEvents++;

            bool switched = equipment.TryResolveSwitch(
                "player",
                "launcher",
                out GameplayActionRecord unequip,
                out GameplayActionRecord equip,
                out EquipmentChangeFailure failure);

            Assert.That(switched, Is.True);
            Assert.That(failure, Is.EqualTo(EquipmentChangeFailure.None));
            Assert.That(unequip.Request.ActionId,
                Is.EqualTo(EquipmentActionIds.Unequip));
            Assert.That(equip.Request.ActionId,
                Is.EqualTo(EquipmentActionIds.Equip));
            Assert.That(unequip.Sequence, Is.EqualTo(1));
            Assert.That(equip.Sequence, Is.EqualTo(2));
            Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(2));
            Assert.That(session.GetActor("player").EquippedItemId,
                Is.EqualTo("launcher"));
            Assert.That(
                session.GetEquipmentEffects("player").MovementSpeedMultiplier,
                Is.EqualTo(0.75f));
            Assert.That(equipment.Records.Select(record => record.Kind),
                Is.EqualTo(new[]
                {
                    EquipmentChangeKind.Unequip,
                    EquipmentChangeKind.Equip,
                }));
            Assert.That(changeEvents, Is.EqualTo(2));
            Assert.That(session.Journal.Entries.Count(entry =>
                entry.Kind == GameplayJournalEntryKind.ActionResolved),
                Is.EqualTo(2));
        }

        [Test]
        public void ObserverFailureCannotInterruptAnEquipmentSwitch()
        {
            GameplaySession session = CreateSession(actionPoints: 4);
            session.EnterTurnMode();
            var equipment = new GameplayEquipmentSession(session);
            int successfulObservers = 0;
            session.EquipmentChanged += _ =>
                throw new InvalidOperationException("observer failed");
            session.EquipmentChanged += _ => successfulObservers++;

            Assert.Throws<AggregateException>(() =>
                equipment.TryResolveSwitch(
                    "player",
                    "launcher",
                    out _,
                    out _,
                    out _));

            Assert.That(successfulObservers, Is.EqualTo(2));
            Assert.That(session.GetActor("player").EquippedItemId,
                Is.EqualTo("launcher"));
            Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(2));
            Assert.That(session.ResolvedActions, Has.Count.EqualTo(2));
            Assert.That(equipment.Records, Has.Count.EqualTo(2));
        }

        [Test]
        public void EquipmentActionsReplayThroughTheExistingActionPipeline()
        {
            GameplaySession source = CreateSession(actionPoints: 4);
            source.EnterTurnMode();
            var sourceEquipment = new GameplayEquipmentSession(source);
            Assert.That(sourceEquipment.TryResolveSwitch(
                "player",
                "launcher",
                out GameplayActionRecord unequip,
                out GameplayActionRecord equip,
                out _), Is.True);

            GameplaySession replay = CreateSession(actionPoints: 4);
            replay.EnterTurnMode();
            var replayEquipment = new GameplayEquipmentSession(replay);
            replayEquipment.Commit(unequip);
            replayEquipment.Commit(equip);

            Assert.That(replay.GetActor("player").EquippedItemId,
                Is.EqualTo("launcher"));
            Assert.That(replay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(2));
            Assert.That(replay.ResolvedActions, Has.Count.EqualTo(2));
        }

        [Test]
        public void UnequippingLeavesHandsEmptyAndRestoresNeutralEffects()
        {
            GameplaySession session = CreateSession(actionPoints: 4);
            session.EnterTurnMode();
            var equipment = new GameplayEquipmentSession(session);

            bool unequipped = equipment.TryResolve(
                "player",
                "rifle",
                equip: false,
                out _,
                out EquipmentChangeFailure failure);

            Assert.That(unequipped, Is.True);
            Assert.That(failure, Is.EqualTo(EquipmentChangeFailure.None));
            Assert.That(session.GetActor("player").EquippedItemId, Is.Null);
            Assert.That(session.GetEquippedAttack("player"), Is.Null);
            Assert.That(
                session.GetEquipmentEffects("player").MovementSpeedMultiplier,
                Is.EqualTo(1f));
            Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(3));
        }

        [Test]
        public void UnaffordableSwitchDoesNotPartiallyUnequip()
        {
            GameplaySession session = CreateSession(actionPoints: 1);
            session.EnterTurnMode();
            var equipment = new GameplayEquipmentSession(session);

            bool switched = equipment.TryResolveSwitch(
                "player",
                "launcher",
                out _,
                out _,
                out EquipmentChangeFailure failure);

            Assert.That(switched, Is.False);
            Assert.That(failure,
                Is.EqualTo(EquipmentChangeFailure.InsufficientActionPoints));
            Assert.That(session.GetActor("player").EquippedItemId,
                Is.EqualTo("rifle"));
            Assert.That(session.ResolvedActions, Is.Empty);
        }

        [Test]
        public void EquipmentSwitchesAreFreeOutsideTurnModeAndRemainReplayable()
        {
            GameplaySession session = CreateSession(actionPoints: 4);
            var equipment = new GameplayEquipmentSession(session);

            bool switched = equipment.TryResolveSwitch(
                "player",
                "launcher",
                out GameplayActionRecord unequip,
                out GameplayActionRecord equip,
                out EquipmentChangeFailure failure);

            Assert.That(switched, Is.True);
            Assert.That(failure, Is.EqualTo(EquipmentChangeFailure.None));
            Assert.That(unequip.Cost.ActionPoints, Is.Zero);
            Assert.That(equip.Cost.ActionPoints, Is.Zero);
            Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(4));
            Assert.That(session.GetActor("player").EquippedItemId,
                Is.EqualTo("launcher"));

            GameplaySession replay = CreateSession(actionPoints: 4);
            var replayEquipment = new GameplayEquipmentSession(replay);
            replayEquipment.Commit(unequip);
            replayEquipment.Commit(equip);
            Assert.That(replay.GetActor("player").EquippedItemId,
                Is.EqualTo("launcher"));
            Assert.That(replay.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(4));
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
                "equipment-test",
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

        private sealed class TestConsumablePowerDefinition
            : ConsumablePowerDefinition
        {
            public TestConsumablePowerDefinition(string id, ActionCost turnCost)
                : base(id, turnCost)
            {
            }

            public override string PowerTypeId => "test-power";
        }
    }
}
