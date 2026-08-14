using GritGud.Presentation.Gameplay;
using UnityEditor;

namespace GritGud.Editor
{
    [CustomEditor(typeof(WeaponPresentationCatalog))]
    public sealed class WeaponPresentationCatalogEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (!DrawDefaultInspector())
            {
                return;
            }

            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
        }
    }
}
