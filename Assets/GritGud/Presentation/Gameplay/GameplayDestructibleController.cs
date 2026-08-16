using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;
using GritGud.Domain.Gameplay;
using GritGud.Domain.Levels;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayDestructibleController : MonoBehaviour
    {
        private readonly Dictionary<string, DestructiblePropPresenter> presenters =
            new Dictionary<string, DestructiblePropPresenter>(StringComparer.Ordinal);

        public DestructiblePropSession Session { get; private set; }

        public void Bind(
            LevelWorld world,
            LevelDocument level,
            GameplayJournal journal)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            Unbind();
            Session = DestructiblePropSession.FromLevel(
                level,
                journal ?? throw new ArgumentNullException(nameof(journal)));
            Session.Damaged += HandleDamage;
            foreach (string propId in Session.PropIds)
            {
                if (!world.TryGetEntity(propId, out LevelEntityView view))
                {
                    throw new InvalidOperationException(
                        $"Loaded level is missing destructible prop '{propId}'.");
                }

                DestructiblePropPresenter presenter =
                    view.GetComponent<DestructiblePropPresenter>();
                if (presenter == null)
                {
                    presenter = view.gameObject.AddComponent<DestructiblePropPresenter>();
                }

                presenter.Bind(Session.GetProp(propId));
                presenters.Add(propId, presenter);
            }

            Physics.SyncTransforms();
            enabled = true;
        }

        public bool TryApplyDamage(
            string propId,
            float requestedDamage,
            out DestructibleDamageRecord record)
        {
            if (Session == null)
            {
                throw new InvalidOperationException(
                    "Destructible gameplay is not bound to a level.");
            }

            if (!Session.TryApplyDamage(propId, requestedDamage, out record))
            {
                return false;
            }

            return true;
        }

        public void CommitDamage(DestructibleDamageRecord record)
        {
            if (Session == null)
            {
                throw new InvalidOperationException(
                    "Destructible gameplay is not bound to a level.");
            }

            Session.CommitDamage(record);
        }

        internal void PresentReplay(
            IReadOnlyList<DestructiblePropSnapshot> snapshots)
        {
            if (snapshots == null)
                throw new ArgumentNullException(nameof(snapshots));
            foreach (DestructiblePropSnapshot snapshot in snapshots)
                Present(snapshot);
        }

        internal void RestoreAuthoritativePresentation()
        {
            if (Session == null) return;
            foreach (string propId in Session.PropIds)
                Present(Session.GetProp(propId));
        }

        public void Unbind()
        {
            if (Session != null)
            {
                Session.Damaged -= HandleDamage;
            }

            foreach (DestructiblePropPresenter presenter in presenters.Values)
            {
                presenter?.Unbind();
            }

            presenters.Clear();
            Session = null;
            enabled = false;
        }

        private void Present(DestructiblePropSnapshot snapshot)
        {
            if (!presenters.TryGetValue(snapshot.PropId, out var presenter))
            {
                throw new InvalidOperationException(
                    $"Destructible prop '{snapshot.PropId}' has no level presenter.");
            }

            presenter.Present(snapshot);
            Physics.SyncTransforms();
        }

        private void HandleDamage(DestructibleDamageRecord record) =>
            Present(record.Resulting);
    }
}
