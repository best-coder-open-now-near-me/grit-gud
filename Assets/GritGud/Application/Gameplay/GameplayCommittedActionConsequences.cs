using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public interface IGameplayCommittedActionSoundQuery
    {
        EncounterSoundEvidence Capture(
            string observerActorId,
            string sourceActorId,
            GameplayPosition origin,
            float soundSignature);
    }

    public sealed class GameplayCommittedActionConsequencePlan
    {
        internal GameplayCommittedActionConsequencePlan(
            GameplayCombatStateSnapshot initialState,
            GameplaySimulationBranch branch)
        {
            InitialState = initialState;
            Branch = branch;
        }

        public GameplayCombatStateSnapshot InitialState { get; }
        public GameplaySimulationBranch Branch { get; }
        public GameplayCombatStateSnapshot ResultingState =>
            Branch.CurrentState;
        public IReadOnlyList<GameplayTrajectoryStep> Steps => Branch.Steps;
    }

    /// <summary>
    /// Executes the same post-commit ordering for headless branches: reduce the
    /// action, derive sound from that resulting state, reduce observer awareness
    /// in stable ID order, then begin the resulting scoped encounter.
    /// </summary>
    public static class GameplayCommittedActionConsequencePlanner
    {
        public static GameplayCommittedActionConsequencePlan Execute(
            GameplayCombatStateSnapshot initialState,
            GameplaySemanticTransition committedAction,
            ScenarioDefinition scenario,
            GameplayHeadlessSpatialEvidence spatial,
            GameplayTransitionReducerRegistry reducers,
            bool authoredActionStartsEncounter = false)
        {
            if (initialState == null) throw new ArgumentNullException(
                nameof(initialState));
            if (committedAction == null) throw new ArgumentNullException(
                nameof(committedAction));
            if (scenario == null) throw new ArgumentNullException(
                nameof(scenario));
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (reducers == null) throw new ArgumentNullException(nameof(reducers));
            if (!string.Equals(initialState.Session.ScenarioId, scenario.Id,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Scenario does not describe the supplied canonical state.",
                    nameof(scenario));

            var branch = new GameplaySimulationBranch(
                "committed-action-consequences",
                initialState,
                reducers);
            branch.Apply(committedAction);
            GameplayActionRecord action = RequireAction(committedAction);
            float signature = ResolveSoundSignature(action, scenario);
            var alerted = new List<string>();
            if (signature > 0f)
            {
                var observers = new List<ScenarioActorDefinition>(scenario.Actors);
                observers.Sort((left, right) => StringComparer.Ordinal.Compare(
                    left.Id, right.Id));
                foreach (ScenarioActorDefinition observer in observers)
                {
                    if (observer.Combat.EnemyBehavior == null
                        || string.Equals(observer.Id, action.Request.ActorId,
                            StringComparison.Ordinal)
                        || !IsHostile(scenario, observer.Id,
                            action.Request.ActorId))
                        continue;
                    EnemyAwarenessSnapshot previous = branch.CurrentState.Session
                        .EncounterState.GetAwareness(observer.Id);
                    EncounterSoundEvidence sound =
                        GameplayHeadlessEncounterEvidence.CaptureSound(
                            branch.CurrentState,
                            spatial,
                            observer.Id,
                            action.Request.ActorId,
                            signature,
                            observer.Combat.EnemyBehavior.AwarenessPolicy
                                .HearingRange);
                    var observation = new EncounterObservation(
                        observer.Id,
                        sight: null,
                        sightTargetPosition: null,
                        sound);
                    Apply(branch, new GameplayEncounterObservationTransitionPayload(
                        observer.Id,
                        observer.Combat.EnemyBehavior,
                        observation));
                    EnemyAwarenessSnapshot resulting = branch.CurrentState.Session
                        .EncounterState.GetAwareness(observer.Id);
                    if (previous.State != EncounterAwarenessState.Alert
                        && resulting.State == EncounterAwarenessState.Alert)
                        alerted.Add(observer.Id);
                }
            }

            if (!branch.CurrentState.Session.EncounterActive
                && (alerted.Count > 0 || authoredActionStartsEncounter))
            {
                string trigger = alerted.Count > 0
                    ? alerted[0]
                    : action.Request.TargetId;
                IReadOnlyList<string> scope = CreateScope(
                    scenario,
                    branch.CurrentState.Session.AllInitiativeOrder,
                    trigger,
                    action.Request.ActorId);
                Apply(branch, new GameplaySessionControlTransitionPayload(
                    action.Request.ActorId,
                    GameplaySemanticCapability.ChangeEncounter,
                    "begin",
                    encounterParticipantIds: scope));
            }
            return new GameplayCommittedActionConsequencePlan(
                initialState,
                branch);
        }

        private static GameplayActionRecord RequireAction(
            GameplaySemanticTransition transition)
        {
            if (transition.Payload is GameplayWeaponTransitionPayload weapon)
                return weapon.Action;
            throw new ArgumentException(
                "Committed consequence planning requires a resolved action payload.",
                nameof(transition));
        }

        private static void Apply(
            GameplaySimulationBranch branch,
            GameplayTransitionPayload payload)
        {
            var identity = new GameplayTransitionIdentity(
                checked(branch.CurrentState.Session.LastTransitionSequence + 1L),
                payload.Profile.Capability.ToString(),
                payload.ActorId,
                payload.SubjectId);
            branch.Apply(new GameplaySemanticTransition(
                identity,
                branch.CurrentState.CanonicalHash,
                payload));
        }

        private static bool IsHostile(
            ScenarioDefinition scenario,
            string observerId,
            string sourceId) => scenario.GetActor(observerId).Combat.IsHostileTo(
                scenario.GetActor(sourceId).Combat.AllegianceId);

        private static IReadOnlyList<string> CreateScope(
            ScenarioDefinition scenario,
            IReadOnlyList<string> initiative,
            string observerId,
            string sourceId) => GameplayEncounterScopeResolver.Resolve(
                scenario,
                initiative,
                observerId,
                sourceId);

        private static float ResolveSoundSignature(
            GameplayActionRecord action,
            ScenarioDefinition scenario)
        {
            if (action.Context is ResolvedTacticalContext context)
                return context.Snapshot.SoundSignature;
            ScenarioActorDefinition actor = scenario.GetActor(
                action.Request.ActorId);
            if (actor.Attack != null && string.Equals(actor.Attack.ActionId,
                    action.Request.ActionId, StringComparison.Ordinal))
                return actor.Attack.SoundSignature;
            foreach (InventoryItemDefinition item in actor.Inventory)
                if (item.Attack != null && string.Equals(item.Attack.ActionId,
                        action.Request.ActionId, StringComparison.Ordinal))
                    return item.Attack.SoundSignature;
            return 0f;
        }
    }

    /// <summary>
    /// Owns deterministic post-action sensing. The committed action and its
    /// frozen context are installed before any observer awareness transition.
    /// Presentation supplies spatial sound evidence and only presents the
    /// resulting encounter transition.
    /// </summary>
    public sealed class GameplayCommittedActionConsequenceCoordinator :
        IDisposable
    {
        private readonly GameplaySession session;
        private readonly IGameplayCommittedActionSoundQuery soundQuery;
        private readonly Func<
            GameplayActionRecord,
            IReadOnlyList<string>,
            bool> beginEncounter;
        private bool disposed;

        /// <summary>
        /// A presentation-owned encounter start can fail after the action has
        /// already committed. Preserve that diagnostic without converting a
        /// valid action into an exception that disables the whole control path.
        /// </summary>
        public string LastEncounterStartFailure { get; private set; } =
            string.Empty;

        public GameplayCommittedActionConsequenceCoordinator(
            GameplaySession gameplaySession,
            IGameplayCommittedActionSoundQuery committedSoundQuery,
            Func<IReadOnlyList<string>, bool> encounterStart)
            : this(
                gameplaySession,
                committedSoundQuery,
                AdaptEncounterStart(encounterStart))
        {
        }

        public GameplayCommittedActionConsequenceCoordinator(
            GameplaySession gameplaySession,
            IGameplayCommittedActionSoundQuery committedSoundQuery,
            Func<
                GameplayActionRecord,
                IReadOnlyList<string>,
                bool> encounterStart)
        {
            session = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            soundQuery = committedSoundQuery ?? throw new ArgumentNullException(
                nameof(committedSoundQuery));
            beginEncounter = encounterStart ?? throw new ArgumentNullException(
                nameof(encounterStart));
            session.ActionResolved += HandleCommittedAction;
        }

        public void Dispose()
        {
            if (disposed) return;
            session.ActionResolved -= HandleCommittedAction;
            disposed = true;
        }

        private void HandleCommittedAction(GameplayActionRecord action)
        {
            LastEncounterStartFailure = string.Empty;
            float signature = ResolveSoundSignature(action);
            GameplayActorSnapshot source = session.GetActor(
                action.Request.ActorId);
            var alertedObservers = new List<string>();
            if (signature > 0f)
            {
                var definitions = new List<ScenarioActorDefinition>(
                    session.Scenario.Actors);
                definitions.Sort((left, right) =>
                    StringComparer.Ordinal.Compare(left.Id, right.Id));
                foreach (ScenarioActorDefinition observer in definitions)
                {
                    if (observer.Combat.EnemyBehavior == null
                        || string.Equals(
                            observer.Id,
                            source.ActorId,
                            StringComparison.Ordinal)
                        || !session.IsHostile(observer.Id, source.ActorId))
                        continue;
                    EncounterSoundEvidence sound = soundQuery.Capture(
                        observer.Id,
                        source.ActorId,
                        source.Pose.Position,
                        signature);
                    var observation = new EncounterObservation(
                        observer.Id,
                        sight: null,
                        sightTargetPosition: null,
                        sound);
                    EnemyAwarenessTransitionRecord transition =
                        session.PrepareAwarenessTransition(
                            observer.Id,
                            observation);
                    session.CommitAwarenessTransition(transition);
                    if (transition.Previous.State
                            != EncounterAwarenessState.Alert
                        && transition.Resulting.State
                            == EncounterAwarenessState.Alert)
                        alertedObservers.Add(observer.Id);
                }
            }

            if (session.EncounterActive) return;
            if (alertedObservers.Count > 0)
            {
                IReadOnlyList<string> scope =
                    session.CreateDetectionEncounterScope(
                        alertedObservers[0],
                        source.ActorId);
                TryBeginEncounter(
                    action,
                    scope,
                    "Committed sound produced Alert awareness but could not begin its encounter.");
                return;
            }
            if (session.ActionStartsEncounter(action))
            {
                IReadOnlyList<string> scope = session.CreateEncounterScope(
                    source.ActorId,
                    action.Request.TargetId);
                TryBeginEncounter(
                    action,
                    scope,
                    "Authored committed action could not begin its encounter.");
            }
        }

        private void TryBeginEncounter(
            GameplayActionRecord action,
            IReadOnlyList<string> scope,
            string failureMessage)
        {
            try
            {
                if (beginEncounter(action, scope))
                    return;

                LastEncounterStartFailure = failureMessage;
            }
            catch (Exception exception)
            {
                LastEncounterStartFailure = failureMessage + " "
                    + exception.Message;
            }
        }

        private float ResolveSoundSignature(GameplayActionRecord action)
        {
            if (action.Context is ResolvedTacticalContext context)
                return context.Snapshot.SoundSignature;
            ScenarioActorDefinition actor = session.Scenario.GetActor(
                action.Request.ActorId);
            if (actor.Attack != null
                && string.Equals(
                    actor.Attack.ActionId,
                    action.Request.ActionId,
                    StringComparison.Ordinal))
                return actor.Attack.SoundSignature;
            foreach (InventoryItemDefinition item in actor.Inventory)
                if (item.Attack != null
                    && string.Equals(
                        item.Attack.ActionId,
                        action.Request.ActionId,
                        StringComparison.Ordinal))
                    return item.Attack.SoundSignature;
            return 0f;
        }

        private static Func<
            GameplayActionRecord,
            IReadOnlyList<string>,
            bool> AdaptEncounterStart(
                Func<IReadOnlyList<string>, bool> encounterStart)
        {
            if (encounterStart == null)
                throw new ArgumentNullException(nameof(encounterStart));
            return (action, scope) => encounterStart(scope);
        }
    }
}
