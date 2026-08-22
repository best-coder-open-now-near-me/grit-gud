using System;
using System.Collections;
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
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GritGud.PlayMode.Tests
{
    public sealed class TurnReplayPresentationPlayModeTests
    {
        [UnityTest]
        public IEnumerator LiveAwayTurnReplayDrivesWorldAndCombatTranscript()
        {
            GameplaySession gameplay = CreateLiveReplayGameplay(
                out AttackDefinition enemyAttack);
            Assert.That(gameplay.BeginEncounter(), Is.True);
            GameplayCombatStateSnapshot initial =
                GameplayCombatStateCapture.Capture(gameplay);
            GameplayTransitionReducerRegistry reducers =
                GameplaySimulationReducers.CreateCurrent();
            GameplayReachableInput[] inputs =
            {
                new GameplayReachableInput(
                    GameplayReachableInputKind.EndTurnControl,
                    "control.end-turn.player",
                    "player",
                    GameplayCapabilityProfiles.EndTurn(emergency: false)),
                new GameplayReachableInput(
                    GameplayReachableInputKind.MovementControl,
                    "ai.move.enemy",
                    "enemy",
                    GameplayCapabilityProfiles.GroundedMove()),
                new GameplayReachableInput(
                    GameplayReachableInputKind.EquippedAttack,
                    "weapon.rifle.power->Actor",
                    "enemy",
                    GameplayCapabilityProfiles.Attack(enemyAttack),
                    "player"),
                new GameplayReachableInput(
                    GameplayReachableInputKind.EndTurnControl,
                    "ai.end-turn.enemy",
                    "enemy",
                    GameplayCapabilityProfiles.EndTurn(emergency: false)),
            };
            GameplayCapabilityRegistry capabilities =
                GameplayCurrentCapabilityCatalog.Create(reducers, inputs);

            GameplayLiveSessionRuntime live = null;
            LevelWorld world = null;
            GameplayWorldRegistry registry = null;
            GameplayTurnReplayWorldPresenter worldPresenter = null;
            GameplayReplayTranscriptPresenter transcriptPresenter = null;
            GameplayWeaponPresenter enemyWeapon = null;
            GameObject host = null;
            GameObject playerActor = null;
            GameObject enemyActor = null;
            float originalTimeScale = Time.timeScale;
            try
            {
                live = new GameplayLiveSessionRuntime(
                    gameplay,
                    CreateReplayExecutionIdentity(gameplay),
                    initial,
                    reducers,
                    capabilities);

                Assert.That(gameplay.TryEndTurn(
                    "player",
                    out TurnEndFailure playerEndFailure), Is.True,
                    playerEndFailure.ToString());
                GameplayActorPose enemyOrigin = gameplay.GetActor("enemy").Pose;
                var route = new MovementRouteRecord(
                    "enemy",
                    enemyOrigin,
                    new[] { new GameplayPosition(3f, 0f, 0f) });
                gameplay.CommitMovementRoute(route);

                var attacks = new GameplayAttackSession(gameplay);
                Assert.That(attacks.TryResolve(
                    "enemy",
                    CreateFullyExposedTarget("enemy", "player"),
                    out GameplayActionRecord attackAction,
                    out AttackResolutionFailure attackFailure), Is.True,
                    attackFailure.ToString());
                Assert.That(attackAction.Outcomes.Any(outcome =>
                    outcome is AttackResolvedActionOutcome), Is.True);
                Assert.That(gameplay.TryEndTurn(
                    "enemy",
                    out TurnEndFailure enemyEndFailure), Is.True,
                    enemyEndFailure.ToString());

                Assert.That(live.TryCreateLastCompletedTurnReplay(
                    out GameplaySemanticReplayTimeline lastTurn), Is.True);
                Assert.That(lastTurn.Frames.Select(frame =>
                        frame.SemanticRecord.GetType()),
                    Is.EqualTo(new[]
                    {
                        typeof(MovementRouteRecord),
                        typeof(GameplayActionRecord),
                        typeof(TurnEndRecord),
                    }));
                Assert.That(live.TryCreateReplaySinceActorLastTurn(
                    "player",
                    out GameplaySemanticReplayTimeline awayReplay,
                    out GameplayPlayerAwayReplayInterval interval), Is.True);
                Assert.That(interval.Windows.Select(window => window.ActorId),
                    Is.EqualTo(new[] { "enemy" }));
                Assert.That(interval.TransitionCount, Is.EqualTo(3));
                Assert.That(awayReplay.Frames, Has.Count.EqualTo(3));

                var transcript = new ReplayCombatTranscript(
                    new GameplaySemanticReplayPlaybackTimeline(awayReplay));
                Assert.That(transcript.Entries.Any(entry =>
                    entry.EventKind ==
                        ReplayCombatTranscriptEventKind.WeaponDischarge),
                    Is.True);

                GameObject prefab = Resources.Load<GameObject>(
                    "Actors/DefaultPlayerActor");
                Assert.That(prefab, Is.Not.Null);
                playerActor = Object.Instantiate(prefab);
                enemyActor = Object.Instantiate(prefab);
                yield return null;

                world = new LevelWorld(
                    new GameObject("Live Away Replay World"),
                    new Dictionary<string, LevelEntityView>(),
                    null);
                registry = new GameplayWorldRegistry(world);
                registry.RegisterActor(
                    "player",
                    ActorPresentationIds.DefaultPlayer,
                    targetable: true,
                    playerActor);
                registry.RegisterActor(
                    "enemy",
                    ActorPresentationIds.DefaultPlayer,
                    targetable: true,
                    enemyActor);

                host = new GameObject("Live Away Replay Presentation Host");
                GameplayAttackController attackController =
                    host.AddComponent<GameplayAttackController>();
                GameplayProjectileController projectileController =
                    host.AddComponent<GameplayProjectileController>();
                enemyWeapon = enemyActor.AddComponent<
                    GameplayWeaponPresenter>();
                enemyWeapon.Bind(
                    gameplay,
                    registry,
                    attackController,
                    projectileController,
                    enemyActor.GetComponent<ActorAnimationCoordinator>(),
                    "enemy",
                    presentAsLocalPlayer: false);

                GameplayInputController input =
                    host.AddComponent<GameplayInputController>();
                GameplayTurnReplayHud hud =
                    host.AddComponent<GameplayTurnReplayHud>();
                GameplayHud gameplayHud = host.AddComponent<GameplayHud>();
                GameplayPartyHud partyHud =
                    host.AddComponent<GameplayPartyHud>();
                GameplayEnemyController enemies =
                    host.AddComponent<GameplayEnemyController>();
                GameplayDialogueDrawer drawer =
                    host.AddComponent<GameplayDialogueDrawer>();
                var liveDialogue = new GameplayDialogueLog();
                liveDialogue.Append(
                    GameplayDialogueChannel.System,
                    "LIVE LOG",
                    "The live transcript must be restored after replay.");
                drawer.Bind(liveDialogue);
                drawer.SetExpanded(false);

                hud.Bind(
                    gameplay,
                    live,
                    GameplayReplaySource.LiveEncounter,
                    () => "player");
                worldPresenter = new GameplayTurnReplayWorldPresenter();
                worldPresenter.Bind(
                    registry,
                    input,
                    hud,
                    projectileController: null,
                    thrownExplosiveController: null,
                    destructibleController: null,
                    vehicleController: null,
                    droneController: null,
                    smokeController: null,
                    fireController: null,
                    liveGameplayHud: gameplayHud,
                    livePartyHud: partyHud,
                    enemyController: enemies,
                    behavioursToSuspend: Array.Empty<Behaviour>());
                transcriptPresenter = new GameplayReplayTranscriptPresenter();
                transcriptPresenter.Bind(
                    hud,
                    drawer,
                    liveDialogue,
                    onLiveExportRequested: null);

                hud.Toggle();

                Assert.That(hud.IsOpen, Is.True, hud.LastOpenFailure);
                Assert.That(hud.Replay.Frames, Has.Count.EqualTo(3));
                Assert.That(hud.ContentSummary.IsReadyToOpen, Is.True,
                    hud.ContentSummary.ValidationMessage);
                Assert.That(hud.ContentSummary.SemanticFrames, Is.EqualTo(3));
                Assert.That(hud.ContentSummary.ActorPoseDeltaFrames,
                    Is.GreaterThanOrEqualTo(1));
                StringAssert.Contains(
                    "LIVE SINCE PLAYER'S LAST TURN",
                    hud.ContentSummary.SourceLabel);
                Assert.That(drawer.HeaderLabel,
                    Is.EqualTo("REPLAY COMBAT TRANSCRIPT"));
                Assert.That(drawer.IsExpanded, Is.True);
                Assert.That(drawer.Source,
                    Is.SameAs(transcriptPresenter.VisibleSource));
                Assert.That(enemyActor.transform.position,
                    Is.EqualTo(new Vector3(5f, 0f, 0f)));

                float movementDuration = hud.Playback.Frames[0]
                    .DurationSeconds;
                hud.AdvancePlayback(movementDuration * 0.5f);

                Assert.That(enemyActor.transform.position.x,
                    Is.EqualTo(4f).Within(0.001f));

                hud.AdvancePlayback(hud.Playback.TotalDurationSeconds);

                Assert.That(drawer.VisibleEntryCount, Is.GreaterThan(0));
                Assert.That(transcriptPresenter.Transcript.Entries.Any(entry =>
                    entry.EventKind ==
                        ReplayCombatTranscriptEventKind.WeaponDischarge),
                    Is.True);
                Assert.That(enemyActor.transform.position,
                    Is.EqualTo(new Vector3(3f, 0f, 0f)));

                hud.Toggle();

                Assert.That(hud.IsOpen, Is.False);
                Assert.That(drawer.Source, Is.SameAs(liveDialogue));
                Assert.That(drawer.IsExpanded, Is.False);
                Assert.That(drawer.HeaderLabel,
                    Is.EqualTo("DIALOGUE - TRANSCRIPT"));
            }
            finally
            {
                transcriptPresenter?.Unbind();
                worldPresenter?.Dispose();
                enemyWeapon?.Unbind();
                live?.Dispose();
                registry?.Dispose();
                world?.Dispose();
                if (registry == null)
                {
                    if (playerActor != null) Object.Destroy(playerActor);
                    if (enemyActor != null) Object.Destroy(enemyActor);
                }
                if (host != null) Object.Destroy(host);
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator ActorReplayLifecycleRestoresLivePresentation()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject actor = Object.Instantiate(prefab);
            LevelWorld world = null;
            GameplayWorldRegistry registry = null;
            try
            {
                yield return null;
                world = new LevelWorld(
                    new GameObject("Replay Lifecycle World"),
                    new Dictionary<string, LevelEntityView>(),
                    null);
                registry = new GameplayWorldRegistry(world);
                registry.RegisterActor(
                    "player",
                    ActorPresentationIds.DefaultPlayer,
                    targetable: false,
                    actor);
                GameplayActorView view = registry.GetActor("player");
                var clear = new GameObject("Clear Torso");
                var wounded = new GameObject("Wounded Torso");
                clear.transform.SetParent(actor.transform, false);
                wounded.transform.SetParent(actor.transform, false);
                view.Wounds.Configure(new ActorWoundVariantBinding(
                    TargetRegionId.Torso,
                    clear,
                    wounded));
                view.Wounds.PresentAuthoritative(
                    new ActorWoundSnapshot("player", 0, 0f));
                ActorPinState livePin = CreatePinState(
                    "player",
                    "live-crate",
                    displacementSequence: 4);
                view.ReplayActions.PresentPinState(livePin);

                ActorAnimationCoordinator animation =
                    actor.GetComponent<ActorAnimationCoordinator>();
                Animator animator = animation.TargetAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animation.PresentWeaponPose(ActorAnimationPoseIds.Rifle);
                Assert.That(animation.TryPresentWeaponFire(), Is.True);
                yield return null;
                int liveActionSequence = animation.ActionSequence;
                Vector3 livePosition = actor.transform.position;
                Quaternion liveRotation = actor.transform.rotation;
                bool locomotionEnabled = actor.GetComponent<
                    ActorLocomotionAnimationPresenter>().enabled;
                ThirdPersonMotor motor = actor.GetComponent<ThirdPersonMotor>();
                ExplorationMovementInput movementInput = actor.GetComponent<
                    ExplorationMovementInput>();
                bool motorEnabled = motor.enabled;
                bool movementInputEnabled = movementInput.enabled;

                using (var replay =
                    new GameplayTurnReplayActorPresenter(view))
                {
                    replay.Begin();
                    replay.Present(
                        new GameplayActorSnapshot(
                            "player",
                            new GameplayActorPose(
                                new GameplayPosition(4f, 0f, 3f),
                                90f,
                                ActorStance.Crouched),
                            new TurnBudget(2, 4f),
                            new ActorWoundSnapshot(
                                "player",
                                headWounds: 0,
                                torsoWounds: 1,
                                leftArmWounds: 0,
                                rightArmWounds: 0,
                                leftLegWounds: 0,
                                rightLegWounds: 0,
                                movementPenalty: 1f)),
                        new TurnReplayActorActionState(
                            "player",
                            TurnReplayActorActionKind.Throw,
                            journalSequence: 1,
                            normalizedProgress: 0.5f));

                    Assert.That(actor.transform.position,
                        Is.EqualTo(new Vector3(4f, 0f, 3f)));
                    Assert.That(view.Stance.Stance,
                        Is.EqualTo(ActorStance.Crouched));
                    Assert.That(clear.activeSelf, Is.False);
                    Assert.That(wounded.activeSelf, Is.True);
                    Assert.That(animation.ReplayAction,
                        Is.EqualTo(ActorAnimationAction.Throw));
                    Assert.That(view.ReplayActions.CurrentState.Kind,
                        Is.EqualTo(TurnReplayActorActionKind.Throw));
                    Assert.That(animator.speed, Is.Zero);
                    Assert.That(actor.GetComponent<
                        ActorLocomotionAnimationPresenter>().enabled,
                        Is.False);
                    Assert.That(motor.enabled, Is.False);
                    Assert.That(movementInput.enabled, Is.False);

                    // The live locomotion presenter stays disabled for replay,
                    // so ordinary route motion must still drive the animator
                    // through the replay presenter itself.
                    replay.Present(
                        new GameplayActorSnapshot(
                            "player",
                            new GameplayActorPose(
                                new GameplayPosition(4.25f, 0f, 3f),
                                90f,
                                ActorStance.Crouched),
                            new TurnBudget(2, 4f)),
                        action: null,
                        replayVelocity: new Vector3(0f, 0f, 6f));
                    Assert.That(animator.GetFloat(
                            ActorAnimationParameters.Speed),
                        Is.EqualTo(6f).Within(0.001f));
                    Assert.That(animator.GetBool(
                            ActorAnimationParameters.Grounded),
                        Is.True);

                    replay.Present(
                        new GameplayActorSnapshot(
                            "player",
                            new GameplayActorPose(
                                new GameplayPosition(4.5f, 1.25f, 3f),
                                90f,
                                ActorStance.Standing),
                            new TurnBudget(2, 4f)),
                        new TurnReplayActorActionState(
                            "player",
                            TurnReplayActorActionKind.Jump,
                            journalSequence: 2,
                            normalizedProgress: 0.5f),
                        replayVelocity: new Vector3(0f, 0f, 5f),
                        replayGrounded: false);

                    Assert.That(animation.ReplayAction,
                        Is.EqualTo(ActorAnimationAction.Jump));
                    Assert.That(view.ReplayActions.CurrentState.Kind,
                        Is.EqualTo(TurnReplayActorActionKind.Jump));
                    Assert.That(animator.GetBool(
                            ActorAnimationParameters.Grounded),
                        Is.False);

                    ActorPinState replayPin = CreatePinState(
                        "player",
                        "replay-crate",
                        displacementSequence: 9);
                    replay.Present(
                        new GameplayActorSnapshot(
                            "player",
                            new GameplayActorPose(
                                new GameplayPosition(5f, 0f, 3f),
                                90f,
                                ActorStance.Crouched),
                            new TurnBudget(2, 4f),
                            new ActorWoundSnapshot(
                                "player",
                                headWounds: 0,
                                torsoWounds: 1,
                                leftArmWounds: 0,
                                rightArmWounds: 0,
                                leftLegWounds: 0,
                                rightLegWounds: 0,
                                movementPenalty: 1f),
                            null,
                            EquipmentEffectSet.None,
                            pinState: replayPin),
                        new TurnReplayActorActionState(
                            "player",
                            TurnReplayActorActionKind.Pinned,
                            journalSequence: 3,
                            normalizedProgress: 0.75f));

                    Assert.That(view.ReplayActions.CurrentPinState,
                        Is.SameAs(replayPin));
                    Assert.That(animation.ReplayAction,
                        Is.EqualTo(ActorAnimationAction.Incapacitate));
                    Assert.That(
                        view.TargetProfile.ProfileKind,
                        Is.EqualTo(ActorTargetProfileKind.PinnedDown));

                    GameplayActorSnapshot contactSnapshot =
                        new GameplayActorSnapshot(
                            "player",
                            new GameplayActorPose(
                                new GameplayPosition(5f, 0f, 3f),
                                90f,
                                ActorStance.Crouched),
                            new TurnBudget(2, 4f),
                            new ActorWoundSnapshot(
                                "player",
                                headWounds: 0,
                                torsoWounds: 1,
                                leftArmWounds: 0,
                                rightArmWounds: 0,
                                leftLegWounds: 0,
                                rightLegWounds: 0,
                                movementPenalty: 1f));
                    replay.Present(
                        contactSnapshot,
                        new TurnReplayActorActionState(
                            "player",
                            TurnReplayActorActionKind.Reaction,
                            journalSequence: 4,
                            normalizedProgress: 0.2f,
                            contactReaction: true,
                            resultingWoundCount: 1,
                            hitRegion: TargetRegionId.Torso));
                    Assert.That(animation.ReplayAction, Is.Null);

                    replay.Present(
                        contactSnapshot,
                        new TurnReplayActorActionState(
                            "player",
                            TurnReplayActorActionKind.Reaction,
                            journalSequence: 4,
                            normalizedProgress: 0.7f,
                            contactReaction: true,
                            resultingWoundCount: 1,
                            hitRegion: TargetRegionId.Torso));
                    Assert.That(animation.ReplayAction, Is.Null);
                    Assert.That(view.InjuryOverlay.HitReactionActive,
                        Is.True);
                    Assert.That(view.InjuryOverlay.HitReactionProgress,
                        Is.EqualTo(0.5f).Within(0.001f));

                    GameplayActorSnapshot incapacitatedSnapshot =
                        new GameplayActorSnapshot(
                            "player",
                            contactSnapshot.Pose,
                            contactSnapshot.TurnBudget,
                            contactSnapshot.Wounds,
                            equippedItemId: null,
                            equipmentEffects: EquipmentEffectSet.None,
                            maximumWounds: 1);
                    replay.Present(
                        incapacitatedSnapshot,
                        new TurnReplayActorActionState(
                            "player",
                            TurnReplayActorActionKind.Reaction,
                            journalSequence: 5,
                            normalizedProgress: 0.7f,
                            contactReaction: true,
                            resultingWoundCount: 1,
                            hitRegion: TargetRegionId.Torso));
                    Assert.That(
                        animation.ReplayAction,
                        Is.EqualTo(
                            ActorAnimationAction.IncapacitateShoulder));

                    replay.Present(incapacitatedSnapshot, action: null);
                    Assert.That(
                        animation.ReplayAction,
                        Is.EqualTo(ActorAnimationAction.Incapacitate));
                    Assert.That(animation.ReplayActionProgress, Is.EqualTo(1f));

                    replay.Present(
                        new GameplayActorSnapshot(
                            "player",
                            new GameplayActorPose(
                                new GameplayPosition(5f, 0f, 3f),
                                90f,
                                ActorStance.Crouched),
                            new TurnBudget(0, 4f),
                            new ActorWoundSnapshot(
                                "player",
                                headWounds: 0,
                                torsoWounds: 1,
                                leftArmWounds: 0,
                                rightArmWounds: 0,
                                leftLegWounds: 0,
                                rightLegWounds: 0,
                                movementPenalty: 1f)),
                        new TurnReplayActorActionState(
                            "player",
                            TurnReplayActorActionKind.GetUp,
                            journalSequence: 6,
                            normalizedProgress: 0.25f));

                    Assert.That(view.ReplayActions.CurrentPinState, Is.Null);
                    Assert.That(animation.ReplayAction,
                        Is.EqualTo(ActorAnimationAction.Interact));
                    Assert.That(
                        view.TargetProfile.ProfileKind,
                        Is.EqualTo(ActorTargetProfileKind.Crouched));

                    replay.Present(
                        new GameplayActorSnapshot(
                            "player",
                            new GameplayActorPose(
                                new GameplayPosition(5f, 0f, 3f),
                                90f,
                                ActorStance.Crouched),
                            new TurnBudget(0, 4f),
                            new ActorWoundSnapshot(
                                "player",
                                headWounds: 0,
                                torsoWounds: 1,
                                leftArmWounds: 0,
                                rightArmWounds: 0,
                                leftLegWounds: 0,
                                rightLegWounds: 0,
                                movementPenalty: 1f)),
                        new TurnReplayActorActionState(
                            "player",
                            TurnReplayActorActionKind.Push,
                            journalSequence: 7,
                            normalizedProgress: 0.4f));

                    Assert.That(animation.ReplayAction,
                        Is.EqualTo(ActorAnimationAction.Push));
                    Assert.That(animation.ReplayActionProgress,
                        Is.EqualTo(0.4f).Within(0.001f));
                }

                Assert.That(actor.transform.position,
                    Is.EqualTo(livePosition));
                Assert.That(actor.transform.rotation,
                    Is.EqualTo(liveRotation));
                Assert.That(view.Stance.Stance,
                    Is.EqualTo(ActorStance.Standing));
                Assert.That(clear.activeSelf, Is.True);
                Assert.That(wounded.activeSelf, Is.False);
                Assert.That(animation.ActionSequence,
                    Is.EqualTo(liveActionSequence));
                Assert.That(animation.ReplayAction, Is.Null);
                Assert.That(view.ReplayActions.CurrentState, Is.Null);
                Assert.That(view.ReplayActions.CurrentPinState,
                    Is.SameAs(livePin));
                Assert.That(
                    view.TargetProfile.ProfileKind,
                    Is.EqualTo(ActorTargetProfileKind.PinnedDown));
                Assert.That(actor.GetComponent<
                    ActorLocomotionAnimationPresenter>().enabled,
                    Is.EqualTo(locomotionEnabled));
                Assert.That(motor.enabled, Is.EqualTo(motorEnabled));
                Assert.That(movementInput.enabled,
                    Is.EqualTo(movementInputEnabled));
            }
            finally
            {
                registry?.Dispose();
                world?.Dispose();
                if (registry == null)
                    Object.Destroy(actor);
            }
        }

        [UnityTest]
        public IEnumerator TraversalPlaybackUsesAuthoredJumpAndRestoresMotorState()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Actors/DefaultPlayerActor");
            GameObject actor = Object.Instantiate(prefab);
            try
            {
                yield return null;
                ThirdPersonMotor motor = actor.GetComponent<ThirdPersonMotor>();
                CharacterController controller =
                    actor.GetComponent<CharacterController>();
                ActorLocomotionAnimationPresenter locomotion = actor.GetComponent<
                    ActorLocomotionAnimationPresenter>();
                ActorAnimationCoordinator animation = actor.GetComponent<
                    ActorAnimationCoordinator>();
                bool motorEnabled = motor.enabled;
                bool controllerEnabled = controller.enabled;
                bool locomotionEnabled = locomotion.enabled;
                var route = new MovementRouteRecord(
                    "player",
                    new GameplayActorPose(
                        new GameplayPosition(0f, 0f, 0f),
                        0f),
                    new TurnBudget(4, 8f),
                    new[]
                    {
                        new MovementRouteSegmentRecord(
                            new GameplayPosition(0f, 0f, 0f),
                            new GameplayPosition(0f, 0f, 2f),
                            MovementRouteSegmentKind.Jump,
                            "jump.playmode",
                            "traversal.jump",
                            2f,
                            0,
                            1.25f,
                            0.8f),
                    });
                var playback = new MovementRoutePlaybackPresenter(motor);

                playback.Begin(route);

                Assert.That(motor.enabled, Is.False);
                Assert.That(controller.enabled, Is.False);
                Assert.That(locomotion.enabled, Is.False);
                Assert.That(playback.Tick(0.4f), Is.False);
                Assert.That(actor.transform.position.y,
                    Is.EqualTo(1.25f).Within(0.001f));
                Assert.That(animation.LastRequestedAction,
                    Is.EqualTo(ActorAnimationAction.Jump));
                Assert.That(playback.Tick(0.4f), Is.True);

                Assert.That(actor.transform.position,
                    Is.EqualTo(new Vector3(0f, 0f, 2f)));
                Assert.That(motor.enabled, Is.EqualTo(motorEnabled));
                Assert.That(controller.enabled, Is.EqualTo(controllerEnabled));
                Assert.That(locomotion.enabled, Is.EqualTo(locomotionEnabled));
            }
            finally
            {
                Object.Destroy(actor);
            }
        }

        private static ActorPinState CreatePinState(
            string actorId,
            string propId,
            long displacementSequence)
        {
            return new ActorPinState(
                actorId,
                propId,
                displacementSequence,
                new DisplacementContactEvidence(
                    actorId,
                    new GameplayPosition(0f, 0.5f, 0f),
                    new GameplayPosition(0f, 1f, 0f),
                    0.1f));
        }

        private static GameplaySession CreateLiveReplayGameplay(
            out AttackDefinition enemyAttack)
        {
            var regions = new List<RegionConsequenceProfile>();
            foreach (TargetRegionId region in Enum.GetValues(
                typeof(TargetRegionId)))
            {
                regions.Add(new RegionConsequenceProfile(
                    region,
                    systemicPerHundred: 10,
                    structuralPerHundred: 10,
                    motorPerHundred: 5,
                    sensoryPerHundred: 5,
                    bleedPerHundred: 0,
                    consciousnessPerHundred: 0,
                    respirationPerHundred: 0));
            }
            var damage = new WeaponDamageProfileDefinition(
                WeaponDamageProfileDefinition.CurrentSchemaVersion,
                "damage.replay-test.rifle",
                DamageMechanism.Ballistic,
                baseImpact: 100,
                penetration: 50,
                WeaponDamageRangeProfile.NoDecay,
                regions);
            enemyAttack = new AttackDefinition(
                "attack.replay-test.rifle",
                "Replay-test rifle",
                new ActionCost(1, 0f, ActionMobility.Set),
                damage,
                accuracyDecay: AccuracyDecayDefinition.None);
            var weapon = new InventoryItemDefinition(
                "weapon.rifle",
                "Rifle",
                hotbarSlot: 1,
                InventoryItemKind.Weapon,
                new ActionCost(1, 0f, ActionMobility.Set),
                EquipmentEffectSet.None,
                enemyAttack,
                occupiedHands: 2);
            var player = new ScenarioActorDefinition(
                "player",
                20,
                new GameplayActorPose(
                    new GameplayPosition(0f, 0f, 0f),
                    0f),
                new TurnBudget(4, 8f),
                combat: new ActorCombatDefinition(
                    "party",
                    new[] { "hostile" },
                    maximumWounds: 10));
            var enemy = new ScenarioActorDefinition(
                "enemy",
                10,
                new GameplayActorPose(
                    new GameplayPosition(5f, 0f, 0f),
                    180f),
                new TurnBudget(4, 8f),
                new[] { weapon },
                weapon.Id,
                combat: new ActorCombatDefinition(
                    "hostile",
                    new[] { "party" },
                    maximumWounds: 10));
            return new GameplaySession(
                new ScenarioDefinition(
                    "live-away-replay-playmode",
                    new ScenarioTimingDefinition(1f),
                    new[] { player, enemy },
                    Array.Empty<ScenarioObjectiveDefinition>()),
                scenarioSeed: 29u);
        }

        private static TargetExposureSnapshot CreateFullyExposedTarget(
            string observerId,
            string targetId)
        {
            var regions = new List<TargetRegionExposure>();
            foreach (TargetRegionId region in Enum.GetValues(
                typeof(TargetRegionId)))
                regions.Add(new TargetRegionExposure(region, 1, 1));
            return new TargetExposureSnapshot(
                observerId,
                targetId,
                regions);
        }

        private static GameplayExecutionIdentity CreateReplayExecutionIdentity(
            GameplaySession gameplay) => new GameplayExecutionIdentity(
            new GameplayContentIdentity(
                gameplay.Scenario.Id,
                scenarioSchemaVersion: 1,
                rulesSchemaVersion:
                    GameplayCombatStateSnapshot.CurrentSchemaVersion,
                new string('a', 64)),
            new SpatialContentIdentity(
                "live-away-replay-level",
                levelSchemaVersion: 1,
                evidenceAlgorithmVersion: 1,
                new string('b', 64)),
            gameplay.RunIdentity);
    }
}
