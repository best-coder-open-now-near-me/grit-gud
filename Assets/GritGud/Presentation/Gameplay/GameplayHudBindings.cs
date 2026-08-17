using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayHudBindings
    {
        private readonly List<IGameplayWarningHintSource> warningHintSources =
            new List<IGameplayWarningHintSource>();
        private Action<string> bugReportExportRequested;
        private Action turnModeToggleRequested;

        public GameplaySession Session { get; private set; }

        public string PlayerActorId { get; private set; }

        public GameplayScenarioAssembly Scenario { get; private set; }

        public TurnMovementController TurnMovement { get; private set; }

        public GameplayActionController ActionController { get; private set; }

        public GameplayAttackController AttackController { get; private set; }

        public GameplayEquipmentController EquipmentController
        {
            get;
            private set;
        }

        public GameplayHotbarController HotbarController { get; private set; }

        public GameplayConsumableController ConsumableController
        {
            get;
            private set;
        }

        public GameplayWeaponTargetingController WeaponTargetingController
        {
            get;
            private set;
        }

        public GameplayProjectileController ProjectileController
        {
            get;
            private set;
        }

        public GameplayDisplacementController DisplacementController
        {
            get;
            private set;
        }

        public IGameplayInputSource InputSource { get; private set; }

        public IReadOnlyList<IGameplayWarningHintSource> WarningHintSources =>
            warningHintSources;

        public string BugReportStatus { get; private set; } = string.Empty;

        public bool BugReportNoteOpen { get; private set; }

        public string BugReportNote { get; set; } = string.Empty;

        public void BindSession(
            GameplaySession session,
            string authoritativePlayerActorId,
            GameplayScenarioAssembly scenarioAssembly)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (string.IsNullOrWhiteSpace(authoritativePlayerActorId))
            {
                throw new ArgumentException(
                    "HUD player actor identifiers cannot be empty.",
                    nameof(authoritativePlayerActorId));
            }

            session.GetActor(authoritativePlayerActorId);
            Session = session;
            PlayerActorId = authoritativePlayerActorId;
            Scenario = scenarioAssembly
                ?? throw new ArgumentNullException(nameof(scenarioAssembly));
        }

        public void SetActor(string authoritativePlayerActorId)
        {
            if (Session == null)
            {
                throw new InvalidOperationException(
                    "Bind the HUD session before changing actors.");
            }
            if (string.IsNullOrWhiteSpace(authoritativePlayerActorId))
            {
                throw new ArgumentException(
                    "HUD player actor identifiers cannot be empty.",
                    nameof(authoritativePlayerActorId));
            }

            Session.GetActor(authoritativePlayerActorId);
            PlayerActorId = authoritativePlayerActorId;
        }

        public void UnbindSession()
        {
            Session = null;
            PlayerActorId = null;
            Scenario = null;
        }

        public void BindTurnMovement(TurnMovementController controller)
        {
            TurnMovement = controller;
        }

        public void UnbindTurnMovement()
        {
            TurnMovement = null;
        }

        public void BindGameplayActions(GameplayActionController controller)
        {
            ActionController = controller
                ?? throw new ArgumentNullException(nameof(controller));
        }

        public void UnbindGameplayActions()
        {
            ActionController = null;
        }

        public void BindGameplayAttack(GameplayAttackController controller)
        {
            AttackController = controller
                ?? throw new ArgumentNullException(nameof(controller));
        }

        public void UnbindGameplayAttack()
        {
            AttackController = null;
        }

        public void BindGameplayEquipment(
            GameplayEquipmentController controller)
        {
            if (EquipmentController != null)
                UnbindWarningHintSource(EquipmentController);

            EquipmentController = controller
                ?? throw new ArgumentNullException(nameof(controller));
            BindWarningHintSource(EquipmentController);
        }

        public void UnbindGameplayEquipment()
        {
            if (EquipmentController != null)
                UnbindWarningHintSource(EquipmentController);
            EquipmentController = null;
        }

        public void BindGameplayHotbar(GameplayHotbarController controller)
        {
            HotbarController = controller
                ?? throw new ArgumentNullException(nameof(controller));
        }

        public void UnbindGameplayHotbar()
        {
            HotbarController = null;
        }

        public void BindGameplayConsumables(
            GameplayConsumableController controller)
        {
            ConsumableController = controller
                ?? throw new ArgumentNullException(nameof(controller));
        }

        public void UnbindGameplayConsumables()
        {
            ConsumableController = null;
        }

        public void BindGameplayWeaponTargeting(
            GameplayWeaponTargetingController controller)
        {
            if (WeaponTargetingController != null)
                UnbindWarningHintSource(WeaponTargetingController);

            WeaponTargetingController = controller
                ?? throw new ArgumentNullException(nameof(controller));
            BindWarningHintSource(WeaponTargetingController);
        }

        public void UnbindGameplayWeaponTargeting()
        {
            if (WeaponTargetingController != null)
                UnbindWarningHintSource(WeaponTargetingController);
            WeaponTargetingController = null;
        }

        public void BindWarningHintSource(IGameplayWarningHintSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!warningHintSources.Contains(source))
                warningHintSources.Add(source);
        }

        public void UnbindWarningHintSource(IGameplayWarningHintSource source)
        {
            if (source != null)
                warningHintSources.Remove(source);
        }

        public void BindGameplayProjectile(
            GameplayProjectileController controller)
        {
            ProjectileController = controller
                ?? throw new ArgumentNullException(nameof(controller));
        }

        public void UnbindGameplayProjectile()
        {
            ProjectileController = null;
        }

        public void BindGameplayDisplacement(
            GameplayDisplacementController controller)
        {
            if (DisplacementController != null)
                UnbindWarningHintSource(DisplacementController);

            DisplacementController = controller
                ?? throw new ArgumentNullException(nameof(controller));
            BindWarningHintSource(DisplacementController);
        }

        public void UnbindGameplayDisplacement()
        {
            if (DisplacementController != null)
                UnbindWarningHintSource(DisplacementController);
            DisplacementController = null;
        }

        public void BindInputSource(IGameplayInputSource source)
        {
            InputSource = source
                ?? throw new ArgumentNullException(nameof(source));
        }

        public void UnbindInputSource()
        {
            InputSource = null;
        }

        public void BindTurnModeToggle(Action toggleRequested)
        {
            turnModeToggleRequested = toggleRequested
                ?? throw new ArgumentNullException(nameof(toggleRequested));
        }

        public void UnbindTurnModeToggle()
        {
            turnModeToggleRequested = null;
        }

        public void RequestTurnModeToggle()
        {
            turnModeToggleRequested?.Invoke();
        }

        public void RequestEndTurn()
        {
            ActionController?.TryEndTurn();
        }

        public string GetBindingDisplay(GameplayControl control) =>
            InputSource?.GetBindingDisplay(control) ?? string.Empty;

        public void BindBugReportExport(Action<string> exportRequested)
        {
            bugReportExportRequested = exportRequested
                ?? throw new ArgumentNullException(nameof(exportRequested));
            BugReportStatus = string.Empty;
            BugReportNoteOpen = false;
            BugReportNote = string.Empty;
        }

        public void UnbindBugReportExport()
        {
            bugReportExportRequested = null;
            BugReportStatus = string.Empty;
            BugReportNoteOpen = false;
            BugReportNote = string.Empty;
        }

        public void SetBugReportStatus(string status)
        {
            BugReportStatus = status ?? string.Empty;
        }

        public void OpenBugReportNote()
        {
            if (bugReportExportRequested == null)
                return;
            BugReportNote = string.Empty;
            BugReportNoteOpen = true;
        }

        public void SubmitBugReportNote(string note)
        {
            BugReportNoteOpen = false;
            BugReportNote = string.Empty;
            bugReportExportRequested?.Invoke(note ?? string.Empty);
        }

        public void CancelBugReportNote()
        {
            BugReportNoteOpen = false;
            BugReportNote = string.Empty;
        }
    }
}
