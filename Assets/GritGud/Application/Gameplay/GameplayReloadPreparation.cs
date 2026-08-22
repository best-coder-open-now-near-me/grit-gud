using System;
using System.Collections.Generic;
using System.Globalization;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public enum GameplayReloadFailure
    {
        None,
        ActorNotActive,
        OperationInProgress,
        ActorIncapacitated,
        ActorPinned,
        ItemNotFound,
        WeaponNotEquipped,
        AmmunitionUnavailable,
        ProfileMismatch,
        MagazineFull,
        ReserveEmpty,
        InsufficientActionPoints,
        InsufficientMovementOpportunity,
        InsufficientCapability,
    }

    internal readonly struct GameplayReloadProfileSemantics
    {
        public GameplayReloadProfileSemantics(
            ActionCost turnCost,
            bool consumesRemainingMovement,
            int policyVersion)
        {
            TurnCost = turnCost;
            ConsumesRemainingMovement = consumesRemainingMovement;
            PolicyVersion = policyVersion;
        }

        public ActionCost TurnCost { get; }
        public bool ConsumesRemainingMovement { get; }
        public int PolicyVersion { get; }
    }

    public static class GameplayReloadPreparation
    {
        public static bool TryPrepare(
            ScenarioDefinition scenario,
            GameplaySessionStateSnapshot session,
            string actorId,
            string weaponItemId,
            out GameplayResolvedActionTransitionPayload payload,
            out GameplayReloadFailure failure)
        {
            if (scenario == null)
                throw new ArgumentNullException(nameof(scenario));
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (!string.Equals(
                    scenario.Id,
                    session.ScenarioId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Reload content does not match canonical scenario state.");
            payload = null;
            if (!GameplayActorActionAuthority.TryAuthorize(
                    session,
                    actorId,
                    GameplayActionTiming.Immediate,
                    startsEncounter: false,
                    blocksPinnedActor: true,
                    out GameplayActorSnapshot actor,
                    out GameplayActorActionFailure authorizationFailure))
                return Fail(ToReloadFailure(authorizationFailure), out failure);

            ScenarioActorDefinition actorDefinition = scenario.GetActor(
                actor.ActorId);
            InventoryItemDefinition weapon = actorDefinition.GetInventoryItem(
                weaponItemId);
            if (weapon == null)
                return Fail(GameplayReloadFailure.ItemNotFound, out failure);
            if (!string.Equals(
                    actor.EquippedItemId,
                    weapon.Id,
                    StringComparison.Ordinal))
                return Fail(
                    GameplayReloadFailure.WeaponNotEquipped,
                    out failure);
            WeaponAmmunitionDefinition ammunition = weapon.Ammunition;
            if (ammunition == null
                || !actor.Ammunition.TryGetMagazine(
                    weapon.Id,
                    out WeaponMagazineSnapshot magazine)
                || !actor.Ammunition.TryGetReserve(
                    ammunition.AmmoTypeId,
                    out int reserve))
                return Fail(
                    GameplayReloadFailure.AmmunitionUnavailable,
                    out failure);
            int minimumReloadCapacity = weapon.Attack?.HandlingProfile
                    ?.MinimumReloadCapacity
                ?? 30;
            if (actor.Capabilities.ReloadCapacity < minimumReloadCapacity)
                return Fail(
                    GameplayReloadFailure.InsufficientCapability,
                    out failure);
            if (magazine.LoadedRounds >= ammunition.MagazineCapacity)
                return Fail(GameplayReloadFailure.MagazineFull, out failure);
            if (reserve <= 0)
                return Fail(GameplayReloadFailure.ReserveEmpty, out failure);

            ActionCost cost = session.Mode == GameplaySessionMode.TurnBased
                ? ammunition.ReloadTurnCost
                : new ActionCost(
                    0,
                    0f,
                    ammunition.ReloadTurnCost.Mobility);
            if (actor.TurnBudget.ActionPoints < cost.ActionPoints)
                return Fail(
                    GameplayReloadFailure.InsufficientActionPoints,
                    out failure);
            if (actor.TurnBudget.MovementOpportunity
                < cost.MovementOpportunity)
                return Fail(
                    GameplayReloadFailure.InsufficientMovementOpportunity,
                    out failure);

            int transfer = Math.Min(
                ammunition.MagazineCapacity - magazine.LoadedRounds,
                reserve);
            long sequence = checked(session.LastActionSequence + 1L);
            var change = new WeaponAmmunitionDelta(
                sequence,
                actor.ActorId,
                weapon.Id,
                ammunition.AmmoTypeId,
                WeaponAmmunitionChangeKind.Reload,
                ammunition.MagazineCapacity,
                magazine.LoadedRounds,
                transfer,
                magazine.LoadedRounds + transfer,
                reserve,
                reserve - transfer);
            TurnBudget resultingBudget = actor.TurnBudget.SpendAction(cost);
            if (session.Mode == GameplaySessionMode.TurnBased
                && ammunition.ConsumesRemainingMovement)
                resultingBudget = new TurnBudget(
                    resultingBudget.ActionPoints,
                    0f);
            var action = new GameplayActionRecord(
                sequence,
                new GameplayActionRequest(
                    actor.ActorId,
                    AmmunitionActionIds.Reload,
                    weapon.Id),
                cost,
                actor.TurnBudget,
                resultingBudget,
                new GameplayActionOutcome[]
                {
                    new WeaponReloadedActionOutcome(change),
                });
            payload = new GameplayResolvedActionTransitionPayload(
                GameplayCapabilityProfiles.Reload(ammunition),
                action);
            failure = GameplayReloadFailure.None;
            return true;
        }

        internal static bool TryReadProfile(
            GameplayCapabilityProfile profile,
            out GameplayReloadProfileSemantics semantics)
        {
            semantics = default;
            if (profile == null
                || profile.Capability != GameplaySemanticCapability.Reload
                || profile.Traits.Count != 9)
                return false;
            try
            {
                if (GameplayCapabilityProfiles.GetSubjectKind(profile)
                        != GameplaySemanticSubjectKind.InventoryItem
                    || profile.GetTrait("resource") != "actor-ammunition"
                    || profile.GetTrait("equipment") != "equipped-only"
                    || profile.GetTrait("transfer") != "bounded-reserve")
                    return false;
                if (!int.TryParse(
                        profile.GetTrait("turn-action-points"),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int actionPoints)
                    || actionPoints < 0
                    || !float.TryParse(
                        profile.GetTrait("turn-movement-opportunity"),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float movement)
                    || float.IsNaN(movement)
                    || float.IsInfinity(movement)
                    || movement < 0f
                    || !Enum.TryParse(
                        profile.GetTrait("turn-mobility"),
                        ignoreCase: false,
                        out ActionMobility mobility)
                    || !Enum.IsDefined(typeof(ActionMobility), mobility)
                    || !int.TryParse(
                        profile.GetTrait("policy-version"),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int policyVersion)
                    || policyVersion <= 0)
                    return false;
                string movementAfter = profile.GetTrait("movement-after");
                if (movementAfter != "zero" && movementAfter != "preserve")
                    return false;
                semantics = new GameplayReloadProfileSemantics(
                    new ActionCost(actionPoints, movement, mobility),
                    movementAfter == "zero",
                    policyVersion);
                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static GameplayReloadFailure ToReloadFailure(
            GameplayActorActionFailure failure)
        {
            switch (failure)
            {
                case GameplayActorActionFailure.ActorUnavailable:
                case GameplayActorActionFailure.ActorNotActive:
                    return GameplayReloadFailure.ActorNotActive;
                case GameplayActorActionFailure.ActorIncapacitated:
                    return GameplayReloadFailure.ActorIncapacitated;
                case GameplayActorActionFailure.ActorPinned:
                    return GameplayReloadFailure.ActorPinned;
                case GameplayActorActionFailure.OperationInProgress:
                    return GameplayReloadFailure.OperationInProgress;
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
        }

        internal static string FailureCode(GameplayReloadFailure failure)
        {
            switch (failure)
            {
                case GameplayReloadFailure.ActorNotActive:
                    return "actor-not-active";
                case GameplayReloadFailure.OperationInProgress:
                    return "operation-in-progress";
                case GameplayReloadFailure.ActorIncapacitated:
                    return "actor-incapacitated";
                case GameplayReloadFailure.ActorPinned:
                    return "actor-pinned";
                case GameplayReloadFailure.ItemNotFound:
                    return "item-not-found";
                case GameplayReloadFailure.WeaponNotEquipped:
                    return "weapon-not-equipped";
                case GameplayReloadFailure.AmmunitionUnavailable:
                    return "ammunition-unavailable";
                case GameplayReloadFailure.ProfileMismatch:
                    return "profile-mismatch";
                case GameplayReloadFailure.MagazineFull:
                    return "magazine-full";
                case GameplayReloadFailure.ReserveEmpty:
                    return "reserve-empty";
                case GameplayReloadFailure.InsufficientActionPoints:
                    return "insufficient-action-points";
                case GameplayReloadFailure.InsufficientMovementOpportunity:
                    return "insufficient-movement-opportunity";
                case GameplayReloadFailure.InsufficientCapability:
                    return "insufficient-reload-capability";
                case GameplayReloadFailure.None:
                    return string.Empty;
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
        }

        private static bool TryGetActor(
            IEnumerable<GameplayActorSnapshot> actors,
            string actorId,
            out GameplayActorSnapshot actor)
        {
            foreach (GameplayActorSnapshot value in actors)
                if (string.Equals(
                    value.ActorId,
                    actorId,
                    StringComparison.Ordinal))
                {
                    actor = value;
                    return true;
                }
            actor = default;
            return false;
        }

        private static bool Fail(
            GameplayReloadFailure value,
            out GameplayReloadFailure failure)
        {
            failure = value;
            return false;
        }
    }

    public sealed class GameplayReloadCandidateExecutionRoute :
        IGameplayCandidateExecutionRoute
    {
        public const string Id = "reload.v1";
        private readonly ScenarioDefinition scenario;
        private readonly HashSet<string> supportedProfiles =
            new HashSet<string>(StringComparer.Ordinal);

        public GameplayReloadCandidateExecutionRoute(
            ScenarioDefinition scenarioDefinition)
        {
            scenario = scenarioDefinition ?? throw new ArgumentNullException(
                nameof(scenarioDefinition));
            foreach (ScenarioActorDefinition actor in scenario.Actors)
            foreach (InventoryItemDefinition item in actor.Inventory)
                if (item.Ammunition != null)
                    supportedProfiles.Add(
                        GameplayCapabilityProfiles.Reload(item.Ammunition)
                            .Signature);
        }

        public string RouteId => Id;

        public bool Supports(GameplayCapabilityProfile profile) =>
            profile != null
            && supportedProfiles.Contains(profile.Signature)
            && GameplayReloadPreparation.TryReadProfile(profile, out _);

        public GameplayExecutableCandidateEvaluation Evaluate(
            GameplayDecisionContext context,
            GameplayCandidate candidate)
        {
            GameplayBasicCandidateRouteUtility.Require(
                context,
                candidate,
                Supports,
                Id);
            bool legal = GameplayReloadPreparation.TryPrepare(
                scenario,
                context.State.Session,
                candidate.ActorId,
                candidate.SubjectId,
                out GameplayResolvedActionTransitionPayload payload,
                out GameplayReloadFailure failure);
            if (legal && !candidate.Profile.Equals(payload.Profile))
            {
                legal = false;
                payload = null;
                failure = GameplayReloadFailure.ProfileMismatch;
            }

            var features = new List<GameplayCandidateOutcomeFeature>();
            if (legal)
            {
                GameplayActorSnapshot actor = context.State.Session.GetActor(
                    candidate.ActorId);
                WeaponAmmunitionDelta change =
                    ((WeaponReloadedActionOutcome)payload.Action.Outcomes[0])
                        .Change;
                features.Add(new GameplayCandidateOutcomeFeature(
                    "ammunition.reload",
                    1f));
                features.Add(new GameplayCandidateOutcomeFeature(
                    "ammunition.reload-rounds",
                    change.ChangedRounds));
                features.Add(new GameplayCandidateOutcomeFeature(
                    "ammunition.reload-readiness",
                    change.PreviousLoadedRounds
                            < actor.Ammunition.GetMagazine(
                                change.WeaponItemId).RoundsPerUse
                        ? 1f
                        : 0f));
                features.Add(new GameplayCandidateOutcomeFeature(
                    "ammunition.reserve-depletion",
                    (float)change.ChangedRounds
                        / change.PreviousReserveRounds));
                features.Add(new GameplayCandidateOutcomeFeature(
                    "cost.action-points",
                    payload.Action.Cost.ActionPoints));
                features.Add(new GameplayCandidateOutcomeFeature(
                    "cost.movement-opportunity",
                    payload.Action.PreviousBudget.MovementOpportunity
                        - payload.Action.ResultingBudget.MovementOpportunity));
            }
            return GameplayBasicCandidateRouteUtility.Result(
                Id,
                context,
                candidate,
                legal,
                legal
                    ? string.Empty
                    : GameplayReloadPreparation.FailureCode(failure),
                new GameplayCandidateOutcomeEstimate(features),
                payload);
        }

        public GameplayTransitionPayload PreparePayload(
            GameplayDecisionContext context,
            GameplayExecutableCandidateEvaluation evaluation) =>
            evaluation?.FrozenPreparation
                as GameplayResolvedActionTransitionPayload
            ?? throw new ArgumentException(
                "Reload preparation is missing.",
                nameof(evaluation));
    }

    public sealed class GameplayReloadSession
    {
        private readonly GameplaySession gameplay;
        private readonly List<WeaponAmmunitionDelta> records =
            new List<WeaponAmmunitionDelta>();

        public GameplayReloadSession(GameplaySession gameplaySession)
        {
            gameplay = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
        }

        public IReadOnlyList<WeaponAmmunitionDelta> Records =>
            records.AsReadOnly();

        public bool TryResolve(
            string actorId,
            out GameplayActionRecord action,
            out GameplayReloadFailure failure)
        {
            if (!gameplay.TryGetActor(
                    actorId,
                    out GameplayActorSnapshot actor))
            {
                action = null;
                failure = GameplayReloadFailure.ActorNotActive;
                return false;
            }
            if (actor.EquippedItemId == null)
            {
                action = null;
                failure = GameplayReloadFailure.WeaponNotEquipped;
                return false;
            }
            GameplaySessionStateSnapshot session =
                GameplayCombatStateCapture.Capture(gameplay).Session;
            if (!GameplayReloadPreparation.TryPrepare(
                    gameplay.Scenario,
                    session,
                    actorId,
                    actor.EquippedItemId,
                    out GameplayResolvedActionTransitionPayload payload,
                    out failure))
            {
                action = null;
                return false;
            }
            action = payload.Action;
            Commit(action);
            return true;
        }

        public void Commit(GameplayActionRecord action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (action.Outcomes.Count != 1
                || !(action.Outcomes[0]
                    is WeaponReloadedActionOutcome reload))
                throw new ArgumentException(
                    "Reload actions require exactly one reload outcome.",
                    nameof(action));
            gameplay.CommitAction(action);
            records.Add(reload.Change);
        }
    }
}
