using System;
using System.Collections.Generic;
using System.Linq;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayActorLifeStatePresenterTests
    {
        [Test]
        public void CanonicalStateDiffOwnsPartyAndHostileTerminalStatusOnce()
        {
            var host = new GameObject("Life State Presentation Host");
            GameObject prefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject partyObject = Object.Instantiate(prefab);
            GameObject hostileObject = Object.Instantiate(prefab);
            LevelWorld world = null;
            GameplayWorldRegistry registry = null;
            WeaponPresentationCatalog weapons = null;
            try
            {
                partyObject.transform.position = Vector3.zero;
                hostileObject.transform.position = Vector3.forward * 3f;
                world = new LevelWorld(
                    new GameObject("Life State Presentation World"),
                    new Dictionary<string, LevelEntityView>(),
                    null);
                registry = new GameplayWorldRegistry(world);
                registry.RegisterActor(
                    "party", "test", targetable: true, partyObject);
                registry.RegisterActor(
                    "hostile", "test", targetable: true, hostileObject);
                GameplaySession session = CreateSession();
                var dialogue = new GameplayDialogueLog();
                weapons = WeaponPresentationCatalog.CreateRuntime();
                GameplayActorLifeStatePresenter presenter =
                    host.AddComponent<GameplayActorLifeStatePresenter>();
                presenter.Bind(
                    session,
                    registry,
                    host.AddComponent<GameplayActionController>(),
                    dialogue,
                    weapons);

                GameplayCombatStateSnapshot active =
                    GameplayCombatStateCapture.Capture(session);
                GameplayCombatStateSnapshot incapacitated = WithLifeState(
                    active,
                    1L,
                    ActorLifeState.Incapacitated,
                    addInjury: true);
                GameplayReductionResult collapse = CreateReduction(
                    active,
                    incapacitated,
                    1L);

                presenter.PresentInstalledState(collapse);
                presenter.PresentInstalledState(collapse);

                Assert.That(
                    partyObject.GetComponent<ActorAnimationCoordinator>()
                        .LastRequestedAction,
                    Is.EqualTo(
                        ActorAnimationAction.IncapacitateShoulder));
                Assert.That(
                    hostileObject.GetComponent<ActorAnimationCoordinator>()
                        .LastRequestedAction,
                    Is.EqualTo(
                        ActorAnimationAction.IncapacitateShoulder));
                Assert.That(presenter.PresentedStatusChangeCount, Is.EqualTo(2));
                Assert.That(dialogue.Entries.Count, Is.EqualTo(2));
                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        "PARTY MEMBER INCAPACITATED",
                        "HOSTILE INCAPACITATED",
                    },
                    dialogue.Entries.Select(entry => entry.Title));

                int partySequence = partyObject.GetComponent<
                    ActorAnimationCoordinator>().ActionSequence;
                int hostileSequence = hostileObject.GetComponent<
                    ActorAnimationCoordinator>().ActionSequence;
                GameplayCombatStateSnapshot dead = WithLifeState(
                    incapacitated,
                    2L,
                    ActorLifeState.Dead,
                    addInjury: false);
                presenter.PresentInstalledState(CreateReduction(
                    incapacitated,
                    dead,
                    2L));

                Assert.That(
                    partyObject.GetComponent<ActorAnimationCoordinator>()
                        .ActionSequence,
                    Is.EqualTo(partySequence));
                Assert.That(
                    hostileObject.GetComponent<ActorAnimationCoordinator>()
                        .ActionSequence,
                    Is.EqualTo(hostileSequence));
                Assert.That(
                    dialogue.Entries.Count(entry =>
                        entry.Title.EndsWith(
                            "DEAD",
                            StringComparison.Ordinal)),
                    Is.EqualTo(2));
                Assert.That(
                    presenter.PresentedStatusChangeCount,
                    Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(host);
                registry?.Dispose();
                world?.Dispose();
                Object.DestroyImmediate(weapons);
            }
        }

        private static GameplayReductionResult CreateReduction(
            GameplayCombatStateSnapshot previous,
            GameplayCombatStateSnapshot resulting,
            long sequence) => new GameplayReductionResult(
                previous,
                resulting,
                new GameplayDomainEvent[]
                {
                    new GameplayTransitionReducedEvent(
                        new GameplayTransitionIdentity(
                            sequence,
                            "test-source-agnostic-life-state",
                            "party",
                            "hostile"),
                        "hostile",
                        new object()),
                });

        private static GameplayCombatStateSnapshot WithLifeState(
            GameplayCombatStateSnapshot source,
            long transitionSequence,
            ActorLifeState lifeState,
            bool addInjury)
        {
            var actors = new List<GameplayActorSnapshot>(
                source.Session.Actors.Count);
            foreach (GameplayActorSnapshot actor in source.Session.Actors)
            {
                string actorId = actor.ActorId;
                IReadOnlyList<InjuryRecord> injuries = addInjury
                    ? new[] { CreateInjury(actorId) }
                    : actor.Injuries.Injuries;
                ActorPhysiologyState physiology = lifeState
                    == ActorLifeState.Dead
                    ? new ActorPhysiologyState(0, 100, 0, 0)
                    : new ActorPhysiologyState(50, 80, 0, 75);
                var injuryState = new ActorInjuryState(
                    actorId,
                    injuries,
                    physiology,
                    lifeState);
                actors.Add(new GameplayActorSnapshot(
                    actor.ActorId,
                    actor.Pose,
                    actor.TurnBudget,
                    LegacyWoundProjection.From(injuryState),
                    actor.EquippedItemId,
                    actor.EquipmentEffects,
                    actor.MaximumWounds,
                    actor.Inventory,
                    actor.ActionPointEconomy,
                    actor.TurnMovementAllowance,
                    actor.PinState,
                    actor.EmergencyActionPointAllowance,
                    actor.SuspendedTurnBudget,
                    actor.AttacksCommittedThisTurn,
                    actor.Ammunition,
                    injuryState));
            }
            GameplaySessionStateSnapshot original = source.Session;
            var session = new GameplaySessionStateSnapshot(
                original.ScenarioId,
                original.Mode,
                original.Operation,
                original.TurnContext,
                original.EncounterActive,
                original.EncounterCompletionRequested,
                original.ActiveActorId,
                original.TurnPhase,
                actors,
                original.InitiativeOrder,
                original.Objectives,
                original.EmergencyResponders,
                original.EmergencyResponderIndex,
                original.EmergencyResumeActorId,
                original.LastActionSequence,
                original.LastTurnSequence,
                original.JournalSequence,
                original.RunIdentity,
                original.Revision + 1L,
                original.VoluntaryTurnReentrySecondsRemaining,
                original.PendingMovementRoute,
                original.PendingVoluntaryTurnCycle,
                transitionSequence,
                original.LastVoluntaryTurnCycleSequence,
                original.EncounterState,
                original.AllInitiativeOrder);
            return new GameplayCombatStateSnapshot(
                session,
                source.Destructibles,
                source.Vehicles,
                source.Projectiles,
                source.SmokeFields,
                source.Coverage,
                source.FireFields,
                source.Drones);
        }

        private static InjuryRecord CreateInjury(string actorId) =>
            new InjuryRecord(
                "injury." + actorId,
                "event." + actorId,
                TargetRegionId.Torso,
                DamageMechanism.Blast,
                severity: 100,
                structuralDamage: 100,
                motorLoss: 100,
                sensoryLoss: 0,
                bleedRate: 0,
                vitalDamage: false,
                compatibilityMovementPenalty: 0f,
                systemicTraumaContribution: 100);

        private static GameplaySession CreateSession()
        {
            CharacterProfileDefinition CreateProfile(
                string id,
                string name) => new CharacterProfileDefinition(
                    id,
                    name,
                    "Test Combatant",
                    new[]
                    {
                        new CharacterRating(CoreAttributeIds.Strength, 3),
                        new CharacterRating(CoreAttributeIds.Dexterity, 3),
                        new CharacterRating(CoreAttributeIds.Grit, 3),
                        new CharacterRating(CoreAttributeIds.Charisma, 3),
                    },
                    Array.Empty<CharacterRating>(),
                    Array.Empty<string>());
            ScenarioActorDefinition CreateActor(
                string actorId,
                int initiative,
                float z) => new ScenarioActorDefinition(
                    actorId,
                    initiative,
                    new GameplayActorPose(
                        new GameplayPosition(0f, 0f, z),
                        0f),
                    new TurnBudget(4, 8f),
                    characterProfile: CreateProfile(
                        "character." + actorId,
                        actorId));
            var party = new PlayerPartyDefinition(
                new[] { "party" },
                "party");
            return new GameplaySession(new ScenarioDefinition(
                "life-state-presentation-test",
                new ScenarioTimingDefinition(1f),
                new[]
                {
                    CreateActor("party", 10, 0f),
                    CreateActor("hostile", 5, 3f),
                },
                Array.Empty<ScenarioObjectiveDefinition>(),
                playerParty: party));
        }
    }
}
