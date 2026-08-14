using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayWeaponTargetingController : MonoBehaviour,
        IGameplayWarningHintSource
    {
        private GameplaySession session;
        private string actorId;
        private Func<bool> confirmFire;
        private Action<bool> targetingChanged;

        public bool IsTargeting { get; private set; }

        public string StatusMessage { get; private set; } = string.Empty;

        public GameplayWarningHintModel CurrentWarningHint
        {
            get
            {
                if (!IsTargeting)
                    return null;

                AttackDefinition attack = session?.GetEquippedAttack(actorId);
                string instruction = attack?.Contact == null
                    ? "CLICK A TARGET OR WORLD POINT TO FIRE"
                    : $"CLICK AN ACTOR WITHIN {attack.Contact.MaximumReach:0.#} M TO STRIKE";
                return new GameplayWarningHintModel(
                    "weapon.attack.confirmation",
                    $"{attack?.DisplayName?.ToUpperInvariant() ?? "ATTACK"} ARMED - {instruction} - PRESS THE LIT WEAPON HOTKEY OR ESC TO CANCEL",
                    90);
            }
        }

        internal void Bind(
            GameplaySession gameplaySession,
            string authoritativeActorId,
            Func<bool> onConfirmFire,
            Action<bool> onTargetingChanged = null)
        {
            Unbind();
            session = gameplaySession
                ?? throw new ArgumentNullException(nameof(gameplaySession));
            if (string.IsNullOrWhiteSpace(authoritativeActorId))
                throw new ArgumentException(
                    "Weapon targeting requires an actor ID.",
                    nameof(authoritativeActorId));
            confirmFire = onConfirmFire
                ?? throw new ArgumentNullException(nameof(onConfirmFire));
            targetingChanged = onTargetingChanged;
            enabled = true;
            SetActor(authoritativeActorId);
        }

        public void SetActor(string authoritativeActorId)
        {
            if (session == null)
            {
                throw new InvalidOperationException(
                    "Bind weapon targeting before changing actors.");
            }
            if (string.IsNullOrWhiteSpace(authoritativeActorId))
            {
                throw new ArgumentException(
                    "Weapon targeting requires an actor ID.",
                    nameof(authoritativeActorId));
            }

            session.GetActor(authoritativeActorId);
            CancelTargeting();
            actorId = authoritativeActorId;
            StatusMessage = string.Empty;
        }

        public bool BeginTargeting()
        {
            AttackDefinition attack = session?.GetEquippedAttack(actorId);
            if (attack == null)
            {
                StatusMessage = "No weapon is equipped.";
                return false;
            }

            IsTargeting = true;
            targetingChanged?.Invoke(true);
            StatusMessage = attack.Contact == null
                ? "FIRE ARMED - SELECT A TARGET OR WORLD POINT"
                : $"{attack.DisplayName.ToUpperInvariant()} ARMED - SELECT AN ACTOR WITHIN {attack.Contact.MaximumReach:0.#} M";
            return true;
        }

        public bool ConfirmTargeting()
        {
            if (!IsTargeting || confirmFire == null)
                return false;
            if (!confirmFire())
                return false;

            IsTargeting = false;
            targetingChanged?.Invoke(false);
            StatusMessage = string.Empty;
            return true;
        }

        public bool ToggleTargeting()
        {
            return IsTargeting ? CancelTargeting() : BeginTargeting();
        }

        public bool CancelTargeting()
        {
            if (!IsTargeting)
                return false;
            IsTargeting = false;
            targetingChanged?.Invoke(false);
            StatusMessage = "Attack canceled.";
            return true;
        }

        internal void Unbind()
        {
            session = null;
            actorId = null;
            confirmFire = null;
            if (IsTargeting)
                targetingChanged?.Invoke(false);
            targetingChanged = null;
            IsTargeting = false;
            StatusMessage = string.Empty;
            enabled = false;
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
