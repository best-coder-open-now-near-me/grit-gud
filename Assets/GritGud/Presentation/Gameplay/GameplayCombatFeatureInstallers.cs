using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors.Animation;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayActorActionsFeatureInstaller :
        IGameplayFeatureInstaller
    {
        private readonly GameplaySession session;
        private readonly ThirdPersonMotor player;
        private readonly GameplayActionController actions;
        private readonly GameplaySessionPresenter sessionPresenter;
        private readonly GameplayAttackController attacks;
        private readonly TargetAcquisitionPresenter targets;
        private readonly GameplayDialogueLog dialogue;
        private readonly GameplayDestructibleController destructibles;
        private readonly GameplaySurfaceImpactPresenter surfaceImpacts;
        private readonly GameplayWorldRegistry worldRegistry;
        private readonly SurfacePresentationCatalog surfaces;
        private readonly Transform worldRoot;
        private readonly GameplayEquipmentController equipment;
        private readonly GameplayScenarioAssembly scenario;
        private readonly GameplaySmokeFieldSession smokeFields;
        private readonly string actorId;
        private readonly string objectiveId;
        private readonly Action<string> requestItemPower;
        private readonly Func<string, bool> canRequestItemPower;

        public GameplayActorActionsFeatureInstaller(
            GameplaySession session,
            ThirdPersonMotor player,
            GameplayActionController actions,
            GameplaySessionPresenter sessionPresenter,
            GameplayAttackController attacks,
            TargetAcquisitionPresenter targets,
            GameplayDialogueLog dialogue,
            GameplayDestructibleController destructibles,
            GameplaySurfaceImpactPresenter surfaceImpacts,
            GameplayWorldRegistry worldRegistry,
            SurfacePresentationCatalog surfaces,
            Transform worldRoot,
            GameplayEquipmentController equipment,
            GameplayScenarioAssembly scenarioAssembly,
            GameplaySmokeFieldSession smokeFieldSession,
            string actorId,
            string objectiveId,
            Action<string> requestItemPower,
            Func<string, bool> canRequestItemPower)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
            this.sessionPresenter = sessionPresenter
                ?? throw new ArgumentNullException(nameof(sessionPresenter));
            this.attacks = attacks ?? throw new ArgumentNullException(nameof(attacks));
            this.targets = targets ?? throw new ArgumentNullException(nameof(targets));
            this.dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
            this.destructibles = destructibles ?? throw new ArgumentNullException(
                nameof(destructibles));
            this.surfaceImpacts = surfaceImpacts ?? throw new ArgumentNullException(
                nameof(surfaceImpacts));
            this.worldRegistry = worldRegistry ?? throw new ArgumentNullException(
                nameof(worldRegistry));
            this.surfaces = surfaces ?? throw new ArgumentNullException(nameof(surfaces));
            this.worldRoot = worldRoot ?? throw new ArgumentNullException(nameof(worldRoot));
            this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            scenario = scenarioAssembly ?? throw new ArgumentNullException(
                nameof(scenarioAssembly));
            smokeFields = smokeFieldSession ?? throw new ArgumentNullException(
                nameof(smokeFieldSession));
            this.actorId = actorId;
            this.objectiveId = objectiveId;
            this.requestItemPower = requestItemPower
                ?? throw new ArgumentNullException(nameof(requestItemPower));
            this.canRequestItemPower = canRequestItemPower
                ?? throw new ArgumentNullException(nameof(canRequestItemPower));
        }

        public GameplayFeatureStage Stage => GameplayFeatureStage.ActorActions;

        public void Install()
        {
            ActorAnimationCoordinator animationCoordinator =
                player.GetComponent<ActorAnimationCoordinator>();
            actions.Bind(
                session,
                sessionPresenter,
                animationCoordinator,
                actorId,
                objectiveId);
            attacks.Bind(
                session,
                targets,
                dialogue,
                actorId,
                sessionPresenter.TryBeginEncounterFromAction,
                destructibles.Session,
                UnityTacticalContextQuery.CreateForWorld(
                    session,
                    worldRegistry,
                    smokeFields),
                new GameplayTacticalContextEvaluator(
                    scenario.TacticalRules));
            surfaceImpacts.Bind(
                attacks,
                worldRegistry,
                surfaces,
                worldRoot);
            equipment.Bind(
                session,
                actorId,
                requestItemPower,
                canRequestItemPower);
        }
    }

    internal sealed class GameplayProjectileDeliveryFeatureInstaller :
        IGameplayFeatureInstaller
    {
        private readonly GameplaySession session;
        private readonly GameplayWorldRegistry worldRegistry;
        private readonly GameplayDestructibleController destructibles;
        private readonly TargetAcquisitionPresenter targets;
        private readonly GameplayDialogueLog dialogue;
        private readonly GameplayActionController actions;
        private readonly GameplaySessionPresenter sessionPresenter;
        private readonly GameplayProjectileController projectiles;
        private readonly GameplayThrownExplosiveController thrownExplosives;
        private readonly GameplaySmokeFieldSession smokeFields;
        private readonly string actorId;
        private readonly uint thrownExplosiveRandomSeed;
        private readonly Action<
            GameplayEmergencyCycleSession,
            GameplayConsumableController> captureInstalled;

        public GameplayProjectileDeliveryFeatureInstaller(
            GameplaySession session,
            GameplayWorldRegistry worldRegistry,
            GameplayDestructibleController destructibles,
            TargetAcquisitionPresenter targets,
            GameplayDialogueLog dialogue,
            GameplayActionController actions,
            GameplaySessionPresenter sessionPresenter,
            GameplayProjectileController projectiles,
            GameplayThrownExplosiveController thrownExplosives,
            GameplaySmokeFieldSession smokeFields,
            string actorId,
            uint thrownExplosiveRandomSeed,
            Action<GameplayEmergencyCycleSession, GameplayConsumableController>
                captureInstalled)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.worldRegistry = worldRegistry ?? throw new ArgumentNullException(
                nameof(worldRegistry));
            this.destructibles = destructibles ?? throw new ArgumentNullException(
                nameof(destructibles));
            this.targets = targets ?? throw new ArgumentNullException(nameof(targets));
            this.dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
            this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
            this.sessionPresenter = sessionPresenter
                ?? throw new ArgumentNullException(nameof(sessionPresenter));
            this.projectiles = projectiles ?? throw new ArgumentNullException(
                nameof(projectiles));
            this.thrownExplosives = thrownExplosives
                ?? throw new ArgumentNullException(nameof(thrownExplosives));
            this.smokeFields = smokeFields ?? throw new ArgumentNullException(
                nameof(smokeFields));
            this.actorId = actorId;
            this.thrownExplosiveRandomSeed = thrownExplosiveRandomSeed;
            this.captureInstalled = captureInstalled
                ?? throw new ArgumentNullException(nameof(captureInstalled));
        }

        public GameplayFeatureStage Stage =>
            GameplayFeatureStage.ProjectileAndConsumableDelivery;

        public void Install()
        {
            var blastWorldQuery = new UnityBlastWorldQuery(
                worldRegistry,
                () => session.Journal.LastEntry?.Sequence ?? 0L,
                propId => destructibles.Session.TryGetProp(propId, out _));
            var blastConsequences = new GameplayBlastConsequenceResolver(
                session,
                destructibles.Session);
            var emergencyCycle = new GameplayEmergencyCycleSession(session);
            projectiles.Bind(
                session,
                worldRegistry,
                blastWorldQuery,
                blastConsequences,
                targets,
                dialogue,
                actorId,
                onTurnModeStartRequested: actions.TryEnterTurnMode,
                onEncounterStartRequested:
                    sessionPresenter.TryBeginEncounterFromAction,
                emergencyCycle: emergencyCycle);
            thrownExplosives.Bind(
                session,
                worldRegistry,
                blastWorldQuery,
                blastConsequences,
                targets,
                dialogue,
                actorId,
                thrownExplosiveRandomSeed,
                sessionPresenter.TryBeginEncounterFromAction,
                smokeFieldSession: smokeFields);
            captureInstalled(
                emergencyCycle,
                new GameplayConsumableController(session, thrownExplosives));
        }
    }

    internal sealed class GameplayHotbarFeatureInstaller :
        IGameplayFeatureInstaller
    {
        private readonly GameplayHotbarController hotbar;
        private readonly GameplaySession session;
        private readonly string actorId;
        private readonly IReadOnlyList<GameplayActorAbilityHotbarDefinition>
            actorAbilities;
        private readonly Func<string, int, bool> activateItem;
        private readonly Func<string, string, bool> activateAbility;
        private readonly Func<GameplayHotbarBinding, bool> canActivate;
        private readonly Action cancelPending;

        public GameplayHotbarFeatureInstaller(
            GameplayHotbarController hotbar,
            GameplaySession session,
            string actorId,
            IReadOnlyList<GameplayActorAbilityHotbarDefinition> actorAbilities,
            Func<string, int, bool> activateItem,
            Func<string, string, bool> activateAbility,
            Func<GameplayHotbarBinding, bool> canActivate,
            Action cancelPending)
        {
            this.hotbar = hotbar ?? throw new ArgumentNullException(nameof(hotbar));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.actorId = actorId;
            this.actorAbilities = actorAbilities ?? throw new ArgumentNullException(
                nameof(actorAbilities));
            this.activateItem = activateItem ?? throw new ArgumentNullException(
                nameof(activateItem));
            this.activateAbility = activateAbility ?? throw new ArgumentNullException(
                nameof(activateAbility));
            this.canActivate = canActivate ?? throw new ArgumentNullException(
                nameof(canActivate));
            this.cancelPending = cancelPending ?? throw new ArgumentNullException(
                nameof(cancelPending));
        }

        public GameplayFeatureStage Stage => GameplayFeatureStage.Hotbar;

        public void Install() => hotbar.Bind(
            session,
            actorId,
            actorAbilities,
            activateItem,
            activateAbility,
            canActivate,
            cancelPending);
    }
}
