using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using NUnit.Framework;

namespace GritGud.Domain.Tests.Gameplay
{
    public sealed class VehicleMomentumSessionTests
    {
        private static readonly VehicleMomentumProfile StandardProfile =
            new VehicleMomentumProfile(
                maximumSpeed: 10f,
                accelerationPerTurn: 4f,
                brakingPerTurn: 2f,
                lowSpeedTurnDegrees: 75f,
                highSpeedTurnDegrees: 25f,
                baseTurningRadius: 0.6f,
                speedTurningRadiusFactor: 0.16f);

        [Test]
        public void SpeedNarrowsAndLengthensForwardEnvelope()
        {
            VehicleMovementEnvelope stopped = CreateSession(speed: 0f)
                .CreateEnvelope();
            VehicleMovementEnvelope fast = CreateSession(speed: 8f)
                .CreateEnvelope();

            Assert.That(fast.MaximumDistance, Is.GreaterThan(stopped.MaximumDistance));
            Assert.That(fast.MinimumDistance, Is.GreaterThan(0f));
            Assert.That(fast.MaximumTurnDegrees,
                Is.LessThan(stopped.MaximumTurnDegrees));
            Assert.That(stopped.CreateBoundary(8).Count, Is.EqualTo(11));
        }

        [Test]
        public void FastVehicleCannotStopInsideBrakingDistance()
        {
            VehicleMomentumSession session = CreateSession(speed: 8f);

            bool resolved = session.TryResolvePath(
                new[]
                {
                    Position(0f, 0f),
                    Position(0f, 3f),
                },
                out var record,
                out var failure);

            Assert.That(resolved, Is.False);
            Assert.That(record, Is.Null);
            Assert.That(failure, Is.EqualTo(VehiclePathFailure.TooShortForBraking));
            Assert.That(session.State.Speed, Is.EqualTo(8f));
        }

        [Test]
        public void WholePathCurvatureRejectsZigzagInsideEndpointEnvelope()
        {
            var permissiveAcceleration = new VehicleMomentumProfile(
                maximumSpeed: 10f,
                accelerationPerTurn: 8f,
                brakingPerTurn: 2f,
                lowSpeedTurnDegrees: 75f,
                highSpeedTurnDegrees: 25f,
                baseTurningRadius: 0.6f,
                speedTurningRadiusFactor: 0.16f);
            var session = new VehicleMomentumSession(
                permissiveAcceleration,
                State(speed: 0f));

            bool resolved = session.TryResolvePath(
                new[]
                {
                    Position(0f, 0f),
                    Position(0f, 1.2f),
                    Position(1f, 1.2f),
                    Position(1f, 2.4f),
                },
                out var record,
                out var failure);

            Assert.That(resolved, Is.False);
            Assert.That(record, Is.Null);
            Assert.That(failure, Is.EqualTo(VehiclePathFailure.CurvatureTooTight));
        }

        [Test]
        public void SmoothCurveRetainsFinalSpeedAndFacing()
        {
            var profile = new VehicleMomentumProfile(
                maximumSpeed: 10f,
                accelerationPerTurn: 6f,
                brakingPerTurn: 2f,
                lowSpeedTurnDegrees: 75f,
                highSpeedTurnDegrees: 25f,
                baseTurningRadius: 0.6f,
                speedTurningRadiusFactor: 0.12f);
            var session = new VehicleMomentumSession(profile, State(speed: 0f));

            bool resolved = session.TryResolvePath(
                SmoothPath(),
                out var record,
                out var failure);

            Assert.That(resolved, Is.True);
            Assert.That(failure, Is.EqualTo(VehiclePathFailure.None));
            Assert.That(record.Sequence, Is.EqualTo(1));
            Assert.That(record.Path.Count, Is.EqualTo(4));
            Assert.That(session.State.Position.X, Is.EqualTo(0.6f));
            Assert.That(session.State.Position.Z, Is.EqualTo(2.8f));
            Assert.That(session.State.Speed, Is.GreaterThan(0f));
            Assert.That(session.State.Speed, Is.LessThanOrEqualTo(6f));
            Assert.That(session.State.ForwardDegrees, Is.GreaterThan(0f));
            Assert.That(session.State.ForwardDegrees, Is.LessThan(30f));
        }

        [Test]
        public void RecordedVehicleMoveReplaysWithoutReplanning()
        {
            var profile = new VehicleMomentumProfile(
                maximumSpeed: 10f,
                accelerationPerTurn: 6f,
                brakingPerTurn: 2f,
                lowSpeedTurnDegrees: 75f,
                highSpeedTurnDegrees: 25f,
                baseTurningRadius: 0.6f,
                speedTurningRadiusFactor: 0.12f);
            var source = new VehicleMomentumSession(profile, State(speed: 0f));
            source.TryResolvePath(SmoothPath(), out var record, out _);
            var replay = new VehicleMomentumSession(profile, State(speed: 0f));

            replay.Commit(record);

            Assert.That(replay.State.Position.X, Is.EqualTo(source.State.Position.X));
            Assert.That(replay.State.Position.Z, Is.EqualTo(source.State.Position.Z));
            Assert.That(replay.State.Speed, Is.EqualTo(source.State.Speed));
            Assert.That(replay.State.ForwardDegrees,
                Is.EqualTo(source.State.ForwardDegrees));
            Assert.That(replay.Records.Count, Is.EqualTo(1));
        }

        private static VehicleMomentumSession CreateSession(float speed) =>
            new VehicleMomentumSession(StandardProfile, State(speed));

        private static VehicleMomentumState State(float speed) =>
            new VehicleMomentumState(
                "vehicle",
                Position(0f, 0f),
                forwardDegrees: 0f,
                speed);

        private static GameplayPosition[] SmoothPath() =>
            new[]
            {
                Position(0f, 0f),
                Position(0.1f, 1f),
                Position(0.3f, 2f),
                Position(0.6f, 2.8f),
            };

        private static GameplayPosition Position(float x, float z) =>
            new GameplayPosition(x, 0f, z);
    }
}
