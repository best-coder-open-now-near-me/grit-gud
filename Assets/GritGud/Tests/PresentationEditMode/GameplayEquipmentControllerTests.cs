using System;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using GritGud.Application.Gameplay;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayEquipmentControllerTests
    {
        [Test]
        public void WarningSelectorUsesTheHighestPriorityBehaviorHint()
        {
            var advisory = new StaticWarningSource(
                new GameplayWarningHintModel(
                    "movement.advisory",
                    "Movement warning",
                    10));
            var confirmation = new StaticWarningSource(
                new GameplayWarningHintModel(
                    "equipment.confirmation",
                    "Equipment warning",
                    100));

            Assert.That(
                GameplayWarningHintSelector.Select(
                    new IGameplayWarningHintSource[]
                    {
                        advisory,
                        confirmation,
                    }),
                Is.SameAs(confirmation.CurrentWarningHint));
        }

        [Test]
        public void UnequippedHotkeyRequiresSecondPressAndSwitchesAtDoubleCost()
        {
            var root = new GameObject("Equipment Controller Test");
            try
            {
                GameplaySession session = CreateSession();
                session.EnterTurnMode();
                GameplayEquipmentController controller =
                    root.AddComponent<GameplayEquipmentController>();
                int powerRequests = 0;
                controller.Bind(session, "player", _ => powerRequests++);

                Assert.That(controller.TryActivateItem("launcher"), Is.True);
                Assert.That(controller.PendingItemId, Is.EqualTo("launcher"));
                Assert.That(controller.StatusMessage, Does.Contain("2 AP"));
                Assert.That(
                    controller.CurrentWarningHint.SourceId,
                    Is.EqualTo("equipment.confirmation"));
                Assert.That(
                    controller.CurrentWarningHint.Text,
                    Is.EqualTo(
                        "CONFIRM SWITCH TO LAUNCHER - CLICK THE ORANGE ARROW "
                        + "OR PRESS [2] AGAIN - ESC TO CANCEL"));
                Assert.That(session.ResolvedActions, Is.Empty);

                Assert.That(controller.TryActivateItem("launcher"), Is.True);
                Assert.That(controller.HasPendingConfirmation, Is.False);
                Assert.That(controller.CurrentWarningHint, Is.Null);
                Assert.That(session.GetActor("player").EquippedItemId,
                    Is.EqualTo("launcher"));
                Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                    Is.EqualTo(2));
                Assert.That(session.ResolvedActions, Has.Count.EqualTo(2));
                Assert.That(powerRequests, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EquippedHotkeyRequestsPowerImmediatelyAndCancelClearsConfirmation()
        {
            var root = new GameObject("Equipment Controller Test");
            try
            {
                GameplaySession session = CreateSession();
                session.EnterTurnMode();
                GameplayEquipmentController controller =
                    root.AddComponent<GameplayEquipmentController>();
                int powerRequests = 0;
                string requestedItem = null;
                controller.Bind(session, "player", itemId =>
                {
                    powerRequests++;
                    requestedItem = itemId;
                });

                Assert.That(controller.TryActivateItem("rifle"), Is.True);
                Assert.That(powerRequests, Is.EqualTo(1));
                Assert.That(requestedItem, Is.EqualTo("rifle"));
                Assert.That(session.ResolvedActions, Is.Empty);

                Assert.That(controller.TryToggleEquipment("rifle"), Is.True);
                Assert.That(controller.HasPendingConfirmation, Is.True);
                Assert.That(
                    controller.CurrentWarningHint.Text,
                    Is.EqualTo(
                        "CONFIRM UNEQUIP RIFLE - CLICK THE ORANGE ARROW OR "
                        + "PRESS [1] AGAIN - ESC TO CANCEL"));
                Assert.That(controller.CancelPending(), Is.True);
                Assert.That(controller.HasPendingConfirmation, Is.False);
                Assert.That(controller.CurrentWarningHint, Is.Null);
                Assert.That(session.GetActor("player").EquippedItemId,
                    Is.EqualTo("rifle"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ItemCanBeActivatedByIdentityWithoutDependingOnItsHotbarSlot()
        {
            var root = new GameObject("Equipment Controller Test");
            try
            {
                GameplaySession session = CreateSession();
                session.EnterTurnMode();
                GameplayEquipmentController controller =
                    root.AddComponent<GameplayEquipmentController>();
                controller.Bind(session, "player", _ => { });

                Assert.That(controller.TryActivateItem("launcher"), Is.True);
                Assert.That(controller.PendingItemId, Is.EqualTo("launcher"));
                Assert.That(controller.TryActivateItem("missing"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PowerGateBlocksWeaponWithoutCancelingPendingState()
        {
            var root = new GameObject("Equipment Power Gate Test");
            try
            {
                GameplaySession session = CreateSession();
                session.EnterTurnMode();
                GameplayEquipmentController controller =
                    root.AddComponent<GameplayEquipmentController>();
                int powerRequests = 0;
                controller.Bind(
                    session,
                    "player",
                    _ => powerRequests++,
                    _ => false);

                Assert.That(controller.TryActivateItem("launcher"), Is.True);
                Assert.That(controller.PendingItemId, Is.EqualTo("launcher"));

                Assert.That(controller.TryActivateItem("rifle"), Is.False);
                Assert.That(powerRequests, Is.Zero);
                Assert.That(controller.PendingItemId, Is.EqualTo("launcher"));
                Assert.That(session.ResolvedActions, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HotbarControllerReassignsOwnedItemsIndependentlyOfEquipment()
        {
            var root = new GameObject("Equipment Controller Test");
            try
            {
                GameplaySession session = CreateSession();
                session.EnterTurnMode();
                GameplayEquipmentController controller =
                    root.AddComponent<GameplayEquipmentController>();
                controller.Bind(session, "player", _ => { });
                GameplayHotbarController hotbar =
                    root.AddComponent<GameplayHotbarController>();
                hotbar.Bind(
                    session,
                    "player",
                    CreateActorAbilityHotbarDefinitions(),
                    controller.TryActivateItem,
                    (_, __) => false);

                var launcher = new GameplayHotbarBinding(
                    GameplayHotbarBindingKind.InventoryItem,
                    "launcher");
                Assert.That(hotbar.TryBindSlot(8, launcher), Is.True);
                Assert.That(hotbar.Bindings[8], Is.EqualTo(launcher));
                Assert.That(hotbar.Bindings.ContainsKey(2), Is.False);
                Assert.That(hotbar.StatusMessage,
                    Is.EqualTo("Launcher assigned to hotkey 8."));
                Assert.That(hotbar.TryActivateSlot(8), Is.True);
                Assert.That(controller.PendingItemId, Is.EqualTo("launcher"));
                Assert.That(
                    controller.CurrentWarningHint.Text,
                    Does.Contain("PRESS [8] AGAIN"));
                Assert.That(hotbar.TryBindSlot(0, launcher), Is.False);
                Assert.That(
                    hotbar.TryBindSlot(
                        1,
                        new GameplayHotbarBinding(
                            GameplayHotbarBindingKind.InventoryItem,
                            "missing")),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HotbarControllerAuthorsAndActivatesActorAbilities()
        {
            var root = new GameObject("Actor Ability Hotbar Test");
            try
            {
                GameplaySession session = CreateSession();
                string requestedAbility = null;
                string requestedOption = null;
                GameplayHotbarController hotbar =
                    root.AddComponent<GameplayHotbarController>();
                hotbar.Bind(
                    session,
                    "player",
                    CreateActorAbilityHotbarDefinitions(),
                    (_, __) => false,
                    (abilityId, optionId) =>
                    {
                        requestedAbility = abilityId;
                        requestedOption = optionId;
                        return true;
                    });

                Assert.That(
                    hotbar.Bindings[4].Kind,
                    Is.EqualTo(GameplayHotbarBindingKind.ActorAbility));
                Assert.That(hotbar.ActorAbilities, Has.Count.EqualTo(1));
                Assert.That(
                    hotbar.ActorAbilities[0].Options,
                    Has.Count.EqualTo(1));
                Assert.That(
                    hotbar.Bindings[4].ContentId,
                    Is.EqualTo("ability.displace"));
                Assert.That(hotbar.TryActivateSlot(4), Is.True);
                Assert.That(hotbar.HasExpandedActorAbility, Is.True);
                Assert.That(requestedAbility, Is.Null);
                Assert.That(
                    hotbar.TryActivateExpandedActorAbilityOption(1),
                    Is.True);
                Assert.That(requestedAbility,
                    Is.EqualTo("ability.displace"));
                Assert.That(requestedOption,
                    Is.EqualTo("close-quarters.push"));
                Assert.That(hotbar.HasExpandedActorAbility, Is.False);
                Assert.That(hotbar.TryActivateSlot(4), Is.True);
                Assert.That(
                    hotbar.TryHandleExpandedActorAbilityHotkey(4),
                    Is.True);
                Assert.That(hotbar.HasExpandedActorAbility, Is.False);

                var displace = new GameplayHotbarBinding(
                    GameplayHotbarBindingKind.ActorAbility,
                    "ability.displace");
                Assert.That(hotbar.TryBindSlot(6, displace), Is.True);
                Assert.That(hotbar.Bindings.ContainsKey(4), Is.False);
                Assert.That(hotbar.Bindings[6], Is.EqualTo(displace));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HotbarControllerKeepsIndependentPartyMemberLayouts()
        {
            var root = new GameObject("Party Hotbar Layout Test");
            try
            {
                GameplaySession session = CreatePartySession();
                GameplayHotbarController hotbar =
                    root.AddComponent<GameplayHotbarController>();
                hotbar.Bind(
                    session,
                    "player",
                    Array.Empty<GameplayActorAbilityHotbarDefinition>(),
                    (_, __) => false,
                    (_, __) => false);
                var rifle = new GameplayHotbarBinding(
                    GameplayHotbarBindingKind.InventoryItem,
                    "rifle");
                var sidearm = new GameplayHotbarBinding(
                    GameplayHotbarBindingKind.InventoryItem,
                    "sidearm");

                Assert.That(hotbar.TryBindSlot(8, rifle), Is.True);
                hotbar.SetActor(
                    "scout",
                    Array.Empty<GameplayActorAbilityHotbarDefinition>());

                Assert.That(hotbar.Bindings.ContainsKey(8), Is.False);
                Assert.That(hotbar.Bindings[2], Is.EqualTo(sidearm));
                Assert.That(hotbar.TryBindSlot(7, sidearm), Is.True);

                hotbar.SetActor(
                    "player",
                    Array.Empty<GameplayActorAbilityHotbarDefinition>());

                Assert.That(hotbar.Bindings[8], Is.EqualTo(rifle));
                Assert.That(hotbar.Bindings.ContainsKey(7), Is.False);

                hotbar.SetActor(
                    "scout",
                    Array.Empty<GameplayActorAbilityHotbarDefinition>());

                Assert.That(hotbar.Bindings[7], Is.EqualTo(sidearm));
                Assert.That(hotbar.Bindings.ContainsKey(8), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UnequippedHotkeySwitchesFreeOutsideTurnMode()
        {
            var root = new GameObject("Equipment Controller Test");
            try
            {
                GameplaySession session = CreateSession();
                GameplayEquipmentController controller =
                    root.AddComponent<GameplayEquipmentController>();
                controller.Bind(session, "player", _ => { });

                Assert.That(controller.TryActivateItem("launcher"), Is.True);
                Assert.That(controller.StatusMessage,
                    Does.Contain("FREE OUT OF TURN MODE"));
                Assert.That(controller.TryActivateItem("launcher"), Is.True);
                Assert.That(session.GetActor("player").EquippedItemId,
                    Is.EqualTo("launcher"));
                Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                    Is.EqualTo(4));
                Assert.That(session.ResolvedActions, Has.Count.EqualTo(2));
                Assert.That(session.ResolvedActions[0].Cost.ActionPoints, Is.Zero);
                Assert.That(session.ResolvedActions[1].Cost.ActionPoints, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameplaySession CreateSession()
        {
            var cost = new ActionCost(1, 0f, ActionMobility.Set);
            var rifle = new InventoryItemDefinition(
                "rifle",
                "Rifle",
                1,
                InventoryItemKind.Weapon,
                cost,
                new EquipmentEffectSet(0.9f),
                CreateAttack("attack.rifle"));
            var launcher = new InventoryItemDefinition(
                "launcher",
                "Launcher",
                2,
                InventoryItemKind.Weapon,
                cost,
                new EquipmentEffectSet(0.75f),
                CreateAttack("attack.launcher"));
            var actions = new[]
            {
                new DisplacementActionDefinition(
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
                    DisplacementResultPolicies.Topple),
            };
            var displace = new DisplacementAbilityDefinition(
                "ability.displace",
                "Displace",
                hotbarSlot: 4,
                actions: actions);
            var actor = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f),
                new[] { rifle, launcher },
                "rifle",
                displacementAbility: displace);
            return new GameplaySession(new ScenarioDefinition(
                "equipment-controller-test",
                new ScenarioTimingDefinition(1f),
                new[] { actor },
                new ScenarioObjectiveDefinition[0]));
        }

        private static GameplaySession CreatePartySession()
        {
            var cost = new ActionCost(1, 0f, ActionMobility.Set);
            var rifle = new InventoryItemDefinition(
                "rifle",
                "Rifle",
                1,
                InventoryItemKind.Weapon,
                cost,
                EquipmentEffectSet.None,
                CreateAttack("attack.rifle"));
            var sidearm = new InventoryItemDefinition(
                "sidearm",
                "Sidearm",
                2,
                InventoryItemKind.Weapon,
                cost,
                EquipmentEffectSet.None,
                CreateAttack("attack.sidearm"));
            var player = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f),
                new[] { rifle },
                "rifle");
            var scout = new ScenarioActorDefinition(
                "scout",
                11,
                new GameplayActorPose(new GameplayPosition(2f, 0f, 0f), 0f),
                new TurnBudget(4, 8f),
                new[] { sidearm },
                "sidearm");
            return new GameplaySession(new ScenarioDefinition(
                "party-hotbar-test",
                new ScenarioTimingDefinition(1f),
                new[] { player, scout },
                Array.Empty<ScenarioObjectiveDefinition>()));
        }

        private static GameplayActorAbilityHotbarDefinition[]
            CreateActorAbilityHotbarDefinitions() =>
                new[]
                {
                    new GameplayActorAbilityHotbarDefinition(
                        "ability.displace",
                        "Displace",
                        authoredSlot: 4,
                        options: new[]
                        {
                            new GameplayActorAbilityOptionDefinition(
                                "close-quarters.push",
                                "Push"),
                        }),
                };

        private static AttackDefinition CreateAttack(string id) =>
            new AttackDefinition(
                id,
                id,
                new ActionCost(1, 0f, ActionMobility.Set),
                2f,
                accuracyDecay: AccuracyDecayDefinition.None);

        private sealed class StaticWarningSource : IGameplayWarningHintSource
        {
            public StaticWarningSource(GameplayWarningHintModel warningHint)
            {
                CurrentWarningHint = warningHint;
            }

            public GameplayWarningHintModel CurrentWarningHint { get; }
        }
    }
}
