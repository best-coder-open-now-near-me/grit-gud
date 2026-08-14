using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayObjectivePresenter : MonoBehaviour
    {
        private static readonly Color ActiveColor = GameplayVisualPalette.WithAlpha(
            GameplayVisualPalette.SignalBlueGlow,
            0.8f);
        private static readonly Color CompleteColor = GameplayVisualPalette.WithAlpha(
            GameplayVisualPalette.SignalGreen,
            0.8f);

        private string objectiveId;
        private GameObject markerRoot;
        private Transform beacon;
        private Material markerMaterial;
        private bool presentedCompletion;

        public GameplaySession Session { get; private set; }

        public string ObjectiveId => objectiveId;

        public bool IsPresented => markerRoot != null;

        public void Bind(GameplaySession session, string authoritativeObjectiveId)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (string.IsNullOrWhiteSpace(authoritativeObjectiveId))
            {
                throw new ArgumentException(
                    "Objective-presenter identifiers cannot be empty.",
                    nameof(authoritativeObjectiveId));
            }

            Unbind();
            Session = session;
            objectiveId = authoritativeObjectiveId;
            GameplayObjectiveSnapshot objective = session.GetObjective(objectiveId);
            CreateMarker(objective);
            PresentCompletion(objective.IsCompleted);
            enabled = true;
        }

        public void Unbind()
        {
            GameplayObjectLifecycle.Destroy(markerRoot);
            markerRoot = null;
            beacon = null;
            GameplayObjectLifecycle.Destroy(markerMaterial);
            markerMaterial = null;
            Session = null;
            objectiveId = null;
            presentedCompletion = false;
            enabled = false;
        }

        private void Update()
        {
            if (Session == null || markerRoot == null)
            {
                return;
            }

            GameplayObjectiveSnapshot objective = Session.GetObjective(objectiveId);
            if (objective.IsCompleted != presentedCompletion)
            {
                PresentCompletion(objective.IsCompleted);
            }

            markerRoot.transform.Rotate(0f, 32f * Time.deltaTime, 0f, Space.World);
            if (beacon != null)
            {
                Vector3 local = beacon.localPosition;
                local.y = 0.72f + (Mathf.Sin(Time.unscaledTime * 2.2f) * 0.08f);
                beacon.localPosition = local;
            }
        }

        private void CreateMarker(GameplayObjectiveSnapshot objective)
        {
            markerRoot = new GameObject($"Objective - {objective.ObjectiveId}");
            markerRoot.transform.SetParent(transform, worldPositionStays: true);
            markerRoot.transform.position = new Vector3(
                objective.Position.X,
                objective.Position.Y + 0.04f,
                objective.Position.Z);

            markerMaterial = RuntimeMaterialFactory.CreateColor(
                ActiveColor,
                "Raised Deck Objective Material");

            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "Interaction Radius";
            pad.transform.SetParent(markerRoot.transform, false);
            pad.transform.localScale = new Vector3(
                objective.InteractionRadius * 2f,
                0.025f,
                objective.InteractionRadius * 2f);
            RemoveCollider(pad);
            ApplyMaterial(pad);

            GameObject beaconObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beaconObject.name = "Objective Beacon";
            beaconObject.transform.SetParent(markerRoot.transform, false);
            beaconObject.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            beaconObject.transform.localScale = Vector3.one * 0.24f;
            RemoveCollider(beaconObject);
            ApplyMaterial(beaconObject);
            beacon = beaconObject.transform;
        }

        private void PresentCompletion(bool completed)
        {
            presentedCompletion = completed;
            if (markerMaterial == null)
            {
                return;
            }

            Color color = completed ? CompleteColor : ActiveColor;
            markerMaterial.color = color;
            if (markerMaterial.HasProperty("_BaseColor"))
            {
                markerMaterial.SetColor("_BaseColor", color);
            }
        }

        private void ApplyMaterial(GameObject target)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = markerMaterial;
            }
        }

        private static void RemoveCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                GameplayObjectLifecycle.Destroy(collider);
            }
        }
    }
}
