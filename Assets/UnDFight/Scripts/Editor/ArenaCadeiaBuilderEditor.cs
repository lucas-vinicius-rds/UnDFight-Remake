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
    /// Idempotent Editor builder for Phase 15.4: creates/rebuilds Arena_Cadeia.unity (Bloco de Celas),
    /// an indoor prison cellblock with fixed dramatic cold-blue skylight lighting (supportsWeatherVariants = false),
    /// full Physics v2 (HingeJoint hanging fluorescent lamps, persistent breakables, MK throwables, dynamic cups),
    /// and strict StageFramingStandards floor coverage (z <= -22.5m, zero black bar).
    /// </summary>
    public static class ArenaCadeiaBuilderEditor
    {
        public const string ConceptSourcePath = "Docs/Referencias/Celas de Prisão Fight Arena Concept.jpg";
        public const string PreviewTexturePath = "Assets/UnDFight/Art/Textures/Stage_Cadeia_Preview.jpg";
        public const string CadeiaTexturesFolder = "Assets/UnDFight/Art/Textures/Cadeia";
        public const string CadeiaMaterialsFolder = "Assets/UnDFight/Art/Materials/Cadeia";
        public const string StagePropsPrefabFolder = StagePhysicsSetupEditor.StagePropsPrefabFolder;

        public const string ArenaCadeiaScenePath = "Assets/UnDFight/Scenes/Arena_Cadeia.unity";
        public const string CadeiaStageDataPath = "Assets/UnDFight/Data/StageData_Cadeia.asset";
        public const string StageRosterPath = ArenaUnDFBuilderEditor.StageRosterPath;

        public const string HangingLampPrefabPath = StagePropsPrefabFolder + "/Prop_Cadeia_HangingLamp.prefab";
        public const string BreakableBenchPrefabPath = StagePropsPrefabFolder + "/Prop_Cadeia_BreakableBench.prefab";
        public const string BreakableLockerPrefabPath = StagePropsPrefabFolder + "/Prop_Cadeia_BreakableLocker.prefab";
        public const string ThrowableStoolPrefabPath = StagePropsPrefabFolder + "/Prop_Cadeia_ThrowableStool.prefab";
        public const string ThrowableBucketPrefabPath = StagePropsPrefabFolder + "/Prop_Cadeia_ThrowableBucket.prefab";
        public const string DynamicCupPrefabPath = StagePropsPrefabFolder + "/Prop_Cadeia_DynamicCup.prefab";

        public const string FloorTexPath = CadeiaTexturesFolder + "/Tex_Cadeia_ConcreteFloor.png";
        public const string WallTexPath = CadeiaTexturesFolder + "/Tex_Cadeia_ConcreteWall.png";
        public const string RustTexPath = CadeiaTexturesFolder + "/Tex_Cadeia_RustMetal.png";
        public const string GraffitiTexPath = CadeiaTexturesFolder + "/Tex_Cadeia_Graffiti.png";

        public const string EnvironmentRootName = "CadeiaEnvironment";

        private struct CadeiaMaterials
        {
            public Material ConcreteFloor;
            public Material ConcreteWall;
            public Material ConcreteDark;
            public Material CellInteriorWall;
            public Material CellInteriorDark;
            public Material IronBars;
            public Material RustMetal;
            public Material SteelLight;
            public Material SteelDark;
            public Material Mattress;
            public Material Pillow;
            public Material LampFixture;
            public Material FluorescentGlow;
            public Material SkylightGlow;
            public Material SkylightBeam;
            public Material WetPuddle;
            public Material FloorJoint;
            public Material GraffitiDecal;
            public Material CableBlack;
            public Material NumberPlate;
            public Material SkyboxDarkIndoor;
        }

        [MenuItem("UnDFight/Stages/Build Arena_Cadeia", false, 123)]
        public static void BuildArenaCadeia()
        {
            StagePhysicsSetupEditor.EnsurePhysicsLayersAndMatrix();

            EnsurePreviewTexture();
            EnsureProceduralTextures();
            CadeiaMaterials mats = EnsureMaterials();
            CreateOrUpdateCadeiaPropPrefabs(mats);
            StageData cadeiaData = EnsureStageDataAndRoster();

            string previousScenePath = SceneManager.GetActiveScene().path;

            // Open Arena_Favela as our verified combat/HUD/camera/bootstrap template and save as Arena_Cadeia
            Scene scene = EditorSceneManager.OpenScene(StagePhysicsSetupEditor.ArenaFavelaScenePath, OpenSceneMode.Single);
            StripSourceStageObjects(scene);
            BuildCadeiaSceneContents(scene, cadeiaData, mats);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ArenaCadeiaScenePath);
            EnsureSceneInBuildSettings(ArenaCadeiaScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!string.IsNullOrEmpty(previousScenePath) &&
                previousScenePath != ArenaCadeiaScenePath &&
                File.Exists(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
            else
            {
                EditorSceneManager.OpenScene(PlayFromMainMenuEditor.MainMenuScenePath, OpenSceneMode.Single);
            }

            Debug.Log("[UnDFight] Fase 15.4 complete: Arena_Cadeia.unity built with 2-story prison cellblock, skylight beam, 4 HingeJoint fluorescent lamps, breakables/throwables/dynamic cups, fixed dramatic indoor lighting (supportsWeatherVariants=false), and StageFramingStandards floor coverage (z <= -22.5m).");
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
                // Always COPY (never move or delete the original concept art)
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
            StageData cadeia = AssetDatabase.LoadAssetAtPath<StageData>(CadeiaStageDataPath);
            if (cadeia == null)
            {
                cadeia = ScriptableObject.CreateInstance<StageData>();
                AssetDatabase.CreateAsset(cadeia, CadeiaStageDataPath);
            }

            Sprite previewSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PreviewTexturePath);
            cadeia.stageId = "bloco_cadeia";
            cadeia.displayName = "BLOCO DE CELAS";
            cadeia.subtitle = "Penitenciária • Pavilhão de Segurança Máxima";
            cadeia.sceneName = "Arena_Cadeia";
            cadeia.previewImage = previewSprite;
            cadeia.previewColor = new Color(0.18f, 0.24f, 0.30f, 1f);
            cadeia.accentColor = new Color(0.56f, 0.78f, 0.92f, 1f);
            cadeia.supportsWeatherVariants = false;
            cadeia.dayVariantLabel = "INTERIOR";
            cadeia.defaultWeather = StageWeatherVariant.Day;
            EditorUtility.SetDirty(cadeia);

            // Register 4 arenas in order: Arena_Favela, Arena_UnDF, Arena_Cadeia, Training
            StageRoster roster = AssetDatabase.LoadAssetAtPath<StageRoster>(StageRosterPath);
            if (roster != null)
            {
                StageData favela = AssetDatabase.LoadAssetAtPath<StageData>(StagePhysicsSetupEditor.FavelaStageDataPath);
                StageData undf = AssetDatabase.LoadAssetAtPath<StageData>(ArenaUnDFBuilderEditor.UnDFStageDataPath);
                StageData training = AssetDatabase.LoadAssetAtPath<StageData>(StagePhysicsSetupEditor.TrainingStageDataPath);

                var ordered = new List<StageData>();
                if (favela != null) ordered.Add(favela);
                if (undf != null) ordered.Add(undf);
                ordered.Add(cadeia);
                if (training != null) ordered.Add(training);

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

            return cadeia;
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
                int undfIdx = scenes.FindIndex(s => s.path == ArenaUnDFBuilderEditor.ArenaUnDFScenePath);
                var entry = new EditorBuildSettingsScene(scenePath, true);
                if (undfIdx >= 0) scenes.Insert(undfIdx + 1, entry);
                else scenes.Add(entry);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ==================== 2. SCENE CONSTRUCTION ====================

        private static void StripSourceStageObjects(Scene scene)
        {
            GameObject existingVol = FindInSceneByName(scene, "Global Volume");
            if (existingVol != null)
            {
                existingVol.transform.SetParent(null, worldPositionStays: true);
            }

            string[] removeNames =
            {
                "FavelaEnvironment",
                "FavelaDetails_Phase15_2",
                "FavelaNightLights",
                "FavelaNightLightsAndWindows",
                "FavelaRainPuddles",
                "InteractiveStageProps",
                EnvironmentRootName
            };
            foreach (string name in removeNames)
            {
                DestroyAllMatchingInScene(scene, name);
            }
        }

        private static void BuildCadeiaSceneContents(Scene scene, StageData cadeiaData, CadeiaMaterials mats)
        {
            var envRoot = new GameObject(EnvironmentRootName);

            // Preserve Global Volume under envRoot
            GameObject existingVol = FindInSceneByName(scene, "Global Volume");
            if (existingVol != null)
            {
                existingVol.transform.SetParent(envRoot.transform, worldPositionStays: true);
            }

            // Dramatic indoor lighting:
            // - Main Directional Light angled like the cold-blue skylight (ONLY light casting shadows!)
            // - Calibrated readability fill so dark outfits (Lucas / Ibaneis) stand out crisply on Z=0
            Light skylightKey = CreateDirectionalLight("SkylightKeyLight_Cadeia", envRoot.transform, new Vector3(54f, 152f, 0f), new Color(0.68f, 0.84f, 1.00f, 1f), intensity: 1.55f, shadows: LightShadows.Soft);
            Light ambientRimFill = CreateDirectionalLight("CellblockRimFill_Cadeia", envRoot.transform, new Vector3(28f, 340f, 0f), new Color(0.76f, 0.86f, 0.96f, 1f), intensity: 0.78f, shadows: LightShadows.None);
            Light fighterFrontFill = CreateDirectionalLight("FighterReadabilityFill_Cadeia", envRoot.transform, new Vector3(16f, 18f, 0f), new Color(0.92f, 0.94f, 0.98f, 1f), intensity: 0.65f, shadows: LightShadows.None);

            // Indoor cold fluorescent / skylight point lights (ALL LightShadows.None for 60+ FPS!)
            var indoorLights = new GameObject("Cadeia_IndoorLights");
            indoorLights.transform.SetParent(envRoot.transform, false);
            CreatePointLight("LampLight_0", indoorLights.transform, new Vector3(-6.80f, 4.45f, 1.85f), new Color(0.72f, 0.88f, 1.00f, 1f), intensity: 13.0f, range: 13.5f);
            CreatePointLight("LampLight_1", indoorLights.transform, new Vector3(-2.10f, 4.45f, 1.85f), new Color(0.78f, 0.92f, 1.00f, 1f), intensity: 14.5f, range: 14.0f);
            CreatePointLight("LampLight_2", indoorLights.transform, new Vector3(2.40f, 4.45f, 1.85f), new Color(0.78f, 0.92f, 1.00f, 1f), intensity: 14.5f, range: 14.0f);
            CreatePointLight("LampLight_3", indoorLights.transform, new Vector3(7.00f, 4.45f, 1.85f), new Color(0.72f, 0.88f, 1.00f, 1f), intensity: 13.0f, range: 13.5f);
            CreatePointLight("SkylightShaftLight", indoorLights.transform, new Vector3(-4.20f, 6.80f, 2.20f), new Color(0.56f, 0.78f, 1.00f, 1f), intensity: 18.0f, range: 16.0f);
            CreatePointLight("StairwellSecurityLight", indoorLights.transform, new Vector3(-11.20f, 3.80f, 3.60f), new Color(0.68f, 0.84f, 0.98f, 1f), intensity: 10.5f, range: 12.0f);
            CreatePointLight("RightGalleryLight", indoorLights.transform, new Vector3(13.50f, 4.80f, 2.40f), new Color(0.70f, 0.86f, 0.98f, 1f), intensity: 10.5f, range: 12.0f);

            // Indoor RenderSettings (cold slate-blue fog & ambient)
            RenderSettings.skybox = mats.SkyboxDarkIndoor;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.26f, 0.34f, 0.44f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.24f, 0.30f, 0.38f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.16f, 0.20f, 1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.12f, 0.17f, 0.23f, 1f);
            RenderSettings.fogStartDistance = 12f;
            RenderSettings.fogEndDistance = 42f;

            // Layer A: Industrial Concrete Floor (extended to z <= -22.5m, width 68m) + expansion joints + grates + wet puddles
            BuildLayerAIndustrialFloor(envRoot.transform, mats);

            // Layer C: 2-Story Prison Cellblock, Open Cells with Bunks/Sinks, Industrial Staircase, Catwalk, Pipes & Skylight
            BuildLayerCPrisonCellblock(envRoot.transform, mats);

            // Layer B: Interactive Physics v2 Props (4 HingeJoint hanging lamps, 2 breakables, 2 MK throwables, 4 dynamic cups)
            var propsGroup = new GameObject("InteractiveStageProps");
            InstantiateCadeiaInteractiveProps(propsGroup.transform);

            // Wire StageInteractableManager, StageWindController (subtle indoor draft) & StageWeatherController (fixed indoor weather)
            WireCadeiaManagersAndWeather(
                scene,
                cadeiaData,
                skylightKey,
                new[] { ambientRimFill, fighterFrontFill },
                new[] { ambientRimFill.intensity, fighterFrontFill.intensity });
        }

        // ==================== LAYER A: INDUSTRIAL CONCRETE FLOOR ====================

        private static void BuildLayerAIndustrialFloor(Transform parent, CadeiaMaterials mats)
        {
            var layerA = new GameObject("LayerA_IndustrialFloor");
            layerA.transform.SetParent(parent, false);

            // Combat lane & cellblock corridor floor: Z in [-2.60 .. +10.40], top at exact Y = 0.00, width 68m
            CreateBox("PrisonFloor_Main", layerA.transform, new Vector3(0f, -0.05f, 3.90f), Vector3.zero, new Vector3(68.0f, 0.10f, 13.00f), mats.ConcreteFloor);

            // Foreground floor strip locked cleanly to backEdgeZ = -2.60 -> extends to Z = -22.50 (zero black bar, zero Z-fighting)
            GameObject frontFloor = CreateBox("PrisonFloor_Front", layerA.transform, new Vector3(0f, -0.05f, -4.55f), Vector3.zero, new Vector3(68.0f, 0.10f, 3.90f), mats.ConcreteFloor);
            StageFramingStandards.EnsureForegroundFloorCoverage(frontFloor.transform, backEdgeZ: -2.60f, targetFrontZ: -22.5f, minWidthX: 68.0f);

            // Concrete expansion joints (strictly >= 0.025m above floor or placed cleanly off Z=0 fighter feet lane)
            var details = new GameObject("Floor_Details");
            details.transform.SetParent(layerA.transform, false);
            float[] jointZs = { -3.40f, -1.35f, 1.55f, 3.55f };
            foreach (float jz in jointZs)
            {
                CreateNoShadowBox($"ConcreteJoint_Z_{jz:F1}", details.transform, new Vector3(0f, 0.025f, jz), Vector3.zero, new Vector3(44.0f, 0.003f, 0.045f), mats.FloorJoint);
            }
            for (int ix = -4; ix <= 4; ix++)
            {
                float jx = ix * 4.6f;
                // Split longitudinal joints so they stay outside Z in [-0.7..+0.7]
                CreateNoShadowBox($"ConcreteJoint_XBack_{ix}", details.transform, new Vector3(jx, 0.025f, 2.55f), Vector3.zero, new Vector3(0.04f, 0.003f, 2.60f), mats.FloorJoint);
                CreateNoShadowBox($"ConcreteJoint_XFront_{ix}", details.transform, new Vector3(jx, 0.025f, -2.55f), Vector3.zero, new Vector3(0.04f, 0.003f, 3.20f), mats.FloorJoint);
            }

            // 3 metallic floor drain grates near the cell bars (Z = 3.25, Y = 0.026m -> zero Z-fighting)
            float[] grateXs = { -5.20f, 0.15f, 5.45f };
            for (int g = 0; g < grateXs.Length; g++)
            {
                BuildFloorDrainGrate($"PrisonDrainGrate_{g}", details.transform, new Vector3(grateXs[g], 0.026f, 3.25f), mats);
            }

            // 5 shallow damp dark puddles on the concrete floor (Y = 0.015m -> zero Z-fighting, faithful to concept art)
            BuildOrganicPuddle("PrisonPuddle_FarLeft", details.transform, new Vector3(-7.40f, 0.015f, 1.25f), new Vector2(2.40f, 0.95f), 8f, mats.WetPuddle);
            BuildOrganicPuddle("PrisonPuddle_MidLeft", details.transform, new Vector3(-1.35f, 0.015f, 1.85f), new Vector2(2.60f, 1.05f), -6f, mats.WetPuddle);
            BuildOrganicPuddle("PrisonPuddle_FrontCenter", details.transform, new Vector3(1.10f, 0.015f, -1.65f), new Vector2(2.50f, 0.90f), 5f, mats.WetPuddle);
            BuildOrganicPuddle("PrisonPuddle_MidRight", details.transform, new Vector3(4.55f, 0.015f, 1.45f), new Vector2(2.20f, 0.90f), -9f, mats.WetPuddle);
            BuildOrganicPuddle("PrisonPuddle_FarRight", details.transform, new Vector3(8.20f, 0.015f, -1.95f), new Vector2(2.80f, 1.10f), 11f, mats.WetPuddle);
        }

        // ==================== LAYER C: 2-STORY PRISON CELLBLOCK, STAIRS, PIPES & SKYLIGHT ====================

        private static void BuildLayerCPrisonCellblock(Transform parent, CadeiaMaterials mats)
        {
            var layerC = new GameObject("LayerC_PrisonCellblock");
            layerC.transform.SetParent(parent, false);

            // Total indoor enclosure: back wall, extreme left/right end walls (past ±24m), and full ceiling
            CreateBox("Cellblock_BackWall", layerC.transform, new Vector3(0f, 5.20f, 9.60f), Vector3.zero, new Vector3(60.0f, 10.80f, 0.60f), mats.ConcreteDark);
            CreateBox("Cellblock_EndWallLeft", layerC.transform, new Vector3(-25.50f, 5.20f, 0.50f), Vector3.zero, new Vector3(0.80f, 10.80f, 22.0f), mats.ConcreteWall);
            CreateBox("Cellblock_EndWallRight", layerC.transform, new Vector3(25.50f, 5.20f, 0.50f), Vector3.zero, new Vector3(0.80f, 10.80f, 22.0f), mats.ConcreteWall);

            // Continuous ceiling slab with cutout framing for skylight on left-center (X = -4.2)
            var ceiling = new GameObject("Cellblock_CeilingAndSkylight");
            ceiling.transform.SetParent(layerC.transform, false);
            CreateBox("Ceiling_MainSlab", ceiling.transform, new Vector3(0f, 9.95f, 1.20f), Vector3.zero, new Vector3(58.0f, 0.50f, 22.0f), mats.ConcreteDark);

            // Concrete roof cross-beams & exposed industrial metal piping
            for (int b = -5; b <= 5; b++)
            {
                float bx = b * 4.6f;
                CreateBox($"CeilingBeam_{b + 5}", ceiling.transform, new Vector3(bx, 9.45f, 2.50f), Vector3.zero, new Vector3(0.45f, 0.55f, 16.0f), mats.ConcreteWall);
            }
            // Longitudinal exposed pipes running along upper cellblock ceiling
            float[] pipeZs = { 2.85f, 3.25f, 3.62f, 4.95f };
            for (int p = 0; p < pipeZs.Length; p++)
            {
                float pr = (p % 2 == 0) ? 0.16f : 0.11f;
                CreateCyl($"CeilingPipe_{p}", ceiling.transform, new Vector3(0f, 8.95f - (p % 2) * 0.22f, pipeZs[p]), new Vector3(0f, 0f, 90f), new Vector3(pr, 26.0f, pr), (p == 1) ? mats.RustMetal : mats.IronBars);
            }

            // Skylight recessed frame + grated glass + subtle cold-blue volumetric light shaft (X = -4.2, Z = 2.6)
            var skylight = new GameObject("Ceiling_Skylight");
            skylight.transform.SetParent(ceiling.transform, false);
            skylight.transform.position = new Vector3(-4.20f, 9.65f, 2.80f);
            CreateBox("Skylight_FrameBack", skylight.transform, new Vector3(0f, -0.05f, 1.15f), Vector3.zero, new Vector3(3.60f, 0.35f, 0.22f), mats.ConcreteWall);
            CreateBox("Skylight_FrameFront", skylight.transform, new Vector3(0f, -0.05f, -1.15f), Vector3.zero, new Vector3(3.60f, 0.35f, 0.22f), mats.ConcreteWall);
            CreateBox("Skylight_FrameL", skylight.transform, new Vector3(-1.75f, -0.05f, 0f), Vector3.zero, new Vector3(0.22f, 0.35f, 2.50f), mats.ConcreteWall);
            CreateBox("Skylight_FrameR", skylight.transform, new Vector3(1.75f, -0.05f, 0f), Vector3.zero, new Vector3(0.22f, 0.35f, 2.50f), mats.ConcreteWall);
            GameObject skyGlass = CreateNoShadowBox("Skylight_GlowGlass", skylight.transform, new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(3.30f, 0.05f, 2.15f), mats.SkylightGlow);
            for (int sb = -3; sb <= 3; sb++)
            {
                CreateNoShadowBox($"Skylight_Bar_{sb + 3}", skylight.transform, new Vector3(sb * 0.45f, -0.08f, 0f), Vector3.zero, new Vector3(0.05f, 0.06f, 2.15f), mats.IronBars);
            }
            // Subtle cold-blue translucent light shaft slanting cleanly all the way down to the concrete floor
            GameObject beam = CreateNoShadowBox("Skylight_LightShaft", skylight.transform, new Vector3(1.05f, -4.95f, -0.35f), new Vector3(4f, 0f, 12f), new Vector3(3.80f, 10.60f, 0.14f), mats.SkylightBeam);
            beam.GetComponent<Renderer>().receiveShadows = false;

            // Hanging industrial overhead cables looping near skylight & pipes
            BuildCatenaryCable("PrisonCable_0", ceiling.transform, new Vector3(-8.5f, 8.85f, 2.75f), new Vector3(-2.1f, 8.85f, 2.75f), 0.78f, 8, 0.026f, mats.CableBlack);
            BuildCatenaryCable("PrisonCable_1", ceiling.transform, new Vector3(-6.2f, 8.75f, 3.10f), new Vector3(1.8f, 8.75f, 3.10f), 0.65f, 8, 0.022f, mats.CableBlack);
            BuildCatenaryCable("PrisonCable_2", ceiling.transform, new Vector3(2.2f, 8.85f, 2.85f), new Vector3(9.4f, 8.85f, 2.85f), 0.58f, 8, 0.024f, mats.CableBlack);

            // --- 1ST FLOOR: ROW OF 9 CELLS (X from -18.4 to +23.0, spacing 4.6m; cell 01..09) ---
            // Note: X in [-14.0 .. -8.5] has the left stairwell & security cage, so ground cells run from X = -6.9 to +20.7 (7 cells)
            // plus far-left service block/armored door closing beyond X = -24m!
            var groundCells = new GameObject("Floor1_CellRow");
            groundCells.transform.SetParent(layerC.transform, false);

            // Continuous horizontal concrete lintel beam above Floor 1 cells (Y = 3.40..3.85)
            CreateBox("Floor1_HeaderBeam", groundCells.transform, new Vector3(3.50f, 3.55f, 4.95f), Vector3.zero, new Vector3(47.0f, 0.52f, 1.30f), mats.ConcreteWall);

            // 8 ground-floor cells from X = -6.9 to +25.3 (spacing 4.6m)
            // Open/ajar cell doors on cells #1 (-2.3), #2 (+2.3) and #3 (+6.9) right in the main camera view!
            float cellSpacing = 4.60f;
            float firstCellX = -6.90f;
            for (int c = 0; c < 8; c++)
            {
                float cx = firstCellX + c * cellSpacing;
                bool isOpenDoor = (c == 1 || c == 2 || c == 3);
                float doorOpenAngle = c == 1 ? 68f : (c == 2 ? 105f : 52f);
                BuildPrisonCell(
                    $"Cell_1F_{c + 1:00}",
                    groundCells.transform,
                    new Vector3(cx, 0f, 4.85f),
                    cellSpacing,
                    height: 3.32f,
                    cellNumber: $"{c + 1:00}",
                    openDoor: isOpenDoor,
                    doorAngleDeg: doorOpenAngle,
                    buildInteriorFurniture: (c <= 5),
                    mats: mats);
            }

            // --- LEFT FLANK: INDUSTRIAL STAIRCASE, SECURITY MESH CAGE, ARMORED DOOR & GRAFFITI (X in [-24 .. -9.2]) ---
            BuildLeftStairwellAndSecurityZone(layerC.transform, mats);

            // --- 2ND FLOOR: CANTILEVERED CONCRETE CATWALK, CORBELS, RUSTED RAILING & UPPER CELL ROW (Y = 3.6..7.2) ---
            var upperFloor = new GameObject("Floor2_CatwalkAndCells");
            upperFloor.transform.SetParent(layerC.transform, false);

            // Cantilevered catwalk deck running continuously across entire hall (X in [-25 .. +25], Z in [3.45..5.25])
            CreateBox("Catwalk_DeckSlab", upperFloor.transform, new Vector3(0f, 3.68f, 4.35f), Vector3.zero, new Vector3(52.0f, 0.28f, 1.95f), mats.ConcreteWall);
            CreateBox("Catwalk_RustEdgeTrim", upperFloor.transform, new Vector3(0f, 3.68f, 3.36f), Vector3.zero, new Vector3(52.0f, 0.26f, 0.05f), mats.RustMetal);

            // Angled concrete support corbels underneath catwalk aligned with cell pillars
            for (int k = -5; k <= 5; k++)
            {
                float kx = firstCellX - cellSpacing * 0.5f + k * cellSpacing;
                CreateBox($"CatwalkCorbel_{k + 5}", upperFloor.transform, new Vector3(kx, 3.34f, 4.15f), new Vector3(24f, 0f, 0f), new Vector3(0.38f, 0.48f, 1.45f), mats.ConcreteDark);
            }

            // Continuous double rusted metallic catwalk guardrail (Y = 3.82..4.95, Z = 3.44)
            CreateBox("CatwalkRail_Top", upperFloor.transform, new Vector3(0f, 4.92f, 3.44f), Vector3.zero, new Vector3(52.0f, 0.065f, 0.065f), mats.RustMetal);
            CreateBox("CatwalkRail_Mid", upperFloor.transform, new Vector3(0f, 4.42f, 3.44f), Vector3.zero, new Vector3(52.0f, 0.05f, 0.05f), mats.RustMetal);
            CreateBox("CatwalkRail_Kick", upperFloor.transform, new Vector3(0f, 3.92f, 3.44f), Vector3.zero, new Vector3(52.0f, 0.14f, 0.04f), mats.RustMetal);
            for (int p = -14; p <= 14; p++)
            {
                float px = p * 1.75f;
                CreateBox($"CatwalkRailPost_{p + 14}", upperFloor.transform, new Vector3(px, 4.38f, 3.44f), Vector3.zero, new Vector3(0.055f, 1.12f, 0.055f), mats.IronBars);
                // Vertical security pickets between posts
                CreateBox($"CatwalkPicketA_{p + 14}", upperFloor.transform, new Vector3(px + 0.58f, 4.38f, 3.44f), Vector3.zero, new Vector3(0.028f, 1.04f, 0.028f), mats.IronBars);
                CreateBox($"CatwalkPicketB_{p + 14}", upperFloor.transform, new Vector3(px + 1.16f, 4.38f, 3.44f), Vector3.zero, new Vector3(0.028f, 1.04f, 0.028f), mats.IronBars);
            }

            // 2nd Floor cell row (10 barred cells along Z = 5.45, Y = 3.82..7.15)
            CreateBox("Floor2_HeaderBeam", upperFloor.transform, new Vector3(0f, 7.28f, 5.45f), Vector3.zero, new Vector3(52.0f, 0.48f, 1.30f), mats.ConcreteWall);
            for (int c = 0; c < 10; c++)
            {
                float cx = -16.10f + c * cellSpacing;
                BuildPrisonCell(
                    $"Cell_2F_{c + 1:00}",
                    upperFloor.transform,
                    new Vector3(cx, 3.82f, 5.45f),
                    cellSpacing,
                    height: 3.25f,
                    cellNumber: $"2{c + 1:0}",
                    openDoor: false,
                    doorAngleDeg: 0f,
                    buildInteriorFurniture: false,
                    mats: mats);
            }

            // Upper 3rd-tier concrete cornice & dark security gallery strip (Y = 7.4..9.7) for vertical depth like concept art
            CreateBox("Floor3_GallerySlab", upperFloor.transform, new Vector3(0f, 7.52f, 4.65f), Vector3.zero, new Vector3(52.0f, 0.24f, 1.50f), mats.ConcreteDark);
            CreateBox("Floor3_GalleryRail", upperFloor.transform, new Vector3(0f, 8.38f, 3.95f), Vector3.zero, new Vector3(52.0f, 0.055f, 0.055f), mats.IronBars);
        }

        private static void BuildPrisonCell(
            string name,
            Transform parent,
            Vector3 baseCenter,
            float width,
            float height,
            string cellNumber,
            bool openDoor,
            float doorAngleDeg,
            bool buildInteriorFurniture,
            CadeiaMaterials mats)
        {
            var cell = new GameObject(name);
            cell.transform.SetParent(parent, false);
            cell.transform.localPosition = baseCenter;

            float halfW = width * 0.5f;
            float pillarW = 0.58f;

            // Left concrete dividing pillar & interior dividing wall
            CreateBox("Pillar_L", cell.transform, new Vector3(-halfW, height * 0.5f, 0f), Vector3.zero, new Vector3(pillarW, height, 0.65f), mats.ConcreteWall);
            CreateBox("DividerWall_L", cell.transform, new Vector3(-halfW, height * 0.5f, 2.35f), Vector3.zero, new Vector3(0.32f, height, 4.40f), mats.CellInteriorWall);
            // Rust stain patch on pillar bottom (offset -0.03m from pillar front Z = -0.325 -> zero Z-fighting)
            CreateNoShadowBox("PillarRustPatch", cell.transform, new Vector3(-halfW, 0.55f, -0.355f), Vector3.zero, new Vector3(pillarW * 0.85f, 0.90f, 0.015f), mats.RustMetal);

            // Cell number plate on pillar (offset -0.04m from pillar front -> zero Z-fighting)
            CreateNoShadowBox("CellNumPlate", cell.transform, new Vector3(-halfW, height - 0.52f, -0.355f), Vector3.zero, new Vector3(0.38f, 0.24f, 0.02f), mats.NumberPlate);
            CreateWorldTmp("CellNumText", cell.transform, cellNumber, new Vector3(-halfW, height - 0.52f, -0.380f), 1.35f, new Color(0.84f, 0.88f, 0.92f, 1f), FontStyles.Bold);

            // Fixed left half of metal cell grille (X from -halfW+0.3 to +0.15)
            float fixedLeft = -halfW + pillarW * 0.5f;
            float doorSplit = 0.15f;
            float fixedWidth = doorSplit - fixedLeft;
            float fixedCenterX = (fixedLeft + doorSplit) * 0.5f;

            BuildBarredPanel("FixedGrille", cell.transform, new Vector3(fixedCenterX, height * 0.5f, 0f), fixedWidth, height, withLockPlate: false, mats);

            // Door right half (hinged at X = +halfW - pillarW*0.5 so when open it swings into the corridor like the concept art!)
            float doorRight = halfW - pillarW * 0.5f;
            float doorWidth = doorRight - doorSplit;
            var doorPivot = new GameObject("CellDoor_Pivot");
            doorPivot.transform.SetParent(cell.transform, false);
            doorPivot.transform.localPosition = new Vector3(doorRight, 0f, -0.02f);
            doorPivot.transform.localRotation = openDoor ? Quaternion.Euler(0f, doorAngleDeg, 0f) : Quaternion.identity;
            BuildBarredPanel("CellDoor_Panel", doorPivot.transform, new Vector3(-doorWidth * 0.5f, height * 0.5f, 0f), doorWidth, height - 0.08f, withLockPlate: true, mats);

            // Interior cell dark backing & furniture (bunk bed + mattress/pillow + metal toilet/sink)
            CreateBox("CellInterior_Back", cell.transform, new Vector3(0f, height * 0.5f, 4.35f), Vector3.zero, new Vector3(width - 0.30f, height, 0.18f), mats.CellInteriorDark);

            if (buildInteriorFurniture)
            {
                // Metal bunk bed on the right interior wall
                var bunk = new GameObject("Cell_BunkBed");
                bunk.transform.SetParent(cell.transform, false);
                bunk.transform.localPosition = new Vector3(0.65f, 0f, 2.75f);
                CreateBox("Post_FL", bunk.transform, new Vector3(-0.92f, 1.02f, -0.44f), Vector3.zero, new Vector3(0.06f, 2.04f, 0.06f), mats.IronBars);
                CreateBox("Post_FR", bunk.transform, new Vector3(0.92f, 1.02f, -0.44f), Vector3.zero, new Vector3(0.06f, 2.04f, 0.06f), mats.IronBars);
                CreateBox("Post_BL", bunk.transform, new Vector3(-0.92f, 1.02f, 0.44f), Vector3.zero, new Vector3(0.06f, 2.04f, 0.06f), mats.IronBars);
                CreateBox("Post_BR", bunk.transform, new Vector3(0.92f, 1.02f, 0.44f), Vector3.zero, new Vector3(0.06f, 2.04f, 0.06f), mats.IronBars);
                CreateBox("Frame_Lower", bunk.transform, new Vector3(0f, 0.52f, 0f), Vector3.zero, new Vector3(1.88f, 0.08f, 0.90f), mats.RustMetal);
                CreateBox("Frame_Upper", bunk.transform, new Vector3(0f, 1.56f, 0f), Vector3.zero, new Vector3(1.88f, 0.08f, 0.90f), mats.RustMetal);
                CreateBox("Mattress_Lower", bunk.transform, new Vector3(0f, 0.64f, 0f), Vector3.zero, new Vector3(1.78f, 0.16f, 0.82f), mats.Mattress);
                CreateBox("Mattress_Upper", bunk.transform, new Vector3(0f, 1.68f, 0f), Vector3.zero, new Vector3(1.78f, 0.16f, 0.82f), mats.Mattress);
                CreateBox("Pillow_Lower", bunk.transform, new Vector3(-0.62f, 0.76f, 0f), Vector3.zero, new Vector3(0.38f, 0.10f, 0.54f), mats.Pillow);
                CreateBox("Pillow_Upper", bunk.transform, new Vector3(-0.62f, 1.80f, 0f), Vector3.zero, new Vector3(0.38f, 0.10f, 0.54f), mats.Pillow);

                // Stainless metal prison toilet / sink combo on left interior corner
                CreateBox("CellSink_Base", cell.transform, new Vector3(-1.38f, 0.42f, 3.60f), Vector3.zero, new Vector3(0.55f, 0.84f, 0.50f), mats.SteelLight);
                CreateCyl("CellToilet_Bowl", cell.transform, new Vector3(-1.38f, 0.24f, 3.12f), Vector3.zero, new Vector3(0.38f, 0.24f, 0.44f), mats.SteelLight);
            }
        }

        private static void BuildBarredPanel(string name, Transform parent, Vector3 localCenter, float width, float height, bool withLockPlate, CadeiaMaterials mats)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = localCenter;

            // Outer frame bars
            CreateBox("Bar_Top", panel.transform, new Vector3(0f, height * 0.5f - 0.04f, 0f), Vector3.zero, new Vector3(width, 0.08f, 0.07f), mats.IronBars);
            CreateBox("Bar_Bot", panel.transform, new Vector3(0f, -height * 0.5f + 0.04f, 0f), Vector3.zero, new Vector3(width, 0.08f, 0.07f), mats.IronBars);
            CreateBox("Bar_Mid1", panel.transform, new Vector3(0f, -0.35f, 0f), Vector3.zero, new Vector3(width, 0.055f, 0.06f), mats.IronBars);
            CreateBox("Bar_Mid2", panel.transform, new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(width, 0.055f, 0.06f), mats.IronBars);

            int vBars = Mathf.Max(5, Mathf.RoundToInt(width / 0.22f));
            for (int i = 0; i < vBars; i++)
            {
                float bx = Mathf.Lerp(-width * 0.5f + 0.04f, width * 0.5f - 0.04f, i / (float)(vBars - 1));
                CreateCyl($"VBar_{i}", panel.transform, new Vector3(bx, 0f, 0f), Vector3.zero, new Vector3(0.038f, height * 0.49f, 0.038f), mats.IronBars);
            }

            if (withLockPlate)
            {
                CreateBox("RustLockPlate", panel.transform, new Vector3(-width * 0.34f, -0.18f, -0.01f), Vector3.zero, new Vector3(0.42f, 0.54f, 0.075f), mats.RustMetal);
            }
        }

        private static void BuildLeftStairwellAndSecurityZone(Transform parent, CadeiaMaterials mats)
        {
            var leftZone = new GameObject("LeftFlank_StairwellAndSecurity");
            leftZone.transform.SetParent(parent, false);

            // Concrete stairwell back & side walls closing X = -25 .. -9.2 cleanly
            CreateBox("Stairwell_WallBack", leftZone.transform, new Vector3(-17.0f, 1.85f, 5.20f), Vector3.zero, new Vector3(16.0f, 3.70f, 0.55f), mats.ConcreteWall);

            // Subtle prison graffiti decal on concrete wall right next to stairs (offset -0.04m from wall front Z=4.925 -> zero Z-fighting)
            GameObject grafQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            grafQuad.name = "PrisonWall_Graffiti";
            grafQuad.transform.SetParent(leftZone.transform, false);
            grafQuad.transform.localPosition = new Vector3(-10.10f, 1.75f, 4.885f);
            grafQuad.transform.localScale = new Vector3(2.20f, 1.35f, 1f);
            Object.DestroyImmediate(grafQuad.GetComponent<Collider>());
            grafQuad.GetComponent<MeshRenderer>().sharedMaterial = mats.GraffitiDecal;

            // Armored steel security door at back left
            CreateBox("ArmoredDoor_Frame", leftZone.transform, new Vector3(-14.80f, 1.25f, 4.89f), Vector3.zero, new Vector3(1.55f, 2.50f, 0.14f), mats.IronBars);
            CreateBox("ArmoredDoor_Leaf", leftZone.transform, new Vector3(-14.80f, 1.24f, 4.86f), Vector3.zero, new Vector3(1.36f, 2.34f, 0.08f), mats.SteelDark);
            CreateBox("ArmoredDoor_Slot", leftZone.transform, new Vector3(-14.80f, 1.78f, 4.80f), Vector3.zero, new Vector3(0.46f, 0.14f, 0.04f), mats.IronBars);

            // Industrial steel staircase climbing from (X = -12.6, Y = 0.0, Z = 2.5) up to 2nd floor catwalk (Y = 3.68, Z = 4.5)
            var stairs = new GameObject("Industrial_Staircase");
            stairs.transform.SetParent(leftZone.transform, false);
            int stepCount = 13;
            for (int s = 0; s < stepCount; s++)
            {
                float t = (s + 0.5f) / stepCount;
                float sy = t * 3.68f;
                float sz = Mathf.Lerp(2.15f, 4.45f, t);
                CreateBox($"MetalStep_{s}", stairs.transform, new Vector3(-12.20f, sy, sz), Vector3.zero, new Vector3(1.65f, 0.075f, 0.28f), mats.RustMetal);
            }
            // Side steel stringers & handrails
            float[] stringerXs = { -13.05f, -11.35f };
            foreach (float sx in stringerXs)
            {
                CreateBox($"StairStringer_{(sx < -12f ? "L" : "R")}", stairs.transform, new Vector3(sx, 1.84f, 3.30f), new Vector3(-58f, 0f, 0f), new Vector3(0.08f, 0.24f, 4.35f), mats.IronBars);
                CreateBox($"StairHandrail_{(sx < -12f ? "L" : "R")}", stairs.transform, new Vector3(sx, 2.75f, 3.15f), new Vector3(-58f, 0f, 0f), new Vector3(0.055f, 0.055f, 4.35f), mats.RustMetal);
            }

            // Security mesh cage tower flanking the staircase (X = -11.2, Y = 0..7.2, Z = 3.8..4.9)
            var cage = new GameObject("Stairwell_SecurityCage");
            cage.transform.SetParent(leftZone.transform, false);
            CreateBox("CagePost_FL", cage.transform, new Vector3(-13.15f, 3.60f, 3.85f), Vector3.zero, new Vector3(0.10f, 7.20f, 0.10f), mats.RustMetal);
            CreateBox("CagePost_FR", cage.transform, new Vector3(-11.25f, 3.60f, 3.85f), Vector3.zero, new Vector3(0.10f, 7.20f, 0.10f), mats.RustMetal);
            for (int r = 0; r < 10; r++)
            {
                CreateBox($"CageCrossBar_{r}", cage.transform, new Vector3(-12.20f, 3.95f + r * 0.34f, 3.85f), Vector3.zero, new Vector3(1.90f, 0.03f, 0.03f), mats.IronBars);
            }
            for (int v = 0; v < 7; v++)
            {
                float vx = Mathf.Lerp(-13.05f, -11.35f, v / 6f);
                CreateBox($"CageVertBar_{v}", cage.transform, new Vector3(vx, 5.50f, 3.85f), Vector3.zero, new Vector3(0.03f, 3.30f, 0.03f), mats.IronBars);
            }

            // Far-left service door & concrete pilasters closing X < -16
            for (int d = 0; d < 2; d++)
            {
                float dx = -18.2f - d * 3.8f;
                CreateBox($"FarLeftPilaster_{d}", leftZone.transform, new Vector3(dx + 1.8f, 1.85f, 4.75f), Vector3.zero, new Vector3(0.55f, 3.70f, 0.65f), mats.ConcreteDark);
                CreateBox($"FarLeftSteelDoor_{d}", leftZone.transform, new Vector3(dx, 1.25f, 4.86f), Vector3.zero, new Vector3(1.45f, 2.45f, 0.08f), mats.SteelDark);
            }
        }

        // ==================== LAYER B: INTERACTIVE PHYSICS V2 PROPS ====================

        private static void InstantiateCadeiaInteractiveProps(Transform propsRoot)
        {
            GameObject lampPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HangingLampPrefabPath);
            GameObject benchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BreakableBenchPrefabPath);
            GameObject lockerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BreakableLockerPrefabPath);
            GameObject stoolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThrowableStoolPrefabPath);
            GameObject bucketPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThrowableBucketPrefabPath);
            GameObject cupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DynamicCupPrefabPath);

            // 1. Four physical HingeJoint fluorescent lamps along the cellblock corridor (swing dramatically on shockwaves!)
            if (lampPrefab != null)
            {
                InstantiateProp(lampPrefab, "Hanging_FluorescentLamp_0", propsRoot, new Vector3(-6.80f, 5.45f, 2.05f), 0f);
                InstantiateProp(lampPrefab, "Hanging_FluorescentLamp_1", propsRoot, new Vector3(-2.10f, 5.45f, 2.05f), 0f);
                InstantiateProp(lampPrefab, "Hanging_FluorescentLamp_2", propsRoot, new Vector3(2.40f, 5.45f, 2.05f), 0f);
                InstantiateProp(lampPrefab, "Hanging_FluorescentLamp_3", propsRoot, new Vector3(7.00f, 5.45f, 2.05f), 0f);
            }

            // 2. Two Breakable props with persistent StageDebris (metal bench left + rusted locker right)
            if (benchPrefab != null)
            {
                InstantiateProp(benchPrefab, "Breakable_MetalBench_Left", propsRoot, new Vector3(-6.30f, 0f, 1.90f), 5f);
            }
            if (lockerPrefab != null)
            {
                InstantiateProp(lockerPrefab, "Breakable_RustLocker_Right", propsRoot, new Vector3(6.30f, 0f, 1.90f), -8f);
            }

            // 3. Two MK-style Throwable props (HoldBlock + LightPunch: BANQUETA & BALDE)
            if (stoolPrefab != null)
            {
                InstantiateProp(stoolPrefab, "Throwable_MetalStool_Left", propsRoot, new Vector3(-3.90f, 0f, 1.35f), 12f);
            }
            if (bucketPrefab != null)
            {
                InstantiateProp(bucketPrefab, "Throwable_MetalBucket_Right", propsRoot, new Vector3(3.60f, 0f, 1.35f), -16f);
            }

            // 4. Four loose PhysX Dynamic prison cups/tins (outside spawns X=±2.2..2.5; kicked by FighterDebrisPusher)
            if (cupPrefab != null)
            {
                InstantiateProp(cupPrefab, "Dynamic_PrisonCup_Left", propsRoot, new Vector3(-5.10f, 0.08f, 0.72f), 20f);
                InstantiateProp(cupPrefab, "Dynamic_PrisonCup_CenterL", propsRoot, new Vector3(-0.85f, 0.08f, 0.54f), -15f);
                InstantiateProp(cupPrefab, "Dynamic_PrisonCup_CenterR", propsRoot, new Vector3(1.15f, 0.08f, -0.42f), 38f);
                InstantiateProp(cupPrefab, "Dynamic_PrisonCup_Right", propsRoot, new Vector3(5.15f, 0.08f, 0.76f), -28f);
            }
        }

        // ==================== STAGE MANAGERS & FIXED INDOOR WEATHER ====================

        private static void WireCadeiaManagersAndWeather(
            Scene scene,
            StageData cadeiaData,
            Light keyLight,
            Light[] secondaryLights,
            float[] secondaryBaseIntensities)
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

            // Very gentle indoor ventilation draft (0.35 + 0.25 m/s):
            // ground props (wake >= 4.2) stay 100% still until kicked; hanging lamps gently sway and react dramatically to combat shockwaves!
            StageWindController wind = managerGo.GetComponent<StageWindController>() ?? managerGo.AddComponent<StageWindController>();
            wind.Configure(new Vector3(1f, 0f, 0.12f), baseSpeed: 0.35f, gustAmp: 0.25f, gustFreq: 0.20f);

            StageWeatherController weather = managerGo.GetComponent<StageWeatherController>() ?? managerGo.AddComponent<StageWeatherController>();

            var indoorProfile = new StageWeatherController.WeatherProfile
            {
                directionalLightColor = new Color(0.68f, 0.84f, 1.00f, 1f),
                directionalLightIntensity = 1.55f,
                directionalLightEuler = new Vector3(54f, 152f, 0f),
                ambientSkyColor = new Color(0.26f, 0.34f, 0.44f, 1f),
                ambientEquatorColor = new Color(0.24f, 0.30f, 0.38f, 1f),
                ambientGroundColor = new Color(0.14f, 0.16f, 0.20f, 1f),
                fogEnabled = true,
                fogColor = new Color(0.12f, 0.17f, 0.23f, 1f),
                fogDensity = 0.012f,
                cameraBackgroundColor = new Color(0.08f, 0.11f, 0.16f, 1f),
                skyTintColor = new Color(0.10f, 0.14f, 0.20f, 1f),
                skyGroundColor = new Color(0.06f, 0.08f, 0.11f, 1f),
                skyExposure = 0.25f,
                skyAtmosphereThickness = 0.40f,
                windBaseSpeed = 0.35f,
                windGustAmplitude = 0.25f,
                windGustFrequency = 0.20f,
                secondaryLightsMultiplier = 1.0f
            };
            weather.SetProfiles(indoorProfile, indoorProfile, indoorProfile);

            var weatherSo = new SerializedObject(weather);
            weatherSo.FindProperty("stageData").objectReferenceValue = cadeiaData;
            weatherSo.FindProperty("mainDirectionalLight").objectReferenceValue = keyLight;
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

            weatherSo.FindProperty("dayOnlyObjects").arraySize = 0;
            weatherSo.FindProperty("clearNightOnlyObjects").arraySize = 0;
            weatherSo.FindProperty("nightOnlyObjects").arraySize = 0;
            weatherSo.FindProperty("rainOnlyObjects").arraySize = 0;
            weatherSo.FindProperty("wetSurfaceRenderers").arraySize = 0;
            weatherSo.ApplyModifiedPropertiesWithoutUndo();

            manager.RefreshDiscoveredProps();
        }

        // ==================== REUSABLE CADEIA PROP PREFABS ====================

        private static void CreateOrUpdateCadeiaPropPrefabs(CadeiaMaterials mats)
        {
            if (!AssetDatabase.IsValidFolder(StagePropsPrefabFolder))
            {
                Directory.CreateDirectory(StagePropsPrefabFolder);
                AssetDatabase.Refresh();
            }

            BuildHangingLampPrefab(HangingLampPrefabPath, mats.LampFixture, mats.FluorescentGlow, mats.CableBlack);
            BuildBreakableBenchPrefab(BreakableBenchPrefabPath, mats.SteelDark, mats.RustMetal);
            BuildBreakableLockerPrefab(BreakableLockerPrefabPath, mats.RustMetal, mats.SteelDark);
            BuildThrowableStoolPrefab(ThrowableStoolPrefabPath, mats.SteelLight, mats.RustMetal);
            BuildThrowableBucketPrefab(ThrowableBucketPrefabPath, mats.SteelLight, mats.RustMetal);
            BuildDynamicCupPrefab(DynamicCupPrefabPath, mats.SteelLight, mats.SteelDark);
        }

        private static void BuildHangingLampPrefab(string prefabPath, Material fixtureMat, Material tubeGlowMat, Material cableMat)
        {
            var root = new GameObject("Prop_Cadeia_HangingLamp");
            try
            {
                // Ceiling conduit anchor box + long suspension cables down to Y = 0
                CreateBox("CeilingBox", root.transform, new Vector3(0f, 0.05f, 0f), Vector3.zero, new Vector3(0.45f, 0.08f, 0.14f), fixtureMat);
                CreateCyl("UpperCableL", root.transform, new Vector3(-0.48f, 2.20f, 0f), Vector3.zero, new Vector3(0.014f, 2.20f, 0.014f), cableMat);
                CreateCyl("UpperCableR", root.transform, new Vector3(0.48f, 2.20f, 0f), Vector3.zero, new Vector3(0.014f, 2.20f, 0.014f), cableMat);

                var anchorRb = root.AddComponent<Rigidbody>();
                anchorRb.isKinematic = true;
                anchorRb.useGravity = false;

                var bodyGo = new GameObject("HangingBody");
                bodyGo.layer = StagePhysicsLayers.StageDebrisLayer;
                bodyGo.transform.SetParent(root.transform, false);
                bodyGo.transform.localPosition = new Vector3(0f, -0.58f, 0f);
                bodyGo.transform.localRotation = Quaternion.identity;

                // Suspension rods from hinge pivot down to industrial fluorescent housing
                CreateCyl("RodL", bodyGo.transform, new Vector3(-0.48f, 0.30f, 0f), Vector3.zero, new Vector3(0.016f, 0.28f, 0.016f), cableMat);
                CreateCyl("RodR", bodyGo.transform, new Vector3(0.48f, 0.30f, 0f), Vector3.zero, new Vector3(0.016f, 0.28f, 0.016f), cableMat);
                CreateBox("LampHood", bodyGo.transform, new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(1.36f, 0.09f, 0.28f), fixtureMat);
                CreateBox("LampReflectorL", bodyGo.transform, new Vector3(0f, -0.02f, -0.14f), new Vector3(30f, 0f, 0f), new Vector3(1.36f, 0.03f, 0.10f), fixtureMat);
                CreateBox("LampReflectorR", bodyGo.transform, new Vector3(0f, -0.02f, 0.14f), new Vector3(-30f, 0f, 0f), new Vector3(1.36f, 0.03f, 0.10f), fixtureMat);
                CreateCyl("FluorescentTubeA", bodyGo.transform, new Vector3(0f, -0.045f, -0.045f), new Vector3(0f, 0f, 90f), new Vector3(0.048f, 0.62f, 0.048f), tubeGlowMat);
                CreateCyl("FluorescentTubeB", bodyGo.transform, new Vector3(0f, -0.045f, 0.045f), new Vector3(0f, 0f, 90f), new Vector3(0.048f, 0.62f, 0.048f), tubeGlowMat);

                var box = bodyGo.AddComponent<BoxCollider>();
                box.size = new Vector3(1.38f, 0.16f, 0.32f);
                box.center = Vector3.zero;
                box.isTrigger = false;

                var swingRb = bodyGo.AddComponent<Rigidbody>();
                swingRb.mass = 1.05f;
                swingRb.isKinematic = false;
                swingRb.useGravity = true;
                swingRb.linearDamping = 0.20f;
                swingRb.angularDamping = 0.65f;
                swingRb.sleepThreshold = 0.015f;
                swingRb.interpolation = RigidbodyInterpolation.Interpolate;

                var hinge = bodyGo.AddComponent<HingeJoint>();
                hinge.connectedBody = anchorRb;
                hinge.axis = Vector3.forward;
                hinge.anchor = new Vector3(0f, 0.58f, 0f);
                hinge.autoConfigureConnectedAnchor = false;
                hinge.connectedAnchor = Vector3.zero;
                hinge.useSpring = true;
                hinge.spring = new JointSpring { spring = 4.2f, damper = 0.95f, targetPosition = 0f };
                hinge.useLimits = true;
                hinge.limits = new JointLimits { min = -55f, max = 55f, bounciness = 0.08f };

                var wind = bodyGo.AddComponent<StageWindAffectable>();
                wind.Configure(cd: 1.25f, areaM2: 0.32f, sensitivity: 1.25f, wakeThreshold: 0.45f, bodyOverride: swingRb);

                var hanging = root.AddComponent<StageHangingProp>();
                var so = new SerializedObject(hanging);
                so.FindProperty("anchorBody").objectReferenceValue = anchorRb;
                so.FindProperty("swingBody").objectReferenceValue = swingRb;
                so.FindProperty("hingeJoint").objectReferenceValue = hinge;
                so.FindProperty("windAffectable").objectReferenceValue = wind;
                so.FindProperty("hingeAxis").vector3Value = Vector3.forward;
                so.FindProperty("localPivotOnSwingBody").vector3Value = new Vector3(0f, 0.58f, 0f);
                so.FindProperty("swingMass").floatValue = 1.05f;
                so.FindProperty("linearDamping").floatValue = 0.20f;
                so.FindProperty("angularDamping").floatValue = 0.65f;
                so.FindProperty("springStrength").floatValue = 4.2f;
                so.FindProperty("springDamper").floatValue = 0.95f;
                so.FindProperty("maxSwingAngle").floatValue = 55f;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildBreakableBenchPrefab(string prefabPath, Material steelMat, Material rustMat)
        {
            var root = new GameObject("Prop_Cadeia_BreakableBench");
            try
            {
                var intact = new GameObject("IntactVisual");
                intact.transform.SetParent(root.transform, false);

                CreateBox("BenchTop", intact.transform, new Vector3(0f, 0.52f, 0f), Vector3.zero, new Vector3(1.52f, 0.07f, 0.48f), steelMat);
                CreateBox("BenchTrim", intact.transform, new Vector3(0f, 0.47f, 0f), Vector3.zero, new Vector3(1.44f, 0.05f, 0.40f), rustMat);
                CreateBox("LegL", intact.transform, new Vector3(-0.62f, 0.25f, 0f), Vector3.zero, new Vector3(0.08f, 0.50f, 0.42f), rustMat);
                CreateBox("LegR", intact.transform, new Vector3(0.62f, 0.25f, 0f), Vector3.zero, new Vector3(0.08f, 0.50f, 0.42f), rustMat);

                var col = root.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.28f, 0f);
                col.size = new Vector3(1.58f, 0.58f, 0.54f);

                var pool = new GameObject("DebrisPool");
                pool.transform.SetParent(root.transform, false);
                for (int i = 0; i < 7; i++)
                {
                    Vector3 pos = new Vector3(Mathf.Lerp(-0.58f, 0.58f, i / 6f), 0.24f + (i % 2) * 0.24f, (i % 2 == 0 ? -0.08f : 0.08f));
                    Vector3 scl = (i < 4) ? new Vector3(0.38f, 0.06f, 0.22f) : new Vector3(0.08f, 0.44f, 0.14f);
                    CreateDebrisShard($"BenchShard_{i}", PrimitiveType.Cube, pool.transform, pos, new Vector3(i * 19f, i * 31f, i * 13f), scl, (i % 2 == 0) ? steelMat : rustMat);
                }

                ConfigureBreakableComponent(root, intact, col, pool.transform, StagePropMaterialType.ConcreteOrMetal, integrity: 2, radiusX: 1.65f, zTolerance: 2.15f);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildBreakableLockerPrefab(string prefabPath, Material rustMat, Material steelMat)
        {
            var root = new GameObject("Prop_Cadeia_BreakableLocker");
            try
            {
                var intact = new GameObject("IntactVisual");
                intact.transform.SetParent(root.transform, false);

                CreateBox("LockerBody", intact.transform, new Vector3(0f, 0.58f, 0f), Vector3.zero, new Vector3(0.76f, 1.16f, 0.52f), rustMat);
                CreateBox("LockerDoorFrame", intact.transform, new Vector3(0f, 0.58f, -0.27f), Vector3.zero, new Vector3(0.68f, 1.06f, 0.03f), steelMat);
                for (int v = 0; v < 3; v++)
                {
                    CreateBox($"Vent_{v}", intact.transform, new Vector3(0f, 0.90f - v * 0.08f, -0.295f), Vector3.zero, new Vector3(0.42f, 0.025f, 0.02f), rustMat);
                }

                var col = root.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.58f, 0f);
                col.size = new Vector3(0.82f, 1.20f, 0.58f);

                var pool = new GameObject("DebrisPool");
                pool.transform.SetParent(root.transform, false);
                for (int i = 0; i < 7; i++)
                {
                    Vector3 pos = new Vector3(((i % 3) - 1) * 0.20f, 0.24f + (i / 3) * 0.34f, (i % 2 == 0 ? -0.12f : 0.12f));
                    Vector3 scl = new Vector3(0.32f, 0.28f, 0.055f);
                    CreateDebrisShard($"LockerShard_{i}", PrimitiveType.Cube, pool.transform, pos, new Vector3(i * 15f, i * 41f, i * 17f), scl, (i % 2 == 0) ? rustMat : steelMat);
                }

                ConfigureBreakableComponent(root, intact, col, pool.transform, StagePropMaterialType.ConcreteOrMetal, integrity: 2, radiusX: 1.65f, zTolerance: 2.15f);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildThrowableStoolPrefab(string prefabPath, Material topMat, Material legMat)
        {
            var root = new GameObject("Prop_Cadeia_ThrowableStool");
            root.layer = StagePhysicsLayers.StageProjectileLayer;
            try
            {
                var intact = new GameObject("IntactVisual");
                intact.transform.SetParent(root.transform, false);

                CreateCyl("StoolSeat", intact.transform, new Vector3(0f, 0.50f, 0f), Vector3.zero, new Vector3(0.40f, 0.035f, 0.40f), topMat);
                CreateCyl("Leg_FL", intact.transform, new Vector3(-0.14f, 0.25f, -0.14f), new Vector3(6f, 0f, -6f), new Vector3(0.04f, 0.25f, 0.04f), legMat);
                CreateCyl("Leg_FR", intact.transform, new Vector3(0.14f, 0.25f, -0.14f), new Vector3(6f, 0f, 6f), new Vector3(0.04f, 0.25f, 0.04f), legMat);
                CreateCyl("Leg_BL", intact.transform, new Vector3(-0.14f, 0.25f, 0.14f), new Vector3(-6f, 0f, -6f), new Vector3(0.04f, 0.25f, 0.04f), legMat);
                CreateCyl("Leg_BR", intact.transform, new Vector3(0.14f, 0.25f, 0.14f), new Vector3(-6f, 0f, 6f), new Vector3(0.04f, 0.25f, 0.04f), legMat);
                CreateCyl("FootRing", intact.transform, new Vector3(0f, 0.16f, 0f), Vector3.zero, new Vector3(0.32f, 0.02f, 0.32f), legMat);

                var col = root.AddComponent<BoxCollider>();
                col.isTrigger = false;
                col.center = new Vector3(0f, 0.27f, 0f);
                col.size = new Vector3(0.44f, 0.54f, 0.44f);

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
                    Material m = (i < 2) ? topMat : legMat;
                    Vector3 scl = (i < 2) ? new Vector3(0.24f, 0.04f, 0.22f) : new Vector3(0.045f, 0.38f, 0.045f);
                    CreateDebrisShard($"StoolShard_{i}", PrimitiveType.Cube, pool.transform, new Vector3(((i % 3) - 1) * 0.12f, 0.22f + (i / 3) * 0.22f, 0f), new Vector3(i * 21f, i * 33f, i * 16f), scl, m);
                }

                ConfigureThrowableComponent(root, intact, col, rb, pool.transform, "BANQUETA", StagePropMaterialType.ConcreteOrMetal, damage: 85, causesKnockdown: true, shatterOnStage: false);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildThrowableBucketPrefab(string prefabPath, Material steelMat, Material rimMat)
        {
            var root = new GameObject("Prop_Cadeia_ThrowableBucket");
            root.layer = StagePhysicsLayers.StageProjectileLayer;
            try
            {
                var intact = new GameObject("IntactVisual");
                intact.transform.SetParent(root.transform, false);

                CreateCyl("BucketLower", intact.transform, new Vector3(0f, 0.14f, 0f), Vector3.zero, new Vector3(0.32f, 0.14f, 0.32f), steelMat);
                CreateCyl("BucketUpper", intact.transform, new Vector3(0f, 0.32f, 0f), Vector3.zero, new Vector3(0.37f, 0.12f, 0.37f), steelMat);
                CreateCyl("BucketRim", intact.transform, new Vector3(0f, 0.44f, 0f), Vector3.zero, new Vector3(0.39f, 0.025f, 0.39f), rimMat);
                CreateBox("BucketHandle", intact.transform, new Vector3(0f, 0.56f, 0f), Vector3.zero, new Vector3(0.38f, 0.025f, 0.025f), rimMat);

                var col = root.AddComponent<BoxCollider>();
                col.isTrigger = false;
                col.center = new Vector3(0f, 0.24f, 0f);
                col.size = new Vector3(0.40f, 0.48f, 0.40f);

                var rb = root.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.mass = 2.1f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                var pool = new GameObject("DebrisPool");
                pool.transform.SetParent(root.transform, false);
                for (int i = 0; i < 6; i++)
                {
                    Material m = (i >= 4) ? rimMat : steelMat;
                    CreateDebrisShard($"BucketShard_{i}", PrimitiveType.Cube, pool.transform, new Vector3(((i % 3) - 1) * 0.10f, 0.18f + (i / 3) * 0.18f, (i % 2 == 0 ? -0.07f : 0.07f)), new Vector3(i * 18f, i * 37f, i * 19f), new Vector3(0.18f, 0.16f, 0.04f), m);
                }

                ConfigureThrowableComponent(root, intact, col, rb, pool.transform, "BALDE", StagePropMaterialType.ConcreteOrMetal, damage: 80, causesKnockdown: true, shatterOnStage: false);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildDynamicCupPrefab(string prefabPath, Material metalMat, Material rimMat)
        {
            var root = new GameObject("Prop_Cadeia_DynamicCup");
            root.layer = StagePhysicsLayers.StageDebrisLayer;
            try
            {
                CreateCyl("CupBody", root.transform, Vector3.zero, Vector3.zero, new Vector3(0.115f, 0.08f, 0.115f), metalMat);
                CreateCyl("CupRim", root.transform, new Vector3(0f, 0.078f, 0f), Vector3.zero, new Vector3(0.122f, 0.012f, 0.122f), rimMat);
                CreateBox("CupHandle", root.transform, new Vector3(0.072f, 0f, 0f), Vector3.zero, new Vector3(0.035f, 0.09f, 0.022f), rimMat);

                var col = root.AddComponent<CapsuleCollider>();
                col.isTrigger = false;
                col.radius = 0.062f;
                col.height = 0.17f;
                col.center = Vector3.zero;

                var rb = root.AddComponent<Rigidbody>();
                rb.mass = 0.28f;
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.sleepThreshold = 0.08f;
                rb.linearDamping = 0.35f;
                rb.angularDamping = 0.55f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                var wind = root.AddComponent<StageWindAffectable>();
                wind.Configure(cd: 1.05f, areaM2: 0.10f, sensitivity: 0.90f, wakeThreshold: 4.3f, bodyOverride: rb);

                var dyn = root.AddComponent<StageDynamicProp>();
                var so = new SerializedObject(dyn);
                so.FindProperty("body").objectReferenceValue = rb;
                so.FindProperty("propCollider").objectReferenceValue = col;
                so.FindProperty("windAffectable").objectReferenceValue = wind;
                so.FindProperty("materialType").enumValueIndex = (int)StagePropMaterialType.ConcreteOrMetal;
                so.FindProperty("mass").floatValue = 0.28f;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ==================== MICRO-BUILDERS ====================

        private static void BuildFloorDrainGrate(string name, Transform parent, Vector3 pos, CadeiaMaterials mats)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            CreateNoShadowBox("FrameBack", g.transform, new Vector3(0f, 0f, 0.16f), Vector3.zero, new Vector3(0.82f, 0.018f, 0.045f), mats.IronBars);
            CreateNoShadowBox("FrameFront", g.transform, new Vector3(0f, 0f, -0.16f), Vector3.zero, new Vector3(0.82f, 0.018f, 0.045f), mats.IronBars);
            CreateNoShadowBox("FrameL", g.transform, new Vector3(-0.39f, 0f, 0f), Vector3.zero, new Vector3(0.045f, 0.018f, 0.36f), mats.IronBars);
            CreateNoShadowBox("FrameR", g.transform, new Vector3(0.39f, 0f, 0f), Vector3.zero, new Vector3(0.045f, 0.018f, 0.36f), mats.IronBars);
            CreateNoShadowBox("Recess", g.transform, new Vector3(0f, -0.007f, 0f), Vector3.zero, new Vector3(0.74f, 0.006f, 0.28f), mats.CellInteriorDark);
            for (int b = 0; b < 6; b++)
            {
                float bx = Mathf.Lerp(-0.30f, 0.30f, b / 5f);
                CreateNoShadowBox($"Bar_{b}", g.transform, new Vector3(bx, 0.002f, 0f), Vector3.zero, new Vector3(0.032f, 0.016f, 0.29f), mats.RustMetal);
            }
        }

        private static void BuildOrganicPuddle(string name, Transform parent, Vector3 pos, Vector2 sizeXZ, float rotY, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            GameObject mainDisc = CreateCyl("Puddle_Main", go.transform, Vector3.zero, Vector3.zero, new Vector3(sizeXZ.x, 0.003f, sizeXZ.y), mat);
            DisableShadows(mainDisc);
            GameObject lobeDisc = CreateCyl("Puddle_Lobe", go.transform, new Vector3(sizeXZ.x * 0.25f, 0.002f, sizeXZ.y * 0.16f), Vector3.zero, new Vector3(sizeXZ.x * 0.54f, 0.0025f, sizeXZ.y * 0.66f), mat);
            DisableShadows(lobeDisc);
        }

        private static void BuildCatenaryCable(string name, Transform parent, Vector3 start, Vector3 end, float sag, int segments, float thickness, Material cableMat)
        {
            var cableRoot = new GameObject(name);
            cableRoot.transform.SetParent(parent, false);
            for (int i = 0; i < segments; i++)
            {
                float t0 = i / (float)segments;
                float t1 = (i + 1) / (float)segments;
                Vector3 p0 = Vector3.Lerp(start, end, t0) - Vector3.up * (4f * sag * t0 * (1f - t0));
                Vector3 p1 = Vector3.Lerp(start, end, t1) - Vector3.up * (4f * sag * t1 * (1f - t1));
                Vector3 delta = p1 - p0;
                float len = delta.magnitude;
                if (len < 1e-4f) continue;

                GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = $"Seg_{i}";
                seg.transform.SetParent(cableRoot.transform, false);
                seg.transform.position = (p0 + p1) * 0.5f;
                seg.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                seg.transform.localScale = new Vector3(thickness, thickness, len + 0.02f);
                Object.DestroyImmediate(seg.GetComponent<Collider>());
                MeshRenderer mr = seg.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    if (cableMat != null) mr.sharedMaterial = cableMat;
                }
            }
        }

        // ==================== PROCEDURAL PNG TEXTURES ====================

        private static void EnsureProceduralTextures()
        {
            if (!AssetDatabase.IsValidFolder(CadeiaTexturesFolder))
            {
                Directory.CreateDirectory(CadeiaTexturesFolder);
                AssetDatabase.Refresh();
            }

            GenerateWeatheredConcreteTexture(FloorTexPath, new Color(0.28f, 0.34f, 0.39f, 1f), stainStrength: 0.16f, withPits: true);
            GenerateWeatheredConcreteTexture(WallTexPath, new Color(0.32f, 0.38f, 0.43f, 1f), stainStrength: 0.14f, withPits: false);
            GenerateRustMetalTexture(RustTexPath);
            GeneratePrisonGraffitiTexture(GraffitiTexPath);
            AssetDatabase.Refresh();
        }

        private static void GenerateWeatheredConcreteTexture(string assetPath, Color baseCol, float stainStrength, bool withPits)
        {
            int s = 512;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float n1 = Mathf.PerlinNoise(x * 0.025f, y * 0.025f);
                    float n2 = Mathf.PerlinNoise(x * 0.11f + 19.3f, y * 0.11f + 4.7f);
                    float streak = Mathf.PerlinNoise(x * 0.08f, y * 0.012f);
                    float delta = ((n1 * 0.55f + n2 * 0.25f + streak * 0.20f) - 0.5f) * stainStrength * 2f;
                    float pit = (withPits && n2 < 0.20f && n1 < 0.38f) ? -0.10f : 0f;
                    tex.SetPixel(x, y, new Color(
                        Mathf.Clamp01(baseCol.r + delta + pit),
                        Mathf.Clamp01(baseCol.g + delta + pit),
                        Mathf.Clamp01(baseCol.b + delta * 1.05f + pit),
                        1f));
                }
            }
            tex.Apply();
            SaveTexturePng(tex, assetPath, wrapRepeat: true);
            Object.DestroyImmediate(tex);
        }

        private static void GenerateRustMetalTexture(string assetPath)
        {
            int s = 256;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            Color darkIron = new Color(0.18f, 0.20f, 0.22f, 1f);
            Color rustA = new Color(0.46f, 0.25f, 0.14f, 1f);
            Color rustB = new Color(0.62f, 0.34f, 0.16f, 1f);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float n1 = Mathf.PerlinNoise(x * 0.06f, y * 0.06f);
                    float n2 = Mathf.PerlinNoise(x * 0.18f + 5.2f, y * 0.09f + 9.1f);
                    float r = n1 * 0.65f + n2 * 0.35f;
                    Color c = r < 0.45f ? Color.Lerp(darkIron, rustA, r / 0.45f) : Color.Lerp(rustA, rustB, (r - 0.45f) / 0.55f);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            SaveTexturePng(tex, assetPath, wrapRepeat: true);
            Object.DestroyImmediate(tex);
        }

        private static void GeneratePrisonGraffitiTexture(string assetPath)
        {
            int w = 512, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));

            // Subtle dark scratched tally marks & distressed spray strokes faithful to left stairwell wall
            Color ink = new Color(0.08f, 0.10f, 0.12f, 0.72f);
            Color scratch = new Color(0.14f, 0.16f, 0.18f, 0.55f);

            // Tally marks (4 vertical + 1 diagonal slash, 3 sets)
            for (int g = 0; g < 3; g++)
            {
                int gx = 56 + g * 66;
                for (int k = 0; k < 4; k++) DrawLine(tex, gx + k * 11, 145, gx + k * 11 + 2, 198, 2.2f, ink);
                DrawLine(tex, gx - 5, 152, gx + 40, 192, 2.5f, ink);
            }

            // Distressed angular wall tag lines ("BLOCO 4" rough scratch style)
            DrawLine(tex, 190, 92, 225, 138, 3.2f, ink);
            DrawLine(tex, 225, 138, 265, 88, 3.0f, ink);
            DrawLine(tex, 260, 118, 315, 126, 2.8f, scratch);
            DrawLine(tex, 290, 74, 345, 142, 3.2f, ink);
            DrawLine(tex, 330, 108, 395, 96, 2.6f, scratch);
            DrawLine(tex, 360, 148, 435, 112, 3.0f, ink);
            DrawLine(tex, 165, 68, 445, 76, 2.0f, scratch);

            tex.Apply();
            SaveTexturePng(tex, assetPath, wrapRepeat: false);
            Object.DestroyImmediate(tex);
        }

        private static void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, float radius, Color col)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) + 1;
            int rCeil = Mathf.CeilToInt(radius);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int cx = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                int cy = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                for (int dy = -rCeil; dy <= rCeil; dy++)
                {
                    for (int dx = -rCeil; dx <= rCeil; dx++)
                    {
                        if (dx * dx + dy * dy <= radius * radius)
                        {
                            int px = cx + dx, py = cy + dy;
                            if (px >= 0 && px < tex.width && py >= 0 && py < tex.height)
                            {
                                tex.SetPixel(px, py, col);
                            }
                        }
                    }
                }
            }
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
                importer.alphaIsTransparency = true;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }
        }

        // ==================== URP MATERIALS FACTORY ====================

        private static CadeiaMaterials EnsureMaterials()
        {
            if (!AssetDatabase.IsValidFolder(CadeiaMaterialsFolder))
            {
                Directory.CreateDirectory(CadeiaMaterialsFolder);
                AssetDatabase.Refresh();
            }

            Texture2D floorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(FloorTexPath);
            Texture2D wallTex = AssetDatabase.LoadAssetAtPath<Texture2D>(WallTexPath);
            Texture2D rustTex = AssetDatabase.LoadAssetAtPath<Texture2D>(RustTexPath);
            Texture2D grafTex = AssetDatabase.LoadAssetAtPath<Texture2D>(GraffitiTexPath);

            return new CadeiaMaterials
            {
                ConcreteFloor = EnsureTexturedLitMaterial("Cadeia_ConcreteFloor.mat", floorTex, new Color(0.86f, 0.90f, 0.94f, 1f), metallic: 0.04f, smoothness: 0.28f, tiling: new Vector2(14f, 4f)),
                ConcreteWall = EnsureTexturedLitMaterial("Cadeia_ConcreteWall.mat", wallTex, new Color(0.84f, 0.89f, 0.94f, 1f), metallic: 0f, smoothness: 0.16f, tiling: new Vector2(4f, 2f)),
                ConcreteDark = EnsureTexturedLitMaterial("Cadeia_ConcreteDark.mat", wallTex, new Color(0.46f, 0.52f, 0.58f, 1f), metallic: 0f, smoothness: 0.14f, tiling: new Vector2(4f, 2f)),
                CellInteriorWall = EnsureLitMaterial("Cadeia_CellInteriorWall.mat", new Color(0.24f, 0.30f, 0.35f, 1f), metallic: 0f, smoothness: 0.12f),
                CellInteriorDark = EnsureLitMaterial("Cadeia_CellInteriorDark.mat", new Color(0.11f, 0.14f, 0.18f, 1f), metallic: 0f, smoothness: 0.08f),
                IronBars = EnsureLitMaterial("Cadeia_IronBars.mat", new Color(0.16f, 0.18f, 0.20f, 1f), metallic: 0.72f, smoothness: 0.36f),
                RustMetal = EnsureTexturedLitMaterial("Cadeia_RustMetal.mat", rustTex, Color.white, metallic: 0.45f, smoothness: 0.24f, tiling: new Vector2(2f, 2f)),
                SteelLight = EnsureLitMaterial("Cadeia_SteelLight.mat", new Color(0.56f, 0.60f, 0.64f, 1f), metallic: 0.76f, smoothness: 0.48f),
                SteelDark = EnsureLitMaterial("Cadeia_SteelDark.mat", new Color(0.22f, 0.25f, 0.29f, 1f), metallic: 0.62f, smoothness: 0.34f),
                Mattress = EnsureLitMaterial("Cadeia_Mattress.mat", new Color(0.44f, 0.46f, 0.44f, 1f), metallic: 0f, smoothness: 0.10f),
                Pillow = EnsureLitMaterial("Cadeia_Pillow.mat", new Color(0.68f, 0.68f, 0.64f, 1f), metallic: 0f, smoothness: 0.10f),
                LampFixture = EnsureLitMaterial("Cadeia_LampFixture.mat", new Color(0.20f, 0.23f, 0.26f, 1f), metallic: 0.65f, smoothness: 0.40f),
                FluorescentGlow = EnsureEmissiveLitMaterial("Cadeia_FluorescentGlow.mat", new Color(0.86f, 0.95f, 1.0f, 1f), new Color(0.76f, 0.92f, 1.0f, 1f) * 2.8f, smoothness: 0.45f),
                SkylightGlow = EnsureEmissiveLitMaterial("Cadeia_SkylightGlow.mat", new Color(0.62f, 0.80f, 0.96f, 1f), new Color(0.48f, 0.74f, 0.98f, 1f) * 2.4f, smoothness: 0.50f),
                SkylightBeam = EnsureTransparentUnlitMaterial("Cadeia_SkylightBeam.mat", new Color(0.54f, 0.76f, 0.98f, 0.052f)),
                WetPuddle = EnsureTransparentLitMaterial("Cadeia_WetPuddle.mat", new Color(0.03f, 0.05f, 0.08f, 0.52f), metallic: 0.42f, smoothness: 0.96f),
                FloorJoint = EnsureLitMaterial("Cadeia_FloorJoint.mat", new Color(0.11f, 0.13f, 0.16f, 1f), metallic: 0f, smoothness: 0.12f),
                GraffitiDecal = EnsureTransparentTexturedMaterial("Cadeia_GraffitiDecal.mat", grafTex, Color.white),
                CableBlack = EnsureLitMaterial("Cadeia_CableBlack.mat", new Color(0.08f, 0.09f, 0.10f, 1f), metallic: 0.15f, smoothness: 0.25f),
                NumberPlate = EnsureLitMaterial("Cadeia_NumberPlate.mat", new Color(0.20f, 0.22f, 0.24f, 1f), metallic: 0.50f, smoothness: 0.30f),
                SkyboxDarkIndoor = EnsureProceduralIndoorSkybox("Cadeia_IndoorSkybox.mat")
            };
        }

        private static Material EnsureProceduralIndoorSkybox(string fileName)
        {
            string path = $"{CadeiaMaterialsFolder}/{fileName}";
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
            if (mat.HasProperty("_SkyTint")) mat.SetColor("_SkyTint", new Color(0.08f, 0.11f, 0.16f, 1f));
            if (mat.HasProperty("_GroundColor")) mat.SetColor("_GroundColor", new Color(0.05f, 0.07f, 0.10f, 1f));
            if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", 0.25f);
            if (mat.HasProperty("_AtmosphereThickness")) mat.SetFloat("_AtmosphereThickness", 0.40f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EnsureLitMaterial(string fileName, Color baseColor, float metallic, float smoothness)
        {
            string path = $"{CadeiaMaterialsFolder}/{fileName}";
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
                if (mat.HasProperty("_BaseMap")) { mat.SetTexture("_BaseMap", tex); mat.SetTextureScale("_BaseMap", tiling); }
                if (mat.HasProperty("_MainTex")) { mat.SetTexture("_MainTex", tex); mat.SetTextureScale("_MainTex", tiling); }
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
            ApplyTransparentFlags(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EnsureTransparentTexturedMaterial(string fileName, Texture2D tex, Color tint)
        {
            Material mat = EnsureTexturedLitMaterial(fileName, tex, tint, metallic: 0f, smoothness: 0.12f, tiling: Vector2.one);
            ApplyTransparentFlags(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EnsureTransparentUnlitMaterial(string fileName, Color tint)
        {
            string path = $"{CadeiaMaterialsFolder}/{fileName}";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
            ApplyTransparentFlags(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void ApplyTransparentFlags(Material mat)
        {
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        // ==================== SHARED HELPERS ====================

        private static void ConfigureBreakableComponent(GameObject root, GameObject intactVisual, Collider intactCol, Transform poolTransform, StagePropMaterialType matType, int integrity, float radiusX, float zTolerance)
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

        private static void ConfigureThrowableComponent(GameObject root, GameObject intactVisual, Collider propCol, Rigidbody rb, Transform poolTransform, string promptLabel, StagePropMaterialType matType, int damage, bool causesKnockdown, bool shatterOnStage)
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

        private static void CreateDebrisShard(string name, PrimitiveType type, Transform poolParent, Vector3 localPos, Vector3 localRot, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = StagePhysicsLayers.StageDebrisLayer;
            go.transform.SetParent(poolParent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localRot);
            go.transform.localScale = localScale;
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;

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

        private static void CreateWorldTmp(string name, Transform parent, string text, Vector3 localPos, float fontSize, Color color, FontStyles style)
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
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(1.2f, 0.5f);
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
