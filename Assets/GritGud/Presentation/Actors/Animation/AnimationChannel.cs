using System;
using System.Collections.Generic;

namespace GritGud.Presentation.Actors.Animation
{
    public enum AnimationChannelId
    {
        Locomotion = 0,
        TurnInPlace = 1,
        WeaponPose = 2,
        Recoil = 3,
        WeaponAim = 4,
        Actions = 5,
        Reactions = 6,
    }

    public enum AnimationMotionSource
    {
        AnimatorController = 0,
        PostAnimationSolver = 1,
    }

    public enum AnimationChannelBlendMode
    {
        Base = 0,
        Override = 1,
        Additive = 2,
    }

    public enum AnimationWeightPolicy
    {
        Full = 0,
        Signal = 1,
        Profile = 2,
        TargetAvailability = 3,
        ActionPulse = 4,
    }

    public enum AnimationExecutionStage
    {
        Animator = 0,
        PostAnimation = 1,
    }

    public readonly struct AnimationChannelDefinition
    {
        public AnimationChannelDefinition(
            AnimationChannelId id,
            AnimationMotionSource motionSource,
            BodyRegion bodyRegion,
            AnimationChannelBlendMode blendMode,
            int priority,
            AnimationWeightPolicy weightPolicy,
            AnimationExecutionStage executionStage,
            string animatorLayerName = null)
        {
            Id = id;
            MotionSource = motionSource;
            BodyRegion = bodyRegion;
            BlendMode = blendMode;
            Priority = priority;
            WeightPolicy = weightPolicy;
            ExecutionStage = executionStage;
            AnimatorLayerName = animatorLayerName?.Trim() ?? string.Empty;
            if (executionStage == AnimationExecutionStage.Animator &&
                blendMode != AnimationChannelBlendMode.Base &&
                string.IsNullOrWhiteSpace(AnimatorLayerName))
            {
                throw new ArgumentException(
                    $"Animator channel '{id}' requires a layer name.",
                    nameof(animatorLayerName));
            }
        }

        public AnimationChannelId Id { get; }

        public AnimationMotionSource MotionSource { get; }

        public BodyRegion BodyRegion { get; }

        public AnimationChannelBlendMode BlendMode { get; }

        public int Priority { get; }

        public AnimationWeightPolicy WeightPolicy { get; }

        public AnimationExecutionStage ExecutionStage { get; }

        public string AnimatorLayerName { get; }
    }

    public static class ActorAnimationChannelPlan
    {
        public static readonly AnimationChannelDefinition Locomotion = new(
            AnimationChannelId.Locomotion,
            AnimationMotionSource.AnimatorController,
            BodyRegion.WholeBody,
            AnimationChannelBlendMode.Base,
            priority: 0,
            AnimationWeightPolicy.Full,
            AnimationExecutionStage.Animator);

        public static readonly AnimationChannelDefinition TurnInPlace = new(
            AnimationChannelId.TurnInPlace,
            AnimationMotionSource.AnimatorController,
            BodyRegion.PelvisAndLegs,
            AnimationChannelBlendMode.Override,
            priority: 100,
            AnimationWeightPolicy.Signal,
            AnimationExecutionStage.Animator,
            ActorAnimationParameters.TurnLayerName);

        public static readonly AnimationChannelDefinition WeaponPose = new(
            AnimationChannelId.WeaponPose,
            AnimationMotionSource.AnimatorController,
            BodyRegion.TorsoAndArms,
            AnimationChannelBlendMode.Override,
            priority: 200,
            AnimationWeightPolicy.Profile,
            AnimationExecutionStage.Animator,
            ActorAnimationParameters.WeaponLayerName);

        public static readonly AnimationChannelDefinition Recoil = new(
            AnimationChannelId.Recoil,
            AnimationMotionSource.AnimatorController,
            BodyRegion.TorsoAndArms,
            AnimationChannelBlendMode.Additive,
            priority: 300,
            AnimationWeightPolicy.ActionPulse,
            AnimationExecutionStage.Animator,
            ActorAnimationParameters.RecoilLayerName);

        public static readonly AnimationChannelDefinition Actions = new(
            AnimationChannelId.Actions,
            AnimationMotionSource.AnimatorController,
            BodyRegion.TorsoAndArms,
            AnimationChannelBlendMode.Override,
            priority: 400,
            AnimationWeightPolicy.ActionPulse,
            AnimationExecutionStage.Animator,
            ActorAnimationParameters.ActionLayerName);

        public static readonly AnimationChannelDefinition Reactions = new(
            AnimationChannelId.Reactions,
            AnimationMotionSource.AnimatorController,
            BodyRegion.WholeBody,
            AnimationChannelBlendMode.Override,
            priority: 500,
            AnimationWeightPolicy.ActionPulse,
            AnimationExecutionStage.Animator,
            ActorAnimationParameters.ReactionLayerName);

        public static readonly AnimationChannelDefinition WeaponAim = new(
            AnimationChannelId.WeaponAim,
            AnimationMotionSource.PostAnimationSolver,
            BodyRegion.TorsoAndArms,
            AnimationChannelBlendMode.Override,
            priority: 600,
            AnimationWeightPolicy.TargetAvailability,
            AnimationExecutionStage.PostAnimation);

        private static readonly AnimationChannelDefinition[] OrderedChannels =
        {
            Locomotion,
            TurnInPlace,
            WeaponPose,
            Recoil,
            Actions,
            Reactions,
            WeaponAim,
        };

        public static IReadOnlyList<AnimationChannelDefinition> Channels =>
            OrderedChannels;

        public static AnimationChannelDefinition Get(AnimationChannelId id)
        {
            foreach (AnimationChannelDefinition channel in OrderedChannels)
            {
                if (channel.Id == id)
                {
                    return channel;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(id), id, null);
        }
    }
}
