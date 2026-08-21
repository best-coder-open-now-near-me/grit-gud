using System;
using System.Collections.Generic;

namespace GritGud.Presentation.LevelEditing.Core
{
    internal sealed class LevelEditorSessionLifecycle : IDisposable
    {
        private readonly List<Action> releaseActions = new List<Action>();
        private bool disposed;

        public int RegistrationCount => releaseActions.Count;

        public void Subscribe(Action subscribe, Action unsubscribe)
        {
            if (subscribe == null)
                throw new ArgumentNullException(nameof(subscribe));
            if (unsubscribe == null)
                throw new ArgumentNullException(nameof(unsubscribe));
            if (disposed)
            {
                throw new ObjectDisposedException(
                    nameof(LevelEditorSessionLifecycle));
            }

            subscribe();
            releaseActions.Add(unsubscribe);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            List<Exception> failures = null;
            for (int index = releaseActions.Count - 1; index >= 0; index--)
            {
                try
                {
                    releaseActions[index]();
                }
                catch (Exception exception)
                {
                    failures ??= new List<Exception>();
                    failures.Add(exception);
                }
            }
            releaseActions.Clear();

            if (failures != null)
            {
                throw new AggregateException(
                    "One or more level editor session cleanup actions failed.",
                    failures);
            }
        }
    }
}
