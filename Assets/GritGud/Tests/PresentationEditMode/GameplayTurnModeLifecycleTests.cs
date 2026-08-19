using System.Collections;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GritGud.Presentation.Tests
{
    public sealed class GameplayTurnModeLifecycleTests
    {
        [UnityTest]
        public IEnumerator VoluntaryAndEncounterTurnsPreserveControlAndExitPolicy()
        {
            using var runtime = new GameplayRuntimeTestHarness();
            yield return runtime.Start();

            GameplayController gameplay = runtime.Gameplay;
            GameplayHud hud = runtime.Hud;
            GameplaySessionPresenter sessionPresenter = runtime.SessionPresenter;
            TurnMovementController turnMovement = runtime.TurnMovement;
            GameplayActionController actions = runtime.Actions;
            GameObject player = GameObject.Find("Player Actor");
            ExplorationMovementInput input =
                player.GetComponent<ExplorationMovementInput>();

            hud.RequestTurnModeToggle();
            Assert.That(
                gameplay.Session.Mode,
                Is.EqualTo(GameplaySessionMode.TurnBased));
            Assert.That(hud.IsCommandBarVisible, Is.True);
            Assert.That(hud.AreTurnResourcesVisible, Is.True);
            Assert.That(hud.IsEndTurnAvailable, Is.True);
            Assert.That(gameplay.Session.ActiveActorId, Is.EqualTo("oren-vale"));
            Assert.That(input.InputEnabled, Is.False);

            GameplayActorView activePartyView = gameplay.WorldRegistry
                .GetActor("oren-vale");
            ExplorationMovementInput activePartyInput =
                activePartyView.MovementInput;
            ActorAnimationCoordinator activePartyPresenter =
                activePartyView.Root.GetComponent<ActorAnimationCoordinator>();
            Animator activePartyAnimator =
                activePartyView.Transform.GetComponentInChildren<Animator>();
            Assert.That(activePartyInput.InputEnabled, Is.False);
            Assert.That(
                activePartyInput.InputSource,
                Is.SameAs(runtime.InputController));
            Assert.That(input.InputSource, Is.Null);
            Assert.That(gameplay.PartyControl.SelectedActorId, Is.EqualTo("oren-vale"));
            Assert.That(gameplay.PartyControl.CommandActorId, Is.EqualTo("oren-vale"));
            Assert.That(gameplay.PartyHud.CurrentModel.Members[1].Selected, Is.True);

            GameplayActorSnapshot playerState = gameplay.Session.GetActor("oren-vale");
            Assert.That(
                playerState.Pose.Position.X,
                Is.EqualTo(activePartyView.Transform.position.x).Within(0.001f));
            Assert.That(
                playerState.Pose.Position.Y,
                Is.EqualTo(activePartyView.Transform.position.y).Within(0.001f));
            Assert.That(
                playerState.Pose.Position.Z,
                Is.EqualTo(activePartyView.Transform.position.z).Within(0.001f));

            CharacterController characterController =
                activePartyView.Transform.GetComponent<CharacterController>();
            float standingHeight = characterController.height;
            Assert.That(
                activePartyAnimator.GetInteger(ActorAnimationParameters.Stance),
                Is.EqualTo((int)ActorStance.Standing));
            Assert.That(sessionPresenter.ToggleStance(), Is.True);
            Assert.That(
                gameplay.Session.GetActor("oren-vale").Pose.Stance,
                Is.EqualTo(ActorStance.Crouched));
            Assert.That(characterController.height, Is.LessThan(standingHeight));
            Assert.That(
                activePartyAnimator.GetInteger(ActorAnimationParameters.Stance),
                Is.EqualTo((int)ActorStance.Crouched));
            Assert.That(sessionPresenter.ToggleStance(), Is.True);
            Assert.That(
                gameplay.Session.GetActor("oren-vale").Pose.Stance,
                Is.EqualTo(ActorStance.Standing));
            Assert.That(
                characterController.height,
                Is.EqualTo(standingHeight).Within(0.001f));
            Assert.That(
                activePartyAnimator.GetInteger(ActorAnimationParameters.Stance),
                Is.EqualTo((int)ActorStance.Standing));
            Assert.That(activePartyPresenter, Is.Not.Null);
            Assert.That(
                actions.EvaluateInteraction(),
                Is.EqualTo(GameplayActionFailure.TargetOutOfRange));

            float activePartyMovementOpportunity = gameplay.Session
                .GetActor("oren-vale")
                .TurnBudget.MovementOpportunity;
            gameplay.Session.SpendMovement(
                "oren-vale",
                activePartyMovementOpportunity);
            Assert.That(turnMovement.SynchronizePlanningState(), Is.True);
            Assert.That(turnMovement.PlanningMaximumCost, Is.Zero);
            Assert.That(turnMovement.PlanPointCount, Is.EqualTo(1));
            Assert.That(actions.TryEndTurn(), Is.True);
            Assert.That(actions.LastTurnEndFailure, Is.EqualTo(TurnEndFailure.None));
            Assert.That(
                gameplay.Session.Mode,
                Is.EqualTo(GameplaySessionMode.TurnBased));
            Assert.That(
                gameplay.Session.Operation,
                Is.EqualTo(GameplaySessionOperation.ResolvingWorldTurn));
            Assert.That(actions.StatusMessage, Is.EqualTo("World turn resolving..."));
            Assert.That(hud.IsEndTurnAvailable, Is.False);
            Assert.That(gameplay.Session.LastCompletedVoluntaryTurnCycle, Is.Null);
            Assert.That(turnMovement.SynchronizePlanningState(), Is.False);
            Assert.That(turnMovement.PlanPointCount, Is.Zero);
            Assert.That(gameplay.Session.CompleteVoluntaryWorldTurn(), Is.True);
            Assert.That(turnMovement.SynchronizePlanningState(), Is.True);
            Assert.That(
                turnMovement.PlanningMaximumCost,
                Is.EqualTo(activePartyMovementOpportunity));
            Assert.That(turnMovement.PlanPointCount, Is.EqualTo(1));
            Assert.That(hud.IsEndTurnAvailable, Is.True);
            Assert.That(gameplay.Session.LastCompletedVoluntaryTurnCycle, Is.Not.Null);

            hud.RequestTurnModeToggle();
            Assert.That(
                gameplay.Session.Mode,
                Is.EqualTo(GameplaySessionMode.Exploration));
            Assert.That(hud.IsCommandBarVisible, Is.True);
            Assert.That(hud.AreTurnResourcesVisible, Is.False);
            Assert.That(activePartyInput.InputEnabled, Is.True);
            Assert.That(input.InputEnabled, Is.False);
            Assert.That(gameplay.Session.ResolvedActions, Is.Empty);
            Assert.That(gameplay.Session.LastCompletedVoluntaryTurnCycle, Is.Not.Null);
            hud.RequestTurnModeToggle();
            Assert.That(
                gameplay.Session.Mode,
                Is.EqualTo(GameplaySessionMode.Exploration));
            Assert.That(
                actions.LastTurnModeEntryFailure,
                Is.EqualTo(TurnModeEntryFailure.VoluntaryReentryLocked));

            Assert.That(
                gameplay.Session.BeginEncounter(new[]
                {
                    "player",
                    "oren-vale",
                    "depot-rifleman",
                }),
                Is.True);
            sessionPresenter.RefreshModePresentation();
            Assert.That(gameplay.Session.ActiveActorId, Is.EqualTo("oren-vale"));
            Assert.That(actions.CanExitTurnMode, Is.False);
            Assert.That(
                hud.CurrentModel.CommandBar.FindCommand(
                    GameplayControl.ToggleTurnMode).Enabled,
                Is.False);
            var activeFlightConstraint = new MutableTurnModeExitConstraint
            {
                BlocksTurnModeExit = true,
            };
            actions.RegisterTurnModeExitConstraint(activeFlightConstraint);
            Assert.That(actions.CanExitTurnMode, Is.False);
            Assert.That(actions.TryExitTurnMode(), Is.False);
            Assert.That(gameplay.Session.EncounterActive, Is.True);
            Assert.That(
                actions.StatusMessage,
                Is.EqualTo(activeFlightConstraint.TurnModeExitBlockedMessage));
            activeFlightConstraint.BlocksTurnModeExit = false;
            Assert.That(actions.CanExitTurnMode, Is.False);
            Assert.That(actions.TryExitTurnMode(), Is.False);
            Assert.That(
                actions.StatusMessage,
                Is.EqualTo("Hostile actors are still capable of responding."));
            Assert.That(actions.TryEndTurn(), Is.True);
            Assert.That(
                gameplay.Session.Mode,
                Is.EqualTo(GameplaySessionMode.TurnBased));
            Assert.That(gameplay.Session.ActiveActorId, Is.EqualTo("player"));
            Assert.That(hud.IsEndTurnAvailable, Is.True);
            Assert.That(
                gameplay.Session.LastEndedTurn.EndingActorId,
                Is.EqualTo("oren-vale"));
            Assert.That(actions.TryEndTurn(), Is.True);
            Assert.That(
                gameplay.Session.ActiveActorId,
                Is.EqualTo("depot-rifleman"));
            Assert.That(hud.IsEndTurnAvailable, Is.False);
            Assert.That(
                gameplay.Session.LastEndedTurn.EndingActorId,
                Is.EqualTo("player"));
            Assert.That(gameplay.Session.CompleteEncounter(), Is.True);
            sessionPresenter.RefreshModePresentation();
            Assert.That(actions.CanExitTurnMode, Is.True);
            Assert.That(actions.TryExitTurnMode(), Is.True);
            Assert.That(gameplay.Session.EncounterActive, Is.False);
            Assert.That(
                gameplay.Session.Mode,
                Is.EqualTo(GameplaySessionMode.Exploration));
        }

        private sealed class MutableTurnModeExitConstraint :
            IGameplayTurnModeExitConstraint
        {
            public bool BlocksTurnModeExit { get; set; }

            public string TurnModeExitBlockedMessage =>
                "The test flight is still active.";
        }
    }
}
