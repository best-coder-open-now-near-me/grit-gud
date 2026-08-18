using System;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Turns;

internal static class SimulationParityChecks
{
    public static void Verify()
    {
        VerifyStanceChange();
        VerifyDirectAttack();
        VerifyDestructibleDirectFire();
        VerifyEquipment();
        VerifyInteraction();
        VerifyThrownExplosive();
        VerifyThrownSmokeAndWorldTime();
        VerifyProjectileLifecycle();
        VerifyVehicleMovement();
        VerifyMovementRoute();
        VerifyCombatantDisplacement();
        VerifyPropDisplacement();
        VerifyPinAndRelease();
        VerifyNormalTurnEnd();
        VerifyTurnEndedSmokeDecay();
        VerifyVoluntaryTurnLifecycle();
        VerifyDirectEncounterCompletion();
        VerifyEncounterAndEmergencyLifecycle();
        VerifyEmergencyPayloadValidation();
    }

    private static void VerifyStanceChange()
    {
        GameplaySession gameplay = CreateGameplay(CreateRifle());
        Require(gameplay.BeginEncounter(), "Encounter did not begin.");
        GameplayCombatStateSnapshot previous =
            GameplayCombatStateCapture.Capture(gameplay);
        var live = new StanceChangeResolver(
            gameplay,
            new AllowStanceTransitions());
        Require(live.TryResolve(
                "player",
                ActorStance.Crouched,
                out StanceChangeRecord stance,
                out StanceChangeFailure failure,
                out string failureCode),
            "Stance change failed: " + failure + "/" + failureCode);
        GameplaySemanticTransition transition = CreateTransition(
            previous,
            new GameplayStanceTransitionPayload(stance));
        GameplayCombatStateSnapshot reduced = Reduce(previous, transition);
        gameplay.RecordSemanticTransition(transition.Identity);
        RequireExact(
            "stance change",
            reduced,
            GameplayCombatStateCapture.Capture(gameplay));
    }

    private static void VerifyDirectAttack()
    {
        AttackDefinition rifle = CreateRifle();
        GameplaySession gameplay = CreateGameplay(rifle);
        Require(gameplay.BeginEncounter(), "Encounter did not begin.");
        var live = new GameplayAttackSession(
            gameplay);
        var exposure = new TargetExposureSnapshot(
            "player",
            "enemy",
            new[]
            {
                new TargetRegionExposure(TargetRegionId.Torso, 5, 5),
            });
        Require(live.TryPrepareResolve(
                "player",
                exposure,
                out GameplayPreparedTransition<GameplayActionRecord> prepared,
                out AttackResolutionFailure failure),
            "Direct attack preparation failed: " + failure);
        GameplayCapabilityProfile profile = GameplayCapabilityProfiles.Attack(
            rifle,
            GameplaySemanticSubjectKind.Actor);
        GameplaySemanticTransition transition = CreateTransition(
            prepared.Previous,
            new GameplayWeaponTransitionPayload(profile, prepared.Record));
        GameplayCombatStateSnapshot reduced = Reduce(
            prepared.Previous,
            transition);
        live.CommitPrepared(prepared);
        gameplay.RecordSemanticTransition(transition.Identity);
        RequireExact(
            "direct attack",
            reduced,
            GameplayCombatStateCapture.Capture(gameplay));
    }

    private static void VerifyDestructibleDirectFire()
    {
        var rifle = new AttackDefinition(
            "attack.cover-breaker",
            "Fire at cover",
            new ActionCost(1, 0f, ActionMobility.Set),
            woundMovementPenalty: 2f,
            accuracyDecay: AccuracyDecayDefinition.None,
            directFireDamage: new DirectFireDamageDefinition(
                "damage.ballistic",
                baseIntegrityDamage: 2f));
        GameplaySession gameplay = CreateGameplay(rifle);
        Require(gameplay.BeginEncounter(), "Encounter did not begin.");
        var destructibles = new DestructiblePropSession(
            new[]
            {
                new DestructiblePropDefinition(
                    "cover-wall",
                    maximumIntegrity: 2f,
                    DestructiblePropState.Intact,
                    new GameplayPosition(0f, 0f, 3f)),
            },
            gameplay.Journal);
        var live = new GameplayAttackSession(
            gameplay,
            destructibles);
        var impact = new DirectFireImpactRecord(
            "cover-wall",
            "surface.wood",
            new GameplayPosition(0f, 1f, 3f),
            normalX: 0f,
            normalY: 0f,
            normalZ: -1f,
            gameplay.Journal.LastEntry?.Sequence ?? 0L);
        Require(live.TryPrepareDischarge(
                "player",
                "cover-wall",
                impact.Point,
                impact,
                out GameplayPreparedTransition<GameplayActionRecord> prepared,
                out AttackResolutionFailure failure),
            "Destructible direct fire preparation failed: " + failure);
        GameplayCapabilityProfile profile = GameplayCapabilityProfiles.Attack(
            rifle,
            GameplaySemanticSubjectKind.DestructibleProp);
        GameplaySemanticTransition transition = CreateTransition(
            prepared.Previous,
            new GameplayWeaponTransitionPayload(profile, prepared.Record));
        GameplayCombatStateSnapshot reduced = Reduce(
            prepared.Previous,
            transition);
        live.CommitPrepared(prepared);
        gameplay.RecordSemanticTransition(transition.Identity);
        RequireExact(
            "destructible direct fire",
            reduced,
            GameplayCombatStateCapture.Capture(gameplay, destructibles));
    }

    private static void VerifyEquipment()
    {
        AttackDefinition rifle = CreateRifle();
        var item = new InventoryItemDefinition(
            "weapon.rifle",
            "Rifle",
            hotbarSlot: 1,
            InventoryItemKind.Weapon,
            new ActionCost(1, 0f, ActionMobility.Set),
            new EquipmentEffectSet(0.9f),
            attack: rifle,
            occupiedHands: 2);
        GameplaySession gameplay = CreateGameplayWithInventory(
            item,
            objective: null);
        Require(gameplay.BeginEncounter(), "Encounter did not begin.");
        GameplayCombatStateSnapshot previous =
            GameplayCombatStateCapture.Capture(gameplay);
        var live = new GameplayEquipmentSession(gameplay);
        Require(live.TryResolve(
                "player",
                item.Id,
                equip: true,
                out GameplayActionRecord action,
                out EquipmentChangeFailure failure),
            "Equipment resolution failed: " + failure);
        var payload = new GameplayResolvedActionTransitionPayload(
            GameplayCapabilityProfiles.Equip(),
            action,
            item.EquippedEffects);
        GameplaySemanticTransition transition = CreateTransition(
            previous,
            payload);
        GameplayCombatStateSnapshot reduced = Reduce(previous, transition);
        gameplay.RecordSemanticTransition(transition.Identity);
        RequireExact(
            "equipment",
            reduced,
            GameplayCombatStateCapture.Capture(gameplay));
    }

    private static void VerifyInteraction()
    {
        var objective = new ScenarioObjectiveDefinition(
            "console",
            new GameplayPosition(0.5f, 0f, 0f));
        GameplaySession gameplay = CreateGameplayWithInventory(
            item: null,
            objective);
        Require(gameplay.BeginEncounter(), "Encounter did not begin.");
        GameplayCombatStateSnapshot previous =
            GameplayCombatStateCapture.Capture(gameplay);
        var live = new GameplayActionResolver(gameplay);
        Require(live.TryResolveInteraction(
                "player",
                objective.Id,
                out GameplayActionRecord action,
                out GameplayActionFailure failure),
            "Interaction resolution failed: " + failure);
        var payload = new GameplayResolvedActionTransitionPayload(
            GameplayCapabilityProfiles.Interact(),
            action);
        GameplaySemanticTransition transition = CreateTransition(
            previous,
            payload);
        GameplayCombatStateSnapshot reduced = Reduce(previous, transition);
        gameplay.RecordSemanticTransition(transition.Identity);
        RequireExact(
            "interaction",
            reduced,
            GameplayCombatStateCapture.Capture(gameplay));
    }

    private static void VerifyThrownExplosive()
    {
        var explosive = new ThrownExplosiveDefinition(
            "item.frag",
            new ActionCost(2, 0f, ActionMobility.Mobile),
            maximumRange: 10f,
            standingLaunchHeight: 1f,
            crouchedLaunchHeight: 0.7f,
            baseUncertaintyRadius: 0f,
            uncertaintyPerMeter: 0f,
            blastRadius: 2f,
            blastWoundMovementPenalty: 1f,
            blastIntegrityDamage: 1f);
        var item = new InventoryItemDefinition(
            explosive.Id,
            "Frag grenade",
            hotbarSlot: 3,
            InventoryItemKind.Consumable,
            new ActionCost(0, 0f, ActionMobility.Mobile),
            EquipmentEffectSet.None,
            consumablePower: explosive,
            occupiedHands: 0,
            initialQuantity: 2);
        GameplaySession gameplay = CreateGameplayWithInventory(
            item,
            objective: null);
        Require(gameplay.BeginEncounter(), "Encounter did not begin.");
        var destructibles = new DestructiblePropSession(
            new[]
            {
                new DestructiblePropDefinition(
                    "blast-crate",
                    maximumIntegrity: 1f,
                    DestructiblePropState.Intact,
                    new GameplayPosition(0f, 0f, 4.5f)),
            },
            gameplay.Journal);
        var evidence = new FixedExplosiveEvidence(new[]
        {
            new BlastEffectRecord(
                "enemy",
                BlastSubjectKind.Actor,
                distance: 1.5f,
                occlusionExposure: 1f,
                distanceFalloff: 1f,
                TargetRegionId.Torso),
            new BlastEffectRecord(
                "blast-crate",
                BlastSubjectKind.DestructibleProp,
                distance: 0.5f,
                occlusionExposure: 1f,
                distanceFalloff: 1f),
        });
        var live = new GameplayThrownExplosiveSession(
            gameplay,
            evidence,
            evidence,
            new GameplayBlastConsequenceResolver(gameplay, destructibles),
            new CenterUncertaintySampler());
        GameplayCombatStateSnapshot previous =
            GameplayCombatStateCapture.Capture(gameplay, destructibles);
        Require(live.TryPrepareThrowItem(
                "player",
                explosive.Id,
                new GameplayPosition(0f, 0f, 4f),
                out ThrownExplosiveRecord prepared,
                out ThrownExplosiveFailure prepareFailure),
            "Thrown explosive preparation failed: " + prepareFailure);
        Require(live.TryCommitPreparedThrow(
                prepared,
                out GameplayActionRecord action,
                out ThrownExplosiveFailure commitFailure),
            "Thrown explosive commit failed: " + commitFailure);
        var payload = new GameplayResolvedActionTransitionPayload(
            GameplayCapabilityProfiles.ThrowExplosive(explosive),
            action);
        GameplaySemanticTransition transition = CreateTransition(
            previous,
            payload);
        GameplayCombatStateSnapshot reduced = Reduce(previous, transition);
        gameplay.RecordSemanticTransition(transition.Identity);
        RequireExact(
            "thrown explosive",
            reduced,
            GameplayCombatStateCapture.Capture(gameplay, destructibles));
    }

    private static void VerifyThrownSmokeAndWorldTime()
    {
        var smokeDefinition = new SmokeFieldDefinition(
            radius: 2f,
            height: 2f,
            explorationDurationSeconds: 4f,
            durationTurnEnds: 4,
            minimumObscuredPath: 0.5f);
        var smoke = new ThrownExplosiveDefinition(
            "item.smoke",
            new ActionCost(1, 0f, ActionMobility.Mobile),
            maximumRange: 10f,
            standingLaunchHeight: 1f,
            crouchedLaunchHeight: 0.7f,
            baseUncertaintyRadius: 0f,
            uncertaintyPerMeter: 0f,
            blastRadius: 0f,
            smokeField: smokeDefinition);
        var item = new InventoryItemDefinition(
            smoke.Id,
            "Smoke grenade",
            hotbarSlot: 3,
            InventoryItemKind.Consumable,
            new ActionCost(0, 0f, ActionMobility.Mobile),
            EquipmentEffectSet.None,
            consumablePower: smoke,
            occupiedHands: 0,
            initialQuantity: 2);
        GameplaySession gameplay = CreateGameplayWithInventory(
            item,
            objective: null);
        var destructibles = new DestructiblePropSession(
            Array.Empty<DestructiblePropDefinition>(),
            gameplay.Journal);
        using (var smokeFields = new GameplaySmokeFieldSession(gameplay))
        {
            var evidence = new FixedExplosiveEvidence(
                Array.Empty<BlastEffectRecord>());
            var live = new GameplayThrownExplosiveSession(
                gameplay,
                evidence,
                evidence,
                new GameplayBlastConsequenceResolver(
                    gameplay,
                    destructibles),
                new CenterUncertaintySampler(),
                smokeFields);
            GameplayCombatStateSnapshot beforeThrow =
                GameplayCombatStateCapture.Capture(
                    gameplay,
                    destructibles,
                    smokeFields: smokeFields);
            Require(live.TryPrepareThrowItem(
                    "player",
                    smoke.Id,
                    new GameplayPosition(0f, 0f, 4f),
                    out ThrownExplosiveRecord prepared,
                    out ThrownExplosiveFailure prepareFailure),
                "Thrown smoke preparation failed: " + prepareFailure);
            Require(live.TryCommitPreparedThrow(
                    prepared,
                    out GameplayActionRecord action,
                    out ThrownExplosiveFailure commitFailure),
                "Thrown smoke commit failed: " + commitFailure);
            var payload = new GameplayResolvedActionTransitionPayload(
                GameplayCapabilityProfiles.ThrowExplosive(smoke),
                action);
            GameplaySemanticTransition transition = CreateTransition(
                beforeThrow,
                payload);
            GameplayCombatStateSnapshot reduced = Reduce(
                beforeThrow,
                transition);
            gameplay.RecordSemanticTransition(transition.Identity);
            RequireExact(
                "thrown smoke",
                reduced,
                GameplayCombatStateCapture.Capture(
                    gameplay,
                    destructibles,
                    smokeFields: smokeFields));

            GameplayCombatStateSnapshot beforeTime =
                GameplayCombatStateCapture.Capture(
                    gameplay,
                    destructibles,
                    smokeFields: smokeFields);
            gameplay.AdvanceContinuousTime(1f);
            smokeFields.AdvanceContinuousTime(1f);
            GameplaySemanticTransition time = CreateTransition(
                beforeTime,
                new GameplayWorldAdvanceTransitionPayload(
                    "player",
                    "continuous-time",
                    elapsedSeconds: 1f));
            GameplayCombatStateSnapshot reducedTime = Reduce(
                beforeTime,
                time);
            gameplay.RecordSemanticTransition(time.Identity);
            RequireExact(
                "continuous smoke time",
                reducedTime,
                GameplayCombatStateCapture.Capture(
                    gameplay,
                    destructibles,
                    smokeFields: smokeFields));
        }
    }

    private static void VerifyProjectileLifecycle()
    {
        var projectile = new ProjectileFlightDefinition(
            "projectile.check",
            speedPerTurn: 4f,
            radius: 0.1f,
            maximumRange: 12f,
            standingLaunchHeight: 1f,
            crouchedLaunchHeight: 0.7f,
            blastRadius: 2f,
            blastWoundMovementPenalty: 1f,
            blastIntegrityDamage: 1f);
        var launcher = new AttackDefinition(
            "attack.launcher",
            "Launch",
            new ActionCost(2, 0f, ActionMobility.Set),
            woundMovementPenalty: 2f,
            projectile);
        GameplaySession gameplay = CreateGameplay(launcher);
        Require(gameplay.BeginEncounter(), "Encounter did not begin.");
        var destructibles = new DestructiblePropSession(
            new[]
            {
                new DestructiblePropDefinition(
                    "impact-crate",
                    maximumIntegrity: 1f,
                    DestructiblePropState.Intact,
                    new GameplayPosition(1f, 0f, 5f)),
            },
            gameplay.Journal);
        var live = new GameplayProjectileSession(
            gameplay,
            new ClearThenImpactProjectileQuery(),
            new GameplayBlastConsequenceResolver(gameplay, destructibles));
        GameplayCombatStateSnapshot launchPrevious =
            GameplayCombatStateCapture.Capture(
                gameplay,
                destructibles,
                projectiles: live);
        Require(live.TryPrepareLaunch(
                "player",
                "enemy",
                gameplay.GetActor("enemy").Pose.Position,
                out GameplayPreparedTransition<GameplayActionRecord> prepared,
                out ProjectileLaunchFailure failure),
            "Projectile launch preparation failed: " + failure);
        GameplayCapabilityProfile launchProfile =
            GameplayCapabilityProfiles.Attack(
                launcher,
                GameplaySemanticSubjectKind.Actor);
        GameplaySemanticTransition launchTransition = CreateTransition(
            launchPrevious,
            new GameplayWeaponTransitionPayload(
                launchProfile,
                prepared.Record));
        GameplayCombatStateSnapshot reducedLaunch = Reduce(
            launchPrevious,
            launchTransition);
        live.CommitPreparedLaunch(prepared);
        gameplay.RecordSemanticTransition(launchTransition.Identity);
        RequireExact(
            "projectile launch",
            reducedLaunch,
            GameplayCombatStateCapture.Capture(
                gameplay,
                destructibles,
                projectiles: live));

        string projectileId = live.ProjectileIds[0];
        GameplayPreparedTransition<ProjectileAdvanceRecord> advance =
            live.PrepareAdvance(projectileId, turnTime: 1f);
        GameplaySemanticTransition advanceTransition = CreateTransition(
            advance.Previous,
            new GameplayProjectileAdvanceTransitionPayload(
                "player",
                advance.Record,
                destructiblesShareGameplayJournal: true));
        GameplayCombatStateSnapshot reducedAdvance = Reduce(
            advance.Previous,
            advanceTransition);
        live.CommitPreparedAdvance(advance);
        gameplay.RecordSemanticTransition(advanceTransition.Identity);
        RequireExact(
            "projectile advance",
            reducedAdvance,
            GameplayCombatStateCapture.Capture(
                gameplay,
                destructibles,
                projectiles: live));

        GameplayPreparedTransition<ProjectileAdvanceRecord> impact =
            live.PrepareAdvance(projectileId, turnTime: 1f);
        GameplaySemanticTransition impactTransition = CreateTransition(
            impact.Previous,
            new GameplayProjectileAdvanceTransitionPayload(
                "player",
                impact.Record,
                destructiblesShareGameplayJournal: true));
        GameplayCombatStateSnapshot reducedImpact = Reduce(
            impact.Previous,
            impactTransition);
        live.CommitPreparedAdvance(impact);
        gameplay.RecordSemanticTransition(impactTransition.Identity);
        RequireExact(
            "projectile blast impact",
            reducedImpact,
            GameplayCombatStateCapture.Capture(
                gameplay,
                destructibles,
                projectiles: live));
    }

    private static void VerifyVehicleMovement()
    {
        GameplaySession gameplay = CreateGameplay(CreateRifle());
        Require(gameplay.BeginEncounter(), "Encounter did not begin.");
        var profile = new VehicleMomentumProfile(
            maximumSpeed: 4f,
            accelerationPerTurn: 1f,
            brakingPerTurn: 1f,
            lowSpeedTurnDegrees: 90f,
            highSpeedTurnDegrees: 30f,
            baseTurningRadius: 0.25f,
            speedTurningRadiusFactor: 0.1f);
        var live = new VehicleMomentumSession(
            profile,
            new VehicleMomentumState(
                "vehicle.check",
                new GameplayPosition(0f, 0f, 0f),
                forwardDegrees: 0f,
                speed: 0f),
            gameplay.Journal);
        GameplayCombatStateSnapshot previous =
            GameplayCombatStateCapture.Capture(
                gameplay,
                vehicles: new[] { live });
        Require(live.TryResolvePath(
                new[]
                {
                    new GameplayPosition(0f, 0f, 0f),
                    new GameplayPosition(0f, 0f, 0.5f),
                },
                out VehicleMomentumRecord movement,
                out VehiclePathFailure failure),
            "Vehicle movement failed: " + failure);
        GameplaySemanticTransition transition = CreateTransition(
            previous,
            new GameplayVehicleMoveTransitionPayload("player", movement));
        GameplayCombatStateSnapshot reduced = Reduce(previous, transition);
        gameplay.RecordSemanticTransition(transition.Identity);
        RequireExact(
            "vehicle movement",
            reduced,
            GameplayCombatStateCapture.Capture(
                gameplay,
                vehicles: new[] { live }));
    }

    private static void VerifyMovementRoute()
    {
        GameplaySession gameplay = CreateGameplay(CreateRifle());
        Require(gameplay.BeginEncounter(), "Encounter did not begin.");
        GameplayActorSnapshot player = gameplay.GetActor("player");
        var route = new MovementRouteRecord(
            player.ActorId,
            player.Pose,
            player.TurnBudget,
            new[]
            {
                new MovementRouteSegmentRecord(
                    player.Pose.Position,
                    new GameplayPosition(1f, 0f, 0f),
                    movementCost: 1f,
                    playbackDurationSeconds: 0.25f),
            });
        GameplayCombatStateSnapshot previous =
            GameplayCombatStateCapture.Capture(gameplay);
        gameplay.CommitMovementRoute(route);
        gameplay.CompleteMovementResolution();
        GameplaySemanticTransition transition = CreateTransition(
            previous,
            new GameplayMoveTransitionPayload(
                GameplayCapabilityProfiles.GroundedMove(),
                route));
        GameplayCombatStateSnapshot reduced = Reduce(previous, transition);
        gameplay.RecordSemanticTransition(transition.Identity);
        RequireExact(
            "movement route",
            reduced,
            GameplayCombatStateCapture.Capture(gameplay));
    }

    private static void VerifyCombatantDisplacement()
    {
        var push = new DisplacementActionDefinition(
            "close-quarters.push-combatant",
            "Push",
            DisplacementActionKind.Push,
            new ActionCost(1, 0f, ActionMobility.Mobile),
            DisplacementSubjectKinds.Combatant,
            reach: 3f,
            maximumDistance: 3f,
            maximumSubjectMass: 100f,
            DisplacementHandRequirement.None,
            DisplacementAutoStowPolicy.Never,
            DisplacementContestPolicy.CloseQuartersControl,
            DisplacementResultPolicies.None);
        var ability = new DisplacementAbilityDefinition(
            "ability.displace",
            "Displace",
            hotbarSlot: 4,
            new[] { push });
        var player = new ScenarioActorDefinition(
            "player",
            10,
            new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
            new TurnBudget(4, 8f),
            attack: null,
            displacementAbility: ability);
        var target = new ScenarioActorDefinition(
            "enemy",
            0,
            new GameplayActorPose(new GameplayPosition(1f, 0f, 0f), 180f),
            new TurnBudget(4, 8f));
        GameplaySession gameplay = CreateSession(
            new[] { player, target },
            Array.Empty<ScenarioObjectiveDefinition>());
        Require(gameplay.BeginEncounter(), "Encounter did not begin.");
        var destructibles = new DestructiblePropSession(
            Array.Empty<DestructiblePropDefinition>(),
            gameplay.Journal);
        var control = new System.Collections.Generic.Dictionary<
            string,
            CloseQuartersControlProfile>(StringComparer.Ordinal)
        {
            ["player"] = new CloseQuartersControlProfile(
                3,
                5,
                "talent.leverage",
                2),
            ["enemy"] = new CloseQuartersControlProfile(3, 4),
        };
        var live = new GameplayDisplacementSession(
            gameplay,
            destructibles,
            new[]
            {
                new DisplacementSubjectDefinition(
                    "player",
                    DisplacementSubjectKind.Combatant,
                    mass: 80f),
                new DisplacementSubjectDefinition(
                    "enemy",
                    DisplacementSubjectKind.Combatant,
                    mass: 80f),
            },
            new AllowDisplacementPaths(),
            new QueueD20RollSource(8, 10),
            control);
        GameplayCombatStateSnapshot previous =
            GameplayCombatStateCapture.Capture(gameplay, destructibles);
        Require(live.TryDisplaceAction(
                "player",
                push.Id,
                "enemy",
                new GameplayPosition(2f, 0f, 0f),
                out GameplayActionRecord action,
                out _,
                out DisplacementResolutionFailure failure),
            "Combatant displacement failed: " + failure);
        var payload = new GameplayResolvedActionTransitionPayload(
            GameplayCapabilityProfiles.Displace(
                push,
                GameplaySemanticSubjectKind.Actor),
            action);
        GameplaySemanticTransition transition = CreateTransition(
            previous,
            payload);
        GameplayCombatStateSnapshot reduced = Reduce(previous, transition);
        gameplay.RecordSemanticTransition(transition.Identity);
        RequireExact(
            "combatant displacement",
            reduced,
            GameplayCombatStateCapture.Capture(gameplay, destructibles));
    }

    private static void VerifyPropDisplacement()
    {
        var actionDefinition = new DisplacementActionDefinition(
            "close-quarters.push",
            "Push",
            DisplacementActionKind.Push,
            new ActionCost(1, 0f, ActionMobility.Mobile),
            DisplacementSubjectKinds.Prop,
            reach: 3f,
            maximumDistance: 3f,
            maximumSubjectMass: 100f,
            DisplacementHandRequirement.None,
            DisplacementAutoStowPolicy.Never,
            DisplacementContestPolicy.None,
            DisplacementResultPolicies.None);
        var ability = new DisplacementAbilityDefinition(
            "ability.displace",
            "Displace",
            hotbarSlot: 4,
            new[] { actionDefinition });
        var player = new ScenarioActorDefinition(
            "player",
            10,
            new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
            new TurnBudget(4, 8f),
            attack: null,
            displacementAbility: ability);
        var target = new ScenarioActorDefinition(
            "enemy",
            0,
            new GameplayActorPose(new GameplayPosition(2f, 0f, 0f), 180f),
            new TurnBudget(4, 8f));
        GameplaySession gameplay = CreateSession(
            new[] { player, target },
            Array.Empty<ScenarioObjectiveDefinition>());
        Require(gameplay.BeginEncounter(), "Encounter did not begin.");
        var destructibles = new DestructiblePropSession(
            new[]
            {
                new DestructiblePropDefinition(
                    "crate",
                    maximumIntegrity: 10f,
                    DestructiblePropState.Intact,
                    new GameplayPosition(0f, 0f, 1f)),
            },
            gameplay.Journal);
        var live = new GameplayDisplacementSession(
            gameplay,
            destructibles,
            new[]
            {
                new DisplacementSubjectDefinition(
                    "player",
                    DisplacementSubjectKind.Combatant,
                    mass: 80f),
                new DisplacementSubjectDefinition(
                    "enemy",
                    DisplacementSubjectKind.Combatant,
                    mass: 80f),
                new DisplacementSubjectDefinition(
                    "crate",
                    DisplacementSubjectKind.Prop,
                    mass: 35f),
            },
            new AllowDisplacementPaths(),
            new ConstantD20RollSource());
        GameplayCombatStateSnapshot previous =
            GameplayCombatStateCapture.Capture(gameplay, destructibles);
        Require(live.TryDisplaceAction(
                "player",
                actionDefinition.Id,
                "crate",
                new GameplayPosition(0f, 0f, 2f),
                out GameplayActionRecord action,
                out _,
                out DisplacementResolutionFailure failure),
            "Prop displacement failed: " + failure);
        var payload = new GameplayResolvedActionTransitionPayload(
            GameplayCapabilityProfiles.Displace(
                actionDefinition,
                GameplaySemanticSubjectKind.DestructibleProp),
            action);
        GameplaySemanticTransition transition = CreateTransition(
            previous,
            payload);
        GameplayCombatStateSnapshot reduced = Reduce(previous, transition);
        gameplay.RecordSemanticTransition(transition.Identity);
        RequireExact(
            "prop displacement",
            reduced,
            GameplayCombatStateCapture.Capture(gameplay, destructibles));
    }

    private static void VerifyPinAndRelease()
    {
        var pin = new DisplacementActionDefinition(
            "close-quarters.pinning-push",
            "Pinning Push",
            DisplacementActionKind.Push,
            new ActionCost(1, 0f, ActionMobility.Mobile),
            DisplacementSubjectKinds.Prop,
            reach: 3f,
            maximumDistance: 3f,
            maximumSubjectMass: 100f,
            DisplacementHandRequirement.None,
            DisplacementAutoStowPolicy.Never,
            DisplacementContestPolicy.None,
            DisplacementResultPolicies.Topple
                | DisplacementResultPolicies.Pin);
        var release = new DisplacementActionDefinition(
            "close-quarters.push-off",
            "Push Off",
            DisplacementActionKind.PushOff,
            new ActionCost(1, 0f, ActionMobility.Mobile),
            DisplacementSubjectKinds.Prop,
            reach: 3f,
            maximumDistance: 3f,
            maximumSubjectMass: 100f,
            DisplacementHandRequirement.None,
            DisplacementAutoStowPolicy.Never,
            DisplacementContestPolicy.None,
            DisplacementResultPolicies.Release);
        var player = new ScenarioActorDefinition(
            "player",
            10,
            new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
            new TurnBudget(4, 8f),
            attack: null,
            displacementAbility: new DisplacementAbilityDefinition(
                "ability.player-displace",
                "Displace",
                hotbarSlot: 4,
                new[] { pin }));
        var target = new ScenarioActorDefinition(
            "enemy",
            0,
            new GameplayActorPose(new GameplayPosition(1f, 0f, 0f), 180f),
            new TurnBudget(4, 8f),
            attack: null,
            displacementAbility: new DisplacementAbilityDefinition(
                "ability.enemy-displace",
                "Displace",
                hotbarSlot: 4,
                new[] { release }));
        GameplaySession gameplay = CreateSession(
            new[] { player, target },
            Array.Empty<ScenarioObjectiveDefinition>());
        Require(gameplay.BeginEncounter(), "Encounter did not begin.");
        var destructibles = new DestructiblePropSession(
            new[]
            {
                new DestructiblePropDefinition(
                    "crate",
                    maximumIntegrity: 10f,
                    DestructiblePropState.Intact,
                    new GameplayPosition(0f, 0f, 1f)),
            },
            gameplay.Journal);
        var live = new GameplayDisplacementSession(
            gameplay,
            destructibles,
            new[]
            {
                new DisplacementSubjectDefinition(
                    "player",
                    DisplacementSubjectKind.Combatant,
                    mass: 80f),
                new DisplacementSubjectDefinition(
                    "enemy",
                    DisplacementSubjectKind.Combatant,
                    mass: 80f),
                new DisplacementSubjectDefinition(
                    "crate",
                    DisplacementSubjectKind.Prop,
                    mass: 35f,
                    toppling: new PropTopplingDefinition(0f, 90f, 0.5f),
                    pinning: new PropPinningDefinition(90f)),
            },
            new PinThenReleaseDisplacementPaths(),
            new ConstantD20RollSource());
        GameplayCombatStateSnapshot beforePin =
            GameplayCombatStateCapture.Capture(gameplay, destructibles);
        Require(live.TryDisplaceAction(
                "player",
                pin.Id,
                "crate",
                new GameplayPosition(0f, 0f, 2f),
                out GameplayActionRecord pinAction,
                out _,
                out DisplacementResolutionFailure pinFailure),
            "Pinning displacement failed: " + pinFailure);
        GameplaySemanticTransition pinTransition = CreateTransition(
            beforePin,
            new GameplayResolvedActionTransitionPayload(
                GameplayCapabilityProfiles.Displace(
                    pin,
                    GameplaySemanticSubjectKind.DestructibleProp),
                pinAction));
        Require(string.Equals(
                pinAction.Request.ActorId,
                "player",
                StringComparison.Ordinal),
            "Pinning action changed its acting actor.");
        Require(beforePin.Session.GetActor("enemy").Pose.FacingDegrees == 180f,
            "Pre-pin canonical target pose was already changed.");
        GameplayCombatStateSnapshot reducedPin = Reduce(
            beforePin,
            pinTransition);
        gameplay.RecordSemanticTransition(pinTransition.Identity);
        RequireExact(
            "pin establishment",
            reducedPin,
            GameplayCombatStateCapture.Capture(gameplay, destructibles));

        GameplayCombatStateSnapshot beforeTurn =
            GameplayCombatStateCapture.Capture(gameplay, destructibles);
        Require(gameplay.TryEndTurn(
                "player",
                out TurnEndFailure turnFailure),
            "Turn handoff before Push Off failed: " + turnFailure);
        GameplaySemanticTransition turn = CreateTransition(
            beforeTurn,
            new GameplayEndTurnTransitionPayload(
                "player",
                emergency: false));
        GameplayCombatStateSnapshot reducedTurn = Reduce(beforeTurn, turn);
        gameplay.RecordSemanticTransition(turn.Identity);
        RequireExact(
            "pin turn handoff",
            reducedTurn,
            GameplayCombatStateCapture.Capture(gameplay, destructibles));

        GameplayCombatStateSnapshot beforeRelease =
            GameplayCombatStateCapture.Capture(gameplay, destructibles);
        Require(live.TryDisplaceAction(
                "enemy",
                release.Id,
                "crate",
                new GameplayPosition(0f, 0.5f, 3f),
                out GameplayActionRecord releaseAction,
                out _,
                out DisplacementResolutionFailure releaseFailure),
            "Push Off failed: " + releaseFailure);
        GameplaySemanticTransition releaseTransition = CreateTransition(
            beforeRelease,
            new GameplayResolvedActionTransitionPayload(
                GameplayCapabilityProfiles.Displace(
                    release,
                    GameplaySemanticSubjectKind.DestructibleProp),
                releaseAction));
        GameplayCombatStateSnapshot reducedRelease = Reduce(
            beforeRelease,
            releaseTransition);
        gameplay.RecordSemanticTransition(releaseTransition.Identity);
        RequireExact(
            "pin release",
            reducedRelease,
            GameplayCombatStateCapture.Capture(gameplay, destructibles));
    }

    private static void VerifyNormalTurnEnd()
    {
        GameplaySession gameplay = CreateGameplay(CreateRifle());
        Require(gameplay.BeginEncounter(), "Encounter did not begin.");
        GameplayCombatStateSnapshot previous =
            GameplayCombatStateCapture.Capture(gameplay);
        Require(gameplay.TryEndTurn(
                "player",
                out TurnEndFailure failure),
            "Normal turn end failed: " + failure);
        GameplaySemanticTransition transition = CreateTransition(
            previous,
            new GameplayEndTurnTransitionPayload(
                "player",
                emergency: false));
        GameplayCombatStateSnapshot reduced = Reduce(previous, transition);
        gameplay.RecordSemanticTransition(transition.Identity);
        RequireExact(
            "normal turn end",
            reduced,
            GameplayCombatStateCapture.Capture(gameplay));
    }

    private static void VerifyTurnEndedSmokeDecay()
    {
        GameplaySession gameplay = CreateGameplay(CreateRifle());
        using (var smokeFields = new GameplaySmokeFieldSession(gameplay))
        {
            smokeFields.Deploy(new SmokeFieldRecord(
                "smoke.turn-decay",
                "player",
                "item.smoke",
                new GameplayPosition(0f, 0f, 2f),
                new SmokeFieldDefinition(
                    radius: 2f,
                    height: 2f,
                    explorationDurationSeconds: 4f,
                    durationTurnEnds: 4,
                    minimumObscuredPath: 0.5f)));
            Require(gameplay.BeginEncounter(), "Encounter did not begin.");
            GameplayCombatStateSnapshot previous =
                GameplayCombatStateCapture.Capture(
                    gameplay,
                    smokeFields: smokeFields);
            Require(gameplay.TryEndTurn(
                    "player",
                    out TurnEndFailure failure),
                "Smoke-decay turn end failed: " + failure);
            GameplaySemanticTransition transition = CreateTransition(
                previous,
                new GameplayEndTurnTransitionPayload(
                    "player",
                    emergency: false));
            GameplayCombatStateSnapshot reduced = Reduce(
                previous,
                transition);
            gameplay.RecordSemanticTransition(transition.Identity);
            RequireExact(
                "turn-ended smoke decay",
                reduced,
                GameplayCombatStateCapture.Capture(
                    gameplay,
                    smokeFields: smokeFields));
        }
    }

    private static void VerifyVoluntaryTurnLifecycle()
    {
        GameplaySession gameplay = CreateGameplay(CreateRifle());
        GameplayCombatStateSnapshot beforeEnter =
            GameplayCombatStateCapture.Capture(gameplay);
        Require(gameplay.TryEnterTurnMode(out TurnModeEntryFailure enterFailure),
            "Voluntary turn entry failed: " + enterFailure);
        GameplaySemanticTransition enter = CreateTransition(
            beforeEnter,
            new GameplaySessionControlTransitionPayload(
                "player",
                GameplaySemanticCapability.ChangeTurnMode,
                "enter"));
        GameplayCombatStateSnapshot reducedEnter = Reduce(beforeEnter, enter);
        gameplay.RecordSemanticTransition(enter.Identity);
        RequireExact(
            "voluntary turn entry",
            reducedEnter,
            GameplayCombatStateCapture.Capture(gameplay));

        GameplayCombatStateSnapshot beforeEnd =
            GameplayCombatStateCapture.Capture(gameplay);
        Require(gameplay.TryEndTurn(
                "player",
                out TurnEndFailure endFailure),
            "Voluntary turn end failed: " + endFailure);
        GameplaySemanticTransition end = CreateTransition(
            beforeEnd,
            new GameplayEndTurnTransitionPayload(
                "player",
                emergency: false));
        GameplayCombatStateSnapshot reducedEnd = Reduce(beforeEnd, end);
        gameplay.RecordSemanticTransition(end.Identity);
        RequireExact(
            "voluntary turn end",
            reducedEnd,
            GameplayCombatStateCapture.Capture(gameplay));

        GameplayCombatStateSnapshot beforeWorld =
            GameplayCombatStateCapture.Capture(gameplay);
        Require(gameplay.CompleteVoluntaryWorldTurn(),
            "Voluntary world turn did not complete.");
        GameplaySemanticTransition world = CreateTransition(
            beforeWorld,
            new GameplayWorldAdvanceTransitionPayload(
                "player",
                "voluntary-cycle"));
        GameplayCombatStateSnapshot reducedWorld = Reduce(beforeWorld, world);
        gameplay.RecordSemanticTransition(world.Identity);
        RequireExact(
            "voluntary world turn",
            reducedWorld,
            GameplayCombatStateCapture.Capture(gameplay));

        GameplayCombatStateSnapshot beforeExit =
            GameplayCombatStateCapture.Capture(gameplay);
        Require(gameplay.TryExitTurnMode(out TurnModeExitFailure exitFailure),
            "Voluntary turn exit failed: " + exitFailure);
        GameplaySemanticTransition exit = CreateTransition(
            beforeExit,
            new GameplaySessionControlTransitionPayload(
                "player",
                GameplaySemanticCapability.ChangeTurnMode,
                "exit",
                gameplay.Scenario.Timing.MinimumVoluntaryTurnSeconds));
        GameplayCombatStateSnapshot reducedExit = Reduce(beforeExit, exit);
        gameplay.RecordSemanticTransition(exit.Identity);
        RequireExact(
            "voluntary turn exit",
            reducedExit,
            GameplayCombatStateCapture.Capture(gameplay));

        GameplayCombatStateSnapshot beforeTime =
            GameplayCombatStateCapture.Capture(gameplay);
        gameplay.AdvanceContinuousTime(0.5f);
        GameplaySemanticTransition time = CreateTransition(
            beforeTime,
            new GameplayWorldAdvanceTransitionPayload(
                "player",
                "continuous-time",
                elapsedSeconds: 0.5f));
        GameplayCombatStateSnapshot reducedTime = Reduce(beforeTime, time);
        gameplay.RecordSemanticTransition(time.Identity);
        RequireExact(
            "voluntary reentry time",
            reducedTime,
            GameplayCombatStateCapture.Capture(gameplay));
    }

    private static void VerifyDirectEncounterCompletion()
    {
        GameplaySession gameplay = CreateGameplay(CreateRifle());
        Require(gameplay.BeginEncounter(), "Encounter did not begin.");
        GameplayCombatStateSnapshot previous =
            GameplayCombatStateCapture.Capture(gameplay);
        Require(gameplay.CompleteEncounter(),
            "Direct encounter completion failed.");
        GameplaySemanticTransition transition = CreateTransition(
            previous,
            new GameplaySessionControlTransitionPayload(
                "player",
                GameplaySemanticCapability.ChangeEncounter,
                "complete"));
        GameplayCombatStateSnapshot reduced = Reduce(previous, transition);
        gameplay.RecordSemanticTransition(transition.Identity);
        RequireExact(
            "direct encounter completion",
            reduced,
            GameplayCombatStateCapture.Capture(gameplay));
    }

    private static void VerifyEncounterAndEmergencyLifecycle()
    {
        GameplaySession gameplay = CreateGameplay(CreateRifle());
        GameplayCombatStateSnapshot beforeEncounter =
            GameplayCombatStateCapture.Capture(gameplay);
        Require(gameplay.BeginEncounter(), "Encounter did not begin.");
        GameplaySemanticTransition beginEncounter = CreateTransition(
            beforeEncounter,
            new GameplaySessionControlTransitionPayload(
                "player",
                GameplaySemanticCapability.ChangeEncounter,
                "begin"));
        GameplayCombatStateSnapshot reducedBegin = Reduce(
            beforeEncounter,
            beginEncounter);
        gameplay.RecordSemanticTransition(beginEncounter.Identity);
        RequireExact(
            "encounter begin",
            reducedBegin,
            GameplayCombatStateCapture.Capture(gameplay));

        GameplayCombatStateSnapshot beforeEmergency =
            GameplayCombatStateCapture.Capture(gameplay);
        gameplay.BeginEmergencyReaction(
            "player",
            new[] { "enemy" },
            actionPointAllowance: 1);
        GameplaySemanticTransition beginEmergency = CreateTransition(
            beforeEmergency,
            new GameplayEmergencyReactionTransitionPayload(
                "player",
                "begin",
                new[] { "enemy" },
                actionPointAllowance: 1));
        GameplayCombatStateSnapshot reducedEmergency = Reduce(
            beforeEmergency,
            beginEmergency);
        gameplay.RecordSemanticTransition(beginEmergency.Identity);
        RequireExact(
            "emergency begin",
            reducedEmergency,
            GameplayCombatStateCapture.Capture(gameplay));

        GameplayCombatStateSnapshot beforeEmergencyTurnEnd =
            GameplayCombatStateCapture.Capture(gameplay);
        Require(gameplay.TryEndEmergencyTurn(
                "enemy",
                out bool passCompleted,
                out TurnEndFailure emergencyFailure),
            "Emergency turn end failed: " + emergencyFailure);
        Require(passCompleted,
            "Single responder did not complete the emergency pass.");
        GameplaySemanticTransition emergencyTurnEnd = CreateTransition(
            beforeEmergencyTurnEnd,
            new GameplayEndTurnTransitionPayload(
                "enemy",
                emergency: true));
        GameplayCombatStateSnapshot reducedEmergencyTurn = Reduce(
            beforeEmergencyTurnEnd,
            emergencyTurnEnd);
        gameplay.RecordSemanticTransition(emergencyTurnEnd.Identity);
        RequireExact(
            "emergency turn end",
            reducedEmergencyTurn,
            GameplayCombatStateCapture.Capture(gameplay));

        GameplayCombatStateSnapshot beforeEmergencyComplete =
            GameplayCombatStateCapture.Capture(gameplay);
        gameplay.CompleteEmergencyReaction("player");
        GameplaySemanticTransition completeEmergency = CreateTransition(
            beforeEmergencyComplete,
            new GameplayEmergencyReactionTransitionPayload(
                "player",
                "complete"));
        GameplayCombatStateSnapshot reducedComplete = Reduce(
            beforeEmergencyComplete,
            completeEmergency);
        gameplay.RecordSemanticTransition(completeEmergency.Identity);
        RequireExact(
            "emergency complete",
            reducedComplete,
            GameplayCombatStateCapture.Capture(gameplay));

        GameplayCombatStateSnapshot beforeRequest =
            GameplayCombatStateCapture.Capture(gameplay);
        Require(gameplay.RequestEncounterCompletionAtTurnEnd(),
            "Encounter completion request failed.");
        GameplaySemanticTransition request = CreateTransition(
            beforeRequest,
            new GameplaySessionControlTransitionPayload(
                "player",
                GameplaySemanticCapability.ChangeEncounter,
                "request-completion"));
        GameplayCombatStateSnapshot reducedRequest = Reduce(
            beforeRequest,
            request);
        gameplay.RecordSemanticTransition(request.Identity);
        RequireExact(
            "encounter completion request",
            reducedRequest,
            GameplayCombatStateCapture.Capture(gameplay));

        GameplayCombatStateSnapshot beforeEncounterEnd =
            GameplayCombatStateCapture.Capture(gameplay);
        Require(gameplay.TryEndTurn(
                "player",
                out TurnEndFailure encounterEndFailure),
            "Encounter-ending turn failed: " + encounterEndFailure);
        GameplaySemanticTransition encounterEnd = CreateTransition(
            beforeEncounterEnd,
            new GameplayEndTurnTransitionPayload(
                "player",
                emergency: false,
                gameplay.Scenario.Timing.MinimumVoluntaryTurnSeconds));
        GameplayCombatStateSnapshot reducedEncounterEnd = Reduce(
            beforeEncounterEnd,
            encounterEnd);
        gameplay.RecordSemanticTransition(encounterEnd.Identity);
        RequireExact(
            "encounter completion at turn end",
            reducedEncounterEnd,
            GameplayCombatStateCapture.Capture(gameplay));
    }

    private static void VerifyEmergencyPayloadValidation()
    {
        RequireThrows<ArgumentException>(() =>
            new GameplayEmergencyReactionTransitionPayload(
                "player",
                "begin",
                new[] { "enemy", "enemy" },
                actionPointAllowance: 1));
        RequireThrows<ArgumentException>(() =>
            new GameplayEmergencyReactionTransitionPayload(
                "player",
                "begin",
                new[] { "player" },
                actionPointAllowance: 1));
    }

    private static GameplaySemanticTransition CreateTransition(
        GameplayCombatStateSnapshot previous,
        GameplayTransitionPayload payload) => new GameplaySemanticTransition(
            new GameplayTransitionIdentity(
                previous.Session.LastTransitionSequence + 1L,
                payload.Profile.Capability.ToString(),
                payload.ActorId,
                payload.SubjectId),
            previous.CanonicalHash,
            payload);

    private static GameplayCombatStateSnapshot Reduce(
        GameplayCombatStateSnapshot previous,
        GameplaySemanticTransition transition) =>
        GameplaySimulationReducers.CreateCurrent().Reduce(
            previous,
            transition).Resulting;

    private static void RequireExact(
        string label,
        GameplayCombatStateSnapshot expected,
        GameplayCombatStateSnapshot actual)
    {
        var differences = GameplayCombatStateDiffer.Compare(expected, actual);
        if (differences.Count == 0) return;
        GameplayStateDifference first = differences[0];
        throw new InvalidOperationException(
            $"{label} live/reducer parity diverged at '{first.Path}': "
            + $"expected '{first.Expected}', actual '{first.Actual}'.");
    }

    private static GameplaySession CreateGameplay(AttackDefinition rifle)
    {
        var player = new ScenarioActorDefinition(
            "player",
            10,
            new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
            new TurnBudget(4, 8f),
            rifle);
        var enemy = new ScenarioActorDefinition(
            "enemy",
            0,
            new GameplayActorPose(new GameplayPosition(1f, 0f, 5f), 180f),
            new TurnBudget(4, 8f),
            rifle);
        return CreateSession(
            new[] { player, enemy },
            Array.Empty<ScenarioObjectiveDefinition>());
    }

    private static GameplaySession CreateGameplayWithInventory(
        InventoryItemDefinition item,
        ScenarioObjectiveDefinition objective)
    {
        var inventory = item == null
            ? Array.Empty<InventoryItemDefinition>()
            : new[] { item };
        var player = new ScenarioActorDefinition(
            "player",
            10,
            new GameplayActorPose(new GameplayPosition(0f, 0f, 0f), 0f),
            new TurnBudget(4, 8f),
            inventory,
            initiallyEquippedItemId: null);
        var enemy = new ScenarioActorDefinition(
            "enemy",
            0,
            new GameplayActorPose(new GameplayPosition(1f, 0f, 5f), 180f),
            new TurnBudget(4, 8f));
        return CreateSession(
            new[] { player, enemy },
            objective == null
                ? Array.Empty<ScenarioObjectiveDefinition>()
                : new[] { objective });
    }

    private static GameplaySession CreateSession(
        ScenarioActorDefinition[] actors,
        ScenarioObjectiveDefinition[] objectives)
    {
        var scenario = new ScenarioDefinition(
            "parity-check",
            new ScenarioTimingDefinition(1f),
            actors,
            objectives);
        return new GameplaySession(scenario, scenarioSeed: 0xC0FFEEu);
    }

    private static AttackDefinition CreateRifle() => new AttackDefinition(
        "attack.rifle",
        "Fire rifle",
        new ActionCost(1, 0f, ActionMobility.Set),
        woundMovementPenalty: 2f,
        accuracyDecay: AccuracyDecayDefinition.None);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void RequireThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(
            $"Expected {typeof(TException).Name} was not thrown.");
    }

    private sealed class AllowStanceTransitions : IStanceTransitionValidator
    {
        public StanceTransitionValidation Validate(
            GameplayActorSnapshot actor,
            ActorStance requestedStance) =>
            StanceTransitionValidation.Allowed();
    }

    private sealed class FixedExplosiveEvidence :
        IThrownExplosiveLandingQuery,
        IBlastWorldQuery
    {
        private readonly BlastEffectRecord[] effects;

        public FixedExplosiveEvidence(BlastEffectRecord[] resolvedEffects)
        {
            effects = resolvedEffects ?? throw new ArgumentNullException(
                nameof(resolvedEffects));
        }

        public ThrownExplosiveLandingResult Resolve(
            GameplayPosition launchOrigin,
            GameplayPosition sampledLanding) =>
            new ThrownExplosiveLandingResult(
                sampledLanding,
                worldStateRevision: 0L);

        public BlastWorldQueryResult Query(BlastWorldQuery query) =>
            new BlastWorldQueryResult(
                query,
                worldStateRevision: 0L,
                effects);
    }

    private sealed class CenterUncertaintySampler : IUncertaintySampler
    {
        public GameplayPosition Sample(
            GameplayPosition center,
            float radius,
            ScenarioRunIdentity run,
            GameplayTransitionIdentity transition,
            string purpose) => center;
    }

    private sealed class ClearThenImpactProjectileQuery :
        IProjectileSegmentQuery
    {
        private int queryCount;

        public ProjectileSegmentQueryResult Query(
            ProjectileSegmentQuery query)
        {
            queryCount++;
            if (queryCount == 1)
                return ProjectileSegmentQueryResult.Clear(
                    worldStateRevision: 0L);
            return ProjectileSegmentQueryResult.Collision(
                worldStateRevision: 0L,
                hitEntityId: "enemy",
                collisionFraction: 0.25f,
                blastEffects: new[]
                {
                    new BlastEffectRecord(
                        "enemy",
                        BlastSubjectKind.Actor,
                        distance: 0f,
                        occlusionExposure: 1f,
                        distanceFalloff: 1f,
                        TargetRegionId.Torso),
                    new BlastEffectRecord(
                        "impact-crate",
                        BlastSubjectKind.DestructibleProp,
                        distance: 0.5f,
                        occlusionExposure: 1f,
                        distanceFalloff: 1f),
                });
        }
    }

    private sealed class AllowDisplacementPaths :
        IDisplacementPathValidator
    {
        public DisplacementPathValidation Validate(
            DisplacementRequest request,
            GameplayPosition origin,
            PropDisplacementState resultingPropState) =>
            DisplacementPathValidation.Allowed();
    }

    private sealed class PinThenReleaseDisplacementPaths :
        IDisplacementPathValidator
    {
        public DisplacementPathValidation Validate(
            DisplacementRequest request,
            GameplayPosition origin,
            PropDisplacementState resultingPropState) =>
            request.ActionKind == DisplacementActionKind.PushOff
                ? DisplacementPathValidation.Allowed()
                : DisplacementPathValidation.Allowed(new[]
                {
                    new DisplacementContactEvidence(
                        "enemy",
                        new GameplayPosition(0.5f, 0.5f, 1.75f),
                        new GameplayPosition(0f, 1f, 0f),
                        0.1f),
                });
    }

    private sealed class ConstantD20RollSource : ID20RollSource
    {
        public int RollD20(
            GameplayTransitionIdentity transition,
            string purpose) => 10;
    }

    private sealed class QueueD20RollSource : ID20RollSource
    {
        private readonly System.Collections.Generic.Queue<int> rolls;

        public QueueD20RollSource(params int[] values)
        {
            rolls = new System.Collections.Generic.Queue<int>(values);
        }

        public int RollD20(
            GameplayTransitionIdentity transition,
            string purpose) => rolls.Dequeue();
    }
}
