using System;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class ProjectileEffectPresenter : IDisposable
    {
        private readonly ProjectilePresentationDefinition presentation;
        private readonly GameObject trailRoot;
        private readonly ParticleSystem[] trailParticles;
        private bool disposed;

        public ProjectileEffectPresenter(
            ProjectilePresentationDefinition definition,
            Transform trailParent)
        {
            presentation = definition ?? throw new ArgumentNullException(
                nameof(definition));
            if (trailParent == null)
            {
                throw new ArgumentNullException(nameof(trailParent));
            }

            if (presentation.TrailEffectPrefab == null)
            {
                trailParticles = Array.Empty<ParticleSystem>();
                return;
            }

            trailRoot = UnityEngine.Object.Instantiate(
                presentation.TrailEffectPrefab,
                trailParent,
                false);
            trailRoot.name = presentation.ProjectileId + " Trail Effect";
            trailRoot.transform.SetLocalPositionAndRotation(
                presentation.TrailLocalPosition,
                presentation.TrailLocalRotation);
            trailRoot.transform.localScale =
                presentation.TrailEffectPrefab.transform.localScale
                * presentation.TrailScale;
            trailParticles = trailRoot.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particles in trailParticles)
            {
                particles.Stop(
                    withChildren: true,
                    stopBehavior:
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.EmissionModule emission = particles.emission;
                emission.enabled = false;
            }
        }

        public bool TrailEmitting
        {
            get
            {
                foreach (ParticleSystem particles in trailParticles)
                {
                    if (particles != null && particles.emission.enabled)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void SetTrailEmission(bool enabled)
        {
            ThrowIfDisposed();
            foreach (ParticleSystem particles in trailParticles)
            {
                if (particles == null)
                {
                    continue;
                }

                ParticleSystem.EmissionModule emission = particles.emission;
                emission.enabled = enabled;
                if (enabled && !particles.isPlaying)
                {
                    particles.Play(withChildren: true);
                }
            }
        }

        public GameObject CreateImpact(
            Vector3 position,
            float blastRadius,
            Transform lifecycleParent = null)
        {
            ThrowIfDisposed();
            if (presentation.ImpactEffectPrefab == null)
            {
                return null;
            }

            GameObject impact = UnityEngine.Object.Instantiate(
                presentation.ImpactEffectPrefab,
                position,
                Quaternion.identity,
                lifecycleParent);
            impact.name = presentation.ProjectileId + " Impact Effect";
            float scale = Mathf.Max(
                0.01f,
                Mathf.Max(0f, blastRadius)
                    * presentation.ImpactScalePerBlastRadius);
            impact.transform.localScale =
                presentation.ImpactEffectPrefab.transform.localScale * scale;
            foreach (ParticleSystem particles in
                impact.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Play(withChildren: true);
            }

            UnityEngine.Object.Destroy(
                impact,
                presentation.ImpactEffectSeconds);
            return impact;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            GameplayObjectLifecycle.Destroy(trailRoot);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(ProjectileEffectPresenter));
            }
        }
    }
}
