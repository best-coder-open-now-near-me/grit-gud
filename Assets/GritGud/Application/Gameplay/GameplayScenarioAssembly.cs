using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public sealed class ScenarioObjectiveRuntimeDefinition
    {
        public ScenarioObjectiveRuntimeDefinition(
            string id,
            string activeHudText,
            string completedHudText)
        {
            Id = string.IsNullOrWhiteSpace(id)
                ? throw new ArgumentException(
                    "Objective identifiers cannot be empty.",
                    nameof(id))
                : id;
            ActiveHudText = activeHudText ?? string.Empty;
            CompletedHudText = completedHudText ?? string.Empty;
        }

        public string Id { get; }

        public string ActiveHudText { get; }

        public string CompletedHudText { get; }
    }

    public sealed class ScenarioActorRuntimeDefinition
    {
        internal ScenarioActorRuntimeDefinition(
            string displayName,
            string presentationId,
            string characterId,
            bool targetable,
            float mass,
            ScenarioActorDefinition gameplayDefinition,
            CloseQuartersControlProfile controlProfile)
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? throw new ArgumentException(
                    "Actor display names cannot be empty.",
                    nameof(displayName))
                : displayName;
            PresentationId = string.IsNullOrWhiteSpace(presentationId)
                ? throw new ArgumentException(
                    "Actor presentation identifiers cannot be empty.",
                    nameof(presentationId))
                : presentationId;
            CharacterId = characterId?.Trim() ?? string.Empty;
            Targetable = targetable;
            Mass = mass;
            GameplayDefinition = gameplayDefinition
                ?? throw new ArgumentNullException(nameof(gameplayDefinition));
            ControlProfile = controlProfile;
        }

        public string Id => GameplayDefinition.Id;

        public string DisplayName { get; }

        public string PresentationId { get; }

        public string CharacterId { get; }

        public bool Targetable { get; }

        public float Mass { get; }

        public ScenarioActorDefinition GameplayDefinition { get; }

        public CloseQuartersControlProfile ControlProfile { get; }

        internal ScenarioActorRuntimeDefinition WithGameplayDefinition(
            ScenarioActorDefinition gameplayDefinition) =>
            new ScenarioActorRuntimeDefinition(
                DisplayName,
                PresentationId,
                CharacterId,
                Targetable,
                Mass,
                gameplayDefinition,
                ControlProfile);
    }

    public sealed class ScenarioVehicleRuntimeDefinition
    {
        internal ScenarioVehicleRuntimeDefinition(
            string entityId,
            VehicleMomentumProfile momentumProfile,
            float startingSpeed,
            string startingOccupantActorId)
        {
            EntityId = string.IsNullOrWhiteSpace(entityId)
                ? throw new ArgumentException(
                    "Vehicle entity identifiers cannot be empty.",
                    nameof(entityId))
                : entityId;
            MomentumProfile = momentumProfile
                ?? throw new ArgumentNullException(nameof(momentumProfile));
            if (!momentumProfile.IsValidSpeed(startingSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(startingSpeed));
            }

            StartingSpeed = startingSpeed;
            StartingOccupantActorId = string.IsNullOrWhiteSpace(
                startingOccupantActorId)
                ? null
                : startingOccupantActorId;
        }

        public string EntityId { get; }

        public VehicleMomentumProfile MomentumProfile { get; }

        public float StartingSpeed { get; }

        public string StartingOccupantActorId { get; }
    }

    public sealed class ScenarioDroneSummonRuntimeDefinition
    {
        public ScenarioDroneSummonRuntimeDefinition(
            string summonerActorId,
            DroneSummonAbilityDefinition ability)
        {
            SummonerActorId = GameplayContentIdentity.RequireText(
                summonerActorId,
                nameof(summonerActorId));
            Ability = ability ?? throw new ArgumentNullException(
                nameof(ability));
        }

        public string SummonerActorId { get; }
        public DroneSummonAbilityDefinition Ability { get; }
    }

    public sealed class GameplayScenarioAssembly
    {
        private readonly Dictionary<string, ScenarioActorRuntimeDefinition>
            actors;
        private readonly Dictionary<string, ScenarioVehicleRuntimeDefinition>
            vehicles;
        private readonly Dictionary<string, DroneArchetypeDefinition>
            droneArchetypes;
        private readonly Dictionary<
            string,
            ScenarioDroneSummonRuntimeDefinition> droneSummonAbilities;
        private readonly Dictionary<string, ScenarioObjectiveRuntimeDefinition>
            objectives;
        private readonly Dictionary<string, DisplacementSubjectDefinition>
            displacementSubjects;
        private readonly IReadOnlyList<TacticalContextRuleDefinition>
            tacticalRules;

        internal GameplayScenarioAssembly(
            string displayName,
            string primaryTargetActorId,
            string primaryObjectiveId,
            uint randomSeed,
            ScenarioDefinition scenario,
            Dictionary<string, ScenarioActorRuntimeDefinition> actorIndex,
            Dictionary<string, ScenarioObjectiveRuntimeDefinition>
                objectiveIndex,
            Dictionary<string, ScenarioVehicleRuntimeDefinition> vehicleIndex,
            Dictionary<string, DisplacementSubjectDefinition>
                displacementSubjectIndex,
            IEnumerable<TacticalContextRuleDefinition> tacticalRuleDefinitions = null,
            Dictionary<string, DroneArchetypeDefinition>
                droneArchetypeIndex = null,
            Dictionary<string, ScenarioDroneSummonRuntimeDefinition>
                droneSummonAbilityIndex = null)
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? throw new ArgumentException(
                    "Scenario display names cannot be empty.",
                    nameof(displayName))
                : displayName;
            PrimaryTargetActorId = primaryTargetActorId ?? string.Empty;
            PrimaryObjectiveId = primaryObjectiveId ?? string.Empty;
            RandomSeed = randomSeed;
            Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            actors = actorIndex
                ?? throw new ArgumentNullException(nameof(actorIndex));
            vehicles = vehicleIndex
                ?? throw new ArgumentNullException(nameof(vehicleIndex));
            droneArchetypes = droneArchetypeIndex
                ?? new Dictionary<string, DroneArchetypeDefinition>(
                    StringComparer.Ordinal);
            droneSummonAbilities = droneSummonAbilityIndex
                ?? new Dictionary<
                    string,
                    ScenarioDroneSummonRuntimeDefinition>(
                        StringComparer.Ordinal);
            objectives = objectiveIndex
                ?? throw new ArgumentNullException(nameof(objectiveIndex));
            displacementSubjects = displacementSubjectIndex
                ?? throw new ArgumentNullException(
                    nameof(displacementSubjectIndex));
            tacticalRules = new List<TacticalContextRuleDefinition>(
                tacticalRuleDefinitions
                    ?? Array.Empty<TacticalContextRuleDefinition>())
                .AsReadOnly();
        }

        public string DisplayName { get; }

        public ScenarioDefinition Scenario { get; }

        public PlayerPartyDefinition PlayerParty => Scenario.PlayerParty;

        public string InitiallySelectedActorId =>
            PlayerParty.InitiallySelectedActorId;

        public string PrimaryTargetActorId { get; }

        public string PrimaryObjectiveId { get; }

        public uint RandomSeed { get; }

        public ScenarioObjectiveRuntimeDefinition PrimaryObjective =>
            string.IsNullOrWhiteSpace(PrimaryObjectiveId)
                ? null
                : objectives[PrimaryObjectiveId];

        public ScenarioActorRuntimeDefinition GetActor(string actorId)
        {
            if (!actors.TryGetValue(
                    actorId ?? string.Empty,
                    out ScenarioActorRuntimeDefinition actor))
            {
                throw new KeyNotFoundException(
                    $"Scenario actor '{actorId}' is not defined.");
            }

            return actor;
        }

        public ScenarioActorDefinition GetActorDefinition(string actorId) =>
            GetActor(actorId).GameplayDefinition;

        public bool TryGetVehicle(
            string vehicleId,
            out ScenarioVehicleRuntimeDefinition vehicle) =>
            vehicles.TryGetValue(vehicleId ?? string.Empty, out vehicle);

        public IReadOnlyCollection<ScenarioActorRuntimeDefinition> Actors =>
            actors.Values;

        public IReadOnlyCollection<ScenarioVehicleRuntimeDefinition> Vehicles =>
            vehicles.Values;

        public IReadOnlyCollection<DroneArchetypeDefinition> DroneArchetypes =>
            droneArchetypes.Values;

        public IReadOnlyCollection<ScenarioDroneSummonRuntimeDefinition>
            DroneSummonAbilities => droneSummonAbilities.Values;

        public DroneArchetypeDefinition GetDroneArchetype(string archetypeId) =>
            droneArchetypes.TryGetValue(
                archetypeId ?? string.Empty,
                out DroneArchetypeDefinition archetype)
                ? archetype
                : throw new KeyNotFoundException(
                    $"Scenario drone archetype '{archetypeId}' is not defined.");

        public ScenarioDroneSummonRuntimeDefinition GetDroneSummonAbility(
            string abilityId) => droneSummonAbilities.TryGetValue(
                abilityId ?? string.Empty,
                out ScenarioDroneSummonRuntimeDefinition ability)
                ? ability
                : throw new KeyNotFoundException(
                    $"Scenario drone summon ability '{abilityId}' is not defined.");

        public IReadOnlyList<ScenarioDroneSummonRuntimeDefinition>
            GetDroneSummonAbilities(string actorId)
        {
            var result = new List<ScenarioDroneSummonRuntimeDefinition>();
            foreach (ScenarioDroneSummonRuntimeDefinition ability in
                droneSummonAbilities.Values)
                if (string.Equals(
                        ability.SummonerActorId,
                        actorId,
                        StringComparison.Ordinal))
                    result.Add(ability);
            result.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.Ability.AbilityId,
                right.Ability.AbilityId));
            return result.AsReadOnly();
        }

        public IReadOnlyCollection<DisplacementSubjectDefinition>
            DisplacementSubjects => displacementSubjects.Values;

        public IReadOnlyList<TacticalContextRuleDefinition> TacticalRules =>
            tacticalRules;

        public GameplayScenarioAssembly WithResolvedActorPoses(
            IReadOnlyDictionary<string, GameplayActorPose> resolvedPoses)
        {
            if (resolvedPoses == null)
            {
                throw new ArgumentNullException(nameof(resolvedPoses));
            }

            ScenarioDefinition resolvedScenario =
                Scenario.WithActorStartingPoses(resolvedPoses);
            var resolvedActors =
                new Dictionary<string, ScenarioActorRuntimeDefinition>(
                    actors.Count,
                    StringComparer.Ordinal);
            foreach (ScenarioActorRuntimeDefinition actor in actors.Values)
            {
                resolvedActors.Add(
                    actor.Id,
                    actor.WithGameplayDefinition(
                        resolvedScenario.GetActor(actor.Id)));
            }

            return new GameplayScenarioAssembly(
                DisplayName,
                PrimaryTargetActorId,
                PrimaryObjectiveId,
                RandomSeed,
                resolvedScenario,
                resolvedActors,
                objectives,
                vehicles,
                displacementSubjects,
                tacticalRules,
                droneArchetypes,
                droneSummonAbilities);
        }

        public bool TryGetDisplacementSubject(
            string subjectId,
            out DisplacementSubjectDefinition subject) =>
            displacementSubjects.TryGetValue(
                subjectId ?? string.Empty,
                out subject);
    }
}
