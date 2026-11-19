using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace UnDFight.Tests
{
    public class ArcadeTowerAndProgressionTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();
        private string _tempDir;
        private string _tempProgressPath;

        [SetUp]
        public void SetUp()
        {
            GameSessionState.Reset();
            _tempDir = Path.Combine(Path.GetTempPath(), "UnDFight_TowerTests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
            _tempProgressPath = Path.Combine(_tempDir, "undfight_progress.json");
            ProgressionManager.SetCustomFilePathForTesting(_tempProgressPath);
            ProgressionManager.ResetProgress(_tempProgressPath, save: true);
        }

        [TearDown]
        public void TearDown()
        {
            GameSessionState.Reset();
            ProgressionManager.SetCustomFilePathForTesting(null);

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

        private Sprite CreateTestSprite()
        {
            var tex = Track(new Texture2D(2, 2, TextureFormat.RGBA32, false));
            return Track(Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f)));
        }

        [Test]
        public void ProgressionManager_SaveLoadResetAndUnlockIbaneis_PersistsCleanlyToJson()
        {
            var lucas = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Lucas.asset");
            var ibaneis = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Ibaneis.asset");
            Assert.IsNotNull(lucas);
            Assert.IsNotNull(ibaneis);

            // Ibaneis is locked by default; regular fighters are unlocked by default
            Assert.IsTrue(ibaneis.isSecretBoss);
            Assert.IsFalse(ibaneis.unlockedByDefault);
            Assert.IsTrue(ProgressionManager.IsFighterUnlocked(lucas));
            Assert.IsFalse(ProgressionManager.IsFighterUnlocked(ibaneis));

            // First unlock returns true and persists to disk
            Assert.IsTrue(ProgressionManager.UnlockFighter(ibaneis, _tempProgressPath));
            Assert.IsTrue(File.Exists(_tempProgressPath));
            Assert.IsTrue(ProgressionManager.IsFighterUnlocked(ibaneis));

            // Subsequent unlock returns false (already unlocked)
            Assert.IsFalse(ProgressionManager.UnlockFighter(ibaneis, _tempProgressPath));

            // Reload from disk into a fresh instance confirms persistence
            GameProgressionData reloaded = ProgressionManager.Load(_tempProgressPath);
            Assert.IsTrue(ProgressionManager.IsFighterUnlocked(ibaneis, reloaded));
            Assert.AreEqual(1, reloaded.unlockedFighterNames.Count);
            Assert.AreEqual("Ibaneis", reloaded.unlockedFighterNames[0]);

            // Reset restores default locked state
            ProgressionManager.ResetProgress(_tempProgressPath, save: true);
            Assert.IsFalse(ProgressionManager.IsFighterUnlocked(ibaneis));
            GameProgressionData afterReset = ProgressionManager.Load(_tempProgressPath);
            Assert.AreEqual(0, afterReset.unlockedFighterNames.Count);
            Assert.AreEqual(0, afterReset.towerCompletionsCount);
            Assert.AreEqual(-1, afterReset.highestTowerDifficultyCleared);
        }

        [Test]
        public void ProgressionManager_RecordTowerVictory_TracksCompletionsDifficultyAndFirstBossUnlock()
        {
            var lucas = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Lucas.asset");
            var tiago = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Tiago.asset");
            var ibaneis = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Ibaneis.asset");

            // First Tower completion on Medium unlocks Ibaneis
            bool unlockedFirst = ProgressionManager.RecordTowerVictory(lucas, CpuDifficulty.Medium, ibaneis, _tempProgressPath);
            Assert.IsTrue(unlockedFirst);
            Assert.IsTrue(ProgressionManager.IsFighterUnlocked(ibaneis));

            GameProgressionData p1 = ProgressionManager.Load(_tempProgressPath);
            Assert.AreEqual(1, p1.towerCompletionsCount);
            Assert.AreEqual((int)CpuDifficulty.Medium, p1.highestTowerDifficultyCleared);
            Assert.IsTrue(p1.ContainsTowerWinnerName("Lucas"));

            // Second completion on Hard with Tiago returns false (already unlocked) and updates stats
            bool unlockedSecond = ProgressionManager.RecordTowerVictory(tiago, CpuDifficulty.Hard, ibaneis, _tempProgressPath);
            Assert.IsFalse(unlockedSecond);

            GameProgressionData p2 = ProgressionManager.Load(_tempProgressPath);
            Assert.AreEqual(2, p2.towerCompletionsCount);
            Assert.AreEqual((int)CpuDifficulty.Hard, p2.highestTowerDifficultyCleared);
            Assert.AreEqual(2, p2.towerCompletedByFighters.Count);
            Assert.IsTrue(p2.ContainsTowerWinnerName("Tiago"));
        }

        [Test]
        public void ArcadeTowerRun_BuildRun_Produces7Matches6UniqueRegularsAndBossIbaneisAtArenaCadeia()
        {
            var fighterRoster = AssetDatabase.LoadAssetAtPath<FighterRoster>("Assets/UnDFight/Data/FighterRoster.asset");
            var stageRoster = AssetDatabase.LoadAssetAtPath<StageRoster>("Assets/UnDFight/Data/StageRoster.asset");
            var lucas = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Lucas.asset");
            var ibaneis = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Ibaneis.asset");
            Assert.IsNotNull(fighterRoster);
            Assert.IsNotNull(stageRoster);

            ArcadeTowerRun run = ArcadeTowerRun.BuildRun(lucas, fighterRoster, stageRoster, CpuDifficulty.Medium, randomSeed: 12345);

            Assert.AreEqual(7, run.TotalMatches);
            Assert.AreSame(lucas, run.PlayerFighter);
            Assert.AreEqual(0, run.CurrentMatchIndex);
            Assert.AreEqual(0, run.ContinuesUsed);
            Assert.IsFalse(run.IsCompleted);

            // First 6 opponents must be unique regular fighters, never repeating the player or the boss
            var seenRegulars = new HashSet<FighterData>();
            for (int i = 0; i < 6; i++)
            {
                ArcadeTowerStageEntry entry = run.Matches[i];
                Assert.IsNotNull(entry.opponent, $"Match {i + 1} missing opponent");
                Assert.IsFalse(entry.isBossMatch, $"Match {i + 1} must not be marked boss");
                Assert.IsFalse(entry.opponent.isSecretBoss, $"Match {i + 1} opponent must not be secret boss");
                Assert.AreNotSame(lucas, entry.opponent, $"Match {i + 1} must not repeat player fighter");
                Assert.IsTrue(seenRegulars.Add(entry.opponent), $"Match {i + 1} repeated opponent {entry.opponent.fighterName}");
                Assert.IsNotNull(stageRoster.FindBySceneName(entry.stageSceneName), $"Invalid stage scene {entry.stageSceneName}");
            }
            Assert.AreEqual(6, seenRegulars.Count);

            // Atmospheric progression across the 6 regular matches
            Assert.AreEqual("Arena_Favela", run.Matches[0].stageSceneName);
            Assert.AreEqual(StageWeatherVariant.Day, run.Matches[0].weather);
            Assert.AreEqual("Arena_UnDF", run.Matches[1].stageSceneName);
            Assert.AreEqual(StageWeatherVariant.Day, run.Matches[1].weather);
            Assert.AreEqual("Arena_UnDF", run.Matches[2].stageSceneName);
            Assert.AreEqual(StageWeatherVariant.Night, run.Matches[2].weather);
            Assert.AreEqual("Arena_Favela", run.Matches[3].stageSceneName);
            Assert.AreEqual(StageWeatherVariant.Night, run.Matches[3].weather);
            Assert.AreEqual("Arena_Favela", run.Matches[4].stageSceneName);
            Assert.AreEqual(StageWeatherVariant.RainyNight, run.Matches[4].weather);
            Assert.AreEqual("Arena_UnDF", run.Matches[5].stageSceneName);
            Assert.AreEqual(StageWeatherVariant.RainyNight, run.Matches[5].weather);

            // 7th match is always Secret Final Boss Ibaneis at Arena_Cadeia (fixed Day/Indoor) on Hard for Medium base
            ArcadeTowerStageEntry bossMatch = run.Matches[6];
            Assert.IsTrue(bossMatch.isBossMatch);
            Assert.AreSame(ibaneis, bossMatch.opponent);
            Assert.AreEqual("Arena_Cadeia", bossMatch.stageSceneName);
            Assert.AreEqual(StageWeatherVariant.Day, bossMatch.weather);
            Assert.AreEqual(CpuDifficulty.Hard, bossMatch.cpuDifficulty);
            Assert.IsNotNull(stageRoster.FindBySceneName(bossMatch.stageSceneName));

            // Easy base scales Boss to Medium
            ArcadeTowerRun easyRun = ArcadeTowerRun.BuildRun(lucas, fighterRoster, stageRoster, CpuDifficulty.Easy, randomSeed: 7);
            Assert.AreEqual(CpuDifficulty.Easy, easyRun.Matches[0].cpuDifficulty);
            Assert.AreEqual(CpuDifficulty.Medium, easyRun.Matches[6].cpuDifficulty);

            // Playing as unlocked Ibaneis still produces 6 distinct regular opponents + Boss Ibaneis at match 7
            ArcadeTowerRun bossPlayerRun = ArcadeTowerRun.BuildRun(ibaneis, fighterRoster, stageRoster, CpuDifficulty.Hard, randomSeed: 99);
            Assert.AreEqual(7, bossPlayerRun.TotalMatches);
            var seenForBossRun = new HashSet<FighterData>();
            for (int i = 0; i < 6; i++)
            {
                Assert.IsFalse(bossPlayerRun.Matches[i].opponent.isSecretBoss);
                Assert.IsTrue(seenForBossRun.Add(bossPlayerRun.Matches[i].opponent));
            }
            Assert.AreSame(ibaneis, bossPlayerRun.Matches[6].opponent);
        }

        [Test]
        public void CharacterSelectController_BlocksLockedIbaneisAndAllowsSelectionImmediatelyAfterUnlock()
        {
            var fighterRoster = AssetDatabase.LoadAssetAtPath<FighterRoster>("Assets/UnDFight/Data/FighterRoster.asset");
            var stageRoster = AssetDatabase.LoadAssetAtPath<StageRoster>("Assets/UnDFight/Data/StageRoster.asset");
            var ibaneis = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Ibaneis.asset");
            Assert.IsNotNull(fighterRoster);
            Assert.IsNotNull(ibaneis);

            int ibaneisIdx = fighterRoster.IndexOf(ibaneis);
            Assert.AreEqual(7, ibaneisIdx);

            var canvasGo = Track(new GameObject("TestTowerSelectCanvas", typeof(RectTransform), typeof(Canvas)));
            var p1Anchor = Track(new GameObject("P1Anchor")).transform;
            var p2Anchor = Track(new GameObject("P2Anchor")).transform;
            var ctrl = canvasGo.AddComponent<CharacterSelectController>();
            Sprite sprite = CreateTestSprite();

            GameSessionState.SelectMode(GameMode.ArcadeTower);
            ctrl.ConfigureReferences(fighterRoster, stageRoster, sprite, p1Anchor, p2Anchor);
            ctrl.BuildUI(canvasGo.transform, sprite);

            Transform grid = canvasGo.transform.Find("RosterGridPanel/Grid");
            Assert.IsNotNull(grid);
            var ibaneisCardName = grid.GetChild(ibaneisIdx).Find("Name")?.GetComponent<TextMeshProUGUI>();
            var ibaneisCardBadge = grid.GetChild(ibaneisIdx).Find("Badge")?.GetComponent<TextMeshProUGUI>();

            // While locked: card shows "???" + "BLOQUEADO", preview is hidden, confirmation is blocked
            ctrl.SetP1Index(ibaneisIdx);
            Assert.AreEqual("???", ibaneisCardName.text);
            Assert.AreEqual("BLOQUEADO", ibaneisCardBadge.text);
            Assert.AreEqual("???", ctrl.P1NameText.text);
            StringAssert.Contains("MODO TORRE", ctrl.P1StatusText.text);
            Assert.IsFalse(ctrl.HasP1PreviewInstance, "Locked fighter must not instantiate 3D preview");
            Assert.IsFalse(ctrl.TryConfirmP1(), "Locked Ibaneis must not be confirmable");
            Assert.IsFalse(ctrl.P1Confirmed);
            Assert.AreEqual(CharacterSelectController.SelectStep.FighterPick, ctrl.CurrentStep);

            // Unlock Ibaneis -> immediately displays name/badge, instantiates preview, and confirms into TowerLadder
            Assert.IsTrue(ProgressionManager.UnlockFighter(ibaneis, _tempProgressPath));
            ctrl.RefreshAll();

            Assert.AreEqual("IBANEIS", ibaneisCardName.text);
            Assert.AreEqual("IBANEIS", ctrl.P1NameText.text);
            Assert.IsTrue(ctrl.HasP1PreviewInstance, "Unlocked Ibaneis must restore 3D preview");
            Assert.IsTrue(ctrl.TryConfirmP1(), "Unlocked Ibaneis must be selectable");
            Assert.IsTrue(ctrl.P1Confirmed);
            Assert.AreEqual(CharacterSelectController.SelectStep.TowerLadder, ctrl.CurrentStep);
            Assert.IsTrue(ctrl.IsTowerLadderOpen);
            Assert.IsNotNull(GameSessionState.ActiveTowerRun);
            Assert.AreEqual(7, GameSessionState.ActiveTowerRun.TotalMatches);
        }

        [Test]
        public void MatchManager_InArcadeTower_AutoEnablesCpuAndUnlocksIbaneisOnBossDefeat()
        {
            var fighterRoster = AssetDatabase.LoadAssetAtPath<FighterRoster>("Assets/UnDFight/Data/FighterRoster.asset");
            var stageRoster = AssetDatabase.LoadAssetAtPath<StageRoster>("Assets/UnDFight/Data/StageRoster.asset");
            var lucas = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Lucas.asset");
            var ibaneis = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Ibaneis.asset");
            Assert.IsFalse(ProgressionManager.IsFighterUnlocked(ibaneis));

            ArcadeTowerRun run = ArcadeTowerRun.BuildRun(lucas, fighterRoster, stageRoster, CpuDifficulty.Medium, randomSeed: 42);
            while (run.HasNextMatch)
            {
                run.AdvanceToNextMatch();
            }
            Assert.IsTrue(run.IsCurrentMatchBoss);
            run.ApplyCurrentMatchToSession();

            // Clone moves/fighters in memory so we can deliver an instant lethal hit cleanly
            var oneShotMove = Track(ScriptableObject.CreateInstance<MoveData>());
            oneShotMove.Configure("OneShot", AttackType.HeavyPunch, 4, 4, 10, 2000, 18, 12, 6, 2.0f, HitHeight.Mid, true, new Vector2(0.75f, 1.15f), new Vector2(1.0f, 0.55f));

            var testP1Data = Track(Object.Instantiate(lucas));
            testP1Data.heavyPunch = oneShotMove;
            var testBossData = Track(Object.Instantiate(ibaneis));

            var simGo = Track(new GameObject("Test_TowerMatchSim"));
            var sim = simGo.AddComponent<FightSimulation>();
            var match = simGo.AddComponent<MatchManager>();

            var p1Go = Track(new GameObject("P1_Lucas"));
            p1Go.transform.position = new Vector3(-0.55f, 0f, 0f);
            p1Go.AddComponent<PlayerInputHandler>();
            var p1 = p1Go.AddComponent<FighterController>();
            p1.SetFighterData(testP1Data);

            var p2Go = Track(new GameObject("P2_Ibaneis"));
            p2Go.transform.position = new Vector3(0.55f, 0f, 0f);
            p2Go.AddComponent<PlayerInputHandler>();
            var p2 = p2Go.AddComponent<FighterController>();
            p2.SetFighterData(testBossData);

            sim.SetParticipants(p1, p2);
            match.InitializeMatch(sim, trainingModeOverride: false);

            // Verify CPU AI was automatically enabled and configured for ArcadeTower Boss difficulty (Hard)
            Assert.IsNotNull(sim.CpuAi, "MatchManager must attach/wire CpuAiController in ArcadeTower");
            Assert.IsTrue(sim.CpuAi.IsEnabled, "CpuAiController must be enabled automatically in GameMode.ArcadeTower");
            Assert.AreEqual(CpuDifficulty.Hard, sim.CpuAi.Difficulty);

            // Disable guard probability so P1 wins 2 rounds cleanly and triggers MatchOver
            sim.CpuAi.SetGuardProbabilityOverride(0f);

            for (int round = 0; round < match.RoundsToWin; round++)
            {
                match.SkipIntroToFighting();
                p1.SetSimPositionX(-0.52f);
                p2.SetSimPositionX(0.52f);
                sim.ForceUpdateFacing();
                p1.InputHandler.SetSimulatedInput(default, true);
                p1.InputHandler.BufferButton(FighterInputButton.HeavyPunch);

                for (int t = 0; t < 25 && match.Phase == MatchPhase.Fighting; t++)
                {
                    sim.StepSimulationTicks(1);
                }
                Assert.AreEqual(MatchPhase.RoundOver, match.Phase, $"Expected RoundOver after KO in round {round + 1}");

                // Step through RoundOver until next RoundIntro or MatchOver
                for (int t = 0; t < 200 && match.Phase == MatchPhase.RoundOver; t++)
                {
                    sim.StepSimulationTicks(1);
                }
            }

            Assert.AreEqual(MatchPhase.MatchOver, match.Phase);
            Assert.AreEqual(0, match.WinnerPlayerIndex);
            Assert.IsTrue(run.IsCompleted);
            Assert.IsTrue(run.UnlockedBossThisRun);
            Assert.IsTrue(ProgressionManager.IsFighterUnlocked(ibaneis), "Defeating Boss Ibaneis on Match 7 must unlock Ibaneis permanently");

            GameProgressionData saved = ProgressionManager.Load(_tempProgressPath);
            Assert.AreEqual(1, saved.towerCompletionsCount);
            Assert.IsTrue(saved.ContainsUnlockedFighterName("Ibaneis"));
            Assert.IsTrue(saved.ContainsTowerWinnerName("Lucas"));

            // Also verify FightMenuController renders Champion panel with Boss Unlock banner
            var menuCanvasGo = Track(new GameObject("Test_TowerMenuCanvas", typeof(RectTransform), typeof(Canvas)));
            var fightMenu = menuCanvasGo.AddComponent<FightMenuController>();
            fightMenu.Configure(sim, match, null, CreateTestSprite());
            fightMenu.BuildUI(menuCanvasGo.transform, CreateTestSprite());
            fightMenu.HandleMatchStateChanged(match);

            Assert.IsTrue(fightMenu.IsTowerMatchOverOpen);
            StringAssert.Contains("CAMPEÃO", fightMenu.TowerMatchOverTitle);
            Assert.IsTrue(fightMenu.IsBossUnlockBannerVisible);
            StringAssert.Contains("IBANEIS", fightMenu.TowerUnlockBannerText);
        }
    }
}
