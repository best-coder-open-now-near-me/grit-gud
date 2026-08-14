using System;
using System.Collections.Generic;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal static class GameplayGroundPlacement
    {
        public static void PlaceOnGround(Transform actor, Vector3 authoredPosition)
        {
            Collider[] actorColliders = actor.GetComponentsInChildren<Collider>();
            var enabledStates = new List<bool>(actorColliders.Length);
            foreach (Collider actorCollider in actorColliders)
            {
                enabledStates.Add(actorCollider.enabled);
                actorCollider.enabled = false;
            }

            Vector3 rayOrigin = authoredPosition + (Vector3.up * 8f);
            bool foundGround = Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                20f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < actorColliders.Length; index++)
            {
                actorColliders[index].enabled = enabledStates[index];
            }

            if (!foundGround)
            {
                throw new InvalidOperationException(
                    $"Gameplay spawn at {authoredPosition} does not have walkable geometry below it.");
            }

            float rootToBottom = 0f;
            if (actor.TryGetComponent(out CharacterController characterController))
            {
                rootToBottom = characterController.center.y -
                    (characterController.height * 0.5f);
            }
            else if (actor.TryGetComponent(out Collider actorCollider))
            {
                rootToBottom = actorCollider.bounds.min.y - actor.position.y;
            }

            actor.position = new Vector3(
                authoredPosition.x,
                hit.point.y - rootToBottom + 0.02f,
                authoredPosition.z);
        }
    }
}
