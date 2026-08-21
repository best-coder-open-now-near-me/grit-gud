using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayVehicleController : MonoBehaviour
    {
        private sealed class VehicleRuntime
        {
            public VehicleRuntime(
                GameObject root,
                VehicleMomentumSession session,
                VehicleMomentumEnvelopePresenter envelope)
            {
                Root = root;
                Session = session;
                Envelope = envelope;
            }

            public GameObject Root { get; }

            public VehicleMomentumSession Session { get; }

            public VehicleMomentumEnvelopePresenter Envelope { get; }

            public string OccupantActorId { get; set; }
        }

        private readonly Dictionary<string, VehicleRuntime> vehicles =
            new Dictionary<string, VehicleRuntime>(StringComparer.Ordinal);
        private GameplaySession gameplaySession;
        private bool replayPresenting;

        public int VehicleCount => vehicles.Count;

        public void Bind(
            LevelWorld world,
            GameplaySession activeGameplaySession,
            IReadOnlyCollection<ScenarioVehicleRuntimeDefinition> definitions)
        {
            Unbind();
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            gameplaySession = activeGameplaySession ??
                throw new ArgumentNullException(nameof(activeGameplaySession));
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            try
            {
                foreach (ScenarioVehicleRuntimeDefinition definition in
                    definitions)
                {
                    BindVehicle(world, definition);
                }
            }
            catch
            {
                Unbind();
                throw;
            }

            enabled = vehicles.Count > 0;
        }

        public VehicleMomentumSession GetSession(string vehicleId) =>
            GetVehicleRuntime(vehicleId).Session;

        public GameObject GetVehicle(string vehicleId) =>
            GetVehicleRuntime(vehicleId).Root;

        public string GetOccupantActorId(string vehicleId) =>
            GetVehicleRuntime(vehicleId).OccupantActorId;

        public bool IsMomentumEnvelopeVisible(string vehicleId) =>
            GetVehicleRuntime(vehicleId).Envelope.PresentationEnabled;

        public void SetOccupant(string vehicleId, string actorId)
        {
            if (gameplaySession == null)
            {
                throw new InvalidOperationException(
                    "Vehicle occupancy is not bound to gameplay.");
            }

            if (!gameplaySession.TryGetActor(actorId, out _))
            {
                throw new ArgumentException(
                    $"Gameplay actor '{actorId}' does not exist.",
                    nameof(actorId));
            }

            VehicleRuntime runtime = GetVehicleRuntime(vehicleId);
            runtime.OccupantActorId = actorId;
            RefreshEnvelopeVisibility(runtime);
        }

        public void ClearOccupant(string vehicleId)
        {
            VehicleRuntime runtime = GetVehicleRuntime(vehicleId);
            runtime.OccupantActorId = null;
            RefreshEnvelopeVisibility(runtime);
        }

        public bool TryResolvePath(
            string vehicleId,
            IReadOnlyList<Vector3> requestedPath,
            out VehicleMomentumRecord record,
            out VehiclePathFailure failure)
        {
            if (requestedPath == null)
            {
                throw new ArgumentNullException(nameof(requestedPath));
            }

            VehicleRuntime runtime = GetVehicleRuntime(vehicleId);
            var path = new List<GameplayPosition>(requestedPath.Count);
            foreach (Vector3 point in requestedPath)
            {
                path.Add(ToGameplayPosition(point));
            }

            if (!runtime.Session.TryResolvePath(
                    path,
                    gameplaySession.LastTransitionSequence + 1L,
                    out record,
                    out failure))
            {
                return false;
            }

            Present(runtime, record);
            return true;
        }

        public void Commit(string vehicleId, VehicleMomentumRecord record)
        {
            VehicleRuntime runtime = GetVehicleRuntime(vehicleId);
            runtime.Session.Commit(record);
            Present(runtime, record);
        }

        public void Unbind()
        {
            foreach (VehicleRuntime runtime in vehicles.Values)
            {
                runtime.Envelope.Unbind();
            }

            vehicles.Clear();
            gameplaySession = null;
            replayPresenting = false;
            enabled = false;
        }

        internal void BeginReplayPresentation()
        {
            replayPresenting = true;
            foreach (VehicleRuntime runtime in vehicles.Values)
                runtime.Envelope.SetPresentationEnabled(false);
        }

        internal void PresentReplay(IReadOnlyList<VehicleMomentumState> states)
        {
            if (!replayPresenting)
                throw new InvalidOperationException(
                    "Begin vehicle replay presentation before sampling it.");
            if (states == null) throw new ArgumentNullException(nameof(states));
            foreach (VehicleMomentumState state in states)
                PresentState(GetVehicleRuntime(state.VehicleId), state);
        }

        internal void EndReplayPresentation()
        {
            if (!replayPresenting) return;
            replayPresenting = false;
            foreach (VehicleRuntime runtime in vehicles.Values)
            {
                PresentState(runtime, runtime.Session.State);
                RefreshEnvelopeVisibility(runtime);
            }
        }

        internal static bool ShouldShowMomentumEnvelope(
            GameplaySession activeGameplaySession,
            string occupantActorId)
        {
            return activeGameplaySession != null
                && activeGameplaySession.Mode == GameplaySessionMode.TurnBased
                && !string.IsNullOrWhiteSpace(occupantActorId)
                && string.Equals(
                    activeGameplaySession.ActiveActorId,
                    occupantActorId,
                    StringComparison.Ordinal);
        }

        private void LateUpdate()
        {
            if (replayPresenting) return;
            foreach (VehicleRuntime runtime in vehicles.Values)
            {
                RefreshEnvelopeVisibility(runtime);
            }
        }

        private void BindVehicle(
            LevelWorld world,
            ScenarioVehicleRuntimeDefinition definition)
        {
            if (definition == null)
            {
                throw new InvalidOperationException(
                    "Vehicle runtime definitions cannot contain null entries.");
            }

            if (!world.TryGetEntity(
                    definition.EntityId,
                    out LevelEntityView vehicleView))
            {
                throw new InvalidOperationException(
                    $"The active level does not contain vehicle "
                    + $"'{definition.EntityId}'.");
            }

            GameObject root = vehicleView.gameObject;
            var initialState = new VehicleMomentumState(
                definition.EntityId,
                ToGameplayPosition(root.transform.position),
                root.transform.eulerAngles.y,
                definition.StartingSpeed);
            var session = new VehicleMomentumSession(
                definition.MomentumProfile,
                initialState,
                gameplaySession.Journal);
            VehicleMomentumEnvelopePresenter envelope =
                root.GetComponent<VehicleMomentumEnvelopePresenter>()
                ?? root.AddComponent<VehicleMomentumEnvelopePresenter>();
            envelope.Bind(session);
            var runtime = new VehicleRuntime(root, session, envelope);
            if (!vehicles.TryAdd(definition.EntityId, runtime))
            {
                envelope.Unbind();
                throw new InvalidOperationException(
                    $"Vehicle '{definition.EntityId}' is bound more than once.");
            }

            if (definition.StartingOccupantActorId != null)
            {
                SetOccupant(
                    definition.EntityId,
                    definition.StartingOccupantActorId);
            }
            else
            {
                RefreshEnvelopeVisibility(runtime);
            }
        }

        private VehicleRuntime GetVehicleRuntime(string vehicleId)
        {
            if (!vehicles.TryGetValue(
                    vehicleId ?? string.Empty,
                    out VehicleRuntime runtime))
            {
                throw new KeyNotFoundException(
                    $"Vehicle '{vehicleId}' is not bound to gameplay.");
            }

            return runtime;
        }

        private void RefreshEnvelopeVisibility(VehicleRuntime runtime)
        {
            runtime.Envelope.SetPresentationEnabled(
                ShouldShowMomentumEnvelope(
                    gameplaySession,
                    runtime.OccupantActorId));
        }

        private static void Present(
            VehicleRuntime runtime,
            VehicleMomentumRecord record)
        {
            PresentState(runtime, record.Resulting);
        }

        private static void PresentState(
            VehicleRuntime runtime,
            VehicleMomentumState state)
        {
            ApplyReplayTransform(runtime.Root, state);
            Physics.SyncTransforms();
            runtime.Envelope.RefreshNow();
        }

        internal static void ApplyReplayTransform(
            GameObject root,
            VehicleMomentumState state)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            root.transform.SetPositionAndRotation(
                new Vector3(
                    state.Position.X,
                    state.Position.Y,
                    state.Position.Z),
                Quaternion.Euler(0f, state.ForwardDegrees, 0f));
        }

        private static GameplayPosition ToGameplayPosition(Vector3 position) =>
            new GameplayPosition(position.x, position.y, position.z);
    }
}
