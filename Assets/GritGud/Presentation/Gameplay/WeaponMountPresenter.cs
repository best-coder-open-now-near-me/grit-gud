using System;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    internal sealed class WeaponMountPresenter : MonoBehaviour
    {
        private Transform grip;
        private GameObject heldWeapon;
        private WeaponRigSocketSet sockets;
        private GameplayCelMaterialStyle style;
        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation = Quaternion.identity;
        private bool localPlayerPresentation;

        internal GameObject HeldWeapon => heldWeapon;

        internal Transform Muzzle => sockets?.Muzzle;

        internal WeaponRigSocketSet Sockets => sockets;

        internal void Bind(Transform weaponGrip, bool presentAsLocalPlayer)
        {
            Clear();
            grip = weaponGrip ?? throw new ArgumentNullException(
                nameof(weaponGrip));
            localPlayerPresentation = presentAsLocalPlayer;
        }

        internal WeaponRigSocketSet Mount(
            WeaponPresentationDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (grip == null)
            {
                throw new InvalidOperationException(
                    "A weapon grip must be bound before mounting a weapon.");
            }

            Clear();
            heldWeapon = Instantiate(definition.Prefab, grip, false);
            heldWeapon.name = definition.Prefab.name + " - Held";
            heldWeapon.transform.SetLocalPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
            heldWeapon.transform.localScale = Vector3.one;
            baseLocalPosition = heldWeapon.transform.localPosition;
            baseLocalRotation = heldWeapon.transform.localRotation;
            sockets = heldWeapon.GetComponent<WeaponRigSocketSet>();
            if (sockets == null)
            {
                throw new InvalidOperationException(
                    $"Weapon presentation '{definition.ItemId}' requires a "
                    + "WeaponRigSocketSet on its prefab root.");
            }

            sockets.Validate(definition.ItemId);
            ApplyLayer();
            DisablePhysics(heldWeapon);
            if (heldWeapon.GetComponentInChildren<Renderer>(true) != null)
            {
                style = GameplayCelMaterialStyle.Create(
                    heldWeapon.transform,
                    configureMaterial:
                        ActorCelShadingPresenter.ConfigureActorMaterial);
            }

            ApplyMountOrientation();
            return sockets;
        }

        internal void SetLocalControl(bool controlledLocally)
        {
            localPlayerPresentation = controlledLocally;
            ApplyLayer();
        }

        internal void CaptureBaseLocalPose()
        {
            if (heldWeapon == null)
            {
                return;
            }

            baseLocalPosition = heldWeapon.transform.localPosition;
            baseLocalRotation = heldWeapon.transform.localRotation;
        }

        internal void SetContactSwing(
            float normalizedWeight,
            Vector3 localAxis,
            float degrees)
        {
            if (heldWeapon == null)
            {
                return;
            }

            heldWeapon.transform.localRotation = baseLocalRotation
                * Quaternion.AngleAxis(
                    degrees * Mathf.Clamp01(normalizedWeight),
                    localAxis);
        }

        internal void Clear()
        {
            style?.Dispose();
            style = null;
            GameplayObjectLifecycle.Destroy(heldWeapon);
            heldWeapon = null;
            sockets = null;
            baseLocalPosition = Vector3.zero;
            baseLocalRotation = Quaternion.identity;
        }

        internal void Unbind()
        {
            Clear();
            grip = null;
            localPlayerPresentation = false;
        }

        private void ApplyMountOrientation()
        {
            if (heldWeapon != null)
            {
                heldWeapon.transform.localPosition = baseLocalPosition;
                heldWeapon.transform.localRotation = baseLocalRotation;
            }
        }

        private void ApplyLayer()
        {
            if (heldWeapon == null || grip == null)
            {
                return;
            }

            int localPlayerLayer = LayerMask.NameToLayer(
                GameplayCameraController.LocalPlayerLayerName);
            SetLayerRecursively(
                heldWeapon.transform,
                localPlayerPresentation && localPlayerLayer >= 0
                    ? localPlayerLayer
                    : grip.gameObject.layer);
        }

        private static void DisablePhysics(GameObject visual)
        {
            foreach (Collider collider in
                visual.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (Rigidbody body in
                visual.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root)
            {
                SetLayerRecursively(child, layer);
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
