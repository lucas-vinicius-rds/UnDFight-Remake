using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnDFight.Tests
{
    public class MenuAndSettingsTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            GameSessionState.Reset();
            _tempDir = Path.Combine(Path.GetTempPath(), "UnDFight_SettingsTests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            GameSessionState.Reset();

            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                {
                    Object.DestroyImmediate(_createdObjects[i]);
                }
            }
            _createdObjects.Clear();

            if (!string.IsNullOrEmpty(_tempDir) && Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        private T Track<T>(T obj) where T : Object
        {
            _createdObjects.Add(obj);
            return obj;
        }

        [Test]
        public void SettingsManager_SaveLoadAndRemapJson_PersistsAllFieldsCleanly()
        {
            string filePath = Path.Combine(_tempDir, "test_settings.json");

            GameSettingsData settings = GameSettingsData.CreateDefaults();
            settings.masterVolume = 0.55f;
            settings.musicVolume = 0.30f;
            settings.sfxVolume = 0.75f;
            settings.resolutionWidth = 1280;
            settings.resolutionHeight = 720;
            settings.fullscreen = false;
            settings.p1DeviceMode = InputAssignmentMode.KeyboardOnly;
            settings.p2DeviceMode = InputAssignmentMode.Gamepad0;
            settings.p1Bindings.SetKey(FighterInputButton.LightPunch, Key.Z);
            settings.p1Bindings.SetKey(FighterInputButton.Block, Key.Space);
            settings.p2Bindings.SetKey(FighterInputButton.HeavyKick, Key.P);

            SettingsManager.Save(settings, filePath);
            Assert.IsTrue(File.Exists(filePath));

            GameSettingsData reloaded = SettingsManager.Load(filePath);
            Assert.AreEqual(0.55f, reloaded.masterVolume, 1e-4f);
            Assert.AreEqual(0.30f, reloaded.musicVolume, 1e-4f);
            Assert.AreEqual(0.75f, reloaded.sfxVolume, 1e-4f);
            Assert.AreEqual(1280, reloaded.resolutionWidth);
            Assert.AreEqual(720, reloaded.resolutionHeight);
            Assert.IsFalse(reloaded.fullscreen);
            Assert.AreEqual(InputAssignmentMode.KeyboardOnly, reloaded.p1DeviceMode);
            Assert.AreEqual(InputAssignmentMode.Gamepad0, reloaded.p2DeviceMode);
            Assert.AreEqual(Key.Z, reloaded.p1Bindings.GetKey(FighterInputButton.LightPunch, 0));
            Assert.AreEqual(Key.Space, reloaded.p1Bindings.GetKey(FighterInputButton.Block, 0));
            Assert.AreEqual(Key.P, reloaded.p2Bindings.GetKey(FighterInputButton.HeavyKick, 1));
        }

        [Test]
        public void SettingsManager_ResetToDefaults_RestoresVolumesVideoAndDefaultBindings()
        {
            string filePath = Path.Combine(_tempDir, "reset_settings.json");

            GameSettingsData custom = GameSettingsData.CreateDefaults();
            custom.masterVolume = 0.1f;
            custom.fullscreen = false;
            custom.resolutionWidth = 1280;
            custom.resolutionHeight = 720;
            custom.p1Bindings.SetKey(FighterInputButton.LightPunch, Key.Q);
            SettingsManager.Save(custom, filePath);

            SettingsManager.ResetToDefaults(filePath, applyAndSave: true);
            GameSettingsData afterReset = SettingsManager.Load(filePath);

            Assert.AreEqual(1.0f, afterReset.masterVolume, 1e-4f);
            Assert.AreEqual(0.8f, afterReset.musicVolume, 1e-4f);
            Assert.AreEqual(0.9f, afterReset.sfxVolume, 1e-4f);
            Assert.AreEqual(1920, afterReset.resolutionWidth);
            Assert.AreEqual(1080, afterReset.resolutionHeight);
            Assert.IsTrue(afterReset.fullscreen);
            Assert.AreEqual(InputAssignmentMode.Auto, afterReset.p1DeviceMode);
            Assert.AreEqual(InputAssignmentMode.Auto, afterReset.p2DeviceMode);
            Assert.AreEqual(Key.J, afterReset.p1Bindings.GetKey(FighterInputButton.LightPunch, 0));
            Assert.AreEqual(Key.Numpad4, afterReset.p2Bindings.GetKey(FighterInputButton.LightPunch, 1));
        }

        [Test]
        public void GameSessionState_RosterSelectionAndMirrorMatch_ResolvesCorrectly()
        {
            var lucas = Track(ScriptableObject.CreateInstance<FighterData>());
            lucas.fighterName = "Lucas";
            lucas.heightMeters = 1.81f;
            lucas.weightKg = 92f;

            var tiago = Track(ScriptableObject.CreateInstance<FighterData>());
            tiago.fighterName = "Tiago";
            tiago.heightMeters = 1.79f;
            tiago.weightKg = 87f;

            var roster = Track(ScriptableObject.CreateInstance<FighterRoster>());
            var field = typeof(FighterRoster).GetField("fighters", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(roster, new List<FighterData> { lucas, tiago });

            // Default fallback without explicit selection
            GameSessionState.ResolveFighters(roster, null, null, out FighterData defP1, out FighterData defP2);
            Assert.AreSame(lucas, defP1);
            Assert.AreSame(tiago, defP2);

            // Mirror match support (Tiago vs Tiago)
            GameSessionState.SelectMode(GameMode.LocalVersus);
            GameSessionState.SelectFighters(tiago, tiago);
            GameSessionState.ResolveFighters(roster, lucas, tiago, out FighterData mirP1, out FighterData mirP2);
            Assert.AreSame(tiago, mirP1);
            Assert.AreSame(tiago, mirP2);
            Assert.IsFalse(GameSessionState.P1SelectsBothFighters);

            // Single-chooser rule check for VS CPU and Training
            GameSessionState.SelectMode(GameMode.VersusCpu);
            Assert.IsTrue(GameSessionState.P1SelectsBothFighters);
            Assert.IsFalse(GameSessionState.IsTrainingMode);

            GameSessionState.SelectMode(GameMode.Training);
            Assert.IsTrue(GameSessionState.P1SelectsBothFighters);
            Assert.IsTrue(GameSessionState.IsTrainingMode);
        }

        [Test]
        public void TrainingMode_InfiniteTimerAndAutoHealthRegenAfter90Ticks()
        {
            var move = Track(ScriptableObject.CreateInstance<MoveData>());
            move.Configure("HeavyPunch", AttackType.HeavyPunch, 7, 4, 16, 200, 18, 12, 7, 2.8f, HitHeight.Mid, false, new Vector2(0.76f, 1.18f), new Vector2(0.92f, 0.46f));

            var data = Track(ScriptableObject.CreateInstance<FighterData>());
            data.fighterName = "Dummy";
            data.maxHealth = 1000;
            data.heavyPunch = move;

            var simGo = Track(new GameObject("Test_TrainingSim"));
            var sim = simGo.AddComponent<FightSimulation>();
            var match = simGo.AddComponent<MatchManager>();

            var p1Go = Track(new GameObject("P1"));
            p1Go.transform.position = new Vector3(-0.55f, 0f, 0f);
            p1Go.AddComponent<PlayerInputHandler>();
            var p1 = p1Go.AddComponent<FighterController>();
            p1.SetFighterData(data);

            var p2Go = Track(new GameObject("P2"));
            p2Go.transform.position = new Vector3(0.55f, 0f, 0f);
            p2Go.AddComponent<PlayerInputHandler>();
            var p2 = p2Go.AddComponent<FighterController>();
            p2.SetFighterData(data);

            sim.SetParticipants(p1, p2);
            match.InitializeMatch(sim, trainingModeOverride: true);

            Assert.IsTrue(match.IsTrainingMode);
            Assert.AreEqual(MatchPhase.Fighting, match.Phase);
            Assert.AreEqual(99, match.RemainingSeconds);

            p1.SetSimPositionX(-0.55f);
            p2.SetSimPositionX(0.55f);
            p1.InputHandler.SetSimulatedInput(default, true);
            p2.InputHandler.SetSimulatedInput(default, true);

            // Land HeavyPunch (200 dmg)
            p1.InputHandler.BufferButton(FighterInputButton.HeavyPunch);
            sim.StepSimulationTicks(1 + 8 + 7); // active + hitstop
            Assert.AreEqual(800, p2.CurrentHealth);
            Assert.AreEqual(MatchPhase.Fighting, match.Phase);

            // Advance 88 ticks without getting hit -> no regen yet, timer still 99
            sim.StepSimulationTicks(88);
            Assert.AreEqual(800, p2.CurrentHealth);
            Assert.AreEqual(99, match.RemainingSeconds);

            // Advance 20 more ticks past 90-tick threshold -> health regenerates back toward 1000
            sim.StepSimulationTicks(20);
            Assert.Greater(p2.CurrentHealth, 800);

            sim.StepSimulationTicks(20);
            Assert.AreEqual(1000, p2.CurrentHealth);
            Assert.AreEqual(MatchPhase.Fighting, match.Phase);
        }

        [Test]
        public void StageRoster_FindsStagesAndResolvesSessionStageSelection()
        {
            var favela = Track(ScriptableObject.CreateInstance<StageData>());
            favela.stageId = "favela_carioca";
            favela.displayName = "FAVELA CARIOCA";
            favela.subtitle = "Bar do Jorge • Pôr do Sol";
            favela.sceneName = "Arena_Favela";

            var treino = Track(ScriptableObject.CreateInstance<StageData>());
            treino.stageId = "sala_de_treino";
            treino.displayName = "SALA DE TREINO";
            treino.subtitle = "Arena Tática • Simulação";
            treino.sceneName = "Training";

            var roster = Track(ScriptableObject.CreateInstance<StageRoster>());
            var field = typeof(StageRoster).GetField("stages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(roster, new List<StageData> { favela, treino });

            Assert.AreEqual(2, roster.Count);
            Assert.AreSame(favela, roster.FindBySceneName("Arena_Favela"));
            Assert.AreSame(treino, roster.FindBySceneName("Training"));
            Assert.IsNull(roster.FindBySceneName("MissingScene"));

            // Default resolves cleanly to Arena_Favela
            Assert.AreEqual("Arena_Favela", GameSessionState.ResolveStageSceneName(roster));

            // Selecting Sala de Treino persists across GameSessionState
            GameSessionState.SelectStage(treino);
            Assert.AreEqual("Training", GameSessionState.SelectedStageSceneName);
            Assert.AreEqual("Training", GameSessionState.ResolveStageSceneName(roster));

            // Unknown scene name falls back safely to first roster entry
            GameSessionState.SelectStageScene("NonExistentArena");
            Assert.AreEqual("Arena_Favela", GameSessionState.ResolveStageSceneName(roster));

            // Direct reopen flag for "TROCAR MAPA"
            GameSessionState.OpenStageSelectOnCharacterSelect = true;
            Assert.IsTrue(GameSessionState.OpenStageSelectOnCharacterSelect);
            GameSessionState.Reset();
            Assert.IsFalse(GameSessionState.OpenStageSelectOnCharacterSelect);
            Assert.AreEqual(GameSessionState.DefaultStageSceneName, GameSessionState.SelectedStageSceneName);
        }

        [Test]
        public void MainMenu_RootMenuLabels_UseStandardizedMkxUnDFLabelsWithoutVersusLocal()
        {
            string[] expectedLabels = { "MODO TORRE", "VS JOGADOR", "VS CPU", "TREINO", "OPÇÕES", "SAIR" };
            Assert.AreEqual(expectedLabels.Length, MainMenuController.RootMenuLabels.Length);
            for (int i = 0; i < expectedLabels.Length; i++)
            {
                Assert.AreEqual(expectedLabels[i], MainMenuController.RootMenuLabels[i]);
                Assert.AreNotEqual("VERSUS LOCAL", MainMenuController.RootMenuLabels[i]);
            }

            var canvasGo = Track(new GameObject("Test_MainMenuCanvas", typeof(RectTransform), typeof(Canvas)));
            var ctrl = canvasGo.AddComponent<MainMenuController>();
            var tex = Track(new Texture2D(2, 2));
            var spr = Track(Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f)));

            ctrl.BuildUI(canvasGo.transform, spr);

            Transform rootGroup = canvasGo.transform.Find("MainMenuPanel/RootMenuGroup");
            Assert.IsNotNull(rootGroup);
            var buttons = rootGroup.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            Assert.AreEqual(expectedLabels.Length, buttons.Length);

            for (int i = 0; i < expectedLabels.Length; i++)
            {
                var lbl = buttons[i].transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
                Assert.IsNotNull(lbl);
                Assert.AreEqual(expectedLabels[i], lbl.text);
                Assert.IsNotNull(buttons[i].transform.Find("DiamondLeft"), "MKX clean menu button must have left diamond");
                Assert.IsNotNull(buttons[i].transform.Find("DiamondRight"), "MKX clean menu button must have right diamond");
                Assert.IsNotNull(buttons[i].transform.Find("SelectionSweep"), "MKX clean menu button must have soft sweep");
            }

            // Feature cards check (TORRE ARCADE, COMBATE RÁPIDO, ARENAS without "2.5D")
            var arenasTitle = canvasGo.transform.Find("TopFeatureCards/FeatureCard_Arenas/CardTitle")?.GetComponent<TextMeshProUGUI>();
            Assert.IsNotNull(arenasTitle);
            Assert.AreEqual("ARENAS", arenasTitle.text);
            StringAssert.DoesNotContain("2.5D", arenasTitle.text);

            // Top-right must stay clean (no "REMAKE 2.5D • 60HZ" badge anywhere)
            foreach (TextMeshProUGUI tmp in canvasGo.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                StringAssert.DoesNotContain("60HZ", tmp.text.ToUpperInvariant());
                StringAssert.DoesNotContain("VERSUS LOCAL", tmp.text.ToUpperInvariant());
            }

            // Dynamic footer description updates on selection move
            Assert.IsNotNull(ctrl.ModeDescriptionText);
            string desc0 = ctrl.ModeDescriptionText.text;
            ctrl.RootNavigation.SetSelectedIndex(1, notifyAudio: false);
            string desc1 = ctrl.ModeDescriptionText.text;
            Assert.AreNotEqual(desc0, desc1);
            StringAssert.Contains("2 JOGADORES", desc1);
        }

        [Test]
        public void UiThemeAssets_OfficialUnDFLogoAndMkxSprites_ExistAsSpritesInArtUI()
        {
            Assert.IsTrue(File.Exists(Path.GetFullPath("Docs/Referencias/Logo_UnDF.png")), "Official Logo_UnDF.png copy missing in Docs/Referencias/");

            string[] requiredSprites =
            {
                "Assets/UnDFight/Art/UI/Logo_UnDF.png",
                "Assets/UnDFight/Art/UI/Logo_UnDF_Emblem.png",
                "Assets/UnDFight/Art/UI/UI_ArchedCard.png",
                "Assets/UnDFight/Art/UI/UI_HexBadge.png",
                "Assets/UnDFight/Art/UI/UI_Diamond.png",
                "Assets/UnDFight/Art/UI/UI_LockIcon.png",
                "Assets/UnDFight/Art/UI/UI_SoftSweep.png",
                "Assets/UnDFight/Art/UI/UI_BladeBarLeft.png",
                "Assets/UnDFight/Art/UI/UI_BladeBarRight.png"
            };

            foreach (string path in requiredSprites)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.IsNotNull(sprite, $"Missing Sprite asset at {path}");
                Assert.Greater(sprite.rect.width, 8f, $"Invalid sprite width for {path}");
                Assert.Greater(sprite.rect.height, 8f, $"Invalid sprite height for {path}");

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, $"Missing TextureImporter at {path}");
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, $"{path} must be imported as Sprite (2D and UI)");
                Assert.IsTrue(importer.alphaIsTransparency, $"{path} must enable alphaIsTransparency");
                Assert.IsFalse(importer.mipmapEnabled, $"{path} must disable mipmaps");
            }

            // ArchedCard must have 9-slice border configured
            Sprite arched = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UnDFight/Art/UI/UI_ArchedCard.png");
            Assert.Greater(arched.border.x, 0f, "UI_ArchedCard must have 9-slice left border");
            Assert.Greater(arched.border.y, 0f, "UI_ArchedCard must have 9-slice bottom arch border");

            // Emblem PNG must have transparent corner background (white background removed)
            byte[] emblemBytes = File.ReadAllBytes(Path.GetFullPath("Assets/UnDFight/Art/UI/Logo_UnDF_Emblem.png"));
            var emblemTex = Track(new Texture2D(2, 2, TextureFormat.RGBA32, false));
            Assert.IsTrue(emblemTex.LoadImage(emblemBytes));
            Assert.Less(emblemTex.GetPixel(0, 0).a, 0.05f, "Logo_UnDF_Emblem corner must be transparent over dark backgrounds");
        }

        [Test]
        public void CharacterSelect_DataDrivenGridLayout_ScalesCleanlyForFutureFightersAndStages()
        {
            // Fighter roster scaling: 1 row up to 8 fighters, 2 centered rows for 9+
            CharacterSelectController.ComputeRosterGridLayout(8, out int cols8, out int rows8, out Vector2 cell8, out _, out Vector2 panel8);
            Assert.AreEqual(1, rows8);
            Assert.AreEqual(8, cols8);
            Assert.Less(panel8.y, 160f, "1-row fighter bar must keep top 65%+ completely open");
            Assert.Less(panel8.x, 1860f);

            CharacterSelectController.ComputeRosterGridLayout(10, out int cols10, out int rows10, out Vector2 cell10, out _, out Vector2 panel10);
            Assert.AreEqual(2, rows10);
            Assert.AreEqual(5, cols10);
            Assert.LessOrEqual(panel10.y, 220f, "2-row fighter bar must remain compact without encroaching on 3D fighters");
            Assert.Less(panel10.x, 1860f);
            Assert.Greater(cell10.x, 100f);

            // Stage bar scaling: 1 row up to 4 stages, 2 rows for 5+
            CharacterSelectController.ComputeStageGridLayout(4, out int sCols4, out int sRows4, out _, out _, out Vector2 sPanel4);
            Assert.AreEqual(1, sRows4);
            Assert.AreEqual(4, sCols4);
            Assert.Less(sPanel4.x, 1860f);

            CharacterSelectController.ComputeStageGridLayout(6, out int sCols6, out int sRows6, out _, out _, out Vector2 sPanel6);
            Assert.AreEqual(2, sRows6);
            Assert.AreEqual(3, sCols6);
            Assert.Less(sPanel6.x, 1860f);
            Assert.LessOrEqual(sPanel6.y, 290f);

            // Verify live runtime GridLayoutGroup adapts cleanly for a 10-fighter roster
            var syntheticFighters = new List<FighterData>();
            for (int i = 0; i < 10; i++)
            {
                var f = Track(ScriptableObject.CreateInstance<FighterData>());
                f.fighterName = $"Fighter_{i + 1}";
                syntheticFighters.Add(f);
            }
            var bigRoster = Track(ScriptableObject.CreateInstance<FighterRoster>());
            var fightersField = typeof(FighterRoster).GetField("fighters", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fightersField.SetValue(bigRoster, syntheticFighters);

            var canvasGo = Track(new GameObject("Test_ScalableGridCanvas", typeof(RectTransform), typeof(Canvas)));
            var ctrl = canvasGo.AddComponent<CharacterSelectController>();
            var tex = Track(new Texture2D(2, 2));
            var spr = Track(Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f)));
            ctrl.ConfigureReferences(bigRoster, null, spr, null, null);
            ctrl.BuildUI(canvasGo.transform, spr);

            Transform grid = canvasGo.transform.Find("RosterGridPanel/Grid");
            Assert.IsNotNull(grid);
            Assert.AreEqual(10, grid.childCount);
            var glg = grid.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            Assert.IsNotNull(glg);
            Assert.AreEqual(5, glg.constraintCount, "10 fighters must auto-wrap into 2 rows of 5 columns");
        }

        [Test]
        public void CharacterSelect_TowerHeaderIsCleanAndPreservesLockedBossRules()
        {
            string tempProgress = Path.Combine(_tempDir, "tower_header_progress.json");
            try
            {
                ProgressionManager.SetCustomFilePathForTesting(tempProgress);
                ProgressionManager.ResetProgress(tempProgress, save: true);

                var fighterRoster = AssetDatabase.LoadAssetAtPath<FighterRoster>("Assets/UnDFight/Data/FighterRoster.asset");
                var stageRoster = AssetDatabase.LoadAssetAtPath<StageRoster>("Assets/UnDFight/Data/StageRoster.asset");
                Assert.IsNotNull(fighterRoster);
                Assert.IsNotNull(stageRoster);

                var canvasGo = Track(new GameObject("Test_TowerHeaderCanvas", typeof(RectTransform), typeof(Canvas)));
                var ctrl = canvasGo.AddComponent<CharacterSelectController>();
                var tex = Track(new Texture2D(2, 2));
                var spr = Track(Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f)));

                GameSessionState.SelectMode(GameMode.ArcadeTower);
                ctrl.ConfigureReferences(fighterRoster, stageRoster, spr, null, null);
                ctrl.BuildUI(canvasGo.transform, spr);

                // Locked boss stays blocked
                ctrl.SetP1Index(7);
                Assert.IsFalse(ctrl.TryConfirmP1());

                // Pick unlocked Lucas and open TowerLadder
                ctrl.SetP1Index(0);
                Assert.IsTrue(ctrl.TryConfirmP1());
                Assert.IsTrue(ctrl.IsTowerLadderOpen);

                // Header must display ONLY "TORRE ARCADE" (never "7 LUTAS • CHEFE FINAL: IBANEIS")
                var heading = ctrl.TowerLadderPanel.transform.Find("TowerHeader/TowerHeading")?.GetComponent<TextMeshProUGUI>();
                Assert.IsNotNull(heading);
                Assert.AreEqual("TORRE ARCADE", heading.text);
                StringAssert.DoesNotContain("7 LUTAS", heading.text);
                StringAssert.DoesNotContain("IBANEIS", heading.text);
            }
            finally
            {
                ProgressionManager.SetCustomFilePathForTesting(null);
            }
        }
    }
}
