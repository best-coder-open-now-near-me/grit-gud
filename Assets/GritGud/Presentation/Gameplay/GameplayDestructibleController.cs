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
                journal ?? throw new ArgumentNullException(nameof(journal)),
                entity => ResolveFractureChunkCount(world, entity));
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

                presenter.Bind(
                    Session.GetProp(propId),
                    view.Archetype.FractureProfile);
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

        internal void PresentDisplacement(DisplacementRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (!record.Succeeded ||
                record.Request.SubjectKind != DisplacementSubjectKind.Prop)
            {
                return;
            }
            if (!presenters.TryGetValue(
                    record.Request.SubjectId,
                    out DestructiblePropPresenter presenter))
            {
                throw new InvalidOperationException(
                    $"Destructible prop '{record.Request.SubjectId}' has no "
                    + "level presenter.");
            }

            presenter.PresentDisplacement(
                record,
                GameplayDisplacementPresentationTiming.GetDurationSeconds(
                    record));
            Physics.SyncTransforms();
        }

        internal void RestoreAuthoritativePresentation()
        {
            if (Session == null) return;
            ClearReplayTransients();
            foreach (string propId in Session.PropIds)
                Present(Session.GetProp(propId));
        }

        internal void ClearReplayTransients()
        {
            foreach (DestructiblePropPresenter presenter in presenters.Values)
                presenter?.ClearTransientDebris();
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

        private void HandleDamage(DestructibleDamageRecord record)
        {
            if (!presenters.TryGetValue(record.PropId, out var presenter))
            {
                throw new InvalidOperationException(
                    $"Destructible prop '{record.PropId}' has no level presenter.");
            }

            presenter.PresentDamage(record, spawnTransientDebris: true);
            Physics.SyncTransforms();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            foreach (DestructiblePropPresenter presenter in presenters.Values)
                presenter?.TickDisplacement(deltaTime);
        }

        private static int ResolveFractureChunkCount(
            LevelWorld world,
            LevelEntity entity)
        {
            if (!world.TryGetEntity(entity.id, out LevelEntityView view))
            {
                throw new InvalidOperationException(
                    $"Loaded level is missing destructible prop '{entity.id}'.");
            }

            return view.Archetype.FractureProfile?.ChunkCount ?? 0;
        }
    }
}
