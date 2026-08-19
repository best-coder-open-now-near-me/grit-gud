using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    /// <summary>
    /// Policy-neutral facts produced by an authoritative action. Search and AI
    /// may value these facts, but this projection contains no preference weights.
    /// </summary>
    public sealed class GameplayTacticalOutcome
    {
        internal GameplayTacticalOutcome(
            GameplayActionRecord action,
            string capabilitySignature,
            IEnumerable<string> featureIds,
            int geometricHitChancePercent,
            int finalHitChancePercent,
            int contextualAccuracyDeltaPercent,
            bool hit,
            int woundsApplied,
            float soundSignature)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
            CapabilitySignature = capabilitySignature ?? string.Empty;
            FeatureIds = new List<string>(
                featureIds ?? Array.Empty<string>()).AsReadOnly();
            GeometricHitChancePercent = geometricHitChancePercent;
            FinalHitChancePercent = finalHitChancePercent;
            ContextualAccuracyDeltaPercent = contextualAccuracyDeltaPercent;
            Hit = hit;
            WoundsApplied = woundsApplied;
            SoundSignature = soundSignature;
        }

        public GameplayActionRecord Action { get; }
        public long ActionSequence => Action.Sequence;
        public string ActorId => Action.Request.ActorId;
        public string SubjectId => Action.Request.TargetId;
        public string CapabilitySignature { get; }
        public IReadOnlyList<string> FeatureIds { get; }
        public int GeometricHitChancePercent { get; }
        public int FinalHitChancePercent { get; }
        public int ContextualAccuracyDeltaPercent { get; }
        public bool Hit { get; }
        public int WoundsApplied { get; }
        public float SoundSignature { get; }
        public int ActionPointsSpent => Action.Cost.ActionPoints;
        public int ActionPointsRemaining => Action.ResultingBudget.ActionPoints;
    }

    public static class GameplayTacticalOutcomeProjector
    {
        public static GameplayTacticalOutcome Project(
            GameplayActionRecord action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (!(outcome is AttackResolvedActionOutcome resolved))
                    continue;
                AttackResolutionRecord attack = resolved.Attack;
                ResolvedTacticalContext context = action.Context
                    as ResolvedTacticalContext;
                return new GameplayTacticalOutcome(
                    action,
                    context?.CapabilitySignature ?? string.Empty,
                    context?.OutcomeFeatureIds ?? Array.Empty<string>(),
                    attack.GeometricHitChancePercent,
                    attack.FinalHitChancePercent,
                    context?.AccuracyDeltaPercent ?? 0,
                    attack.Hit,
                    attack.Wound == null ? 0 : 1,
                    context?.Snapshot.SoundSignature ?? 0f);
            }

            return new GameplayTacticalOutcome(
                action,
                action.Context?.CapabilitySignature ?? string.Empty,
                Array.Empty<string>(),
                geometricHitChancePercent: 0,
                finalHitChancePercent: 0,
                contextualAccuracyDeltaPercent:
                    action.Context?.AccuracyDeltaPercent ?? 0,
                hit: false,
                woundsApplied: 0,
                soundSignature: 0f);
        }
    }
}
