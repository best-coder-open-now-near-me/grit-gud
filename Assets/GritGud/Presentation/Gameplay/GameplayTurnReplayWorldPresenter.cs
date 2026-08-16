using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayTurnReplayWorldPresenter : IDisposable
    {
        private readonly Dictionary<string, GameplayTurnReplayActorPresenter>
            actors = new Dictionary<string, GameplayTurnReplayActorPresenter>(
                StringComparer.Ordinal);
        private GameplaySession gameplay;
        private GameplayWorldRegistry world;
        private GameplayInputController input;
        private GameplayTurnReplayHud hud;
        private GameplayProjectileController projectiles;
        private GameplayDestructibleController destructibles;
        private GameplayVehicleController vehicles;
        private GameplaySmokeFieldController smoke;
        private TurnReplayEventCrossingDetector crossings;
        private bool presenting;

        public void Bind(
            GameplaySession session,
            GameplayWorldRegistry registry,
            GameplayInputController inputController,
            GameplayTurnReplayHud replayHud,
            GameplayProjectileController projectileController,
            GameplayDestructibleController destructibleController,
            GameplayVehicleController vehicleController,
            GameplaySmokeFieldController smokeController)
        {
            Dispose();
            gameplay = session ?? throw new ArgumentNullException(nameof(session));
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
            gameplay = null;
            world = null;
            input = null;
            hud = null;
            projectiles = null;
            destructibles = null;
            vehicles = null;
            smoke = null;
        }

        private void HandleOpenChanged(bool open)
        {
            if (!open)
            {
                Restore();
                return;
            }

            actors.Clear();
            TurnReplayStateWindow stateWindow = hud.StateWindow;
            if (stateWindow == null)
                return;
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
                input.SetCameraOnly(true);
                crossings = new TurnReplayEventCrossingDetector(
                    hud.EventTimeline,
                    hud.TimeSeconds);
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
            IReadOnlyList<TurnReplayEventCrossing> crossed;
            if (hud.IsPlaying)
            {
                crossed = crossings.Advance(hud.TimeSeconds);
            }
            else
            {
                crossings.Seek(hud.TimeSeconds);
                crossed = Array.Empty<TurnReplayEventCrossing>();
                ClearTransients();
            }
            PresentCrossings(crossed);
            Present();
        }

        private void Present()
        {
            TurnReplayStateWindow window = hud.StateWindow;
            if (window == null)
                return;
            TurnReplayWorldStateSample sample =
                TurnReplayWorldStateSampler.SampleAtTime(
                    window,
                    hud.EventTimeline,
                    hud.TimeSeconds);
            PresentActors(sample, hud.TimeSeconds);
            destructibles.PresentReplay(sample.Destructibles);
            projectiles.PresentReplay(sample.Projectiles);
            vehicles.PresentReplay(sample.Vehicles);
            smoke.PresentReplay(sample.SmokeFields);
        }

        private void PresentActorsAt(float timeSeconds)
        {
            TurnReplayStateWindow window = hud.StateWindow;
            if (window == null)
                return;
            PresentActors(
                TurnReplayWorldStateSampler.SampleAtTime(
                    window,
                    hud.EventTimeline,
                    timeSeconds),
                timeSeconds);
        }

        private void PresentActors(
            TurnReplayWorldStateSample sample,
            float timeSeconds)
        {
            var actionStates = new Dictionary<
                string,
                TurnReplayActorActionState>(StringComparer.Ordinal);
            foreach (TurnReplayActorActionState state in
                TurnReplayActorActionProjector.Project(
                    hud.EventTimeline,
                    timeSeconds))
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

        private void PresentCrossings(
            IReadOnlyList<TurnReplayEventCrossing> values)
        {
            foreach (TurnReplayEventCrossing crossing in values)
            {
                PresentActorsAt(crossing.TimeSeconds);
                float progress = crossing.Boundary
                    == TurnReplayEventBoundary.Start ? 0f : 1f;
                foreach (TurnReplayActorActionState action in
                    TurnReplayActorActionProjector.Project(
                        crossing.TimedEvent,
                        progress))
                {
                    if (!actors.TryGetValue(
                            action.ActorId,
                            out GameplayTurnReplayActorPresenter actor))
                        continue;
                    actor.PresentTransient(
                        new GameplayTurnReplayTransientCue(
                            action.ActorId,
                            action.Kind,
                            crossing));
                }
            }
        }

        private void ClearTransients()
        {
            foreach (GameplayTurnReplayActorPresenter actor in actors.Values)
                actor.ClearTransients();
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
            TryRestore(() => input?.SetCameraOnly(false), ref failure);
            actors.Clear();
            crossings = null;
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
