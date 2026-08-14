using System;

namespace GritGud.Domain.Gameplay
{
    public enum DisplacementSubjectKind
    {
        Prop,
        Combatant,
    }

    public enum DisplacementActionKind
    {
        Throw,
        Push,
        Lift,
    }

    public readonly struct DisplacementRequest
    {
        public DisplacementRequest(
            string actorId,
            string actionId,
            string subjectId,
            DisplacementSubjectKind subjectKind,
            float subjectMass,
            GameplayPosition destination)
            : this(
                actorId,
                actionId,
                subjectId,
                subjectKind,
                subjectMass,
                destination,
                DisplacementActionKind.Throw)
        {
        }

        public DisplacementRequest(
            string actorId,
            string actionId,
            string subjectId,
            DisplacementSubjectKind subjectKind,
            float subjectMass,
            GameplayPosition destination,
            DisplacementActionKind actionKind)
            : this(
                actorId,
                actionId,
                subjectId,
                subjectKind,
                subjectMass,
                DisplacementSizeClass.Medium,
                destination,
                actionKind)
        {
        }

        public DisplacementRequest(
            string actorId,
            string actionId,
            string subjectId,
            DisplacementSubjectKind subjectKind,
            float subjectMass,
            DisplacementSizeClass subjectSize,
            GameplayPosition destination,
            DisplacementActionKind actionKind)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new ArgumentException(
                    "Displacement requests require an acting combatant.",
                    nameof(actorId));
            }

            if (string.IsNullOrWhiteSpace(actionId))
            {
                throw new ArgumentException(
                    "Displacement requests require an authored action.",
                    nameof(actionId));
            }

            if (string.IsNullOrWhiteSpace(subjectId))
            {
                throw new ArgumentException(
                    "Displacement requests require a subject.",
                    nameof(subjectId));
            }

            if (string.Equals(actorId, subjectId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A combatant cannot displace itself.",
                    nameof(subjectId));
            }

            if (!Enum.IsDefined(typeof(DisplacementSubjectKind), subjectKind))
            {
                throw new ArgumentOutOfRangeException(nameof(subjectKind));
            }

            if (!Enum.IsDefined(typeof(DisplacementActionKind), actionKind))
            {
                throw new ArgumentOutOfRangeException(nameof(actionKind));
            }

            if (float.IsNaN(subjectMass)
                || float.IsInfinity(subjectMass)
                || subjectMass <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(subjectMass));
            }

            if (!Enum.IsDefined(typeof(DisplacementSizeClass), subjectSize))
            {
                throw new ArgumentOutOfRangeException(nameof(subjectSize));
            }

            ActorId = actorId;
            ActionId = actionId;
            SubjectId = subjectId;
            SubjectKind = subjectKind;
            SubjectMass = subjectMass;
            SubjectSize = subjectSize;
            Destination = destination;
            ActionKind = actionKind;
        }

        public string ActorId { get; }

        public string ActionId { get; }

        public string SubjectId { get; }

        public DisplacementSubjectKind SubjectKind { get; }

        public float SubjectMass { get; }

        public DisplacementSizeClass SubjectSize { get; }

        public GameplayPosition Destination { get; }

        public DisplacementActionKind ActionKind { get; }
    }

    public readonly struct CloseQuartersControlProfile
    {
        public CloseQuartersControlProfile(
            int strengthRating,
            int skillRating,
            string talentId = "",
            int talentModifier = 0)
        {
            if (strengthRating < CoreAttributeSet.MinimumRating
                || strengthRating > CoreAttributeSet.MaximumRating)
            {
                throw new ArgumentOutOfRangeException(nameof(strengthRating));
            }

            if (skillRating < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(skillRating));
            }

            if (talentModifier != 0 && string.IsNullOrWhiteSpace(talentId))
            {
                throw new ArgumentException(
                    "A control modifier requires a recorded talent identifier.",
                    nameof(talentId));
            }

            StrengthRating = strengthRating;
            SkillRating = skillRating;
            TalentId = talentId ?? string.Empty;
            TalentModifier = talentModifier;
        }

        public int StrengthRating { get; }

        public int SkillRating { get; }

        public string TalentId { get; }

        public int TalentModifier { get; }
    }

    public sealed class CloseQuartersControlRecord
    {
        public CloseQuartersControlRecord(
            int attackerRoll,
            CloseQuartersControlProfile attacker,
            int defenderRoll,
            CloseQuartersControlProfile defender)
        {
            if (attackerRoll < 1 || attackerRoll > 20)
            {
                throw new ArgumentOutOfRangeException(nameof(attackerRoll));
            }

            if (defenderRoll < 1 || defenderRoll > 20)
            {
                throw new ArgumentOutOfRangeException(nameof(defenderRoll));
            }

            AttackerRoll = attackerRoll;
            Attacker = attacker;
            DefenderRoll = defenderRoll;
            Defender = defender;
        }

        public int AttackerRoll { get; }

        public CloseQuartersControlProfile Attacker { get; }

        public int DefenderRoll { get; }

        public CloseQuartersControlProfile Defender { get; }

        public int AttackerTotal =>
            AttackerRoll
            + Attacker.StrengthRating
            + Attacker.SkillRating
            + Attacker.TalentModifier;

        public int DefenderTotal =>
            DefenderRoll
            + Defender.StrengthRating
            + Defender.SkillRating
            + Defender.TalentModifier;

        public bool AttackerSucceeded => AttackerTotal > DefenderTotal;
    }

    public sealed class PropDisplacementState
    {
        public PropDisplacementState(
            GameplayPropPose pose,
            DestructiblePropPosture posture)
        {
            if (!Enum.IsDefined(typeof(DestructiblePropPosture), posture))
            {
                throw new ArgumentOutOfRangeException(nameof(posture));
            }

            Pose = pose;
            Posture = posture;
        }

        public GameplayPropPose Pose { get; }

        public DestructiblePropPosture Posture { get; }

        public bool HasSameState(PropDisplacementState other) =>
            other != null
            && Posture == other.Posture
            && Pose.HasSameState(other.Pose);
    }

    public sealed class DisplacementRecord
    {
        public DisplacementRecord(
            long sequence,
            DisplacementRequest request,
            GameplayPosition previousPosition,
            GameplayPosition resultingPosition,
            CloseQuartersControlRecord controlContest = null)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            if (request.SubjectKind == DisplacementSubjectKind.Combatant
                && controlContest == null)
            {
                throw new ArgumentException(
                    "Combatant displacement requires a recorded control contest.",
                    nameof(controlContest));
            }

            if (request.SubjectKind != DisplacementSubjectKind.Combatant)
            {
                throw new ArgumentException(
                    "Prop displacement requires recorded prop state.",
                    nameof(request));
            }

            bool moved = previousPosition.DistanceTo(resultingPosition) > 0f;
            bool succeeded = request.SubjectKind == DisplacementSubjectKind.Prop
                || controlContest.AttackerSucceeded;
            if (moved != succeeded)
            {
                throw new ArgumentException(
                    "Displacement movement must agree with its recorded outcome.",
                    nameof(resultingPosition));
            }

            if (succeeded
                && resultingPosition.DistanceTo(request.Destination) > 0f)
            {
                throw new ArgumentException(
                    "Successful displacement must resolve at the validated destination.",
                    nameof(resultingPosition));
            }

            Sequence = sequence;
            Request = request;
            PreviousPosition = previousPosition;
            ResultingPosition = resultingPosition;
            ControlContest = controlContest;
            AppliedResults = DisplacementResultPolicies.None;
        }

        public DisplacementRecord(
            long sequence,
            DisplacementRequest request,
            PropDisplacementState previousPropState,
            PropDisplacementState resultingPropState,
            DisplacementResultPolicies appliedResults =
                DisplacementResultPolicies.None)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            if (request.SubjectKind != DisplacementSubjectKind.Prop)
            {
                throw new ArgumentException(
                    "Only prop displacement records prop state.",
                    nameof(request));
            }

            PreviousPropState = previousPropState ??
                throw new ArgumentNullException(nameof(previousPropState));
            ResultingPropState = resultingPropState ??
                throw new ArgumentNullException(nameof(resultingPropState));
            DisplacementResultPolicies knownResults =
                DisplacementResultPolicies.Topple
                | DisplacementResultPolicies.Release
                | DisplacementResultPolicies.CollisionDamage;
            if ((appliedResults & ~knownResults) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(appliedResults));
            }

            bool toppled = previousPropState.Posture
                    != DestructiblePropPosture.Toppled
                && resultingPropState.Posture
                    == DestructiblePropPosture.Toppled;
            if (toppled != appliedResults.HasFlag(
                    DisplacementResultPolicies.Topple))
            {
                throw new ArgumentException(
                    "Recorded Topple policy must agree with prop posture.",
                    nameof(appliedResults));
            }

            bool changed = !previousPropState.HasSameState(resultingPropState);
            if (!changed)
            {
                throw new ArgumentException(
                    "Successful prop displacement must change pose or posture.",
                    nameof(resultingPropState));
            }

            if (resultingPropState.Pose.Position.DistanceTo(
                    request.Destination) > 0f)
            {
                throw new ArgumentException(
                    "Successful displacement must resolve at the validated destination.",
                    nameof(resultingPropState));
            }

            Sequence = sequence;
            Request = request;
            PreviousPosition = previousPropState.Pose.Position;
            ResultingPosition = resultingPropState.Pose.Position;
            AppliedResults = appliedResults;
        }

        public long Sequence { get; }

        public DisplacementRequest Request { get; }

        public GameplayPosition PreviousPosition { get; }

        public GameplayPosition ResultingPosition { get; }

        public CloseQuartersControlRecord ControlContest { get; }

        public PropDisplacementState PreviousPropState { get; }

        public PropDisplacementState ResultingPropState { get; }

        public DisplacementResultPolicies AppliedResults { get; }

        public bool Succeeded =>
            Request.SubjectKind == DisplacementSubjectKind.Prop
            || ControlContest.AttackerSucceeded;
    }

    public sealed class DisplacementActionOutcome : GameplayActionOutcome
    {
        public DisplacementActionOutcome(DisplacementRecord displacement)
            : base((displacement ?? throw new ArgumentNullException(
                nameof(displacement))).Request.SubjectId)
        {
            Displacement = displacement;
        }

        public DisplacementRecord Displacement { get; }
    }
}
