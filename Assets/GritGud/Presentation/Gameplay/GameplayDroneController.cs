using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayDroneController : MonoBehaviour
    {
        internal const string AbilityId = "ability.control-drone";
        internal const string MoveOptionId = "drone.move";
        internal const string AttackOptionId = "drone.attack";
        internal const int HotbarSlot = 7;

        private enum CommandMode { None, Move, Attack }

        private readonly Dictionary<string, GameObject> roots =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private GameplayDroneSession drones;
        private GameplaySession gameplay;
        private GameplayWorldRegistry registry;
        private GameplaySmokeFieldSession smoke;
        private GameplayDialogueLog dialogue;
        private Func<Vector2, bool> pointerBlocked;
        private string commandDroneId;
        private CommandMode mode;
        private bool replayPresenting;

        public bool IsTargeting => mode != CommandMode.None;
        public GameplayDroneSession Session => drones;

        internal void Bind(
            LevelWorld world,
            GameplaySession gameplaySession,
            GameplayWorldRegistry worldRegistry,
            IEnumerable<DroneDefinition> definitions,
            DestructiblePropSession destructibles,
            GameplaySmokeFieldSession smokeFields,
            GameplayDialogueLog dialogueLog,
            uint randomSeed,
            Func<Vector2, bool> isPointerBlocked)
        {
            Unbind();
            gameplay = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            registry = worldRegistry ?? throw new ArgumentNullException(
                nameof(worldRegistry));
            smoke = smokeFields;
            dialogue = dialogueLog ?? throw new ArgumentNullException(
                nameof(dialogueLog));
            pointerBlocked = isPointerBlocked;
            var copied = new List<DroneDefinition>(definitions
                ?? throw new ArgumentNullException(nameof(definitions)));
            drones = new GameplayDroneSession(gameplay, copied, destructibles);
            foreach (DroneDefinition definition in copied)
            {
                if (!world.TryGetEntity(definition.Id, out LevelEntityView view))
                    throw new InvalidOperationException(
                        $"Level is missing drone entity '{definition.Id}'.");
                roots.Add(definition.Id, view.gameObject);
                GameplayDroneVisualPresenter visual = view.gameObject
                    .GetComponent<GameplayDroneVisualPresenter>()
                    ?? view.gameObject.AddComponent<GameplayDroneVisualPresenter>();
                visual.Build();
            }
            enabled = copied.Count > 0;
        }

        public bool TryToggle(string controllerActorId, string optionId)
        {
            DroneSnapshot drone;
            if (!TryFindControllerDrone(controllerActorId, out drone))
                return false;
            CommandMode requested = string.Equals(optionId, MoveOptionId,
                StringComparison.Ordinal)
                    ? CommandMode.Move
                    : string.Equals(optionId, AttackOptionId,
                        StringComparison.Ordinal)
                        ? CommandMode.Attack
                        : CommandMode.None;
            if (requested == CommandMode.None) return false;
            if (mode == requested
                && string.Equals(commandDroneId, drone.DroneId,
                    StringComparison.Ordinal))
            {
                CancelTargeting();
                return true;
            }
            mode = requested;
            commandDroneId = drone.DroneId;
            dialogue.Append(
                GameplayDialogueChannel.System,
                "SCOUT DRONE",
                requested == CommandMode.Move
                    ? "Select a destination within the drone movement radius."
                    : "Select a visible hostile actor for the drone weapon.");
            return true;
        }

        public void CancelTargeting()
        {
            mode = CommandMode.None;
            commandDroneId = null;
        }

        internal void BeginReplayPresentation()
        {
            if (drones == null)
                throw new InvalidOperationException(
                    "Bind drones before replay presentation.");
            replayPresenting = true;
        }

        internal void PresentReplay(IReadOnlyList<DroneSnapshot> snapshots)
        {
            if (!replayPresenting)
                throw new InvalidOperationException(
                    "Begin drone replay presentation before sampling it.");
            if (snapshots == null)
                throw new ArgumentNullException(nameof(snapshots));
            var retained = new HashSet<string>(StringComparer.Ordinal);
            foreach (DroneSnapshot snapshot in snapshots)
            {
                retained.Add(snapshot.DroneId);
                ApplySnapshot(snapshot);
            }
            foreach (KeyValuePair<string, GameObject> entry in roots)
                entry.Value.SetActive(retained.Contains(entry.Key));
        }

        internal void EndReplayPresentation()
        {
            if (!replayPresenting) return;
            replayPresenting = false;
            RefreshAuthoritativePresentation();
        }

        internal void RefreshAuthoritativePresentation()
        {
            if (drones == null)
                throw new InvalidOperationException(
                    "Bind drones before refreshing their presentation.");
            foreach (DroneSnapshot snapshot in drones.CaptureDrones())
                ApplySnapshot(snapshot);
        }

        public bool TryAttackDroneAtPointer(
            string attackingActorId,
            Ray pointerRay)
        {
            if (drones == null
                || string.IsNullOrWhiteSpace(attackingActorId)
                || !TryAcquireDrone(pointerRay, out DroneSnapshot drone))
                return false;
            AttackDefinition attack = gameplay.GetEquippedAttack(
                attackingActorId);
            if (attack == null || attack.DirectVehicleIntegrityDamage <= 0f)
                return false;
            DroneExposureSnapshot exposure = CaptureActorExposure(
                attackingActorId,
                drone);
            if (exposure.VisibleSampleCount == 0) return false;
            return TryResolveActorAttack(
                attackingActorId,
                exposure,
                out _);
        }

        internal bool TrySelectActorAttackTarget(
            string attackingActorId,
            out DroneExposureSnapshot selectedExposure,
            out int selectedHitChance)
        {
            selectedExposure = null;
            selectedHitChance = 0;
            if (drones == null || string.IsNullOrWhiteSpace(attackingActorId))
                return false;
            AttackDefinition attack = gameplay.GetEquippedAttack(
                attackingActorId);
            if (attack == null || attack.DirectVehicleIntegrityDamage <= 0f)
                return false;
            GameplayPosition origin = gameplay.GetActor(attackingActorId)
                .Pose.Position;
            float bestDistance = float.PositiveInfinity;
            foreach (DroneSnapshot drone in drones.CaptureDrones())
            {
                if (!drone.IsOperational
                    || !gameplay.IsHostile(
                        attackingActorId,
                        drone.Definition.ControllerActorId))
                    continue;
                DroneExposureSnapshot exposure = CaptureActorExposure(
                    attackingActorId,
                    drone);
                float distance = origin.DistanceTo(drone.Position);
                int hitChance = DroneDirectAttackRules
                    .CalculateHitChancePercent(attack, exposure, distance);
                if (selectedExposure == null
                    || hitChance > selectedHitChance
                    || (hitChance == selectedHitChance
                        && distance < bestDistance))
                {
                    selectedExposure = exposure;
                    selectedHitChance = hitChance;
                    bestDistance = distance;
                }
            }
            return selectedExposure != null && selectedHitChance > 0;
        }

        internal bool TryResolveActorAttack(
            string attackingActorId,
            DroneExposureSnapshot exposure,
            out ActorDroneAttackRecord resolved)
        {
            resolved = null;
            if (drones == null || exposure == null) return false;
            DroneSnapshot drone = drones.GetDrone(exposure.DroneId);
            AttackDefinition attack = gameplay.GetEquippedAttack(
                attackingActorId);
            if (attack == null || attack.DirectVehicleIntegrityDamage <= 0f)
                return false;
            GameplayPosition origin = gameplay.GetActor(attackingActorId)
                .Pose.Position;
            GameObject targetRoot = roots[drone.DroneId];
            long sequence = gameplay.LastActionSequence + 1L;
            try
            {
                ActorDroneAttackRecord record = drones.PrepareActorAttack(
                    attackingActorId,
                    drone.DroneId,
                    exposure,
                    origin.DistanceTo(drone.Position),
                    GameplayAddressedRandom.SampleUInt32(
                        gameplay.RunIdentity,
                        new GameplayTransitionIdentity(
                            sequence,
                            GameplaySemanticCapability.DirectAttack.ToString(),
                            attackingActorId,
                            drone.DroneId),
                        "resolution"));
                drones.CommitActorAttack(record);
                GameplayDroneVisualPresenter visual = targetRoot.GetComponent<
                    GameplayDroneVisualPresenter>();
                visual?.SetOperational(drones.GetDrone(drone.DroneId)
                    .IsOperational);
                dialogue.Append(GameplayDialogueChannel.System, "DRONE IMPACT",
                    record.Hit
                        ? $"{drone.DroneId} integrity {record.Damage.Resulting.RemainingIntegrity:0.#}/{drone.Definition.MaximumIntegrity:0.#}."
                        : $"{attackingActorId} missed {drone.DroneId}.");
                resolved = record;
                return true;
            }
            catch (InvalidOperationException exception)
            {
                dialogue.Append(GameplayDialogueChannel.System,
                    "DRONE ATTACK REJECTED", exception.Message);
                return false;
            }
        }

        private DroneExposureSnapshot CaptureActorExposure(
            string attackingActorId,
            DroneSnapshot drone)
        {
            GameplayActorView observer = registry.GetActor(attackingActorId);
            GameObject targetRoot = roots[drone.DroneId];
            GameplayActorSnapshot observerState = gameplay.GetActor(
                attackingActorId);
            GameplayPosition origin = observerState.Pose.Position;
            foreach (TargetRegionSample sample in
                ActorTargetProfileCatalog.CreateWorldSamples(
                    observerState.Pose,
                    observerState.IsPinned))
            {
                if (sample.Id != TargetRegionId.Head) continue;
                origin = sample.Center;
                break;
            }
            IReadOnlyList<TargetRegionSample> samples =
                GameplayDroneTargetProfile.CreateWorldSamples(drone);
            var query = new UnityTargetExposureQuery(
                observer.Transform,
                targetRoot.transform,
                Physics.DefaultRaycastLayers,
                () => gameplay.Revision,
                smoke);
            TargetExposureSnapshot raster = query.Capture(
                attackingActorId,
                origin,
                drone.DroneId,
                samples);
            return new DroneExposureSnapshot(
                attackingActorId,
                drone.DroneId,
                raster.VisibleSampleCount,
                raster.TotalSampleCount);
        }

        public void Unbind()
        {
            CancelTargeting();
            replayPresenting = false;
            roots.Clear();
            drones = null;
            gameplay = null;
            registry = null;
            smoke = null;
            dialogue = null;
            pointerBlocked = null;
            enabled = false;
        }

        private void ApplySnapshot(DroneSnapshot snapshot)
        {
            if (!roots.TryGetValue(snapshot.DroneId, out GameObject root))
                throw new InvalidOperationException(
                    $"Drone snapshot '{snapshot.DroneId}' has no visual.");
            root.SetActive(true);
            root.transform.SetPositionAndRotation(
                new Vector3(
                    snapshot.Position.X,
                    snapshot.Position.Y,
                    snapshot.Position.Z),
                Quaternion.Euler(0f, snapshot.FacingDegrees, 0f));
            root.GetComponent<GameplayDroneVisualPresenter>()?
                .SetOperational(snapshot.IsOperational);
        }

        private void Update()
        {
            if (mode == CommandMode.None || Mouse.current == null) return;
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                CancelTargeting();
                return;
            }
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;
            Vector2 pointer = Mouse.current.position.ReadValue();
            if (pointerBlocked?.Invoke(pointer) == true) return;
            Camera gameplayCamera = Camera.main;
            if (gameplayCamera == null) return;
            Ray ray = gameplayCamera.ScreenPointToRay(pointer);
            if (mode == CommandMode.Move) TryCommitMove(ray);
            else TryCommitAttack(ray);
        }

        private void TryCommitMove(Ray ray)
        {
            if (!Physics.Raycast(ray, out RaycastHit hit, 250f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return;
            DroneSnapshot drone = drones.GetDrone(commandDroneId);
            var destination = new GameplayPosition(
                hit.point.x,
                drone.Position.Y,
                hit.point.z);
            try
            {
                DroneMoveRecord record = drones.PrepareMove(
                    drone.DroneId,
                    destination,
                    CalculateFacing(drone.Position, destination));
                drones.CommitMove(record);
                GameObject root = roots[drone.DroneId];
                root.transform.SetPositionAndRotation(
                    new Vector3(destination.X, destination.Y, destination.Z),
                    Quaternion.Euler(0f, record.ResultingFacingDegrees, 0f));
                dialogue.Append(GameplayDialogueChannel.System, "SCOUT DRONE",
                    $"Moved to {destination.X:0.0}, {destination.Z:0.0}; controller AP {record.ResultingBudget.ActionPoints}.");
                CancelTargeting();
            }
            catch (InvalidOperationException exception)
            {
                dialogue.Append(GameplayDialogueChannel.System,
                    "DRONE COMMAND REJECTED", exception.Message);
            }
        }

        private void TryCommitAttack(Ray ray)
        {
            DroneSnapshot drone = drones.GetDrone(commandDroneId);
            GameObject root = roots[drone.DroneId];
            var query = new UnityPointerTargetQuery(
                root.transform,
                registry,
                Physics.DefaultRaycastLayers,
                candidate => candidate.Targetable
                    && !gameplay.IsActorIncapacitated(candidate.ActorId)
                    && gameplay.IsHostile(
                        drone.Definition.ControllerActorId,
                        candidate.ActorId));
            if (!query.TryAcquire(ray, out GameplayActorView target)) return;
            GameplayActorSnapshot targetState = gameplay.GetActor(target.ActorId);
            if (!DroneSensorRules.CanObserve(
                    drone,
                    targetState.Pose.Position)) return;
            IReadOnlyList<ActorTargetRegionSample> presented =
                target.TargetProfile.GetTargetRegionSamples();
            var samples = new List<TargetRegionSample>(presented.Count);
            foreach (ActorTargetRegionSample sample in presented)
                samples.Add(new TargetRegionSample(
                    sample.Id,
                    new GameplayPosition(
                        sample.WorldCenter.x,
                        sample.WorldCenter.y,
                        sample.WorldCenter.z),
                    sample.Radius));
            var exposureQuery = new UnityTargetExposureQuery(
                root.transform,
                target.Transform,
                Physics.DefaultRaycastLayers,
                () => gameplay.Revision,
                smoke);
            TargetExposureSnapshot exposure = exposureQuery.Capture(
                drone.DroneId,
                drone.Position,
                target.ActorId,
                samples);
            if (exposure.VisibleSampleCount == 0) return;
            long resolutionSequence = gameplay.LastTransitionSequence + 1L;
            var transitionIdentity = new GameplayTransitionIdentity(
                resolutionSequence,
                GameplaySemanticCapability.DirectAttack.ToString(),
                drone.Definition.ControllerActorId,
                target.ActorId);
            AttackResolutionRecord resolution = AttackResolutionRules.Resolve(
                resolutionSequence,
                GameplayAddressedRandom.SampleUInt32(
                    gameplay.RunIdentity,
                    transitionIdentity,
                    "resolution"),
                exposure,
                drone.Definition.Attack.AccuracyDecay,
                drone.Position.DistanceTo(targetState.Pose.Position),
                targetState.Wounds,
                drone.Definition.Attack.WoundMovementPenalty);
            try
            {
                DroneAttackRecord record = drones.PrepareActorAttack(
                    drone.DroneId,
                    resolution);
                drones.CommitAttack(record);
                dialogue.Append(GameplayDialogueChannel.System, "SCOUT DRONE",
                    resolution.Hit
                        ? $"Hit {target.ActorId}: {resolution.HitRegion} wounded; controller AP {record.ResultingBudget.ActionPoints}."
                        : $"Missed {target.ActorId}; controller AP {record.ResultingBudget.ActionPoints}.");
                CancelTargeting();
            }
            catch (InvalidOperationException exception)
            {
                dialogue.Append(GameplayDialogueChannel.System,
                    "DRONE COMMAND REJECTED", exception.Message);
            }
        }

        private bool TryFindControllerDrone(
            string actorId,
            out DroneSnapshot result)
        {
            if (drones != null)
                foreach (DroneSnapshot drone in drones.CaptureDrones())
                    if (string.Equals(
                        drone.Definition.ControllerActorId,
                        actorId,
                        StringComparison.Ordinal))
                    {
                        result = drone;
                        return true;
                    }
            result = default;
            return false;
        }

        private bool TryAcquireDrone(
            Ray ray,
            out DroneSnapshot result)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                250f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(
                right.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null) continue;
                foreach (DroneSnapshot drone in drones.CaptureDrones())
                {
                    Transform root = roots[drone.DroneId].transform;
                    Transform candidate = hit.collider.transform;
                    if (drone.IsOperational
                        && (candidate == root || candidate.IsChildOf(root)))
                    {
                        result = drone;
                        return true;
                    }
                }
            }
            result = default;
            return false;
        }

        private static GameplayPosition ToGameplayPosition(Vector3 value) =>
            new GameplayPosition(value.x, value.y, value.z);

        private static float CalculateFacing(
            GameplayPosition origin,
            GameplayPosition destination)
        {
            float dx = destination.X - origin.X;
            float dz = destination.Z - origin.Z;
            return Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
        }
    }
}
