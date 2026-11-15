using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnDFight
{
    [Serializable]
    public struct ResolutionOption
    {
        public int width;
        public int height;

        public ResolutionOption(int width, int height)
        {
            this.width = width;
            this.height = height;
        }

        public string Label => $"{width} x {height}";
    }

    [Serializable]
    public class PlayerKeyBindings
    {
        public string lightPunch;
        public string heavyPunch;
        public string lightKick;
        public string heavyKick;
        public string block;
        public string special;

        public static PlayerKeyBindings CreateDefaultP1()
        {
            return new PlayerKeyBindings
            {
                lightPunch = Key.J.ToString(),
                heavyPunch = Key.K.ToString(),
                lightKick = Key.U.ToString(),
                heavyKick = Key.I.ToString(),
                block = Key.L.ToString(),
                special = Key.O.ToString()
            };
        }

        public static PlayerKeyBindings CreateDefaultP2()
        {
            return new PlayerKeyBindings
            {
                lightPunch = Key.Numpad4.ToString(),
                heavyPunch = Key.Numpad5.ToString(),
                lightKick = Key.Numpad1.ToString(),
                heavyKick = Key.Numpad2.ToString(),
                block = Key.Numpad6.ToString(),
                special = Key.Numpad3.ToString()
            };
        }

        public PlayerKeyBindings Clone()
        {
            return new PlayerKeyBindings
            {
                lightPunch = lightPunch,
                heavyPunch = heavyPunch,
                lightKick = lightKick,
                heavyKick = heavyKick,
                block = block,
                special = special
            };
        }

        public Key GetKey(FighterInputButton button, int playerIndex)
        {
            string raw = button switch
            {
                FighterInputButton.LightPunch => lightPunch,
                FighterInputButton.HeavyPunch => heavyPunch,
                FighterInputButton.LightKick => lightKick,
                FighterInputButton.HeavyKick => heavyKick,
                FighterInputButton.Block => block,
                FighterInputButton.Special => special,
                _ => string.Empty
            };

            if (!string.IsNullOrEmpty(raw) && Enum.TryParse(raw, true, out Key parsed) && parsed != Key.None)
            {
                return parsed;
            }

            PlayerKeyBindings fallback = playerIndex == 0 ? CreateDefaultP1() : CreateDefaultP2();
            return button switch
            {
                FighterInputButton.LightPunch => Enum.Parse<Key>(fallback.lightPunch),
                FighterInputButton.HeavyPunch => Enum.Parse<Key>(fallback.heavyPunch),
                FighterInputButton.LightKick => Enum.Parse<Key>(fallback.lightKick),
                FighterInputButton.HeavyKick => Enum.Parse<Key>(fallback.heavyKick),
                FighterInputButton.Block => Enum.Parse<Key>(fallback.block),
                FighterInputButton.Special => Enum.Parse<Key>(fallback.special),
                _ => Key.None
            };
        }

        public void SetKey(FighterInputButton button, Key key)
        {
            if (key == Key.None)
            {
                return;
            }

            string value = key.ToString();
            switch (button)
            {
                case FighterInputButton.LightPunch: lightPunch = value; break;
                case FighterInputButton.HeavyPunch: heavyPunch = value; break;
                case FighterInputButton.LightKick: lightKick = value; break;
                case FighterInputButton.HeavyKick: heavyKick = value; break;
                case FighterInputButton.Block: block = value; break;
                case FighterInputButton.Special: special = value; break;
            }
        }
    }

    [Serializable]
    public class GameSettingsData
    {
        public float masterVolume = 1.0f;
        public float musicVolume = 0.8f;
        public float sfxVolume = 0.9f;

        public int resolutionWidth = 1920;
        public int resolutionHeight = 1080;
        public bool fullscreen = true;

        public InputAssignmentMode p1DeviceMode = InputAssignmentMode.Auto;
        public InputAssignmentMode p2DeviceMode = InputAssignmentMode.Auto;
        public PlayerKeyBindings p1Bindings = PlayerKeyBindings.CreateDefaultP1();
        public PlayerKeyBindings p2Bindings = PlayerKeyBindings.CreateDefaultP2();

        public static GameSettingsData CreateDefaults()
        {
            return new GameSettingsData();
        }

        public GameSettingsData Clone()
        {
            return new GameSettingsData
            {
                masterVolume = masterVolume,
                musicVolume = musicVolume,
                sfxVolume = sfxVolume,
                resolutionWidth = resolutionWidth,
                resolutionHeight = resolutionHeight,
                fullscreen = fullscreen,
                p1DeviceMode = p1DeviceMode,
                p2DeviceMode = p2DeviceMode,
                p1Bindings = p1Bindings != null ? p1Bindings.Clone() : PlayerKeyBindings.CreateDefaultP1(),
                p2Bindings = p2Bindings != null ? p2Bindings.Clone() : PlayerKeyBindings.CreateDefaultP2()
            };
        }

        public void ClampAndValidate()
        {
            masterVolume = Mathf.Clamp01(masterVolume);
            musicVolume = Mathf.Clamp01(musicVolume);
            sfxVolume = Mathf.Clamp01(sfxVolume);

            if (resolutionWidth < 640 || resolutionHeight < 360)
            {
                resolutionWidth = 1920;
                resolutionHeight = 1080;
            }

            if (p1Bindings == null) p1Bindings = PlayerKeyBindings.CreateDefaultP1();
            if (p2Bindings == null) p2Bindings = PlayerKeyBindings.CreateDefaultP2();
            if (string.IsNullOrEmpty(p1Bindings.special)) p1Bindings.special = Key.O.ToString();
            if (string.IsNullOrEmpty(p2Bindings.special)) p2Bindings.special = Key.Numpad3.ToString();
        }

        public PlayerKeyBindings GetBindingsForPlayer(int playerIndex)
        {
            return playerIndex == 0 ? p1Bindings : p2Bindings;
        }

        public InputAssignmentMode GetDeviceModeForPlayer(int playerIndex)
        {
            return playerIndex == 0 ? p1DeviceMode : p2DeviceMode;
        }

        public void SetDeviceModeForPlayer(int playerIndex, InputAssignmentMode mode)
        {
            if (playerIndex == 0) p1DeviceMode = mode;
            else p2DeviceMode = mode;
        }
    }

    /// <summary>
    /// JSON persistence and live application of audio, video, and control settings.
    /// </summary>
    public static class SettingsManager
    {
        public const string SettingsFileName = "undfight_settings.json";

        public static readonly ResolutionOption[] SupportedResolutions =
        {
            new ResolutionOption(1280, 720),
            new ResolutionOption(1600, 900),
            new ResolutionOption(1920, 1080),
            new ResolutionOption(2560, 1440)
        };

        private static GameSettingsData _current;

        public static string DefaultFilePath => Path.Combine(Application.persistentDataPath, SettingsFileName);

        public static GameSettingsData Current
        {
            get
            {
                if (_current == null)
                {
                    _current = Load(DefaultFilePath);
                }
                return _current;
            }
        }

        public static event Action<GameSettingsData> OnSettingsApplied;

        public static GameSettingsData Load(string customPath = null)
        {
            string path = string.IsNullOrEmpty(customPath) ? DefaultFilePath : customPath;
            GameSettingsData loaded = null;

            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        loaded = JsonUtility.FromJson<GameSettingsData>(json);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SettingsManager] Failed to parse '{path}', falling back to defaults: {ex.Message}");
                }
            }

            if (loaded == null)
            {
                loaded = GameSettingsData.CreateDefaults();
            }

            loaded.ClampAndValidate();
            if (string.IsNullOrEmpty(customPath) || customPath == DefaultFilePath)
            {
                _current = loaded;
            }
            return loaded;
        }

        public static void Save(GameSettingsData data = null, string customPath = null)
        {
            GameSettingsData target = (data ?? Current).Clone();
            target.ClampAndValidate();

            string path = string.IsNullOrEmpty(customPath) ? DefaultFilePath : customPath;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonUtility.ToJson(target, prettyPrint: true);
            File.WriteAllText(path, json);

            if (string.IsNullOrEmpty(customPath) || customPath == DefaultFilePath)
            {
                _current = target;
            }
        }

        public static GameSettingsData ResetToDefaults(string customPath = null, bool applyAndSave = true)
        {
            GameSettingsData defaults = GameSettingsData.CreateDefaults();
            defaults.ClampAndValidate();

            if (string.IsNullOrEmpty(customPath) || customPath == DefaultFilePath)
            {
                _current = defaults;
            }

            if (applyAndSave)
            {
                Save(defaults, customPath);
                ApplySettings(defaults, applyResolution: false);
            }

            return defaults;
        }

        public static void ApplySettings(GameSettingsData data = null, bool applyResolution = true)
        {
            GameSettingsData target = data ?? Current;
            target.ClampAndValidate();

            if (Application.isPlaying)
            {
                AudioListener.volume = target.masterVolume;
                if (applyResolution)
                {
                    FullScreenMode fsMode = target.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                    Screen.SetResolution(target.resolutionWidth, target.resolutionHeight, fsMode);
                }

                PlayerInputHandler[] handlers = UnityEngine.Object.FindObjectsByType<PlayerInputHandler>(FindObjectsSortMode.None);
                for (int i = 0; i < handlers.Length; i++)
                {
                    handlers[i].ApplySettings(target);
                }
            }

            OnSettingsApplied?.Invoke(target);
        }

        public static int FindResolutionIndex(int width, int height)
        {
            for (int i = 0; i < SupportedResolutions.Length; i++)
            {
                if (SupportedResolutions[i].width == width && SupportedResolutions[i].height == height)
                {
                    return i;
                }
            }
            return 2; // 1920x1080 default
        }

        public static string FormatDeviceModeLabel(InputAssignmentMode mode)
        {
            return mode switch
            {
                InputAssignmentMode.Auto => "Auto (KB + Pad)",
                InputAssignmentMode.KeyboardOnly => "Apenas Teclado",
                InputAssignmentMode.Gamepad0 => "Gamepad 1",
                InputAssignmentMode.Gamepad1 => "Gamepad 2",
                _ => mode.ToString()
            };
        }
    }
}
