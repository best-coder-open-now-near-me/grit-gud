using System;
using System.Collections.Generic;
using GritGud.Presentation.LevelEditing.Core;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class LevelEditorSessionLifecycleTests
    {
        [Test]
        public void DisposeReleasesSubscriptionsInReverseOrderOnlyOnce()
        {
            var calls = new List<string>();
            var lifecycle = new LevelEditorSessionLifecycle();

            lifecycle.Subscribe(
                () => calls.Add("subscribe-first"),
                () => calls.Add("release-first"));
            lifecycle.Subscribe(
                () => calls.Add("subscribe-second"),
                () => calls.Add("release-second"));

            Assert.That(lifecycle.RegistrationCount, Is.EqualTo(2));
            lifecycle.Dispose();
            lifecycle.Dispose();

            Assert.That(
                calls,
                Is.EqualTo(new[]
                {
                    "subscribe-first",
                    "subscribe-second",
                    "release-second",
                    "release-first",
                }));
            Assert.That(lifecycle.RegistrationCount, Is.Zero);
        }

        [Test]
        public void FailedSubscriptionIsNotRegisteredForRelease()
        {
            var lifecycle = new LevelEditorSessionLifecycle();
            bool releaseCalled = false;

            Assert.Throws<InvalidOperationException>(() =>
                lifecycle.Subscribe(
                    () => throw new InvalidOperationException("subscribe"),
                    () => releaseCalled = true));

            Assert.That(lifecycle.RegistrationCount, Is.Zero);
            lifecycle.Dispose();
            Assert.That(releaseCalled, Is.False);
        }

        [Test]
        public void DisposeAttemptsEveryReleaseAndReportsAllFailures()
        {
            var calls = new List<string>();
            var lifecycle = new LevelEditorSessionLifecycle();
            lifecycle.Subscribe(
                () => { },
                () =>
                {
                    calls.Add("first");
                    throw new InvalidOperationException("first");
                });
            lifecycle.Subscribe(
                () => { },
                () =>
                {
                    calls.Add("second");
                    throw new ArgumentException("second");
                });

            AggregateException exception = Assert.Throws<AggregateException>(
                () => lifecycle.Dispose());

            Assert.That(calls, Is.EqualTo(new[] { "second", "first" }));
            Assert.That(exception.InnerExceptions, Has.Count.EqualTo(2));
            Assert.That(lifecycle.RegistrationCount, Is.Zero);
            Assert.DoesNotThrow(() => lifecycle.Dispose());
        }

        [Test]
        public void SubscribeAfterDisposeIsRejected()
        {
            var lifecycle = new LevelEditorSessionLifecycle();
            lifecycle.Dispose();

            Assert.Throws<ObjectDisposedException>(() =>
                lifecycle.Subscribe(() => { }, () => { }));
        }
    }
}
