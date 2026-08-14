using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    public readonly struct ActorLocomotionSnapshot
    {
        public ActorLocomotionSnapshot(
            Vector3 velocity,
            Quaternion rotation,
            bool grounded,
            float turnDegreesPerSecond)
        {
            Velocity = velocity;
            Rotation = rotation;
            Grounded = grounded;
            TurnDegreesPerSecond = turnDegreesPerSecond;
        }

        public Vector3 Velocity { get; }

        public Quaternion Rotation { get; }

        public bool Grounded { get; }

        public float TurnDegreesPerSecond { get; }
    }
}
