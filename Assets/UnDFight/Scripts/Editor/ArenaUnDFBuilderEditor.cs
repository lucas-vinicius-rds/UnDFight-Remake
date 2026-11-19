using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UnDFight.Editor
{
    /// <summary>
    /// Idempotent Editor builder for Phase 15.3: creates/rebuilds Arena_UnDF.unity (Frente do Prédio da UnDF),
    /// copies Stage_UnDF_Preview.jpg (preserving the original concept art), generates persistent URP materials
    /// & procedural PNG textures, registers StageData_UnDF in StageRoster & EditorBuildSettings, builds full
    /// Physics v2 props (3 HingeJoint flags, hanging academic banner, breakables, MK throwables, dynamic props),
    /// enforces StageFramingStandards (z <= -22.5m, zero black bar), and configures Day / Night / RainyNight.
    /// </summary>
    public static class ArenaUnDFBuilderEditor
    {
        public const string ConceptSourcePath = "Docs/Referencias/UnDF Fight Arena Concept.jpg";
        public const string PreviewTexturePath = "Assets/UnDFight/Art/Textures/Stage_UnDF_Preview.jpg";
        public const string UnDFTexturesFolder = "Assets/UnDFight/Art/Textures/UnDF";
        public const string UnDFMaterialsFolder = "Assets/UnDFight/Art/Materials/UnDF";
        public const string StagePropsPrefabFolder = StagePhysicsSetupEditor.StagePropsPrefabFolder;

        public const string ArenaUnDFScenePath = "Assets/UnDFight/Scenes/Arena_UnDF.unity";
        public const string UnDFStageDataPath = "Assets/UnDFight/Data/StageData_UnDF.asset";
        public const string StageRosterPath = "Assets/UnDFight/Data/StageRoster.asset";

        public const string HangingFlagPrefabPath = StagePropsPrefabFolder + "/Prop_UnDF_HangingFlag.prefab";
        public const string HangingBannerPrefabPath = StagePropsPrefabFolder + "/Prop_UnDF_HangingBanner.prefab";
        public const string BreakableBarrierPrefabPath = StagePropsPrefabFolder + "/Prop_UnDF_BreakableBarrier.prefab";
        public const string BreakableEaselPrefabPath = StagePropsPrefabFolder + "/Prop_UnDF_BreakableEasel.prefab";
        public const string ThrowableConePrefabPath = StagePropsPrefabFolder + "/Prop_UnDF_ThrowableCone.prefab";
        public const string ThrowableTrashBinPrefabPath = StagePropsPrefabFolder + "/Prop_UnDF_ThrowableTrashBin.prefab";

        public const string FacadeLogoTexPath = UnDFTexturesFolder + "/Tex_UnDF_FacadeLogo.png";
        public const string FlagDFTexPath = UnDFTexturesFolder + "/Tex_Flag_DF.png";
        public const string FlagBrazilTexPath = UnDFTexturesFolder + "/Tex_Flag_Brazil.png";
        public const string FlagUnDFTexPath = UnDFTexturesFolder + "/Tex_Flag_UnDF.png";
        public const string SignNoParkingTexPath = UnDFTexturesFolder + "/Tex_Sign_NoParking.png";
        public const string AsphaltTexPath = UnDFTexturesFolder + "/Tex_Asphalt_Parking.png";

        public const string EnvironmentRootName = "UnDFEnvironment";
        public const string DaySunName = "UnDF_DaySun";
        public const string ClearNightSkyName = "UnDF_ClearNightSky";
        public const string NightLightsAndWindowsName = "UnDF_NightLightsAndWindows";
        public const string RainPuddlesName = "UnDF_RainPuddles";
        public const string WeatherCloudsPrefix = "UnDFWeatherClouds";

        private struct UnDFMaterials
        {
            public Material Asphalt;
            public Material ConcreteWall;
            public Material ConcreteSidewalk;
            public Material CurbYellow;
            public Material PaintYellow;
            public Material TireMark;
            public Material DarkAluminum;
            public Material SteelSilver;
            public Material GlassDark;
            public Material MarqueeGlass;
            public Material FacadeLogo;
            public Material FacadeTrim;
            public Material LobbyInterior;
            public Material LobbyRedStair;
            public Material AcUnitWhite;
            public Material AcVentDark;
            public Material FlyerPaper;
            public Material TrashBinGray;
            public Material TreeBark;
            public Material TreeLeafGreen;
            public Material TreeLeafGold;
            public Material GrassBed;
            public Material FlagDF;
            public Material FlagBrazil;
            public Material FlagUnDF;
            public Material SignNoParking;
            public Material BannerPurple;
            public Material ConeOrange;
            public Material ConeStripeWhite;
            public Material NeighborWallA;
            public Material NeighborWallB;
            public Material LampGlow;
            public Material NightWindowWarm;
            public Material NightWindowCool;
            public Material SunGlow;
            public Material MoonGlow;
            public Material StarGlow;
            public Material RainPuddle;
            public Material SkyboxDay;
            public StageFramingStandards.CloudMaterials Clouds;
        }

        [MenuItem("UnDFight/Stages/Build Arena_UnDF", false, 122)]
        public static void BuildArenaUnDF()
        {
            StagePhysicsSetupEditor.EnsurePhysicsLayersAndMatrix();
            StagePhysicsSetupEditor.CreateOrUpdateStagePropPrefabs();

            EnsurePreviewTexture();
            EnsureProceduralTextures();
            UnDFMaterials mats = EnsureMaterials();
            CreateOrUpdateUnDFPropPrefabs(mats);
            StageData undfData = EnsureStageDataAndRoster();

            string previousScenePath = SceneManager.GetActiveScene().path;

            // Open Arena_Favela as our verified combat/HUD/camera/bootstrap template and save as Arena_UnDF
            Scene scene = EditorSceneManager.OpenScene(StagePhysicsSetupEditor.ArenaFavelaScenePath, OpenSceneMode.Single);
            StripFavelaSpecificObjects(scene);
            BuildUnDFSceneContents(scene, undfData, mats);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ArenaUnDFScenePath);
            EnsureSceneInBuildSettings(ArenaUnDFScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!string.IsNullOrEmpty(previousScenePath) &&
                previousScenePath != ArenaUnDFScenePath &&
                File.Exists(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
            else
            {
                EditorSceneManager.OpenScene(PlayFromMainMenuEditor.MainMenuScenePath, OpenSceneMode.Single);
            }

            Debug.Log("[UnDFight] Fase 15.3 complete: Arena_UnDF.unity built with UnDF facade, 3 HingeJoint flags, hanging academic banner, breakables/throwables/dynamic props, StageFramingStandards coverage (z <= -22.5m), and 3 full weather presets (Day / Night / RainyNight).");
        }

        // ==================== 1. PREVIEW, STAGEDATA, ROSTER & BUILD SETTINGS ====================

        private static void EnsurePreviewTexture()
        {
            string fullSrc = Path.GetFullPath(ConceptSourcePath);
            string fullDst = Path.GetFullPath(PreviewTexturePath);
            string dstDir = Path.GetDirectoryName(fullDst);
            if (!string.IsNullOrEmpty(dstDir) && !Directory.Exists(dstDir))
            {
                Directory.CreateDirectory(dstDir);
            }

            if (File.Exists(fullSrc))
            {
                // Always COPY (never move or delete the original concept file)
                File.Copy(fullSrc, fullDst, overwrite: true);
                AssetDatabase.ImportAsset(PreviewTexturePath, ImportAssetOptions.ForceUpdate);
            }

            var importer = AssetImporter.GetAtPath(PreviewTexturePath) as TextureImporter;
            if (importer != null)
            {
                bool dirty = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    dirty = true;
                }
                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    dirty = true;
                }
                if (importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = false;
                    dirty = true;
                }
                if (dirty)
                {
                    importer.SaveAndReimport();
                }
            }
        }

        private static StageData EnsureStageDataAndRoster()
        {
            StageData undf = AssetDatabase.LoadAssetAtPath<StageData>(UnDFStageDataPath);
            if (undf == null)
            {
                undf = ScriptableObject.CreateInstance<StageData>();
                AssetDatabase.CreateAsset(undf, UnDFStageDataPath);
            }

            Sprite previewSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PreviewTexturePath);
            undf.stageId = "undf_campus";
            undf.displayName = "FRENTE DA UnDF";
            undf.subtitle = "Campus Universitário • Brasília - DF";
            undf.sceneName = "Arena_UnDF";
            undf.previewImage = previewSprite;
            undf.previewColor = new Color(0.46f, 0.28f, 0.58f, 1f);
            undf.accentColor = new Color(0.96f, 0.74f, 0.16f, 1f);
            undf.supportsWeatherVariants = true;
            undf.dayVariantLabel = "DIA";
            undf.defaultWeather = StageWeatherVariant.Day;
            EditorUtility.SetDirty(undf);

            // Register in StageRoster in order: Arena_Favela, Arena_UnDF, Training
            StageRoster roster = AssetDatabase.LoadAssetAtPath<StageRoster>(StageRosterPath);
            if (roster != null)
            {
                StageData favela = AssetDatabase.LoadAssetAtPath<StageData>(StagePhysicsSetupEditor.FavelaStageDataPath);
                StageData training = AssetDatabase.LoadAssetAtPath<StageData>(StagePhysicsSetupEditor.TrainingStageDataPath);

                var ordered = new List<StageData>();
                if (favela != null) ordered.Add(favela);
                ordered.Add(undf);
                if (training != null) ordered.Add(training);

                // Preserve any extra future stages already registered
                for (int i = 0; i < roster.Count; i++)
                {
                    StageData existing = roster.GetStage(i);
                    if (existing != null && !ordered.Contains(existing))
                    {
                        ordered.Add(existing);
                    }
                }

                var rosterSo = new SerializedObject(roster);
                SerializedProperty stagesProp = rosterSo.FindProperty("stages");
                stagesProp.arraySize = ordered.Count;
                for (int i = 0; i < ordered.Count; i++)
                {
                    stagesProp.GetArrayElementAtIndex(i).objectReferenceValue = ordered[i];
                }
                rosterSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(roster);
            }

            return undf;
        }

        private static void EnsureSceneInBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int existingIdx = scenes.FindIndex(s => s.path == scenePath);
            if (existingIdx >= 0)
            {
                scenes[existingIdx].enabled = true;
            }
            else
            {
                // Insert right after Arena_Favela if found, otherwise append
                int favelaIdx = scenes.FindIndex(s => s.path == StagePhysicsSetupEditor.ArenaFavelaScenePath);
                var newEntry = new EditorBuildSettingsScene(scenePath, true);
                if (favelaIdx >= 0)
                {
                    scenes.Insert(favelaIdx + 1, newEntry);
                }
                else
                {
                    scenes.Add(newEntry);
                }
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ==================== 2. SCENE BUILDER ====================

        private static void StripFavelaSpecificObjects(Scene scene)
        {
            // Preserve the scene's Global Volume (ACES + Bloom) by reparenting it to root before stripping FavelaEnvironment
            GameObject existingVol = FindInSceneByName(scene, "Global Volume");
            if (existingVol != null)
            {
                existingVol.transform.SetParent(null, worldPositionStays: true);
            }

            string[] favelaRoots =
            {
                "FavelaEnvironment",
                "FavelaDetails_Phase15_2",
                "FavelaNightLights",
                "FavelaNightLightsAndWindows",
                "FavelaRainPuddles",
                "InteractiveStageProps",
                EnvironmentRootName,
                DaySunName,
                ClearNightSkyName,
                NightLightsAndWindowsName,
                RainPuddlesName,
                $"{WeatherCloudsPrefix}_DayClouds",
                $"{WeatherCloudsPrefix}_ClearNightClouds",
                $"{WeatherCloudsPrefix}_RainClouds"
            };

            foreach (string name in favelaRoots)
            {
                DestroyAllMatchingInScene(scene, name);
            }
        }

        private static void BuildUnDFSceneContents(Scene scene, StageData undfData, UnDFMaterials mats)
        {
            // Environment root
            var envRoot = new GameObject(EnvironmentRootName);
            var wetRenderers = new List<Renderer>();
            var nightWindowSpecs = new List<(string Name, Vector3 WorldPos, Vector3 Size, bool Cool)>();

            // Lighting & Global Volume (same 3-light structure as Favela for StageWeatherController)
            Light sunKeyLight = CreateDirectionalLight("SunKeyLight_UnDFDay", envRoot.transform, new Vector3(32f, 215f, 0f), new Color(1.00f, 0.93f, 0.80f, 1f), intensity: 2.15f, shadows: LightShadows.Soft);
            Light skyFillLight = CreateDirectionalLight("SkyFillLight_UnDF", envRoot.transform, new Vector3(40f, 38f, 0f), new Color(0.64f, 0.80f, 1.00f, 1f), intensity: 0.72f, shadows: LightShadows.None);
            Light fighterFillLight = CreateDirectionalLight("FighterReadabilityFill_UnDF", envRoot.transform, new Vector3(24f, 345f, 0f), new Color(1.00f, 0.95f, 0.88f, 1f), intensity: 0.95f, shadows: LightShadows.None);

            // Reparent preserved Global Volume cleanly under envRoot
            GameObject existingVol = FindInSceneByName(scene, "Global Volume");
            if (existingVol != null)
            {
                existingVol.transform.SetParent(envRoot.transform, worldPositionStays: true);
            }

            // Default Day skybox & lighting in Editor
            RenderSettings.skybox = mats.SkyboxDay;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.64f, 0.78f, 0.96f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.76f, 0.74f, 0.72f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.30f, 0.30f, 1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.76f, 0.83f, 0.92f, 1f);
            RenderSettings.fogDensity = 0.0055f;

            // Layer A: Asphalt parking lot (extended to Z = -22.5m, width 68m) + road markings + yellow curb + sidewalk
            BuildLayerAFloorAndParking(envRoot.transform, mats, wetRenderers);

            // Layer C: Main UnDF Central Facade + Entrance + Left 3-Story Wing + Right 3-Story Wing & Big Tree
            BuildLayerCUnDFBuilding(envRoot.transform, mats, nightWindowSpecs);

            // Layer D: Neighboring Modernist Campus Buildings (closing beyond ±22m) + Parallax Backdrop
            GameObject backdropLayer = BuildLayerDNeighborCampusAndHorizon(envRoot.transform, mats, nightWindowSpecs);

            // Layer B: Interactive Physics v2 Props (3 HingeJoint flags, hanging academic banner, 2 breakables, 2 MK throwables, dynamic cans & cones)
            var propsGroup = new GameObject("InteractiveStageProps");
            InstantiateUnDFInteractiveProps(propsGroup.transform, mats);

            // Weather visual groups (Day Sun, Clear Night Moon+Stars, Night Lights+24+ Lit Windows, Rain Puddles, 3 Cloud Layers)
            GameObject daySun = BuildDaySun(backdropLayer.transform, mats);
            GameObject clearNightSky = BuildClearNightSky(backdropLayer.transform, mats);
            GameObject nightLightsAndWindows = BuildNightLightsAndWindows(mats, nightWindowSpecs);
            GameObject rainPuddles = BuildRainPuddles(mats);

            StageFramingStandards.WeatherCloudLayers clouds = StageFramingStandards.BuildWeatherCloudLayer(
                backdropLayer.transform,
                WeatherCloudsPrefix,
                regionCenter: new Vector3(0f, 15.8f, 44.0f),
                regionSize: new Vector3(62.0f, 4.4f, 6.0f),
                dayMat: mats.Clouds.DayCloud,
                clearNightMat: mats.Clouds.ClearNightCloud,
                stormMat: mats.Clouds.StormCloud,
                dayCount: 7,
                clearNightCount: 5,
                stormCount: 10);

            // Default Editor view = Day
            daySun.SetActive(true);
            if (clouds.DayClouds != null) clouds.DayClouds.SetActive(true);
            clearNightSky.SetActive(false);
            if (clouds.ClearNightClouds != null) clouds.ClearNightClouds.SetActive(false);
            nightLightsAndWindows.SetActive(false);
            rainPuddles.SetActive(false);
            if (clouds.RainClouds != null) clouds.RainClouds.SetActive(false);

            // Wire StageInteractableManager, StageWindController & StageWeatherController
            WireStageManagersAndWeather(
                scene,
                undfData,
                sunKeyLight,
                new[] { skyFillLight, fighterFillLight },
                new[] { skyFillLight.intensity, fighterFillLight.intensity },
                daySun,
                clearNightSky,
                nightLightsAndWindows,
                rainPuddles,
                clouds,
                wetRenderers);
        }

        // ==================== LAYER A: PARKING ASPHALT, MARKINGS, YELLOW CURB & SIDEWALK ====================

        private static void BuildLayerAFloorAndParking(Transform parent, UnDFMaterials mats, List<Renderer> wetRenderers)
        {
            var layerA = new GameObject("LayerA_ParkingAndSidewalk");
            layerA.transform.SetParent(parent, false);

            // Combat lane asphalt: Z in [-2.60 .. +2.85], top at exact Y = 0.00, width 68m
            GameObject mainAsphalt = CreateBox("ParkingAsphalt_Main", layerA.transform, new Vector3(0f, -0.05f, 0.125f), Vector3.zero, new Vector3(68.0f, 0.10f, 5.45f), mats.Asphalt);
            // Foreground asphalt extension: locked seam at backEdgeZ = -2.60 -> extends to Z = -22.50 (zero black bar, zero Z-fighting)
            GameObject frontAsphalt = CreateBox("ParkingAsphalt_Front", layerA.transform, new Vector3(0f, -0.05f, -4.55f), Vector3.zero, new Vector3(68.0f, 0.10f, 3.90f), mats.Asphalt);
            StageFramingStandards.EnsureForegroundFloorCoverage(frontAsphalt.transform, backEdgeZ: -2.60f, targetFrontZ: -22.5f, minWidthX: 68.0f);

            // Yellow-painted concrete curb: Z in [+2.85 .. +3.03], top Y = 0.12, width 68m
            GameObject curb = CreateBox("Curb_Yellow", layerA.transform, new Vector3(0f, 0.06f, 2.94f), Vector3.zero, new Vector3(68.0f, 0.12f, 0.18f), mats.CurbYellow);
            // Elevated campus concrete sidewalk: Z in [+3.03 .. +6.00], top Y = 0.12, width 68m
            GameObject sidewalk = CreateBox("Sidewalk_Campus", layerA.transform, new Vector3(0f, 0.06f, 4.515f), Vector3.zero, new Vector3(68.0f, 0.12f, 2.97f), mats.ConcreteSidewalk);

            wetRenderers.Add(mainAsphalt.GetComponent<Renderer>());
            wetRenderers.Add(frontAsphalt.GetComponent<Renderer>());
            wetRenderers.Add(curb.GetComponent<Renderer>());
            wetRenderers.Add(sidewalk.GetComponent<Renderer>());

            // Road markings (all strictly at Y = 0.026m >= MinSurfaceSeparation 0.025m above Y=0, placed outside Z=0 fighter feet lane)
            var markings = new GameObject("Parking_RoadMarkings");
            markings.transform.SetParent(layerA.transform, false);

            // Left parking stall yellow border & angled lines (X in [-12..-4.5], Z in [-3.4..-1.1] and [+1.2..+2.6])
            CreateNoShadowBox("ParkingBorder_LeftFront", markings.transform, new Vector3(-8.2f, 0.026f, -1.05f), Vector3.zero, new Vector3(7.6f, 0.004f, 0.12f), mats.PaintYellow);
            CreateNoShadowBox("ParkingBorder_LeftSide", markings.transform, new Vector3(-4.45f, 0.026f, -2.55f), Vector3.zero, new Vector3(0.12f, 0.004f, 3.10f), mats.PaintYellow);
            for (int i = 0; i < 4; i++)
            {
                float sx = -10.8f + i * 1.95f;
                CreateNoShadowBox($"ParkingStallLine_{i}", markings.transform, new Vector3(sx, 0.026f, -2.55f), new Vector3(0f, -16f, 0f), new Vector3(0.11f, 0.004f, 3.10f), mats.PaintYellow);
            }
            // Subtle yellow parking divider near back left
            CreateNoShadowBox("ParkingBackStripe_L", markings.transform, new Vector3(-7.6f, 0.026f, 2.25f), Vector3.zero, new Vector3(5.8f, 0.004f, 0.10f), mats.PaintYellow);

            // Painted yellow directional arrow in foreground lane (Z = -1.95, clearly visible & zero conflict with fighters at Z=0)
            var arrow = new GameObject("Parking_DirectionalArrow");
            arrow.transform.SetParent(markings.transform, false);
            arrow.transform.position = new Vector3(0.65f, 0.026f, -1.95f);
            arrow.transform.rotation = Quaternion.Euler(0f, -18f, 0f);
            CreateNoShadowBox("Arrow_Stem", arrow.transform, new Vector3(0f, 0f, -0.35f), Vector3.zero, new Vector3(0.24f, 0.004f, 1.35f), mats.PaintYellow);
            CreateNoShadowBox("Arrow_HeadL", arrow.transform, new Vector3(-0.24f, 0f, 0.25f), new Vector3(0f, 36f, 0f), new Vector3(0.22f, 0.004f, 0.85f), mats.PaintYellow);
            CreateNoShadowBox("Arrow_HeadR", arrow.transform, new Vector3(0.24f, 0f, 0.25f), new Vector3(0f, -36f, 0f), new Vector3(0.22f, 0.004f, 0.85f), mats.PaintYellow);
            CreateNoShadowBox("Arrow_BranchR", arrow.transform, new Vector3(0.45f, 0f, -0.25f), new Vector3(0f, 45f, 0f), new Vector3(0.18f, 0.004f, 0.80f), mats.PaintYellow);

            // Subtle curved tire tracks on foreground right & left
            for (int t = 0; t < 6; t++)
            {
                float a = t * 8f;
                CreateNoShadowBox($"TireTrack_A_{t}", markings.transform, new Vector3(1.55f + t * 0.48f, 0.025f, -2.55f + Mathf.Sin(t * 0.45f) * 0.35f), new Vector3(0f, -12f + a * 0.6f, 0f), new Vector3(0.55f, 0.003f, 0.14f), mats.TireMark);
                CreateNoShadowBox($"TireTrack_B_{t}", markings.transform, new Vector3(1.55f + t * 0.48f, 0.025f, -3.15f + Mathf.Sin(t * 0.45f) * 0.35f), new Vector3(0f, -12f + a * 0.6f, 0f), new Vector3(0.55f, 0.003f, 0.14f), mats.TireMark);
            }
        }

        // ==================== LAYER C: UnDF MAIN BUILDING, ENTRANCE, WINGS & BIG TREE ====================

        private static void BuildLayerCUnDFBuilding(
            Transform parent,
            UnDFMaterials mats,
            List<(string Name, Vector3 WorldPos, Vector3 Size, bool Cool)> nightWindowSpecs)
        {
            var layerC = new GameObject("LayerC_UnDFBuilding");
            layerC.transform.SetParent(parent, false);

            // --- 1. CENTRAL FACADE PANEL (X in [-5.2..+5.2], Y in [3.2..9.4], Z = 5.80) ---
            var central = new GameObject("UnDF_CentralFacade");
            central.transform.SetParent(layerC.transform, false);

            CreateBox("CentralBlock_Structure", central.transform, new Vector3(0f, 6.30f, 8.20f), Vector3.zero, new Vector3(10.40f, 6.20f, 4.80f), mats.ConcreteWall);

            // Framed giant white/cream UnDF logo panel (front face at Z = 5.76)
            CreateBox("FacadeLogo_BackPlate", central.transform, new Vector3(0f, 6.30f, 5.76f), Vector3.zero, new Vector3(10.20f, 6.08f, 0.12f), mats.FacadeTrim);
            // Textured Quad facing -Z (toward camera): default Unity Quad faces -Z!
            GameObject logoQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            logoQuad.name = "FacadeLogo_Surface";
            logoQuad.transform.SetParent(central.transform, false);
            logoQuad.transform.localPosition = new Vector3(0f, 6.30f, 5.665f); // 0.035m ahead of BackPlate front (5.70)
            logoQuad.transform.localRotation = Quaternion.identity;
            logoQuad.transform.localScale = new Vector3(9.96f, 5.84f, 1f);
            Object.DestroyImmediate(logoQuad.GetComponent<Collider>());
            logoQuad.GetComponent<MeshRenderer>().sharedMaterial = mats.FacadeLogo;

            // Crisp 3D TextMeshPro typography on top of facade panel (Z = 5.62 -> 0.045m ahead of Quad -> zero Z-fighting!)
            CreateWorldTmp("FacadeText_UnDF", central.transform, "UnDF", new Vector3(0f, 5.10f, 5.62f), 10.5f, new Color(0.36f, 0.21f, 0.24f, 1f), FontStyles.Normal, charSpacing: 2f);
            CreateWorldTmp("FacadeText_Title", central.transform, "UNIVERSIDADE DO DISTRITO FEDERAL", new Vector3(0f, 4.42f, 5.62f), 2.70f, new Color(0.28f, 0.19f, 0.22f, 1f), FontStyles.Bold, charSpacing: 1.4f);
            CreateWorldTmp("FacadeText_Subtitle", central.transform, "PROFESSOR JORGE AMAURY MAIA NUNES", new Vector3(0f, 4.10f, 5.62f), 2.18f, new Color(0.34f, 0.25f, 0.27f, 1f), FontStyles.Normal, charSpacing: 1.1f);
            CreateWorldTmp("FacadeText_SecEdu", central.transform, "Secretaria\nde Educação", new Vector3(1.15f, 3.74f, 5.62f), 1.65f, new Color(0.18f, 0.16f, 0.18f, 1f), FontStyles.Bold, charSpacing: 0.4f, align: TextAlignmentOptions.Right, boxWidth: 1.65f);
            CreateWorldTmp("FacadeText_GDF", central.transform, "GDF", new Vector3(3.52f, 3.75f, 5.62f), 3.15f, new Color(0.96f, 0.97f, 1.0f, 1f), FontStyles.Bold, charSpacing: 3.5f, align: TextAlignmentOptions.Center, boxWidth: 1.80f);

            // Top-right cantilevered facade lamp arm (matching concept art)
            CreateBox("Facade_TopLampArm", central.transform, new Vector3(5.65f, 8.95f, 5.35f), new Vector3(0f, 0f, 32f), new Vector3(1.35f, 0.07f, 0.07f), mats.DarkAluminum);
            CreateBox("Facade_TopLampHead", central.transform, new Vector3(6.22f, 9.32f, 5.35f), new Vector3(0f, 0f, -15f), new Vector3(0.36f, 0.09f, 0.18f), mats.DarkAluminum);

            // --- 2. MAIN ENTRANCE, GLASS DOORS, RED LOBBY STAIR & CURVED MARQUEE ---
            var entrance = new GameObject("UnDF_MainEntrance");
            entrance.transform.SetParent(layerC.transform, false);

            // Recessed entrance frame piers
            CreateBox("Entrance_PierL", entrance.transform, new Vector3(-4.95f, 1.66f, 5.95f), Vector3.zero, new Vector3(0.55f, 3.08f, 0.75f), mats.DarkAluminum);
            CreateBox("Entrance_PierR", entrance.transform, new Vector3(4.95f, 1.66f, 5.95f), Vector3.zero, new Vector3(0.55f, 3.08f, 0.75f), mats.DarkAluminum);
            CreateBox("Entrance_HeaderBar", entrance.transform, new Vector3(0f, 3.12f, 5.95f), Vector3.zero, new Vector3(10.30f, 0.22f, 0.75f), mats.DarkAluminum);

            // Glass panels left & right of open central sliding door gap (X in [-0.75 .. +0.75])
            CreateBox("Entrance_GlassLeft", entrance.transform, new Vector3(-2.75f, 1.56f, 6.08f), Vector3.zero, new Vector3(3.95f, 2.86f, 0.08f), mats.GlassDark);
            CreateBox("Entrance_GlassRight", entrance.transform, new Vector3(2.75f, 1.56f, 6.08f), Vector3.zero, new Vector3(3.95f, 2.86f, 0.08f), mats.GlassDark);
            // Vertical aluminum mullions
            float[] mullionsX = { -4.68f, -2.75f, -0.78f, 0.78f, 2.75f, 4.68f };
            foreach (float mx in mullionsX)
            {
                CreateBox($"DoorMullion_{mx:F1}", entrance.transform, new Vector3(mx, 1.56f, 6.02f), Vector3.zero, new Vector3(0.08f, 2.90f, 0.14f), mats.DarkAluminum);
            }
            CreateBox("DoorTransomBar", entrance.transform, new Vector3(0f, 2.48f, 6.02f), Vector3.zero, new Vector3(9.45f, 0.08f, 0.14f), mats.DarkAluminum);

            // Illuminated interior lobby with modern Reception Desk + subtle back ramp (zero Lobby_RedStair)
            CreateBox("Lobby_Floor", entrance.transform, new Vector3(0f, 0.14f, 7.60f), Vector3.zero, new Vector3(9.60f, 0.06f, 3.20f), mats.ConcreteSidewalk);
            CreateBox("Lobby_BackWall", entrance.transform, new Vector3(0f, 1.66f, 9.10f), Vector3.zero, new Vector3(9.60f, 3.08f, 0.25f), mats.LobbyInterior);

            var receptionDesk = new GameObject("Lobby_ReceptionDesk");
            receptionDesk.transform.SetParent(entrance.transform, false);
            receptionDesk.transform.localPosition = new Vector3(0f, 0.17f, 7.15f);
            CreateBox("Desk_BaseKick", receptionDesk.transform, new Vector3(0f, 0.05f, 0.03f), Vector3.zero, new Vector3(1.68f, 0.10f, 0.44f), mats.DarkAluminum);
            CreateBox("Desk_Body", receptionDesk.transform, new Vector3(0f, 0.49f, 0f), Vector3.zero, new Vector3(1.74f, 0.80f, 0.52f), mats.AcUnitWhite);
            CreateBox("Desk_TopCounter", receptionDesk.transform, new Vector3(0f, 0.93f, -0.02f), Vector3.zero, new Vector3(1.86f, 0.08f, 0.64f), mats.ConcreteSidewalk);
            // Subtle institutional UnDF purple accent band on front of desk (0.03m clear -> zero Z-fighting)
            CreateNoShadowBox("Desk_PurpleStripe", receptionDesk.transform, new Vector3(0f, 0.68f, -0.295f), Vector3.zero, new Vector3(1.64f, 0.14f, 0.016f), mats.BannerPurple);
            // Reception monitor + keyboard on counter
            CreateBox("Desk_MonitorBase", receptionDesk.transform, new Vector3(0.30f, 0.99f, 0.04f), Vector3.zero, new Vector3(0.18f, 0.03f, 0.14f), mats.DarkAluminum);
            CreateBox("Desk_MonitorNeck", receptionDesk.transform, new Vector3(0.30f, 1.07f, 0.07f), Vector3.zero, new Vector3(0.05f, 0.14f, 0.04f), mats.DarkAluminum);
            CreateBox("Desk_MonitorScreen", receptionDesk.transform, new Vector3(0.30f, 1.24f, 0.05f), new Vector3(0f, -12f, 0f), new Vector3(0.46f, 0.28f, 0.035f), mats.DarkAluminum);
            CreateNoShadowBox("Desk_Keyboard", receptionDesk.transform, new Vector3(0.18f, 0.985f, -0.10f), Vector3.zero, new Vector3(0.34f, 0.02f, 0.12f), mats.DarkAluminum);
            // Receptionist chair / stool behind desk
            CreateCyl("Desk_ChairLeg", receptionDesk.transform, new Vector3(-0.18f, 0.28f, 0.55f), Vector3.zero, new Vector3(0.05f, 0.28f, 0.05f), mats.DarkAluminum);
            CreateCyl("Desk_ChairBase", receptionDesk.transform, new Vector3(-0.18f, 0.03f, 0.55f), Vector3.zero, new Vector3(0.36f, 0.03f, 0.36f), mats.DarkAluminum);
            CreateBox("Desk_ChairSeat", receptionDesk.transform, new Vector3(-0.18f, 0.56f, 0.55f), Vector3.zero, new Vector3(0.42f, 0.07f, 0.40f), mats.TrashBinGray);
            CreateBox("Desk_ChairBack", receptionDesk.transform, new Vector3(-0.18f, 0.88f, 0.72f), Vector3.zero, new Vector3(0.40f, 0.54f, 0.06f), mats.TrashBinGray);

            // Subtle internal accessibility ramp running laterally along the far back lobby wall (Z ~ 8.35..8.90)
            var backRamp = new GameObject("Lobby_BackRamp");
            backRamp.transform.SetParent(entrance.transform, false);
            CreateBox("BackRamp_Deck", backRamp.transform, new Vector3(0.20f, 0.44f, 8.62f), new Vector3(0f, 0f, 7.5f), new Vector3(4.60f, 0.14f, 0.68f), mats.ConcreteSidewalk);
            CreateBox("BackRamp_Landing", backRamp.transform, new Vector3(2.95f, 0.76f, 8.62f), Vector3.zero, new Vector3(1.10f, 0.14f, 0.72f), mats.ConcreteSidewalk);
            CreateBox("BackRamp_Handrail", backRamp.transform, new Vector3(0.20f, 1.18f, 8.26f), new Vector3(0f, 0f, 7.5f), new Vector3(4.65f, 0.04f, 0.04f), mats.SteelSilver);
            for (int p = 0; p < 4; p++)
            {
                float t = p / 3f;
                float px = Mathf.Lerp(-1.95f, 2.35f, t);
                float py = Mathf.Lerp(0.52f, 1.08f, t);
                CreateCyl($"BackRamp_Post_{p}", backRamp.transform, new Vector3(px, py, 8.26f), Vector3.zero, new Vector3(0.035f, 0.38f, 0.035f), mats.SteelSilver);
            }

            // Paper flyers / academic notices taped on right-side glass panels (Z = 5.99 vs Glass front 6.04 -> 0.05m clear)
            Vector3[] flyerPositions =
            {
                new Vector3(1.20f, 2.02f, 5.99f), new Vector3(1.58f, 1.98f, 5.99f),
                new Vector3(1.98f, 2.05f, 5.99f), new Vector3(2.38f, 1.96f, 5.99f),
                new Vector3(1.30f, 1.54f, 5.99f), new Vector3(1.74f, 1.50f, 5.99f),
                new Vector3(2.18f, 1.56f, 5.99f), new Vector3(2.54f, 1.52f, 5.99f)
            };
            for (int f = 0; f < flyerPositions.Length; f++)
            {
                CreateNoShadowBox($"GlassFlyer_{f}", entrance.transform, flyerPositions[f], new Vector3(0f, 0f, (f % 3 - 1) * 2.5f), new Vector3(0.24f, 0.32f, 0.012f), mats.FlyerPaper);
            }

            // Curved translucent metallic entrance marquee advancing to Z = 3.55
            var marquee = new GameObject("Entrance_CurvedMarquee");
            marquee.transform.SetParent(entrance.transform, false);
            float[] postXs = { -4.35f, 0.95f, 3.35f, 4.45f };
            foreach (float px in postXs)
            {
                CreateBox($"MarqueePost_{px:F1}", marquee.transform, new Vector3(px, 1.66f, 3.65f), Vector3.zero, new Vector3(0.09f, 3.08f, 0.09f), mats.DarkAluminum);
            }
            CreateBox("Marquee_FrontBeam", marquee.transform, new Vector3(0.05f, 3.16f, 3.65f), Vector3.zero, new Vector3(9.05f, 0.11f, 0.12f), mats.DarkAluminum);
            CreateBox("Marquee_SideBeamL", marquee.transform, new Vector3(-4.35f, 3.22f, 4.72f), Vector3.zero, new Vector3(0.10f, 0.10f, 2.25f), mats.DarkAluminum);
            CreateBox("Marquee_SideBeamR", marquee.transform, new Vector3(4.45f, 3.22f, 4.72f), Vector3.zero, new Vector3(0.10f, 0.10f, 2.25f), mats.DarkAluminum);

            // Curved translucent polycarbonate arch built from 4 angled roof segments
            (float z, float y, float pitch, float depth)[] archSegs =
            {
                (3.85f, 3.24f,  16f, 0.56f),
                (4.38f, 3.36f,   7f, 0.62f),
                (4.95f, 3.40f,  -4f, 0.62f),
                (5.48f, 3.32f, -14f, 0.58f)
            };
            for (int a = 0; a < archSegs.Length; a++)
            {
                var seg = archSegs[a];
                GameObject pane = CreateBox($"MarqueeArch_{a}", marquee.transform, new Vector3(0.05f, seg.y, seg.z), new Vector3(seg.pitch, 0f, 0f), new Vector3(8.92f, 0.045f, seg.depth), mats.MarqueeGlass);
                DisableShadows(pane);
            }

            // --- 3. LEFT 3-STORY WING (X in [-16.0 .. -5.2]) ---
            var leftWing = new GameObject("UnDF_LeftWing");
            leftWing.transform.SetParent(layerC.transform, false);
            CreateBox("LeftWing_Body", leftWing.transform, new Vector3(-10.60f, 4.56f, 8.65f), Vector3.zero, new Vector3(10.80f, 8.88f, 5.50f), mats.ConcreteWall);
            CreateBox("LeftWing_RoofCap", leftWing.transform, new Vector3(-10.60f, 9.06f, 8.60f), Vector3.zero, new Vector3(11.00f, 0.16f, 5.70f), mats.DarkAluminum);

            // 3 stories of dark-framed horizontal windows (front face Z = 5.90 -> frame Z = 5.87, glass Z = 5.855, night glow Z = 5.81)
            float[] floorYs = { 1.65f, 4.45f, 7.20f };
            float[] leftColXs = { -6.80f, -9.85f, -12.90f, -14.85f };
            for (int f = 0; f < floorYs.Length; f++)
            {
                for (int c = 0; c < leftColXs.Length; c++)
                {
                    float wx = leftColXs[c];
                    float wy = floorYs[f];
                    float ww = (c == 3) ? 1.35f : 2.35f;
                    float wh = (f == 0) ? 1.85f : 1.55f;
                    BuildCampusWindow($"LeftWin_F{f}_C{c}", leftWing.transform, new Vector3(wx, wy, 5.87f), ww, wh, mats, hasTiltedPane: (f == 1 && c == 1));

                    // Register warm/cool lit classroom window for nightOnlyObjects (>= 0.045m ahead of glass)
                    bool isLit = !((f == 0 && c == 3) || (f == 2 && c == 2));
                    if (isLit)
                    {
                        nightWindowSpecs.Add(($"NightWin_Left_F{f}_C{c}", new Vector3(wx, wy, 5.805f), new Vector3(ww - 0.16f, wh - 0.16f, 0.022f), (f + c) % 2 == 0));
                    }

                    // External white AC condenser units mounted beneath upper-story windows
                    if (f >= 1 && c <= 2)
                    {
                        BuildAcCondenser($"LeftAC_F{f}_C{c}", leftWing.transform, new Vector3(wx + ww * 0.30f, wy - wh * 0.5f - 0.34f, 5.66f), mats);
                    }
                }
            }

            // Accessibility ramp with double metallic handrails along LeftWing ground floor (X in [-13.6..-8.4], Z = 4.65)
            var accessRamp = new GameObject("LeftWing_AccessibilityRamp");
            accessRamp.transform.SetParent(leftWing.transform, false);
            CreateBox("Ramp_Slab", accessRamp.transform, new Vector3(-11.0f, 0.36f, 4.95f), new Vector3(0f, 0f, -5.5f), new Vector3(5.20f, 0.24f, 1.35f), mats.ConcreteSidewalk);
            CreateBox("Ramp_Landing", accessRamp.transform, new Vector3(-14.1f, 0.56f, 4.95f), Vector3.zero, new Vector3(1.25f, 0.52f, 1.45f), mats.ConcreteSidewalk);
            CreateBox("Ramp_RailTop", accessRamp.transform, new Vector3(-11.0f, 1.15f, 4.30f), new Vector3(0f, 0f, -5.5f), new Vector3(5.30f, 0.045f, 0.045f), mats.DarkAluminum);
            CreateBox("Ramp_RailMid", accessRamp.transform, new Vector3(-11.0f, 0.85f, 4.30f), new Vector3(0f, 0f, -5.5f), new Vector3(5.30f, 0.035f, 0.035f), mats.DarkAluminum);
            for (int rp = 0; rp < 5; rp++)
            {
                float t = rp / 4f;
                float rx = Mathf.Lerp(-13.5f, -8.5f, t);
                float ry = Mathf.Lerp(0.76f, 0.52f, t);
                CreateCyl($"RampPost_{rp}", accessRamp.transform, new Vector3(rx, ry, 4.30f), Vector3.zero, new Vector3(0.04f, 0.54f, 0.04f), mats.DarkAluminum);
            }

            // Two gray campus trash bins near left entrance pier (matching concept art)
            BuildStaticTrashBin("CampusBin_LeftEnt_0", leftWing.transform, new Vector3(-5.65f, 0.12f, 4.95f), mats);
            BuildStaticTrashBin("CampusBin_LeftEnt_1", leftWing.transform, new Vector3(-5.12f, 0.12f, 4.95f), mats);

            // Decorative metal crowd-control barrier on far-left sidewalk
            BuildStaticMetalBarrier("SidewalkBarrier_Left", leftWing.transform, new Vector3(-8.45f, 0.12f, 4.05f), mats);

            // Left campus streetlamp pole
            CreateCyl("CampusLampPole_Left", leftWing.transform, new Vector3(-13.20f, 2.95f, 3.45f), Vector3.zero, new Vector3(0.14f, 2.85f, 0.14f), mats.DarkAluminum);
            CreateBox("CampusLampArm_Left", leftWing.transform, new Vector3(-12.65f, 5.58f, 3.45f), new Vector3(0f, 0f, 10f), new Vector3(1.15f, 0.07f, 0.07f), mats.DarkAluminum);
            CreateBox("CampusLampHead_Left", leftWing.transform, new Vector3(-12.10f, 5.66f, 3.45f), Vector3.zero, new Vector3(0.34f, 0.09f, 0.18f), mats.DarkAluminum);

            // Left flank trees closing X < -14
            BuildCampusTree("CampusTree_FarLeft_0", leftWing.transform, new Vector3(-16.40f, 0.12f, 4.45f), scale: 0.92f, mats);
            BuildCampusTree("CampusTree_FarLeft_1", leftWing.transform, new Vector3(-20.20f, 0.12f, 5.20f), scale: 1.05f, mats);

            // --- 4. RIGHT 3-STORY WING, BIKE RACK, NO-PARKING SIGN & BIG GOLDEN-GREEN TREE (X in [+5.2..+22.0]) ---
            var rightWing = new GameObject("UnDF_RightWing");
            rightWing.transform.SetParent(layerC.transform, false);
            CreateBox("RightWing_Body", rightWing.transform, new Vector3(10.60f, 4.56f, 8.65f), Vector3.zero, new Vector3(10.80f, 8.88f, 5.50f), mats.ConcreteWall);
            CreateBox("RightWing_RoofCap", rightWing.transform, new Vector3(10.60f, 9.06f, 8.60f), Vector3.zero, new Vector3(11.00f, 0.16f, 5.70f), mats.DarkAluminum);

            float[] rightColXs = { 6.80f, 9.85f, 12.90f, 14.85f };
            for (int f = 0; f < floorYs.Length; f++)
            {
                for (int c = 0; c < rightColXs.Length; c++)
                {
                    float wx = rightColXs[c];
                    float wy = floorYs[f];
                    float ww = (c == 3) ? 1.35f : 2.35f;
                    float wh = (f == 0) ? 1.85f : 1.55f;
                    BuildCampusWindow($"RightWin_F{f}_C{c}", rightWing.transform, new Vector3(wx, wy, 5.87f), ww, wh, mats, hasTiltedPane: (f == 1 && c == 0));

                    bool isLit = !((f == 0 && c == 2) || (f == 2 && c == 1));
                    if (isLit)
                    {
                        nightWindowSpecs.Add(($"NightWin_Right_F{f}_C{c}", new Vector3(wx, wy, 5.805f), new Vector3(ww - 0.16f, wh - 0.16f, 0.022f), (f + c) % 2 != 0));
                    }

                    if (f >= 1 && c <= 2)
                    {
                        BuildAcCondenser($"RightAC_F{f}_C{c}", rightWing.transform, new Vector3(wx - ww * 0.28f, wy - wh * 0.5f - 0.34f, 5.66f), mats);
                    }
                }
            }

            // Metallic bicycle rack along right wing wall (X = +7.35, Z = 4.65)
            var bikeRack = new GameObject("RightWing_BikeRack");
            bikeRack.transform.SetParent(rightWing.transform, false);
            bikeRack.transform.position = new Vector3(7.35f, 0.12f, 4.75f);
            CreateBox("BikeRack_BaseF", bikeRack.transform, new Vector3(0f, 0.03f, -0.32f), Vector3.zero, new Vector3(2.35f, 0.05f, 0.06f), mats.DarkAluminum);
            CreateBox("BikeRack_BaseB", bikeRack.transform, new Vector3(0f, 0.03f, 0.32f), Vector3.zero, new Vector3(2.35f, 0.05f, 0.06f), mats.DarkAluminum);
            for (int b = 0; b < 7; b++)
            {
                float bx = -1.02f + b * 0.34f;
                CreateCyl($"BikeHoopL_{b}", bikeRack.transform, new Vector3(bx, 0.32f, -0.22f), new Vector3(14f, 0f, 0f), new Vector3(0.032f, 0.30f, 0.032f), mats.SteelSilver);
                CreateCyl($"BikeHoopR_{b}", bikeRack.transform, new Vector3(bx, 0.32f, 0.22f), new Vector3(-14f, 0f, 0f), new Vector3(0.032f, 0.30f, 0.032f), mats.SteelSilver);
                CreateBox($"BikeHoopTop_{b}", bikeRack.transform, new Vector3(bx, 0.58f, 0f), Vector3.zero, new Vector3(0.035f, 0.035f, 0.34f), mats.SteelSilver);
            }

            // "Proibido Estacionar" street sign on yellow-base pole + right trash bin (matching concept art)
            var noPark = new GameObject("RightSide_NoParkingSign");
            noPark.transform.SetParent(rightWing.transform, false);
            noPark.transform.position = new Vector3(8.75f, 0.12f, 3.35f);
            CreateCyl("Sign_YellowBase", noPark.transform, new Vector3(0f, 0.26f, 0f), Vector3.zero, new Vector3(0.18f, 0.26f, 0.18f), mats.PaintYellow);
            CreateCyl("Sign_Pole", noPark.transform, new Vector3(0f, 1.35f, 0f), Vector3.zero, new Vector3(0.055f, 1.15f, 0.055f), mats.SteelSilver);
            CreateBox("Sign_PlateBack", noPark.transform, new Vector3(0f, 2.38f, 0.01f), Vector3.zero, new Vector3(0.56f, 0.68f, 0.03f), mats.SteelSilver);
            GameObject signQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            signQuad.name = "Sign_Face";
            signQuad.transform.SetParent(noPark.transform, false);
            signQuad.transform.localPosition = new Vector3(0f, 2.38f, -0.032f); // >= 0.025m separation
            signQuad.transform.localScale = new Vector3(0.52f, 0.52f, 1f);
            Object.DestroyImmediate(signQuad.GetComponent<Collider>());
            signQuad.GetComponent<MeshRenderer>().sharedMaterial = mats.SignNoParking;

            BuildStaticTrashBin("CampusBin_Right", rightWing.transform, new Vector3(9.45f, 0.12f, 3.45f), mats);

            // Large leafy green-gold campus tree in planted curb bed on the right (X = 11.4, Z = 4.15)
            CreateBox("RightTree_GrassBed", rightWing.transform, new Vector3(11.65f, 0.145f, 4.15f), Vector3.zero, new Vector3(3.10f, 0.06f, 2.05f), mats.GrassBed);
            BuildCampusTree("CampusTree_HeroRight", rightWing.transform, new Vector3(11.45f, 0.16f, 4.15f), scale: 1.38f, mats);

            // Right campus streetlamp pole
            CreateCyl("CampusLampPole_Right", rightWing.transform, new Vector3(14.50f, 2.95f, 3.45f), Vector3.zero, new Vector3(0.14f, 2.85f, 0.14f), mats.DarkAluminum);
            CreateBox("CampusLampArm_Right", rightWing.transform, new Vector3(13.95f, 5.58f, 3.45f), new Vector3(0f, 0f, -10f), new Vector3(1.15f, 0.07f, 0.07f), mats.DarkAluminum);
            CreateBox("CampusLampHead_Right", rightWing.transform, new Vector3(13.40f, 5.66f, 3.45f), Vector3.zero, new Vector3(0.34f, 0.09f, 0.18f), mats.DarkAluminum);
        }

        // ==================== LAYER D: NEIGHBOR MODERNIST CAMPUS BUILDINGS & PARALLAX ====================

        private static GameObject BuildLayerDNeighborCampusAndHorizon(
            Transform parent,
            UnDFMaterials mats,
            List<(string Name, Vector3 WorldPos, Vector3 Size, bool Cool)> nightWindowSpecs)
        {
            var layerD = new GameObject("LayerD_Parallax_CampusBackdrop");
            layerD.transform.SetParent(parent, false);
            var parallax = layerD.AddComponent<StageParallaxLayer>();
            var pSo = new SerializedObject(parallax);
            SerializedProperty factorProp = pSo.FindProperty("parallaxFactor");
            if (factorProp != null) factorProp.floatValue = 0.08f;
            pSo.ApplyModifiedPropertiesWithoutUndo();

            // Left neighboring modernist campus buildings closing continuously past X = -28m
            BuildNeighborBuilding("NeighborCampus_LeftNear", layerD.transform, new Vector3(-19.5f, 5.2f, 13.5f), new Vector3(9.5f, 10.4f, 6.5f), mats.NeighborWallA, mats, nightWindowSpecs, addNightWins: false);
            BuildNeighborBuilding("NeighborCampus_LeftFar", layerD.transform, new Vector3(-28.5f, 6.0f, 18.0f), new Vector3(11.0f, 12.0f, 7.0f), mats.NeighborWallB, mats, nightWindowSpecs, addNightWins: false);

            // Right neighboring modernist campus building (visible behind the right tree, faithful to concept art!)
            BuildNeighborBuilding("NeighborCampus_RightNear", layerD.transform, new Vector3(19.2f, 5.4f, 13.2f), new Vector3(10.0f, 10.8f, 6.5f), mats.NeighborWallA, mats, nightWindowSpecs, addNightWins: false);
            BuildNeighborBuilding("NeighborCampus_RightFar", layerD.transform, new Vector3(28.5f, 6.2f, 18.5f), new Vector3(11.0f, 12.4f, 7.2f), mats.NeighborWallB, mats, nightWindowSpecs, addNightWins: false);

            // Distant Brasília skyline blocks behind the campus (Z = 28..38)
            CreateBox("DistantCampusBlock_L", layerD.transform, new Vector3(-12.5f, 5.5f, 28.0f), Vector3.zero, new Vector3(14.0f, 11.0f, 6.0f), mats.NeighborWallB);
            CreateBox("DistantCampusBlock_C", layerD.transform, new Vector3(1.5f, 4.8f, 32.0f), Vector3.zero, new Vector3(16.0f, 9.6f, 6.0f), mats.NeighborWallA);
            CreateBox("DistantCampusBlock_R", layerD.transform, new Vector3(16.5f, 5.8f, 29.0f), Vector3.zero, new Vector3(14.0f, 11.6f, 6.0f), mats.NeighborWallB);

            // Background campus tree line
            BuildCampusTree("BackdropTree_L", layerD.transform, new Vector3(-15.5f, 0.1f, 16.5f), scale: 1.25f, mats);
            BuildCampusTree("BackdropTree_R", layerD.transform, new Vector3(15.2f, 0.1f, 15.8f), scale: 1.30f, mats);
            BuildCampusTree("BackdropTree_FarR", layerD.transform, new Vector3(23.5f, 0.1f, 10.5f), scale: 1.15f, mats);

            // Distant ground horizon apron so distant horizon never shows sky under buildings
            CreateBox("DistantCampusGround", layerD.transform, new Vector3(0f, -0.08f, 26.0f), Vector3.zero, new Vector3(96.0f, 0.12f, 42.0f), mats.GrassBed);

            return layerD;
        }

        private static void BuildNeighborBuilding(
            string name,
            Transform parent,
            Vector3 center,
            Vector3 size,
            Material wallMat,
            UnDFMaterials mats,
            List<(string Name, Vector3 WorldPos, Vector3 Size, bool Cool)> nightWindowSpecs,
            bool addNightWins)
        {
            var b = new GameObject(name);
            b.transform.SetParent(parent, false);
            CreateBox("Body", b.transform, center, Vector3.zero, size, wallMat);
            CreateBox("RoofTrim", b.transform, new Vector3(center.x, center.y + size.y * 0.5f + 0.12f, center.z), Vector3.zero, new Vector3(size.x + 0.3f, 0.24f, size.z + 0.3f), mats.DarkAluminum);

            float frontZ = center.z - size.z * 0.5f - 0.03f;
            for (int f = 0; f < 3; f++)
            {
                float wy = center.y - size.y * 0.28f + f * (size.y * 0.28f);
                CreateBox($"BandWin_{f}", b.transform, new Vector3(center.x, wy, frontZ), Vector3.zero, new Vector3(size.x * 0.78f, size.y * 0.14f, 0.04f), mats.GlassDark);
                if (addNightWins)
                {
                    nightWindowSpecs.Add(($"{name}_NightWin_{f}", new Vector3(center.x, wy, frontZ - 0.045f), new Vector3(size.x * 0.72f, size.y * 0.12f, 0.02f), f % 2 == 0));
                }
            }
        }

        // ==================== LAYER B: INTERACTIVE PHYSICS V2 PROPS ====================

        private static void InstantiateUnDFInteractiveProps(Transform propsRoot, UnDFMaterials mats)
        {
            GameObject flagPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HangingFlagPrefabPath);
            GameObject barrierPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BreakableBarrierPrefabPath);
            GameObject easelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BreakableEaselPrefabPath);
            GameObject throwConePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThrowableConePrefabPath);
            GameObject throwBinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThrowableTrashBinPrefabPath);
            GameObject dynCanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StagePhysicsSetupEditor.DynamicCanPrefabPath);
            GameObject dynConePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaFavelaUpgradeEditor.DynamicConePrefabPath);

            // 1. Three physical HingeJoint flags right of the entrance (DF, Brazil, UnDF)
            if (flagPrefab != null)
            {
                InstantiateFlagVariant(flagPrefab, "Hanging_Flag_DF", propsRoot, new Vector3(3.90f, 0.12f, 3.92f), poleHeight: 3.75f, mats.FlagDF);
                InstantiateFlagVariant(flagPrefab, "Hanging_Flag_Brazil", propsRoot, new Vector3(4.45f, 0.12f, 3.92f), poleHeight: 4.30f, mats.FlagBrazil);
                InstantiateFlagVariant(flagPrefab, "Hanging_Flag_UnDF", propsRoot, new Vector3(5.00f, 0.12f, 3.92f), poleHeight: 3.75f, mats.FlagUnDF);
            }

            // 2. Two Breakable props with persistent StageDebris (metal containment barrier left + UnDF info easel right)
            if (barrierPrefab != null)
            {
                InstantiateProp(barrierPrefab, "Breakable_Barrier_Left", propsRoot, new Vector3(-6.40f, 0f, 1.90f), 6f);
            }
            if (easelPrefab != null)
            {
                InstantiateProp(easelPrefab, "Breakable_Easel_Right", propsRoot, new Vector3(6.40f, 0f, 1.90f), -8f);
            }

            // 4. Two MK-style Throwable props (HoldBlock + LightPunch)
            if (throwBinPrefab != null)
            {
                InstantiateProp(throwBinPrefab, "Throwable_TrashBin_Left", propsRoot, new Vector3(-3.90f, 0f, 1.35f), 10f);
            }
            if (throwConePrefab != null)
            {
                InstantiateProp(throwConePrefab, "Throwable_Cone_Right", propsRoot, new Vector3(3.40f, 0f, 1.35f), -12f);
            }

            // 5. Loose PhysX Dynamic props (kicked by FighterDebrisPusher, storm-pushed only in RainyNight)
            if (dynCanPrefab != null)
            {
                InstantiateProp(dynCanPrefab, "Dynamic_Can_Left", propsRoot, new Vector3(-1.25f, 0.09f, 0.36f), 18f);
                InstantiateProp(dynCanPrefab, "Dynamic_Can_Center", propsRoot, new Vector3(0.55f, 0.09f, -0.34f), -25f);
                InstantiateProp(dynCanPrefab, "Dynamic_Can_Right", propsRoot, new Vector3(4.65f, 0.09f, 0.42f), 32f);
            }
            if (dynConePrefab != null)
            {
                InstantiateProp(dynConePrefab, "Dynamic_Cone_Left", propsRoot, new Vector3(-5.35f, 0.005f, 0.85f), 14f);
                InstantiateProp(dynConePrefab, "Dynamic_Cone_Right", propsRoot, new Vector3(5.45f, 0.005f, 0.82f), -16f);
            }
        }

        private static void InstantiateFlagVariant(GameObject prefab, string instanceName, Transform parent, Vector3 basePos, float poleHeight, Material flagMat)
        {
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.name = instanceName;
            // Anchor sits at the vertical midpoint of the flag (poleHeight - 0.34m) so connectedAnchor == Vector3.zero!
            float anchorY = poleHeight - 0.34f;
            inst.transform.localPosition = basePos + new Vector3(0f, anchorY, 0f);
            inst.transform.localRotation = Quaternion.Euler(0f, 14f, 0f); // Slight yaw so +X wind billows the flag naturally
            inst.transform.localScale = Vector3.one;

            // Adjust pole cylinder to extend cleanly from sidewalk (y = -anchorY) up to top finial (y = +0.34)
            Transform pole = inst.transform.Find("FlagPole");
            if (pole != null)
            {
                pole.localPosition = new Vector3(0f, (0.34f - anchorY) * 0.5f, 0f);
                pole.localScale = new Vector3(0.055f, poleHeight * 0.5f, 0.055f);
            }

            Transform body = inst.transform.Find("HangingBody");
            if (body != null && flagMat != null)
            {
                foreach (MeshRenderer mr in body.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr.name == "FlagCloth")
                    {
                        mr.sharedMaterial = flagMat;
                    }
                }
            }
        }

        // ==================== WEATHER VISUAL LAYERS (DAY / NIGHT / RAINY NIGHT) ====================

        private static GameObject BuildDaySun(Transform backdropParent, UnDFMaterials mats)
        {
            var sunRoot = new GameObject(DaySunName);
            sunRoot.transform.SetParent(backdropParent, false);
            GameObject disc = CreateSphere("Day_SunDisc", sunRoot.transform, new Vector3(-16.5f, 16.5f, 48.0f), Vector3.zero, new Vector3(3.2f, 3.2f, 0.3f), mats.SunGlow);
            DisableShadows(disc);
            GameObject halo = CreateSphere("Day_SunHalo", sunRoot.transform, new Vector3(-16.5f, 16.5f, 48.45f), Vector3.zero, new Vector3(6.0f, 6.0f, 0.25f), mats.SunGlow);
            DisableShadows(halo);
            return sunRoot;
        }

        private static GameObject BuildClearNightSky(Transform backdropParent, UnDFMaterials mats)
        {
            var sky = new GameObject(ClearNightSkyName);
            sky.transform.SetParent(backdropParent, false);

            GameObject moon = CreateSphere("UnDF_MoonDisc", sky.transform, new Vector3(16.8f, 16.4f, 48.5f), Vector3.zero, new Vector3(2.4f, 2.4f, 0.3f), mats.MoonGlow);
            DisableShadows(moon);

            Vector3[] stars =
            {
                new Vector3(-26f, 17.5f, 49.5f), new Vector3(-22f, 19.2f, 49.5f), new Vector3(-18f, 16.8f, 49.5f),
                new Vector3(-14f, 20.1f, 49.5f), new Vector3(-10f, 17.9f, 49.5f), new Vector3(-6f, 19.6f, 49.5f),
                new Vector3(-2f, 18.2f, 49.5f),  new Vector3(2f, 20.4f, 49.5f),   new Vector3(6f, 18.8f, 49.5f),
                new Vector3(10f, 17.2f, 49.5f),  new Vector3(13.5f, 19.5f, 49.5f),new Vector3(19.5f, 18.6f, 49.5f),
                new Vector3(23f, 16.9f, 49.5f),  new Vector3(26.5f, 19.4f, 49.5f),new Vector3(-16f, 14.5f, 49.5f),
                new Vector3(-12f, 15.2f, 49.5f), new Vector3(-7.5f, 15.8f, 49.5f),new Vector3(7.5f, 15.4f, 49.5f),
                new Vector3(12f, 14.8f, 49.5f),  new Vector3(15.2f, 14.2f, 49.5f),new Vector3(21f, 14.6f, 49.5f),
                new Vector3(-24f, 15.6f, 49.5f), new Vector3(-19.5f, 18.1f, 49.5f),new Vector3(0f, 16.9f, 49.5f),
                new Vector3(4.5f, 16.2f, 49.5f), new Vector3(18.5f, 20.6f, 49.5f),new Vector3(-4.5f, 20.8f, 49.5f),
                new Vector3(24.5f, 14.9f, 49.5f)
            };
            for (int i = 0; i < stars.Length; i++)
            {
                float s = (i % 3 == 0) ? 0.24f : 0.16f;
                GameObject st = CreateSphere($"UnDF_Star_{i}", sky.transform, stars[i], Vector3.zero, new Vector3(s, s, 0.10f), mats.StarGlow);
                DisableShadows(st);
            }

            return sky;
        }

        private static GameObject BuildNightLightsAndWindows(
            UnDFMaterials mats,
            List<(string Name, Vector3 WorldPos, Vector3 Size, bool Cool)> nightWindowSpecs)
        {
            var nightRoot = new GameObject(NightLightsAndWindowsName);

            // Point/Spot lights — ALL strictly LightShadows.None for 60+ FPS performance!
            // 1. Uplight / Spot highlighting the iconic giant UnDF facade sign
            CreatePointLight("NightLight_UnDFFacadeGlow", nightRoot.transform, new Vector3(0f, 6.25f, 2.55f), new Color(0.90f, 0.84f, 1.0f, 1f), intensity: 10.5f, range: 13.0f);
            // 2. Top-right cantilevered facade lamp
            CreatePointLight("NightLight_FacadeTopLamp", nightRoot.transform, new Vector3(6.10f, 8.85f, 4.90f), new Color(1.0f, 0.84f, 0.56f, 1f), intensity: 11.5f, range: 13.0f);
            // 3. Illuminated lobby & red staircase inside main entrance
            CreatePointLight("NightLight_LobbyInterior", nightRoot.transform, new Vector3(0f, 2.45f, 7.20f), new Color(1.0f, 0.82f, 0.52f, 1f), intensity: 10.5f, range: 9.5f);
            // 4. Two marquee downlights illuminating the entrance & combat street
            CreatePointLight("NightLight_MarqueeL", nightRoot.transform, new Vector3(-2.65f, 2.95f, 3.85f), new Color(0.94f, 0.96f, 1.0f, 1f), intensity: 9.5f, range: 11.5f);
            CreatePointLight("NightLight_MarqueeR", nightRoot.transform, new Vector3(2.85f, 2.95f, 3.85f), new Color(0.94f, 0.96f, 1.0f, 1f), intensity: 9.5f, range: 11.5f);
            // 5. Side streetlamp poles
            CreatePointLight("NightLight_PoleLeft", nightRoot.transform, new Vector3(-12.10f, 5.25f, 2.95f), new Color(1.0f, 0.82f, 0.52f, 1f), intensity: 12.0f, range: 14.0f);
            CreatePointLight("NightLight_PoleRight", nightRoot.transform, new Vector3(13.40f, 5.25f, 2.95f), new Color(1.0f, 0.82f, 0.52f, 1f), intensity: 12.0f, range: 14.0f);

            // Emissive streetlamp lenses (offset downward >= 0.03m from metal housings)
            CreateNoShadowBox("LampGlow_FacadeTop", nightRoot.transform, new Vector3(6.22f, 9.25f, 5.35f), new Vector3(0f, 0f, -15f), new Vector3(0.30f, 0.04f, 0.14f), mats.LampGlow);
            CreateNoShadowBox("LampGlow_PoleLeft", nightRoot.transform, new Vector3(-12.10f, 5.59f, 3.45f), Vector3.zero, new Vector3(0.28f, 0.04f, 0.14f), mats.LampGlow);
            CreateNoShadowBox("LampGlow_PoleRight", nightRoot.transform, new Vector3(13.40f, 5.59f, 3.45f), Vector3.zero, new Vector3(0.28f, 0.04f, 0.14f), mats.LampGlow);
            CreateNoShadowBox("LampGlow_MarqueeL", nightRoot.transform, new Vector3(-2.65f, 3.12f, 4.35f), Vector3.zero, new Vector3(0.95f, 0.04f, 0.18f), mats.NightWindowCool);
            CreateNoShadowBox("LampGlow_MarqueeR", nightRoot.transform, new Vector3(2.85f, 3.12f, 4.35f), Vector3.zero, new Vector3(0.95f, 0.04f, 0.18f), mats.NightWindowCool);

            // 20+ lit classroom windows across Left & Right 3-story wings (strictly >= 0.035m ahead of base panes)
            var windowsContainer = new GameObject("UnDF_LitClassroomWindows");
            windowsContainer.transform.SetParent(nightRoot.transform, false);
            for (int i = 0; i < nightWindowSpecs.Count; i++)
            {
                var sp = nightWindowSpecs[i];
                CreateNoShadowBox(sp.Name, windowsContainer.transform, sp.WorldPos, Vector3.zero, sp.Size, sp.Cool ? mats.NightWindowCool : mats.NightWindowWarm);
            }

            return nightRoot;
        }

        private static GameObject BuildRainPuddles(UnDFMaterials mats)
        {
            var puddlesRoot = new GameObject(RainPuddlesName);

            // 7 shallow reflective puddles over parking asphalt (Y = 0.016m > 0.00m) & sidewalk (Y = 0.136m > 0.12m) -> zero Z-fighting
            BuildOrganicPuddle("UnDF_Puddle_Left", puddlesRoot.transform, new Vector3(-5.60f, 0.016f, 0.82f), new Vector2(2.60f, 1.15f), 7f, mats.RainPuddle);
            BuildOrganicPuddle("UnDF_Puddle_CenterLeft", puddlesRoot.transform, new Vector3(-2.05f, 0.016f, 1.05f), new Vector2(2.90f, 1.25f), -5f, mats.RainPuddle);
            BuildOrganicPuddle("UnDF_Puddle_FrontCenter", puddlesRoot.transform, new Vector3(0.45f, 0.016f, -1.05f), new Vector2(2.80f, 1.10f), 4f, mats.RainPuddle);
            BuildOrganicPuddle("UnDF_Puddle_CenterRight", puddlesRoot.transform, new Vector3(3.15f, 0.016f, 0.92f), new Vector2(2.45f, 1.10f), -8f, mats.RainPuddle);
            BuildOrganicPuddle("UnDF_Puddle_FarRight", puddlesRoot.transform, new Vector3(6.85f, 0.016f, 0.78f), new Vector2(2.40f, 1.05f), 10f, mats.RainPuddle);
            BuildOrganicPuddle("UnDF_Puddle_FrontLeft", puddlesRoot.transform, new Vector3(-4.20f, 0.016f, -2.90f), new Vector2(3.10f, 1.35f), -6f, mats.RainPuddle);
            BuildOrganicPuddle("UnDF_Puddle_SidewalkEntry", puddlesRoot.transform, new Vector3(-1.35f, 0.136f, 3.85f), new Vector2(2.15f, 0.85f), 3f, mats.RainPuddle);

            return puddlesRoot;
        }

        private static void BuildOrganicPuddle(string name, Transform parent, Vector3 pos, Vector2 sizeXZ, float rotY, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            GameObject mainDisc = CreateCyl("Puddle_Main", go.transform, Vector3.zero, Vector3.zero, new Vector3(sizeXZ.x, 0.003f, sizeXZ.y), mat);
            DisableShadows(mainDisc);
            GameObject lobeDisc = CreateCyl("Puddle_Lobe", go.transform, new Vector3(sizeXZ.x * 0.24f, 0.002f, sizeXZ.y * 0.16f), Vector3.zero, new Vector3(sizeXZ.x * 0.56f, 0.0025f, sizeXZ.y * 0.66f), mat);
            DisableShadows(lobeDisc);
        }

        // ==================== STAGE MANAGERS & WEATHER WIRING ====================

        private static void WireStageManagersAndWeather(
            Scene scene,
            StageData undfData,
            Light sunKeyLight,
            Light[] secondaryLights,
            float[] secondaryBaseIntensities,
            GameObject daySun,
            GameObject clearNightSky,
            GameObject nightLightsAndWindows,
            GameObject rainPuddles,
            StageFramingStandards.WeatherCloudLayers clouds,
            List<Renderer> wetRenderers)
        {
            GameObject managerGo = FindInSceneByName(scene, "StageInteractableManager");
            if (managerGo == null)
            {
                managerGo = new GameObject("StageInteractableManager");
            }

            FightSimulation sim = Object.FindAnyObjectByType<FightSimulation>();
            MatchManager match = Object.FindAnyObjectByType<MatchManager>();

            StageInteractableManager manager = managerGo.GetComponent<StageInteractableManager>() ?? managerGo.AddComponent<StageInteractableManager>();
            var managerSo = new SerializedObject(manager);
            managerSo.FindProperty("simulation").objectReferenceValue = sim;
            managerSo.FindProperty("matchManager").objectReferenceValue = match;
            managerSo.FindProperty("ensureStagePhysicsBounds").boolValue = true;
            managerSo.ApplyModifiedPropertiesWithoutUndo();
            manager.EnsureStagePhysicsBounds();

            StageWindController wind = managerGo.GetComponent<StageWindController>() ?? managerGo.AddComponent<StageWindController>();
            wind.Configure(new Vector3(1f, 0f, 0.18f), baseSpeed: 0.55f, gustAmp: 0.45f, gustFreq: 0.22f);

            StageWeatherController weather = managerGo.GetComponent<StageWeatherController>() ?? managerGo.AddComponent<StageWeatherController>();

            // Brasília sunny Day profile (calibrated wind 0.55 / 0.45, crisp clear blue sky warming near horizon)
            var dayProfile = new StageWeatherController.WeatherProfile
            {
                directionalLightColor = new Color(1.00f, 0.92f, 0.78f, 1f),
                directionalLightIntensity = 2.15f,
                directionalLightEuler = new Vector3(32f, 215f, 0f),
                ambientSkyColor = new Color(0.62f, 0.78f, 0.96f, 1f),
                ambientEquatorColor = new Color(0.76f, 0.74f, 0.70f, 1f),
                ambientGroundColor = new Color(0.32f, 0.30f, 0.30f, 1f),
                fogEnabled = true,
                fogColor = new Color(0.76f, 0.83f, 0.92f, 1f),
                fogDensity = 0.0055f,
                cameraBackgroundColor = new Color(0.56f, 0.74f, 0.94f, 1f),
                skyTintColor = new Color(0.52f, 0.74f, 0.96f, 1f),
                skyGroundColor = new Color(0.46f, 0.44f, 0.40f, 1f),
                skyExposure = 1.25f,
                skyAtmosphereThickness = 0.95f,
                windBaseSpeed = 0.55f,
                windGustAmplitude = 0.45f,
                windGustFrequency = 0.22f,
                secondaryLightsMultiplier = 1.0f
            };

            var nightProfile = StageWeatherController.WeatherProfile.CreateDefaultNight();
            var rainyProfile = StageWeatherController.WeatherProfile.CreateDefaultRainyNight();
            weather.SetProfiles(dayProfile, nightProfile, rainyProfile);

            var dayList = new List<GameObject> { daySun };
            if (clouds.DayClouds != null) dayList.Add(clouds.DayClouds);

            var clearNightList = new List<GameObject> { clearNightSky };
            if (clouds.ClearNightClouds != null) clearNightList.Add(clouds.ClearNightClouds);

            var nightList = new List<GameObject> { nightLightsAndWindows };

            var rainList = new List<GameObject> { rainPuddles };
            if (clouds.RainClouds != null) rainList.Add(clouds.RainClouds);

            var weatherSo = new SerializedObject(weather);
            weatherSo.FindProperty("stageData").objectReferenceValue = undfData;
            weatherSo.FindProperty("mainDirectionalLight").objectReferenceValue = sunKeyLight;
            weatherSo.FindProperty("windController").objectReferenceValue = wind;

            SerializedProperty secLightsProp = weatherSo.FindProperty("secondaryFillLights");
            secLightsProp.arraySize = secondaryLights.Length;
            SerializedProperty secIntProp = weatherSo.FindProperty("secondaryFillBaseIntensities");
            secIntProp.arraySize = secondaryBaseIntensities.Length;
            for (int i = 0; i < secondaryLights.Length; i++)
            {
                secLightsProp.GetArrayElementAtIndex(i).objectReferenceValue = secondaryLights[i];
                secIntProp.GetArrayElementAtIndex(i).floatValue = secondaryBaseIntensities[i];
            }

            AssignObjectArray(weatherSo.FindProperty("dayOnlyObjects"), dayList);
            AssignObjectArray(weatherSo.FindProperty("clearNightOnlyObjects"), clearNightList);
            AssignObjectArray(weatherSo.FindProperty("nightOnlyObjects"), nightList);
            AssignObjectArray(weatherSo.FindProperty("rainOnlyObjects"), rainList);
            AssignObjectArray(weatherSo.FindProperty("wetSurfaceRenderers"), wetRenderers);
            weatherSo.ApplyModifiedPropertiesWithoutUndo();

            manager.RefreshDiscoveredProps();
        }

        // ==================== REUSABLE UnDF PROP PREFABS ====================

        private static void CreateOrUpdateUnDFPropPrefabs(UnDFMaterials mats)
        {
            if (!AssetDatabase.IsValidFolder(StagePropsPrefabFolder))
            {
                Directory.CreateDirectory(StagePropsPrefabFolder);
                AssetDatabase.Refresh();
            }

            BuildHangingFlagPrefab(HangingFlagPrefabPath, mats.SteelSilver, mats.FlagUnDF);
            BuildHangingBannerPrefab(HangingBannerPrefabPath, mats.DarkAluminum, mats.BannerPurple);
            BuildBreakableBarrierPrefab(BreakableBarrierPrefabPath, mats.SteelSilver, mats.DarkAluminum);
            BuildBreakableEaselPrefab(BreakableEaselPrefabPath, mats.DarkAluminum, mats.FacadeLogo);
            BuildThrowableConePrefab(ThrowableConePrefabPath, mats.ConeOrange, mats.ConeStripeWhite, mats.DarkAluminum);
            BuildThrowableTrashBinPrefab(ThrowableTrashBinPrefabPath, mats.TrashBinGray, mats.DarkAluminum);
        }

        private static void BuildHangingFlagPrefab(string prefabPath, Material poleMat, Material defaultFlagMat)
        {
            var root = new GameObject("Prop_UnDF_HangingFlag");
            try
            {
                // Flagpole & top finial on kinematic anchor (anchor Y = flag center, pole top at Y = +0.34)
                CreateCyl("FlagPole", root.transform, new Vector3(0f, -1.56f, 0f), Vector3.zero, new Vector3(0.055f, 1.90f, 0.055f), poleMat);
                CreateSphere("FlagFinial", root.transform, new Vector3(0f, 0.38f, 0f), Vector3.zero, new Vector3(0.11f, 0.11f, 0.11f), poleMat);

                var anchorRb = root.AddComponent<Rigidbody>();
                anchorRb.isKinematic = true;
                anchorRb.useGravity = false;

                var bodyGo = new GameObject("HangingBody");
                bodyGo.layer = StagePhysicsLayers.StageDebrisLayer;
                bodyGo.transform.SetParent(root.transform, false);
                bodyGo.transform.localPosition = new Vector3(0.46f, 0f, 0f);
                bodyGo.transform.localRotation = Quaternion.identity;

                // Flag cloth panel attached along its left edge to the pole
                CreateBox("FlagCloth", bodyGo.transform, Vector3.zero, Vector3.zero, new Vector3(0.88f, 0.56f, 0.032f), defaultFlagMat);

                var box = bodyGo.AddComponent<BoxCollider>();
                box.size = new Vector3(0.88f, 0.56f, 0.04f);
                box.center = Vector3.zero;
                box.isTrigger = false;

                var swingRb = bodyGo.AddComponent<Rigidbody>();
                swingRb.mass = 0.30f;
                swingRb.isKinematic = false;
                swingRb.useGravity = true;
                swingRb.linearDamping = 0.35f;
                swingRb.angularDamping = 0.75f;
                swingRb.sleepThreshold = 0.02f;
                swingRb.interpolation = RigidbodyInterpolation.Interpolate;

                var hinge = bodyGo.AddComponent<HingeJoint>();
                hinge.connectedBody = anchorRb;
                hinge.axis = Vector3.up;
                hinge.anchor = new Vector3(-0.46f, 0f, 0f);
                hinge.autoConfigureConnectedAnchor = false;
                hinge.connectedAnchor = Vector3.zero;
                hinge.useSpring = true;
                hinge.spring = new JointSpring { spring = 2.4f, damper = 0.70f, targetPosition = 0f };
                hinge.useLimits = true;
                hinge.limits = new JointLimits { min = -68f, max = 68f, bounciness = 0.05f };

                var wind = bodyGo.AddComponent<StageWindAffectable>();
                wind.Configure(cd: 1.35f, areaM2: 0.42f, sensitivity: 1.25f, wakeThreshold: 0.65f, bodyOverride: swingRb);

                var hanging = root.AddComponent<StageHangingProp>();
                var so = new SerializedObject(hanging);
                so.FindProperty("anchorBody").objectReferenceValue = anchorRb;
                so.FindProperty("swingBody").objectReferenceValue = swingRb;
                so.FindProperty("hingeJoint").objectReferenceValue = hinge;
                so.FindProperty("windAffectable").objectReferenceValue = wind;
                so.FindProperty("hingeAxis").vector3Value = Vector3.up;
                so.FindProperty("localPivotOnSwingBody").vector3Value = new Vector3(-0.46f, 0f, 0f);
                so.FindProperty("swingMass").floatValue = 0.30f;
                so.FindProperty("linearDamping").floatValue = 0.35f;
                so.FindProperty("angularDamping").floatValue = 0.75f;
                so.FindProperty("springStrength").floatValue = 2.4f;
                so.FindProperty("springDamper").floatValue = 0.70f;
                so.FindProperty("maxSwingAngle").floatValue = 68f;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildHangingBannerPrefab(string prefabPath, Material frameMat, Material bannerMat)
        {
            var root = new GameObject("Prop_UnDF_HangingBanner");
            try
            {
                CreateBox("MountBar", root.transform, Vector3.zero, Vector3.zero, new Vector3(1.12f, 0.05f, 0.06f), frameMat);

                var anchorRb = root.AddComponent<Rigidbody>();
                anchorRb.isKinematic = true;
                anchorRb.useGravity = false;

                var bodyGo = new GameObject("HangingBody");
                bodyGo.layer = StagePhysicsLayers.StageDebrisLayer;
                bodyGo.transform.SetParent(root.transform, false);
                bodyGo.transform.localPosition = new Vector3(0f, -0.34f, 0f);
                bodyGo.transform.localRotation = Quaternion.identity;

                CreateCyl("RodL", bodyGo.transform, new Vector3(-0.42f, 0.22f, 0f), Vector3.zero, new Vector3(0.022f, 0.14f, 0.022f), frameMat);
                CreateCyl("RodR", bodyGo.transform, new Vector3(0.42f, 0.22f, 0f), Vector3.zero, new Vector3(0.022f, 0.14f, 0.022f), frameMat);
                CreateBox("BannerPanel", bodyGo.transform, new Vector3(0f, -0.02f, 0f), Vector3.zero, new Vector3(1.04f, 0.38f, 0.05f), bannerMat);
                CreateBox("BannerTrimTop", bodyGo.transform, new Vector3(0f, 0.18f, 0f), Vector3.zero, new Vector3(1.08f, 0.04f, 0.065f), frameMat);
                CreateBox("BannerTrimBot", bodyGo.transform, new Vector3(0f, -0.22f, 0f), Vector3.zero, new Vector3(1.08f, 0.04f, 0.065f), frameMat);
                CreateWorldTmp("BannerText", bodyGo.transform, "UnDF • CAMPUS", new Vector3(0f, -0.02f, -0.036f), 1.45f, Color.white, FontStyles.Bold, charSpacing: 2f);

                var box = bodyGo.AddComponent<BoxCollider>();
                box.size = new Vector3(1.08f, 0.42f, 0.07f);
                box.center = new Vector3(0f, -0.02f, 0f);
                box.isTrigger = false;

                var swingRb = bodyGo.AddComponent<Rigidbody>();
                swingRb.mass = 1.15f;
                swingRb.isKinematic = false;
                swingRb.useGravity = true;
                swingRb.linearDamping = 0.25f;
                swingRb.angularDamping = 0.80f;
                swingRb.sleepThreshold = 0.02f;
                swingRb.interpolation = RigidbodyInterpolation.Interpolate;

                var hinge = bodyGo.AddComponent<HingeJoint>();
                hinge.connectedBody = anchorRb;
                hinge.axis = Vector3.forward;
                hinge.anchor = new Vector3(0f, 0.34f, 0f);
                hinge.autoConfigureConnectedAnchor = false;
                hinge.connectedAnchor = Vector3.zero;
                hinge.useSpring = true;
                hinge.spring = new JointSpring { spring = 6.0f, damper = 1.5f, targetPosition = 0f };
                hinge.useLimits = true;
                hinge.limits = new JointLimits { min = -52f, max = 52f, bounciness = 0.08f };

                var wind = bodyGo.AddComponent<StageWindAffectable>();
                wind.Configure(cd: 1.25f, areaM2: 0.36f, sensitivity: 1.20f, wakeThreshold: 0.70f, bodyOverride: swingRb);

                var hanging = root.AddComponent<StageHangingProp>();
                var so = new SerializedObject(hanging);
                so.FindProperty("anchorBody").objectReferenceValue = anchorRb;
                so.FindProperty("swingBody").objectReferenceValue = swingRb;
                so.FindProperty("hingeJoint").objectReferenceValue = hinge;
                so.FindProperty("windAffectable").objectReferenceValue = wind;
                so.FindProperty("hingeAxis").vector3Value = Vector3.forward;
                so.FindProperty("localPivotOnSwingBody").vector3Value = new Vector3(0f, 0.34f, 0f);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildBreakableBarrierPrefab(string prefabPath, Material steelMat, Material darkMat)
        {
            var root = new GameObject("Prop_UnDF_BreakableBarrier");
            try
            {
                var intact = new GameObject("IntactVisual");
                intact.transform.SetParent(root.transform, false);

                CreateBox("TopBar", intact.transform, new Vector3(0f, 0.96f, 0f), Vector3.zero, new Vector3(1.55f, 0.055f, 0.055f), steelMat);
                CreateBox("BotBar", intact.transform, new Vector3(0f, 0.24f, 0f), Vector3.zero, new Vector3(1.55f, 0.05f, 0.05f), steelMat);
                CreateBox("LegL", intact.transform, new Vector3(-0.75f, 0.48f, 0f), Vector3.zero, new Vector3(0.055f, 0.96f, 0.055f), steelMat);
                CreateBox("LegR", intact.transform, new Vector3(0.75f, 0.48f, 0f), Vector3.zero, new Vector3(0.055f, 0.96f, 0.055f), steelMat);
                CreateBox("FootL", intact.transform, new Vector3(-0.75f, 0.025f, 0f), Vector3.zero, new Vector3(0.08f, 0.05f, 0.44f), darkMat);
                CreateBox("FootR", intact.transform, new Vector3(0.75f, 0.025f, 0f), Vector3.zero, new Vector3(0.08f, 0.05f, 0.44f), darkMat);
                for (int i = 0; i < 6; i++)
                {
                    float bx = Mathf.Lerp(-0.55f, 0.55f, i / 5f);
                    CreateBox($"Picket_{i}", intact.transform, new Vector3(bx, 0.60f, 0f), Vector3.zero, new Vector3(0.035f, 0.72f, 0.035f), steelMat);
                }

                var col = root.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.50f, 0f);
                col.size = new Vector3(1.60f, 1.00f, 0.48f);

                var pool = new GameObject("DebrisPool");
                pool.transform.SetParent(root.transform, false);
                for (int i = 0; i < 7; i++)
                {
                    Vector3 pos = new Vector3(Mathf.Lerp(-0.6f, 0.6f, i / 6f), 0.35f + (i % 3) * 0.22f, (i % 2 == 0 ? -0.06f : 0.06f));
                    Vector3 scl = (i < 2) ? new Vector3(0.68f, 0.055f, 0.055f) : new Vector3(0.045f, 0.58f, 0.045f);
                    CreateDebrisShard($"MetalBarShard_{i}", PrimitiveType.Cube, pool.transform, pos, new Vector3(i * 15f, i * 27f, i * 19f), scl, (i % 3 == 0) ? darkMat : steelMat);
                }

                ConfigureBreakableComponent(root, intact, col, pool.transform, StagePropMaterialType.ConcreteOrMetal, integrity: 2, radiusX: 1.65f, zTolerance: 2.15f);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildBreakableEaselPrefab(string prefabPath, Material darkMat, Material boardMat)
        {
            var root = new GameObject("Prop_UnDF_BreakableEasel");
            try
            {
                var intact = new GameObject("IntactVisual");
                intact.transform.SetParent(root.transform, false);

                CreateBox("BoardFrame", intact.transform, new Vector3(0f, 0.78f, 0f), new Vector3(-8f, 0f, 0f), new Vector3(0.88f, 0.78f, 0.06f), darkMat);
                CreateBox("BoardFace", intact.transform, new Vector3(0f, 0.78f, -0.036f), new Vector3(-8f, 0f, 0f), new Vector3(0.80f, 0.68f, 0.015f), boardMat);
                CreateBox("LegFL", intact.transform, new Vector3(-0.36f, 0.45f, -0.14f), new Vector3(-12f, 0f, 0f), new Vector3(0.05f, 0.92f, 0.05f), darkMat);
                CreateBox("LegFR", intact.transform, new Vector3(0.36f, 0.45f, -0.14f), new Vector3(-12f, 0f, 0f), new Vector3(0.05f, 0.92f, 0.05f), darkMat);
                CreateBox("LegBack", intact.transform, new Vector3(0f, 0.48f, 0.24f), new Vector3(18f, 0f, 0f), new Vector3(0.05f, 0.98f, 0.05f), darkMat);

                var col = root.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.60f, 0f);
                col.size = new Vector3(0.96f, 1.22f, 0.62f);

                var pool = new GameObject("DebrisPool");
                pool.transform.SetParent(root.transform, false);
                for (int i = 0; i < 6; i++)
                {
                    Vector3 pos = new Vector3(((i % 3) - 1) * 0.24f, 0.35f + (i / 3) * 0.42f, (i % 2 == 0 ? -0.06f : 0.08f));
                    Vector3 scl = (i < 3) ? new Vector3(0.36f, 0.30f, 0.04f) : new Vector3(0.055f, 0.56f, 0.055f);
                    CreateDebrisShard($"EaselShard_{i}", PrimitiveType.Cube, pool.transform, pos, new Vector3(i * 19f, i * 31f, i * 11f), scl, (i < 3) ? boardMat : darkMat);
                }

                ConfigureBreakableComponent(root, intact, col, pool.transform, StagePropMaterialType.Plastic, integrity: 2, radiusX: 1.65f, zTolerance: 2.15f);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildThrowableConePrefab(string prefabPath, Material orangeMat, Material stripeMat, Material baseMat)
        {
            var root = new GameObject("Prop_UnDF_ThrowableCone");
            root.layer = StagePhysicsLayers.StageProjectileLayer;
            try
            {
                var intact = new GameObject("IntactVisual");
                intact.transform.SetParent(root.transform, false);

                CreateBox("ConeBase", intact.transform, new Vector3(0f, 0.03f, 0f), Vector3.zero, new Vector3(0.42f, 0.06f, 0.42f), baseMat);
                CreateCyl("ConeTier0", intact.transform, new Vector3(0f, 0.16f, 0f), Vector3.zero, new Vector3(0.28f, 0.11f, 0.28f), orangeMat);
                CreateCyl("ConeStripe", intact.transform, new Vector3(0f, 0.31f, 0f), Vector3.zero, new Vector3(0.22f, 0.065f, 0.22f), stripeMat);
                CreateCyl("ConeTier1", intact.transform, new Vector3(0f, 0.44f, 0f), Vector3.zero, new Vector3(0.16f, 0.085f, 0.16f), orangeMat);
                CreateCyl("ConeTip", intact.transform, new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(0.095f, 0.045f, 0.095f), orangeMat);

                var col = root.AddComponent<BoxCollider>();
                col.isTrigger = false;
                col.center = new Vector3(0f, 0.30f, 0f);
                col.size = new Vector3(0.42f, 0.60f, 0.42f);

                var rb = root.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.mass = 2.0f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                var pool = new GameObject("DebrisPool");
                pool.transform.SetParent(root.transform, false);
                for (int i = 0; i < 6; i++)
                {
                    Material m = (i == 0) ? baseMat : ((i % 2 == 0) ? stripeMat : orangeMat);
                    CreateDebrisShard($"ConeShard_{i}", PrimitiveType.Cube, pool.transform, new Vector3(((i % 3) - 1) * 0.10f, 0.14f + (i / 3) * 0.22f, 0f), new Vector3(i * 22f, i * 35f, i * 14f), new Vector3(0.16f, 0.12f, 0.10f), m);
                }

                ConfigureThrowableComponent(root, intact, col, rb, pool.transform, "CONE", StagePropMaterialType.Plastic, damage: 80, causesKnockdown: true, shatterOnStage: false);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildThrowableTrashBinPrefab(string prefabPath, Material binMat, Material lidMat)
        {
            var root = new GameObject("Prop_UnDF_ThrowableTrashBin");
            root.layer = StagePhysicsLayers.StageProjectileLayer;
            try
            {
                var intact = new GameObject("IntactVisual");
                intact.transform.SetParent(root.transform, false);

                CreateCyl("BinBody", intact.transform, new Vector3(0f, 0.32f, 0f), Vector3.zero, new Vector3(0.40f, 0.32f, 0.40f), binMat);
                CreateCyl("BinRim", intact.transform, new Vector3(0f, 0.64f, 0f), Vector3.zero, new Vector3(0.44f, 0.04f, 0.44f), lidMat);
                CreateBox("BinHandleL", intact.transform, new Vector3(-0.22f, 0.56f, 0f), Vector3.zero, new Vector3(0.04f, 0.10f, 0.12f), lidMat);
                CreateBox("BinHandleR", intact.transform, new Vector3(0.22f, 0.56f, 0f), Vector3.zero, new Vector3(0.04f, 0.10f, 0.12f), lidMat);

                var col = root.AddComponent<BoxCollider>();
                col.isTrigger = false;
                col.center = new Vector3(0f, 0.34f, 0f);
                col.size = new Vector3(0.46f, 0.68f, 0.46f);

                var rb = root.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.mass = 2.6f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                var pool = new GameObject("DebrisPool");
                pool.transform.SetParent(root.transform, false);
                for (int i = 0; i < 6; i++)
                {
                    Material m = (i >= 4) ? lidMat : binMat;
                    CreateDebrisShard($"BinShard_{i}", PrimitiveType.Cube, pool.transform, new Vector3(((i % 3) - 1) * 0.12f, 0.18f + (i / 3) * 0.28f, (i % 2 == 0 ? -0.08f : 0.08f)), new Vector3(i * 17f, i * 43f, i * 21f), new Vector3(0.22f, 0.16f, 0.07f), m);
                }

                ConfigureThrowableComponent(root, intact, col, rb, pool.transform, "LIXEIRA", StagePropMaterialType.Plastic, damage: 85, causesKnockdown: true, shatterOnStage: false);
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

        private static void ConfigureThrowableComponent(
            GameObject root,
            GameObject intactVisual,
            Collider propCol,
            Rigidbody rb,
            Transform poolTransform,
            string promptLabel,
            StagePropMaterialType matType,
            int damage,
            bool causesKnockdown,
            bool shatterOnStage)
        {
            var prop = root.AddComponent<StageThrowableProp>();
            var so = new SerializedObject(prop);
            so.FindProperty("interactionRadiusX").floatValue = 1.65f;
            so.FindProperty("maxZDistanceFromFightPlane").floatValue = 1.85f;
            so.FindProperty("fightPlaneZ").floatValue = 0f;
            so.FindProperty("promptLabel").stringValue = promptLabel;
            so.FindProperty("materialType").enumValueIndex = (int)matType;
            so.FindProperty("damage").intValue = damage;
            so.FindProperty("causesKnockdown").boolValue = causesKnockdown;
            so.FindProperty("breakOnFighterHit").boolValue = true;
            so.FindProperty("breakOnStageHit").boolValue = shatterOnStage;
            so.FindProperty("intactVisualRoot").objectReferenceValue = intactVisual;
            so.FindProperty("propCollider").objectReferenceValue = propCol;
            so.FindProperty("body").objectReferenceValue = rb;
            so.FindProperty("debrisPoolRoot").objectReferenceValue = poolTransform;
            so.ApplyModifiedPropertiesWithoutUndo();
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

        // ==================== ARCHITECTURAL MICRO-BUILDERS ====================

        private static void BuildCampusWindow(string name, Transform parent, Vector3 center, float width, float height, UnDFMaterials mats, bool hasTiltedPane)
        {
            var win = new GameObject(name);
            win.transform.SetParent(parent, false);
            win.transform.localPosition = center;

            CreateBox("Frame", win.transform, Vector3.zero, Vector3.zero, new Vector3(width, height, 0.08f), mats.DarkAluminum);
            CreateBox("Glass", win.transform, new Vector3(0f, 0f, -0.025f), Vector3.zero, new Vector3(width - 0.14f, height - 0.14f, 0.04f), mats.GlassDark);
            // Mullions
            CreateBox("MidH", win.transform, new Vector3(0f, 0.04f, -0.042f), Vector3.zero, new Vector3(width - 0.08f, 0.05f, 0.04f), mats.DarkAluminum);
            int divs = width > 1.8f ? 3 : 2;
            for (int d = 1; d < divs; d++)
            {
                float dx = Mathf.Lerp(-width * 0.5f, width * 0.5f, d / (float)divs);
                CreateBox($"MidV_{d}", win.transform, new Vector3(dx, 0f, -0.042f), Vector3.zero, new Vector3(0.045f, height - 0.08f, 0.04f), mats.DarkAluminum);
            }

            if (hasTiltedPane)
            {
                CreateBox("TiltedSash", win.transform, new Vector3(-width * 0.16f, -height * 0.22f, -0.14f), new Vector3(26f, 0f, 0f), new Vector3(width * 0.30f, height * 0.42f, 0.03f), mats.GlassDark);
            }
        }

        private static void BuildAcCondenser(string name, Transform parent, Vector3 pos, UnDFMaterials mats)
        {
            var ac = new GameObject(name);
            ac.transform.SetParent(parent, false);
            ac.transform.localPosition = pos;
            CreateBox("Box", ac.transform, Vector3.zero, Vector3.zero, new Vector3(0.68f, 0.46f, 0.34f), mats.AcUnitWhite);
            CreateCyl("FanGrille", ac.transform, new Vector3(-0.10f, 0f, -0.185f), new Vector3(90f, 0f, 0f), new Vector3(0.32f, 0.015f, 0.32f), mats.AcVentDark);
            CreateBox("BracketL", ac.transform, new Vector3(-0.24f, -0.26f, 0.04f), Vector3.zero, new Vector3(0.04f, 0.08f, 0.34f), mats.DarkAluminum);
            CreateBox("BracketR", ac.transform, new Vector3(0.24f, -0.26f, 0.04f), Vector3.zero, new Vector3(0.04f, 0.08f, 0.34f), mats.DarkAluminum);
        }

        private static void BuildStaticTrashBin(string name, Transform parent, Vector3 basePos, UnDFMaterials mats)
        {
            var bin = new GameObject(name);
            bin.transform.SetParent(parent, false);
            bin.transform.localPosition = basePos;
            CreateCyl("Body", bin.transform, new Vector3(0f, 0.34f, 0f), Vector3.zero, new Vector3(0.38f, 0.34f, 0.38f), mats.TrashBinGray);
            CreateCyl("Rim", bin.transform, new Vector3(0f, 0.68f, 0f), Vector3.zero, new Vector3(0.42f, 0.035f, 0.42f), mats.DarkAluminum);
        }

        private static void BuildStaticMetalBarrier(string name, Transform parent, Vector3 basePos, UnDFMaterials mats)
        {
            var bar = new GameObject(name);
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = basePos;
            CreateBox("Top", bar.transform, new Vector3(0f, 0.92f, 0f), Vector3.zero, new Vector3(1.85f, 0.05f, 0.05f), mats.SteelSilver);
            CreateBox("Bot", bar.transform, new Vector3(0f, 0.22f, 0f), Vector3.zero, new Vector3(1.85f, 0.045f, 0.045f), mats.SteelSilver);
            CreateBox("LegL", bar.transform, new Vector3(-0.90f, 0.46f, 0f), Vector3.zero, new Vector3(0.05f, 0.92f, 0.05f), mats.SteelSilver);
            CreateBox("LegR", bar.transform, new Vector3(0.90f, 0.46f, 0f), Vector3.zero, new Vector3(0.05f, 0.92f, 0.05f), mats.SteelSilver);
            for (int i = 0; i < 7; i++)
            {
                float bx = Mathf.Lerp(-0.70f, 0.70f, i / 6f);
                CreateBox($"Bar_{i}", bar.transform, new Vector3(bx, 0.57f, 0f), Vector3.zero, new Vector3(0.03f, 0.70f, 0.03f), mats.SteelSilver);
            }
        }

        private static void BuildCampusTree(string name, Transform parent, Vector3 basePos, float scale, UnDFMaterials mats)
        {
            var tree = new GameObject(name);
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = basePos;
            tree.transform.localScale = Vector3.one * scale;

            CreateCyl("Trunk", tree.transform, new Vector3(0f, 1.95f, 0f), new Vector3(0f, 0f, 2.5f), new Vector3(0.42f, 1.95f, 0.42f), mats.TreeBark);
            CreateCyl("BranchL", tree.transform, new Vector3(-0.55f, 3.35f, 0f), new Vector3(0f, 0f, 32f), new Vector3(0.22f, 0.95f, 0.22f), mats.TreeBark);
            CreateCyl("BranchR", tree.transform, new Vector3(0.62f, 3.45f, 0.10f), new Vector3(0f, 0f, -28f), new Vector3(0.22f, 1.00f, 0.22f), mats.TreeBark);

            // Lush multi-cluster golden-green crown matching UnDF concept art
            CreateSphere("Canopy_Center", tree.transform, new Vector3(0f, 4.95f, 0f), Vector3.zero, new Vector3(3.40f, 2.75f, 3.00f), mats.TreeLeafGreen);
            CreateSphere("Canopy_Left", tree.transform, new Vector3(-1.45f, 4.35f, -0.25f), Vector3.zero, new Vector3(2.35f, 1.95f, 2.15f), mats.TreeLeafGold);
            CreateSphere("Canopy_Right", tree.transform, new Vector3(1.50f, 4.55f, 0.20f), Vector3.zero, new Vector3(2.45f, 2.10f, 2.25f), mats.TreeLeafGreen);
            CreateSphere("Canopy_Top", tree.transform, new Vector3(0.25f, 6.05f, -0.10f), Vector3.zero, new Vector3(2.55f, 1.85f, 2.30f), mats.TreeLeafGold);
            CreateSphere("Canopy_Front", tree.transform, new Vector3(-0.35f, 4.25f, -0.95f), Vector3.zero, new Vector3(2.05f, 1.65f, 1.75f), mats.TreeLeafGold);
        }

        // ==================== PROCEDURAL PNG TEXTURE GENERATORS ====================

        private static void EnsureProceduralTextures()
        {
            if (!AssetDatabase.IsValidFolder(UnDFTexturesFolder))
            {
                Directory.CreateDirectory(UnDFTexturesFolder);
                AssetDatabase.Refresh();
            }

            GenerateFacadeLogoTexture(FacadeLogoTexPath);
            GenerateFlagDFTexture(FlagDFTexPath);
            GenerateFlagBrazilTexture(FlagBrazilTexPath);
            GenerateFlagUnDFTexture(FlagUnDFTexPath);
            GenerateNoParkingTexture(SignNoParkingTexPath);
            GenerateAsphaltTexture(AsphaltTexPath);
            AssetDatabase.Refresh();
        }

        private static void GenerateFacadeLogoTexture(string assetPath)
        {
            int w = 1024;
            int h = 640;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color creamTop = new Color(0.97f, 0.94f, 0.89f, 1f);
            Color creamBot = new Color(0.92f, 0.89f, 0.84f, 1f);

            // Base cream wall gradient with subtle architectural plaster grain
            for (int y = 0; y < h; y++)
            {
                float fy = y / (float)(h - 1);
                Color rowCol = Color.Lerp(creamBot, creamTop, fy);
                for (int x = 0; x < w; x++)
                {
                    float n = (Mathf.PerlinNoise(x * 0.04f, y * 0.04f) - 0.5f) * 0.025f;
                    tex.SetPixel(x, y, new Color(
                        Mathf.Clamp01(rowCol.r + n),
                        Mathf.Clamp01(rowCol.g + n),
                        Mathf.Clamp01(rowCol.b + n),
                        1f));
                }
            }

            // Left edge geometric interlocking lilac diamond outlines (faithful to concept art)
            Color lilacLine = new Color(0.67f, 0.54f, 0.76f, 1f);
            for (int row = 0; row < 6; row++)
            {
                DrawDiamondOutline(tex, 42, 70 + row * 100, 62, 56, 3.5f, lilacLine);
                DrawDiamondOutline(tex, 96, 120 + row * 100, 56, 52, 3.0f, new Color(0.75f, 0.64f, 0.83f, 1f));
            }

            // Central 3 overlapping UnDF diamonds (positioned clearly below top HUD timer)
            int cy = 408;
            int rX = 96;
            int rY = 86;
            int cx1 = 436;
            int cx2 = 514;
            int cx3 = 592;

            Color cRight = new Color(0.89f, 0.85f, 0.93f, 1f);
            Color cMid = new Color(0.78f, 0.69f, 0.85f, 1f);
            Color cLeft = new Color(0.60f, 0.46f, 0.72f, 1f);
            Color cOverlap12 = new Color(0.45f, 0.21f, 0.42f, 1f);
            Color cOverlap23 = new Color(0.72f, 0.62f, 0.81f, 1f);

            for (int y = cy - rY - 2; y <= cy + rY + 2; y++)
            {
                if (y < 0 || y >= h) continue;
                for (int x = cx1 - rX - 2; x <= cx3 + rX + 2; x++)
                {
                    if (x < 0 || x >= w) continue;
                    bool in1 = InsideDiamond(x, y, cx1, cy, rX, rY);
                    bool in2 = InsideDiamond(x, y, cx2, cy, rX * 0.92f, rY * 0.92f);
                    bool in3 = InsideDiamond(x, y, cx3, cy, rX * 0.84f, rY * 0.84f);

                    if (in1 && in2) tex.SetPixel(x, y, cOverlap12);
                    else if (in2 && in3) tex.SetPixel(x, y, cOverlap23);
                    else if (in1) tex.SetPixel(x, y, cLeft);
                    else if (in2) tex.SetPixel(x, y, cMid);
                    else if (in3) tex.SetPixel(x, y, cRight);
                }
            }

            // Bottom-right GDF institutional blue badge + golden Ipê emblem
            int bx0 = 732, bx1 = 948, by0 = 24, by1 = 104;
            Color gdfBlue = new Color(0.11f, 0.34f, 0.64f, 1f);
            Color ipeGold = new Color(0.92f, 0.74f, 0.18f, 1f);
            Color ipeTrunk = new Color(0.55f, 0.40f, 0.16f, 1f);
            FillRect(tex, bx0, by0, bx1, by1, gdfBlue);
            FillCircle(tex, bx0 + 44, by0 + 48, 20, ipeGold);
            FillCircle(tex, bx0 + 34, by0 + 43, 14, ipeGold);
            FillCircle(tex, bx0 + 54, by0 + 43, 14, ipeGold);
            FillRect(tex, bx0 + 41, by0 + 14, bx0 + 47, by0 + 38, ipeTrunk);

            // Thin dark architectural border
            Color border = new Color(0.24f, 0.19f, 0.17f, 1f);
            FillRect(tex, 0, 0, w, 8, border);
            FillRect(tex, 0, h - 8, w, h, border);
            FillRect(tex, 0, 0, 8, h, border);
            FillRect(tex, w - 8, 0, w, h, border);

            tex.Apply();
            SaveTexturePng(tex, assetPath, wrapRepeat: false);
            Object.DestroyImmediate(tex);
        }

        private static void GenerateFlagDFTexture(string assetPath)
        {
            int w = 256, h = 160;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            FillRect(tex, 0, 0, w, h, new Color(0.96f, 0.96f, 0.96f, 1f));
            // Central green square + yellow Brasília Cross
            FillRect(tex, 74, 26, 182, 134, new Color(0.06f, 0.48f, 0.22f, 1f));
            Color yellow = new Color(0.97f, 0.80f, 0.12f, 1f);
            FillRect(tex, 118, 42, 138, 118, yellow);
            FillRect(tex, 90, 70, 166, 90, yellow);
            tex.Apply();
            SaveTexturePng(tex, assetPath, wrapRepeat: false);
            Object.DestroyImmediate(tex);
        }

        private static void GenerateFlagBrazilTexture(string assetPath)
        {
            int w = 256, h = 160;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            FillRect(tex, 0, 0, w, h, new Color(0.00f, 0.58f, 0.23f, 1f));
            Color yellow = new Color(0.99f, 0.86f, 0.04f, 1f);
            Color blue = new Color(0.02f, 0.16f, 0.48f, 1f);
            Color white = new Color(0.97f, 0.97f, 0.97f, 1f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (InsideDiamond(x, y, w / 2, h / 2, 102, 62))
                    {
                        tex.SetPixel(x, y, yellow);
                    }
                }
            }
            FillCircle(tex, w / 2, h / 2, 34, blue);
            for (int x = w / 2 - 33; x <= w / 2 + 33; x++)
            {
                float dx = (x - w / 2f) / 34f;
                int bandY = Mathf.RoundToInt(h / 2f + 4f - dx * 9f);
                for (int dy = -3; dy <= 3; dy++)
                {
                    int py = bandY + dy;
                    if ((x - w / 2) * (x - w / 2) + (py - h / 2) * (py - h / 2) <= 33 * 33)
                    {
                        tex.SetPixel(x, py, white);
                    }
                }
            }
            tex.Apply();
            SaveTexturePng(tex, assetPath, wrapRepeat: false);
            Object.DestroyImmediate(tex);
        }

        private static void GenerateFlagUnDFTexture(string assetPath)
        {
            int w = 256, h = 160;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            FillRect(tex, 0, 0, w, h, new Color(0.96f, 0.95f, 0.96f, 1f));
            Color leftP = new Color(0.56f, 0.36f, 0.68f, 1f);
            Color midP = new Color(0.75f, 0.63f, 0.84f, 1f);
            Color ovP = new Color(0.42f, 0.18f, 0.40f, 1f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool in1 = InsideDiamond(x, y, 108, 80, 44, 42);
                    bool in2 = InsideDiamond(x, y, 146, 80, 40, 38);
                    if (in1 && in2) tex.SetPixel(x, y, ovP);
                    else if (in1) tex.SetPixel(x, y, leftP);
                    else if (in2) tex.SetPixel(x, y, midP);
                }
            }
            tex.Apply();
            SaveTexturePng(tex, assetPath, wrapRepeat: false);
            Object.DestroyImmediate(tex);
        }

        private static void GenerateNoParkingTexture(string assetPath)
        {
            int s = 256;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            Color bg = new Color(0.94f, 0.94f, 0.92f, 1f);
            Color red = new Color(0.84f, 0.12f, 0.12f, 1f);
            Color black = new Color(0.10f, 0.10f, 0.12f, 1f);
            FillRect(tex, 0, 0, s, s, bg);

            // Outer red ring
            int cx = s / 2, cy = s / 2;
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d <= 116f && d >= 90f)
                    {
                        tex.SetPixel(x, y, red);
                    }
                }
            }

            // Bold letter "E"
            FillRect(tex, 92, 68, 114, 188, black);
            FillRect(tex, 92, 168, 166, 188, black);
            FillRect(tex, 92, 118, 156, 138, black);
            FillRect(tex, 92, 68, 166, 88, black);

            // Red diagonal slash across the circle
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d <= 104f && Mathf.Abs((x - cx) + (y - cy)) <= 13f)
                    {
                        tex.SetPixel(x, y, red);
                    }
                }
            }

            tex.Apply();
            SaveTexturePng(tex, assetPath, wrapRepeat: false);
            Object.DestroyImmediate(tex);
        }

        private static void GenerateAsphaltTexture(string assetPath)
        {
            int s = 512;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float n1 = Mathf.PerlinNoise(x * 0.045f, y * 0.045f);
                    float n2 = Mathf.PerlinNoise(x * 0.22f + 13.7f, y * 0.22f + 7.1f);
                    float grain = (n1 * 0.65f + n2 * 0.35f) * 0.14f;
                    float v = 0.22f + grain;
                    tex.SetPixel(x, y, new Color(v, v * 0.99f, v * 1.02f, 1f));
                }
            }
            tex.Apply();
            SaveTexturePng(tex, assetPath, wrapRepeat: true);
            Object.DestroyImmediate(tex);
        }

        private static void SaveTexturePng(Texture2D tex, string assetPath, bool wrapRepeat)
        {
            string fullPath = Path.GetFullPath(assetPath);
            string dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = wrapRepeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }
        }

        private static bool InsideDiamond(int x, int y, float cx, float cy, float rx, float ry)
        {
            return (Mathf.Abs(x - cx) / rx + Mathf.Abs(y - cy) / ry) <= 1.0f;
        }

        private static void DrawDiamondOutline(Texture2D tex, int cx, int cy, float rx, float ry, float thickness, Color col)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - rx - thickness));
            int x1 = Mathf.Min(tex.width - 1, Mathf.CeilToInt(cx + rx + thickness));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - ry - thickness));
            int y1 = Mathf.Min(tex.height - 1, Mathf.CeilToInt(cy + ry + thickness));
            float outer = 1f + thickness / Mathf.Min(rx, ry);
            float inner = 1f - thickness / Mathf.Min(rx, ry);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float d = Mathf.Abs(x - cx) / rx + Mathf.Abs(y - cy) / ry;
                    if (d <= outer && d >= inner)
                    {
                        tex.SetPixel(x, y, col);
                    }
                }
            }
        }

        private static void FillRect(Texture2D tex, int x0, int y0, int x1, int y1, Color col)
        {
            int sx = Mathf.Clamp(x0, 0, tex.width);
            int ex = Mathf.Clamp(x1, 0, tex.width);
            int sy = Mathf.Clamp(y0, 0, tex.height);
            int ey = Mathf.Clamp(y1, 0, tex.height);
            for (int y = sy; y < ey; y++)
            {
                for (int x = sx; x < ex; x++)
                {
                    tex.SetPixel(x, y, col);
                }
            }
        }

        private static void FillCircle(Texture2D tex, int cx, int cy, int r, Color col)
        {
            int r2 = r * r;
            for (int y = Mathf.Max(0, cy - r); y <= Mathf.Min(tex.height - 1, cy + r); y++)
            {
                for (int x = Mathf.Max(0, cx - r); x <= Mathf.Min(tex.width - 1, cx + r); x++)
                {
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r2)
                    {
                        tex.SetPixel(x, y, col);
                    }
                }
            }
        }

        // ==================== URP MATERIAL ASSET FACTORY ====================

        private static UnDFMaterials EnsureMaterials()
        {
            if (!AssetDatabase.IsValidFolder(UnDFMaterialsFolder))
            {
                Directory.CreateDirectory(UnDFMaterialsFolder);
                AssetDatabase.Refresh();
            }

            Texture2D asphaltTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AsphaltTexPath);
            Texture2D facadeTex = AssetDatabase.LoadAssetAtPath<Texture2D>(FacadeLogoTexPath);
            Texture2D flagDFTex = AssetDatabase.LoadAssetAtPath<Texture2D>(FlagDFTexPath);
            Texture2D flagBrTex = AssetDatabase.LoadAssetAtPath<Texture2D>(FlagBrazilTexPath);
            Texture2D flagUnDFTex = AssetDatabase.LoadAssetAtPath<Texture2D>(FlagUnDFTexPath);
            Texture2D noParkTex = AssetDatabase.LoadAssetAtPath<Texture2D>(SignNoParkingTexPath);

            Material asphalt = EnsureTexturedLitMaterial("UnDF_Asphalt.mat", asphaltTex, new Color(0.88f, 0.88f, 0.90f, 1f), metallic: 0.04f, smoothness: 0.24f, tiling: new Vector2(18f, 6f));
            Material facadeLogo = EnsureTexturedLitMaterial("UnDF_FacadeLogo.mat", facadeTex, Color.white, metallic: 0f, smoothness: 0.16f, tiling: Vector2.one);
            Material flagDF = EnsureTexturedLitMaterial("UnDF_FlagDF.mat", flagDFTex, Color.white, metallic: 0f, smoothness: 0.12f, tiling: Vector2.one);
            Material flagBrazil = EnsureTexturedLitMaterial("UnDF_FlagBrazil.mat", flagBrTex, Color.white, metallic: 0f, smoothness: 0.12f, tiling: Vector2.one);
            Material flagUnDF = EnsureTexturedLitMaterial("UnDF_FlagUnDF.mat", flagUnDFTex, Color.white, metallic: 0f, smoothness: 0.12f, tiling: Vector2.one);
            Material signNoPark = EnsureTexturedLitMaterial("UnDF_SignNoParking.mat", noParkTex, Color.white, metallic: 0.10f, smoothness: 0.40f, tiling: Vector2.one);

            return new UnDFMaterials
            {
                Asphalt = asphalt,
                ConcreteWall = EnsureLitMaterial("UnDF_ConcreteWall.mat", new Color(0.86f, 0.83f, 0.78f, 1f), metallic: 0f, smoothness: 0.14f),
                ConcreteSidewalk = EnsureLitMaterial("UnDF_ConcreteSidewalk.mat", new Color(0.68f, 0.66f, 0.63f, 1f), metallic: 0f, smoothness: 0.18f),
                CurbYellow = EnsureLitMaterial("UnDF_CurbYellow.mat", new Color(0.88f, 0.70f, 0.16f, 1f), metallic: 0f, smoothness: 0.22f),
                PaintYellow = EnsureLitMaterial("UnDF_PaintYellow.mat", new Color(0.95f, 0.78f, 0.16f, 1f), metallic: 0f, smoothness: 0.25f),
                TireMark = EnsureLitMaterial("UnDF_TireMark.mat", new Color(0.14f, 0.14f, 0.15f, 1f), metallic: 0f, smoothness: 0.15f),
                DarkAluminum = EnsureLitMaterial("UnDF_DarkAluminum.mat", new Color(0.16f, 0.17f, 0.20f, 1f), metallic: 0.60f, smoothness: 0.38f),
                SteelSilver = EnsureLitMaterial("UnDF_SteelSilver.mat", new Color(0.66f, 0.69f, 0.72f, 1f), metallic: 0.72f, smoothness: 0.52f),
                GlassDark = EnsureLitMaterial("UnDF_GlassDark.mat", new Color(0.12f, 0.16f, 0.22f, 1f), metallic: 0.28f, smoothness: 0.86f),
                MarqueeGlass = EnsureTransparentLitMaterial("UnDF_MarqueeGlass.mat", new Color(0.76f, 0.84f, 0.90f, 0.42f), metallic: 0.10f, smoothness: 0.82f),
                FacadeLogo = facadeLogo,
                FacadeTrim = EnsureLitMaterial("UnDF_FacadeTrim.mat", new Color(0.28f, 0.22f, 0.20f, 1f), metallic: 0.25f, smoothness: 0.30f),
                LobbyInterior = EnsureLitMaterial("UnDF_LobbyInterior.mat", new Color(0.84f, 0.78f, 0.64f, 1f), metallic: 0f, smoothness: 0.20f),
                LobbyRedStair = EnsureLitMaterial("UnDF_LobbyRedStair.mat", new Color(0.78f, 0.22f, 0.14f, 1f), metallic: 0f, smoothness: 0.32f),
                AcUnitWhite = EnsureLitMaterial("UnDF_AcUnitWhite.mat", new Color(0.90f, 0.89f, 0.86f, 1f), metallic: 0.10f, smoothness: 0.32f),
                AcVentDark = EnsureLitMaterial("UnDF_AcVentDark.mat", new Color(0.18f, 0.19f, 0.20f, 1f), metallic: 0.20f, smoothness: 0.25f),
                FlyerPaper = EnsureLitMaterial("UnDF_FlyerPaper.mat", new Color(0.94f, 0.93f, 0.89f, 1f), metallic: 0f, smoothness: 0.12f),
                TrashBinGray = EnsureLitMaterial("UnDF_TrashBinGray.mat", new Color(0.46f, 0.48f, 0.47f, 1f), metallic: 0.08f, smoothness: 0.30f),
                TreeBark = EnsureLitMaterial("UnDF_TreeBark.mat", new Color(0.32f, 0.22f, 0.14f, 1f), metallic: 0f, smoothness: 0.10f),
                TreeLeafGreen = EnsureLitMaterial("UnDF_TreeLeafGreen.mat", new Color(0.24f, 0.44f, 0.16f, 1f), metallic: 0f, smoothness: 0.16f),
                TreeLeafGold = EnsureLitMaterial("UnDF_TreeLeafGold.mat", new Color(0.48f, 0.56f, 0.18f, 1f), metallic: 0f, smoothness: 0.18f),
                GrassBed = EnsureLitMaterial("UnDF_GrassBed.mat", new Color(0.26f, 0.42f, 0.18f, 1f), metallic: 0f, smoothness: 0.12f),
                FlagDF = flagDF,
                FlagBrazil = flagBrazil,
                FlagUnDF = flagUnDF,
                SignNoParking = signNoPark,
                BannerPurple = EnsureLitMaterial("UnDF_BannerPurple.mat", new Color(0.44f, 0.22f, 0.56f, 1f), metallic: 0f, smoothness: 0.24f),
                ConeOrange = EnsureLitMaterial("UnDF_ConeOrange.mat", new Color(0.96f, 0.36f, 0.06f, 1f), metallic: 0f, smoothness: 0.45f),
                ConeStripeWhite = EnsureLitMaterial("UnDF_ConeStripeWhite.mat", new Color(0.96f, 0.96f, 0.96f, 1f), metallic: 0f, smoothness: 0.65f),
                NeighborWallA = EnsureLitMaterial("UnDF_NeighborWallA.mat", new Color(0.76f, 0.75f, 0.72f, 1f), metallic: 0f, smoothness: 0.14f),
                NeighborWallB = EnsureLitMaterial("UnDF_NeighborWallB.mat", new Color(0.68f, 0.70f, 0.73f, 1f), metallic: 0f, smoothness: 0.14f),
                LampGlow = EnsureEmissiveLitMaterial("UnDF_LampGlow.mat", new Color(1.0f, 0.92f, 0.70f, 1f), new Color(1.0f, 0.84f, 0.46f, 1f) * 2.6f, smoothness: 0.35f),
                NightWindowWarm = EnsureEmissiveLitMaterial("UnDF_NightWindowWarm.mat", new Color(1.0f, 0.84f, 0.48f, 1f), new Color(1.0f, 0.72f, 0.28f, 1f) * 1.65f, smoothness: 0.25f),
                NightWindowCool = EnsureEmissiveLitMaterial("UnDF_NightWindowCool.mat", new Color(0.84f, 0.92f, 1.0f, 1f), new Color(0.72f, 0.86f, 1.0f, 1f) * 1.55f, smoothness: 0.25f),
                SunGlow = EnsureUnlitMaterial("UnDF_SunGlow.mat", new Color(1.0f, 0.92f, 0.72f, 1f)),
                MoonGlow = EnsureUnlitMaterial("UnDF_MoonGlow.mat", new Color(0.92f, 0.95f, 1.0f, 1f)),
                StarGlow = EnsureUnlitMaterial("UnDF_StarGlow.mat", new Color(0.88f, 0.92f, 1.0f, 1f)),
                RainPuddle = EnsureTransparentLitMaterial("UnDF_RainPuddle.mat", new Color(0.025f, 0.04f, 0.07f, 0.42f), metallic: 0.38f, smoothness: 0.95f),
                SkyboxDay = EnsureProceduralSkybox("UnDF_DaySkybox.mat"),
                Clouds = StageFramingStandards.EnsureStandardCloudMaterials(UnDFMaterialsFolder)
            };
        }

        private static Material EnsureProceduralSkybox(string fileName)
        {
            string path = $"{UnDFMaterialsFolder}/{fileName}";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Skybox/Procedural");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }
            if (mat.HasProperty("_SkyTint")) mat.SetColor("_SkyTint", new Color(0.52f, 0.74f, 0.96f, 1f));
            if (mat.HasProperty("_GroundColor")) mat.SetColor("_GroundColor", new Color(0.46f, 0.44f, 0.40f, 1f));
            if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", 1.25f);
            if (mat.HasProperty("_AtmosphereThickness")) mat.SetFloat("_AtmosphereThickness", 0.95f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EnsureLitMaterial(string fileName, Color baseColor, float metallic, float smoothness)
        {
            string path = $"{UnDFMaterialsFolder}/{fileName}";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EnsureTexturedLitMaterial(string fileName, Texture2D tex, Color tint, float metallic, float smoothness, Vector2 tiling)
        {
            Material mat = EnsureLitMaterial(fileName, tint, metallic, smoothness);
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", tex);
                    mat.SetTextureScale("_BaseMap", tiling);
                }
                if (mat.HasProperty("_MainTex"))
                {
                    mat.SetTexture("_MainTex", tex);
                    mat.SetTextureScale("_MainTex", tiling);
                }
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EnsureEmissiveLitMaterial(string fileName, Color baseColor, Color emissionHdr, float smoothness)
        {
            Material mat = EnsureLitMaterial(fileName, baseColor, metallic: 0f, smoothness: smoothness);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionHdr);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EnsureTransparentLitMaterial(string fileName, Color baseColor, float metallic, float smoothness)
        {
            Material mat = EnsureLitMaterial(fileName, baseColor, metallic, smoothness);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EnsureUnlitMaterial(string fileName, Color baseColor)
        {
            string path = $"{UnDFMaterialsFolder}/{fileName}";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ==================== PRIMITIVE & UTILITY HELPERS ====================

        private static Light CreateDirectionalLight(string name, Transform parent, Vector3 euler, Color color, float intensity, LightShadows shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(euler);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = color;
            l.intensity = intensity;
            l.shadows = shadows;
            return l;
        }

        private static void CreatePointLight(string name, Transform parent, Vector3 worldPos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
        }

        private static void CreateWorldTmp(
            string name,
            Transform parent,
            string text,
            Vector3 localPos,
            float fontSize,
            Color color,
            FontStyles style,
            float charSpacing = 0f,
            TextAlignmentOptions align = TextAlignmentOptions.Center,
            float boxWidth = 10f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = align;
            tmp.characterSpacing = charSpacing;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            var rt = tmp.rectTransform;
            rt.sizeDelta = new Vector2(boxWidth, 2f);
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat)
        {
            return CreatePrimitive(name, PrimitiveType.Cube, parent, localPos, localEuler, localScale, mat);
        }

        private static GameObject CreateNoShadowBox(string name, Transform parent, Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat)
        {
            GameObject go = CreatePrimitive(name, PrimitiveType.Cube, parent, localPos, localEuler, localScale, mat);
            DisableShadows(go);
            return go;
        }

        private static GameObject CreateCyl(string name, Transform parent, Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat)
        {
            return CreatePrimitive(name, PrimitiveType.Cylinder, parent, localPos, localEuler, localScale, mat);
        }

        private static GameObject CreateSphere(string name, Transform parent, Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat)
        {
            return CreatePrimitive(name, PrimitiveType.Sphere, parent, localPos, localEuler, localScale, mat);
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;

            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            if (mat != null)
            {
                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = mat;
            }
            return go;
        }

        private static void DisableShadows(GameObject go)
        {
            if (go == null) return;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void InstantiateProp(GameObject prefab, string instanceName, Transform parent, Vector3 pos, float rotY)
        {
            if (prefab == null) return;
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.name = instanceName;
            inst.transform.localPosition = pos;
            inst.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            inst.transform.localScale = Vector3.one;
        }

        private static void AssignObjectArray<T>(SerializedProperty arrayProp, List<T> items) where T : Object
        {
            if (arrayProp == null) return;
            arrayProp.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
            {
                arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
        }

        private static GameObject FindInSceneByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindRecursive(root.transform, name);
                if (found != null) return found.gameObject;
            }
            return null;
        }

        private static Transform FindRecursive(Transform current, string name)
        {
            if (current.name == name) return current;
            for (int i = 0; i < current.childCount; i++)
            {
                Transform res = FindRecursive(current.GetChild(i), name);
                if (res != null) return res;
            }
            return null;
        }

        private static void DestroyAllMatchingInScene(Scene scene, string exactName)
        {
            var toDestroy = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                CollectMatching(root.transform, exactName, toDestroy);
            }
            for (int i = 0; i < toDestroy.Count; i++)
            {
                if (toDestroy[i] != null) Object.DestroyImmediate(toDestroy[i]);
            }
        }

        private static void CollectMatching(Transform current, string exactName, List<GameObject> results)
        {
            if (current.name == exactName)
            {
                results.Add(current.gameObject);
                return;
            }
            for (int i = 0; i < current.childCount; i++)
            {
                CollectMatching(current.GetChild(i), exactName, results);
            }
        }
    }
}
