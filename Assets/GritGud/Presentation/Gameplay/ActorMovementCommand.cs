using System;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    public readonly struct ActorMovementCommand
    {
        public ActorMovementCommand(Vector3 worldDirection, bool sprint)
        {
            if (!IsFinite(worldDirection))
            {
                throw new ArgumentException(
                    "Movement direction must contain only finite values.",
                    nameof(worldDirection));
            }

            worldDirection.y = 0f;
            WorldDirection = Vector3.ClampMagnitude(worldDirection, 1f);
            Sprint = sprint;
        }

        public Vector3 WorldDirection { get; }

        public bool Sprint { get; }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public interface IActorMovementCommandSource
    {
        ActorMovementCommand ReadMovementCommand();
    }
}
