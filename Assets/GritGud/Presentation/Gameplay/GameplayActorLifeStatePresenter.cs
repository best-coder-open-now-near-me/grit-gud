using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors;
using GritGud.Presentation.Actors.Animation;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    /// <summary>
    /// Sole live presentation boundary for canonical actor life-state changes.
    /// It observes every installed semantic reduction, keeps status separate
    /// from posture, and preserves source-specific terminal evidence when it
    /// is available. Ordinary nonterminal flinches remain outside this owner.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class GameplayActorLifeStatePresenter : MonoBehaviour
    {
        private sealed class PendingTerminalPresentation
        {
            public PendingTerminalPresentation(
                long transitionSequence,
                string actorId,
                string sourceActorId,
                TargetRegionId? hitRegion,
                float handoffEventNormalizedTime,
                float remainingSeconds)
            {
                TransitionSequence = transitionSequence;
                ActorId = actorId;
                SourceActorId = sourceActorId;
                HitRegion = hitRegion;
                HandoffEventNormalizedTime = handoffEventNormalizedTime;
                RemainingSeconds = remainingSeconds;
            }

            public long TransitionSequence { get; }
            public string ActorId { get; }
            public string SourceActorId { get; }
            public TargetRegionId? HitRegion { get; }
            public float HandoffEventNormalizedTime { get; }
            public float RemainingSeconds { get; set; }
        }

        private static readonly Quaternion GenericCollapseRotation =
            Quaternion.Euler(0f, 0f, 78f);
        private static readonly Vector3 GenericCollapseOffset =
            new Vector3(0f, 0.15f, 0f);

        private readonly List<PendingTerminalPresentation> pending = new();
        private readonly HashSet<string> presentedStatusChanges = new(
            StringComparer.Ordinal);
        private GameplayLiveSessionRuntime runtime;
        private GameplaySession session;
        private GameplayWorldRegistry registry;
        private GameplayActionController actionController;
        private GameplayDialogueLog dialogue;
        private WeaponPresentationCatalog weapons;

        internal int PendingPresentationCount => pending.Count;

        internal int PresentedStatusChangeCount =>
            presentedStatusChanges.Count;

        internal void Bind(
            GameplayLiveSessionRuntime liveRuntime,
            GameplaySession gameplaySession,
            GameplayWorldRegistry worldRegistry,
            GameplayActionController actions,
            GameplayDialogueLog dialogueLog,
            WeaponPresentationCatalog weaponCatalog = null)
        {
            if (liveRuntime == null)
                throw new ArgumentNullException(nameof(liveRuntime));
            Bind(
                gameplaySession,
                worldRegistry,
                actions,
                dialogueLog,
                weaponCatalog);
            runtime = liveRuntime;
            runtime.StateInstalled += PresentInstalledState;
        }

        internal void Bind(
            GameplaySession gameplaySession,
            GameplayWorldRegistry worldRegistry,
            GameplayActionController actions,
            GameplayDialogueLog dialogueLog,
            WeaponPresentationCatalog weaponCatalog = null)
        {
            Unbind();
            session = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            registry = worldRegistry ?? throw new ArgumentNullException(
                nameof(worldRegistry));
            actionController = actions ?? throw new ArgumentNullException(
                nameof(actions));
            dialogue = dialogueLog ?? throw new ArgumentNullException(
                nameof(dialogueLog));
            weapons = weaponCatalog ?? WeaponPresentationCatalog.LoadDefault();
            enabled = true;
        }

        internal void Unbind()
        {
            if (runtime != null)
                runtime.StateInstalled -= PresentInstalledState;
            pending.Clear();
            presentedStatusChanges.Clear();
            runtime = null;
            session = null;
            registry = null;
            actionController = null;
            dialogue = null;
            weapons = null;
            enabled = false;
        }

        internal void PresentInstalledState(GameplayReductionResult reduction)
        {
            if (reduction == null)
                throw new ArgumentNullException(nameof(reduction));
            GameplayTransitionReducedEvent transition =
                RequireTransition(reduction);
            foreach (GameplayActorSnapshot resulting in
                reduction.Resulting.Session.Actors)
            {
                if (!TryFindActor(
                        reduction.Previous.Session.Actors,
                        resulting.ActorId,
                        out GameplayActorSnapshot previous)
                    || previous.LifeState == resulting.LifeState)
                {
                    continue;
                }

                string stableKey = transition.Transition.Sequence + ":"
                    + resulting.ActorId + ":" + previous.LifeState + ":"
                    + resulting.LifeState;
                if (!presentedStatusChanges.Add(stableKey))
                    continue;

                PresentStatus(previous, resulting);
                if (previous.LifeState == ActorLifeState.Active
                    && resulting.LifeState != ActorLifeState.Active)
                {
                    ScheduleTerminalPresentation(
                        reduction.Previous,
                        previous,
                        resulting,
                        transition);
                }
                else if (previous.LifeState != ActorLifeState.Active
                    && resulting.LifeState == ActorLifeState.Active)
                {
                    RemovePending(resulting.ActorId);
                }
            }
        }

        internal void Tick(float unscaledDeltaTime)
        {
            float elapsed = Mathf.Max(0f, unscaledDeltaTime);
            for (int index = pending.Count - 1; index >= 0; index--)
            {
                PendingTerminalPresentation presentation = pending[index];
                presentation.RemainingSeconds -= elapsed;
                if (presentation.RemainingSeconds > 0f)
                    continue;
                if (TryPresentTerminal(presentation))
                    pending.RemoveAt(index);
            }
        }

        private void ScheduleTerminalPresentation(
            GameplayCombatStateSnapshot previousState,
            GameplayActorSnapshot previous,
            GameplayActorSnapshot resulting,
            GameplayTransitionReducedEvent transition)
        {
            InjuryRecord injury = FindNewestInjury(previous, resulting);
            ResolveSourcePresentation(
                previousState,
                resulting.ActorId,
                transition,
                out string sourceActorId,
                out float delaySeconds,
                out float handoffEventNormalizedTime);
            var presentation = new PendingTerminalPresentation(
                transition.Transition.Sequence,
                resulting.ActorId,
                sourceActorId,
                injury?.Region,
                handoffEventNormalizedTime,
                delaySeconds);
            if (delaySeconds <= 0f && TryPresentTerminal(presentation))
                return;
            pending.Add(presentation);
        }

        private bool TryPresentTerminal(
            PendingTerminalPresentation presentation)
        {
            if (!registry.TryGetActor(
                    presentation.ActorId,
                    out GameplayActorView target))
                return true;
            ActorAnimationCoordinator animation = target.Root.GetComponent<
                ActorAnimationCoordinator>();
            if (animation == null)
                return true;
            if (animation.IsPresentingReplay)
                return false;

            if (!animation.PresentTerminalCollapse(presentation.HitRegion))
            {
                animation.PresentIncapacitation(
                    GenericCollapseRotation,
                    GenericCollapseOffset);
            }

            target.Root.GetComponent<ActorRagdollPresenter>()
                ?.ArmIncapacitation(
                    presentation.TransitionSequence,
                    presentation.HitRegion,
                    ResolveImpulseDirection(
                        target,
                        presentation.SourceActorId),
                    presentation.HandoffEventNormalizedTime);
            return true;
        }

        private void PresentStatus(
            GameplayActorSnapshot previous,
            GameplayActorSnapshot resulting)
        {
            bool partyMember = session.Scenario.PlayerParty?.Contains(
                resulting.ActorId) == true;
            string subject = partyMember ? "PARTY MEMBER" : "HOSTILE";
            string displayName = session.Scenario
                .GetActor(resulting.ActorId).CharacterProfile?.DisplayName
                ?? resulting.ActorId;
            string state;
            string message;
            switch (resulting.LifeState)
            {
                case ActorLifeState.Dead:
                    state = "DEAD";
                    message = displayName + " is dead.";
                    break;
                case ActorLifeState.Incapacitated:
                    state = "INCAPACITATED";
                    message = displayName
                        + " can no longer act or respond.";
                    break;
                default:
                    state = "RECOVERED";
                    message = displayName + " is active again.";
                    break;
            }
            dialogue.Append(
                GameplayDialogueChannel.System,
                subject + " " + state,
                message);
            actionController.PresentExternalStatus(message);
        }

        private void ResolveSourcePresentation(
            GameplayCombatStateSnapshot previousState,
            string targetActorId,
            GameplayTransitionReducedEvent transition,
            out string sourceActorId,
            out float delaySeconds,
            out float handoffEventNormalizedTime)
        {
            sourceActorId = transition.Transition.ActorId;
            delaySeconds = 0f;
            handoffEventNormalizedTime = 0f;
            if (!(transition.SemanticRecord is GameplayActionRecord action))
                return;
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (!(outcome is AttackResolvedActionOutcome attack)
                    || !string.Equals(
                        attack.Attack.TargetId,
                        targetActorId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                AttackResolutionRecord resolution = attack.Attack;
                sourceActorId = resolution.AttackerId;
                if (!resolution.IsContactAttack)
                    return;
                string itemId = previousState.Session
                    .GetActor(resolution.AttackerId).EquippedItemId;
                if (!weapons.TryGet(
                        itemId,
                        out WeaponPresentationDefinition weapon)
                    || weapon.AttackPresentation !=
                        WeaponAttackPresentationKind.ContactStrike)
                {
                    return;
                }
                delaySeconds = weapon.ContactStrikeSeconds
                    * weapon.ContactImpactNormalizedTime;
                handoffEventNormalizedTime =
                    weapon.ContactImpactNormalizedTime;
                return;
            }
        }

        private Vector3 ResolveImpulseDirection(
            GameplayActorView target,
            string sourceActorId)
        {
            Vector3 fallback = target.Transform.forward;
            if (string.IsNullOrWhiteSpace(sourceActorId)
                || !registry.TryGetActor(
                    sourceActorId,
                    out GameplayActorView source))
            {
                return fallback;
            }
            Vector3 displacement = target.Transform.position
                - source.Transform.position;
            return displacement.sqrMagnitude > 0.0001f
                ? displacement.normalized
                : fallback;
        }

        private void RemovePending(string actorId)
        {
            for (int index = pending.Count - 1; index >= 0; index--)
                if (string.Equals(
                    pending[index].ActorId,
                    actorId,
                    StringComparison.Ordinal))
                    pending.RemoveAt(index);
        }

        private static GameplayTransitionReducedEvent RequireTransition(
            GameplayReductionResult reduction)
        {
            GameplayTransitionReducedEvent result = null;
            foreach (GameplayDomainEvent domainEvent in reduction.DomainEvents)
            {
                if (!(domainEvent is GameplayTransitionReducedEvent reduced))
                    continue;
                if (result != null)
                    throw new InvalidOperationException(
                        "A canonical reduction has multiple transition records.");
                result = reduced;
            }
            return result ?? throw new InvalidOperationException(
                "A canonical reduction has no transition record.");
        }

        private static InjuryRecord FindNewestInjury(
            GameplayActorSnapshot previous,
            GameplayActorSnapshot resulting)
        {
            if (resulting.Injuries.Injuries.Count
                <= previous.Injuries.Injuries.Count)
                return null;
            return resulting.Injuries.Injuries[
                resulting.Injuries.Injuries.Count - 1];
        }

        private static bool TryFindActor(
            IReadOnlyList<GameplayActorSnapshot> actors,
            string actorId,
            out GameplayActorSnapshot result)
        {
            foreach (GameplayActorSnapshot actor in actors)
                if (string.Equals(
                    actor.ActorId,
                    actorId,
                    StringComparison.Ordinal))
                {
                    result = actor;
                    return true;
                }
            result = default;
            return false;
        }

        private void Update() => Tick(Time.unscaledDeltaTime);

        private void OnDestroy() => Unbind();
    }
}
