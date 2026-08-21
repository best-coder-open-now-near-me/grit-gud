using System;

namespace GritGud.Domain.Gameplay
{
    public static class DestructibleFracture
    {
        public const int MaximumChunkCount = 63;

        public static ulong AllChunksMask(int chunkCount)
        {
            ValidateChunkCount(chunkCount);
            return chunkCount == 0
                ? 0UL
                : (1UL << chunkCount) - 1UL;
        }

        public static int CountDetachedChunks(ulong mask)
        {
            int count = 0;
            while (mask != 0UL)
            {
                mask &= mask - 1UL;
                count++;
            }

            return count;
        }

        public static ulong CreateInitialMask(
            DestructiblePropState state,
            int chunkCount,
            string propId)
        {
            ValidateChunkCount(chunkCount);
            if (chunkCount == 0 || state == DestructiblePropState.Intact)
            {
                return 0UL;
            }

            if (state == DestructiblePropState.Destroyed)
            {
                return AllChunksMask(chunkCount);
            }

            int targetCount = Math.Max(1, chunkCount / 2);
            targetCount = Math.Min(chunkCount - 1, targetCount);
            return SelectChunks(
                RequirePropId(propId),
                chunkCount,
                0UL,
                targetCount,
                preferredChunkIndex: -1);
        }

        public static ulong CreateResultingMask(
            string propId,
            int chunkCount,
            ulong previousMask,
            float maximumIntegrity,
            float remainingIntegrity,
            int preferredChunkIndex = -1)
        {
            ValidateChunkCount(chunkCount);
            if (chunkCount == 0)
            {
                if (previousMask != 0UL)
                {
                    throw new ArgumentOutOfRangeException(nameof(previousMask));
                }

                return 0UL;
            }

            ulong allMask = AllChunksMask(chunkCount);
            if ((previousMask & ~allMask) != 0UL)
            {
                throw new ArgumentOutOfRangeException(nameof(previousMask));
            }

            if (!IsFinite(maximumIntegrity)
                || maximumIntegrity <= 0f
                || !IsFinite(remainingIntegrity)
                || remainingIntegrity < 0f
                || remainingIntegrity > maximumIntegrity)
            {
                throw new ArgumentOutOfRangeException(nameof(remainingIntegrity));
            }

            if (preferredChunkIndex < -1 || preferredChunkIndex >= chunkCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(preferredChunkIndex));
            }

            if (remainingIntegrity <= 0f)
            {
                return allMask;
            }

            float damageFraction = 1f - (remainingIntegrity / maximumIntegrity);
            int targetCount = (int)Math.Ceiling(damageFraction * chunkCount);
            targetCount = Math.Max(1, Math.Min(chunkCount - 1, targetCount));
            targetCount = Math.Max(
                targetCount,
                CountDetachedChunks(previousMask));
            return SelectChunks(
                RequirePropId(propId),
                chunkCount,
                previousMask,
                targetCount,
                preferredChunkIndex);
        }

        public static void ValidateSnapshot(
            DestructiblePropState state,
            int chunkCount,
            ulong detachedChunkMask)
        {
            ValidateChunkCount(chunkCount);
            ulong allMask = AllChunksMask(chunkCount);
            if ((detachedChunkMask & ~allMask) != 0UL)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(detachedChunkMask));
            }

            if (chunkCount == 0)
            {
                return;
            }

            switch (state)
            {
                case DestructiblePropState.Intact:
                    if (detachedChunkMask != 0UL)
                    {
                        throw new ArgumentException(
                            "An intact prop cannot have detached fracture chunks.",
                            nameof(detachedChunkMask));
                    }
                    break;
                case DestructiblePropState.Damaged:
                    if (detachedChunkMask == 0UL
                        || detachedChunkMask == allMask)
                    {
                        throw new ArgumentException(
                            "A damaged prop must retain and detach fracture chunks.",
                            nameof(detachedChunkMask));
                    }
                    break;
                case DestructiblePropState.Destroyed:
                    if (detachedChunkMask != allMask)
                    {
                        throw new ArgumentException(
                            "A destroyed prop must detach every fracture chunk.",
                            nameof(detachedChunkMask));
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state));
            }
        }

        private static ulong SelectChunks(
            string propId,
            int chunkCount,
            ulong previousMask,
            int targetCount,
            int preferredChunkIndex)
        {
            ulong result = previousMask;
            int detachedCount = CountDetachedChunks(result);
            if (preferredChunkIndex >= 0
                && detachedCount < targetCount)
            {
                ulong preferredBit = 1UL << preferredChunkIndex;
                if ((result & preferredBit) == 0UL)
                {
                    result |= preferredBit;
                    detachedCount++;
                }
            }

            int startIndex = (int)(StableHash(propId) % (uint)chunkCount);
            for (int offset = 0;
                offset < chunkCount && detachedCount < targetCount;
                offset++)
            {
                int chunkIndex = (startIndex + offset) % chunkCount;
                ulong bit = 1UL << chunkIndex;
                if ((result & bit) != 0UL)
                {
                    continue;
                }

                result |= bit;
                detachedCount++;
            }

            return result;
        }

        private static uint StableHash(string value)
        {
            const uint offset = 2166136261U;
            const uint prime = 16777619U;
            uint hash = offset;
            foreach (char character in value)
            {
                hash ^= character;
                hash *= prime;
            }

            return hash;
        }

        private static string RequirePropId(string propId)
        {
            if (string.IsNullOrWhiteSpace(propId))
            {
                throw new ArgumentException(
                    "Fracture selection requires a stable prop identifier.",
                    nameof(propId));
            }

            return propId;
        }

        private static void ValidateChunkCount(int chunkCount)
        {
            if (chunkCount == 1
                || chunkCount < 0
                || chunkCount > MaximumChunkCount)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkCount));
            }
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
