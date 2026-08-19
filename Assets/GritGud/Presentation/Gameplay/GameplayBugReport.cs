using System;
using System.Globalization;
using System.Text;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Presentation.Persistence;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal readonly struct GameplayBugReportRuntime
    {
        public GameplayBugReportRuntime(
            DateTime generatedAtUtc,
            string productName,
            string gameVersion,
            string unityVersion,
            string platform,
            string operatingSystem,
            string deviceModel,
            string graphicsDevice,
            int screenWidth,
            int screenHeight)
        {
            GeneratedAtUtc = generatedAtUtc.ToUniversalTime();
            ProductName = productName ?? string.Empty;
            GameVersion = gameVersion ?? string.Empty;
            UnityVersion = unityVersion ?? string.Empty;
            Platform = platform ?? string.Empty;
            OperatingSystem = operatingSystem ?? string.Empty;
            DeviceModel = deviceModel ?? string.Empty;
            GraphicsDevice = graphicsDevice ?? string.Empty;
            ScreenWidth = screenWidth;
            ScreenHeight = screenHeight;
        }

        public DateTime GeneratedAtUtc { get; }

        public string ProductName { get; }

        public string GameVersion { get; }

        public string UnityVersion { get; }

        public string Platform { get; }

        public string OperatingSystem { get; }

        public string DeviceModel { get; }

        public string GraphicsDevice { get; }

        public int ScreenWidth { get; }

        public int ScreenHeight { get; }

        public static GameplayBugReportRuntime Capture()
        {
            return new GameplayBugReportRuntime(
                DateTime.UtcNow,
                UnityEngine.Application.productName,
                UnityEngine.Application.version,
                UnityEngine.Application.unityVersion,
                UnityEngine.Application.platform.ToString(),
                SystemInfo.operatingSystem,
                SystemInfo.deviceModel,
                SystemInfo.graphicsDeviceName,
                Screen.width,
                Screen.height);
        }
    }

    internal readonly struct GameplayBugReportRouteState
    {
        public GameplayBugReportRouteState(
            bool bound,
            int planPointCount,
            float plannedCost,
            bool isPlaying,
            float committedCost,
            RoutePlanFailure lastPlanFailure,
            string statusMessage)
        {
            Bound = bound;
            PlanPointCount = planPointCount;
            PlannedCost = plannedCost;
            IsPlaying = isPlaying;
            CommittedCost = committedCost;
            LastPlanFailure = lastPlanFailure;
            StatusMessage = statusMessage ?? string.Empty;
        }

        public bool Bound { get; }

        public int PlanPointCount { get; }

        public float PlannedCost { get; }

        public bool IsPlaying { get; }

        public float CommittedCost { get; }

        public RoutePlanFailure LastPlanFailure { get; }

        public string StatusMessage { get; }

        public static GameplayBugReportRouteState Capture(
            TurnMovementController controller)
        {
            return controller == null
                ? new GameplayBugReportRouteState(
                    false,
                    0,
                    0f,
                    false,
                    0f,
                    RoutePlanFailure.None,
                    string.Empty)
                : new GameplayBugReportRouteState(
                    controller.Session != null,
                    controller.PlanPointCount,
                    controller.PlannedCost,
                    controller.IsPlaying,
                    controller.CommittedCost,
                    controller.LastPlanFailure,
                    controller.StatusMessage);
        }
    }

    internal static class GameplayBugReportFormatter
    {
        private const int FormatVersion = 2;

        public static string Format(
            GameplaySession session,
            GameplayGuidanceEntry guidance,
            GameplayBugReportRouteState route,
            GameplayBugReportRuntime runtime,
            GameplayPartyControlSnapshot? partyControl = null)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (guidance == null)
            {
                throw new ArgumentNullException(nameof(guidance));
            }

            var report = new StringBuilder(2048);
            report.AppendLine("GRIT GUD BUG REPORT");
            report.Append("Format version: ").AppendLine(
                FormatVersion.ToString(CultureInfo.InvariantCulture));
            report.Append("Generated UTC: ").AppendLine(
                runtime.GeneratedAtUtc.ToString(
                    "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                    CultureInfo.InvariantCulture));
            report.AppendLine();
            report.AppendLine("PLAYER NOTES (optional before sharing)");
            report.AppendLine("What I did:");
            report.AppendLine("What happened:");
            report.AppendLine("How often it repeats:");
            report.AppendLine();

            report.AppendLine("CURRENT EXPECTED BEHAVIOR");
            AppendField(report, "Guidance ID", guidance.Id);
            AppendField(report, "Title", guidance.Title);
            AppendField(report, "Expected", guidance.ExpectedBehavior);
            AppendField(report, "Why", guidance.Rationale);
            AppendField(report, "Tip", guidance.PlayerTip);
            report.AppendLine();

            report.AppendLine("AUTHORITATIVE SESSION");
            AppendField(report, "Scenario", session.Scenario.Id);
            AppendField(report, "Mode", session.Mode.ToString());
            AppendField(report, "Turn context", session.TurnContext.ToString());
            AppendField(report, "Encounter active", FormatBool(session.EncounterActive));
            AppendField(report, "Operation", session.Operation.ToString());
            AppendField(report, "Active actor", session.ActiveActorId ?? "<none>");
            AppendField(
                report,
                "Initiative",
                string.Join(" -> ", session.InitiativeOrder));
            if (session.Scenario.PlayerParty != null)
            {
                AppendField(
                    report,
                    "Player party",
                    string.Join(", ", session.Scenario.PlayerParty.ActorIds));
                AppendField(
                    report,
                    "Selected party actor",
                    partyControl?.SelectedActorId ?? "<none>");
                AppendField(
                    report,
                    "Command party actor",
                    partyControl?.CommandActorId ?? "<none>");
                AppendField(
                    report,
                    "Party defeated",
                    FormatBool(IsPartyDefeated(session)));
            }
            report.AppendLine("Actors:");
            foreach (string actorId in session.InitiativeOrder)
            {
                AppendActor(report, session.GetActor(actorId));
            }

            report.AppendLine("Objectives:");
            if (session.Scenario.Objectives.Count == 0)
            {
                report.AppendLine("  <none>");
            }
            else
            {
                foreach (ScenarioObjectiveDefinition objective in
                    session.Scenario.Objectives)
                {
                    GameplayObjectiveSnapshot state =
                        session.GetObjective(objective.Id);
                    report.Append("  ")
                        .Append(objective.Id)
                        .Append(" | position=")
                        .Append(FormatPosition(objective.Position))
                        .Append(" | radius=")
                        .Append(FormatFloat(objective.InteractionRadius))
                        .Append(" | interaction=")
                        .Append(state.Interaction.Id)
                        .Append(" | turn-cost=")
                        .Append(state.Interaction.TurnCost.ActionPoints.ToString(
                            CultureInfo.InvariantCulture))
                        .Append(" AP + ")
                        .Append(FormatFloat(
                            state.Interaction.TurnCost.MovementOpportunity))
                        .Append(" move")
                        .Append(" | complete=")
                        .AppendLine(FormatBool(state.IsCompleted));
                }
            }

            AppendCompletedCycle(report, session.LastCompletedVoluntaryTurnCycle);
            AppendEndedTurn(report, session.LastEndedTurn);
            AppendPendingRoute(report, session.PendingMovementRoute);
            AppendGameplayJournal(report, session.Journal.Entries);
            report.AppendLine();

            report.AppendLine("ROUTE / PRESENTATION STATE");
            AppendField(report, "Bound", FormatBool(route.Bound));
            AppendField(
                report,
                "Provisional points",
                route.PlanPointCount.ToString(CultureInfo.InvariantCulture));
            AppendField(report, "Provisional cost", FormatFloat(route.PlannedCost));
            AppendField(report, "Playback active", FormatBool(route.IsPlaying));
            AppendField(report, "Committed cost", FormatFloat(route.CommittedCost));
            AppendField(report, "Last plan failure", route.LastPlanFailure.ToString());
            AppendField(
                report,
                "Status",
                string.IsNullOrWhiteSpace(route.StatusMessage)
                    ? "<none>"
                    : OneLine(route.StatusMessage));
            report.AppendLine();

            report.AppendLine("RUNTIME");
            AppendField(report, "Product", runtime.ProductName);
            AppendField(report, "Game version", runtime.GameVersion);
            AppendField(report, "Unity version", runtime.UnityVersion);
            AppendField(report, "Platform", runtime.Platform);
            AppendField(report, "Operating system", runtime.OperatingSystem);
            AppendField(report, "Device", runtime.DeviceModel);
            AppendField(report, "Graphics", runtime.GraphicsDevice);
            AppendField(
                report,
                "Screen",
                runtime.ScreenWidth.ToString(CultureInfo.InvariantCulture)
                + "x"
                + runtime.ScreenHeight.ToString(CultureInfo.InvariantCulture));
            report.AppendLine();
            report.AppendLine("END REPORT");
            return report.ToString();
        }

        private static void AppendCompletedCycle(
            StringBuilder report,
            VoluntaryTurnCycleRecord cycle)
        {
            if (cycle == null)
            {
                AppendField(report, "Last voluntary cycle", "<none>");
                return;
            }

            AppendField(
                report,
                "Last voluntary cycle",
                cycle.Sequence.ToString(CultureInfo.InvariantCulture));
            report.AppendLine("Cycle actor state before replenishment:");
            foreach (GameplayActorSnapshot actor in cycle.Actors)
            {
                AppendActor(report, actor);
            }
        }

        private static void AppendPendingRoute(
            StringBuilder report,
            MovementRouteRecord route)
        {
            if (route == null)
            {
                AppendField(report, "Pending authoritative route", "<none>");
                return;
            }

            report.Append("Pending authoritative route: actor=")
                .Append(route.ActorId)
                .Append(" | points=")
                .Append(route.Points.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" | cost=")
                .Append(FormatFloat(route.TotalCost))
                .Append(" | destination=")
                .AppendLine(FormatPosition(route.Destination));
        }

        private static void AppendEndedTurn(
            StringBuilder report,
            TurnEndRecord turn)
        {
            if (turn == null)
            {
                AppendField(report, "Last ended turn", "<none>");
                return;
            }

            report.Append("Last ended turn: #")
                .Append(turn.Sequence.ToString(CultureInfo.InvariantCulture))
                .Append(" | actor=")
                .Append(turn.EndingActorId)
                .Append(" | next=")
                .Append(turn.NextActorId);
            AppendPersonalTurnStart(report, turn.PersonalTurnStart);
            report.AppendLine();
        }

        private static void AppendGameplayJournal(
            StringBuilder report,
            System.Collections.Generic.IReadOnlyList<GameplayJournalEntry> entries)
        {
            report.AppendLine("Gameplay journal:");
            if (entries.Count == 0)
            {
                report.AppendLine("  <none>");
                return;
            }

            foreach (GameplayJournalEntry entry in entries)
            {
                report.Append("  #")
                    .Append(entry.Sequence.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ");
                switch (entry)
                {
                    case TurnModeChangedJournalEntry turnMode:
                        report.Append("TurnModeChanged | ")
                            .Append(turnMode.PreviousMode)
                            .Append(" -> ")
                            .Append(turnMode.ResultingMode)
                            .Append(" | context=")
                            .Append(turnMode.Context)
                            .Append(" | active=")
                            .AppendLine(turnMode.ActiveActorId);
                        break;
                    case EncounterChangedJournalEntry encounter:
                        report.Append("EncounterChanged | active=")
                            .AppendLine(FormatBool(encounter.IsActive));
                        break;
                    case EnemyDecisionCommittedJournalEntry enemy:
                        EnemyTacticalDecisionRecord decision = enemy.Decision;
                        report.Append("EnemyDecisionCommitted | actor=")
                            .Append(decision.ActorId)
                            .Append(" | target=")
                            .Append(decision.TargetId)
                            .Append(" | kind=")
                            .Append(decision.Kind)
                            .Append(" | rationale=")
                            .Append(decision.Rationale);
                        if (decision.Exposure != null)
                        {
                            report.Append(" | exposure=")
                                .Append(decision.Exposure.VisibleSampleCount
                                    .ToString(CultureInfo.InvariantCulture))
                                .Append('/')
                                .Append(decision.Exposure.TotalSampleCount
                                    .ToString(CultureInfo.InvariantCulture));
                        }
                        if (decision.MovementRoute != null)
                        {
                            report.Append(" | route-cost=")
                                .Append(FormatFloat(
                                    decision.MovementRoute.TotalCost))
                                .Append(" | destination=")
                                .Append(FormatPosition(
                                    decision.MovementRoute.Destination));
                        }
                        report.AppendLine();
                        break;
                    case MovementBudgetSpentJournalEntry movementSpent:
                        report.Append("MovementBudgetSpent | actor=")
                            .Append(movementSpent.ActorId)
                            .Append(" | amount=")
                            .AppendLine(FormatFloat(movementSpent.Amount));
                        break;
                    case StanceChangedJournalEntry stance:
                        report.Append("StanceChanged | actor=")
                            .Append(stance.StanceChange.ActorId)
                            .Append(" | ")
                            .Append(stance.StanceChange.PreviousPose.Stance)
                            .Append(" -> ")
                            .AppendLine(stance.StanceChange.ResultingPose.Stance.ToString());
                        break;
                    case MovementRouteCommittedJournalEntry movementCommitted:
                        AppendMovementJournalLine(
                            report,
                            "MovementRouteCommitted",
                            movementCommitted.Route);
                        break;
                    case MovementRouteCompletedJournalEntry movementCompleted:
                        AppendMovementJournalLine(
                            report,
                            "MovementRouteCompleted",
                            movementCompleted.Route);
                        break;
                    case ActionResolvedJournalEntry action:
                        AppendActionJournalLine(report, action.Action);
                        break;
                    case DisplacementResolvedJournalEntry displacement:
                        report.Append("DisplacementResolved | actor=")
                            .Append(displacement.Displacement.Request.ActorId)
                            .Append(" | subject=")
                            .Append(displacement.Displacement.Request.SubjectId)
                            .Append(" | kind=")
                            .Append(displacement.Displacement.Request.SubjectKind)
                            .Append(" | actionId=")
                            .Append(displacement.Displacement.Request.ActionId)
                            .Append(" | intent=")
                            .Append(displacement.Displacement.Request.ActionKind)
                            .Append(" | succeeded=")
                            .AppendLine(FormatBool(displacement.Displacement.Succeeded));
                        break;
                    case DestructibleDamagedJournalEntry damage:
                        report.Append("DestructibleDamaged | prop=")
                            .Append(damage.Damage.PropId)
                            .Append(" | damage=")
                            .Append(damage.Damage.AppliedDamage.ToString(
                                CultureInfo.InvariantCulture))
                            .Append(" | state=")
                            .Append(damage.Damage.Previous.State)
                            .Append(" -> ")
                            .AppendLine(damage.Damage.Resulting.State.ToString());
                        break;
                    case VehicleMomentumResolvedJournalEntry vehicle:
                        report.Append("VehicleMomentumResolved | vehicle=")
                            .Append(vehicle.Momentum.Resulting.VehicleId)
                            .Append(" | speed=")
                            .Append(vehicle.Momentum.Previous.Speed.ToString(
                                CultureInfo.InvariantCulture))
                            .Append(" -> ")
                            .AppendLine(vehicle.Momentum.Resulting.Speed.ToString(
                                CultureInfo.InvariantCulture));
                        break;
                    case ProjectileAdvancedJournalEntry projectile:
                        ProjectileAdvanceRecord advance = projectile.Advance;
                        report.Append("ProjectileAdvanced | projectile=")
                            .Append(advance.ProjectileId)
                            .Append(" | turn-time=")
                            .Append(FormatFloat(advance.Previous.ElapsedTurnTime))
                            .Append(" -> ")
                            .Append(FormatFloat(advance.Resulting.ElapsedTurnTime))
                            .Append(" | world-revision=")
                            .Append(advance.WorldStateRevision.ToString(
                                CultureInfo.InvariantCulture))
                            .Append(" | status=")
                            .Append(advance.Resulting.Status);
                        if (advance.Resulting.Impact != null)
                        {
                            report.Append(" | hit=")
                                .Append(advance.Resulting.Impact.HitEntityId);
                        }

                        report.AppendLine();
                        break;
                    case EmergencyReactionChangedJournalEntry emergency:
                        EmergencyReactionWindowRecord window = emergency.Window;
                        report.Append("EmergencyReactionChanged | window-sequence=")
                            .Append(window.Sequence.ToString(
                                CultureInfo.InvariantCulture))
                            .Append(" | trigger=")
                            .Append(window.TriggerType)
                            .Append(':')
                            .Append(window.TriggerId)
                            .Append(" | initiator=")
                            .Append(window.InitiatorActorId)
                            .Append(" | responders=")
                            .Append(string.Join(" -> ", window.ResponderIds))
                            .Append(" | AP=")
                            .Append(window.ActionPointAllowance.ToString(
                                CultureInfo.InvariantCulture))
                            .Append(" | status=")
                            .AppendLine(window.Status.ToString());
                        break;
                    case TurnEndedJournalEntry turn:
                        report.Append("TurnEnded | actor=")
                            .Append(turn.Turn.EndingActorId)
                            .Append(" | next=")
                            .Append(turn.Turn.NextActorId);
                        AppendPersonalTurnStart(
                            report,
                            turn.Turn.PersonalTurnStart);
                        report.AppendLine();
                        break;
                    case VoluntaryTurnCycleCompletedJournalEntry cycle:
                        report.Append("VoluntaryTurnCycleCompleted | cycle=")
                            .AppendLine(cycle.Cycle.Sequence.ToString(
                                CultureInfo.InvariantCulture));
                        break;
                    default:
                        report.AppendLine(entry.Kind.ToString());
                        break;
                }
            }
        }

        private static void AppendPersonalTurnStart(
            StringBuilder report,
            PersonalTurnStartRecord start)
        {
            if (start == null) return;
            PersonalTurnActionPointGrant ap = start.ActionPoints;
            report.Append(" | AP grant=")
                .Append(ap.PreviousActionPoints.ToString(
                    CultureInfo.InvariantCulture))
                .Append(" + ")
                .Append(ap.GrantedActionPoints.ToString(
                    CultureInfo.InvariantCulture))
                .Append(" = ")
                .Append(ap.ResultingActionPoints.ToString(
                    CultureInfo.InvariantCulture))
                .Append(" | requested=")
                .Append(ap.RequestedIncome.ToString(
                    CultureInfo.InvariantCulture))
                .Append(" | cap-waste=")
                .Append(ap.CapWaste.ToString(CultureInfo.InvariantCulture))
                .Append(" | move=")
                .Append(FormatFloat(start.RefreshedMovement));
        }

        private static void AppendMovementJournalLine(
            StringBuilder report,
            string label,
            MovementRouteRecord route)
        {
            report.Append(label)
                .Append(" | actor=")
                .Append(route.ActorId)
                .Append(" | cost=")
                .Append(FormatFloat(route.TotalCost))
                .Append(" | destination=")
                .AppendLine(FormatPosition(route.Destination));
        }

        private static void AppendActionJournalLine(
            StringBuilder report,
            GameplayActionRecord action)
        {
            report.Append("ActionResolved | action-sequence=")
                .Append(action.Sequence.ToString(CultureInfo.InvariantCulture))
                .Append(" | actor=")
                .Append(action.Request.ActorId)
                .Append(" | action=")
                .Append(action.Request.ActionId)
                .Append(" | target=")
                .Append(action.Request.TargetId)
                .Append(" | AP=")
                .Append(action.Cost.ActionPoints.ToString(CultureInfo.InvariantCulture))
                .Append(" | move=")
                .Append(FormatFloat(action.Cost.MovementOpportunity))
                .Append(" | mobility=")
                .AppendLine(action.Cost.Mobility.ToString());
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                switch (outcome)
                {
                    case ObjectiveCompletedActionOutcome objective:
                        report.Append("    ObjectiveCompleted | objective=")
                            .Append(objective.ObjectiveId)
                            .AppendLine(" | incomplete -> complete");
                        break;
                    case AttackResolvedActionOutcome resolvedAttack:
                        AttackResolutionRecord attack = resolvedAttack.Attack;
                        report.Append("    AttackResolved | attack-sequence=")
                            .Append(attack.Sequence.ToString(
                                CultureInfo.InvariantCulture))
                            .Append(" | seed=")
                            .Append(attack.ResolutionSeed.ToString(
                                CultureInfo.InvariantCulture))
                            .Append(" | exposure=")
                            .Append(attack.Exposure.VisibleSampleCount.ToString(
                                CultureInfo.InvariantCulture))
                            .Append('/')
                            .Append(attack.Exposure.TotalSampleCount.ToString(
                                CultureInfo.InvariantCulture))
                            .Append(" | chance=")
                            .Append(attack.FinalHitChancePercent.ToString(
                                CultureInfo.InvariantCulture))
                            .Append(" | d100=")
                            .Append(attack.HitRoll.ToString(
                                CultureInfo.InvariantCulture))
                            .Append(" | outcome=")
                            .Append(attack.Hit ? "hit" : "miss");
                        if (attack.Hit)
                        {
                            report.Append(" | region-roll=d")
                                .Append(attack.Exposure.VisibleSampleCount.ToString(
                                    CultureInfo.InvariantCulture))
                                .Append('=')
                                .Append(attack.RegionRoll.ToString(
                                    CultureInfo.InvariantCulture))
                                .Append(" | region=")
                                .Append(attack.HitRegion)
                                .Append(" | wounds=")
                                .Append(attack.Wound.Previous.WoundCount.ToString(
                                    CultureInfo.InvariantCulture))
                                .Append(" -> ")
                                .Append(attack.Wound.Resulting.WoundCount.ToString(
                                    CultureInfo.InvariantCulture));
                        }

                        report.AppendLine();
                        break;
                    case WeaponDischargedActionOutcome dischargedWeapon:
                        WeaponDischargeRecord discharge =
                            dischargedWeapon.Discharge;
                        report.Append("    WeaponDischarged | discharge-sequence=")
                            .Append(discharge.Sequence.ToString(
                                CultureInfo.InvariantCulture))
                            .Append(" | origin=")
                            .Append(FormatPosition(discharge.Origin))
                            .Append(" | aim=")
                            .Append(FormatPosition(discharge.AimPoint))
                            .Append(" | distance=")
                            .Append(FormatFloat(discharge.Distance));
                        if (discharge.Impact != null)
                        {
                            report.Append(" | impact-surface=")
                                .Append(discharge.Impact.SurfaceId)
                                .Append(" | impact-revision=")
                                .Append(discharge.Impact.WorldStateRevision.ToString(
                                    CultureInfo.InvariantCulture));
                        }
                        if (discharge.Damage != null)
                        {
                            report.Append(" | prop-damage=")
                                .Append(FormatFloat(discharge.Damage.AppliedDamage))
                                .Append(" | prop-state=")
                                .Append(discharge.Damage.Resulting.State);
                        }
                        report.AppendLine();
                        break;
                    case ProjectileLaunchedActionOutcome launchedProjectile:
                        ProjectileLaunchRecord launch = launchedProjectile.Launch;
                        report.Append("    ProjectileLaunched | launch-sequence=")
                            .Append(launch.Sequence.ToString(
                                CultureInfo.InvariantCulture))
                            .Append(" | projectile=")
                            .Append(launch.ProjectileId)
                            .Append(" | type=")
                            .Append(launch.Definition.Id)
                            .Append(" | speed-per-turn=")
                            .Append(FormatFloat(launch.Definition.SpeedPerTurn))
                            .Append(" | radius=")
                            .Append(FormatFloat(launch.Definition.Radius))
                            .Append(" | maximum-range=")
                            .Append(FormatFloat(launch.Definition.MaximumRange))
                            .Append(" | origin=")
                            .Append(FormatPosition(launch.Origin))
                            .Append(" | aim=")
                            .Append(FormatPosition(launch.AimPoint))
                            .Append(" | emergency-reaction=")
                            .AppendLine(
                                launch.Definition.OpensEmergencyReactionWindow
                                    ? "authored-on"
                                    : "off");
                        break;
                    case ThrownExplosiveActionOutcome thrownExplosive:
                        ThrownExplosiveRecord thrown = thrownExplosive.Record;
                        report.Append("    ThrownExplosive | throw-sequence=")
                            .Append(thrown.Sequence.ToString(
                                CultureInfo.InvariantCulture))
                            .Append(" | item=")
                            .Append(thrown.Definition.Id)
                            .Append(" | intended=")
                            .Append(FormatPosition(thrown.IntendedLanding))
                            .Append(" | resolved=")
                            .Append(FormatPosition(thrown.ResolvedLanding))
                            .Append(" | world-revision=")
                            .Append(thrown.WorldStateRevision.ToString(
                                CultureInfo.InvariantCulture));
                        if (thrown.SmokeField != null)
                        {
                            SmokeFieldDefinition smoke =
                                thrown.SmokeField.Definition;
                            report.Append(" | smoke-field=")
                                .Append(thrown.SmokeField.Id)
                                .Append(" | smoke-radius=")
                                .Append(FormatFloat(smoke.Radius))
                                .Append(" | smoke-height=")
                                .Append(FormatFloat(smoke.Height))
                                .Append(" | smoke-exploration-seconds=")
                                .Append(FormatFloat(
                                    smoke.ExplorationDurationSeconds))
                                .Append(" | smoke-turn-ends=")
                                .Append(smoke.DurationTurnEnds.ToString(
                                    CultureInfo.InvariantCulture))
                                .Append(" | smoke-obscured-path=")
                                .Append(FormatFloat(
                                    smoke.MinimumObscuredPath));
                        }
                        else
                        {
                            report.Append(" | blast-radius=")
                                .Append(FormatFloat(
                                    thrown.Definition.BlastRadius));
                        }
                        report.AppendLine();
                        break;
                    case InventoryQuantityChangedActionOutcome inventory:
                        InventoryQuantityChangeRecord change = inventory.Change;
                        report.Append("    InventoryConsumed | item=")
                            .Append(change.ItemId)
                            .Append(" | quantity=")
                            .Append(change.PreviousQuantity.ToString(
                                CultureInfo.InvariantCulture))
                            .Append(" - ")
                            .Append(change.ConsumedQuantity.ToString(
                                CultureInfo.InvariantCulture))
                            .Append(" = ")
                            .AppendLine(change.ResultingQuantity.ToString(
                                CultureInfo.InvariantCulture));
                        break;
                    default:
                        report.Append("    ")
                            .Append(outcome.GetType().Name)
                            .Append(" | target=")
                            .AppendLine(outcome.TargetId);
                        break;
                }
            }
        }

        private static void AppendActor(
            StringBuilder report,
            GameplayActorSnapshot actor)
        {
            report.Append("  ")
                .Append(actor.ActorId)
                .Append(" | position=")
                .Append(FormatPosition(actor.Pose.Position))
                .Append(" | facing=")
                .Append(FormatFloat(actor.Pose.FacingDegrees))
                .Append(" | stance=")
                .Append(actor.Pose.Stance)
                .Append(" | AP=")
                .Append(actor.TurnBudget.ActionPoints.ToString(
                    CultureInfo.InvariantCulture))
                .Append(" | move=")
                .Append(FormatFloat(actor.TurnBudget.MovementOpportunity))
                .Append(" | wounds=")
                .Append(actor.Wounds.WoundCount.ToString(
                    CultureInfo.InvariantCulture))
                .Append('/')
                .Append(actor.MaximumWounds == int.MaxValue
                    ? "unbounded"
                    : actor.MaximumWounds.ToString(
                        CultureInfo.InvariantCulture))
                .Append(" | wound-regions=H:")
                .Append(actor.Wounds.HeadWounds.ToString(
                    CultureInfo.InvariantCulture))
                .Append(" T:")
                .Append(actor.Wounds.TorsoWounds.ToString(
                    CultureInfo.InvariantCulture))
                .Append(" LA:")
                .Append(actor.Wounds.LeftArmWounds.ToString(
                    CultureInfo.InvariantCulture))
                .Append(" RA:")
                .Append(actor.Wounds.RightArmWounds.ToString(
                    CultureInfo.InvariantCulture))
                .Append(" LL:")
                .Append(actor.Wounds.LeftLegWounds.ToString(
                    CultureInfo.InvariantCulture))
                .Append(" RL:")
                .Append(actor.Wounds.RightLegWounds.ToString(
                    CultureInfo.InvariantCulture))
                .Append(" U:")
                .Append(actor.Wounds.UnlocalizedWounds.ToString(
                    CultureInfo.InvariantCulture))
                .Append(" | incapacitated=")
                .Append(FormatBool(actor.IsIncapacitated))
                .Append(" | inventory=")
                .Append(FormatInventory(actor.Inventory))
                .Append(" | wound-move-penalty=")
                .AppendLine(FormatFloat(actor.Wounds.MovementPenalty));
        }

        private static bool IsPartyDefeated(GameplaySession session)
        {
            foreach (string actorId in session.Scenario.PlayerParty.ActorIds)
                if (!session.IsActorIncapacitated(actorId))
                    return false;
            return true;
        }

        private static string FormatInventory(ActorInventorySnapshot inventory)
        {
            if (inventory.Quantities.Count == 0)
            {
                return "<none>";
            }

            var values = new string[inventory.Quantities.Count];
            for (int index = 0; index < inventory.Quantities.Count; index++)
            {
                InventoryQuantitySnapshot quantity = inventory.Quantities[index];
                values[index] = quantity.ItemId + ":"
                    + quantity.Quantity.ToString(CultureInfo.InvariantCulture);
            }

            return string.Join(",", values);
        }

        private static void AppendField(
            StringBuilder report,
            string label,
            string value)
        {
            report.Append(label)
                .Append(": ")
                .AppendLine(string.IsNullOrEmpty(value) ? "<empty>" : value);
        }

        private static string FormatPosition(GameplayPosition position)
        {
            return "(" + FormatFloat(position.X)
                + ", " + FormatFloat(position.Y)
                + ", " + FormatFloat(position.Z) + ")";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatBool(bool value)
        {
            return value ? "yes" : "no";
        }

        private static string OneLine(string value)
        {
            return value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }
    }

    internal static class GameplayBugReportExporter
    {
        public static string Export(
            GameplaySession session,
            TurnMovementController turnMovement,
            GameplayGuidanceEntry guidance,
            string playerNote = null,
            GameplayPartyControlSnapshot? partyControl = null)
        {
            GameplayBugReportRuntime runtime =
                GameplayBugReportRuntime.Capture();
            string report = GameplayBugReportFormatter.Format(
                session,
                guidance,
                GameplayBugReportRouteState.Capture(turnMovement),
                runtime,
                partyControl);
            report = PrependPlayerNote(report, playerNote);
            string fileName = "grit-gud-bug-report-"
                + runtime.GeneratedAtUtc.ToString(
                    "yyyyMMdd-HHmmss'Z'",
                    CultureInfo.InvariantCulture)
                + ".txt";
            return TextFileTransfer.Export(
                fileName,
                report,
                "text/plain;charset=utf-8");
        }

        internal static string PrependPlayerNote(string report, string playerNote)
        {
            if (string.IsNullOrWhiteSpace(playerNote)) return report;
            return "PLAYER NOTE" + Environment.NewLine
                + "===========" + Environment.NewLine
                + playerNote.Trim() + Environment.NewLine
                + Environment.NewLine + report;
        }
    }
}
