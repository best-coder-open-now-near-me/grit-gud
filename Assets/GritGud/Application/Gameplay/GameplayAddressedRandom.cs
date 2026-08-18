using System;

namespace GritGud.Application.Gameplay
{
    public static class GameplayAddressedRandom
    {
        public static uint SampleUInt32(
            ScenarioRunIdentity run,
            GameplayTransitionIdentity transition,
            string purpose,
            int sampleIndex = 0)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (transition.Sequence <= 0
                || string.IsNullOrWhiteSpace(transition.Kind)
                || string.IsNullOrWhiteSpace(transition.ActorId)
                || string.IsNullOrWhiteSpace(transition.SubjectId))
                throw new ArgumentException(
                    "Random samples require a complete transition identity.",
                    nameof(transition));
            if (string.IsNullOrWhiteSpace(purpose))
                throw new ArgumentException(
                    "Random sample purposes cannot be empty.",
                    nameof(purpose));
            if (sampleIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(sampleIndex));

            unchecked
            {
                ulong hash = 14695981039346656037UL;
                MixUInt32(ref hash, run.ScenarioSeed);
                MixInt32(ref hash, run.RandomSchemaVersion);
                MixInt64(ref hash, transition.Sequence);
                MixText(ref hash, transition.Kind);
                MixText(ref hash, transition.ActorId);
                MixText(ref hash, transition.SubjectId);
                MixText(ref hash, purpose.Trim());
                MixInt32(ref hash, sampleIndex);
                hash ^= hash >> 30;
                hash *= 0xBF58476D1CE4E5B9UL;
                hash ^= hash >> 27;
                hash *= 0x94D049BB133111EBUL;
                hash ^= hash >> 31;
                uint result = (uint)(hash ^ (hash >> 32));
                return result == 0u ? 0x6D2B79F5u : result;
            }
        }

        public static int RollD20(
            ScenarioRunIdentity run,
            GameplayTransitionIdentity transition,
            string purpose,
            int sampleIndex = 0) =>
            (int)(SampleUInt32(
                run,
                transition,
                purpose,
                sampleIndex) % 20u) + 1;

        public static double SampleUnit(
            ScenarioRunIdentity run,
            GameplayTransitionIdentity transition,
            string purpose,
            int sampleIndex = 0) =>
            SampleUInt32(run, transition, purpose, sampleIndex)
            / ((double)uint.MaxValue + 1d);

        private static void MixText(ref ulong hash, string value)
        {
            string text = value ?? string.Empty;
            MixInt32(ref hash, text.Length);
            foreach (char character in text)
            {
                MixByte(ref hash, (byte)character);
                MixByte(ref hash, (byte)(character >> 8));
            }
        }

        private static void MixInt32(ref ulong hash, int value) =>
            MixUInt32(ref hash, unchecked((uint)value));

        private static void MixUInt32(ref ulong hash, uint value)
        {
            MixByte(ref hash, (byte)value);
            MixByte(ref hash, (byte)(value >> 8));
            MixByte(ref hash, (byte)(value >> 16));
            MixByte(ref hash, (byte)(value >> 24));
        }

        private static void MixInt64(ref ulong hash, long value)
        {
            ulong unsigned = unchecked((ulong)value);
            for (int shift = 0; shift < 64; shift += 8)
                MixByte(ref hash, (byte)(unsigned >> shift));
        }

        private static void MixByte(ref ulong hash, byte value)
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }
    }
}
