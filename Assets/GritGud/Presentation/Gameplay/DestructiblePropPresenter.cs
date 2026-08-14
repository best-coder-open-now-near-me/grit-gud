using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class DestructiblePropPresenter : MonoBehaviour
    {
        public const float DamagedHeightFraction = 0.55f;

        private readonly List<ChildTransformState> childStates =
            new List<ChildTransformState>();
        private readonly List<ComponentEnabledState<Collider>> colliderStates =
            new List<ComponentEnabledState<Collider>>();
        private readonly List<ComponentEnabledState<Renderer>> rendererStates =
            new List<ComponentEnabledState<Renderer>>();
        private string propId;

        public bool IsBound => !string.IsNullOrEmpty(propId);

        public DestructiblePropState State { get; private set; }

        public void Bind(DestructiblePropSnapshot snapshot)
        {
            Unbind();
            propId = snapshot.PropId;
            for (int index = 0; index < transform.childCount; index++)
            {
                Transform child = transform.GetChild(index);
                childStates.Add(new ChildTransformState(
                    child,
                    child.localPosition,
                    child.localScale));
            }

            foreach (Collider propCollider in GetComponentsInChildren<Collider>(true))
            {
                colliderStates.Add(new ComponentEnabledState<Collider>(
                    propCollider,
                    propCollider.enabled));
            }

            foreach (Renderer propRenderer in GetComponentsInChildren<Renderer>(true))
            {
                rendererStates.Add(new ComponentEnabledState<Renderer>(
                    propRenderer,
                    propRenderer.enabled));
            }

            Present(snapshot);
        }

        public void Present(DestructiblePropSnapshot snapshot)
        {
            if (!IsBound)
            {
                throw new InvalidOperationException(
                    "Destructible presentation must be bound before use.");
            }

            if (!string.Equals(propId, snapshot.PropId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A destructible presenter cannot change prop identity.",
                    nameof(snapshot));
            }

            bool destroyed = snapshot.State == DestructiblePropState.Destroyed;
            foreach (ComponentEnabledState<Collider> state in colliderStates)
            {
                if (state.Component != null)
                {
                    state.Component.enabled = !destroyed && state.Enabled;
                }
            }

            foreach (ComponentEnabledState<Renderer> state in rendererStates)
            {
                if (state.Component != null)
                {
                    state.Component.enabled = !destroyed && state.Enabled;
                }
            }

            float heightFraction = snapshot.State == DestructiblePropState.Damaged
                ? DamagedHeightFraction
                : 1f;
            foreach (ChildTransformState state in childStates)
            {
                if (state.Transform == null)
                {
                    continue;
                }

                state.Transform.localPosition = state.LocalPosition;
                state.Transform.localScale = new Vector3(
                    state.LocalScale.x,
                    state.LocalScale.y * heightFraction,
                    state.LocalScale.z);
            }

            State = snapshot.State;
        }

        public void Unbind()
        {
            foreach (ChildTransformState state in childStates)
            {
                if (state.Transform != null)
                {
                    state.Transform.localPosition = state.LocalPosition;
                    state.Transform.localScale = state.LocalScale;
                }
            }

            foreach (ComponentEnabledState<Collider> state in colliderStates)
            {
                if (state.Component != null)
                {
                    state.Component.enabled = state.Enabled;
                }
            }

            foreach (ComponentEnabledState<Renderer> state in rendererStates)
            {
                if (state.Component != null)
                {
                    state.Component.enabled = state.Enabled;
                }
            }

            propId = null;
            childStates.Clear();
            colliderStates.Clear();
            rendererStates.Clear();
        }

        private readonly struct ChildTransformState
        {
            public ChildTransformState(
                Transform target,
                Vector3 localPosition,
                Vector3 localScale)
            {
                Transform = target;
                LocalPosition = localPosition;
                LocalScale = localScale;
            }

            public Transform Transform { get; }

            public Vector3 LocalPosition { get; }

            public Vector3 LocalScale { get; }
        }

        private readonly struct ComponentEnabledState<T>
            where T : Component
        {
            public ComponentEnabledState(T component, bool enabled)
            {
                Component = component;
                Enabled = enabled;
            }

            public T Component { get; }

            public bool Enabled { get; }
        }
    }
}
