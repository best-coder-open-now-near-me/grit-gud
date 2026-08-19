using System;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed class GameplayTacticalContextRequest
    {
        public GameplayTacticalContextRequest(
            GameplayCapabilityProfile profile,
            string attackerId,
            GameplaySubjectReference subject,
            float soundSignature)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            AttackerId = GameplayContentIdentity.RequireText(
                attackerId,
                nameof(attackerId));
            if (string.IsNullOrWhiteSpace(subject.Id)
                || !Enum.IsDefined(
                    typeof(GameplaySemanticSubjectKind),
                    subject.Kind))
                throw new ArgumentException(
                    "Tactical context requests require a valid subject.",
                    nameof(subject));
            if (GameplayCapabilityProfiles.GetSubjectKind(profile)
                != subject.Kind)
                throw new ArgumentException(
                    "Tactical context subject kind does not match the capability profile.",
                    nameof(subject));
            GameplayNumericPolicy.RequireFinite(
                soundSignature,
                nameof(soundSignature));
            if (soundSignature < 0f || soundSignature > 1f)
                throw new ArgumentOutOfRangeException(nameof(soundSignature));
            Subject = subject;
            SoundSignature = soundSignature;
        }

        public GameplayCapabilityProfile Profile { get; }
        public string AttackerId { get; }
        public GameplaySubjectReference Subject { get; }
        public float SoundSignature { get; }
    }

    public interface IGameplayTacticalContextQuery
    {
        TacticalContextSnapshot Capture(
            GameplayCombatStateSnapshot state,
            GameplayTacticalContextRequest request);
    }

    public sealed class GameplayTacticalContextEvidencePolicy
    {
        public GameplayTacticalContextEvidencePolicy(
            float contactMaximumDistance = 2f,
            float closeMaximumDistance = 8f,
            float effectiveMaximumDistance = 20f,
            float longMaximumDistance = 40f,
            float protectedMaximumVisibleFraction = 0.74f)
        {
            RequireFinitePositive(
                contactMaximumDistance,
                nameof(contactMaximumDistance));
            RequireFinitePositive(
                closeMaximumDistance,
                nameof(closeMaximumDistance));
            RequireFinitePositive(
                effectiveMaximumDistance,
                nameof(effectiveMaximumDistance));
            RequireFinitePositive(
                longMaximumDistance,
                nameof(longMaximumDistance));
            if (!(contactMaximumDistance < closeMaximumDistance
                && closeMaximumDistance < effectiveMaximumDistance
                && effectiveMaximumDistance < longMaximumDistance))
                throw new ArgumentException(
                    "Tactical range thresholds must be strictly increasing.");
            GameplayNumericPolicy.RequireFinite(
                protectedMaximumVisibleFraction,
                nameof(protectedMaximumVisibleFraction));
            if (protectedMaximumVisibleFraction <= 0f
                || protectedMaximumVisibleFraction >= 1f)
                throw new ArgumentOutOfRangeException(
                    nameof(protectedMaximumVisibleFraction));
            ContactMaximumDistance = contactMaximumDistance;
            CloseMaximumDistance = closeMaximumDistance;
            EffectiveMaximumDistance = effectiveMaximumDistance;
            LongMaximumDistance = longMaximumDistance;
            ProtectedMaximumVisibleFraction =
                protectedMaximumVisibleFraction;
        }

        public float ContactMaximumDistance { get; }
        public float CloseMaximumDistance { get; }
        public float EffectiveMaximumDistance { get; }
        public float LongMaximumDistance { get; }
        public float ProtectedMaximumVisibleFraction { get; }

        public TacticalRangeBand ClassifyRange(float distance)
        {
            GameplayNumericPolicy.RequireFinite(distance, nameof(distance));
            if (distance < 0f)
                throw new ArgumentOutOfRangeException(nameof(distance));
            if (distance <= ContactMaximumDistance)
                return TacticalRangeBand.Contact;
            if (distance <= CloseMaximumDistance)
                return TacticalRangeBand.Close;
            if (distance <= EffectiveMaximumDistance)
                return TacticalRangeBand.Effective;
            if (distance <= LongMaximumDistance)
                return TacticalRangeBand.Long;
            return TacticalRangeBand.Extreme;
        }

        public TacticalExposureBand ClassifyExposure(
            TargetExposureSnapshot exposure)
        {
            if (exposure == null) return TacticalExposureBand.Unknown;
            if (exposure.VisibleSampleCount == 0)
                return TacticalExposureBand.Hidden;
            return exposure.VisibleFraction
                    <= ProtectedMaximumVisibleFraction
                ? TacticalExposureBand.Protected
                : TacticalExposureBand.Exposed;
        }

        private static void RequireFinitePositive(
            float value,
            string parameterName)
        {
            GameplayNumericPolicy.RequireFinite(value, parameterName);
            if (value <= 0f)
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    public static class GameplayTacticalContextEvidenceRules
    {
        public static TacticalVisibilityRelation ResolveVisibility(
            bool attackerSeesTarget,
            bool targetSeesAttacker)
        {
            if (attackerSeesTarget)
            {
                return targetSeesAttacker
                    ? TacticalVisibilityRelation.Mutual
                    : TacticalVisibilityRelation.AttackerOnly;
            }
            return targetSeesAttacker
                ? TacticalVisibilityRelation.TargetOnly
                : TacticalVisibilityRelation.Neither;
        }

        public static TacticalAwarenessBand ResolveTargetAwareness(
            GameplayEncounterStateSnapshot encounter,
            string targetActorId)
        {
            if (encounter == null)
                throw new ArgumentNullException(nameof(encounter));
            if (!encounter.TryGetAwareness(
                    targetActorId,
                    out EnemyAwarenessSnapshot awareness))
                return TacticalAwarenessBand.Unknown;
            switch (awareness.State)
            {
                case EncounterAwarenessState.Unaware:
                    return TacticalAwarenessBand.Unaware;
                case EncounterAwarenessState.Suspicious:
                    return TacticalAwarenessBand.Suspicious;
                case EncounterAwarenessState.Alert:
                    return TacticalAwarenessBand.Alert;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(awareness.State));
            }
        }
    }

    public sealed class GameplayHeadlessTacticalContextQuery :
        IGameplayTacticalContextQuery
    {
        private readonly GameplayHeadlessSpatialEvidence spatial;
        private readonly GameplayTacticalContextEvidencePolicy policy;

        public GameplayHeadlessTacticalContextQuery(
            GameplayHeadlessSpatialEvidence spatialEvidence,
            GameplayTacticalContextEvidencePolicy evidencePolicy = null)
        {
            spatial = spatialEvidence ?? throw new ArgumentNullException(
                nameof(spatialEvidence));
            policy = evidencePolicy
                ?? new GameplayTacticalContextEvidencePolicy();
        }

        public TacticalContextSnapshot Capture(
            GameplayCombatStateSnapshot state,
            GameplayTacticalContextRequest request)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (request.Subject.Kind != GameplaySemanticSubjectKind.Actor)
                throw new NotSupportedException(
                    "The first tactical-context evidence slice supports actor subjects only.");
            GameplayActorSnapshot attacker = state.Session.GetActor(
                request.AttackerId);
            GameplayActorSnapshot target = state.Session.GetActor(
                request.Subject.Id);
            TargetExposureSnapshot forward =
                GameplayHeadlessEncounterEvidence.CaptureSight(
                    state,
                    spatial,
                    attacker.ActorId,
                    target.ActorId);
            TargetExposureSnapshot reverse =
                GameplayHeadlessEncounterEvidence.CaptureSight(
                    state,
                    spatial,
                    target.ActorId,
                    attacker.ActorId);
            float distance = attacker.Pose.Position.DistanceTo(
                target.Pose.Position);
            GameplaySpatialEvidenceStamp stamp = spatial.Stamp(state);
            if (stamp.WorldRevision != state.Session.Revision)
                throw new InvalidOperationException(
                    "Headless spatial evidence revision does not match canonical state.");
            return new TacticalContextSnapshot(
                attacker.ActorId,
                request.Subject,
                request.Profile.Signature,
                stamp.WorldRevision,
                GameplayTacticalContextEvidenceRules.ResolveTargetAwareness(
                    state.Session.EncounterState,
                    target.ActorId),
                GameplayTacticalContextEvidenceRules.ResolveVisibility(
                    forward.VisibleSampleCount > 0,
                    reverse.VisibleSampleCount > 0),
                attacker.Pose.Stance,
                target.Pose.Stance,
                policy.ClassifyRange(distance),
                policy.ClassifyExposure(forward),
                TacticalIsolationBand.Unknown,
                nearbyAttackerAllies: 0,
                nearbyTargetAllies: 0,
                attackerSuppressed: false,
                targetSuppressed: false,
                targetDisplaced: target.IsPinned,
                request.SoundSignature,
                attacker.TurnBudget.ActionPoints,
                target.TurnBudget.ActionPoints);
        }
    }
}
