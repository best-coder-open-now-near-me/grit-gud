using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;
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
        internal const string DismissOptionId = "drone.dismiss";
        internal const int HotbarSlot = 8;

        private enum CommandMode { None, Summon, Move, Attack }

        private readonly Dictionary<string, GameObject> roots =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private GameplayDroneSession drones;
        private GameplaySession gameplay;
        private GameplayWorldRegistry registry;
        private GameplaySmokeFieldSession smoke;
        private DestructiblePropSession destructibles;
        private GameplayScenarioAssembly assembly;
        private GameplayHeadlessSpatialEvidence spatial;
        private DronePresentationCatalog presentations;
        private Transform presentationParent;
        private GameplayDialogueLog dialogue;
        private Func<Vector2, bool> pointerBlocked;
        private Action<string> hotbarChanged;
        private string commandDroneId;
        private string commandSummonerId;
        private CommandMode mode;
        private bool replayPresentation;
        private int replayPresentedDischargeCount;

        public bool IsTargeting => mode != CommandMode.None;
        public GameplayDroneSession Session => drones;
        internal int ReplayPresentedDischargeCount =>
            replayPresentedDischargeCount;
        internal int ReplayTransientVisualCount
        {
            get
            {
                int count = 0;
                foreach (GameObject root in roots.Values)
                    count += root.GetComponent<GameplayDroneVisualPresenter>()?
                        .ReplayTransientVisualCount ?? 0;
                return count;
            }
        }

        internal void Bind(
            LevelWorld world,
            GameplaySession gameplaySession,
            GameplayWorldRegistry worldRegistry,
            GameplayScenarioAssembly scenarioAssembly,
            GameplayStaticSpatialContent spatialContent,
            DestructiblePropSession destructibles,
            GameplaySmokeFieldSession smokeFields,
            GameplayDialogueLog dialogueLog,
            uint randomSeed,
            Func<Vector2, bool> isPointerBlocked,
            Action<string> onHotbarChanged = null,
            DronePresentationCatalog presentationCatalog = null)
        {
            Unbind();
            gameplay = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            registry = worldRegistry ?? throw new ArgumentNullException(
                nameof(worldRegistry));
            assembly = scenarioAssembly ?? throw new ArgumentNullException(
                nameof(scenarioAssembly));
            if (spatialContent == null)
                throw new ArgumentNullException(nameof(spatialContent));
            spatial = spatialContent.CreateEvidence();
            presentations = presentationCatalog
                ?? DronePresentationCatalog.LoadDefault();
            presentationParent = (world ?? throw new ArgumentNullException(
                nameof(world))).Root.transform;
            this.destructibles = destructibles;
            smoke = smokeFields;
            dialogue = dialogueLog ?? throw new ArgumentNullException(
                nameof(dialogueLog));
            pointerBlocked = isPointerBlocked;
            hotbarChanged = onHotbarChanged;
            drones = new GameplayDroneSession(
                gameplay,
                assembly.DroneArchetypes,
                destructibles);
            enabled = assembly.DroneSummonAbilities.Count > 0;
        }

        public bool TryToggle(string summonerActorId, string optionId)
        {
            SummonedDroneSnapshot drone;
            if (!TryFindSummonerDrone(summonerActorId, out drone))
            {
                if (optionId != null
                    || assembly.GetDroneSummonAbilities(
                        summonerActorId).Count == 0)
                    return false;
                if (mode == CommandMode.Summon)
                {
                    CancelTargeting();
                    return true;
                }
                mode = CommandMode.Summon;
                commandDroneId = null;
                commandSummonerId = summonerActorId;
                dialogue.Append(
                    GameplayDialogueChannel.System,
                    "SUMMON DRONE",
                    "Select a clear deployment position within summon range.");
                return true;
            }
            if (string.Equals(
                    optionId,
                    DismissOptionId,
                    StringComparison.Ordinal))
                return TryDismiss(drone);
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
            commandSummonerId = drone.SummonerActorId;
            dialogue.Append(
                GameplayDialogueChannel.System,
                "SCOUT DRONE",
                requested == CommandMode.Move
                    ? "Select a destination within the drone movement radius."
                    : "Select a visible hostile actor for the drone weapon.");
            return true;
        }

        internal bool HasActiveSummon(string summonerActorId) =>
            TryFindSummonerDrone(summonerActorId, out _);

        public void CancelTargeting()
        {
            mode = CommandMode.None;
            commandDroneId = null;
            commandSummonerId = null;
        }

        internal void RefreshAuthoritativePresentation()
        {
            if (drones == null)
                throw new InvalidOperationException(
                    "Bind drones before refreshing their presentation.");
            Reconcile(drones.CaptureDrones());
            foreach (ScenarioDroneSummonRuntimeDefinition ability in
                assembly.DroneSummonAbilities)
                hotbarChanged?.Invoke(ability.SummonerActorId);
        }

        internal void BeginReplayPresentation()
        {
            if (drones == null)
                throw new InvalidOperationException(
                    "Bind drones before beginning replay presentation.");
            if (replayPresentation)
                throw new InvalidOperationException(
                    "Drone replay presentation is already active.");
            CancelTargeting();
            ClearReplayTransients();
            replayPresentedDischargeCount = 0;
            replayPresentation = true;
        }

        internal void PresentReplay(IReadOnlyList<SummonedDroneSnapshot> snapshots)
        {
            if (!replayPresentation)
                throw new InvalidOperationException(
                    "Begin drone replay presentation before projecting state.");
            if (snapshots == null) throw new ArgumentNullException(
                nameof(snapshots));
            Reconcile(snapshots);
        }

        internal void PresentReplayEvent(
            ReplayCombatPresentationEvent presentationEvent)
        {
            if (!replayPresentation)
                throw new InvalidOperationException(
                    "Begin drone replay presentation before projecting events.");
            if (presentationEvent == null) throw new ArgumentNullException(
                nameof(presentationEvent));
            if (presentationEvent.ShooterKind !=
                    ReplayCombatPresentationSubjectKind.Drone
                || !roots.TryGetValue(
                    presentationEvent.ShooterId,
                    out GameObject root))
                throw new InvalidOperationException(
                    $"Replay transition {presentationEvent.TransitionSequence} "
                    + $"has no drone shooter '{presentationEvent.ShooterId}'.");
            GameplayDroneVisualPresenter visual = root.GetComponent<
                GameplayDroneVisualPresenter>()
                ?? throw new InvalidOperationException(
                    $"Replay drone '{presentationEvent.ShooterId}' has no visual presenter.");
            visual.PresentReplayDischarge(
                presentationEvent.PresentationId,
                presentationEvent.Origin,
                presentationEvent.Destination,
                presentationEvent.Outcome !=
                        ReplayCombatPresentationOutcome.Miss
                    && presentationEvent.Outcome !=
                        ReplayCombatPresentationOutcome.Blocked);
            replayPresentedDischargeCount++;
        }

        internal void ClearReplayTransients()
        {
            foreach (GameObject root in roots.Values)
                root.GetComponent<GameplayDroneVisualPresenter>()?
                    .ClearReplayTransients();
        }

        internal void EndReplayPresentation()
        {
            if (!replayPresentation) return;
            ClearReplayTransients();
            replayPresentation = false;
            RefreshAuthoritativePresentation();
        }

        internal Transform GetPresentationTransform(string droneId)
        {
            if (!roots.TryGetValue(droneId, out GameObject root))
                throw new KeyNotFoundException(
                    $"Drone '{droneId}' has no presentation root.");
            return root.transform;
        }

        public bool TryAttackDroneAtPointer(
            string attackingActorId,
            Ray pointerRay)
        {
            if (drones == null
                || string.IsNullOrWhiteSpace(attackingActorId)
                || !TryAcquireDrone(pointerRay, out SummonedDroneSnapshot drone))
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
            GameplayActorSnapshot attackingActor = gameplay.GetActor(
                attackingActorId);
            if (!GameplayInjuryCapabilityProjection.CanUseAttack(
                    attackingActor.Capabilities,
                    attack))
                return false;
            GameplayPosition origin = attackingActor.Pose.Position;
            int accuracyDelta = GameplayInjuryCapabilityProjection
                .CalculateAccuracyDeltaPercent(attackingActor.Capabilities);
            float bestDistance = float.PositiveInfinity;
            foreach (SummonedDroneSnapshot drone in drones.CaptureDrones())
            {
                if (!drone.IsOperational
                    || !gameplay.IsHostile(
                        attackingActorId,
                        drone.SummonerActorId))
                    continue;
                DroneExposureSnapshot exposure = CaptureActorExposure(
                    attackingActorId,
                    drone);
                float distance = origin.DistanceTo(drone.Position);
                int hitChance = DroneDirectAttackRules
                    .CalculateHitChancePercent(
                        attack,
                        exposure,
                        distance,
                        accuracyDelta);
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
            SummonedDroneSnapshot drone = drones.GetDrone(exposure.DroneId);
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
                        "resolution"),
                    spatial.ResolveDroneCrashTrajectory(
                        drone.Position,
                        origin,
                        drone.Definition.Crash.MaximumDriftDistance,
                        checked(gameplay.LastTransitionSequence + 1L)));
                drones.CommitActorAttack(record);
                if (record.Damage?.StartedCrash == true)
                    CommitCrashImpact(
                        attackingActorId,
                        record.Damage.Resulting);
                RefreshAuthoritativePresentation();
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

        private void CommitCrashImpact(
            string advancingActorId,
            SummonedDroneSnapshot crashing)
        {
            GameplayCombatStateSnapshot state = GameplayCombatStateCapture
                .Capture(
                    gameplay,
                    destructibles,
                    smokeFields: smoke,
                    drones: drones);
            var candidate = new GameplayCandidate(
                "live.drone-crash." + crashing.DroneId,
                GameplayCapabilityProfiles.AdvanceDroneCrash(),
                advancingActorId,
                crashing.DroneId,
                new GameplayDroneCrashIntent(crashing.DroneId));
            var context = new GameplayDecisionContext(
                state,
                GameplayObservationSnapshot.FullState(
                    advancingActorId,
                    state));
            var route = new GameplayDroneCrashCandidateExecutionRoute(spatial);
            GameplayExecutableCandidateEvaluation evaluation = route.Evaluate(
                context,
                candidate);
            if (!evaluation.IsLegal)
                throw new InvalidOperationException(
                    "Drone crash impact preparation failed: "
                    + evaluation.FailureCode);
            var payload = (GameplayDroneCrashImpactTransitionPayload)
                route.PreparePayload(context, evaluation);
            drones.CommitCrashImpact(
                advancingActorId,
                payload.Impact,
                evaluation.Evidence);
        }

        private DroneExposureSnapshot CaptureActorExposure(
            string attackingActorId,
            SummonedDroneSnapshot drone)
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
            EndReplayPresentation();
            CancelTargeting();
            foreach (GameObject root in roots.Values)
                DestroyPresentationRoot(root);
            roots.Clear();
            drones = null;
            gameplay = null;
            registry = null;
            destructibles = null;
            assembly = null;
            spatial = null;
            presentations = null;
            presentationParent = null;
            smoke = null;
            dialogue = null;
            pointerBlocked = null;
            hotbarChanged = null;
            replayPresentation = false;
            replayPresentedDischargeCount = 0;
            enabled = false;
        }

        private void ApplySnapshot(SummonedDroneSnapshot snapshot)
        {
            if (!snapshot.IsVisible)
            {
                if (roots.TryGetValue(
                        snapshot.DroneId,
                        out GameObject hidden))
                {
                    roots.Remove(snapshot.DroneId);
                    DestroyPresentationRoot(hidden);
                }
                return;
            }
            if (!roots.TryGetValue(snapshot.DroneId, out GameObject root))
            {
                DronePresentationDefinition definition = presentations.Get(
                    snapshot.Definition.PresentationId);
                root = definition.Prefab == null
                    ? new GameObject(snapshot.DroneId)
                    : Instantiate(definition.Prefab, presentationParent);
                root.name = snapshot.DroneId;
                root.transform.SetParent(presentationParent, worldPositionStays: true);
                GameplayDroneVisualPresenter visual = root.GetComponent<
                    GameplayDroneVisualPresenter>()
                    ?? root.AddComponent<GameplayDroneVisualPresenter>();
                visual.Build();
                roots.Add(snapshot.DroneId, root);
            }
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

        private void Reconcile(
            IReadOnlyList<SummonedDroneSnapshot> snapshots)
        {
            var retained = new HashSet<string>(StringComparer.Ordinal);
            foreach (SummonedDroneSnapshot snapshot in snapshots)
            {
                if (snapshot.IsVisible) retained.Add(snapshot.DroneId);
                ApplySnapshot(snapshot);
            }
            var removed = new List<string>();
            foreach (KeyValuePair<string, GameObject> entry in roots)
                if (!retained.Contains(entry.Key)) removed.Add(entry.Key);
            foreach (string droneId in removed)
            {
                GameObject root = roots[droneId];
                roots.Remove(droneId);
                DestroyPresentationRoot(root);
            }
        }

        private static void DestroyPresentationRoot(GameObject root)
        {
            if (root == null) return;
            if (UnityEngine.Application.isPlaying)
                UnityEngine.Object.Destroy(root);
            else UnityEngine.Object.DestroyImmediate(root);
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
            if (mode == CommandMode.Summon) TryCommitSummon(ray);
            else if (mode == CommandMode.Move) TryCommitMove(ray);
            else TryCommitAttack(ray);
        }

        private void TryCommitSummon(Ray ray)
        {
            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    250f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
                return;
            IReadOnlyList<ScenarioDroneSummonRuntimeDefinition> abilities =
                assembly.GetDroneSummonAbilities(commandSummonerId);
            if (abilities.Count == 0) return;
            ScenarioDroneSummonRuntimeDefinition runtime = abilities[0];
            DroneArchetypeDefinition archetype = assembly.GetDroneArchetype(
                runtime.Ability.DroneArchetypeId);
            GameplayActorSnapshot summoner = gameplay.GetActor(
                commandSummonerId);
            var position = new GameplayPosition(
                hit.point.x,
                hit.point.y + runtime.Ability.SpawnHeight,
                hit.point.z);
            GameplayCombatStateSnapshot state = GameplayCombatStateCapture
                .Capture(
                    gameplay,
                    destructibles,
                    smokeFields: smoke,
                    drones: drones);
            if (summoner.Pose.Position.DistanceTo(position)
                    > runtime.Ability.MaximumSpawnDistance
                || spatial.BlocksPath(
                    state,
                    summoner.Pose.Position,
                    position,
                    clearanceRadius: 0.25f))
            {
                dialogue.Append(
                    GameplayDialogueChannel.System,
                    "DRONE SUMMON REJECTED",
                    "Choose a clear position within summon range.");
                return;
            }
            try
            {
                var record = new SummonDroneRecord(
                    checked(gameplay.LastTransitionSequence + 1L),
                    summoner.ActorId,
                    runtime.Ability,
                    archetype,
                    position,
                    summoner.Pose.FacingDegrees,
                    summoner.TurnBudget,
                    summoner.TurnBudget.SpendAction(
                        runtime.Ability.SummonCost));
                drones.CommitSummon(record);
                RefreshAuthoritativePresentation();
                dialogue.Append(
                    GameplayDialogueChannel.System,
                    "SCOUT DRONE",
                    $"Summoned {record.DroneInstanceId}; shared AP {record.ResultingBudget.ActionPoints}.");
                CancelTargeting();
            }
            catch (InvalidOperationException exception)
            {
                dialogue.Append(
                    GameplayDialogueChannel.System,
                    "DRONE SUMMON REJECTED",
                    exception.Message);
            }
        }

        private bool TryDismiss(SummonedDroneSnapshot drone)
        {
            GameplayActorSnapshot summoner = gameplay.GetActor(
                drone.SummonerActorId);
            var cost = new ActionCost(0, 0f, ActionMobility.Mobile);
            try
            {
                SummonedDroneSnapshot dismissed = drone.WithLifecycle(
                    SummonLifecycleState.Dismissed,
                    drone.RemainingIntegrity,
                    drone.RemainingDurationTurns);
                var record = new DismissDroneRecord(
                    checked(gameplay.LastTransitionSequence + 1L),
                    summoner.ActorId,
                    cost,
                    summoner.TurnBudget,
                    summoner.TurnBudget.SpendAction(cost),
                    drone,
                    dismissed);
                drones.CommitDismiss(record);
                RefreshAuthoritativePresentation();
                CancelTargeting();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                dialogue.Append(
                    GameplayDialogueChannel.System,
                    "DRONE DISMISS REJECTED",
                    exception.Message);
                return false;
            }
        }

        private void TryCommitMove(Ray ray)
        {
            if (!Physics.Raycast(ray, out RaycastHit hit, 250f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return;
            SummonedDroneSnapshot drone = drones.GetDrone(commandDroneId);
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
            SummonedDroneSnapshot drone = drones.GetDrone(commandDroneId);
            GameObject root = roots[drone.DroneId];
            var query = new UnityPointerTargetQuery(
                root.transform,
                registry,
                Physics.DefaultRaycastLayers,
                candidate => candidate.Targetable
                    && !gameplay.IsActorIncapacitated(candidate.ActorId)
                    && gameplay.IsHostile(
                        drone.SummonerActorId,
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
                drone.SummonerActorId,
                target.ActorId);
            AttackResolutionRecord resolution = AttackResolutionRules.Resolve(
                resolutionSequence,
                GameplayAddressedRandom.SampleUInt32(
                    gameplay.RunIdentity,
                    transitionIdentity,
                    "resolution"),
                exposure,
                drone.Position.DistanceTo(targetState.Pose.Position),
                targetState.Wounds,
                drone.Definition.Attack,
                targetInjuryStateBefore: targetState.Injuries);
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

        private bool TryFindSummonerDrone(
            string summonerActorId,
            out SummonedDroneSnapshot result)
        {
            if (drones != null)
                foreach (SummonedDroneSnapshot drone in drones.CaptureDrones())
                    if (string.Equals(
                        drone.SummonerActorId,
                        summonerActorId,
                        StringComparison.Ordinal)
                        && drone.IsOperational)
                    {
                        result = drone;
                        return true;
                    }
            result = default;
            return false;
        }

        private bool TryAcquireDrone(
            Ray ray,
            out SummonedDroneSnapshot result)
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
                foreach (SummonedDroneSnapshot drone in drones.CaptureDrones())
                {
                    if (!drone.IsOperational
                        || !roots.TryGetValue(
                            drone.DroneId,
                            out GameObject rootObject))
                        continue;
                    Transform root = rootObject.transform;
                    Transform candidate = hit.collider.transform;
                    if (candidate == root || candidate.IsChildOf(root))
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
