using System.Linq;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayHudModelTests
    {
        [Test]
        public void ModelOwnsAuthoredLabelsBindingsAndCommandAvailability()
        {
            GameplaySession session = CreateSession();
            ScenarioObjectiveRuntimeDefinition objective =
                CreateObjectiveContent();

            GameplayHudModel exploration = GameplayHudModelBuilder.Build(
                session,
                "player",
                "Depot Yard",
                objective,
                interactionAvailable: true,
                default,
                string.Empty,
                turnModeExitAvailable: false);

            Assert.That(exploration.ScenarioDisplayName, Is.EqualTo("Depot Yard"));
            Assert.That(exploration.ModeLabel, Is.EqualTo("EXPLORATION MODE"));
            Assert.That(exploration.ObjectiveSummary,
                Is.EqualTo("OBJECTIVE - REACH THE DECK"));
            Assert.That(exploration.CommandBar.HotbarSlots,
                Has.Count.EqualTo(GameplayCommandBarModel.HotbarSlotCount));
            Assert.That(
                exploration.CommandBar.FindCommand(
                    GameplayControl.ToggleTurnMode).Label,
                Is.EqualTo("ENTER TURN MODE"));
            Assert.That(exploration.CommandBar.Resources, Is.Null);
            Assert.That(exploration.CommandBar.BodyStatus.Regions,
                Has.Count.EqualTo(6));
            Assert.That(exploration.CommandBar.BodyStatus.TotalWounds,
                Is.Zero);
            Assert.That(exploration.CommandBar.Hints.Any(
                hint => hint.Control == GameplayControl.AimLook), Is.True);

            Assert.That(session.EnterTurnMode(), Is.True);
            var resolver = new GameplayActionResolver(session);
            Assert.That(resolver.TryResolveInteraction(
                "player",
                "objective",
                out _,
                out _), Is.True);
            GameplayHudModel turn = GameplayHudModelBuilder.Build(
                session,
                "player",
                "Depot Yard",
                objective,
                interactionAvailable: false,
                new GameplayRouteCommandBarState(
                    2,
                    1.5f,
                    false,
                    0f,
                    string.Empty),
                string.Empty,
                turnModeExitAvailable: true);

            Assert.That(turn.ModeLabel, Is.EqualTo("TURN MODE"));
            Assert.That(turn.ObjectiveSummary,
                Is.EqualTo("OBJECTIVE - DECK SECURED"));
            Assert.That(turn.CommandBar.Resources.ActionPoints, Is.EqualTo(3));
            Assert.That(turn.CommandBar.Resources.MaximumActionPoints, Is.EqualTo(4));
            Assert.That(turn.CommandBar.Status, Is.EqualTo("ROUTE - 1.5"));
            Assert.That(
                turn.CommandBar.FindCommand(GameplayControl.EndTurn).Enabled,
                Is.True);
            Assert.That(turn.CommandBar.Hints.Any(
                hint => hint.Control == GameplayControl.Attack), Is.True);
            Assert.That(
                turn.CommandBar.Hints.Single(
                    hint => hint.Control == GameplayControl.UndoRoute).Label,
                Is.EqualTo("RETRACT"));
            Assert.That(
                turn.CommandBar.Hints.Single(
                    hint => hint.Control == GameplayControl.CancelRoute).Label,
                Is.EqualTo("CLEAR ROUTE"));
            Assert.That(
                turn.CommandBar.Hints.Single(
                    hint => hint.Control == GameplayControl.ConfirmRoute).Label,
                Is.EqualTo("MOVE"));
        }

        [Test]
        public void BodyStatusProjectsThePlayersRegionalWounds()
        {
            var player = new ScenarioActorDefinition(
                "player",
                initiative: 10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f),
                combat: new ActorCombatDefinition(
                    "player",
                    new[] { "enemy" },
                    maximumWounds: 3));
            var enemy = new ScenarioActorDefinition(
                "enemy",
                initiative: 20,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 5f),
                    180f),
                new TurnBudget(4, 8f),
                new AttackDefinition(
                    "attack.enemy",
                    "Enemy shot",
                    new ActionCost(1, 0f, ActionMobility.Set),
                    woundMovementPenalty: 2f,
                    accuracyDecay: AccuracyDecayDefinition.None),
                combat: new ActorCombatDefinition(
                    "enemy",
                    new[] { "player" },
                    maximumWounds: 2));
            var session = new GameplaySession(new ScenarioDefinition(
                "regional-wound-hud-test",
                new ScenarioTimingDefinition(1f),
                new[] { player, enemy },
                new ScenarioObjectiveDefinition[0]));
            Assert.That(session.BeginEncounter(), Is.True);
            var attacks = new GameplayAttackSession(session, 3u);
            Assert.That(attacks.TryResolve(
                "enemy",
                new TargetExposureSnapshot(
                    "enemy",
                    "player",
                    new[]
                    {
                        new TargetRegionExposure(
                            TargetRegionId.LeftArm,
                            visibleSampleCount: 5,
                            totalSampleCount: 5),
                    }),
                out _,
                out _), Is.True);

            GameplayHudModel model = GameplayHudModelBuilder.Build(
                session,
                "player",
                "Depot Yard",
                primaryObjective: null,
                interactionAvailable: false,
                default,
                string.Empty,
                turnModeExitAvailable: false);

            Assert.That(model.CommandBar.BodyStatus.TotalWounds,
                Is.EqualTo(1));
            Assert.That(model.CommandBar.BodyStatus.MaximumWounds,
                Is.EqualTo(3));
            Assert.That(model.CommandBar.BodyStatus
                .FindRegion(TargetRegionId.LeftArm).WoundCount,
                Is.EqualTo(1));
            Assert.That(model.CommandBar.BodyStatus
                .FindRegion(TargetRegionId.RightArm).WoundCount,
                Is.Zero);
            Assert.That(model.CommandBar.BodyStatus.MovementPenalty,
                Is.EqualTo(2f));
        }

        [Test]
        public void WorldTurnDisablesCommandsAndOverridesPresentationStatus()
        {
            GameplaySession session = CreateSession();
            session.EnterTurnMode();
            session.TryEndTurn("player", out _);

            GameplayHudModel model = GameplayHudModelBuilder.Build(
                session,
                "player",
                "Depot Yard",
                CreateObjectiveContent(),
                interactionAvailable: false,
                new GameplayRouteCommandBarState(
                    3,
                    2f,
                    false,
                    0f,
                    "Route ready."),
                "Interaction ready.",
                turnModeExitAvailable: false);

            Assert.That(model.CommandBar.Status,
                Is.EqualTo("WORLD TURN - RESOLVING"));
            Assert.That(
                model.CommandBar.FindCommand(GameplayControl.EndTurn).Enabled,
                Is.False);
            Assert.That(
                model.CommandBar.FindCommand(
                    GameplayControl.ToggleTurnMode).Enabled,
                Is.False);
        }

        [Test]
        public void TurnModeExitAvailabilityComesFromTheOwningController()
        {
            GameplaySession session = CreateSession();
            Assert.That(session.BeginEncounter(), Is.True);

            GameplayHudModel available = GameplayHudModelBuilder.Build(
                session,
                "player",
                "Depot Yard",
                CreateObjectiveContent(),
                interactionAvailable: false,
                default,
                string.Empty,
                turnModeExitAvailable: true);
            GameplayHudModel blocked = GameplayHudModelBuilder.Build(
                session,
                "player",
                "Depot Yard",
                CreateObjectiveContent(),
                interactionAvailable: false,
                default,
                string.Empty,
                turnModeExitAvailable: false);

            Assert.That(
                available.CommandBar.FindCommand(
                    GameplayControl.ToggleTurnMode).Enabled,
                Is.True);
            Assert.That(
                blocked.CommandBar.FindCommand(
                    GameplayControl.ToggleTurnMode).Enabled,
                Is.False);
        }

        [Test]
        public void VoluntaryExitDisablesTurnEntryUntilMinimumTurnElapses()
        {
            GameplaySession session = CreateSession();
            session.EnterTurnMode();
            session.TryExitTurnMode(out _);

            GameplayHudModel locked = GameplayHudModelBuilder.Build(
                session,
                "player",
                "Depot Yard",
                CreateObjectiveContent(),
                interactionAvailable: false,
                default,
                string.Empty,
                turnModeExitAvailable: false);

            Assert.That(
                locked.CommandBar.FindCommand(
                    GameplayControl.ToggleTurnMode).Enabled,
                Is.False);
            Assert.That(locked.CommandBar.Status,
                Is.EqualTo("WORLD TURN - TURN MODE IN 1.3S"));

            session.AdvanceContinuousTime(
                session.Scenario.Timing.MinimumVoluntaryTurnSeconds);
            GameplayHudModel ready = GameplayHudModelBuilder.Build(
                session,
                "player",
                "Depot Yard",
                CreateObjectiveContent(),
                interactionAvailable: false,
                default,
                string.Empty,
                turnModeExitAvailable: false);

            Assert.That(
                ready.CommandBar.FindCommand(
                    GameplayControl.ToggleTurnMode).Enabled,
                Is.True);
            Assert.That(ready.CommandBar.Status, Is.Empty);
        }

        [Test]
        public void HotbarProjectsEquipmentStateCostsEffectsAndConfirmation()
        {
            GameplaySession session = CreateEquipmentSession();
            session.EnterTurnMode();

            GameplayHudModel model = GameplayHudModelBuilder.Build(
                session,
                "player",
                "Depot Yard",
                CreateObjectiveContent(),
                interactionAvailable: false,
                default,
                string.Empty,
                turnModeExitAvailable: true,
                pendingEquipmentItemId: "launcher");

            GameplayHotbarSlotModel rifle = model.CommandBar.HotbarSlots[0];
            GameplayHotbarSlotModel launcher = model.CommandBar.HotbarSlots[1];
            Assert.That(rifle.Label, Is.EqualTo("RIFLE"));
            Assert.That(rifle.IsEquipped, Is.True);
            Assert.That(rifle.Enabled, Is.True);
            Assert.That(rifle.PrimaryClickRequestsPower, Is.False);
            Assert.That(rifle.EquipmentLabel, Is.EqualTo("v"));
            Assert.That(rifle.PowerTooltip, Does.Contain("1 AP"));
            Assert.That(rifle.PowerTooltip, Does.Contain("90%"));
            Assert.That(launcher.IsEquipped, Is.False);
            Assert.That(launcher.Enabled, Is.False);
            Assert.That(launcher.EquipmentEnabled, Is.True);
            Assert.That(launcher.EquipmentLabel, Is.EqualTo("^"));
            Assert.That(launcher.AwaitingConfirmation, Is.True);
            Assert.That(launcher.EquipmentTooltip,
                Does.Contain("UNEQUIP RIFLE + EQUIP LAUNCHER"));
            Assert.That(launcher.EquipmentTooltip, Does.Contain("2 AP"));
            Assert.That(launcher.EquipmentTooltip, Does.Contain("75%"));
            Assert.That(launcher.EquipmentTooltip, Does.Contain("100%"));
        }

        [Test]
        public void HotbarProjectsResolvedSwitchCostAndRequirement()
        {
            GameplaySession session = CreateEquipmentSession(
                actionPoints: 1);
            session.EnterTurnMode();

            GameplayHudModel model = GameplayHudModelBuilder.Build(
                session,
                "player",
                "Depot Yard",
                CreateObjectiveContent(),
                interactionAvailable: false,
                default,
                string.Empty,
                turnModeExitAvailable: true);

            GameplayHotbarSlotModel launcher =
                model.CommandBar.HotbarSlots[1];
            Assert.That(launcher.EquipmentEnabled, Is.False);
            Assert.That(launcher.EquipmentTooltip,
                Does.Contain("COST - 2 AP"));
            Assert.That(launcher.EquipmentTooltip,
                Does.Contain("REQUIRES - INSUFFICIENT AP"));
        }

        [Test]
        public void ExplorationHotbarAllowsEquippedWeaponAndFreeEquipmentChanges()
        {
            GameplaySession session = CreateEquipmentSession();

            GameplayHudModel model = GameplayHudModelBuilder.Build(
                session,
                "player",
                "Depot Yard",
                CreateObjectiveContent(),
                interactionAvailable: false,
                default,
                string.Empty,
                turnModeExitAvailable: false);

            GameplayHotbarSlotModel rifle = model.CommandBar.HotbarSlots[0];
            GameplayHotbarSlotModel launcher = model.CommandBar.HotbarSlots[1];
            Assert.That(rifle.Enabled, Is.True);
            Assert.That(rifle.PowerTooltip,
                Does.Contain("FREE OUT OF TURN MODE"));
            Assert.That(rifle.EquipmentEnabled, Is.True);
            Assert.That(launcher.Enabled, Is.False);
            Assert.That(launcher.EquipmentEnabled, Is.True);
            Assert.That(launcher.EquipmentTooltip,
                Does.Contain("FREE OUT OF TURN MODE"));
        }

        [Test]
        public void ArmedWeaponProjectsAsPendingAndMakesItsTileCancelable()
        {
            GameplaySession session = CreateEquipmentSession();

            GameplayHudModel model = GameplayHudModelBuilder.Build(
                session,
                "player",
                "Depot Yard",
                CreateObjectiveContent(),
                interactionAvailable: false,
                default,
                string.Empty,
                turnModeExitAvailable: false,
                pendingWeaponItemId: "rifle");

            GameplayHotbarSlotModel rifle = model.CommandBar.HotbarSlots[0];
            GameplayHotbarSlotModel launcher = model.CommandBar.HotbarSlots[1];
            Assert.That(rifle.IsPowerPending, Is.True);
            Assert.That(rifle.PrimaryClickRequestsPower, Is.True);
            Assert.That(launcher.IsPowerPending, Is.False);
            Assert.That(launcher.PrimaryClickRequestsPower, Is.False);
        }

        [Test]
        public void ExplorationImmediateWeaponRemainsAvailableDuringReentryLock()
        {
            GameplaySession session = CreateEquipmentSession();
            session.EnterTurnMode();
            session.TryExitTurnMode(out _);

            GameplayHudModel locked = GameplayHudModelBuilder.Build(
                session,
                "player",
                "Depot Yard",
                CreateObjectiveContent(),
                interactionAvailable: false,
                default,
                string.Empty,
                turnModeExitAvailable: false);
            Assert.That(locked.CommandBar.HotbarSlots[0].Enabled, Is.True);

            session.AdvanceContinuousTime(
                session.Scenario.Timing.MinimumVoluntaryTurnSeconds);
            GameplayHudModel ready = GameplayHudModelBuilder.Build(
                session,
                "player",
                "Depot Yard",
                CreateObjectiveContent(),
                interactionAvailable: false,
                default,
                string.Empty,
                turnModeExitAvailable: false);
            Assert.That(ready.CommandBar.HotbarSlots[0].Enabled, Is.True);
        }

        [Test]
        public void ThrownExplosivePowerIsAvailableWithoutBeingEquipped()
        {
            GameplaySession session = CreateConsumableSession();

            GameplayHudModel exploration = GameplayHudModelBuilder.Build(
                session,
                "player",
                "Depot Yard",
                CreateObjectiveContent(),
                interactionAvailable: false,
                default,
                string.Empty,
                turnModeExitAvailable: false);

            GameplayHotbarSlotModel grenade =
                exploration.CommandBar.HotbarSlots[2];
            Assert.That(grenade.Label, Is.EqualTo("FRAG GRENADE  x3"));
            Assert.That(grenade.Enabled, Is.True);
            Assert.That(grenade.PrimaryClickRequestsPower, Is.True);
            Assert.That(grenade.IsEquipped, Is.False);
            Assert.That(grenade.EquipmentEnabled, Is.False);
            Assert.That(grenade.PowerTooltip, Does.Contain("POWER - THROW"));
            Assert.That(grenade.PowerTooltip, Does.Contain("QUANTITY - 3"));
            Assert.That(grenade.PowerTooltip, Does.Contain("2 AP"));
            Assert.That(grenade.PowerTooltip,
                Does.Contain("IF COMBAT STARTS"));
            Assert.That(grenade.PowerTooltip, Does.Contain("RANGE - 12 M"));
            Assert.That(grenade.PowerTooltip, Does.Contain("BLAST - 5 M"));

            GameplayHudModel aiming = GameplayHudModelBuilder.Build(
                session,
                "player",
                "Depot Yard",
                CreateObjectiveContent(),
                interactionAvailable: false,
                default,
                string.Empty,
                turnModeExitAvailable: false,
                pendingConsumableItemId: "item.frag-grenade");
            Assert.That(
                aiming.CommandBar.HotbarSlots[2].IsPowerPending,
                Is.True);
            Assert.That(
                aiming.CommandBar.HotbarSlots[0].IsPowerPending,
                Is.False);

            Assert.That(session.EnterTurnMode(), Is.True);
            GameplayHudModel turn = GameplayHudModelBuilder.Build(
                session,
                "player",
                "Depot Yard",
                CreateObjectiveContent(),
                interactionAvailable: false,
                default,
                string.Empty,
                turnModeExitAvailable: true);

            Assert.That(turn.CommandBar.HotbarSlots[2].Enabled, Is.True);
        }

        [Test]
        public void ContactWeaponTooltipProjectsAuthoredReachAndTargetRule()
        {
            GameplaySession session = CreateContactWeaponSession();

            GameplayHudModel model = GameplayHudModelBuilder.Build(
                session,
                "player",
                "Depot Yard",
                CreateObjectiveContent(),
                interactionAvailable: false,
                default,
                string.Empty,
                turnModeExitAvailable: false);

            GameplayHotbarSlotModel knife = model.CommandBar.HotbarSlots[4];
            Assert.That(knife.Enabled, Is.True);
            Assert.That(knife.IsEquipped, Is.True);
            Assert.That(knife.PowerTooltip, Does.Contain("KNIFE STRIKE"));
            Assert.That(knife.PowerTooltip, Does.Contain("REACH - 2 M"));
            Assert.That(knife.PowerTooltip,
                Does.Contain("TARGET - ACTOR ONLY"));
        }

        [Test]
        public void HotbarProjectsRuntimeReassignmentsInsteadOfAuthoredSlots()
        {
            GameplaySession session = CreateEquipmentSession();
            var bindings = new Dictionary<int, GameplayHotbarBinding>
            {
                {
                    8,
                    new GameplayHotbarBinding(
                        GameplayHotbarBindingKind.InventoryItem,
                        "launcher")
                },
            };

            GameplayHudModel model = GameplayHudModelBuilder.Build(
                session,
                "player",
                "Depot Yard",
                CreateObjectiveContent(),
                interactionAvailable: false,
                default,
                string.Empty,
                turnModeExitAvailable: false,
                hotbarBindings: bindings);

            Assert.That(model.CommandBar.HotbarSlots[1].ContentId, Is.Empty);
            Assert.That(model.CommandBar.HotbarSlots[7].ContentId,
                Is.EqualTo("launcher"));
        }

        [Test]
        public void HotbarProjectsActorAbilityAvailabilityAndTargetingState()
        {
            GameplaySession session = CreateActorAbilitySession(
                out DisplacementActionDefinition push);
            Assert.That(session.EnterTurnMode(), Is.True);
            var bindings = new Dictionary<int, GameplayHotbarBinding>
            {
                {
                    4,
                    new GameplayHotbarBinding(
                        GameplayHotbarBindingKind.ActorAbility,
                        "ability.displace")
                },
            };
            var pushOption = new GameplayActorAbilityOptionDefinition(
                push.Id,
                push.DisplayName);
            var abilities = new Dictionary<
                string,
                GameplayActorAbilityHotbarState>
            {
                {
                    "ability.displace",
                    new GameplayActorAbilityHotbarState(
                        new GameplayActorAbilityHotbarDefinition(
                            "ability.displace",
                            "Displace",
                            authoredSlot: 4,
                            new[] { pushOption }),
                        enabled: true,
                        pending: true,
                        tooltip: "DISPLACE\nSELECT AN INTENT",
                        options: new[]
                        {
                            new GameplayActorAbilityOptionHotbarState(
                                pushOption,
                                enabled: true,
                                pending: true,
                                tooltip: "PUSH\nCOST - 1 AP",
                                selectionLabel: "PUSH  -  1 AP"),
                        })
                },
            };

            GameplayHudModel model = GameplayHudModelBuilder.Build(
                session,
                "player",
                "Depot Yard",
                CreateObjectiveContent(),
                interactionAvailable: false,
                default,
                string.Empty,
                turnModeExitAvailable: true,
                hotbarBindings: bindings,
                actorAbilities: abilities);

            GameplayHotbarSlotModel slot =
                model.CommandBar.HotbarSlots[3];
            Assert.That(slot.BindingKind,
                Is.EqualTo(GameplayHotbarBindingKind.ActorAbility));
            Assert.That(slot.ContentId, Is.EqualTo("ability.displace"));
            Assert.That(slot.Label, Is.EqualTo("DISPLACE"));
            Assert.That(slot.Enabled, Is.True);
            Assert.That(slot.PrimaryClickRequestsPower, Is.True);
            Assert.That(slot.IsPowerPending, Is.True);
            Assert.That(slot.EquipmentLabel, Is.Empty);
            Assert.That(slot.PowerTooltip, Does.Contain("SELECT AN INTENT"));
            Assert.That(slot.AbilityOptions, Has.Count.EqualTo(1));
            Assert.That(slot.AbilityOptions[0].Id, Is.EqualTo(push.Id));
            Assert.That(slot.AbilityOptions[0].Label,
                Is.EqualTo("PUSH  -  1 AP"));
            Assert.That(slot.AbilityOptions[0].Pending, Is.True);
            Assert.That(slot.AbilityOptions[0].Tooltip,
                Does.Contain("COST - 1 AP"));
        }

        private static GameplaySession CreateSession()
        {
            var actor = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f));
            var objective = new ScenarioObjectiveDefinition(
                "objective",
                new GameplayPosition(0f, 0f, 0f),
                1f,
                new GameplayInteractionDefinition(
                    "objective.use",
                    "Use objective",
                    new ActionCost(1, 1f, ActionMobility.Set)));
            return new GameplaySession(new ScenarioDefinition(
                "hud-model-test",
                new ScenarioTimingDefinition(1.25f),
                new[] { actor },
                new[] { objective }));
        }

        private static GameplaySession CreateActorAbilitySession(
            out DisplacementActionDefinition push)
        {
            push = new DisplacementActionDefinition(
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
            var actor = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f),
                inventory: new InventoryItemDefinition[0],
                initiallyEquippedItemId: null,
                displacementAbility: new DisplacementAbilityDefinition(
                    "ability.displace",
                    "Displace",
                    hotbarSlot: 4,
                    new[] { push }));
            return new GameplaySession(new ScenarioDefinition(
                "actor-ability-hud-test",
                new ScenarioTimingDefinition(1f),
                new[] { actor },
                new ScenarioObjectiveDefinition[0]));
        }

        private static GameplaySession CreateEquipmentSession(
            int actionPoints = 4)
        {
            var equipmentCost = new ActionCost(1, 0f, ActionMobility.Set);
            var rifle = new InventoryItemDefinition(
                "rifle",
                "Rifle",
                1,
                InventoryItemKind.Weapon,
                equipmentCost,
                new EquipmentEffectSet(0.9f),
                new AttackDefinition(
                    "attack.rifle",
                    "Fire rifle",
                    new ActionCost(1, 0f, ActionMobility.Set),
                    2f,
                    accuracyDecay: AccuracyDecayDefinition.None));
            var launcher = new InventoryItemDefinition(
                "launcher",
                "Launcher",
                2,
                InventoryItemKind.Weapon,
                equipmentCost,
                new EquipmentEffectSet(0.75f),
                new AttackDefinition(
                    "attack.rocket",
                    "Launch rocket",
                    new ActionCost(2, 0f, ActionMobility.Set),
                    2f,
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
                "equipment-hud-test",
                new ScenarioTimingDefinition(1f),
                new[] { player },
                new ScenarioObjectiveDefinition[0]));
        }

        private static GameplaySession CreateConsumableSession()
        {
            var grenade = new InventoryItemDefinition(
                "item.frag-grenade",
                "Frag Grenade",
                3,
                InventoryItemKind.Consumable,
                new ActionCost(0, 0f, ActionMobility.Mobile),
                EquipmentEffectSet.None,
                consumablePower: new ThrownExplosiveDefinition(
                    "item.frag-grenade",
                    new ActionCost(2, 0f, ActionMobility.Mobile),
                    12f,
                    1.2f,
                    0.82f,
                    0.75f,
                    0.12f,
                    5f,
                    2f),
                initialQuantity: 3);
            var player = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f),
                new[] { grenade },
                initiallyEquippedItemId: null);
            return new GameplaySession(new ScenarioDefinition(
                "consumable-hud-test",
                new ScenarioTimingDefinition(1f),
                new[] { player },
                new ScenarioObjectiveDefinition[0]));
        }

        private static GameplaySession CreateContactWeaponSession()
        {
            var knife = new InventoryItemDefinition(
                "weapon.combat-knife",
                "Combat Knife",
                5,
                InventoryItemKind.Weapon,
                new ActionCost(1, 0f, ActionMobility.Mobile),
                EquipmentEffectSet.None,
                new AttackDefinition(
                    "attack.combat-knife",
                    "Knife strike",
                    new ActionCost(1, 0f, ActionMobility.Mobile),
                    2f,
                    contact: new ContactAttackDefinition(2f)));
            var player = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f),
                new[] { knife },
                "weapon.combat-knife");
            return new GameplaySession(new ScenarioDefinition(
                "contact-weapon-hud-test",
                new ScenarioTimingDefinition(1f),
                new[] { player },
                new ScenarioObjectiveDefinition[0]));
        }

        private static ScenarioObjectiveRuntimeDefinition
            CreateObjectiveContent() =>
            new ScenarioObjectiveRuntimeDefinition(
                "objective",
                "REACH THE DECK",
                "DECK SECURED");
    }
}
