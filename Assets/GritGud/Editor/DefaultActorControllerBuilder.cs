using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Actors.Animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using static GritGud.Editor.DefaultActorAssetRecipe;

namespace GritGud.Editor
{
    internal static class DefaultActorControllerBuilder
    {
        internal static AnimatorController Build(
            IReadOnlyDictionary<DefaultActorClipDefinition, AnimationClip> clips,
            AnimationClip crouchedIdle,
            AnimationClip crouchedWalk,
            AnimationClip turnLeft,
            AnimationClip turnRight,
            AvatarMask lowerBodyMask,
            IReadOnlyDictionary<DefaultActorClipDefinition, AnimationClip>
                rifleLocomotion,
            AnimationClip rifleFire,
            AnimationClip launcherAim,
            AnimationClip launcherFire,
            AnimationClip throwClip,
            AnimationClip jumpClip,
            AnimationClip knifeIdleClip,
            AnimationClip knifeStrikeClip,
            AnimationClip pushClip,
            AnimationClip shoulderFallClip,
            AnimationClip fallOverClip,
            AvatarMask upperBodyMask)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(
                    ControllerPath);
            AddParameters(controller);

            AnimatorState locomotionState = controller.CreateBlendTreeInController(
                StandingStateName,
                out BlendTree locomotion,
                0);
            locomotion.blendType = BlendTreeType.FreeformCartesian2D;
            locomotion.blendParameter = ActorAnimationParameters.MoveXName;
            locomotion.blendParameterY = ActorAnimationParameters.MoveYName;
            locomotion.useAutomaticThresholds = false;
            foreach (DefaultActorClipDefinition definition in ClipDefinitions)
            {
                locomotion.AddChild(
                    clips[definition],
                    definition.BlendPosition);
            }

            locomotionState.writeDefaultValues = false;
            AnimatorState crouchedState = controller.CreateBlendTreeInController(
                CrouchedStateName,
                out BlendTree crouchedLocomotion,
                0);
            crouchedLocomotion.blendType = BlendTreeType.Simple1D;
            crouchedLocomotion.blendParameter =
                ActorAnimationParameters.SpeedName;
            crouchedLocomotion.useAutomaticThresholds = false;
            crouchedLocomotion.AddChild(crouchedIdle, 0f);
            crouchedLocomotion.AddChild(
                crouchedWalk,
                CrouchedWalkBlendSpeed);
            crouchedState.writeDefaultValues = false;

            ConfigureStanceTransition(
                locomotionState.AddTransition(crouchedState),
                ActorStance.Crouched);
            ConfigureStanceTransition(
                crouchedState.AddTransition(locomotionState),
                ActorStance.Standing);

            controller.layers[0].stateMachine.defaultState = locomotionState;
            AddTurnInPlaceLayer(
                controller,
                clips[ClipDefinitions[0]],
                turnLeft,
                turnRight,
                lowerBodyMask);
            AddWeaponLayer(
                controller,
                rifleLocomotion,
                launcherAim,
                knifeIdleClip,
                upperBodyMask);
            AddRecoilLayer(
                controller,
                rifleFire,
                launcherFire,
                upperBodyMask);
            AddActionLayer(
                controller,
                rifleFire,
                launcherFire,
                throwClip,
                knifeStrikeClip,
                upperBodyMask);
            AddTraversalLayer(controller, jumpClip);
            AddDisplacementLayer(controller, pushClip);
            AddReactionLayer(
                controller,
                shoulderFallClip,
                fallOverClip);
            EditorUtility.SetDirty(locomotion);
            EditorUtility.SetDirty(crouchedLocomotion);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddParameters(AnimatorController controller)
        {
            controller.AddParameter(
                ActorAnimationParameters.MoveXName,
                AnimatorControllerParameterType.Float);
            controller.AddParameter(
                ActorAnimationParameters.MoveYName,
                AnimatorControllerParameterType.Float);
            controller.AddParameter(
                ActorAnimationParameters.SpeedName,
                AnimatorControllerParameterType.Float);
            controller.AddParameter(
                ActorAnimationParameters.GroundedName,
                AnimatorControllerParameterType.Bool);
            controller.AddParameter(
                ActorAnimationParameters.TurnRateName,
                AnimatorControllerParameterType.Float);
            controller.AddParameter(
                ActorAnimationParameters.StanceName,
                AnimatorControllerParameterType.Int);
            controller.AddParameter(
                ActorAnimationParameters.InteractName,
                AnimatorControllerParameterType.Trigger);
            controller.AddParameter(
                ActorAnimationParameters.WeaponPoseName,
                AnimatorControllerParameterType.Int);
        }

        private static void AddTurnInPlaceLayer(
            AnimatorController controller,
            AnimationClip idle,
            AnimationClip turnLeft,
            AnimationClip turnRight,
            AvatarMask lowerBodyMask)
        {
            controller.AddLayer(ActorAnimationParameters.TurnLayerName);
            AnimatorControllerLayer[] layers = controller.layers;
            int layerIndex = layers.Length - 1;
            AnimatorControllerLayer layer = layers[layerIndex];
            layer.avatarMask = lowerBodyMask;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.defaultWeight = 0f;
            layer.iKPass = false;
            layers[layerIndex] = layer;
            controller.layers = layers;

            AnimatorStateMachine machine = layer.stateMachine;
            machine.name = ActorAnimationParameters.TurnLayerName;
            AnimatorState turnState = machine.AddState(
                ActorAnimationParameters.TurnInPlaceStateName,
                new Vector3(250f, 120f));
            turnState.writeDefaultValues = false;
            turnState.speed = TurnPlaybackSpeed;
            var turnInPlace = new BlendTree
            {
                name = TurnInPlaceBlendName,
                blendType = BlendTreeType.Simple1D,
                blendParameter = ActorAnimationParameters.TurnRateName,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(turnInPlace, controller);
            turnInPlace.AddChild(turnLeft, -1f);
            turnInPlace.AddChild(idle, 0f);
            turnInPlace.AddChild(turnRight, 1f);
            turnState.motion = turnInPlace;
            machine.defaultState = turnState;
            EditorUtility.SetDirty(turnInPlace);
            EditorUtility.SetDirty(machine);
        }

        private static void ConfigureStanceTransition(
            AnimatorStateTransition transition,
            ActorStance stance)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = StanceTransitionDuration;
            transition.AddCondition(
                AnimatorConditionMode.Equals,
                (int)stance,
                ActorAnimationParameters.StanceName);
        }

        private static void AddWeaponLayer(
            AnimatorController controller,
            IReadOnlyDictionary<DefaultActorClipDefinition, AnimationClip>
                rifleLocomotion,
            AnimationClip launcherAimClip,
            AnimationClip knifeIdleClip,
            AvatarMask upperBodyMask)
        {
            controller.AddLayer(ActorAnimationParameters.WeaponLayerName);
            AnimatorControllerLayer[] layers = controller.layers;
            int layerIndex = layers.Length - 1;
            AnimatorControllerLayer layer = layers[layerIndex];
            layer.avatarMask = upperBodyMask;
            layer.defaultWeight = 1f;
            layer.iKPass = true;
            layers[layerIndex] = layer;
            controller.layers = layers;

            AnimatorStateMachine machine = layer.stateMachine;
            machine.name = ActorAnimationParameters.WeaponLayerName;
            AnimatorState empty = machine.AddState(
                ActorAnimationParameters.EmptyHandsStateName,
                new Vector3(50f, 120f));
            empty.writeDefaultValues = false;

            AnimatorState rifleAim = machine.AddState(
                ActorAnimationParameters.RifleAimStateName,
                new Vector3(300f, 40f));
            rifleAim.writeDefaultValues = false;
            var rifleBlend = new BlendTree
            {
                name = "Rifle Locomotion",
                blendType = BlendTreeType.FreeformCartesian2D,
                blendParameter = ActorAnimationParameters.MoveXName,
                blendParameterY = ActorAnimationParameters.MoveYName,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(rifleBlend, controller);
            foreach (DefaultActorClipDefinition definition in
                RifleLocomotionDefinitions)
            {
                rifleBlend.AddChild(
                    rifleLocomotion[definition],
                    definition.BlendPosition);
            }

            rifleAim.motion = rifleBlend;
            AnimatorState launcherAim = machine.AddState(
                ActorAnimationParameters.LauncherAimStateName,
                new Vector3(300f, 210f));
            launcherAim.motion = launcherAimClip;
            launcherAim.writeDefaultValues = false;
            AnimatorState knifeIdle = machine.AddState(
                ActorAnimationParameters.KnifeIdleStateName,
                new Vector3(550f, 210f));
            knifeIdle.motion = knifeIdleClip;
            knifeIdle.writeDefaultValues = false;
            machine.defaultState = empty;

            AddWeaponPoseTransition(empty, rifleAim, RiflePoseValue);
            AddWeaponPoseTransition(empty, launcherAim, LauncherPoseValue);
            AddWeaponPoseTransition(empty, knifeIdle, MeleePoseValue);
            AddWeaponPoseTransition(rifleAim, empty, EmptyPoseValue);
            AddWeaponPoseTransition(
                rifleAim,
                launcherAim,
                LauncherPoseValue);
            AddWeaponPoseTransition(rifleAim, knifeIdle, MeleePoseValue);
            AddWeaponPoseTransition(launcherAim, empty, EmptyPoseValue);
            AddWeaponPoseTransition(
                launcherAim,
                rifleAim,
                RiflePoseValue);
            AddWeaponPoseTransition(launcherAim, knifeIdle, MeleePoseValue);
            AddWeaponPoseTransition(knifeIdle, empty, EmptyPoseValue);
            AddWeaponPoseTransition(knifeIdle, rifleAim, RiflePoseValue);
            AddWeaponPoseTransition(
                knifeIdle,
                launcherAim,
                LauncherPoseValue);
            EditorUtility.SetDirty(rifleBlend);
            EditorUtility.SetDirty(machine);
        }

        private static void AddRecoilLayer(
            AnimatorController controller,
            AnimationClip rifleRecoilClip,
            AnimationClip launcherRecoilClip,
            AvatarMask upperBodyMask)
        {
            controller.AddLayer(ActorAnimationParameters.RecoilLayerName);
            AnimatorControllerLayer[] layers = controller.layers;
            int layerIndex = layers.Length - 1;
            AnimatorControllerLayer layer = layers[layerIndex];
            layer.avatarMask = upperBodyMask;
            layer.blendingMode = AnimatorLayerBlendingMode.Additive;
            layer.defaultWeight = 0f;
            layer.iKPass = false;
            layers[layerIndex] = layer;
            controller.layers = layers;

            AnimatorStateMachine machine = layer.stateMachine;
            machine.name = ActorAnimationParameters.RecoilLayerName;
            AnimatorState idle = machine.AddState(
                ActorAnimationParameters.NoRecoilStateName,
                new Vector3(50f, 120f));
            idle.writeDefaultValues = false;
            AnimatorState rifle = machine.AddState(
                ActorAnimationParameters.RifleRecoilStateName,
                new Vector3(300f, 40f));
            rifle.motion = rifleRecoilClip;
            rifle.speed = RifleRecoilPlaybackSpeed;
            rifle.writeDefaultValues = false;
            AnimatorState launcher = machine.AddState(
                ActorAnimationParameters.LauncherRecoilStateName,
                new Vector3(300f, 210f));
            launcher.motion = launcherRecoilClip;
            launcher.speed = LauncherRecoilPlaybackSpeed;
            launcher.writeDefaultValues = false;
            AddRecoilReturnTransition(rifle, idle);
            AddRecoilReturnTransition(launcher, idle);
            machine.defaultState = idle;
            EditorUtility.SetDirty(machine);
        }

        private static void AddActionLayer(
            AnimatorController controller,
            AnimationClip rifleFireClip,
            AnimationClip launcherFireClip,
            AnimationClip throwClip,
            AnimationClip knifeStrikeClip,
            AvatarMask upperBodyMask)
        {
            controller.AddLayer(ActorAnimationParameters.ActionLayerName);
            AnimatorControllerLayer[] layers = controller.layers;
            int layerIndex = layers.Length - 1;
            AnimatorControllerLayer layer = layers[layerIndex];
            layer.avatarMask = upperBodyMask;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.defaultWeight = 0f;
            layer.iKPass = false;
            layers[layerIndex] = layer;
            controller.layers = layers;

            AnimatorStateMachine machine = layer.stateMachine;
            machine.name = ActorAnimationParameters.ActionLayerName;
            AnimatorState idle = machine.AddState(
                ActorAnimationParameters.NoActionStateName,
                new Vector3(50f, 120f));
            idle.writeDefaultValues = false;
            idle.AddStateMachineBehaviour<
                ActorActionLayerReleaseBehaviour>();
            AnimatorState rifleFire = machine.AddState(
                ActorAnimationParameters.RifleFireStateName,
                new Vector3(300f, 40f));
            rifleFire.motion = rifleFireClip;
            rifleFire.writeDefaultValues = false;
            AnimatorState launcherFire = machine.AddState(
                ActorAnimationParameters.LauncherFireStateName,
                new Vector3(300f, 120f));
            launcherFire.motion = launcherFireClip;
            launcherFire.writeDefaultValues = false;
            AnimatorState throwing = machine.AddState(
                ActorAnimationParameters.ThrowStateName,
                new Vector3(300f, 200f));
            throwing.motion = throwClip;
            throwing.writeDefaultValues = false;
            AnimatorState knifeStrike = machine.AddState(
                ActorAnimationParameters.KnifeStrikeStateName,
                new Vector3(300f, 320f));
            knifeStrike.motion = knifeStrikeClip;
            knifeStrike.speed = Mathf.Max(
                0.01f,
                knifeStrikeClip.length / ContactStrikeSeconds);
            knifeStrike.writeDefaultValues = false;

            AddActionReturnTransition(
                rifleFire,
                idle,
                ActionExitNormalizedTime);
            AddActionReturnTransition(
                launcherFire,
                idle,
                ActionExitNormalizedTime);
            AddActionReturnTransition(
                throwing,
                idle,
                ActionExitNormalizedTime);
            AddActionReturnTransition(
                knifeStrike,
                idle,
                ActionExitNormalizedTime);
            machine.defaultState = idle;
            EditorUtility.SetDirty(machine);
        }

        private static void AddWeaponPoseTransition(
            AnimatorState source,
            AnimatorState destination,
            int poseValue)
        {
            AnimatorStateTransition transition =
                source.AddTransition(destination);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0.12f;
            transition.AddCondition(
                AnimatorConditionMode.Equals,
                poseValue,
                ActorAnimationParameters.WeaponPoseName);
        }

        private static void AddTraversalLayer(
            AnimatorController controller,
            AnimationClip jumpClip)
        {
            controller.AddLayer(ActorAnimationParameters.TraversalLayerName);
            AnimatorControllerLayer[] layers = controller.layers;
            int layerIndex = layers.Length - 1;
            AnimatorControllerLayer layer = layers[layerIndex];
            layer.avatarMask = null;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.defaultWeight = 0f;
            layer.iKPass = false;
            layers[layerIndex] = layer;
            controller.layers = layers;

            AnimatorStateMachine machine = layer.stateMachine;
            machine.name = ActorAnimationParameters.TraversalLayerName;
            AnimatorState idle = machine.AddState(
                ActorAnimationParameters.NoTraversalStateName,
                new Vector3(50f, 120f));
            idle.writeDefaultValues = false;
            idle.AddStateMachineBehaviour<ActorActionLayerReleaseBehaviour>();
            AnimatorState jump = machine.AddState(
                ActorAnimationParameters.JumpStateName,
                new Vector3(300f, 120f));
            jump.motion = jumpClip;
            jump.writeDefaultValues = false;

            AnimatorStateTransition exit = jump.AddTransition(idle);
            exit.hasExitTime = true;
            exit.exitTime = ActionExitNormalizedTime;
            exit.hasFixedDuration = true;
            exit.duration = ActionReturnTransitionSeconds;
            machine.defaultState = idle;
            EditorUtility.SetDirty(machine);
        }

        private static void AddReactionLayer(
            AnimatorController controller,
            AnimationClip shoulderFallClip,
            AnimationClip fallOverClip)
        {
            controller.AddLayer(ActorAnimationParameters.ReactionLayerName);
            AnimatorControllerLayer[] layers = controller.layers;
            int layerIndex = layers.Length - 1;
            AnimatorControllerLayer layer = layers[layerIndex];
            layer.avatarMask = null;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.defaultWeight = 0f;
            layer.iKPass = false;
            layers[layerIndex] = layer;
            controller.layers = layers;

            AnimatorStateMachine machine = layer.stateMachine;
            machine.name = ActorAnimationParameters.ReactionLayerName;
            AnimatorState idle = machine.AddState(
                ActorAnimationParameters.NoReactionStateName,
                new Vector3(50f, 160f));
            idle.writeDefaultValues = false;
            idle.AddStateMachineBehaviour<ActorActionLayerReleaseBehaviour>();
            AnimatorState hit = machine.AddState(
                ActorAnimationParameters.HitReactionStateName,
                new Vector3(300f, 40f));
            hit.motion = shoulderFallClip;
            hit.writeDefaultValues = false;
            AddActionReturnTransition(
                hit,
                idle,
                HitReactionExitNormalizedTime);
            AnimatorState shoulderFall = machine.AddState(
                ActorAnimationParameters.ShoulderFallStateName,
                new Vector3(300f, 160f));
            shoulderFall.motion = shoulderFallClip;
            shoulderFall.writeDefaultValues = false;
            AnimatorState fallOver = machine.AddState(
                ActorAnimationParameters.FallOverStateName,
                new Vector3(300f, 280f));
            fallOver.motion = fallOverClip;
            fallOver.writeDefaultValues = false;
            machine.defaultState = idle;
            EditorUtility.SetDirty(machine);
        }

        private static void AddDisplacementLayer(
            AnimatorController controller,
            AnimationClip pushClip)
        {
            controller.AddLayer(
                ActorAnimationParameters.DisplacementLayerName);
            AnimatorControllerLayer[] layers = controller.layers;
            int layerIndex = layers.Length - 1;
            AnimatorControllerLayer layer = layers[layerIndex];
            layer.avatarMask = null;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.defaultWeight = 0f;
            layer.iKPass = false;
            layers[layerIndex] = layer;
            controller.layers = layers;

            AnimatorStateMachine machine = layer.stateMachine;
            machine.name = ActorAnimationParameters.DisplacementLayerName;
            AnimatorState idle = machine.AddState(
                ActorAnimationParameters.NoDisplacementStateName,
                new Vector3(50f, 120f));
            idle.writeDefaultValues = false;
            idle.AddStateMachineBehaviour<ActorActionLayerReleaseBehaviour>();
            AnimatorState push = machine.AddState(
                ActorAnimationParameters.PushStateName,
                new Vector3(300f, 120f));
            push.motion = pushClip;
            push.speed = Mathf.Max(0.01f, pushClip.length / PushSeconds);
            push.writeDefaultValues = false;
            AddActionReturnTransition(
                push,
                idle,
                ActionExitNormalizedTime);
            machine.defaultState = idle;
            EditorUtility.SetDirty(machine);
        }

        private static void AddActionReturnTransition(
            AnimatorState action,
            AnimatorState idle,
            float exitNormalizedTime)
        {
            AnimatorStateTransition exit = action.AddTransition(idle);
            exit.hasExitTime = true;
            exit.exitTime = exitNormalizedTime;
            exit.hasFixedDuration = true;
            exit.duration = ActionReturnTransitionSeconds;
        }

        private static void AddRecoilReturnTransition(
            AnimatorState recoil,
            AnimatorState idle)
        {
            AnimatorStateTransition transition = recoil.AddTransition(idle);
            transition.hasExitTime = true;
            transition.exitTime = RecoilExitNormalizedTime;
            transition.hasFixedDuration = true;
            transition.duration =
                RecoilReturnTransitionSeconds;
        }
    }
}
