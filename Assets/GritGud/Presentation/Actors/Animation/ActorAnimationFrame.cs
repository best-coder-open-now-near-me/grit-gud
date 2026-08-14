using GritGud.Domain.Gameplay;

namespace GritGud.Presentation.Actors.Animation
{
    public readonly struct ActorAnimationFrame
    {
        public ActorAnimationFrame(
            ActorLocomotionAnimationState locomotion,
            ActorStance stance)
        {
            Locomotion = locomotion;
            Stance = stance;
        }

        public ActorLocomotionAnimationState Locomotion { get; }

        public ActorStance Stance { get; }
    }
}
