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
        private sealed class OptionalWorldProjection
        {
            private readonly string name;
            private readonly Action begin;
            private readonly Action<GameplayPresentationWorldStateSample> present;
            private readonly Action clearTransients;
            private readonly Action end;
            private bool active;
            private bool started;

            public OptionalWorldProjection(
                string projectionName,
                Action beginPresentation,
                Action<GameplayPresentationWorldStateSample> presentSample,
                Action clearPresentationTransients,
                Action endPresentation)
            {
                name = projectionName;
                begin = beginPresentation ?? throw new ArgumentNullException(
                    nameof(beginPresentation));
                present = presentSample ?? throw new ArgumentNullException(
                    nameof(presentSample));
                clearTransients = clearPresentationTransients
                    ?? throw new ArgumentNullException(
                        nameof(clearPresentationTransients));
                end = endPresentation ?? throw new ArgumentNullException(
                    nameof(endPresentation));
            }

            public void Begin()
            {
                active = true;
                started = true;
                try
                {
                    begin();
                }
                catch (Exception exception)
                {
                    Disable("start", exception);
                }
            }

            public void Present(GameplayPresentationWorldStateSample sample)
            {
                if (!active) return;
                try
                {
                    present(sample);
                }
                catch (Exception exception)
                {
                    Disable("sample", exception);
                }
            }

            public void ClearTransients()
            {
                if (!active) return;
                try
                {
                    clearTransients();
                }
                catch (Exception exception)
                {
                    Disable("clear transients", exception);
                }
            }

            public void End()
            {
                if (!started) return;
                try
                {
                    end();
                }
                catch (Exception exception)
                {
                    Warn("restore", exception);
                }
                finally
                {
                    active = false;
                    started = false;
                }
            }

            private void Disable(string operation, Exception exception)
            {
                active = false;
                if (started)
                {
                    try
                    {
                        end();
                    }
                    catch (Exception restoreException)
                    {
                        Warn("restore after failure", restoreException);
                    }
                }
                started = false;
                Warn(operation, exception);
            }

            private void Warn(string operation, Exception exception) =>
                Debug.LogWarning(
                    $"Replay {name} presentation could not {operation} and "
                    + $"was disabled for this playback: {exception.Message}");
        }

        private readonly Dictionary<string, GameplayTurnReplayActorPresenter>
            actors = new Dictionary<string, GameplayTurnReplayActorPresenter>(
                StringComparer.Ordinal);
        private readonly List<OptionalWorldProjection> optionalProjections =
            new List<OptionalWorldProjection>();
        private readonly List<Behaviour> liveBehaviours =
            new List<Behaviour>();
        private readonly List<bool> liveBehaviourEnabled =
            new List<bool>();
        private GameplayWorldRegistry world;
        private GameplayInputController input;
        private GameplayTurnReplayHud hud;
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
            RegisterOptionalProjections(
                projectileController,
                destructibleController,
                vehicleController,
                smokeController,
                fireController);
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
            gameplayHud = null;
            partyHud = null;
            enemies = null;
            optionalProjections.Clear();
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
                foreach (OptionalWorldProjection projection in
                    optionalProjections)
                {
                    projection.Begin();
                }
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
            PresentActors(sample, position);
            foreach (OptionalWorldProjection projection in optionalProjections)
                projection.Present(sample);
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
            foreach (OptionalWorldProjection projection in optionalProjections)
                projection.ClearTransients();
        }

        private void Restore()
        {
            if (!presenting)
                return;
            Exception failure = null;
            foreach (GameplayTurnReplayActorPresenter actor in actors.Values)
                TryRestore(actor.Dispose, ref failure);
            for (int index = optionalProjections.Count - 1;
                index >= 0;
                index--)
            {
                optionalProjections[index].End();
            }
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
            {
                Debug.LogWarning(
                    "Replay restored its core state but an optional live "
                    + $"presentation detail failed to restore: {failure.Message}");
            }
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

        private void RegisterOptionalProjections(
            GameplayProjectileController projectileController,
            GameplayDestructibleController destructibleController,
            GameplayVehicleController vehicleController,
            GameplaySmokeFieldController smokeController,
            GameplayFireFieldController fireController)
        {
            optionalProjections.Clear();
            if (projectileController != null)
            {
                optionalProjections.Add(new OptionalWorldProjection(
                    "projectile",
                    projectileController.BeginReplayPresentation,
                    sample => projectileController.PresentReplay(
                        sample.Projectiles),
                    () => { },
                    projectileController.EndReplayPresentation));
            }
            if (destructibleController != null)
            {
                optionalProjections.Add(new OptionalWorldProjection(
                    "destructible",
                    destructibleController.ClearReplayTransients,
                    sample => destructibleController.PresentReplay(
                        sample.Destructibles),
                    destructibleController.ClearReplayTransients,
                    destructibleController.RestoreAuthoritativePresentation));
            }
            if (vehicleController != null)
            {
                optionalProjections.Add(new OptionalWorldProjection(
                    "vehicle",
                    vehicleController.BeginReplayPresentation,
                    sample => vehicleController.PresentReplay(sample.Vehicles),
                    () => { },
                    vehicleController.EndReplayPresentation));
            }
            if (smokeController != null)
            {
                optionalProjections.Add(new OptionalWorldProjection(
                    "smoke-field",
                    smokeController.BeginReplayPresentation,
                    sample => smokeController.PresentReplay(sample.SmokeFields),
                    () => { },
                    smokeController.EndReplayPresentation));
            }
            if (fireController != null)
            {
                optionalProjections.Add(new OptionalWorldProjection(
                    "fire-field",
                    fireController.BeginReplayPresentation,
                    sample => fireController.PresentReplay(sample.FireFields),
                    () => { },
                    fireController.EndReplayPresentation));
            }
        }
    }
}
