using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayAttackControllerTests
    {
        [Test]
        public void ResolvedAttackPublishesStructuredCombatDiagnostic()
        {
            var host = new GameObject("Attack Controller Test");
            try
            {
                GameplaySession session = CreateSession();
                session.EnterTurnMode();
                TargetAcquisitionPresenter acquisition =
                    host.AddComponent<TargetAcquisitionPresenter>();
                GameplayAttackController controller =
                    host.AddComponent<GameplayAttackController>();
                var dialogue = new GameplayDialogueLog();
                controller.Bind(
                    session,
                    acquisition,
                    dialogue,
                    "player");

                bool resolved = controller.TryAttack(CreateExposure());

                Assert.That(resolved, Is.True);
                Assert.That(controller.LastFailure,
                    Is.EqualTo(AttackResolutionFailure.None));
                Assert.That(controller.LastResolution.Hit, Is.True);
                Assert.That(session.GetActor("target").Wounds.WoundCount,
                    Is.EqualTo(1));
                Assert.That(dialogue.Entries, Has.Count.EqualTo(1));
                Assert.That(dialogue.Entries[0].Channel,
                    Is.EqualTo(GameplayDialogueChannel.CombatDiagnostics));
                Assert.That(dialogue.Entries[0].Title,
                    Is.EqualTo("player ATTACKS target"));
                Assert.That(dialogue.Entries[0].Message,
                    Does.Contain("HIT CHANCE -"));
                Assert.That(dialogue.Entries[0].Message,
                    Does.Contain("REGION ROLL -"));
                Assert.That(dialogue.Entries[0].Message,
                    Does.Contain("WOUND -"));
                Assert.That(dialogue.Entries[0].Message,
                    Does.Contain("OUTCOME - HIT"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AttackWithoutPointerTargetReportsFailure()
        {
            var host = new GameObject("Attack Controller Failure Test");
            try
            {
                GameplaySession session = CreateSession();
                session.EnterTurnMode();
                GameplayAttackController controller =
                    host.AddComponent<GameplayAttackController>();
                controller.Bind(
                    session,
                    host.AddComponent<TargetAcquisitionPresenter>(),
                    new GameplayDialogueLog(),
                    "player");

                Assert.That(controller.TryAttack(), Is.False);
                Assert.That(controller.LastFailure,
                    Is.EqualTo(AttackResolutionFailure.TargetNotFound));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ExplorationAttackBeginsEncounterAndResolvesOpeningShot()
        {
            var host = new GameObject("Exploration Attack Controller Test");
            try
            {
                GameplaySession session = CreateSession(
                    reactiveTarget: true);
                GameplayAttackController controller =
                    host.AddComponent<GameplayAttackController>();
                controller.Bind(
                    session,
                    host.AddComponent<TargetAcquisitionPresenter>(),
                    new GameplayDialogueLog(),
                    "player",
                    onEncounterStartRequested:
                        session.BeginEncounterFromAction);

                bool resolved = controller.TryAttack(CreateExposure());

                Assert.That(resolved, Is.True);
                Assert.That(session.EncounterActive, Is.True);
                Assert.That(session.Mode,
                    Is.EqualTo(GameplaySessionMode.TurnBased));
                Assert.That(controller.LastResolution, Is.Not.Null);
                Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                    Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void OpeningShotCommitsBeforeHigherInitiativeActorTakesControl()
        {
            var host = new GameObject("Lower Initiative Opening Shot Test");
            try
            {
                GameplaySession session = CreateSession(
                    reactiveTarget: true,
                    playerInitiative: 1,
                    targetInitiative: 20);
                GameplayAttackController controller =
                    host.AddComponent<GameplayAttackController>();
                controller.Bind(
                    session,
                    host.AddComponent<TargetAcquisitionPresenter>(),
                    new GameplayDialogueLog(),
                    "player",
                    onEncounterStartRequested:
                        session.BeginEncounterFromAction);

                Assert.That(controller.TryAttack(CreateExposure()), Is.True);

                Assert.That(session.GetActor("target").Wounds.WoundCount,
                    Is.EqualTo(1));
                Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                    Is.EqualTo(3));
                Assert.That(session.EncounterActive, Is.True);
                Assert.That(session.ActiveActorId, Is.EqualTo("target"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void LiveOpeningShotDefersInitiativeUntilHitReactionCompletes()
        {
            var host = new GameObject("Deferred Opening Shot Test");
            try
            {
                GameplaySession session = CreateSession(
                    reactiveTarget: true,
                    playerInitiative: 1,
                    targetInitiative: 20);
                host.AddComponent<ThirdPersonMotor>();
                ExplorationMovementInput movementInput =
                    host.AddComponent<ExplorationMovementInput>();
                GameplaySessionPresenter sessionPresenter =
                    host.AddComponent<GameplaySessionPresenter>();
                sessionPresenter.Bind(
                    session,
                    movementInput,
                    host.transform,
                    "player");
                sessionPresenter.BindEncounterPresentation(
                    new GameplayDialogueLog());
                using var committedConsequences =
                    new GameplayCommittedActionConsequenceCoordinator(
                        session,
                        new SilentCommittedActionSoundQuery(),
                        sessionPresenter
                            .TryBeginEncounterFromCommittedAction);
                GameplayAttackController controller =
                    host.AddComponent<GameplayAttackController>();
                controller.Bind(
                    session,
                    host.AddComponent<TargetAcquisitionPresenter>(),
                    new GameplayDialogueLog(),
                    "player",
                    sessionPresenter.TryBeginEncounterFromAction);
                bool reactionPublishedBeforeEncounter = false;
                controller.AttackResolved += _ =>
                    reactionPublishedBeforeEncounter =
                        !session.EncounterActive;

                Assert.That(controller.TryAttack(CreateExposure()), Is.True);

                Assert.That(reactionPublishedBeforeEncounter, Is.True);
                Assert.That(session.GetActor("target").Wounds.WoundCount,
                    Is.EqualTo(1));
                Assert.That(session.EncounterActive, Is.False);
                Assert.That(sessionPresenter.EncounterStartPending, Is.True);
                Assert.That(movementInput.InputEnabled, Is.False);
                Assert.That(
                    committedConsequences.LastEncounterStartFailure,
                    Is.Empty);

                sessionPresenter.Tick(
                    ActorInjuryAnimationOverlayProjector.HitReactionSeconds
                    + 0.15f);
                Assert.That(session.EncounterActive, Is.False);

                sessionPresenter.Tick(0.02f);
                Assert.That(session.EncounterActive, Is.True);
                Assert.That(session.ActiveActorId, Is.EqualTo("target"));
                Assert.That(sessionPresenter.EncounterStartPending, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ExplorationWorldDischargeDoesNotBeginEncounter()
        {
            var host = new GameObject("Exploration World Discharge Test");
            try
            {
                GameplaySession session = CreateSession();
                GameplayAttackController controller =
                    host.AddComponent<GameplayAttackController>();
                controller.Bind(
                    session,
                    host.AddComponent<TargetAcquisitionPresenter>(),
                    new GameplayDialogueLog(),
                    "player",
                    onEncounterStartRequested:
                        session.BeginEncounterFromAction);
                GameplayActionRecord published = null;
                controller.WeaponDischarged += action => published = action;

                bool fired = controller.TryDischarge(
                    new GameplayPosition(5f, 0f, 0f));

                Assert.That(fired, Is.True);
                Assert.That(session.EncounterActive, Is.False);
                Assert.That(session.Mode,
                    Is.EqualTo(GameplaySessionMode.Exploration));
                Assert.That(controller.LastResolution, Is.Null);
                Assert.That(controller.LastDischarge, Is.Not.Null);
                Assert.That(controller.LastDischarge.AimPoint.X,
                    Is.EqualTo(5f));
                Assert.That(published, Is.SameAs(
                    controller.LastResolvedAction));
                Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                    Is.EqualTo(4));
                Assert.That(controller.LastResolvedAction.Cost.ActionPoints,
                    Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ExplorationDischargeAtConfiguredObjectBeginsEncounter()
        {
            var host = new GameObject("Responsive Object Discharge Test");
            try
            {
                GameplaySession session = CreateSession(
                    responsiveObjectId: "alarm-panel");
                GameplayAttackController controller =
                    host.AddComponent<GameplayAttackController>();
                controller.Bind(
                    session,
                    host.AddComponent<TargetAcquisitionPresenter>(),
                    new GameplayDialogueLog(),
                    "player",
                    onEncounterStartRequested:
                        session.BeginEncounterFromAction);

                bool fired = controller.TryDischarge(
                    "alarm-panel",
                    new GameplayPosition(5f, 0f, 0f));

                Assert.That(fired, Is.True);
                Assert.That(session.EncounterActive, Is.True);
                Assert.That(session.Mode,
                    Is.EqualTo(GameplaySessionMode.TurnBased));
                Assert.That(controller.LastDischarge.TargetId,
                    Is.EqualTo("alarm-panel"));
                Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                    Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ExplorationAttackAgainstInertActorDoesNotBeginEncounter()
        {
            var host = new GameObject("Inert Actor Attack Test");
            try
            {
                GameplaySession session = CreateSession();
                GameplayAttackController controller =
                    host.AddComponent<GameplayAttackController>();
                controller.Bind(
                    session,
                    host.AddComponent<TargetAcquisitionPresenter>(),
                    new GameplayDialogueLog(),
                    "player",
                    onEncounterStartRequested:
                        session.BeginEncounterFromAction);

                Assert.That(controller.TryAttack(CreateExposure()), Is.True);
                Assert.That(session.EncounterActive, Is.False);
                Assert.That(session.Mode,
                    Is.EqualTo(GameplaySessionMode.Exploration));
                Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                    Is.EqualTo(4));
                Assert.That(controller.LastResolvedAction.Cost.ActionPoints,
                    Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ExplorationAttackStartsEncounterDuringVoluntaryReentryCooldown()
        {
            var host = new GameObject("Locked Exploration Attack Test");
            try
            {
                GameplaySession session = CreateSession(
                    reactiveTarget: true);
                session.EnterTurnMode();
                session.TryExitTurnMode(out _);
                GameplayAttackController controller =
                    host.AddComponent<GameplayAttackController>();
                controller.Bind(
                    session,
                    host.AddComponent<TargetAcquisitionPresenter>(),
                    new GameplayDialogueLog(),
                    "player",
                    onEncounterStartRequested:
                        session.BeginEncounterFromAction);

                Assert.That(controller.TryAttack(CreateExposure()), Is.True);
                Assert.That(session.EncounterActive, Is.True);
                Assert.That(session.Mode,
                    Is.EqualTo(GameplaySessionMode.TurnBased));
                Assert.That(controller.LastFailure,
                    Is.EqualTo(AttackResolutionFailure.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void WeaponTargetingArmsConfirmsAndPublishesCursorState()
        {
            var host = new GameObject("Weapon Targeting Confirmation Test");
            try
            {
                GameplaySession session = CreateSession();
                var states = new System.Collections.Generic.List<bool>();
                int confirmations = 0;
                GameplayWeaponTargetingController controller =
                    host.AddComponent<GameplayWeaponTargetingController>();
                controller.Bind(
                    session,
                    "player",
                    () =>
                    {
                        confirmations++;
                        return true;
                    },
                    states.Add);

                Assert.That(controller.BeginTargeting(), Is.True);
                Assert.That(controller.IsTargeting, Is.True);
                Assert.That(controller.CurrentWarningHint.Text,
                    Does.Contain("CLICK A TARGET OR WORLD POINT"));
                Assert.That(controller.CurrentWarningHint.Text,
                    Does.Contain("TO CANCEL"));
                Assert.That(controller.CurrentWarningHint.Text,
                    Does.Not.Contain("AGAIN TO FIRE"));
                Assert.That(controller.ConfirmTargeting(), Is.True);

                Assert.That(confirmations, Is.EqualTo(1));
                Assert.That(controller.IsTargeting, Is.False);
                Assert.That(states, Is.EqualTo(new[] { true, false }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void FailedWeaponConfirmationStaysArmedForRetryOrCancel()
        {
            var host = new GameObject("Weapon Targeting Retry Test");
            try
            {
                GameplayWeaponTargetingController controller =
                    host.AddComponent<GameplayWeaponTargetingController>();
                controller.Bind(CreateSession(), "player", () => false);

                controller.BeginTargeting();

                Assert.That(controller.ConfirmTargeting(), Is.False);
                Assert.That(controller.IsTargeting, Is.True);
                Assert.That(controller.CancelTargeting(), Is.True);
                Assert.That(controller.IsTargeting, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RepeatingArmedWeaponHotkeyCancelsInsteadOfFiring()
        {
            var host = new GameObject("Weapon Targeting Toggle Test");
            try
            {
                int confirmations = 0;
                GameplayWeaponTargetingController controller =
                    host.AddComponent<GameplayWeaponTargetingController>();
                controller.Bind(
                    CreateSession(),
                    "player",
                    () =>
                    {
                        confirmations++;
                        return true;
                    });

                Assert.That(controller.ToggleTargeting(), Is.True);
                Assert.That(controller.IsTargeting, Is.True);
                Assert.That(controller.ToggleTargeting(), Is.True);
                Assert.That(controller.IsTargeting, Is.False);
                Assert.That(confirmations, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ContactTargetingExplainsActorAndReachRequirement()
        {
            var host = new GameObject("Contact Targeting Test");
            try
            {
                GameplayWeaponTargetingController controller =
                    host.AddComponent<GameplayWeaponTargetingController>();
                controller.Bind(CreateContactSession(1.5f), "player", () => true);

                Assert.That(controller.BeginTargeting(), Is.True);
                Assert.That(controller.CurrentWarningHint.Text,
                    Does.Contain("KNIFE STRIKE ARMED"));
                Assert.That(controller.CurrentWarningHint.Text,
                    Does.Contain("ACTOR WITHIN 2 M"));
                Assert.That(controller.CurrentWarningHint.Text,
                    Does.Not.Contain("WORLD POINT"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void InvalidContactAttackDoesNotOpenEncounter()
        {
            var host = new GameObject("Contact Encounter Preflight Test");
            try
            {
                GameplaySession session = CreateContactSession(
                    targetDistance: 2.5f,
                    reactiveTarget: true);
                GameplayAttackController controller =
                    host.AddComponent<GameplayAttackController>();
                controller.Bind(
                    session,
                    host.AddComponent<TargetAcquisitionPresenter>(),
                    new GameplayDialogueLog(),
                    "player",
                    onEncounterStartRequested:
                        session.BeginEncounterFromAction);

                Assert.That(controller.TryAttack(CreateExposure()), Is.False);
                Assert.That(controller.LastFailure,
                    Is.EqualTo(AttackResolutionFailure.TargetOutOfReach));
                Assert.That(session.EncounterActive, Is.False);
                Assert.That(session.Mode,
                    Is.EqualTo(GameplaySessionMode.Exploration));
                Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                    Is.EqualTo(4));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static GameplaySession CreateSession(
            bool reactiveTarget = false,
            string responsiveObjectId = null,
            int playerInitiative = 10,
            int targetInitiative = 0)
        {
            var player = new ScenarioActorDefinition(
                "player",
                playerInitiative,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f),
                new AttackDefinition(
                    "attack.rifle",
                    "Fire rifle",
                    new ActionCost(1, 0f, ActionMobility.Set),
                    2f,
                    accuracyDecay: AccuracyDecayDefinition.None));
            var target = new ScenarioActorDefinition(
                "target",
                targetInitiative,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 5f), 0f),
                new TurnBudget(0, 8f));
            var responses = new System.Collections.Generic.List<
                AttackResponseDefinition>();
            if (reactiveTarget)
            {
                responses.Add(new AttackResponseDefinition(
                    "target",
                    startsEncounter: true));
            }
            if (!string.IsNullOrWhiteSpace(responsiveObjectId))
            {
                responses.Add(new AttackResponseDefinition(
                    responsiveObjectId,
                    startsEncounter: true));
            }

            return new GameplaySession(new ScenarioDefinition(
                "attack-controller-test",
                new ScenarioTimingDefinition(1.25f),
                new[] { player, target },
                Array.Empty<ScenarioObjectiveDefinition>(),
                responses));
        }

        private sealed class SilentCommittedActionSoundQuery :
            IGameplayCommittedActionSoundQuery
        {
            public EncounterSoundEvidence Capture(
                string observerActorId,
                string sourceActorId,
                GameplayPosition origin,
                float soundSignature) =>
                new EncounterSoundEvidence(
                    sourceActorId,
                    origin,
                    0f);
        }

        private static GameplaySession CreateContactSession(
            float targetDistance,
            bool reactiveTarget = false)
        {
            var player = new ScenarioActorDefinition(
                "player",
                10,
                new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
                new TurnBudget(4, 8f),
                new AttackDefinition(
                    "attack.combat-knife",
                    "Knife strike",
                    new ActionCost(1, 0f, ActionMobility.Mobile),
                    2f,
                    contact: new ContactAttackDefinition(2f)));
            var target = new ScenarioActorDefinition(
                "target",
                0,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, targetDistance),
                    0f),
                new TurnBudget(0, 8f));
            return new GameplaySession(new ScenarioDefinition(
                "contact-controller-test",
                new ScenarioTimingDefinition(1.25f),
                new[] { player, target },
                Array.Empty<ScenarioObjectiveDefinition>(),
                reactiveTarget
                    ? new[]
                    {
                        new AttackResponseDefinition(
                            "target",
                            startsEncounter: true),
                    }
                    : Array.Empty<AttackResponseDefinition>()));
        }

        private static TargetExposureSnapshot CreateExposure()
        {
            return new TargetExposureSnapshot(
                "player",
                "target",
                new[]
                {
                    new TargetRegionExposure(TargetRegionId.Head, 0, 5),
                    new TargetRegionExposure(TargetRegionId.Torso, 5, 5),
                    new TargetRegionExposure(TargetRegionId.LeftArm, 0, 5),
                    new TargetRegionExposure(TargetRegionId.RightArm, 0, 5),
                    new TargetRegionExposure(TargetRegionId.LeftLeg, 5, 5),
                    new TargetRegionExposure(TargetRegionId.RightLeg, 0, 5),
                });
        }
    }
}
