using UnityEngine;

namespace GritGud.Presentation.LevelEditing.Core
{
    public sealed class LevelSnapSettings
    {
        public bool Enabled { get; set; } = true;

        public Vector3 SnapPosition(Vector3 position, float horizontalStep)
        {
            if (!Enabled)
            {
                return position;
            }

            float step = Mathf.Max(0.01f, horizontalStep);
            return new Vector3(
                Mathf.Round(position.x / step) * step,
                Mathf.Round(position.y / 0.25f) * 0.25f,
                Mathf.Round(position.z / step) * step);
        }
    }
}
