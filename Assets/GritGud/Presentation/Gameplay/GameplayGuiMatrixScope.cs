using System;
using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal readonly struct GameplayGuiMatrixScope : IDisposable
    {
        private readonly Matrix4x4 previousMatrix;

        public GameplayGuiMatrixScope(Matrix4x4 matrix)
        {
            previousMatrix = GUI.matrix;
            GUI.matrix = matrix;
        }

        public void Dispose()
        {
            GUI.matrix = previousMatrix;
        }
    }
}
