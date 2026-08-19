using System.Linq;
using GritGud.Application.Gameplay;
using GritGud.Application.Levels;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Presentation.Gameplay;
using GritGud.Presentation.Levels;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class CommittedLevelLibraryIntegrationTests
    {
        [Test]
        public void EveryPublishedLevelIsEditableAndPlayable()
        {
            CommittedLevelLibrary library =
                UnityCommittedLevelLibrary.LoadDefault();

            Assert.That(library.Entries, Is.Not.Empty);
            foreach (CommittedLevelEntry entry in library.Entries)
            {
                Assert.That(
                    entry.CanEdit,
                    Is.True,
                    $"{entry.ResourceKey}: {entry.StatusMessage}");
                Assert.That(
                    entry.CanPlay,
                    Is.True,
                    $"{entry.ResourceKey}: {entry.StatusMessage}");
                Assert.DoesNotThrow(() => GameplayContentLoader.LoadCommitted(
                    library.OpenForPlay(entry.ResourceKey)));
            }
        }

        [Test]
        public void PublishedDepotEnforcesTacticalRuleAndBankedApLifecycle()
        {
            GameplayContentPackage content = GameplayContentLoader.LoadDefault();
            ScenarioDefinition scenario = content.Assembly.Scenario;

            Assert.That(content.Scenario.schemaVersion,
                Is.EqualTo(ScenarioContentDocument.CurrentSchemaVersion));
            Assert.That(scenario.Timing.ActionPointEconomy.StartingActionPoints,
                Is.EqualTo(4));
            Assert.That(scenario.Timing.ActionPointEconomy.IncomePerPersonalTurn,
                Is.EqualTo(4));
            Assert.That(scenario.Timing.ActionPointEconomy.MaximumHeldActionPoints,
                Is.EqualTo(6));
            Assert.That(content.Assembly.TacticalRules.Select(rule => rule.RuleId),
                Does.Contain("rule.ambush.direct-attack.actor"));

            var session = new GameplaySession(
                scenario,
                scenarioSeed: content.Assembly.RandomSeed);
            Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(4));
            Assert.That(session.EnterTurnMode(), Is.True);
            Assert.That(session.TryExitTurnMode(out _), Is.True);
            Assert.That(session.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(4), "UI mode changes cannot mint AP.");
            session.AdvanceContinuousTime(
                scenario.Timing.MinimumVoluntaryTurnSeconds);
            Assert.That(session.EnterTurnMode(), Is.True);
            string voluntaryActorId = session.ActiveActorId;
            Assert.That(session.GetActor(voluntaryActorId)
                .TurnBudget.ActionPoints, Is.EqualTo(4));
            Assert.That(session.TryEndTurn(voluntaryActorId, out _), Is.True);
            Assert.That(session.CompleteVoluntaryWorldTurn(), Is.True);
            Assert.That(session.GetActor(voluntaryActorId)
                .TurnBudget.ActionPoints,
                Is.EqualTo(6));
            Assert.That(session.LastCompletedVoluntaryTurnCycle
                .PersonalTurnStarts.Single(
                    start => start.ActorId == voluntaryActorId)
                .ActionPoints.CapWaste, Is.EqualTo(2));

            var encounter = new GameplaySession(
                scenario,
                scenarioSeed: content.Assembly.RandomSeed);
            Assert.That(encounter.BeginEncounter(), Is.True);
            Assert.That(encounter.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(4), "Encounter onset cannot mint AP.");
            Assert.That(encounter.CompleteEncounter(), Is.True);
            Assert.That(encounter.GetActor("player").TurnBudget.ActionPoints,
                Is.EqualTo(4), "Encounter completion cannot mint AP.");
        }

        [Test]
        public void DefaultPublishedLevelUsesStableIdentityAndDetachedSnapshots()
        {
            CommittedLevelLibrary library =
                UnityCommittedLevelLibrary.LoadDefault();
            CommittedLevelEntry entry = library.Find(
                UnityCommittedLevelLibrary.DefaultResourceKey);

            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.LevelId, Is.EqualTo("main-depot-yard-v1"));
            Assert.That(entry.DisplayName, Is.EqualTo("Depot Yard"));

            var first = library.OpenForEditing(entry.ResourceKey);
            Assert.That(first.schemaVersion, Is.EqualTo(LevelDocument.CurrentSchemaVersion));
            Assert.That(first.environment.practicalLights, Has.Count.EqualTo(5));
            Assert.That(first.dressing.decals, Has.Count.EqualTo(4));
            Assert.That(first.dressing.ambientVfx, Has.Count.EqualTo(3));
            Assert.That(first.dressing.audioZones, Has.Count.EqualTo(3));
            first.displayName = "Changed";

            Assert.That(
                library.OpenForEditing(entry.ResourceKey).displayName,
                Is.EqualTo("Depot Yard"));
        }

        [Test]
        public void DepotPublishesTopplingVerificationGroupAndSettledPile()
        {
            CommittedLevelLibrary library =
                UnityCommittedLevelLibrary.LoadDefault();
            LevelDocument level = library.OpenForEditing(
                UnityCommittedLevelLibrary.DefaultResourceKey);
            GameplayContentPackage content =
                GameplayContentLoader.LoadCommitted(level);

            Assert.That(level.groups.Any(group =>
                group.id == "toppling-verification"), Is.True);
            Assert.That(level.scenario.props.Select(prop => prop.entityId),
                Does.Contain("crate-exposure-demo"));
            Assert.That(level.scenario.props.Select(prop => prop.entityId),
                Does.Contain("barrel-yard-01"));
            Assert.That(level.scenario.props.Select(prop => prop.entityId),
                Does.Contain("crate-warehouse-03"));
            LevelEntity settledTop = level.entities.Single(entity =>
                entity.id == "crate-warehouse-03");
            Assert.That(settledTop.groupId,
                Is.EqualTo("toppling-verification"));
            Assert.That(settledTop.transform.position.y, Is.GreaterThan(0f));
            Assert.That(
                settledTop.transform.pitchDegrees != 0f
                    || settledTop.transform.rollDegrees != 0f,
                Is.True);
            Assert.That(content.Assembly.TryGetDisplacementSubject(
                "crate-warehouse-03",
                out DisplacementSubjectDefinition pileSubject), Is.True);
            Assert.That(pileSubject.Toppling, Is.Not.Null);
            Assert.That(content.FractureSpatialProfiles.Keys,
                Does.Contain("prop.crate.standard"));
            Assert.That(content.FractureSpatialProfiles.Keys,
                Does.Contain("prop.barrel.metal"));
            Assert.That(
                content.FractureSpatialProfiles.Values.All(profile =>
                    profile.ChunkCount == 12),
                Is.True);

            Assert.That(content.Assembly.TryGetDisplacementSubject(
                "barrel-yard-01",
                out DisplacementSubjectDefinition pinSubject), Is.True);
            Assert.That(pinSubject.Toppling, Is.Not.Null);
            Assert.That(pinSubject.Pinning, Is.Not.Null);
            Assert.That(pinSubject.Pinning.MaximumActorMass,
                Is.EqualTo(90f));
            Assert.That(
                content.Assembly.GetActorDefinition("player")
                    .GetDisplacementAction("close-quarters.push-off")
                    .AllowedResults,
                Is.EqualTo(DisplacementResultPolicies.Release));
            Assert.That(
                content.Assembly.GetActorDefinition("oren-vale")
                    .GetDisplacementAction("close-quarters.push-off")
                    .AllowedResults,
                Is.EqualTo(DisplacementResultPolicies.Release));
            Assert.That(
                content.Assembly.GetActorDefinition("depot-rifleman")
                    .GetDisplacementAction("close-quarters.push-off")
                    .AllowedResults,
                Is.EqualTo(DisplacementResultPolicies.Release));
        }

        [Test]
        public void DepotPartyBeginsOutsideRiflemanViewCone()
        {
            CommittedLevelLibrary library =
                UnityCommittedLevelLibrary.LoadDefault();
            GameplayContentPackage content = GameplayContentLoader.LoadCommitted(
                library.OpenForPlay(
                    UnityCommittedLevelLibrary.DefaultResourceKey));
            var gameplay = new GameplaySession(content.Assembly.Scenario);
            const string observerId = "depot-rifleman";

            foreach (string partyActorId in content.Assembly.PlayerParty.ActorIds)
            {
                GameplayActorSnapshot partyActor = gameplay.GetActor(
                    partyActorId);
                var fullyExposed = new TargetExposureSnapshot(
                    observerId,
                    partyActorId,
                    new[]
                    {
                        new TargetRegionExposure(
                            TargetRegionId.Torso,
                            visibleSampleCount: 1,
                            totalSampleCount: 1),
                    });
                EnemyAwarenessTransitionRecord observation = gameplay
                    .PrepareAwarenessTransition(
                        observerId,
                        new EncounterObservation(
                            observerId,
                            fullyExposed,
                            partyActor.Pose.Position));

                Assert.That(
                    observation.Resulting.State,
                    Is.EqualTo(EncounterAwarenessState.Unaware),
                    $"{partyActorId} begins inside the rifleman's view cone.");
                Assert.That(observation.Resulting.Suspicion, Is.Zero);
            }
        }

        [Test]
        public void DetectionScopeIncludesOnlyTheDetectedPartyMember()
        {
            CommittedLevelLibrary library =
                UnityCommittedLevelLibrary.LoadDefault();
            GameplayContentPackage content = GameplayContentLoader.LoadCommitted(
                library.OpenForPlay(
                    UnityCommittedLevelLibrary.DefaultResourceKey));
            var gameplay = new GameplaySession(content.Assembly.Scenario);

            var scope = gameplay.CreateDetectionEncounterScope(
                "depot-rifleman",
                "player");

            Assert.That(scope, Does.Contain("depot-rifleman"));
            Assert.That(scope, Does.Contain("player"));
            Assert.That(scope, Does.Not.Contain("oren-vale"));
            Assert.That(scope, Has.Count.EqualTo(2));
        }

        [Test]
        public void CombatEntryPresentationLocksAndRestoresPlayerInput()
        {
            CommittedLevelLibrary library =
                UnityCommittedLevelLibrary.LoadDefault();
            GameplayContentPackage content = GameplayContentLoader.LoadCommitted(
                library.OpenForPlay(
                    UnityCommittedLevelLibrary.DefaultResourceKey));
            var gameplay = new GameplaySession(content.Assembly.Scenario);
            var host = new GameObject("Combat Entry Presentation Test");
            try
            {
                GameplayInputController input =
                    host.AddComponent<GameplayInputController>();
                GameplayHud hud = host.AddComponent<GameplayHud>();
                GameplayPartyHud partyHud = host.AddComponent<GameplayPartyHud>();
                GameplayTacticalTransitionPresenter transition =
                    host.AddComponent<GameplayTacticalTransitionPresenter>();
                transition.Bind(
                    gameplay,
                    GameplayVisualTheme.LoadDefault(),
                    input,
                    hud,
                    partyHud);

                transition.BeginCombatEntry("depot-rifleman", "player");

                Assert.That(input.Suppressed, Is.True);
                Assert.That(hud.enabled, Is.False);
                Assert.That(partyHud.enabled, Is.False);

                transition.CompleteCombatEntry();

                Assert.That(input.Suppressed, Is.False);
                Assert.That(hud.enabled, Is.True);
                Assert.That(partyHud.enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
