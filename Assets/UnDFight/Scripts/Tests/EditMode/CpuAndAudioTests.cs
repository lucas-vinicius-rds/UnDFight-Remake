using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace UnDFight.Tests
{
    public class CpuAndAudioTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();
        private float _savedMasterVolume;
        private float _savedMusicVolume;
        private float _savedSfxVolume;

        [SetUp]
        public void SetUp()
        {
            _savedMasterVolume = SettingsManager.Current.masterVolume;
            _savedMusicVolume = SettingsManager.Current.musicVolume;
            _savedSfxVolume = SettingsManager.Current.sfxVolume;
            GameSessionState.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                {
                    Object.DestroyImmediate(_createdObjects[i]);
                }
            }
            _createdObjects.Clear();

            GameSessionState.Reset();
            AudioListener.volume = _savedMasterVolume;
            SettingsManager.Current.masterVolume = _savedMasterVolume;
            SettingsManager.Current.musicVolume = _savedMusicVolume;
            SettingsManager.Current.sfxVolume = _savedSfxVolume;
        }

        private T Track<T>(T obj) where T : Object
        {
            _createdObjects.Add(obj);
            return obj;
        }

        private (FightSimulation sim, MatchManager match, FighterController p1, FighterController p2, CpuAiController ai) CreateCpuRig(
            CpuDifficulty difficulty,
            bool withMatchManager = false)
        {
            var lp = Track(ScriptableObject.CreateInstance<MoveData>());
            lp.Configure("LightPunch", AttackType.LightPunch, 4, 3, 9, 50, 12, 8, 4, 1.4f, HitHeight.Mid, false, new Vector2(0.68f, 1.18f), new Vector2(0.78f, 0.42f));

            var hp = Track(ScriptableObject.CreateInstance<MoveData>());
            hp.Configure("HeavyPunch", AttackType.HeavyPunch, 7, 4, 16, 110, 18, 12, 7, 2.8f, HitHeight.Mid, false, new Vector2(0.76f, 1.18f), new Vector2(0.92f, 0.46f));

            var lk = Track(ScriptableObject.CreateInstance<MoveData>());
            lk.Configure("LightKick", AttackType.LightKick, 5, 3, 11, 65, 14, 9, 5, 1.8f, HitHeight.Low, false, new Vector2(0.74f, 0.52f), new Vector2(0.92f, 0.44f));

            var hk = Track(ScriptableObject.CreateInstance<MoveData>());
            hk.Configure("HeavyKick", AttackType.HeavyKick, 9, 4, 20, 135, 22, 14, 8, 3.6f, HitHeight.High, true, new Vector2(0.86f, 1.24f), new Vector2(1.10f, 0.56f));

            var data = Track(ScriptableObject.CreateInstance<FighterData>());
            data.fighterName = "Tester";
            data.heightMeters = 1.81f;
            data.weightKg = 92f;
            data.maxHealth = 1000;
            data.lightPunch = lp;
            data.heavyPunch = hp;
            data.lightKick = lk;
            data.heavyKick = hk;

            var simGo = Track(new GameObject("Test_CpuSim"));
            var sim = simGo.AddComponent<FightSimulation>();
            var ai = simGo.AddComponent<CpuAiController>();

            FighterController p1 = CreateFighter("Test_P1", data, -2.2f);
            FighterController p2 = CreateFighter("Test_P2_CPU", data, 2.2f);
            sim.SetParticipants(p1, p2);
            sim.SetCpuAiController(ai);

            MatchManager match = null;
            if (withMatchManager)
            {
                match = simGo.AddComponent<MatchManager>();
                match.InitializeMatch(sim, trainingModeOverride: false);
                ai.Configure(sim, p2, p1, match);
            }
            else
            {
                ai.Configure(sim, p2, p1, null);
            }

            ai.SetDifficulty(difficulty);
            ai.SetSeed(42);
            ai.SetEnabled(true);
            return (sim, match, p1, p2, ai);
        }

        private FighterController CreateFighter(string name, FighterData data, float startX)
        {
            var go = Track(new GameObject(name));
            go.transform.position = new Vector3(startX, 0f, 0f);
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.36f;
            col.height = data.heightMeters;
            col.center = new Vector3(0f, data.heightMeters * 0.5f, 0f);
            go.AddComponent<PlayerInputHandler>();
            var fc = go.AddComponent<FighterController>();
            fc.SetFighterData(data);
            return fc;
        }

        [Test]
        public void CpuAi_WhenDistant_AdvancesTowardOpponent()
        {
            var (sim, _, p1, p2, _) = CreateCpuRig(CpuDifficulty.Medium);
            p1.SetSimPositionX(-3.0f);
            p2.SetSimPositionX(3.0f);
            p1.InputHandler.SetSimulatedInput(default, true);
            sim.ForceUpdateFacing();

            float initialDistance = Mathf.Abs(p2.SimPosition.x - p1.SimPosition.x);
            sim.StepSimulationTicks(35);
            float finalDistance = Mathf.Abs(p2.SimPosition.x - p1.SimPosition.x);

            Assert.Less(finalDistance, initialDistance - 0.5f, "CPU should close horizontal distance when far from P1.");
        }

        [Test]
        public void CpuAi_ReactiveDefense_RespectsReactionDelayTicksWithoutFrameZeroInputReading()
        {
            var (sim, _, p1, p2, ai) = CreateCpuRig(CpuDifficulty.Hard);
            ai.SetGuardProbabilityOverride(1.0f);

            // Prime neutral history outside engagement range so neither fighter starts an attack or hitstop during warmup
            p1.SetSimPositionX(-1.35f);
            p2.SetSimPositionX(1.35f);
            sim.ForceUpdateFacing();
            p1.InputHandler.SetSimulatedInput(default, true);
            sim.StepSimulationTicks(8);

            // Place within attack/defense range immediately before P1 initiates HeavyPunch
            p1.SetSimPositionX(-0.70f);
            p2.SetSimPositionX(0.70f);
            sim.ForceUpdateFacing();

            // P1 starts HeavyPunch (Startup = 7 ticks, active ticks 8..11)
            p1.InputHandler.BufferButton(FighterInputButton.HeavyPunch);

            // Tick 1 of attack: P1 consumes input and enters Attack; CPU saw Idle on input frame
            sim.StepSimulationTicks(1);
            Assert.AreEqual(FighterState.Attack, p1.State, "P1 should enter Attack on tick 1.");
            Assert.IsFalse(p2.InputHandler.CurrentInput.HoldBlock, "CPU must not read input on frame 0.");

            // Ticks strictly before ReactionDelayTicks (Hard = 4 ticks): ticks +1, +2, +3
            for (int t = 1; t < ai.ActiveProfile.ReactionDelayTicks; t++)
            {
                sim.StepSimulationTicks(1);
                Assert.IsFalse(
                    p2.InputHandler.CurrentInput.HoldBlock,
                    $"CPU blocked too early at delay tick {t} (< {ai.ActiveProfile.ReactionDelayTicks}).");
            }

            // On ReactionDelayTicks elapsed, delayed snapshot observes Attack -> CPU engages block
            sim.StepSimulationTicks(1);
            Assert.IsTrue(p2.InputHandler.CurrentInput.HoldBlock, "CPU should engage block once ReactionDelayTicks have elapsed.");

            // Continue through HeavyPunch active window -> attack should be blocked (no HP loss)
            sim.StepSimulationTicks(5);
            Assert.AreEqual(p2.MaxHealth, p2.CurrentHealth, "Blocked HeavyPunch must not deal damage.");
        }

        [Test]
        public void CpuAi_WithinAttackRange_ExecutesAttackWithin45Ticks()
        {
            var (sim, _, p1, p2, _) = CreateCpuRig(CpuDifficulty.Hard);
            p1.SetSimPositionX(-0.52f);
            p2.SetSimPositionX(0.52f);
            p1.InputHandler.SetSimulatedInput(default, true);
            sim.ForceUpdateFacing();

            bool attacked = false;
            for (int t = 0; t < 45; t++)
            {
                sim.StepSimulationTicks(1);
                if (p2.State == FighterState.Attack)
                {
                    attacked = true;
                    break;
                }
            }

            Assert.IsTrue(attacked, "CPU inside close range should initiate at least one attack within 45 ticks.");
        }

        [Test]
        public void CpuAi_DuringRoundIntroAndRoundOver_DoesNotMoveOrAttack()
        {
            var (sim, match, p1, p2, ai) = CreateCpuRig(CpuDifficulty.Hard, withMatchManager: true);
            float startP2X = p2.SimPosition.x;

            Assert.AreEqual(MatchPhase.RoundIntro, match.Phase);
            sim.StepSimulationTicks(40);
            Assert.AreEqual(MatchPhase.RoundIntro, match.Phase);
            Assert.AreEqual(startP2X, p2.SimPosition.x, 1e-4f, "CPU must not move during RoundIntro.");
            Assert.AreNotEqual(FighterState.Attack, p2.State, "CPU must not attack during RoundIntro.");

            // Advance into Fighting, trigger a Round 1 KO -> RoundOver, and verify CPU freezes inputs
            match.SkipIntroToFighting();
            Assert.IsTrue(match.InputsAllowed);

            ai.SetGuardProbabilityOverride(0f);
            p1.SetSimPositionX(-0.55f);
            p2.SetSimPositionX(0.55f);
            sim.ForceUpdateFacing();
            p1.Data.heavyPunch.damage = 1000;
            p1.InputHandler.SetSimulatedInput(default, true);
            p1.InputHandler.BufferButton(FighterInputButton.HeavyPunch);

            for (int t = 0; t < 20 && match.Phase == MatchPhase.Fighting; t++)
            {
                sim.StepSimulationTicks(1);
            }

            Assert.AreEqual(MatchPhase.RoundOver, match.Phase);
            Assert.IsFalse(match.InputsAllowed);

            ai.SetGuardProbabilityOverride(1.0f);
            sim.ClearHitstop();
            // Let post-KO hit knockback momentum decay while staying inside RoundOver
            for (int settle = 0; settle < 40 && Mathf.Abs(p2.SimVelocity.x) > 1e-4f; settle++)
            {
                sim.StepSimulationTicks(1);
            }

            Assert.AreEqual(MatchPhase.RoundOver, match.Phase);
            float roundOverX = p2.SimPosition.x;
            sim.StepSimulationTicks(25);
            Assert.IsFalse(p2.InputHandler.CurrentInput.HoldBlock);
            Assert.AreEqual(0f, p2.InputHandler.CurrentInput.MoveX, 1e-4f);
            Assert.AreNotEqual(FighterState.Attack, p2.State);
            Assert.AreEqual(roundOverX, p2.SimPosition.x, 1e-4f);
        }

        [Test]
        public void AudioAndShake_FactoryClipsGenerateSamplesVolumesSyncAndCameraShakes()
        {
            var clips = new List<AudioClip>
            {
                ProceduralAudioFactory.CreateUiMoveClip(),
                ProceduralAudioFactory.CreateUiConfirmClip(),
                ProceduralAudioFactory.CreateUiBackClip(),
                ProceduralAudioFactory.CreateWhooshLightClip(),
                ProceduralAudioFactory.CreateWhooshHeavyClip(),
                ProceduralAudioFactory.CreateHitLightClip(),
                ProceduralAudioFactory.CreateHitHeavyClip(),
                ProceduralAudioFactory.CreateBlockImpactClip(),
                ProceduralAudioFactory.CreateKnockdownClip(),
                ProceduralAudioFactory.CreateRoundGongClip(),
                ProceduralAudioFactory.CreateAmbientMusicLoopClip()
            };

            foreach (AudioClip clip in clips)
            {
                Assert.IsNotNull(clip);
                Track(clip);
                Assert.Greater(clip.samples, 0, $"Clip {clip.name} must contain generated samples.");
                Assert.Greater(clip.length, 0f, $"Clip {clip.name} must have positive length.");
            }

            var audioGo = Track(new GameObject("Test_AudioManager"));
            var audioMgr = audioGo.AddComponent<FightAudioManager>();
            audioMgr.EnsureAudioReady();

            var settings = GameSettingsData.CreateDefaults();
            settings.masterVolume = 0.60f;
            settings.musicVolume = 0.50f;
            settings.sfxVolume = 0.40f;
            SettingsManager.ApplySettings(settings, applyResolution: false);

            Assert.AreEqual(0.60f, AudioListener.volume, 1e-3f);
            Assert.AreEqual(0.50f * 0.45f, audioMgr.MusicSource.volume, 1e-3f);
            Assert.AreEqual(0.40f, audioMgr.SfxVolume, 1e-3f);

            // Session CPU difficulty cycling & labels
            Assert.AreEqual(CpuDifficulty.Medium, GameSessionState.SelectedCpuDifficulty);
            Assert.AreEqual("MÉDIO", GameSessionState.GetCpuDifficultyLabel(GameSessionState.SelectedCpuDifficulty));
            GameSessionState.CycleCpuDifficulty(1);
            Assert.AreEqual(CpuDifficulty.Hard, GameSessionState.SelectedCpuDifficulty);
            Assert.AreEqual("DIFÍCIL", GameSessionState.GetCpuDifficultyLabel(CpuDifficulty.Hard));
            GameSessionState.CycleCpuDifficulty(1);
            Assert.AreEqual(CpuDifficulty.Easy, GameSessionState.SelectedCpuDifficulty);
            Assert.AreEqual("FÁCIL", GameSessionState.GetCpuDifficultyLabel(CpuDifficulty.Easy));

            // FightCameraController impact shake offsets XY without touching Z and decays to 0
            var camGo = Track(new GameObject("Test_FightCamera"));
            var cam = camGo.AddComponent<FightCameraController>();
            var t1 = Track(new GameObject("T1")).transform;
            var t2 = Track(new GameObject("T2")).transform;
            var mid = Track(new GameObject("Mid")).transform;
            t1.position = new Vector3(-1f, 0f, 0f);
            t2.position = new Vector3(1f, 0f, 0f);
            cam.ConfigureTargets(t1, t2, mid);

            cam.TriggerImpactShake(0.14f, 0.16f);
            cam.StepTick(1f / 60f);
            Assert.Greater(cam.CurrentShakeOffsetMagnitude, 0.001f);
            Assert.AreEqual(0f, mid.position.z, 1e-5f);

            for (int i = 0; i < 15; i++)
            {
                cam.StepTick(1f / 60f);
            }
            Assert.AreEqual(0f, cam.CurrentShakeOffsetMagnitude, 1e-4f);
            Assert.AreEqual(0f, mid.position.z, 1e-5f);
        }
    }
}
