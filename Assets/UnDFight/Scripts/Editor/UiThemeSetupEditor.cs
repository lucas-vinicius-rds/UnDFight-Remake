using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UnDFight.Editor
{
    /// <summary>
    /// Phase 17.1 Editor utility:
    /// 1) Generates and persists all matte MKX + official UnDF UI sprites in Assets/UnDFight/Art/UI/
    ///    (including Logo_UnDF_Emblem extracted cleanly from Logo_UnDF.png with soft white-removal AA).
    /// 2) Rebuilds MainMenu.unity with the atmospheric Arena_UnDF backdrop + MKX arched cards + clean 6-item list.
    /// 3) Rebuilds CharacterSelect.unity with open top 65% 3D staging, UnDF blue/crimson rim lights,
    ///    3D stage preview backdrop, scalable fighter/stage bars, and matte Arcade Tower UI.
    /// </summary>
    public static class UiThemeSetupEditor
    {
        public const string UiArtFolder = "Assets/UnDFight/Art/UI";
        public const string DocsLogoPath = "Docs/Referencias/Logo_UnDF.png";

        public const string LogoFullPath = UiArtFolder + "/Logo_UnDF.png";
        public const string LogoEmblemPath = UiArtFolder + "/Logo_UnDF_Emblem.png";
        public const string ArchedCardPath = UiArtFolder + "/UI_ArchedCard.png";
        public const string HexBadgePath = UiArtFolder + "/UI_HexBadge.png";
        public const string DiamondPath = UiArtFolder + "/UI_Diamond.png";
        public const string LockIconPath = UiArtFolder + "/UI_LockIcon.png";
        public const string SoftSweepPath = UiArtFolder + "/UI_SoftSweep.png";
        public const string BladeBarLeftPath = UiArtFolder + "/UI_BladeBarLeft.png";
        public const string BladeBarRightPath = UiArtFolder + "/UI_BladeBarRight.png";
        public const string VignetteVerticalPath = UiArtFolder + "/UI_VignetteVertical.png";
        public const string CircleBadgePath = UiArtFolder + "/UI_CircleBadge.png";

        public const string WhiteSpritePath = "Assets/UnDFight/Art/Textures/UI_White.png";
        public const string SelectFloorMatPath = UiArtFolder + "/Mat_SelectFloor.mat";
        public const string MainMenuScenePath = "Assets/UnDFight/Scenes/MainMenu.unity";
        public const string CharacterSelectScenePath = "Assets/UnDFight/Scenes/CharacterSelect.unity";
        public const string FighterRosterPath = "Assets/UnDFight/Data/FighterRoster.asset";
        public const string StageRosterPath = "Assets/UnDFight/Data/StageRoster.asset";

        public static readonly string[] CombatArenaScenePaths =
        {
            "Assets/UnDFight/Scenes/Arena_Favela.unity",
            "Assets/UnDFight/Scenes/Arena_UnDF.unity",
            "Assets/UnDFight/Scenes/Arena_Cadeia.unity",
            "Assets/UnDFight/Scenes/Training.unity"
        };

        [MenuItem("UnDFight/UI/Rebuild MKX UnDF UI Theme & Menus", false, 130)]
        public static void RebuildAllUiThemeAndMenus()
        {
            GenerateAndPersistAllUiSprites();

            string previousScenePath = SceneManager.GetActiveScene().path;
            RebuildMainMenuScene();
            RebuildCharacterSelectScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!string.IsNullOrEmpty(previousScenePath) && File.Exists(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
            else
            {
                EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            }

            Debug.Log("[UnDFight] Fase 17.1 complete: Generated MKX + official UnDF sprites in Assets/UnDFight/Art/UI/ and rebuilt MainMenu.unity & CharacterSelect.unity.");
        }

        public static void GenerateAndPersistAllUiSprites()
        {
            EnsureFolder(UiArtFolder);

            // Ensure Logo_UnDF.png exists (copy from Docs/Referencias if needed, never delete original)
            string fullLogoDst = Path.GetFullPath(LogoFullPath);
            string fullLogoDocs = Path.GetFullPath(DocsLogoPath);
            if (!File.Exists(fullLogoDst) && File.Exists(fullLogoDocs))
            {
                File.Copy(fullLogoDocs, fullLogoDst, overwrite: true);
            }

            AssetDatabase.ImportAsset(LogoFullPath, ImportAssetOptions.ForceUpdate);
            ConfigureSpriteImporter(LogoFullPath, Vector4.zero);

            GenerateEmblemFromOfficialLogo();
            GenerateArchedCardSprite();
            GenerateHexBadgeSprite();
            GenerateDiamondSprite();
            GenerateLockIconSprite();
            GenerateSoftSweepSprite();
            GenerateBladeBarSprite(BladeBarLeftPath, flipRight: false);
            GenerateBladeBarSprite(BladeBarRightPath, flipRight: true);
            GenerateVignetteVerticalSprite();
            GenerateCircleBadgeSprite();

            AssetDatabase.Refresh();
        }

        // ==================== 1. PROCEDURAL & EXTRACTED UI SPRITES ====================

        private static void GenerateEmblemFromOfficialLogo()
        {
            string fullSrc = Path.GetFullPath(LogoFullPath);
            if (!File.Exists(fullSrc))
            {
                Debug.LogError($"[UiThemeSetupEditor] Official logo not found at {LogoFullPath}");
                return;
            }

            byte[] bytes = File.ReadAllBytes(fullSrc);
            var srcTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!srcTex.LoadImage(bytes))
            {
                Object.DestroyImmediate(srcTex);
                Debug.LogError($"[UiThemeSetupEditor] Failed to decode {LogoFullPath}");
                return;
            }

            int sw = srcTex.width;
            int sh = srcTex.height;
            Color[] srcPixels = srcTex.GetPixels();

            // Locate tight bounding box of the blue diamond/chevron emblem in the upper half of Logo_UnDF.png
            int minX = sw;
            int maxX = 0;
            int minY = sh;
            int maxY = 0;
            for (int y = 0; y < sh; y++)
            {
                for (int x = 0; x < sw; x++)
                {
                    Color c = srcPixels[y * sw + x];
                    // Official chevrons are distinct blues where B clearly exceeds R
                    if (c.b > c.r + 0.06f && c.b > 0.25f)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (maxX <= minX || maxY <= minY)
            {
                Object.DestroyImmediate(srcTex);
                Debug.LogError("[UiThemeSetupEditor] Could not locate blue chevron region in Logo_UnDF.png");
                return;
            }

            const int pad = 6;
            int x0 = Mathf.Max(0, minX - pad);
            int x1 = Mathf.Min(sw - 1, maxX + pad);
            int y0 = Mathf.Max(0, minY - pad);
            int y1 = Mathf.Min(sh - 1, maxY + pad);

            int cw = x1 - x0 + 1;
            int ch = y1 - y0 + 1;

            var outTex = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
            var outPixels = new Color[cw * ch];

            for (int y = 0; y < ch; y++)
            {
                for (int x = 0; x < cw; x++)
                {
                    Color c = srcPixels[(y0 + y) * sw + (x0 + x)];

                    // Remove near-white background with smooth anti-aliased de-matting
                    float distFromWhite = Mathf.Max(1f - c.r, Mathf.Max(1f - c.g, 1f - c.b));
                    float blueDelta = c.b - c.r;

                    if (distFromWhite <= 0.025f || (blueDelta < 0.015f && c.r > 0.72f && c.g > 0.72f && c.b > 0.72f))
                    {
                        outPixels[y * cw + x] = new Color(0f, 0f, 0f, 0f);
                        continue;
                    }

                    // Discard any accidental dark gray/black text artifact if near bottom border
                    if (blueDelta < 0.02f && c.b < 0.35f)
                    {
                        outPixels[y * cw + x] = new Color(0f, 0f, 0f, 0f);
                        continue;
                    }

                    // Smooth edge alpha derived from distance to white background
                    // Lightest chevron (#8BC9EE) has distFromWhite = 1 - 0.545 = 0.455
                    float alpha = Mathf.Clamp01((distFromWhite - 0.02f) / 0.34f);
                    if (alpha < 0.995f && alpha > 0.01f)
                    {
                        // Un-blend white background so edges stay crisp on dark #0B0E14 backgrounds without white halo
                        float r = Mathf.Clamp01((c.r - (1f - alpha)) / alpha);
                        float g = Mathf.Clamp01((c.g - (1f - alpha)) / alpha);
                        float b = Mathf.Clamp01((c.b - (1f - alpha)) / alpha);
                        outPixels[y * cw + x] = new Color(r, g, b, alpha);
                    }
                    else
                    {
                        outPixels[y * cw + x] = new Color(c.r, c.g, c.b, 1f);
                    }
                }
            }

            outTex.SetPixels(outPixels);
            outTex.Apply();
            SaveTextureAsSprite(outTex, LogoEmblemPath, Vector4.zero);
            Object.DestroyImmediate(outTex);
            Object.DestroyImmediate(srcTex);
        }

        private static void GenerateArchedCardSprite()
        {
            const int w = 256;
            const int h = 320;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;

                    // Horizontal bounds with slight margin for crisp AA
                    float leftDist = px - 3f;
                    float rightDist = (w - 3f) - px;
                    float topDist = (h - 3f) - py;

                    // Gentle MKX bottom arch / subtle downward point toward center
                    float nx = Mathf.Abs((px - w * 0.5f) / (w * 0.5f - 3f));
                    float archCurve = 4f + 28f * Mathf.Pow(Mathf.Clamp01(nx), 1.65f);
                    float bottomDist = py - archCurve;

                    // Slight 4px top corner bevel
                    float topLeftBevel = (px - 3f) + ((h - 3f) - py) - 6f;
                    float topRightBevel = ((w - 3f) - px) + ((h - 3f) - py) - 6f;

                    float sdf = Mathf.Min(
                        Mathf.Min(leftDist, rightDist),
                        Mathf.Min(Mathf.Min(topDist, bottomDist), Mathf.Min(topLeftBevel, topRightBevel)));

                    float a = Mathf.Clamp01(sdf * 0.85f + 0.5f);
                    pixels[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            // 9-slice preserves top bevel, side edges, and bottom arch cleanly at any card size
            SaveTextureAsSprite(tex, ArchedCardPath, new Vector4(24f, 52f, 24f, 24f));
            Object.DestroyImmediate(tex);
        }

        private static void GenerateHexBadgeSprite()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float half = size * 0.5f;
            float r = size * 0.44f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs((x + 0.5f) - half);
                    float py = Mathf.Abs((y + 0.5f) - half);

                    // Pointy-top regular hexagon SDF
                    float d = Mathf.Max(px * 0.8660254f + py * 0.5f, py) - r;
                    float a = Mathf.Clamp01(-d * 0.85f + 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            SaveTextureAsSprite(tex, HexBadgePath, Vector4.zero);
            Object.DestroyImmediate(tex);
        }

        private static void GenerateDiamondSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float half = size * 0.5f;
            float r = size * 0.42f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs((x + 0.5f) - half);
                    float py = Mathf.Abs((y + 0.5f) - half);
                    float d = (px + py) * 0.7071068f - r * 0.7071068f;
                    float a = Mathf.Clamp01(-d * 0.9f + 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            SaveTextureAsSprite(tex, DiamondPath, Vector4.zero);
            Object.DestroyImmediate(tex);
        }

        private static void GenerateLockIconSprite()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float cx = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;

                    // Padlock body (lower rectangle with subtle chamfer)
                    float bodyDx = Mathf.Abs(px - cx) - 32f;
                    float bodyDy = Mathf.Abs(py - 42f) - 26f;
                    float bodySdf = Mathf.Max(bodyDx, bodyDy);

                    // Keyhole cutout inside body
                    float holeCircle = Vector2.Distance(new Vector2(px, py), new Vector2(cx, 46f)) - 6.5f;
                    float holeSlot = Mathf.Max(Mathf.Abs(px - cx) - 3.2f, Mathf.Abs(py - 34f) - 9.5f);
                    float keyholeSdf = Mathf.Min(holeCircle, holeSlot);

                    float bodyWithHole = Mathf.Max(bodySdf, -keyholeSdf);

                    // Padlock shackle (top U-arch)
                    float shackleCy = 82f;
                    float shackleOuterR = 23f;
                    float shackleInnerR = 14f;
                    float shackleSdf;
                    if (py >= shackleCy)
                    {
                        float radial = Vector2.Distance(new Vector2(px, py), new Vector2(cx, shackleCy));
                        shackleSdf = Mathf.Max(radial - shackleOuterR, shackleInnerR - radial);
                    }
                    else if (py >= 62f)
                    {
                        float absX = Mathf.Abs(px - cx);
                        shackleSdf = Mathf.Max(absX - shackleOuterR, shackleInnerR - absX);
                    }
                    else
                    {
                        shackleSdf = 100f;
                    }

                    float sdf = Mathf.Min(bodyWithHole, shackleSdf);
                    float a = Mathf.Clamp01(-sdf * 0.85f + 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            SaveTextureAsSprite(tex, LockIconPath, Vector4.zero);
            Object.DestroyImmediate(tex);
        }

        private static void GenerateSoftSweepSprite()
        {
            const int w = 512;
            const int h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float vertFade = SmoothStep(0f, 0.16f, v) * SmoothStep(0f, 0.16f, 1f - v);
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float horizFade = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 1.75f);
                    float a = Mathf.Clamp01(horizFade * vertFade);
                    pixels[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            SaveTextureAsSprite(tex, SoftSweepPath, Vector4.zero);
            Object.DestroyImmediate(tex);
        }

        private static void GenerateBladeBarSprite(string assetPath, bool flipRight)
        {
            const int w = 512;
            const int h = 48;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                float py = y + 0.5f;
                for (int x = 0; x < w; x++)
                {
                    float px = flipRight ? ((w - 1 - x) + 0.5f) : (x + 0.5f);

                    // BladeBarLeft: crisp slanted inner edge on right (near timer), tapered razor point on left
                    float topEdge = (h - 3f) - py;
                    float bottomEdge = py - 3f;
                    float rightSlant = (w - 4f - (h - py) * 0.35f) - px;
                    float leftBladeTip = (px - 4f) - (py - 3f) * 0.85f;
                    float leftTopBevel = (px - 4f) + ((h - 3f) - py) - 18f;

                    float sdf = Mathf.Min(
                        Mathf.Min(topEdge, bottomEdge),
                        Mathf.Min(rightSlant, Mathf.Min(leftBladeTip, leftTopBevel)));

                    float a = Mathf.Clamp01(sdf * 0.85f + 0.5f);
                    pixels[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            SaveTextureAsSprite(tex, assetPath, Vector4.zero);
            Object.DestroyImmediate(tex);
        }

        private static void GenerateVignetteVerticalSprite()
        {
            const int w = 64;
            const int h = 512;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h; // 0 = bottom, 1 = top
                // Rich smooth dusk vignette: strong matte anchor behind lower 6 menu items & top-left logo, clear dusk window on facade
                float bottomDark = (1f - SmoothStep(0.36f, 0.68f, v)) * 0.94f;
                float topDark = SmoothStep(0.76f, 0.97f, v) * 0.82f;
                float baseTint = 0.68f;
                float verticalAlpha = Mathf.Clamp01(Mathf.Max(baseTint, Mathf.Max(bottomDark, topDark)));

                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float edgeX = Mathf.Abs(u - 0.5f) * 2f; // 0 center .. 1 edges
                    float sideVignette = Mathf.Pow(edgeX, 1.9f) * 0.24f;
                    float a = Mathf.Clamp01(verticalAlpha + sideVignette * (1f - verticalAlpha));
                    pixels[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            SaveTextureAsSprite(tex, VignetteVerticalPath, Vector4.zero);
            Object.DestroyImmediate(tex);
        }

        private static void GenerateCircleBadgeSprite()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float half = size * 0.5f;
            float radius = size * 0.46f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) - radius;
                    float a = Mathf.Clamp01(-dist * 0.9f + 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            SaveTextureAsSprite(tex, CircleBadgePath, Vector4.zero);
            Object.DestroyImmediate(tex);
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static void SaveTextureAsSprite(Texture2D tex, string assetPath, Vector4 border)
        {
            string fullPath = Path.GetFullPath(assetPath);
            EnsureFolder(Path.GetDirectoryName(assetPath));
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureSpriteImporter(assetPath, border);
        }

        private static void ConfigureSpriteImporter(string assetPath, Vector4 border)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
            if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; dirty = true; }
            if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; dirty = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
            if (importer.spriteBorder != border) { importer.spriteBorder = border; dirty = true; }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
        }

        // ==================== 2. MAIN MENU SCENE REBUILD (ARENA_UnDF BACKDROP + MKX LAYOUT) ====================

        public static void RebuildMainMenuScene()
        {
            Scene menuScene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

            // Remove old dark empty diorama or prior backdrop
            DestroyRootIfExists(menuScene, "MenuDiorama");
            DestroyRootIfExists(menuScene, "MainMenu_UnDFBackdrop");
            DestroyRootIfExists(menuScene, "Key Light (Amber)");
            DestroyRootIfExists(menuScene, "Rim Light (Cool)");
            DestroyRootIfExists(menuScene, "MainMenu_KeyLight");
            DestroyRootIfExists(menuScene, "MainMenu_FillLight");

            // Clone static architectural facade & parking lot from Arena_UnDF.unity for authentic backdrop
            CloneUnDFBackdropIntoScene(menuScene);

            // Soft dusk/night atmospheric lighting for maximum menu legibility
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.075f, 0.115f, 0.185f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.055f, 0.080f, 0.130f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.030f, 0.040f, 0.060f, 1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.043f, 0.055f, 0.078f, 1f);
            RenderSettings.fogDensity = 0.026f;

            var keyLightGo = new GameObject("MainMenu_KeyLight");
            SceneManager.MoveGameObjectToScene(keyLightGo, menuScene);
            keyLightGo.transform.rotation = Quaternion.Euler(26f, 22f, 0f);
            var keyLight = keyLightGo.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(0.50f, 0.66f, 0.86f, 1f);
            keyLight.intensity = 0.34f;
            keyLight.shadows = LightShadows.Soft;

            var fillLightGo = new GameObject("MainMenu_FillLight");
            SceneManager.MoveGameObjectToScene(fillLightGo, menuScene);
            fillLightGo.transform.position = new Vector3(0f, 5.2f, 2.5f);
            var fillLight = fillLightGo.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.color = new Color(0.24f, 0.54f, 0.78f, 1f); // Subtle UnDF architectural dusk wash
            fillLight.intensity = 6.5f;
            fillLight.range = 24f;
            fillLight.shadows = LightShadows.None;

            // Frame Main Camera on the illuminated UnDF campus facade + parking lot
            Camera cam = FindOrCreateMainCamera(menuScene);
            cam.transform.position = new Vector3(-2.1f, 2.25f, -8.8f);
            cam.transform.rotation = Quaternion.Euler(-3.5f, 8.5f, 0f);
            cam.fieldOfView = 39f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.043f, 0.055f, 0.078f, 1f);
            if (cam.GetComponent<MenuCameraSway>() == null)
            {
                cam.gameObject.AddComponent<MenuCameraSway>();
            }

            // Rebuild MainMenuCanvas UI with official UnDF sprites
            GameObject canvasGo = FindRootByName(menuScene, "MainMenuCanvas");
            if (canvasGo == null)
            {
                canvasGo = CreateStandardCanvas(menuScene, "MainMenuCanvas");
            }
            ConfigureCanvasScaler(canvasGo);

            MainMenuController ctrl = canvasGo.GetComponent<MainMenuController>();
            if (ctrl == null) ctrl = canvasGo.AddComponent<MainMenuController>();

            Sprite white = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
            Sprite emblem = AssetDatabase.LoadAssetAtPath<Sprite>(LogoEmblemPath);
            Sprite arched = AssetDatabase.LoadAssetAtPath<Sprite>(ArchedCardPath);
            Sprite diamond = AssetDatabase.LoadAssetAtPath<Sprite>(DiamondPath);
            Sprite sweep = AssetDatabase.LoadAssetAtPath<Sprite>(SoftSweepPath);
            Sprite vignette = AssetDatabase.LoadAssetAtPath<Sprite>(VignetteVerticalPath);
            Sprite circleBadge = AssetDatabase.LoadAssetAtPath<Sprite>(CircleBadgePath);
            FighterRoster roster = AssetDatabase.LoadAssetAtPath<FighterRoster>(FighterRosterPath);

            ctrl.ConfigureThemeSprites(white, emblem, arched, diamond, sweep, vignette, circleBadge);
            AssignSerializedField(ctrl, "fighterRoster", roster);
            ctrl.BuildUI(canvasGo.transform, white);

            EditorUtility.SetDirty(ctrl);
            EditorSceneManager.MarkSceneDirty(menuScene);
            EditorSceneManager.SaveScene(menuScene, MainMenuScenePath);
        }

        private static void CloneUnDFBackdropIntoScene(Scene targetScene)
        {
            var backdropRoot = new GameObject("MainMenu_UnDFBackdrop");
            SceneManager.MoveGameObjectToScene(backdropRoot, targetScene);

            if (!File.Exists(Path.GetFullPath(ArenaUnDFBuilderEditor.ArenaUnDFScenePath)))
            {
                Debug.LogWarning("[UiThemeSetupEditor] Arena_UnDF.unity not found; MainMenu backdrop skipped.");
                return;
            }

            Scene undfScene = EditorSceneManager.OpenScene(ArenaUnDFBuilderEditor.ArenaUnDFScenePath, OpenSceneMode.Additive);
            try
            {
                string[] staticRootNames =
                {
                    ArenaUnDFBuilderEditor.EnvironmentRootName,
                    ArenaUnDFBuilderEditor.NightLightsAndWindowsName
                };

                foreach (string rootName in staticRootNames)
                {
                    GameObject srcRoot = FindRootByName(undfScene, rootName);
                    if (srcRoot == null) continue;

                    GameObject clone = Object.Instantiate(srcRoot, backdropRoot.transform);
                    clone.name = rootName;
                    clone.SetActive(true);
                    StripPhysicsAndCombatComponents(clone);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(undfScene, removeScene: true);
            }
        }

        private static void StripPhysicsAndCombatComponents(GameObject root)
        {
            // Keep pure static visual architecture + non-shadow point lights for dusk ambiance
            foreach (var joint in root.GetComponentsInChildren<Joint>(true)) Object.DestroyImmediate(joint);
            foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
            foreach (var col in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true)) Object.DestroyImmediate(ps.gameObject);

            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (typeName.StartsWith("Stage") || typeName.StartsWith("Fight") || typeName.Contains("Wind") || typeName.Contains("Weather"))
                {
                    Object.DestroyImmediate(mb);
                }
            }

            foreach (var l in root.GetComponentsInChildren<Light>(true))
            {
                if (l.type != LightType.Directional)
                {
                    l.shadows = LightShadows.None;
                    l.intensity *= 0.38f;
                }
            }
        }

        // ==================== 3. CHARACTER SELECT SCENE REBUILD (MKX + RIM LIGHTS + 3D STAGE BACKDROP) ====================

        public static void RebuildCharacterSelectScene()
        {
            Scene selectScene = EditorSceneManager.OpenScene(CharacterSelectScenePath, OpenSceneMode.Single);

            DestroyRootIfExists(selectScene, "Key Light (Amber)");
            DestroyRootIfExists(selectScene, "Rim Light (Cool)");
            DestroyRootIfExists(selectScene, "Select_KeyLight");
            DestroyRootIfExists(selectScene, "Select_P1RimLight");
            DestroyRootIfExists(selectScene, "Select_P2RimLight");

            // Matte graphite ambient + solid obsidian clear color
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.14f, 0.18f, 0.24f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.09f, 0.12f, 0.16f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.05f, 0.08f, 1f);
            RenderSettings.fog = false;

            // Main Camera framed cleanly so top 65% showcases P1 & P2 models above the bottom roster/stage bars
            Camera cam = FindOrCreateMainCamera(selectScene);
            cam.transform.position = new Vector3(0f, 0.90f, -6.05f);
            cam.transform.rotation = Quaternion.Euler(2.0f, 0f, 0f);
            cam.fieldOfView = 35f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.043f, 0.055f, 0.078f, 1f);

            // Neutral key light (shadows only on main directional light)
            var keyGo = new GameObject("Select_KeyLight");
            SceneManager.MoveGameObjectToScene(keyGo, selectScene);
            keyGo.transform.rotation = Quaternion.Euler(24f, 336f, 0f);
            var keyLight = keyGo.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(0.90f, 0.94f, 0.98f, 1f);
            keyLight.intensity = 1.05f;
            keyLight.shadows = LightShadows.Soft;

            // P1 Rim Light (#3D8AC7 UnDF institutional blue, zero shadows on point light)
            var p1RimGo = new GameObject("Select_P1RimLight");
            SceneManager.MoveGameObjectToScene(p1RimGo, selectScene);
            p1RimGo.transform.position = new Vector3(-2.95f, 2.15f, 1.35f);
            var p1Rim = p1RimGo.AddComponent<Light>();
            p1Rim.type = LightType.Point;
            p1Rim.color = MenuUiFactory.UnDFBlue;
            p1Rim.intensity = 16f;
            p1Rim.range = 8.5f;
            p1Rim.shadows = LightShadows.None;

            // P2 Rim Light (#991B1B matte crimson, zero shadows on point light)
            var p2RimGo = new GameObject("Select_P2RimLight");
            SceneManager.MoveGameObjectToScene(p2RimGo, selectScene);
            p2RimGo.transform.position = new Vector3(2.95f, 2.15f, 1.35f);
            var p2Rim = p2RimGo.AddComponent<Light>();
            p2Rim.type = LightType.Point;
            p2Rim.color = MenuUiFactory.CrimsonDark;
            p2Rim.intensity = 18f;
            p2Rim.range = 8.5f;
            p2Rim.shadows = LightShadows.None;

            // PreviewStage: Matte graphite floor + P1/P2 anchors + 3D StageBackdropQuad behind fighters
            GameObject stageRoot = FindRootByName(selectScene, "PreviewStage");
            if (stageRoot == null)
            {
                stageRoot = new GameObject("PreviewStage");
                SceneManager.MoveGameObjectToScene(stageRoot, selectScene);
            }

            Transform floorTr = stageRoot.transform.Find("StageFloor");
            if (floorTr == null)
            {
                var floorGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floorGo.name = "StageFloor";
                floorGo.transform.SetParent(stageRoot.transform, false);
                floorTr = floorGo.transform;
            }
            floorTr.localPosition = new Vector3(0f, -0.05f, 0f);
            floorTr.localScale = new Vector3(14f, 0.1f, 8f);
            var floorRenderer = floorTr.GetComponent<MeshRenderer>();
            if (floorRenderer != null)
            {
                floorRenderer.sharedMaterial = EnsureSelectFloorMaterial();
            }

            Transform p1Anchor = FindOrCreateChild(stageRoot.transform, "P1PreviewAnchor");
            p1Anchor.localPosition = new Vector3(-1.62f, 0.0f, 0.0f);
            p1Anchor.localRotation = Quaternion.identity;

            Transform p2Anchor = FindOrCreateChild(stageRoot.transform, "P2PreviewAnchor");
            p2Anchor.localPosition = new Vector3(1.62f, 0.0f, 0.0f);
            p2Anchor.localRotation = Quaternion.identity;

            // 3D Stage Preview Backdrop behind fighters (active only during StageSelect step)
            Transform backdropTr = stageRoot.transform.Find("StageBackdropQuad");
            if (backdropTr == null)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "StageBackdropQuad";
                var col = quad.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                quad.transform.SetParent(stageRoot.transform, false);
                backdropTr = quad.transform;
            }
            backdropTr.localPosition = new Vector3(0f, 1.05f, 4.20f);
            backdropTr.localRotation = Quaternion.identity;
            backdropTr.localScale = new Vector3(15.2f, 8.6f, 1f);
            backdropTr.gameObject.SetActive(false);
            var backdropRenderer = backdropTr.GetComponent<MeshRenderer>();

            // Rebuild CharacterSelectCanvas UI
            GameObject canvasGo = FindRootByName(selectScene, "CharacterSelectCanvas");
            if (canvasGo == null)
            {
                canvasGo = CreateStandardCanvas(selectScene, "CharacterSelectCanvas");
            }
            ConfigureCanvasScaler(canvasGo);

            CharacterSelectController ctrl = canvasGo.GetComponent<CharacterSelectController>();
            if (ctrl == null) ctrl = canvasGo.AddComponent<CharacterSelectController>();

            Sprite white = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
            Sprite emblem = AssetDatabase.LoadAssetAtPath<Sprite>(LogoEmblemPath);
            Sprite hexBadge = AssetDatabase.LoadAssetAtPath<Sprite>(HexBadgePath);
            Sprite diamond = AssetDatabase.LoadAssetAtPath<Sprite>(DiamondPath);
            Sprite lockIcon = AssetDatabase.LoadAssetAtPath<Sprite>(LockIconPath);
            FighterRoster fighterRoster = AssetDatabase.LoadAssetAtPath<FighterRoster>(FighterRosterPath);
            StageRoster stageRoster = AssetDatabase.LoadAssetAtPath<StageRoster>(StageRosterPath);

            ctrl.ConfigureReferences(fighterRoster, stageRoster, white, p1Anchor, p2Anchor);
            ctrl.ConfigureThemeSprites(emblem, hexBadge, diamond, lockIcon, backdropRenderer);
            ctrl.BuildUI(canvasGo.transform, white);

            EditorUtility.SetDirty(ctrl);
            EditorSceneManager.MarkSceneDirty(selectScene);
            EditorSceneManager.SaveScene(selectScene, CharacterSelectScenePath);
        }

        private static Material EnsureSelectFloorMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(SelectFloorMatPath);
            if (mat == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(lit);
                AssetDatabase.CreateAsset(mat, SelectFloorMatPath);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.068f, 0.088f, 0.125f, 1f));
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(0.068f, 0.088f, 0.125f, 1f));
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.22f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.10f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ==================== 4. APPLY MKX FIGHT HUD & UNIFIED MODALS TO ALL 4 ARENAS ====================

        [MenuItem("UnDFight/UI/Apply MKX Fight HUD To All Arenas", false, 131)]
        public static void ApplyMkxFightHudToAllArenas()
        {
            // Ensure all required sprites exist on disk before wiring scenes
            if (AssetDatabase.LoadAssetAtPath<Sprite>(BladeBarLeftPath) == null ||
                AssetDatabase.LoadAssetAtPath<Sprite>(LogoEmblemPath) == null ||
                AssetDatabase.LoadAssetAtPath<Sprite>(CircleBadgePath) == null)
            {
                GenerateAndPersistAllUiSprites();
            }

            Sprite white = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
            Sprite bladeLeft = AssetDatabase.LoadAssetAtPath<Sprite>(BladeBarLeftPath);
            Sprite bladeRight = AssetDatabase.LoadAssetAtPath<Sprite>(BladeBarRightPath);
            Sprite diamond = AssetDatabase.LoadAssetAtPath<Sprite>(DiamondPath);
            Sprite circleBadge = AssetDatabase.LoadAssetAtPath<Sprite>(CircleBadgePath);
            Sprite emblem = AssetDatabase.LoadAssetAtPath<Sprite>(LogoEmblemPath);

            string previousScenePath = SceneManager.GetActiveScene().path;
            int updatedCount = 0;

            foreach (string scenePath in CombatArenaScenePaths)
            {
                if (!File.Exists(Path.GetFullPath(scenePath)))
                {
                    Debug.LogWarning($"[UiThemeSetupEditor] Combat arena scene not found: {scenePath}");
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                FightSimulation sim = Object.FindAnyObjectByType<FightSimulation>();
                MatchManager mm = Object.FindAnyObjectByType<MatchManager>();
                CombatVfxController vfx = Object.FindAnyObjectByType<CombatVfxController>();
                StageInteractableManager stageMgr = Object.FindAnyObjectByType<StageInteractableManager>();

                // 1) Rebuild FightHUDCanvas with MKX tapered blade bars + bottom-corner EX/SUPER meters
                FightHUD hud = Object.FindAnyObjectByType<FightHUD>();
                GameObject hudCanvasGo = hud != null ? hud.gameObject : FindRootByName(scene, "FightHUDCanvas");
                if (hudCanvasGo == null)
                {
                    hudCanvasGo = CreateStandardCanvas(scene, "FightHUDCanvas");
                }
                ConfigureCanvasScaler(hudCanvasGo);
                if (hud == null) hud = hudCanvasGo.AddComponent<FightHUD>();

                hud.ConfigureThemeSprites(white, bladeLeft, bladeRight, diamond, circleBadge, emblem);
                hud.BuildMkxUI(hudCanvasGo.transform);
                hud.Configure(sim, mm);
                if (stageMgr != null)
                {
                    AssignSerializedField(hud, "stageInteractableManager", stageMgr);
                }
                EditorUtility.SetDirty(hud);

                // 2) Update TrainingDebugHUD (clean matte footer panel, hidden by default outside Training mode)
                TrainingDebugHUD debugHud = Object.FindAnyObjectByType<TrainingDebugHUD>();
                if (debugHud != null)
                {
                    ConfigureCanvasScaler(debugHud.gameObject);
                    Transform debugPanelTr = debugHud.transform.Find("DebugPanel");
                    GameObject debugPanelGo = debugPanelTr != null ? debugPanelTr.gameObject : null;
                    TMPro.TextMeshProUGUI debugText = debugHud.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);

                    if (debugPanelGo != null)
                    {
                        var panelImg = debugPanelGo.GetComponent<UnityEngine.UI.Image>();
                        if (panelImg != null)
                        {
                            panelImg.color = new Color(0.043f, 0.055f, 0.078f, 0.84f);
                        }
                        // Default serialized active state: active only in Training scene; runtime RefreshVisibility enforces GameMode
                        bool isTrainingScene = scenePath.EndsWith("Training.unity", System.StringComparison.OrdinalIgnoreCase);
                        debugPanelGo.SetActive(isTrainingScene);
                    }

                    debugHud.Configure(sim, debugText, debugPanelGo);
                    if (debugPanelGo != null)
                    {
                        bool isTrainingScene = scenePath.EndsWith("Training.unity", System.StringComparison.OrdinalIgnoreCase);
                        debugPanelGo.SetActive(isTrainingScene);
                    }
                    EditorUtility.SetDirty(debugHud);

                    if (vfx != null)
                    {
                        AssignSerializedField(vfx, "debugHud", debugHud);
                        EditorUtility.SetDirty(vfx);
                    }
                }

                // 3) Rebuild FightMenuCanvas (Pause, Options, MatchOver, Tower modals in unified matte UnDF theme)
                FightMenuController menuCtrl = Object.FindAnyObjectByType<FightMenuController>();
                GameObject menuCanvasGo = menuCtrl != null ? menuCtrl.gameObject : FindRootByName(scene, "FightMenuCanvas");
                if (menuCanvasGo == null)
                {
                    menuCanvasGo = CreateStandardCanvas(scene, "FightMenuCanvas");
                }
                ConfigureCanvasScaler(menuCanvasGo);
                if (menuCtrl == null) menuCtrl = menuCanvasGo.AddComponent<FightMenuController>();

                menuCtrl.Configure(sim, mm, vfx, white);
                menuCtrl.ConfigureThemeSprites(emblem);
                menuCtrl.BuildUI(menuCanvasGo.transform, white);

                Transform pauseTr = menuCanvasGo.transform.Find("PausePanel");
                if (pauseTr != null) pauseTr.gameObject.SetActive(false);
                Transform moTr = menuCanvasGo.transform.Find("MatchOverPanel");
                if (moTr != null) moTr.gameObject.SetActive(false);
                Transform optTr = menuCanvasGo.transform.Find("OptionsPanel");
                if (optTr != null) optTr.gameObject.SetActive(false);
                EditorUtility.SetDirty(menuCtrl);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, scenePath);
                updatedCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!string.IsNullOrEmpty(previousScenePath) && File.Exists(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
            else
            {
                EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            }

            Debug.Log($"[UnDFight] Fase 17.2 complete: Applied MKX Fight HUD, clean TrainingDebugHUD visibility, and unified UnDF modals to {updatedCount} combat arenas.");
        }

        // ==================== HELPERS ====================

        private static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;
            string full = Path.GetFullPath(folderPath);
            if (!Directory.Exists(full))
            {
                Directory.CreateDirectory(full);
            }
        }

        private static GameObject FindRootByName(Scene scene, string rootName)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == rootName) return go;
            }
            return null;
        }

        private static void DestroyRootIfExists(Scene scene, string rootName)
        {
            GameObject go = FindRootByName(scene, rootName);
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        private static Transform FindOrCreateChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Camera FindOrCreateMainCamera(Scene scene)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                var c = go.GetComponent<Camera>();
                if (c != null) return c;
            }
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            SceneManager.MoveGameObjectToScene(camGo, scene);
            camGo.AddComponent<AudioListener>();
            return camGo.AddComponent<Camera>();
        }

        private static GameObject CreateStandardCanvas(Scene scene, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(go, scene);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            ConfigureCanvasScaler(go);
            return go;
        }

        private static void ConfigureCanvasScaler(GameObject canvasGo)
        {
            var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler == null) scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static void AssignSerializedField(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
