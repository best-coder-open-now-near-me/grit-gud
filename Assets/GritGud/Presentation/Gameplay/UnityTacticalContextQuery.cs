using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    public sealed class UnityTacticalContextQuery :
        IGameplayTacticalContextQuery
    {
        private readonly Func<string, string, ITargetExposureQuery> queryForPair;
        private readonly Func<long> currentWorldRevision;
        private readonly GameplayTacticalContextEvidencePolicy policy;

        public UnityTacticalContextQuery(
            Func<string, string, ITargetExposureQuery> exposureQueryForPair,
            Func<long> worldRevision,
            GameplayTacticalContextEvidencePolicy evidencePolicy = null)
        {
            queryForPair = exposureQueryForPair ?? throw new ArgumentNullException(
                nameof(exposureQueryForPair));
            currentWorldRevision = worldRevision ?? throw new ArgumentNullException(
                nameof(worldRevision));
            policy = evidencePolicy
                ?? new GameplayTacticalContextEvidencePolicy();
        }

        internal static UnityTacticalContextQuery CreateForWorld(
            GameplaySession session,
            GameplayWorldRegistry registry,
            ISightObscuranceQuery sightObscurance = null)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var queries = new Dictionary<string, UnityTargetExposureQuery>(
                StringComparer.Ordinal);
            return new UnityTacticalContextQuery(
                (observerId, targetId) =>
                {
                    string key = observerId + "\n" + targetId;
                    if (!queries.TryGetValue(key, out UnityTargetExposureQuery query))
                    {
                        GameplayActorView observer = registry.GetActor(observerId);
                        GameplayActorView target = registry.GetActor(targetId);
                        query = new UnityTargetExposureQuery(
                            observer.Transform,
                            target.Transform,
                            Physics.DefaultRaycastLayers,
                            () => session.Revision,
                            sightObscurance);
                        queries.Add(key, query);
                    }
                    return query;
                },
                () => session.Revision);
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
                    "The first live tactical-context slice supports actor subjects only.");
            long revision = currentWorldRevision();
            if (revision < 0L || revision != state.Session.Revision)
                throw new InvalidOperationException(
                    "Live tactical evidence revision does not match canonical state.");
            GameplayActorSnapshot attacker = state.Session.GetActor(
                request.AttackerId);
            GameplayActorSnapshot target = state.Session.GetActor(
                request.Subject.Id);
            TargetExposureSnapshot forward = Capture(attacker, target);
            TargetExposureSnapshot reverse = Capture(target, attacker);
            float distance = attacker.Pose.Position.DistanceTo(
                target.Pose.Position);
            return new TacticalContextSnapshot(
                attacker.ActorId,
                request.Subject,
                request.Profile.Signature,
                revision,
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

        private TargetExposureSnapshot Capture(
            GameplayActorSnapshot observer,
            GameplayActorSnapshot target)
        {
            ITargetExposureQuery query = queryForPair(
                observer.ActorId,
                target.ActorId) ?? throw new InvalidOperationException(
                    $"No live exposure query is installed for '{observer.ActorId}' -> '{target.ActorId}'.");
            return query.Capture(
                observer.ActorId,
                GetObserverOrigin(observer),
                target.ActorId,
                ActorTargetProfileCatalog.CreateWorldSamples(
                    target.Pose,
                    target.IsPinned));
        }

        private static GameplayPosition GetObserverOrigin(
            GameplayActorSnapshot actor)
        {
            foreach (TargetRegionSample sample in
                ActorTargetProfileCatalog.CreateWorldSamples(
                    actor.Pose,
                    actor.IsPinned))
            {
                if (sample.Id == TargetRegionId.Head) return sample.Center;
            }
            throw new InvalidOperationException(
                $"Actor '{actor.ActorId}' has no head target region.");
        }
    }
}
