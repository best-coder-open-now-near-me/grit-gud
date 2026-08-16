using UnityEngine;

namespace GritGud.Presentation.Actors.Animation
{
    public static class ActorAnimationParameters
    {
        public const string MoveXName = "MoveX";
        public const string MoveYName = "MoveY";
        public const string SpeedName = "Speed";
        public const string GroundedName = "Grounded";
        public const string TurnRateName = "TurnRate";
        public const string StanceName = "Stance";
        public const string InteractName = "Interact";
        public const string WeaponPoseName = "WeaponPose";
        public const string TurnLayerName = "Lower Body Turn";
        public const string TurnInPlaceStateName = "Turn In Place";
        public const string WeaponLayerName = "Weapon Upper Body";
        public const string RecoilLayerName = "Weapon Recoil";
        public const string ActionLayerName = "Actor Actions";
        public const string TraversalLayerName = "Actor Traversal";
        public const string EmptyHandsStateName = "Empty Hands";
        public const string RifleAimStateName = "Rifle Aim";
        public const string LauncherAimStateName = "Launcher Aim";
        public const string NoRecoilStateName = "No Recoil";
        public const string RifleRecoilStateName = "Rifle Recoil";
        public const string LauncherRecoilStateName = "Launcher Recoil";
        public const string NoActionStateName = "No Action";
        public const string ThrowStateName = "Throw";
        public const string NoTraversalStateName = "No Traversal";
        public const string JumpStateName = "Jump";

        public static readonly int MoveX = Animator.StringToHash(MoveXName);
        public static readonly int MoveY = Animator.StringToHash(MoveYName);
        public static readonly int Speed = Animator.StringToHash(SpeedName);
        public static readonly int Grounded = Animator.StringToHash(GroundedName);
        public static readonly int TurnRate = Animator.StringToHash(TurnRateName);
        public static readonly int Stance = Animator.StringToHash(StanceName);
        public static readonly int Interact = Animator.StringToHash(InteractName);
        public static readonly int WeaponPose = Animator.StringToHash(WeaponPoseName);
        public static readonly int TurnInPlaceState =
            Animator.StringToHash(TurnInPlaceStateName);
        public static readonly int EmptyHandsState =
            Animator.StringToHash(EmptyHandsStateName);
        public static readonly int RifleAimState =
            Animator.StringToHash(RifleAimStateName);
        public static readonly int LauncherAimState =
            Animator.StringToHash(LauncherAimStateName);
        public static readonly int NoRecoilState =
            Animator.StringToHash(NoRecoilStateName);
        public static readonly int RifleRecoilState =
            Animator.StringToHash(RifleRecoilStateName);
        public static readonly int LauncherRecoilState =
            Animator.StringToHash(LauncherRecoilStateName);
        public static readonly int NoActionState =
            Animator.StringToHash(NoActionStateName);
        public static readonly int ThrowState =
            Animator.StringToHash(ThrowStateName);
        public static readonly int JumpState =
            Animator.StringToHash(JumpStateName);
    }
}
