using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal static class GameplayObjectLifecycle
    {
        public static void Destroy(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (target is GameObject gameObject)
            {
                gameObject.SetActive(false);
            }

            if (UnityEngine.Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
