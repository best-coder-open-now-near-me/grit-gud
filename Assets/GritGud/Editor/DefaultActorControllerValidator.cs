using System;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors.Animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using static GritGud.Editor.DefaultActorAssetRecipe;

namespace GritGud.Editor
{
    internal static class DefaultActorControllerValidator
    {
        internal static void Validate(AnimatorController controller)
        {
            ValidateParameters(controller);
            AnimatorStateMachine stateMachine =
                controller.layers[0].stateMachine;
            AnimatorState standingState =
                FindState(stateMachine, StandingStateName);
            AnimatorState crouchedState =
                FindState(stateMachine, CrouchedStateName);
            if (standingState == null || crouchedState == null ||
                stateMachine.defaultState != standingState ||
                !(standingState.motion is BlendTree locomotion) ||
                locomotion.blendType != BlendTreeType.FreeformCartesian2D ||
                locomotion.blendParameter !=
                    ActorAnimationParameters.MoveXName ||
                locomotion.blendParameterY !=
                    ActorAnimationParameters.MoveYName)
            {
                throw new InvalidOperationException(
                    "The default player controller requires a standing "
                    + "MoveX/MoveY 2D locomotion state.");
            }

            ValidateStandingLocomotion(locomotion);
            ValidateCrouchedLocomotion(crouchedState);
            ValidateStanceTransition(
                standingState,
                crouchedState,
                ActorStance.Crouched);
            ValidateStanceTransition(
                crouchedState,
                standingState,
                ActorStance.Standing);
            ValidateTurnInPlaceLayer(controller);
            ValidateWeaponLayer(controller);
            ValidateRecoilLayer(controller);
            ValidateActionLayer(controller);
            ValidateTraversalLayer(controller);
        }

        internal static bool HasRequiredTurnLayer(
            AnimatorController controller)
        {
            AnimatorControllerLayer turnLayer =
                FindLayer(controller, ActorAnimationParameters.TurnLayerName);
            if (turnLayer == null || turnLayer.avatarMask == null ||
                !IsBodyRegionMask(
                    turnLayer.avatarMask,
                    BodyRegion.PelvisAndLegs))
            {
                return false;
            }

            AnimatorState turnState = FindState(
                turnLayer.stateMachine,
                ActorAnimationParameters.TurnInPlaceStateName);
            return turnState?.motion is BlendTree turnInPlace &&
                Mathf.Abs(
                    turnState.speed -
                    TurnPlaybackSpeed) <= 0.001f &&
                turnInPlace.name == TurnInPlaceBlendName &&
                turnInPlace.blendType == BlendTreeType.Simple1D &&
                turnInPlace.blendParameter ==
                    ActorAnimationParameters.TurnRateName &&
                turnInPlace.children.Length == 3;
        }

        internal static bool HasRequiredRecoilLayer(
            AnimatorController controller)
        {
            AnimatorControllerLayer recoilLayer =
                FindLayer(controller, ActorAnimationParameters.RecoilLayerName);
            return recoilLayer != null &&
                recoilLayer.avatarMask != null &&
                recoilLayer.blendingMode == AnimatorLayerBlendingMode.Additive &&
                !recoilLayer.iKPass &&
                Mathf.Abs(recoilLayer.defaultWeight) <= 0.001f &&
                FindState(
                    recoilLayer.stateMachine,
                    ActorAnimationParameters.NoRecoilStateName) != null &&
                FindState(
                    recoilLayer.stateMachine,
                    ActorAnimationParameters.RifleRecoilStateName) != null &&
                FindState(
                    recoilLayer.stateMachine,
                    ActorAnimationParameters.LauncherRecoilStateName) != null;
        }

        internal static bool HasRequiredActionLayer(
            AnimatorController controller)
        {
            AnimatorControllerLayer actionLayer =
                FindLayer(controller, ActorAnimationParameters.ActionLayerName);
            return actionLayer != null &&
                actionLayer.avatarMask != null &&
                actionLayer.blendingMode == AnimatorLayerBlendingMode.Override &&
                !actionLayer.iKPass &&
                Mathf.Abs(actionLayer.defaultWeight) <= 0.001f &&
                HasActionLayerReleaseBehaviour(
                    FindState(
                        actionLayer.stateMachine,
                        ActorAnimationParameters.NoActionStateName)) &&
                FindState(
                    actionLayer.stateMachine,
                    ActorAnimationParameters.ThrowStateName) != null;
        }

        internal static bool HasRequiredTraversalLayer(
            AnimatorController controller)
        {
            AnimatorControllerLayer layer = FindLayer(
                controller,
                ActorAnimationParameters.TraversalLayerName);
            return layer != null &&
                layer.avatarMask == null &&
                layer.blendingMode == AnimatorLayerBlendingMode.Override &&
                !layer.iKPass &&
                Mathf.Abs(layer.defaultWeight) <= 0.001f &&
                HasActionLayerReleaseBehaviour(FindState(
                    layer.stateMachine,
                    ActorAnimationParameters.NoTraversalStateName)) &&
                FindState(
                    layer.stateMachine,
                    ActorAnimationParameters.JumpStateName) != null;
        }

        private static void ValidateParameters(AnimatorController controller)
        {
            ValidateParameter(
                controller,
                ActorAnimationParameters.MoveXName,
                AnimatorControllerParameterType.Float);
            ValidateParameter(
                controller,
                ActorAnimationParameters.MoveYName,
                AnimatorControllerParameterType.Float);
            ValidateParameter(
                controller,
                ActorAnimationParameters.SpeedName,
                AnimatorControllerParameterType.Float);
            ValidateParameter(
                controller,
                ActorAnimationParameters.GroundedName,
                AnimatorControllerParameterType.Bool);
            ValidateParameter(
                controller,
                ActorAnimationParameters.TurnRateName,
                AnimatorControllerParameterType.Float);
            ValidateParameter(
                controller,
                ActorAnimationParameters.StanceName,
                AnimatorControllerParameterType.Int);
            ValidateParameter(
                controller,
                ActorAnimationParameters.InteractName,
                AnimatorControllerParameterType.Trigger);
            ValidateParameter(
                controller,
                ActorAnimationParameters.WeaponPoseName,
                AnimatorControllerParameterType.Int);
        }

        private static void ValidateStandingLocomotion(BlendTree locomotion)
        {
            ChildMotion[] children = locomotion.children;
            if (children.Length != ClipDefinitions.Length)
            {
                throw new InvalidOperationException(
                    $"The locomotion blend tree requires "
                    + $"{ClipDefinitions.Length} motions.");
            }

            for (int index = 0; index < ClipDefinitions.Length; index++)
            {
                DefaultActorClipDefinition definition =
                    ClipDefinitions[index];
                AnimationClip expectedClip =
                    LoadAnimationClip(GetClipPath(definition));
                if (expectedClip == null ||
                    children[index].motion != expectedClip ||
                    Vector2.Distance(
                        children[index].position,
                        definition.BlendPosition) > 0.001f)
                {
                    throw new InvalidOperationException(
                        $"The {definition.DisplayName} locomotion entry is "
                        + "missing or misconfigured.");
                }
            }
        }

        private static void ValidateParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type)
        {
            foreach (AnimatorControllerParameter parameter in
                controller.parameters)
            {
                if (parameter.name == name && parameter.type == type)
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                $"Animator parameter '{name}' is missing or has the wrong type.");
        }

        private static AnimatorState FindState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                if (child.state != null && child.state.name == stateName)
                {
                    return child.state;
                }
            }

            return null;
        }

        private static void ValidateTurnInPlaceLayer(
            AnimatorController controller)
        {
            AnimatorControllerLayer turnLayer =
                FindLayer(controller, ActorAnimationParameters.TurnLayerName);
            AnimatorState turnState = turnLayer != null
                ? FindState(
                    turnLayer.stateMachine,
                    ActorAnimationParameters.TurnInPlaceStateName)
                : null;
            if (turnLayer == null || turnLayer.avatarMask == null ||
                !IsBodyRegionMask(
                    turnLayer.avatarMask,
                    BodyRegion.PelvisAndLegs) ||
                turnLayer.blendingMode != AnimatorLayerBlendingMode.Override ||
                turnLayer.iKPass ||
                Mathf.Abs(turnLayer.defaultWeight) > 0.001f ||
                turnState == null ||
                turnLayer.stateMachine.defaultState != turnState ||
                Mathf.Abs(
                    turnState.speed -
                    TurnPlaybackSpeed) > 0.001f ||
                !(turnState.motion is BlendTree turnInPlace) ||
                turnInPlace.name != TurnInPlaceBlendName ||
                turnInPlace.blendType != BlendTreeType.Simple1D ||
                turnInPlace.blendParameter !=
                    ActorAnimationParameters.TurnRateName)
            {
                throw new InvalidOperationException(
                    "Turn-in-place requires a disabled-by-default lower-body "
                    + "override layer.");
            }

            ChildMotion[] children = turnInPlace.children;
            AnimationClip idle =
                LoadAnimationClip(GetClipPath(ClipDefinitions[0]));
            AnimationClip turnLeft = LoadAnimationClip(TurnLeftPath);
            AnimationClip turnRight = LoadAnimationClip(TurnRightPath);
            if (idle == null || turnLeft == null || turnRight == null ||
                children.Length != 3 || children[0].motion != turnLeft ||
                Mathf.Abs(children[0].threshold + 1f) > 0.001f ||
                children[1].motion != idle ||
                Mathf.Abs(children[1].threshold) > 0.001f ||
                children[2].motion != turnRight ||
                Mathf.Abs(children[2].threshold - 1f) > 0.001f)
            {
                throw new InvalidOperationException(
                    "The lower-body turn tree requires synchronized left, "
                    + "idle, and right motions.");
            }
        }

        private static bool IsBodyRegionMask(
            AvatarMask mask,
            BodyRegion region)
        {
            GameObject sourceVisual =
                AssetDatabase.LoadAssetAtPath<GameObject>(SourceVisualPath);
            Animator animator = sourceVisual != null
                ? sourceVisual.GetComponentInChildren<Animator>(true)
                : null;
            return BodyRegionMaskBuilder.Matches(mask, animator, region);
        }

        private static void ValidateCrouchedLocomotion(AnimatorState state)
        {
            if (!(state.motion is BlendTree locomotion) ||
                locomotion.blendType != BlendTreeType.Simple1D ||
                locomotion.blendParameter !=
                    ActorAnimationParameters.SpeedName)
            {
                throw new InvalidOperationException(
                    "The crouched locomotion state requires a Speed 1D "
                    + "blend tree.");
            }

            ChildMotion[] children = locomotion.children;
            AnimationClip expectedIdle = LoadAnimationClip(CrouchedIdlePath);
            AnimationClip expectedWalk = LoadAnimationClip(CrouchedWalkPath);
            if (children.Length != 2 ||
                children[0].motion != expectedIdle ||
                Mathf.Abs(children[0].threshold) > 0.001f ||
                children[1].motion != expectedWalk ||
                Mathf.Abs(
                    children[1].threshold -
                    CrouchedWalkBlendSpeed) > 0.001f)
            {
                throw new InvalidOperationException(
                    "The crouched locomotion tree requires its idle and "
                    + "in-place walk clips.");
            }
        }

        private static void ValidateWeaponLayer(
            AnimatorController controller)
        {
            AnimatorControllerLayer weaponLayer =
                FindLayer(controller, ActorAnimationParameters.WeaponLayerName);
            if (weaponLayer == null || weaponLayer.avatarMask == null ||
                weaponLayer.blendingMode != AnimatorLayerBlendingMode.Override ||
                !weaponLayer.iKPass ||
                Mathf.Abs(weaponLayer.defaultWeight - 1f) > 0.001f)
            {
                throw new InvalidOperationException(
                    "The weapon layer requires its upper-body mask, IK pass, "
                    + "and full controller weight.");
            }

            AnimatorState rifleAim = FindState(
                weaponLayer.stateMachine,
                ActorAnimationParameters.RifleAimStateName);
            AnimatorState launcherAim = FindState(
                weaponLayer.stateMachine,
                ActorAnimationParameters.LauncherAimStateName);
            if (rifleAim == null || launcherAim == null ||
                !(rifleAim.motion is BlendTree rifleLocomotion) ||
                rifleLocomotion.blendType !=
                    BlendTreeType.FreeformCartesian2D ||
                rifleLocomotion.blendParameter !=
                    ActorAnimationParameters.MoveXName ||
                rifleLocomotion.blendParameterY !=
                    ActorAnimationParameters.MoveYName)
            {
                throw new InvalidOperationException(
                    "The weapon layer requires authored rifle and launcher "
                    + "pose states.");
            }

            ChildMotion[] children = rifleLocomotion.children;
            if (children.Length != RifleLocomotionDefinitions.Length)
            {
                throw new InvalidOperationException(
                    $"Rifle locomotion requires "
                    + $"{RifleLocomotionDefinitions.Length} clips.");
            }

            for (int index = 0;
                index < RifleLocomotionDefinitions.Length;
                index++)
            {
                DefaultActorClipDefinition definition =
                    RifleLocomotionDefinitions[index];
                AnimationClip expected =
                    LoadAnimationClip(GetRifleClipPath(definition));
                if (children[index].motion != expected ||
                    Vector2.Distance(
                        children[index].position,
                        definition.BlendPosition) > 0.001f)
                {
                    throw new InvalidOperationException(
                        $"Rifle locomotion entry '{definition.DisplayName}' "
                        + "is misconfigured.");
                }
            }

            if (launcherAim.motion != LoadAnimationClip(LauncherAimPath))
            {
                throw new InvalidOperationException(
                    "Weapon launcher motions do not match their "
                    + "authored clips.");
            }
        }

        private static void ValidateRecoilLayer(
            AnimatorController controller)
        {
            AnimatorControllerLayer recoilLayer =
                FindLayer(controller, ActorAnimationParameters.RecoilLayerName);
            AnimatorControllerLayer weaponLayer =
                FindLayer(controller, ActorAnimationParameters.WeaponLayerName);
            AnimatorState idle = recoilLayer != null
                ? FindState(
                    recoilLayer.stateMachine,
                    ActorAnimationParameters.NoRecoilStateName)
                : null;
            AnimatorState rifle = recoilLayer != null
                ? FindState(
                    recoilLayer.stateMachine,
                    ActorAnimationParameters.RifleRecoilStateName)
                : null;
            AnimatorState launcher = recoilLayer != null
                ? FindState(
                    recoilLayer.stateMachine,
                    ActorAnimationParameters.LauncherRecoilStateName)
                : null;
            if (recoilLayer == null || recoilLayer.avatarMask == null ||
                weaponLayer == null ||
                recoilLayer.avatarMask != weaponLayer.avatarMask ||
                recoilLayer.blendingMode != AnimatorLayerBlendingMode.Additive ||
                recoilLayer.iKPass ||
                Mathf.Abs(recoilLayer.defaultWeight) > 0.001f ||
                idle == null || rifle == null || launcher == null ||
                recoilLayer.stateMachine.defaultState != idle ||
                idle.motion != null ||
                rifle.motion != LoadAnimationClip(RifleFirePath) ||
                Mathf.Abs(
                    rifle.speed -
                    RifleRecoilPlaybackSpeed) >
                    0.001f ||
                launcher.motion != LoadAnimationClip(LauncherFirePath) ||
                Mathf.Abs(
                    launcher.speed -
                    LauncherRecoilPlaybackSpeed) >
                    0.001f ||
                !HasReturnTransition(rifle, idle) ||
                !HasReturnTransition(launcher, idle) ||
                !HasAdditiveReferencePose(RifleFirePath) ||
                !HasAdditiveReferencePose(LauncherFirePath))
            {
                throw new InvalidOperationException(
                    "Weapon recoil requires an additive upper-body layer, "
                    + "authored recoil clips, and an empty return state.");
            }
        }

        private static void ValidateActionLayer(
            AnimatorController controller)
        {
            AnimatorControllerLayer actionLayer =
                FindLayer(controller, ActorAnimationParameters.ActionLayerName);
            AnimatorControllerLayer weaponLayer =
                FindLayer(controller, ActorAnimationParameters.WeaponLayerName);
            AnimatorState idle = actionLayer != null
                ? FindState(
                    actionLayer.stateMachine,
                    ActorAnimationParameters.NoActionStateName)
                : null;
            AnimatorState throwing = actionLayer != null
                ? FindState(
                    actionLayer.stateMachine,
                    ActorAnimationParameters.ThrowStateName)
                : null;
            if (actionLayer == null || actionLayer.avatarMask == null ||
                weaponLayer == null ||
                actionLayer.avatarMask != weaponLayer.avatarMask ||
                actionLayer.blendingMode != AnimatorLayerBlendingMode.Override ||
                actionLayer.iKPass ||
                Mathf.Abs(actionLayer.defaultWeight) > 0.001f ||
                idle == null || throwing == null ||
                actionLayer.stateMachine.defaultState != idle ||
                idle.motion != null ||
                !HasActionLayerReleaseBehaviour(idle) ||
                throwing.motion != LoadAnimationClip(ThrowPath) ||
                !HasActionReturnTransition(throwing, idle))
            {
                throw new InvalidOperationException(
                    "Actor actions require an upper-body override layer, "
                    + "an authored throw motion and a self-releasing return.");
            }
        }

        private static void ValidateTraversalLayer(
            AnimatorController controller)
        {
            AnimatorControllerLayer layer = FindLayer(
                controller,
                ActorAnimationParameters.TraversalLayerName);
            AnimatorState idle = layer != null
                ? FindState(
                    layer.stateMachine,
                    ActorAnimationParameters.NoTraversalStateName)
                : null;
            AnimatorState jump = layer != null
                ? FindState(
                    layer.stateMachine,
                    ActorAnimationParameters.JumpStateName)
                : null;
            if (layer == null || layer.avatarMask != null ||
                layer.blendingMode != AnimatorLayerBlendingMode.Override ||
                layer.iKPass ||
                Mathf.Abs(layer.defaultWeight) > 0.001f ||
                idle == null || jump == null ||
                layer.stateMachine.defaultState != idle ||
                idle.motion != null ||
                !HasActionLayerReleaseBehaviour(idle) ||
                jump.motion != LoadAnimationClip(JumpPath) ||
                !HasActionReturnTransition(jump, idle))
            {
                throw new InvalidOperationException(
                    "Actor traversal requires a full-body override layer, "
                    + "an authored jump motion and a self-releasing return.");
            }
        }

        private static bool HasActionLayerReleaseBehaviour(
            AnimatorState state)
        {
            if (state == null)
            {
                return false;
            }

            foreach (StateMachineBehaviour behaviour in state.behaviours)
            {
                if (behaviour is ActorActionLayerReleaseBehaviour)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasActionReturnTransition(
            AnimatorState throwing,
            AnimatorState idle)
        {
            foreach (AnimatorStateTransition transition in throwing.transitions)
            {
                if (transition.destinationState == idle &&
                    transition.hasExitTime &&
                    transition.hasFixedDuration &&
                    Mathf.Abs(
                        transition.exitTime -
                        ActionExitNormalizedTime) <= 0.001f &&
                    Mathf.Abs(
                        transition.duration -
                        ActionReturnTransitionSeconds) <= 0.001f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasReturnTransition(
            AnimatorState recoil,
            AnimatorState idle)
        {
            foreach (AnimatorStateTransition transition in recoil.transitions)
            {
                if (transition.destinationState == idle &&
                    transition.hasExitTime &&
                    transition.hasFixedDuration &&
                    Mathf.Abs(
                        transition.exitTime -
                        RecoilExitNormalizedTime) <= 0.001f &&
                    Mathf.Abs(
                        transition.duration -
                        RecoilReturnTransitionSeconds) <= 0.001f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAdditiveReferencePose(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            {
                return false;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips == null || clips.Length == 0)
            {
                return false;
            }

            foreach (ModelImporterClipAnimation clip in clips)
            {
                if (!clip.hasAdditiveReferencePose ||
                    Mathf.Abs(
                        clip.additiveReferencePoseFrame
                        - clip.lastFrame) > 0.001f)
                {
                    return false;
                }
            }

            return true;
        }

        private static AnimatorControllerLayer FindLayer(
            AnimatorController controller,
            string layerName)
        {
            if (controller == null)
            {
                return null;
            }

            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name == layerName)
                {
                    return layer;
                }
            }

            return null;
        }

        private static void ValidateStanceTransition(
            AnimatorState source,
            AnimatorState destination,
            ActorStance expectedStance)
        {
            foreach (AnimatorStateTransition transition in source.transitions)
            {
                if (transition.destinationState != destination ||
                    transition.hasExitTime ||
                    !transition.hasFixedDuration ||
                    Mathf.Abs(
                        transition.duration -
                        StanceTransitionDuration) > 0.001f)
                {
                    continue;
                }

                foreach (AnimatorCondition condition in transition.conditions)
                {
                    if (condition.parameter ==
                            ActorAnimationParameters.StanceName &&
                        condition.mode == AnimatorConditionMode.Equals &&
                        Mathf.Abs(
                            condition.threshold -
                            (int)expectedStance) < 0.001f)
                    {
                        return;
                    }
                }
            }

            throw new InvalidOperationException(
                $"The transition from '{source.name}' to "
                + $"'{destination.name}' must respond immediately to stance "
                + $"'{expectedStance}'.");
        }
    }
}
