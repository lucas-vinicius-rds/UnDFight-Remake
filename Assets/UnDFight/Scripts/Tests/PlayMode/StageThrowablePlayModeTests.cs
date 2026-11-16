using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnDFight.Tests
{
    public class StageThrowablePlayModeTests
    {
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            GameSessionState.Reset();
            StagePhysicsLayers.ApplyCollisionIsolation();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }
            _created.Clear();
            GameSessionState.Reset();
        }

        private T Track<T>(T obj) where T : Object
        {
            _created.Add(obj);
            return obj;
        }

        [UnityTest]
        public IEnumerator RealPhysX_OnTriggerEnter_HitsDefenderHurtbox_AndResetsOnRoundStart()
        {
            var data = Track(ScriptableObject.CreateInstance<FighterData>());
            data.fighterName = "PlayModeTester";
            data.heightMeters = 1.80f;
            data.weightKg = 80f;
            data.maxHealth = 1000;

            var simGo = Track(new GameObject("PM_FightSim"));
            var sim = simGo.AddComponent<FightSimulation>();
            var match = simGo.AddComponent<MatchManager>();
            var stageMgr = simGo.AddComponent<StageInteractableManager>();

            var p1 = CreateFighter("PM_P1", data, -1.8f);
            var p2 = CreateFighter("PM_P2", data, 1.4f);
            sim.SetParticipants(p1, p2);
            match.InitializeMatch(sim, trainingModeOverride: false);
            match.SkipIntroToFighting();
            stageMgr.Configure(sim, match);

            var chairGo = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            chairGo.name = "PM_Chair";
            chairGo.transform.position = new Vector3(-1.8f, 0f, 1.2f);
            chairGo.transform.localScale = new Vector3(0.45f, 0.55f, 0.45f);
            var chair = chairGo.AddComponent<StageThrowableProp>();
            chair.ConfigureForRuntime(85, knockdown: true, shatterOnFighter: true, shatterOnStage: false, StagePropMaterialType.Plastic);
            stageMgr.RefreshDiscoveredProps();

            // Yield once so Awake/OnEnable bind in Play Mode
            yield return null;

            p1.InputHandler.SetSimulatedInput(new FighterInputState { HoldBlock = true }, true);
            p1.InputHandler.BufferButton(FighterInputButton.LightPunch);

            float elapsed = 0f;
            while (chair.State != StageThrowableState.Broken && elapsed < 1.5f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(StageThrowableState.Broken, chair.State, "Real Play Mode PhysX OnTriggerEnter should detect P2 CapsuleCollider and shatter the chair.");
            Assert.AreEqual(p1.MaxHealth, p1.CurrentHealth, "Thrower P1 must not take damage from own thrown object.");
            Assert.AreEqual(85, p2.MaxHealth - p2.CurrentHealth, "Defender P2 must receive exact throwable damage via OnTriggerEnter.");

            match.StartRound();
            yield return null;

            Assert.AreEqual(StageThrowableState.Ready, chair.State);
            Assert.IsTrue(chair.Body.isKinematic);
            Assert.IsFalse(chair.Body.useGravity);
        }

        private FighterController CreateFighter(string name, FighterData data, float startX)
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

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            go.AddComponent<PlayerInputHandler>();
            var fc = go.AddComponent<FighterController>();
            fc.SetFighterData(data);
            return fc;
        }
    }
}
