using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Metadata for a playable fighting stage used by StageRoster, Stage Select, and GameSessionState.
    /// </summary>
    [CreateAssetMenu(menuName = "UnDFight/Stage Data", fileName = "StageData_New")]
    public class StageData : ScriptableObject
    {
        public string stageId = "stage_default";
        public string displayName = "ARENA";
        public string subtitle = "Arena de Combate";
        public string sceneName = "Training";
        public Color previewColor = new Color(0.85f, 0.45f, 0.16f, 1f);
        public Color accentColor = new Color(1.0f, 0.72f, 0.0f, 1f);
        public Sprite previewImage;

        [Header("Weather & Time-of-Day Variants")]
        public bool supportsWeatherVariants = false;
        public string dayVariantLabel = "DIA";
        public StageWeatherVariant defaultWeather = StageWeatherVariant.Day;
    }
}
