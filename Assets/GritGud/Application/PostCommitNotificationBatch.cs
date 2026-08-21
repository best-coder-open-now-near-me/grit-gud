using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace GritGud.Application
{
    internal sealed class PostCommitNotificationBatch
    {
        private readonly List<Action> notifications = new List<Action>();
        private bool published;

        public void Add<T>(Action<T> handlers, T value)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate candidate in handlers.GetInvocationList())
            {
                var handler = (Action<T>)candidate;
                notifications.Add(() => handler(value));
            }
        }

        public void Add<TEventArgs>(
            EventHandler<TEventArgs> handlers,
            object sender,
            TEventArgs args)
            where TEventArgs : EventArgs
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate candidate in handlers.GetInvocationList())
            {
                var handler = (EventHandler<TEventArgs>)candidate;
                notifications.Add(() => handler(sender, args));
            }
        }

        public void Publish(string failureMessage)
        {
            if (published)
            {
                throw new InvalidOperationException(
                    "Post-commit notifications can only be published once.");
            }

            if (string.IsNullOrWhiteSpace(failureMessage))
            {
                throw new ArgumentException(
                    "Post-commit notification failures require a message.",
                    nameof(failureMessage));
            }

            published = true;
            List<Exception> failures = null;
            foreach (Action notification in notifications)
            {
                try
                {
                    notification();
                }
                catch (Exception exception)
                {
                    failures ??= new List<Exception>();
                    failures.Add(exception);
                }
            }

            if (failures == null)
            {
                return;
            }

            if (failures.Count == 1)
            {
                ExceptionDispatchInfo.Capture(failures[0]).Throw();
            }

            throw new AggregateException(failureMessage, failures);
        }
    }
}
