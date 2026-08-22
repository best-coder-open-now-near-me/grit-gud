using System;
using System.Collections.Generic;
using GritGud.Presentation.Actors;
using GritGud.Presentation.Actors.Animation;
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
                ActorAnimationCoordinator animation = actor.Root.GetComponent<
                    ActorAnimationCoordinator>();
                views.Add(new GroundingView(
                    actor.Transform,
                    quad.transform,
                    animation,
                    actor.Root.GetComponent<ActorRagdollPresenter>(),
                    ResolveBodyAnchors(animation?.TargetAnimator)));
            }

            enabled = true;
            RefreshNow();
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

        private void LateUpdate() => RefreshNow();

        internal void RefreshNow()
        {
            if (definition == null)
            {
                return;
            }

            foreach (GroundingView view in views)
            {
                if (view.Actor == null || view.Visual == null)
                {
                    continue;
                }

                if (IsTerminal(view)
                    && TryResolveBodyFootprint(
                        view,
                        definition.Diameter,
                        out Vector3 center,
                        out Quaternion rotation,
                        out Vector3 scale))
                {
                    center.y = view.Actor.position.y
                        + definition.HeightOffset;
                    view.Visual.SetPositionAndRotation(center, rotation);
                    view.Visual.localScale = scale;
                    continue;
                }

                Vector3 standingPosition = view.Actor.position;
                standingPosition.y += definition.HeightOffset;
                view.Visual.SetPositionAndRotation(
                    standingPosition,
                    Quaternion.Euler(-90f, 0f, 0f));
                view.Visual.localScale = Vector3.one * definition.Diameter;
            }
        }

        private void OnDestroy() => Unbind();

        private static bool IsTerminal(GroundingView view)
        {
            if (view.Animation?.IsPresentingReplay == true)
            {
                return IsTerminalAction(view.Animation.ReplayAction);
            }
            return view.Ragdoll?.IsRagdollActive == true
                || view.Ragdoll?.HasPendingActivation == true
                || IsTerminalAction(view.Animation?.LastRequestedAction);
        }

        private static bool IsTerminalAction(ActorAnimationAction? action) =>
            action == ActorAnimationAction.Incapacitate
            || action == ActorAnimationAction.IncapacitateShoulder;

        private static bool TryResolveBodyFootprint(
            GroundingView view,
            float standingDiameter,
            out Vector3 center,
            out Quaternion rotation,
            out Vector3 scale)
        {
            center = view.Actor.position;
            rotation = Quaternion.Euler(-90f, 0f, 0f);
            scale = Vector3.one * standingDiameter;
            if (view.BodyAnchors.Count < 2)
                return false;

            Transform first = null;
            Transform second = null;
            float maximumDistance = 0f;
            for (int left = 0; left < view.BodyAnchors.Count; left++)
            {
                Transform leftAnchor = view.BodyAnchors[left];
                if (leftAnchor == null)
                    continue;
                for (int right = left + 1;
                    right < view.BodyAnchors.Count;
                    right++)
                {
                    Transform rightAnchor = view.BodyAnchors[right];
                    if (rightAnchor == null)
                        continue;
                    Vector3 offset = rightAnchor.position
                        - leftAnchor.position;
                    offset.y = 0f;
                    float distance = offset.sqrMagnitude;
                    if (distance <= maximumDistance)
                        continue;
                    maximumDistance = distance;
                    first = leftAnchor;
                    second = rightAnchor;
                }
            }
            if (first == null || second == null)
                return false;

            Vector3 major = second.position - first.position;
            major.y = 0f;
            if (major.sqrMagnitude < 0.0001f)
            {
                major = view.Actor.forward;
                major.y = 0f;
            }
            if (major.sqrMagnitude < 0.0001f)
                major = Vector3.forward;
            major.Normalize();
            Vector3 minor = new Vector3(-major.z, 0f, major.x);
            float minimumMajor = float.PositiveInfinity;
            float maximumMajor = float.NegativeInfinity;
            float minimumMinor = float.PositiveInfinity;
            float maximumMinor = float.NegativeInfinity;
            foreach (Transform anchor in view.BodyAnchors)
            {
                if (anchor == null)
                    continue;
                float majorPosition = Vector3.Dot(anchor.position, major);
                float minorPosition = Vector3.Dot(anchor.position, minor);
                minimumMajor = Mathf.Min(minimumMajor, majorPosition);
                maximumMajor = Mathf.Max(maximumMajor, majorPosition);
                minimumMinor = Mathf.Min(minimumMinor, minorPosition);
                maximumMinor = Mathf.Max(maximumMinor, minorPosition);
            }

            float majorSpan = maximumMajor - minimumMajor;
            float minorSpan = maximumMinor - minimumMinor;
            float length = Mathf.Max(
                standingDiameter,
                majorSpan + standingDiameter * 0.35f);
            float width = Mathf.Min(
                length,
                Mathf.Max(
                    standingDiameter * 0.55f,
                    minorSpan + standingDiameter * 0.25f));
            center = major * ((minimumMajor + maximumMajor) * 0.5f)
                + minor * ((minimumMinor + maximumMinor) * 0.5f);
            center.y = view.Actor.position.y;
            rotation = Quaternion.LookRotation(Vector3.up, major);
            scale = new Vector3(width, length, 1f);
            return true;
        }

        private static IReadOnlyList<Transform> ResolveBodyAnchors(
            Animator animator)
        {
            if (animator == null || !animator.isHuman)
                return Array.Empty<Transform>();
            HumanBodyBones[] bodyBones =
            {
                HumanBodyBones.Hips,
                HumanBodyBones.Chest,
                HumanBodyBones.UpperChest,
                HumanBodyBones.Head,
                HumanBodyBones.LeftHand,
                HumanBodyBones.RightHand,
                HumanBodyBones.LeftFoot,
                HumanBodyBones.RightFoot,
            };
            var anchors = new List<Transform>(bodyBones.Length);
            var unique = new HashSet<Transform>();
            foreach (HumanBodyBones bodyBone in bodyBones)
            {
                Transform anchor = animator.GetBoneTransform(bodyBone);
                if (anchor != null && unique.Add(anchor))
                    anchors.Add(anchor);
            }
            return anchors.AsReadOnly();
        }

        private sealed class GroundingView
        {
            public GroundingView(
                Transform actor,
                Transform visual,
                ActorAnimationCoordinator animation,
                ActorRagdollPresenter ragdoll,
                IReadOnlyList<Transform> bodyAnchors)
            {
                Actor = actor;
                Visual = visual;
                Animation = animation;
                Ragdoll = ragdoll;
                BodyAnchors = bodyAnchors;
            }

            public Transform Actor { get; }
            public Transform Visual { get; }
            public ActorAnimationCoordinator Animation { get; }
            public ActorRagdollPresenter Ragdoll { get; }
            public IReadOnlyList<Transform> BodyAnchors { get; }
        }
    }
}
