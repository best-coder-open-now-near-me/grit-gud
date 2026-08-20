using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayAmmunitionStateTests
    {
        [Test]
        public void ActorStateInitializesEveryMagazineAndSharedReserve()
        {
            GameplayActorState state = CreateState();

            ActorAmmunitionSnapshot ammunition =
                state.CreateSnapshot().Ammunition;

            Assert.That(
                ammunition.GetMagazine("weapon.rifle").LoadedRounds,
                Is.EqualTo(6));
            Assert.That(
                ammunition.GetMagazine("weapon.sidearm").LoadedRounds,
                Is.EqualTo(3));
            Assert.That(ammunition.GetReserve("ammo.rifle"), Is.EqualTo(18));
        }

        [Test]
        public void SpendChangesOnlyTheSelectedMagazine()
        {
            GameplayActorState state = CreateState();
            var spend = new WeaponAmmunitionDelta(
                1,
                "alpha",
                "weapon.rifle",
                "ammo.rifle",
                WeaponAmmunitionChangeKind.Spend,
                6,
                6,
                1,
                5,
                18,
                18);

            state.ApplyAmmunition(spend);
            ActorAmmunitionSnapshot ammunition =
                state.CreateSnapshot().Ammunition;

            Assert.That(
                ammunition.GetMagazine("weapon.rifle").LoadedRounds,
                Is.EqualTo(5));
            Assert.That(
                ammunition.GetMagazine("weapon.sidearm").LoadedRounds,
                Is.EqualTo(3));
            Assert.That(ammunition.GetReserve("ammo.rifle"), Is.EqualTo(18));
        }

        [Test]
        public void StaleAmmunitionDeltaIsRejectedWithoutMutation()
        {
            GameplayActorState state = CreateState();
            var stale = new WeaponAmmunitionDelta(
                2,
                "alpha",
                "weapon.rifle",
                "ammo.rifle",
                WeaponAmmunitionChangeKind.Spend,
                6,
                5,
                1,
                4,
                18,
                18);
            ActorAmmunitionSnapshot before =
                state.CreateSnapshot().Ammunition;

            Assert.Throws<InvalidOperationException>(
                () => state.ApplyAmmunition(stale));

            ActorAmmunitionSnapshot after =
                state.CreateSnapshot().Ammunition;
            Assert.That(
                after.GetMagazine("weapon.rifle").LoadedRounds,
                Is.EqualTo(before.GetMagazine("weapon.rifle").LoadedRounds));
            Assert.That(
                after.GetReserve("ammo.rifle"),
                Is.EqualTo(before.GetReserve("ammo.rifle")));
        }

        [Test]
        public void ReloadDeltaRequiresExactRoundConservation()
        {
            Assert.Throws<ArgumentException>(() =>
                new WeaponAmmunitionDelta(
                    3,
                    "alpha",
                    "weapon.rifle",
                    "ammo.rifle",
                    WeaponAmmunitionChangeKind.Reload,
                    6,
                    2,
                    4,
                    6,
                    18,
                    15));
        }

        [Test]
        public void AmmoWeaponRequiresExactlyOneMatchingReserveType()
        {
            InventoryItemDefinition weapon = CreateWeapon(
                "weapon.rifle",
                1,
                6);

            Assert.Throws<ArgumentException>(() =>
                new ScenarioActorDefinition(
                    "alpha",
                    1,
                    new GameplayActorPose(
                        new GameplayPosition(0f, 0f, 0f),
                        0f),
                    new TurnBudget(4, 8f),
                    new[] { weapon },
                    weapon.Id));
        }

        [Test]
        public void ScenarioAssemblerCreatesExactWeaponAmmoContract()
        {
            ScenarioActorContentData actor = CreateAuthoredActor();

            GameplayInventoryAssembler.Validate(actor);
            InventoryItemDefinition weapon =
                GameplayInventoryAssembler.CreateDefinitions(actor)[0];
            IReadOnlyList<AmmunitionReserveDefinition> reserves =
                GameplayInventoryAssembler.CreateAmmunitionReserves(actor);

            Assert.That(weapon.Ammunition.AmmoTypeId,
                Is.EqualTo("ammo.rifle"));
            Assert.That(weapon.Ammunition.MagazineCapacity, Is.EqualTo(6));
            Assert.That(weapon.Ammunition.InitialLoadedRounds, Is.EqualTo(5));
            Assert.That(weapon.Ammunition.RoundsPerUse, Is.EqualTo(1));
            Assert.That(weapon.Ammunition.ReloadTurnCost.ActionPoints,
                Is.EqualTo(2));
            Assert.That(reserves, Has.Count.EqualTo(1));
            Assert.That(reserves[0].Rounds, Is.EqualTo(18));
        }

        [Test]
        public void ScenarioAssemblerRejectsAmmoWeaponWithoutReserve()
        {
            ScenarioActorContentData actor = CreateAuthoredActor();
            actor.ammunitionReserves.Clear();

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(
                    () => GameplayInventoryAssembler.Validate(actor));

            Assert.That(exception.Message,
                Does.Contain("ammo.rifle").And.Contain("reserve"));
        }

        private static GameplayActorState CreateState()
        {
            InventoryItemDefinition rifle = CreateWeapon(
                "weapon.rifle",
                1,
                6);
            InventoryItemDefinition sidearm = CreateWeapon(
                "weapon.sidearm",
                2,
                3);
            var definition = new ScenarioActorDefinition(
                "alpha",
                1,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f),
                new[] { rifle, sidearm },
                rifle.Id,
                ammunitionReserves: new[]
                {
                    new AmmunitionReserveDefinition("ammo.rifle", 18),
                });
            return new GameplayActorState(
                definition,
                new ScenarioTimingDefinition(1f));
        }

        private static InventoryItemDefinition CreateWeapon(
            string id,
            int slot,
            int initialLoadedRounds) => new InventoryItemDefinition(
                id,
                id,
                slot,
                InventoryItemKind.Weapon,
                new ActionCost(0, 0f, ActionMobility.Set),
                EquipmentEffectSet.None,
                new AttackDefinition(
                    "attack." + id,
                    "Fire",
                    new ActionCost(2, 0f, ActionMobility.Set),
                    1f,
                    accuracyDecay: new AccuracyDecayDefinition(10f, 25f)),
                ammunition: new WeaponAmmunitionDefinition(
                    "ammo.rifle",
                    6,
                    initialLoadedRounds,
                    1,
                    new ActionCost(2, 0f, ActionMobility.Set),
                    consumesRemainingMovement: true,
                    reloadPolicyVersion: 1));

        private static ScenarioActorContentData CreateAuthoredActor()
        {
            var actor = new ScenarioActorContentData { id = "alpha" };
            actor.inventory.Add(new ScenarioInventoryItemData
            {
                id = "weapon.rifle",
                displayName = "Rifle",
                hotbarSlot = 1,
                kind = "weapon",
                attackCapability = new ScenarioAttackCapabilityData
                {
                    enabled = true,
                    actionId = "attack.rifle",
                    displayName = "Fire",
                    turnCost = new ScenarioActionCostData
                    {
                        actionPoints = 2,
                    },
                    woundMovementPenalty = 1f,
                    accuracyDecay = new ScenarioAccuracyDecayData
                    {
                        halfLifeDistance = 10f,
                        minimumAccuracyPercent = 25f,
                    },
                },
                ammunition = new ScenarioWeaponAmmunitionData
                {
                    ammoTypeId = "ammo.rifle",
                    magazineCapacity = 6,
                    initialLoadedRounds = 5,
                    roundsPerUse = 1,
                    reloadCost = new ScenarioActionCostData
                    {
                        actionPoints = 2,
                    },
                    consumesRemainingMovement = true,
                    reloadPolicyVersion = 1,
                },
            });
            actor.ammunitionReserves.Add(new ScenarioAmmunitionReserveData
            {
                ammoTypeId = "ammo.rifle",
                rounds = 18,
            });
            return actor;
        }
    }
}
