using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors.Animation;
using UnityEngine;
using UnityEngine.Rendering;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class MovementRouteGhostPresenter : IDisposable
    {
        internal const string GhostShaderName = "GritGud/TacticalWireframe";
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Color = Shader.PropertyToID("_Color");
        private static readonly int LineColor = Shader.PropertyToID("_LineColor");
        private static readonly int FillColor = Shader.PropertyToID("_FillColor");
        private static readonly UnityEngine.Color GhostColor =
            GameplayVisualPalette.RouteGhost;
        private const float PreviewTimeScale = 1.25f;
        private const float EndpointHoldDuration = 1f;

        private GameObject ghost;
        private Material ghostMaterial;
        private ActorAnimationCoordinator animationCoordinator;
        private ActorStancePresenter sourceStancePresenter;
        private GameObject routeLineObject;
        private LineRenderer routeLine;
        private Material routeMaterial;
        private float previewSeconds;
        private float endpointHoldRemaining;
        private int presentedTraversalSegment = -1;

        public MovementRouteGhostPresenter(Transform sourceActor)
        {
            if (sourceActor == null)
            {
                throw new ArgumentNullException(nameof(sourceActor));
            }

            CreateGhost(sourceActor);
            CreateRouteLine();
            Hide();
        }

        public void Present(MovementRoutePlanner planner, float deltaTime)
        {
            if (planner == null || !planner.CanConfirm)
            {
                Hide();
                return;
            }

            routeLineObject.SetActive(true);
            var linePositions = new List<Vector3>();
            foreach (MovementRouteSegmentRecord segment in planner.Segments)
            {
                if (linePositions.Count == 0)
                    linePositions.Add(MovementRouteSampling.ToVector3(
                        segment.From));
                int subdivisions = segment.IsTraversal ? 12 : 1;
                for (int step = 1; step <= subdivisions; step++)
                {
                    linePositions.Add(MovementRouteSampling.ToVector3(
                        segment.Sample((float)step / subdivisions)));
                }
            }
            routeLine.positionCount = linePositions.Count;
            for (int index = 0; index < linePositions.Count; index++)
                routeLine.SetPosition(
                    index,
                    linePositions[index] + (Vector3.up * 0.06f));

            ghost.SetActive(true);
            bool holdingAtEndpoint = AdvancePreview(
                planner.TotalPlaybackDurationSeconds,
                Mathf.Max(0f, deltaTime));
            if (!MovementRouteSampling.TrySample(
                    planner.Segments,
                    previewSeconds,
                    out Vector3 position,
                    out Vector3 direction,
                    out int segmentIndex,
                    out _))
            {
                return;
            }

            Quaternion rotation = ghost.transform.rotation;
            Vector3 planarDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (planarDirection.sqrMagnitude > 0.0001f)
            {
                rotation = Quaternion.LookRotation(planarDirection, Vector3.up);
            }

            ghost.transform.SetPositionAndRotation(position, rotation);
            if (animationCoordinator != null &&
                animationCoordinator.Profile != null)
            {
                if (sourceStancePresenter != null)
                {
                    animationCoordinator.PresentStance(
                        sourceStancePresenter.Stance);
                }

                if (planner.Segments[segmentIndex].IsTraversal
                    && presentedTraversalSegment != segmentIndex)
                {
                    animationCoordinator.TryRequestAction(
                        ActorAnimationAction.Jump);
                    presentedTraversalSegment = segmentIndex;
                }

                ActorLocomotionAnimationState locomotion =
                    ActorLocomotionAnimationProjector.Project(
                        holdingAtEndpoint
                            ? Vector3.zero
                            : planarDirection.normalized * 2f,
                        rotation,
                        !planner.Segments[segmentIndex].IsTraversal,
                        0f,
                        animationCoordinator.Profile.LocomotionReferenceSpeed,
                        animationCoordinator.Profile.TurnReferenceDegreesPerSecond);
                animationCoordinator.PresentFrame(
                    new ActorAnimationFrame(
                        locomotion,
                        animationCoordinator.CurrentStance),
                    deltaTime);
            }
        }

        public void Hide()
        {
            if (ghost != null)
            {
                ghost.SetActive(false);
            }

            if (routeLineObject != null)
            {
                routeLineObject.SetActive(false);
            }

            previewSeconds = 0f;
            endpointHoldRemaining = 0f;
            presentedTraversalSegment = -1;
        }

        private bool AdvancePreview(float routeDuration, float deltaTime)
        {
            if (routeDuration <= 0f)
            {
                previewSeconds = 0f;
                endpointHoldRemaining = 0f;
                return false;
            }

            if (endpointHoldRemaining > 0f
                && previewSeconds < routeDuration - 0.001f)
            {
                endpointHoldRemaining = 0f;
            }

            if (endpointHoldRemaining > 0f)
            {
                previewSeconds = routeDuration;
                endpointHoldRemaining -= deltaTime;
                if (endpointHoldRemaining > 0f)
                {
                    return true;
                }

                previewSeconds = 0f;
                endpointHoldRemaining = 0f;
                presentedTraversalSegment = -1;
                return false;
            }

            previewSeconds = Mathf.Min(
                routeDuration,
                previewSeconds + (PreviewTimeScale * deltaTime));
            if (previewSeconds >= routeDuration)
            {
                endpointHoldRemaining = EndpointHoldDuration;
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            GameplayObjectLifecycle.Destroy(ghost);
            ghost = null;
            GameplayObjectLifecycle.Destroy(ghostMaterial);
            ghostMaterial = null;
            animationCoordinator = null;
            sourceStancePresenter = null;
            GameplayObjectLifecycle.Destroy(routeLineObject);
            routeLineObject = null;
            routeLine = null;
            GameplayObjectLifecycle.Destroy(routeMaterial);
            routeMaterial = null;
        }

        private void CreateGhost(Transform sourceActor)
        {
            sourceStancePresenter = sourceActor.GetComponent<ActorStancePresenter>();
            ghost = UnityEngine.Object.Instantiate(sourceActor.gameObject);
            ghost.name = "Movement Planning Ghost";

            ThirdPersonMotor motor = ghost.GetComponent<ThirdPersonMotor>();
            if (motor != null)
            {
                motor.enabled = false;
            }

            ExplorationMovementInput input =
                ghost.GetComponent<ExplorationMovementInput>();
            if (input != null)
            {
                input.enabled = false;
            }

            ActorLocomotionAnimationPresenter locomotionPresenter =
                ghost.GetComponent<ActorLocomotionAnimationPresenter>();
            if (locomotionPresenter != null)
            {
                locomotionPresenter.enabled = false;
            }

            foreach (Collider collider in ghost.GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }

            ghostMaterial = CreateGhostMaterial();
            foreach (Renderer renderer in ghost.GetComponentsInChildren<Renderer>())
            {
                int materialCount = ResolveSubMeshCount(renderer);
                var materials = new Material[materialCount];
                for (int index = 0; index < materials.Length; index++)
                {
                    materials[index] = ghostMaterial;
                }

                renderer.sharedMaterials = materials;
                renderer.SetPropertyBlock(null);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            animationCoordinator =
                ghost.GetComponent<ActorAnimationCoordinator>();
        }

        private static int ResolveSubMeshCount(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinnedRenderer &&
                skinnedRenderer.sharedMesh != null)
            {
                return Mathf.Max(1, skinnedRenderer.sharedMesh.subMeshCount);
            }

            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            return meshFilter != null && meshFilter.sharedMesh != null
                ? Mathf.Max(1, meshFilter.sharedMesh.subMeshCount)
                : Mathf.Max(1, renderer.sharedMaterials.Length);
        }

        private static Material CreateGhostMaterial()
        {
            Shader shader = Shader.Find(GhostShaderName)
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "No compatible movement ghost shader is available.");
            }

            var material = new Material(shader)
            {
                name = "Tactical Movement Ghost",
                hideFlags = HideFlags.HideAndDontSave,
            };
            if (material.HasProperty(BaseColor))
            {
                material.SetColor(BaseColor, GhostColor);
            }

            if (material.HasProperty(Color))
            {
                material.SetColor(Color, GhostColor);
            }

            if (material.HasProperty(LineColor))
            {
                material.SetColor(LineColor, GameplayVisualPalette.RouteLine);
            }

            if (material.HasProperty(FillColor))
            {
                material.SetColor(FillColor, GameplayVisualPalette.RouteFill);
            }

            return material;
        }

        private void CreateRouteLine()
        {
            routeLineObject = new GameObject("Planned Movement Route");
            routeLine = routeLineObject.AddComponent<LineRenderer>();
            routeLine.useWorldSpace = true;
            routeLine.startWidth = 0.08f;
            routeLine.endWidth = 0.08f;
            routeLine.startColor = GhostColor;
            routeLine.endColor = GhostColor;
            routeLine.numCapVertices = 4;
            routeLine.numCornerVertices = 2;
            Shader shader = Shader.Find("Sprites/Default")
                ?? Shader.Find("UI/Default");
            if (shader != null)
            {
                routeMaterial = new Material(shader)
                {
                    name = "Planned Movement Route Material",
                    color = GhostColor,
                };
                routeLine.sharedMaterial = routeMaterial;
            }
        }
    }
}
