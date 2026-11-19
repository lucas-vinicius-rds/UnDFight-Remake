using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnDFight.Editor
{
    /// <summary>
    /// Reusable framing, floor-coverage, anti-Z-fighting, and atmospheric cloud standards for ALL
    /// 2.5D arenas in UnDFight Remake (Arena_Favela, future Arena_UnDF, future Arena_Cadeia, etc.).
    /// </summary>
    public static class StageFramingStandards
    {
        /// <summary>
        /// Minimum foreground floor Z extent required so the bottom frustum edge of FightCameraController
        /// never reveals void below the stage during max zoom-out (12m), high jumps, shake, or 21:9 ultrawide.
        /// </summary>
        public const float MinForegroundFloorZ = -22.0f;

        /// <summary>
        /// Minimum total horizontal width of ground planes (covers x in [-32, +32] with safety margin).
        /// </summary>
        public const float MinFloorWidthX = 64.0f;

        /// <summary>
        /// Minimum horizontal extent from center on both left and right sides for side backdrop architecture.
        /// </summary>
        public const float MinSideBackdropExtentX = 22.0f;

        /// <summary>
        /// Minimum offset between overlapping visual surfaces (posters, graffiti, lit windows, grates, puddles)
        /// and their host geometry to guarantee zero Z-fighting.
        /// </summary>
        public const float MinSurfaceSeparation = 0.025f;

        public struct WeatherCloudLayers
        {
            public GameObject DayClouds;
            public GameObject ClearNightClouds;
            public GameObject RainClouds;
        }

        public struct CloudMaterials
        {
            public Material DayCloud;
            public Material ClearNightCloud;
            public Material StormCloud;
        }

        /// <summary>
        /// Resizes and repositions a foreground floor strip (box primitive with unit cube mesh) so its
        /// front edge reaches targetFrontZ while keeping its back seam locked exactly at backEdgeZ
        /// (zero overlap / zero Z-fighting against the adjacent combat lane floor).
        /// </summary>
        public static void EnsureForegroundFloorCoverage(
            Transform floorTransform,
            float backEdgeZ,
            float targetFrontZ = MinForegroundFloorZ,
            float minWidthX = MinFloorWidthX)
        {
            if (floorTransform == null)
            {
                return;
            }

            float frontZ = Mathf.Min(targetFrontZ, backEdgeZ - 1.0f);
            float depthZ = backEdgeZ - frontZ;
            float centerZ = (backEdgeZ + frontZ) * 0.5f;

            Vector3 pos = floorTransform.localPosition;
            pos.z = centerZ;
            floorTransform.localPosition = pos;

            Vector3 scale = floorTransform.localScale;
            scale.x = Mathf.Max(scale.x, minWidthX);
            scale.z = depthZ;
            floorTransform.localScale = scale;
        }

        /// <summary>
        /// Analytically validates that the camera's bottom frustum edge (left, center, right rays)
        /// intersects y = 0 strictly inside combinedFloorBounds at FightCameraController max zoom-out
        /// (P1 = stageMinX, P2 = stageMaxX, CameraDistance = maxDistance) across 16:9 and 21:9 aspects.
        /// </summary>
        public static bool ValidateCameraBottomFloorCoverage(
            Camera cam,
            FightCameraController camCtrl,
            Bounds combinedFloorBounds)
        {
            return ValidateCameraBottomFloorCoverage(cam, camCtrl, combinedFloorBounds, out _);
        }

        public static bool ValidateCameraBottomFloorCoverage(
            Camera cam,
            FightCameraController camCtrl,
            Bounds combinedFloorBounds,
            out string failureReason,
            float safetyMargin = 0.25f)
        {
            failureReason = string.Empty;

            if (cam == null)
            {
                failureReason = "Camera is null.";
                return false;
            }

            float maxDistance = camCtrl != null ? camCtrl.MaxDistance : 12.0f;
            float heightOffset = camCtrl != null ? camCtrl.HeightOffset : 1.35f;
            float stageMinX = camCtrl != null ? camCtrl.StageMinX : -8.0f;
            float stageMaxX = camCtrl != null ? camCtrl.StageMaxX : 8.0f;

            float fovDeg = cam.fieldOfView > 1f ? cam.fieldOfView : 40f;
            float tanHalfV = Mathf.Tan(fovDeg * 0.5f * Mathf.Deg2Rad);
            Quaternion camRot = cam.transform.rotation;

            // Evaluate max zoom-out at center (P1=stageMinX, P2=stageMaxX) plus edge-clamped camera X positions
            float halfViewWidth = maxDistance * 0.45f;
            float clampedMinX = Mathf.Min(stageMinX + halfViewWidth, 0f);
            float clampedMaxX = Mathf.Max(stageMaxX - halfViewWidth, 0f);
            float[] testCamXs = { 0f, clampedMinX, clampedMaxX };
            float[] testAspects = { 16f / 9f, 21f / 9f };
            float[] rayUs = { -1f, 0f, 1f };

            // Also include worst-case downward impact shake (-0.25m) to be extra strict
            float[] testHeights = { heightOffset, Mathf.Max(0.85f, heightOffset - 0.25f) };

            foreach (float aspect in testAspects)
            {
                float tanHalfH = tanHalfV * aspect;
                foreach (float camX in testCamXs)
                {
                    foreach (float camY in testHeights)
                    {
                        Vector3 origin = new Vector3(camX, camY, -maxDistance);
                        foreach (float u in rayUs)
                        {
                            Vector3 localDir = new Vector3(u * tanHalfH, -tanHalfV, 1f).normalized;
                            Vector3 worldDir = camRot * localDir;

                            if (worldDir.y >= -1e-4f)
                            {
                                failureReason = $"Bottom frustum ray (u={u:F1}, aspect={aspect:F2}) does not slope downward toward y=0 (dir.y={worldDir.y:F4}).";
                                return false;
                            }

                            float t = -origin.y / worldDir.y;
                            Vector3 hit = origin + worldDir * t;

                            if (hit.z < combinedFloorBounds.min.z + safetyMargin ||
                                hit.z > combinedFloorBounds.max.z - safetyMargin)
                            {
                                failureReason = $"Bottom ray (u={u:F1}, camX={camX:F2}, aspect={aspect:F2}, dist={maxDistance:F1}) hits y=0 at z={hit.z:F2}, outside floor Z bounds [{combinedFloorBounds.min.z:F2}..{combinedFloorBounds.max.z:F2}] with margin {safetyMargin:F2}.";
                                return false;
                            }

                            if (hit.x < combinedFloorBounds.min.x + safetyMargin ||
                                hit.x > combinedFloorBounds.max.x - safetyMargin)
                            {
                                failureReason = $"Bottom ray (u={u:F1}, camX={camX:F2}, aspect={aspect:F2}, dist={maxDistance:F1}) hits y=0 at x={hit.x:F2}, outside floor X bounds [{combinedFloorBounds.min.x:F2}..{combinedFloorBounds.max.x:F2}] with margin {safetyMargin:F2}.";
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Ensures reusable stylized cloud URP materials exist in the specified folder.
        /// </summary>
        public static CloudMaterials EnsureStandardCloudMaterials(string materialsFolder)
        {
            if (!AssetDatabase.IsValidFolder(materialsFolder))
            {
                Directory.CreateDirectory(materialsFolder);
                AssetDatabase.Refresh();
            }

            return new CloudMaterials
            {
                DayCloud = EnsureCloudMaterial($"{materialsFolder}/Stage_CloudDay.mat", new Color(0.99f, 0.80f, 0.60f, 0.78f)),
                ClearNightCloud = EnsureCloudMaterial($"{materialsFolder}/Stage_CloudNight.mat", new Color(0.11f, 0.16f, 0.28f, 0.58f)),
                StormCloud = EnsureCloudMaterial($"{materialsFolder}/Stage_CloudStorm.mat", new Color(0.14f, 0.17f, 0.24f, 0.88f))
            };
        }

        /// <summary>
        /// Builds three reusable weather-specific stylized cloud containers (Day, ClearNight, RainyNight)
        /// ready to be wired into StageWeatherController.dayOnlyObjects, clearNightOnlyObjects, and rainOnlyObjects.
        /// </summary>
        public static WeatherCloudLayers BuildWeatherCloudLayer(
            Transform parent,
            string namePrefix,
            Vector3 regionCenter,
            Vector3 regionSize,
            Material dayMat,
            Material clearNightMat,
            Material stormMat,
            int dayCount = 6,
            int clearNightCount = 5,
            int stormCount = 9)
        {
            var result = new WeatherCloudLayers
            {
                DayClouds = BuildSingleCloudGroup(parent, $"{namePrefix}_DayClouds", regionCenter, regionSize, dayMat, dayCount, scaleMultiplier: 1.0f, seedOffset: 11),
                ClearNightClouds = BuildSingleCloudGroup(parent, $"{namePrefix}_ClearNightClouds", regionCenter + new Vector3(0f, 0.8f, 0.5f), regionSize, clearNightMat, clearNightCount, scaleMultiplier: 0.92f, seedOffset: 37),
                RainClouds = BuildSingleCloudGroup(parent, $"{namePrefix}_RainClouds", regionCenter + new Vector3(0f, -0.6f, -1.2f), new Vector3(regionSize.x * 1.15f, regionSize.y * 0.85f, regionSize.z), stormMat, stormCount, scaleMultiplier: 1.38f, seedOffset: 73)
            };

            return result;
        }

        private static GameObject BuildSingleCloudGroup(
            Transform parent,
            string groupName,
            Vector3 regionCenter,
            Vector3 regionSize,
            Material cloudMat,
            int clusterCount,
            float scaleMultiplier,
            int seedOffset)
        {
            var root = new GameObject(groupName);
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            int count = Mathf.Max(1, clusterCount);
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (float)(count - 1) : 0.5f;
                float hashA = Mathf.Sin((i + seedOffset) * 12.9898f) * 0.5f + 0.5f;
                float hashB = Mathf.Cos((i + seedOffset) * 78.233f) * 0.5f + 0.5f;

                float cx = regionCenter.x + Mathf.Lerp(-regionSize.x * 0.5f, regionSize.x * 0.5f, t) + (hashA - 0.5f) * 2.2f;
                float cy = regionCenter.y + (hashB - 0.5f) * regionSize.y;
                float cz = regionCenter.z + (hashA - 0.5f) * regionSize.z;

                var cluster = new GameObject($"CloudCluster_{i}");
                cluster.transform.SetParent(root.transform, false);
                cluster.transform.position = new Vector3(cx, cy, cz);

                float baseW = (4.6f + hashA * 3.4f) * scaleMultiplier;
                float baseH = (1.15f + hashB * 0.75f) * scaleMultiplier;
                float baseD = (1.4f + hashA * 0.8f) * scaleMultiplier;

                CreateCloudPuff("Puff_Center", cluster.transform, Vector3.zero, new Vector3(baseW, baseH, baseD), cloudMat);
                CreateCloudPuff("Puff_Left", cluster.transform, new Vector3(-baseW * 0.34f, -baseH * 0.14f, 0.15f), new Vector3(baseW * 0.68f, baseH * 0.78f, baseD * 0.85f), cloudMat);
                CreateCloudPuff("Puff_Right", cluster.transform, new Vector3(baseW * 0.36f, -baseH * 0.10f, -0.15f), new Vector3(baseW * 0.72f, baseH * 0.82f, baseD * 0.85f), cloudMat);
                CreateCloudPuff("Puff_Top", cluster.transform, new Vector3((hashB - 0.5f) * baseW * 0.25f, baseH * 0.28f, 0.08f), new Vector3(baseW * 0.55f, baseH * 0.72f, baseD * 0.80f), cloudMat);
            }

            return root;
        }

        private static void CreateCloudPuff(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.name = name;
            puff.transform.SetParent(parent, false);
            puff.transform.localPosition = localPos;
            puff.transform.localScale = localScale;

            Collider col = puff.GetComponent<Collider>();
            if (col != null)
            {
                Object.DestroyImmediate(col);
            }

            MeshRenderer mr = puff.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                if (mat != null)
                {
                    mr.sharedMaterial = mat;
                }
            }
        }

        private static Material EnsureCloudMaterial(string assetPath, Color tint)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Unlit/Color");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, assetPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
