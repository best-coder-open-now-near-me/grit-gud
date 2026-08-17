using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayBugReportTests
    {
        [Test]
        public void PlayerNoteIsPrependedToExportedDiagnostics()
        {
            string report = GameplayBugReportExporter.PrependPlayerNote(
                "GRIT GUD BUG REPORT",
                "Rocket remained stuck after impact.");

            StringAssert.StartsWith("PLAYER NOTE", report);
            Assert.That(report.IndexOf("Rocket remained stuck", StringComparison.Ordinal),
                Is.LessThan(report.IndexOf("GRIT GUD BUG REPORT", StringComparison.Ordinal)));
        }
        [Test]
        public void FormatterCapturesGuidanceAuthoritativeStateAndRuntime()
        {
            GameplaySession session = CreateSession();
            session.EnterTurnMode();
            session.SpendMovement("player", 2.5f);
            session.TryExitTurnMode(out TurnModeExitFailure failure);
            Assert.That(failure, Is.EqualTo(TurnModeExitFailure.None));

            var guidance = new GameplayGuidanceEntry(
                "turn.voluntary.entry",
                "Voluntary tactical interval",
                "Entering starts with full AP.",
                "The environment advances when the interval ends.",
                "Press T when you want to plan.");
            var route = new GameplayBugReportRouteState(
                true,
                3,
                1.25f,
                false,
                0f,
                RoutePlanFailure.None,
                "Route ready.");
            var runtime = new GameplayBugReportRuntime(
                new DateTime(2026, 8, 9, 14, 5, 6, 7, DateTimeKind.Utc),
                "Grit Gud",
                "0.1.0",
                "6000.4.10f1",
                "WebGLPlayer",
                "Browser",
                "WebGL Device",
                "WebGL Graphics",
                1280,
                720);

            string report = GameplayBugReportFormatter.Format(
                session,
                guidance,
                route,
                runtime);

            Assert.That(report, Does.Contain("Generated UTC: 2026-08-09T14:05:06.007Z"));
            Assert.That(report, Does.Contain("Guidance ID: turn.voluntary.entry"));
            Assert.That(report, Does.Contain("Expected: Entering starts with full AP."));
            Assert.That(report, Does.Contain("Scenario: bug-report-test"));
            Assert.That(report, Does.Contain("Mode: Exploration"));
            Assert.That(report, Does.Contain("Initiative: player -> target"));
            Assert.That(report, Does.Contain(
                "player | position=(1.25, 0, -2.5) | facing=90 | stance=Standing | AP=4 | move=8"));
            Assert.That(report, Does.Contain("Last voluntary cycle: 1"));
            Assert.That(report, Does.Contain("Last ended turn: <none>"));
            Assert.That(report, Does.Contain("AP=4 | move=5.5"));
            Assert.That(report, Does.Contain("Provisional points: 3"));
            Assert.That(report, Does.Contain("Provisional cost: 1.25"));
            Assert.That(report, Does.Contain("Platform: WebGLPlayer"));
            Assert.That(report, Does.Contain("Screen: 1280x720"));
        }

        [Test]
        public void FormatterIsDeterministicForTheSameSnapshot()
        {
            GameplaySession session = CreateSession();
            var guidance = new GameplayGuidanceEntry(
                "test.guidance",
                "Test",
                "Expected",
                "Why",
                "Tip");
            var runtime = new GameplayBugReportRuntime(
                DateTime.UnixEpoch,
                "Game",
                "Version",
                "Unity",
                "Platform",
                "OS",
                "Device",
                "Graphics",
                800,
                600);
            var route = new GameplayBugReportRouteState(
                false,
                0,
                0f,
                false,
                0f,
                RoutePlanFailure.None,
                string.Empty);

            string first = GameplayBugReportFormatter.Format(
                session,
                guidance,
                route,
                runtime);
            string second = GameplayBugReportFormatter.Format(
                session,
                guidance,
                route,
                runtime);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Does.Contain("Pending authoritative route: <none>"));
            Assert.That(first, Does.Contain("Last voluntary cycle: <none>"));
            Assert.That(first, Does.Contain("Gameplay journal:"));
        }

        [Test]
        public void FormatterCapturesPartyControlWithoutProgressionState()
        {
            GameplaySession session = CreatePartySession();
            using var control = new GameplayPartyControlSession(session);

            Assert.That(control.TrySelectActor("vale", out _), Is.True);
            string report = GameplayBugReportFormatter.Format(
                session,
                new GameplayGuidanceEntry(
                    "party.test",
                    "Party test",
                    "Party state is authoritative.",
                    "Diagnostics identify the selected character.",
                    "Select a party member."),
                default(GameplayBugReportRouteState),
                CreateRuntime(),
                control.Snapshot);

            Assert.That(report, Does.Contain("Player party: mara, vale"));
            Assert.That(report, Does.Contain("Selected party actor: vale"));
            Assert.That(report, Does.Contain("Command party actor: vale"));
            Assert.That(report, Does.Contain("Party defeated: no"));
            Assert.That(report, Does.Not.Contain("progression"));
            Assert.That(report, Does.Not.Contain("unspent"));
        }

        [Test]
        public void FormatterCapturesResolvedActionEffects()
        {
            GameplaySession session = CreateSession();
            session.EnterTurnMode();
            var resolver = new GameplayActionResolver(session);
            Assert.That(resolver.TryResolveInteraction(
                "player",
                "raised-deck",
                out _,
                out _), Is.True);
            var guidance = new GameplayGuidanceEntry(
                "action.interact.ready",
                "Interaction",
                "The target owns its interaction.",
                "Animation only presents it.",
                "Press E to interact.");
            var runtime = new GameplayBugReportRuntime(
                DateTime.UnixEpoch,
                "Game",
                "Version",
                "Unity",
                "Platform",
                "OS",
                "Device",
                "Graphics",
                800,
                600);

            string report = GameplayBugReportFormatter.Format(
                session,
                guidance,
                default(GameplayBugReportRouteState),
                runtime);

            Assert.That(report, Does.Contain(
                "#2 | ActionResolved | action-sequence=1 | actor=player | action=secure-raised-deck | target=raised-deck | AP=1 | move=1 | mobility=Set"));
            Assert.That(report, Does.Contain(
                "ObjectiveCompleted | objective=raised-deck | incomplete -> complete"));
        }

        [Test]
        public void FormatterCapturesConsumableStateAndQuantityTransition()
        {
            GameplaySession session = CreateConsumableSession(
                out ThrownExplosiveDefinition definition);
            GameplayActorSnapshot player = session.GetActor("player");
            var record = new ThrownExplosiveRecord(
                1,
                "player",
                definition,
                player.Pose.Position,
                definition.GetLaunchOrigin(player.Pose),
                new GameplayPosition(4f, 0f, 0f),
                new GameplayPosition(4f, 0f, 0f),
                new GameplayPosition(4f, 0f, 0f),
                uncertaintyRadius: 0.9f,
                worldStateRevision: 9,
                Array.Empty<BlastEffectRecord>());
            var action = new GameplayActionRecord(
                1,
                new GameplayActionRequest(
                    "player",
                    definition.Id,
                    definition.Id),
                new ActionCost(0, 0f, ActionMobility.Mobile),
                player.TurnBudget,
                player.TurnBudget,
                new GameplayActionOutcome[]
                {
                    new ThrownExplosiveActionOutcome(record),
                    new InventoryQuantityChangedActionOutcome(
                        new InventoryQuantityChangeRecord(
                            "player",
                            definition.Id,
                            previousQuantity: 2,
                            consumedQuantity: 1,
                            resultingQuantity: 1)),
                });
            session.CommitAction(action);

            string report = GameplayBugReportFormatter.Format(
                session,
                new GameplayGuidanceEntry(
                    "throw.ready",
                    "Throw",
                    "A throw consumes one item.",
                    "Inventory is authoritative.",
                    "Choose a landing point."),
                default(GameplayBugReportRouteState),
                CreateRuntime());

            Assert.That(report, Does.Contain(
                "player | position=(0, 0, 0) | facing=90 | stance=Standing "
                + "| AP=4 | move=8"));
            Assert.That(report, Does.Contain(
                "inventory=item.frag:1"));
            Assert.That(report, Does.Contain(
                "ThrownExplosive | throw-sequence=1 | item=item.frag"));
            Assert.That(report, Does.Contain(
                "InventoryConsumed | item=item.frag | quantity=2 - 1 = 1"));
        }

        [Test]
        public void FormatterCapturesEmergencyReactionEvidence()
        {
            GameplaySession session = CreateSession();
            session.BeginEncounter();
            var cycle = new GameplayEmergencyCycleSession(session);
            Assert.That(cycle.TryOpen(
                "projectile",
                "projectile.one",
                "player",
                actionPointAllowance: 3,
                new PendingEmergencyResolution()), Is.True);

            string report = GameplayBugReportFormatter.Format(
                session,
                new GameplayGuidanceEntry(
                    "emergency.reaction",
                    "Emergency reaction",
                    "Responders receive the authored allowance.",
                    "The reaction interrupts ordinary initiative.",
                    "Respond or end the reaction."),
                default(GameplayBugReportRouteState),
                new GameplayBugReportRuntime(
                    DateTime.UnixEpoch,
                    "Game",
                    "Version",
                    "Unity",
                    "Platform",
                    "OS",
                    "Device",
                    "Graphics",
                    800,
                    600));

            Assert.That(report, Does.Contain(
                "EmergencyReactionChanged | window-sequence=1 "
                + "| trigger=projectile:projectile.one | initiator=player "
                + "| responders=target | AP=3 | status=Pending"));
        }

        private static GameplaySession CreateSession()
        {
            var player = new ScenarioActorDefinition(
                "player",
                initiative: 20,
                new GameplayActorPose(
                    new GameplayPosition(1.25f, 0f, -2.5f),
                    90f),
                new TurnBudget(4, 8f));
            var target = new ScenarioActorDefinition(
                "target",
                initiative: 10,
                new GameplayActorPose(
                    new GameplayPosition(5f, 0f, 0f),
                    180f),
                new TurnBudget(2, 4f));
            var objective = new ScenarioObjectiveDefinition(
                "raised-deck",
                new GameplayPosition(1.25f, 0f, -2.5f),
                interactionRadius: 1.5f,
                new GameplayInteractionDefinition(
                    "secure-raised-deck",
                    "Secure raised deck",
                    new ActionCost(1, 1f, ActionMobility.Set)));
            return new GameplaySession(new ScenarioDefinition(
                "bug-report-test",
                new ScenarioTimingDefinition(1.25f),
                new[] { player, target },
                new[] { objective }));
        }

        private static GameplaySession CreatePartySession()
        {
            CharacterProfileDefinition CreateProfile(string id, string name) =>
                new CharacterProfileDefinition(
                    id,
                    name,
                    "Test Operative",
                    new[]
                    {
                        new CharacterRating(CoreAttributeIds.Strength, 3),
                        new CharacterRating(CoreAttributeIds.Dexterity, 3),
                        new CharacterRating(CoreAttributeIds.Grit, 3),
                        new CharacterRating(CoreAttributeIds.Charisma, 3),
                    },
                    Array.Empty<CharacterRating>(),
                    Array.Empty<string>());
            var mara = new ScenarioActorDefinition(
                "mara",
                10,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f),
                characterProfile: CreateProfile("character.mara", "Mara"));
            var vale = new ScenarioActorDefinition(
                "vale",
                9,
                new GameplayActorPose(new GameplayPosition(2f, 0f, 0f), 0f),
                new TurnBudget(4, 8f),
                characterProfile: CreateProfile("character.vale", "Vale"));
            var party = new PlayerPartyDefinition(
                new[] { "mara", "vale" },
                "mara");
            return new GameplaySession(new ScenarioDefinition(
                "bug-report-party-test",
                new ScenarioTimingDefinition(1f),
                new[] { mara, vale },
                Array.Empty<ScenarioObjectiveDefinition>(),
                playerParty: party));
        }

        private static GameplaySession CreateConsumableSession(
            out ThrownExplosiveDefinition definition)
        {
            definition = new ThrownExplosiveDefinition(
                "item.frag",
                new ActionCost(2, 0f, ActionMobility.Mobile),
                maximumRange: 12f,
                standingLaunchHeight: 1.2f,
                crouchedLaunchHeight: 0.8f,
                baseUncertaintyRadius: 0.5f,
                uncertaintyPerMeter: 0.1f,
                blastRadius: 5f,
                blastWoundMovementPenalty: 2f);
            var grenade = new InventoryItemDefinition(
                definition.Id,
                "Frag Grenade",
                hotbarSlot: 3,
                InventoryItemKind.Consumable,
                new ActionCost(0, 0f, ActionMobility.Mobile),
                EquipmentEffectSet.None,
                consumablePower: definition,
                initialQuantity: 2);
            var player = new ScenarioActorDefinition(
                "player",
                initiative: 10,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f),
                new[] { grenade },
                initiallyEquippedItemId: null);
            return new GameplaySession(new ScenarioDefinition(
                "bug-report-consumable-test",
                new ScenarioTimingDefinition(1f),
                new[] { player },
                Array.Empty<ScenarioObjectiveDefinition>()));
        }

        private static GameplayBugReportRuntime CreateRuntime() =>
            new GameplayBugReportRuntime(
                DateTime.UnixEpoch,
                "Game",
                "Version",
                "Unity",
                "Platform",
                "OS",
                "Device",
                "Graphics",
                800,
                600);

        private sealed class PendingEmergencyResolution
            : IEmergencyCycleResolution
        {
            public bool IsResolved => false;

            public void ResolveAfterResponsePass()
            {
            }
        }
    }
}
