using GritGud.Domain.Levels;
using UnityEngine;

namespace GritGud.Presentation.Levels.Runtime
{
    public sealed class AmbientAudioZoneController : MonoBehaviour
    {
        private AudioSource source;
        private Vector3 size;
        private float authoredVolume;
        private float fadeDistance;
        private bool playbackEnabled;
        private AudioListener listener;
        private float nextListenerSearchTime;

        public void Initialize(LevelAudioZoneData data, AudioClip clip, bool enabled)
        {
            name = data.displayName;
            transform.position = ToVector(data.center);
            size = ToVector(data.size);
            authoredVolume = Mathf.Clamp01(data.volume);
            fadeDistance = Mathf.Max(0f, data.fadeDistance);
            source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            SetPlaybackEnabled(enabled);
        }

        public void SetPlaybackEnabled(bool enabled)
        {
            playbackEnabled = enabled;
            if (source == null)
                return;
            if (!enabled)
            {
                source.Stop();
                source.volume = 0f;
            }
            else if (!source.isPlaying)
            {
                source.Play();
            }
        }

        private void Update()
        {
            if (!playbackEnabled || source == null)
                return;
            if (listener == null || !listener.isActiveAndEnabled)
                FindListener();
            source.volume = listener == null
                ? 0f
                : authoredVolume * CalculateGain(
                    transform.position,
                    size,
                    listener.transform.position,
                    fadeDistance);
        }

        private void FindListener()
        {
            if (Time.unscaledTime < nextListenerSearchTime)
                return;
            nextListenerSearchTime = Time.unscaledTime + 1f;
            AudioListener[] listeners = FindObjectsByType<AudioListener>(
                FindObjectsInactive.Exclude);
            listener = null;
            foreach (AudioListener candidate in listeners)
            {
                if (candidate != null && candidate.isActiveAndEnabled)
                {
                    listener = candidate;
                    break;
                }
            }
        }

        internal static float CalculateGain(
            Vector3 center,
            Vector3 size,
            Vector3 listenerPosition,
            float fadeDistance)
        {
            Vector3 half = size * 0.5f;
            Vector3 delta = listenerPosition - center;
            var outside = new Vector3(
                Mathf.Max(0f, Mathf.Abs(delta.x) - half.x),
                Mathf.Max(0f, Mathf.Abs(delta.y) - half.y),
                Mathf.Max(0f, Mathf.Abs(delta.z) - half.z));
            float distance = outside.magnitude;
            if (distance <= 0f)
                return 1f;
            return fadeDistance <= 0f
                ? 0f
                : Mathf.Clamp01(1f - distance / fadeDistance);
        }

        private static Vector3 ToVector(Float3Data value) =>
            new Vector3(value.x, value.y, value.z);
    }
}
