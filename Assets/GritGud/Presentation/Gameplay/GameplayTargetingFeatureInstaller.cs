using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayTargetingFeatureInstaller :
        IGameplayFeatureInstaller
    {
        private readonly GameplaySession session;
        private readonly GameplayWorldRegistry worldRegistry;
        private readonly GameplaySmokeFieldSession smokeFields;
        private readonly TargetAcquisitionPresenter targetAcquisition;
        private readonly GameplayDisplacementController displacement;
        private readonly GameplayDestructibleController destructibles;
        private readonly LevelWorld levelWorld;
        private readonly GameplayScenarioAssembly scenario;
        private readonly GameplayDialogueLog dialogue;
        private readonly GameplaySessionPresenter sessionPresenter;
        private readonly TurnMovementController turnMovement;
        private readonly ExplorationMovementInput movementInput;
        private readonly GameplayInputController input;
        private readonly ThirdPersonMotor player;
        private readonly GameplayHud hud;
        private readonly string actorId;
        private readonly IEnumerable<LevelTraversalLinkData> traversalLinks;
        private readonly uint displacementRandomSeed;
        private readonly Func<Vector2, bool> pointerBlocker;

        public GameplayTargetingFeatureInstaller(
            GameplaySession session,
            GameplayWorldRegistry worldRegistry,
            GameplaySmokeFieldSession smokeFields,
            TargetAcquisitionPresenter targetAcquisition,
            GameplayDisplacementController displacement,
            GameplayDestructibleController destructibles,
            LevelWorld levelWorld,
            GameplayScenarioAssembly scenario,
            GameplayDialogueLog dialogue,
            GameplaySessionPresenter sessionPresenter,
            TurnMovementController turnMovement,
            ExplorationMovementInput movementInput,
            GameplayInputController input,
            ThirdPersonMotor player,
            GameplayHud hud,
            string actorId,
            IEnumerable<LevelTraversalLinkData> traversalLinks,
            uint displacementRandomSeed,
            Func<Vector2, bool> pointerBlocker)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.worldRegistry = worldRegistry ?? throw new ArgumentNullException(
                nameof(worldRegistry));
            this.smokeFields = smokeFields ?? throw new ArgumentNullException(
                nameof(smokeFields));
            this.targetAcquisition = targetAcquisition
                ?? throw new ArgumentNullException(nameof(targetAcquisition));
            this.displacement = displacement ?? throw new ArgumentNullException(
                nameof(displacement));
            this.destructibles = destructibles ?? throw new ArgumentNullException(
                nameof(destructibles));
            this.levelWorld = levelWorld ?? throw new ArgumentNullException(
                nameof(levelWorld));
            this.scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            this.dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
            this.sessionPresenter = sessionPresenter
                ?? throw new ArgumentNullException(nameof(sessionPresenter));
            this.turnMovement = turnMovement ?? throw new ArgumentNullException(
                nameof(turnMovement));
            this.movementInput = movementInput ?? throw new ArgumentNullException(
                nameof(movementInput));
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.hud = hud ?? throw new ArgumentNullException(nameof(hud));
            this.actorId = string.IsNullOrWhiteSpace(actorId)
                ? throw new ArgumentException("An actor is required.", nameof(actorId))
                : actorId;
            this.traversalLinks = traversalLinks;
            this.displacementRandomSeed = displacementRandomSeed;
            this.pointerBlocker = pointerBlocker ?? throw new ArgumentNullException(
                nameof(pointerBlocker));
        }

        public GameplayFeatureStage Stage =>
            GameplayFeatureStage.TargetingAndMovement;

        public void Install()
        {
            targetAcquisition.Bind(
                session,
                worldRegistry,
                actorId,
                smokeFields);
            targetAcquisition.SetPointerBlocker(pointerBlocker);
            displacement.Bind(
                session,
                destructibles,
                levelWorld,
                worldRegistry,
                scenario,
                displacementRandomSeed,
                targetAcquisition,
                dialogue,
                sessionPresenter.TryBeginEncounterFromAction);
            turnMovement.Bind(
                session,
                movementInput,
                input,
                player,
                actorId,
                traversalLinks);
            hud.BindTurnMovement(turnMovement);
        }
    }
}
