using System;
using System.Collections.Generic;
using GritGud.Domain.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [Serializable]
    public sealed class ActorWoundVariantBinding
    {
        [SerializeField]
        private TargetRegionId region;

        [SerializeField]
        private GameObject clearVariant;

        [SerializeField]
        private GameObject woundedVariant;

        public ActorWoundVariantBinding(
            TargetRegionId targetRegion,
            GameObject clearVisual,
            GameObject woundedVisual)
        {
            region = targetRegion;
            clearVariant = clearVisual;
            woundedVariant = woundedVisual;
        }

        public TargetRegionId Region => region;

        public GameObject ClearVariant => clearVariant;

        public GameObject WoundedVariant => woundedVariant;

        internal void Present(ActorWoundSnapshot wounds)
        {
            bool wounded = wounds.GetWoundCount(region) > 0;
            if (clearVariant != null)
                clearVariant.SetActive(!wounded);
            if (woundedVariant != null)
                woundedVariant.SetActive(wounded);
        }
    }

    /// <summary>
    /// Content-driven hook for actor wound meshes, decals, or material variants.
    /// Replay captures exact active states before projecting sampled wounds.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActorWoundVariantPresenter : MonoBehaviour
    {
        [SerializeField]
        private ActorWoundVariantBinding[] bindings =
            Array.Empty<ActorWoundVariantBinding>();

        private Dictionary<GameObject, bool> originalActiveStates;
        private ActorWoundSnapshot? originalWounds;

        public ActorWoundSnapshot? CurrentWounds { get; private set; }

        internal bool IsPresentingReplay => originalActiveStates != null;

        internal void Configure(params ActorWoundVariantBinding[] values)
        {
            if (IsPresentingReplay)
            {
                throw new InvalidOperationException(
                    "Wound variants cannot be reconfigured during replay.");
            }
            bindings = values ?? Array.Empty<ActorWoundVariantBinding>();
        }

        internal void PresentAuthoritative(ActorWoundSnapshot wounds)
        {
            if (IsPresentingReplay)
            {
                throw new InvalidOperationException(
                    "Authoritative wound presentation is paused during replay.");
            }
            Present(wounds);
        }

        internal void BeginReplayPresentation()
        {
            if (IsPresentingReplay)
            {
                throw new InvalidOperationException(
                    "Wound replay presentation is already active.");
            }
            originalActiveStates = new Dictionary<GameObject, bool>();
            originalWounds = CurrentWounds;
            foreach (ActorWoundVariantBinding binding in bindings)
            {
                if (binding == null)
                    continue;
                Capture(binding.ClearVariant);
                Capture(binding.WoundedVariant);
            }
        }

        internal void PresentReplay(ActorWoundSnapshot wounds)
        {
            if (!IsPresentingReplay)
            {
                throw new InvalidOperationException(
                    "Begin wound replay presentation before projecting wounds.");
            }
            Present(wounds);
        }

        internal void EndReplayPresentation()
        {
            if (!IsPresentingReplay)
                return;
            foreach (KeyValuePair<GameObject, bool> entry in originalActiveStates)
            {
                if (entry.Key != null)
                    entry.Key.SetActive(entry.Value);
            }
            originalActiveStates = null;
            CurrentWounds = originalWounds;
            originalWounds = null;
        }

        private void Present(ActorWoundSnapshot wounds)
        {
            foreach (ActorWoundVariantBinding binding in bindings)
                binding?.Present(wounds);
            CurrentWounds = wounds;
        }

        private void Capture(GameObject variant)
        {
            if (variant != null && !originalActiveStates.ContainsKey(variant))
                originalActiveStates.Add(variant, variant.activeSelf);
        }

        private void OnDestroy()
        {
            EndReplayPresentation();
        }
    }
}
