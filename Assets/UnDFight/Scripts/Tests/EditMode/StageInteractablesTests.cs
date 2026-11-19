using System.Collections.Generic;
using NUnit.Framework;
using UnDFight.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            Assert.IsTrue(StagePhysicsLayers.HasDedicatedLayers, "Dedicated PhysX layers StagePhysics, StageDebris, FighterHurtbox, StageProjectile, FighterDebrisPusher must exist.");

            int stagePhysics = StagePhysicsLayers.StagePhysicsLayer;
            int stageDebris = StagePhysicsLayers.StageDebrisLayer;
            int fighterLayer = StagePhysicsLayers.FighterHurtboxLayer;
            int stageProjectile = StagePhysicsLayers.StageProjectileLayer;
            int debrisPusher = StagePhysicsLayers.FighterDebrisPusherLayer;

            Assert.AreEqual(12, debrisPusher, "FighterDebrisPusher must occupy preferred layer slot 12.");

            Assert.IsFalse(Physics.GetIgnoreLayerCollision(stageDebris, stagePhysics), "StageDebris must collide with StagePhysics.");
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(stageDebris, debrisPusher), "StageDebris must collide with FighterDebrisPusher.");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(stageDebris, fighterLayer), "StageDebris must NEVER collide with FighterHurtbox.");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(stageDebris, 0), "StageDebris must ignore Default layer.");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(fighterLayer, stagePhysics), "FighterHurtbox must ignore StagePhysics.");
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(stageProjectile, stagePhysics), "StageProjectile must collide with StagePhysics.");
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(stageProjectile, fighterLayer), "StageProjectile must trigger-detect FighterHurtbox.");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(stageProjectile, 0), "StageProjectile must ignore Default layer.");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(stageProjectile, stageDebris), "StageProjectile must ignore StageDebris.");

            // FighterDebrisPusher (12) must collide EXCLUSIVELY with StageDebris (9)
            for (int other = 0; other < 32; other++)
            {
                bool expectedIgnore = other != stageDebris;
                Assert.AreEqual(
                    expectedIgnore,
                    Physics.GetIgnoreLayerCollision(debrisPusher, other),
                    $"FighterDebrisPusher collision mismatch against layer {other}.");
            }
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
                "Assets/UnDFight/Prefabs/Fighter_Rochard.prefab",
                "Assets/UnDFight/Prefabs/Fighter_Isaac.prefab",
                "Assets/UnDFight/Prefabs/Fighter_Enomoto.prefab",
                "Assets/UnDFight/Prefabs/Fighter_Gabubu.prefab",
                "Assets/UnDFight/Prefabs/Fighter_Ibaneis.prefab"
            };
            int fighterLayer = StagePhysicsLayers.FighterHurtboxLayer;
            int pusherLayer = StagePhysicsLayers.FighterDebrisPusherLayer;
            foreach (string fPath in fighterPaths)
            {
                GameObject fPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fPath);
                Assert.IsNotNull(fPrefab, $"Missing fighter prefab: {fPath}");
                Assert.AreEqual(fighterLayer, fPrefab.layer, $"{fPath} root must be isolated on FighterHurtbox layer.");
                var rb = fPrefab.GetComponent<Rigidbody>();
                Assert.IsNotNull(rb, $"{fPath} must carry a kinematic Rigidbody for hurtbox trigger detection.");
                Assert.IsTrue(rb.isKinematic, $"{fPath} Rigidbody must be kinematic.");
                Assert.IsFalse(rb.useGravity, $"{fPath} Rigidbody must not use PhysX gravity.");

                Transform pusherChild = fPrefab.transform.Find(FighterDebrisPusher.DefaultChildName);
                Assert.IsNotNull(pusherChild, $"{fPath} must have FighterDebrisPusher child.");
                Assert.AreEqual(pusherLayer, pusherChild.gameObject.layer, $"{fPath} FighterDebrisPusher must be on layer 12.");
                var pusherComp = pusherChild.GetComponent<FighterDebrisPusher>();
                var pusherRb = pusherChild.GetComponent<Rigidbody>();
                var pusherCol = pusherChild.GetComponent<CapsuleCollider>();
                Assert.IsNotNull(pusherComp);
                Assert.IsNotNull(pusherRb);
                Assert.IsTrue(pusherRb.isKinematic);
                Assert.IsFalse(pusherRb.useGravity);
                Assert.IsNotNull(pusherCol);
                Assert.IsFalse(pusherCol.isTrigger, "FighterDebrisPusher CapsuleCollider must be solid (isTrigger = false).");
            }

            GameObject canPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UnDFight/Prefabs/StageProps/Prop_DynamicCan.prefab");
            Assert.IsNotNull(canPrefab, "Missing Prop_DynamicCan.prefab");
            Assert.AreEqual(StagePhysicsLayers.StageDebrisLayer, canPrefab.layer);
            Assert.IsNotNull(canPrefab.GetComponent<StageDynamicProp>());
            Assert.IsNotNull(canPrefab.GetComponent<StageWindAffectable>());

            GameObject signPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UnDFight/Prefabs/StageProps/Prop_HangingSign.prefab");
            Assert.IsNotNull(signPrefab, "Missing Prop_HangingSign.prefab");
            var hangComp = signPrefab.GetComponent<StageHangingProp>();
            Assert.IsNotNull(hangComp);
            Assert.IsNotNull(signPrefab.GetComponentInChildren<HingeJoint>(true));
            Assert.IsNotNull(signPrefab.GetComponentInChildren<StageWindAffectable>(true));

            AudioClip impactClip = ProceduralAudioFactory.CreatePropImpactClip();
            AudioClip woodBreakClip = ProceduralAudioFactory.CreatePropBreakWoodPlasticClip();
            AudioClip glassBreakClip = ProceduralAudioFactory.CreatePropBreakGlassClip();
            AudioClip rainLoopClip = ProceduralAudioFactory.CreateRainLoopClip();
            AudioClip windLoopClip = ProceduralAudioFactory.CreateNightWindLoopClip();
            Assert.Greater(impactClip.length, 0.05f);
            Assert.Greater(woodBreakClip.length, 0.10f);
            Assert.Greater(glassBreakClip.length, 0.10f);
            Assert.Greater(rainLoopClip.length, 1.0f);
            Assert.Greater(windLoopClip.length, 1.0f);
            Track(impactClip);
            Track(woodBreakClip);
            Track(glassBreakClip);
            Track(rainLoopClip);
            Track(windLoopClip);
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

        [Test]
        public void StageDebrisFragment_PersistsAsDynamicRigidbodyUntilRoundReset()
        {
            StageBreakableProp prop = CreateTestProp("Test_PersistentCrate", new Vector3(0.4f, 0f, 1.0f), integrity: 2, radiusX: 1.5f);
            var (sim, match, p1, p2, stageMgr, _, _) = CreateStageCombatRig(withMatchManager: true);
            p1.SetSimPositionX(-0.45f);
            p2.SetSimPositionX(0.55f);
            sim.ForceUpdateFacing();

            p1.InputHandler.SetSimulatedInput(default);
            p1.InputHandler.BufferButton(FighterInputButton.HeavyPunch);
            sim.StepSimulationTicks(12);
            Assert.IsTrue(prop.IsBroken);

            // Simulate 6 full seconds of PhysX (360 fixed ticks) well past legacy freeze/hide thresholds
            for (int i = 0; i < 360; i++)
            {
                Physics.Simulate(FightSimulation.TickDeltaTime);
            }

            for (int i = 0; i < prop.Fragments.Count; i++)
            {
                StageDebrisFragment frag = prop.Fragments[i];
                Assert.IsTrue(frag.PersistUntilReset, "Debris fragments must persist until round reset by default.");
                Assert.IsTrue(frag.gameObject.activeSelf, $"Persistent fragment {i} must remain active after 6s.");
                Assert.IsFalse(frag.IsFrozen, $"Persistent fragment {i} must not freeze to kinematic.");
                Assert.IsFalse(frag.Body.isKinematic, $"Persistent fragment {i} must stay a real dynamic Rigidbody.");
            }

            // Shockwave wakes persistent fragments and applies impulse
            StageDebrisFragment sample = prop.Fragments[0];
            sample.Body.Sleep();
            int affected = stageMgr.ApplyStageShockwave(sample.transform.position - new Vector3(0.5f, 0f, 0f), radius: 3.5f, force: 4.5f);
            Assert.Greater(affected, 0, "ApplyStageShockwave must affect nearby persistent debris fragments.");
            Physics.Simulate(FightSimulation.TickDeltaTime);
            Assert.Greater(sample.Body.linearVelocity.magnitude, 0.2f, "Shockwave must wake and accelerate sleeping persistent debris.");

            // Round reset cleans everything
            match.StartRound();
            for (int i = 0; i < prop.Fragments.Count; i++)
            {
                Assert.IsFalse(prop.Fragments[i].gameObject.activeSelf);
            }
        }

        [Test]
        public void FighterDebrisPusher_OneWayCoupling_PushesDynamicPropWithoutAltering60HzDeterminism()
        {
            var (sim, _, p1, p2, stageMgr, _, _) = CreateStageCombatRig(withMatchManager: true);
            p1.SetSimPositionX(-1.6f);
            p2.SetSimPositionX(2.4f);
            p1.transform.position = new Vector3(-1.6f, 0f, 0f);
            p2.transform.position = new Vector3(2.4f, 0f, 0f);
            p1.DebrisPusher?.SyncFromFighter(useMovePosition: false);
            p2.DebrisPusher?.SyncFromFighter(useMovePosition: false);
            sim.ForceUpdateFacing();

            // Reference fighter simulation with NO dynamic props and NO PhysX contact
            var refSimGo = Track(new GameObject("Test_OneWayRefSim"));
            var refSim = refSimGo.AddComponent<FightSimulation>();
            var refP1 = CreateFighter("RefP1", p1.Data, -1.6f, withKinematicRigidbody: false);
            var refP2 = CreateFighter("RefP2", p2.Data, 2.4f, withKinematicRigidbody: false);
            refSim.SetParticipants(refP1, refP2);
            if (refP1.DebrisPusher != null) refP1.DebrisPusher.gameObject.SetActive(false);
            if (refP2.DebrisPusher != null) refP2.DebrisPusher.gameObject.SetActive(false);

            // Loose dynamic can placed directly in P1's forward walk path at Z=0
            var canGo = Track(GameObject.CreatePrimitive(PrimitiveType.Cylinder));
            canGo.name = "Test_CanInPath";
            canGo.transform.position = new Vector3(-0.85f, 0.14f, 0f);
            canGo.transform.localScale = new Vector3(0.14f, 0.12f, 0.14f);
            var dynCan = canGo.AddComponent<StageDynamicProp>();
            dynCan.EnsureComponents();
            stageMgr.RefreshDiscoveredProps();

            FighterDebrisPusher pusher = p1.EnsureDebrisPusher();
            Assert.IsNotNull(pusher);
            Assert.AreEqual(StagePhysicsLayers.FighterDebrisPusherLayer, pusher.gameObject.layer);
            Assert.IsTrue(pusher.Body.isKinematic);
            Assert.IsFalse(pusher.PusherCollider.isTrigger);

            Vector3 initialCanPos = canGo.transform.position;
            bool pushed = false;

            for (int tick = 0; tick < 40; tick++)
            {
                var moveRight = new FighterInputState { MoveX = 1f, MoveY = 0f };
                p1.InputHandler.SetSimulatedInput(moveRight, true);
                refP1.InputHandler.SetSimulatedInput(moveRight, true);
                p2.InputHandler.SetSimulatedInput(default, true);
                refP2.InputHandler.SetSimulatedInput(default, true);

                sim.StepSimulationTicks(1);
                refSim.StepSimulationTicks(1);

                p1.transform.position = new Vector3(p1.SimPosition.x, p1.SimPosition.y, 0f);
                p2.transform.position = new Vector3(p2.SimPosition.x, p2.SimPosition.y, 0f);
                pusher.SyncFromFighter(useMovePosition: true);
                Physics.SyncTransforms();
                Physics.Simulate(FightSimulation.TickDeltaTime);

                if (!pushed && pusher.PusherCollider.bounds.Intersects(dynCan.PropCollider.bounds))
                {
                    pushed = pusher.ApplyPushToBody(dynCan.Body, dynCan.transform.position);
                }

                // Bit-for-bit identical 60 Hz deterministic trajectory compared to reference fighter
                Assert.AreEqual(refP1.SimPosition.x, p1.SimPosition.x, 0f, $"Tick {tick}: Fighter SimPosition.x must be bit-identical.");
                Assert.AreEqual(refP1.SimPosition.y, p1.SimPosition.y, 0f, $"Tick {tick}: Fighter SimPosition.y must be bit-identical.");
                Assert.AreEqual(refP1.SimVelocity.x, p1.SimVelocity.x, 0f, $"Tick {tick}: Fighter SimVelocity.x must be bit-identical.");
                Assert.AreEqual(refP1.SimVelocity.y, p1.SimVelocity.y, 0f, $"Tick {tick}: Fighter SimVelocity.y must be bit-identical.");
                Assert.AreEqual(0f, p1.transform.position.z, 0f);
            }

            Assert.IsTrue(pushed, "FighterDebrisPusher must contact and kick the dynamic can in its path.");
            Assert.Greater(canGo.transform.position.x - initialCanPos.x, 0.35f, "Dynamic prop must be pushed forward by the fighter.");
        }

        [Test]
        public void StageWindController_AppliesQuadraticDragForce_AndSwingsHingeJointProp()
        {
            // 1. Exact quadratic drag law: doubling v_rel quadruples F_drag
            Vector3 f1 = StageWindController.ComputeDragForce(new Vector3(4f, 0f, 0f), Vector3.zero, 1.225f, 1.05f, 0.20f, 1.0f);
            Vector3 f2 = StageWindController.ComputeDragForce(new Vector3(8f, 0f, 0f), Vector3.zero, 1.225f, 1.05f, 0.20f, 1.0f);
            float expectedF1 = 0.5f * 1.225f * 1.05f * 0.20f * 4f * 4f;
            Assert.AreEqual(expectedF1, f1.x, 1e-4f);
            Assert.AreEqual(4f, f2.x / f1.x, 1e-4f, "Aerodynamic drag must scale quadratically with v_rel^2.");

            // 2. Wind controller accelerates a dynamic prop & swings a HingeJoint StageHangingProp
            var windGo = Track(new GameObject("Test_WindController"));
            var wind = windGo.AddComponent<StageWindController>();
            wind.Configure(Vector3.right, baseSpeed: 8.5f, gustAmp: 2.0f, gustFreq: 0.5f);

            var floorGo = Track(new GameObject("Test_WindFloor"));
            floorGo.layer = StagePhysicsLayers.StagePhysicsLayer;
            floorGo.transform.position = new Vector3(0f, -0.5f, 0f);
            var floorBox = floorGo.AddComponent<BoxCollider>();
            floorBox.size = new Vector3(30f, 1f, 10f);

            var canGo = Track(GameObject.CreatePrimitive(PrimitiveType.Cylinder));
            canGo.transform.position = new Vector3(0f, 0.2f, 0f);
            canGo.transform.localScale = new Vector3(0.14f, 0.12f, 0.14f);
            var can = canGo.AddComponent<StageDynamicProp>();
            can.EnsureComponents();
            can.WindAffectable.Configure(cd: 1.15f, areaM2: 0.22f, sensitivity: 1.5f, wakeThreshold: 1.5f, bodyOverride: can.Body);

            var signGo = Track(new GameObject("Test_HangingSign"));
            signGo.transform.position = new Vector3(0f, 3.0f, 2.0f);
            var hanging = signGo.AddComponent<StageHangingProp>();
            hanging.EnsureComponents();
            hanging.CacheInitialTransform();
            Quaternion initialSwingRot = hanging.SwingBody.transform.rotation;

            Physics.SyncTransforms();
            for (int i = 0; i < 60; i++)
            {
                wind.StepWind(FightSimulation.TickDeltaTime, useSceneDiscoveryFallback: true);
                Physics.Simulate(FightSimulation.TickDeltaTime);
            }

            Assert.Greater(can.transform.position.x, 0.25f, "Strong aerodynamic wind must push the dynamic prop in the wind direction.");
            float swingAngleDelta = Quaternion.Angle(initialSwingRot, hanging.SwingBody.transform.rotation);
            Assert.Greater(swingAngleDelta, 2.5f, "Aerodynamic wind must swing StageHangingProp via its HingeJoint.");

            // Reset restores exact hinge rest pose
            hanging.ResetProp();
            Assert.Less(Quaternion.Angle(initialSwingRot, hanging.SwingBody.transform.rotation), 0.1f);
        }

        [Test]
        public void StageWeatherController_AndSessionWeather_SwitchDayNightRainyNight_AndRespectFixedWeatherStages()
        {
            var favelaData = AssetDatabase.LoadAssetAtPath<StageData>("Assets/UnDFight/Data/StageData_FavelaCarioca.asset");
            var treinoData = AssetDatabase.LoadAssetAtPath<StageData>("Assets/UnDFight/Data/StageData_SalaDeTreino.asset");
            Assert.IsNotNull(favelaData);
            Assert.IsNotNull(treinoData);
            Assert.IsTrue(favelaData.supportsWeatherVariants, "Favela Carioca must support weather variants.");
            Assert.IsFalse(treinoData.supportsWeatherVariants, "Sala de Treino must keep weather fixed.");

            // Session cycling
            Assert.AreEqual(StageWeatherVariant.Day, GameSessionState.SelectedWeather);
            Assert.AreEqual("PÔR DO SOL", GameSessionState.GetWeatherLabel(StageWeatherVariant.Day, favelaData));
            GameSessionState.CycleWeather(1);
            Assert.AreEqual(StageWeatherVariant.Night, GameSessionState.SelectedWeather);
            Assert.AreEqual("NOITE", GameSessionState.GetWeatherLabel(StageWeatherVariant.Night, favelaData));
            GameSessionState.CycleWeather(1);
            Assert.AreEqual(StageWeatherVariant.RainyNight, GameSessionState.SelectedWeather);
            Assert.AreEqual("NOITE CHUVOSA", GameSessionState.GetWeatherLabel(StageWeatherVariant.RainyNight, favelaData));

            // Controller switching on weather-enabled stage
            var root = Track(new GameObject("Test_WeatherRig"));
            var lightGo = Track(new GameObject("Test_DirLight"));
            var dirLight = lightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;

            var nightLamp = Track(new GameObject("Test_NightLamp"));
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            var floorRenderer = floor.GetComponent<MeshRenderer>();

            var wind = root.AddComponent<StageWindController>();
            var weather = root.AddComponent<StageWeatherController>();
            weather.ConfigureReferences(favelaData, dirLight, wind, new[] { nightLamp }, new Renderer[] { floorRenderer });

            weather.ApplyWeather(StageWeatherVariant.Day);
            float dayIntensity = dirLight.intensity;
            float dayWind = wind.BaseWindSpeed;
            Assert.AreEqual(StageWeatherVariant.Day, weather.CurrentWeather);
            Assert.IsFalse(nightLamp.activeSelf, "Night-only objects must stay off during Day.");
            Assert.IsFalse(weather.IsWetSurfaceActive);

            weather.ApplyWeather(StageWeatherVariant.Night);
            Assert.AreEqual(StageWeatherVariant.Night, weather.CurrentWeather);
            Assert.Less(dirLight.intensity, dayIntensity, "Night directional light must be dimmer than Day.");
            Assert.Greater(wind.BaseWindSpeed, dayWind, "Night wind must be stronger than Day breeze.");
            Assert.IsTrue(nightLamp.activeSelf, "Night-only objects must activate in Night.");
            Assert.IsFalse(weather.IsWetSurfaceActive);

            weather.ApplyWeather(StageWeatherVariant.RainyNight);
            Assert.AreEqual(StageWeatherVariant.RainyNight, weather.CurrentWeather);
            Assert.IsTrue(nightLamp.activeSelf, "Night-only objects must remain active in RainyNight.");
            Assert.Greater(wind.BaseWindSpeed, 5.0f, "RainyNight must apply strong storm gusts.");
            Assert.IsTrue(weather.IsWetSurfaceActive, "RainyNight must enable wet surface MaterialPropertyBlock.");
            Assert.IsNotNull(weather.RainParticleSystem, "RainyNight must create/activate 3D rain ParticleSystem.");
            Assert.IsTrue(weather.RainParticleSystem.gameObject.activeSelf);
            Assert.IsTrue(weather.RainParticleSystem.collision.enabled, "3D rain must enable PhysX World collision.");
            Assert.AreEqual(ParticleSystemCollisionType.World, weather.RainParticleSystem.collision.type);
            Assert.AreEqual(ParticleSystemCollisionMode.Collision3D, weather.RainParticleSystem.collision.mode);
            Assert.Greater(weather.RainParticleSystem.subEmitters.subEmittersCount, 0, "3D rain must attach splash sub-emitter on collision.");

            int expectedRainMask = (1 << StagePhysicsLayers.StagePhysicsLayer) | (1 << StagePhysicsLayers.StageDebrisLayer);
            Assert.AreEqual(expectedRainMask, weather.RainParticleSystem.collision.collidesWith.value, "3D rain must collide with StagePhysics and StageDebris.");

            // Fixed-weather stage ignores RainyNight and forces defaultWeather (Day)
            weather.StageData = treinoData;
            weather.ApplyWeather(StageWeatherVariant.RainyNight);
            Assert.AreEqual(StageWeatherVariant.Day, weather.CurrentWeather, "Stages with supportsWeatherVariants=false must force defaultWeather.");
            Assert.IsFalse(weather.RainParticleSystem.gameObject.activeSelf, "Fixed-weather stage must disable 3D rain.");
            Assert.IsFalse(nightLamp.activeSelf);

            // CharacterSelectController Stage Select weather selector UI check
            var roster = AssetDatabase.LoadAssetAtPath<FighterRoster>("Assets/UnDFight/Data/FighterRoster.asset");
            var stageRoster = AssetDatabase.LoadAssetAtPath<StageRoster>("Assets/UnDFight/Data/StageRoster.asset");
            if (roster != null && stageRoster != null)
            {
                GameSessionState.SelectWeather(StageWeatherVariant.Day);
                var canvasGo = Track(new GameObject("Test_StageWeatherUI", typeof(RectTransform), typeof(Canvas)));
                var ctrl = canvasGo.AddComponent<CharacterSelectController>();
                var tex = Track(new Texture2D(2, 2));
                var spr = Track(Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f)));
                ctrl.ConfigureReferences(roster, stageRoster, spr, null, null);
                ctrl.BuildUI(canvasGo.transform, spr);
                ctrl.ConfirmBothAndOpenStageSelect(0, 1);

                Assert.IsNotNull(ctrl.WeatherLabelText);
                StringAssert.Contains("PÔR DO SOL", ctrl.WeatherLabelText.text);

                ctrl.StepWeatherSelection(1);
                Assert.AreEqual(StageWeatherVariant.Night, GameSessionState.SelectedWeather);
                StringAssert.Contains("NOITE", ctrl.WeatherLabelText.text);

                ctrl.StepWeatherSelection(1);
                Assert.AreEqual(StageWeatherVariant.RainyNight, GameSessionState.SelectedWeather);
                StringAssert.Contains("NOITE CHUVOSA", ctrl.WeatherLabelText.text);
            }
        }

        [Test]
        public void WeatherAudio_OnlyPlaysInRainyNight_SilentInDayAndCleanNight()
        {
            var audioGo = Track(new GameObject("Test_WeatherAudioMgr"));
            var audioMgr = audioGo.AddComponent<FightAudioManager>();
            audioMgr.EnsureAudioReady();

            audioMgr.SetWeatherAmbience(StageWeatherVariant.Day, enabled: true);
            Assert.IsNull(audioMgr.WeatherAmbientSource.clip, "Day must NOT activate weather ambience audio.");

            audioMgr.SetWeatherAmbience(StageWeatherVariant.Night, enabled: true);
            Assert.IsNull(audioMgr.WeatherAmbientSource.clip, "Clean Night must NOT activate rain/wind ambience audio.");

            audioMgr.SetWeatherAmbience(StageWeatherVariant.RainyNight, enabled: true);
            Assert.IsNotNull(audioMgr.WeatherAmbientSource.clip, "RainyNight must activate the rain ambience loop clip.");
            Assert.AreEqual("AMB_Weather_Rain_Loop", audioMgr.WeatherAmbientSource.clip.name);

            audioMgr.SetWeatherAmbience(StageWeatherVariant.Night, enabled: true);
            Assert.IsNull(audioMgr.WeatherAmbientSource.clip, "Switching back from RainyNight to clean Night must stop weather ambience.");
        }

        [Test]
        public void DayAndNightWind_StaysBelowGroundPropThresholds_WhileRainyNightMovesObjects()
        {
            GameObject canPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UnDFight/Prefabs/StageProps/Prop_DynamicCan.prefab");
            GameObject cratePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UnDFight/Prefabs/StageProps/Prop_BreakableCrate.prefab");
            GameObject signPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UnDFight/Prefabs/StageProps/Prop_HangingSign.prefab");
            Assert.IsNotNull(canPrefab);
            Assert.IsNotNull(cratePrefab);
            Assert.IsNotNull(signPrefab);

            StageWindAffectable canWind = canPrefab.GetComponent<StageWindAffectable>();
            StageWindAffectable debrisWind = cratePrefab.GetComponentInChildren<StageDebrisFragment>(true).GetComponent<StageWindAffectable>();
            StageWindAffectable signWind = signPrefab.GetComponentInChildren<StageWindAffectable>(true);
            Assert.IsNotNull(canWind);
            Assert.IsNotNull(debrisWind);
            Assert.IsNotNull(signWind);

            Assert.AreEqual(4.2f, canWind.WakeGustThreshold, 0.01f);
            Assert.AreEqual(0.95f, canWind.WindSensitivity, 0.01f);
            Assert.AreEqual(4.8f, debrisWind.WakeGustThreshold, 0.01f);
            Assert.AreEqual(0.90f, debrisWind.WindSensitivity, 0.01f);
            Assert.AreEqual(0.75f, signWind.WakeGustThreshold, 0.01f);
            Assert.AreEqual(1.20f, signWind.WindSensitivity, 0.01f);

            var windGo = Track(new GameObject("Test_CalibratedWind"));
            var wind = windGo.AddComponent<StageWindController>();

            // Day max wind peak (~1.0 m/s) and Night max wind peak (~1.8 m/s) stay well below ground prop thresholds
            wind.ApplyWeatherPreset(StageWeatherVariant.Day);
            float dayPeak = wind.BaseWindSpeed + wind.GustAmplitude;
            Assert.LessOrEqual(dayPeak, 1.05f);
            Assert.Less(dayPeak, canWind.WakeGustThreshold, "Day breeze must not wake/drag resting cans.");
            Assert.Less(dayPeak, debrisWind.WakeGustThreshold, "Day breeze must not wake/drag resting debris.");

            wind.ApplyWeatherPreset(StageWeatherVariant.Night);
            float nightPeak = wind.BaseWindSpeed + wind.GustAmplitude;
            Assert.LessOrEqual(nightPeak, 1.85f);
            Assert.Less(nightPeak, canWind.WakeGustThreshold, "Clean Night breeze must not wake/drag resting cans.");
            Assert.Less(nightPeak, debrisWind.WakeGustThreshold, "Clean Night breeze must not wake/drag resting debris.");

            // Simulate resting can on floor during Night -> stays asleep & unmoved
            var floorGo = Track(new GameObject("Test_CalibratedFloor"));
            floorGo.layer = StagePhysicsLayers.StagePhysicsLayer;
            floorGo.transform.position = new Vector3(0f, -0.5f, 0f);
            floorGo.AddComponent<BoxCollider>().size = new Vector3(30f, 1f, 10f);

            GameObject canInst = Track(Object.Instantiate(canPrefab, new Vector3(0f, 0.12f, 0f), Quaternion.identity));
            var dynCan = canInst.GetComponent<StageDynamicProp>();
            dynCan.EnsureComponents();
            Physics.SyncTransforms();
            for (int i = 0; i < 30; i++) Physics.Simulate(FightSimulation.TickDeltaTime);
            dynCan.Body.linearVelocity = Vector3.zero;
            dynCan.Body.angularVelocity = Vector3.zero;
            dynCan.Body.Sleep();
            Vector3 restingPos = canInst.transform.position;

            for (int i = 0; i < 45; i++)
            {
                wind.StepWind(FightSimulation.TickDeltaTime, useSceneDiscoveryFallback: true);
                Physics.Simulate(FightSimulation.TickDeltaTime);
            }
            Assert.Less(Vector3.Distance(restingPos, canInst.transform.position), 0.02f, "Resting dynamic can must NOT be dragged by clean Night breeze.");

            // Switch to RainyNight (6.2..10.7 m/s) -> exceeds threshold, wakes and moves the can!
            wind.ApplyWeatherPreset(StageWeatherVariant.RainyNight);
            Assert.Greater(wind.BaseWindSpeed, canWind.WakeGustThreshold);
            Assert.Greater(wind.BaseWindSpeed, debrisWind.WakeGustThreshold);
            for (int i = 0; i < 60; i++)
            {
                wind.StepWind(FightSimulation.TickDeltaTime, useSceneDiscoveryFallback: true);
                Physics.Simulate(FightSimulation.TickDeltaTime);
            }
            Assert.Greater(canInst.transform.position.x - restingPos.x, 0.15f, "RainyNight storm gusts must exceed wakeThreshold and move ground props.");
        }

        [Test]
        public void StageWeatherController_TogglesDayNightClearNightAndRainOnlyObjects_AcrossAllThreeWeathers()
        {
            var favelaData = AssetDatabase.LoadAssetAtPath<StageData>("Assets/UnDFight/Data/StageData_FavelaCarioca.asset");
            Assert.IsNotNull(favelaData);

            var root = Track(new GameObject("Test_WeatherGroupsRig"));
            var dirLightGo = Track(new GameObject("Test_WeatherGroupsDirLight"));
            var dirLight = dirLightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;

            var dayObj = Track(new GameObject("Test_DayOnlySun"));
            var nightObj = Track(new GameObject("Test_NightLightsAndWindows"));
            var clearNightObj = Track(new GameObject("Test_ClearNightMoonAndStars"));
            var rainObj = Track(new GameObject("Test_RainOnlyPuddles"));

            var wind = root.AddComponent<StageWindController>();
            var weather = root.AddComponent<StageWeatherController>();
            weather.ConfigureReferences(
                favelaData,
                dirLight,
                wind,
                nightObjs: new[] { nightObj },
                wetRenderers: null,
                dayObjs: new[] { dayObj },
                clearNightObjs: new[] { clearNightObj },
                rainObjs: new[] { rainObj });

            Assert.AreEqual(1, weather.DayOnlyObjects.Length);
            Assert.AreEqual(1, weather.NightOnlyObjects.Length);
            Assert.AreEqual(1, weather.ClearNightOnlyObjects.Length);
            Assert.AreEqual(1, weather.RainOnlyObjects.Length);

            // Day (Sunset): only dayObj active
            weather.ApplyWeather(StageWeatherVariant.Day);
            Assert.IsTrue(dayObj.activeSelf, "DayOnlyObjects must be active in Day.");
            Assert.IsFalse(nightObj.activeSelf, "NightOnlyObjects must be inactive in Day.");
            Assert.IsFalse(clearNightObj.activeSelf, "ClearNightOnlyObjects must be inactive in Day.");
            Assert.IsFalse(rainObj.activeSelf, "RainOnlyObjects must be inactive in Day.");

            // Clean Night: nightObj + clearNightObj active; dayObj + rainObj inactive
            weather.ApplyWeather(StageWeatherVariant.Night);
            Assert.IsFalse(dayObj.activeSelf, "DayOnlyObjects must be inactive in Night.");
            Assert.IsTrue(nightObj.activeSelf, "NightOnlyObjects must be active in Night.");
            Assert.IsTrue(clearNightObj.activeSelf, "ClearNightOnlyObjects must be active in clean Night.");
            Assert.IsFalse(rainObj.activeSelf, "RainOnlyObjects must be inactive in clean Night.");

            // Rainy Night: nightObj + rainObj active; dayObj + clearNightObj inactive
            weather.ApplyWeather(StageWeatherVariant.RainyNight);
            Assert.IsFalse(dayObj.activeSelf, "DayOnlyObjects must be inactive in RainyNight.");
            Assert.IsTrue(nightObj.activeSelf, "NightOnlyObjects must remain active in RainyNight.");
            Assert.IsFalse(clearNightObj.activeSelf, "ClearNightOnlyObjects must be inactive in RainyNight.");
            Assert.IsTrue(rainObj.activeSelf, "RainOnlyObjects must be active in RainyNight.");
        }

        [Test]
        public void Phase15_2_NewPrefabsAndArenaFavelaDetails_AreConfiguredCorrectly()
        {
            // 1. Validate StageData_FavelaCarioca weather metadata
            var favelaData = AssetDatabase.LoadAssetAtPath<StageData>("Assets/UnDFight/Data/StageData_FavelaCarioca.asset");
            Assert.IsNotNull(favelaData);
            Assert.IsTrue(favelaData.supportsWeatherVariants);
            Assert.AreEqual("PÔR DO SOL", favelaData.dayVariantLabel);

            // 2. Validate new Phase 15.2 reusable prefabs
            GameObject clothPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UnDFight/Prefabs/StageProps/Prop_HangingCloth.prefab");
            Assert.IsNotNull(clothPrefab, "Missing Prop_HangingCloth.prefab");
            var clothHang = clothPrefab.GetComponent<StageHangingProp>();
            var clothHinge = clothPrefab.GetComponentInChildren<HingeJoint>(true);
            var clothWind = clothPrefab.GetComponentInChildren<StageWindAffectable>(true);
            Assert.IsNotNull(clothHang);
            Assert.IsNotNull(clothHinge);
            Assert.AreEqual(Vector3.right, clothHinge.axis, "Prop_HangingCloth HingeJoint must swing around horizontal Vector3.right axis.");
            Assert.IsNotNull(clothWind);
            Assert.AreEqual(0.65f, clothWind.WakeGustThreshold, 0.02f);
            Assert.AreEqual(1.35f, clothWind.WindSensitivity, 0.02f);
            Assert.AreEqual(StagePhysicsLayers.StageDebrisLayer, clothHinge.gameObject.layer);

            GameObject lanternPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UnDFight/Prefabs/StageProps/Prop_HangingLantern.prefab");
            Assert.IsNotNull(lanternPrefab, "Missing Prop_HangingLantern.prefab");
            Assert.IsNotNull(lanternPrefab.GetComponent<StageHangingProp>());
            var lanternHinge = lanternPrefab.GetComponentInChildren<HingeJoint>(true);
            Assert.IsNotNull(lanternHinge);
            Assert.AreEqual(StagePhysicsLayers.StageDebrisLayer, lanternHinge.gameObject.layer);
            Assert.IsNotNull(lanternPrefab.GetComponentInChildren<StageWindAffectable>(true));

            GameObject conePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UnDFight/Prefabs/StageProps/Prop_DynamicCone.prefab");
            Assert.IsNotNull(conePrefab, "Missing Prop_DynamicCone.prefab");
            Assert.AreEqual(StagePhysicsLayers.StageDebrisLayer, conePrefab.layer);
            Assert.IsNotNull(conePrefab.GetComponent<StageDynamicProp>());
            var coneRb = conePrefab.GetComponent<Rigidbody>();
            Assert.IsNotNull(coneRb);
            Assert.AreEqual(0.45f, coneRb.mass, 0.02f);
            var coneWind = conePrefab.GetComponent<StageWindAffectable>();
            Assert.IsNotNull(coneWind);
            Assert.AreEqual(4.4f, coneWind.WakeGustThreshold, 0.05f, "Dynamic cone must stay still in Day/Night breeze until kicked or storm hits.");

            // 3. Validate Arena_Favela.unity scene wiring (open additively and close cleanly)
            Scene prevActive = SceneManager.GetActiveScene();
            Scene favelaScene = EditorSceneManager.OpenScene("Assets/UnDFight/Scenes/Arena_Favela.unity", OpenSceneMode.Additive);
            try
            {
                StageWeatherController weather = null;
                StageInteractableManager stageMgr = null;
                var hangingProps = new List<StageHangingProp>();
                var dynamicProps = new List<StageDynamicProp>();

                foreach (GameObject rootGo in favelaScene.GetRootGameObjects())
                {
                    if (weather == null) weather = rootGo.GetComponentInChildren<StageWeatherController>(true);
                    if (stageMgr == null) stageMgr = rootGo.GetComponentInChildren<StageInteractableManager>(true);
                    hangingProps.AddRange(rootGo.GetComponentsInChildren<StageHangingProp>(true));
                    dynamicProps.AddRange(rootGo.GetComponentsInChildren<StageDynamicProp>(true));
                }

                Assert.IsNotNull(weather, "Arena_Favela must have StageWeatherController.");
                Assert.IsNotNull(stageMgr, "Arena_Favela must have StageInteractableManager.");

                // DayOnlyObjects must contain Sunset_SunDisc
                Assert.IsNotNull(weather.DayOnlyObjects);
                bool hasSunDisc = false;
                foreach (GameObject go in weather.DayOnlyObjects)
                {
                    if (go != null && go.name == "Sunset_SunDisc") hasSunDisc = true;
                }
                Assert.IsTrue(hasSunDisc, "Sunset_SunDisc must be assigned to StageWeatherController.DayOnlyObjects.");

                // ClearNightOnlyObjects must contain FavelaClearNightSky
                Assert.IsNotNull(weather.ClearNightOnlyObjects);
                bool hasClearNightSky = false;
                foreach (GameObject go in weather.ClearNightOnlyObjects)
                {
                    if (go != null && go.name == "FavelaClearNightSky") hasClearNightSky = true;
                }
                Assert.IsTrue(hasClearNightSky, "FavelaClearNightSky must be assigned to StageWeatherController.ClearNightOnlyObjects.");

                // NightOnlyObjects must contain FavelaNightLightsAndWindows (all point lights LightShadows.None) & FavelaNightWindows_Hillside (>= 14 lit windows)
                Assert.IsNotNull(weather.NightOnlyObjects);
                GameObject nightLightsGo = null;
                GameObject hillsideWinGo = null;
                foreach (GameObject go in weather.NightOnlyObjects)
                {
                    if (go == null) continue;
                    if (go.name == "FavelaNightLightsAndWindows") nightLightsGo = go;
                    if (go.name == "FavelaNightWindows_Hillside") hillsideWinGo = go;
                }
                Assert.IsNotNull(nightLightsGo, "FavelaNightLightsAndWindows must be in NightOnlyObjects.");
                Assert.IsNotNull(hillsideWinGo, "FavelaNightWindows_Hillside must be in NightOnlyObjects.");
                Assert.GreaterOrEqual(hillsideWinGo.transform.childCount, 14, "Hillside favela must have at least 14 warm lit night window panels.");

                Light[] nightLights = nightLightsGo.GetComponentsInChildren<Light>(true);
                Assert.GreaterOrEqual(nightLights.Length, 5);
                foreach (Light nl in nightLights)
                {
                    Assert.AreEqual(LightShadows.None, nl.shadows, $"Night point light '{nl.name}' must use LightShadows.None for 60 FPS.");
                }

                // RainOnlyObjects must contain FavelaRainPuddles (>= 5 puddles)
                Assert.IsNotNull(weather.RainOnlyObjects);
                GameObject puddlesGo = null;
                foreach (GameObject go in weather.RainOnlyObjects)
                {
                    if (go != null && go.name == "FavelaRainPuddles") puddlesGo = go;
                }
                Assert.IsNotNull(puddlesGo, "FavelaRainPuddles must be in RainOnlyObjects.");
                Assert.GreaterOrEqual(puddlesGo.transform.childCount, 5, "Arena_Favela must have at least 5 rain puddles.");

                // Hanging props: 1 bar sign + 3 clothesline clothes + 1 right lantern = >= 5 HingeJoint hanging props
                Assert.GreaterOrEqual(hangingProps.Count, 5, "Arena_Favela must contain Hanging_BarSign, 3 clothesline HingeJoints, and Hanging_RightLantern.");
                int clothCount = 0;
                bool hasRightLantern = false;
                foreach (StageHangingProp hp in hangingProps)
                {
                    Assert.IsNotNull(hp.GetComponentInChildren<HingeJoint>(true), $"Hanging prop '{hp.name}' must have a HingeJoint.");
                    if (hp.name.StartsWith("Hanging_Cloth_")) clothCount++;
                    if (hp.name == "Hanging_RightLantern") hasRightLantern = true;
                }
                Assert.AreEqual(3, clothCount, "Arena_Favela must have 3 physical clothesline HingeJoint garments.");
                Assert.IsTrue(hasRightLantern, "Arena_Favela must have Hanging_RightLantern.");

                // Dynamic props: 3 cans + 2 cones = >= 5 StageDynamicProps
                Assert.GreaterOrEqual(dynamicProps.Count, 5, "Arena_Favela must contain dynamic cans and dynamic cones.");
                int coneCount = 0;
                foreach (StageDynamicProp dp in dynamicProps)
                {
                    if (dp.name.StartsWith("Dynamic_Cone_")) coneCount++;
                }
                Assert.GreaterOrEqual(coneCount, 2, "Arena_Favela must contain at least 2 Dynamic_Cone props.");
            }
            finally
            {
                if (favelaScene.IsValid() && favelaScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(favelaScene, removeScene: true);
                }
                if (prevActive.IsValid())
                {
                    SceneManager.SetActiveScene(prevActive);
                }
            }
        }

        [Test]
        public void Phase15_2_1_ArenaFavela_PassesMaxZoomOutFloorCoverage_AndHasRightHillsideFillAndWeatherClouds()
        {
            Scene prevActive = SceneManager.GetActiveScene();
            Scene favelaScene = EditorSceneManager.OpenScene("Assets/UnDFight/Scenes/Arena_Favela.unity", OpenSceneMode.Additive);
            try
            {
                Camera mainCam = null;
                FightCameraController camCtrl = null;
                StageWeatherController weather = null;
                GameObject sandFront = null;
                GameObject cobbleStreet = null;
                GameObject sandBack = null;
                GameObject sidewalkBack = null;
                GameObject rightFill = null;

                foreach (GameObject rootGo in favelaScene.GetRootGameObjects())
                {
                    foreach (Transform t in rootGo.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "Main Camera") mainCam = t.GetComponent<Camera>();
                        if (t.name == "SandShoulder_Front") sandFront = t.gameObject;
                        if (t.name == "CobblestoneStreet") cobbleStreet = t.gameObject;
                        if (t.name == "SandShoulder_Back") sandBack = t.gameObject;
                        if (t.name == "Sidewalk_Back") sidewalkBack = t.gameObject;
                        if (t.name == ArenaFavelaUpgradeEditor.HillsideRightFillName) rightFill = t.gameObject;
                    }
                    if (camCtrl == null) camCtrl = rootGo.GetComponentInChildren<FightCameraController>(true);
                    if (weather == null) weather = rootGo.GetComponentInChildren<StageWeatherController>(true);
                }

                Assert.IsNotNull(mainCam, "Arena_Favela must have Main Camera.");
                Assert.IsNotNull(camCtrl, "Arena_Favela must have FightCameraController.");
                Assert.IsNotNull(weather, "Arena_Favela must have StageWeatherController.");
                Assert.IsNotNull(sandFront, "Arena_Favela must have SandShoulder_Front.");
                Assert.IsNotNull(cobbleStreet, "Arena_Favela must have CobblestoneStreet.");

                Renderer sandFrontR = sandFront.GetComponent<Renderer>();
                Renderer cobbleR = cobbleStreet.GetComponent<Renderer>();
                Assert.IsNotNull(sandFrontR);
                Assert.IsNotNull(cobbleR);

                // Foreground floor must cover z <= -20.0 (standard targets -22.5) and never overlap CobblestoneStreet seam at -2.60
                Assert.LessOrEqual(sandFrontR.bounds.min.z, -20.0f, "SandShoulder_Front must extend past z <= -20.0 to prevent bottom black bar.");
                Assert.GreaterOrEqual(sandFrontR.bounds.size.x, StageFramingStandards.MinFloorWidthX, "SandShoulder_Front width must meet MinFloorWidthX.");
                Assert.GreaterOrEqual(cobbleR.bounds.size.x, StageFramingStandards.MinFloorWidthX, "CobblestoneStreet width must meet MinFloorWidthX.");
                Assert.AreEqual(-2.60f, sandFrontR.bounds.max.z, 0.01f, "SandShoulder_Front must align cleanly to CobblestoneStreet seam (-2.60) with zero Z-fighting.");

                Bounds combinedFloor = sandFrontR.bounds;
                combinedFloor.Encapsulate(cobbleR.bounds);
                if (sandBack != null && sandBack.GetComponent<Renderer>() != null) combinedFloor.Encapsulate(sandBack.GetComponent<Renderer>().bounds);
                if (sidewalkBack != null && sidewalkBack.GetComponent<Renderer>() != null) combinedFloor.Encapsulate(sidewalkBack.GetComponent<Renderer>().bounds);

                // Analytical max zoom-out validation (left, center, right bottom frustum rays across 16:9 and 21:9)
                bool covered = StageFramingStandards.ValidateCameraBottomFloorCoverage(mainCam, camCtrl, combinedFloor, out string failureReason);
                Assert.IsTrue(covered, $"Arena_Favela failed bottom floor coverage at max zoom-out: {failureReason}");

                // Negative regression check: old short floor ending at z = -6.50 MUST fail validation!
                Bounds legacyShortFloor = new Bounds(new Vector3(0f, -0.05f, -2.25f), new Vector3(56f, 0.1f, 8.5f)); // z in [-6.5, +2.0]
                bool legacyPassed = StageFramingStandards.ValidateCameraBottomFloorCoverage(mainCam, camCtrl, legacyShortFloor, out _);
                Assert.IsFalse(legacyPassed, "StageFramingStandards validator must detect short foreground floors (z = -6.50).");

                // Right hillside fill exists with >= 12 terraced houses
                Assert.IsNotNull(rightFill, "FavelaHillsideRightFill must exist to fill the upper-right ramp backdrop.");
                Assert.GreaterOrEqual(rightFill.transform.childCount, 12, "FavelaHillsideRightFill must contain at least 12 terraced hillside houses.");

                // Weather clouds & shoreline lights wired cleanly to StageWeatherController
                bool hasDayClouds = false;
                foreach (GameObject go in weather.DayOnlyObjects)
                {
                    if (go != null && go.name == $"{ArenaFavelaUpgradeEditor.WeatherCloudsPrefix}_DayClouds") hasDayClouds = true;
                }
                Assert.IsTrue(hasDayClouds, "DayOnlyObjects must include Day weather clouds.");

                bool hasClearNightClouds = false;
                foreach (GameObject go in weather.ClearNightOnlyObjects)
                {
                    if (go != null && go.name == $"{ArenaFavelaUpgradeEditor.WeatherCloudsPrefix}_ClearNightClouds") hasClearNightClouds = true;
                }
                Assert.IsTrue(hasClearNightClouds, "ClearNightOnlyObjects must include ClearNight weather clouds.");

                bool hasRainClouds = false;
                foreach (GameObject go in weather.RainOnlyObjects)
                {
                    if (go != null && go.name == $"{ArenaFavelaUpgradeEditor.WeatherCloudsPrefix}_RainClouds") hasRainClouds = true;
                }
                Assert.IsTrue(hasRainClouds, "RainOnlyObjects must include RainyNight storm clouds.");

                bool hasShorelineLights = false;
                GameObject hillsideWinGo = null;
                foreach (GameObject go in weather.NightOnlyObjects)
                {
                    if (go == null) continue;
                    if (go.name == ArenaFavelaUpgradeEditor.BayShorelineLightsName) hasShorelineLights = true;
                    if (go.name == ArenaFavelaUpgradeEditor.HillsideNightWindowsName) hillsideWinGo = go;
                }
                Assert.IsTrue(hasShorelineLights, "NightOnlyObjects must include FavelaBayShorelineLights.");
                Assert.IsNotNull(hillsideWinGo);
                Assert.GreaterOrEqual(hillsideWinGo.transform.childCount, 30, "FavelaNightWindows_Hillside must include original + right-side lit windows (>= 30).");
            }
            finally
            {
                if (favelaScene.IsValid() && favelaScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(favelaScene, removeScene: true);
                }
                if (prevActive.IsValid())
                {
                    SceneManager.SetActiveScene(prevActive);
                }
            }
        }

        [Test]
        public void Phase15_3_ArenaUnDF_MetadataBuildSettingsFramingPhysicsAndThreeWeathers_AreValid()
        {
            // 1. Original concept art must still exist intact (never moved/deleted)
            Assert.IsTrue(System.IO.File.Exists(ArenaUnDFBuilderEditor.ConceptSourcePath), "Original concept art in Docs/Referencias must remain untouched.");

            // 2. StageData_UnDF metadata, preview Sprite, StageRoster & EditorBuildSettings
            var undfData = AssetDatabase.LoadAssetAtPath<StageData>(ArenaUnDFBuilderEditor.UnDFStageDataPath);
            Assert.IsNotNull(undfData, "StageData_UnDF.asset must exist.");
            Assert.AreEqual("undf_campus", undfData.stageId);
            Assert.AreEqual("FRENTE DA UnDF", undfData.displayName);
            Assert.AreEqual("Arena_UnDF", undfData.sceneName);
            Assert.IsTrue(undfData.supportsWeatherVariants);
            Assert.AreEqual("DIA", undfData.dayVariantLabel);
            Assert.AreEqual(StageWeatherVariant.Day, undfData.defaultWeather);
            Assert.IsNotNull(undfData.previewImage, "StageData_UnDF must reference Stage_UnDF_Preview Sprite.");

            var stageRoster = AssetDatabase.LoadAssetAtPath<StageRoster>(ArenaUnDFBuilderEditor.StageRosterPath);
            Assert.IsNotNull(stageRoster);
            Assert.GreaterOrEqual(stageRoster.Count, 3, "StageRoster must list at least Favela, UnDF, and Training.");
            bool inRoster = false;
            for (int i = 0; i < stageRoster.Count; i++)
            {
                if (stageRoster.GetStage(i) == undfData) inRoster = true;
            }
            Assert.IsTrue(inRoster, "StageData_UnDF must be registered in StageRoster.asset.");

            bool inBuildSettings = false;
            foreach (EditorBuildSettingsScene bs in EditorBuildSettings.scenes)
            {
                if (bs.enabled && bs.path == ArenaUnDFBuilderEditor.ArenaUnDFScenePath)
                {
                    inBuildSettings = true;
                    break;
                }
            }
            Assert.IsTrue(inBuildSettings, "Arena_UnDF.unity must be enabled in EditorBuildSettings.");

            // 3. Weather label check across Favela ("PÔR DO SOL"), UnDF ("DIA"), and Training ("CLIMA FIXO")
            var favelaData = AssetDatabase.LoadAssetAtPath<StageData>(StagePhysicsSetupEditor.FavelaStageDataPath);
            var trainingData = AssetDatabase.LoadAssetAtPath<StageData>(StagePhysicsSetupEditor.TrainingStageDataPath);
            Assert.AreEqual("PÔR DO SOL", GameSessionState.GetWeatherLabel(StageWeatherVariant.Day, favelaData));
            Assert.AreEqual("DIA", GameSessionState.GetWeatherLabel(StageWeatherVariant.Day, undfData));
            Assert.AreEqual("NOITE", GameSessionState.GetWeatherLabel(StageWeatherVariant.Night, undfData));
            Assert.AreEqual("NOITE CHUVOSA", GameSessionState.GetWeatherLabel(StageWeatherVariant.RainyNight, undfData));
            Assert.IsFalse(trainingData.supportsWeatherVariants);

            // 4. Validate Arena_UnDF.unity framing, Physics v2 props, and 3 weather presets
            Scene prevActive = SceneManager.GetActiveScene();
            Scene undfScene = EditorSceneManager.OpenScene(ArenaUnDFBuilderEditor.ArenaUnDFScenePath, OpenSceneMode.Additive);
            try
            {
                Camera mainCam = null;
                FightCameraController camCtrl = null;
                StageWeatherController weather = null;
                StageWindController wind = null;
                StageInteractableManager stageMgr = null;
                GameObject asphaltMain = null;
                GameObject asphaltFront = null;
                GameObject sidewalk = null;

                var hangingProps = new List<StageHangingProp>();
                var breakableProps = new List<StageBreakableProp>();
                var throwableProps = new List<StageThrowableProp>();
                var dynamicProps = new List<StageDynamicProp>();

                foreach (GameObject rootGo in undfScene.GetRootGameObjects())
                {
                    foreach (Transform t in rootGo.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "Main Camera") mainCam = t.GetComponent<Camera>();
                        if (t.name == "ParkingAsphalt_Main") asphaltMain = t.gameObject;
                        if (t.name == "ParkingAsphalt_Front") asphaltFront = t.gameObject;
                        if (t.name == "Sidewalk_Campus") sidewalk = t.gameObject;
                    }
                    if (camCtrl == null) camCtrl = rootGo.GetComponentInChildren<FightCameraController>(true);
                    if (weather == null) weather = rootGo.GetComponentInChildren<StageWeatherController>(true);
                    if (wind == null) wind = rootGo.GetComponentInChildren<StageWindController>(true);
                    if (stageMgr == null) stageMgr = rootGo.GetComponentInChildren<StageInteractableManager>(true);
                    hangingProps.AddRange(rootGo.GetComponentsInChildren<StageHangingProp>(true));
                    breakableProps.AddRange(rootGo.GetComponentsInChildren<StageBreakableProp>(true));
                    throwableProps.AddRange(rootGo.GetComponentsInChildren<StageThrowableProp>(true));
                    dynamicProps.AddRange(rootGo.GetComponentsInChildren<StageDynamicProp>(true));
                }

                Assert.IsNotNull(mainCam, "Arena_UnDF must have Main Camera.");
                Assert.IsNotNull(camCtrl, "Arena_UnDF must have FightCameraController.");
                Assert.IsNotNull(weather, "Arena_UnDF must have StageWeatherController.");
                Assert.IsNotNull(wind, "Arena_UnDF must have StageWindController.");
                Assert.IsNotNull(stageMgr, "Arena_UnDF must have StageInteractableManager.");
                Assert.IsNotNull(asphaltMain, "Arena_UnDF must have ParkingAsphalt_Main.");
                Assert.IsNotNull(asphaltFront, "Arena_UnDF must have ParkingAsphalt_Front.");

                // Floor coverage validation (zero black bar at 12m max zoom-out across 16:9 & 21:9)
                Renderer frontR = asphaltFront.GetComponent<Renderer>();
                Renderer mainR = asphaltMain.GetComponent<Renderer>();
                Assert.LessOrEqual(frontR.bounds.min.z, StageFramingStandards.MinForegroundFloorZ, "ParkingAsphalt_Front must reach z <= -22.0m.");
                Assert.AreEqual(-2.60f, frontR.bounds.max.z, 0.01f, "ParkingAsphalt_Front seam must match ParkingAsphalt_Main (-2.60) with zero Z-fighting.");

                Bounds combinedFloor = frontR.bounds;
                combinedFloor.Encapsulate(mainR.bounds);
                if (sidewalk != null && sidewalk.GetComponent<Renderer>() != null) combinedFloor.Encapsulate(sidewalk.GetComponent<Renderer>().bounds);

                bool covered = StageFramingStandards.ValidateCameraBottomFloorCoverage(mainCam, camCtrl, combinedFloor, out string reason);
                Assert.IsTrue(covered, $"Arena_UnDF failed bottom floor coverage validation: {reason}");

                // Physics v2 props check: 3 flags + 1 hanging banner, >= 2 breakables, >= 2 throwables, >= 4 dynamic props
                int flagCount = 0;
                bool hasBanner = false;
                foreach (StageHangingProp hp in hangingProps)
                {
                    Assert.IsNotNull(hp.GetComponentInChildren<HingeJoint>(true), $"Hanging prop '{hp.name}' must have HingeJoint.");
                    Assert.IsNotNull(hp.GetComponentInChildren<StageWindAffectable>(true), $"Hanging prop '{hp.name}' must have StageWindAffectable.");
                    if (hp.name.StartsWith("Hanging_Flag_")) flagCount++;
                    if (hp.name == "Hanging_AcademicBanner") hasBanner = true;
                }
                Assert.AreEqual(3, flagCount, "Arena_UnDF must have 3 physical HingeJoint flags (DF, Brazil, UnDF).");
                Assert.IsTrue(hasBanner, "Arena_UnDF must have Hanging_AcademicBanner.");
                Assert.GreaterOrEqual(breakableProps.Count, 2, "Arena_UnDF must have at least 2 StageBreakableProp instances.");
                Assert.GreaterOrEqual(throwableProps.Count, 2, "Arena_UnDF must have at least 2 StageThrowableProp instances.");
                Assert.GreaterOrEqual(dynamicProps.Count, 4, "Arena_UnDF must have at least 4 loose StageDynamicProp instances.");

                // Weather arrays, night lights LightShadows.None, >= 20 lit windows, >= 6 puddles, wetSurfaceRenderers
                Assert.GreaterOrEqual(weather.DayOnlyObjects.Length, 2, "Arena_UnDF DayOnlyObjects must include Sun & DayClouds.");
                Assert.GreaterOrEqual(weather.ClearNightOnlyObjects.Length, 2, "Arena_UnDF ClearNightOnlyObjects must include Moon/Stars & NightClouds.");
                Assert.GreaterOrEqual(weather.NightOnlyObjects.Length, 1, "Arena_UnDF NightOnlyObjects must include NightLightsAndWindows.");
                Assert.GreaterOrEqual(weather.RainOnlyObjects.Length, 2, "Arena_UnDF RainOnlyObjects must include RainPuddles & StormClouds.");
                Assert.GreaterOrEqual(weather.WetSurfaceRenderers.Length, 4, "Arena_UnDF WetSurfaceRenderers must cover asphalt, curb, and sidewalk.");

                GameObject nightGroup = weather.NightOnlyObjects[0];
                foreach (Light l in nightGroup.GetComponentsInChildren<Light>(true))
                {
                    Assert.AreEqual(LightShadows.None, l.shadows, $"Night light '{l.name}' in Arena_UnDF must use LightShadows.None.");
                }
                Transform litWindows = nightGroup.transform.Find("UnDF_LitClassroomWindows");
                Assert.IsNotNull(litWindows);
                Assert.GreaterOrEqual(litWindows.childCount, 20, "Arena_UnDF must have at least 20 lit classroom windows at night.");

                GameObject puddlesObj = null;
                foreach (GameObject ro in weather.RainOnlyObjects)
                {
                    if (ro != null && ro.name == ArenaUnDFBuilderEditor.RainPuddlesName) puddlesObj = ro;
                }
                Assert.IsNotNull(puddlesObj);
                Assert.GreaterOrEqual(puddlesObj.transform.childCount, 6, "Arena_UnDF must have at least 6 reflective rain puddles.");

                // Switch cleanly through Day -> Night -> RainyNight and back to Day
                weather.ApplyWeather(StageWeatherVariant.Day);
                Assert.IsTrue(weather.DayOnlyObjects[0].activeSelf);
                Assert.IsFalse(weather.NightOnlyObjects[0].activeSelf);
                Assert.IsFalse(weather.ClearNightOnlyObjects[0].activeSelf);
                Assert.IsFalse(weather.RainOnlyObjects[0].activeSelf);
                Assert.LessOrEqual(wind.BaseWindSpeed + wind.GustAmplitude, 1.05f);

                weather.ApplyWeather(StageWeatherVariant.Night);
                Assert.IsFalse(weather.DayOnlyObjects[0].activeSelf);
                Assert.IsTrue(weather.NightOnlyObjects[0].activeSelf);
                Assert.IsTrue(weather.ClearNightOnlyObjects[0].activeSelf);
                Assert.IsFalse(weather.RainOnlyObjects[0].activeSelf);
                Assert.LessOrEqual(wind.BaseWindSpeed + wind.GustAmplitude, 1.85f);

                weather.ApplyWeather(StageWeatherVariant.RainyNight);
                Assert.IsFalse(weather.DayOnlyObjects[0].activeSelf);
                Assert.IsTrue(weather.NightOnlyObjects[0].activeSelf);
                Assert.IsFalse(weather.ClearNightOnlyObjects[0].activeSelf);
                Assert.IsTrue(weather.RainOnlyObjects[0].activeSelf);
                Assert.Greater(wind.BaseWindSpeed, 5.0f);
                Assert.IsTrue(weather.IsWetSurfaceActive);
            }
            finally
            {
                if (undfScene.IsValid() && undfScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(undfScene, removeScene: true);
                }
                if (prevActive.IsValid())
                {
                    SceneManager.SetActiveScene(prevActive);
                }
            }
        }
    }
}
