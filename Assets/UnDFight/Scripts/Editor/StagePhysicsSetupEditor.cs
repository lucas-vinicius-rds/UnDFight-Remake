using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnDFight.Editor
{
    /// <summary>
    /// Idempotent Editor setup utility configuring 5-layer PhysX isolation (StagePhysics, StageDebris,
    /// FighterHurtbox, StageProjectile, FighterDebrisPusher), all 8 Fighter prefabs, reusable stage prop
    /// prefabs (breakable, throwable, dynamic, hanging), StageData weather flags, and Arena_Favela Physics v2.
    /// </summary>
    public static class StagePhysicsSetupEditor
    {
        public const string StagePropsPrefabFolder = "Assets/UnDFight/Prefabs/StageProps";
        public const string CratePrefabPath = StagePropsPrefabFolder + "/Prop_BreakableCrate.prefab";
        public const string CrateStackPrefabPath = StagePropsPrefabFolder + "/Prop_BreakableCrateStack.prefab";
        public const string TablePrefabPath = StagePropsPrefabFolder + "/Prop_BreakableTable.prefab";
        public const string ThrowableChairPrefabPath = StagePropsPrefabFolder + "/Prop_ThrowableChair.prefab";
        public const string ThrowableBottlePrefabPath = StagePropsPrefabFolder + "/Prop_ThrowableBottle.prefab";
        public const string DynamicCanPrefabPath = StagePropsPrefabFolder + "/Prop_DynamicCan.prefab";
        public const string HangingSignPrefabPath = StagePropsPrefabFolder + "/Prop_HangingSign.prefab";
        public const string ArenaFavelaScenePath = "Assets/UnDFight/Scenes/Arena_Favela.unity";
        public const string FavelaStageDataPath = "Assets/UnDFight/Data/StageData_FavelaCarioca.asset";
        public const string TrainingStageDataPath = "Assets/UnDFight/Data/StageData_SalaDeTreino.asset";

        [MenuItem("UnDFight/Setup/Configure Stage Physics v2 & Weather (Fase 15.1)", false, 120)]
        public static void ApplyPhase15_1Setup()
        {
            ApplyPhase12Setup();
        }

        public static void ApplyPhase12Setup()
        {
            EnsurePhysicsLayersAndMatrix();
            AssignFighterPrefabsLayerAndKinematicBody();
            CreateOrUpdateStagePropPrefabs();
            ConfigureStageDataWeatherFlags();
            SetupArenaFavelaPropsAndManager();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UnDFight] Fase 15.1 setup applied: 5 PhysX layers isolated (incl. FighterDebrisPusher), all 8 Fighter prefabs updated, persistent debris + DynamicCan/HangingSign prefabs generated, StageData weather flags set, Arena_Favela Wind & Weather configured.");
        }

        public static void EnsurePhysicsLayersAndMatrix()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets != null && assets.Length > 0)
            {
                var tagManager = new SerializedObject(assets[0]);
                SerializedProperty layersProp = tagManager.FindProperty("layers");
                if (layersProp != null && layersProp.isArray)
                {
                    EnsureLayerSlot(layersProp, StagePhysicsLayers.PreferredStagePhysicsLayer, StagePhysicsLayers.StagePhysicsLayerName);
                    EnsureLayerSlot(layersProp, StagePhysicsLayers.PreferredStageDebrisLayer, StagePhysicsLayers.StageDebrisLayerName);
                    EnsureLayerSlot(layersProp, StagePhysicsLayers.PreferredFighterHurtboxLayer, StagePhysicsLayers.FighterHurtboxLayerName);
                    EnsureLayerSlot(layersProp, StagePhysicsLayers.PreferredStageProjectileLayer, StagePhysicsLayers.StageProjectileLayerName);
                    EnsureLayerSlot(layersProp, StagePhysicsLayers.PreferredFighterDebrisPusherLayer, StagePhysicsLayers.FighterDebrisPusherLayerName);
                    tagManager.ApplyModifiedProperties();
                }
            }

            StagePhysicsLayers.ApplyCollisionIsolation(debrisCollidesWithDebris: true);
            AssetDatabase.SaveAssets();
        }

        public static void AssignFighterPrefabsLayerAndKinematicBody()
        {
            int fighterLayer = LayerMask.NameToLayer(StagePhysicsLayers.FighterHurtboxLayerName);
            int pusherLayer = LayerMask.NameToLayer(StagePhysicsLayers.FighterDebrisPusherLayerName);

            string[] fighterPaths = Directory.Exists("Assets/UnDFight/Prefabs")
                ? Directory.GetFiles("Assets/UnDFight/Prefabs", "Fighter_*.prefab", SearchOption.TopDirectoryOnly)
                : new string[0];

            for (int i = 0; i < fighterPaths.Length; i++)
            {
                string path = fighterPaths[i].Replace('\\', '/');
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                {
                    continue;
                }

                bool dirty = false;
                if (fighterLayer >= 0 && root.layer != fighterLayer)
                {
                    root.layer = fighterLayer;
                    dirty = true;
                }

                Rigidbody rb = root.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = root.AddComponent<Rigidbody>();
                    dirty = true;
                }
                if (!rb.isKinematic || rb.useGravity || rb.collisionDetectionMode != CollisionDetectionMode.ContinuousSpeculative || rb.interpolation != RigidbodyInterpolation.None)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    rb.interpolation = RigidbodyInterpolation.None;
                    dirty = true;
                }

                CapsuleCollider hurtbox = root.GetComponent<CapsuleCollider>();
                if (hurtbox != null && !hurtbox.isTrigger)
                {
                    hurtbox.isTrigger = true;
                    dirty = true;
                }

                FighterController fc = root.GetComponent<FighterController>();
                float pushRadius = fc != null && fc.Data != null ? fc.Data.pushboxRadius : 0.36f;
                float height = fc != null && fc.Data != null ? fc.Data.heightMeters : FighterScaling.ReferenceHeightMeters;

                Transform pusherTr = root.transform.Find(FighterDebrisPusher.DefaultChildName);
                GameObject pusherGo;
                if (pusherTr == null)
                {
                    pusherGo = new GameObject(FighterDebrisPusher.DefaultChildName);
                    pusherGo.transform.SetParent(root.transform, false);
                    pusherGo.transform.localPosition = Vector3.zero;
                    pusherGo.transform.localRotation = Quaternion.identity;
                    pusherGo.transform.localScale = Vector3.one;
                    dirty = true;
                }
                else
                {
                    pusherGo = pusherTr.gameObject;
                }

                if (pusherLayer >= 0 && pusherGo.layer != pusherLayer)
                {
                    pusherGo.layer = pusherLayer;
                    dirty = true;
                }

                CapsuleCollider pusherCol = pusherGo.GetComponent<CapsuleCollider>();
                if (pusherCol == null)
                {
                    pusherCol = pusherGo.AddComponent<CapsuleCollider>();
                    dirty = true;
                }
                if (pusherCol.isTrigger || Mathf.Abs(pusherCol.height - height) > 0.01f)
                {
                    pusherCol.isTrigger = false;
                    pusherCol.direction = 1;
                    pusherCol.radius = pushRadius * 1.05f;
                    pusherCol.height = height;
                    pusherCol.center = new Vector3(0f, height * 0.5f, 0f);
                    dirty = true;
                }

                Rigidbody pusherRb = pusherGo.GetComponent<Rigidbody>();
                if (pusherRb == null)
                {
                    pusherRb = pusherGo.AddComponent<Rigidbody>();
                    dirty = true;
                }
                if (!pusherRb.isKinematic || pusherRb.useGravity || pusherRb.collisionDetectionMode != CollisionDetectionMode.ContinuousSpeculative || pusherRb.interpolation != RigidbodyInterpolation.None)
                {
                    pusherRb.isKinematic = true;
                    pusherRb.useGravity = false;
                    pusherRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    pusherRb.interpolation = RigidbodyInterpolation.None;
                    dirty = true;
                }

                FighterDebrisPusher pusherComp = pusherGo.GetComponent<FighterDebrisPusher>();
                if (pusherComp == null)
                {
                    pusherComp = pusherGo.AddComponent<FighterDebrisPusher>();
                    dirty = true;
                }

                var pusherSo = new SerializedObject(pusherComp);
                if (pusherSo.FindProperty("fighter").objectReferenceValue != fc ||
                    pusherSo.FindProperty("body").objectReferenceValue != pusherRb ||
                    pusherSo.FindProperty("pusherCollider").objectReferenceValue != pusherCol)
                {
                    pusherSo.FindProperty("fighter").objectReferenceValue = fc;
                    pusherSo.FindProperty("body").objectReferenceValue = pusherRb;
                    pusherSo.FindProperty("pusherCollider").objectReferenceValue = pusherCol;
                    pusherSo.ApplyModifiedPropertiesWithoutUndo();
                    dirty = true;
                }

                if (dirty)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void ConfigureStageDataWeatherFlags()
        {
            StageData favela = AssetDatabase.LoadAssetAtPath<StageData>(FavelaStageDataPath);
            if (favela != null)
            {
                favela.supportsWeatherVariants = true;
                favela.dayVariantLabel = "PÔR DO SOL";
                favela.defaultWeather = StageWeatherVariant.Day;
                favela.subtitle = "Bar do Jorge • Rio de Janeiro";
                EditorUtility.SetDirty(favela);
            }

            StageData training = AssetDatabase.LoadAssetAtPath<StageData>(TrainingStageDataPath);
            if (training != null)
            {
                training.supportsWeatherVariants = false;
                training.dayVariantLabel = "DIA";
                training.defaultWeather = StageWeatherVariant.Day;
                EditorUtility.SetDirty(training);
            }
        }

        public static void CreateOrUpdateStagePropPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(StagePropsPrefabFolder))
            {
                Directory.CreateDirectory(StagePropsPrefabFolder);
                AssetDatabase.Refresh();
            }

            Material redPlastic = LoadMaterialOrFallback("Assets/UnDFight/Art/Materials/StageFavela/Favela_PlasticRed.mat");
            Material yellowPlastic = LoadMaterialOrFallback("Assets/UnDFight/Art/Materials/StageFavela/Favela_PlasticYellow.mat");
            Material amberBottle = LoadMaterialOrFallback("Assets/UnDFight/Art/Materials/StageFavela/Favela_BottleAmber.mat");
            Material greenBottle = LoadMaterialOrFallback("Assets/UnDFight/Art/Materials/StageFavela/Favela_BottleGreen.mat");
            Material woodSign = LoadMaterialOrFallback("Assets/UnDFight/Art/Materials/StageFavela/Favela_WoodSign.mat");
            Material metalPole = LoadMaterialOrFallback("Assets/UnDFight/Art/Materials/StageFavela/Favela_RoofMetal.mat");

            BuildCratePrefab(CratePrefabPath, redPlastic, yellowPlastic);
            BuildCrateStackPrefab(CrateStackPrefabPath, redPlastic, yellowPlastic);
            BuildTablePrefab(TablePrefabPath, redPlastic, amberBottle, greenBottle);
            BuildThrowableChairPrefab(ThrowableChairPrefabPath, redPlastic, yellowPlastic);
            BuildThrowableBottlePrefab(ThrowableBottlePrefabPath, amberBottle, greenBottle, yellowPlastic);
            BuildDynamicCanPrefab(DynamicCanPrefabPath, redPlastic, yellowPlastic);
            BuildHangingSignPrefab(HangingSignPrefabPath, woodSign, metalPole);
            ArenaFavelaUpgradeEditor.CreateOrUpdatePhase15_2PropPrefabs();
        }

        public static void SetupArenaFavelaPropsAndManager()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            Scene scene = EditorSceneManager.OpenScene(ArenaFavelaScenePath, OpenSceneMode.Single);

            FightSimulation sim = Object.FindAnyObjectByType<FightSimulation>();
            MatchManager match = Object.FindAnyObjectByType<MatchManager>();

            GameObject managerGo = GameObject.Find("StageInteractableManager");
            if (managerGo == null)
            {
                managerGo = new GameObject("StageInteractableManager");
            }
            StageInteractableManager manager = managerGo.GetComponent<StageInteractableManager>();
            if (manager == null)
            {
                manager = managerGo.AddComponent<StageInteractableManager>();
            }

            // Wire serialized fields cleanly
            var managerSo = new SerializedObject(manager);
            managerSo.FindProperty("simulation").objectReferenceValue = sim;
            managerSo.FindProperty("matchManager").objectReferenceValue = match;
            managerSo.FindProperty("ensureStagePhysicsBounds").boolValue = true;
            managerSo.ApplyModifiedPropertiesWithoutUndo();

            if (match != null)
            {
                var matchSo = new SerializedObject(match);
                SerializedProperty propRef = matchSo.FindProperty("stageInteractableManager");
                if (propRef != null)
                {
                    propRef.objectReferenceValue = manager;
                    matchSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            manager.EnsureStagePhysicsBounds();

            GameObject propsGroup = GameObject.Find("InteractiveStageProps");
            if (propsGroup != null)
            {
                Object.DestroyImmediate(propsGroup);
            }
            propsGroup = new GameObject("InteractiveStageProps");

            GameObject cratePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CratePrefabPath);
            GameObject crateStackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrateStackPrefabPath);
            GameObject tablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TablePrefabPath);
            GameObject chairPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThrowableChairPrefabPath);
            GameObject bottlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThrowableBottlePrefabPath);
            GameObject canPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DynamicCanPrefabPath);
            GameObject signPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HangingSignPrefabPath);

            InstantiateProp(crateStackPrefab, "Breakable_CrateStack_Left", propsGroup.transform, new Vector3(-6.35f, 0f, 1.32f), 8f);
            InstantiateProp(cratePrefab, "Breakable_Crate_MidLeft", propsGroup.transform, new Vector3(-4.85f, 0f, 1.42f), -12f);
            InstantiateProp(chairPrefab, "Throwable_Chair_Left", propsGroup.transform, new Vector3(-3.35f, 0f, 1.22f), -28f);
            InstantiateProp(bottlePrefab, "Throwable_Bottle_Right", propsGroup.transform, new Vector3(3.45f, 0f, 1.22f), 14f);
            InstantiateProp(tablePrefab, "Breakable_Table_Right", propsGroup.transform, new Vector3(5.45f, 0f, 1.38f), 6f);
            InstantiateProp(crateStackPrefab, "Breakable_CrateStack_Right", propsGroup.transform, new Vector3(6.85f, 0f, 1.28f), -10f);

            // Loose PhysX dynamic cans near the combat plane (kicked by FighterDebrisPusher, pushed by wind & shockwaves)
            InstantiateProp(canPrefab, "Dynamic_Can_Left", propsGroup.transform, new Vector3(-2.15f, 0.09f, 0.25f), 22f);
            InstantiateProp(canPrefab, "Dynamic_Can_Center", propsGroup.transform, new Vector3(0.40f, 0.09f, -0.32f), -15f);
            InstantiateProp(canPrefab, "Dynamic_Can_Right", propsGroup.transform, new Vector3(2.45f, 0.09f, 0.28f), 40f);

            // Suspended HingeJoint sign near the bar/streetlamp reacting to wind gusts & combat shockwaves
            InstantiateProp(signPrefab, "Hanging_BarSign", propsGroup.transform, new Vector3(-2.95f, 3.35f, 2.15f), 0f);

            ConfigureArenaFavelaWindAndWeather(managerGo);
            ArenaFavelaUpgradeEditor.ApplyPhase15_2DetailsToOpenArenaFavela(scene);
            manager.RefreshDiscoveredProps();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != ArenaFavelaScenePath && File.Exists(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
            else
            {
                EditorSceneManager.OpenScene(PlayFromMainMenuEditor.MainMenuScenePath, OpenSceneMode.Single);
            }
        }

        private static void ConfigureArenaFavelaWindAndWeather(GameObject managerGo)
        {
            StageWindController wind = managerGo.GetComponent<StageWindController>();
            if (wind == null)
            {
                wind = managerGo.AddComponent<StageWindController>();
            }
            wind.Configure(new Vector3(1f, 0f, 0.14f), baseSpeed: 0.55f, gustAmp: 0.45f, gustFreq: 0.22f);

            StageWeatherController weather = managerGo.GetComponent<StageWeatherController>();
            if (weather == null)
            {
                weather = managerGo.AddComponent<StageWeatherController>();
            }

            // Build/rebuild dedicated night-only lights (inactive by default for Day/Sunset)
            GameObject existingNight = GameObject.Find("FavelaNightLights");
            if (existingNight != null)
            {
                Object.DestroyImmediate(existingNight);
            }
            var nightRoot = new GameObject("FavelaNightLights");
            CreatePointLight("NightLight_StreetLamp", nightRoot.transform, new Vector3(-1.18f, 4.95f, 1.85f), new Color(1.0f, 0.78f, 0.42f, 1f), intensity: 14f, range: 14f);
            CreatePointLight("NightLight_BarFacade", nightRoot.transform, new Vector3(-6.20f, 2.75f, 2.25f), new Color(1.0f, 0.62f, 0.24f, 1f), intensity: 10f, range: 11f);
            CreatePointLight("NightLight_RightWindow", nightRoot.transform, new Vector3(4.30f, 2.85f, 2.35f), new Color(1.0f, 0.80f, 0.48f, 1f), intensity: 8f, range: 10f);
            nightRoot.SetActive(false);

            Light keyLight = null;
            var secondaryLights = new System.Collections.Generic.List<Light>();
            var secondaryBaseIntensities = new System.Collections.Generic.List<float>();
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l == null || l.type != LightType.Directional) continue;
                if (l.name.Contains("SunKeyLight") || keyLight == null)
                {
                    if (keyLight != null && l.name.Contains("SunKeyLight"))
                    {
                        secondaryLights.Add(keyLight);
                        secondaryBaseIntensities.Add(keyLight.intensity);
                    }
                    keyLight = l;
                }
                else
                {
                    secondaryLights.Add(l);
                    secondaryBaseIntensities.Add(l.intensity);
                }
            }

            var wetRenderers = new System.Collections.Generic.List<Renderer>();
            string[] wetObjectNames = { "CobblestoneStreet", "SandShoulder_Front", "SandShoulder_Back", "Sidewalk_Back", "Bar_Floor" };
            foreach (string objName in wetObjectNames)
            {
                GameObject go = GameObject.Find(objName);
                if (go != null)
                {
                    Renderer r = go.GetComponent<Renderer>();
                    if (r != null) wetRenderers.Add(r);
                }
            }

            StageData favelaData = AssetDatabase.LoadAssetAtPath<StageData>(FavelaStageDataPath);

            var dayProf = new StageWeatherController.WeatherProfile
            {
                directionalLightColor = new Color(1.00f, 0.62f, 0.26f, 1f),
                directionalLightIntensity = 2.45f,
                directionalLightEuler = new Vector3(24f, 228f, 0f),
                ambientSkyColor = new Color(0.98f, 0.68f, 0.42f, 1f),
                ambientEquatorColor = new Color(0.88f, 0.58f, 0.38f, 1f),
                ambientGroundColor = new Color(0.48f, 0.32f, 0.22f, 1f),
                fogEnabled = true,
                fogColor = new Color(0.88f, 0.56f, 0.30f, 1f),
                fogDensity = 0.0095f,
                cameraBackgroundColor = new Color(0.80f, 0.48f, 0.26f, 1f),
                skyTintColor = new Color(0.92f, 0.58f, 0.36f, 1f),
                skyGroundColor = new Color(0.38f, 0.26f, 0.18f, 1f),
                skyExposure = 1.15f,
                skyAtmosphereThickness = 1.45f,
                windBaseSpeed = 0.55f,
                windGustAmplitude = 0.45f,
                windGustFrequency = 0.22f,
                secondaryLightsMultiplier = 1.0f
            };

            var nightProf = StageWeatherController.WeatherProfile.CreateDefaultNight();
            var rainyProf = StageWeatherController.WeatherProfile.CreateDefaultRainyNight();

            weather.SetProfiles(dayProf, nightProf, rainyProf);

            var weatherSo = new SerializedObject(weather);
            weatherSo.FindProperty("stageData").objectReferenceValue = favelaData;
            weatherSo.FindProperty("mainDirectionalLight").objectReferenceValue = keyLight;
            weatherSo.FindProperty("windController").objectReferenceValue = wind;

            SerializedProperty secLightsProp = weatherSo.FindProperty("secondaryFillLights");
            secLightsProp.arraySize = secondaryLights.Count;
            SerializedProperty secIntProp = weatherSo.FindProperty("secondaryFillBaseIntensities");
            secIntProp.arraySize = secondaryBaseIntensities.Count;
            for (int i = 0; i < secondaryLights.Count; i++)
            {
                secLightsProp.GetArrayElementAtIndex(i).objectReferenceValue = secondaryLights[i];
                secIntProp.GetArrayElementAtIndex(i).floatValue = secondaryBaseIntensities[i];
            }

            SerializedProperty nightObjsProp = weatherSo.FindProperty("nightOnlyObjects");
            nightObjsProp.arraySize = 1;
            nightObjsProp.GetArrayElementAtIndex(0).objectReferenceValue = nightRoot;

            SerializedProperty wetProp = weatherSo.FindProperty("wetSurfaceRenderers");
            wetProp.arraySize = wetRenderers.Count;
            for (int i = 0; i < wetRenderers.Count; i++)
            {
                wetProp.GetArrayElementAtIndex(i).objectReferenceValue = wetRenderers[i];
            }

            weatherSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreatePointLight(string name, Transform parent, Vector3 worldPos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        private static void InstantiateProp(GameObject prefab, string instanceName, Transform parent, Vector3 pos, float rotY)
        {
            if (prefab == null)
            {
                return;
            }

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.name = instanceName;
            inst.transform.localPosition = pos;
            inst.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            inst.transform.localScale = Vector3.one;
        }

        private static void BuildCratePrefab(string prefabPath, Material redMat, Material yellowMat)
        {
            var root = new GameObject("Prop_BreakableCrate");
            try
            {
                var intact = new GameObject("IntactVisual");
                intact.transform.SetParent(root.transform, false);

                CreatePrimitivePart("CrateBody", PrimitiveType.Cube, intact.transform, new Vector3(0f, 0.16f, 0f), Vector3.zero, new Vector3(0.46f, 0.32f, 0.36f), redMat);
                CreatePrimitivePart("CrateRimTop", PrimitiveType.Cube, intact.transform, new Vector3(0f, 0.31f, 0f), Vector3.zero, new Vector3(0.49f, 0.045f, 0.39f), yellowMat);

                var col = root.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.17f, 0f);
                col.size = new Vector3(0.50f, 0.35f, 0.40f);

                var pool = new GameObject("DebrisPool");
                pool.transform.SetParent(root.transform, false);
                for (int i = 0; i < 6; i++)
                {
                    Material mat = (i % 3 == 0) ? yellowMat : redMat;
                    Vector3 pos = new Vector3(((i % 3) - 1) * 0.14f, 0.10f + (i / 3) * 0.14f, (i % 2 == 0 ? -0.09f : 0.09f));
                    Vector3 rot = new Vector3(i * 17f, i * 41f, i * 13f);
                    Vector3 scl = new Vector3(0.22f, 0.06f, 0.14f);
                    CreateDebrisShard($"Shard_{i}", PrimitiveType.Cube, pool.transform, pos, rot, scl, mat);
                }

                ConfigureBreakableComponent(root, intact, col, pool.transform, StagePropMaterialType.Plastic, 2, 1.45f, 1.85f);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildCrateStackPrefab(string prefabPath, Material redMat, Material yellowMat)
        {
            var root = new GameObject("Prop_BreakableCrateStack");
            try
            {
                var intact = new GameObject("IntactVisual");
                intact.transform.SetParent(root.transform, false);

                CreatePrimitivePart("Crate_0", PrimitiveType.Cube, intact.transform, new Vector3(0f, 0.16f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0.46f, 0.32f, 0.36f), redMat);
                CreatePrimitivePart("Crate_1", PrimitiveType.Cube, intact.transform, new Vector3(0f, 0.49f, 0f), new Vector3(0f, 4f, 0f), new Vector3(0.46f, 0.32f, 0.36f), redMat);
                CreatePrimitivePart("Crate_2", PrimitiveType.Cube, intact.transform, new Vector3(0f, 0.82f, 0f), new Vector3(0f, 9f, 0f), new Vector3(0.46f, 0.32f, 0.36f), yellowMat);

                var col = root.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.50f, 0f);
                col.size = new Vector3(0.52f, 1.00f, 0.42f);

                var pool = new GameObject("DebrisPool");
                pool.transform.SetParent(root.transform, false);
                for (int i = 0; i < 8; i++)
                {
                    Material mat = (i >= 5) ? yellowMat : redMat;
                    Vector3 pos = new Vector3(((i % 2) == 0 ? -0.12f : 0.12f), 0.16f + (i / 2) * 0.22f, ((i % 3) - 1) * 0.08f);
                    Vector3 rot = new Vector3(i * 21f, i * 33f, i * 15f);
                    Vector3 scl = new Vector3(0.24f, 0.08f, 0.16f);
                    CreateDebrisShard($"Shard_{i}", PrimitiveType.Cube, pool.transform, pos, rot, scl, mat);
                }

                ConfigureBreakableComponent(root, intact, col, pool.transform, StagePropMaterialType.Plastic, 2, 1.65f, 1.85f);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildTablePrefab(string prefabPath, Material redMat, Material amberBottleMat, Material greenBottleMat)
        {
            var root = new GameObject("Prop_BreakableTable");
            try
            {
                var intact = new GameObject("IntactVisual");
                intact.transform.SetParent(root.transform, false);

                CreatePrimitivePart("TableTop", PrimitiveType.Cube, intact.transform, new Vector3(0f, 0.72f, 0f), Vector3.zero, new Vector3(0.84f, 0.05f, 0.84f), redMat);
                CreatePrimitivePart("Leg_FL", PrimitiveType.Cube, intact.transform, new Vector3(-0.34f, 0.36f, -0.34f), Vector3.zero, new Vector3(0.05f, 0.72f, 0.05f), redMat);
                CreatePrimitivePart("Leg_BL", PrimitiveType.Cube, intact.transform, new Vector3(-0.34f, 0.36f, 0.34f), Vector3.zero, new Vector3(0.05f, 0.72f, 0.05f), redMat);
                CreatePrimitivePart("Leg_FR", PrimitiveType.Cube, intact.transform, new Vector3(0.34f, 0.36f, -0.34f), Vector3.zero, new Vector3(0.05f, 0.72f, 0.05f), redMat);
                CreatePrimitivePart("Leg_BR", PrimitiveType.Cube, intact.transform, new Vector3(0.34f, 0.36f, 0.34f), Vector3.zero, new Vector3(0.05f, 0.72f, 0.05f), redMat);
                CreatePrimitivePart("TableBottle_1", PrimitiveType.Cylinder, intact.transform, new Vector3(0.06f, 0.86f, -0.05f), Vector3.zero, new Vector3(0.06f, 0.13f, 0.06f), amberBottleMat);
                CreatePrimitivePart("TableBottle_2", PrimitiveType.Cylinder, intact.transform, new Vector3(-0.14f, 0.86f, 0.10f), Vector3.zero, new Vector3(0.06f, 0.13f, 0.06f), greenBottleMat);

                var col = root.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.48f, 0f);
                col.size = new Vector3(0.88f, 0.96f, 0.88f);

                var pool = new GameObject("DebrisPool");
                pool.transform.SetParent(root.transform, false);
                CreateDebrisShard("TopShard_0", PrimitiveType.Cube, pool.transform, new Vector3(-0.20f, 0.72f, -0.18f), new Vector3(4f, 12f, 8f), new Vector3(0.40f, 0.05f, 0.40f), redMat);
                CreateDebrisShard("TopShard_1", PrimitiveType.Cube, pool.transform, new Vector3(0.20f, 0.72f, -0.18f), new Vector3(-5f, -15f, 6f), new Vector3(0.40f, 0.05f, 0.40f), redMat);
                CreateDebrisShard("TopShard_2", PrimitiveType.Cube, pool.transform, new Vector3(-0.18f, 0.72f, 0.20f), new Vector3(6f, 22f, -7f), new Vector3(0.40f, 0.05f, 0.40f), redMat);
                CreateDebrisShard("TopShard_3", PrimitiveType.Cube, pool.transform, new Vector3(0.20f, 0.72f, 0.20f), new Vector3(-4f, -8f, -5f), new Vector3(0.40f, 0.05f, 0.40f), redMat);
                CreateDebrisShard("LegShard_0", PrimitiveType.Cube, pool.transform, new Vector3(-0.30f, 0.34f, -0.26f), new Vector3(12f, 0f, 15f), new Vector3(0.06f, 0.48f, 0.06f), redMat);
                CreateDebrisShard("LegShard_1", PrimitiveType.Cube, pool.transform, new Vector3(0.30f, 0.34f, 0.26f), new Vector3(-14f, 10f, -12f), new Vector3(0.06f, 0.48f, 0.06f), redMat);
                CreateDebrisShard("BottleShard_1", PrimitiveType.Cube, pool.transform, new Vector3(0.06f, 0.85f, -0.05f), new Vector3(18f, 35f, 24f), new Vector3(0.07f, 0.14f, 0.07f), amberBottleMat);
                CreateDebrisShard("BottleShard_2", PrimitiveType.Cube, pool.transform, new Vector3(-0.14f, 0.85f, 0.10f), new Vector3(-20f, 45f, -18f), new Vector3(0.07f, 0.14f, 0.07f), greenBottleMat);

                ConfigureBreakableComponent(root, intact, col, pool.transform, StagePropMaterialType.Plastic, 2, 1.65f, 1.85f);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureBreakableComponent(
            GameObject root,
            GameObject intactVisual,
            Collider intactCol,
            Transform poolTransform,
            StagePropMaterialType matType,
            int integrity,
            float radiusX,
            float zTolerance)
        {
            var prop = root.AddComponent<StageBreakableProp>();
            var so = new SerializedObject(prop);
            so.FindProperty("maxIntegrity").intValue = integrity;
            so.FindProperty("triggerRadiusX").floatValue = radiusX;
            so.FindProperty("maxZDistanceFromFightPlane").floatValue = zTolerance;
            so.FindProperty("fightPlaneZ").floatValue = 0f;
            so.FindProperty("materialType").enumValueIndex = (int)matType;
            so.FindProperty("intactVisualRoot").objectReferenceValue = intactVisual;
            so.FindProperty("intactCollider").objectReferenceValue = intactCol;
            so.FindProperty("debrisPoolRoot").objectReferenceValue = poolTransform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreatePrimitivePart(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 localPos,
            Vector3 localRot,
            Vector3 localScale,
            Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localRot);
            go.transform.localScale = localScale;

            Collider primCol = go.GetComponent<Collider>();
            if (primCol != null)
            {
                Object.DestroyImmediate(primCol);
            }

            if (mat != null)
            {
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        private static void CreateDebrisShard(
            string name,
            PrimitiveType type,
            Transform poolParent,
            Vector3 localPos,
            Vector3 localRot,
            Vector3 localScale,
            Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = StagePhysicsLayers.StageDebrisLayer;
            go.transform.SetParent(poolParent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localRot);
            go.transform.localScale = localScale;

            if (mat != null)
            {
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.sleepThreshold = 0.08f;
            rb.linearDamping = 0.45f;
            rb.angularDamping = 0.65f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var wind = go.AddComponent<StageWindAffectable>();
            wind.Configure(cd: 1.05f, areaM2: 0.09f, sensitivity: 0.90f, wakeThreshold: 4.8f, bodyOverride: rb);

            var frag = go.AddComponent<StageDebrisFragment>();
            frag.PersistUntilReset = true;
            frag.EnsureComponents();
            go.SetActive(false);
        }

        private static void BuildDynamicCanPrefab(string prefabPath, Material redMat, Material yellowMat)
        {
            var root = new GameObject("Prop_DynamicCan");
            root.layer = StagePhysicsLayers.StageDebrisLayer;
            try
            {
                CreatePrimitivePart("CanBody", PrimitiveType.Cylinder, root.transform, Vector3.zero, Vector3.zero, new Vector3(0.11f, 0.09f, 0.11f), redMat);
                CreatePrimitivePart("CanStripe", PrimitiveType.Cylinder, root.transform, Vector3.zero, Vector3.zero, new Vector3(0.114f, 0.032f, 0.114f), yellowMat);

                var col = root.AddComponent<CapsuleCollider>();
                col.isTrigger = false;
                col.radius = 0.06f;
                col.height = 0.18f;
                col.center = Vector3.zero;

                var rb = root.AddComponent<Rigidbody>();
                rb.mass = 0.25f;
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.sleepThreshold = 0.08f;
                rb.linearDamping = 0.35f;
                rb.angularDamping = 0.55f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                var wind = root.AddComponent<StageWindAffectable>();
                wind.Configure(cd: 1.05f, areaM2: 0.11f, sensitivity: 0.95f, wakeThreshold: 4.2f, bodyOverride: rb);

                var dyn = root.AddComponent<StageDynamicProp>();
                var so = new SerializedObject(dyn);
                so.FindProperty("body").objectReferenceValue = rb;
                so.FindProperty("propCollider").objectReferenceValue = col;
                so.FindProperty("windAffectable").objectReferenceValue = wind;
                so.FindProperty("materialType").enumValueIndex = (int)StagePropMaterialType.ConcreteOrMetal;
                so.FindProperty("mass").floatValue = 0.25f;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildHangingSignPrefab(string prefabPath, Material boardMat, Material bracketMat)
        {
            var root = new GameObject("Prop_HangingSign");
            try
            {
                // Top mounting bar visual on the kinematic anchor
                CreatePrimitivePart("TopMountBar", PrimitiveType.Cube, root.transform, Vector3.zero, Vector3.zero, new Vector3(1.05f, 0.05f, 0.08f), bracketMat);

                var anchorRb = root.AddComponent<Rigidbody>();
                anchorRb.isKinematic = true;
                anchorRb.useGravity = false;

                var bodyGo = new GameObject("HangingBody");
                bodyGo.layer = StagePhysicsLayers.StageDebrisLayer;
                bodyGo.transform.SetParent(root.transform, false);
                bodyGo.transform.localPosition = new Vector3(0f, -0.36f, 0f);
                bodyGo.transform.localRotation = Quaternion.identity;

                CreatePrimitivePart("ChainLeft", PrimitiveType.Cylinder, bodyGo.transform, new Vector3(-0.34f, 0.22f, 0f), Vector3.zero, new Vector3(0.025f, 0.14f, 0.025f), bracketMat);
                CreatePrimitivePart("ChainRight", PrimitiveType.Cylinder, bodyGo.transform, new Vector3(0.34f, 0.22f, 0f), Vector3.zero, new Vector3(0.025f, 0.14f, 0.025f), bracketMat);
                CreatePrimitivePart("SignBoard", PrimitiveType.Cube, bodyGo.transform, new Vector3(0f, -0.04f, 0f), Vector3.zero, new Vector3(0.92f, 0.42f, 0.06f), boardMat);
                CreatePrimitivePart("SignTrim", PrimitiveType.Cube, bodyGo.transform, new Vector3(0f, -0.04f, 0f), Vector3.zero, new Vector3(0.96f, 0.045f, 0.075f), bracketMat);

                var box = bodyGo.AddComponent<BoxCollider>();
                box.size = new Vector3(0.94f, 0.44f, 0.08f);
                box.center = new Vector3(0f, -0.04f, 0f);
                box.isTrigger = false;

                var swingRb = bodyGo.AddComponent<Rigidbody>();
                swingRb.mass = 1.4f;
                swingRb.isKinematic = false;
                swingRb.useGravity = true;
                swingRb.linearDamping = 0.25f;
                swingRb.angularDamping = 0.85f;
                swingRb.sleepThreshold = 0.02f;
                swingRb.interpolation = RigidbodyInterpolation.Interpolate;

                var hinge = bodyGo.AddComponent<HingeJoint>();
                hinge.connectedBody = anchorRb;
                hinge.axis = Vector3.forward;
                hinge.anchor = new Vector3(0f, 0.36f, 0f);
                hinge.autoConfigureConnectedAnchor = false;
                hinge.connectedAnchor = Vector3.zero;
                hinge.useSpring = true;
                hinge.spring = new JointSpring { spring = 6.5f, damper = 1.6f, targetPosition = 0f };
                hinge.useLimits = true;
                hinge.limits = new JointLimits { min = -55f, max = 55f, bounciness = 0.1f };

                var wind = bodyGo.AddComponent<StageWindAffectable>();
                wind.Configure(cd: 1.25f, areaM2: 0.36f, sensitivity: 1.20f, wakeThreshold: 0.75f, bodyOverride: swingRb);

                var hanging = root.AddComponent<StageHangingProp>();
                var so = new SerializedObject(hanging);
                so.FindProperty("anchorBody").objectReferenceValue = anchorRb;
                so.FindProperty("swingBody").objectReferenceValue = swingRb;
                so.FindProperty("hingeJoint").objectReferenceValue = hinge;
                so.FindProperty("windAffectable").objectReferenceValue = wind;
                so.FindProperty("hingeAxis").vector3Value = Vector3.forward;
                so.FindProperty("localPivotOnSwingBody").vector3Value = new Vector3(0f, 0.36f, 0f);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildThrowableChairPrefab(string prefabPath, Material redMat, Material yellowMat)
        {
            var root = new GameObject("Prop_ThrowableChair");
            root.layer = StagePhysicsLayers.StageProjectileLayer;
            try
            {
                var intact = new GameObject("IntactVisual");
                intact.transform.SetParent(root.transform, false);

                CreatePrimitivePart("Seat", PrimitiveType.Cube, intact.transform, new Vector3(0f, 0.44f, 0f), Vector3.zero, new Vector3(0.44f, 0.045f, 0.44f), redMat);
                CreatePrimitivePart("Backrest", PrimitiveType.Cube, intact.transform, new Vector3(0f, 0.68f, 0.19f), new Vector3(6f, 0f, 0f), new Vector3(0.44f, 0.44f, 0.04f), redMat);
                CreatePrimitivePart("BackStripe", PrimitiveType.Cube, intact.transform, new Vector3(0f, 0.72f, 0.165f), new Vector3(6f, 0f, 0f), new Vector3(0.32f, 0.10f, 0.02f), yellowMat);
                CreatePrimitivePart("Leg_FL", PrimitiveType.Cube, intact.transform, new Vector3(-0.18f, 0.22f, -0.18f), Vector3.zero, new Vector3(0.05f, 0.44f, 0.05f), redMat);
                CreatePrimitivePart("Leg_FR", PrimitiveType.Cube, intact.transform, new Vector3(0.18f, 0.22f, -0.18f), Vector3.zero, new Vector3(0.05f, 0.44f, 0.05f), redMat);
                CreatePrimitivePart("Leg_BL", PrimitiveType.Cube, intact.transform, new Vector3(-0.18f, 0.22f, 0.18f), Vector3.zero, new Vector3(0.05f, 0.44f, 0.05f), redMat);
                CreatePrimitivePart("Leg_BR", PrimitiveType.Cube, intact.transform, new Vector3(0.18f, 0.22f, 0.18f), Vector3.zero, new Vector3(0.05f, 0.44f, 0.05f), redMat);

                var col = root.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.45f, 0f);
                col.size = new Vector3(0.48f, 0.88f, 0.48f);

                var rb = root.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.mass = 1.0f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                var pool = new GameObject("DebrisPool");
                pool.transform.SetParent(root.transform, false);
                CreateDebrisShard("SeatShard_0", PrimitiveType.Cube, pool.transform, new Vector3(-0.10f, 0.44f, 0f), new Vector3(5f, 18f, -10f), new Vector3(0.22f, 0.045f, 0.36f), redMat);
                CreateDebrisShard("SeatShard_1", PrimitiveType.Cube, pool.transform, new Vector3(0.10f, 0.44f, 0f), new Vector3(-6f, -14f, 12f), new Vector3(0.22f, 0.045f, 0.36f), redMat);
                CreateDebrisShard("BackShard_0", PrimitiveType.Cube, pool.transform, new Vector3(0f, 0.68f, 0.18f), new Vector3(12f, 20f, 8f), new Vector3(0.36f, 0.24f, 0.04f), redMat);
                CreateDebrisShard("StripeShard", PrimitiveType.Cube, pool.transform, new Vector3(0f, 0.72f, 0.16f), new Vector3(-8f, 35f, 15f), new Vector3(0.24f, 0.08f, 0.03f), yellowMat);
                CreateDebrisShard("LegShard_0", PrimitiveType.Cube, pool.transform, new Vector3(-0.16f, 0.22f, -0.14f), new Vector3(16f, 12f, -20f), new Vector3(0.05f, 0.38f, 0.05f), redMat);
                CreateDebrisShard("LegShard_1", PrimitiveType.Cube, pool.transform, new Vector3(0.16f, 0.22f, 0.14f), new Vector3(-14f, -18f, 18f), new Vector3(0.05f, 0.38f, 0.05f), redMat);

                ConfigureThrowableComponent(
                    root,
                    rb,
                    col,
                    intact,
                    pool.transform,
                    promptLabel: "ARREMESSAR CADEIRA",
                    damage: 85,
                    knockdown: true,
                    shatterOnFighter: true,
                    shatterOnStage: false,
                    matType: StagePropMaterialType.Plastic,
                    radiusX: 1.65f,
                    zTolerance: 2.0f);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildThrowableBottlePrefab(string prefabPath, Material amberMat, Material greenMat, Material labelMat)
        {
            var root = new GameObject("Prop_ThrowableBottle");
            root.layer = StagePhysicsLayers.StageProjectileLayer;
            try
            {
                var intact = new GameObject("IntactVisual");
                intact.transform.SetParent(root.transform, false);

                CreatePrimitivePart("BottleBody", PrimitiveType.Cylinder, intact.transform, new Vector3(0f, 0.16f, 0f), Vector3.zero, new Vector3(0.16f, 0.16f, 0.16f), amberMat);
                CreatePrimitivePart("BottleNeck", PrimitiveType.Cylinder, intact.transform, new Vector3(0f, 0.36f, 0f), Vector3.zero, new Vector3(0.075f, 0.10f, 0.075f), greenMat);
                CreatePrimitivePart("BottleLabel", PrimitiveType.Cylinder, intact.transform, new Vector3(0f, 0.17f, 0f), Vector3.zero, new Vector3(0.166f, 0.075f, 0.166f), labelMat);
                CreatePrimitivePart("BottleCap", PrimitiveType.Cylinder, intact.transform, new Vector3(0f, 0.47f, 0f), Vector3.zero, new Vector3(0.085f, 0.02f, 0.085f), labelMat);

                var col = root.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.24f, 0f);
                col.size = new Vector3(0.24f, 0.50f, 0.24f);

                var rb = root.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.mass = 1.0f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                var pool = new GameObject("DebrisPool");
                pool.transform.SetParent(root.transform, false);
                for (int i = 0; i < 6; i++)
                {
                    Material mat = (i % 3 == 0) ? greenMat : amberMat;
                    Vector3 pos = new Vector3(((i % 3) - 1) * 0.06f, 0.12f + (i / 3) * 0.14f, (i % 2 == 0 ? -0.05f : 0.05f));
                    Vector3 rot = new Vector3(i * 22f, i * 48f, i * 19f);
                    Vector3 scl = new Vector3(0.08f, 0.12f, 0.04f);
                    CreateDebrisShard($"GlassShard_{i}", PrimitiveType.Cube, pool.transform, pos, rot, scl, mat);
                }

                ConfigureThrowableComponent(
                    root,
                    rb,
                    col,
                    intact,
                    pool.transform,
                    promptLabel: "ARREMESSAR GARRAFA",
                    damage: 75,
                    knockdown: false,
                    shatterOnFighter: true,
                    shatterOnStage: true,
                    matType: StagePropMaterialType.Glass,
                    radiusX: 1.65f,
                    zTolerance: 2.0f);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureThrowableComponent(
            GameObject root,
            Rigidbody body,
            Collider propCollider,
            GameObject intactVisual,
            Transform poolTransform,
            string promptLabel,
            int damage,
            bool knockdown,
            bool shatterOnFighter,
            bool shatterOnStage,
            StagePropMaterialType matType,
            float radiusX,
            float zTolerance)
        {
            var throwable = root.AddComponent<StageThrowableProp>();
            var so = new SerializedObject(throwable);
            so.FindProperty("interactionRadiusX").floatValue = radiusX;
            so.FindProperty("maxZDistanceFromFightPlane").floatValue = zTolerance;
            so.FindProperty("fightPlaneZ").floatValue = 0f;
            so.FindProperty("promptLabel").stringValue = promptLabel;
            so.FindProperty("damage").intValue = damage;
            so.FindProperty("causesKnockdown").boolValue = knockdown;
            so.FindProperty("breakOnFighterHit").boolValue = shatterOnFighter;
            so.FindProperty("breakOnStageHit").boolValue = shatterOnStage;
            so.FindProperty("materialType").enumValueIndex = (int)matType;
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("propCollider").objectReferenceValue = propCollider;
            so.FindProperty("intactVisualRoot").objectReferenceValue = intactVisual;
            so.FindProperty("debrisPoolRoot").objectReferenceValue = poolTransform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material LoadMaterialOrFallback(string path)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                return mat;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            return urpLit != null ? new Material(urpLit) : null;
        }

        private static void EnsureLayerSlot(SerializedProperty layersProp, int preferredSlot, string layerName)
        {
            for (int i = 0; i < layersProp.arraySize; i++)
            {
                if (layersProp.GetArrayElementAtIndex(i).stringValue == layerName)
                {
                    return;
                }
            }

            if (preferredSlot >= 8 && preferredSlot < layersProp.arraySize)
            {
                SerializedProperty slot = layersProp.GetArrayElementAtIndex(preferredSlot);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = layerName;
                    return;
                }
            }

            for (int i = 8; i < layersProp.arraySize; i++)
            {
                SerializedProperty slot = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = layerName;
                    return;
                }
            }
        }
    }
}
