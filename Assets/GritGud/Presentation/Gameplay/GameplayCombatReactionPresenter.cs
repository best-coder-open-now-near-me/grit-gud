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
    /// Projects committed wounds onto actor animation. Contact attacks defer
    /// only this visual reaction until the authored strike point; gameplay
    /// state and capability changes remain immediate and authoritative.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class GameplayCombatReactionPresenter : MonoBehaviour
    {
        private sealed class PendingReaction
        {
            public PendingReaction(
                GameplayActionRecord action,
                AttackResolutionRecord resolution,
                float remainingSeconds)
            {
                Action = action;
                Resolution = resolution;
                RemainingSeconds = remainingSeconds;
            }

            public GameplayActionRecord Action { get; }

            public AttackResolutionRecord Resolution { get; }

            public float RemainingSeconds { get; set; }
        }

        private readonly List<PendingReaction> pending = new();
        private GameplaySession session;
        private GameplayWorldRegistry registry;
        private GameplayAttackController attacks;
        private WeaponPresentationCatalog weapons;

        internal int PendingReactionCount => pending.Count;

        internal void Bind(
            GameplaySession gameplaySession,
            GameplayWorldRegistry worldRegistry,
            GameplayAttackController attackController,
            WeaponPresentationCatalog weaponCatalog = null)
        {
            Unbind();
            session = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            registry = worldRegistry ?? throw new ArgumentNullException(
                nameof(worldRegistry));
            attacks = attackController ?? throw new ArgumentNullException(
                nameof(attackController));
            weapons = weaponCatalog ?? WeaponPresentationCatalog.LoadDefault();
            attacks.AttackResolved += HandleAttackResolved;
            enabled = true;
        }

        internal void Unbind()
        {
            if (attacks != null)
                attacks.AttackResolved -= HandleAttackResolved;
            pending.Clear();
            session = null;
            registry = null;
            attacks = null;
            weapons = null;
            enabled = false;
        }

        private void HandleAttackResolved(GameplayActionRecord action)
        {
            if (!TryGetAttackResolution(action, out var resolution)
                || resolution.Wound == null
                || !registry.TryGetActor(
                    resolution.TargetId,
                    out GameplayActorView target))
            {
                return;
            }

            float delay = ResolveDelaySeconds(resolution);
            bool incapacitated = session.IsActorIncapacitated(
                resolution.TargetId);
            ActorAnimationCoordinator animation = target.Root.GetComponent<
                ActorAnimationCoordinator>();
            if (delay > 0f && incapacitated)
                animation?.DeferIncapacitationPresentation();
            if (delay <= 0f)
            {
                if (TryPresent(action, resolution))
                    return;
            }

            pending.Add(new PendingReaction(action, resolution, delay));
        }

        private float ResolveDelaySeconds(AttackResolutionRecord resolution)
        {
            if (!resolution.IsContactAttack)
                return 0f;
            string itemId = session.GetActor(
                resolution.AttackerId).EquippedItemId;
            if (string.IsNullOrWhiteSpace(itemId))
                return 0f;
            WeaponPresentationDefinition definition = weapons.Get(itemId);
            if (definition.AttackPresentation !=
                WeaponAttackPresentationKind.ContactStrike)
            {
                return 0f;
            }
            return definition.ContactStrikeSeconds *
                definition.ContactImpactNormalizedTime;
        }

        internal void Tick(float deltaTime)
        {
            float elapsed = Mathf.Max(0f, deltaTime);
            for (int index = pending.Count - 1; index >= 0; index--)
            {
                PendingReaction reaction = pending[index];
                reaction.RemainingSeconds -= elapsed;
                if (reaction.RemainingSeconds > 0f)
                    continue;
                if (TryPresent(reaction.Action, reaction.Resolution))
                    pending.RemoveAt(index);
            }
        }

        private bool TryPresent(
            GameplayActionRecord action,
            AttackResolutionRecord resolution)
        {
            if (!registry.TryGetActor(
                    resolution.TargetId,
                    out GameplayActorView target)
                || !resolution.HitRegion.HasValue)
            {
                return true;
            }
            ActorAnimationCoordinator animation = target.Root.GetComponent<
                ActorAnimationCoordinator>();
            if (animation != null && animation.IsPresentingReplay)
                return false;
            bool incapacitated = session.IsActorIncapacitated(
                resolution.TargetId);
            bool pinned = session.GetActor(resolution.TargetId).IsPinned;
            bool presented = incapacitated
                ? animation != null && animation.PresentWoundReaction(
                    resolution.HitRegion.Value,
                    incapacitated: true,
                    pinned)
                : !pinned && target.InjuryOverlay.PresentHitReaction(
                    resolution.HitRegion.Value);
            if (presented && incapacitated)
            {
                Vector3 impulseDirection = target.Transform.forward;
                if (registry.TryGetActor(
                        resolution.AttackerId,
                        out GameplayActorView attacker))
                {
                    Vector3 displacement = target.Transform.position -
                        attacker.Transform.position;
                    if (displacement.sqrMagnitude > 0.0001f)
                        impulseDirection = displacement.normalized;
                }
                target.Root.GetComponent<ActorRagdollPresenter>()
                    ?.ArmIncapacitation(
                        ResolveJournalSequence(action),
                        resolution.HitRegion,
                        impulseDirection,
                        resolution.IsContactAttack
                            ? GameplayCloseQuartersPresentationTiming
                                .ContactImpactNormalizedTime
                            : 0f);
            }
            return true;
        }

        private long ResolveJournalSequence(GameplayActionRecord action)
        {
            IReadOnlyList<GameplayJournalEntry> entries =
                session.Journal.Entries;
            for (int index = entries.Count - 1; index >= 0; index--)
            {
                if (entries[index] is ActionResolvedJournalEntry resolved &&
                    ReferenceEquals(resolved.Action, action))
                {
                    return resolved.Sequence;
                }
            }
            return 0L;
        }

        private static bool TryGetAttackResolution(
            GameplayActionRecord action,
            out AttackResolutionRecord resolution)
        {
            if (action != null)
            {
                foreach (GameplayActionOutcome outcome in action.Outcomes)
                {
                    if (outcome is AttackResolvedActionOutcome attack)
                    {
                        resolution = attack.Attack;
                        return true;
                    }
                }
            }
            resolution = null;
            return false;
        }

        private void Update() => Tick(Time.unscaledDeltaTime);

        private void OnDestroy() => Unbind();
    }
}
