using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayEncounterActorsFeatureInstaller :
        IGameplayFeatureInstaller
    {
        private readonly GameplaySession session;
        private readonly Func<GameplayEmergencyCycleSession> resolveEmergencyCycle;
        private readonly GameplayActionController actions;
        private readonly GameplayProjectileController projectiles;
        private readonly PlayerPartyDefinition party;
        private readonly GameplayWorldRegistry worldRegistry;
        private readonly GameplayAttackController attacks;
        private readonly TargetAcquisitionPresenter targets;
        private readonly GameplayEnemyController enemies;
        private readonly GameplaySessionPresenter sessionPresenter;
        private readonly GameplayDisplacementController displacement;
        private readonly GameplayPartyControlSession partyControl;
        private readonly GameplayDialogueLog dialogue;
        private readonly GameplaySmokeFieldSession smokeFields;
        private readonly IEnumerable<LevelTraversalLinkData> traversalLinks;
        private readonly GameplayCombatReactionPresenter combatReactions;
        private readonly Action<GameplayPartyPresentationSession>
            capturePartyPresentation;

        public GameplayEncounterActorsFeatureInstaller(
            GameplaySession session,
            Func<GameplayEmergencyCycleSession> resolveEmergencyCycle,
            GameplayActionController actions,
            GameplayProjectileController projectiles,
            PlayerPartyDefinition party,
            GameplayWorldRegistry worldRegistry,
            GameplayAttackController attacks,
            TargetAcquisitionPresenter targets,
            GameplayEnemyController enemies,
            GameplaySessionPresenter sessionPresenter,
            GameplayDisplacementController displacement,
            GameplayPartyControlSession partyControl,
            GameplayDialogueLog dialogue,
            GameplaySmokeFieldSession smokeFields,
            IEnumerable<LevelTraversalLinkData> traversalLinks,
            GameplayCombatReactionPresenter combatReactions,
            Action<GameplayPartyPresentationSession> capturePartyPresentation)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.resolveEmergencyCycle = resolveEmergencyCycle
                ?? throw new ArgumentNullException(nameof(resolveEmergencyCycle));
            this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
            this.projectiles = projectiles ?? throw new ArgumentNullException(
                nameof(projectiles));
            this.party = party ?? throw new ArgumentNullException(nameof(party));
            this.worldRegistry = worldRegistry ?? throw new ArgumentNullException(
                nameof(worldRegistry));
            this.attacks = attacks ?? throw new ArgumentNullException(nameof(attacks));
            this.targets = targets ?? throw new ArgumentNullException(nameof(targets));
            this.enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
            this.sessionPresenter = sessionPresenter
                ?? throw new ArgumentNullException(nameof(sessionPresenter));
            this.displacement = displacement ?? throw new ArgumentNullException(
                nameof(displacement));
            this.partyControl = partyControl ?? throw new ArgumentNullException(
                nameof(partyControl));
            this.dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
            this.smokeFields = smokeFields ?? throw new ArgumentNullException(
                nameof(smokeFields));
            this.traversalLinks = traversalLinks;
            this.combatReactions = combatReactions
                ?? throw new ArgumentNullException(nameof(combatReactions));
            this.capturePartyPresentation = capturePartyPresentation
                ?? throw new ArgumentNullException(nameof(capturePartyPresentation));
        }

        public GameplayFeatureStage Stage => GameplayFeatureStage.EncounterActors;

        public void Install()
        {
            GameplayEmergencyCycleSession emergencyCycle =
                resolveEmergencyCycle() ?? throw new InvalidOperationException(
                    "Projectile delivery must install before encounter actors.");
            actions.BindEmergencyCycle(emergencyCycle);
            actions.RegisterTurnModeExitConstraint(projectiles);
            var partyPresentation = new GameplayPartyPresentationSession(
                session,
                party,
                worldRegistry,
                attacks,
                projectiles,
                targets);
            capturePartyPresentation(partyPresentation);
            enemies.Bind(
                session,
                worldRegistry,
                sessionPresenter,
                actions,
                attacks,
                projectiles,
                displacement,
                emergencyCycle,
                partyControl,
                dialogue,
                sessionPresenter.TryBeginEncounter,
                obscuranceQuery: smokeFields,
                traversalLinks: traversalLinks);
            combatReactions.Bind(session, worldRegistry, attacks);
        }
    }

    internal sealed class GameplayAimingFeatureInstaller :
        IGameplayFeatureInstaller
    {
        private readonly GameplaySession session;
        private readonly string actorId;
        private readonly TargetAcquisitionPresenter targets;
        private readonly GameplayProjectileController projectiles;
        private readonly GameplayWeaponTargetingController weaponTargeting;
        private readonly GameplayTargetingCursorPresenter cursor;
        private readonly Func<Transform> selectedMuzzle;
        private readonly Func<bool> confirmFire;
        private readonly Func<bool> shouldShowCursor;
        private readonly Func<bool?> resolveCursorValidity;

        public GameplayAimingFeatureInstaller(
            GameplaySession session,
            string actorId,
            TargetAcquisitionPresenter targets,
            GameplayProjectileController projectiles,
            GameplayWeaponTargetingController weaponTargeting,
            GameplayTargetingCursorPresenter cursor,
            Func<Transform> selectedMuzzle,
            Func<bool> confirmFire,
            Func<bool> shouldShowCursor,
            Func<bool?> resolveCursorValidity)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.actorId = actorId;
            this.targets = targets ?? throw new ArgumentNullException(nameof(targets));
            this.projectiles = projectiles ?? throw new ArgumentNullException(
                nameof(projectiles));
            this.weaponTargeting = weaponTargeting
                ?? throw new ArgumentNullException(nameof(weaponTargeting));
            this.cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
            this.selectedMuzzle = selectedMuzzle ?? throw new ArgumentNullException(
                nameof(selectedMuzzle));
            this.confirmFire = confirmFire ?? throw new ArgumentNullException(
                nameof(confirmFire));
            this.shouldShowCursor = shouldShowCursor
                ?? throw new ArgumentNullException(nameof(shouldShowCursor));
            this.resolveCursorValidity = resolveCursorValidity
                ?? throw new ArgumentNullException(nameof(resolveCursorValidity));
        }

        public GameplayFeatureStage Stage =>
            GameplayFeatureStage.AimingPresentation;

        public void Install()
        {
            targets.SetWeaponAimOriginProvider(ResolveMuzzlePosition);
            projectiles.BindVisualLaunchOrigin(ResolveMuzzlePosition);
            weaponTargeting.Bind(
                session,
                actorId,
                confirmFire,
                active => targets.SetWeaponTargetingActive(active));
            cursor.Bind(shouldShowCursor, resolveCursorValidity);
        }

        private Vector3? ResolveMuzzlePosition()
        {
            Transform muzzle = selectedMuzzle();
            return muzzle != null ? muzzle.position : (Vector3?)null;
        }
    }

    internal sealed class GameplayObjectiveFeatureInstaller :
        IGameplayFeatureInstaller
    {
        private readonly GameplaySession session;
        private readonly GameplayObjectivePresenter objectivePresenter;
        private readonly string objectiveId;

        public GameplayObjectiveFeatureInstaller(
            GameplaySession session,
            GameplayObjectivePresenter objectivePresenter,
            string objectiveId)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.objectivePresenter = objectivePresenter
                ?? throw new ArgumentNullException(nameof(objectivePresenter));
            this.objectiveId = objectiveId;
        }

        public GameplayFeatureStage Stage => GameplayFeatureStage.Objective;

        public void Install()
        {
            if (!string.IsNullOrWhiteSpace(objectiveId))
                objectivePresenter.Bind(session, objectiveId);
        }
    }
}
