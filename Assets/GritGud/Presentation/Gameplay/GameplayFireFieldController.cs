using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    /// <summary>
    /// Projects authoritative persistent-fire snapshots into visuals. Particle
    /// bounds never participate in damage, routing, or simulation evidence.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayFireFieldController : MonoBehaviour
    {
        private sealed class FireVisual
        {
            public FireVisual(GameObject root, Vector3 baseScale)
            {
                Root = root;
                BaseScale = baseScale;
            }

            public GameObject Root { get; }
            public Vector3 BaseScale { get; }
        }

        private readonly Dictionary<string, FireVisual> visuals =
            new Dictionary<string, FireVisual>(StringComparer.Ordinal);
        private GameplayFireFieldSession fireFields;
        private ConsumablePresentationCatalog presentationCatalog;
        private bool replayPresenting;

        internal int ActiveVisualCount => visuals.Count;

        internal void Bind(
            GameplayFireFieldSession fields,
            ConsumablePresentationCatalog presentation = null)
        {
            Unbind();
            fireFields = fields ?? throw new ArgumentNullException(nameof(fields));
            presentationCatalog = presentation
                ?? ConsumablePresentationCatalog.LoadDefault();
            fireFields.FieldDeployed += HandleFieldDeployed;
            fireFields.FieldChanged += HandleFieldChanged;
            fireFields.FieldExpired += HandleFieldExpired;
            foreach (FireFieldSnapshot snapshot in fireFields.CaptureActiveFields())
                CreateVisual(snapshot);
            enabled = true;
        }

        internal void Unbind()
        {
            if (fireFields != null)
            {
                fireFields.FieldDeployed -= HandleFieldDeployed;
                fireFields.FieldChanged -= HandleFieldChanged;
                fireFields.FieldExpired -= HandleFieldExpired;
            }
            foreach (FireVisual visual in visuals.Values)
                GameplayObjectLifecycle.Destroy(visual.Root);
            visuals.Clear();
            fireFields = null;
            presentationCatalog = null;
            replayPresenting = false;
            enabled = false;
        }

        private void OnDestroy() => Unbind();

        internal void BeginReplayPresentation()
        {
            if (fireFields == null)
                throw new InvalidOperationException(
                    "Bind fire fields before replay presentation.");
            replayPresenting = true;
            ReplaceVisuals(Array.Empty<FireFieldSnapshot>());
        }

        internal void PresentReplay(IReadOnlyList<FireFieldSnapshot> snapshots)
        {
            if (!replayPresenting)
                throw new InvalidOperationException(
                    "Begin fire replay presentation before sampling it.");
            SynchronizeVisuals(snapshots ?? throw new ArgumentNullException(
                nameof(snapshots)));
        }

        internal void EndReplayPresentation()
        {
            if (!replayPresenting) return;
            replayPresenting = false;
            ReplaceVisuals(fireFields.CaptureActiveFields());
        }

        private void HandleFieldDeployed(FireFieldSnapshot snapshot)
        {
            if (!replayPresenting)
                CreateVisual(snapshot);
        }

        private void HandleFieldChanged(FireFieldSnapshot snapshot)
        {
            if (!replayPresenting)
                ApplyScale(snapshot);
        }

        private void HandleFieldExpired(FireFieldRecord field)
        {
            if (replayPresenting) return;
            if (!visuals.TryGetValue(field.Id, out FireVisual visual)) return;
            visuals.Remove(field.Id);
            GameplayObjectLifecycle.Destroy(visual.Root);
        }

        private void CreateVisual(FireFieldSnapshot snapshot)
        {
            if (visuals.ContainsKey(snapshot.Field.Id))
                throw new InvalidOperationException(
                    $"Fire visual '{snapshot.Field.Id}' is already active.");
            ThrownExplosivePresentationDefinition presentation =
                presentationCatalog.GetThrownExplosive(
                    snapshot.Field.SourceItemId);
            GameObject prefab = presentation.PersistentAreaEffectPrefab;
            if (prefab == null)
                throw new InvalidOperationException(
                    $"Fire presentation '{snapshot.Field.SourceItemId}' "
                    + "requires a persistent-area effect prefab.");
            GameObject root = Instantiate(
                prefab,
                ToVector3(snapshot.Field.Origin) + Vector3.up * 0.03f,
                Quaternion.identity,
                transform);
            root.name = snapshot.Field.Id + " Fire Field";
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            visuals.Add(snapshot.Field.Id, new FireVisual(
                root,
                prefab.transform.localScale));
            ApplyScale(snapshot);
        }

        private void ApplyScale(FireFieldSnapshot snapshot)
        {
            if (!visuals.TryGetValue(snapshot.Field.Id, out FireVisual visual))
                throw new InvalidOperationException(
                    $"Fire visual '{snapshot.Field.Id}' is not active.");
            float scale = snapshot.CurrentRadius
                * presentationCatalog.GetThrownExplosive(
                    snapshot.Field.SourceItemId).PersistentEffectScalePerRadius;
            visual.Root.transform.localScale = Vector3.Scale(
                visual.BaseScale,
                new Vector3(scale, scale, scale));
        }

        private void ReplaceVisuals(
            IReadOnlyList<FireFieldSnapshot> snapshots)
        {
            foreach (FireVisual visual in visuals.Values)
                GameplayObjectLifecycle.Destroy(visual.Root);
            visuals.Clear();
            SynchronizeVisuals(snapshots);
        }

        private void SynchronizeVisuals(
            IReadOnlyList<FireFieldSnapshot> snapshots)
        {
            var retained = new HashSet<string>(StringComparer.Ordinal);
            foreach (FireFieldSnapshot snapshot in snapshots)
            {
                retained.Add(snapshot.Field.Id);
                if (!visuals.ContainsKey(snapshot.Field.Id))
                    CreateVisual(snapshot);
                else
                    ApplyScale(snapshot);
            }
            var removed = new List<string>();
            foreach (string fieldId in visuals.Keys)
                if (!retained.Contains(fieldId))
                    removed.Add(fieldId);
            foreach (string fieldId in removed)
            {
                GameplayObjectLifecycle.Destroy(visuals[fieldId].Root);
                visuals.Remove(fieldId);
            }
        }

        private static Vector3 ToVector3(GameplayPosition value) =>
            new Vector3(value.X, value.Y, value.Z);
    }
}
