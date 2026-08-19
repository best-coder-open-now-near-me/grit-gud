using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    [Flags]
    public enum GameplayCombatStateCoverage
    {
        None = 0,
        Session = 1 << 0,
        Destructibles = 1 << 1,
        Vehicles = 1 << 2,
        Projectiles = 1 << 3,
        SmokeFields = 1 << 4,
    }

    public sealed class GameplaySessionStateSnapshot
    {
        public const int CurrentSchemaVersion = 4;

        public GameplaySessionStateSnapshot(
            string scenarioId,
            GameplaySessionMode mode,
            GameplaySessionOperation operation,
            TurnModeContext turnContext,
            bool encounterActive,
            bool encounterCompletionRequested,
            string activeActorId,
            GameplayTurnPhase turnPhase,
            IEnumerable<GameplayActorSnapshot> actors,
            IEnumerable<string> initiativeOrder,
            IEnumerable<GameplayObjectiveSnapshot> objectives,
            IEnumerable<string> emergencyResponders,
            int emergencyResponderIndex,
            string emergencyResumeActorId,
            long lastActionSequence,
            long lastTurnSequence,
            long journalSequence,
            ScenarioRunIdentity runIdentity = null,
            long revision = 0L,
            float voluntaryTurnReentrySecondsRemaining = 0f,
            MovementRouteRecord pendingMovementRoute = null,
            VoluntaryTurnCycleRecord pendingVoluntaryTurnCycle = null,
            long lastTransitionSequence = 0L,
            long lastVoluntaryTurnCycleSequence = 0L,
            GameplayEncounterStateSnapshot encounterState = null,
            IEnumerable<string> allInitiativeOrder = null)
        {
            if (!Enum.IsDefined(typeof(GameplaySessionMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (!Enum.IsDefined(typeof(GameplaySessionOperation), operation))
                throw new ArgumentOutOfRangeException(nameof(operation));
            if (!Enum.IsDefined(typeof(TurnModeContext), turnContext))
                throw new ArgumentOutOfRangeException(nameof(turnContext));
            if (!Enum.IsDefined(typeof(GameplayTurnPhase), turnPhase))
                throw new ArgumentOutOfRangeException(nameof(turnPhase));
            if (lastActionSequence < 0 || lastTurnSequence < 0
                || journalSequence < 0 || revision < 0
                || lastTransitionSequence < 0
                || lastVoluntaryTurnCycleSequence < 0)
                throw new ArgumentOutOfRangeException(nameof(journalSequence));
            GameplayNumericPolicy.RequireFinite(
                voluntaryTurnReentrySecondsRemaining,
                nameof(voluntaryTurnReentrySecondsRemaining));
            if (voluntaryTurnReentrySecondsRemaining < 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(voluntaryTurnReentrySecondsRemaining));

            if (string.IsNullOrWhiteSpace(scenarioId))
                throw new ArgumentException("Combat state requires a scenario ID.",
                    nameof(scenarioId));
            SchemaVersion = CurrentSchemaVersion;
            ScenarioId = scenarioId;
            Mode = mode;
            Operation = operation;
            TurnContext = turnContext;
            EncounterActive = encounterActive;
            EncounterCompletionRequested = encounterCompletionRequested;
            ActiveActorId = activeActorId ?? string.Empty;
            TurnPhase = turnPhase;
            Actors = CopyActors(actors);
            InitiativeOrder = CopyIds(initiativeOrder, "initiative actor");
            AllInitiativeOrder = CopyIds(
                allInitiativeOrder ?? initiativeOrder,
                "all-actor initiative");
            Objectives = CopyObjectives(objectives);
            EmergencyResponders = CopyIds(
                emergencyResponders, "emergency responder", allowEmpty: true);
            if (emergencyResponderIndex < -1
                || emergencyResponderIndex > EmergencyResponders.Count)
                throw new ArgumentOutOfRangeException(nameof(emergencyResponderIndex));
            EmergencyResponderIndex = emergencyResponderIndex;
            EmergencyResumeActorId = emergencyResumeActorId ?? string.Empty;
            LastActionSequence = lastActionSequence;
            LastTurnSequence = lastTurnSequence;
            JournalSequence = journalSequence;
            RunIdentity = runIdentity ?? new ScenarioRunIdentity(
                scenarioId + ".run",
                scenarioSeed: 0u);
            Revision = revision;
            VoluntaryTurnReentrySecondsRemaining =
                voluntaryTurnReentrySecondsRemaining;
            PendingMovementRoute = pendingMovementRoute;
            PendingVoluntaryTurnCycle = pendingVoluntaryTurnCycle;
            LastTransitionSequence = lastTransitionSequence;
            LastVoluntaryTurnCycleSequence =
                lastVoluntaryTurnCycleSequence;
            EncounterState = encounterState
                ?? new GameplayEncounterStateSnapshot();
        }

        public int SchemaVersion { get; }
        public string ScenarioId { get; }
        public GameplaySessionMode Mode { get; }
        public GameplaySessionOperation Operation { get; }
        public TurnModeContext TurnContext { get; }
        public bool EncounterActive { get; }
        public bool EncounterCompletionRequested { get; }
        public string ActiveActorId { get; }
        public GameplayTurnPhase TurnPhase { get; }
        public IReadOnlyList<GameplayActorSnapshot> Actors { get; }
        public IReadOnlyList<string> InitiativeOrder { get; }
        public IReadOnlyList<string> AllInitiativeOrder { get; }
        public IReadOnlyList<GameplayObjectiveSnapshot> Objectives { get; }
        public IReadOnlyList<string> EmergencyResponders { get; }
        public int EmergencyResponderIndex { get; }
        public string EmergencyResumeActorId { get; }
        public long LastActionSequence { get; }
        public long LastTurnSequence { get; }
        public long JournalSequence { get; }
        public ScenarioRunIdentity RunIdentity { get; }
        public long Revision { get; }
        public float VoluntaryTurnReentrySecondsRemaining { get; }
        public MovementRouteRecord PendingMovementRoute { get; }
        public VoluntaryTurnCycleRecord PendingVoluntaryTurnCycle { get; }
        public long LastTransitionSequence { get; }
        public long LastVoluntaryTurnCycleSequence { get; }
        public GameplayEncounterStateSnapshot EncounterState { get; }

        public GameplayActorSnapshot GetActor(string actorId)
        {
            foreach (GameplayActorSnapshot actor in Actors)
                if (string.Equals(actor.ActorId, actorId, StringComparison.Ordinal))
                    return actor;
            throw new KeyNotFoundException($"Actor snapshot '{actorId}' was not found.");
        }

        private static IReadOnlyList<GameplayActorSnapshot> CopyActors(
            IEnumerable<GameplayActorSnapshot> actors)
        {
            if (actors == null) throw new ArgumentNullException(nameof(actors));
            var copy = new List<GameplayActorSnapshot>(actors);
            if (copy.Count == 0)
                throw new ArgumentException("Combat state requires actors.", nameof(actors));
            copy.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.ActorId, right.ActorId));
            for (int index = 1; index < copy.Count; index++)
                if (string.Equals(copy[index - 1].ActorId, copy[index].ActorId,
                    StringComparison.Ordinal))
                    throw new ArgumentException("Actor snapshots must be unique.", nameof(actors));
            return copy.AsReadOnly();
        }

        private static IReadOnlyList<GameplayObjectiveSnapshot> CopyObjectives(
            IEnumerable<GameplayObjectiveSnapshot> objectives)
        {
            if (objectives == null)
                throw new ArgumentNullException(nameof(objectives));
            var copy = new List<GameplayObjectiveSnapshot>(objectives);
            copy.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.ObjectiveId, right.ObjectiveId));
            for (int index = 1; index < copy.Count; index++)
                if (string.Equals(copy[index - 1].ObjectiveId,
                    copy[index].ObjectiveId, StringComparison.Ordinal))
                    throw new ArgumentException("Objective snapshots must be unique.",
                        nameof(objectives));
            return copy.AsReadOnly();
        }

        private static IReadOnlyList<string> CopyIds(
            IEnumerable<string> ids,
            string label,
            bool allowEmpty = false)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            var copy = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                if (string.IsNullOrWhiteSpace(id) || !unique.Add(id))
                    throw new ArgumentException($"Invalid or duplicate {label} ID.",
                        nameof(ids));
                copy.Add(id);
            }
            if (!allowEmpty && copy.Count == 0)
                throw new ArgumentException($"Combat state requires a {label}.",
                    nameof(ids));
            return copy.AsReadOnly();
        }
    }

    public sealed class GameplayCombatStateSnapshot
    {
        public const int CurrentSchemaVersion = 4;

        public GameplayCombatStateSnapshot(
            GameplaySessionStateSnapshot session,
            IEnumerable<DestructiblePropSnapshot> destructibles = null,
            IEnumerable<VehicleMomentumState> vehicles = null,
            IEnumerable<ProjectileFlightSnapshot> projectiles = null,
            IEnumerable<SmokeFieldSnapshot> smokeFields = null,
            GameplayCombatStateCoverage coverage =
                GameplayCombatStateCoverage.Session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            if ((coverage & GameplayCombatStateCoverage.Session) == 0
                || (coverage & ~AllCoverage) != 0)
                throw new ArgumentOutOfRangeException(nameof(coverage));
            Coverage = coverage;
            Destructibles = CopyAndSort(
                destructibles, value => value.PropId, "destructible");
            Vehicles = CopyAndSort(
                vehicles, value => value.VehicleId, "vehicle");
            Projectiles = CopyAndSort(
                projectiles, value => value.ProjectileId, "projectile");
            SmokeFields = CopyAndSort(
                smokeFields, value => value.Field.Id, "smoke field");
            CanonicalHash = GameplayCombatStateHasher.Hash(this);
        }

        public int SchemaVersion => CurrentSchemaVersion;
        public static GameplayCombatStateCoverage AllCoverage =>
            GameplayCombatStateCoverage.Session
            | GameplayCombatStateCoverage.Destructibles
            | GameplayCombatStateCoverage.Vehicles
            | GameplayCombatStateCoverage.Projectiles
            | GameplayCombatStateCoverage.SmokeFields;
        public GameplaySessionStateSnapshot Session { get; }
        public GameplayCombatStateCoverage Coverage { get; }
        public IReadOnlyList<DestructiblePropSnapshot> Destructibles { get; }
        public IReadOnlyList<VehicleMomentumState> Vehicles { get; }
        public IReadOnlyList<ProjectileFlightSnapshot> Projectiles { get; }
        public IReadOnlyList<SmokeFieldSnapshot> SmokeFields { get; }
        public string CanonicalHash { get; }

        public bool Covers(GameplayCombatStateCoverage required) =>
            (Coverage & required) == required;

        public void RequireCoverage(GameplayCombatStateCoverage required)
        {
            if (!Covers(required))
                throw new InvalidOperationException(
                    $"Canonical state does not cover required components '{required}'.");
        }

        private static IReadOnlyList<T> CopyAndSort<T>(
            IEnumerable<T> values,
            Func<T, string> getId,
            string label)
        {
            var copy = new List<T>(values ?? Array.Empty<T>());
            copy.Sort((left, right) => StringComparer.Ordinal.Compare(
                getId(left), getId(right)));
            for (int index = 0; index < copy.Count; index++)
            {
                string id = getId(copy[index]);
                if (string.IsNullOrWhiteSpace(id))
                    throw new ArgumentException($"A {label} snapshot has no identifier.");
                if (index > 0 && string.Equals(
                    getId(copy[index - 1]), id, StringComparison.Ordinal))
                    throw new ArgumentException($"Duplicate {label} snapshot '{id}'.");
            }
            return copy.AsReadOnly();
        }
    }

    public static class GameplayCombatStateCapture
    {
        public static GameplayCombatStateSnapshot Capture(
            GameplaySession gameplay,
            DestructiblePropSession destructibles = null,
            IEnumerable<VehicleMomentumSession> vehicles = null,
            GameplayProjectileSession projectiles = null,
            GameplaySmokeFieldSession smokeFields = null)
        {
            if (gameplay == null) throw new ArgumentNullException(nameof(gameplay));
            var actors = new List<GameplayActorSnapshot>();
            foreach (string actorId in gameplay.AllActorIds)
                actors.Add(gameplay.GetActor(actorId));
            var objectives = new List<GameplayObjectiveSnapshot>();
            foreach (ScenarioObjectiveDefinition definition in gameplay.Scenario.Objectives)
                objectives.Add(gameplay.GetObjective(definition.Id));
            var vehicleStates = new List<VehicleMomentumState>();
            foreach (VehicleMomentumSession vehicle in
                vehicles ?? Array.Empty<VehicleMomentumSession>())
                vehicleStates.Add(vehicle.State);
            var propStates = new List<DestructiblePropSnapshot>();
            if (destructibles != null)
                foreach (string propId in destructibles.PropIds)
                    propStates.Add(destructibles.GetProp(propId));
            var projectileStates = new List<ProjectileFlightSnapshot>();
            if (projectiles != null)
                foreach (string projectileId in projectiles.ProjectileIds)
                    projectileStates.Add(projectiles.GetProjectile(projectileId));

            var session = new GameplaySessionStateSnapshot(
                gameplay.Scenario.Id,
                gameplay.Mode,
                gameplay.Operation,
                gameplay.TurnContext,
                gameplay.EncounterActive,
                gameplay.EncounterCompletionRequested,
                gameplay.ActiveActorId,
                gameplay.TurnPhase,
                actors,
                gameplay.InitiativeOrder,
                objectives,
                gameplay.EmergencyResponders,
                gameplay.EmergencyResponderIndex,
                gameplay.EmergencyResumeActorId,
                gameplay.LastResolvedAction?.Sequence ?? 0L,
                gameplay.LastEndedTurn?.Sequence ?? 0L,
                gameplay.Journal.LastEntry?.Sequence ?? 0L,
                gameplay.RunIdentity,
                gameplay.Revision,
                gameplay.VoluntaryTurnReentrySecondsRemaining,
                gameplay.PendingMovementRoute,
                gameplay.PendingVoluntaryTurnCycle,
                gameplay.LastTransitionSequence,
                gameplay.LastCompletedVoluntaryTurnCycle?.Sequence ?? 0L,
                gameplay.EncounterState,
                gameplay.AllInitiativeOrder);
            GameplayCombatStateCoverage coverage =
                GameplayCombatStateCoverage.Session;
            if (destructibles != null)
                coverage |= GameplayCombatStateCoverage.Destructibles;
            if (vehicles != null)
                coverage |= GameplayCombatStateCoverage.Vehicles;
            if (projectiles != null)
                coverage |= GameplayCombatStateCoverage.Projectiles;
            if (smokeFields != null)
                coverage |= GameplayCombatStateCoverage.SmokeFields;
            return new GameplayCombatStateSnapshot(
                session,
                propStates,
                vehicleStates,
                projectileStates,
                smokeFields?.CaptureActiveFields(),
                coverage);
        }
    }

    public static class GameplayCombatStateHasher
    {
        public static string Hash(GameplayCombatStateSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            string canonical = BuildCanonical(snapshot);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                var result = new StringBuilder(bytes.Length * 2);
                foreach (byte value in bytes) result.Append(value.ToString("x2"));
                return result.ToString();
            }
        }

        internal static string BuildCanonical(GameplayCombatStateSnapshot state)
        {
            var text = new StringBuilder();
            GameplaySessionStateSnapshot session = state.Session;
            Append(text, "schema", state.SchemaVersion);
            Append(text, "coverage", (int)state.Coverage);
            Append(text, "session.schema", session.SchemaVersion);
            Append(text, "scenario", session.ScenarioId);
            Append(text, "run.schema", session.RunIdentity.SchemaVersion);
            Append(text, "run.id", session.RunIdentity.RunId);
            Append(text, "run.seed", session.RunIdentity.ScenarioSeed);
            Append(text, "run.randomSchema",
                session.RunIdentity.RandomSchemaVersion);
            Append(text, "revision", session.Revision);
            Append(text, "mode", (int)session.Mode);
            Append(text, "operation", (int)session.Operation);
            Append(text, "context", (int)session.TurnContext);
            Append(text, "encounter", session.EncounterActive);
            Append(text, "completion", session.EncounterCompletionRequested);
            Append(text, "active", session.ActiveActorId);
            Append(text, "phase", (int)session.TurnPhase);
            Append(text, "action.sequence", session.LastActionSequence);
            Append(text, "turn.sequence", session.LastTurnSequence);
            Append(text, "journal.sequence", session.JournalSequence);
            Append(text, "transition.sequence", session.LastTransitionSequence);
            Append(text, "voluntary.lastSequence",
                session.LastVoluntaryTurnCycleSequence);
            Append(text, "voluntary.reentrySeconds",
                session.VoluntaryTurnReentrySecondsRemaining);
            AppendPendingMovement(text, session.PendingMovementRoute);
            AppendPendingVoluntaryCycle(
                text,
                session.PendingVoluntaryTurnCycle);
            for (int index = 0; index < session.InitiativeOrder.Count; index++)
                Append(text, "initiative." + index, session.InitiativeOrder[index]);
            for (int index = 0; index < session.AllInitiativeOrder.Count; index++)
            {
                Append(text, "allInitiative." + index,
                    session.AllInitiativeOrder[index]);
            }
            for (int index = 0;
                index < session.EncounterState.ParticipantIds.Count;
                index++)
            {
                Append(text, "encounter.participant." + index,
                    session.EncounterState.ParticipantIds[index]);
            }
            Append(text, "encounter.transition.sequence",
                session.EncounterState.LastTransitionSequence);
            foreach (EnemyAwarenessSnapshot awareness in
                session.EncounterState.Awareness)
            {
                string root = "awareness." + awareness.ActorId;
                Append(text, root + ".state", (int)awareness.State);
                Append(text, root + ".suspicion", awareness.Suspicion);
                Append(text, root + ".lastKnown.id",
                    awareness.LastKnownHostileId);
                Append(text, root + ".lastKnown.position",
                    awareness.LastKnownHostilePosition.HasValue
                        ? (object)awareness.LastKnownHostilePosition.Value
                        : string.Empty);
                Append(text, root + ".patrol.index",
                    awareness.PatrolWaypointIndex);
            }
            Append(text, "emergency.index", session.EmergencyResponderIndex);
            Append(text, "emergency.resume", session.EmergencyResumeActorId);
            for (int index = 0; index < session.EmergencyResponders.Count; index++)
                Append(text, "emergency.responder." + index,
                    session.EmergencyResponders[index]);
            foreach (GameplayActorSnapshot actor in session.Actors)
                AppendActor(text, actor);
            foreach (GameplayObjectiveSnapshot objective in session.Objectives)
            {
                string root = "objective." + objective.ObjectiveId;
                Append(text, root + ".position", objective.Position);
                Append(text, root + ".completed", objective.IsCompleted);
            }
            foreach (DestructiblePropSnapshot prop in state.Destructibles)
            {
                string root = "prop." + prop.PropId;
                Append(text, root + ".state", (int)prop.State);
                Append(text, root + ".maximum", prop.MaximumIntegrity);
                Append(text, root + ".remaining", prop.RemainingIntegrity);
                Append(text, root + ".position", prop.Pose.Position);
                Append(text, root + ".yaw", prop.Pose.YawDegrees);
                Append(text, root + ".pitch", prop.Pose.PitchDegrees);
                Append(text, root + ".roll", prop.Pose.RollDegrees);
                Append(text, root + ".posture", (int)prop.Posture);
                Append(text, root + ".fracture.count", prop.FractureChunkCount);
                Append(
                    text,
                    root + ".fracture.detached",
                    prop.DetachedFractureChunks.ToString());
            }
            foreach (VehicleMomentumState vehicle in state.Vehicles)
            {
                string root = "vehicle." + vehicle.VehicleId;
                Append(text, root + ".position", vehicle.Position);
                Append(text, root + ".forward", vehicle.ForwardDegrees);
                Append(text, root + ".speed", vehicle.Speed);
            }
            foreach (ProjectileFlightSnapshot projectile in state.Projectiles)
            {
                string root = "projectile." + projectile.ProjectileId;
                Append(text, root + ".position", projectile.Position);
                Append(text, root + ".launch.sequence", projectile.Launch.Sequence);
                Append(text, root + ".source", projectile.Launch.AttackerId);
                Append(text, root + ".target", projectile.Launch.IntendedTargetId);
                Append(text, root + ".action", projectile.Launch.ActionId);
                Append(text, root + ".origin", projectile.Launch.Origin);
                Append(text, root + ".aim", projectile.Launch.AimPoint);
                Append(text, root + ".definition.id",
                    projectile.Launch.Definition.Id);
                Append(text, root + ".definition.speed",
                    projectile.Launch.Definition.SpeedPerTurn);
                Append(text, root + ".definition.radius",
                    projectile.Launch.Definition.Radius);
                Append(text, root + ".definition.range",
                    projectile.Launch.Definition.MaximumRange);
                Append(text, root + ".definition.launchHeight.standing",
                    projectile.Launch.Definition.StandingLaunchHeight);
                Append(text, root + ".definition.launchHeight.crouched",
                    projectile.Launch.Definition.CrouchedLaunchHeight);
                Append(text, root + ".definition.emergency",
                    projectile.Launch.Definition.OpensEmergencyReactionWindow);
                Append(text, root + ".definition.blast.radius",
                    projectile.Launch.Definition.BlastRadius);
                Append(text, root + ".definition.blast.woundPenalty",
                    projectile.Launch.Definition.BlastWoundMovementPenalty);
                Append(text, root + ".definition.blast.integrityDamage",
                    projectile.Launch.Definition.BlastIntegrityDamage);
                Append(text, root + ".allowance.ap",
                    projectile.Launch.TurnActionPointAllowance);
                Append(text, root + ".remaining.ap",
                    projectile.Launch.RemainingActionPointsAfterLaunch);
                Append(text, root + ".distance", projectile.DistanceTraveled);
                Append(text, root + ".time", projectile.ElapsedTurnTime);
                Append(text, root + ".status", (int)projectile.Status);
                if (projectile.Impact != null)
                {
                    Append(text, root + ".impact.entity",
                        projectile.Impact.HitEntityId);
                    Append(text, root + ".impact.position",
                        projectile.Impact.Position);
                    Append(text, root + ".impact.time",
                        projectile.Impact.ArrivalTurnTime);
                    Append(text, root + ".impact.revision",
                        projectile.Impact.WorldStateRevision);
                    var effects = new List<BlastEffectRecord>(
                        projectile.Impact.BlastEffects);
                    effects.Sort(CompareBlastEffects);
                    for (int index = 0; index < effects.Count; index++)
                    {
                        BlastEffectRecord effect = effects[index];
                        string effectRoot = root + ".impact.effect." + index;
                        Append(text, effectRoot + ".entity", effect.EntityId);
                        Append(text, effectRoot + ".kind",
                            (int)effect.SubjectKind);
                        Append(text, effectRoot + ".distance", effect.Distance);
                        Append(text, effectRoot + ".occlusion",
                            effect.OcclusionExposure);
                        Append(text, effectRoot + ".falloff",
                            effect.DistanceFalloff);
                        Append(text, effectRoot + ".region",
                            effect.InjuryRegion.HasValue
                                ? (int)effect.InjuryRegion.Value
                                : -1);
                    }
                }
            }
            foreach (SmokeFieldSnapshot smoke in state.SmokeFields)
            {
                string root = "smoke." + smoke.Field.Id;
                Append(text, root + ".source.actor", smoke.Field.SourceActorId);
                Append(text, root + ".source.item", smoke.Field.SourceItemId);
                Append(text, root + ".origin", smoke.Field.Origin);
                Append(text, root + ".radius", smoke.Field.Definition.Radius);
                Append(text, root + ".height", smoke.Field.Definition.Height);
                Append(text, root + ".duration.exploration",
                    smoke.Field.Definition.ExplorationDurationSeconds);
                Append(text, root + ".duration.turns",
                    smoke.Field.Definition.DurationTurnEnds);
                Append(text, root + ".minimumObscuredPath",
                    smoke.Field.Definition.MinimumObscuredPath);
                Append(text, root + ".remaining", smoke.RemainingFraction);
            }
            return text.ToString();
        }

        private static void AppendPendingMovement(
            StringBuilder text,
            MovementRouteRecord route)
        {
            Append(text, "movement.pending", route != null);
            if (route == null) return;
            Append(text, "movement.actor", route.ActorId);
            Append(text, "movement.origin.position", route.OriginPose.Position);
            Append(text, "movement.origin.facing", route.OriginPose.FacingDegrees);
            Append(text, "movement.origin.stance", (int)route.OriginPose.Stance);
            Append(text, "movement.previous.ap", route.PreviousBudget.ActionPoints);
            Append(text, "movement.previous.move",
                route.PreviousBudget.MovementOpportunity);
            Append(text, "movement.frozenBudget", route.HasFrozenBudget);
            for (int index = 0; index < route.Segments.Count; index++)
            {
                MovementRouteSegmentRecord segment = route.Segments[index];
                string root = "movement.segment." + index;
                Append(text, root + ".from", segment.From);
                Append(text, root + ".to", segment.To);
                Append(text, root + ".kind", (int)segment.Kind);
                Append(text, root + ".link", segment.TraversalLinkId);
                Append(text, root + ".action", segment.ActionId);
                Append(text, root + ".moveCost", segment.MovementCost);
                Append(text, root + ".apCost", segment.ActionPointCost);
                Append(text, root + ".arcHeight", segment.ArcHeight);
                Append(text, root + ".duration",
                    segment.PlaybackDurationSeconds);
            }
        }

        private static void AppendPendingVoluntaryCycle(
            StringBuilder text,
            VoluntaryTurnCycleRecord cycle)
        {
            Append(text, "voluntary.pending", cycle != null);
            if (cycle == null) return;
            Append(text, "voluntary.sequence", cycle.Sequence);
            foreach (GameplayActorSnapshot actor in cycle.Actors)
                AppendActor(text, actor, "voluntary.actor.");
        }

        private static void AppendActor(
            StringBuilder text,
            GameplayActorSnapshot actor,
            string prefix = "actor.")
        {
            string root = prefix + actor.ActorId;
            Append(text, root + ".position", actor.Pose.Position);
            Append(text, root + ".facing", actor.Pose.FacingDegrees);
            Append(text, root + ".stance", (int)actor.Pose.Stance);
            Append(text, root + ".ap", actor.TurnBudget.ActionPoints);
            Append(text, root + ".move", actor.TurnBudget.MovementOpportunity);
            Append(text, root + ".allowance.ap", actor.TurnActionPointAllowance);
            Append(text, root + ".allowance.maximumAp", actor.MaximumActionPoints);
            Append(text, root + ".allowance.move", actor.TurnMovementAllowance);
            Append(text, root + ".allowance.emergencyAp",
                actor.EmergencyActionPointAllowance);
            Append(text, root + ".suspended", actor.SuspendedTurnBudget.HasValue);
            if (actor.SuspendedTurnBudget.HasValue)
            {
                Append(text, root + ".suspended.ap",
                    actor.SuspendedTurnBudget.Value.ActionPoints);
                Append(text, root + ".suspended.move",
                    actor.SuspendedTurnBudget.Value.MovementOpportunity);
            }
            Append(text, root + ".equipped", actor.EquippedItemId ?? string.Empty);
            Append(text, root + ".equipment.movementMultiplier",
                actor.EquipmentEffects.MovementSpeedMultiplier);
            Append(text, root + ".wounds", actor.Wounds.WoundCount);
            Append(text, root + ".wounds.head", actor.Wounds.HeadWounds);
            Append(text, root + ".wounds.torso", actor.Wounds.TorsoWounds);
            Append(text, root + ".wounds.leftArm", actor.Wounds.LeftArmWounds);
            Append(text, root + ".wounds.rightArm", actor.Wounds.RightArmWounds);
            Append(text, root + ".wounds.leftLeg", actor.Wounds.LeftLegWounds);
            Append(text, root + ".wounds.rightLeg", actor.Wounds.RightLegWounds);
            Append(text, root + ".wounds.unlocalized",
                actor.Wounds.UnlocalizedWounds);
            Append(text, root + ".penalty", actor.Wounds.MovementPenalty);
            Append(text, root + ".maximumWounds", actor.MaximumWounds);
            Append(text, root + ".pin.active", actor.IsPinned);
            if (actor.PinState != null)
            {
                Append(text, root + ".pin.prop", actor.PinState.PropId);
                Append(
                    text,
                    root + ".pin.displacement",
                    actor.PinState.DisplacementSequence);
                Append(
                    text,
                    root + ".pin.contact.point",
                    actor.PinState.Contact.Point);
                Append(
                    text,
                    root + ".pin.contact.normal",
                    actor.PinState.Contact.Normal);
                Append(
                    text,
                    root + ".pin.contact.depth",
                    actor.PinState.Contact.OverlapDepth);
            }
            var quantities = new List<InventoryQuantitySnapshot>(
                actor.Inventory.Quantities);
            quantities.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.ItemId, right.ItemId));
            foreach (InventoryQuantitySnapshot quantity in quantities)
                Append(text, root + ".inventory." + quantity.ItemId, quantity.Quantity);
        }

        private static void Append(StringBuilder text, string key, object value)
        {
            text.Append(key).Append('=');
            switch (value)
            {
                case null: break;
                case float number:
                    text.Append(Normalize(number));
                    break;
                case double number:
                    text.Append(number.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case GameplayPosition position:
                    text.Append(Normalize(position.X)).Append(',')
                        .Append(Normalize(position.Y)).Append(',')
                        .Append(Normalize(position.Z));
                    break;
                default:
                    text.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
            }
            text.Append('\n');
        }

        private static string Normalize(float value) =>
            GameplayNumericPolicy.FormatCanonical(value);

        private static int CompareBlastEffects(
            BlastEffectRecord left,
            BlastEffectRecord right)
        {
            int comparison = StringComparer.Ordinal.Compare(
                left.EntityId,
                right.EntityId);
            if (comparison != 0) return comparison;
            comparison = left.SubjectKind.CompareTo(right.SubjectKind);
            if (comparison != 0) return comparison;
            comparison = Nullable.Compare(left.InjuryRegion, right.InjuryRegion);
            if (comparison != 0) return comparison;
            comparison = left.Distance.CompareTo(right.Distance);
            if (comparison != 0) return comparison;
            comparison = left.OcclusionExposure.CompareTo(
                right.OcclusionExposure);
            if (comparison != 0) return comparison;
            return left.DistanceFalloff.CompareTo(right.DistanceFalloff);
        }
    }

    public sealed class GameplayStateDifference
    {
        public GameplayStateDifference(string path, string expected, string actual)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Expected = expected ?? string.Empty;
            Actual = actual ?? string.Empty;
        }
        public string Path { get; }
        public string Expected { get; }
        public string Actual { get; }
    }

    public static class GameplayCombatStateDiffer
    {
        public static IReadOnlyList<GameplayStateDifference> Compare(
            GameplayCombatStateSnapshot expected,
            GameplayCombatStateSnapshot actual)
        {
            if (expected == null) throw new ArgumentNullException(nameof(expected));
            if (actual == null) throw new ArgumentNullException(nameof(actual));
            var expectedFields = Parse(GameplayCombatStateHasher.BuildCanonical(expected));
            var actualFields = Parse(GameplayCombatStateHasher.BuildCanonical(actual));
            var paths = new SortedSet<string>(expectedFields.Keys, StringComparer.Ordinal);
            paths.UnionWith(actualFields.Keys);
            var differences = new List<GameplayStateDifference>();
            foreach (string path in paths)
            {
                expectedFields.TryGetValue(path, out string expectedValue);
                actualFields.TryGetValue(path, out string actualValue);
                if (!string.Equals(expectedValue, actualValue, StringComparison.Ordinal))
                    differences.Add(new GameplayStateDifference(
                        path, expectedValue, actualValue));
            }
            return differences.AsReadOnly();
        }

        private static Dictionary<string, string> Parse(string canonical)
        {
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in canonical.Split(new[] { '\n' },
                StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = line.IndexOf('=');
                fields.Add(line.Substring(0, separator), line.Substring(separator + 1));
            }
            return fields;
        }
    }

    public sealed class GameplayInvariantViolation
    {
        public GameplayInvariantViolation(string code, string path, string message)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }
        public string Code { get; }
        public string Path { get; }
        public string Message { get; }
    }

    public static class GameplayCombatInvariantValidator
    {
        public static IReadOnlyList<GameplayInvariantViolation> Validate(
            GameplayCombatStateSnapshot state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var violations = new List<GameplayInvariantViolation>();
            var actorIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameplayActorSnapshot actor in state.Session.Actors)
            {
                actorIds.Add(actor.ActorId);
                string path = "actor." + actor.ActorId;
                if (actor.TurnBudget.ActionPoints < 0
                    || actor.TurnBudget.MovementOpportunity < 0f)
                    violations.Add(new GameplayInvariantViolation(
                        "budget.negative", path, "Turn resources cannot be negative."));
                foreach (InventoryQuantitySnapshot quantity in actor.Inventory.Quantities)
                    if (quantity.Quantity < 0)
                        violations.Add(new GameplayInvariantViolation(
                            "inventory.negative", path + ".inventory." + quantity.ItemId,
                            "Inventory quantity cannot be negative."));
            }
            if (state.Session.AllInitiativeOrder.Count != actorIds.Count)
                violations.Add(new GameplayInvariantViolation(
                    "initiative.cardinality", "session.initiative",
                    "All-actor initiative must contain every actor exactly once."));
            foreach (string actorId in state.Session.AllInitiativeOrder)
                if (!actorIds.Contains(actorId))
                    violations.Add(new GameplayInvariantViolation(
                        "initiative.unknown-actor", "session.initiative." + actorId,
                        "Initiative cannot reference an unknown actor."));
            foreach (string actorId in state.Session.InitiativeOrder)
                if (!actorIds.Contains(actorId))
                    violations.Add(new GameplayInvariantViolation(
                        "initiative.unknown-scoped-actor",
                        "session.initiative." + actorId,
                        "Scoped initiative cannot reference an unknown actor."));
            foreach (EnemyAwarenessSnapshot awareness in
                state.Session.EncounterState.Awareness)
                if (!actorIds.Contains(awareness.ActorId))
                    violations.Add(new GameplayInvariantViolation(
                        "awareness.unknown-actor",
                        "session.awareness." + awareness.ActorId,
                        "Awareness cannot reference an unknown actor."));
            bool scopedEncounter = state.Session.EncounterActive;
            IReadOnlyList<string> participants = state.Session.EncounterState
                .ParticipantIds;
            if (scopedEncounter != (participants.Count > 0))
                violations.Add(new GameplayInvariantViolation(
                    "encounter.scope",
                    "session.encounter.participants",
                    "Active encounters require a non-empty scope and inactive encounters require none."));
            if (scopedEncounter
                && !SameIds(participants, state.Session.InitiativeOrder))
                violations.Add(new GameplayInvariantViolation(
                    "encounter.initiative-scope",
                    "session.encounter.participants",
                    "Encounter participants and scoped initiative must agree."));
            if (!scopedEncounter
                && !SameIds(
                    state.Session.InitiativeOrder,
                    state.Session.AllInitiativeOrder))
                violations.Add(new GameplayInvariantViolation(
                    "initiative.non-encounter-scope",
                    "session.initiative",
                    "Outside an encounter, initiative must include every actor."));
            if (state.Session.Mode == GameplaySessionMode.TurnBased
                && (string.IsNullOrWhiteSpace(state.Session.ActiveActorId)
                    || !Contains(
                        state.Session.InitiativeOrder,
                        state.Session.ActiveActorId)))
                violations.Add(new GameplayInvariantViolation(
                    "turn.active-actor", "session.activeActor",
                    "Turn mode requires a known active actor."));
            bool emergencyActive = state.Session.TurnPhase
                == GameplayTurnPhase.EmergencyReaction;
            if (emergencyActive
                != (state.Session.EmergencyResponders.Count > 0
                    && state.Session.EmergencyResponderIndex >= 0
                    && !string.IsNullOrWhiteSpace(
                        state.Session.EmergencyResumeActorId)))
                violations.Add(new GameplayInvariantViolation(
                    "emergency.state", "session.emergency",
                    "Emergency phase and responder state must agree."));
            foreach (string responderId in state.Session.EmergencyResponders)
                if (!actorIds.Contains(responderId))
                    violations.Add(new GameplayInvariantViolation(
                        "emergency.unknown-actor",
                        "session.emergency." + responderId,
                        "Emergency responders must be known actors."));
            return violations.AsReadOnly();
        }

        private static bool SameIds(
            IReadOnlyList<string> left,
            IReadOnlyList<string> right)
        {
            if (left.Count != right.Count) return false;
            for (int index = 0; index < left.Count; index++)
                if (!string.Equals(left[index], right[index],
                    StringComparison.Ordinal)) return false;
            return true;
        }

        private static bool Contains(
            IReadOnlyList<string> values,
            string value)
        {
            foreach (string candidate in values)
                if (string.Equals(candidate, value,
                    StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
