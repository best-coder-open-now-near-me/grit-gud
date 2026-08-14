using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    /// <summary>Projectile adapter for the trigger-agnostic emergency-cycle session.</summary>
    public sealed class GameplayImpactCycleSession
    {
        private readonly GameplaySession gameplay;
        private readonly GameplayProjectileSession projectiles;
        private readonly GameplayEmergencyCycleSession emergencyCycle;
        private readonly HashSet<string> observedProjectileIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> projectedLaunchTurns =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public GameplayImpactCycleSession(
            GameplaySession gameplaySession,
            GameplayProjectileSession projectileSession)
            : this(
                gameplaySession,
                projectileSession,
                new GameplayEmergencyCycleSession(gameplaySession))
        {
        }

        public GameplayImpactCycleSession(
            GameplaySession gameplaySession,
            GameplayProjectileSession projectileSession,
            GameplayEmergencyCycleSession emergencyCycleSession)
        {
            gameplay = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            projectiles = projectileSession ?? throw new ArgumentNullException(nameof(projectileSession));
            emergencyCycle = emergencyCycleSession ?? throw new ArgumentNullException(
                nameof(emergencyCycleSession));
        }

        public EmergencyReactionWindowRecord CurrentWindow => emergencyCycle.CurrentWindow;
        public bool HasPendingOrActiveWindow => emergencyCycle.HasPendingOrActiveWindow;
        public event Action<EmergencyReactionWindowRecord> WindowChanged
        {
            add => emergencyCycle.WindowChanged += value;
            remove => emergencyCycle.WindowChanged -= value;
        }
        public event Action<ProjectileAdvanceRecord> ProjectileAdvanced;
        public event Action<ProjectileAdvancePrediction> ReactionPredicted;

        public bool ObserveLaunch(ProjectileLaunchRecord launch)
        {
            if (launch == null) throw new ArgumentNullException(nameof(launch));
            if (!observedProjectileIds.Add(launch.ProjectileId)) return false;
            projectedLaunchTurns.Add(
                launch.ProjectileId,
                launch.AttackerId);

            float preReactionTurnTime =
                (float)launch.RemainingActionPointsAfterLaunch
                / launch.TurnActionPointAllowance;
            if (preReactionTurnTime > 0f)
            {
                Advance(launch.ProjectileId, preReactionTurnTime);
            }

            ProjectileFlightSnapshot flight = projectiles.GetProjectile(
                launch.ProjectileId);
            if (flight.Status != ProjectileFlightStatus.InFlight)
            {
                return false;
            }
            if (!gameplay.EncounterActive
                || !launch.Definition.OpensEmergencyReactionWindow
                || emergencyCycle.HasPendingOrActiveWindow)
            {
                return false;
            }

            ProjectileAdvancePrediction prediction = projectiles.PredictAdvance(
                launch.ProjectileId,
                turnTime: 1f);
            if (!prediction.HasCollision)
            {
                return false;
            }

            int actionPointAllowance = Math.Max(
                1,
                Math.Min(
                    launch.TurnActionPointAllowance,
                    (int)Math.Ceiling(
                        (prediction.CollisionTurnTime
                            * launch.TurnActionPointAllowance)
                        - 0.0001f)));
            float sharedTurnTime =
                (float)actionPointAllowance
                / launch.TurnActionPointAllowance;
            bool opened = emergencyCycle.TryOpen(
                "projectile",
                launch.ProjectileId,
                launch.AttackerId,
                actionPointAllowance,
                new ProjectileResolution(
                    this,
                    launch.ProjectileId,
                    sharedTurnTime));
            if (opened)
            {
                ReactionPredicted?.Invoke(prediction);
            }

            return opened;
        }

        public bool TryEndTurn(string actorId, out TurnEndFailure failure) =>
            emergencyCycle.TryEndTurn(actorId, out failure);

        public bool ConsumeProjectedLaunchTurn(
            string projectileId,
            string endingActorId)
        {
            if (!projectedLaunchTurns.TryGetValue(
                    projectileId,
                    out string launchActorId)
                || !string.Equals(
                    launchActorId,
                    endingActorId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            projectedLaunchTurns.Remove(projectileId);
            return true;
        }

        private sealed class ProjectileResolution : IEmergencyCycleResolution
        {
            private readonly GameplayImpactCycleSession owner;
            private readonly string projectileId;
            private readonly float sharedTurnTime;

            public ProjectileResolution(
                GameplayImpactCycleSession owner,
                string projectileId,
                float reactionTurnTime)
            {
                this.owner = owner;
                this.projectileId = projectileId;
                sharedTurnTime = reactionTurnTime;
            }

            public bool IsResolved =>
                owner.projectiles.GetProjectile(projectileId).Status != ProjectileFlightStatus.InFlight;

            public void ResolveAfterResponsePass()
            {
                owner.Advance(projectileId, sharedTurnTime);
            }
        }

        private void Advance(string projectileId, float turnTime)
        {
            ProjectileAdvanceRecord advance = projectiles.Advance(
                projectileId,
                turnTime);
            ProjectileAdvanced?.Invoke(advance);
        }
    }
}
