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

            if (attack.IsContactAttack)
            {
                lines.Add(
                    $"CONTACT REACH - {Format(attack.Distance)} m"
                    + $" <= {Format(attack.MaximumReach.Value)} m"
                    + " - VALID");
                lines.Add(
                    $"HIT CHANCE - {attack.GeometricHitChancePercent}% geometric"
                    + " x 100% contact accuracy"
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
                    + $" = {attack.FinalHitChancePercent}%");
            }
            lines.Add(
                $"HIT ROLL - d100 = {attack.HitRoll}"
                + $" - {attack.HitRoll} <= {attack.FinalHitChancePercent}"
                + $" - {(attack.Hit ? "HIT" : "MISS")}");
            if (attack.Hit)
            {
                ActorWoundRecord wound = attack.Wound;
                lines.Add(
                    $"REGION ROLL - d{attack.Exposure.VisibleSampleCount}"
                    + $" = {attack.RegionRoll} - {attack.HitRegion}");
                lines.Add(
                    $"WOUND - count {wound.Previous.WoundCount}"
                    + $" + 1 = {wound.Resulting.WoundCount}"
                    + $" - {wound.Region} count "
                    + $"{wound.Previous.GetWoundCount(wound.Region)}"
                    + $" + 1 = {wound.Resulting.GetWoundCount(wound.Region)}"
                    + $" - movement penalty {Format(wound.Previous.MovementPenalty)}"
                    + $" + {Format(wound.AppliedMovementPenalty)}"
                    + $" = {Format(wound.Resulting.MovementPenalty)}");
                lines.Add($"OUTCOME - HIT - {attack.HitRegion} WOUND APPLIED");
            }
            else
            {
                lines.Add("REGION ROLL - NOT ROLLED ON MISS");
                lines.Add("OUTCOME - MISS - NO WOUND");
            }

            return lines.ToArray();
        }

        public static string[] FormatDischarge(GameplayActionRecord action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            WeaponDischargeRecord discharge = FindDischarge(action);
            return new[]
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
                "OUTCOME - WORLD DISCHARGE - NO TARGET HIT ROLL",
            };
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
