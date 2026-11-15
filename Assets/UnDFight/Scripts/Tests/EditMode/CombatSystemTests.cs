using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace UnDFight.Tests
{
    public class CombatSystemTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();

        private sealed class FighterRig
        {
            public FightSimulation Sim;
            public MatchManager Match;
            public FighterController P1;
            public FighterController P2;
            public FighterData LucasData;
            public FighterData TiagoData;
            public MoveData LightPunch;
            public MoveData HeavyPunch;
            public MoveData LightKick;
            public MoveData HeavyKick;
        }

        [SetUp]
        public void SetUp()
        {
            GameSessionState.CurrentMode = GameMode.LocalVersus;
        }

        [TearDown]
        public void TearDown()
        {
            GameSessionState.CurrentMode = GameMode.LocalVersus;
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                {
                    Object.DestroyImmediate(_createdObjects[i]);
                }
            }
            _createdObjects.Clear();
        }

        private T Track<T>(T obj) where T : Object
        {
            _createdObjects.Add(obj);
            return obj;
        }

        private FighterRig CreateRig(bool withMatchManager = false)
        {
            var rig = new FighterRig();

            rig.LightPunch = Track(ScriptableObject.CreateInstance<MoveData>());
            rig.LightPunch.Configure("LightPunch", AttackType.LightPunch, 4, 3, 9, 50, 12, 8, 4, 1.4f, HitHeight.Mid, false, new Vector2(0.68f, 1.18f), new Vector2(0.78f, 0.42f));

            rig.HeavyPunch = Track(ScriptableObject.CreateInstance<MoveData>());
            rig.HeavyPunch.Configure("HeavyPunch", AttackType.HeavyPunch, 7, 4, 16, 110, 18, 12, 7, 2.8f, HitHeight.Mid, false, new Vector2(0.76f, 1.18f), new Vector2(0.92f, 0.46f));

            rig.LightKick = Track(ScriptableObject.CreateInstance<MoveData>());
            rig.LightKick.Configure("LightKick", AttackType.LightKick, 5, 3, 11, 65, 14, 9, 5, 1.8f, HitHeight.Low, false, new Vector2(0.74f, 0.52f), new Vector2(0.92f, 0.44f));

            rig.HeavyKick = Track(ScriptableObject.CreateInstance<MoveData>());
            rig.HeavyKick.Configure("HeavyKick", AttackType.HeavyKick, 9, 4, 20, 135, 22, 14, 8, 3.6f, HitHeight.High, true, new Vector2(0.86f, 1.24f), new Vector2(1.10f, 0.56f));

            rig.LucasData = CreateFighterData("Lucas", 1.81f, 92f, rig);
            rig.TiagoData = CreateFighterData("Tiago", 1.79f, 87f, rig);

            var simGo = Track(new GameObject("Test_FightSimulation"));
            rig.Sim = simGo.AddComponent<FightSimulation>();

            rig.P1 = CreateFighterObject("Test_P1_Lucas", rig.LucasData, -0.55f);
            rig.P2 = CreateFighterObject("Test_P2_Tiago", rig.TiagoData, 0.55f);

            rig.Sim.SetParticipants(rig.P1, rig.P2);

            if (withMatchManager)
            {
                rig.Match = simGo.AddComponent<MatchManager>();
                rig.Match.InitializeMatch(rig.Sim);
                // InitializeMatch resets positions to -2.2 / +2.2; move closer when tests attack
            }

            return rig;
        }

        private FighterData CreateFighterData(string name, float height, float weight, FighterRig rig)
        {
            var data = Track(ScriptableObject.CreateInstance<FighterData>());
            data.fighterName = name;
            data.heightMeters = height;
            data.weightKg = weight;
            data.maxHealth = 1000;
            data.lightPunch = rig.LightPunch;
            data.heavyPunch = rig.HeavyPunch;
            data.lightKick = rig.LightKick;
            data.heavyKick = rig.HeavyKick;
            return data;
        }

        private FighterController CreateFighterObject(string name, FighterData data, float startX)
        {
            var go = Track(new GameObject(name));
            go.transform.position = new Vector3(startX, 0f, 0f);
            go.AddComponent<PlayerInputHandler>();
            var controller = go.AddComponent<FighterController>();
            controller.SetFighterData(data);
            return controller;
        }

        [Test]
        public void AttackWindow_StartupActiveRecovery_RespectsTicksAndSingleHitPerExecution()
        {
            FighterRig rig = CreateRig();
            rig.P1.InputHandler.SetSimulatedInput(default, true);
            rig.P2.InputHandler.SetSimulatedInput(default, true);

            // Buffer LightPunch (Startup=4, Active=3 [5..7], Recovery=9, Total=16)
            rig.P1.InputHandler.BufferButton(FighterInputButton.LightPunch);

            // Tick 1: consumes attack and enters Attack (StateTicks = 0)
            rig.Sim.StepSimulationTicks(1);
            Assert.AreEqual(FighterState.Attack, rig.P1.State);
            Assert.AreEqual(0, rig.P1.StateTicks);
            Assert.AreEqual(1000, rig.P2.CurrentHealth);

            // Startup ticks 1..4 -> no hit yet
            rig.Sim.StepSimulationTicks(4);
            Assert.AreEqual(4, rig.P1.StateTicks);
            Assert.AreEqual(1000, rig.P2.CurrentHealth);
            Assert.AreEqual(FighterState.Idle, rig.P2.State);

            // First active tick (StateTicks == 5) -> hit registers once + 4-tick hitstop triggers
            rig.Sim.StepSimulationTicks(1);
            Assert.AreEqual(5, rig.P1.StateTicks);
            Assert.AreEqual(950, rig.P2.CurrentHealth);
            Assert.AreEqual(FighterState.Hit, rig.P2.State);
            Assert.IsTrue(rig.P1.HasRegisteredHit);
            Assert.AreEqual(4, rig.Sim.HitstopTicksRemaining);

            // Complete hitstop (4 ticks) + remaining active & recovery ticks (11 ticks) -> single hit only!
            rig.Sim.StepSimulationTicks(4 + 11);
            Assert.AreEqual(950, rig.P2.CurrentHealth);
            Assert.AreEqual(FighterState.Idle, rig.P1.State);
            // Hitstun is 12 ticks vs 11 remaining attack ticks (+1 frame advantage), so 1 more tick finishes hitstun
            Assert.AreEqual(FighterState.Hit, rig.P2.State);
            rig.Sim.StepSimulationTicks(1);
            Assert.AreEqual(FighterState.Idle, rig.P2.State);
        }

        [Test]
        public void HitResolution_AppliesDamageHitstunAndWeightScaledKnockback()
        {
            // Case A: Light attacker hits heavier Lucas (92 kg)
            FighterRig rigHeavyDef = CreateRig();
            rigHeavyDef.P2.SetFighterData(rigHeavyDef.LucasData);
            rigHeavyDef.P2.ResetForRound(0.55f, -1);
            rigHeavyDef.P1.InputHandler.SetSimulatedInput(default, true);
            rigHeavyDef.P2.InputHandler.SetSimulatedInput(default, true);
            rigHeavyDef.P1.InputHandler.BufferButton(FighterInputButton.HeavyPunch);

            // Consumption tick (1) + Startup (7) + 1st active tick (1) = 9 ticks
            rigHeavyDef.Sim.StepSimulationTicks(9);
            Assert.AreEqual(890, rigHeavyDef.P2.CurrentHealth);
            Assert.AreEqual(FighterState.Hit, rigHeavyDef.P2.State);
            float lucasStartX = rigHeavyDef.P2.SimPosition.x;

            // Wait out hitstop (7) + hitstun (18)
            rigHeavyDef.Sim.StepSimulationTicks(7 + 18);
            float lucasDisplacement = rigHeavyDef.P2.SimPosition.x - lucasStartX;
            Assert.AreEqual(FighterState.Idle, rigHeavyDef.P2.State);

            // Case B: Attacker hits lighter Tiago (87 kg)
            FighterRig rigLightDef = CreateRig();
            rigLightDef.P1.InputHandler.SetSimulatedInput(default, true);
            rigLightDef.P2.InputHandler.SetSimulatedInput(default, true);
            rigLightDef.P1.InputHandler.BufferButton(FighterInputButton.HeavyPunch);

            rigLightDef.Sim.StepSimulationTicks(9);
            Assert.AreEqual(890, rigLightDef.P2.CurrentHealth);
            float tiagoStartX = rigLightDef.P2.SimPosition.x;

            rigLightDef.Sim.StepSimulationTicks(7 + 18);
            float tiagoDisplacement = rigLightDef.P2.SimPosition.x - tiagoStartX;

            Assert.Greater(lucasDisplacement, 0.2f);
            Assert.Greater(tiagoDisplacement, lucasDisplacement);
        }

        [Test]
        public void BlockResolution_StandingAndCrouching_PreventsDamageAndAppliesBlockstun()
        {
            // 1. Standing block (HoldBlock) stops HeavyPunch (Mid) with 0 damage & 12 ticks blockstun
            FighterRig rigStandBlock = CreateRig();
            rigStandBlock.P1.InputHandler.SetSimulatedInput(default, true);
            rigStandBlock.P2.InputHandler.SetSimulatedInput(new FighterInputState { HoldBlock = true }, true);
            rigStandBlock.P1.InputHandler.BufferButton(FighterInputButton.HeavyPunch);

            rigStandBlock.Sim.StepSimulationTicks(1 + 8); // consume + startup 7 + tick 8 active
            Assert.AreEqual(1000, rigStandBlock.P2.CurrentHealth);
            Assert.AreEqual(FighterState.Block, rigStandBlock.P2.State);

            // Release HoldBlock during blockstun -> still locked in Block for remaining blockstun ticks!
            rigStandBlock.P2.InputHandler.SetSimulatedInput(default, true);
            rigStandBlock.Sim.StepSimulationTicks(7 + 6); // 7 hitstop + 6 blockstun ticks
            Assert.AreEqual(FighterState.Block, rigStandBlock.P2.State);
            rigStandBlock.Sim.StepSimulationTicks(6); // finish 12-tick blockstun
            Assert.AreEqual(FighterState.Idle, rigStandBlock.P2.State);

            // 2. Holding Back (without HoldBlock button) also blocks High HeavyKick when hit
            FighterRig rigHoldBack = CreateRig();
            rigHoldBack.P1.InputHandler.SetSimulatedInput(default, true);
            rigHoldBack.P2.InputHandler.SetSimulatedInput(new FighterInputState { MoveX = 1f }, true); // P2 faces -1, so +1 is Back
            rigHoldBack.P1.InputHandler.BufferButton(FighterInputButton.HeavyKick);
            rigHoldBack.Sim.StepSimulationTicks(1 + 10); // consume + startup 9 + tick 10 active
            Assert.AreEqual(1000, rigHoldBack.P2.CurrentHealth);
            Assert.AreEqual(FighterState.Block, rigHoldBack.P2.State);

            // 3. Standing Block fails against Low LightKick, while Crouch Block defends Low LightKick
            FighterRig rigLowCheck = CreateRig();
            rigLowCheck.P1.InputHandler.SetSimulatedInput(default, true);
            rigLowCheck.P2.InputHandler.SetSimulatedInput(new FighterInputState { HoldBlock = true }, true);
            rigLowCheck.P1.InputHandler.BufferButton(FighterInputButton.LightKick);
            rigLowCheck.Sim.StepSimulationTicks(1 + 6);
            Assert.AreEqual(935, rigLowCheck.P2.CurrentHealth);
            Assert.AreEqual(FighterState.Hit, rigLowCheck.P2.State);

            FighterRig rigCrouchBlock = CreateRig();
            rigCrouchBlock.P1.InputHandler.SetSimulatedInput(default, true);
            rigCrouchBlock.P2.InputHandler.SetSimulatedInput(new FighterInputState { MoveY = -1f, HoldBlock = true }, true);
            rigCrouchBlock.P1.InputHandler.BufferButton(FighterInputButton.LightKick);
            rigCrouchBlock.Sim.StepSimulationTicks(1 + 6);
            Assert.AreEqual(1000, rigCrouchBlock.P2.CurrentHealth);
            Assert.AreEqual(FighterState.Block, rigCrouchBlock.P2.State);
        }

        [Test]
        public void HeavyKick_KnockdownAndGetUpWithWakeupInvincibility()
        {
            FighterRig rig = CreateRig();
            rig.P1.InputHandler.SetSimulatedInput(default, true);
            rig.P2.InputHandler.SetSimulatedInput(default, true);
            rig.P1.InputHandler.BufferButton(FighterInputButton.HeavyKick);

            rig.Sim.StepSimulationTicks(1 + 10); // active tick 10
            Assert.AreEqual(865, rig.P2.CurrentHealth);
            Assert.AreEqual(FighterState.KnockDown, rig.P2.State);
            Assert.IsTrue(rig.P2.IsInvulnerable);

            // Advance hitstop (8) + knockdown (45) -> transitions to GetUp with invincibility
            rig.Sim.StepSimulationTicks(8 + FighterController.KnockDownDurationTicks);
            Assert.AreEqual(FighterState.GetUp, rig.P2.State);
            Assert.IsTrue(rig.P2.IsInvulnerable);
            Assert.Greater(rig.P2.WakeupInvincibilityTicks, 0);

            // Advance GetUp (40) + grace ticks (6) -> back to Idle & vulnerable
            rig.Sim.StepSimulationTicks(FighterController.GetUpDurationTicks + FighterController.WakeupInvincibilityGraceTicks);
            Assert.AreEqual(FighterState.Idle, rig.P2.State);
            Assert.IsFalse(rig.P2.IsInvulnerable);
        }

        [Test]
        public void SimultaneousHit_SameTick_TriggersTrade()
        {
            FighterRig rig = CreateRig();
            rig.P1.InputHandler.SetSimulatedInput(default, true);
            rig.P2.InputHandler.SetSimulatedInput(default, true);

            rig.P1.InputHandler.BufferButton(FighterInputButton.LightPunch);
            rig.P2.InputHandler.BufferButton(FighterInputButton.LightPunch);

            rig.Sim.StepSimulationTicks(1 + 5);
            Assert.AreEqual(950, rig.P1.CurrentHealth);
            Assert.AreEqual(950, rig.P2.CurrentHealth);
            Assert.AreEqual(FighterState.Hit, rig.P1.State);
            Assert.AreEqual(FighterState.Hit, rig.P2.State);
        }

        [Test]
        public void MatchManager_BestOfThree_KOAndTimeOver_FollowsRules()
        {
            FighterRig rig = CreateRig(withMatchManager: true);
            Assert.AreEqual(MatchPhase.RoundIntro, rig.Match.Phase);
            Assert.AreEqual(99, rig.Match.RemainingSeconds);

            // Step 90 ticks of RoundIntro -> Fighting
            rig.Sim.StepSimulationTicks(90);
            Assert.AreEqual(MatchPhase.Fighting, rig.Match.Phase);

            // 60 ticks = 1 second on timer (99 -> 98)
            rig.Sim.StepSimulationTicks(60);
            Assert.AreEqual(98, rig.Match.RemainingSeconds);

            // Round 1: Win by Time Over (deal 50 damage to P2 then advance remaining 98*60 ticks)
            rig.P1.SetSimPositionX(-0.55f);
            rig.P2.SetSimPositionX(0.55f);
            rig.P1.InputHandler.SetSimulatedInput(default, true);
            rig.P2.InputHandler.SetSimulatedInput(default, true);
            rig.P1.InputHandler.BufferButton(FighterInputButton.LightPunch);
            rig.Sim.StepSimulationTicks(1 + 5 + 4); // hit + hitstop
            Assert.AreEqual(950, rig.P2.CurrentHealth);

            rig.Sim.StepSimulationTicks(rig.Match.RoundTimerTicks);
            Assert.AreEqual(MatchPhase.RoundOver, rig.Match.Phase);
            Assert.AreEqual("TIME OVER!", rig.Match.BannerText);
            Assert.AreEqual(1, rig.Match.P1RoundsWon);
            Assert.AreEqual(0, rig.Match.P2RoundsWon);
            Assert.AreEqual(FighterState.Defeat, rig.P2.State);

            // Advance RoundOver (150) + RoundIntro (90) -> Round 2 starts cleanly
            rig.Sim.StepSimulationTicks(150 + 90);
            Assert.AreEqual(MatchPhase.Fighting, rig.Match.Phase);
            Assert.AreEqual(2, rig.Match.CurrentRound);
            Assert.AreEqual(1000, rig.P1.CurrentHealth);
            Assert.AreEqual(1000, rig.P2.CurrentHealth);
            Assert.AreEqual(-2.2f, rig.P1.SimPosition.x, 0.01f);
            Assert.AreEqual(2.2f, rig.P2.SimPosition.x, 0.01f);

            // Round 2: Win by KO using HeavyKick repeated strikes (keep attacker within reach after knockback)
            for (int cycle = 0; cycle < 10 && rig.Match.Phase == MatchPhase.Fighting; cycle++)
            {
                rig.P1.SetSimPositionX(rig.P2.SimPosition.x - 1.1f);
                rig.P1.InputHandler.BufferButton(FighterInputButton.HeavyKick);
                // Step past startup+active+recovery+hitstop+knockdown+getup+grace
                rig.Sim.StepSimulationTicks(110);
            }

            Assert.AreEqual(MatchPhase.RoundOver, rig.Match.Phase);
            Assert.AreEqual("K.O.!", rig.Match.BannerText);
            Assert.AreEqual(2, rig.Match.P1RoundsWon);
            Assert.AreEqual(0, rig.P2.CurrentHealth);

            // Finish RoundOver -> MatchOver ("LUCAS WINS!")
            rig.Sim.StepSimulationTicks(150);
            Assert.AreEqual(MatchPhase.MatchOver, rig.Match.Phase);
            Assert.AreEqual("LUCAS WINS!", rig.Match.BannerText);

            // Restart resets to Round 1, 0-0
            rig.Match.RestartMatch();
            Assert.AreEqual(MatchPhase.RoundIntro, rig.Match.Phase);
            Assert.AreEqual(1, rig.Match.CurrentRound);
            Assert.AreEqual(0, rig.Match.P1RoundsWon);
            Assert.AreEqual(0, rig.Match.P2RoundsWon);
        }
    }
}
