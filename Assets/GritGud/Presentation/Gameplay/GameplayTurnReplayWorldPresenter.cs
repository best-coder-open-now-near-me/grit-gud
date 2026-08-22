using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class ReplayTimedPresentationEventCursor
    {
        private readonly HashSet<string> emitted = new HashSet<string>(
            StringComparer.Ordinal);

        public bool TryCross(
            string stableKey,
            float eventTimeSeconds,
            float previousTimeSeconds,
            float currentTimeSeconds) =>
            currentTimeSeconds > previousTimeSeconds
            && eventTimeSeconds >= previousTimeSeconds
            && eventTimeSeconds <= currentTimeSeconds
            && emitted.Add(stableKey);

        public void RebuildMark(
            string stableKey,
            float eventTimeSeconds,
            float timeSeconds)
        {
            if (eventTimeSeconds <= timeSeconds)
                emitted.Add(stableKey);
        }

        public void Clear() => emitted.Clear();
    }

    /// <summary>
    /// Presentation adapter over an exact reducer-verified trajectory. It
    /// samples immutable before/result states and never reconstructs gameplay.
    /// </summary>
    internal sealed class GameplayTurnReplayWorldPresenter : IDisposable
    {
        private const float PreEventNormalizedOffset = 0.0001f;

        private sealed class CrossedTimedEvent
        {
            public CrossedTimedEvent(
                GameplaySemanticReplayPlaybackFrame playbackFrame,
                ReplayCombatPresentationEvent presentationEvent,
                float timeSeconds,
                int order)
            {
                PlaybackFrame = playbackFrame;
                PresentationEvent = presentationEvent;
                TimeSeconds = timeSeconds;
                Order = order;
            }

            public GameplaySemanticReplayPlaybackFrame PlaybackFrame { get; }
            public ReplayCombatPresentationEvent PresentationEvent { get; }
            public float TimeSeconds { get; }
            public int Order { get; }
        }

        private sealed class ReplayCameraSubject
        {
            public ReplayCameraSubject(
                ReplayCombatPresentationSubjectKind kind,
                string id,
                Transform target)
            {
                Kind = kind;
                Id = string.IsNullOrWhiteSpace(id)
                    ? throw new ArgumentException(
                        "Replay camera subjects require an identifier.",
                        nameof(id))
                    : id;
                Target = target != null
                    ? target
                    : throw new ArgumentNullException(nameof(target));
            }

            public ReplayCombatPresentationSubjectKind Kind { get; }
            public string Id { get; }
            public Transform Target { get; }
            public string Key => Kind + ":" + Id;
        }

        private sealed class OptionalWorldProjection
        {
            private readonly string name;
            private readonly Action begin;
            private readonly Action<GameplayPresentationWorldStateSample> present;
            private readonly Action clearTransients;
            private readonly Action end;
            private readonly bool required;
            private bool active;
            private bool started;

            public OptionalWorldProjection(
                string projectionName,
                Action beginPresentation,
                Action<GameplayPresentationWorldStateSample> presentSample,
                Action clearPresentationTransients,
                Action endPresentation,
                bool isRequired = false)
            {
                name = projectionName;
                begin = beginPresentation ?? throw new ArgumentNullException(
                    nameof(beginPresentation));
                present = presentSample ?? throw new ArgumentNullException(
                    nameof(presentSample));
                clearTransients = clearPresentationTransients
                    ?? throw new ArgumentNullException(
                        nameof(clearPresentationTransients));
                end = endPresentation ?? throw new ArgumentNullException(
                    nameof(endPresentation));
                required = isRequired;
            }

            public void Begin()
            {
                active = true;
                started = true;
                try
                {
                    begin();
                }
                catch (Exception exception)
                {
                    if (required)
                        throw new InvalidOperationException(
                            $"Replay required {name} presentation could not start: "
                            + exception.Message,
                            exception);
                    Disable("start", exception);
                }
            }

            public void Present(GameplayPresentationWorldStateSample sample)
            {
                if (!active) return;
                try
                {
                    present(sample);
                }
                catch (Exception exception)
                {
                    if (required)
                        throw new InvalidOperationException(
                            $"Replay transition {sample.Frame.Transition.Identity.Sequence} "
                            + $"required {name} projection failed for "
                            + $"{sample.Frame.SemanticRecord.GetType().Name}: "
                            + exception.Message,
                            exception);
                    Disable("sample", exception);
                }
            }

            public void ClearTransients()
            {
                if (!active) return;
                try
                {
                    clearTransients();
                }
                catch (Exception exception)
                {
                    if (required)
                        throw new InvalidOperationException(
                            $"Replay required {name} transients could not clear: "
                            + exception.Message,
                            exception);
                    Disable("clear transients", exception);
                }
            }

            public void End()
            {
                if (!started) return;
                try
                {
                    end();
                }
                catch (Exception exception)
                {
                    Warn("restore", exception);
                }
                finally
                {
                    active = false;
                    started = false;
                }
            }

            private void Disable(string operation, Exception exception)
            {
                active = false;
                if (started)
                {
                    try
                    {
                        end();
                    }
                    catch (Exception restoreException)
                    {
                        Warn("restore after failure", restoreException);
                    }
                }
                started = false;
                Warn(operation, exception);
            }

            private void Warn(string operation, Exception exception) =>
                Debug.LogWarning(
                    $"Replay {name} presentation could not {operation} and "
                    + $"was disabled for this playback: {exception.Message}");
        }

        private readonly Dictionary<string, GameplayTurnReplayActorPresenter>
            actors = new Dictionary<string, GameplayTurnReplayActorPresenter>(
                StringComparer.Ordinal);
        private readonly List<OptionalWorldProjection> optionalProjections =
            new List<OptionalWorldProjection>();
        private readonly List<Behaviour> liveBehaviours =
            new List<Behaviour>();
        private readonly List<bool> liveBehaviourEnabled =
            new List<bool>();
        private readonly ReplayTimedPresentationEventCursor timedEventCursor =
            new ReplayTimedPresentationEventCursor();
        private readonly HashSet<string> presentedDischargeEvents =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> presentedProjectileImpactEvents =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> presentedReactionEvents =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> presentedIncapacitations =
            new HashSet<string>(StringComparer.Ordinal);
        private GameplayWorldRegistry world;
        private GameplayProjectileController projectiles;
        private GameplayThrownExplosiveController thrownExplosives;
        private GameplayDroneController drones;
        private GameplayInputController input;
        private GameplayTurnReplayHud hud;
        private GameplayHud gameplayHud;
        private GameplayPartyHud partyHud;
        private GameplayEnemyController enemies;
        private GameplayCameraRig camera;
        private GameplayReplayCameraSnapshot cameraSnapshot;
        private GameplayPresentationWorldStateSample replayCameraSample;
        private GameplaySemanticReplayPlaybackPosition replayCameraPosition;
        private bool hasReplayCameraPosition;
        private bool replayCameraAuto = true;
        private bool replayCameraFree;
        private string manualReplayCameraSubjectKey;
        private bool gameplayHudWasVisible;
        private bool partyHudWasSuppressed;
        private bool enemiesWerePaused;
        private float priorTimeScale;
        private bool presenting;

        internal int PresentedDischargeCount => presentedDischargeEvents.Count;
        internal int PresentedProjectileImpactCount =>
            presentedProjectileImpactEvents.Count;
        internal int PresentedReactionCount => presentedReactionEvents.Count;
        internal int PresentedIncapacitationCount =>
            presentedIncapacitations.Count;

        public void Bind(
            GameplayWorldRegistry registry,
            GameplayInputController inputController,
            GameplayTurnReplayHud replayHud,
            GameplayProjectileController projectileController,
            GameplayThrownExplosiveController thrownExplosiveController,
            GameplayDestructibleController destructibleController,
            GameplayVehicleController vehicleController,
            GameplayDroneController droneController,
            GameplaySmokeFieldController smokeController,
            GameplayFireFieldController fireController,
            GameplayHud liveGameplayHud,
            GameplayPartyHud livePartyHud,
            GameplayEnemyController enemyController,
            IEnumerable<Behaviour> behavioursToSuspend,
            GameplayCameraRig replayCamera = null)
        {
            Dispose();
            world = registry ?? throw new ArgumentNullException(nameof(registry));
            projectiles = projectileController;
            thrownExplosives = thrownExplosiveController;
            drones = droneController;
            input = inputController ?? throw new ArgumentNullException(
                nameof(inputController));
            hud = replayHud ?? throw new ArgumentNullException(nameof(replayHud));
            gameplayHud = liveGameplayHud ?? throw new ArgumentNullException(
                nameof(liveGameplayHud));
            partyHud = livePartyHud ?? throw new ArgumentNullException(
                nameof(livePartyHud));
            enemies = enemyController ?? throw new ArgumentNullException(
                nameof(enemyController));
            camera = replayCamera;
            foreach (Behaviour behaviour in behavioursToSuspend
                ?? throw new ArgumentNullException(nameof(behavioursToSuspend)))
            {
                if (behaviour == null)
                    throw new ArgumentException(
                        "Replay suspension cannot contain a null behaviour.",
                        nameof(behavioursToSuspend));
                if (!liveBehaviours.Contains(behaviour))
                    liveBehaviours.Add(behaviour);
            }
            RegisterOptionalProjections(
                projectileController,
                thrownExplosiveController,
                destructibleController,
                vehicleController,
                droneController,
                smokeController,
                fireController);
            hud.OpenChanged += HandleOpenChanged;
            hud.PlayheadChanged += HandlePlayheadChanged;
            hud.CameraCommandRequested += HandleCameraCommand;
            hud.BindPresentationPreflight(RequireReplayDependencies);
        }

        public void Dispose()
        {
            Restore();
            if (hud != null)
            {
                hud.ClearPresentationPreflight(RequireReplayDependencies);
                hud.OpenChanged -= HandleOpenChanged;
                hud.PlayheadChanged -= HandlePlayheadChanged;
                hud.CameraCommandRequested -= HandleCameraCommand;
            }
            world = null;
            projectiles = null;
            thrownExplosives = null;
            drones = null;
            input = null;
            hud = null;
            gameplayHud = null;
            partyHud = null;
            enemies = null;
            camera = null;
            cameraSnapshot = null;
            replayCameraSample = null;
            hasReplayCameraPosition = false;
            replayCameraAuto = true;
            replayCameraFree = false;
            manualReplayCameraSubjectKey = null;
            optionalProjections.Clear();
            liveBehaviours.Clear();
            liveBehaviourEnabled.Clear();
            timedEventCursor.Clear();
            presentedDischargeEvents.Clear();
            presentedProjectileImpactEvents.Clear();
            presentedReactionEvents.Clear();
            presentedIncapacitations.Clear();
        }

        private void HandleOpenChanged(bool open)
        {
            if (!open)
            {
                Restore();
                return;
            }
            if (hud.Playback == null)
                return;

            actors.Clear();
            timedEventCursor.Clear();
            presentedDischargeEvents.Clear();
            presentedProjectileImpactEvents.Clear();
            presentedReactionEvents.Clear();
            presentedIncapacitations.Clear();
            presenting = true;
            try
            {
                gameplayHudWasVisible = gameplayHud.IsVisible;
                partyHudWasSuppressed = partyHud.IsPresentationSuppressed;
                enemiesWerePaused = enemies.ReplayPaused;
                priorTimeScale = Time.timeScale;
                cameraSnapshot = camera?.CaptureReplaySnapshot();
                replayCameraAuto = true;
                replayCameraFree = false;
                manualReplayCameraSubjectKey = null;
                replayCameraSample = null;
                hasReplayCameraPosition = false;
                hud.SetReplayCameraState(
                    GameplayReplayCameraMode.Auto,
                    "AUTO");
                Time.timeScale = 0f;
                gameplayHud.Hide();
                partyHud.SetPresentationSuppressed(true);
                input.SetCameraOnly(true);
                enemies.SetReplayPaused(true);
                liveBehaviourEnabled.Clear();
                foreach (Behaviour behaviour in liveBehaviours)
                {
                    liveBehaviourEnabled.Add(behaviour.enabled);
                    behaviour.enabled = false;
                }
                foreach (GameplayActorView actor in world.Actors)
                {
                    var presenter = new GameplayTurnReplayActorPresenter(actor);
                    actors.Add(actor.ActorId, presenter);
                    presenter.Begin();
                }
                foreach (OptionalWorldProjection projection in
                    optionalProjections)
                {
                    projection.Begin();
                }
                Present();
                RebuildTimedEventCursor(hud.TimeSeconds);
            }
            catch
            {
                Restore();
                throw;
            }
        }

        private GameplayReplayPresentationCompatibility
            RequireReplayDependencies(
            GameplaySemanticReplayTimeline replay)
        {
            var matchedActorIds = new List<string>();
            var missingActorIds = new List<string>();
            foreach (GameplayActorSnapshot snapshot in replay.InitialState
                .Session.Actors)
            {
                if (world.TryGetActor(
                    snapshot.ActorId,
                    out GameplayActorView _))
                    matchedActorIds.Add(snapshot.ActorId);
                else
                    missingActorIds.Add(snapshot.ActorId);
            }
            bool containsProjectiles = replay.InitialState.Projectiles.Count > 0;
            bool containsThrownExplosives = false;
            foreach (GameplaySemanticReplayFrame frame in replay.Frames)
            {
                containsProjectiles |= frame.SemanticRecord
                    is ProjectileAdvanceRecord
                    || frame.Resulting.Projectiles.Count > 0;
                containsThrownExplosives |= ContainsThrownExplosive(
                    frame.SemanticRecord);
            }
            if (containsProjectiles && projectiles == null)
                throw new InvalidOperationException(
                    "Replay cannot open: the verified timeline contains "
                    + "projectile records but no GameplayProjectileController "
                    + "is bound for required projection.");
            if (containsThrownExplosives &&
                thrownExplosives?.Session == null)
            {
                throw new InvalidOperationException(
                    "Replay cannot open: the verified timeline contains "
                    + "thrown-explosive records but no bound "
                    + "GameplayThrownExplosiveController exists for required "
                    + "held, flight, and impact projection.");
            }
            bool containsDrones = replay.InitialState.Drones.Count > 0;
            foreach (GameplaySemanticReplayFrame frame in replay.Frames)
                containsDrones |= frame.Resulting.Drones.Count > 0;
            if (containsDrones && drones?.Session == null)
                throw new InvalidOperationException(
                    "Replay cannot open: the verified timeline contains "
                    + "drones but no bound GameplayDroneController exists for "
                    + "required projection.");
            return new GameplayReplayPresentationCompatibility(
                matchedActorIds,
                missingActorIds);
        }

        private void HandlePlayheadChanged(
            GameplayReplayPlayheadChange change)
        {
            if (!presenting)
                return;
            if (!change.PresentsTimedEvents)
            {
                ClearTransients();
                Present();
                RebuildTimedEventCursor(change.TimeSeconds);
                return;
            }
            PresentTimedEvents(
                change.PreviousTimeSeconds,
                change.TimeSeconds);
            Present();
        }

        private void Present()
        {
            GameplaySemanticReplayPlaybackTimeline playback = hud.Playback;
            if (playback == null)
                return;
            GameplaySemanticReplayPlaybackPosition position = playback.Locate(
                hud.TimeSeconds);
            Present(position, hud.TimeSeconds);
        }

        private void Present(
            GameplaySemanticReplayPlaybackPosition position,
            float timeSeconds)
        {
            GameplayPresentationWorldStateSample sample =
                GameplaySemanticReplaySampler.Sample(
                    position.Frame,
                    position.Progress);
            PresentActors(sample, position, timeSeconds);
            foreach (OptionalWorldProjection projection in optionalProjections)
                projection.Present(sample);
            PresentReplayCamera(sample, position);
        }

        private void PresentReplayCamera(
            GameplayPresentationWorldStateSample sample,
            GameplaySemanticReplayPlaybackPosition position)
        {
            replayCameraSample = sample;
            replayCameraPosition = position;
            hasReplayCameraPosition = true;
            if (camera == null)
                return;
            if (replayCameraFree)
                return;
            IReadOnlyList<ReplayCameraSubject> subjects =
                BuildReplayCameraSubjects(sample);
            if (subjects.Count == 0)
                return;
            ReplayCameraSubject selected = null;
            if (replayCameraAuto)
            {
                string actorId = position.Frame.Transition.Identity.ActorId;
                foreach (ReplayCameraSubject subject in subjects)
                    if (subject.Kind ==
                            ReplayCombatPresentationSubjectKind.Actor
                        && string.Equals(
                            subject.Id,
                            actorId,
                            StringComparison.Ordinal))
                    {
                        selected = subject;
                        break;
                    }
            }
            else
            {
                foreach (ReplayCameraSubject subject in subjects)
                    if (string.Equals(
                            subject.Key,
                            manualReplayCameraSubjectKey,
                            StringComparison.Ordinal))
                    {
                        selected = subject;
                        break;
                    }
            }
            selected ??= subjects[0];
            if (!replayCameraAuto)
                manualReplayCameraSubjectKey = selected.Key;
            camera.SetReplayTarget(selected.Target);
            hud.SetReplayCameraState(
                replayCameraAuto
                    ? GameplayReplayCameraMode.Auto
                    : GameplayReplayCameraMode.Subject,
                replayCameraAuto ? "AUTO" : selected.Id);
        }

        private IReadOnlyList<ReplayCameraSubject> BuildReplayCameraSubjects(
            GameplayPresentationWorldStateSample sample)
        {
            var subjects = new List<ReplayCameraSubject>();
            foreach (GameplayActorSnapshot actor in
                hud.Replay.InitialState.Session.Actors)
            {
                if (!sample.Actors.ContainsKey(actor.ActorId)
                    || !world.TryGetActor(
                        actor.ActorId,
                        out GameplayActorView view))
                    continue;
                subjects.Add(new ReplayCameraSubject(
                    ReplayCombatPresentationSubjectKind.Actor,
                    actor.ActorId,
                    view.Transform));
            }
            if (drones != null)
                foreach (SummonedDroneSnapshot drone in sample.Drones)
                    subjects.Add(new ReplayCameraSubject(
                        ReplayCombatPresentationSubjectKind.Drone,
                        drone.DroneId,
                        drones.GetPresentationTransform(drone.DroneId)));
            return subjects.AsReadOnly();
        }

        private void HandleCameraCommand(
            GameplayReplayCameraCommand command)
        {
            if (!presenting || camera == null || replayCameraSample == null
                || !hasReplayCameraPosition)
                return;
            if (command == GameplayReplayCameraCommand.Free)
            {
                replayCameraAuto = false;
                replayCameraFree = true;
                manualReplayCameraSubjectKey = null;
                camera.BeginReplayFreeCamera();
                hud.SetReplayCameraState(
                    GameplayReplayCameraMode.Free,
                    "FREE");
                return;
            }
            if (command == GameplayReplayCameraCommand.Auto)
            {
                camera.EndReplayFreeCamera();
                replayCameraAuto = true;
                replayCameraFree = false;
                manualReplayCameraSubjectKey = null;
                PresentReplayCamera(
                    replayCameraSample,
                    replayCameraPosition);
                return;
            }
            if (command != GameplayReplayCameraCommand.PreviousSubject
                && command != GameplayReplayCameraCommand.NextSubject)
                return;
            camera.EndReplayFreeCamera();
            replayCameraFree = false;
            IReadOnlyList<ReplayCameraSubject> subjects =
                BuildReplayCameraSubjects(replayCameraSample);
            if (subjects.Count == 0)
                return;
            int currentIndex = -1;
            for (int index = 0; index < subjects.Count; index++)
            {
                if ((!string.IsNullOrWhiteSpace(
                            manualReplayCameraSubjectKey)
                        && string.Equals(
                            subjects[index].Key,
                            manualReplayCameraSubjectKey,
                            StringComparison.Ordinal))
                    || (string.IsNullOrWhiteSpace(
                            manualReplayCameraSubjectKey)
                        && camera.Target == subjects[index].Target))
                {
                    currentIndex = index;
                    break;
                }
            }
            int direction = command ==
                    GameplayReplayCameraCommand.NextSubject
                ? 1
                : -1;
            int selectedIndex = currentIndex < 0
                ? direction > 0 ? 0 : subjects.Count - 1
                : (currentIndex + direction + subjects.Count)
                    % subjects.Count;
            replayCameraAuto = false;
            manualReplayCameraSubjectKey = subjects[selectedIndex].Key;
            PresentReplayCamera(replayCameraSample, replayCameraPosition);
        }

        private void PresentActors(
            GameplayPresentationWorldStateSample sample,
            GameplaySemanticReplayPlaybackPosition position,
            float timeSeconds)
        {
            var actionStates = new Dictionary<
                string,
                TurnReplayActorActionState>(StringComparer.Ordinal);
            foreach (TurnReplayActorActionState state in
                TurnReplayActorActionProjector.Project(
                    position.Frame,
                    position.Progress))
            {
                actionStates[state.ActorId] = state;
            }
            foreach (KeyValuePair<string, GameplayActorSnapshot> entry in
                sample.Actors)
            {
                if (!actors.TryGetValue(
                        entry.Key,
                        out GameplayTurnReplayActorPresenter actor))
                    throw new InvalidOperationException(
                        $"Replay transition {position.Frame.Transition.Identity.Sequence} "
                        + $"cannot project actor '{entry.Key}': no replay actor presenter exists.");
                actionStates.TryGetValue(
                    entry.Key,
                    out TurnReplayActorActionState action);
                TryResolveReplayLocomotion(
                    position,
                    entry.Key,
                    out Vector3 replayVelocity,
                    out bool replayGrounded);
                ReplayActorTerminalPoseSample terminalPose = hud.Playback
                    .SampleTerminalPose(entry.Key, timeSeconds);
                actor.Present(
                    entry.Value,
                    action,
                    position,
                    replayVelocity,
                    replayGrounded,
                    terminalPose);
            }
        }

        private void PresentTimedEvents(float previousTime, float currentTime)
        {
            GameplaySemanticReplayPlaybackTimeline playback = hud.Playback;
            if (playback == null || currentTime <= previousTime) return;
            var crossed = new List<CrossedTimedEvent>();
            int order = 0;
            foreach (GameplaySemanticReplayPlaybackFrame playbackFrame in
                playback.Frames)
            {
                if (playbackFrame.StartSeconds > currentTime
                    || playbackFrame.EndSeconds < previousTime)
                    continue;
                foreach (ReplayCombatPresentationEvent presentationEvent in
                    ReplayCombatPresentationEventProjector.Project(
                        playbackFrame.Frame))
                {
                    float eventTime = playbackFrame.StartSeconds
                        + (playbackFrame.DurationSeconds
                            * presentationEvent.NormalizedTime);
                    if (!timedEventCursor.TryCross(
                            presentationEvent.StableKey,
                            eventTime,
                            previousTime,
                            currentTime))
                        continue;
                    crossed.Add(new CrossedTimedEvent(
                        playbackFrame,
                        presentationEvent,
                        eventTime,
                        order++));
                }
            }
            crossed.Sort((left, right) =>
            {
                int timeOrder = left.TimeSeconds.CompareTo(right.TimeSeconds);
                return timeOrder != 0
                    ? timeOrder
                    : left.Order.CompareTo(right.Order);
            });
            foreach (CrossedTimedEvent crossedEvent in crossed)
            {
                float preEventTime = Mathf.Max(
                    crossedEvent.PlaybackFrame.StartSeconds,
                    crossedEvent.TimeSeconds
                        - crossedEvent.PlaybackFrame.DurationSeconds
                        * PreEventNormalizedOffset);
                Present(playback.Locate(preEventTime), preEventTime);
                PresentTimedEvent(crossedEvent.PresentationEvent);
            }
        }

        private void PresentTimedEvent(
            ReplayCombatPresentationEvent presentationEvent)
        {
            if (presentationEvent.Kind == ReplayCombatPresentationEventKind
                    .ThrownExplosiveRelease)
            {
                presentedDischargeEvents.Add(presentationEvent.StableKey);
                return;
            }
            if (presentationEvent.Kind == ReplayCombatPresentationEventKind
                    .ThrownExplosiveImpact)
            {
                presentedProjectileImpactEvents.Add(
                    presentationEvent.StableKey);
                return;
            }
            if (presentationEvent.Kind ==
                ReplayCombatPresentationEventKind.ProjectileImpact)
            {
                if (projectiles == null)
                    throw new InvalidOperationException(
                        $"Replay transition {presentationEvent.TransitionSequence} "
                        + $"cannot project impact for projectile "
                        + $"'{presentationEvent.ProjectileId}': no projectile presenter is bound.");
                projectiles.PresentReplayImpact(presentationEvent);
                presentedProjectileImpactEvents.Add(
                    presentationEvent.StableKey);
                return;
            }
            if (presentationEvent.Kind ==
                ReplayCombatPresentationEventKind.DroneCrashImpact)
            {
                presentedProjectileImpactEvents.Add(
                    presentationEvent.StableKey);
                return;
            }
            if (presentationEvent.Kind ==
                ReplayCombatPresentationEventKind.Reaction)
            {
                presentedReactionEvents.Add(presentationEvent.StableKey);
                return;
            }
            if (presentationEvent.Kind ==
                    ReplayCombatPresentationEventKind.Incapacitation
                || presentationEvent.Kind ==
                    ReplayCombatPresentationEventKind.Death)
            {
                presentedIncapacitations.Add(presentationEvent.StableKey);
                return;
            }

            if (presentationEvent.ShooterKind ==
                ReplayCombatPresentationSubjectKind.Drone)
            {
                if (drones == null)
                    throw new InvalidOperationException(
                        $"Replay transition {presentationEvent.TransitionSequence} "
                        + $"cannot project {presentationEvent.Kind} for drone "
                        + $"'{presentationEvent.ShooterId}': no drone presenter is bound.");
                drones.PresentReplayEvent(presentationEvent);
                presentedDischargeEvents.Add(presentationEvent.StableKey);
                return;
            }

            if (!actors.TryGetValue(
                    presentationEvent.ActorId,
                    out GameplayTurnReplayActorPresenter actor))
                throw new InvalidOperationException(
                    $"Replay transition {presentationEvent.TransitionSequence} "
                    + $"cannot project {presentationEvent.Kind} for actor "
                    + $"'{presentationEvent.ActorId}': no replay actor presenter exists.");
            actor.PresentEvent(presentationEvent);
            presentedDischargeEvents.Add(presentationEvent.StableKey);
        }

        private void RebuildTimedEventCursor(float timeSeconds)
        {
            timedEventCursor.Clear();
            GameplaySemanticReplayPlaybackTimeline playback = hud.Playback;
            if (playback == null) return;
            foreach (GameplaySemanticReplayPlaybackFrame playbackFrame in
                playback.Frames)
            {
                if (playbackFrame.StartSeconds > timeSeconds) break;
                foreach (ReplayCombatPresentationEvent presentationEvent in
                    ReplayCombatPresentationEventProjector.Project(
                        playbackFrame.Frame))
                {
                    float eventTime = playbackFrame.StartSeconds
                        + (playbackFrame.DurationSeconds
                            * presentationEvent.NormalizedTime);
                    timedEventCursor.RebuildMark(
                        presentationEvent.StableKey,
                        eventTime,
                        timeSeconds);
                }
            }
        }

        private static bool TryResolveReplayLocomotion(
            GameplaySemanticReplayPlaybackPosition position,
            string actorId,
            out Vector3 velocity,
            out bool grounded)
        {
            velocity = Vector3.zero;
            grounded = true;
            if (position.Progress >= 1f
                || !(position.Frame.SemanticRecord is MovementRouteRecord route)
                || !string.Equals(route.ActorId, actorId, StringComparison.Ordinal)
                || !GameplayMovementPresentationSampler.TrySample(
                    route,
                    route.TotalPlaybackDurationSeconds * position.Progress,
                    out GameplayMovementPresentationSample sampled)
                || sampled.SegmentIndex < 0
                || sampled.SegmentIndex >= route.Segments.Count)
            {
                return false;
            }

            MovementRouteSegmentRecord segment = route.Segments[
                sampled.SegmentIndex];
            float normalizedFrameDuration = position.PlaybackFrame
                .DurationSeconds / route.TotalPlaybackDurationSeconds;
            float segmentDuration = Mathf.Max(
                0.0001f,
                segment.PlaybackDurationSeconds * normalizedFrameDuration);
            velocity = new Vector3(
                (segment.To.X - segment.From.X) / segmentDuration,
                (segment.To.Y - segment.From.Y) / segmentDuration,
                (segment.To.Z - segment.From.Z) / segmentDuration);
            grounded = !segment.IsTraversal;
            return true;
        }

        private void ClearTransients()
        {
            foreach (GameplayTurnReplayActorPresenter actor in actors.Values)
                actor.ClearTransients();
            foreach (OptionalWorldProjection projection in optionalProjections)
                projection.ClearTransients();
        }

        private void Restore()
        {
            if (!presenting)
                return;
            Exception failure = null;
            foreach (GameplayTurnReplayActorPresenter actor in actors.Values)
                TryRestore(actor.Dispose, ref failure);
            for (int index = optionalProjections.Count - 1;
                index >= 0;
                index--)
            {
                optionalProjections[index].End();
            }
            if (cameraSnapshot != null)
            {
                GameplayReplayCameraSnapshot captured = cameraSnapshot;
                TryRestore(
                    () => camera?.RestoreReplaySnapshot(captured),
                    ref failure);
            }
            cameraSnapshot = null;
            replayCameraSample = null;
            hasReplayCameraPosition = false;
            replayCameraAuto = true;
            replayCameraFree = false;
            manualReplayCameraSubjectKey = null;
            TryRestore(() => input?.SetCameraOnly(false), ref failure);
            TryRestore(() => Time.timeScale = priorTimeScale, ref failure);
            TryRestore(
                () => partyHud?.SetPresentationSuppressed(
                    partyHudWasSuppressed),
                ref failure);
            if (gameplayHudWasVisible)
                TryRestore(() => gameplayHud?.Show(), ref failure);
            for (int index = 0;
                index < liveBehaviourEnabled.Count
                    && index < liveBehaviours.Count;
                index++)
            {
                int captured = index;
                TryRestore(
                    () => liveBehaviours[captured].enabled =
                        liveBehaviourEnabled[captured],
                    ref failure);
            }
            TryRestore(
                () => enemies?.SetReplayPaused(enemiesWerePaused),
                ref failure);
            liveBehaviourEnabled.Clear();
            actors.Clear();
            timedEventCursor.Clear();
            presenting = false;
            if (failure != null)
            {
                Debug.LogWarning(
                    "Replay restored its core state but an optional live "
                    + $"presentation detail failed to restore: {failure.Message}");
            }
        }

        private static void TryRestore(Action restore, ref Exception failure)
        {
            try
            {
                restore();
            }
            catch (Exception exception)
            {
                failure ??= exception;
            }
        }

        private void RegisterOptionalProjections(
            GameplayProjectileController projectileController,
            GameplayThrownExplosiveController thrownExplosiveController,
            GameplayDestructibleController destructibleController,
            GameplayVehicleController vehicleController,
            GameplayDroneController droneController,
            GameplaySmokeFieldController smokeController,
            GameplayFireFieldController fireController)
        {
            optionalProjections.Clear();
            if (projectileController != null)
            {
                optionalProjections.Add(new OptionalWorldProjection(
                    "projectile",
                    projectileController.BeginReplayPresentation,
                    sample => projectileController.PresentReplay(
                        sample.Projectiles),
                    projectileController.ClearReplayTransients,
                    projectileController.EndReplayPresentation,
                    isRequired: true));
            }
            if (thrownExplosiveController != null)
            {
                optionalProjections.Add(new OptionalWorldProjection(
                    "thrown-explosive",
                    thrownExplosiveController.BeginReplayPresentation,
                    thrownExplosiveController.PresentReplay,
                    thrownExplosiveController.ClearReplayTransients,
                    thrownExplosiveController.EndReplayPresentation,
                    isRequired: true));
            }
            if (destructibleController != null)
            {
                optionalProjections.Add(new OptionalWorldProjection(
                    "destructible",
                    destructibleController.ClearReplayTransients,
                    sample => destructibleController.PresentReplay(
                        sample.Destructibles),
                    destructibleController.ClearReplayTransients,
                    destructibleController.RestoreAuthoritativePresentation));
            }
            if (vehicleController != null)
            {
                optionalProjections.Add(new OptionalWorldProjection(
                    "vehicle",
                    vehicleController.BeginReplayPresentation,
                    sample => vehicleController.PresentReplay(sample.Vehicles),
                    () => { },
                    vehicleController.EndReplayPresentation));
            }
            if (droneController?.Session != null)
            {
                optionalProjections.Add(new OptionalWorldProjection(
                    "drone",
                    droneController.BeginReplayPresentation,
                    sample => droneController.PresentReplay(sample.Drones),
                    droneController.ClearReplayTransients,
                    droneController.EndReplayPresentation,
                    isRequired: true));
            }
            if (smokeController != null)
            {
                optionalProjections.Add(new OptionalWorldProjection(
                    "smoke-field",
                    smokeController.BeginReplayPresentation,
                    sample => smokeController.PresentReplay(sample.SmokeFields),
                    () => { },
                    smokeController.EndReplayPresentation));
            }
            if (fireController != null)
            {
                optionalProjections.Add(new OptionalWorldProjection(
                    "fire-field",
                    fireController.BeginReplayPresentation,
                    sample => fireController.PresentReplay(sample.FireFields),
                    () => { },
                    fireController.EndReplayPresentation));
            }
        }

        private static bool ContainsThrownExplosive(object semanticRecord)
        {
            if (!(semanticRecord is GameplayActionRecord action)) return false;
            foreach (GameplayActionOutcome outcome in action.Outcomes)
                if (outcome is ThrownExplosiveActionOutcome)
                    return true;
            return false;
        }
    }
}
