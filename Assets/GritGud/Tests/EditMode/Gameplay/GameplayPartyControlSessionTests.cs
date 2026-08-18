using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests
{
    public sealed class GameplayPartyControlSessionTests
    {
        [Test]
        public void ExplorationSelectionMovesDisplayAndCommandAuthorityTogether()
        {
            GameplaySession gameplay = CreateGameplay();
            using var control = new GameplayPartyControlSession(gameplay);
            GameplayPartyControlSnapshot observed = default;
            control.ControlChanged += snapshot => observed = snapshot;

            Assert.That(control.SelectedActorId, Is.EqualTo("mara"));
            Assert.That(control.CommandActorId, Is.EqualTo("mara"));

            Assert.That(control.TrySelectActor(
                "vale",
                out GameplayPartySelectionFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(GameplayPartySelectionFailure.None));
            Assert.That(control.SelectedActorId, Is.EqualTo("vale"));
            Assert.That(control.CommandActorId, Is.EqualTo("vale"));
            Assert.That(observed.SelectedActorId, Is.EqualTo("vale"));
            Assert.That(observed.CommandActorId, Is.EqualTo("vale"));
        }

        [Test]
        public void ExplorationCycleWrapsAcrossCapablePartyMembers()
        {
            GameplaySession gameplay = CreateGameplay();
            using var control = new GameplayPartyControlSession(gameplay);

            Assert.That(control.TrySelectNextActor(
                out GameplayPartySelectionFailure firstFailure), Is.True);
            Assert.That(firstFailure, Is.EqualTo(
                GameplayPartySelectionFailure.None));
            Assert.That(control.SelectedActorId, Is.EqualTo("vale"));

            Assert.That(control.TrySelectNextActor(
                out GameplayPartySelectionFailure secondFailure), Is.True);
            Assert.That(secondFailure, Is.EqualTo(
                GameplayPartySelectionFailure.None));
            Assert.That(control.SelectedActorId, Is.EqualTo("mara"));
        }

        [Test]
        public void ExplorationCycleRejectsWhenNoAlternateActorIsCapable()
        {
            GameplaySession gameplay = CreateGameplay();
            using var control = new GameplayPartyControlSession(gameplay);
            Incapacitate(gameplay, "vale", "raider");

            Assert.That(control.TrySelectNextActor(
                out GameplayPartySelectionFailure failure), Is.False);

            Assert.That(failure, Is.EqualTo(
                GameplayPartySelectionFailure.NoAlternateCapableActor));
            Assert.That(control.SelectedActorId, Is.EqualTo("mara"));
        }

        [Test]
        public void PartyHudModelSeparatesSelectionFromInitiativeAuthority()
        {
            GameplaySession gameplay = CreateGameplay();
            using var control = new GameplayPartyControlSession(gameplay);

            GameplayPartyHudModel exploration =
                GameplayPartyHudModelBuilder.Build(gameplay, control);

            Assert.That(exploration.InitiativeControlsSelection, Is.False);
            Assert.That(exploration.Members, Has.Count.EqualTo(2));
            Assert.That(exploration.Members[0].ActorId, Is.EqualTo("mara"));
            Assert.That(exploration.Members[0].Selected, Is.True);
            Assert.That(exploration.Members[0].Commanding, Is.True);
            Assert.That(exploration.Members[0].CanSelect, Is.False);
            Assert.That(exploration.Members[1].CanSelect, Is.True);

            Assert.That(gameplay.BeginEncounter(), Is.True);
            GameplayPartyHudModel combat =
                GameplayPartyHudModelBuilder.Build(gameplay, control);

            Assert.That(combat.InitiativeControlsSelection, Is.True);
            Assert.That(combat.Members[0].CanSelect, Is.False);
            Assert.That(combat.Members[1].CanSelect, Is.False);
            Assert.That(combat.Members[0].Commanding, Is.True);
        }

        [Test]
        public void TurnBasedControlFollowsFriendlyInitiativeAndDisablesOnEnemyTurn()
        {
            GameplaySession gameplay = CreateGameplay();
            using var control = new GameplayPartyControlSession(gameplay);

            Assert.That(gameplay.BeginEncounter(), Is.True);
            Assert.That(gameplay.ActiveActorId, Is.EqualTo("mara"));
            Assert.That(control.SelectedActorId, Is.EqualTo("mara"));
            Assert.That(control.CommandActorId, Is.EqualTo("mara"));

            Assert.That(gameplay.TryEndTurn("mara", out _), Is.True);

            Assert.That(gameplay.ActiveActorId, Is.EqualTo("raider"));
            Assert.That(control.SelectedActorId, Is.EqualTo("mara"));
            Assert.That(control.CommandActorId, Is.Null);

            Assert.That(gameplay.TryEndTurn("raider", out _), Is.True);

            Assert.That(gameplay.ActiveActorId, Is.EqualTo("vale"));
            Assert.That(control.SelectedActorId, Is.EqualTo("vale"));
            Assert.That(control.CommandActorId, Is.EqualTo("vale"));
        }

        [Test]
        public void ThrowingControlObserverCannotInterruptLaterObserversOrTurnAdvance()
        {
            GameplaySession gameplay = CreateGameplay();
            using var control = new GameplayPartyControlSession(gameplay);
            Assert.That(gameplay.BeginEncounter(), Is.True);
            GameplayPartyControlSnapshot? observed = null;
            control.ControlChanged += _ => throw new InvalidOperationException(
                "control projection failed");
            control.ControlChanged += snapshot => observed = snapshot;

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    gameplay.TryEndTurn("mara", out _));

            Assert.That(
                exception.Message,
                Is.EqualTo("control projection failed"));
            Assert.That(gameplay.Mode, Is.EqualTo(GameplaySessionMode.TurnBased));
            Assert.That(gameplay.ActiveActorId, Is.EqualTo("raider"));
            Assert.That(observed.HasValue, Is.True);
            Assert.That(observed.Value.CommandActorId, Is.Null);
        }

        [Test]
        public void ExplorationSelectionIsRejectedDuringTurnMode()
        {
            GameplaySession gameplay = CreateGameplay();
            using var control = new GameplayPartyControlSession(gameplay);
            gameplay.EnterTurnMode();

            Assert.That(control.TrySelectActor(
                "vale",
                out GameplayPartySelectionFailure failure), Is.False);

            Assert.That(failure, Is.EqualTo(
                GameplayPartySelectionFailure
                    .TurnBasedControlFollowsInitiative));

            Assert.That(control.TrySelectNextActor(
                out GameplayPartySelectionFailure cycleFailure), Is.False);
            Assert.That(cycleFailure, Is.EqualTo(
                GameplayPartySelectionFailure
                    .TurnBasedControlFollowsInitiative));
        }

        [Test]
        public void HostilityAndDefeatEvaluateTheWholeParty()
        {
            GameplaySession gameplay = CreateGameplay();
            using var control = new GameplayPartyControlSession(gameplay);

            Assert.That(control.IsPartyDefeated, Is.False);
            Assert.That(control.HasCapableHostileActor(), Is.True);

            Incapacitate(gameplay, "mara", "raider");
            Assert.That(control.IsPartyDefeated, Is.False);
            Assert.That(control.SelectedActorId, Is.EqualTo("vale"));
            Assert.That(control.CommandActorId, Is.EqualTo("vale"));

            Incapacitate(gameplay, "vale", "raider");
            Assert.That(control.IsPartyDefeated, Is.True);
            Assert.That(control.SelectedActorId, Is.Null);
            Assert.That(control.CommandActorId, Is.Null);
        }

        [Test]
        public void ResponsiveOneWayEnemyHostilityKeepsEncounterRelevant()
        {
            GameplaySession gameplay = CreateGameplay(partyHostile: false);
            using var control = new GameplayPartyControlSession(gameplay);

            Assert.That(gameplay.IsHostile("mara", "raider"), Is.False);
            Assert.That(gameplay.IsHostile("raider", "mara"), Is.True);
            Assert.That(control.HasCapableHostileActor(), Is.True);
        }

        private static GameplaySession CreateGameplay(bool partyHostile = true)
        {
            ScenarioActorDefinition mara = CreateActor(
                "mara",
                initiative: 5,
                identityId: "character.mara",
                allegianceId: "party",
                hostileAllegianceId: partyHostile ? "raider" : "other");
            ScenarioActorDefinition raider = CreateActor(
                "raider",
                initiative: 4,
                identityId: "character.raider",
                allegianceId: "raider",
                hostileAllegianceId: "party");
            ScenarioActorDefinition vale = CreateActor(
                "vale",
                initiative: 3,
                identityId: "character.vale",
                allegianceId: "party",
                hostileAllegianceId: partyHostile ? "raider" : "other");
            var party = new PlayerPartyDefinition(
                new[] { "mara", "vale" },
                "mara");
            var scenario = new ScenarioDefinition(
                "party-control-test",
                new ScenarioTimingDefinition(1f),
                new[] { mara, raider, vale },
                Array.Empty<ScenarioObjectiveDefinition>(),
                playerParty: party);
            return new GameplaySession(scenario, scenarioSeed: 1u);
        }

        private static ScenarioActorDefinition CreateActor(
            string id,
            int initiative,
            string identityId,
            string allegianceId,
            string hostileAllegianceId)
        {
            var profile = new CharacterProfileDefinition(
                identityId,
                id,
                "Test Actor",
                new[]
                {
                    new CharacterRating(CoreAttributeIds.Strength, 3),
                    new CharacterRating(CoreAttributeIds.Dexterity, 3),
                    new CharacterRating(CoreAttributeIds.Grit, 3),
                    new CharacterRating(CoreAttributeIds.Charisma, 3),
                },
                Array.Empty<CharacterRating>(),
                Array.Empty<string>());
            var combat = new ActorCombatDefinition(
                allegianceId,
                new[] { hostileAllegianceId },
                maximumWounds: 1);
            return new ScenarioActorDefinition(
                id,
                initiative,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f),
                attack: new AttackDefinition(
                    "attack.test",
                    "Test attack",
                    new ActionCost(1, 0f, ActionMobility.Set),
                    woundMovementPenalty: 1f,
                    accuracyDecay: AccuracyDecayDefinition.None),
                combat: combat,
                characterProfile: profile);
        }

        private static void Incapacitate(
            GameplaySession gameplay,
            string targetId,
            string attackerId)
        {
            var exposure = new TargetExposureSnapshot(
                attackerId,
                targetId,
                new[]
                {
                    new TargetRegionExposure(
                        TargetRegionId.Torso,
                        visibleSampleCount: 1,
                        totalSampleCount: 1),
                });
            var attacks = new GameplayAttackSession(
                gameplay,
                authoredScenarioSeed: 1u);
            Assert.That(attacks.TryResolve(
                attackerId,
                exposure,
                out _,
                out AttackResolutionFailure failure), Is.True);
            Assert.That(failure, Is.EqualTo(AttackResolutionFailure.None));
        }
    }
}
