using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayAttackController : MonoBehaviour
    {
        private GameplayAttackSession attacks;
        private TargetAcquisitionPresenter acquisition;
        private GameplayDialogueLog dialogue;
        private string actorId;
        private Func<GameplayActionRecord, bool> beginEncounter;

        public GameplaySession Session { get; private set; }

        public AttackResolutionFailure LastFailure { get; private set; }

        public GameplayActionRecord LastResolvedAction { get; private set; }

        public AttackResolutionRecord LastResolution { get; private set; }

        public WeaponDischargeRecord LastDischarge { get; private set; }

        public string StatusMessage { get; private set; } = string.Empty;

        public event Action<GameplayActionRecord> AttackResolved;

        public event Action<GameplayActionRecord> WeaponDischarged;

        internal void Bind(
            GameplaySession session,
            TargetAcquisitionPresenter targetAcquisition,
            GameplayDialogueLog dialogueLog,
            string authoritativeActorId,
            uint scenarioSeed,
            Func<GameplayActionRecord, bool> onEncounterStartRequested = null,
            DestructiblePropSession destructibleSession = null)
        {
            Unbind();
            Session = session ?? throw new ArgumentNullException(nameof(session));
            acquisition = targetAcquisition ??
                throw new ArgumentNullException(nameof(targetAcquisition));
            dialogue = dialogueLog ??
                throw new ArgumentNullException(nameof(dialogueLog));
            if (string.IsNullOrWhiteSpace(authoritativeActorId))
            {
                throw new ArgumentException(
                    "Attack-controller actor identifiers cannot be empty.",
                    nameof(authoritativeActorId));
            }

            beginEncounter = onEncounterStartRequested
                ?? Session.BeginEncounterFromAction;
            attacks = new GameplayAttackSession(
                Session,
                scenarioSeed,
                destructibleSession);
            LastFailure = AttackResolutionFailure.None;
            LastResolvedAction = null;
            LastResolution = null;
            LastDischarge = null;
            StatusMessage = string.Empty;
            enabled = true;
            SetActor(authoritativeActorId);
        }

        public void SetActor(string authoritativeActorId)
        {
            if (Session == null || attacks == null)
            {
                throw new InvalidOperationException(
                    "Bind gameplay attacks before changing actors.");
            }
            if (string.IsNullOrWhiteSpace(authoritativeActorId))
            {
                throw new ArgumentException(
                    "Attack-controller actor identifiers cannot be empty.",
                    nameof(authoritativeActorId));
            }

            Session.GetActor(authoritativeActorId);
            actorId = authoritativeActorId;
            LastFailure = AttackResolutionFailure.None;
            LastResolvedAction = null;
            LastResolution = null;
            LastDischarge = null;
            StatusMessage = string.Empty;
        }

        public void Unbind()
        {
            Session = null;
            attacks = null;
            acquisition = null;
            dialogue = null;
            actorId = null;
            beginEncounter = null;
            LastFailure = AttackResolutionFailure.None;
            LastResolvedAction = null;
            LastResolution = null;
            LastDischarge = null;
            StatusMessage = string.Empty;
            AttackResolved = null;
            WeaponDischarged = null;
            enabled = false;
        }

        public bool TryAttack()
        {
            TargetExposureSnapshot exposure = acquisition?.CurrentSnapshot;
            if (exposure != null)
            {
                return TryAttack(exposure);
            }

            if (Session?.GetEquippedAttack(actorId)?.Contact != null)
            {
                LastFailure = AttackResolutionFailure.TargetRequired;
                StatusMessage = DescribeFailure(LastFailure);
                return false;
            }

            if (acquisition == null
                || !acquisition.TryGetWeaponAim(
                    out GameplayWeaponAim aim))
            {
                LastFailure = AttackResolutionFailure.TargetNotFound;
                StatusMessage = DescribeFailure(LastFailure);
                return false;
            }

            var impact = new DirectFireImpactRecord(
                aim.TargetId,
                aim.SurfaceId,
                ToGameplayPosition(aim.Position),
                aim.Normal.x,
                aim.Normal.y,
                aim.Normal.z,
                aim.WorldStateRevision,
                aim.PreferredFractureChunkIndex);
            return TryDischarge(
                aim.TargetId,
                ToGameplayPosition(aim.Position),
                impact);
        }

        internal bool TryDischarge(GameplayPosition aimPoint) =>
            TryDischarge(GameplayTargetIds.WorldAimPoint, aimPoint);

        internal bool TryDischarge(
            string targetId,
            GameplayPosition aimPoint) =>
            TryDischarge(targetId, aimPoint, impact: null);

        internal bool TryDischarge(
            string targetId,
            GameplayPosition aimPoint,
            DirectFireImpactRecord impact)
        {
            if (attacks == null || actorId == null)
            {
                LastFailure = AttackResolutionFailure.AttackUnavailable;
                StatusMessage = DescribeFailure(LastFailure);
                return false;
            }

            AttackResolutionFailure readiness = attacks.EvaluateDischarge(
                actorId,
                targetId,
                aimPoint,
                impact);
            if (readiness != AttackResolutionFailure.None)
            {
                LastFailure = readiness;
                StatusMessage = DescribeFailure(readiness);
                return false;
            }

            if (!attacks.TryDischarge(
                    actorId,
                    targetId,
                    aimPoint,
                    impact,
                    out GameplayActionRecord action,
                    out AttackResolutionFailure failure))
            {
                LastFailure = failure;
                StatusMessage = DescribeFailure(failure);
                return false;
            }

            WeaponDischargeRecord discharge =
                ((WeaponDischargedActionOutcome)action.Outcomes[0]).Discharge;
            GameplayEncounterActionTransition.BeginAfterCommittedAction(
                Session,
                action,
                beginEncounter,
                "attack");
            LastFailure = AttackResolutionFailure.None;
            LastResolvedAction = action;
            LastResolution = null;
            LastDischarge = discharge;
            StatusMessage = discharge.Damage == null
                ? Session.GetEquippedAttack(actorId)?.DisplayName
                    ?? "Weapon fired."
                : $"Hit {discharge.Damage.PropId}: "
                    + $"{discharge.Damage.Resulting.State.ToString().ToLowerInvariant()}.";
            if (GameplayCombatDiagnosticFormatter.TryFormatAction(
                    action,
                    out GameplayDiagnosticProjection diagnostic))
            {
                dialogue.AppendCombatDiagnostic(diagnostic);
            }
            WeaponDischarged?.Invoke(action);
            return true;
        }

        internal bool TryAttack(TargetExposureSnapshot exposure)
        {
            if (attacks == null || actorId == null || exposure == null)
            {
                LastFailure = AttackResolutionFailure.TargetNotFound;
                StatusMessage = DescribeFailure(LastFailure);
                return false;
            }

            AttackResolutionFailure readiness = attacks.EvaluateResolve(
                actorId,
                exposure);
            if (readiness != AttackResolutionFailure.None)
            {
                LastFailure = readiness;
                StatusMessage = DescribeFailure(readiness);
                return false;
            }

            return TryResolveActorAttack(
                actorId,
                exposure,
                out _,
                out _);
        }

        internal bool TryResolveActorAttack(
            string attackingActorId,
            TargetExposureSnapshot exposure,
            out GameplayActionRecord action,
            out AttackResolutionFailure failure)
        {
            action = null;
            if (attacks == null
                || string.IsNullOrWhiteSpace(attackingActorId)
                || exposure == null)
            {
                failure = AttackResolutionFailure.TargetNotFound;
                if (string.Equals(
                        attackingActorId,
                        actorId,
                        StringComparison.Ordinal))
                {
                    LastFailure = failure;
                    StatusMessage = DescribeFailure(failure);
                }
                return false;
            }

            if (!attacks.TryResolve(
                    attackingActorId,
                    exposure,
                    out action,
                    out failure))
            {
                if (string.Equals(
                        attackingActorId,
                        actorId,
                        StringComparison.Ordinal))
                {
                    LastFailure = failure;
                    StatusMessage = DescribeFailure(failure);
                }
                return false;
            }

            AttackResolutionRecord resolution =
                ((AttackResolvedActionOutcome)action.Outcomes[0]).Attack;
            GameplayEncounterActionTransition.BeginAfterCommittedAction(
                Session,
                action,
                beginEncounter,
                "attack");
            LastFailure = AttackResolutionFailure.None;
            LastResolvedAction = action;
            LastResolution = resolution;
            LastDischarge = null;
            StatusMessage = resolution.Hit
                ? $"Hit {resolution.TargetId}: {resolution.HitRegion} wounded."
                : $"Missed {resolution.TargetId}.";
            if (GameplayCombatDiagnosticFormatter.TryFormatAction(
                    action,
                    out GameplayDiagnosticProjection diagnostic))
            {
                dialogue.AppendCombatDiagnostic(diagnostic);
            }
            AttackResolved?.Invoke(action);
            failure = AttackResolutionFailure.None;
            return true;
        }

        public void ClearStatus()
        {
            StatusMessage = string.Empty;
        }

        private static string DescribeFailure(AttackResolutionFailure failure)
        {
            switch (failure)
            {
                case AttackResolutionFailure.TurnModeRequired:
                    return "Enter turn mode before attacking.";
                case AttackResolutionFailure.ActorNotActive:
                    return "Only the active actor can attack.";
                case AttackResolutionFailure.ActorIncapacitated:
                    return "An incapacitated actor cannot attack.";
                case AttackResolutionFailure.ActorPinned:
                    return "Push off the pinning prop before attacking.";
                case AttackResolutionFailure.OperationInProgress:
                    return "Wait for the current movement to resolve.";
                case AttackResolutionFailure.AttackUnavailable:
                    return "No equipped attack is available.";
                case AttackResolutionFailure.TargetNotFound:
                    return "No valid pointer aim point is available.";
                case AttackResolutionFailure.TargetIncapacitated:
                    return "That target is already incapacitated.";
                case AttackResolutionFailure.ExposureMismatch:
                    return "The target exposure no longer matches the attacker.";
                case AttackResolutionFailure.TargetRequired:
                    return "This attack requires an actor target.";
                case AttackResolutionFailure.TargetOutOfReach:
                    return "That target is outside this attack's reach.";
                case AttackResolutionFailure.WorldStateChanged:
                    return "The aimed world state changed; aim again.";
                case AttackResolutionFailure.InsufficientActionPoints:
                    return "Not enough AP remains for this attack.";
                case AttackResolutionFailure.InsufficientMovementOpportunity:
                    return "Not enough movement remains for this attack.";
                case AttackResolutionFailure.None:
                    return string.Empty;
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private static GameplayPosition ToGameplayPosition(Vector3 position) =>
            new GameplayPosition(position.x, position.y, position.z);
    }
}
