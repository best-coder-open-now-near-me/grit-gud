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
        GameplayPosition Sample(
            GameplayPosition center,
            float radius,
            ScenarioRunIdentity run,
            GameplayTransitionIdentity transition,
            string purpose);
    }

    public sealed class AddressedUncertaintySampler : IUncertaintySampler
    {
        public GameplayPosition Sample(
            GameplayPosition center,
            float radius,
            ScenarioRunIdentity run,
            GameplayTransitionIdentity transition,
            string purpose)
        {
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0f)
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (radius == 0f) return center;
            double angle = GameplayAddressedRandom.SampleUnit(
                run,
                transition,
                purpose,
                sampleIndex: 0) * Math.PI * 2d;
            double radialRoll = GameplayAddressedRandom.SampleUnit(
                run,
                transition,
                purpose,
                sampleIndex: 1);
            double distance = (1d - Math.Sqrt(radialRoll)) * radius;
            return new GameplayPosition(
                center.X + (float)(Math.Cos(angle) * distance),
                center.Y,
                center.Z + (float)(Math.Sin(angle) * distance));
        }
    }
}
