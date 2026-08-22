using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayMoveTransitionPayload : GameplayTransitionPayload
    {
        public GameplayMoveTransitionPayload(
            GameplayCapabilityProfile profile,
            MovementRouteRecord route)
            : base(
                profile,
                (route ?? throw new ArgumentNullException(nameof(route))).ActorId,
                route.ActorId)
        {
            if (profile.Capability != GameplaySemanticCapability.Move)
                throw new ArgumentException(
                    "Movement payloads require the Move capability.",
                    nameof(profile));
            Route = route;
        }

        public MovementRouteRecord Route { get; }
    }

    public sealed class GameplayStanceTransitionPayload :
        GameplayTransitionPayload
    {
        public GameplayStanceTransitionPayload(StanceChangeRecord stance)
            : base(
                GameplayCapabilityProfiles.ChangeStance(),
                (stance ?? throw new ArgumentNullException(nameof(stance))).ActorId,
                stance.ActorId)
        {
            Stance = stance;
        }

        public StanceChangeRecord Stance { get; }
    }

    public sealed class GameplayWeaponTransitionPayload :
        GameplayTransitionPayload
    {
        public GameplayWeaponTransitionPayload(
            GameplayCapabilityProfile profile,
            GameplayActionRecord action)
            : base(
                profile,
                (action ?? throw new ArgumentNullException(nameof(action)))
                    .Request.ActorId,
                action.Request.TargetId)
        {
            if (profile.Capability != GameplaySemanticCapability.DirectAttack
                && profile.Capability
                    != GameplaySemanticCapability.LaunchProjectile)
                throw new ArgumentException(
                    "Weapon payloads require an attack capability.",
                    nameof(profile));
            Action = action;
        }

        public GameplayActionRecord Action { get; }
    }

    public sealed class GameplayEndTurnTransitionPayload :
        GameplayTransitionPayload
    {
        public GameplayEndTurnTransitionPayload(
            string actorId,
            bool emergency,
            float minimumVoluntaryTurnSeconds = 0f)
            : base(
                GameplayCapabilityProfiles.EndTurn(emergency),
                actorId,
                actorId)
        {
            GameplayNumericPolicy.RequireFinite(
                minimumVoluntaryTurnSeconds,
                nameof(minimumVoluntaryTurnSeconds));
            if (minimumVoluntaryTurnSeconds < 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(minimumVoluntaryTurnSeconds));
            Emergency = emergency;
            MinimumVoluntaryTurnSeconds = minimumVoluntaryTurnSeconds;
        }

        public bool Emergency { get; }
        public float MinimumVoluntaryTurnSeconds { get; }
    }

    public sealed class GameplayCoreTransitionReducer :
        IGameplaySemanticTransitionReducer
    {
        public bool Supports(GameplayCapabilityProfile profile)
        {
            if (profile == null) return false;
            switch (profile.Capability)
            {
                case GameplaySemanticCapability.Move:
                    return profile.Equals(GameplayCapabilityProfiles.GroundedMove())
                        || profile.Equals(
                            GameplayCapabilityProfiles.TraversalMove());
                case GameplaySemanticCapability.ChangeStance:
                    return profile.Equals(
                        GameplayCapabilityProfiles.ChangeStance());
                case GameplaySemanticCapability.DirectAttack:
                    return IsSupportedDirectAttack(profile);
                case GameplaySemanticCapability.LaunchProjectile:
                    return IsSupportedProjectileAttack(profile);
                default:
                    return false;
            }
        }

        public GameplayReductionResult Reduce(
            GameplayCombatStateSnapshot state,
            GameplaySemanticTransition transition)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (transition == null)
                throw new ArgumentNullException(nameof(transition));
            if (!Supports(transition.Profile))
                throw new NotSupportedException(
                    $"Core reducer does not support '{transition.Profile.Signature}'.");

            switch (transition.Payload)
            {
                case GameplayMoveTransitionPayload move:
                    return ReduceMove(state, transition, move);
                case GameplayStanceTransitionPayload stance:
                    return ReduceStance(state, transition, stance);
                case GameplayWeaponTransitionPayload weapon:
                    return ReduceWeapon(state, transition, weapon);
                case GameplayEndTurnTransitionPayload endTurn:
                    return ReduceEndTurn(state, transition, endTurn);
                default:
                    throw new ArgumentException(
                        "The transition payload does not match the core capability.",
                        nameof(transition));
            }
        }

        private static GameplayReductionResult ReduceMove(
            GameplayCombatStateSnapshot state,
            GameplaySemanticTransition transition,
            GameplayMoveTransitionPayload payload)
        {
            GameplaySessionStateSnapshot session = state.Session;
            RequireIdleActiveActor(session, payload.ActorId);
            GameplayActorSnapshot actor = session.GetActor(payload.ActorId);
            MovementRouteRecord route = payload.Route;
            if (actor.IsPinned)
                throw new InvalidOperationException("Pinned actors cannot move.");
            if (!PosesMatch(actor.Pose, route.OriginPose))
                throw new InvalidOperationException(
                    "Movement no longer starts at the canonical actor pose.");
            if (route.HasFrozenBudget
                && !BudgetsMatch(actor.TurnBudget, route.PreviousBudget))
                throw new InvalidOperationException(
                    "Movement was prepared against a stale turn budget.");
            TurnBudget budget = actor.TurnBudget.SpendAction(new ActionCost(
                route.TotalActionPointCost,
                route.TotalCost,
                ActionMobility.Mobile));
            var pose = new GameplayActorPose(
                route.Destination,
                route.FinalFacingDegrees,
                actor.Pose.Stance);
            IReadOnlyList<GameplayActorSnapshot> actors = ReplaceActor(
                session.Actors,
                CopyActor(actor, pose, budget));
            GameplaySessionStateSnapshot resultingSession = CopySession(
                session,
                actors,
                session.ActiveActorId,
                session.LastActionSequence,
                session.LastTurnSequence,
                checked(session.JournalSequence + 2L),
                checked(session.Revision + 2L),
                transition.Identity.Sequence);
            GameplayCombatStateSnapshot resulting = CopyCombatState(
                state,
                resultingSession);
            return Result(state, resulting, transition, route);
        }

        private static GameplayReductionResult ReduceStance(
            GameplayCombatStateSnapshot state,
            GameplaySemanticTransition transition,
            GameplayStanceTransitionPayload payload)
        {
            GameplaySessionStateSnapshot session = state.Session;
            RequireIdleStanceActor(session, payload.ActorId);
            GameplayActorSnapshot actor = session.GetActor(payload.ActorId);
            if (actor.IsPinned)
                throw new InvalidOperationException(
                    "Pinned actors cannot change stance.");
            if (!PosesMatch(actor.Pose, payload.Stance.PreviousPose))
                throw new InvalidOperationException(
                    "Stance change no longer starts at the canonical pose.");
            if (payload.Stance.ResultingPose.Stance == ActorStance.Standing
                && !actor.Capabilities.CanStand)
                throw new InvalidOperationException(
                    "Actor injuries prevent transitioning to a standing stance.");
            IReadOnlyList<GameplayActorSnapshot> actors = ReplaceActor(
                session.Actors,
                CopyActor(
                    actor,
                    payload.Stance.ResultingPose,
                    actor.TurnBudget));
            GameplaySessionStateSnapshot resultingSession = CopySession(
                session,
                actors,
                session.ActiveActorId,
                session.LastActionSequence,
                session.LastTurnSequence,
                checked(session.JournalSequence + 1L),
                checked(session.Revision + 1L),
                transition.Identity.Sequence);
            GameplayCombatStateSnapshot resulting = CopyCombatState(
                state,
                resultingSession);
            return Result(state, resulting, transition, payload.Stance);
        }

        private static GameplayReductionResult ReduceWeapon(
            GameplayCombatStateSnapshot state,
            GameplaySemanticTransition transition,
            GameplayWeaponTransitionPayload payload)
        {
            GameplayCombatStateSnapshot projected =
                GameplayWeaponActionStateProjector.Project(
                    state,
                    payload.Action);
            GameplaySessionStateSnapshot projectedSession = projected.Session;
            GameplaySessionStateSnapshot resultingSession = CopySession(
                projectedSession,
                projectedSession.Actors,
                projectedSession.ActiveActorId,
                projectedSession.LastActionSequence,
                projectedSession.LastTurnSequence,
                projectedSession.JournalSequence,
                projectedSession.Revision,
                transition.Identity.Sequence);
            GameplayCombatStateSnapshot resulting = CopyCombatState(
                projected,
                resultingSession);
            return Result(state, resulting, transition, payload.Action);
        }

        private static GameplayReductionResult ReduceEndTurn(
            GameplayCombatStateSnapshot state,
            GameplaySemanticTransition transition,
            GameplayEndTurnTransitionPayload payload)
        {
            GameplaySessionStateSnapshot session = state.Session;
            RequireIdleActiveActor(session, payload.ActorId);
            if (payload.Emergency
                || session.TurnPhase != GameplayTurnPhase.Normal)
                throw new NotSupportedException(
                    "The core walking slice supports normal turn completion only.");
            if (!session.EncounterActive
                || session.EncounterCompletionRequested)
                throw new NotSupportedException(
                    "Voluntary and encounter-completion turns require their extended reducers.");

            int activeIndex = IndexOf(
                session.InitiativeOrder,
                payload.ActorId);
            string nextActorId = FindNextCapableActor(session, activeIndex)
                ?? payload.ActorId;
            GameplayActorSnapshot nextActor = session.GetActor(nextActorId);
            ActorInjuryState advanced = ActorInjuryRules.AdvanceSystemic(
                nextActor.Injuries);
            PersonalTurnActionPointGrant grant =
                PersonalTurnActionPointRules.Grant(
                    nextActor.TurnBudget.ActionPoints,
                    nextActor.ActionPointEconomy);
            TurnBudget refreshed = new TurnBudget(
                grant.ResultingActionPoints,
                GameplayInjuryCapabilityProjection.CalculateMovementAllowance(
                    nextActor.TurnMovementAllowance,
                    advanced.Capabilities));
            IReadOnlyList<GameplayActorSnapshot> actors = ReplaceActor(
                session.Actors,
                CopyActor(
                    nextActor,
                    nextActor.Pose,
                    refreshed,
                    attacksCommittedThisTurn: 0,
                    injuries: advanced));
            long turnSequence = checked(session.LastTurnSequence + 1L);
            var record = new TurnEndRecord(
                turnSequence,
                payload.ActorId,
                nextActorId,
                personalTurnStart: new PersonalTurnStartRecord(
                    nextActorId,
                    grant,
                    refreshed.MovementOpportunity,
                    ActorPhysiologyAdvanceRecord.From(
                        nextActor.Injuries,
                        advanced)));
            GameplaySessionStateSnapshot resultingSession = CopySession(
                session,
                actors,
                nextActorId,
                session.LastActionSequence,
                turnSequence,
                checked(session.JournalSequence + 1L),
                checked(session.Revision + 1L),
                transition.Identity.Sequence);
            GameplayCombatStateSnapshot resulting = CopyCombatState(
                state,
                resultingSession);
            return Result(state, resulting, transition, record);
        }

        private static bool IsSupportedDirectAttack(
            GameplayCapabilityProfile profile)
        {
            try
            {
                string delivery = profile.GetTrait("delivery");
                string targeting = profile.GetTrait("targeting");
                string resource = profile.GetTrait("resource");
                string consequence = profile.GetTrait("consequence");
                GameplaySemanticSubjectKind subject =
                    GameplayCapabilityProfiles.GetSubjectKind(profile);
                return (delivery == "immediate-ranged" || delivery == "contact")
                    && targeting == "semantic-subject"
                    && resource == "equipped-weapon"
                    && DirectConsequenceMatches(subject, consequence)
                    && (delivery != "contact"
                        || subject == GameplaySemanticSubjectKind.Actor);
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        private static bool IsSupportedProjectileAttack(
            GameplayCapabilityProfile profile)
        {
            try
            {
                string consequence = profile.GetTrait("consequence");
                GameplaySemanticSubjectKind subject =
                    GameplayCapabilityProfiles.GetSubjectKind(profile);
                return profile.GetTrait("delivery") == "turn-flight"
                    && profile.GetTrait("targeting")
                        == "semantic-subject"
                    && profile.GetTrait("resource") == "equipped-weapon"
                    && (subject == GameplaySemanticSubjectKind.Actor
                        || subject
                            == GameplaySemanticSubjectKind.DestructibleProp
                        || subject == GameplaySemanticSubjectKind.Vehicle
                        || subject == GameplaySemanticSubjectKind.WorldPosition)
                    && (consequence == "impact"
                        || consequence == "blast-actor-and-destructible")
                    && (profile.GetTrait("emergency") == "opens"
                        || profile.GetTrait("emergency") == "none");
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        private static bool DirectConsequenceMatches(
            GameplaySemanticSubjectKind subject,
            string consequence)
        {
            switch (subject)
            {
                case GameplaySemanticSubjectKind.Actor:
                    return consequence == "actor-wound";
                case GameplaySemanticSubjectKind.DestructibleProp:
                    return consequence == "destructible-damage";
                case GameplaySemanticSubjectKind.Vehicle:
                case GameplaySemanticSubjectKind.WorldPosition:
                    return consequence == "discharge-only";
                default:
                    return false;
            }
        }

        private static GameplayReductionResult Result(
            GameplayCombatStateSnapshot previous,
            GameplayCombatStateSnapshot resulting,
            GameplaySemanticTransition transition,
            object record) => new GameplayReductionResult(
                previous,
                resulting,
                new GameplayDomainEvent[]
                {
                    new GameplayTransitionReducedEvent(
                        transition.Identity,
                        transition.Payload.SubjectId,
                        record),
                });

        private static GameplayCombatStateSnapshot CopyCombatState(
            GameplayCombatStateSnapshot source,
            GameplaySessionStateSnapshot session) =>
            new GameplayCombatStateSnapshot(
                session,
                source.Destructibles,
                source.Vehicles,
                source.Projectiles,
                source.SmokeFields,
                source.Coverage,
                source.FireFields,
                source.Drones);

        private static GameplaySessionStateSnapshot CopySession(
            GameplaySessionStateSnapshot source,
            IEnumerable<GameplayActorSnapshot> actors,
            string activeActorId,
            long lastActionSequence,
            long lastTurnSequence,
            long journalSequence,
            long revision,
            long transitionSequence) => new GameplaySessionStateSnapshot(
                source.ScenarioId,
                source.Mode,
                source.Operation,
                source.TurnContext,
                source.EncounterActive,
                source.EncounterCompletionRequested,
                activeActorId,
                source.TurnPhase,
                actors,
                source.InitiativeOrder,
                source.Objectives,
                source.EmergencyResponders,
                source.EmergencyResponderIndex,
                source.EmergencyResumeActorId,
                lastActionSequence,
                lastTurnSequence,
                journalSequence,
                source.RunIdentity,
                revision,
                source.VoluntaryTurnReentrySecondsRemaining,
                source.PendingMovementRoute,
                source.PendingVoluntaryTurnCycle,
                transitionSequence,
                source.LastVoluntaryTurnCycleSequence,
                source.EncounterState,
                source.AllInitiativeOrder);

        private static GameplayActorSnapshot CopyActor(
            GameplayActorSnapshot actor,
            GameplayActorPose pose,
            TurnBudget budget,
            int? attacksCommittedThisTurn = null,
            ActorInjuryState injuries = null) => new GameplayActorSnapshot(
                actor.ActorId,
                pose,
                budget,
                actor.Wounds,
                actor.EquippedItemId,
                actor.EquipmentEffects,
                actor.MaximumWounds,
                actor.Inventory,
                actor.ActionPointEconomy,
                actor.TurnMovementAllowance,
                actor.PinState,
                actor.EmergencyActionPointAllowance,
                actor.SuspendedTurnBudget,
                attacksCommittedThisTurn
                    ?? actor.AttacksCommittedThisTurn,
                actor.Ammunition,
                injuries ?? actor.Injuries);

        private static IReadOnlyList<GameplayActorSnapshot> ReplaceActor(
            IReadOnlyList<GameplayActorSnapshot> actors,
            GameplayActorSnapshot replacement)
        {
            var result = new List<GameplayActorSnapshot>(actors.Count);
            bool replaced = false;
            foreach (GameplayActorSnapshot actor in actors)
            {
                if (string.Equals(
                    actor.ActorId,
                    replacement.ActorId,
                    StringComparison.Ordinal))
                {
                    result.Add(replacement);
                    replaced = true;
                }
                else
                {
                    result.Add(actor);
                }
            }
            if (!replaced)
                throw new KeyNotFoundException(
                    $"Actor '{replacement.ActorId}' is not canonical state.");
            return result.AsReadOnly();
        }

        private static void RequireIdleActiveActor(
            GameplaySessionStateSnapshot session,
            string actorId)
        {
            if (session.Mode != GameplaySessionMode.TurnBased)
                throw new InvalidOperationException(
                    "The core reducer requires turn mode.");
            if (session.Operation != GameplaySessionOperation.None)
                throw new InvalidOperationException(
                    "The core reducer requires an idle session.");
            if (!string.Equals(
                session.ActiveActorId,
                actorId,
                StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Only the active actor can reduce this transition.");
        }

        private static void RequireIdleStanceActor(
            GameplaySessionStateSnapshot session,
            string actorId)
        {
            if (session.Operation != GameplaySessionOperation.None)
                throw new InvalidOperationException(
                    "The core reducer requires an idle session.");
            if (session.Mode == GameplaySessionMode.TurnBased
                && !string.Equals(
                    session.ActiveActorId,
                    actorId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Only the active actor can reduce this transition in turn mode.");
        }

        private static int IndexOf(
            IReadOnlyList<string> ids,
            string actorId)
        {
            for (int index = 0; index < ids.Count; index++)
                if (string.Equals(ids[index], actorId, StringComparison.Ordinal))
                    return index;
            throw new InvalidOperationException(
                "The active actor is absent from initiative.");
        }

        private static string FindNextCapableActor(
            GameplaySessionStateSnapshot session,
            int activeIndex)
        {
            for (int offset = 1; offset <= session.InitiativeOrder.Count; offset++)
            {
                string candidate = session.InitiativeOrder[
                    (activeIndex + offset) % session.InitiativeOrder.Count];
                if (!session.GetActor(candidate).IsIncapacitated)
                    return candidate;
            }
            return null;
        }

        private static bool PosesMatch(
            GameplayActorPose left,
            GameplayActorPose right) =>
            left.Position.X == right.Position.X
            && left.Position.Y == right.Position.Y
            && left.Position.Z == right.Position.Z
            && left.FacingDegrees == right.FacingDegrees
            && left.Stance == right.Stance;

        private static bool BudgetsMatch(TurnBudget left, TurnBudget right) =>
            left.ActionPoints == right.ActionPoints
            && left.MovementOpportunity == right.MovementOpportunity;
    }
}
