using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UnDFight.Editor
{
    /// <summary>
    /// Idempotent Editor utility for Phase 15.2: revitalizes Arena_Favela.unity with architectural
    /// details inspired by Docs/Referencias/mapa.jpg, new PhysX v2 props (clothesline HingeJoint,
    /// hanging lantern HingeJoint, dynamic cones), full arena corner closure, hillside antennas/tanks,
    /// and complete Day (Sunset) / Clean Night / Rainy Night visual layers with zero Z-fighting.
    /// </summary>
    public static class ArenaFavelaUpgradeEditor
    {
        public const string MaterialsFolder = "Assets/UnDFight/Art/Materials/StageFavela";
        public const string StagePropsPrefabFolder = StagePhysicsSetupEditor.StagePropsPrefabFolder;

        public const string HangingClothPrefabPath = StagePropsPrefabFolder + "/Prop_HangingCloth.prefab";
        public const string HangingLanternPrefabPath = StagePropsPrefabFolder + "/Prop_HangingLantern.prefab";
        public const string DynamicConePrefabPath = StagePropsPrefabFolder + "/Prop_DynamicCone.prefab";

        public const string DetailsContainerName = "FavelaDetails_Phase15_2";
        public const string HillsideExtrasContainerName = "FavelaHillsideExtras_Phase15_2";
        public const string HillsideRightFillName = "FavelaHillsideRightFill";
        public const string BayHorizonDetailsName = "FavelaBayHorizon_Phase15_2_1";
        public const string BayShorelineLightsName = "FavelaBayShorelineLights";
        public const string WeatherCloudsPrefix = "FavelaWeatherClouds";
        public const string NightLightsAndWindowsName = "FavelaNightLightsAndWindows";
        public const string HillsideNightWindowsName = "FavelaNightWindows_Hillside";
        public const string ClearNightSkyName = "FavelaClearNightSky";
        public const string RainPuddlesName = "FavelaRainPuddles";
        public const string SunsetHaloName = "FavelaSunsetHalo";

        private struct MaterialPalette
        {
            public Material RedBrick;
            public Material StuccoOchre;
            public Material StuccoCoral;
            public Material StuccoGreen;
            public Material StuccoWhite;
            public Material StuccoLightBlue;
            public Material SidewalkConcrete;
            public Material PoleConcrete;
            public Material RoofMetal;
            public Material BlackCable;
            public Material WoodSign;
            public Material SteelShutter;
            public Material PlasticRed;
            public Material PlasticYellow;
            public Material PlasticBlueTank;
            public Material BottleAmber;
            public Material BottleGreen;
            public Material PalmBark;
            public Material PalmLeaf;
            public Material ClothesWhite;
            public Material ClothesOrange;
            public Material ClothesBlue;
            public Material WarmEstufaGlow;
            public Material StreetLampGlow;
            public Material SunGlow;
            public Material DarkInterior;
            public Material WaterBay;
            public Material DistantMountain;
            public Material DistantMountainFar;

            // Phase 15.2 / 15.2.1 dedicated URP assets
            public Material MetalDark;
            public Material NightWindowGlow;
            public Material NightWindowWarmAlt;
            public Material RainPuddle;
            public Material MoonGlow;
            public Material StarGlow;
            public Material PosterCyan;
            public Material PosterYellow;
            public Material PosterRed;
            public Material PosterWhite;
            public Material GraffitiMagenta;
            public Material GraffitiTeal;
            public Material GraffitiGold;
            public Material ConeOrange;
            public Material ConeStripeWhite;
            public Material LanternBulbGlow;
            public Material TerracottaPot;
            public Material ShorelineLightGlow;
            public StageFramingStandards.CloudMaterials Clouds;
        }

        [MenuItem("UnDFight/Setup/Upgrade Arena Favela Visuals & Physics", false, 121)]
        public static void UpgradeArenaFavela()
        {
            StagePhysicsSetupEditor.EnsurePhysicsLayersAndMatrix();
            StagePhysicsSetupEditor.ConfigureStageDataWeatherFlags();

            MaterialPalette palette = EnsureMaterials();
            CreateOrUpdatePhase15_2PropPrefabs(palette);

            string previousScenePath = SceneManager.GetActiveScene().path;
            Scene scene = EditorSceneManager.OpenScene(StagePhysicsSetupEditor.ArenaFavelaScenePath, OpenSceneMode.Single);

            ApplyPhase15_2DetailsToOpenArenaFavela(scene, palette);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!string.IsNullOrEmpty(previousScenePath) &&
                previousScenePath != StagePhysicsSetupEditor.ArenaFavelaScenePath &&
                File.Exists(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
            else
            {
                EditorSceneManager.OpenScene(PlayFromMainMenuEditor.MainMenuScenePath, OpenSceneMode.Single);
            }

            Debug.Log("[UnDFight] Fase 15.2 applied: Arena_Favela revitalized with Bar do Jorge details, arena corner closure, physical clothesline & lantern HingeJoints, dynamic cones, hillside night windows, clear night sky, and reflective rain puddles.");
        }

        public static void CreateOrUpdatePhase15_2PropPrefabs()
        {
            CreateOrUpdatePhase15_2PropPrefabs(EnsureMaterials());
        }

        public static void ApplyPhase15_2DetailsToOpenArenaFavela(Scene scene)
        {
            MaterialPalette palette = EnsureMaterials();
            CreateOrUpdatePhase15_2PropPrefabs(palette);
            ApplyPhase15_2DetailsToOpenArenaFavela(scene, palette);
        }

        private static void ApplyPhase15_2DetailsToOpenArenaFavela(Scene scene, MaterialPalette pal)
        {
            // Clean up any legacy or duplicated containers (including inactive objects!)
            DestroyAllMatchingInScene(scene, "FavelaNightLights");
            DestroyAllMatchingInScene(scene, NightLightsAndWindowsName);
            DestroyAllMatchingInScene(scene, DetailsContainerName);
            DestroyAllMatchingInScene(scene, HillsideExtrasContainerName);
            DestroyAllMatchingInScene(scene, HillsideRightFillName);
            DestroyAllMatchingInScene(scene, BayHorizonDetailsName);
            DestroyAllMatchingInScene(scene, BayShorelineLightsName);
            DestroyAllMatchingInScene(scene, $"{WeatherCloudsPrefix}_DayClouds");
            DestroyAllMatchingInScene(scene, $"{WeatherCloudsPrefix}_ClearNightClouds");
            DestroyAllMatchingInScene(scene, $"{WeatherCloudsPrefix}_RainClouds");
            DestroyAllMatchingInScene(scene, HillsideNightWindowsName);
            DestroyAllMatchingInScene(scene, ClearNightSkyName);
            DestroyAllMatchingInScene(scene, RainPuddlesName);
            DestroyAllMatchingInScene(scene, SunsetHaloName);

            // 0. Phase 15.2.1: Enforce foreground floor coverage & width (eliminates bottom black bar on max zoom-out)
            ApplyForegroundFloorFramingStandards(scene);

            // Clean up Phase 15.2 interactive prop instances inside InteractiveStageProps so re-runs never duplicate
            GameObject propsGroup = FindInSceneByName(scene, "InteractiveStageProps");
            if (propsGroup == null)
            {
                propsGroup = new GameObject("InteractiveStageProps");
            }
            RemoveChildIfExists(propsGroup.transform, "Hanging_Cloth_White");
            RemoveChildIfExists(propsGroup.transform, "Hanging_Cloth_Orange");
            RemoveChildIfExists(propsGroup.transform, "Hanging_Cloth_Blue");
            RemoveChildIfExists(propsGroup.transform, "Hanging_RightLantern");
            RemoveChildIfExists(propsGroup.transform, "Dynamic_Cone_Left");
            RemoveChildIfExists(propsGroup.transform, "Dynamic_Cone_Right");

            // Hide legacy static upper-bar clothesline shirts so the new PhysX HingeJoint clothesline replaces them cleanly
            string[] legacyStaticShirts =
            {
                "Clothesline_Wire",
                "Laundry_WhiteShirt_Body", "Laundry_WhiteShirt_SleeveL", "Laundry_WhiteShirt_SleeveR",
                "Laundry_OrangeShirt_Body", "Laundry_OrangeShirt_SleeveL", "Laundry_OrangeShirt_SleeveR",
                "Laundry_BlueTowel_Front", "Laundry_BlueTowel_Top"
            };
            foreach (string staticName in legacyStaticShirts)
            {
                GameObject staticGo = FindInSceneByName(scene, staticName);
                if (staticGo != null)
                {
                    staticGo.SetActive(false);
                }
            }

            // 1. Build main architectural & urban details container
            var detailsRoot = new GameObject(DetailsContainerName);
            List<Renderer> extraWetRenderers = new List<Renderer>();

            BuildBarDoJorgeDetails(detailsRoot.transform, pal);
            BuildUrbanStreetAndStairDetails(detailsRoot.transform, pal);
            BuildArenaCornerClosure(detailsRoot.transform, pal, extraWetRenderers);
            BuildRightRampFoliagePoleAndCables(detailsRoot.transform, pal);

            // 2. Build hillside antennas, satellite dishes, extra blue tanks & right-side hillside fill inside parallax layer
            GameObject hillsideLayer = FindInSceneByName(scene, "LayerD_Parallax_HillsideFavela");
            var rightFillLitWindowSpecs = new List<(string Name, Vector3 WorldPos, Vector3 Size, bool AltWarm)>();
            if (hillsideLayer != null)
            {
                var hillsideExtras = new GameObject(HillsideExtrasContainerName);
                hillsideExtras.transform.SetParent(hillsideLayer.transform, false);
                BuildHillsideExtras(hillsideLayer.transform, hillsideExtras.transform, pal);

                var rightFillRoot = new GameObject(HillsideRightFillName);
                rightFillRoot.transform.SetParent(hillsideLayer.transform, false);
                BuildHillsideRightFill(rightFillRoot.transform, pal, rightFillLitWindowSpecs);
            }

            // 3. Instantiate new Phase 15.2 PhysX v2 interactive props inside InteractiveStageProps
            InstantiatePhase15_2InteractiveProps(propsGroup.transform, pal);

            // 4. Build Bay/Sugarloaf horizon framing, weather-specific visual groups & clouds
            GameObject bayLayer = FindInSceneByName(scene, "LayerD_Parallax_BayAndSugarloaf");
            GameObject shorelineLights = BuildBayAndSugarloafHorizon(scene, bayLayer, pal);

            GameObject sunDisc = FindInSceneByName(scene, "Sunset_SunDisc");
            GameObject sunsetHalo = BuildSunsetHalo(bayLayer, sunDisc, pal);
            GameObject clearNightSky = BuildClearNightSky(bayLayer, pal);
            GameObject nightLightsAndWindows = BuildNightLightsAndWindows(pal);
            GameObject hillsideNightWindows = BuildHillsideNightWindows(hillsideLayer, pal, rightFillLitWindowSpecs);
            GameObject rainPuddles = BuildRainPuddles(pal);

            StageFramingStandards.WeatherCloudLayers cloudLayers = StageFramingStandards.BuildWeatherCloudLayer(
                bayLayer != null ? bayLayer.transform : null,
                WeatherCloudsPrefix,
                regionCenter: new Vector3(16.0f, 15.0f, 47.5f),
                regionSize: new Vector3(44.0f, 4.2f, 5.5f),
                dayMat: pal.Clouds.DayCloud,
                clearNightMat: pal.Clouds.ClearNightCloud,
                stormMat: pal.Clouds.StormCloud,
                dayCount: 7,
                clearNightCount: 5,
                stormCount: 10);

            // Set default editor/scene visibility to Day (Sunset)
            if (sunDisc != null) sunDisc.SetActive(true);
            if (sunsetHalo != null) sunsetHalo.SetActive(true);
            if (cloudLayers.DayClouds != null) cloudLayers.DayClouds.SetActive(true);
            if (clearNightSky != null) clearNightSky.SetActive(false);
            if (cloudLayers.ClearNightClouds != null) cloudLayers.ClearNightClouds.SetActive(false);
            if (nightLightsAndWindows != null) nightLightsAndWindows.SetActive(false);
            if (hillsideNightWindows != null) hillsideNightWindows.SetActive(false);
            if (shorelineLights != null) shorelineLights.SetActive(false);
            if (rainPuddles != null) rainPuddles.SetActive(false);
            if (cloudLayers.RainClouds != null) cloudLayers.RainClouds.SetActive(false);

            // 5. Wire StageWeatherController & refresh StageInteractableManager
            WireWeatherControllerAndManager(
                scene,
                sunDisc,
                sunsetHalo,
                clearNightSky,
                nightLightsAndWindows,
                hillsideNightWindows,
                shorelineLights,
                rainPuddles,
                cloudLayers,
                extraWetRenderers);
        }

        private static void ApplyForegroundFloorFramingStandards(Scene scene)
        {
            // Keep exact seam at Z = -2.60 against CobblestoneStreet (zero overlap / zero Z-fighting),
            // extending foreground floor continuously to Z = -22.50 and width X = 68.0m.
            GameObject sandFront = FindInSceneByName(scene, "SandShoulder_Front");
            if (sandFront != null)
            {
                StageFramingStandards.EnsureForegroundFloorCoverage(
                    sandFront.transform,
                    backEdgeZ: -2.60f,
                    targetFrontZ: -22.5f,
                    minWidthX: 68.0f);
            }

            // Widen street & back shoulder to 68.0m so ultrawide max zoom-out never clips laterally
            string[] wideStreetParts = { "CobblestoneStreet", "SandShoulder_Back" };
            foreach (string partName in wideStreetParts)
            {
                GameObject part = FindInSceneByName(scene, partName);
                if (part != null)
                {
                    Vector3 s = part.transform.localScale;
                    s.x = Mathf.Max(s.x, 68.0f);
                    part.transform.localScale = s;
                }
            }
        }

        private static void BuildRightRampFoliagePoleAndCables(Transform parent, MaterialPalette pal)
        {
            var rampTopGroup = new GameObject("RightRamp_FoliagePoleAndCables");
            rampTopGroup.transform.SetParent(parent, false);

            // Extra tropical palms & lush foliage flanking the ramp top and behind RightSobrado
            BuildTropicalPalm("PalmTree_RampTopLeft", rampTopGroup.transform, new Vector3(3.10f, 2.25f, 11.40f), 4.10f, pal);
            BuildTropicalPalm("PalmTree_RampTopRight", rampTopGroup.transform, new Vector3(6.85f, 2.55f, 12.10f), 4.65f, pal);
            BuildTropicalPalm("PalmTree_BehindRightSobrado", rampTopGroup.transform, new Vector3(9.55f, 1.35f, 8.90f), 5.10f, pal);
            CreateSphere("TropicalBush_RampTopL", rampTopGroup.transform, new Vector3(3.30f, 2.45f, 10.60f), Vector3.zero, new Vector3(1.15f, 0.85f, 1.00f), pal.PalmLeaf);
            CreateSphere("TropicalBush_RampTopR", rampTopGroup.transform, new Vector3(6.65f, 2.75f, 11.45f), Vector3.zero, new Vector3(1.30f, 0.92f, 1.10f), pal.PalmLeaf);

            // High secondary utility pole at the top of the ramp with warm street lamp head
            CreateCyl("UtilityPole_RampTop", rampTopGroup.transform, new Vector3(6.25f, 4.95f, 12.35f), Vector3.zero, new Vector3(0.15f, 2.55f, 0.15f), pal.PoleConcrete);
            CreateBox("RampTopPole_Crossbar", rampTopGroup.transform, new Vector3(6.25f, 7.25f, 12.35f), Vector3.zero, new Vector3(0.95f, 0.08f, 0.09f), pal.PoleConcrete);
            CreateBox("RampTopPole_LampArm", rampTopGroup.transform, new Vector3(5.72f, 6.75f, 12.35f), new Vector3(0f, 0f, -14f), new Vector3(1.05f, 0.06f, 0.06f), pal.MetalDark);
            CreateBox("RampTopPole_LampHead", rampTopGroup.transform, new Vector3(5.26f, 6.64f, 12.35f), Vector3.zero, new Vector3(0.30f, 0.09f, 0.16f), pal.MetalDark);

            // Overhead catenary electrical cables crossing the upper-right sky toward RightSobrado & ramp poles
            BuildCatenaryCable("OverheadCable_RightTop_0", rampTopGroup.transform, new Vector3(-2.35f, 6.32f, 2.45f), new Vector3(9.80f, 5.85f, 3.60f), sag: 0.42f, segments: 9, thickness: 0.022f, pal.BlackCable);
            BuildCatenaryCable("OverheadCable_RightTop_1", rampTopGroup.transform, new Vector3(-2.35f, 5.95f, 2.45f), new Vector3(6.25f, 5.30f, 6.80f), sag: 0.34f, segments: 8, thickness: 0.020f, pal.BlackCable);
            BuildCatenaryCable("OverheadCable_RightTop_2", rampTopGroup.transform, new Vector3(6.25f, 5.30f, 6.80f), new Vector3(6.25f, 7.20f, 12.35f), sag: 0.28f, segments: 7, thickness: 0.018f, pal.BlackCable);
            BuildCatenaryCable("OverheadCable_RightTop_3", rampTopGroup.transform, new Vector3(6.25f, 5.20f, 6.80f), new Vector3(12.75f, 6.18f, 2.45f), sag: 0.32f, segments: 7, thickness: 0.020f, pal.BlackCable);
        }

        private static void BuildCatenaryCable(
            string name,
            Transform parent,
            Vector3 start,
            Vector3 end,
            float sag,
            int segments,
            float thickness,
            Material cableMat)
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

                Collider col = seg.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                MeshRenderer mr = seg.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    if (cableMat != null) mr.sharedMaterial = cableMat;
                }
            }
        }

        private static void BuildHillsideRightFill(
            Transform rightFillRoot,
            MaterialPalette pal,
            List<(string Name, Vector3 WorldPos, Vector3 Size, bool AltWarm)> litWindowSpecs)
        {
            // 14 terraced hillside houses framing the ramp vista (x = 0.6..13.6, y = 2.9..10.4, z = 11.2..25.6)
            // Leaves an intentional scenic opening above the ramp center for Sugarloaf, Bay, Sun/Moon & Clouds.
            (Vector3 pos, Vector3 size, Material wallMat, bool hasTank, bool hasAntenna, bool hasParapet, bool hasLaundry, int litMask)[] specs =
            {
                // Left-of-ramp terraced slope (closing gap between MidBrickHouse and Sugarloaf)
                (new Vector3(1.15f, 4.15f, 12.60f), new Vector3(2.70f, 2.50f, 2.40f), pal.StuccoCoral,     true,  false, true,  false, 3),
                (new Vector3(3.35f, 3.45f, 13.80f), new Vector3(2.40f, 2.20f, 2.20f), pal.StuccoLightBlue, false, true,  true,  true,  2),
                (new Vector3(2.05f, 6.25f, 16.40f), new Vector3(2.90f, 2.70f, 2.60f), pal.RedBrick,        true,  true,  false, false, 3),
                (new Vector3(4.20f, 5.25f, 17.80f), new Vector3(2.55f, 2.35f, 2.40f), pal.StuccoOchre,     false, false, true,  false, 1),
                (new Vector3(1.65f, 8.45f, 20.80f), new Vector3(3.10f, 2.85f, 2.70f), pal.StuccoWhite,     true,  true,  true,  false, 3),
                (new Vector3(4.35f, 7.65f, 22.40f), new Vector3(2.85f, 2.55f, 2.60f), pal.StuccoGreen,     true,  false, false, false, 2),

                // Lower ramp-base houses (keep skyline open above y ~ 4.6 for Bay + Sugarloaf!)
                (new Vector3(5.85f, 3.15f, 15.60f), new Vector3(2.25f, 1.95f, 2.15f), pal.StuccoCoral,     false, true,  true,  false, 1),
                (new Vector3(7.35f, 3.65f, 17.20f), new Vector3(2.45f, 2.20f, 2.30f), pal.StuccoWhite,     true,  false, false, false, 2),

                // Right-of-ramp terraced slope behind palm trees & RightSobrado (closes empty sky above P2 Tiago)
                (new Vector3(8.95f, 4.60f, 14.40f), new Vector3(2.75f, 2.60f, 2.50f), pal.StuccoOchre,     true,  false, true,  true,  3),
                (new Vector3(11.45f, 4.95f, 15.20f), new Vector3(2.90f, 2.75f, 2.55f), pal.StuccoLightBlue, false, true,  true,  false, 2),
                (new Vector3(9.45f, 6.95f, 18.80f), new Vector3(2.95f, 2.80f, 2.65f), pal.RedBrick,        true,  true,  false, false, 3),
                (new Vector3(12.20f, 7.30f, 20.00f), new Vector3(3.10f, 2.85f, 2.70f), pal.StuccoGreen,     true,  false, true,  false, 1),
                (new Vector3(9.90f, 9.35f, 23.80f), new Vector3(3.20f, 2.90f, 2.80f), pal.StuccoCoral,     false, true,  true,  false, 3),
                (new Vector3(12.85f, 9.70f, 24.80f), new Vector3(3.30f, 2.95f, 2.85f), pal.StuccoWhite,     true,  false, false, false, 2)
            };

            for (int i = 0; i < specs.Length; i++)
            {
                var sp = specs[i];
                var houseGo = new GameObject($"RightFillHouse_{i}");
                houseGo.transform.SetParent(rightFillRoot, false);

                CreateBox("Body", houseGo.transform, sp.pos, Vector3.zero, sp.size, sp.wallMat);

                float roofY = sp.pos.y + sp.size.y * 0.5f + 0.07f;
                float roofTilt = (i % 2 == 0) ? 5.5f : -4.5f;
                CreateBox("Roof", houseGo.transform, new Vector3(sp.pos.x, roofY, sp.pos.z), new Vector3(roofTilt, 0f, 0f), new Vector3(sp.size.x * 1.07f, 0.12f, sp.size.z * 1.07f), pal.RoofMetal);

                float frontZ = sp.pos.z - sp.size.z * 0.5f;
                float winW = sp.size.x * 0.21f;
                float winH = sp.size.y * 0.25f;
                float winY = sp.pos.y + sp.size.y * 0.10f;
                float dx = sp.size.x * 0.23f;

                // Base dark window cutouts (offset -0.025m from house facade)
                CreateBox("WinDark_L", houseGo.transform, new Vector3(sp.pos.x - dx, winY, frontZ - 0.025f), Vector3.zero, new Vector3(winW, winH, 0.03f), pal.DarkInterior);
                CreateBox("WinDark_R", houseGo.transform, new Vector3(sp.pos.x + dx, winY, frontZ - 0.025f), Vector3.zero, new Vector3(winW, winH, 0.03f), pal.DarkInterior);

                // Register warm lit window overlays (offset -0.065m from house facade, >= 0.035m ahead of dark panes -> zero Z-fighting)
                if ((sp.litMask & 1) != 0)
                {
                    litWindowSpecs.Add(($"RightFillNightWin_{i}_L", new Vector3(sp.pos.x - dx, winY, frontZ - 0.065f), new Vector3(winW * 0.88f, winH * 0.88f, 0.022f), i % 2 == 0));
                }
                if ((sp.litMask & 2) != 0)
                {
                    litWindowSpecs.Add(($"RightFillNightWin_{i}_R", new Vector3(sp.pos.x + dx, winY, frontZ - 0.065f), new Vector3(winW * 0.88f, winH * 0.88f, 0.022f), i % 2 != 0));
                }

                if (sp.hasParapet)
                {
                    CreateBox("Parapet", houseGo.transform, new Vector3(sp.pos.x, roofY + 0.28f, frontZ + 0.09f), Vector3.zero, new Vector3(sp.size.x * 0.96f, 0.48f, 0.14f), pal.RedBrick);
                }
                if (sp.hasTank)
                {
                    BuildWaterTank($"Tank_{i}", houseGo.transform, new Vector3(sp.pos.x - sp.size.x * 0.22f, roofY + 0.05f, sp.pos.z - 0.10f), 0.78f, pal);
                }
                if (sp.hasAntenna)
                {
                    BuildTvAntenna($"Ant_{i}", houseGo.transform, new Vector3(sp.pos.x + sp.size.x * 0.30f, roofY + 0.05f, sp.pos.z - 0.15f), 1.15f, pal);
                }
                if (sp.hasLaundry)
                {
                    // Distant rooftop clothesline silhouette
                    float ly = roofY + 0.72f;
                    CreateBox("DistantClothesline", houseGo.transform, new Vector3(sp.pos.x, ly, frontZ + 0.12f), Vector3.zero, new Vector3(sp.size.x * 0.76f, 0.025f, 0.025f), pal.BlackCable);
                    CreateBox("DistantCloth_0", houseGo.transform, new Vector3(sp.pos.x - 0.42f, ly - 0.22f, frontZ + 0.12f), Vector3.zero, new Vector3(0.32f, 0.38f, 0.04f), pal.ClothesWhite);
                    CreateBox("DistantCloth_1", houseGo.transform, new Vector3(sp.pos.x + 0.02f, ly - 0.20f, frontZ + 0.12f), Vector3.zero, new Vector3(0.30f, 0.34f, 0.04f), pal.ClothesOrange);
                    CreateBox("DistantCloth_2", houseGo.transform, new Vector3(sp.pos.x + 0.44f, ly - 0.24f, frontZ + 0.12f), Vector3.zero, new Vector3(0.28f, 0.40f, 0.04f), pal.ClothesBlue);
                }
            }
        }

        private static GameObject BuildBayAndSugarloafHorizon(Scene scene, GameObject bayLayer, MaterialPalette pal)
        {
            if (bayLayer == null)
            {
                return null;
            }

            // Reposition & scale iconic Sugarloaf + Morro da Urca + Coastal Ridge so Sugarloaf rises clearly
            // in the scenic opening above the ramp, faithful to Docs/Referencias/mapa.jpg
            GameObject paoDeAcucar = FindInSceneByName(scene, "Morro_PaoDeAcucar");
            if (paoDeAcucar != null)
            {
                paoDeAcucar.transform.position = new Vector3(16.8f, 3.35f, 45.5f);
                paoDeAcucar.transform.localScale = new Vector3(5.6f, 11.2f, 4.2f);
            }

            GameObject morroDaUrca = FindInSceneByName(scene, "Morro_DaUrca");
            if (morroDaUrca != null)
            {
                morroDaUrca.transform.position = new Vector3(22.6f, 3.35f, 48.0f);
                morroDaUrca.transform.localScale = new Vector3(6.2f, 7.6f, 4.6f);
            }

            GameObject coastalRight = FindInSceneByName(scene, "CoastalRidge_Right");
            if (coastalRight != null)
            {
                coastalRight.transform.position = new Vector3(31.0f, 3.35f, 51.0f);
                coastalRight.transform.localScale = new Vector3(11.0f, 6.6f, 5.0f);
            }

            var horizonRoot = new GameObject(BayHorizonDetailsName);
            horizonRoot.transform.SetParent(bayLayer.transform, false);

            // Visible Guanabara Bay waterline sitting cleanly at the foot of Sugarloaf as viewed from the hillside
            GameObject waterSheet = CreateBox("BayHorizon_WaterSheet", horizonRoot.transform, new Vector3(17.5f, 3.48f, 38.5f), Vector3.zero, new Vector3(56.0f, 0.12f, 23.0f), pal.WaterBay);
            DisableShadows(waterSheet);

            // Two-tone coastal mountain ridges closing the distant horizon smoothly left & right
            CreateSphere("CoastalRidge_MidLeft", horizonRoot.transform, new Vector3(8.8f, 3.30f, 49.0f), Vector3.zero, new Vector3(14.5f, 8.2f, 6.0f), pal.DistantMountain);
            CreateSphere("CoastalRidge_FarBackdropA", horizonRoot.transform, new Vector3(19.5f, 3.20f, 53.5f), Vector3.zero, new Vector3(22.0f, 9.6f, 6.5f), pal.DistantMountainFar);
            CreateSphere("CoastalRidge_FarBackdropB", horizonRoot.transform, new Vector3(33.0f, 3.20f, 54.0f), Vector3.zero, new Vector3(20.0f, 8.8f, 6.5f), pal.DistantMountainFar);

            // Distant city/shoreline lights along the bay waterline (active in Night & RainyNight)
            var shoreLights = new GameObject(BayShorelineLightsName);
            shoreLights.transform.SetParent(bayLayer.transform, false);
            for (int i = 0; i < 18; i++)
            {
                float t = i / 17f;
                float lx = Mathf.Lerp(9.5f, 31.5f, t);
                float ly = 3.74f + (i % 3) * 0.13f;
                float lz = 43.2f + (i % 4) * 0.85f;
                float sz = (i % 4 == 0) ? 0.34f : 0.24f;
                GameObject dot = CreateSphere($"ShoreLight_{i}", shoreLights.transform, new Vector3(lx, ly, lz), Vector3.zero, new Vector3(sz * 1.4f, sz, 0.18f), pal.ShorelineLightGlow);
                DisableShadows(dot);
            }

            return shoreLights;
        }

        private static void BuildBarDoJorgeDetails(Transform parent, MaterialPalette pal)
        {
            var barGroup = new GameObject("BarDoJorge_Details");
            barGroup.transform.SetParent(parent, false);

            // Corrugated metal awning / canopy above Bar do Jorge sign (Y=3.26 sloping to 3.12, Z=2.06..2.70)
            var awningRoot = new GameObject("Bar_CorrugatedAwning");
            awningRoot.transform.SetParent(barGroup.transform, false);
            CreateBox("Awning_BackBar", awningRoot.transform, new Vector3(-6.40f, 3.30f, 2.74f), Vector3.zero, new Vector3(4.95f, 0.06f, 0.07f), pal.MetalDark);
            CreateBox("Awning_FrontTrim", awningRoot.transform, new Vector3(-6.40f, 3.10f, 2.02f), Vector3.zero, new Vector3(4.95f, 0.05f, 0.05f), pal.MetalDark);
            CreateBox("Awning_BraceL", awningRoot.transform, new Vector3(-8.55f, 3.04f, 2.40f), new Vector3(32f, 0f, 0f), new Vector3(0.04f, 0.04f, 0.78f), pal.MetalDark);
            CreateBox("Awning_BraceR", awningRoot.transform, new Vector3(-4.25f, 3.04f, 2.40f), new Vector3(32f, 0f, 0f), new Vector3(0.04f, 0.04f, 0.78f), pal.MetalDark);

            int ridgeCount = 12;
            for (int i = 0; i < ridgeCount; i++)
            {
                float x = Mathf.Lerp(-8.72f, -4.08f, i / (float)(ridgeCount - 1));
                float yOff = (i % 2 == 0) ? 0.014f : -0.008f;
                CreateBox(
                    $"Awning_Ridge_{i}",
                    awningRoot.transform,
                    new Vector3(x, 3.21f + yOff, 2.38f),
                    new Vector3(15f, 0f, 0f),
                    new Vector3(0.44f, 0.025f, 0.78f),
                    pal.RoofMetal);
            }

            // Two gooseneck warm lamps illuminating SignBoard_BarDoJorge
            float[] gooseneckXs = { -7.35f, -5.45f };
            for (int i = 0; i < gooseneckXs.Length; i++)
            {
                float gx = gooseneckXs[i];
                var lamp = new GameObject($"Bar_GooseneckLamp_{i}");
                lamp.transform.SetParent(barGroup.transform, false);
                CreateBox("MountPlate", lamp.transform, new Vector3(gx, 3.24f, 2.75f), Vector3.zero, new Vector3(0.10f, 0.10f, 0.04f), pal.MetalDark);
                CreateCyl("ArmHorizontal", lamp.transform, new Vector3(gx, 3.27f, 2.52f), new Vector3(90f, 0f, 0f), new Vector3(0.028f, 0.24f, 0.028f), pal.MetalDark);
                CreateCyl("ArmDrop", lamp.transform, new Vector3(gx, 3.19f, 2.30f), new Vector3(-18f, 0f, 0f), new Vector3(0.026f, 0.10f, 0.026f), pal.MetalDark);
                CreateCyl("Shade", lamp.transform, new Vector3(gx, 3.12f, 2.34f), new Vector3(-24f, 0f, 0f), new Vector3(0.20f, 0.045f, 0.20f), pal.MetalDark);
                CreateSphere("Bulb", lamp.transform, new Vector3(gx, 3.08f, 2.36f), Vector3.zero, new Vector3(0.08f, 0.08f, 0.08f), pal.LanternBulbGlow);
            }

            // Price board & colorful posters on Bar_LeftWall (front face Z=2.90 -> offset >= 0.035m -> Z=2.865)
            var postersRoot = new GameObject("Bar_PostersAndPriceBoard");
            postersRoot.transform.SetParent(barGroup.transform, false);
            CreateBox("PriceBoard_Frame", postersRoot.transform, new Vector3(-9.10f, 1.62f, 2.865f), Vector3.zero, new Vector3(0.70f, 0.88f, 0.025f), pal.MetalDark);
            CreateBox("PriceBoard_Paper", postersRoot.transform, new Vector3(-9.10f, 1.62f, 2.845f), Vector3.zero, new Vector3(0.62f, 0.80f, 0.015f), pal.PosterWhite);
            CreateBox("PriceBoard_Header", postersRoot.transform, new Vector3(-9.10f, 1.92f, 2.832f), Vector3.zero, new Vector3(0.54f, 0.10f, 0.010f), pal.PosterRed);
            for (int line = 0; line < 4; line++)
            {
                CreateBox(
                    $"PriceBoard_Line_{line}",
                    postersRoot.transform,
                    new Vector3(-9.10f, 1.74f - line * 0.13f, 2.832f),
                    Vector3.zero,
                    new Vector3(0.48f, 0.035f, 0.010f),
                    pal.MetalDark);
            }

            CreateBox("Poster_BarLeftTop", postersRoot.transform, new Vector3(-9.12f, 2.38f, 2.865f), new Vector3(0f, 0f, 3f), new Vector3(0.52f, 0.42f, 0.018f), pal.PosterYellow);
            CreateBox("Poster_BarLeftTopStripe", postersRoot.transform, new Vector3(-9.12f, 2.48f, 2.852f), new Vector3(0f, 0f, 3f), new Vector3(0.46f, 0.09f, 0.010f), pal.PosterRed);

            // Posters on Bar_RightPier (front face Z=2.90 -> Z=2.865)
            CreateBox("Poster_RightPier_Cyan", postersRoot.transform, new Vector3(-4.05f, 1.68f, 2.865f), new Vector3(0f, 0f, -2.5f), new Vector3(0.38f, 0.50f, 0.018f), pal.PosterCyan);
            CreateBox("Poster_RightPier_Yellow", postersRoot.transform, new Vector3(-4.05f, 2.26f, 2.865f), new Vector3(0f, 0f, 2f), new Vector3(0.36f, 0.38f, 0.018f), pal.PosterYellow);

            // Retro boombox / speaker radio on Bar_Counter (top Y=1.04, Z=4.35)
            var radio = new GameObject("Bar_CounterRadio");
            radio.transform.SetParent(barGroup.transform, false);
            radio.transform.position = new Vector3(-5.15f, 1.17f, 4.30f);
            radio.transform.rotation = Quaternion.Euler(0f, -14f, 0f);
            CreateBox("RadioBody", radio.transform, Vector3.zero, Vector3.zero, new Vector3(0.44f, 0.24f, 0.18f), pal.MetalDark);
            CreateCyl("SpeakerL", radio.transform, new Vector3(-0.12f, -0.01f, -0.095f), new Vector3(90f, 0f, 0f), new Vector3(0.13f, 0.015f, 0.13f), pal.RoofMetal);
            CreateCyl("SpeakerR", radio.transform, new Vector3(0.12f, -0.01f, -0.095f), new Vector3(90f, 0f, 0f), new Vector3(0.13f, 0.015f, 0.13f), pal.RoofMetal);
            CreateBox("TunerDial", radio.transform, new Vector3(0f, 0.06f, -0.096f), Vector3.zero, new Vector3(0.16f, 0.04f, 0.012f), pal.PosterYellow);
            CreateBox("Handle", radio.transform, new Vector3(0f, 0.15f, 0f), Vector3.zero, new Vector3(0.32f, 0.025f, 0.025f), pal.RoofMetal);
            CreateCyl("Antenna", radio.transform, new Vector3(0.17f, 0.26f, 0.04f), new Vector3(0f, 0f, -18f), new Vector3(0.012f, 0.22f, 0.012f), pal.RoofMetal);

            // Extra interior shelf + decorative bottles and counter cans
            CreateBox("Bar_Shelf_Lower", barGroup.transform, new Vector3(-5.85f, 1.46f, 5.88f), Vector3.zero, new Vector3(2.15f, 0.06f, 0.28f), pal.WoodSign);
            for (int b = 0; b < 6; b++)
            {
                float bx = -6.65f + b * 0.30f;
                Material bMat = (b % 2 == 0) ? pal.BottleGreen : pal.BottleAmber;
                CreateCyl($"ShelfLowerBottle_{b}", barGroup.transform, new Vector3(bx, 1.62f, 5.88f), Vector3.zero, new Vector3(0.065f, 0.13f, 0.065f), bMat);
            }
            for (int c = 0; c < 4; c++)
            {
                Material cMat = (c % 2 == 0) ? pal.PlasticRed : pal.PlasticYellow;
                CreateCyl($"CounterCan_{c}", barGroup.transform, new Vector3(-5.82f + c * 0.12f, 1.10f, 4.22f + (c % 2) * 0.08f), Vector3.zero, new Vector3(0.065f, 0.065f, 0.065f), cMat);
            }

            // Visible taut clothesline poles & cable above Bar 2F balcony for the 3 PhysX HingeJoint garments
            var lineGroup = new GameObject("Bar_PhysicalClotheslineRig");
            lineGroup.transform.SetParent(barGroup.transform, false);
            CreateCyl("Clothesline_PostL", lineGroup.transform, new Vector3(-8.20f, 4.18f, 2.26f), Vector3.zero, new Vector3(0.045f, 0.56f, 0.045f), pal.MetalDark);
            CreateCyl("Clothesline_PostR", lineGroup.transform, new Vector3(-4.35f, 4.18f, 2.26f), Vector3.zero, new Vector3(0.045f, 0.56f, 0.045f), pal.MetalDark);
            CreateBox("Clothesline_Cable", lineGroup.transform, new Vector3(-6.275f, 4.44f, 2.26f), Vector3.zero, new Vector3(3.92f, 0.022f, 0.022f), pal.BlackCable);

            // Potted terracotta plants on Bar 2F balcony parapet
            BuildPottedPlant("BarBalcony_Plant_L", barGroup.transform, new Vector3(-8.65f, 4.02f, 2.35f), pal);
            BuildPottedPlant("BarBalcony_Plant_R", barGroup.transform, new Vector3(-4.25f, 4.02f, 2.35f), pal);
        }

        private static void BuildUrbanStreetAndStairDetails(Transform parent, MaterialPalette pal)
        {
            var urbanGroup = new GameObject("UrbanDetails_StreetAndMidground");
            urbanGroup.transform.SetParent(parent, false);

            // Metallic stair handrails along StairStep_0..9 (X=-2.95, Y=0.24..2.58, Z=3.25..7.03)
            var stairRail = new GameObject("Stair_MetallicHandrails");
            stairRail.transform.SetParent(urbanGroup.transform, false);
            float[] railXs = { -3.65f, -2.25f };
            foreach (float rx in railXs)
            {
                // Rail runs from (rx, 1.05, 3.25) to (rx, 3.39, 7.03) -> dy=2.34, dz=3.78, len=4.445, pitch=-31.76 deg
                Vector3 mid = new Vector3(rx, 2.22f, 5.14f);
                CreateBox($"Handrail_Bar_{(rx < -3f ? "L" : "R")}", stairRail.transform, mid, new Vector3(-31.76f, 0f, 0f), new Vector3(0.045f, 0.045f, 4.52f), pal.MetalDark);
                for (int p = 0; p < 4; p++)
                {
                    float t = p / 3f;
                    float py = Mathf.Lerp(0.66f, 3.00f, t);
                    float pz = Mathf.Lerp(3.25f, 7.03f, t);
                    CreateCyl($"Handrail_Post_{(rx < -3f ? "L" : "R")}_{p}", stairRail.transform, new Vector3(rx, py, pz), Vector3.zero, new Vector3(0.04f, 0.44f, 0.04f), pal.MetalDark);
                }
            }

            // UtilityPole_Main energy meter box, metallic conduits, pole posters & mounted trash bin (pole at X=-2.35, Z=2.45)
            var poleDetails = new GameObject("UtilityPole_Main_Details");
            poleDetails.transform.SetParent(urbanGroup.transform, false);
            CreateBox("PowerMeter_Box", poleDetails.transform, new Vector3(-2.35f, 1.92f, 2.28f), Vector3.zero, new Vector3(0.26f, 0.34f, 0.15f), pal.RoofMetal);
            CreateBox("PowerMeter_Glass", poleDetails.transform, new Vector3(-2.35f, 1.96f, 2.195f), Vector3.zero, new Vector3(0.14f, 0.12f, 0.012f), pal.DarkInterior);
            CreateCyl("PowerConduit_Lower", poleDetails.transform, new Vector3(-2.27f, 0.96f, 2.33f), Vector3.zero, new Vector3(0.032f, 0.85f, 0.032f), pal.MetalDark);
            CreateCyl("PowerConduit_Upper", poleDetails.transform, new Vector3(-2.27f, 3.85f, 2.33f), Vector3.zero, new Vector3(0.032f, 1.80f, 0.032f), pal.MetalDark);
            for (int c = 0; c < 4; c++)
            {
                CreateBox($"ConduitClamp_{c}", poleDetails.transform, new Vector3(-2.35f, 0.85f + c * 1.15f, 2.45f), Vector3.zero, new Vector3(0.24f, 0.04f, 0.24f), pal.MetalDark);
            }

            // Pole posters (offset >= 0.03m from pole front Z=2.35)
            CreateBox("PolePoster_Top", poleDetails.transform, new Vector3(-2.35f, 1.48f, 2.315f), new Vector3(0f, 0f, -4f), new Vector3(0.22f, 0.28f, 0.015f), pal.PosterYellow);
            CreateBox("PolePoster_Bot", poleDetails.transform, new Vector3(-2.35f, 1.14f, 2.315f), new Vector3(0f, 0f, 3f), new Vector3(0.20f, 0.24f, 0.015f), pal.PosterCyan);

            // Fixed metal street trash bin mounted on the pole
            var trashBin = new GameObject("Pole_TrashBin");
            trashBin.transform.SetParent(poleDetails.transform, false);
            CreateBox("TrashBin_Bracket", trashBin.transform, new Vector3(-2.22f, 0.92f, 2.45f), Vector3.zero, new Vector3(0.16f, 0.06f, 0.06f), pal.MetalDark);
            CreateBox("TrashBin_Body", trashBin.transform, new Vector3(-2.02f, 0.92f, 2.45f), Vector3.zero, new Vector3(0.32f, 0.44f, 0.28f), pal.MetalDark);
            CreateBox("TrashBin_Rim", trashBin.transform, new Vector3(-2.02f, 1.14f, 2.45f), Vector3.zero, new Vector3(0.35f, 0.04f, 0.31f), pal.RoofMetal);

            // Colorful urban graffiti mural on MidBrickHouse_Base left wall (Z_front=3.40 -> Z=3.36, 0.04m clear)
            // and on MidBrickHouse_TerraceWall (Z_front=2.96 -> Z=2.92, 0.04m clear)
            var graffiti = new GameObject("Urban_GraffitiMural");
            graffiti.transform.SetParent(urbanGroup.transform, false);
            CreateBox("Graffiti_WallBack", graffiti.transform, new Vector3(0.06f, 1.32f, 3.362f), Vector3.zero, new Vector3(0.84f, 1.30f, 0.016f), pal.GraffitiTeal);
            CreateBox("Graffiti_WallSlash1", graffiti.transform, new Vector3(0.04f, 1.44f, 3.342f), new Vector3(0f, 0f, 16f), new Vector3(0.76f, 0.26f, 0.014f), pal.GraffitiMagenta);
            CreateBox("Graffiti_WallSlash2", graffiti.transform, new Vector3(0.10f, 1.05f, 3.342f), new Vector3(0f, 0f, -12f), new Vector3(0.68f, 0.20f, 0.014f), pal.GraffitiGold);
            CreateBox("Graffiti_WallAccent", graffiti.transform, new Vector3(-0.08f, 1.72f, 3.342f), new Vector3(0f, 0f, -8f), new Vector3(0.42f, 0.14f, 0.014f), pal.PosterWhite);

            CreateBox("Graffiti_TerraceBase", graffiti.transform, new Vector3(-0.35f, 3.28f, 2.922f), Vector3.zero, new Vector3(2.30f, 0.56f, 0.016f), pal.GraffitiMagenta);
            CreateBox("Graffiti_TerraceWave1", graffiti.transform, new Vector3(-0.70f, 3.32f, 2.902f), new Vector3(0f, 0f, 9f), new Vector3(1.10f, 0.26f, 0.014f), pal.GraffitiGold);
            CreateBox("Graffiti_TerraceWave2", graffiti.transform, new Vector3(0.28f, 3.22f, 2.902f), new Vector3(0f, 0f, -11f), new Vector3(1.05f, 0.24f, 0.014f), pal.GraffitiTeal);

            // Small street art tag on RightSobrado_FrontBrickWall (Z_front=2.57 -> Z=2.53)
            CreateBox("Graffiti_RightWall_1", graffiti.transform, new Vector3(6.65f, 0.72f, 2.535f), new Vector3(0f, 0f, 6f), new Vector3(0.88f, 0.34f, 0.016f), pal.GraffitiTeal);
            CreateBox("Graffiti_RightWall_2", graffiti.transform, new Vector3(6.68f, 0.74f, 2.518f), new Vector3(0f, 0f, -9f), new Vector3(0.64f, 0.16f, 0.014f), pal.GraffitiGold);

            // 2 metallic storm drain grates along the back gutter (ground Y=0.00 -> grate Y=0.024, >=0.02m clear)
            BuildStormDrainGrate("Street_StormDrain_Left", urbanGroup.transform, new Vector3(-4.15f, 0.024f, 1.98f), pal);
            BuildStormDrainGrate("Street_StormDrain_Right", urbanGroup.transform, new Vector3(4.25f, 0.024f, 1.98f), pal);

            // Small visual pebbles / street gutter grit along Z=1.88..2.12 (well clear of fighter Z=0 lane)
            var pebblesRoot = new GameObject("Street_GutterPebbles");
            pebblesRoot.transform.SetParent(urbanGroup.transform, false);
            Vector3[] pebblePositions =
            {
                new Vector3(-7.40f, 0.032f, 1.96f), new Vector3(-6.15f, 0.028f, 2.04f),
                new Vector3(-4.72f, 0.030f, 1.92f), new Vector3(-2.60f, 0.034f, 2.06f),
                new Vector3(-0.85f, 0.028f, 1.94f), new Vector3(1.25f, 0.032f, 2.03f),
                new Vector3(2.90f, 0.029f, 1.95f),  new Vector3(5.10f, 0.033f, 2.05f),
                new Vector3(6.40f, 0.030f, 1.94f),  new Vector3(8.15f, 0.032f, 2.02f)
            };
            for (int i = 0; i < pebblePositions.Length; i++)
            {
                float sx = 0.08f + (i % 3) * 0.035f;
                float sy = 0.045f + (i % 2) * 0.02f;
                float sz = 0.07f + ((i + 1) % 3) * 0.03f;
                CreateBox(
                    $"GutterStone_{i}",
                    pebblesRoot.transform,
                    pebblePositions[i],
                    new Vector3(i * 11f, i * 29f, i * 7f),
                    new Vector3(sx, sy, sz),
                    (i % 2 == 0) ? pal.PoleConcrete : pal.SidewalkConcrete);
            }

            // Ramp metallic top guardrail posts & Ramp secondary street lamp
            var rampDetails = new GameObject("HillRamp_GuardrailAndLamp");
            rampDetails.transform.SetParent(urbanGroup.transform, false);
            float[] rampXs = { 3.82f, 6.08f };
            foreach (float rx in rampXs)
            {
                CreateBox($"RampTopRail_{(rx < 5f ? "L" : "R")}", rampDetails.transform, new Vector3(rx, 2.34f, 8.80f), new Vector3(-14.2f, 0f, 0f), new Vector3(0.055f, 0.055f, 10.6f), pal.MetalDark);
                for (int p = 0; p < 5; p++)
                {
                    float t = p / 4f;
                    float pz = Mathf.Lerp(3.9f, 13.5f, t);
                    float py = Mathf.Lerp(0.85f, 3.25f, t);
                    CreateCyl($"RampRailPost_{(rx < 5f ? "L" : "R")}_{p}", rampDetails.transform, new Vector3(rx, py, pz), Vector3.zero, new Vector3(0.045f, 0.34f, 0.045f), pal.MetalDark);
                }
            }

            // Secondary lamp pole along the ramp
            CreateCyl("UtilityPole_Ramp", rampDetails.transform, new Vector3(6.25f, 2.95f, 6.80f), Vector3.zero, new Vector3(0.16f, 2.40f, 0.16f), pal.PoleConcrete);
            CreateBox("RampPole_LampArm", rampDetails.transform, new Vector3(5.78f, 4.95f, 6.80f), new Vector3(0f, 0f, -14f), new Vector3(0.95f, 0.06f, 0.06f), pal.MetalDark);
            CreateBox("RampPole_LampHead", rampDetails.transform, new Vector3(5.36f, 4.86f, 6.80f), Vector3.zero, new Vector3(0.28f, 0.09f, 0.16f), pal.MetalDark);

            // Roof antennas & blue water tank on foreground/midground buildings
            BuildTvAntenna("MidBrick_TvAntenna", urbanGroup.transform, new Vector3(2.35f, 5.15f, 5.10f), 1.15f, pal);
            BuildSatelliteDish("GreenHouse_SatDish", urbanGroup.transform, new Vector3(-0.15f, 5.42f, 4.75f), 25f, pal);
            BuildWaterTank("MidBrick_BlueTank", urbanGroup.transform, new Vector3(0.90f, 5.15f, 5.65f), 0.90f, pal);
            BuildTvAntenna("BarRoof_TvAntenna", urbanGroup.transform, new Vector3(-7.90f, 5.80f, 4.10f), 1.20f, pal);
        }

        private static void BuildArenaCornerClosure(Transform parent, MaterialPalette pal, List<Renderer> extraWetRenderers)
        {
            var cornersRoot = new GameObject("ArenaCorners_Closure");
            cornersRoot.transform.SetParent(parent, false);

            // Extend sidewalk & curb cleanly past X=-15 and X=+15 without overlap (Sidewalk_Back covers -15..+15)
            GameObject swLeft = CreateBox("Sidewalk_LeftExt", cornersRoot.transform, new Vector3(-19.0f, 0.06f, 3.19f), Vector3.zero, new Vector3(8.0f, 0.11f, 1.72f), pal.SidewalkConcrete);
            GameObject curbLeft = CreateBox("Curb_LeftExt", cornersRoot.transform, new Vector3(-19.0f, 0.06f, 2.25f), Vector3.zero, new Vector3(8.0f, 0.12f, 0.16f), pal.PoleConcrete);
            GameObject swRight = CreateBox("Sidewalk_RightExt", cornersRoot.transform, new Vector3(19.0f, 0.06f, 3.19f), Vector3.zero, new Vector3(8.0f, 0.11f, 1.72f), pal.SidewalkConcrete);
            GameObject curbRight = CreateBox("Curb_RightExt", cornersRoot.transform, new Vector3(19.0f, 0.06f, 2.25f), Vector3.zero, new Vector3(8.0f, 0.12f, 0.16f), pal.PoleConcrete);

            extraWetRenderers.Add(swLeft.GetComponent<Renderer>());
            extraWetRenderers.Add(curbLeft.GetComponent<Renderer>());
            extraWetRenderers.Add(swRight.GetComponent<Renderer>());
            extraWetRenderers.Add(curbRight.GetComponent<Renderer>());

            // LEFT EDGE CLOSURE (X = -21 .. -10): 2nd floor above Bar_LeftAnnexWall + corner house + stairs + foliage + secondary pole
            var leftEdge = new GameObject("LeftEdge_Architecture");
            leftEdge.transform.SetParent(cornersRoot.transform, false);
            CreateBox("LeftAnnex_2F_Slab", leftEdge.transform, new Vector3(-11.85f, 3.35f, 4.60f), Vector3.zero, new Vector3(4.65f, 0.16f, 3.85f), pal.SidewalkConcrete);
            CreateBox("LeftAnnex_2F_Body", leftEdge.transform, new Vector3(-11.85f, 4.65f, 4.85f), Vector3.zero, new Vector3(4.30f, 2.50f, 3.30f), pal.StuccoOchre);
            CreateBox("LeftAnnex_2F_Parapet", leftEdge.transform, new Vector3(-11.85f, 3.80f, 2.75f), Vector3.zero, new Vector3(4.50f, 0.72f, 0.18f), pal.RedBrick);
            CreateBox("LeftAnnex_2F_Roof", leftEdge.transform, new Vector3(-11.85f, 5.96f, 4.75f), new Vector3(4f, 0f, 0f), new Vector3(4.60f, 0.12f, 3.80f), pal.RoofMetal);
            CreateBox("LeftAnnex_1F_WindowFrame", leftEdge.transform, new Vector3(-11.60f, 1.85f, 2.87f), Vector3.zero, new Vector3(1.10f, 1.15f, 0.06f), pal.WoodSign);
            CreateBox("LeftAnnex_1F_WindowDark", leftEdge.transform, new Vector3(-11.60f, 1.85f, 2.845f), Vector3.zero, new Vector3(0.92f, 0.98f, 0.04f), pal.DarkInterior);
            CreateBox("LeftAnnex_2F_WindowFrame", leftEdge.transform, new Vector3(-11.60f, 4.75f, 3.17f), Vector3.zero, new Vector3(1.05f, 1.05f, 0.06f), pal.WoodSign);
            CreateBox("LeftAnnex_2F_WindowDark", leftEdge.transform, new Vector3(-11.60f, 4.75f, 3.145f), Vector3.zero, new Vector3(0.88f, 0.88f, 0.04f), pal.DarkInterior);
            BuildWaterTank("LeftAnnex_BlueTank", leftEdge.transform, new Vector3(-12.50f, 6.02f, 4.65f), 0.92f, pal);
            BuildPottedPlant("LeftAnnex_Plant", leftEdge.transform, new Vector3(-10.85f, 4.16f, 2.75f), pal);

            // Side alley stairs + far left house closing X=-21..-14
            for (int s = 0; s < 6; s++)
            {
                CreateBox($"LeftAlley_Step_{s}", leftEdge.transform, new Vector3(-14.75f, 0.24f + s * 0.26f, 3.25f + s * 0.44f), Vector3.zero, new Vector3(1.30f, 0.26f, 0.48f), pal.SidewalkConcrete);
            }
            CreateBox("LeftFarHouse_1F", leftEdge.transform, new Vector3(-17.80f, 1.75f, 4.85f), Vector3.zero, new Vector3(4.80f, 3.30f, 3.80f), pal.StuccoCoral);
            CreateBox("LeftFarHouse_2F", leftEdge.transform, new Vector3(-18.10f, 4.70f, 5.15f), Vector3.zero, new Vector3(4.60f, 2.65f, 3.40f), pal.RedBrick);
            CreateBox("LeftFarHouse_Roof", leftEdge.transform, new Vector3(-18.10f, 6.08f, 5.10f), new Vector3(4f, 0f, 0f), new Vector3(4.90f, 0.12f, 3.70f), pal.RoofMetal);
            CreateBox("LeftFarHouse_Shutter", leftEdge.transform, new Vector3(-17.20f, 1.20f, 2.92f), Vector3.zero, new Vector3(1.65f, 2.15f, 0.08f), pal.SteelShutter);
            BuildTropicalPalm("PalmTree_LeftEdge", leftEdge.transform, new Vector3(-14.65f, 0.11f, 4.10f), 4.1f, pal);

            // Secondary left utility pole
            CreateCyl("UtilityPole_LeftEdge", leftEdge.transform, new Vector3(-13.45f, 3.35f, 2.45f), Vector3.zero, new Vector3(0.18f, 3.35f, 0.18f), pal.PoleConcrete);
            CreateBox("LeftPole_Crossbar", leftEdge.transform, new Vector3(-13.45f, 6.25f, 2.45f), Vector3.zero, new Vector3(1.05f, 0.09f, 0.11f), pal.PoleConcrete);
            CreateBox("LeftPole_LampArm", leftEdge.transform, new Vector3(-12.90f, 5.15f, 2.38f), new Vector3(0f, 0f, 12f), new Vector3(1.10f, 0.06f, 0.06f), pal.MetalDark);
            CreateBox("LeftPole_LampHead", leftEdge.transform, new Vector3(-12.40f, 5.26f, 2.38f), Vector3.zero, new Vector3(0.28f, 0.09f, 0.16f), pal.MetalDark);

            // RIGHT EDGE CLOSURE (X = +12 .. +22): corner house, tropical trees, balcony plants & secondary pole
            var rightEdge = new GameObject("RightEdge_Architecture");
            rightEdge.transform.SetParent(cornersRoot.transform, false);
            CreateBox("RightFarHouse_1F", rightEdge.transform, new Vector3(15.20f, 1.72f, 4.95f), Vector3.zero, new Vector3(5.60f, 3.25f, 4.10f), pal.StuccoGreen);
            CreateBox("RightFarHouse_2F_Slab", rightEdge.transform, new Vector3(15.20f, 3.38f, 4.75f), Vector3.zero, new Vector3(5.90f, 0.16f, 4.45f), pal.SidewalkConcrete);
            CreateBox("RightFarHouse_2F", rightEdge.transform, new Vector3(15.40f, 4.70f, 5.10f), Vector3.zero, new Vector3(5.20f, 2.55f, 3.60f), pal.RedBrick);
            CreateBox("RightFarHouse_Roof", rightEdge.transform, new Vector3(15.40f, 6.04f, 5.05f), new Vector3(4f, 0f, 0f), new Vector3(5.55f, 0.12f, 3.90f), pal.RoofMetal);
            CreateBox("RightFarHouse_Shutter", rightEdge.transform, new Vector3(14.60f, 1.18f, 2.87f), Vector3.zero, new Vector3(1.75f, 2.10f, 0.08f), pal.SteelShutter);
            CreateBox("RightFarHouse_2F_WindowFrame", rightEdge.transform, new Vector3(14.85f, 4.72f, 3.27f), Vector3.zero, new Vector3(1.10f, 1.05f, 0.06f), pal.WoodSign);
            CreateBox("RightFarHouse_2F_WindowDark", rightEdge.transform, new Vector3(14.85f, 4.72f, 3.245f), Vector3.zero, new Vector3(0.92f, 0.88f, 0.04f), pal.DarkInterior);
            CreateBox("RightFarWall_End", rightEdge.transform, new Vector3(19.60f, 2.15f, 5.20f), Vector3.zero, new Vector3(3.60f, 4.10f, 3.40f), pal.StuccoOchre);
            BuildWaterTank("RightFar_BlueTank", rightEdge.transform, new Vector3(14.30f, 6.10f, 4.85f), 0.94f, pal);
            BuildTvAntenna("RightSobrado_TvAntenna", rightEdge.transform, new Vector3(10.85f, 5.72f, 4.65f), 1.15f, pal);

            // Balcony plants on RightSobrado_2F_BrickBalcony (top Y=4.09, Z=2.28)
            BuildPottedPlant("RightBalcony_Plant_L", rightEdge.transform, new Vector3(7.45f, 4.09f, 2.28f), pal);
            BuildPottedPlant("RightBalcony_Plant_R", rightEdge.transform, new Vector3(10.20f, 4.09f, 2.28f), pal);

            // Extra tropical vegetation on right edge & ramp base
            BuildTropicalPalm("PalmTree_RightEdge", rightEdge.transform, new Vector3(12.45f, 0.11f, 3.15f), 4.35f, pal);
            CreateSphere("ExtraTropicalBush_RampBase", rightEdge.transform, new Vector3(5.65f, 0.42f, 4.25f), Vector3.zero, new Vector3(0.95f, 0.72f, 0.85f), pal.PalmLeaf);
            CreateSphere("ExtraTropicalBush_Right", rightEdge.transform, new Vector3(12.25f, 0.48f, 3.10f), Vector3.zero, new Vector3(1.15f, 0.82f, 0.95f), pal.PalmLeaf);

            // Secondary right utility pole
            CreateCyl("UtilityPole_RightEdge", rightEdge.transform, new Vector3(12.75f, 3.35f, 2.45f), Vector3.zero, new Vector3(0.18f, 3.35f, 0.18f), pal.PoleConcrete);
            CreateBox("RightPole_Crossbar", rightEdge.transform, new Vector3(12.75f, 6.25f, 2.45f), Vector3.zero, new Vector3(1.05f, 0.09f, 0.11f), pal.PoleConcrete);
        }

        private static void BuildHillsideExtras(Transform hillsideLayer, Transform extrasRoot, MaterialPalette pal)
        {
            // Add TV antennas, satellite dishes, extra blue water tanks & parapet walls to existing HillHouse_0..15 roofs
            int[] antennaHouses = { 0, 2, 4, 6, 8, 11, 13, 15 };
            int[] dishHouses = { 1, 3, 5, 9, 12, 14 };
            int[] extraTankHouses = { 7, 9, 12 };
            int[] parapetHouses = { 1, 3, 4, 7, 8, 11, 14 };

            for (int i = 0; i < 16; i++)
            {
                Transform house = hillsideLayer.Find($"HillHouse_{i}");
                if (house == null) continue;

                Renderer hr = house.GetComponent<Renderer>();
                Bounds hb = hr != null ? hr.bounds : new Bounds(house.position, house.lossyScale);
                float roofTopY = hb.max.y + 0.18f;

                if (System.Array.IndexOf(antennaHouses, i) >= 0)
                {
                    Vector3 pos = new Vector3(hb.center.x + hb.extents.x * 0.45f, roofTopY, hb.center.z - hb.extents.z * 0.25f);
                    BuildTvAntenna($"HillAntenna_{i}", extrasRoot, pos, 1.35f, pal);
                }
                if (System.Array.IndexOf(dishHouses, i) >= 0)
                {
                    Vector3 pos = new Vector3(hb.center.x - hb.extents.x * 0.42f, roofTopY, hb.center.z - hb.extents.z * 0.35f);
                    BuildSatelliteDish($"HillSatDish_{i}", extrasRoot, pos, -22f + (i % 3) * 18f, pal);
                }
                if (System.Array.IndexOf(extraTankHouses, i) >= 0)
                {
                    Vector3 pos = new Vector3(hb.center.x + hb.extents.x * 0.15f, roofTopY, hb.center.z - 0.15f);
                    BuildWaterTank($"HillExtraTank_{i}", extrasRoot, pos, 0.96f, pal);
                }
                if (System.Array.IndexOf(parapetHouses, i) >= 0)
                {
                    Vector3 pos = new Vector3(hb.center.x, roofTopY + 0.28f, hb.min.z + 0.08f);
                    CreateBox($"HillParapet_{i}", extrasRoot, pos, Vector3.zero, new Vector3(hb.size.x * 0.96f, 0.56f, 0.16f), pal.RedBrick);
                }
            }

            // Mid-slope hillside houses framing the ramp vista (visible below the top HUD!)
            CreateBox("HillExtraHouse_RampMidA", extrasRoot, new Vector3(4.15f, 3.65f, 15.80f), Vector3.zero, new Vector3(2.30f, 2.20f, 2.20f), pal.StuccoCoral);
            CreateBox("HillExtraRoof_RampMidA", extrasRoot, new Vector3(4.15f, 4.82f, 15.80f), Vector3.zero, new Vector3(2.48f, 0.12f, 2.38f), pal.RoofMetal);
            BuildWaterTank("HillExtraTank_RampMidA", extrasRoot, new Vector3(3.65f, 4.88f, 15.65f), 0.72f, pal);
            BuildTvAntenna("HillExtraAnt_RampMidA", extrasRoot, new Vector3(4.85f, 4.88f, 15.65f), 1.05f, pal);

            CreateBox("HillExtraHouse_RampMidB", extrasRoot, new Vector3(6.45f, 4.75f, 19.20f), Vector3.zero, new Vector3(2.60f, 2.50f, 2.40f), pal.StuccoOchre);
            CreateBox("HillExtraRoof_RampMidB", extrasRoot, new Vector3(6.45f, 6.08f, 19.20f), Vector3.zero, new Vector3(2.78f, 0.12f, 2.58f), pal.RoofMetal);
            BuildWaterTank("HillExtraTank_RampMidB", extrasRoot, new Vector3(6.95f, 6.14f, 19.05f), 0.78f, pal);
        }

        private static void InstantiatePhase15_2InteractiveProps(Transform propsRoot, MaterialPalette pal)
        {
            GameObject clothPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HangingClothPrefabPath);
            GameObject lanternPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HangingLanternPrefabPath);
            GameObject conePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DynamicConePrefabPath);

            // 3 PhysX HingeJoint clothes on the Bar do Jorge 2F clothesline (angled slightly so +X wind swings them naturally)
            if (clothPrefab != null)
            {
                InstantiateClothVariant(clothPrefab, "Hanging_Cloth_White", propsRoot, new Vector3(-7.30f, 4.44f, 2.26f), 18f, pal.ClothesWhite, isTowel: false);
                InstantiateClothVariant(clothPrefab, "Hanging_Cloth_Orange", propsRoot, new Vector3(-6.25f, 4.44f, 2.26f), -16f, pal.ClothesOrange, isTowel: false);
                InstantiateClothVariant(clothPrefab, "Hanging_Cloth_Blue", propsRoot, new Vector3(-5.20f, 4.44f, 2.26f), 20f, pal.ClothesBlue, isTowel: true);
            }

            // 1 PhysX HingeJoint hanging lantern under RightSobrado 2F balcony slab (balancing Hanging_BarSign on the left)
            if (lanternPrefab != null)
            {
                GameObject lantern = (GameObject)PrefabUtility.InstantiatePrefab(lanternPrefab, propsRoot);
                lantern.name = "Hanging_RightLantern";
                lantern.transform.localPosition = new Vector3(7.35f, 3.03f, 2.22f);
                lantern.transform.localRotation = Quaternion.identity;
                lantern.transform.localScale = Vector3.one;
            }

            // 2 loose PhysX StageDynamicProp cones near the left/right edges (keeping center fight lane clear)
            if (conePrefab != null)
            {
                GameObject coneL = (GameObject)PrefabUtility.InstantiatePrefab(conePrefab, propsRoot);
                coneL.name = "Dynamic_Cone_Left";
                coneL.transform.localPosition = new Vector3(-5.65f, 0.005f, 0.78f);
                coneL.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
                coneL.transform.localScale = Vector3.one;

                GameObject coneR = (GameObject)PrefabUtility.InstantiatePrefab(conePrefab, propsRoot);
                coneR.name = "Dynamic_Cone_Right";
                coneR.transform.localPosition = new Vector3(5.85f, 0.005f, 0.72f);
                coneR.transform.localRotation = Quaternion.Euler(0f, -18f, 0f);
                coneR.transform.localScale = Vector3.one;
            }
        }

        private static void InstantiateClothVariant(
            GameObject prefab,
            string instanceName,
            Transform parent,
            Vector3 pos,
            float rotY,
            Material clothMat,
            bool isTowel)
        {
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.name = instanceName;
            inst.transform.localPosition = pos;
            inst.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            inst.transform.localScale = Vector3.one;

            Transform body = inst.transform.Find("HangingBody");
            if (body != null)
            {
                foreach (MeshRenderer mr in body.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr.name.StartsWith("Cloth_"))
                    {
                        mr.sharedMaterial = clothMat;
                    }
                    if (isTowel && mr.name.Contains("Sleeve"))
                    {
                        mr.gameObject.SetActive(false);
                    }
                    if (isTowel && mr.name == "Cloth_Body")
                    {
                        mr.transform.localScale = new Vector3(0.48f, 0.62f, 0.045f);
                    }
                }
            }
        }

        private static GameObject BuildSunsetHalo(GameObject bayLayer, GameObject sunDisc, MaterialPalette pal)
        {
            var halo = new GameObject(SunsetHaloName);
            if (bayLayer != null)
            {
                halo.transform.SetParent(bayLayer.transform, false);
            }

            // Frame the sunset sun disc harmoniously beside Sugarloaf's right shoulder (faithful to mapa.jpg)
            Vector3 sunPos = new Vector3(21.8f, 10.9f, 53.8f);
            if (sunDisc != null)
            {
                sunDisc.transform.position = sunPos;
            }
            // Place a subtle warm glow ring behind the sun disc (offset +0.45m in Z -> zero Z-fighting)
            halo.transform.position = new Vector3(sunPos.x, sunPos.y, sunPos.z + 0.45f);
            CreateSphere("Sun_OuterGlow", halo.transform, Vector3.zero, Vector3.zero, new Vector3(5.8f, 5.8f, 0.25f), pal.SunGlow);
            return halo;
        }

        private static GameObject BuildClearNightSky(GameObject bayLayer, MaterialPalette pal)
        {
            var sky = new GameObject(ClearNightSkyName);
            if (bayLayer != null)
            {
                sky.transform.SetParent(bayLayer.transform, false);
            }

            // Soft moon well-framed in the upper-right sky above Sugarloaf & Guanabara Bay
            CreateSphere("Night_MoonDisc", sky.transform, new Vector3(21.4f, 12.6f, 53.2f), Vector3.zero, new Vector3(2.3f, 2.3f, 0.3f), pal.MoonGlow);

            // Subtle star field across the upper sky arc (deterministic pseudo-random layout)
            Vector3[] stars =
            {
                new Vector3(-14.5f, 17.5f, 54.4f), new Vector3(-10.2f, 19.2f, 54.4f), new Vector3(-6.4f, 18.0f, 54.4f),
                new Vector3(-2.1f, 20.4f, 54.4f),  new Vector3(1.8f, 18.6f, 54.4f),   new Vector3(4.9f, 16.5f, 54.4f),
                new Vector3(7.6f, 19.8f, 54.4f),   new Vector3(10.5f, 15.2f, 54.4f),  new Vector3(12.4f, 17.9f, 54.4f),
                new Vector3(14.1f, 13.8f, 54.4f),  new Vector3(15.6f, 16.4f, 54.4f),  new Vector3(16.8f, 19.1f, 54.4f),
                new Vector3(19.5f, 15.6f, 54.4f),  new Vector3(21.2f, 13.2f, 54.4f),  new Vector3(22.8f, 17.4f, 54.4f),
                new Vector3(24.6f, 11.4f, 54.4f),  new Vector3(26.4f, 14.9f, 54.4f),  new Vector3(28.5f, 18.2f, 54.4f),
                new Vector3(11.8f, 11.2f, 54.4f),  new Vector3(13.6f, 9.8f, 54.4f),   new Vector3(15.9f, 9.2f, 54.4f),
                new Vector3(20.4f, 10.1f, 54.4f),  new Vector3(23.1f, 9.4f, 54.4f),   new Vector3(8.8f, 13.4f, 54.4f),
                new Vector3(6.2f, 14.1f, 54.4f),   new Vector3(25.2f, 16.7f, 54.4f),  new Vector3(18.4f, 20.5f, 54.4f),
                new Vector3(13.2f, 20.2f, 54.4f)
            };

            for (int i = 0; i < stars.Length; i++)
            {
                float s = (i % 3 == 0) ? 0.24f : (i % 2 == 0 ? 0.17f : 0.13f);
                CreateSphere($"Night_Star_{i}", sky.transform, stars[i], Vector3.zero, new Vector3(s, s, 0.10f), pal.StarGlow);
            }

            return sky;
        }

        private static GameObject BuildNightLightsAndWindows(MaterialPalette pal)
        {
            var nightRoot = new GameObject(NightLightsAndWindowsName);

            // Point lights (ALL LightShadows.None for rock-solid 60 FPS performance)
            CreatePointLight("NightLight_StreetLampMain", nightRoot.transform, new Vector3(-1.18f, 4.85f, 1.85f), new Color(1.0f, 0.78f, 0.44f, 1f), intensity: 13.5f, range: 14.5f);
            CreatePointLight("NightLight_BarFacade", nightRoot.transform, new Vector3(-6.35f, 2.95f, 2.15f), new Color(1.0f, 0.66f, 0.28f, 1f), intensity: 10.5f, range: 11.0f);
            CreatePointLight("NightLight_BarInterior", nightRoot.transform, new Vector3(-6.45f, 2.20f, 4.10f), new Color(1.0f, 0.64f, 0.24f, 1f), intensity: 7.5f, range: 8.5f);
            CreatePointLight("NightLight_BarEstufa", nightRoot.transform, new Vector3(-6.75f, 1.48f, 4.05f), new Color(1.0f, 0.72f, 0.30f, 1f), intensity: 4.2f, range: 4.5f);
            CreatePointLight("NightLight_RightSobrado", nightRoot.transform, new Vector3(7.35f, 2.55f, 2.05f), new Color(1.0f, 0.74f, 0.38f, 1f), intensity: 9.5f, range: 10.5f);
            CreatePointLight("NightLight_RampLamp", nightRoot.transform, new Vector3(5.36f, 4.65f, 6.70f), new Color(1.0f, 0.78f, 0.46f, 1f), intensity: 10.0f, range: 12.0f);
            CreatePointLight("NightLight_RampTopLamp", nightRoot.transform, new Vector3(5.26f, 6.42f, 12.20f), new Color(1.0f, 0.76f, 0.42f, 1f), intensity: 11.0f, range: 14.0f);
            CreatePointLight("NightLight_LeftEdgeLamp", nightRoot.transform, new Vector3(-12.40f, 4.95f, 2.10f), new Color(1.0f, 0.76f, 0.42f, 1f), intensity: 9.0f, range: 11.5f);

            // Emissive street lamp lenses (offset slightly downward to avoid Z-fighting with metal lamp heads)
            CreateBox("LampGlow_MainPole", nightRoot.transform, new Vector3(-1.18f, 5.31f, 2.35f), Vector3.zero, new Vector3(0.28f, 0.045f, 0.14f), pal.StreetLampGlow);
            CreateBox("LampGlow_RampPole", nightRoot.transform, new Vector3(5.36f, 4.79f, 6.80f), Vector3.zero, new Vector3(0.24f, 0.045f, 0.13f), pal.StreetLampGlow);
            CreateBox("LampGlow_RampTopPole", nightRoot.transform, new Vector3(5.26f, 6.57f, 12.35f), Vector3.zero, new Vector3(0.26f, 0.045f, 0.14f), pal.StreetLampGlow);
            CreateBox("LampGlow_LeftEdgePole", nightRoot.transform, new Vector3(-12.40f, 5.19f, 2.38f), Vector3.zero, new Vector3(0.24f, 0.045f, 0.13f), pal.StreetLampGlow);

            // Midground lit windows (strictly >= 0.03m in front of base dark window panes -> zero Z-fighting)
            CreateBox("NightWin_GreenHouse", nightRoot.transform, new Vector3(-1.05f, 3.85f, 4.01f), Vector3.zero, new Vector3(0.76f, 0.80f, 0.02f), pal.NightWindowGlow);
            CreateBox("NightWin_RightSobrado", nightRoot.transform, new Vector3(8.95f, 1.85f, 2.505f), Vector3.zero, new Vector3(0.96f, 1.06f, 0.02f), pal.NightWindowWarmAlt);
            CreateBox("NightWin_LeftAnnex1F", nightRoot.transform, new Vector3(-11.60f, 1.85f, 2.815f), Vector3.zero, new Vector3(0.86f, 0.92f, 0.02f), pal.NightWindowWarmAlt);
            CreateBox("NightWin_LeftAnnex2F", nightRoot.transform, new Vector3(-11.60f, 4.75f, 3.115f), Vector3.zero, new Vector3(0.82f, 0.82f, 0.02f), pal.NightWindowGlow);
            CreateBox("NightWin_RightFar2F", nightRoot.transform, new Vector3(14.85f, 4.72f, 3.215f), Vector3.zero, new Vector3(0.86f, 0.82f, 0.02f), pal.NightWindowGlow);

            return nightRoot;
        }

        private static GameObject BuildHillsideNightWindows(
            GameObject hillsideLayer,
            MaterialPalette pal,
            List<(string Name, Vector3 WorldPos, Vector3 Size, bool AltWarm)> rightFillLitWindowSpecs)
        {
            if (hillsideLayer == null)
            {
                return null;
            }

            var windowsRoot = new GameObject(HillsideNightWindowsName);
            windowsRoot.transform.SetParent(hillsideLayer.transform, false);

            // Light up 18 hillside house windows with organic variation, offset -0.045m in Z from each WinA/WinB -> zero Z-fighting
            int litCount = 0;
            for (int i = 0; i < 16; i++)
            {
                Transform house = hillsideLayer.transform.Find($"HillHouse_{i}");
                if (house == null) continue;

                // Deterministic pattern: light WinA on most houses, WinB on selected houses -> 18+ warm glowing hillside windows
                bool lightA = (i % 4 != 2) || i == 2;
                bool lightB = (i % 3 == 0) || (i == 4) || (i == 7) || (i == 11);

                if (lightA)
                {
                    Transform winA = house.Find($"WinA_{i}");
                    if (winA != null)
                    {
                        CreateLitHillsideWindow($"HillNightWin_{i}_A", windowsRoot.transform, winA, (litCount % 2 == 0) ? pal.NightWindowGlow : pal.NightWindowWarmAlt);
                        litCount++;
                    }
                }
                if (lightB)
                {
                    Transform winB = house.Find($"WinB_{i}");
                    if (winB != null)
                    {
                        CreateLitHillsideWindow($"HillNightWin_{i}_B", windowsRoot.transform, winB, (litCount % 2 == 0) ? pal.NightWindowWarmAlt : pal.NightWindowGlow);
                        litCount++;
                    }
                }
            }

            // Lit warm windows on the ramp-vista mid-slope houses (offset >= 0.04m in front of facades -> zero Z-fighting)
            CreateBox("HillNightWin_RampMidA_L", windowsRoot.transform, new Vector3(3.75f, 3.85f, 14.66f), Vector3.zero, new Vector3(0.48f, 0.56f, 0.025f), pal.NightWindowWarmAlt);
            CreateBox("HillNightWin_RampMidA_R", windowsRoot.transform, new Vector3(4.55f, 3.85f, 14.66f), Vector3.zero, new Vector3(0.48f, 0.56f, 0.025f), pal.NightWindowGlow);
            CreateBox("HillNightWin_RampMidB_L", windowsRoot.transform, new Vector3(6.00f, 4.95f, 17.96f), Vector3.zero, new Vector3(0.54f, 0.62f, 0.025f), pal.NightWindowGlow);
            CreateBox("HillNightWin_RampMidB_R", windowsRoot.transform, new Vector3(6.90f, 4.95f, 17.96f), Vector3.zero, new Vector3(0.54f, 0.62f, 0.025f), pal.NightWindowWarmAlt);

            // Phase 15.2.1: 18+ warm lit windows across the new right-side hillside houses above the ramp
            if (rightFillLitWindowSpecs != null)
            {
                for (int i = 0; i < rightFillLitWindowSpecs.Count; i++)
                {
                    var spec = rightFillLitWindowSpecs[i];
                    CreateBox(spec.Name, windowsRoot.transform, spec.WorldPos, Vector3.zero, spec.Size, spec.AltWarm ? pal.NightWindowWarmAlt : pal.NightWindowGlow);
                }
            }

            return windowsRoot;
        }

        private static void CreateLitHillsideWindow(string name, Transform parent, Transform baseWin, Material mat)
        {
            Renderer r = baseWin.GetComponent<Renderer>();
            Bounds b = r != null ? r.bounds : new Bounds(baseWin.position, baseWin.lossyScale);
            // Offset -0.045m toward camera (-Z) from the front face of baseWin
            Vector3 worldPos = new Vector3(b.center.x, b.center.y, b.min.z - 0.045f);
            Vector3 size = new Vector3(Mathf.Max(0.45f, b.size.x * 0.90f), Mathf.Max(0.45f, b.size.y * 0.90f), 0.025f);
            CreateBox(name, parent, worldPos, Vector3.zero, size, mat);
        }

        private static GameObject BuildRainPuddles(MaterialPalette pal)
        {
            var puddlesRoot = new GameObject(RainPuddlesName);

            // 6 shallow reflective puddles over cobblestone street (Y=0.015m -> bottom 0.012m > 0.00m floor)
            // and sidewalk (Y=0.130m -> bottom 0.127m > 0.115m sidewalk) -> ZERO Z-fighting
            BuildOrganicPuddle("RainPuddle_StreetLeft", puddlesRoot.transform, new Vector3(-5.15f, 0.015f, 0.78f), new Vector2(2.35f, 1.05f), 8f, pal.RainPuddle);
            BuildOrganicPuddle("RainPuddle_StreetLampCenter", puddlesRoot.transform, new Vector3(-1.55f, 0.015f, 0.95f), new Vector2(2.75f, 1.25f), -6f, pal.RainPuddle);
            BuildOrganicPuddle("RainPuddle_StreetFrontCenter", puddlesRoot.transform, new Vector3(0.95f, 0.015f, -0.85f), new Vector2(2.40f, 0.95f), 5f, pal.RainPuddle);
            BuildOrganicPuddle("RainPuddle_StreetMidRight", puddlesRoot.transform, new Vector3(3.95f, 0.015f, 0.88f), new Vector2(2.25f, 1.05f), -10f, pal.RainPuddle);
            BuildOrganicPuddle("RainPuddle_StreetFarRight", puddlesRoot.transform, new Vector3(7.15f, 0.015f, 0.65f), new Vector2(2.15f, 1.00f), 11f, pal.RainPuddle);
            BuildOrganicPuddle("RainPuddle_SidewalkBar", puddlesRoot.transform, new Vector3(-5.85f, 0.130f, 2.75f), new Vector2(1.75f, 0.72f), -4f, pal.RainPuddle);

            return puddlesRoot;
        }

        private static void BuildOrganicPuddle(string name, Transform parent, Vector3 pos, Vector2 sizeXZ, float rotY, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            // Main oval disc (Unity Cylinder height=2, so scaleY=0.003 -> 0.006m thickness)
            GameObject mainDisc = CreateCyl("Puddle_Main", go.transform, Vector3.zero, Vector3.zero, new Vector3(sizeXZ.x, 0.003f, sizeXZ.y), mat);
            DisableShadows(mainDisc);

            // Secondary offset lobe for organic shape (slight +0.002m Y separation to prevent self Z-fighting)
            GameObject lobeDisc = CreateCyl("Puddle_Lobe", go.transform, new Vector3(sizeXZ.x * 0.24f, 0.002f, sizeXZ.y * 0.16f), Vector3.zero, new Vector3(sizeXZ.x * 0.58f, 0.0025f, sizeXZ.y * 0.68f), mat);
            DisableShadows(lobeDisc);
        }

        private static void WireWeatherControllerAndManager(
            Scene scene,
            GameObject sunDisc,
            GameObject sunsetHalo,
            GameObject clearNightSky,
            GameObject nightLightsAndWindows,
            GameObject hillsideNightWindows,
            GameObject shorelineLights,
            GameObject rainPuddles,
            StageFramingStandards.WeatherCloudLayers cloudLayers,
            List<Renderer> extraWetRenderers)
        {
            GameObject managerGo = FindInSceneByName(scene, "StageInteractableManager");
            if (managerGo == null)
            {
                return;
            }

            StageWeatherController weather = managerGo.GetComponent<StageWeatherController>();
            if (weather != null)
            {
                var dayList = new List<GameObject>();
                if (sunDisc != null) dayList.Add(sunDisc);
                if (sunsetHalo != null) dayList.Add(sunsetHalo);
                if (cloudLayers.DayClouds != null) dayList.Add(cloudLayers.DayClouds);

                var nightList = new List<GameObject>();
                if (nightLightsAndWindows != null) nightList.Add(nightLightsAndWindows);
                if (hillsideNightWindows != null) nightList.Add(hillsideNightWindows);
                if (shorelineLights != null) nightList.Add(shorelineLights);

                var clearNightList = new List<GameObject>();
                if (clearNightSky != null) clearNightList.Add(clearNightSky);
                if (cloudLayers.ClearNightClouds != null) clearNightList.Add(cloudLayers.ClearNightClouds);

                var rainList = new List<GameObject>();
                if (rainPuddles != null) rainList.Add(rainPuddles);
                if (cloudLayers.RainClouds != null) rainList.Add(cloudLayers.RainClouds);

                var wetRenderers = new List<Renderer>();
                string[] baseWetNames =
                {
                    "CobblestoneStreet", "SandShoulder_Front", "SandShoulder_Back",
                    "Sidewalk_Back", "ConcreteCurb_Back", "Bar_Floor", "HillRamp_Deck"
                };
                foreach (string wName in baseWetNames)
                {
                    GameObject go = FindInSceneByName(scene, wName);
                    if (go != null)
                    {
                        Renderer r = go.GetComponent<Renderer>();
                        if (r != null && !wetRenderers.Contains(r)) wetRenderers.Add(r);
                    }
                }
                for (int i = 0; i < extraWetRenderers.Count; i++)
                {
                    if (extraWetRenderers[i] != null && !wetRenderers.Contains(extraWetRenderers[i]))
                    {
                        wetRenderers.Add(extraWetRenderers[i]);
                    }
                }

                var so = new SerializedObject(weather);
                AssignObjectArray(so.FindProperty("dayOnlyObjects"), dayList);
                AssignObjectArray(so.FindProperty("nightOnlyObjects"), nightList);
                AssignObjectArray(so.FindProperty("clearNightOnlyObjects"), clearNightList);
                AssignObjectArray(so.FindProperty("rainOnlyObjects"), rainList);
                AssignObjectArray(so.FindProperty("wetSurfaceRenderers"), wetRenderers);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            StageInteractableManager manager = managerGo.GetComponent<StageInteractableManager>();
            if (manager != null)
            {
                manager.RefreshDiscoveredProps();
            }
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

        // ==================== REUSABLE PHASE 15.2 PROP PREFABS ====================

        private static void CreateOrUpdatePhase15_2PropPrefabs(MaterialPalette pal)
        {
            if (!AssetDatabase.IsValidFolder(StagePropsPrefabFolder))
            {
                Directory.CreateDirectory(StagePropsPrefabFolder);
                AssetDatabase.Refresh();
            }

            BuildHangingClothPrefab(HangingClothPrefabPath, pal.ClothesWhite, pal.WoodSign);
            BuildHangingLanternPrefab(HangingLanternPrefabPath, pal.MetalDark, pal.LanternBulbGlow);
            BuildDynamicConePrefab(DynamicConePrefabPath, pal.ConeOrange, pal.ConeStripeWhite, pal.MetalDark);
        }

        private static void BuildHangingClothPrefab(string prefabPath, Material clothMat, Material pinMat)
        {
            var root = new GameObject("Prop_HangingCloth");
            try
            {
                // Two small wooden clothespins on the kinematic anchor
                CreateBox("Clothespin_L", root.transform, new Vector3(-0.18f, 0.02f, 0f), Vector3.zero, new Vector3(0.035f, 0.08f, 0.045f), pinMat);
                CreateBox("Clothespin_R", root.transform, new Vector3(0.18f, 0.02f, 0f), Vector3.zero, new Vector3(0.035f, 0.08f, 0.045f), pinMat);

                var anchorRb = root.AddComponent<Rigidbody>();
                anchorRb.isKinematic = true;
                anchorRb.useGravity = false;

                var bodyGo = new GameObject("HangingBody");
                bodyGo.layer = StagePhysicsLayers.StageDebrisLayer;
                bodyGo.transform.SetParent(root.transform, false);
                bodyGo.transform.localPosition = new Vector3(0f, -0.32f, 0f);
                bodyGo.transform.localRotation = Quaternion.identity;

                CreateBox("Cloth_Body", bodyGo.transform, Vector3.zero, Vector3.zero, new Vector3(0.46f, 0.56f, 0.045f), clothMat);
                CreateBox("Cloth_SleeveL", bodyGo.transform, new Vector3(-0.28f, 0.13f, 0f), new Vector3(0f, 0f, 18f), new Vector3(0.18f, 0.22f, 0.042f), clothMat);
                CreateBox("Cloth_SleeveR", bodyGo.transform, new Vector3(0.28f, 0.13f, 0f), new Vector3(0f, 0f, -18f), new Vector3(0.18f, 0.22f, 0.042f), clothMat);

                var box = bodyGo.AddComponent<BoxCollider>();
                box.size = new Vector3(0.48f, 0.58f, 0.06f);
                box.center = Vector3.zero;
                box.isTrigger = false;

                var swingRb = bodyGo.AddComponent<Rigidbody>();
                swingRb.mass = 0.35f;
                swingRb.isKinematic = false;
                swingRb.useGravity = true;
                swingRb.linearDamping = 0.30f;
                swingRb.angularDamping = 0.70f;
                swingRb.sleepThreshold = 0.02f;
                swingRb.interpolation = RigidbodyInterpolation.Interpolate;

                var hinge = bodyGo.AddComponent<HingeJoint>();
                hinge.connectedBody = anchorRb;
                hinge.axis = Vector3.right;
                hinge.anchor = new Vector3(0f, 0.32f, 0f);
                hinge.autoConfigureConnectedAnchor = false;
                hinge.connectedAnchor = Vector3.zero;
                hinge.useSpring = true;
                hinge.spring = new JointSpring { spring = 2.8f, damper = 0.75f, targetPosition = 0f };
                hinge.useLimits = true;
                hinge.limits = new JointLimits { min = -65f, max = 65f, bounciness = 0.05f };

                var wind = bodyGo.AddComponent<StageWindAffectable>();
                wind.Configure(cd: 1.35f, areaM2: 0.34f, sensitivity: 1.35f, wakeThreshold: 0.65f, bodyOverride: swingRb);

                var hanging = root.AddComponent<StageHangingProp>();
                var so = new SerializedObject(hanging);
                so.FindProperty("anchorBody").objectReferenceValue = anchorRb;
                so.FindProperty("swingBody").objectReferenceValue = swingRb;
                so.FindProperty("hingeJoint").objectReferenceValue = hinge;
                so.FindProperty("windAffectable").objectReferenceValue = wind;
                so.FindProperty("hingeAxis").vector3Value = Vector3.right;
                so.FindProperty("localPivotOnSwingBody").vector3Value = new Vector3(0f, 0.32f, 0f);
                so.FindProperty("swingMass").floatValue = 0.35f;
                so.FindProperty("linearDamping").floatValue = 0.30f;
                so.FindProperty("angularDamping").floatValue = 0.70f;
                so.FindProperty("springStrength").floatValue = 2.8f;
                so.FindProperty("springDamper").floatValue = 0.75f;
                so.FindProperty("maxSwingAngle").floatValue = 65f;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildHangingLanternPrefab(string prefabPath, Material metalMat, Material bulbGlowMat)
        {
            var root = new GameObject("Prop_HangingLantern");
            try
            {
                CreateCyl("CeilingMount", root.transform, Vector3.zero, Vector3.zero, new Vector3(0.18f, 0.03f, 0.18f), metalMat);

                var anchorRb = root.AddComponent<Rigidbody>();
                anchorRb.isKinematic = true;
                anchorRb.useGravity = false;

                var bodyGo = new GameObject("HangingBody");
                bodyGo.layer = StagePhysicsLayers.StageDebrisLayer;
                bodyGo.transform.SetParent(root.transform, false);
                bodyGo.transform.localPosition = new Vector3(0f, -0.38f, 0f);
                bodyGo.transform.localRotation = Quaternion.identity;

                CreateCyl("ChainRod", bodyGo.transform, new Vector3(0f, 0.22f, 0f), Vector3.zero, new Vector3(0.025f, 0.16f, 0.025f), metalMat);
                CreateCyl("LanternShade", bodyGo.transform, new Vector3(0f, 0.05f, 0f), Vector3.zero, new Vector3(0.32f, 0.045f, 0.32f), metalMat);
                CreateCyl("CageRingBot", bodyGo.transform, new Vector3(0f, -0.16f, 0f), Vector3.zero, new Vector3(0.24f, 0.02f, 0.24f), metalMat);
                for (int i = 0; i < 4; i++)
                {
                    float ang = i * Mathf.PI * 0.5f;
                    CreateBox($"CageBar_{i}", bodyGo.transform, new Vector3(Mathf.Cos(ang) * 0.105f, -0.055f, Mathf.Sin(ang) * 0.105f), Vector3.zero, new Vector3(0.018f, 0.21f, 0.018f), metalMat);
                }
                CreateSphere("LanternBulb", bodyGo.transform, new Vector3(0f, -0.05f, 0f), Vector3.zero, new Vector3(0.14f, 0.17f, 0.14f), bulbGlowMat);

                var box = bodyGo.AddComponent<BoxCollider>();
                box.size = new Vector3(0.30f, 0.34f, 0.30f);
                box.center = new Vector3(0f, -0.04f, 0f);
                box.isTrigger = false;

                var swingRb = bodyGo.AddComponent<Rigidbody>();
                swingRb.mass = 0.95f;
                swingRb.isKinematic = false;
                swingRb.useGravity = true;
                swingRb.linearDamping = 0.25f;
                swingRb.angularDamping = 0.80f;
                swingRb.sleepThreshold = 0.02f;
                swingRb.interpolation = RigidbodyInterpolation.Interpolate;

                var hinge = bodyGo.AddComponent<HingeJoint>();
                hinge.connectedBody = anchorRb;
                hinge.axis = Vector3.forward;
                hinge.anchor = new Vector3(0f, 0.38f, 0f);
                hinge.autoConfigureConnectedAnchor = false;
                hinge.connectedAnchor = Vector3.zero;
                hinge.useSpring = true;
                hinge.spring = new JointSpring { spring = 5.5f, damper = 1.3f, targetPosition = 0f };
                hinge.useLimits = true;
                hinge.limits = new JointLimits { min = -52f, max = 52f, bounciness = 0.08f };

                var wind = bodyGo.AddComponent<StageWindAffectable>();
                wind.Configure(cd: 1.20f, areaM2: 0.24f, sensitivity: 1.15f, wakeThreshold: 0.75f, bodyOverride: swingRb);

                var hanging = root.AddComponent<StageHangingProp>();
                var so = new SerializedObject(hanging);
                so.FindProperty("anchorBody").objectReferenceValue = anchorRb;
                so.FindProperty("swingBody").objectReferenceValue = swingRb;
                so.FindProperty("hingeJoint").objectReferenceValue = hinge;
                so.FindProperty("windAffectable").objectReferenceValue = wind;
                so.FindProperty("hingeAxis").vector3Value = Vector3.forward;
                so.FindProperty("localPivotOnSwingBody").vector3Value = new Vector3(0f, 0.38f, 0f);
                so.FindProperty("swingMass").floatValue = 0.95f;
                so.FindProperty("linearDamping").floatValue = 0.25f;
                so.FindProperty("angularDamping").floatValue = 0.80f;
                so.FindProperty("springStrength").floatValue = 5.5f;
                so.FindProperty("springDamper").floatValue = 1.3f;
                so.FindProperty("maxSwingAngle").floatValue = 52f;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildDynamicConePrefab(string prefabPath, Material orangeMat, Material whiteStripeMat, Material baseMat)
        {
            var root = new GameObject("Prop_DynamicCone");
            root.layer = StagePhysicsLayers.StageDebrisLayer;
            try
            {
                // Urban traffic cone built from stacked tapered tiers + weighted base
                CreateBox("ConeBase", root.transform, new Vector3(0f, 0.025f, 0f), Vector3.zero, new Vector3(0.36f, 0.05f, 0.36f), baseMat);
                CreateCyl("ConeTier_0", root.transform, new Vector3(0f, 0.13f, 0f), Vector3.zero, new Vector3(0.25f, 0.09f, 0.25f), orangeMat);
                CreateCyl("ConeStripe", root.transform, new Vector3(0f, 0.26f, 0f), Vector3.zero, new Vector3(0.195f, 0.055f, 0.195f), whiteStripeMat);
                CreateCyl("ConeTier_1", root.transform, new Vector3(0f, 0.37f, 0f), Vector3.zero, new Vector3(0.145f, 0.07f, 0.145f), orangeMat);
                CreateCyl("ConeTip", root.transform, new Vector3(0f, 0.46f, 0f), Vector3.zero, new Vector3(0.085f, 0.04f, 0.085f), orangeMat);

                var col = root.AddComponent<BoxCollider>();
                col.isTrigger = false;
                col.size = new Vector3(0.34f, 0.50f, 0.34f);
                col.center = new Vector3(0f, 0.25f, 0f);

                var rb = root.AddComponent<Rigidbody>();
                rb.mass = 0.45f;
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.sleepThreshold = 0.08f;
                rb.linearDamping = 0.38f;
                rb.angularDamping = 0.58f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                var wind = root.AddComponent<StageWindAffectable>();
                wind.Configure(cd: 1.05f, areaM2: 0.16f, sensitivity: 0.92f, wakeThreshold: 4.4f, bodyOverride: rb);

                var dyn = root.AddComponent<StageDynamicProp>();
                var so = new SerializedObject(dyn);
                so.FindProperty("body").objectReferenceValue = rb;
                so.FindProperty("propCollider").objectReferenceValue = col;
                so.FindProperty("windAffectable").objectReferenceValue = wind;
                so.FindProperty("materialType").enumValueIndex = (int)StagePropMaterialType.Plastic;
                so.FindProperty("mass").floatValue = 0.45f;
                so.FindProperty("linearDamping").floatValue = 0.38f;
                so.FindProperty("angularDamping").floatValue = 0.58f;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ==================== ARCHITECTURAL MICRO-BUILDERS ====================

        private static void BuildStormDrainGrate(string name, Transform parent, Vector3 pos, MaterialPalette pal)
        {
            var grate = new GameObject(name);
            grate.transform.SetParent(parent, false);
            grate.transform.position = pos;
            CreateBox("FrameBack", grate.transform, new Vector3(0f, 0f, 0.15f), Vector3.zero, new Vector3(0.84f, 0.02f, 0.05f), pal.MetalDark);
            CreateBox("FrameFront", grate.transform, new Vector3(0f, 0f, -0.15f), Vector3.zero, new Vector3(0.84f, 0.02f, 0.05f), pal.MetalDark);
            CreateBox("FrameLeft", grate.transform, new Vector3(-0.395f, 0f, 0f), Vector3.zero, new Vector3(0.05f, 0.02f, 0.34f), pal.MetalDark);
            CreateBox("FrameRight", grate.transform, new Vector3(0.395f, 0f, 0f), Vector3.zero, new Vector3(0.05f, 0.02f, 0.34f), pal.MetalDark);
            CreateBox("DrainRecess", grate.transform, new Vector3(0f, -0.008f, 0f), Vector3.zero, new Vector3(0.74f, 0.006f, 0.25f), pal.DarkInterior);
            for (int b = 0; b < 6; b++)
            {
                float bx = Mathf.Lerp(-0.30f, 0.30f, b / 5f);
                CreateBox($"Bar_{b}", grate.transform, new Vector3(bx, 0.003f, 0f), Vector3.zero, new Vector3(0.035f, 0.018f, 0.28f), pal.MetalDark);
            }
        }

        private static void BuildTvAntenna(string name, Transform parent, Vector3 basePos, float height, MaterialPalette pal)
        {
            var ant = new GameObject(name);
            ant.transform.SetParent(parent, false);
            ant.transform.position = basePos;
            CreateCyl("Mast", ant.transform, new Vector3(0f, height * 0.5f, 0f), Vector3.zero, new Vector3(0.035f, height * 0.5f, 0.035f), pal.MetalDark);
            CreateBox("Boom", ant.transform, new Vector3(0f, height * 0.88f, 0f), new Vector3(0f, 22f, 0f), new Vector3(0.95f, 0.026f, 0.026f), pal.RoofMetal);
            for (int e = 0; e < 4; e++)
            {
                float ex = Mathf.Lerp(-0.38f, 0.38f, e / 3f);
                float len = Mathf.Lerp(0.68f, 0.32f, e / 3f);
                CreateBox($"Element_{e}", ant.transform, new Vector3(ex, height * 0.88f, 0f), new Vector3(0f, 22f, 0f), new Vector3(0.022f, 0.022f, len), pal.RoofMetal);
            }
        }

        private static void BuildSatelliteDish(string name, Transform parent, Vector3 basePos, float yaw, MaterialPalette pal)
        {
            var sat = new GameObject(name);
            sat.transform.SetParent(parent, false);
            sat.transform.position = basePos;
            sat.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            CreateCyl("Stand", sat.transform, new Vector3(0f, 0.32f, 0f), Vector3.zero, new Vector3(0.05f, 0.32f, 0.05f), pal.MetalDark);
            CreateCyl("Dish", sat.transform, new Vector3(0f, 0.68f, -0.06f), new Vector3(62f, 0f, 0f), new Vector3(0.76f, 0.045f, 0.76f), pal.RoofMetal);
            CreateCyl("LnbArm", sat.transform, new Vector3(0f, 0.72f, -0.26f), new Vector3(62f, 0f, 0f), new Vector3(0.024f, 0.22f, 0.024f), pal.MetalDark);
            CreateSphere("LnbHead", sat.transform, new Vector3(0f, 0.82f, -0.44f), Vector3.zero, new Vector3(0.09f, 0.09f, 0.09f), pal.MetalDark);
        }

        private static void BuildWaterTank(string name, Transform parent, Vector3 basePos, float scale, MaterialPalette pal)
        {
            var tank = new GameObject(name);
            tank.transform.SetParent(parent, false);
            tank.transform.position = basePos;
            tank.transform.localScale = Vector3.one * scale;
            CreateCyl("TankBody", tank.transform, new Vector3(0f, 0.44f, 0f), Vector3.zero, new Vector3(1.06f, 0.44f, 1.06f), pal.PlasticBlueTank);
            CreateCyl("TankLid", tank.transform, new Vector3(0f, 0.90f, 0f), Vector3.zero, new Vector3(1.12f, 0.04f, 1.12f), pal.PlasticBlueTank);
        }

        private static void BuildPottedPlant(string name, Transform parent, Vector3 basePos, MaterialPalette pal)
        {
            var pot = new GameObject(name);
            pot.transform.SetParent(parent, false);
            pot.transform.position = basePos;
            CreateCyl("PotBody", pot.transform, new Vector3(0f, 0.14f, 0f), Vector3.zero, new Vector3(0.28f, 0.14f, 0.28f), pal.TerracottaPot);
            CreateCyl("PotRim", pot.transform, new Vector3(0f, 0.27f, 0f), Vector3.zero, new Vector3(0.32f, 0.025f, 0.32f), pal.TerracottaPot);
            CreateSphere("Foliage_Center", pot.transform, new Vector3(0f, 0.44f, 0f), Vector3.zero, new Vector3(0.42f, 0.36f, 0.42f), pal.PalmLeaf);
            CreateSphere("Foliage_Side", pot.transform, new Vector3(0.14f, 0.38f, -0.06f), Vector3.zero, new Vector3(0.28f, 0.24f, 0.28f), pal.PalmLeaf);
        }

        private static void BuildTropicalPalm(string name, Transform parent, Vector3 basePos, float height, MaterialPalette pal)
        {
            var palm = new GameObject(name);
            palm.transform.SetParent(parent, false);
            palm.transform.position = basePos;
            int segs = 4;
            float segH = height / segs;
            for (int i = 0; i < segs; i++)
            {
                float r = Mathf.Lerp(0.15f, 0.09f, i / (float)(segs - 1));
                CreateCyl($"Trunk_{i}", palm.transform, new Vector3(i * 0.05f, segH * (i + 0.5f), 0f), new Vector3(0f, 0f, -3f), new Vector3(r, segH * 0.52f, r), pal.PalmBark);
            }
            Vector3 crown = new Vector3((segs - 1) * 0.05f, height, 0f);
            for (int f = 0; f < 7; f++)
            {
                float yaw = f * (360f / 7f);
                var frondHolder = new GameObject($"Frond_{f}");
                frondHolder.transform.SetParent(palm.transform, false);
                frondHolder.transform.localPosition = crown;
                frondHolder.transform.localRotation = Quaternion.Euler(24f, yaw, 0f);
                CreateBox("Blade", frondHolder.transform, new Vector3(0f, -0.08f, 0.78f), new Vector3(14f, 0f, 0f), new Vector3(0.34f, 0.04f, 1.55f), pal.PalmLeaf);
            }
        }

        // ==================== URP MATERIAL ASSET FACTORY ====================

        private static MaterialPalette EnsureMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            {
                Directory.CreateDirectory(MaterialsFolder);
                AssetDatabase.Refresh();
            }

            return new MaterialPalette
            {
                RedBrick = LoadMat("Favela_RedBrick.mat"),
                StuccoOchre = LoadMat("Favela_StuccoOchre.mat"),
                StuccoCoral = LoadMat("Favela_StuccoCoral.mat"),
                StuccoGreen = LoadMat("Favela_StuccoGreen.mat"),
                StuccoWhite = LoadMat("Favela_StuccoWhite.mat"),
                StuccoLightBlue = EnsureLitMaterial("Favela_StuccoLightBlue.mat", new Color(0.56f, 0.72f, 0.84f, 1f), metallic: 0f, smoothness: 0.16f),
                SidewalkConcrete = LoadMat("Favela_SidewalkConcrete.mat"),
                PoleConcrete = LoadMat("Favela_PoleConcrete.mat"),
                RoofMetal = LoadMat("Favela_RoofMetal.mat"),
                BlackCable = LoadMat("Favela_BlackCable.mat"),
                WoodSign = LoadMat("Favela_WoodSign.mat"),
                SteelShutter = LoadMat("Favela_SteelShutter.mat"),
                PlasticRed = LoadMat("Favela_PlasticRed.mat"),
                PlasticYellow = LoadMat("Favela_PlasticYellow.mat"),
                PlasticBlueTank = LoadMat("Favela_PlasticBlueTank.mat"),
                BottleAmber = LoadMat("Favela_BottleAmber.mat"),
                BottleGreen = LoadMat("Favela_BottleGreen.mat"),
                PalmBark = LoadMat("Favela_PalmBark.mat"),
                PalmLeaf = LoadMat("Favela_PalmLeaf.mat"),
                ClothesWhite = LoadMat("Favela_ClothesWhite.mat"),
                ClothesOrange = LoadMat("Favela_ClothesOrange.mat"),
                ClothesBlue = LoadMat("Favela_ClothesBlue.mat"),
                WarmEstufaGlow = LoadMat("Favela_WarmEstufaGlow.mat"),
                StreetLampGlow = LoadMat("Favela_StreetLampGlow.mat"),
                SunGlow = LoadMat("Favela_SunGlow.mat"),
                DarkInterior = LoadMat("Favela_DarkInterior.mat"),
                WaterBay = LoadMat("Favela_WaterBay.mat"),
                DistantMountain = LoadMat("Favela_DistantMountain.mat"),
                DistantMountainFar = LoadMat("Favela_DistantMountainFar.mat"),

                MetalDark = EnsureLitMaterial("Favela_MetalDark.mat", new Color(0.14f, 0.15f, 0.17f, 1f), metallic: 0.65f, smoothness: 0.35f),
                NightWindowGlow = EnsureEmissiveLitMaterial("Favela_NightWindowGlow.mat", new Color(1.0f, 0.78f, 0.36f, 1f), new Color(1.0f, 0.66f, 0.22f, 1f) * 1.65f, smoothness: 0.25f),
                NightWindowWarmAlt = EnsureEmissiveLitMaterial("Favela_NightWindowWarmAlt.mat", new Color(1.0f, 0.66f, 0.28f, 1f), new Color(1.0f, 0.50f, 0.14f, 1f) * 1.55f, smoothness: 0.25f),
                RainPuddle = EnsureTransparentReflectiveMaterial("Favela_RainPuddle.mat", new Color(0.025f, 0.04f, 0.07f, 0.42f), metallic: 0.38f, smoothness: 0.95f),
                MoonGlow = EnsureUnlitMaterial("Favela_MoonGlow.mat", new Color(0.92f, 0.95f, 1.0f, 1f)),
                StarGlow = EnsureUnlitMaterial("Favela_StarGlow.mat", new Color(0.88f, 0.92f, 1.0f, 1f)),
                PosterCyan = EnsureLitMaterial("Favela_PosterCyan.mat", new Color(0.14f, 0.68f, 0.82f, 1f), metallic: 0f, smoothness: 0.18f),
                PosterYellow = EnsureLitMaterial("Favela_PosterYellow.mat", new Color(0.95f, 0.78f, 0.18f, 1f), metallic: 0f, smoothness: 0.18f),
                PosterRed = EnsureLitMaterial("Favela_PosterRed.mat", new Color(0.84f, 0.20f, 0.16f, 1f), metallic: 0f, smoothness: 0.18f),
                PosterWhite = EnsureLitMaterial("Favela_PosterWhite.mat", new Color(0.92f, 0.90f, 0.84f, 1f), metallic: 0f, smoothness: 0.15f),
                GraffitiMagenta = EnsureLitMaterial("Favela_GraffitiMagenta.mat", new Color(0.82f, 0.18f, 0.52f, 1f), metallic: 0f, smoothness: 0.20f),
                GraffitiTeal = EnsureLitMaterial("Favela_GraffitiTeal.mat", new Color(0.10f, 0.64f, 0.66f, 1f), metallic: 0f, smoothness: 0.20f),
                GraffitiGold = EnsureLitMaterial("Favela_GraffitiGold.mat", new Color(0.94f, 0.68f, 0.14f, 1f), metallic: 0f, smoothness: 0.22f),
                ConeOrange = EnsureLitMaterial("Favela_ConeOrange.mat", new Color(0.96f, 0.36f, 0.06f, 1f), metallic: 0f, smoothness: 0.45f),
                ConeStripeWhite = EnsureLitMaterial("Favela_ConeStripeWhite.mat", new Color(0.96f, 0.96f, 0.96f, 1f), metallic: 0f, smoothness: 0.65f),
                LanternBulbGlow = EnsureEmissiveLitMaterial("Favela_LanternBulbGlow.mat", new Color(1.0f, 0.86f, 0.52f, 1f), new Color(1.0f, 0.72f, 0.28f, 1f) * 2.8f, smoothness: 0.40f),
                TerracottaPot = EnsureLitMaterial("Favela_TerracottaPot.mat", new Color(0.68f, 0.32f, 0.18f, 1f), metallic: 0f, smoothness: 0.15f),
                ShorelineLightGlow = EnsureUnlitMaterial("Favela_ShorelineLightGlow.mat", new Color(1.0f, 0.84f, 0.46f, 1f)),
                Clouds = StageFramingStandards.EnsureStandardCloudMaterials(MaterialsFolder)
            };
        }

        private static Material LoadMat(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{fileName}");
        }

        private static Material EnsureLitMaterial(string fileName, Color baseColor, float metallic, float smoothness)
        {
            string path = $"{MaterialsFolder}/{fileName}";
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

        private static Material EnsureTransparentReflectiveMaterial(string fileName, Color baseColor, float metallic, float smoothness)
        {
            Material mat = EnsureLitMaterial(fileName, baseColor, metallic, smoothness);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);     // Alpha
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
            string path = $"{MaterialsFolder}/{fileName}";
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

        // ==================== PRIMITIVE & SCENE HELPERS ====================

        private static GameObject CreateBox(string name, Transform parent, Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat)
        {
            return CreatePrimitive(name, PrimitiveType.Cube, parent, localPos, localEuler, localScale, mat);
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
            if (r != null)
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
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

        private static void RemoveChildIfExists(Transform parent, string childName)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform c = parent.GetChild(i);
                if (c != null && c.name == childName)
                {
                    Object.DestroyImmediate(c.gameObject);
                }
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
                if (toDestroy[i] != null)
                {
                    Object.DestroyImmediate(toDestroy[i]);
                }
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
