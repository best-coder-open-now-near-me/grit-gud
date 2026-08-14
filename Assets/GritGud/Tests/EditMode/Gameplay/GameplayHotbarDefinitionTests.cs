using System;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayHotbarDefinitionTests
    {
        [Test]
        public void InventoryItemsRejectSlotsOutsideTheSharedHotbarRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CreateWeapon(GameplayHotbarRules.SlotCount + 1));
        }

        [Test]
        public void ActorRejectsInventoryAndAbilitySlotCollisions()
        {
            InventoryItemDefinition weapon = CreateWeapon(4);
            var push = new DisplacementActionDefinition(
                "close-quarters.push",
                "Push",
                DisplacementActionKind.Push,
                new ActionCost(1, 0f, ActionMobility.Mobile),
                DisplacementSubjectKinds.Prop,
                reach: 2f,
                maximumDistance: 3f,
                maximumSubjectMass: 90f,
                DisplacementHandRequirement.OneHandFree,
                DisplacementAutoStowPolicy.Allowed,
                DisplacementContestPolicy.None,
                DisplacementResultPolicies.Topple);
            var displace = new DisplacementAbilityDefinition(
                "ability.displace",
                "Displace",
                hotbarSlot: 4,
                new[] { push });

            Assert.Throws<ArgumentException>(() =>
                new ScenarioActorDefinition(
                    "player",
                    10,
                    new GameplayActorPose(
                        new GameplayPosition(0f, 0f, 0f),
                        0f),
                    new TurnBudget(4, 8f),
                    new[] { weapon },
                    initiallyEquippedItemId: "weapon.rifle",
                    displacementAbility: displace));
        }

        private static InventoryItemDefinition CreateWeapon(int hotbarSlot) =>
            new InventoryItemDefinition(
                "weapon.rifle",
                "Rifle",
                hotbarSlot,
                InventoryItemKind.Weapon,
                new ActionCost(1, 0f, ActionMobility.Set),
                new EquipmentEffectSet(0.9f),
                new AttackDefinition(
                    "attack.rifle",
                    "Fire rifle",
                    new ActionCost(1, 0f, ActionMobility.Set),
                    2f,
                    accuracyDecay: AccuracyDecayDefinition.None));
    }
}
