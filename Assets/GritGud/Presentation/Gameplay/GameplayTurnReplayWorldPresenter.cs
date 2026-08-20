using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;

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
        private GameplayWorldRegistry world;
        private GameplayInputController input;
        private GameplayTurnReplayHud hud;
        private GameplayProjectileController projectiles;
        private GameplayDestructibleController destructibles;
        private GameplayVehicleController vehicles;
        private GameplaySmokeFieldController smoke;
        private GameplayFireFieldController fire;
        private GameplayDroneController drones;
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
            GameplayDroneController droneController)
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
                destructibles.ClearReplayTransients();
                input.SetCameraOnly(true);
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
                actor.Present(entry.Value, action);
            }
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
            TryRestore(() => input?.SetCameraOnly(false), ref failure);
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
    }
}
