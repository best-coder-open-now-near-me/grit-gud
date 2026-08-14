using System;
using GritGud.Presentation.Actors;
using UnityEngine;
using static GritGud.Editor.DefaultActorAssetRecipe;

namespace GritGud.Editor
{
    internal static class DefaultActorMotionProfileValidator
    {
        internal static void Validate(ActorMotionProfile profile)
        {
            if (profile == null ||
                Mathf.Abs(profile.WalkSpeed - WalkSpeed) > 0.001f ||
                Mathf.Abs(profile.SprintSpeed - SprintSpeed) > 0.001f ||
                Mathf.Abs(profile.CrouchedSpeed - CrouchedSpeed) > 0.001f ||
                Mathf.Abs(
                    profile.Acceleration - MovementAcceleration) > 0.001f ||
                Mathf.Abs(
                    profile.GravityMagnitude - GravityMagnitude) > 0.001f ||
                Mathf.Abs(
                    profile.GroundedDownwardSpeed -
                    GroundedDownwardSpeed) > 0.001f ||
                Mathf.Abs(
                    profile.TurnSharpness -
                    MovementTurnSharpness) > 0.001f ||
                Mathf.Abs(
                    profile.FallResetDistance - FallResetDistance) > 0.001f)
            {
                throw new InvalidOperationException(
                    "The default actor motion profile does not match the "
                    + "authored recipe.");
            }
        }
    }
}
