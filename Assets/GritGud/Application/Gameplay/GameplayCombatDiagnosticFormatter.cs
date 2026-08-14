using System;
using System.Collections.Generic;
using System.Globalization;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public enum GameplayDiagnosticPolicy
    {
        Formatted,
        NonDiagnostic,
    }

    public sealed class GameplayDiagnosticProjection
    {
        public GameplayDiagnosticProjection(
            string title,
            IEnumerable<string> lines)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException(
                    "Diagnostic projections require titles.",
                    nameof(title));
            }

            Title = title;
            Lines = new List<string>(
                lines ?? throw new ArgumentNullException(nameof(lines)))
                .AsReadOnly();
        }

        public string Title { get; }

        public IReadOnlyList<string> Lines { get; }
    }

    public static class GameplayCombatDiagnosticFormatter
    {
        public static bool TryFormatAction(
            GameplayActionRecord action,
            out GameplayDiagnosticProjection projection)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var lines = new List<string>();
            string title = null;
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                switch (outcome)
                {
                    case AttackResolvedActionOutcome attack:
                        title = $"{attack.Attack.AttackerId} ATTACKS "
                            + attack.Attack.TargetId;
                        lines.AddRange(AttackDiagnosticFormatter.Format(action));
                        break;

                    case WeaponDischargedActionOutcome discharge:
                        title = discharge.Discharge.AttackerId + " FIRES";
                        lines.AddRange(
                            AttackDiagnosticFormatter.FormatDischarge(action));
                        break;

                    case ProjectileLaunchedActionOutcome launched:
                        title = launched.Launch.AttackerId + " LAUNCHES "
                            + launched.Launch.ProjectileId;
                        AppendProjectileLaunch(lines, action, launched.Launch);
                        break;

                    case ThrownExplosiveActionOutcome thrown:
                        title = thrown.Record.ThrowerId + " THROWS "
                            + thrown.Record.Definition.Id;
                        AppendThrownExplosive(lines, action, thrown.Record);
                        break;

                    case DisplacementActionOutcome displaced:
                        title = displaced.Displacement.Request.ActorId
                            + " DISPLACES "
                            + displaced.Displacement.Request.SubjectId;
                        AppendDisplacement(lines, action, displaced.Displacement);
                        break;

                    case InventoryQuantityChangedActionOutcome inventory:
                        AppendInventoryQuantity(lines, inventory.Change);
                        break;

                    case ObjectiveCompletedActionOutcome _:
                    case EquipmentChangedActionOutcome _:
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Undeclared action outcome '{outcome.GetType().Name}'.");
                }
            }

            if (title == null)
            {
                projection = null;
                return false;
            }

            projection = new GameplayDiagnosticProjection(title, lines);
            return true;
        }

        public static GameplayDiagnosticProjection FormatProjectileAdvance(
            ProjectileAdvanceRecord advance)
        {
            if (advance == null)
            {
                throw new ArgumentNullException(nameof(advance));
            }

            var lines = new List<string>
            {
                "DISTANCE - " + Format(advance.Previous.DistanceTraveled)
                    + " + "
                    + Format(advance.Previous.Position.DistanceTo(
                        advance.Resulting.Position))
                    + " = " + Format(advance.Resulting.DistanceTraveled)
                    + " m",
                "TURN TIME - " + Format(advance.Previous.ElapsedTurnTime)
                    + " -> " + Format(advance.Resulting.ElapsedTurnTime),
                advance.Resulting.Impact == null
                    ? "SEGMENT - CLEAR - WORLD REVISION "
                        + advance.WorldStateRevision
                    : "IMPACT - "
                        + advance.Resulting.Impact.HitEntityId
                        + " - FRACTION "
                        + Format(advance.CollisionFraction ?? 0f)
                        + " - WORLD REVISION "
                        + advance.WorldStateRevision,
            };
            if (advance.Resulting.Impact != null)
            {
                AppendBlastEffects(
                    lines,
                    advance.Resulting.Impact.BlastEffects,
                    advance.Resulting.Launch.Definition
                        .BlastWoundMovementPenalty,
                    advance.Resulting.Launch.Definition
                        .BlastIntegrityDamage);
            }

            return new GameplayDiagnosticProjection(
                advance.ProjectileId + " ADVANCES",
                lines);
        }

        public static GameplayDiagnosticProjection FormatReactionPrediction(
            ProjectileAdvancePrediction prediction,
            EmergencyReactionWindowRecord window)
        {
            if (prediction == null)
            {
                throw new ArgumentNullException(nameof(prediction));
            }

            if (window == null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            ProjectileLaunchRecord launch = prediction.Previous.Launch;
            return new GameplayDiagnosticProjection(
                prediction.ProjectileId + " REACTION WINDOW",
                new[]
                {
                    "PREDICTED COLLISION TIME - "
                        + Format(prediction.CollisionTurnTime) + " turn",
                    "REACTION AP - ceil("
                        + Format(prediction.CollisionTurnTime)
                        + " x " + launch.TurnActionPointAllowance
                        + ") = " + window.ActionPointAllowance,
                    "SHARED RESOLUTION INTERVAL - "
                        + window.ActionPointAllowance + " / "
                        + launch.TurnActionPointAllowance + " = "
                        + Format((float)window.ActionPointAllowance
                            / launch.TurnActionPointAllowance)
                        + " turn",
                    "PREDICTION WORLD REVISION - "
                        + prediction.WorldStateRevision
                        + " - COLLISION REQUERIED AFTER RESPONSES",
                });
        }

        public static GameplayDiagnosticProjection FormatEnemyDecision(
            EnemyTacticalDecisionRecord decision)
        {
            if (decision == null)
            {
                throw new ArgumentNullException(nameof(decision));
            }

            var lines = new List<string>
            {
                "DECISION - " + decision.Kind,
                "RATIONALE - " + decision.Rationale,
            };
            if (decision.Exposure != null)
            {
                lines.Add("LOS - " + decision.Exposure.VisibleSampleCount
                    + " / " + decision.Exposure.TotalSampleCount
                    + " samples visible");
            }

            if (decision.MovementRoute != null)
            {
                lines.Add("ROUTE - "
                    + Format(decision.MovementRoute.TotalCost)
                    + " m to " + decision.MovementRoute.Destination);
            }

            return new GameplayDiagnosticProjection(
                decision.ActorId + " TACTICAL DECISION",
                lines);
        }

        public static bool TryFormatJournalEntry(
            GameplayJournalEntry entry,
            out GameplayDiagnosticProjection projection)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            switch (entry)
            {
                case ActionResolvedJournalEntry action:
                    return TryFormatAction(action.Action, out projection);

                case DestructibleDamagedJournalEntry destructible:
                    projection = FormatDestructibleDamage(
                        destructible.Damage);
                    return true;

                case VehicleMomentumResolvedJournalEntry vehicle:
                    projection = FormatVehicleMomentum(vehicle.Momentum);
                    return true;

                case ProjectileAdvancedJournalEntry projectile:
                    projection = FormatProjectileAdvance(projectile.Advance);
                    return true;

                case EmergencyReactionChangedJournalEntry reaction:
                    projection = FormatReactionWindow(reaction.Window);
                    return true;

                case EnemyDecisionCommittedJournalEntry enemy:
                    projection = FormatEnemyDecision(enemy.Decision);
                    return true;

                case TurnModeChangedJournalEntry _:
                case EncounterChangedJournalEntry _:
                case MovementBudgetSpentJournalEntry _:
                case StanceChangedJournalEntry _:
                case MovementRouteCommittedJournalEntry _:
                case MovementRouteCompletedJournalEntry _:
                case DisplacementResolvedJournalEntry _:
                case TurnEndedJournalEntry _:
                case VoluntaryTurnCycleCompletedJournalEntry _:
                    projection = null;
                    return false;

                default:
                    throw new InvalidOperationException(
                        $"Journal entry '{entry.GetType().Name}' has no diagnostic policy.");
            }
        }

        public static GameplayDiagnosticPolicy GetActionOutcomePolicy(
            Type outcomeType)
        {
            if (outcomeType == typeof(AttackResolvedActionOutcome)
                || outcomeType == typeof(WeaponDischargedActionOutcome)
                || outcomeType == typeof(ProjectileLaunchedActionOutcome)
                || outcomeType == typeof(ThrownExplosiveActionOutcome)
                || outcomeType == typeof(InventoryQuantityChangedActionOutcome)
                || outcomeType == typeof(DisplacementActionOutcome))
            {
                return GameplayDiagnosticPolicy.Formatted;
            }

            if (outcomeType == typeof(ObjectiveCompletedActionOutcome)
                || outcomeType == typeof(EquipmentChangedActionOutcome))
            {
                return GameplayDiagnosticPolicy.NonDiagnostic;
            }

            throw new ArgumentException(
                $"Action outcome '{outcomeType?.Name}' has no diagnostic policy.",
                nameof(outcomeType));
        }

        public static GameplayDiagnosticPolicy GetJournalEntryPolicy(
            GameplayJournalEntryKind kind)
        {
            switch (kind)
            {
                case GameplayJournalEntryKind.ActionResolved:
                case GameplayJournalEntryKind.DestructibleDamaged:
                case GameplayJournalEntryKind.VehicleMomentumResolved:
                case GameplayJournalEntryKind.ProjectileAdvanced:
                case GameplayJournalEntryKind.EmergencyReactionChanged:
                case GameplayJournalEntryKind.EnemyDecisionCommitted:
                    return GameplayDiagnosticPolicy.Formatted;

                case GameplayJournalEntryKind.TurnModeChanged:
                case GameplayJournalEntryKind.EncounterChanged:
                case GameplayJournalEntryKind.MovementBudgetSpent:
                case GameplayJournalEntryKind.StanceChanged:
                case GameplayJournalEntryKind.MovementRouteCommitted:
                case GameplayJournalEntryKind.MovementRouteCompleted:
                case GameplayJournalEntryKind.DisplacementResolved:
                case GameplayJournalEntryKind.TurnEnded:
                case GameplayJournalEntryKind.VoluntaryTurnCycleCompleted:
                    return GameplayDiagnosticPolicy.NonDiagnostic;

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static GameplayDiagnosticProjection FormatDestructibleDamage(
            DestructibleDamageRecord damage) =>
            new GameplayDiagnosticProjection(
                damage.PropId + " TAKES DAMAGE",
                new[]
                {
                    "INTEGRITY - "
                        + Format(damage.Previous.RemainingIntegrity)
                        + " - " + Format(damage.AppliedDamage)
                        + " = "
                        + Format(damage.Resulting.RemainingIntegrity),
                    "STATE - " + damage.Previous.State
                        + " -> " + damage.Resulting.State,
                });

        private static GameplayDiagnosticProjection FormatVehicleMomentum(
            VehicleMomentumRecord momentum) =>
            new GameplayDiagnosticProjection(
                momentum.Previous.VehicleId + " RESOLVES MOMENTUM",
                new[]
                {
                    "SPEED - " + Format(momentum.Previous.Speed)
                        + " -> " + Format(momentum.Resulting.Speed),
                    "FACING - " + Format(momentum.Previous.ForwardDegrees)
                        + " -> " + Format(momentum.Resulting.ForwardDegrees)
                        + " degrees",
                    "POSITION - "
                        + FormatPosition(momentum.Previous.Position)
                        + " -> "
                        + FormatPosition(momentum.Resulting.Position),
                    "PATH - " + momentum.Path.Count + " recorded points",
                });

        private static GameplayDiagnosticProjection FormatReactionWindow(
            EmergencyReactionWindowRecord window) =>
            new GameplayDiagnosticProjection(
                window.TriggerId + " REACTION " + window.Status,
                new[]
                {
                    "TRIGGER - " + window.TriggerType
                        + " - " + window.TriggerId,
                    "INITIATOR - " + window.InitiatorActorId,
                    "RESPONDERS - " + string.Join(", ", window.ResponderIds),
                    "ALLOWANCE - " + window.ActionPointAllowance + " AP",
                });

        private static void AppendProjectileLaunch(
            ICollection<string> lines,
            GameplayActionRecord action,
            ProjectileLaunchRecord launch)
        {
            AppendCost(lines, action);
            lines.Add("ORIGIN - " + FormatPosition(launch.Origin));
            lines.Add("AIM - " + FormatPosition(launch.AimPoint));
            lines.Add("FLIGHT - " + Format(launch.Definition.SpeedPerTurn)
                + " m/turn - RADIUS " + Format(launch.Definition.Radius)
                + " m - MAX " + Format(launch.Definition.MaximumRange) + " m");
            lines.Add("EMERGENCY REACTION - "
                + (launch.Definition.OpensEmergencyReactionWindow
                    ? "AUTHORED ON"
                    : "OFF"));
        }

        private static void AppendThrownExplosive(
            ICollection<string> lines,
            GameplayActionRecord action,
            ThrownExplosiveRecord thrown)
        {
            AppendCost(lines, action);
            lines.Add("UNCERTAINTY - "
                + Format(thrown.UncertaintyRadius) + " m");
            lines.Add("LANDING ERROR - "
                + Format(thrown.IntendedLanding.DistanceTo(
                    thrown.SampledLanding)) + " m");
            lines.Add("INTENDED - " + FormatPosition(thrown.IntendedLanding));
            lines.Add("SAMPLED - " + FormatPosition(thrown.SampledLanding));
            lines.Add("RESOLVED - " + FormatPosition(thrown.ResolvedLanding));
            if (thrown.Definition.SmokeField != null)
            {
                SmokeFieldDefinition smoke = thrown.Definition.SmokeField;
                lines.Add("SMOKE VOLUME - RADIUS "
                    + Format(smoke.Radius) + " m - HEIGHT "
                    + Format(smoke.Height) + " m");
                lines.Add("SMOKE LIFETIME - "
                    + Format(smoke.ExplorationDurationSeconds)
                    + " s exploration / " + smoke.DurationTurnEnds
                    + " ended turns");
                lines.Add("SIGHT BLOCK - "
                    + Format(smoke.MinimumObscuredPath)
                    + " m traversed smoke");
            }
            else
            {
                lines.Add("BLAST RADIUS - "
                    + Format(thrown.Definition.BlastRadius) + " m");
            }
            AppendBlastEffects(
                lines,
                thrown.BlastEffects,
                thrown.Definition.BlastWoundMovementPenalty,
                thrown.Definition.BlastIntegrityDamage,
                thrown.Definition.SmokeField != null ? "SMOKE" : "BLAST");
        }

        private static void AppendInventoryQuantity(
            ICollection<string> lines,
            InventoryQuantityChangeRecord change)
        {
            lines.Add("INVENTORY - " + change.ItemId
                + " - " + change.PreviousQuantity
                + " - " + change.ConsumedQuantity
                + " = " + change.ResultingQuantity);
        }

        private static void AppendDisplacement(
            ICollection<string> lines,
            GameplayActionRecord action,
            DisplacementRecord displacement)
        {
            AppendCost(lines, action);
            lines.Add("ACTION - " + displacement.Request.ActionId
                + " - " + displacement.Request.ActionKind);
            lines.Add("SUBJECT - " + displacement.Request.SubjectId
                + " - " + displacement.Request.SubjectKind
                + " - MASS " + Format(displacement.Request.SubjectMass));
            lines.Add("POSITION - "
                + FormatPosition(displacement.PreviousPosition)
                + " -> " + FormatPosition(displacement.ResultingPosition));
            if (displacement.ControlContest != null)
            {
                lines.Add("CONTROL - ATTACKER "
                    + displacement.ControlContest.AttackerTotal
                    + " VS DEFENDER "
                    + displacement.ControlContest.DefenderTotal
                    + " - "
                    + (displacement.Succeeded ? "SUCCESS" : "RESISTED"));
            }

            if (displacement.Request.SubjectKind
                == DisplacementSubjectKind.Prop)
            {
                lines.Add("PROP POSTURE - "
                    + displacement.PreviousPropState.Posture
                    + " -> " + displacement.ResultingPropState.Posture
                    + " - RESULTS " + displacement.AppliedResults);
            }
        }

        private static void AppendBlastEffects(
            ICollection<string> lines,
            IReadOnlyList<BlastEffectRecord> effects,
            float woundPenalty,
            float integrityDamage,
            string effectLabel = "BLAST")
        {
            foreach (BlastEffectRecord effect in effects)
            {
                string location = effect.SubjectKind == BlastSubjectKind.Actor
                    ? effect.InjuryRegion.HasValue
                        ? effect.InjuryRegion.Value.ToString()
                        : effect.Exposure > 0f
                            ? "UNLOCALIZED"
                            : "NO INJURY"
                    : "N/A";
                lines.Add(effectLabel + " " + effect.EntityId
                    + " - " + effect.SubjectKind
                    + " - DISTANCE " + Format(effect.Distance) + " m"
                    + " - OCCLUSION " + Format(effect.OcclusionExposure)
                    + " x FALLOFF " + Format(effect.DistanceFalloff)
                    + " = EXPOSURE " + Format(effect.Exposure)
                    + " - REGION " + location);
                if (effect.Exposure > 0f
                    && (woundPenalty > 0f || integrityDamage > 0f))
                {
                    lines.Add(effect.SubjectKind == BlastSubjectKind.Actor
                        ? "ACTOR CONSEQUENCE - " + Format(woundPenalty)
                            + " x " + Format(effect.Exposure)
                            + " = " + Format(woundPenalty * effect.Exposure)
                            + " movement penalty"
                        : "PROP CONSEQUENCE - " + Format(integrityDamage)
                            + " x " + Format(effect.Exposure)
                            + " = " + Format(integrityDamage * effect.Exposure)
                            + " integrity damage");
                }
            }
        }

        private static void AppendCost(
            ICollection<string> lines,
            GameplayActionRecord action)
        {
            lines.Add("COST - AP " + action.PreviousBudget.ActionPoints
                + " - " + action.Cost.ActionPoints
                + " = " + action.ResultingBudget.ActionPoints
                + " - MOVE "
                + Format(action.PreviousBudget.MovementOpportunity)
                + " - " + Format(action.Cost.MovementOpportunity)
                + " = " + Format(action.ResultingBudget.MovementOpportunity));
        }

        private static string FormatPosition(GameplayPosition position) =>
            "(" + Format(position.X) + ", " + Format(position.Y)
                + ", " + Format(position.Z) + ")";

        private static string Format(float value) =>
            value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
