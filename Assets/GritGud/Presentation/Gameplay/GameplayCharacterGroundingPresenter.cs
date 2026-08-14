using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    internal sealed class GameplayCharacterGroundingPresenter : MonoBehaviour
    {
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int EdgeSoftness = Shader.PropertyToID("_EdgeSoftness");

        private readonly List<GroundingView> views = new List<GroundingView>();
        private GameObject root;
        private Material material;
        private CharacterGroundingPresentationDefinition definition;

        public void Bind(
            GameplayWorldRegistry registry,
            GameplayVisualTheme theme,
            Transform parent)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }
            if (theme == null)
            {
                throw new ArgumentNullException(nameof(theme));
            }
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            Unbind();
            definition = theme.Grounding;
            if (!definition.Enabled)
            {
                return;
            }

            Shader shader = Shader.Find("GritGud/ContactGrounding");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "The contact-grounding shader could not be loaded.");
            }

            material = new Material(shader)
            {
                name = "Character Contact Grounding",
                hideFlags = HideFlags.HideAndDontSave,
            };
            Color groundingColor = definition.Color;
            groundingColor.a = definition.Opacity;
            material.SetColor(ColorProperty, groundingColor);
            material.SetFloat(EdgeSoftness, definition.EdgeSoftness);

            root = new GameObject("Gameplay Character Grounding");
            root.transform.SetParent(parent, false);
            foreach (GameplayActorView actor in registry.Actors)
            {
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = actor.ActorId + " Contact Grounding";
                quad.transform.SetParent(root.transform, false);
                Collider collider = quad.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = false;
                    GameplayObjectLifecycle.Destroy(collider);
                }
                MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                views.Add(new GroundingView(actor.Transform, quad.transform));
            }

            enabled = true;
            RefreshViews();
        }

        public void Unbind()
        {
            views.Clear();
            GameplayObjectLifecycle.Destroy(root);
            GameplayObjectLifecycle.Destroy(material);
            root = null;
            material = null;
            definition = null;
            enabled = false;
        }

        private void LateUpdate() => RefreshViews();

        private void RefreshViews()
        {
            if (definition == null)
            {
                return;
            }

            Vector3 scale = Vector3.one * definition.Diameter;
            foreach (GroundingView view in views)
            {
                if (view.Actor == null || view.Visual == null)
                {
                    continue;
                }

                Vector3 position = view.Actor.position;
                position.y += definition.HeightOffset;
                view.Visual.SetPositionAndRotation(
                    position,
                    Quaternion.Euler(-90f, 0f, 0f));
                view.Visual.localScale = scale;
            }
        }

        private void OnDestroy() => Unbind();

        private readonly struct GroundingView
        {
            public GroundingView(Transform actor, Transform visual)
            {
                Actor = actor;
                Visual = visual;
            }

            public Transform Actor { get; }
            public Transform Visual { get; }
        }
    }
}
