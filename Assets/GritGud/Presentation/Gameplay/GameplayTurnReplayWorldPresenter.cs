using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    /// <summary>
    /// Presentation adapter over an exact reducer-verified trajectory. It
    /// samples immutable before/result states and never reconstructs gameplay.
    /// </summary>
    internal sealed class GameplayTurnReplayWorldPresenter : IDisposable
    {
        private readonly Dictionary<string, GameplayTurnReplayActorPresenter>
            actors = new Dictionary<string, GameplayTurnReplayActorPresenter>(
                StringComparer.Ordinal);
        private readonly List<Behaviour> liveBehaviours =
            new List<Behaviour>();
        private readonly List<bool> liveBehaviourEnabled =
            new List<bool>();
        private GameplayWorldRegistry world;
        private GameplayInputController input;
        private GameplayTurnReplayHud hud;
        private GameplayProjectileController projectiles;
        private GameplayDestructibleController destructibles;
        private GameplayVehicleController vehicles;
        private GameplaySmokeFieldController smoke;
        private GameplayFireFieldController fire;
        private GameplayDroneController drones;
        private GameplayCameraRig cameraRig;
        private GameplayReplayCameraCutPresenter cameraCuts;
        private GameplaySession gameplay;
        private GameplayHud gameplayHud;
        private GameplayPartyHud partyHud;
        private GameplayEnemyController enemies;
        private bool gameplayHudWasVisible;
        private bool partyHudWasSuppressed;
        private bool enemiesWerePaused;
        private float priorTimeScale;
        private bool presenting;

        public void Bind(
            GameplayWorldRegistry registry,
            GameplayInputController inputController,
            GameplayTurnReplayHud replayHud,
            GameplayProjectileController projectileController,
            GameplayDestructibleController destructibleController,
            GameplayVehicleController vehicleController,
            GameplaySmokeFieldController smokeController,
            GameplayFireFieldController fireController,
            GameplayDroneController droneController,
            GameplayCameraRig replayCameraRig,
            GameplayReplayCameraCutPresenter replayCameraCuts,
            GameplaySession session,
            GameplayHud liveGameplayHud,
            GameplayPartyHud livePartyHud,
            GameplayEnemyController enemyController,
            IEnumerable<Behaviour> behavioursToSuspend)
        {
            Dispose();
            world = registry ?? throw new ArgumentNullException(nameof(registry));
            input = inputController ?? throw new ArgumentNullException(
                nameof(inputController));
            hud = replayHud ?? throw new ArgumentNullException(nameof(replayHud));
            projectiles = projectileController ?? throw new ArgumentNullException(
                nameof(projectileController));
            destructibles = destructibleController ?? throw new ArgumentNullException(
                nameof(destructibleController));
            vehicles = vehicleController ?? throw new ArgumentNullException(
                nameof(vehicleController));
            smoke = smokeController ?? throw new ArgumentNullException(
                nameof(smokeController));
            fire = fireController ?? throw new ArgumentNullException(
                nameof(fireController));
            drones = droneController ?? throw new ArgumentNullException(
                nameof(droneController));
            cameraRig = replayCameraRig ?? throw new ArgumentNullException(
                nameof(replayCameraRig));
            cameraCuts = replayCameraCuts ?? throw new ArgumentNullException(
                nameof(replayCameraCuts));
            gameplay = session ?? throw new ArgumentNullException(
                nameof(session));
            gameplayHud = liveGameplayHud ?? throw new ArgumentNullException(
                nameof(liveGameplayHud));
            partyHud = livePartyHud ?? throw new ArgumentNullException(
                nameof(livePartyHud));
            enemies = enemyController ?? throw new ArgumentNullException(
                nameof(enemyController));
            foreach (Behaviour behaviour in behavioursToSuspend
                ?? throw new ArgumentNullException(nameof(behavioursToSuspend)))
            {
                if (behaviour == null)
                    throw new ArgumentException(
                        "Replay suspension cannot contain a null behaviour.",
                        nameof(behavioursToSuspend));
                if (!liveBehaviours.Contains(behaviour))
                    liveBehaviours.Add(behaviour);
            }
            hud.OpenChanged += HandleOpenChanged;
            hud.PlayheadChanged += HandlePlayheadChanged;
        }

        public void Dispose()
        {
            Restore();
            if (hud != null)
            {
                hud.OpenChanged -= HandleOpenChanged;
                hud.PlayheadChanged -= HandlePlayheadChanged;
            }
            world = null;
            input = null;
            hud = null;
            projectiles = null;
            destructibles = null;
            vehicles = null;
            smoke = null;
            fire = null;
            drones = null;
            cameraRig = null;
            cameraCuts = null;
            gameplay = null;
            gameplayHud = null;
            partyHud = null;
            enemies = null;
            liveBehaviours.Clear();
            liveBehaviourEnabled.Clear();
        }

        private void HandleOpenChanged(bool open)
        {
            if (!open)
            {
                Restore();
                return;
            }
            if (hud.Playback == null)
                return;

            actors.Clear();
            presenting = true;
            try
            {
                gameplayHudWasVisible = gameplayHud.IsVisible;
                partyHudWasSuppressed = partyHud.IsPresentationSuppressed;
                enemiesWerePaused = enemies.ReplayPaused;
                priorTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                gameplayHud.Hide();
                partyHud.SetPresentationSuppressed(true);
                input.SetCameraOnly(true);
                enemies.SetReplayPaused(true);
                liveBehaviourEnabled.Clear();
                foreach (Behaviour behaviour in liveBehaviours)
                {
                    liveBehaviourEnabled.Add(behaviour.enabled);
                    behaviour.enabled = false;
                }
                foreach (GameplayActorView actor in world.Actors)
                {
                    var presenter = new GameplayTurnReplayActorPresenter(actor);
                    actors.Add(actor.ActorId, presenter);
                    presenter.Begin();
                }
                projectiles.BeginReplayPresentation();
                vehicles.BeginReplayPresentation();
                smoke.BeginReplayPresentation();
                fire.BeginReplayPresentation();
                drones.BeginReplayPresentation();
                cameraCuts.Begin(cameraRig, world);
                destructibles.ClearReplayTransients();
                Present();
            }
            catch
            {
                Restore();
                throw;
            }
        }

        private void HandlePlayheadChanged(float _)
        {
            if (!presenting)
                return;
            ClearTransients();
            Present();
        }

        private void Present()
        {
            GameplaySemanticReplayPlaybackTimeline playback = hud.Playback;
            if (playback == null)
                return;
            GameplaySemanticReplayPlaybackPosition position = playback.Locate(
                hud.TimeSeconds);
            GameplayPresentationWorldStateSample sample =
                GameplaySemanticReplaySampler.Sample(
                    position.Frame,
                    position.Progress);
            string actorId = position.Frame.Transition.Payload.ActorId;
            ScenarioActorDefinition definition = gameplay.Scenario.GetActor(
                actorId);
            string focusId = actorId;
            string focusLabel = definition.CharacterProfile?.DisplayName
                ?? actorId;
            if (position.Frame.Transition.Payload
                is GameplayDroneMoveTransitionPayload droneMove)
            {
                focusId = droneMove.Movement.DroneId;
                focusLabel = FormatSubjectLabel(focusId);
            }
            else if (position.Frame.Transition.Payload
                is GameplayDroneAttackTransitionPayload droneAttack)
            {
                focusId = droneAttack.Action.DroneId;
                focusLabel = FormatSubjectLabel(focusId);
            }
            cameraCuts.Focus(
                focusId,
                focusLabel);
            PresentActors(sample, position);
            destructibles.PresentReplay(sample.Destructibles);
            projectiles.PresentReplay(sample.Projectiles);
            vehicles.PresentReplay(sample.Vehicles);
            smoke.PresentReplay(sample.SmokeFields);
            fire.PresentReplay(sample.FireFields);
            drones.PresentReplay(sample.Drones);
        }

        private void PresentActors(
            GameplayPresentationWorldStateSample sample,
            GameplaySemanticReplayPlaybackPosition position)
        {
            var actionStates = new Dictionary<
                string,
                TurnReplayActorActionState>(StringComparer.Ordinal);
            foreach (TurnReplayActorActionState state in
                TurnReplayActorActionProjector.Project(
                    position.Frame,
                    position.Progress))
            {
                actionStates[state.ActorId] = state;
            }
            foreach (KeyValuePair<string, GameplayActorSnapshot> entry in
                sample.Actors)
            {
                if (!actors.TryGetValue(
                        entry.Key,
                        out GameplayTurnReplayActorPresenter actor))
                    continue;
                actionStates.TryGetValue(
                    entry.Key,
                    out TurnReplayActorActionState action);
                TryResolveReplayLocomotion(
                    position,
                    entry.Key,
                    out Vector3 replayVelocity,
                    out bool replayGrounded);
                actor.Present(
                    entry.Value,
                    action,
                    position,
                    replayVelocity,
                    replayGrounded);
            }
        }

        private static bool TryResolveReplayLocomotion(
            GameplaySemanticReplayPlaybackPosition position,
            string actorId,
            out Vector3 velocity,
            out bool grounded)
        {
            velocity = Vector3.zero;
            grounded = true;
            if (position.Progress >= 1f
                || !(position.Frame.SemanticRecord is MovementRouteRecord route)
                || !string.Equals(route.ActorId, actorId, StringComparison.Ordinal)
                || !GameplayMovementPresentationSampler.TrySample(
                    route,
                    route.TotalPlaybackDurationSeconds * position.Progress,
                    out GameplayMovementPresentationSample sampled)
                || sampled.SegmentIndex < 0
                || sampled.SegmentIndex >= route.Segments.Count)
            {
                return false;
            }

            MovementRouteSegmentRecord segment = route.Segments[
                sampled.SegmentIndex];
            float normalizedFrameDuration = position.PlaybackFrame
                .DurationSeconds / route.TotalPlaybackDurationSeconds;
            float segmentDuration = Mathf.Max(
                0.0001f,
                segment.PlaybackDurationSeconds * normalizedFrameDuration);
            velocity = new Vector3(
                (segment.To.X - segment.From.X) / segmentDuration,
                (segment.To.Y - segment.From.Y) / segmentDuration,
                (segment.To.Z - segment.From.Z) / segmentDuration);
            grounded = !segment.IsTraversal;
            return true;
        }

        private void ClearTransients()
        {
            foreach (GameplayTurnReplayActorPresenter actor in actors.Values)
                actor.ClearTransients();
            destructibles.ClearReplayTransients();
        }

        private void Restore()
        {
            if (!presenting)
                return;
            Exception failure = null;
            foreach (GameplayTurnReplayActorPresenter actor in actors.Values)
                TryRestore(actor.Dispose, ref failure);
            TryRestore(
                () => projectiles?.EndReplayPresentation(),
                ref failure);
            TryRestore(
                () => destructibles?.RestoreAuthoritativePresentation(),
                ref failure);
            TryRestore(
                () => vehicles?.EndReplayPresentation(),
                ref failure);
            TryRestore(
                () => smoke?.EndReplayPresentation(),
                ref failure);
            TryRestore(
                () => fire?.EndReplayPresentation(),
                ref failure);
            TryRestore(
                () => drones?.EndReplayPresentation(),
                ref failure);
            TryRestore(() => cameraCuts?.End(), ref failure);
            TryRestore(() => input?.SetCameraOnly(false), ref failure);
            TryRestore(() => Time.timeScale = priorTimeScale, ref failure);
            TryRestore(
                () => partyHud?.SetPresentationSuppressed(
                    partyHudWasSuppressed),
                ref failure);
            if (gameplayHudWasVisible)
                TryRestore(() => gameplayHud?.Show(), ref failure);
            for (int index = 0;
                index < liveBehaviourEnabled.Count
                    && index < liveBehaviours.Count;
                index++)
            {
                int captured = index;
                TryRestore(
                    () => liveBehaviours[captured].enabled =
                        liveBehaviourEnabled[captured],
                    ref failure);
            }
            TryRestore(
                () => enemies?.SetReplayPaused(enemiesWerePaused),
                ref failure);
            liveBehaviourEnabled.Clear();
            actors.Clear();
            presenting = false;
            if (failure != null)
                throw failure;
        }

        private static void TryRestore(Action restore, ref Exception failure)
        {
            try
            {
                restore();
            }
            catch (Exception exception)
            {
                failure ??= exception;
            }
        }

        private static string FormatSubjectLabel(string subjectId) =>
            subjectId.Replace('-', ' ');
    }
}
