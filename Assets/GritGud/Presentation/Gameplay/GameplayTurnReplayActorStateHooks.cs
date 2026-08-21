using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    /// <summary>
    /// Seekable actor-local extension point for presentation variants that are
    /// not represented by the shared Animator profile.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayTurnReplayActorStateHooks : MonoBehaviour
    {
        public event Action<TurnReplayActorActionState> StatePresented;

        public event Action Cleared;

        public event Action<ActorPinState> PinStatePresented;

        public TurnReplayActorActionState CurrentState { get; private set; }

        public ActorPinState CurrentPinState { get; private set; }

        internal void Present(TurnReplayActorActionState state)
        {
            CurrentState = state;
            if (state == null)
                Cleared?.Invoke();
            else
                StatePresented?.Invoke(state);
        }

        internal void Clear()
        {
            if (CurrentState == null)
                return;
            CurrentState = null;
            Cleared?.Invoke();
        }

        internal void PresentPinState(ActorPinState state)
        {
            CurrentPinState = state;
            PinStatePresented?.Invoke(state);
        }
    }
}
