using System;
using System.Collections.Generic;
using System.Threading;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class GameplayBattleTerminalTests
    {
        [Test]
        public void AllEmptyBattleEndsInStalemateInsteadOfExecutionFailure()
        {
            GameplayScenarioAssembly assembly = CreateAssembly();
            var level = new LevelDocument
            {
                levelId = "ammo-stalemate-level",
                schemaVersion = LevelDocument.CurrentSchemaVersion,
            };
            level.Normalize();
            var spatialContent = new GameplayStaticSpatialContent(
                level,
                new GameplayFractureSpatialCatalogDocument());
            GameplayCombatStateSnapshot initial =
                GameplayHeadlessBattleStateFactory.Create(
                    assembly,
                    spatialContent);
            var identity = new GameplayExecutionIdentity(
                new GameplayContentIdentity(
                    assembly.Scenario.Id,
                    ScenarioContentDocument.CurrentSchemaVersion,
                    GameplayCombatStateSnapshot.CurrentSchemaVersion,
                    GameplayCanonicalValueDigest.Calculate(assembly.Scenario)),
                spatialContent.Identity,
                initial.Session.RunIdentity);
            var runner = new GameplayBattleRunner(
                assembly,
                spatialContent,
                identity,
                new EndTurnPolicy(),
                deadlinePolicy: new GameplayExecutionDeadlinePolicy(
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromMinutes(1),
                    TimeSpan.FromMinutes(2)),
                logicalGuardPolicy: new GameplayExecutionLogicalGuardPolicy(
                    maximumTransitions: 20,
                    maximumRepeatedMaterialStates: 10,
                    maximumNoProgressTurns: 2));

            GameplayBattleRunResult result = runner.RunAsync(initial)
                .GetAwaiter().GetResult();

            Assert.That(result.Terminal.Kind,
                Is.EqualTo(GameplayBattleTerminalKind.Stalemate),
                result.Terminal.FailureKind + ": "
                    + result.Terminal.FailureMessage);
            Assert.That(result.Terminal.IsSuccessful, Is.True);
            Assert.That(result.Terminal.FailureKind, Is.Null);
            Assert.That(result.FailureDiagnostic, Is.Null);
            Assert.That(result.Terminal.CapablePartyActorIds,
                Is.EqualTo(new[] { "player" }));
            Assert.That(result.Terminal.CapableHostileActorIds,
                Is.EqualTo(new[] { "enemy" }));
            Assert.That(result.FinalState.Session.GetActor("player")
                .Ammunition.GetMagazine("weapon.player-rifle")
                .LoadedRounds, Is.Zero);
            Assert.That(result.FinalState.Session.GetActor("enemy")
                .Ammunition.GetMagazine("weapon.enemy-rifle")
                .LoadedRounds, Is.Zero);
        }

        private static GameplayScenarioAssembly CreateAssembly()
        {
            ScenarioActorDefinition player = CreateActor(
                "player",
                "weapon.player-rifle",
                initiative: 10,
                allegianceId: "party",
                hostileAllegianceId: "hostile");
            ScenarioActorDefinition enemy = CreateActor(
                "enemy",
                "weapon.enemy-rifle",
                initiative: 0,
                allegianceId: "hostile",
                hostileAllegianceId: "party",
                enemyBehavior: new EnemyBehaviorDefinition(
                    "enemy.stalemate",
                    perceptionRange: 20f,
                    viewAngleDegrees: 180f,
                    preferredEngagementRange: 8f,
                    movementSearchRadius: 1f,
                    maximumAttacksPerTurn: 1));
            var scenario = new ScenarioDefinition(
                "ammo-stalemate",
                new ScenarioTimingDefinition(1f),
                new[] { player, enemy },
                Array.Empty<ScenarioObjectiveDefinition>(),
                playerParty: new PlayerPartyDefinition(
                    new[] { player.Id },
                    player.Id));
            var actors = new Dictionary<
                string,
                ScenarioActorRuntimeDefinition>(StringComparer.Ordinal)
            {
                [player.Id] = RuntimeActor(player),
                [enemy.Id] = RuntimeActor(enemy),
            };
            return new GameplayScenarioAssembly(
                "Ammo stalemate",
                enemy.Id,
                primaryObjectiveId: string.Empty,
                randomSeed: 37u,
                scenario,
                actors,
                new Dictionary<
                    string,
                    ScenarioObjectiveRuntimeDefinition>(
                        StringComparer.Ordinal),
                new Dictionary<
                    string,
                    ScenarioVehicleRuntimeDefinition>(
                        StringComparer.Ordinal),
                new Dictionary<string, DisplacementSubjectDefinition>(
                    StringComparer.Ordinal));
        }

        private static ScenarioActorDefinition CreateActor(
            string actorId,
            string weaponId,
            int initiative,
            string allegianceId,
            string hostileAllegianceId,
            EnemyBehaviorDefinition enemyBehavior = null)
        {
            var attack = new AttackDefinition(
                "attack." + actorId + ".rifle",
                "Fire",
                new ActionCost(1, 0f, ActionMobility.Set),
                woundMovementPenalty: 2f,
                accuracyDecay: AccuracyDecayDefinition.None);
            var weapon = new InventoryItemDefinition(
                weaponId,
                "Empty Rifle",
                hotbarSlot: 1,
                InventoryItemKind.Weapon,
                new ActionCost(1, 0f, ActionMobility.Set),
                EquipmentEffectSet.None,
                attack,
                ammunition: new WeaponAmmunitionDefinition(
                    "ammo.rifle",
                    magazineCapacity: 1,
                    initialLoadedRounds: 0,
                    roundsPerUse: 1,
                    reloadTurnCost: new ActionCost(
                        2,
                        0f,
                        ActionMobility.Set),
                    consumesRemainingMovement: true,
                    reloadPolicyVersion: 1));
            return new ScenarioActorDefinition(
                actorId,
                initiative,
                new GameplayActorPose(
                    new GameplayPosition(
                        actorId == "player" ? 0f : 5f,
                        0f,
                        0f),
                    actorId == "player" ? 90f : 270f),
                new TurnBudget(4, 0f),
                new[] { weapon },
                weapon.Id,
                characterProfile: CreateCharacterProfile(actorId),
                combat: new ActorCombatDefinition(
                    allegianceId,
                    new[] { hostileAllegianceId },
                    maximumWounds: 2,
                    enemyBehavior),
                ammunitionReserves: new[]
                {
                    new AmmunitionReserveDefinition("ammo.rifle", 0),
                });
        }

        private static CharacterProfileDefinition CreateCharacterProfile(
            string actorId) => new CharacterProfileDefinition(
            "character." + actorId,
            actorId,
            "Test Combatant",
            new[]
            {
                new CharacterRating(CoreAttributeIds.Strength, 3),
                new CharacterRating(CoreAttributeIds.Dexterity, 3),
                new CharacterRating(CoreAttributeIds.Grit, 3),
                new CharacterRating(CoreAttributeIds.Charisma, 3),
            },
            Array.Empty<CharacterRating>(),
            Array.Empty<string>());

        private static ScenarioActorRuntimeDefinition RuntimeActor(
            ScenarioActorDefinition actor) =>
            new ScenarioActorRuntimeDefinition(
                actor.Id,
                "presentation." + actor.Id,
                actor.CharacterProfile.IdentityId,
                targetable: true,
                mass: 80f,
                actor,
                controlProfile: default);

        private sealed class EndTurnPolicy :
            IGameplayCandidatePolicy,
            IGameplayIdentifiedCandidatePolicy
        {
            public string PolicyId => "policy.test.end-turn";
            public int PolicyVersion => 1;

            public GameplayPolicyScore Score(
                GameplayDecisionContext context,
                GameplayExecutableCandidateEvaluation evaluation,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new GameplayPolicyScore(
                    evaluation,
                    evaluation.Candidate.Profile.Capability
                            == GameplaySemanticCapability.EndTurn
                        ? 1000f
                        : 0f);
            }
        }
    }
}
