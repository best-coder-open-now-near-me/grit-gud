using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;

namespace GritGud.Presentation.Gameplay
{
    /// <summary>
    /// Captures short-lived, authoritative action noise for exploration
    /// perception. Each observer can consume an emitted sound once, so an
    /// update-rate change cannot turn one gunshot into repeated suspicion.
    /// </summary>
    internal sealed class GameplayExplorationSoundLedger : IDisposable
    {
        private const float LifetimeSeconds = 2f;
        private readonly GameplaySession session;
        private readonly List<SoundRecord> sounds = new List<SoundRecord>();
        private bool disposed;

        public GameplayExplorationSoundLedger(GameplaySession gameplaySession)
        {
            session = gameplaySession ?? throw new ArgumentNullException(
                nameof(gameplaySession));
            session.ActionResolved += HandleActionResolved;
            session.ModeChanged += HandleModeChanged;
        }

        public void Advance(float elapsedSeconds)
        {
            if (elapsedSeconds <= 0f)
                return;
            for (int index = sounds.Count - 1; index >= 0; index--)
            {
                SoundRecord sound = sounds[index];
                sound.AgeSeconds += elapsedSeconds;
                if (sound.AgeSeconds >= LifetimeSeconds)
                    sounds.RemoveAt(index);
            }
        }

        public bool TryConsume(
            string observerId,
            out string sourceId,
            out GameplayPosition origin,
            out float loudness)
        {
            if (string.IsNullOrWhiteSpace(observerId))
                throw new ArgumentException(
                    "Sound observers require an actor identifier.",
                    nameof(observerId));
            for (int index = sounds.Count - 1; index >= 0; index--)
            {
                SoundRecord sound = sounds[index];
                if (string.Equals(sound.SourceId, observerId,
                    StringComparison.Ordinal)
                    || !sound.HeardBy.Add(observerId))
                {
                    continue;
                }

                sourceId = sound.SourceId;
                origin = sound.Origin;
                loudness = sound.Loudness;
                return true;
            }

            sourceId = null;
            origin = default;
            loudness = 0f;
            return false;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            session.ActionResolved -= HandleActionResolved;
            session.ModeChanged -= HandleModeChanged;
            sounds.Clear();
            disposed = true;
        }

        private void HandleActionResolved(GameplayActionRecord action)
        {
            if (action == null || !TryGetLoudness(action, out float loudness)
                || !session.TryGetActor(action.Request.ActorId,
                    out GameplayActorSnapshot actor))
            {
                return;
            }

            sounds.Add(new SoundRecord(
                action.Request.ActorId,
                actor.Pose.Position,
                loudness));
        }

        private void HandleModeChanged(GameplayModeChange change)
        {
            // Exploration sound is a momentary world observation. It cannot
            // leak across a mode boundary and re-trigger an old encounter.
            sounds.Clear();
        }

        private static bool TryGetLoudness(
            GameplayActionRecord action,
            out float loudness)
        {
            foreach (GameplayActionOutcome outcome in action.Outcomes)
            {
                if (outcome is ThrownExplosiveActionOutcome
                    || outcome is ProjectileLaunchedActionOutcome)
                {
                    loudness = 1f;
                    return true;
                }
                if (outcome is WeaponDischargedActionOutcome)
                {
                    loudness = 0.9f;
                    return true;
                }
                if (outcome is AttackResolvedActionOutcome)
                {
                    loudness = 0.8f;
                    return true;
                }
            }

            loudness = 0f;
            return false;
        }

        private sealed class SoundRecord
        {
            public SoundRecord(
                string sourceId,
                GameplayPosition origin,
                float loudness)
            {
                SourceId = sourceId;
                Origin = origin;
                Loudness = loudness;
                HeardBy = new HashSet<string>(StringComparer.Ordinal);
            }

            public string SourceId { get; }

            public GameplayPosition Origin { get; }

            public float Loudness { get; }

            public HashSet<string> HeardBy { get; }

            public float AgeSeconds { get; set; }
        }
    }
}
