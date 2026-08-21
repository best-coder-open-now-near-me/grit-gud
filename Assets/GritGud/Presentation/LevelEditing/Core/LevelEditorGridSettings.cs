using System;

namespace GritGud.Presentation.LevelEditing.Core
{
    [Serializable]
    public sealed class LevelEditorGridSettings
    {
        private bool visible = true;
        private float spacing = 2.5f;
        private float elevation;

        public event Action Changed;

        public bool Visible => visible;

        public float Spacing => spacing;

        public float Elevation => elevation;

        public void Configure(bool isVisible, float gridSpacing, float gridElevation)
        {
            if (gridSpacing <= 0f || float.IsNaN(gridSpacing) || float.IsInfinity(gridSpacing))
                throw new ArgumentOutOfRangeException(nameof(gridSpacing));
            if (float.IsNaN(gridElevation) || float.IsInfinity(gridElevation))
                throw new ArgumentOutOfRangeException(nameof(gridElevation));
            if (visible == isVisible
                && Math.Abs(spacing - gridSpacing) < 0.0001f
                && Math.Abs(elevation - gridElevation) < 0.0001f)
            {
                return;
            }

            visible = isVisible;
            spacing = gridSpacing;
            elevation = gridElevation;
            Changed?.Invoke();
        }
    }
}
