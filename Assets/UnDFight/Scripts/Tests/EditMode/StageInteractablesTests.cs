using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnDFight.Tests
{
    public class StageInteractablesTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();
        private SimulationMode _prevSimulationMode;

        [SetUp]
        public void SetUp()
        {
            _prevSimulationMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            GameSessionState.Reset();
            StagePhysicsLayers.ApplyCollisionIsolation();
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
            Physics.simulationMode = _prevSimulationMode;
            GameSessionState.Reset();
        }

        private T Track<T>(T obj) where T : Object
        {
            _createdObjects.Add(obj);
            return obj;
        }

        private sealed class DummyResettable : MonoBehaviour, IStageResettable
        {
            public int ResetCount { get; private set; }
            public void ResetProp() => ResetCount++;
        }

        private (FightSimulation sim, MatchManager match, FighterController p1, FighterController p2, StageInteractableManager stageMgr, MoveData lp, MoveData hp) CreateStageCombatRig(bool withMatchManager = true)
        {
            var lp = Track(ScriptableObject.CreateInstance<MoveData>());
            lp.Configure("LightPunch", AttackType.LightPunch, 4, 3, 9, 50, 12, 8, 4, 1.4f, HitHeight.Mid, false, new Vector2(0.68f, 1.18f), new Vector2(0.78f, 0.42f));

            var hp = Track(ScriptableObject.CreateInstance<MoveData>());
            hp.Configure("HeavyPunch", AttackType.HeavyPunch, 7, 4, 16, 110, 18, 12, 7, 2.8f, HitHeight.Mid, true, new Vector2(0.76f, 1.18f), new Vector2(0.92f, 0.46f));

            var data = Track(ScriptableObject.CreateInstance<FighterData>());
            data.fighterName = "Tester";
            data.heightMeters = 1.80f;
            data.weightKg = 80f;
            data.maxHealth = 1000;
            data.lightPunch = lp;
            data.heavyPunch = hp;

            var simGo = Track(new GameObject("Test_StageCombatSim"));
            var sim = simGo.AddComponent<FightSimulation>();
            var p1 = CreateFighter("Test_P1", data, -0.55f);
            var p2 = CreateFighter("Test_P2", data, 0.55f);
            sim.SetParticipants(p1, p2);

            MatchManager match = null;
            if (withMatchManager)
            {
                match = simGo.AddComponent<MatchManager>();
                match.InitializeMatch(sim, trainingModeOverride: false);
                match.SkipIntroToFighting();
            }

            var stageMgr = simGo.AddComponent<StageInteractableManager>();
            stageMgr.Configure(sim, match);

            return (sim, match, p1, p2, stageMgr, lp, hp);
        }

        private FighterController CreateFighter(string name, FighterData data, float startX, bool withKinematicRigidbody = true)
        {
            var go = Track(new GameObject(name));
            go.transform.position = new Vector3(startX, 0f, 0f);
            if (StagePhysicsLayers.HasDedicatedLayers)
            {
                go.layer = StagePhysicsLayers.FighterHurtboxLayer;
            }
            var col = go.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.radius = 0.36f;
            col.height = data.heightMeters;
            col.center = new Vector3(0f, data.heightMeters * 0.5f, 0f);
            if (withKinematicRigidbody)
            {
                var rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }
            go.AddComponent<PlayerInputHandler>();
            var fc = go.AddComponent<FighterController>();
            fc.SetFighterData(data);
            return fc;
        }

        private StageThrowableProp CreateTestThrowableProp(
            string name,
            Vector3 position,
            int damage = 85,
            bool knockdown = true,
            bool shatterOnFighter = true,
            bool shatterOnStage = false,
            StagePropMaterialType matType = StagePropMaterialType.Plastic)
        {
            var propGo = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            propGo.name = name;
            propGo.transform.position = position;
            propGo.transform.localScale = new Vector3(0.45f, 0.55f, 0.45f);
            var prop = propGo.AddComponent<StageThrowableProp>();
            prop.ConfigureForRuntime(damage, knockdown, shatterOnFighter, shatterOnStage, matType);
            return prop;
        }

        private StageBreakableProp CreateTestProp(string name, Vector3 position, int integrity = 2, float radiusX = 1.5f, float zTolerance = 1.85f)
        {
            var propGo = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            propGo.name = name;
            propGo.transform.position = position;
            var prop = propGo.AddComponent<StageBreakableProp>();
            prop.ConfigureForRuntime(integrity, radiusX, zTolerance, StagePropMaterialType.Plastic);
            return prop;
        }

        [Test]
        public void PhysXLayersAndMatrix_AreIsolatedFromFighters()
        {
            Assert.IsTrue(StagePhysicsLayers.HasDedicatedLayers, "Dedicated PhysX layers StagePhysics, StageDebris, FighterHurtbox must exist.");

            int stagePhysics = StagePhysicsLayers.StagePhysicsLayer;
            int stageDebris = StagePhysicsLayers.StageDebrisLayer;
            int fighterLayer = StagePhysicsLayers.FighterHurtboxLayer;
            int stageProjectile = StagePhysicsLayers.StageProjectileLayer;

            Assert.IsFalse(Physics.GetIgnoreLayerCollision(stageDebris, stagePhysics), "StageDebris must collide with StagePhysics.");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(stageDebris, fighterLayer), "StageDebris must NEVER collide with FighterHurtbox.");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(stageDebris, 0), "StageDebris must ignore Default layer.");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(fighterLayer, stagePhysics), "FighterHurtbox must ignore StagePhysics.");
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(stageProjectile, stagePhysics), "StageProjectile must collide with StagePhysics.");
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(stageProjectile, fighterLayer), "StageProjectile must trigger-detect FighterHurtbox.");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(stageProjectile, 0), "StageProjectile must ignore Default layer.");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(stageProjectile, stageDebris), "StageProjectile must ignore StageDebris.");
        }

        [Test]
        public void StageInteractableManager_AutoDiscoversPropsAndCreatesStagePhysicsBounds()
        {
            StageBreakableProp propA = CreateTestProp("Test_CrateA", new Vector3(-4.0f, 0f, 1.2f));
            StageBreakableProp propB = CreateTestProp("Test_CrateB", new Vector3(4.0f, 0f, 1.2f));

            var dummyGo = Track(new GameObject("Test_CustomResettable"));
            var dummy = dummyGo.AddComponent<DummyResettable>();

            var simGo = Track(new GameObject("Test_AutoDiscoverSim"));
            var sim = simGo.AddComponent<FightSimulation>();
            var mgr = simGo.AddComponent<StageInteractableManager>();
            mgr.Configure(sim, null);

            CollectionAssert.Contains(mgr.DiscoveredProps, propA);
            CollectionAssert.Contains(mgr.DiscoveredProps, propB);
            CollectionAssert.Contains(mgr.Resettables, dummy);

            Assert.IsNotNull(mgr.GroundPhysicsCollider);
            Assert.IsNotNull(mgr.LeftWallPhysicsCollider);
            Assert.IsNotNull(mgr.RightWallPhysicsCollider);
            Assert.AreEqual(StagePhysicsLayers.StagePhysicsLayer, mgr.GroundPhysicsCollider.gameObject.layer);

            float groundTopY = mgr.GroundPhysicsCollider.bounds.max.y;
            Assert.AreEqual(sim.GroundY, groundTopY, 1e-3f);
        }

        [Test]
        public void StageBreakableProp_BreaksOnHeavyImpact_AndActivatesStageDebrisRigidbodies()
        {
            StageBreakableProp nearProp = CreateTestProp("Test_NearCrate", new Vector3(0.4f, 0f, 1.2f), integrity: 2, radiusX: 1.5f);
            StageBreakableProp farZProp = CreateTestProp("Test_FarBackgroundCrate", new Vector3(0.4f, 0f, 3.2f), integrity: 2, radiusX: 1.5f);
            StageBreakableProp farXProp = CreateTestProp("Test_FarSideCrate", new Vector3(5.8f, 0f, 1.2f), integrity: 2, radiusX: 1.5f);

            var (sim, _, p1, p2, stageMgr, _, _) = CreateStageCombatRig(withMatchManager: true);
            p1.SetSimPositionX(-0.45f);
            p2.SetSimPositionX(0.55f);
            sim.ForceUpdateFacing();

            p1.InputHandler.SetSimulatedInput(default);
            p1.InputHandler.BufferButton(FighterInputButton.HeavyPunch);
            sim.StepSimulationTicks(12);

            Assert.IsTrue(nearProp.IsBroken, "Prop inside X radius and Z tolerance should shatter on HeavyPunch hit.");
            Assert.IsTrue(nearProp.gameObject.activeSelf, "Root prop GameObject must stay active so round reset works.");
            Assert.IsFalse(farZProp.IsBroken, "Prop outside Z tolerance must remain intact.");
            Assert.IsFalse(farXProp.IsBroken, "Prop outside X radius must remain intact.");

            Assert.GreaterOrEqual(nearProp.Fragments.Count, 5);
            Assert.LessOrEqual(nearProp.Fragments.Count, 8);

            for (int i = 0; i < nearProp.Fragments.Count; i++)
            {
                StageDebrisFragment frag = nearProp.Fragments[i];
                Assert.IsTrue(frag.gameObject.activeSelf, $"Debris fragment {i} should be active after breaking.");
                Assert.AreEqual(StagePhysicsLayers.StageDebrisLayer, frag.gameObject.layer);
                Assert.IsNotNull(frag.Body);
                Assert.IsFalse(frag.Body.isKinematic);
                Assert.IsTrue(frag.Body.useGravity);
            }
        }

        [Test]
        public void StageBreakableProp_TwoLightHitsBreakTwoIntegrityProp()
        {
            StageBreakableProp prop = CreateTestProp("Test_LightHitCrate", new Vector3(0.3f, 0f, 1.1f), integrity: 2, radiusX: 1.5f);
            var (sim, _, p1, p2, _, _, _) = CreateStageCombatRig(withMatchManager: true);
            p1.SetSimPositionX(-0.45f);
            p2.SetSimPositionX(0.55f);
            sim.ForceUpdateFacing();

            p1.InputHandler.SetSimulatedInput(default);
            p1.InputHandler.BufferButton(FighterInputButton.LightPunch);
            sim.StepSimulationTicks(25);

            Assert.IsFalse(prop.IsBroken, "A single light hit should damage but not break a 2-HP prop.");
            Assert.AreEqual(1, prop.CurrentIntegrity);

            p1.SetSimPositionX(-0.45f);
            p2.SetSimPositionX(0.55f);
            p1.InputHandler.BufferButton(FighterInputButton.LightPunch);
            sim.StepSimulationTicks(25);

            Assert.IsTrue(prop.IsBroken, "Two light hits near the prop should break a 2-HP prop.");
            Assert.AreEqual(0, prop.CurrentIntegrity);
        }

        [Test]
        public void StageBreakableProp_BreaksWhenDownedFighterSlamsGroundNearby()
        {
            StageBreakableProp prop = CreateTestProp("Test_KnockdownCrate", new Vector3(1.65f, 0f, 1.2f), integrity: 2, radiusX: 1.5f);
            var (sim, _, p1, p2, stageMgr, _, _) = CreateStageCombatRig(withMatchManager: true);
            p1.SetSimPositionX(-0.45f);
            p2.SetSimPositionX(0.55f);
            sim.ForceUpdateFacing();

            p1.InputHandler.SetSimulatedInput(default);
            p1.InputHandler.BufferButton(FighterInputButton.HeavyPunch);
            sim.StepSimulationTicks(25);

            // Pushback/knockdown slide or ground impact at StateTicks == 10 reaches nearby prop
            Assert.IsTrue(p2.State == FighterState.KnockDown || p2.State == FighterState.GetUp);

            // Now test direct ground knockdown slam right next to a second prop discovered by stageMgr
            StageBreakableProp secondProp = CreateTestProp("Test_SlamCrate", new Vector3(3.0f, 0f, 1.2f), integrity: 2, radiusX: 1.4f);
            stageMgr.RefreshDiscoveredProps();
            p2.SetSimPositionX(3.0f);
            p2.ApplyIncomingAttack(p1.Data.heavyPunch, 1, out _, out _, out _);
            sim.StepSimulationTicks(12);
            Assert.IsTrue(secondProp.IsBroken, "Fighter knockdown ground slam near a prop must break it.");
        }

        [Test]
        public void MatchManager_StartRound_ResetsBrokenPropsAndDeactivatesDebris()
        {
            StageBreakableProp prop = CreateTestProp("Test_ResetCrate", new Vector3(0.4f, 0f, 1.2f), integrity: 2, radiusX: 1.5f);
            var (sim, match, p1, p2, _, _, _) = CreateStageCombatRig(withMatchManager: true);
            p1.SetSimPositionX(-0.45f);
            p2.SetSimPositionX(0.55f);
            sim.ForceUpdateFacing();

            p1.InputHandler.SetSimulatedInput(default);
            p1.InputHandler.BufferButton(FighterInputButton.HeavyPunch);
            sim.StepSimulationTicks(12);
            Assert.IsTrue(prop.IsBroken);

            match.StartRound();

            Assert.IsFalse(prop.IsBroken, "StartRound() must restore broken stage props.");
            Assert.AreEqual(prop.MaxIntegrity, prop.CurrentIntegrity);
            for (int i = 0; i < prop.Fragments.Count; i++)
            {
                Assert.IsFalse(prop.Fragments[i].gameObject.activeSelf, "Pooled debris must be deactivated cleanly on round reset.");
            }
        }

        [Test]
        public void FightersWithKinematicRigidbody_Remain60HzDeterministicAndMatchNonRigidbodySim()
        {
            CreateTestProp("Test_DetCrate", new Vector3(0.4f, 0f, 1.2f), integrity: 2, radiusX: 1.5f);
            var (sim, _, p1, p2, _, _, _) = CreateStageCombatRig(withMatchManager: true);

            Rigidbody rb1 = p1.GetComponent<Rigidbody>();
            Rigidbody rb2 = p2.GetComponent<Rigidbody>();
            Assert.IsNotNull(rb1, "Fighters must carry a kinematic Rigidbody for safe PhysX OnTriggerEnter detection.");
            Assert.IsNotNull(rb2, "Fighters must carry a kinematic Rigidbody for safe PhysX OnTriggerEnter detection.");
            Assert.IsTrue(rb1.isKinematic);
            Assert.IsFalse(rb1.useGravity);
            Assert.IsTrue(rb2.isKinematic);
            Assert.IsFalse(rb2.useGravity);
            Assert.AreEqual(60, FightSimulation.TickRate);
            Assert.AreEqual(1f / 60f, FightSimulation.TickDeltaTime, 1e-6f);

            // Also compare full jump + horizontal walk against a reference fighter with NO Rigidbody
            var data = p1.Data;
            var refSimGo = Track(new GameObject("Test_NoRbRefSim"));
            var refSim = refSimGo.AddComponent<FightSimulation>();
            var refP1 = CreateFighter("Test_RefP1", data, -2.0f, withKinematicRigidbody: false);
            var refP2 = CreateFighter("Test_RefP2", data, 2.0f, withKinematicRigidbody: false);
            refSim.SetParticipants(refP1, refP2);

            p1.SetSimPositionX(-2.0f);
            p2.SetSimPositionX(2.0f);
            sim.ForceUpdateFacing();

            for (int tick = 0; tick < 45; tick++)
            {
                FighterInputState inp = tick == 2
                    ? new FighterInputState { MoveX = 1f, MoveY = 1f }
                    : new FighterInputState { MoveX = 1f, MoveY = 0f };
                p1.InputHandler.SetSimulatedInput(inp, true);
                refP1.InputHandler.SetSimulatedInput(inp, true);
                sim.StepSimulationTicks(1);
                refSim.StepSimulationTicks(1);
                p1.transform.position = new Vector3(p1.SimPosition.x, p1.SimPosition.y, 0f);
                p2.transform.position = new Vector3(p2.SimPosition.x, p2.SimPosition.y, 0f);
                Physics.Simulate(FightSimulation.TickDeltaTime);

                Assert.AreEqual(refP1.SimPosition.x, p1.SimPosition.x, 1e-5f);
                Assert.AreEqual(refP1.SimPosition.y, p1.SimPosition.y, 1e-5f);
                Assert.AreEqual(refP1.SimVelocity.x, p1.SimVelocity.x, 1e-5f);
                Assert.AreEqual(refP1.SimVelocity.y, p1.SimVelocity.y, 1e-5f);
                Assert.AreEqual(refP1.IsGrounded, p1.IsGrounded);
                Assert.AreEqual(0f, p1.transform.position.z, 1e-5f);
            }
        }

        [Test]
        public void StagePropsPrefabsAndAudio_AreValid()
        {
            string[] breakablePrefabPaths =
            {
                "Assets/UnDFight/Prefabs/StageProps/Prop_BreakableCrate.prefab",
                "Assets/UnDFight/Prefabs/StageProps/Prop_BreakableCrateStack.prefab",
                "Assets/UnDFight/Prefabs/StageProps/Prop_BreakableTable.prefab"
            };

            foreach (string path in breakablePrefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, $"Missing reusable stage prop prefab: {path}");
                var prop = prefab.GetComponent<StageBreakableProp>();
                Assert.IsNotNull(prop, $"{path} must carry StageBreakableProp.");
                var fragments = prefab.GetComponentsInChildren<StageDebrisFragment>(true);
                Assert.GreaterOrEqual(fragments.Length, 5, $"{path} must include at least 5 pooled debris fragments.");
                Assert.LessOrEqual(fragments.Length, 8, $"{path} must include at most 8 pooled debris fragments.");
            }

            string[] throwablePrefabPaths =
            {
                "Assets/UnDFight/Prefabs/StageProps/Prop_ThrowableChair.prefab",
                "Assets/UnDFight/Prefabs/StageProps/Prop_ThrowableBottle.prefab"
            };

            foreach (string path in throwablePrefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, $"Missing reusable throwable prop prefab: {path}");
                Assert.AreEqual(StagePhysicsLayers.StageProjectileLayer, prefab.layer, $"{path} must use StageProjectile layer.");
                var throwable = prefab.GetComponent<StageThrowableProp>();
                Assert.IsNotNull(throwable, $"{path} must carry StageThrowableProp.");
                var rb = prefab.GetComponent<Rigidbody>();
                Assert.IsNotNull(rb, $"{path} must carry Rigidbody.");
                Assert.IsTrue(rb.isKinematic, $"{path} must start kinematic.");
                Assert.IsFalse(rb.useGravity, $"{path} must start with gravity disabled.");
                var fragments = prefab.GetComponentsInChildren<StageDebrisFragment>(true);
                Assert.GreaterOrEqual(fragments.Length, 5, $"{path} must include pooled debris fragments.");
            }

            string[] fighterPaths =
            {
                "Assets/UnDFight/Prefabs/Fighter_Lucas.prefab",
                "Assets/UnDFight/Prefabs/Fighter_Tiago.prefab",
                "Assets/UnDFight/Prefabs/Fighter_Jompes.prefab",
                "Assets/UnDFight/Prefabs/Fighter_Rochard.prefab"
            };
            int fighterLayer = StagePhysicsLayers.FighterHurtboxLayer;
            foreach (string fPath in fighterPaths)
            {
                GameObject fPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fPath);
                Assert.IsNotNull(fPrefab);
                Assert.AreEqual(fighterLayer, fPrefab.layer, $"{fPath} root must be isolated on FighterHurtbox layer.");
                var rb = fPrefab.GetComponent<Rigidbody>();
                Assert.IsNotNull(rb, $"{fPath} must carry a kinematic Rigidbody for hurtbox trigger detection.");
                Assert.IsTrue(rb.isKinematic, $"{fPath} Rigidbody must be kinematic.");
                Assert.IsFalse(rb.useGravity, $"{fPath} Rigidbody must not use PhysX gravity.");
            }

            AudioClip impactClip = ProceduralAudioFactory.CreatePropImpactClip();
            AudioClip woodBreakClip = ProceduralAudioFactory.CreatePropBreakWoodPlasticClip();
            AudioClip glassBreakClip = ProceduralAudioFactory.CreatePropBreakGlassClip();
            Assert.Greater(impactClip.length, 0.05f);
            Assert.Greater(woodBreakClip.length, 0.10f);
            Assert.Greater(glassBreakClip.length, 0.10f);
            Track(impactClip);
            Track(woodBreakClip);
            Track(glassBreakClip);
        }

        [Test]
        public void StageThrowableProp_HoldBlockAndLightPunch_LaunchesPhysXProjectile_AndUpdatesHudPrompt()
        {
            StageThrowableProp chair = CreateTestThrowableProp("Test_ThrowableChair", new Vector3(-2.1f, 0f, 1.2f));
            var (sim, match, p1, p2, stageMgr, _, _) = CreateStageCombatRig(withMatchManager: true);
            p1.SetSimPositionX(-2.0f);
            p2.SetSimPositionX(2.0f);
            sim.ForceUpdateFacing();

            var hudGo = Track(new GameObject("Test_HudCanvas", typeof(RectTransform)));
            var hud = hudGo.AddComponent<FightHUD>();
            hud.Configure(sim, match);
            hud.RefreshStagePromptsNow();

            Assert.IsTrue(hud.IsStagePromptVisible(0), "HUD prompt must show when P1 is within reach of a Ready throwable prop.");
            Assert.IsFalse(hud.IsStagePromptVisible(1), "HUD prompt must stay hidden when P2 is out of reach.");

            p1.InputHandler.SetSimulatedInput(new FighterInputState { HoldBlock = true }, true);
            p1.InputHandler.BufferButton(FighterInputButton.LightPunch);
            sim.StepSimulationTicks(1);

            Assert.AreEqual(StageThrowableState.Grabbed, chair.State, "Prop must enter Grabbed windup immediately on HoldBlock + LightPunch.");
            hud.RefreshStagePromptsNow();
            Assert.IsFalse(hud.IsStagePromptVisible(0), "HUD prompt must hide as soon as prop is grabbed.");

            for (int i = 0; i < FighterController.StageThrowReleaseTick; i++)
            {
                sim.StepSimulationTicks(1);
            }
            Physics.Simulate(FightSimulation.TickDeltaTime);

            Assert.AreEqual(StageThrowableState.Thrown, chair.State, "Prop must launch after throw startup ticks.");
            Assert.IsFalse(chair.Body.isKinematic, "Thrown projectile Rigidbody must become dynamic (isKinematic = false).");
            Assert.IsTrue(chair.Body.useGravity, "Thrown projectile Rigidbody must use gravity.");
            Assert.Greater(chair.Body.linearVelocity.x * p1.FacingSign, 1f, "Thrown projectile must travel in attacker's FacingSign direction.");
            Assert.IsFalse(chair.PropCollider.isTrigger, "Thrown projectile collider must collide with StagePhysics and overlap FighterHurtbox triggers.");
        }

        [Test]
        public void StageThrowableProp_PhysXTriggerHitDefender_AppliesDamage_RespectsBlock_AndIgnoresThrower()
        {
            StageThrowableProp chair = CreateTestThrowableProp("Test_HitChair", new Vector3(-1.8f, 0f, 1.15f), damage: 85, knockdown: true, shatterOnFighter: true);
            var (sim, _, p1, p2, _, _, _) = CreateStageCombatRig(withMatchManager: true);
            p1.SetSimPositionX(-1.8f);
            p2.SetSimPositionX(1.6f);
            p1.transform.position = new Vector3(-1.8f, 0f, 0f);
            p2.transform.position = new Vector3(1.6f, 0f, 0f);
            sim.ForceUpdateFacing();
            Physics.SyncTransforms();

            p1.InputHandler.SetSimulatedInput(new FighterInputState { HoldBlock = true }, true);
            p1.InputHandler.BufferButton(FighterInputButton.LightPunch);

            for (int i = 0; i < 60 && chair.State != StageThrowableState.Broken; i++)
            {
                sim.StepSimulationTicks(1);
                p1.transform.position = new Vector3(p1.SimPosition.x, p1.SimPosition.y, 0f);
                p2.transform.position = new Vector3(p2.SimPosition.x, p2.SimPosition.y, 0f);
                Physics.SyncTransforms();
                Physics.Simulate(FightSimulation.TickDeltaTime);

                if (chair.State == StageThrowableState.Thrown)
                {
                    // Thrower collider overlap must be ignored even if overlapping
                    chair.ResolveTriggerOverlap(p1.HurtboxCollider);
                    if (chair.PropCollider.bounds.Intersects(p2.HurtboxCollider.bounds))
                    {
                        chair.ResolveTriggerOverlap(p2.HurtboxCollider);
                    }
                }
            }

            Assert.AreEqual(p1.MaxHealth, p1.CurrentHealth, "Thrower must NEVER be hurt by own thrown prop.");
            Assert.AreEqual(StageThrowableState.Broken, chair.State, "Chair should shatter on hitting defender hurtbox.");
            Assert.Less(p2.CurrentHealth, p2.MaxHealth, "Defender hurtbox overlap must apply projectile damage.");
            Assert.AreEqual(85, p2.MaxHealth - p2.CurrentHealth);
            Assert.IsTrue(p2.State == FighterState.KnockDown || p2.State == FighterState.GetUp, "Knockdown prop must knock down defender.");
        }

        [Test]
        public void StageThrowableProp_BlockingDefender_GuardsWithChipDamage()
        {
            StageThrowableProp bottle = CreateTestThrowableProp("Test_BlockBottle", new Vector3(-1.8f, 0f, 1.15f), damage: 75, knockdown: false, shatterOnFighter: true, shatterOnStage: true, StagePropMaterialType.Glass);
            var (sim2, _, a1, a2, _, _, _) = CreateStageCombatRig(withMatchManager: true);
            a1.SetSimPositionX(-1.8f);
            a2.SetSimPositionX(1.4f);
            a1.transform.position = new Vector3(-1.8f, 0f, 0f);
            a2.transform.position = new Vector3(1.4f, 0f, 0f);
            sim2.ForceUpdateFacing();
            Physics.SyncTransforms();

            a2.InputHandler.SetSimulatedInput(new FighterInputState { HoldBlock = true }, true);
            a1.InputHandler.SetSimulatedInput(new FighterInputState { HoldBlock = true }, true);
            a1.InputHandler.BufferButton(FighterInputButton.LightPunch);

            for (int i = 0; i < 60 && bottle.State != StageThrowableState.Broken; i++)
            {
                sim2.StepSimulationTicks(1);
                a1.transform.position = new Vector3(a1.SimPosition.x, a1.SimPosition.y, 0f);
                a2.transform.position = new Vector3(a2.SimPosition.x, a2.SimPosition.y, 0f);
                Physics.SyncTransforms();
                Physics.Simulate(FightSimulation.TickDeltaTime);

                if (bottle.State == StageThrowableState.Thrown && bottle.PropCollider.bounds.Intersects(a2.HurtboxCollider.bounds))
                {
                    bottle.ResolveTriggerOverlap(a2.HurtboxCollider);
                }
            }

            Assert.AreEqual(StageThrowableState.Broken, bottle.State);
            Assert.AreEqual(FighterState.Block, a2.State, "Blocking defender must guard the incoming stage projectile.");
            Assert.AreEqual(12, a2.MaxHealth - a2.CurrentHealth, "Blocked throwable must apply only chip damage.");
        }

        [Test]
        public void StageThrowableProp_MissBecomesSpent_AndStartRoundRestoresReadyKinematicState()
        {
            Vector3 initialPos = new Vector3(-2.0f, 0f, 1.2f);
            StageThrowableProp chair = CreateTestThrowableProp("Test_ResetChair", initialPos);
            var (sim, match, p1, p2, stageMgr, _, _) = CreateStageCombatRig(withMatchManager: true);

            // Throw toward right while P2 is behind P1 so projectile misses forward and hits floor/wall
            p1.SetSimPositionX(-2.0f);
            p2.SetSimPositionX(7.5f);
            p1.transform.position = new Vector3(-2.0f, 0f, 0f);
            p2.transform.position = new Vector3(7.5f, 0f, 0f);
            sim.ForceUpdateFacing();
            p2.SetSimPositionX(-6.0f);
            p2.transform.position = new Vector3(-6.0f, 0f, 0f);
            Physics.SyncTransforms();

            p1.InputHandler.SetSimulatedInput(new FighterInputState { HoldBlock = true }, true);
            p1.InputHandler.BufferButton(FighterInputButton.LightPunch);

            for (int i = 0; i < 120 && chair.State != StageThrowableState.Spent; i++)
            {
                sim.StepSimulationTicks(1);
                Physics.Simulate(FightSimulation.TickDeltaTime);
                if (chair.State == StageThrowableState.Thrown &&
                    stageMgr.GroundPhysicsCollider != null &&
                    chair.PropCollider.bounds.Intersects(stageMgr.GroundPhysicsCollider.bounds))
                {
                    chair.ResolveStageCollision(new Vector2(chair.transform.position.x, sim.GroundY));
                }
            }

            Assert.AreEqual(StageThrowableState.Spent, chair.State, "Missed non-fragile prop touching StagePhysics floor/wall must transition to Spent.");
            Assert.AreEqual(p2.MaxHealth, p2.CurrentHealth);

            match.StartRound();

            Assert.AreEqual(StageThrowableState.Ready, chair.State, "StartRound() must restore StageThrowableProp to Ready.");
            Assert.IsTrue(chair.Body.isKinematic, "StartRound() must restore Rigidbody.isKinematic = true.");
            Assert.IsFalse(chair.Body.useGravity, "StartRound() must restore Rigidbody.useGravity = false.");
            Assert.AreEqual(Vector3.zero, chair.Body.linearVelocity);
            Assert.Less(Vector3.Distance(initialPos, chair.transform.position), 1e-4f, "StartRound() must restore original prop position.");
        }
    }
}
