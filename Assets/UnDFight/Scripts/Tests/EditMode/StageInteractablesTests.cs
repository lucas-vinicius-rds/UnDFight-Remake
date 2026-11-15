using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnDFight.Tests
{
    public class StageInteractablesTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();

        [SetUp]
        public void SetUp()
        {
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

        private FighterController CreateFighter(string name, FighterData data, float startX)
        {
            var go = Track(new GameObject(name));
            go.transform.position = new Vector3(startX, 0f, 0f);
            var col = go.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.radius = 0.36f;
            col.height = data.heightMeters;
            col.center = new Vector3(0f, data.heightMeters * 0.5f, 0f);
            go.AddComponent<PlayerInputHandler>();
            var fc = go.AddComponent<FighterController>();
            fc.SetFighterData(data);
            return fc;
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

            Assert.IsFalse(Physics.GetIgnoreLayerCollision(stageDebris, stagePhysics), "StageDebris must collide with StagePhysics.");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(stageDebris, fighterLayer), "StageDebris must NEVER collide with FighterHurtbox.");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(stageDebris, 0), "StageDebris must ignore Default layer.");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(fighterLayer, stagePhysics), "FighterHurtbox must ignore StagePhysics.");
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
        public void FightersAndSimulation_Remain60HzDeterministicWithoutFighterRigidbodies()
        {
            CreateTestProp("Test_DetCrate", new Vector3(0.4f, 0f, 1.2f), integrity: 2, radiusX: 1.5f);
            var (sim, _, p1, p2, _, _, _) = CreateStageCombatRig(withMatchManager: true);

            Assert.IsNull(p1.GetComponent<Rigidbody>(), "Fighters must not use dynamic PhysX Rigidbodies.");
            Assert.IsNull(p2.GetComponent<Rigidbody>(), "Fighters must not use dynamic PhysX Rigidbodies.");
            Assert.AreEqual(60, FightSimulation.TickRate);
            Assert.AreEqual(1f / 60f, FightSimulation.TickDeltaTime, 1e-6f);

            p1.SetSimPositionX(-0.45f);
            p2.SetSimPositionX(0.55f);
            sim.ForceUpdateFacing();
            p1.InputHandler.SetSimulatedInput(default);
            p1.InputHandler.BufferButton(FighterInputButton.HeavyPunch);
            sim.StepSimulationTicks(15);

            Assert.AreEqual(0f, p1.transform.position.z, 1e-5f);
            Assert.AreEqual(0f, p2.transform.position.z, 1e-5f);
            Assert.Less(p2.CurrentHealth, p2.MaxHealth);
        }

        [Test]
        public void StagePropsPrefabsAndAudio_AreValid()
        {
            string[] prefabPaths =
            {
                "Assets/UnDFight/Prefabs/StageProps/Prop_BreakableCrate.prefab",
                "Assets/UnDFight/Prefabs/StageProps/Prop_BreakableCrateStack.prefab",
                "Assets/UnDFight/Prefabs/StageProps/Prop_BreakableTable.prefab"
            };

            foreach (string path in prefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, $"Missing reusable stage prop prefab: {path}");
                var prop = prefab.GetComponent<StageBreakableProp>();
                Assert.IsNotNull(prop, $"{path} must carry StageBreakableProp.");
                var fragments = prefab.GetComponentsInChildren<StageDebrisFragment>(true);
                Assert.GreaterOrEqual(fragments.Length, 5, $"{path} must include at least 5 pooled debris fragments.");
                Assert.LessOrEqual(fragments.Length, 8, $"{path} must include at most 8 pooled debris fragments.");
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
                Assert.IsNull(fPrefab.GetComponent<Rigidbody>(), $"{fPath} must not carry a PhysX Rigidbody.");
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
    }
}
