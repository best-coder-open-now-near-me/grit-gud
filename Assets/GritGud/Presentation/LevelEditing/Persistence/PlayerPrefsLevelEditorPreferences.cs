using GritGud.Presentation.LevelEditing.Core;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.Persistence
{
    [System.Serializable]
    public sealed class LevelEditorLocalPreferences
    {
        public bool snapEnabled = true;
        public bool gridVisible = true;
        public float gridSpacing = 2.5f;
        public float gridElevation;
        public LevelEditorCameraState camera;
    }

    public interface ILevelEditorPreferencesStore
    {
        LevelEditorLocalPreferences Load();

        void Save(LevelEditorLocalPreferences preferences);
    }

    public sealed class PlayerPrefsLevelEditorPreferences : ILevelEditorPreferencesStore
    {
        public const string StorageKey = "GritGud.LevelEditor.LocalPreferences.v1";

        public LevelEditorLocalPreferences Load()
        {
            if (!PlayerPrefs.HasKey(StorageKey))
            {
                return new LevelEditorLocalPreferences();
            }

            string serialized = PlayerPrefs.GetString(StorageKey);
            try
            {
                LevelEditorLocalPreferences preferences =
                    JsonUtility.FromJson<LevelEditorLocalPreferences>(serialized);
                return preferences ?? new LevelEditorLocalPreferences();
            }
            catch (System.ArgumentException)
            {
                return new LevelEditorLocalPreferences();
            }
        }

        public void Save(LevelEditorLocalPreferences preferences)
        {
            if (preferences == null)
            {
                return;
            }

            PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(preferences));
            PlayerPrefs.Save();
        }
    }
}
