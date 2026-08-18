using System;
using GritGud.Application;

namespace GritGud.Application.Gameplay
{
    internal sealed class GameplayNotificationBatch
    {
        private readonly PostCommitNotificationBatch notifications =
            new PostCommitNotificationBatch();

        public void Add<T>(Action<T> handlers, T value)
        {
            notifications.Add(handlers, value);
        }

        public void Publish()
        {
            notifications.Publish(
                "One or more gameplay observers failed after the authoritative commit.");
        }
    }
}
