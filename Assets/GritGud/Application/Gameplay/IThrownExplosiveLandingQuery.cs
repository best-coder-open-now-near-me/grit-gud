using System;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public readonly struct ThrownExplosiveLandingResult
    {
        public ThrownExplosiveLandingResult(
            GameplayPosition landingPosition,
            long worldStateRevision)
        {
            if (worldStateRevision < 0L)
                throw new ArgumentOutOfRangeException(nameof(worldStateRevision));
            LandingPosition = landingPosition;
            WorldStateRevision = worldStateRevision;
        }
        public GameplayPosition LandingPosition { get; }
        public long WorldStateRevision { get; }
    }

    public interface IThrownExplosiveLandingQuery
    {
        ThrownExplosiveLandingResult Resolve(
            GameplayPosition launchOrigin,
            GameplayPosition sampledLanding);
    }

    public interface IUncertaintySampler
    {
        GameplayPosition Sample(GameplayPosition center, float radius);
    }

    public sealed class SeededUncertaintySampler : IUncertaintySampler
    {
        private uint state;

        public SeededUncertaintySampler(uint seed)
        {
            state = seed != 0u ? seed : 0x6D2B79F5u;
        }

        public GameplayPosition Sample(GameplayPosition center, float radius)
        {
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0f)
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (radius == 0f) return center;
            double angle = NextUnit() * Math.PI * 2d;
            double radialRoll = NextUnit();
            double distance = (1d - Math.Sqrt(radialRoll)) * radius;
            return new GameplayPosition(
                center.X + (float)(Math.Cos(angle) * distance),
                center.Y,
                center.Z + (float)(Math.Sin(angle) * distance));
        }

        private double NextUnit()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state / ((double)uint.MaxValue + 1d);
        }
    }
}
