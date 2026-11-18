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
            public MoveData CrouchKick;
            public MoveData JumpAttack;
            public MoveData ThrowMove;
            public MoveData ExHeavyPunch;
            public MoveData ExHeavyKick;
            public MoveData Special;
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

            rig.CrouchKick = Track(ScriptableObject.CreateInstance<MoveData>());
            rig.CrouchKick.Configure("CrouchKick", AttackType.CrouchKick, 7, 4, 17, 95, 20, 13, 7, 2.8f, HitHeight.Low, true, new Vector2(0.88f, 0.36f), new Vector2(1.14f, 0.44f));

            rig.JumpAttack = Track(ScriptableObject.CreateInstance<MoveData>());
            rig.JumpAttack.Configure("JumpAttack", AttackType.JumpAttack, 4, 8, 6, 85, 18, 12, 6, 1.9f, HitHeight.Overhead, false, new Vector2(0.68f, 0.58f), new Vector2(0.98f, 0.76f), airOnlyValue: true);

            rig.ThrowMove = Track(ScriptableObject.CreateInstance<MoveData>());
            rig.ThrowMove.Configure("Throw", AttackType.Throw, 5, 3, 14, 115, 20, 10, 8, 3.5f, HitHeight.Unblockable, true, new Vector2(0.58f, 1.02f), new Vector2(0.64f, 0.56f), isThrowValue: true);

            rig.ExHeavyPunch = Track(ScriptableObject.CreateInstance<MoveData>());
            rig.ExHeavyPunch.Configure("EX Soco Forte", AttackType.ExHeavyPunch, 5, 5, 14, 145, 22, 15, 9, 3.6f, HitHeight.Mid, false, new Vector2(0.84f, 1.18f), new Vector2(1.06f, 0.54f), chip: 18, lungeSpeedValue: 2.4f);
            rig.ExHeavyPunch.superCost = FighterData.ExSuperCost;
            rig.ExHeavyPunch.isExMove = true;

            rig.ExHeavyKick = Track(ScriptableObject.CreateInstance<MoveData>());
            rig.ExHeavyKick.Configure("EX Chute Forte", AttackType.ExHeavyKick, 7, 5, 18, 175, 24, 16, 10, 4.4f, HitHeight.High, true, new Vector2(0.96f, 1.28f), new Vector2(1.24f, 0.70f), chip: 20, lungeSpeedValue: 3.0f);
            rig.ExHeavyKick.superCost = FighterData.ExSuperCost;
            rig.ExHeavyKick.isExMove = true;

            rig.Special = Track(ScriptableObject.CreateInstance<MoveData>());
            rig.Special.Configure("Golpe Especial", AttackType.Special, 8, 5, 15, 220, 30, 14, 12, 4.4f, HitHeight.Mid, true, new Vector2(0.95f, 1.15f), new Vector2(1.25f, 0.72f), chip: 30, lungeSpeedValue: 5.5f);

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
            data.crouchKick = rig.CrouchKick;
            data.jumpAttack = rig.JumpAttack;
            data.throwMove = rig.ThrowMove;
            data.exHeavyPunch = rig.ExHeavyPunch;
            data.exHeavyKick = rig.ExHeavyKick;
            data.specialMove = rig.Special;
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

        [Test]
        public void ChainCombo_LPIntoHPAndSpecial_WorksOnHitWithComboCounterAndScaling()
        {
            FighterRig rig = CreateRig();
            rig.P1.InputHandler.SetSimulatedInput(default, true);
            rig.P2.InputHandler.SetSimulatedInput(default, true);
            rig.P1.SetSuperGauge(100);

            var events = new List<CombatHitEvent>();
            rig.Sim.OnCombatHit += e => events.Add(e);

            // LP (startup 4, active 3, hitstop 4, damage 50)
            rig.P1.InputHandler.BufferButton(FighterInputButton.LightPunch);
            rig.Sim.StepSimulationTicks(1 + 5); // LP hits on tick 5; hitstop 4 ticks follows

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(1, events[0].ComboCount);
            Assert.AreEqual(50, events[0].DamageDealt);
            Assert.AreEqual(950, rig.P2.CurrentHealth);

            // Wait out LP hitstop (4 ticks) and keep P1 close so HP reaches after LP knockback
            rig.Sim.StepSimulationTicks(4);
            rig.P1.SetSimPositionX(rig.P2.SimPosition.x - 0.9f);
            rig.P1.InputHandler.BufferButton(FighterInputButton.HeavyPunch);

            // HP cancels immediately out of LP (startup 7, active 4, hitstop 7, damage 110 * 0.85 = 94)
            rig.Sim.StepSimulationTicks(1 + 8 + 7);
            Assert.AreEqual(AttackType.HeavyPunch, rig.P1.CurrentAttack);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(2, events[1].ComboCount);
            Assert.AreEqual(94, events[1].DamageDealt); // Round(110 * 0.85) = 94
            Assert.AreEqual(144, events[1].ComboDamage);

            // Cancel HP on hit into Special (damage 220 * 0.72 = 158)
            rig.P1.SetSimPositionX(rig.P2.SimPosition.x - 1.0f);
            rig.P1.InputHandler.BufferButton(FighterInputButton.Special);
            rig.Sim.StepSimulationTicks(1 + 9);
            Assert.AreEqual(AttackType.Special, rig.P1.CurrentAttack);
            Assert.AreEqual(0, rig.P1.SuperGauge);
            Assert.AreEqual(3, events.Count);
            Assert.IsTrue(events[2].IsSpecial);
            Assert.AreEqual(3, events[2].ComboCount);
            Assert.AreEqual(158, events[2].DamageDealt); // Round(220 * 0.72) = 158
            Assert.AreEqual(302, events[2].ComboDamage);
        }

        [Test]
        public void ChainCombo_RejectsInvalidRoutesAndCancelsOnWhiff()
        {
            FighterRig rig = CreateRig();
            rig.P1.InputHandler.SetSimulatedInput(default, true);
            rig.P2.InputHandler.SetSimulatedInput(default, true);
            rig.P1.SetSimPositionX(-3.2f);
            rig.P2.SetSimPositionX(3.2f);

            // Whiff LP -> cannot chain into HP without HasRegisteredHit
            rig.P1.InputHandler.BufferButton(FighterInputButton.LightPunch);
            rig.Sim.StepSimulationTicks(6);
            rig.P1.InputHandler.BufferButton(FighterInputButton.HeavyPunch);
            rig.Sim.StepSimulationTicks(3);
            Assert.AreEqual(AttackType.LightPunch, rig.P1.CurrentAttack);

            // Wait back to Idle, put in range, verify HP cannot cancel backwards into LP nor LP into LP
            rig.Sim.StepSimulationTicks(30);
            Assert.AreEqual(FighterState.Idle, rig.P1.State);
            Assert.IsFalse(PlayerInputHandler.IsChainCancelAllowed(AttackType.LightPunch, AttackType.LightPunch, allowSpecial: true));
            Assert.IsFalse(PlayerInputHandler.IsChainCancelAllowed(AttackType.HeavyPunch, AttackType.LightPunch, allowSpecial: true));
            Assert.IsFalse(PlayerInputHandler.IsChainCancelAllowed(AttackType.HeavyPunch, AttackType.Special, allowSpecial: false));
            Assert.IsTrue(PlayerInputHandler.IsChainCancelAllowed(AttackType.LightPunch, AttackType.LightKick, allowSpecial: false));
            Assert.IsTrue(PlayerInputHandler.IsChainCancelAllowed(AttackType.LightKick, AttackType.HeavyKick, allowSpecial: false));
            Assert.IsTrue(PlayerInputHandler.IsChainCancelAllowed(AttackType.HeavyKick, AttackType.Special, allowSpecial: true));
        }

        [Test]
        public void ComboScaling_FourthHitScalesToSixtyPercentMinimumFifty_AndResetsOnIdle()
        {
            Assert.AreEqual(1.00f, FighterController.ComputeComboScalingMultiplier(1), 0.001f);
            Assert.AreEqual(0.85f, FighterController.ComputeComboScalingMultiplier(2), 0.001f);
            Assert.AreEqual(0.72f, FighterController.ComputeComboScalingMultiplier(3), 0.001f);
            Assert.AreEqual(0.60f, FighterController.ComputeComboScalingMultiplier(4), 0.001f);
            Assert.AreEqual(0.50f, FighterController.ComputeComboScalingMultiplier(5), 0.001f);
            Assert.AreEqual(0.50f, FighterController.ComputeComboScalingMultiplier(9), 0.001f);

            FighterRig rig = CreateRig();
            rig.P1.InputHandler.SetSimulatedInput(default, true);
            rig.P2.InputHandler.SetSimulatedInput(default, true);

            rig.P1.InputHandler.BufferButton(FighterInputButton.LightPunch);
            rig.Sim.StepSimulationTicks(1 + 5);
            Assert.AreEqual(1, rig.P2.ReceivedComboHits);
            Assert.AreEqual(50, rig.P2.ReceivedComboDamage);

            // Step until P2 returns to Idle -> combo tracker resets
            rig.Sim.StepSimulationTicks(35);
            Assert.AreEqual(FighterState.Idle, rig.P2.State);
            Assert.AreEqual(0, rig.P2.ReceivedComboHits);
            Assert.AreEqual(0, rig.P2.ReceivedComboDamage);
        }

        [Test]
        public void SuperGauge_AccumulatesAcrossRounds_ResetsOnRestart_AndAutoRefillsInTraining()
        {
            FighterRig rig = CreateRig(withMatchManager: true);
            rig.Sim.StepSimulationTicks(90); // RoundIntro -> Fighting
            rig.P1.InputHandler.SetSimulatedInput(default, true);
            rig.P2.InputHandler.SetSimulatedInput(default, true);

            // Whiff at far range: +2 gauge
            rig.P1.SetSimPositionX(-3.0f);
            rig.P2.SetSimPositionX(3.0f);
            rig.P1.InputHandler.BufferButton(FighterInputButton.LightPunch);
            rig.Sim.StepSimulationTicks(2);
            Assert.AreEqual(2, rig.P1.SuperGauge);

            rig.Sim.StepSimulationTicks(25);
            // Hit at close range: LP grants +14 attacker total (+7 defender)
            rig.P1.SetSimPositionX(-0.55f);
            rig.P2.SetSimPositionX(0.55f);
            rig.P1.InputHandler.BufferButton(FighterInputButton.LightPunch);
            rig.Sim.StepSimulationTicks(1 + 5);
            Assert.AreEqual(16, rig.P1.SuperGauge);
            Assert.AreEqual(7, rig.P2.SuperGauge);

            // Block grants +6 attacker total, +9 defender
            rig.Sim.StepSimulationTicks(25);
            rig.P1.SetSimPositionX(-0.55f);
            rig.P2.SetSimPositionX(0.55f);
            rig.P2.InputHandler.SetSimulatedInput(new FighterInputState { MoveX = 1f }, true);
            rig.P1.InputHandler.BufferButton(FighterInputButton.LightPunch);
            rig.Sim.StepSimulationTicks(1 + 5);
            Assert.AreEqual(22, rig.P1.SuperGauge);
            Assert.AreEqual(16, rig.P2.SuperGauge);

            // Time over to start Round 2 -> SuperGauge must persist across rounds
            int p1GaugeAfterRound1 = rig.P1.SuperGauge;
            rig.Sim.StepSimulationTicks(rig.Match.RoundTimerTicks + 150 + 90);
            Assert.AreEqual(2, rig.Match.CurrentRound);
            Assert.AreEqual(p1GaugeAfterRound1, rig.P1.SuperGauge);

            // RestartMatch resets SuperGauge to 0 in standard Versus
            rig.Match.RestartMatch();
            Assert.AreEqual(0, rig.P1.SuperGauge);
            Assert.AreEqual(0, rig.P2.SuperGauge);

            // Training mode starts with 100 and refills after Special ends
            GameSessionState.CurrentMode = GameMode.Training;
            rig.Match.SetTrainingMode(true);
            rig.Match.RestartMatch();
            Assert.AreEqual(100, rig.P1.SuperGauge);
            rig.Sim.StepSimulationTicks(90);
            rig.P1.SetSimPositionX(-0.55f);
            rig.P2.SetSimPositionX(0.55f);
            rig.P1.InputHandler.BufferButton(FighterInputButton.LightPunch);
            rig.P1.InputHandler.BufferButton(FighterInputButton.HeavyPunch); // Simultaneous LP+HP triggers Special
            rig.Sim.StepSimulationTicks(2);
            Assert.AreEqual(AttackType.Special, rig.P1.CurrentAttack);
            Assert.AreEqual(0, rig.P1.SuperGauge);

            // Finish Special recovery + hitstop (28 + 12 = 40 ticks) -> Training refills back to 100
            rig.Sim.StepSimulationTicks(45);
            Assert.AreNotEqual(FighterState.Attack, rig.P1.State);
            Assert.AreEqual(100, rig.P1.SuperGauge);
        }

        [Test]
        public void VisualTurn180_FlipsFacingImmediately_TurnsSmoothlyThroughFront_AndSnapsOnResetOrAttack()
        {
            var simGo = Track(new GameObject("Test_TurnSim"));
            var sim = simGo.AddComponent<FightSimulation>();

            var lucasData = CreateFighterData("Lucas", 1.81f, 92f, new FighterRig
            {
                LightPunch = Track(ScriptableObject.CreateInstance<MoveData>()),
                HeavyPunch = Track(ScriptableObject.CreateInstance<MoveData>()),
                LightKick = Track(ScriptableObject.CreateInstance<MoveData>()),
                HeavyKick = Track(ScriptableObject.CreateInstance<MoveData>()),
                Special = Track(ScriptableObject.CreateInstance<MoveData>()),
            });
            lucasData.lightPunch.Configure("LP", AttackType.LightPunch, 4, 3, 9, 50, 12, 8, 4, 1.4f, HitHeight.Mid, false, new Vector2(0.68f, 1.18f), new Vector2(0.78f, 0.42f));

            var p1Go = Track(new GameObject("Test_TurnP1"));
            p1Go.transform.position = new Vector3(-1f, 0f, 0f);
            var p1Vis = Track(new GameObject("Visual"));
            p1Vis.transform.SetParent(p1Go.transform, false);
            p1Go.AddComponent<PlayerInputHandler>();
            var p1 = p1Go.AddComponent<FighterController>();
            p1.SetFighterData(lucasData);

            var p2Go = Track(new GameObject("Test_TurnP2"));
            p2Go.transform.position = new Vector3(1f, 0f, 0f);
            var p2Vis = Track(new GameObject("Visual"));
            p2Vis.transform.SetParent(p2Go.transform, false);
            p2Go.AddComponent<PlayerInputHandler>();
            var p2 = p2Go.AddComponent<FighterController>();
            p2.SetFighterData(lucasData);

            sim.SetParticipants(p1, p2);
            p1.InputHandler.SetSimulatedInput(default, true);
            p2.InputHandler.SetSimulatedInput(default, true);

            // Round init snapped P1 facing +1 (yaw = +90 deg)
            Assert.AreEqual(1, p1.FacingSign);
            Assert.AreEqual(90f, p1.VisualYaw, 0.01f);
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(p1Vis.transform.localEulerAngles.y, 90f)), 0.05f);

            // Side-swap: P1 crosses past P2 (to x=2.0, right of P2 at x=1.0) so UpdateFacing flips P1 to -1
            p1.SetSimPositionX(2.0f);
            sim.StepSimulationTicks(1);
            Assert.AreEqual(-1, p1.FacingSign, "Logical FacingSign must flip immediately with zero input delay");
            Assert.IsTrue(p1.IsTurnAnimActive, "Turn180 animation flag should activate while grounded & Idle");

            // At tick 4 of the 8-tick turn, visual yaw must pass through ~180 deg (chest/face toward camera), never 0 deg (back)
            sim.StepSimulationTicks(4);
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(p1.VisualYaw, 180f)), 10f, "Mid-turn visual yaw should cross 180 deg (facing camera)");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(p1Vis.transform.localEulerAngles.y, 180f)), 10f);
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(p1Vis.transform.localEulerAngles.y, 0f)), 165f);

            // After full 8+ ticks, visual yaw reaches 270 deg (-90 deg) and turn ends
            sim.StepSimulationTicks(6);
            Assert.AreEqual(270f, p1.VisualYaw, 0.01f);
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(p1Vis.transform.localEulerAngles.y, 270f)), 0.05f);
            Assert.IsFalse(p1.IsTurnAnimActive);

            // Attack during an active turn must snap yaw immediately without delaying attack startup
            p1.SetSimPositionX(0.0f);
            sim.StepSimulationTicks(2);
            Assert.AreEqual(1, p1.FacingSign);
            Assert.Greater(p1.VisualYaw, 95f);
            p1.InputHandler.BufferButton(FighterInputButton.LightPunch);
            sim.StepSimulationTicks(1);
            Assert.AreEqual(FighterState.Attack, p1.State);
            Assert.AreEqual(90f, p1.VisualYaw, 0.01f);
            Assert.IsFalse(p1.IsTurnAnimActive);

            // ResetForRound must snap immediately without triggering turn animation
            p1.ResetForRound(-2.2f, 1);
            Assert.AreEqual(90f, p1.VisualYaw, 0.01f);
            Assert.IsFalse(p1.IsTurnAnimActive);
        }

        [Test]
        public void DashForward_MovesQuickly_AndCanCancelIntoAttackAfterTick8()
        {
            FighterRig rig = CreateRig();
            rig.P1.SetSimPositionX(-2.5f);
            rig.P2.SetSimPositionX(2.5f);
            rig.Sim.ForceUpdateFacing();
            rig.P1.InputHandler.SetSimulatedInput(default, true);
            rig.P2.InputHandler.SetSimulatedInput(default, true);

            // Double-tap forward (+1 -> 0 -> +1) triggers DashForward
            float startX = rig.P1.SimPosition.x;
            rig.P1.InputHandler.SetSimulatedInput(new FighterInputState { MoveX = 1f }, true);
            rig.Sim.StepSimulationTicks(1);
            rig.P1.InputHandler.SetSimulatedInput(new FighterInputState { MoveX = 0f }, true);
            rig.Sim.StepSimulationTicks(1);
            rig.P1.InputHandler.SetSimulatedInput(new FighterInputState { MoveX = 1f }, true);
            rig.Sim.StepSimulationTicks(1);

            Assert.AreEqual(FighterState.DashForward, rig.P1.State);
            rig.P1.InputHandler.SetSimulatedInput(default, true);

            // Tick 5 of dash: cannot cancel into attack yet (_stateTicks < 8)
            rig.Sim.StepSimulationTicks(5);
            Assert.AreEqual(FighterState.DashForward, rig.P1.State);
            Assert.AreEqual(5, rig.P1.StateTicks);
            rig.P1.InputHandler.BufferButton(FighterInputButton.LightPunch);
            rig.Sim.StepSimulationTicks(1);
            Assert.AreEqual(FighterState.DashForward, rig.P1.State, "DashForward must not cancel into attack before tick 8.");

            // Reach tick 8 -> buffered LightPunch cancels DashForward into Attack
            rig.Sim.StepSimulationTicks(2);
            Assert.AreEqual(FighterState.Attack, rig.P1.State);
            Assert.AreEqual(AttackType.LightPunch, rig.P1.CurrentAttack);
            Assert.Greater(rig.P1.SimPosition.x - startX, 0.8f);
        }

        [Test]
        public void DashBackward_GrantsInvulnerabilityDuringFirst5Ticks_ThenRecovers()
        {
            FighterRig rig = CreateRig();
            rig.P1.SetSimPositionX(-0.6f);
            rig.P2.SetSimPositionX(0.6f);
            rig.Sim.ForceUpdateFacing();
            rig.P1.InputHandler.SetSimulatedInput(default, true);
            rig.P2.InputHandler.SetSimulatedInput(default, true);

            // P2 faces -1, so +1 is backward dash
            float p2StartX = rig.P2.SimPosition.x;
            rig.P2.InputHandler.BufferDashDirection(1, rig.Sim.CurrentTick + 1);
            rig.Sim.StepSimulationTicks(1);

            Assert.AreEqual(FighterState.DashBackward, rig.P2.State);
            Assert.IsTrue(rig.P2.IsDashInvulnerable);
            Assert.IsTrue(rig.P2.IsInvulnerable);

            // Keep P1 overlapping during first 5 ticks of backdash -> attack must whiff cleanly
            rig.P1.InputHandler.BufferButton(FighterInputButton.LightPunch);
            rig.Sim.StepSimulationTicks(5); // StateTicks == 5 (still invulnerable)
            Assert.IsTrue(rig.P2.IsDashInvulnerable);
            Assert.AreEqual(1000, rig.P2.CurrentHealth);

            // Tick 6 (_stateTicks == 6): invulnerability expires
            rig.Sim.StepSimulationTicks(1);
            Assert.IsFalse(rig.P2.IsDashInvulnerable);
            Assert.IsFalse(rig.P2.IsInvulnerable);

            // Finish 16-tick backdash -> returns cleanly to Idle further back
            rig.Sim.StepSimulationTicks(12);
            Assert.AreEqual(FighterState.Idle, rig.P2.State);
            Assert.Greater(rig.P2.SimPosition.x, p2StartX + 0.9f);
        }

        [Test]
        public void CrouchKick_HitsLow_RequiresCrouchGuard_AndCausesKnockdown()
        {
            // 1. Standing block fails against CrouchKick (Low) and results in KnockDown
            FighterRig rigStand = CreateRig();
            rigStand.P1.SetSimPositionX(-0.65f);
            rigStand.P2.SetSimPositionX(0.65f);
            rigStand.Sim.ForceUpdateFacing();
            rigStand.P1.InputHandler.SetSimulatedInput(new FighterInputState { MoveY = -1f }, true);
            rigStand.P2.InputHandler.SetSimulatedInput(new FighterInputState { HoldBlock = true }, true);

            rigStand.P1.InputHandler.BufferButton(FighterInputButton.HeavyKick);
            rigStand.Sim.StepSimulationTicks(1);
            Assert.AreEqual(FighterState.Attack, rigStand.P1.State);
            Assert.AreEqual(AttackType.CrouchKick, rigStand.P1.CurrentAttack);
            Assert.IsTrue(rigStand.P1.IsCrouching, "CrouchKick must preserve low crouching hurtbox.");

            rigStand.Sim.StepSimulationTicks(8); // Startup 7 + active tick 8
            Assert.AreEqual(905, rigStand.P2.CurrentHealth);
            Assert.AreEqual(FighterState.KnockDown, rigStand.P2.State);

            // 2. Crouch guard blocks CrouchKick cleanly
            FighterRig rigCrouchBlock = CreateRig();
            rigCrouchBlock.P1.SetSimPositionX(-0.65f);
            rigCrouchBlock.P2.SetSimPositionX(0.65f);
            rigCrouchBlock.Sim.ForceUpdateFacing();
            rigCrouchBlock.P1.InputHandler.SetSimulatedInput(new FighterInputState { MoveY = -1f }, true);
            rigCrouchBlock.P2.InputHandler.SetSimulatedInput(new FighterInputState { MoveY = -1f, HoldBlock = true }, true);

            rigCrouchBlock.P1.InputHandler.BufferButton(FighterInputButton.HeavyKick);
            rigCrouchBlock.Sim.StepSimulationTicks(1 + 8);
            Assert.AreEqual(1000, rigCrouchBlock.P2.CurrentHealth);
            Assert.AreEqual(FighterState.Block, rigCrouchBlock.P2.State);
        }

        [Test]
        public void JumpAttack_HitsOverhead_BreaksCrouchGuard_AndIsBlockedStanding()
        {
            // 1. Crouch guard fails against Overhead JumpAttack
            FighterRig rigVsCrouch = CreateRig();
            rigVsCrouch.P1.SetSimPositionX(-2.0f);
            rigVsCrouch.P2.SetSimPositionX(0.55f);
            rigVsCrouch.Sim.ForceUpdateFacing();
            rigVsCrouch.P1.InputHandler.SetSimulatedInput(new FighterInputState { MoveX = 1f, MoveY = 1f }, true);
            rigVsCrouch.P2.InputHandler.SetSimulatedInput(new FighterInputState { MoveY = -1f, HoldBlock = true }, true);

            // Jump across toward P2 and press JumpAttack on descent (tick 28 of jump arc)
            rigVsCrouch.Sim.StepSimulationTicks(28);
            Assert.IsFalse(rigVsCrouch.P1.IsGrounded);
            rigVsCrouch.P1.InputHandler.SetSimulatedInput(default, true);
            rigVsCrouch.P1.InputHandler.BufferButton(FighterInputButton.HeavyKick);
            rigVsCrouch.Sim.StepSimulationTicks(1);
            Assert.AreEqual(FighterState.Attack, rigVsCrouch.P1.State);
            Assert.AreEqual(AttackType.JumpAttack, rigVsCrouch.P1.CurrentAttack);
            Assert.Greater(rigVsCrouch.P1.SimVelocity.x, 0.5f, "JumpAttack must preserve horizontal jump velocity.");

            // Wait through descent until JumpAttack connects overhead against crouching defender
            for (int t = 0; t < 24 && rigVsCrouch.P2.CurrentHealth == 1000; t++)
            {
                rigVsCrouch.Sim.StepSimulationTicks(1);
            }
            Assert.AreEqual(915, rigVsCrouch.P2.CurrentHealth);
            Assert.AreEqual(FighterState.Hit, rigVsCrouch.P2.State);

            // 2. Standing guard blocks Overhead JumpAttack
            FighterRig rigVsStand = CreateRig();
            rigVsStand.P1.SetSimPositionX(-2.0f);
            rigVsStand.P2.SetSimPositionX(0.55f);
            rigVsStand.Sim.ForceUpdateFacing();
            rigVsStand.P1.InputHandler.SetSimulatedInput(new FighterInputState { MoveX = 1f, MoveY = 1f }, true);
            rigVsStand.P2.InputHandler.SetSimulatedInput(new FighterInputState { HoldBlock = true }, true);

            rigVsStand.Sim.StepSimulationTicks(28);
            rigVsStand.P1.InputHandler.SetSimulatedInput(default, true);
            rigVsStand.P1.InputHandler.BufferButton(FighterInputButton.HeavyKick);

            for (int t = 0; t < 24 && rigVsStand.P2.State != FighterState.Block; t++)
            {
                rigVsStand.Sim.StepSimulationTicks(1);
            }
            Assert.AreEqual(1000, rigVsStand.P2.CurrentHealth);
            Assert.AreEqual(FighterState.Block, rigVsStand.P2.State);
        }

        [Test]
        public void Throw_BeatsPassiveGuard_WhiffsOnAirborneOrHitstun_AndCausesKnockdown()
        {
            // 1. Throw beats passive guard (HoldBlock) and causes KnockDown
            FighterRig rig = CreateRig();
            rig.P1.SetSimPositionX(-0.45f);
            rig.P2.SetSimPositionX(0.45f);
            rig.Sim.ForceUpdateFacing();
            rig.P1.InputHandler.SetSimulatedInput(default, true);
            rig.P2.InputHandler.SetSimulatedInput(new FighterInputState { HoldBlock = true }, true);

            rig.P1.InputHandler.BufferButton(FighterInputButton.LightPunch);
            rig.P1.InputHandler.BufferButton(FighterInputButton.LightKick);
            rig.Sim.StepSimulationTicks(1);
            Assert.AreEqual(FighterState.Attack, rig.P1.State);
            Assert.AreEqual(AttackType.Throw, rig.P1.CurrentAttack);

            rig.Sim.StepSimulationTicks(6); // Startup 5 + active tick 6
            Assert.AreEqual(885, rig.P2.CurrentHealth);
            Assert.AreEqual(FighterState.KnockDown, rig.P2.State);

            // 2. Throw whiffs on airborne defender
            FighterRig rigAir = CreateRig();
            rigAir.P1.SetSimPositionX(-0.45f);
            rigAir.P2.SetSimPositionX(0.45f);
            rigAir.Sim.ForceUpdateFacing();
            rigAir.P1.InputHandler.SetSimulatedInput(default, true);
            rigAir.P2.InputHandler.SetSimulatedInput(new FighterInputState { MoveY = 1f }, true);
            rigAir.Sim.StepSimulationTicks(2);
            Assert.IsFalse(rigAir.P2.IsGrounded);
            rigAir.P1.InputHandler.BufferThrowInput(rigAir.Sim.CurrentTick);
            rigAir.Sim.StepSimulationTicks(8);
            Assert.AreEqual(1000, rigAir.P2.CurrentHealth);

            // 3. Throw whiffs on defender already in Hitstun
            FighterRig rigHitstun = CreateRig();
            rigHitstun.P1.SetSimPositionX(-0.45f);
            rigHitstun.P2.SetSimPositionX(0.45f);
            rigHitstun.Sim.ForceUpdateFacing();
            rigHitstun.P1.InputHandler.SetSimulatedInput(default, true);
            rigHitstun.P2.InputHandler.SetSimulatedInput(default, true);
            rigHitstun.P2.ApplyIncomingAttack(rigHitstun.LightPunch, 1, out _, out _, out _);
            Assert.AreEqual(FighterState.Hit, rigHitstun.P2.State);

            rigHitstun.P1.InputHandler.BufferThrowInput(rigHitstun.Sim.CurrentTick);
            rigHitstun.Sim.StepSimulationTicks(8);
            Assert.AreEqual(950, rigHitstun.P2.CurrentHealth, "Throw must whiff on defender in Hitstun.");
        }

        [Test]
        public void ThrowTech_CancelsThrowDamage_AndPushesBothFightersApart()
        {
            FighterRig rig = CreateRig();
            rig.P1.SetSimPositionX(-0.45f);
            rig.P2.SetSimPositionX(0.45f);
            rig.Sim.ForceUpdateFacing();
            rig.P1.InputHandler.SetSimulatedInput(default, true);
            rig.P2.InputHandler.SetSimulatedInput(default, true);

            var events = new List<CombatHitEvent>();
            rig.Sim.OnCombatHit += e => events.Add(e);

            float p1StartX = rig.P1.SimPosition.x;
            float p2StartX = rig.P2.SimPosition.x;

            // P1 attempts Throw on tick 1; P2 techs with LP+LK within window
            rig.P1.InputHandler.BufferThrowInput(rig.Sim.CurrentTick + 1);
            rig.Sim.StepSimulationTicks(3);
            rig.P2.InputHandler.BufferThrowInput(rig.Sim.CurrentTick);
            rig.Sim.StepSimulationTicks(4); // Reaches P1 Throw active tick

            Assert.AreEqual(1000, rig.P1.CurrentHealth);
            Assert.AreEqual(1000, rig.P2.CurrentHealth);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(HitOutcomeType.ThrowTech, events[0].Outcome);
            Assert.AreEqual(0, events[0].DamageDealt);
            Assert.AreEqual(6, rig.Sim.HitstopTicksRemaining);
            Assert.AreEqual(FighterState.Hit, rig.P1.State);
            Assert.AreEqual(FighterState.Hit, rig.P2.State);

            // Step through hitstop (6) + tech separation (10) -> both pushed apart and back to Idle
            rig.Sim.StepSimulationTicks(6 + 10);
            Assert.AreEqual(FighterState.Idle, rig.P1.State);
            Assert.AreEqual(FighterState.Idle, rig.P2.State);
            Assert.Less(rig.P1.SimPosition.x, p1StartX - 0.1f);
            Assert.Greater(rig.P2.SimPosition.x, p2StartX + 0.1f);
        }

        [Test]
        public void ExHeavyPunch_Consumes50SuperGauge_AndDealsEnhancedDamage()
        {
            FighterRig rig = CreateRig();
            rig.P1.SetSimPositionX(-0.60f);
            rig.P2.SetSimPositionX(0.60f);
            rig.Sim.ForceUpdateFacing();
            rig.P1.InputHandler.SetSimulatedInput(default, true);
            rig.P2.InputHandler.SetSimulatedInput(default, true);
            rig.P1.SetSuperGauge(75);

            var events = new List<CombatHitEvent>();
            rig.Sim.OnCombatHit += e => events.Add(e);

            // Press HeavyPunch + Special simultaneously with 75 gauge -> spends 50 (leaving 25) and deals 145 dmg
            rig.P1.InputHandler.BufferExInput(AttackType.ExHeavyPunch, rig.Sim.CurrentTick + 1);
            rig.Sim.StepSimulationTicks(1);
            Assert.AreEqual(FighterState.Attack, rig.P1.State);
            Assert.AreEqual(AttackType.ExHeavyPunch, rig.P1.CurrentAttack);
            Assert.AreEqual(25, rig.P1.SuperGauge);

            rig.Sim.StepSimulationTicks(6); // Startup 5 + active tick 6
            Assert.AreEqual(855, rig.P2.CurrentHealth);
            Assert.AreEqual(1, events.Count);
            Assert.IsTrue(events[0].IsExMove);
            Assert.AreEqual(145, events[0].DamageDealt);
            Assert.AreEqual(25, rig.P1.SuperGauge, "EX moves must not gain meter back on hit.");
        }

        [Test]
        public void LucasSpecial_SuperArmor_AbsorbsNormalStrikeWithoutInterruptingAttack()
        {
            FighterRig rig = CreateRig();
            rig.Special.Configure("Martelo do Asfalto", AttackType.Special, 10, 5, 17, 250, 24, 16, 14, 5.4f, HitHeight.Mid, true, new Vector2(0.92f, 1.16f), new Vector2(1.18f, 0.72f), chip: 35, lungeSpeedValue: 1.8f);
            rig.Special.specialMechanic = SpecialMechanicType.ArmoredSmash;
            rig.Special.superCost = 100;
            rig.Special.armorStartTick = 2;
            rig.Special.armorEndTick = 14;
            rig.Special.superFreezeTicks = 0;

            rig.P1.SetSimPositionX(-0.55f);
            rig.P2.SetSimPositionX(0.55f);
            rig.Sim.ForceUpdateFacing();
            rig.P1.InputHandler.SetSimulatedInput(default, true);
            rig.P2.InputHandler.SetSimulatedInput(default, true);
            rig.P1.SetSuperGauge(100);

            var events = new List<CombatHitEvent>();
            rig.Sim.OnCombatHit += e => events.Add(e);

            // P1 starts ArmoredSmash; P2 starts LightPunch (startup 4, active 5)
            rig.P1.InputHandler.BufferButton(FighterInputButton.Special);
            rig.Sim.StepSimulationTicks(1);
            Assert.AreEqual(AttackType.Special, rig.P1.CurrentAttack);

            rig.P2.InputHandler.BufferButton(FighterInputButton.LightPunch);
            rig.Sim.StepSimulationTicks(6); // P2's LP hits P1 during P1's armor window!

            // P1 absorbs LP at 60% damage (50 * 0.6 = 30), stays in Special Attack state, and counter-hits P2!
            Assert.AreEqual(970, rig.P1.CurrentHealth);
            Assert.AreEqual(FighterState.Attack, rig.P1.State, "Super Armor must prevent hitstun interruption.");
            Assert.AreEqual(AttackType.Special, rig.P1.CurrentAttack);
            Assert.IsTrue(events.Exists(e => e.WasArmorAbsorbed && e.Defender == rig.P1));

            // Finish hitstop + Lucas Special active frame -> P2 gets crushed by Martelo do Asfalto
            rig.Sim.StepSimulationTicks(12);
            Assert.AreEqual(750, rig.P2.CurrentHealth);
            Assert.AreEqual(FighterState.KnockDown, rig.P2.State);
        }

        [Test]
        public void TiagoSpecial_MultiHitRush_RegistersTwoDistinctHitsAndComboCount()
        {
            FighterRig rig = CreateRig();
            rig.Special.Configure("Sequência de Rua", AttackType.Special, 7, 3, 18, 105, 22, 15, 6, 1.4f, HitHeight.Mid, true, new Vector2(0.88f, 1.18f), new Vector2(1.16f, 0.66f), chip: 30, lungeSpeedValue: 3.8f);
            rig.Special.specialMechanic = SpecialMechanicType.MultiHitRush;
            rig.Special.superCost = 100;
            rig.Special.secondActiveStartTick = 12;
            rig.Special.secondActiveEndTick = 14;
            rig.Special.secondHitDamage = 145;
            rig.Special.superFreezeTicks = 0;

            rig.P1.SetSimPositionX(-0.55f);
            rig.P2.SetSimPositionX(0.55f);
            rig.Sim.ForceUpdateFacing();
            rig.P1.InputHandler.SetSimulatedInput(default, true);
            rig.P2.InputHandler.SetSimulatedInput(default, true);
            rig.P1.SetSuperGauge(100);

            var events = new List<CombatHitEvent>();
            rig.Sim.OnCombatHit += e => events.Add(e);

            rig.P1.InputHandler.BufferButton(FighterInputButton.Special);
            // Step through startup (7) + 1st hit + hitstop (6) + 2nd window (ticks 9..12)
            rig.Sim.StepSimulationTicks(1 + 8 + 6 + 6);

            Assert.AreEqual(2, events.Count, "MultiHitRush must register 2 distinct hits.");
            Assert.AreEqual(1, events[0].ComboCount);
            Assert.AreEqual(105, events[0].DamageDealt);
            Assert.AreEqual(HitOutcomeType.Hit, events[0].Outcome);

            Assert.AreEqual(2, events[1].ComboCount);
            Assert.AreEqual(Mathf.RoundToInt(145 * 0.85f), events[1].DamageDealt);
            Assert.AreEqual(HitOutcomeType.KnockDown, events[1].Outcome);
        }

        [Test]
        public void JompesSpecial_SpawnsDeterministicProjectile_ThatHitsAndClashesAt60Hz()
        {
            // 1. Projectile spawns at tick 8 and hits distant opponent deterministically
            FighterRig rigHit = CreateRig();
            rigHit.Special.Configure("Golpe Sombrio", AttackType.Special, 7, 4, 13, 210, 22, 14, 10, 4.4f, HitHeight.Mid, true, new Vector2(0.90f, 1.14f), new Vector2(1.14f, 0.58f), chip: 28, lungeSpeedValue: 0f);
            rigHit.Special.specialMechanic = SpecialMechanicType.ProjectileShot;
            rigHit.Special.superCost = 100;
            rigHit.Special.spawnsProjectile = true;
            rigHit.Special.projectileSpeed = 10.5f;
            rigHit.Special.projectileLifetimeTicks = 45;
            rigHit.Special.projectileSpawnOffset = new Vector2(0.70f, 1.15f);
            rigHit.Special.projectileHitboxSize = new Vector2(0.58f, 0.48f);
            rigHit.Special.superFreezeTicks = 0;

            rigHit.P1.SetSimPositionX(-2.2f);
            rigHit.P2.SetSimPositionX(2.2f);
            rigHit.Sim.ForceUpdateFacing();
            rigHit.P1.InputHandler.SetSimulatedInput(default, true);
            rigHit.P2.InputHandler.SetSimulatedInput(default, true);
            rigHit.P1.SetSuperGauge(100);

            rigHit.P1.InputHandler.BufferButton(FighterInputButton.Special);
            rigHit.Sim.StepSimulationTicks(9); // Consume (1) + 8 ticks -> spawns projectile
            Assert.AreEqual(1, rigHit.Sim.ActiveProjectiles.Count);

            // Advance until projectile crosses arena and hits P2
            rigHit.Sim.StepSimulationTicks(30);
            Assert.AreEqual(0, rigHit.Sim.ActiveProjectiles.Count);
            Assert.AreEqual(790, rigHit.P2.CurrentHealth);
            Assert.AreEqual(FighterState.KnockDown, rigHit.P2.State);

            // 2. Projectile vs Projectile clash cancels both cleanly
            FighterRig rigClash = CreateRig();
            rigClash.Special.Configure("Golpe Sombrio", AttackType.Special, 7, 4, 13, 210, 22, 14, 10, 4.4f, HitHeight.Mid, true, new Vector2(0.90f, 1.14f), new Vector2(1.14f, 0.58f), chip: 28, lungeSpeedValue: 0f);
            rigClash.Special.specialMechanic = SpecialMechanicType.ProjectileShot;
            rigClash.Special.superCost = 100;
            rigClash.Special.spawnsProjectile = true;
            rigClash.Special.projectileSpeed = 10.5f;
            rigClash.Special.projectileLifetimeTicks = 45;
            rigClash.Special.projectileSpawnOffset = new Vector2(0.70f, 1.15f);
            rigClash.Special.projectileHitboxSize = new Vector2(0.58f, 0.48f);
            rigClash.Special.superFreezeTicks = 0;

            rigClash.P1.SetSimPositionX(-3.0f);
            rigClash.P2.SetSimPositionX(3.0f);
            rigClash.Sim.ForceUpdateFacing();
            rigClash.P1.InputHandler.SetSimulatedInput(default, true);
            rigClash.P2.InputHandler.SetSimulatedInput(default, true);
            rigClash.P1.SetSuperGauge(100);
            rigClash.P2.SetSuperGauge(100);

            int clashCount = 0;
            rigClash.Sim.OnProjectileClashed += _ => clashCount++;

            rigClash.P1.InputHandler.BufferButton(FighterInputButton.Special);
            rigClash.P2.InputHandler.BufferButton(FighterInputButton.Special);
            rigClash.Sim.StepSimulationTicks(30);

            Assert.AreEqual(1, clashCount);
            Assert.AreEqual(0, rigClash.Sim.ActiveProjectiles.Count);
            Assert.AreEqual(1000, rigClash.P1.CurrentHealth);
            Assert.AreEqual(1000, rigClash.P2.CurrentHealth);
        }

        [Test]
        public void RochardSpecial_AntiAirInvulnerability_EvadesJumpAttackAndPunishes()
        {
            FighterRig rig = CreateRig();
            rig.Special.Configure("Tornado Relâmpago", AttackType.Special, 6, 4, 12, 225, 22, 14, 10, 4.4f, HitHeight.High, true, new Vector2(0.86f, 1.32f), new Vector2(1.26f, 1.05f), chip: 26, lungeSpeedValue: 4.6f);
            rig.Special.specialMechanic = SpecialMechanicType.AntiAirSpin;
            rig.Special.superCost = 100;
            rig.Special.antiAirInvulnerable = true;
            rig.Special.antiAirStartTick = 1;
            rig.Special.antiAirEndTick = 12;
            rig.Special.superFreezeTicks = 0;

            rig.P1.SetSimPositionX(-2.3f);
            rig.P2.SetSimPositionX(0.55f);
            rig.Sim.ForceUpdateFacing();
            rig.P1.InputHandler.SetSimulatedInput(new FighterInputState { MoveX = 1f, MoveY = 1f }, true);
            rig.P2.InputHandler.SetSimulatedInput(default, true);
            rig.P2.SetSuperGauge(100);

            // P1 jumps in and starts JumpAttack on descent
            rig.Sim.StepSimulationTicks(27);
            rig.P1.InputHandler.SetSimulatedInput(default, true);
            rig.P1.InputHandler.BufferButton(FighterInputButton.HeavyKick);
            rig.Sim.StepSimulationTicks(4);

            // P2 counters with AntiAirSpin Special while P1 is airborne attacking
            rig.P2.InputHandler.BufferButton(FighterInputButton.Special);
            rig.Sim.StepSimulationTicks(12);

            Assert.AreEqual(1000, rig.P2.CurrentHealth, "Anti-air invulnerability must evade incoming JumpAttack.");
            Assert.AreEqual(775, rig.P1.CurrentHealth, "Rochard AntiAirSpin must punish airborne attacker.");
            Assert.AreEqual(FighterState.KnockDown, rig.P1.State);
        }

        [Test]
        public void SuperFlash_FreezesSimulationBriefly_AndTriggersCameraZoom()
        {
            FighterRig rig = CreateRig();
            rig.Special.Configure("Martelo do Asfalto", AttackType.Special, 10, 5, 17, 250, 24, 16, 14, 5.4f, HitHeight.Mid, true, new Vector2(0.92f, 1.16f), new Vector2(1.18f, 0.72f), chip: 35, lungeSpeedValue: 1.8f);
            rig.Special.superCost = 100;
            rig.Special.superFreezeTicks = 14;

            var camGo = Track(new GameObject("Test_SuperCam"));
            var cam = camGo.AddComponent<FightCameraController>();
            var mid = Track(new GameObject("CamMid")).transform;
            cam.ConfigureTargets(rig.P1.transform, rig.P2.transform, mid);
            rig.Sim.SetParticipants(rig.P1, rig.P2, cam);

            rig.P1.SetSimPositionX(-1.2f);
            rig.P2.SetSimPositionX(1.2f);
            rig.P1.InputHandler.SetSimulatedInput(default, true);
            rig.P2.InputHandler.SetSimulatedInput(default, true);
            rig.P1.SetSuperGauge(100);

            int flashEvents = 0;
            rig.Sim.OnSuperFlashTriggered += (caster, ticks) => { if (caster == rig.P1 && ticks == 14) flashEvents++; };

            float initialCamDist = cam.CurrentDistance;
            rig.P1.InputHandler.BufferButton(FighterInputButton.Special);
            rig.Sim.StepSimulationTicks(1);

            Assert.AreEqual(1, flashEvents);
            Assert.AreEqual(14, rig.Sim.SuperFlashTicksRemaining);
            Assert.IsTrue(cam.IsSuperCameraActive);
            Assert.AreEqual(0, rig.P1.StateTicks, "Fighter simulation ticks must stay frozen during SuperFlash.");

            // Step 10 ticks of SuperFlash -> camera zooms in dramatically while fighter stays frozen
            rig.Sim.StepSimulationTicks(10);
            Assert.AreEqual(4, rig.Sim.SuperFlashTicksRemaining);
            Assert.AreEqual(0, rig.P1.StateTicks);
            Assert.Less(cam.CurrentDistance, initialCamDist - 1.0f);

            // Finish remaining 4 SuperFlash ticks + 1 normal tick -> simulation resumes cleanly
            rig.Sim.StepSimulationTicks(5);
            Assert.AreEqual(0, rig.Sim.SuperFlashTicksRemaining);
            Assert.IsFalse(cam.IsSuperCameraActive);
            Assert.AreEqual(1, rig.P1.StateTicks);
        }

        [Test]
        public void CrouchWalkForward_MovesAtReducedSpeed_AndKeepsCrouchedHurtbox()
        {
            FighterRig rig = CreateRig();
            rig.P1.SetSimPositionX(-2.0f);
            rig.P2.SetSimPositionX(2.0f);
            rig.Sim.ForceUpdateFacing();

            float startX = rig.P1.SimPosition.x;
            float fullHeight = rig.P1.CurrentHurtboxHeight;

            // Hold Down + Forward (crouch walk forward) for 30 ticks (0.5 s)
            rig.P1.InputHandler.SetSimulatedInput(new FighterInputState { MoveX = 1f, MoveY = -1f }, true);
            rig.P2.InputHandler.SetSimulatedInput(default, true);
            rig.Sim.StepSimulationTicks(30);

            Assert.AreEqual(FighterState.CrouchWalkForward, rig.P1.State);
            Assert.IsTrue(rig.P1.IsCrouching, "CrouchWalkForward must preserve crouched state.");
            Assert.Less(rig.P1.CurrentHurtboxHeight, fullHeight * 0.70f, "CrouchWalkForward must maintain low hurtbox.");
            float moved = rig.P1.SimPosition.x - startX;
            float expected = rig.LucasData.EffectiveCrouchWalkForwardSpeed * 0.5f;
            Assert.AreEqual(expected, moved, 0.02f);
            Assert.Less(rig.P1.SimVelocity.x, rig.LucasData.EffectiveWalkForwardSpeed, "CrouchWalkForward must be slower than standing WalkForward.");
        }

        [Test]
        public void CrouchWalkBackward_MovesBackward_KeepsLowHurtbox_AndBlocksLowAttacks()
        {
            FighterRig rig = CreateRig();
            rig.P1.SetSimPositionX(-0.65f);
            rig.P2.SetSimPositionX(0.65f);
            rig.Sim.ForceUpdateFacing();

            float p2StartX = rig.P2.SimPosition.x;

            // P2 holds Down + Right (Back relative to FacingSign=-1) while P1 steps crouched or throws Low LightKick
            rig.P1.InputHandler.SetSimulatedInput(default, true);
            rig.P2.InputHandler.SetSimulatedInput(new FighterInputState { MoveX = 1f, MoveY = -1f }, true);
            rig.Sim.StepSimulationTicks(15);

            Assert.AreEqual(FighterState.CrouchWalkBackward, rig.P2.State);
            Assert.IsTrue(rig.P2.IsCrouching);
            Assert.Greater(rig.P2.SimPosition.x, p2StartX + 0.35f, "CrouchWalkBackward must retreat backward.");

            // Re-close distance while P2 keeps walking backward crouched and tests automatic Low guard
            rig.P1.SetSimPositionX(rig.P2.SimPosition.x - 0.85f);
            rig.P1.InputHandler.BufferButton(FighterInputButton.LightKick);
            rig.Sim.StepSimulationTicks(1 + rig.LightKick.FirstActiveTick); // start + FirstActiveTick of LightKick (Low)

            Assert.AreEqual(1000, rig.P2.CurrentHealth, "CrouchWalkBackward must block Low attacks without taking full damage.");
            Assert.AreEqual(FighterState.Block, rig.P2.State);
        }

        private void SetupTestComboStrings(
            FighterRig rig,
            out MoveData comboPunchFinisher,
            out MoveData comboKickFinisher,
            out MoveData overheadFinisher)
        {
            comboPunchFinisher = Track(ScriptableObject.CreateInstance<MoveData>());
            comboPunchFinisher.Configure(
                "ComboPunchFinisher",
                AttackType.ComboPunchFinisher,
                startup: 6,
                active: 4,
                recovery: 14,
                dmg: 120,
                hitstun: 20,
                blockstun: 13,
                hitstop: 6,
                kb: 2.6f,
                height: HitHeight.Mid,
                knockdown: false,
                centerOffset: new Vector2(0.80f, 1.18f),
                size: new Vector2(1.02f, 0.52f),
                chip: 0,
                lungeSpeedValue: 2.8f);

            comboKickFinisher = Track(ScriptableObject.CreateInstance<MoveData>());
            comboKickFinisher.Configure(
                "ComboKickFinisher",
                AttackType.ComboKickFinisher,
                startup: 7,
                active: 4,
                recovery: 15,
                dmg: 135,
                hitstun: 22,
                blockstun: 14,
                hitstop: 6,
                kb: 3.2f,
                height: HitHeight.High,
                knockdown: true,
                centerOffset: new Vector2(0.88f, 1.20f),
                size: new Vector2(1.12f, 0.58f),
                chip: 0,
                lungeSpeedValue: 3.0f);

            overheadFinisher = Track(ScriptableObject.CreateInstance<MoveData>());
            overheadFinisher.Configure(
                "Quebra-Guarda",
                AttackType.ComboPunchFinisher,
                startup: 6,
                active: 4,
                recovery: 14,
                dmg: 125,
                hitstun: 22,
                blockstun: 14,
                hitstop: 6,
                kb: 2.8f,
                height: HitHeight.Overhead,
                knockdown: true,
                centerOffset: new Vector2(0.82f, 1.18f),
                size: new Vector2(1.06f, 0.54f),
                chip: 0,
                lungeSpeedValue: 3.0f);

            rig.LucasData.comboPunchFinisher = comboPunchFinisher;
            rig.LucasData.comboKickFinisher = comboKickFinisher;
            rig.LucasData.comboStrings = new[]
            {
                new ComboStringDefinition
                {
                    stringName = "QUEBRA-GUARDA",
                    allowSpecialOrExCancelOnEnd = true,
                    steps = new[]
                    {
                        new ComboStringStep { inputButton = FighterInputButton.LightPunch, requireCrouch = false, attackType = AttackType.LightPunch },
                        new ComboStringStep { inputButton = FighterInputButton.LightPunch, requireCrouch = false, attackType = AttackType.LightPunch },
                        new ComboStringStep { inputButton = FighterInputButton.HeavyPunch, requireCrouch = false, attackType = AttackType.ComboPunchFinisher, stepMoveOverride = overheadFinisher },
                    }
                },
                new ComboStringDefinition
                {
                    stringName = "GIRO DE RUA",
                    allowSpecialOrExCancelOnEnd = true,
                    steps = new[]
                    {
                        new ComboStringStep { inputButton = FighterInputButton.LightPunch, requireCrouch = false, attackType = AttackType.LightPunch },
                        new ComboStringStep { inputButton = FighterInputButton.LightKick,  requireCrouch = false, attackType = AttackType.LightKick },
                        new ComboStringStep { inputButton = FighterInputButton.HeavyKick,  requireCrouch = false, attackType = AttackType.ComboKickFinisher },
                    }
                },
                new ComboStringDefinition
                {
                    stringName = "RASTEIRA BRUTAL",
                    allowSpecialOrExCancelOnEnd = true,
                    steps = new[]
                    {
                        new ComboStringStep { inputButton = FighterInputButton.LightKick,  requireCrouch = false, attackType = AttackType.LightKick },
                        new ComboStringStep { inputButton = FighterInputButton.LightPunch, requireCrouch = false, attackType = AttackType.LightPunch },
                        new ComboStringStep { inputButton = FighterInputButton.HeavyKick,  requireCrouch = true,  attackType = AttackType.CrouchKick },
                    }
                },
                new ComboStringDefinition
                {
                    stringName = "ESMAGAMENTO",
                    allowSpecialOrExCancelOnEnd = true,
                    steps = new[]
                    {
                        new ComboStringStep { inputButton = FighterInputButton.LightPunch, requireCrouch = false, attackType = AttackType.LightPunch },
                        new ComboStringStep { inputButton = FighterInputButton.HeavyPunch, requireCrouch = false, attackType = AttackType.HeavyPunch },
                        new ComboStringStep { inputButton = FighterInputButton.HeavyPunch, requireCrouch = false, attackType = AttackType.ComboPunchFinisher },
                    }
                },
            };
        }

        [Test]
        public void ComboString_DialInput_ChainsThreeDistinctAttacksOnHit_WithComboScaling()
        {
            FighterRig rig = CreateRig();
            SetupTestComboStrings(rig, out _, out MoveData comboKickFinisher, out _);

            rig.P1.SetSimPositionX(-0.55f);
            rig.P2.SetSimPositionX(0.55f);
            rig.Sim.ForceUpdateFacing();
            rig.P1.InputHandler.SetSimulatedInput(default, true);
            rig.P2.InputHandler.SetSimulatedInput(default, true);

            var events = new List<CombatHitEvent>();
            rig.Sim.OnCombatHit += e => events.Add(e);

            string completedStringName = null;
            void HandleCompleted(FighterController attacker, string name)
            {
                if (attacker == rig.P1) completedStringName = name;
            }
            FighterController.OnComboStringCompleted += HandleCompleted;

            try
            {
                // Dial-a-Combo rapid input: LP -> LK -> HK ("GIRO DE RUA": LightPunch -> LightKick -> ComboKickFinisher)
                rig.P1.InputHandler.BufferDialComboSequence(
                    FighterInputButton.LightPunch,
                    FighterInputButton.LightKick,
                    FighterInputButton.HeavyKick);

                // Step through all 3 hits + hitstops
                rig.Sim.StepSimulationTicks(42);

                Assert.AreEqual(3, events.Count, "Dial-a-Combo string LP -> LK -> HK must land all 3 hits.");
                Assert.AreEqual(AttackType.LightPunch, events[0].Move.attackType);
                Assert.AreEqual(AttackType.LightKick, events[1].Move.attackType);
                Assert.AreEqual(AttackType.ComboKickFinisher, events[2].Move.attackType);
                Assert.AreSame(comboKickFinisher, events[2].Move);

                // Verify deterministic combo counter, scaling (100% -> 85% -> 72%), and string name
                int expectedHit1 = 50;                                      // 50 * 1.00
                int expectedHit2 = Mathf.RoundToInt(65 * 0.85f);            // 55
                int expectedHit3 = Mathf.RoundToInt(135 * 0.72f);           // 97
                Assert.AreEqual(1, events[0].ComboCount);
                Assert.AreEqual(2, events[1].ComboCount);
                Assert.AreEqual(3, events[2].ComboCount);
                Assert.AreEqual(expectedHit1, events[0].DamageDealt);
                Assert.AreEqual(expectedHit2, events[1].DamageDealt);
                Assert.AreEqual(expectedHit3, events[2].DamageDealt);
                Assert.AreEqual(expectedHit1 + expectedHit2 + expectedHit3, events[2].ComboDamage);
                Assert.AreEqual("GIRO DE RUA", events[1].ComboStringName);
                Assert.AreEqual("GIRO DE RUA", events[2].ComboStringName);
                Assert.AreEqual("GIRO DE RUA", completedStringName);
                Assert.AreEqual(FighterState.KnockDown, rig.P2.State);
            }
            finally
            {
                FighterController.OnComboStringCompleted -= HandleCompleted;
            }
        }

        [Test]
        public void ComboString_ChainsAsBlockstring_OnBlockedHit_AndMixupOverheadBreaksCrouchGuard()
        {
            FighterRig rig = CreateRig();
            SetupTestComboStrings(rig, out _, out _, out MoveData overheadFinisher);

            rig.P1.SetSimPositionX(-0.55f);
            rig.P2.SetSimPositionX(0.55f);
            rig.Sim.ForceUpdateFacing();
            rig.P1.InputHandler.SetSimulatedInput(default, true);
            // P2 holds crouch guard (Down + HoldBlock) throughout the entire string
            rig.P2.InputHandler.SetSimulatedInput(new FighterInputState { MoveY = -1f, HoldBlock = true }, true);

            var events = new List<CombatHitEvent>();
            rig.Sim.OnCombatHit += e => events.Add(e);

            // P1 dials "QUEBRA-GUARDA": LP (Mid) -> LP (Mid) -> HP (ComboPunchFinisher with Overhead stepMoveOverride)
            rig.P1.InputHandler.BufferDialComboSequence(
                FighterInputButton.LightPunch,
                FighterInputButton.LightPunch,
                FighterInputButton.HeavyPunch);

            rig.Sim.StepSimulationTicks(40);

            Assert.AreEqual(3, events.Count, "Combo string must chain on blocked hits as a true MK-style blockstring.");
            Assert.AreEqual(HitOutcomeType.Blocked, events[0].Outcome, "1st hit (Mid) must be blocked by crouch guard.");
            Assert.AreEqual(HitOutcomeType.Blocked, events[1].Outcome, "2nd hit (Mid) must chain on block and be blocked by crouch guard.");
            Assert.AreEqual("QUEBRA-GUARDA", events[1].ComboStringName);

            // 3rd hit uses Overhead stepMoveOverride, breaking P2's crouch guard and knocking down!
            Assert.AreSame(overheadFinisher, events[2].Move);
            Assert.AreEqual(HitHeight.Overhead, events[2].Move.hitHeight);
            Assert.AreEqual(HitOutcomeType.KnockDown, events[2].Outcome, "3rd hit (Overhead finisher) must break crouch guard!");
            Assert.AreEqual(125, events[2].DamageDealt);
            Assert.AreEqual("QUEBRA-GUARDA", events[2].ComboStringName);
            Assert.AreEqual(FighterState.KnockDown, rig.P2.State);
        }

        [Test]
        public void ComboString_LastHit_CanCancelIntoSpecialOrExMove()
        {
            FighterRig rig = CreateRig();
            SetupTestComboStrings(rig, out MoveData comboPunchFinisher, out _, out _);

            rig.P1.SetSimPositionX(-0.55f);
            rig.P2.SetSimPositionX(0.55f);
            rig.Sim.ForceUpdateFacing();
            rig.P1.InputHandler.SetSimulatedInput(default, true);
            rig.P2.InputHandler.SetSimulatedInput(default, true);
            rig.P1.SetSuperGauge(100);

            var events = new List<CombatHitEvent>();
            rig.Sim.OnCombatHit += e => events.Add(e);

            // Execute 3-hit string "ESMAGAMENTO": LP -> HP -> HP (ComboPunchFinisher, non-knockdown Mid)
            rig.P1.InputHandler.BufferDialComboSequence(
                FighterInputButton.LightPunch,
                FighterInputButton.HeavyPunch,
                FighterInputButton.HeavyPunch);

            // Advance until the 3rd hit (ComboPunchFinisher) connects and finishes hitstop
            for (int t = 0; t < 45 && events.Count < 3; t++)
            {
                rig.Sim.StepSimulationTicks(1);
            }
            Assert.AreEqual(3, events.Count);
            Assert.AreSame(comboPunchFinisher, events[2].Move);
            Assert.AreEqual("ESMAGAMENTO", events[2].ComboStringName);

            // Wait out 3rd hit's hitstop, then cancel ComboPunchFinisher recovery into Special (or EX)
            while (rig.Sim.HitstopTicksRemaining > 0)
            {
                rig.Sim.StepSimulationTicks(1);
            }
            rig.P1.SetSuperGauge(100);
            rig.P1.InputHandler.BufferButton(FighterInputButton.Special);
            rig.Sim.StepSimulationTicks(1 + rig.Special.FirstActiveTick);

            Assert.AreEqual(AttackType.Special, rig.P1.CurrentAttack, "Final hit of Combo String must allow canceling into Special/EX.");
            Assert.AreEqual(4, events.Count);
            Assert.IsTrue(events[3].IsSpecial);
            Assert.AreEqual(4, events[3].ComboCount);
            Assert.AreEqual(Mathf.RoundToInt(rig.Special.damage * 0.60f), events[3].DamageDealt);
        }
    }
}
