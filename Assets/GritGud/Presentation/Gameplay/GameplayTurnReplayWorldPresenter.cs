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
        private readonly Dictionary<string, GameplayActorPose> finalPoses =
            new Dictionary<string, GameplayActorPose>(StringComparer.Ordinal);
        private GameplaySession gameplay;
        private GameplayWorldRegistry world;
        private GameplayInputController input;
        private GameplayTurnReplayHud hud;
        private bool presenting;

        public void Bind(
            GameplaySession session,
            GameplayWorldRegistry registry,
            GameplayInputController inputController,
            GameplayTurnReplayHud replayHud)
        {
            Dispose();
            gameplay = session ?? throw new ArgumentNullException(nameof(session));
            world = registry ?? throw new ArgumentNullException(nameof(registry));
            input = inputController ?? throw new ArgumentNullException(
                nameof(inputController));
            hud = replayHud ?? throw new ArgumentNullException(nameof(replayHud));
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
        }

        private void HandleOpenChanged(bool open)
        {
            if (!open)
            {
                Restore();
                return;
            }

            originals.Clear();
            finalPoses.Clear();
            foreach (GameplayActorView actor in world.Actors)
            {
                originals.Add(
                    actor.ActorId,
                    new OriginalActorState(
                        actor.Transform.position,
                        actor.Transform.rotation,
                        actor.Stance.Stance));
                finalPoses.Add(actor.ActorId, gameplay.GetActor(actor.ActorId).Pose);
                actor.Motor?.StopPlanarMovement();
            }
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
            TurnReplayWindow window = hud.Window;
            if (window == null)
                return;
            IReadOnlyDictionary<string, GameplayActorPose> poses =
                TurnReplayPoseProjector.Project(window, finalPoses, playhead);
            foreach (KeyValuePair<string, GameplayActorPose> entry in poses)
            {
                if (!world.TryGetActor(entry.Key, out GameplayActorView actor))
                    continue;
                GameplayActorPose pose = entry.Value;
                actor.Transform.SetPositionAndRotation(
                    new Vector3(
                        pose.Position.X,
                        pose.Position.Y,
                        pose.Position.Z),
                    Quaternion.Euler(0f, pose.FacingDegrees, 0f));
                if (actor.Stance.Stance != pose.Stance)
                    actor.Stance.ApplyResolved(pose.Stance);
            }
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
            originals.Clear();
            finalPoses.Clear();
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
