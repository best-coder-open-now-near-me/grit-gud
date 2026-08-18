using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;

namespace GritGud.Application.Gameplay
{
    [Flags]
    public enum GameplayCapabilitySupportStage
    {
        None = 0,
        CandidateConstruction = 1 << 0,
        LegalityAndEvidence = 1 << 1,
        PureStateReduction = 1 << 2,
        DomainEventProduction = 1 << 3,
        ReplayEncodingAndReduction = 1 << 4,
        HeadlessExecution = 1 << 5,
        LiveInstallation = 1 << 6,
        Complete = CandidateConstruction
            | LegalityAndEvidence
            | PureStateReduction
            | DomainEventProduction
            | ReplayEncodingAndReduction
            | HeadlessExecution
            | LiveInstallation,
    }

    public enum GameplayReachableInputKind
    {
        MovementControl,
        StanceControl,
        EquippedAttack,
        InventoryWeapon,
        InventoryConsumable,
        CharacterAbility,
        ContextualInteraction,
        EndTurnControl,
        EmergencyControl,
        EnemyDecision,
        SessionControl,
        SystemContinuation,
    }

    public sealed class GameplayReachableInput
    {
        public GameplayReachableInput(
            GameplayReachableInputKind kind,
            string sourceId,
            string actorId,
            GameplayCapabilityProfile profile,
            string subjectIdHint = null)
        {
            if (!Enum.IsDefined(typeof(GameplayReachableInputKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            Kind = kind;
            SourceId = GameplayContentIdentity.RequireText(
                sourceId,
                nameof(sourceId));
            ActorId = GameplayContentIdentity.RequireText(
                actorId,
                nameof(actorId));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            SubjectKind = GameplayCapabilityProfiles.GetSubjectKind(profile);
            SubjectIdHint = string.IsNullOrWhiteSpace(subjectIdHint)
                ? null
                : subjectIdHint.Trim();
        }

        public GameplayReachableInputKind Kind { get; }
        public string SourceId { get; }
        public string ActorId { get; }
        public GameplayCapabilityProfile Profile { get; }
        public GameplaySemanticSubjectKind SubjectKind { get; }
        public string SubjectIdHint { get; }
    }

    public sealed class GameplayCapabilityRegistration
    {
        private readonly Dictionary<GameplayCapabilitySupportStage, string>
            implementations = new Dictionary<
                GameplayCapabilitySupportStage,
                string>();

        internal GameplayCapabilityRegistration(
            GameplayCapabilityProfile profile)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public GameplayCapabilityProfile Profile { get; }

        public GameplayCapabilitySupportStage Stages { get; private set; }

        public IReadOnlyDictionary<GameplayCapabilitySupportStage, string>
            Implementations => implementations;

        internal void Register(
            GameplayCapabilitySupportStage stage,
            string implementationId)
        {
            if (!IsSingleStage(stage))
                throw new ArgumentException(
                    "Capability stages must be registered individually.",
                    nameof(stage));
            string id = GameplayContentIdentity.RequireText(
                implementationId,
                nameof(implementationId));
            if (implementations.ContainsKey(stage))
                throw new InvalidOperationException(
                    $"Capability '{Profile.Signature}' already registered stage '{stage}'.");
            implementations.Add(stage, id);
            Stages |= stage;
        }

        private static bool IsSingleStage(GameplayCapabilitySupportStage stage)
        {
            int value = (int)stage;
            return value > 0 && (value & (value - 1)) == 0;
        }
    }

    public sealed class GameplayCapabilityRegistry
    {
        private readonly Dictionary<string, GameplayCapabilityRegistration>
            registrations = new Dictionary<
                string,
                GameplayCapabilityRegistration>(StringComparer.Ordinal);
        private readonly GameplayTransitionReducerRegistry reducers;

        public GameplayCapabilityRegistry(
            GameplayTransitionReducerRegistry reducerRegistry)
        {
            reducers = reducerRegistry ?? throw new ArgumentNullException(
                nameof(reducerRegistry));
        }

        public IReadOnlyCollection<GameplayCapabilityRegistration>
            Registrations => registrations.Values;

        public void RegisterStage(
            GameplayCapabilityProfile profile,
            GameplayCapabilitySupportStage stage,
            string implementationId)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (stage == GameplayCapabilitySupportStage.PureStateReduction
                && !reducers.Supports(profile))
                throw new InvalidOperationException(
                    $"Capability '{profile.Signature}' cannot declare reduction without a reducer.");
            if (!registrations.TryGetValue(
                profile.Signature,
                out GameplayCapabilityRegistration registration))
            {
                registration = new GameplayCapabilityRegistration(profile);
                registrations.Add(profile.Signature, registration);
            }
            registration.Register(stage, implementationId);
        }

        public bool TryGet(
            GameplayCapabilityProfile profile,
            out GameplayCapabilityRegistration registration)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            return registrations.TryGetValue(
                profile.Signature,
                out registration);
        }

        public void RequireCandidateRoute(GameplayCandidate candidate)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            GameplayCapabilitySupportStage required =
                GameplayCapabilitySupportStage.CandidateConstruction
                | GameplayCapabilitySupportStage.LegalityAndEvidence
                | GameplayCapabilitySupportStage.PureStateReduction
                | GameplayCapabilitySupportStage.DomainEventProduction
                | GameplayCapabilitySupportStage.HeadlessExecution;
            if (!TryGet(candidate.Profile, out GameplayCapabilityRegistration route)
                || (route.Stages & required) != required
                || !reducers.Supports(candidate.Profile))
                throw new NotSupportedException(
                    $"Candidate '{candidate.CandidateId}' has no complete fail-closed simulation route for '{candidate.Profile.Signature}'.");
        }

        public void RequireCompleteRoute(GameplayCapabilityProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!TryGet(profile, out GameplayCapabilityRegistration route)
                || (route.Stages & GameplayCapabilitySupportStage.Complete)
                    != GameplayCapabilitySupportStage.Complete
                || !reducers.Supports(profile))
                throw new NotSupportedException(
                    $"Capability '{profile.Signature}' has no complete simulation route.");
        }
    }

    public sealed class GameplayCapabilityCoverageIssue
    {
        public GameplayCapabilityCoverageIssue(
            string code,
            string sourceId,
            string profileSignature,
            GameplayCapabilitySupportStage missingStages)
        {
            Code = GameplayContentIdentity.RequireText(code, nameof(code));
            SourceId = sourceId ?? string.Empty;
            ProfileSignature = GameplayContentIdentity.RequireText(
                profileSignature,
                nameof(profileSignature));
            MissingStages = missingStages;
        }

        public string Code { get; }
        public string SourceId { get; }
        public string ProfileSignature { get; }
        public GameplayCapabilitySupportStage MissingStages { get; }

        public bool IsBlocking => !string.Equals(
            Code,
            "capability.unreachable-implementation",
            StringComparison.Ordinal);
    }

    public sealed class GameplayCapabilityCoverageReport
    {
        internal GameplayCapabilityCoverageReport(
            IEnumerable<GameplayReachableInput> reachableInputs,
            IEnumerable<GameplayCapabilityCoverageIssue> issues)
        {
            ReachableInputs = new List<GameplayReachableInput>(
                reachableInputs).AsReadOnly();
            Issues = new List<GameplayCapabilityCoverageIssue>(
                issues).AsReadOnly();
        }

        public IReadOnlyList<GameplayReachableInput> ReachableInputs { get; }
        public IReadOnlyList<GameplayCapabilityCoverageIssue> Issues { get; }
        public bool IsComplete
        {
            get
            {
                foreach (GameplayCapabilityCoverageIssue issue in Issues)
                    if (issue.IsBlocking) return false;
                return true;
            }
        }

        public bool HasUnreachableImplementations
        {
            get
            {
                foreach (GameplayCapabilityCoverageIssue issue in Issues)
                    if (!issue.IsBlocking) return true;
                return false;
            }
        }

        public void RequireComplete(string scenarioId)
        {
            if (IsComplete) return;
            var details = new List<string>();
            foreach (GameplayCapabilityCoverageIssue issue in Issues)
            {
                if (!issue.IsBlocking) continue;
                details.Add(
                    $"{issue.Code}: {issue.SourceId} -> {issue.ProfileSignature} missing {issue.MissingStages}");
            }
            throw new InvalidOperationException(
                $"Scenario '{scenarioId}' has incomplete simulation capability coverage: "
                + string.Join(" | ", details));
        }
    }

    public static class GameplayCapabilityCoverageValidator
    {
        public static GameplayCapabilityCoverageReport Validate(
            GameplayScenarioAssembly assembly,
            LevelDocument level,
            GameplayCapabilityRegistry registry)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            return ValidateInputs(
                GameplayReachableInputEnumerator.Enumerate(assembly, level),
                registry);
        }

        public static GameplayCapabilityCoverageReport Validate(
            ScenarioDefinition scenario,
            LevelDocument level,
            GameplayCapabilityRegistry registry)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            return ValidateInputs(
                GameplayReachableInputEnumerator.Enumerate(scenario, level),
                registry);
        }

        public static GameplayCapabilityCoverageReport Validate(
            IEnumerable<GameplayReachableInput> reachableInputs,
            GameplayCapabilityRegistry registry)
        {
            if (reachableInputs == null)
                throw new ArgumentNullException(nameof(reachableInputs));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            return ValidateInputs(
                new List<GameplayReachableInput>(reachableInputs).AsReadOnly(),
                registry);
        }

        private static GameplayCapabilityCoverageReport ValidateInputs(
            IReadOnlyList<GameplayReachableInput> inputs,
            GameplayCapabilityRegistry registry)
        {
            var issues = new List<GameplayCapabilityCoverageIssue>();
            var reachableProfiles = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameplayReachableInput input in inputs)
            {
                reachableProfiles.Add(input.Profile.Signature);
                if (!registry.TryGet(
                    input.Profile,
                    out GameplayCapabilityRegistration registration))
                {
                    issues.Add(new GameplayCapabilityCoverageIssue(
                        "capability.missing-route",
                        input.SourceId,
                        input.Profile.Signature,
                        GameplayCapabilitySupportStage.Complete));
                    continue;
                }
                GameplayCapabilitySupportStage missing =
                    GameplayCapabilitySupportStage.Complete
                    & ~registration.Stages;
                if (missing != GameplayCapabilitySupportStage.None)
                    issues.Add(new GameplayCapabilityCoverageIssue(
                        "capability.incomplete-route",
                        input.SourceId,
                        input.Profile.Signature,
                        missing));
            }
            foreach (GameplayCapabilityRegistration registration in
                registry.Registrations)
            {
                if (!reachableProfiles.Contains(registration.Profile.Signature))
                    issues.Add(new GameplayCapabilityCoverageIssue(
                        "capability.unreachable-implementation",
                        string.Empty,
                        registration.Profile.Signature,
                        GameplayCapabilitySupportStage.None));
            }
            return new GameplayCapabilityCoverageReport(inputs, issues);
        }
    }

    public static class GameplayReachableInputEnumerator
    {
        public static IReadOnlyList<GameplayReachableInput> Enumerate(
            GameplayScenarioAssembly assembly,
            LevelDocument level)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));
            var result = new List<GameplayReachableInput>(
                Enumerate(
                    assembly.Scenario,
                    level,
                    assembly.Vehicles.Count > 0));
            foreach (ScenarioVehicleRuntimeDefinition vehicle in
                assembly.Vehicles)
            {
                if (vehicle.StartingOccupantActorId == null) continue;
                Add(result, GameplayReachableInputKind.ContextualInteraction,
                    vehicle.EntityId + ".move",
                    vehicle.StartingOccupantActorId,
                    GameplayCapabilityProfiles.VehicleMove(),
                    vehicle.EntityId);
            }
            return result.AsReadOnly();
        }

        public static IReadOnlyList<GameplayReachableInput> Enumerate(
            ScenarioDefinition scenario,
            LevelDocument level)
            => Enumerate(scenario, level, hasVehicles: false);

        private static IReadOnlyList<GameplayReachableInput> Enumerate(
            ScenarioDefinition scenario,
            LevelDocument level,
            bool hasVehicles)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (level == null) throw new ArgumentNullException(nameof(level));
            var result = new List<GameplayReachableInput>();
            bool hasTraversal = level.traversalLinks != null
                && level.traversalLinks.Count > 0;
            bool hasEmergency = HasEmergencyProjectile(scenario);
            bool hasProjectile = HasProjectile(scenario);
            bool hasDestructibles = HasTacticalDestructible(level);
            foreach (ScenarioActorDefinition actor in scenario.Actors)
            {
                bool playerControlled = scenario.PlayerParty == null
                    || scenario.PlayerParty.Contains(actor.Id);
                bool aiControlled = actor.Combat.EnemyBehavior != null;
                if (playerControlled || aiControlled)
                {
                    GameplayReachableInputKind controlKind = playerControlled
                        ? GameplayReachableInputKind.MovementControl
                        : GameplayReachableInputKind.EnemyDecision;
                    Add(result, controlKind,
                        playerControlled ? "control.move" : "ai.move",
                        actor.Id,
                        GameplayCapabilityProfiles.GroundedMove());
                    if (hasTraversal)
                        Add(result, controlKind,
                            playerControlled
                                ? "control.move.traversal"
                                : "ai.move.traversal",
                            actor.Id,
                            GameplayCapabilityProfiles.TraversalMove());
                    Add(result,
                        playerControlled
                            ? GameplayReachableInputKind.EndTurnControl
                            : GameplayReachableInputKind.EnemyDecision,
                        playerControlled ? "control.end-turn" : "ai.end-turn",
                        actor.Id,
                        GameplayCapabilityProfiles.EndTurn(emergency: false));
                }
                if (playerControlled)
                    Add(result, GameplayReachableInputKind.StanceControl,
                        "control.stance", actor.Id,
                        GameplayCapabilityProfiles.ChangeStance());
                if (hasEmergency)
                    Add(result, GameplayReachableInputKind.EmergencyControl,
                        "control.end-emergency-turn", actor.Id,
                        GameplayCapabilityProfiles.EndTurn(emergency: true));
                if (actor.Attack != null)
                    AddAttack(result, actor, actor.Attack,
                        GameplayReachableInputKind.EquippedAttack,
                        actor.Attack.ActionId,
                        playerControlled,
                        aiControlled,
                        hasDestructibles,
                        hasVehicles);
                foreach (InventoryItemDefinition item in actor.Inventory)
                {
                    if (item.IsEquippable)
                    {
                        if (playerControlled)
                            Add(result,
                                GameplayReachableInputKind.InventoryWeapon,
                                item.Id + ".equip", actor.Id,
                                GameplayCapabilityProfiles.Equip(),
                                item.Id);
                        AddAttack(result, actor, item.Attack,
                            GameplayReachableInputKind.InventoryWeapon,
                            item.Id + ".power",
                            playerControlled,
                            aiControlled && string.Equals(
                                item.Id,
                                actor.InitiallyEquippedItemId,
                                StringComparison.Ordinal),
                            hasDestructibles,
                            hasVehicles);
                    }
                    else if (playerControlled && item.ConsumablePower
                        is ThrownExplosiveDefinition explosive)
                    {
                        Add(result,
                            GameplayReachableInputKind.InventoryConsumable,
                            item.Id + ".power",
                            actor.Id,
                            GameplayCapabilityProfiles.ThrowExplosive(explosive));
                    }
                    else if (playerControlled)
                    {
                        throw new NotSupportedException(
                            $"Consumable power '{item.ConsumablePower?.PowerTypeId}' has no semantic capability profile.");
                    }
                }
                if (playerControlled || aiControlled)
                    foreach (DisplacementActionDefinition action in
                        actor.DisplacementActions)
                    {
                        GameplayReachableInputKind kind = playerControlled
                            ? GameplayReachableInputKind.CharacterAbility
                            : GameplayReachableInputKind.EnemyDecision;
                        string source = playerControlled
                            ? action.Id
                            : "ai." + actor.Combat.EnemyBehavior.BehaviorId
                                + "." + action.Id;
                        if (action.Accepts(DisplacementSubjectKind.Combatant))
                            Add(result, kind, source + "->Actor", actor.Id,
                                GameplayCapabilityProfiles.Displace(
                                    action,
                                    GameplaySemanticSubjectKind.Actor));
                        if (hasDestructibles
                            && action.Accepts(DisplacementSubjectKind.Prop))
                            Add(result, kind,
                                source + "->DestructibleProp", actor.Id,
                                GameplayCapabilityProfiles.Displace(
                                    action,
                                    GameplaySemanticSubjectKind
                                        .DestructibleProp));
                    }
            }
            IEnumerable<string> interactionActors = scenario.PlayerParty == null
                ? ActorIds(scenario.Actors)
                : scenario.PlayerParty.ActorIds;
            foreach (ScenarioObjectiveDefinition objective in scenario.Objectives)
                foreach (string actorId in interactionActors)
                    Add(result,
                        GameplayReachableInputKind.ContextualInteraction,
                        objective.Interaction.Id,
                        actorId,
                        GameplayCapabilityProfiles.Interact(),
                        objective.Id);
            string systemActorId = scenario.Actors[0].Id;
            Add(result, GameplayReachableInputKind.SessionControl,
                "control.turn-mode.enter", systemActorId,
                GameplayCapabilityProfiles.ChangeTurnMode("enter"));
            Add(result, GameplayReachableInputKind.SessionControl,
                "control.turn-mode.exit", systemActorId,
                GameplayCapabilityProfiles.ChangeTurnMode("exit"));
            Add(result, GameplayReachableInputKind.SystemContinuation,
                "system.world.continuous-time", systemActorId,
                GameplayCapabilityProfiles.AdvanceWorld("continuous-time"));
            Add(result, GameplayReachableInputKind.SystemContinuation,
                "system.world.voluntary-cycle", systemActorId,
                GameplayCapabilityProfiles.AdvanceWorld("voluntary-cycle"));
            Add(result, GameplayReachableInputKind.SystemContinuation,
                "system.encounter.begin", systemActorId,
                GameplayCapabilityProfiles.ChangeEncounter("begin"));
            Add(result, GameplayReachableInputKind.SystemContinuation,
                "system.encounter.request-completion", systemActorId,
                GameplayCapabilityProfiles.ChangeEncounter(
                    "request-completion"));
            Add(result, GameplayReachableInputKind.SystemContinuation,
                "system.encounter.complete", systemActorId,
                GameplayCapabilityProfiles.ChangeEncounter("complete"));
            if (hasProjectile)
                Add(result, GameplayReachableInputKind.SystemContinuation,
                    "system.projectile.advance", systemActorId,
                    GameplayCapabilityProfiles.AdvanceProjectile());
            if (hasEmergency)
            {
                Add(result, GameplayReachableInputKind.SystemContinuation,
                    "system.emergency.begin", systemActorId,
                    GameplayCapabilityProfiles.EmergencyReaction("begin"));
                Add(result, GameplayReachableInputKind.SystemContinuation,
                    "system.emergency.complete", systemActorId,
                    GameplayCapabilityProfiles.EmergencyReaction("complete"));
            }
            return result.AsReadOnly();
        }

        private static void AddAttack(
            ICollection<GameplayReachableInput> result,
            ScenarioActorDefinition actor,
            AttackDefinition attack,
            GameplayReachableInputKind kind,
            string sourceId,
            bool playerControlled,
            bool aiControlled,
            bool hasDestructibles,
            bool hasVehicles)
        {
            AddAttackSubject(
                result,
                actor,
                attack,
                kind,
                sourceId,
                playerControlled,
                aiControlled,
                GameplaySemanticSubjectKind.Actor);
            if (attack.Contact != null) return;
            AddAttackSubject(
                result,
                actor,
                attack,
                kind,
                sourceId,
                playerControlled,
                aiControlled,
                GameplaySemanticSubjectKind.WorldPosition);
            if (hasDestructibles
                && (attack.Projectile != null
                    || attack.DirectFireDamage != null))
                AddAttackSubject(
                    result,
                    actor,
                    attack,
                    kind,
                    sourceId,
                    playerControlled,
                    aiControlled,
                    GameplaySemanticSubjectKind.DestructibleProp);
            if (hasVehicles)
                AddAttackSubject(
                    result,
                    actor,
                    attack,
                    kind,
                    sourceId,
                    playerControlled,
                    aiControlled,
                    GameplaySemanticSubjectKind.Vehicle);
        }

        private static void AddAttackSubject(
            ICollection<GameplayReachableInput> result,
            ScenarioActorDefinition actor,
            AttackDefinition attack,
            GameplayReachableInputKind kind,
            string sourceId,
            bool playerControlled,
            bool aiControlled,
            GameplaySemanticSubjectKind subjectKind)
        {
            string suffix = "->" + subjectKind;
            GameplayCapabilityProfile profile = GameplayCapabilityProfiles
                .Attack(attack, subjectKind);
            if (playerControlled)
                Add(result, kind, sourceId + suffix, actor.Id, profile);
            if (aiControlled)
                Add(result, GameplayReachableInputKind.EnemyDecision,
                    "ai." + actor.Combat.EnemyBehavior.BehaviorId + "."
                        + sourceId + suffix,
                    actor.Id,
                    profile);
        }

        private static bool HasEmergencyProjectile(ScenarioDefinition scenario)
        {
            foreach (ScenarioActorDefinition actor in scenario.Actors)
            {
                if (actor.Attack?.Projectile?.OpensEmergencyReactionWindow == true)
                    return true;
                foreach (InventoryItemDefinition item in actor.Inventory)
                    if (item.Attack?.Projectile
                        ?.OpensEmergencyReactionWindow == true)
                        return true;
            }
            return false;
        }

        private static bool HasProjectile(ScenarioDefinition scenario)
        {
            foreach (ScenarioActorDefinition actor in scenario.Actors)
            {
                if (actor.Attack?.Projectile != null) return true;
                foreach (InventoryItemDefinition item in actor.Inventory)
                    if (item.Attack?.Projectile != null) return true;
            }
            return false;
        }

        private static bool HasTacticalDestructible(LevelDocument level)
        {
            foreach (LevelEntity entity in level.entities)
                if (entity?.destructible?.enabled == true) return true;
            return false;
        }

        private static IEnumerable<string> ActorIds(
            IEnumerable<ScenarioActorDefinition> actors)
        {
            foreach (ScenarioActorDefinition actor in actors)
                yield return actor.Id;
        }

        private static void Add(
            ICollection<GameplayReachableInput> result,
            GameplayReachableInputKind kind,
            string sourceId,
            string actorId,
            GameplayCapabilityProfile profile,
            string subjectIdHint = null) => result.Add(
                new GameplayReachableInput(
                    kind,
                    sourceId,
                    actorId,
                    profile,
                    subjectIdHint));
    }
}
