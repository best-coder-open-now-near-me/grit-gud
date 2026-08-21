using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GritGud.Application.Levels;
using GritGud.Domain.Levels;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing
{
    internal sealed class LevelEditorPhysicsPlacementCoordinator : IDisposable
    {
        private readonly MonoBehaviour coroutineHost;
        private readonly LevelEditorWorkspace workspace;
        private readonly LevelSelectionModel selection;
        private readonly LevelWorldProjector projector;
        private readonly Action activateDefaultTool;
        private readonly Action<string> setStatus;
        private readonly Action<LevelTransformData> syncInspector;
        private readonly List<PhysicsPlacementBody> activePlacements =
            new List<PhysicsPlacementBody>();
        private Coroutine activeCoroutine;
        private bool cancelRequested;
        private bool disposed;

        public LevelEditorPhysicsPlacementCoordinator(
            MonoBehaviour coroutineHost,
            LevelEditorWorkspace workspace,
            LevelSelectionModel selection,
            LevelWorldProjector projector,
            Action activateDefaultTool,
            Action<string> setStatus,
            Action<LevelTransformData> syncInspector)
        {
            this.coroutineHost = coroutineHost
                ?? throw new ArgumentNullException(nameof(coroutineHost));
            this.workspace = workspace
                ?? throw new ArgumentNullException(nameof(workspace));
            this.selection = selection
                ?? throw new ArgumentNullException(nameof(selection));
            this.projector = projector
                ?? throw new ArgumentNullException(nameof(projector));
            this.activateDefaultTool = activateDefaultTool
                ?? throw new ArgumentNullException(nameof(activateDefaultTool));
            this.setStatus = setStatus
                ?? throw new ArgumentNullException(nameof(setStatus));
            this.syncInspector = syncInspector
                ?? throw new ArgumentNullException(nameof(syncInspector));
        }

        public bool IsRunning { get; private set; }

        public void Start(string dropHeightText, bool keepUpright)
        {
            ThrowIfDisposed();
            if (IsRunning)
            {
                setStatus("A physics placement is already settling.");
                return;
            }
            if (!TryParse(dropHeightText, out float dropHeight)
                || dropHeight <= 0f
                || dropHeight > 25f)
            {
                setStatus(
                    "Drop height must be greater than 0 and no more than 25 meters.");
                return;
            }

            int unsupportedCount = CollectPlacements();
            if (activePlacements.Count == 0)
            {
                setStatus(
                    "Select one or more loose world props to drop; structures and vehicles are unsupported.");
                return;
            }

            activateDefaultTool();
            cancelRequested = false;
            IsRunning = true;
            activeCoroutine = coroutineHost.StartCoroutine(
                SettleEntities(dropHeight, keepUpright, unsupportedCount));
        }

        public void Cancel()
        {
            if (IsRunning)
                cancelRequested = true;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            cancelRequested = true;
            if (activeCoroutine != null && coroutineHost != null)
                coroutineHost.StopCoroutine(activeCoroutine);
            activeCoroutine = null;
            RestoreAndCleanupPlacements();
            IsRunning = false;
        }

        internal static bool RequiresBoundsFallback(
            IEnumerable<Collider> colliders)
        {
            Collider[] values = colliders?
                .Where(collider => collider != null)
                .ToArray()
                ?? Array.Empty<Collider>();
            return !values.Any(collider =>
                    collider.enabled && !collider.isTrigger)
                || values.Any(collider => collider.enabled
                    && collider is MeshCollider mesh
                    && !mesh.convex);
        }

        private int CollectPlacements()
        {
            activePlacements.Clear();
            int unsupportedCount = 0;
            foreach (string entityId in selection.Targets
                .Select(target => target.EntityId)
                .Distinct(StringComparer.Ordinal))
            {
                LevelEntity entity = workspace.FindEntitySnapshot(entityId);
                if (entity == null
                    || !projector.TryGetEntity(
                        entity.id,
                        out LevelEntityView view))
                {
                    unsupportedCount++;
                    continue;
                }
                LevelArchetypeCapabilities blocked =
                    LevelArchetypeCapabilities.PlacementSurface
                    | LevelArchetypeCapabilities.Vehicle;
                if ((view.Archetype.Capabilities & blocked) != 0
                    || view.GetComponent<Rigidbody>() != null)
                {
                    unsupportedCount++;
                    continue;
                }
                activePlacements.Add(new PhysicsPlacementBody(entity, view));
            }

            return unsupportedCount;
        }

        private IEnumerator SettleEntities(
            float dropHeight,
            bool keepUpright,
            int unsupportedCount)
        {
            int stableSteps = 0;
            bool capturedFinalTransforms = false;
            try
            {
                int fallbackCount = PreparePlacements(
                    dropHeight,
                    keepUpright);
                setStatus(
                    $"Settling {activePlacements.Count} prop(s): "
                    + $"{activePlacements.Count - fallbackCount} authored collider(s), "
                    + $"{fallbackCount} bounds fallback(s), {unsupportedCount} skipped.");

                float elapsed = 0f;
                while (!cancelRequested
                    && elapsed < 5f
                    && stableSteps < 12)
                {
                    yield return new WaitForFixedUpdate();
                    elapsed += Time.fixedDeltaTime;
                    bool stable = activePlacements.All(placement =>
                        placement.Body.linearVelocity.sqrMagnitude < 0.0025f
                        && placement.Body.angularVelocity.sqrMagnitude
                            < 0.0025f);
                    stableSteps = stable ? stableSteps + 1 : 0;
                }

                if (!cancelRequested && !disposed)
                {
                    foreach (PhysicsPlacementBody placement in activePlacements)
                        placement.After = placement.View.ReadTransform();
                    capturedFinalTransforms = true;
                }
            }
            finally
            {
                CleanupTemporaryPhysics(
                    restoreOriginalTransforms:
                        cancelRequested || disposed || !capturedFinalTransforms);
                activeCoroutine = null;
                IsRunning = false;
            }

            if (disposed)
                yield break;
            if (cancelRequested)
            {
                cancelRequested = false;
                syncInspector(activePlacements[0].Before);
                setStatus(
                    "Canceled physics placement and restored all previous transforms.");
                activePlacements.Clear();
                yield break;
            }

            ILevelEditCommand[] commands = activePlacements
                .Select(placement =>
                    (ILevelEditCommand)new SetEntityTransformCommand(
                        placement.Entity.id,
                        placement.Before,
                        placement.After))
                .ToArray();
            if (commands.Length == 1)
                workspace.Execute(commands[0]);
            else
                workspace.ExecuteTransaction("Drop and settle props", commands);
            syncInspector(activePlacements[0].After);
            setStatus(stableSteps >= 12
                ? $"Dropped and settled {activePlacements.Count} prop(s) as one undoable operation."
                : $"Physics placement timed out and saved {activePlacements.Count} final pose(s). Undo restores the batch.");
            activePlacements.Clear();
        }

        private int PreparePlacements(float dropHeight, bool keepUpright)
        {
            int fallbackCount = 0;
            foreach (PhysicsPlacementBody placement in activePlacements)
            {
                Collider[] colliders = placement.View
                    .GetComponentsInChildren<Collider>(true);
                if (RequiresBoundsFallback(colliders))
                {
                    fallbackCount++;
                    foreach (Collider collider in colliders)
                    {
                        if (!collider.enabled)
                            continue;
                        collider.enabled = false;
                        placement.DisabledColliders.Add(collider);
                    }
                    Bounds bounds = LevelEntityView.CalculateVisualLocalBounds(
                        placement.View.Archetype.Presentation.Prefab,
                        placement.View.Archetype.Presentation.LocalBounds);
                    placement.Fallback = placement.View.gameObject
                        .AddComponent<BoxCollider>();
                    placement.Fallback.center = bounds.center;
                    placement.Fallback.size = bounds.size;
                }

                placement.Body = placement.View.gameObject
                    .AddComponent<Rigidbody>();
                placement.Body.mass = 1f;
                placement.Body.collisionDetectionMode =
                    CollisionDetectionMode.ContinuousSpeculative;
                placement.Body.constraints = keepUpright
                    ? RigidbodyConstraints.FreezeRotationX
                        | RigidbodyConstraints.FreezeRotationZ
                    : RigidbodyConstraints.None;
                placement.View.transform.position += Vector3.up * dropHeight;
                placement.Body.position = placement.View.transform.position;
                placement.Body.rotation = placement.View.transform.rotation;
            }

            return fallbackCount;
        }

        private void RestoreAndCleanupPlacements()
        {
            CleanupTemporaryPhysics(restoreOriginalTransforms: true);
            activePlacements.Clear();
        }

        private void CleanupTemporaryPhysics(bool restoreOriginalTransforms)
        {
            foreach (PhysicsPlacementBody placement in activePlacements)
            {
                if (placement.Body != null)
                {
                    placement.Body.isKinematic = true;
                    UnityEngine.Object.Destroy(placement.Body);
                    placement.Body = null;
                }
                if (placement.Fallback != null)
                {
                    UnityEngine.Object.Destroy(placement.Fallback);
                    placement.Fallback = null;
                }
                foreach (Collider collider in placement.DisabledColliders)
                {
                    if (collider != null)
                        collider.enabled = true;
                }
                placement.DisabledColliders.Clear();
                if (restoreOriginalTransforms && placement.View != null)
                    placement.View.ApplyTransform(placement.Before);
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        private static bool TryParse(string text, out float value)
        {
            bool parsed = float.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
            return parsed && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private sealed class PhysicsPlacementBody
        {
            public PhysicsPlacementBody(LevelEntity entity, LevelEntityView view)
            {
                Entity = entity;
                View = view;
                Before = entity.transform;
            }

            public LevelEntity Entity { get; }
            public LevelEntityView View { get; }
            public LevelTransformData Before { get; }
            public LevelTransformData After { get; set; }
            public Rigidbody Body { get; set; }
            public BoxCollider Fallback { get; set; }
            public List<Collider> DisabledColliders { get; } =
                new List<Collider>();
        }
    }
}
