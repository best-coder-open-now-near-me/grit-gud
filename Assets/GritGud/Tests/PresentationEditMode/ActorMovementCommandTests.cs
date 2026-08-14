using System;
using GritGud.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace GritGud.Presentation.Tests
{
    public sealed class ActorMovementCommandTests
    {
        [Test]
        public void DirectionIsPlanarAndClamped()
        {
            var command = new ActorMovementCommand(
                new Vector3(3f, 8f, 4f),
                sprint: true);

            Assert.That(command.WorldDirection.y, Is.Zero);
            Assert.That(command.WorldDirection.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(command.Sprint, Is.True);
        }

        [Test]
        public void AnalogDirectionMagnitudeIsPreserved()
        {
            var direction = new Vector3(0.25f, 0f, 0.5f);

            var command = new ActorMovementCommand(direction, sprint: false);

            Assert.That(command.WorldDirection, Is.EqualTo(direction));
            Assert.That(command.Sprint, Is.False);
        }

        [Test]
        public void NonFiniteDirectionIsRejected()
        {
            Assert.Throws<ArgumentException>(() =>
                new ActorMovementCommand(
                    new Vector3(float.NaN, 0f, 0f),
                    sprint: false));
        }
    }
}
