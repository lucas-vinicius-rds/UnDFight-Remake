using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace UnDFight
{
    /// <summary>
    /// Reusable stage weather & time-of-day controller supporting Day, Night, and RainyNight presets.
    /// Drives Directional Light, Ambient/Fog/Skybox, night-only lights/windows, wind presets,
    /// real 3D wind-tilted rain particles with StagePhysics/StageDebris collision + splash sub-emitters,
    /// wet surface MaterialPropertyBlocks (never modifying disk .mat assets), subtle lightning flashes,
    /// and procedural weather ambience audio.
    /// </summary>
    public class StageWeatherController : MonoBehaviour
    {
        [Serializable]
        public struct WeatherProfile
        {
            public Color directionalLightColor;
            public float directionalLightIntensity;
            public Vector3 directionalLightEuler;

            public Color ambientSkyColor;
            public Color ambientEquatorColor;
            public Color ambientGroundColor;

            public bool fogEnabled;
            public Color fogColor;
            public float fogDensity;

            public Color cameraBackgroundColor;

            public Color skyTintColor;
            public Color skyGroundColor;
            public float skyExposure;
            public float skyAtmosphereThickness;

            public float windBaseSpeed;
            public float windGustAmplitude;
            public float windGustFrequency;
            public float secondaryLightsMultiplier;

            public static WeatherProfile CreateDefaultDay()
            {
                return new WeatherProfile
                {
                    directionalLightColor = new Color(1.00f, 0.74f, 0.48f, 1f),
                    directionalLightIntensity = 1.65f,
                    directionalLightEuler = new Vector3(24f, -35f, 0f),
                    ambientSkyColor = new Color(0.86f, 0.58f, 0.42f, 1f),
                    ambientEquatorColor = new Color(0.68f, 0.48f, 0.36f, 1f),
                    ambientGroundColor = new Color(0.28f, 0.22f, 0.18f, 1f),
                    fogEnabled = true,
                    fogColor = new Color(0.82f, 0.56f, 0.38f, 1f),
                    fogDensity = 0.008f,
                    cameraBackgroundColor = new Color(0.75f, 0.48f, 0.32f, 1f),
                    skyTintColor = new Color(0.88f, 0.56f, 0.38f, 1f),
                    skyGroundColor = new Color(0.36f, 0.26f, 0.20f, 1f),
                    skyExposure = 1.15f,
                    skyAtmosphereThickness = 1.45f,
                    windBaseSpeed = 0.55f,
                    windGustAmplitude = 0.45f,
                    windGustFrequency = 0.22f,
                    secondaryLightsMultiplier = 1.0f
                };
            }

            public static WeatherProfile CreateDefaultNight()
            {
                return new WeatherProfile
                {
                    directionalLightColor = new Color(0.44f, 0.58f, 0.92f, 1f),
                    directionalLightIntensity = 0.48f,
                    directionalLightEuler = new Vector3(38f, -125f, 0f),
                    ambientSkyColor = new Color(0.11f, 0.16f, 0.28f, 1f),
                    ambientEquatorColor = new Color(0.14f, 0.16f, 0.24f, 1f),
                    ambientGroundColor = new Color(0.06f, 0.06f, 0.09f, 1f),
                    fogEnabled = true,
                    fogColor = new Color(0.08f, 0.11f, 0.19f, 1f),
                    fogDensity = 0.015f,
                    cameraBackgroundColor = new Color(0.05f, 0.07f, 0.14f, 1f),
                    skyTintColor = new Color(0.08f, 0.12f, 0.24f, 1f),
                    skyGroundColor = new Color(0.04f, 0.05f, 0.09f, 1f),
                    skyExposure = 0.42f,
                    skyAtmosphereThickness = 0.65f,
                    windBaseSpeed = 1.05f,
                    windGustAmplitude = 0.75f,
                    windGustFrequency = 0.30f,
                    secondaryLightsMultiplier = 0.38f
                };
            }

            public static WeatherProfile CreateDefaultRainyNight()
            {
                return new WeatherProfile
                {
                    directionalLightColor = new Color(0.40f, 0.52f, 0.78f, 1f),
                    directionalLightIntensity = 0.36f,
                    directionalLightEuler = new Vector3(44f, -135f, 0f),
                    ambientSkyColor = new Color(0.09f, 0.13f, 0.22f, 1f),
                    ambientEquatorColor = new Color(0.12f, 0.15f, 0.21f, 1f),
                    ambientGroundColor = new Color(0.05f, 0.06f, 0.08f, 1f),
                    fogEnabled = true,
                    fogColor = new Color(0.07f, 0.10f, 0.16f, 1f),
                    fogDensity = 0.024f,
                    cameraBackgroundColor = new Color(0.04f, 0.06f, 0.11f, 1f),
                    skyTintColor = new Color(0.07f, 0.10f, 0.18f, 1f),
                    skyGroundColor = new Color(0.03f, 0.04f, 0.07f, 1f),
                    skyExposure = 0.32f,
                    skyAtmosphereThickness = 0.50f,
                    windBaseSpeed = 6.2f,
                    windGustAmplitude = 4.5f,
                    windGustFrequency = 0.65f,
                    secondaryLightsMultiplier = 0.28f
                };
            }
        }

        [Header("Stage & Scene References")]
        [SerializeField] private StageData stageData;
        [SerializeField] private Light mainDirectionalLight;
        [SerializeField] private Light[] secondaryFillLights = Array.Empty<Light>();
        [SerializeField] private float[] secondaryFillBaseIntensities = Array.Empty<float>();
        [SerializeField] private StageWindController windController;
        [SerializeField] private GameObject[] nightOnlyObjects = Array.Empty<GameObject>();
        [SerializeField] private Renderer[] wetSurfaceRenderers = Array.Empty<Renderer>();

        [Header("Weather Profiles")]
        [SerializeField] private WeatherProfile dayProfile = WeatherProfile.CreateDefaultDay();
        [SerializeField] private WeatherProfile nightProfile = WeatherProfile.CreateDefaultNight();
        [SerializeField] private WeatherProfile rainyNightProfile = WeatherProfile.CreateDefaultRainyNight();

        [Header("3D Rain & Wet Surface Tuning")]
        [SerializeField] private ParticleSystem rainParticleSystem;
        [SerializeField] private ParticleSystem rainSplashSubEmitter;
        [SerializeField] private Vector3 rainEmitterOffset = new Vector3(0f, 10.5f, 1.5f);
        [SerializeField] private Vector3 rainEmitterBoxSize = new Vector3(26f, 0.8f, 14f);
        [SerializeField, Min(50f)] private float rainEmissionRate = 620f;
        [SerializeField, Min(4f)] private float rainFallSpeed = 16.5f;
        [SerializeField, Range(0f, 1f)] private float wetSurfaceSmoothness = 0.86f;
        [SerializeField, Range(0.3f, 1f)] private float wetAlbedoDarkenMultiplier = 0.72f;

        [Header("Storm Lightning & Controls")]
        [SerializeField] private bool applySessionWeatherOnStart = true;
        [SerializeField] private bool allowF6ToggleInTraining = true;
        [SerializeField] private bool enableLightningFlashes = true;
        [SerializeField, Min(3f)] private float minLightningInterval = 9.0f;
        [SerializeField, Min(4f)] private float maxLightningInterval = 18.0f;

        private StageWeatherVariant _currentWeather = StageWeatherVariant.Day;
        private Material _runtimeSkyboxMaterial;
        private Material _originalSkyboxMaterial;
        private Material _runtimeRainMaterial;
        private Material _runtimeSplashMaterial;
        private MaterialPropertyBlock _wetPropertyBlock;
        private float _lightningCountdown;
        private float _lightningFlashTimer;
        private float _baseDirLightIntensity = 1f;
        private Color _baseDirLightColor = Color.white;
        private bool _appliedOnce;

        private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropId = Shader.PropertyToID("_Color");
        private static readonly int SmoothnessPropId = Shader.PropertyToID("_Smoothness");

        public StageData StageData { get => stageData; set => stageData = value; }
        public StageWeatherVariant CurrentWeather => _currentWeather;
        public Light MainDirectionalLight => mainDirectionalLight;
        public StageWindController WindController => windController;
        public ParticleSystem RainParticleSystem => rainParticleSystem;
        public ParticleSystem RainSplashSubEmitter => rainSplashSubEmitter;
        public GameObject[] NightOnlyObjects => nightOnlyObjects;
        public Renderer[] WetSurfaceRenderers => wetSurfaceRenderers;
        public bool IsWetSurfaceActive => _currentWeather == StageWeatherVariant.RainyNight;

        private void Awake()
        {
            ResolveReferences();
            _originalSkyboxMaterial = RenderSettings.skybox;
            _wetPropertyBlock = new MaterialPropertyBlock();
            _lightningCountdown = UnityEngine.Random.Range(minLightningInterval, maxLightningInterval);
        }

        private void Start()
        {
            if (applySessionWeatherOnStart && !_appliedOnce)
            {
                ApplyWeather(GameSessionState.SelectedWeather);
            }
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                FightAudioManager.Instance?.StopWeatherAmbience();
            }
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
            {
                FightAudioManager.Instance?.StopWeatherAmbience();
            }

            if (_runtimeSkyboxMaterial != null)
            {
                if (RenderSettings.skybox == _runtimeSkyboxMaterial)
                {
                    RenderSettings.skybox = _originalSkyboxMaterial;
                }
                DestroySafe(_runtimeSkyboxMaterial);
                _runtimeSkyboxMaterial = null;
            }
            if (_runtimeRainMaterial != null)
            {
                DestroySafe(_runtimeRainMaterial);
                _runtimeRainMaterial = null;
            }
            if (_runtimeSplashMaterial != null)
            {
                DestroySafe(_runtimeSplashMaterial);
                _runtimeSplashMaterial = null;
            }
        }

        public void ConfigureReferences(
            StageData data,
            Light dirLight,
            StageWindController wind,
            GameObject[] nightObjs = null,
            Renderer[] wetRenderers = null)
        {
            stageData = data;
            if (dirLight != null) mainDirectionalLight = dirLight;
            if (wind != null) windController = wind;
            if (nightObjs != null) nightOnlyObjects = nightObjs;
            if (wetRenderers != null) wetSurfaceRenderers = wetRenderers;
        }

        public void SetProfiles(WeatherProfile day, WeatherProfile night, WeatherProfile rainyNight)
        {
            dayProfile = day;
            nightProfile = night;
            rainyNightProfile = rainyNight;
        }

        /// <summary>
        /// Applies the requested weather variant. If StageData is assigned and supportsWeatherVariants == false,
        /// strictly forces StageData.defaultWeather so fixed-weather arenas ignore session weather overrides.
        /// </summary>
        public void ApplyWeather(StageWeatherVariant requestedVariant)
        {
            ResolveReferences();
            _appliedOnce = true;

            StageWeatherVariant effective = requestedVariant;
            if (stageData != null && !stageData.supportsWeatherVariants)
            {
                effective = stageData.defaultWeather;
            }

            _currentWeather = effective;
            WeatherProfile profile = GetProfile(effective);

            if (mainDirectionalLight != null)
            {
                mainDirectionalLight.color = profile.directionalLightColor;
                mainDirectionalLight.intensity = profile.directionalLightIntensity;
                mainDirectionalLight.transform.rotation = Quaternion.Euler(profile.directionalLightEuler);
                _baseDirLightColor = profile.directionalLightColor;
                _baseDirLightIntensity = profile.directionalLightIntensity;
            }

            if (secondaryFillLights != null && secondaryFillLights.Length > 0)
            {
                float mult = profile.secondaryLightsMultiplier > 0.001f ? profile.secondaryLightsMultiplier : 1f;
                for (int i = 0; i < secondaryFillLights.Length; i++)
                {
                    if (secondaryFillLights[i] == null) continue;
                    float baseInt = (secondaryFillBaseIntensities != null && i < secondaryFillBaseIntensities.Length)
                        ? secondaryFillBaseIntensities[i]
                        : secondaryFillLights[i].intensity;
                    secondaryFillLights[i].intensity = baseInt * mult;
                }
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = profile.ambientSkyColor;
            RenderSettings.ambientEquatorColor = profile.ambientEquatorColor;
            RenderSettings.ambientGroundColor = profile.ambientGroundColor;
            RenderSettings.ambientLight = profile.ambientEquatorColor;

            RenderSettings.fog = profile.fogEnabled;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = profile.fogColor;
            RenderSettings.fogDensity = profile.fogDensity;

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.backgroundColor = profile.cameraBackgroundColor;
            }

            ApplyRuntimeSkyboxProfile(profile);

            bool nightActive = effective == StageWeatherVariant.Night || effective == StageWeatherVariant.RainyNight;
            SetNightObjectsActive(nightActive);

            if (windController != null)
            {
                windController.BaseWindSpeed = profile.windBaseSpeed;
                windController.GustAmplitude = profile.windGustAmplitude;
                windController.GustFrequency = profile.windGustFrequency;
            }

            bool isRain = effective == StageWeatherVariant.RainyNight;
            if (isRain)
            {
                EnsureRainSystem();
                if (rainParticleSystem != null)
                {
                    rainParticleSystem.gameObject.SetActive(true);
                    UpdateRainWindTilt();
                    if (!rainParticleSystem.isPlaying)
                    {
                        rainParticleSystem.Play(withChildren: true);
                    }
                }
                _lightningFlashTimer = 0f;
                _lightningCountdown = UnityEngine.Random.Range(minLightningInterval, maxLightningInterval);
            }
            else if (rainParticleSystem != null)
            {
                rainParticleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
                rainParticleSystem.gameObject.SetActive(false);
            }

            ApplyWetSurfaces(isRain);

            if (Application.isPlaying)
            {
                FightAudioManager.Instance?.SetWeatherAmbience(effective, enabled: true);
            }
        }

        public WeatherProfile GetProfile(StageWeatherVariant variant)
        {
            return variant switch
            {
                StageWeatherVariant.Night => nightProfile,
                StageWeatherVariant.RainyNight => rainyNightProfile,
                _ => dayProfile
            };
        }

        public void EnsureRainSystem()
        {
            if (rainParticleSystem != null)
            {
                ConfigureRainCollisionLayers();
                return;
            }

            Transform existing = transform.Find("Rain3D_ParticleSystem");
            GameObject rainGo = existing != null ? existing.gameObject : new GameObject("Rain3D_ParticleSystem");
            if (existing == null)
            {
                rainGo.transform.SetParent(transform, false);
            }
            rainGo.transform.localPosition = rainEmitterOffset;
            rainGo.transform.localRotation = Quaternion.identity;
            rainGo.transform.localScale = Vector3.one;

            rainParticleSystem = rainGo.GetComponent<ParticleSystem>();
            if (rainParticleSystem == null)
            {
                rainParticleSystem = rainGo.AddComponent<ParticleSystem>();
            }
            rainParticleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = rainParticleSystem.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.85f, 1.30f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.022f);
            main.startColor = new Color(0.78f, 0.88f, 1.0f, 0.42f);
            main.maxParticles = 1400;
            main.gravityModifier = 0f;

            var emission = rainParticleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = rainEmissionRate;

            var shape = rainParticleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = rainEmitterBoxSize;

            var vel = rainParticleSystem.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(2.2f);
            vel.y = new ParticleSystem.MinMaxCurve(-rainFallSpeed);
            vel.z = new ParticleSystem.MinMaxCurve(0.25f);

            var psRenderer = rainGo.GetComponent<ParticleSystemRenderer>();
            if (psRenderer != null)
            {
                psRenderer.renderMode = ParticleSystemRenderMode.Stretch;
                psRenderer.lengthScale = 4.6f;
                psRenderer.velocityScale = 0.045f;
                psRenderer.shadowCastingMode = ShadowCastingMode.Off;
                psRenderer.receiveShadows = false;
                if (_runtimeRainMaterial == null)
                {
                    _runtimeRainMaterial = CreateRuntimeParticleMaterial(new Color(0.78f, 0.90f, 1.0f, 0.36f));
                }
                if (_runtimeRainMaterial != null)
                {
                    psRenderer.sharedMaterial = _runtimeRainMaterial;
                }
            }

            // Create Splash Sub-Emitter on collision
            Transform existingSplash = rainGo.transform.Find("RainSplash_SubEmitter");
            GameObject splashGo = existingSplash != null ? existingSplash.gameObject : new GameObject("RainSplash_SubEmitter");
            if (existingSplash == null)
            {
                splashGo.transform.SetParent(rainGo.transform, false);
            }
            splashGo.transform.localPosition = Vector3.zero;
            rainSplashSubEmitter = splashGo.GetComponent<ParticleSystem>();
            if (rainSplashSubEmitter == null)
            {
                rainSplashSubEmitter = splashGo.AddComponent<ParticleSystem>();
            }
            rainSplashSubEmitter.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var splashMain = rainSplashSubEmitter.main;
            splashMain.loop = false;
            splashMain.playOnAwake = false;
            splashMain.duration = 0.20f;
            splashMain.simulationSpace = ParticleSystemSimulationSpace.World;
            splashMain.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            splashMain.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 2.1f);
            splashMain.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.065f);
            splashMain.startColor = new Color(0.82f, 0.90f, 1.0f, 0.50f);
            splashMain.gravityModifier = 0.85f;
            splashMain.maxParticles = 450;

            var splashEmission = rainSplashSubEmitter.emission;
            splashEmission.enabled = true;
            splashEmission.rateOverTime = 0f;
            splashEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 2, 4) });

            var splashShape = rainSplashSubEmitter.shape;
            splashShape.enabled = true;
            splashShape.shapeType = ParticleSystemShapeType.Hemisphere;
            splashShape.radius = 0.04f;

            var splashRenderer = splashGo.GetComponent<ParticleSystemRenderer>();
            if (splashRenderer != null)
            {
                splashRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                splashRenderer.shadowCastingMode = ShadowCastingMode.Off;
                splashRenderer.receiveShadows = false;
                if (_runtimeSplashMaterial == null)
                {
                    _runtimeSplashMaterial = CreateRuntimeParticleMaterial(new Color(0.84f, 0.92f, 1.0f, 0.45f));
                }
                if (_runtimeSplashMaterial != null)
                {
                    splashRenderer.sharedMaterial = _runtimeSplashMaterial;
                }
            }

            ConfigureRainCollisionLayers();

            var subEmitters = rainParticleSystem.subEmitters;
            subEmitters.enabled = true;
            while (subEmitters.subEmittersCount > 0)
            {
                subEmitters.RemoveSubEmitter(0);
            }
            subEmitters.AddSubEmitter(
                rainSplashSubEmitter,
                ParticleSystemSubEmitterType.Collision,
                ParticleSystemSubEmitterProperties.InheritNothing);
        }

        private void ConfigureRainCollisionLayers()
        {
            if (rainParticleSystem == null)
            {
                return;
            }

            var collision = rainParticleSystem.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.dampen = 1f;
            collision.bounce = 0f;
            collision.lifetimeLoss = 1f;
            collision.radiusScale = 0.25f;

            int stagePhysics = LayerMask.NameToLayer(StagePhysicsLayers.StagePhysicsLayerName);
            int stageDebris = LayerMask.NameToLayer(StagePhysicsLayers.StageDebrisLayerName);
            int mask = 0;
            if (stagePhysics >= 0) mask |= (1 << stagePhysics);
            if (stageDebris >= 0) mask |= (1 << stageDebris);
            if (mask == 0) mask = ~0;
            collision.collidesWith = mask;
        }

        private void Update()
        {
            if (_currentWeather == StageWeatherVariant.RainyNight)
            {
                UpdateRainWindTilt();
                if (enableLightningFlashes && Application.isPlaying)
                {
                    UpdateLightning(Time.deltaTime);
                }
            }

            if (allowF6ToggleInTraining && Application.isPlaying && GameSessionState.IsTrainingMode)
            {
                Keyboard kb = Keyboard.current;
                if (kb != null && kb.f6Key.wasPressedThisFrame)
                {
                    int next = (((int)_currentWeather) + 1) % 3;
                    StageWeatherVariant nextVariant = (StageWeatherVariant)next;
                    GameSessionState.SelectWeather(nextVariant);
                    ApplyWeather(nextVariant);
                }
            }
        }

        public void UpdateRainWindTilt()
        {
            if (rainParticleSystem == null)
            {
                return;
            }

            Vector3 wind = windController != null ? windController.CurrentWindVelocity : new Vector3(3.2f, 0f, 0.4f);
            var vel = rainParticleSystem.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(wind.x * 0.72f);
            vel.y = new ParticleSystem.MinMaxCurve(-rainFallSpeed);
            vel.z = new ParticleSystem.MinMaxCurve(wind.z * 0.72f);
        }

        private void UpdateLightning(float dt)
        {
            if (mainDirectionalLight == null)
            {
                return;
            }

            if (_lightningFlashTimer > 0f)
            {
                _lightningFlashTimer = Mathf.Max(0f, _lightningFlashTimer - dt);
                float t = _lightningFlashTimer / 0.22f;
                float pulse = Mathf.Sin(t * Mathf.PI * 2f) * 0.5f + 0.5f;
                mainDirectionalLight.intensity = _baseDirLightIntensity + 1.85f * t * pulse;
                mainDirectionalLight.color = Color.Lerp(_baseDirLightColor, new Color(0.84f, 0.92f, 1.0f, 1f), t);
                if (_lightningFlashTimer <= 0f)
                {
                    mainDirectionalLight.intensity = _baseDirLightIntensity;
                    mainDirectionalLight.color = _baseDirLightColor;
                }
                return;
            }

            _lightningCountdown -= dt;
            if (_lightningCountdown <= 0f)
            {
                _lightningFlashTimer = 0.22f;
                _lightningCountdown = UnityEngine.Random.Range(minLightningInterval, maxLightningInterval);
            }
        }

        private void ApplyWetSurfaces(bool wet)
        {
            if (wetSurfaceRenderers == null || wetSurfaceRenderers.Length == 0)
            {
                return;
            }

            if (_wetPropertyBlock == null)
            {
                _wetPropertyBlock = new MaterialPropertyBlock();
            }

            for (int i = 0; i < wetSurfaceRenderers.Length; i++)
            {
                Renderer r = wetSurfaceRenderers[i];
                if (r == null)
                {
                    continue;
                }

                if (!wet)
                {
                    r.SetPropertyBlock(null);
                    continue;
                }

                r.GetPropertyBlock(_wetPropertyBlock);
                Material sharedMat = r.sharedMaterial;
                Color baseCol = Color.white;
                if (sharedMat != null)
                {
                    if (sharedMat.HasProperty(BaseColorPropId))
                    {
                        baseCol = sharedMat.GetColor(BaseColorPropId);
                    }
                    else if (sharedMat.HasProperty(ColorPropId))
                    {
                        baseCol = sharedMat.GetColor(ColorPropId);
                    }
                }

                Color wetCol = new Color(
                    baseCol.r * wetAlbedoDarkenMultiplier,
                    baseCol.g * wetAlbedoDarkenMultiplier,
                    baseCol.b * (wetAlbedoDarkenMultiplier + 0.04f),
                    baseCol.a);

                _wetPropertyBlock.SetColor(BaseColorPropId, wetCol);
                _wetPropertyBlock.SetColor(ColorPropId, wetCol);
                _wetPropertyBlock.SetFloat(SmoothnessPropId, wetSurfaceSmoothness);
                r.SetPropertyBlock(_wetPropertyBlock);
            }
        }

        private void ApplyRuntimeSkyboxProfile(WeatherProfile profile)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Material sourceSky = _originalSkyboxMaterial != null ? _originalSkyboxMaterial : RenderSettings.skybox;
            if (sourceSky == null)
            {
                return;
            }

            if (_runtimeSkyboxMaterial == null)
            {
                _runtimeSkyboxMaterial = new Material(sourceSky);
                _runtimeSkyboxMaterial.name = sourceSky.name + "_RuntimeWeatherInstance";
                RenderSettings.skybox = _runtimeSkyboxMaterial;
            }

            if (_runtimeSkyboxMaterial.HasProperty("_SkyTint"))
            {
                _runtimeSkyboxMaterial.SetColor("_SkyTint", profile.skyTintColor);
            }
            if (_runtimeSkyboxMaterial.HasProperty("_GroundColor"))
            {
                _runtimeSkyboxMaterial.SetColor("_GroundColor", profile.skyGroundColor);
            }
            if (_runtimeSkyboxMaterial.HasProperty("_Exposure"))
            {
                _runtimeSkyboxMaterial.SetFloat("_Exposure", profile.skyExposure);
            }
            if (_runtimeSkyboxMaterial.HasProperty("_AtmosphereThickness"))
            {
                _runtimeSkyboxMaterial.SetFloat("_AtmosphereThickness", profile.skyAtmosphereThickness);
            }
        }

        private void SetNightObjectsActive(bool active)
        {
            if (nightOnlyObjects == null)
            {
                return;
            }

            for (int i = 0; i < nightOnlyObjects.Length; i++)
            {
                if (nightOnlyObjects[i] != null)
                {
                    nightOnlyObjects[i].SetActive(active);
                }
            }
        }

        private void ResolveReferences()
        {
            if (mainDirectionalLight == null)
            {
                Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] != null && lights[i].type == LightType.Directional)
                    {
                        mainDirectionalLight = lights[i];
                        break;
                    }
                }
            }

            if (windController == null)
            {
                windController = GetComponent<StageWindController>();
                if (windController == null)
                {
                    windController = FindAnyObjectByType<StageWindController>();
                }
            }
        }

        private static Material CreateRuntimeParticleMaterial(Color tint)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;

            var mat = new Material(shader);
            mat.hideFlags = HideFlags.DontSave;
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent in URP
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);     // Alpha blend
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            return mat;
        }

        private static void DestroySafe(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
