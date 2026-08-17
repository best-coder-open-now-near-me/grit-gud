using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    public sealed class GameplayTurnReplayTransientCue
    {
        public GameplayTurnReplayTransientCue(
            string actorId,
            TurnReplayActorActionKind actionKind,
            TurnReplayEventCrossing crossing)
        {
            ActorId = string.IsNullOrWhiteSpace(actorId)
                ? throw new ArgumentException(
                    "Replay transient cues require an actor identifier.",
                    nameof(actorId))
                : actorId;
            ActionKind = actionKind;
            Crossing = crossing ?? throw new ArgumentNullException(
                nameof(crossing));
        }

        public string ActorId { get; }

        public TurnReplayActorActionKind ActionKind { get; }

        public TurnReplayEventCrossing Crossing { get; }
    }

    [Serializable]
    public sealed class GameplayTurnReplayTransientBinding
    {
        [SerializeField]
        private TurnReplayActorActionKind actionKind;

        [SerializeField]
        private TurnReplayEventBoundary boundary;

        [SerializeField]
        private AudioClip audioClip;

        [SerializeField]
        private GameObject effectPrefab;

        [SerializeField, Min(0.01f)]
        private float lifetimeSeconds = 1f;

        public GameplayTurnReplayTransientBinding(
            TurnReplayActorActionKind replayActionKind,
            TurnReplayEventBoundary eventBoundary,
            AudioClip clip,
            GameObject particleOrEffectPrefab,
            float lifetime)
        {
            actionKind = replayActionKind;
            boundary = eventBoundary;
            audioClip = clip;
            effectPrefab = particleOrEffectPrefab;
            lifetimeSeconds = Mathf.Max(0.01f, lifetime);
        }

        public TurnReplayActorActionKind ActionKind => actionKind;

        public TurnReplayEventBoundary Boundary => boundary;

        public AudioClip AudioClip => audioClip;

        public GameObject EffectPrefab => effectPrefab;

        public float LifetimeSeconds => Mathf.Max(
            Mathf.Max(0.01f, lifetimeSeconds),
            audioClip != null ? audioClip.length : 0f);

        internal bool Matches(GameplayTurnReplayTransientCue cue) =>
            cue.ActionKind == actionKind
            && cue.Crossing.Boundary == boundary;
    }

    /// <summary>
    /// Actor-local extension point for replay-only audio and particle adapters.
    /// Cues are emitted only by the continuous-forward crossing path.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayTurnReplayTransientHooks : MonoBehaviour
    {
        private sealed class ActiveTransient
        {
            public ActiveTransient(GameObject root, float lifetime)
            {
                Root = root;
                RemainingSeconds = lifetime;
            }

            public GameObject Root { get; }

            public float RemainingSeconds { get; set; }
        }

        [SerializeField]
        private GameplayTurnReplayTransientBinding[] bindings =
            Array.Empty<GameplayTurnReplayTransientBinding>();

        private readonly List<ActiveTransient> active = new();

        public event Action<GameplayTurnReplayTransientCue> CuePresented;

        public event Action Cleared;

        public GameplayTurnReplayTransientCue LastCue { get; private set; }

        public int CueSequence { get; private set; }

        internal int ActiveTransientCount => active.Count;

        internal void Configure(
            params GameplayTurnReplayTransientBinding[] values)
        {
            ClearActive();
            bindings = values
                ?? Array.Empty<GameplayTurnReplayTransientBinding>();
        }

        internal void Present(GameplayTurnReplayTransientCue cue)
        {
            LastCue = cue ?? throw new ArgumentNullException(nameof(cue));
            CueSequence++;
            foreach (GameplayTurnReplayTransientBinding binding in bindings)
            {
                if (binding != null && binding.Matches(cue))
                    Present(binding);
            }
            CuePresented?.Invoke(cue);
        }

        internal void Clear()
        {
            ClearActive();
            LastCue = null;
            Cleared?.Invoke();
        }

        private void Update()
        {
            float elapsed = Mathf.Max(0f, Time.unscaledDeltaTime);
            for (int index = active.Count - 1; index >= 0; index--)
            {
                ActiveTransient transient = active[index];
                transient.RemainingSeconds -= elapsed;
                if (transient.RemainingSeconds > 0f)
                    continue;
                GameplayObjectLifecycle.Destroy(transient.Root);
                active.RemoveAt(index);
            }
        }

        private void Present(GameplayTurnReplayTransientBinding binding)
        {
            if (binding.AudioClip == null && binding.EffectPrefab == null)
                return;
            var root = new GameObject(
                "Replay " + binding.ActionKind + " " + binding.Boundary);
            root.transform.SetParent(transform, false);
            if (binding.EffectPrefab != null)
            {
                GameObject effect = Instantiate(
                    binding.EffectPrefab,
                    root.transform);
                foreach (ParticleSystem particles in
                    effect.GetComponentsInChildren<ParticleSystem>(true))
                {
                    particles.Play(true);
                }
            }
            if (binding.AudioClip != null)
            {
                AudioSource source = root.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.PlayOneShot(binding.AudioClip);
            }
            active.Add(new ActiveTransient(root, binding.LifetimeSeconds));
        }

        private void ClearActive()
        {
            foreach (ActiveTransient transient in active)
                GameplayObjectLifecycle.Destroy(transient.Root);
            active.Clear();
        }

        private void OnDestroy()
        {
            ClearActive();
        }
    }
}
