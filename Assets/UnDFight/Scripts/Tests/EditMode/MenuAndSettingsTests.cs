using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
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
    }
}
