using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Domain.Turns;
using NUnit.Framework;

namespace GritGud.Domain.Tests
{
    public sealed class GameplayScenarioAssemblerTests
    {
        [Test]
        public void AssemblerBuildsScenarioFromContentAndLevelBinding()
        {
            ScenarioContentDocument content = CreateContent();
            LevelDocument level = CreateLevel();

            GameplayScenarioAssembly result =
                new GameplayScenarioAssembler().Assemble(content, level);

            Assert.That(result.Scenario.Id, Is.EqualTo("scenario.test"));
            Assert.That(
                result.Scenario.Timing.MinimumVoluntaryTurnSeconds,
                Is.EqualTo(2.5f));
            Assert.That(
                result.PlayerParty.ActorIds,
                Is.EqualTo(new[] { "actor.player" }));
            Assert.That(
                result.InitiallySelectedActorId,
                Is.EqualTo("actor.player"));
            Assert.That(result.Scenario.Actors, Has.Count.EqualTo(2));
            Assert.That(result.Scenario.Objectives, Has.Count.EqualTo(1));
            ScenarioActorDefinition player =
                result.GetActorDefinition("actor.player");
            ScenarioActorDefinition target =
                result.GetActorDefinition("actor.target");
            Assert.That(player.CoreAttributes.Dexterity, Is.EqualTo(4));
            Assert.That(player.Initiative, Is.EqualTo(4));
            Assert.That(
                player.StartingTurnBudget.MovementOpportunity,
                Is.EqualTo(8f));
            Assert.That(target.Initiative, Is.EqualTo(3));
            Assert.That(
                target.StartingTurnBudget.MovementOpportunity,
                Is.EqualTo(7f));
            CloseQuartersControlProfile playerControl =
                result.GetActor("actor.player").ControlProfile;
            Assert.That(playerControl.StrengthRating, Is.EqualTo(3));
            Assert.That(playerControl.SkillRating, Is.EqualTo(4));
            ScenarioObjectiveDefinition objective = result.Scenario.Objectives[0];
            Assert.That(objective.Position.X, Is.EqualTo(12f).Within(0.001f));
            Assert.That(objective.Position.Y, Is.EqualTo(3f).Within(0.001f));
            Assert.That(objective.Position.Z, Is.EqualTo(4f).Within(0.001f));
            Assert.That(objective.Interaction.TurnCost.ActionPoints, Is.EqualTo(1));
            Assert.That(result.GetActor("actor.player").Mass, Is.EqualTo(80f));
            Assert.That(
                result.GetActorDefinition("actor.player").Attack.ActionId,
                Is.EqualTo("attack.rifle"));
            Assert.That(
                result.GetActorDefinition("actor.player")
                    .Attack.WoundMovementPenalty,
                Is.EqualTo(2f));
            Assert.That(
                result.GetActorDefinition("actor.player")
                    .Attack.AccuracyDecay.HalfLifeDistance,
                Is.EqualTo(60f));
            Assert.That(
                result.GetActorDefinition("actor.player")
                    .Attack.AccuracyDecay.MinimumAccuracyPercent,
                Is.EqualTo(5f));
            Assert.That(
                result.GetActorDefinition("actor.player").Attack.Projectile,
                Is.Null,
                "Ordinary attacks must remain immediate unless projectile behavior is explicitly authored.");
            DisplacementActionDefinition push = result
                .GetActorDefinition("actor.player")
                .GetDisplacementAction("close-quarters.push");
            Assert.That(push, Is.Not.Null);
            Assert.That(push.DisplayName, Is.EqualTo("Push"));
            Assert.That(push.Intent, Is.EqualTo(DisplacementActionKind.Push));
            Assert.That(push.Cost.ActionPoints, Is.EqualTo(1));
            Assert.That(
                result.GetActorDefinition("actor.player")
                    .DisplacementAbility.Id,
                Is.EqualTo("ability.displace"));
            Assert.That(
                result.GetActorDefinition("actor.player")
                    .DisplacementAbility.HotbarSlot,
                Is.EqualTo(4));
            Assert.That(push.AcceptedSubjects,
                Is.EqualTo(DisplacementSubjectKinds.Prop));
            Assert.That(push.Reach, Is.EqualTo(2f));
            Assert.That(push.MaximumDistance, Is.EqualTo(3f));
            Assert.That(push.MaximumSubjectMass, Is.EqualTo(90f));
            Assert.That(push.HandRequirement,
                Is.EqualTo(DisplacementHandRequirement.OneHandFree));
            Assert.That(push.AutoStowPolicy,
                Is.EqualTo(DisplacementAutoStowPolicy.Allowed));
            Assert.That(push.ContestPolicy,
                Is.EqualTo(DisplacementContestPolicy.None));
            Assert.That(push.AllowedResults,
                Is.EqualTo(
                    DisplacementResultPolicies.Topple
                    | DisplacementResultPolicies.Pin
                    | DisplacementResultPolicies.CollisionDamage));
            DisplacementActionDefinition pushOff = result
                .GetActorDefinition("actor.player")
                .GetDisplacementAction("close-quarters.push-off");
            Assert.That(pushOff, Is.Not.Null);
            Assert.That(pushOff.Intent,
                Is.EqualTo(DisplacementActionKind.PushOff));
            Assert.That(pushOff.AcceptedSubjects,
                Is.EqualTo(DisplacementSubjectKinds.Prop));
            Assert.That(pushOff.AllowedResults,
                Is.EqualTo(DisplacementResultPolicies.Release));
            Assert.That(
                result.TryGetDisplacementSubject(
                    "actor.player",
                    out DisplacementSubjectDefinition playerSubject),
                Is.True);
            Assert.That(playerSubject.Kind,
                Is.EqualTo(DisplacementSubjectKind.Combatant));
            Assert.That(playerSubject.Mass, Is.EqualTo(80f));
            Assert.That(
                result.TryGetDisplacementSubject(
                    "prop.one",
                    out DisplacementSubjectDefinition propSubject),
                Is.True);
            Assert.That(propSubject.Kind,
                Is.EqualTo(DisplacementSubjectKind.Prop));
            Assert.That(propSubject.Mass, Is.EqualTo(35f));
            Assert.That(propSubject.Toppling, Is.Not.Null);
            Assert.That(propSubject.Toppling.RollOffsetDegrees, Is.EqualTo(90f));
            Assert.That(propSubject.Toppling.ElevationOffset, Is.EqualTo(0.45f));
            Assert.That(propSubject.Pinning, Is.Not.Null);
            Assert.That(propSubject.Pinning.MaximumActorMass,
                Is.EqualTo(90f));
            Assert.That(propSubject.Pinning.MinimumContactDepth,
                Is.EqualTo(0.05f));
            Assert.That(
                result.TryGetVehicle(
                    "vehicle.one",
                    out ScenarioVehicleRuntimeDefinition vehicle),
                Is.True);
            Assert.That(vehicle.EntityId, Is.EqualTo("vehicle.one"));
            Assert.That(vehicle.MomentumProfile.MaximumSpeed, Is.EqualTo(10f));
            Assert.That(vehicle.StartingSpeed, Is.EqualTo(4f));
        }

        [Test]
        public void AssemblerBuildsOrderedMultiActorPlayerParty()
        {
            ScenarioContentDocument content = CreateContent();
            content.playerParty.actorIds.Add("actor.target");
            content.playerParty.initiallySelectedActorId = "actor.target";
            content.primaryTargetActorId = string.Empty;

            GameplayScenarioAssembly result =
                new GameplayScenarioAssembler().Assemble(
                    content,
                    CreateLevel());

            Assert.That(
                result.PlayerParty.ActorIds,
                Is.EqualTo(new[] { "actor.player", "actor.target" }));
            Assert.That(
                result.InitiallySelectedActorId,
                Is.EqualTo("actor.target"));
            Assert.That(result.PlayerParty.Contains("actor.player"), Is.True);
            Assert.That(result.PlayerParty.Contains("actor.target"), Is.True);
        }

        [Test]
        public void PlayerPartyRejectsPrimaryTargetMembership()
        {
            ScenarioContentDocument content = CreateContent();
            content.playerParty.actorIds.Add("actor.target");

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    new GameplayScenarioAssembler().Assemble(
                        content,
                        CreateLevel()));

            Assert.That(exception.Message,
                Does.Contain("player party actor")
                    .And.Contain("primary target"));
        }

        [Test]
        public void PlayerPartyRejectsUnknownActor()
        {
            ScenarioContentDocument content = CreateContent();
            content.playerParty.actorIds.Add("actor.missing");

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    new GameplayScenarioAssembler().Assemble(
                        content,
                        CreateLevel()));

            Assert.That(exception.Message,
                Does.Contain("Player party actor 'actor.missing' is not defined"));
        }

        [Test]
        public void PlayerPartyRejectsDuplicateActor()
        {
            ScenarioContentDocument content = CreateContent();
            content.playerParty.actorIds.Add("actor.player");

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    new GameplayScenarioAssembler().Assemble(
                        content,
                        CreateLevel()));

            Assert.That(exception.Message,
                Does.Contain("listed more than once"));
        }

        [Test]
        public void PlayerPartyRejectsInitialSelectionOutsideRoster()
        {
            ScenarioContentDocument content = CreateContent();
            content.playerParty.initiallySelectedActorId = "actor.target";

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    new GameplayScenarioAssembler().Assemble(
                        content,
                        CreateLevel()));

            Assert.That(exception.Message,
                Does.Contain("is not in the player party"));
        }

        [Test]
        public void PlayerPartyRejectsEnemyAiOwnership()
        {
            ScenarioContentDocument content = CreateContent();
            content.playerParty.actorIds.Add("actor.target");
            content.actors[1].combat = new ScenarioActorCombatData
            {
                allegianceId = "raider",
                hostileAllegianceIds = { "player" },
                maximumWounds = 2,
                enemyBehavior = new ScenarioEnemyBehaviorData
                {
                    behaviorId = "behavior.rifleman",
                    perceptionRange = 30f,
                    viewAngleDegrees = 120f,
                    preferredEngagementRange = 12f,
                    movementSearchRadius = 6f,
                    maximumAttacksPerTurn = 1,
                },
            };
            content.actors[1].attackCapability = content.actors[0]
                .attackCapability;

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    new GameplayScenarioAssembler().Assemble(
                        content,
                        CreateLevel()));

            Assert.That(exception.Message,
                Does.Contain("cannot own enemy behavior"));
        }

        [Test]
        public void PlayerPartyRejectsDuplicateCharacterIdentity()
        {
            ScenarioContentDocument content = CreateContent();
            content.playerParty.actorIds.Add("actor.target");
            content.actors[1].characterProfile.identityId =
                content.actors[0].characterProfile.identityId;

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    new GameplayScenarioAssembler().Assemble(
                        content,
                        CreateLevel()));

            Assert.That(exception.Message,
                Does.Contain("character identity")
                    .And.Contain("used more than once"));
        }

        [Test]
        public void MissingCharacterProfileIsRejected()
        {
            ScenarioContentDocument content = CreateContent();
            content.actors[1].characterProfile =
                new ScenarioCharacterProfileData();

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    new GameplayScenarioAssembler().Assemble(
                        content,
                        CreateLevel()));

            Assert.That(exception.Message,
                Does.Contain("requires a character profile"));
        }

        [Test]
        public void EmptyOptionalDisplacementAbilityIsTreatedAsOmitted()
        {
            ScenarioContentDocument content = CreateContent();
            content.actors[1].displacementAbility =
                new ScenarioDisplacementAbilityData();

            GameplayScenarioAssembly result =
                new GameplayScenarioAssembler().Assemble(
                    content,
                    CreateLevel());

            Assert.That(
                result.GetActorDefinition("actor.target")
                    .DisplacementAbility,
                Is.Null);
        }

        [Test]
        public void EmptyOptionalEnemyBehaviorIsTreatedAsOmitted()
        {
            ScenarioContentDocument content = CreateContent();
            content.actors[0].combat = new ScenarioActorCombatData
            {
                allegianceId = "player",
                hostileAllegianceIds = { "raider" },
                maximumWounds = 3,
                enemyBehavior = new ScenarioEnemyBehaviorData(),
            };

            GameplayScenarioAssembly result =
                new GameplayScenarioAssembler().Assemble(
                    content,
                    CreateLevel());

            Assert.That(
                result.GetActorDefinition("actor.player")
                    .Combat.EnemyBehavior,
                Is.Null);
        }

        [Test]
        public void PartiallyAuthoredEnemyBehaviorIsRejected()
        {
            ScenarioContentDocument content = CreateContent();
            content.actors[1].combat = new ScenarioActorCombatData
            {
                allegianceId = "raider",
                hostileAllegianceIds = { "player" },
                maximumWounds = 2,
                enemyBehavior = new ScenarioEnemyBehaviorData
                {
                    behaviorId = "behavior.incomplete",
                },
            };

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    new GameplayScenarioAssembler().Assemble(
                        content,
                        CreateLevel()));

            Assert.That(exception.Message,
                Does.Contain("perception range"));
        }

        [Test]
        public void PartiallyAuthoredDisplacementAbilityIsRejected()
        {
            ScenarioContentDocument content = CreateContent();
            content.actors[1].displacementAbility =
                new ScenarioDisplacementAbilityData
                {
                    displayName = "Incomplete Displace",
                };

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    new GameplayScenarioAssembler().Assemble(
                        content,
                        CreateLevel()));

            Assert.That(exception.Message,
                Does.Contain("displacement ability ID cannot be empty"));
        }

        [Test]
        public void AssemblerBuildsExplicitActorAndObjectAttackResponses()
        {
            ScenarioContentDocument content = CreateContent();
            content.actors[1].attackResponse =
                new ScenarioAttackResponseData
                {
                    startsEncounter = true,
                };
            content.props[0].attackResponse =
                new ScenarioAttackResponseData
                {
                    startsEncounter = true,
                };

            ScenarioDefinition scenario = new GameplayScenarioAssembler()
                .Assemble(content, CreateLevel())
                .Scenario;

            Assert.That(
                scenario.TryGetAttackResponse(
                    "actor.target",
                    out AttackResponseDefinition actorResponse),
                Is.True);
            Assert.That(actorResponse.StartsEncounter, Is.True);
            Assert.That(
                scenario.TryGetAttackResponse(
                    "prop.one",
                    out AttackResponseDefinition propResponse),
                Is.True);
            Assert.That(propResponse.StartsEncounter, Is.True);
            Assert.That(
                scenario.TryGetAttackResponse(
                    "objective.owner",
                    out _),
                Is.False,
                "Unconfigured level geometry must remain inert.");
        }

        [Test]
        public void PartiallyAuthoredCharacterProfileIsRejected()
        {
            ScenarioContentDocument content = CreateContent();
            content.actors[1].characterProfile =
                new ScenarioCharacterProfileData
                {
                    displayName = "Incomplete target",
                };

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    new GameplayScenarioAssembler().Assemble(
                        content,
                        CreateLevel()));

            Assert.That(exception.Message,
                Does.Contain("character identity cannot be empty"));
        }

        [Test]
        public void CharacterProfileMissingCoreAttributeIsRejected()
        {
            ScenarioContentDocument content = CreateContent();
            content.actors[1].characterProfile.attributes.RemoveAt(3);

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    new GameplayScenarioAssembler().Assemble(
                        content,
                        CreateLevel()));

            Assert.That(exception.Message,
                Does.Contain(CoreAttributeIds.Charisma));
        }

        [Test]
        public void CharacterProfileMissingCloseQuartersSkillIsRejected()
        {
            ScenarioContentDocument content = CreateContent();
            content.actors[1].characterProfile.skills.Clear();

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    new GameplayScenarioAssembler().Assemble(
                        content,
                        CreateLevel()));

            Assert.That(exception.Message,
                Does.Contain(CharacterSkillIds.CloseQuarters));
        }

        [Test]
        public void ControlTalentMustBelongToCharacterProfile()
        {
            ScenarioContentDocument content = CreateContent();
            content.actors[1].control.talentId = "talent.unowned";
            content.actors[1].control.talentModifier = 2;

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    new GameplayScenarioAssembler().Assemble(
                        content,
                        CreateLevel()));

            Assert.That(exception.Message,
                Does.Contain("is not owned"));
        }

        [Test]
        public void ImmediateAttackWithoutAccuracyDecayIsRejected()
        {
            ScenarioContentDocument content = CreateContent();
            content.actors[0].attackCapability.accuracyDecay = null;

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    new GameplayScenarioAssembler().Assemble(
                        content,
                        CreateLevel()));

            Assert.That(exception.Message,
                Does.Contain("requires an accuracy-decay function"));
        }

        [Test]
        public void AssemblerBuildsContactAttackWithoutRangedAccuracyDecay()
        {
            ScenarioContentDocument content = CreateContent();
            ScenarioAttackCapabilityData attack =
                content.actors[0].attackCapability;
            attack.actionId = "attack.combat-knife";
            attack.displayName = "Knife strike";
            attack.accuracyDecay = null;
            attack.contact = new ScenarioContactAttackData
            {
                enabled = true,
                maximumReach = 2f,
            };

            AttackDefinition definition = new GameplayScenarioAssembler()
                .Assemble(content, CreateLevel())
                .GetActorDefinition("actor.player")
                .Attack;

            Assert.That(definition.Contact, Is.Not.Null);
            Assert.That(definition.Contact.MaximumReach, Is.EqualTo(2f));
            Assert.That(definition.AccuracyDecay,
                Is.SameAs(AccuracyDecayDefinition.None));
            Assert.That(definition.CanTargetWorldPoint, Is.False);
        }

        [Test]
        public void ContactAttackRejectsRangedDecayAndInvalidReach()
        {
            ScenarioContentDocument content = CreateContent();
            ScenarioAttackCapabilityData attack =
                content.actors[0].attackCapability;
            attack.contact = new ScenarioContactAttackData
            {
                enabled = true,
                maximumReach = 0f,
            };

            InvalidOperationException combined =
                Assert.Throws<InvalidOperationException>(() =>
                    new GameplayScenarioAssembler().Assemble(
                        content,
                        CreateLevel()));
            Assert.That(combined.Message,
                Does.Contain("cannot author ranged accuracy decay"));

            attack.accuracyDecay = null;
            InvalidOperationException invalidReach =
                Assert.Throws<InvalidOperationException>(() =>
                    new GameplayScenarioAssembler().Assemble(
                        content,
                        CreateLevel()));
            Assert.That(invalidReach.Message,
                Does.Contain("contact attack maximum reach"));
        }

        [Test]
        public void AssemblerAuthorsProjectilePolicyOnlyWhenExplicitlyEnabled()
        {
            ScenarioContentDocument content = CreateContent();
            ScenarioAttackCapabilityData attack =
                content.actors[0].attackCapability;
            attack.projectile = new ScenarioProjectileCapabilityData
            {
                enabled = true,
                id = "projectile.rocket.synty",
                speedPerTurn = 4f,
                radius = 0.12f,
                maximumRange = 24f,
                standingLaunchHeight = 1.35f,
                crouchedLaunchHeight = 0.9f,
                opensEmergencyReactionWindow = true,
            };

            ProjectileFlightDefinition projectile =
                new GameplayScenarioAssembler()
                    .Assemble(content, CreateLevel())
                    .GetActorDefinition("actor.player")
                    .Attack.Projectile;

            Assert.That(projectile.Id, Is.EqualTo("projectile.rocket.synty"));
            Assert.That(projectile.SpeedPerTurn, Is.EqualTo(4f));
            Assert.That(projectile.StandingLaunchHeight, Is.EqualTo(1.35f));
            Assert.That(projectile.CrouchedLaunchHeight, Is.EqualTo(0.9f));
            Assert.That(projectile.OpensEmergencyReactionWindow, Is.True);
        }

        [Test]
        public void ProjectileIgnoresJsonUtilityDefaultAccuracyObject()
        {
            ScenarioContentDocument content = CreateContent();
            ScenarioAttackCapabilityData attack =
                content.actors[0].attackCapability;
            attack.accuracyDecay = new ScenarioAccuracyDecayData();
            attack.directFireDamage = new ScenarioDirectFireDamageData();
            attack.projectile = new ScenarioProjectileCapabilityData
            {
                enabled = true,
                id = "projectile.rocket.synty",
                speedPerTurn = 4f,
                radius = 0.12f,
                maximumRange = 24f,
                standingLaunchHeight = 1.35f,
                crouchedLaunchHeight = 0.9f,
                opensEmergencyReactionWindow = true,
            };

            AttackDefinition definition = new GameplayScenarioAssembler()
                .Assemble(content, CreateLevel())
                .GetActorDefinition("actor.player")
                .Attack;

            Assert.That(definition.Projectile, Is.Not.Null);
            Assert.That(definition.AccuracyDecay, Is.Null);
            Assert.That(definition.DirectFireDamage, Is.Null);
        }

        [Test]
        public void AssemblerBuildsAuthoredThrownExplosiveConsumable()
        {
            ScenarioContentDocument content = CreateContent();
            ScenarioActorContentData player = content.actors[0];
            player.attackCapability = null;
            player.inventory.Add(CreateGrenadeItem(quantity: 3));

            InventoryItemDefinition item = new GameplayScenarioAssembler()
                .Assemble(content, CreateLevel())
                .GetActorDefinition("actor.player")
                .GetInventoryItem("item.frag-grenade");

            Assert.That(item.Kind, Is.EqualTo(InventoryItemKind.Consumable));
            Assert.That(item.InitialQuantity, Is.EqualTo(3));
            Assert.That(item.ConsumablePower,
                Is.TypeOf<ThrownExplosiveDefinition>());
            var grenade = (ThrownExplosiveDefinition)item.ConsumablePower;
            Assert.That(grenade.TurnCost.ActionPoints, Is.EqualTo(2));
            Assert.That(grenade.MaximumRange, Is.EqualTo(12f));
            Assert.That(grenade.StandingLaunchHeight, Is.EqualTo(1.2f));
            Assert.That(grenade.CrouchedLaunchHeight, Is.EqualTo(0.82f));
            Assert.That(grenade.BlastRadius, Is.EqualTo(5f));
            Assert.That(grenade.BlastWoundMovementPenalty, Is.EqualTo(2f));
            Assert.That(grenade.BlastIntegrityDamage, Is.EqualTo(4f));
        }

        [Test]
        public void AssemblerBuildsAuthoredSmokeGrenadeConsumable()
        {
            ScenarioContentDocument content = CreateContent();
            ScenarioActorContentData player = content.actors[0];
            player.attackCapability = null;
            player.inventory.Add(CreateSmokeGrenadeItem());

            InventoryItemDefinition item = new GameplayScenarioAssembler()
                .Assemble(content, CreateLevel())
                .GetActorDefinition("actor.player")
                .GetInventoryItem("item.smoke-grenade");
            var grenade = (ThrownExplosiveDefinition)item.ConsumablePower;

            Assert.That(grenade.DeploysSmoke, Is.True);
            Assert.That(grenade.BlastRadius, Is.Zero);
            Assert.That(grenade.AreaRadius, Is.EqualTo(4f));
            Assert.That(grenade.SmokeField.Height, Is.EqualTo(2.8f));
            Assert.That(grenade.SmokeField.ExplorationDurationSeconds,
                Is.EqualTo(24f));
            Assert.That(grenade.SmokeField.DurationTurnEnds, Is.EqualTo(4));
            Assert.That(grenade.SmokeField.MinimumObscuredPath,
                Is.EqualTo(0.75f));
        }

        [Test]
        public void FragIgnoresJsonUtilityDefaultSmokeObject()
        {
            ScenarioContentDocument content = CreateContent();
            content.actors[0].attackCapability = null;
            ScenarioInventoryItemData item = CreateGrenadeItem(quantity: 3);
            item.consumablePower.thrownExplosive.smokeField =
                new ScenarioSmokeFieldData();
            content.actors[0].inventory.Add(item);

            var grenade = (ThrownExplosiveDefinition)
                new GameplayScenarioAssembler()
                    .Assemble(content, CreateLevel())
                    .GetActorDefinition("actor.player")
                    .GetInventoryItem("item.frag-grenade")
                    .ConsumablePower;

            Assert.That(grenade.SmokeField, Is.Null);
            Assert.That(grenade.BlastRadius, Is.EqualTo(5f));
        }

        [Test]
        public void ConsumableRequiresPositiveAuthoredQuantity()
        {
            ScenarioContentDocument content = CreateContent();
            content.actors[0].attackCapability = null;
            content.actors[0].inventory.Add(CreateGrenadeItem(quantity: 0));

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    new GameplayScenarioAssembler().Assemble(
                        content,
                        CreateLevel()));

            Assert.That(exception.Message,
                Does.Contain("quantity must be greater than zero"));
        }

        [Test]
        public void WeaponCannotAuthorConsumableQuantity()
        {
            ScenarioContentDocument content = CreateContent();
            ScenarioInventoryItemData weapon = CreateGrenadeItem(quantity: 2);
            weapon.kind = "weapon";
            weapon.consumablePower = null;
            weapon.attackCapability = content.actors[0].attackCapability;
            content.actors[0].attackCapability = null;
            content.actors[0].inventory.Add(weapon);

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    new GameplayScenarioAssembler().Assemble(
                        content,
                        CreateLevel()));

            Assert.That(exception.Message,
                Does.Contain("cannot author a consumable quantity"));
        }

        [Test]
        public void RuntimePoseOverridesBecomeAuthoritativeStartingPoses()
        {
            ScenarioContentDocument content = CreateContent();
            LevelDocument level = CreateLevel();
            var resolvedPose = new GameplayActorPose(
                new GameplayPosition(3f, 0.02f, 7f),
                135f,
                ActorStance.Crouched);
            var overrides = new System.Collections.Generic.Dictionary<
                string,
                GameplayActorPose>
            {
                ["actor.player"] = resolvedPose,
            };

            GameplayScenarioAssembly result =
                new GameplayScenarioAssembler()
                    .Assemble(content, level)
                    .WithResolvedActorPoses(overrides);

            Assert.That(
                result.GetActorDefinition("actor.player").StartingPose,
                Is.EqualTo(resolvedPose));
        }

        [Test]
        public void MismatchedLevelFailsBeforeRuntimeComposition()
        {
            ScenarioContentDocument content = CreateContent();
            LevelDocument level = CreateLevel();
            level.levelId = "different-level";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new GameplayScenarioAssembler().Assemble(content, level));

            Assert.That(exception.Message, Does.Contain("requires level"));
        }

        [Test]
        public void NonPositiveMinimumTurnDurationFailsAssembly()
        {
            ScenarioContentDocument content = CreateContent();
            content.timing.minimumVoluntaryTurnSeconds = 0f;

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    new GameplayScenarioAssembler().Assemble(
                        content,
                        CreateLevel()));

            Assert.That(exception.Message,
                Does.Contain("Minimum voluntary turn duration"));
        }

        [TestCase(ActionMobilityCodec.MobileValue, ActionMobility.Mobile)]
        [TestCase(ActionMobilityCodec.MomentumValue, ActionMobility.Momentum)]
        [TestCase(ActionMobilityCodec.SetValue, ActionMobility.Set)]
        public void EverySerializedMobilityAssembles(
            string serialized,
            ActionMobility expected)
        {
            ScenarioContentDocument content = CreateContent();
            content.objectives[0].turnCost.mobility = serialized;

            GameplayScenarioAssembly assembly =
                new GameplayScenarioAssembler().Assemble(
                    content,
                    CreateLevel());

            Assert.That(
                assembly.Scenario.Objectives[0].Interaction.TurnCost.Mobility,
                Is.EqualTo(expected));
        }

        [Test]
        public void VehicleStartingSpeedCannotExceedItsProfileMaximum()
        {
            ScenarioContentDocument content = CreateContent();
            content.vehicles[0].startingSpeed =
                content.vehicles[0].maximumSpeed + 1f;

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new GameplayScenarioAssembler().Assemble(
                    content,
                    CreateLevel()));
        }

        [Test]
        public void AssemblerBuildsAuthoredEnemyCombatPolicy()
        {
            ScenarioContentDocument content = CreateContent();
            content.actors[1].combat = new ScenarioActorCombatData
            {
                allegianceId = "raider",
                hostileAllegianceIds = { "player" },
                maximumWounds = 2,
                enemyBehavior = new ScenarioEnemyBehaviorData
                {
                    behaviorId = "behavior.rifleman",
                    perceptionRange = 30f,
                    viewAngleDegrees = 120f,
                    preferredEngagementRange = 12f,
                    movementSearchRadius = 6f,
                    maximumAttacksPerTurn = 1,
                    minimumAttackHitChancePercent = 40,
                },
            };
            content.actors[1].attackCapability = content.actors[0]
                .attackCapability;

            ScenarioActorDefinition enemy = new GameplayScenarioAssembler()
                .Assemble(content, CreateLevel())
                .GetActorDefinition("actor.target");

            Assert.That(enemy.Combat.AllegianceId, Is.EqualTo("raider"));
            Assert.That(enemy.Combat.MaximumWounds, Is.EqualTo(2));
            Assert.That(enemy.Combat.IsHostileTo("player"), Is.True);
            Assert.That(enemy.Combat.EnemyBehavior.BehaviorId,
                Is.EqualTo("behavior.rifleman"));
            Assert.That(enemy.Combat.EnemyBehavior.PerceptionRange,
                Is.EqualTo(30f));
            Assert.That(enemy.Combat.EnemyBehavior.ViewAngleDegrees,
                Is.EqualTo(120f));
            Assert.That(
                enemy.Combat.EnemyBehavior.MinimumAttackHitChancePercent,
                Is.EqualTo(40));
        }

        private static ScenarioInventoryItemData CreateGrenadeItem(
            int quantity) =>
            new ScenarioInventoryItemData
            {
                id = "item.frag-grenade",
                displayName = "Frag Grenade",
                hotbarSlot = 3,
                kind = "consumable",
                quantity = quantity,
                equipmentCost = new ScenarioActionCostData
                {
                    mobility = "mobile",
                },
                equippedEffects = new ScenarioEquipmentEffectData(),
                consumablePower = new ScenarioConsumablePowerData
                {
                    type = ThrownExplosiveDefinition.TypeId,
                    thrownExplosive = new ScenarioThrownExplosiveData
                    {
                        turnCost = new ScenarioActionCostData
                        {
                            actionPoints = 2,
                            mobility = "mobile",
                        },
                        maximumRange = 12f,
                        standingLaunchHeight = 1.2f,
                        crouchedLaunchHeight = 0.82f,
                        baseUncertaintyRadius = 0.75f,
                        uncertaintyPerMeter = 0.12f,
                        blastRadius = 5f,
                        blastWoundMovementPenalty = 2f,
                        blastIntegrityDamage = 4f,
                    },
                },
            };

        private static ScenarioInventoryItemData CreateSmokeGrenadeItem() =>
            new ScenarioInventoryItemData
            {
                id = "item.smoke-grenade",
                displayName = "Smoke Grenade",
                hotbarSlot = 6,
                kind = "consumable",
                quantity = 2,
                equipmentCost = new ScenarioActionCostData
                {
                    mobility = "mobile",
                },
                equippedEffects = new ScenarioEquipmentEffectData(),
                consumablePower = new ScenarioConsumablePowerData
                {
                    type = ThrownExplosiveDefinition.TypeId,
                    thrownExplosive = new ScenarioThrownExplosiveData
                    {
                        turnCost = new ScenarioActionCostData
                        {
                            actionPoints = 2,
                            mobility = "mobile",
                        },
                        maximumRange = 12f,
                        standingLaunchHeight = 1.2f,
                        crouchedLaunchHeight = 0.82f,
                        baseUncertaintyRadius = 0.55f,
                        uncertaintyPerMeter = 0.08f,
                        smokeField = new ScenarioSmokeFieldData
                        {
                            radius = 4f,
                            height = 2.8f,
                            explorationDurationSeconds = 24f,
                            durationTurnEnds = 4,
                            minimumObscuredPath = 0.75f,
                        },
                    },
                },
            };

        private static ScenarioContentDocument CreateContent()
        {
            return new ScenarioContentDocument
            {
                scenarioId = "scenario.test",
                levelId = "level.test",
                playerParty = new ScenarioPlayerPartyData
                {
                    actorIds = { "actor.player" },
                    initiallySelectedActorId = "actor.player",
                },
                primaryTargetActorId = "actor.target",
                primaryObjectiveId = "objective.one",
                randomSeed = 42u,
                timing = new ScenarioTimingData
                {
                    minimumVoluntaryTurnSeconds = 2.5f,
                },
                actors =
                {
                    CreateActor("actor.player", "actor.player.default", 4, 80f),
                    CreateActor("actor.target", "actor.target.capsule", 3, 75f),
                },
                objectives =
                {
                    new ScenarioObjectiveContentData
                    {
                        id = "objective.one",
                        levelInteractionPointId = "interaction.one",
                        levelInteractionPointType = "objective",
                        actionId = "objective.secure",
                        displayName = "Secure objective",
                        activeHudText = "SECURE THE OBJECTIVE",
                        completedHudText = "OBJECTIVE SECURED",
                        turnCost = new ScenarioActionCostData
                        {
                            actionPoints = 1,
                            movementOpportunity = 2f,
                            mobility = "set",
                        },
                    },
                },
                props =
                {
                    new ScenarioPropContentData
                    {
                        entityId = "prop.one",
                        mass = 35f,
                        toppling = new ScenarioPropTopplingData
                        {
                            enabled = true,
                            rollOffsetDegrees = 90f,
                            elevationOffset = 0.45f,
                        },
                        pinning = new ScenarioPropPinningData
                        {
                            enabled = true,
                            maximumActorMass = 90f,
                            minimumContactDepth = 0.05f,
                        },
                    },
                },
                vehicles =
                {
                    new ScenarioVehicleContentData
                    {
                        entityId = "vehicle.one",
                        maximumSpeed = 10f,
                        accelerationPerTurn = 4f,
                        brakingPerTurn = 2f,
                        lowSpeedTurnDegrees = 75f,
                        highSpeedTurnDegrees = 25f,
                        baseTurningRadius = 0.6f,
                        speedTurningRadiusFactor = 0.16f,
                        startingSpeed = 4f,
                    },
                },
            };
        }

        private static ScenarioActorContentData CreateActor(
            string id,
            string presentationId,
            int dexterity,
            float mass)
        {
            return new ScenarioActorContentData
            {
                id = id,
                displayName = id,
                presentationId = presentationId,
                position = new Float3Data(0f, 0f, 0f),
                stance = "standing",
                turnBudget = new ScenarioTurnBudgetData
                {
                    actionPoints = 4,
                },
                mass = mass,
                control = new ScenarioControlProfileData(),
                characterProfile = new ScenarioCharacterProfileData
                {
                    identityId = "character." + id,
                    displayName = id,
                    archetype = "Test Actor",
                    attributes =
                    {
                        new ScenarioCharacterRatingData
                        {
                            id = CoreAttributeIds.Strength,
                            rating = 3,
                        },
                        new ScenarioCharacterRatingData
                        {
                            id = CoreAttributeIds.Dexterity,
                            rating = dexterity,
                        },
                        new ScenarioCharacterRatingData
                        {
                            id = CoreAttributeIds.Grit,
                            rating = 3,
                        },
                        new ScenarioCharacterRatingData
                        {
                            id = CoreAttributeIds.Charisma,
                            rating = 2,
                        },
                    },
                    skills =
                    {
                        new ScenarioCharacterRatingData
                        {
                            id = CharacterSkillIds.CloseQuarters,
                            rating = 4,
                        },
                    },
                },
                displacementAbility = id == "actor.player"
                    ? new ScenarioDisplacementAbilityData
                    {
                        id = "ability.displace",
                        displayName = "Displace",
                        hotbarSlot = 4,
                        actions = new System.Collections.Generic.List<
                            ScenarioDisplacementActionData>
                        {
                            new ScenarioDisplacementActionData
                            {
                                id = "close-quarters.push",
                                displayName = "Push",
                                intent = "push",
                                cost = new ScenarioActionCostData
                                {
                                    actionPoints = 1,
                                    movementOpportunity = 0f,
                                    mobility = "mobile",
                                },
                                acceptedSubjectKinds =
                                    new System.Collections.Generic.List<string>
                                    {
                                        "prop",
                                    },
                                reach = 2f,
                                maximumDistance = 3f,
                                maximumSubjectMass = 90f,
                                handRequirement = "one-hand-free",
                                autoStowPolicy = "allowed",
                                contestPolicy = "none",
                                allowedResults =
                                    new System.Collections.Generic.List<string>
                                    {
                                        "topple",
                                        "pin",
                                        "collision-damage",
                                    },
                            },
                            new ScenarioDisplacementActionData
                            {
                                id = "close-quarters.push-off",
                                displayName = "Push Off",
                                intent = "push-off",
                                cost = new ScenarioActionCostData
                                {
                                    actionPoints = 2,
                                    movementOpportunity = 0f,
                                    mobility = "set",
                                },
                                acceptedSubjectKinds =
                                    new System.Collections.Generic.List<string>
                                    {
                                        "prop",
                                    },
                                reach = 2f,
                                maximumDistance = 1.25f,
                                maximumSubjectMass = 40f,
                                handRequirement = "both-hands-free",
                                autoStowPolicy = "allowed",
                                contestPolicy = "none",
                                allowedResults =
                                    new System.Collections.Generic.List<string>
                                    {
                                        "release",
                                    },
                            },
                        },
                    }
                    : null,
                attackCapability = id == "actor.player"
                    ? new ScenarioAttackCapabilityData
                    {
                        enabled = true,
                        actionId = "attack.rifle",
                        displayName = "Fire rifle",
                        turnCost = new ScenarioActionCostData
                        {
                            actionPoints = 1,
                            movementOpportunity = 0f,
                            mobility = "set",
                        },
                        woundMovementPenalty = 2f,
                        accuracyDecay = new ScenarioAccuracyDecayData
                        {
                            halfLifeDistance = 60f,
                            minimumAccuracyPercent = 5f,
                        },
                    }
                    : null,
            };
        }

        private static LevelDocument CreateLevel()
        {
            var level = new LevelDocument
            {
                levelId = "level.test",
            };
            level.entities.Add(new LevelEntity
            {
                id = "objective.owner",
                archetypeId = "structure.floor.standard",
                transform = new LevelTransformData(
                    new Float3Data(10f, 3f, 5f),
                    90f),
                interactionPoints =
                {
                    new InteractionPointData
                    {
                        id = "interaction.one",
                        type = "objective",
                        localPosition = new Float3Data(1f, 0f, 2f),
                        radius = 1.5f,
                    },
                },
            });
            level.entities.Add(new LevelEntity
            {
                id = "prop.one",
                archetypeId = "prop.crate.standard",
            });
            level.entities.Add(new LevelEntity
            {
                id = "vehicle.one",
                archetypeId = "vehicle.buggy.standard",
            });
            return level;
        }
    }
}
