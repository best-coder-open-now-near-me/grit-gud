using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayTurnReplayWorldPresenter : IDisposable
    {
        private readonly Dictionary<string, OriginalActorState> originals =
            new Dictionary<string, OriginalActorState>(StringComparer.Ordinal);
        private GameplaySession gameplay;
        private GameplayWorldRegistry world;
        private GameplayInputController input;
        private GameplayTurnReplayHud hud;
        private GameplayProjectileController projectiles;
        private GameplayDestructibleController destructibles;
        private bool presenting;

        public void Bind(
            GameplaySession session,
            GameplayWorldRegistry registry,
            GameplayInputController inputController,
            GameplayTurnReplayHud replayHud,
            GameplayProjectileController projectileController,
            GameplayDestructibleController destructibleController)
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
        }

        private void HandleOpenChanged(bool open)
        {
            if (!open)
            {
                Restore();
                return;
            }

            originals.Clear();
            TurnReplayStateWindow stateWindow = hud.StateWindow;
            if (stateWindow == null)
                return;
            foreach (GameplayActorView actor in world.Actors)
            {
                originals.Add(
                    actor.ActorId,
                    new OriginalActorState(
                        actor.Transform.position,
                        actor.Transform.rotation,
                        actor.Stance.Stance));
                actor.Motor?.StopPlanarMovement();
            }
            projectiles.BeginReplayPresentation();
            presenting = true;
            input.SetCameraOnly(true);
            Present(hud.Playhead);
        }

        private void HandlePlayheadChanged(float playhead)
        {
            if (presenting)
                Present(playhead);
        }

        private void Present(float playhead)
        {
            TurnReplayStateWindow window = hud.StateWindow;
            if (window == null)
                return;
            TurnReplayWorldStateSample sample =
                TurnReplayWorldStateSampler.Sample(window, playhead);
            foreach (KeyValuePair<string, GameplayActorSnapshot> entry in
                sample.Actors)
            {
                if (!world.TryGetActor(entry.Key, out GameplayActorView actor))
                    continue;
                GameplayActorPose pose = entry.Value.Pose;
                actor.Transform.SetPositionAndRotation(
                    new Vector3(
                        pose.Position.X,
                        pose.Position.Y,
                        pose.Position.Z),
                    Quaternion.Euler(0f, pose.FacingDegrees, 0f));
                if (actor.Stance.Stance != pose.Stance)
                    actor.Stance.ApplyResolved(pose.Stance);
            }
            destructibles.PresentReplay(sample.Destructibles);
            projectiles.PresentReplay(sample.Projectiles);
        }

        private void Restore()
        {
            if (!presenting)
                return;
            foreach (KeyValuePair<string, OriginalActorState> entry in originals)
            {
                if (!world.TryGetActor(entry.Key, out GameplayActorView actor))
                    continue;
                actor.Transform.SetPositionAndRotation(
                    entry.Value.Position,
                    entry.Value.Rotation);
                if (actor.Stance.Stance != entry.Value.Stance)
                    actor.Stance.ApplyResolved(entry.Value.Stance);
            }
            input?.SetCameraOnly(false);
            projectiles?.EndReplayPresentation();
            destructibles?.RestoreAuthoritativePresentation();
            originals.Clear();
            presenting = false;
        }

        private readonly struct OriginalActorState
        {
            public OriginalActorState(
                Vector3 position,
                Quaternion rotation,
                ActorStance stance)
            {
                Position = position;
                Rotation = rotation;
                Stance = stance;
            }

            public Vector3 Position { get; }

            public Quaternion Rotation { get; }

            public ActorStance Stance { get; }
        }
    }
}
