using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnDFight
{
    [Serializable]
    public class GameProgressionData
    {
        public List<string> unlockedFighterNames = new List<string>();
        public int towerCompletionsCount = 0;
        public List<string> towerCompletedByFighters = new List<string>();
        public int highestTowerDifficultyCleared = -1;

        public static GameProgressionData CreateDefaults()
        {
            return new GameProgressionData();
        }

        public GameProgressionData Clone()
        {
            return new GameProgressionData
            {
                unlockedFighterNames = unlockedFighterNames != null
                    ? new List<string>(unlockedFighterNames)
                    : new List<string>(),
                towerCompletionsCount = towerCompletionsCount,
                towerCompletedByFighters = towerCompletedByFighters != null
                    ? new List<string>(towerCompletedByFighters)
                    : new List<string>(),
                highestTowerDifficultyCleared = highestTowerDifficultyCleared
            };
        }

        public void Validate()
        {
            unlockedFighterNames = SanitizeUniqueNames(unlockedFighterNames);
            towerCompletedByFighters = SanitizeUniqueNames(towerCompletedByFighters);
            if (towerCompletionsCount < 0)
            {
                towerCompletionsCount = 0;
            }
            if (highestTowerDifficultyCleared < -1)
            {
                highestTowerDifficultyCleared = -1;
            }
            else if (highestTowerDifficultyCleared > (int)CpuDifficulty.Hard)
            {
                highestTowerDifficultyCleared = (int)CpuDifficulty.Hard;
            }
        }

        public bool ContainsUnlockedFighterName(string fighterName)
        {
            if (string.IsNullOrWhiteSpace(fighterName) || unlockedFighterNames == null)
            {
                return false;
            }

            string trimmed = fighterName.Trim();
            for (int i = 0; i < unlockedFighterNames.Count; i++)
            {
                if (string.Equals(unlockedFighterNames[i], trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public bool ContainsTowerWinnerName(string fighterName)
        {
            if (string.IsNullOrWhiteSpace(fighterName) || towerCompletedByFighters == null)
            {
                return false;
            }

            string trimmed = fighterName.Trim();
            for (int i = 0; i < towerCompletedByFighters.Count; i++)
            {
                if (string.Equals(towerCompletedByFighters[i], trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static List<string> SanitizeUniqueNames(List<string> source)
        {
            var result = new List<string>();
            if (source == null)
            {
                return result;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < source.Count; i++)
            {
                string raw = source[i];
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                string trimmed = raw.Trim();
                if (seen.Add(trimmed))
                {
                    result.Add(trimmed);
                }
            }
            return result;
        }
    }

    /// <summary>
    /// JSON persistence for player progression: permanent fighter unlocks (such as secret boss Ibaneis)
    /// and Arcade Tower completion statistics. Supports isolated custom paths for unit tests.
    /// </summary>
    public static class ProgressionManager
    {
        public const string ProgressFileName = "undfight_progress.json";

        private static GameProgressionData _current;
        private static string _customPathOverride;

        public static string DefaultFilePath => Path.Combine(Application.persistentDataPath, ProgressFileName);
        public static string ActiveFilePath => string.IsNullOrEmpty(_customPathOverride) ? DefaultFilePath : _customPathOverride;

        public static GameProgressionData Current
        {
            get
            {
                if (_current == null)
                {
                    _current = Load(ActiveFilePath);
                }
                return _current;
            }
        }

        public static event Action<GameProgressionData> OnProgressChanged;

        /// <summary>
        /// Redirects default Load/Save/Current/IsFighterUnlocked calls to an isolated file path (intended for tests).
        /// Pass null or empty string to restore the standard persistentDataPath location.
        /// </summary>
        public static void SetCustomFilePathForTesting(string customPath)
        {
            _customPathOverride = string.IsNullOrWhiteSpace(customPath) ? null : customPath;
            _current = null;
        }

        public static GameProgressionData Load(string customPath = null)
        {
            string path = ResolvePath(customPath);
            GameProgressionData loaded = null;

            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        loaded = JsonUtility.FromJson<GameProgressionData>(json);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ProgressionManager] Failed to parse '{path}', falling back to defaults: {ex.Message}");
                }
            }

            if (loaded == null)
            {
                loaded = GameProgressionData.CreateDefaults();
            }

            loaded.Validate();
            if (IsActivePath(customPath))
            {
                _current = loaded;
            }
            return loaded;
        }

        public static void Save(GameProgressionData data = null, string customPath = null)
        {
            GameProgressionData target = (data ?? Current).Clone();
            target.Validate();

            string path = ResolvePath(customPath);
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonUtility.ToJson(target, prettyPrint: true);
            File.WriteAllText(path, json);

            if (IsActivePath(customPath))
            {
                _current = target;
                OnProgressChanged?.Invoke(_current);
            }
        }

        public static GameProgressionData ResetProgress(string customPath = null, bool save = true)
        {
            GameProgressionData defaults = GameProgressionData.CreateDefaults();
            defaults.Validate();

            bool isActive = IsActivePath(customPath);
            if (isActive)
            {
                _current = defaults;
            }

            if (save)
            {
                Save(defaults, customPath);
            }
            else if (isActive)
            {
                OnProgressChanged?.Invoke(_current);
            }

            return defaults;
        }

        public static bool IsFighterUnlocked(FighterData data)
        {
            return IsFighterUnlocked(data, Current);
        }

        public static bool IsFighterUnlocked(FighterData data, GameProgressionData progress)
        {
            if (data == null)
            {
                return false;
            }

            if (data.unlockedByDefault)
            {
                return true;
            }

            GameProgressionData source = progress ?? Current;
            return source != null && source.ContainsUnlockedFighterName(data.fighterName);
        }

        public static bool UnlockFighter(FighterData data, string customPath = null)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.fighterName))
            {
                return false;
            }

            bool isActive = IsActivePath(customPath);
            GameProgressionData target = isActive ? Current : Load(customPath);
            target.Validate();

            if (target.ContainsUnlockedFighterName(data.fighterName))
            {
                return false;
            }

            target.unlockedFighterNames.Add(data.fighterName.Trim());
            Save(target, customPath);
            return true;
        }

        public static bool RecordTowerVictory(
            FighterData playerFighter,
            CpuDifficulty difficulty,
            FighterData bossToUnlock,
            string customPath = null)
        {
            bool isActive = IsActivePath(customPath);
            GameProgressionData target = isActive ? Current : Load(customPath);
            target.Validate();

            target.towerCompletionsCount++;

            if (playerFighter != null && !string.IsNullOrWhiteSpace(playerFighter.fighterName))
            {
                if (!target.ContainsTowerWinnerName(playerFighter.fighterName))
                {
                    target.towerCompletedByFighters.Add(playerFighter.fighterName.Trim());
                }
            }

            int diffInt = Mathf.Clamp((int)difficulty, (int)CpuDifficulty.Easy, (int)CpuDifficulty.Hard);
            if (diffInt > target.highestTowerDifficultyCleared)
            {
                target.highestTowerDifficultyCleared = diffInt;
            }

            bool unlockedBossForFirstTime = false;
            if (bossToUnlock != null && !string.IsNullOrWhiteSpace(bossToUnlock.fighterName))
            {
                if (!target.ContainsUnlockedFighterName(bossToUnlock.fighterName))
                {
                    target.unlockedFighterNames.Add(bossToUnlock.fighterName.Trim());
                    unlockedBossForFirstTime = true;
                }
            }

            Save(target, customPath);
            return unlockedBossForFirstTime;
        }

        private static string ResolvePath(string customPath)
        {
            return string.IsNullOrEmpty(customPath) ? ActiveFilePath : customPath;
        }

        private static bool IsActivePath(string customPath)
        {
            return string.IsNullOrEmpty(customPath) ||
                   string.Equals(customPath, ActiveFilePath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
