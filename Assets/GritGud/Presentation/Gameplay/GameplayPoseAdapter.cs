using System;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal static class GameplayPoseAdapter
    {
        public static GameplayActorPose FromTransform(
            Transform transform,
            ActorStance stance = ActorStance.Standing)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            Vector3 position = transform.position;
            return new GameplayActorPose(
                new GameplayPosition(position.x, position.y, position.z),
                transform.eulerAngles.y,
                stance);
        }
    }
}
