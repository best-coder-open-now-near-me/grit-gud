using System;
using System.Collections.Generic;
using System.Globalization;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public static class AttackDiagnosticFormatter
    {
        public static string[] Format(GameplayActionRecord action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            AttackResolutionRecord attack = FindAttack(action);
            var lines = new List<string>
            {
                $"ATTACK #{attack.Sequence} - ACTION #{action.Sequence}",
                $"ACTOR - {attack.AttackerId} -> {attack.TargetId}",
                $"SEED - {attack.ResolutionSeed.ToString(CultureInfo.InvariantCulture)}",
                $"COST - AP {action.PreviousBudget.ActionPoints}"
                    + $" - {action.Cost.ActionPoints}"
                    + $" = {action.ResultingBudget.ActionPoints}"
                    + $" - MOVE {Format(action.PreviousBudget.MovementOpportunity)}"
                    + $" - {Format(action.Cost.MovementOpportunity)}"
                    + $" = {Format(action.ResultingBudget.MovementOpportunity)}",
                $"SILHOUETTE - {attack.Exposure.TotalSampleCount} painted cells"
                    + $" - {attack.Exposure.VisibleSampleCount} world-visible"
                    + $" - {attack.GeometricHitChancePercent}% geometric",
            };

            foreach (TargetRegionExposure region in attack.Exposure.Regions)
            {
                float rollShare = attack.Exposure.VisibleSampleCount == 0
                    ? 0f
                    : region.VisibleSampleCount
                        / (float)attack.Exposure.VisibleSampleCount;
                lines.Add(
                    $"REGION {region.Id.ToString().ToUpperInvariant()}"
                    + $" - {region.TotalSampleCount} painted"
                    + $" - {region.VisibleSampleCount} world-visible"
                    + $" - {FormatPercent(region.VisibleFraction)} exposed"
                    + $" - {FormatPercent(rollShare)} hit-location share");
            }

            AppendTacticalContext(lines, attack.Context);

            if (attack.IsContactAttack)
            {
                lines.Add(
                    $"CONTACT REACH - {Format(attack.Distance)} m"
                    + $" <= {Format(attack.MaximumReach.Value)} m"
                    + " - VALID");
                lines.Add(
                    $"HIT CHANCE - {attack.GeometricHitChancePercent}% geometric"
                    + " x 100% contact accuracy"
                    + FormatContextAccuracy(attack.Context)
                    + FormatCapabilityAccuracy(
                        attack.CapabilityAccuracyDeltaPercent)
                    + $" = {attack.FinalHitChancePercent}%");
            }
            else
            {
                lines.Add(
                    $"ACCURACY - {Format(attack.Distance)} m"
                    + $" - {Format(attack.AccuracyPercent)}%"
                    + $" - half-life {Format(attack.AccuracyDecay.HalfLifeDistance)} m"
                    + $" - floor {Format(attack.AccuracyDecay.MinimumAccuracyPercent)}%");
                lines.Add(
                    $"HIT CHANCE - {attack.GeometricHitChancePercent}% geometric"
                    + $" x {Format(attack.AccuracyPercent)}% accuracy"
                    + FormatContextAccuracy(attack.Context)
                    + FormatCapabilityAccuracy(
                        attack.CapabilityAccuracyDeltaPercent)
                    + $" = {attack.FinalHitChancePercent}%");
            }
            lines.Add(
                $"HIT ROLL - d100 = {attack.HitRoll}"
                + $" - {attack.HitRoll} <= {attack.FinalHitChancePercent}"
                + $" - {(attack.Hit ? "HIT" : "MISS")}");
            if (attack.Hit)
            {
                ActorWoundRecord wound = attack.Wound;
                ActorInjuryDelta injury = attack.Injury;
                lines.Add(
                    $"REGION ROLL - d{attack.Exposure.VisibleSampleCount}"
                    + $" = {attack.RegionRoll} - {attack.HitRegion}");
                lines.Add("IMPACT - " + injury.Impact.Mechanism
                    + " - severity " + injury.Impact.Severity
                    + " - source " + injury.Impact.SourceActorId
                    + " - weapon " + injury.Impact.WeaponId);
                lines.Add("INJURY - structural "
                    + injury.Injury.StructuralDamage + " - motor "
                    + injury.Injury.MotorLoss + " - sensory "
                    + injury.Injury.SensoryLoss + " - bleed "
                    + injury.Injury.BleedRate);
                lines.Add("SYSTEMIC - blood "
                    + injury.PreviousPhysiology.BloodReserve + " -> "
                    + injury.ResultingPhysiology.BloodReserve + " - shock "
                    + injury.PreviousPhysiology.Shock + " -> "
                    + injury.ResultingPhysiology.Shock
                    + " - consciousness "
                    + injury.PreviousPhysiology.Consciousness + " -> "
                    + injury.ResultingPhysiology.Consciousness);
                lines.Add("LIFE STATE - " + injury.PreviousLifeState
                    + " -> " + injury.ResultingLifeState);
                lines.Add(
                    $"COMPATIBILITY WOUND - count {wound.Previous.WoundCount}"
                    + $" + 1 = {wound.Resulting.WoundCount}");
                lines.Add($"OUTCOME - HIT - {attack.HitRegion} INJURY APPLIED");
            }
            else
            {
                lines.Add("REGION ROLL - NOT ROLLED ON MISS");
                lines.Add("OUTCOME - MISS - NO INJURY");
            }

            return lines.ToArray();
        }

        private static void AppendTacticalContext(
            ICollection<string> lines,
            IGameplayActionContext actionContext)
        {
            if (actionContext == null)
            {
                lines.Add("TACTICAL CONTEXT - NONE");
                return;
            }
            if (!(actionContext is ResolvedTacticalContext context))
                throw new InvalidOperationException(
                    "Attack diagnostics require resolved tactical context.");

            TacticalContextSnapshot snapshot = context.Snapshot;
            lines.Add(
                $"TACTICAL CONTEXT - REVISION {snapshot.StateRevision}"
                + $" - {snapshot.CapabilitySignature}"
                + $" - {snapshot.Subject.Kind}:{snapshot.Subject.Id}"
                + $" - DIGEST {context.CanonicalDigest}");
            lines.Add(
                $"TACTICAL EVIDENCE - AWARENESS {snapshot.TargetAwareness}"
                + $" - VISIBILITY {snapshot.Visibility}"
                + $" - RANGE {snapshot.RangeBand}"
                + $" - EXPOSURE {snapshot.ExposureBand}"
                + $" - ISOLATION {snapshot.IsolationBand}"
                + $" - STANCE {snapshot.AttackerStance}->{snapshot.TargetStance}"
                + $" - SUPPRESSED {snapshot.AttackerSuppressed}->{snapshot.TargetSuppressed}"
                + $" - DISPLACED {snapshot.TargetDisplaced}"
                + $" - ALLIES {snapshot.NearbyAttackerAllies}:{snapshot.NearbyTargetAllies}"
                + $" - AP {snapshot.AttackerActionPoints}:{snapshot.TargetActionPoints}"
                + $" - SOUND {Format(snapshot.SoundSignature)}");
            if (context.Modifiers.Count == 0)
            {
                lines.Add("TACTICAL MODIFIERS - NONE");
            }
            foreach (AppliedTacticalModifier modifier in context.Modifiers)
            {
                lines.Add(
                    $"TACTICAL RULE - {modifier.RuleId}"
                    + $" - ORDER {modifier.RuleOrder}"
                    + $" - ACCURACY {modifier.Consequences.AccuracyDeltaPercent:+#;-#;0}%"
                    + $" - OUTCOMES {string.Join(",", modifier.OutcomeFeatureIds)}");
            }
            lines.Add(
                "TACTICAL OUTCOMES - "
                + (context.OutcomeFeatureIds.Count == 0
                    ? "NONE"
                    : string.Join(",", context.OutcomeFeatureIds)));
        }

        private static string FormatContextAccuracy(
            IGameplayActionContext context) => context == null
                || context.AccuracyDeltaPercent == 0
                    ? string.Empty
                    : $" {context.AccuracyDeltaPercent:+#;-#}% context";

        private static string FormatCapabilityAccuracy(int delta) =>
            delta == 0
                ? string.Empty
                : $" {delta:+#;-#}% capability";

        public static string[] FormatDischarge(GameplayActionRecord action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            WeaponDischargeRecord discharge = FindDischarge(action);
            var lines = new List<string>
            {
                $"DISCHARGE #{discharge.Sequence} - ACTION #{action.Sequence}",
                $"ACTOR - {discharge.AttackerId} -> {discharge.TargetId}",
                $"COST - AP {action.PreviousBudget.ActionPoints}"
                    + $" - {action.Cost.ActionPoints}"
                    + $" = {action.ResultingBudget.ActionPoints}"
                    + $" - MOVE {Format(action.PreviousBudget.MovementOpportunity)}"
                    + $" - {Format(action.Cost.MovementOpportunity)}"
                    + $" = {Format(action.ResultingBudget.MovementOpportunity)}",
                $"ORIGIN - {Format(discharge.Origin.X)},"
                    + $" {Format(discharge.Origin.Y)},"
                    + $" {Format(discharge.Origin.Z)}",
                $"AIM - {Format(discharge.AimPoint.X)},"
                    + $" {Format(discharge.AimPoint.Y)},"
                    + $" {Format(discharge.AimPoint.Z)}",
                $"DISTANCE - {Format(discharge.Distance)} m",
            };
            if (discharge.Impact != null)
            {
                lines.Add($"IMPACT - {discharge.Impact.SurfaceId}"
                    + $" - REV {discharge.Impact.WorldStateRevision}");
            }
            if (discharge.Damage != null)
            {
                lines.Add($"PROP DAMAGE - {Format(discharge.Damage.AppliedDamage)}"
                    + $" - {Format(discharge.Damage.Previous.RemainingIntegrity)}"
                    + $" -> {Format(discharge.Damage.Resulting.RemainingIntegrity)}"
                    + $" - {discharge.Damage.Resulting.State.ToString().ToUpperInvariant()}");
            }
            lines.Add("OUTCOME - WORLD DISCHARGE - NO TARGET HIT ROLL");
            return lines.ToArray();
        }

        private static AttackResolutionRecord FindAttack(
            GameplayActionRecord action)
        {
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (outcome is AttackResolvedActionOutcome attack)
                {
                    return attack.Attack;
                }
            }

            throw new ArgumentException(
                "Combat diagnostics require an attack action.",
                nameof(action));
        }

        private static WeaponDischargeRecord FindDischarge(
            GameplayActionRecord action)
        {
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (outcome is WeaponDischargedActionOutcome discharge)
                {
                    return discharge.Discharge;
                }
            }

            throw new ArgumentException(
                "Discharge diagnostics require a weapon-discharge action.",
                nameof(action));
        }

        private static string Format(float value) =>
            value.ToString("0.##", CultureInfo.InvariantCulture);

        private static string FormatPercent(float fraction) =>
            (fraction * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}
