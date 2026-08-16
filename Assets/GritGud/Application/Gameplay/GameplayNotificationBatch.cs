using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace GritGud.Application.Gameplay
{
    internal sealed class GameplayNotificationBatch
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

        public void Publish()
        {
            if (published)
            {
                throw new InvalidOperationException(
                    "Gameplay notifications can only be published once.");
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

            throw new AggregateException(
                "One or more gameplay observers failed after the authoritative commit.",
                failures);
        }
    }
}
