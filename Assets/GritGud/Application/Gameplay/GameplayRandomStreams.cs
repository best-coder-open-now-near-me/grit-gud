using System;

namespace GritGud.Application.Gameplay
{
    public static class GameplayRandomStreams
    {
        public const string AttackResolution = "combat.attack-resolution";
        public const string DisplacementControl = "combat.displacement-control";
        public const string ThrownExplosiveUncertainty =
            "combat.thrown-explosive-uncertainty";
        public const string Initiative = "combat.initiative";

        public static uint DeriveSeed(uint scenarioSeed, string streamId)
        {
            if (string.IsNullOrWhiteSpace(streamId))
            {
                throw new ArgumentException(
                    "Random stream identifiers cannot be empty.",
                    nameof(streamId));
            }

            unchecked
            {
                uint hash = 2166136261u;
                MixByte(ref hash, (byte)scenarioSeed);
                MixByte(ref hash, (byte)(scenarioSeed >> 8));
                MixByte(ref hash, (byte)(scenarioSeed >> 16));
                MixByte(ref hash, (byte)(scenarioSeed >> 24));
                foreach (char character in streamId)
                {
                    MixByte(ref hash, (byte)character);
                    MixByte(ref hash, (byte)(character >> 8));
                }

                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                hash ^= hash >> 16;
                return hash == 0u ? 0x6D2B79F5u : hash;
            }
        }

        private static void MixByte(ref uint hash, byte value)
        {
            hash ^= value;
            hash *= 16777619u;
        }
    }
}
