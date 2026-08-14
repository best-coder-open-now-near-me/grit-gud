using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class VehicleMomentumEnvelopePresenter : MonoBehaviour
    {
        private const string EnvelopeShaderName = "GritGud/EmissiveSurface";
        private const int ArcSegments = 18;
        private const float GroundOffset = 0.07f;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int EmissionIntensity =
            Shader.PropertyToID("_EmissionIntensity");

        private VehicleMomentumSession session;
        private LineRenderer outerBoundary;
        private LineRenderer brakingBoundary;
        private LineRenderer forwardLine;
        private Material material;
        private bool hasBrakingBoundary;
        private bool presentationEnabled;

        public bool IsBound => session != null;

        public bool PresentationEnabled => presentationEnabled;

        public int OuterBoundaryPointCount =>
            outerBoundary != null ? outerBoundary.positionCount : 0;

        public void Bind(VehicleMomentumSession momentumSession)
        {
            session = momentumSession ??
                throw new ArgumentNullException(nameof(momentumSession));
            presentationEnabled = false;
            EnsureResources();
            RefreshNow();
            enabled = true;
        }

        public void RefreshNow()
        {
            if (session == null)
            {
                return;
            }

            VehicleMovementEnvelope envelope = session.CreateEnvelope();
            SetPoints(outerBoundary, envelope.CreateBoundary(ArcSegments));
            if (envelope.MinimumDistance > 0.001f)
            {
                SetPoints(
                    brakingBoundary,
                    envelope.CreateMinimumDistanceArc(ArcSegments));
                hasBrakingBoundary = true;
            }
            else
            {
                hasBrakingBoundary = false;
            }

            VehicleMomentumState state = envelope.State;
            double radians = state.ForwardDegrees * (Math.PI / 180d);
            var forwardEnd = new GameplayPosition(
                state.Position.X
                    + (float)(Math.Sin(radians) * envelope.MaximumDistance),
                state.Position.Y,
                state.Position.Z
                    + (float)(Math.Cos(radians) * envelope.MaximumDistance));
            SetPoints(
                forwardLine,
                new[] { state.Position, forwardEnd });
            ApplyPresentationVisibility();
        }

        public void SetPresentationEnabled(bool isEnabled)
        {
            presentationEnabled = isEnabled;
            ApplyPresentationVisibility();
        }

        public void Unbind()
        {
            session = null;
            DestroyLine(outerBoundary);
            DestroyLine(brakingBoundary);
            DestroyLine(forwardLine);
            outerBoundary = null;
            brakingBoundary = null;
            forwardLine = null;
            GameplayObjectLifecycle.Destroy(material);
            material = null;
            hasBrakingBoundary = false;
            presentationEnabled = false;
            enabled = false;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void EnsureResources()
        {
            if (material != null)
            {
                return;
            }

            Shader shader = Shader.Find(EnvelopeShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    $"Vehicle envelope shader '{EnvelopeShaderName}' was not found.");
            }

            material = new Material(shader)
            {
                name = "Vehicle Momentum Envelope",
                hideFlags = HideFlags.HideAndDontSave,
            };
            material.SetColor(BaseColor, GameplayVisualPalette.EmissionBase);
            material.SetColor(EmissionColor, GameplayVisualPalette.SignalBlueGlow);
            material.SetFloat(EmissionIntensity, 2.4f);
            outerBoundary = CreateLine("Maximum Reach", 0.045f);
            brakingBoundary = CreateLine("Minimum Braking Reach", 0.025f);
            forwardLine = CreateLine("Current Forward", 0.025f);
        }

        private LineRenderer CreateLine(string lineName, float width)
        {
            var lineObject = new GameObject(lineName);
            lineObject.transform.SetParent(transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = material;
            line.widthMultiplier = width;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return line;
        }

        private void ApplyPresentationVisibility()
        {
            if (outerBoundary != null)
            {
                outerBoundary.enabled = presentationEnabled;
            }

            if (brakingBoundary != null)
            {
                brakingBoundary.enabled =
                    presentationEnabled && hasBrakingBoundary;
            }

            if (forwardLine != null)
            {
                forwardLine.enabled = presentationEnabled;
            }
        }

        private static void SetPoints(
            LineRenderer line,
            IReadOnlyList<GameplayPosition> points)
        {
            line.positionCount = points.Count;
            for (int index = 0; index < points.Count; index++)
            {
                GameplayPosition point = points[index];
                line.SetPosition(
                    index,
                    new Vector3(point.X, point.Y + GroundOffset, point.Z));
            }
        }

        private static void DestroyLine(LineRenderer line)
        {
            GameplayObjectLifecycle.Destroy(
                line != null ? line.gameObject : null);
        }
    }
}
