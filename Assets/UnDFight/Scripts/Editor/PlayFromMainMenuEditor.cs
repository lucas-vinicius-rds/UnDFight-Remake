using UnityEditor;
using UnityEditor.SceneManagement;

namespace UnDFight.Editor
{
    /// <summary>
    /// Ensures Play Mode in the Unity Editor always boots from MainMenu.unity by default.
    /// Can be toggled via UnDFight/Iniciar Play Mode no Menu Principal for isolated scene testing.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromMainMenuEditor
    {
        public const string PrefKey = "UnDFight.PlayFromMainMenu";
        public const string MenuPath = "UnDFight/Iniciar Play Mode no Menu Principal";
        public const string MainMenuScenePath = "Assets/UnDFight/Scenes/MainMenu.unity";

        static PlayFromMainMenuEditor()
        {
            ApplyPlayModeStartScene();
        }

        public static bool IsEnabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set
            {
                EditorPrefs.SetBool(PrefKey, value);
                ApplyPlayModeStartScene();
            }
        }

        public static void ApplyPlayModeStartScene()
        {
            if (IsEnabled)
            {
                SceneAsset mainMenu = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath);
                EditorSceneManager.playModeStartScene = mainMenu;
            }
            else
            {
                EditorSceneManager.playModeStartScene = null;
            }
        }

        [MenuItem(MenuPath, false, 100)]
        private static void TogglePlayFromMainMenu()
        {
            IsEnabled = !IsEnabled;
        }

        [MenuItem(MenuPath, true)]
        private static bool TogglePlayFromMainMenuValidate()
        {
            Menu.SetChecked(MenuPath, IsEnabled);
            return true;
        }
    }
}
