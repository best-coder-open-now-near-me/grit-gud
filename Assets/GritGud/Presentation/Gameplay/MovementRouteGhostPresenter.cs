using System;
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
        private const float PreviewSpeed = 2f;
        private const float EndpointHoldDuration = 1f;

        private GameObject ghost;
        private Material ghostMaterial;
        private ActorAnimationCoordinator animationCoordinator;
        private ActorStancePresenter sourceStancePresenter;
        private GameObject routeLineObject;
        private LineRenderer routeLine;
        private Material routeMaterial;
        private float previewDistance;
        private float endpointHoldRemaining;

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
            routeLine.positionCount = planner.Points.Count;
            for (int index = 0; index < planner.Points.Count; index++)
            {
                routeLine.SetPosition(
                    index,
                    MovementRouteSampling.ToVector3(planner.Points[index])
                        + (Vector3.up * 0.06f));
            }

            ghost.SetActive(true);
            bool holdingAtEndpoint = AdvancePreview(
                planner.TotalCost,
                Mathf.Max(0f, deltaTime));
            if (!MovementRouteSampling.TrySample(
                    planner.Points,
                    previewDistance,
                    out Vector3 position,
                    out Vector3 direction))
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

                ActorLocomotionAnimationState locomotion =
                    ActorLocomotionAnimationProjector.Project(
                    holdingAtEndpoint
                        ? Vector3.zero
                        : planarDirection.normalized * PreviewSpeed,
                    rotation,
                    true,
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

            previewDistance = 0f;
            endpointHoldRemaining = 0f;
        }

        private bool AdvancePreview(float routeLength, float deltaTime)
        {
            if (routeLength <= 0f)
            {
                previewDistance = 0f;
                endpointHoldRemaining = 0f;
                return false;
            }

            if (endpointHoldRemaining > 0f
                && previewDistance < routeLength - 0.001f)
            {
                endpointHoldRemaining = 0f;
            }

            if (endpointHoldRemaining > 0f)
            {
                previewDistance = routeLength;
                endpointHoldRemaining -= deltaTime;
                if (endpointHoldRemaining > 0f)
                {
                    return true;
                }

                previewDistance = 0f;
                endpointHoldRemaining = 0f;
                return false;
            }

            previewDistance = Mathf.Min(
                routeLength,
                previewDistance + (PreviewSpeed * deltaTime));
            if (previewDistance >= routeLength)
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
