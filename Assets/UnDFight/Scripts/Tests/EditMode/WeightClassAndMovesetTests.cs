using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnDFight.Tests
{
    public class WeightClassAndMovesetTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
                }
            }
            _createdObjects.Clear();
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            _createdObjects.Add(obj);
            return obj;
        }

        [Test]
        public void GetWeightClass_Boundaries_ClassifyLightMediumAndHeavy()
        {
            Assert.AreEqual(WeightClass.Light, FighterScaling.GetWeightClass(62f));
            Assert.AreEqual(WeightClass.Light, FighterScaling.GetWeightClass(75f));
            Assert.AreEqual(WeightClass.Medium, FighterScaling.GetWeightClass(75.1f));
            Assert.AreEqual(WeightClass.Medium, FighterScaling.GetWeightClass(87f));
            Assert.AreEqual(WeightClass.Medium, FighterScaling.GetWeightClass(90f));
            Assert.AreEqual(WeightClass.Heavy, FighterScaling.GetWeightClass(90.1f));
            Assert.AreEqual(WeightClass.Heavy, FighterScaling.GetWeightClass(92f));

            Assert.AreEqual("LEVE", FighterScaling.GetWeightClassLabel(WeightClass.Light));
            Assert.AreEqual("MÉDIO", FighterScaling.GetWeightClassLabel(WeightClass.Medium));
            Assert.AreEqual("PESADO", FighterScaling.GetWeightClassLabel(WeightClass.Heavy));
            Assert.AreEqual("BRUTAMONTES", FighterScaling.GetArchetypeLabel(FightingArchetype.HeavyBrawler));
            Assert.AreEqual("BRIGUENTO DE RUA", FighterScaling.GetArchetypeLabel(FightingArchetype.StreetScrappy));
            Assert.AreEqual("ÁGIL", FighterScaling.GetArchetypeLabel(FightingArchetype.AgileStriker));
            Assert.AreEqual("TÉCNICO", FighterScaling.GetArchetypeLabel(FightingArchetype.TechnicalStriker));
        }

        [Test]
        public void FighterRoster_ContainsFourPlayableFightersInExpectedOrder()
        {
            var roster = AssetDatabase.LoadAssetAtPath<FighterRoster>("Assets/UnDFight/Data/FighterRoster.asset");
            Assert.IsNotNull(roster);
            Assert.AreEqual(4, roster.Count);
            Assert.AreEqual("Lucas", roster.GetFighter(0).fighterName);
            Assert.AreEqual("Tiago", roster.GetFighter(1).fighterName);
            Assert.AreEqual("Jompes", roster.GetFighter(2).fighterName);
            Assert.AreEqual("Rochard", roster.GetFighter(3).fighterName);
        }

        [Test]
        public void FighterAssets_JompesAndRochard_HaveLightMovesets52BonesAndExactScaling()
        {
            var jompes = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Jompes.asset");
            var rochard = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Rochard.asset");
            Assert.IsNotNull(jompes);
            Assert.IsNotNull(rochard);

            Assert.AreEqual(1.65f, jompes.heightMeters, 0.001f);
            Assert.AreEqual(70f, jompes.weightKg, 0.01f);
            Assert.AreEqual(WeightClass.Light, jompes.WeightCategory);
            Assert.AreEqual(FightingArchetype.TechnicalStriker, jompes.archetype);
            Assert.AreEqual(1.65f / 1.81f, jompes.ModelScale, 1e-5f);
            Assert.AreEqual(1.65f / 1.81f, jompes.HurtboxScale, 1e-5f);
            Assert.AreEqual(1.0f, jompes.VisualScale, 1e-5f);

            Assert.AreEqual(1.75f, rochard.heightMeters, 0.001f);
            Assert.AreEqual(67f, rochard.weightKg, 0.01f);
            Assert.AreEqual(WeightClass.Light, rochard.WeightCategory);
            Assert.AreEqual(FightingArchetype.AgileStriker, rochard.archetype);
            Assert.AreEqual(1.75f / 1.81f, rochard.ModelScale, 1e-5f);
            Assert.AreEqual(1.75f / 1.81f, rochard.HurtboxScale, 1e-5f);
            Assert.AreEqual(1.0f, rochard.VisualScale, 1e-5f);

            Assert.AreNotSame(jompes.lightPunch, rochard.lightPunch);
            Assert.AreNotSame(jompes.heavyPunch, rochard.heavyPunch);
            Assert.AreNotSame(jompes.lightKick, rochard.lightKick);
            Assert.AreNotSame(jompes.heavyKick, rochard.heavyKick);
            Assert.AreNotSame(jompes.specialMove, rochard.specialMove);

            AssertLightTicks(jompes);
            AssertLightTicks(rochard);
            Assert.AreEqual(24, jompes.specialMove.TotalTicks);
            Assert.AreEqual(22, rochard.specialMove.TotalTicks);

            string jompesFbx = "Assets/UnDFight/Art/Models/Jompes/Jompes.fbx";
            string rochardFbx = "Assets/UnDFight/Art/Models/Rochard/Rochard.fbx";
            AssertMovesetMatchesFbx(jompes, jompesFbx, "AOC_Jompes");
            AssertMovesetMatchesFbx(rochard, rochardFbx, "AOC_Rochard");

            Assert.AreEqual(52, ((ModelImporter)AssetImporter.GetAtPath(jompesFbx)).humanDescription.human.Length);
            Assert.AreEqual(52, ((ModelImporter)AssetImporter.GetAtPath(rochardFbx)).humanDescription.human.Length);
        }

        private static void AssertLightTicks(FighterData data)
        {
            Assert.AreEqual(13, data.lightPunch.TotalTicks);
            Assert.AreEqual(21, data.heavyPunch.TotalTicks);
            Assert.AreEqual(16, data.lightKick.TotalTicks);
            Assert.AreEqual(27, data.heavyKick.TotalTicks);
        }

        [Test]
        public void FighterAssets_LucasAndTiago_HaveDistinctMovesetsMatching60FpsFbxClips()
        {
            var lucas = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Lucas.asset");
            var tiago = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Tiago.asset");

            Assert.IsNotNull(lucas);
            Assert.IsNotNull(tiago);

            Assert.AreEqual(92f, lucas.weightKg, 0.01f);
            Assert.AreEqual(WeightClass.Heavy, lucas.WeightCategory);
            Assert.AreEqual(FightingArchetype.HeavyBrawler, lucas.archetype);

            Assert.AreEqual(87f, tiago.weightKg, 0.01f);
            Assert.AreEqual(WeightClass.Medium, tiago.WeightCategory);
            Assert.AreEqual(FightingArchetype.StreetScrappy, tiago.archetype);

            // Distinct MoveData assets
            Assert.AreNotSame(lucas.lightPunch, tiago.lightPunch);
            Assert.AreNotSame(lucas.heavyPunch, tiago.heavyPunch);
            Assert.AreNotSame(lucas.lightKick, tiago.lightKick);
            Assert.AreNotSame(lucas.heavyKick, tiago.heavyKick);
            Assert.AreNotSame(lucas.specialMove, tiago.specialMove);

            AssertMovesetMatchesFbx(lucas, "Assets/UnDFight/Art/Models/Lucas/Lucas.fbx", "AOC_Lucas");
            AssertMovesetMatchesFbx(tiago, "Assets/UnDFight/Art/Models/Tiago/Tiago.fbx", "AOC_Tiago");
        }

        private static void AssertMovesetMatchesFbx(FighterData data, string fbxPath, string expectedControllerName)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            Assert.IsNotNull(importer, $"Missing ModelImporter at {fbxPath}");
            foreach (ModelImporterClipAnimation clipMeta in importer.clipAnimations)
            {
                Assert.IsFalse(clipMeta.heightFromFeet, $"{fbxPath} clip {clipMeta.name} must keep heightFromFeet=false");
                Assert.IsTrue(clipMeta.lockRootHeightY, $"{fbxPath} clip {clipMeta.name} must lockRootHeightY=true");
            }

            var clips = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                {
                    clips[clip.name] = clip;
                }
            }

            Assert.AreEqual(16, clips.Count, $"Expected 16 clips in {fbxPath}");

            foreach (AttackType attackType in new[]
                     { AttackType.LightPunch, AttackType.HeavyPunch, AttackType.LightKick, AttackType.HeavyKick, AttackType.Special })
            {
                MoveData move = data.GetMoveData(attackType);
                Assert.IsNotNull(move, $"{data.fighterName} missing {attackType}");
                Assert.Greater(move.TotalTicks, 0);
                Assert.Greater(move.damage, 0);
                Assert.IsTrue(clips.TryGetValue(attackType.ToString(), out AnimationClip clip), $"Missing clip {attackType} in {fbxPath}");
                Assert.AreEqual(60, Mathf.RoundToInt(clip.frameRate), $"Clip {clip.name} must be 60 fps");
                int clipFrames = Mathf.RoundToInt(clip.length * 60f);
                Assert.AreEqual(move.TotalTicks, clipFrames, $"{data.fighterName} {attackType} MoveData.TotalTicks must equal FBX clip frame length");
            }

            Assert.IsNotNull(data.prefab);
            Animator anim = data.prefab.GetComponentInChildren<Animator>(true);
            Assert.IsNotNull(anim);
            Assert.IsInstanceOf<AnimatorOverrideController>(anim.runtimeAnimatorController);
            Assert.AreEqual(expectedControllerName, anim.runtimeAnimatorController.name);
        }

        [Test]
        public void HeavyPunch_LucasDealsMoreDamageKnockbackAndLongerDurationThanTiago()
        {
            var lucasData = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Lucas.asset");
            var tiagoData = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Tiago.asset");
            Assert.IsNotNull(lucasData);
            Assert.IsNotNull(tiagoData);

            Assert.Greater(lucasData.heavyPunch.TotalTicks, tiagoData.heavyPunch.TotalTicks);
            Assert.Greater(lucasData.heavyPunch.damage, tiagoData.heavyPunch.damage);
            Assert.Greater(lucasData.heavyPunch.knockback, tiagoData.heavyPunch.knockback);

            // Simulate both fighters' HeavyPunch against identical Medium (87 kg) defender
            SimulateHeavyPunch(lucasData, tiagoData, out int lucasDmg, out float lucasKb, out int lucasTotalTicks);
            SimulateHeavyPunch(tiagoData, tiagoData, out int tiagoDmg, out float tiagoKb, out int tiagoTotalTicks);

            Assert.AreEqual(140, lucasDmg);
            Assert.AreEqual(108, tiagoDmg);
            Assert.Greater(lucasDmg, tiagoDmg);
            Assert.Greater(lucasKb, tiagoKb);
            Assert.Greater(lucasTotalTicks, tiagoTotalTicks);
        }

        private void SimulateHeavyPunch(
            FighterData attackerData,
            FighterData defenderData,
            out int damageDealt,
            out float knockbackDistance,
            out int totalTicks)
        {
            var simGo = Track(new GameObject("Test_WeightSim"));
            var sim = simGo.AddComponent<FightSimulation>();

            var p1Go = Track(new GameObject("Attacker"));
            p1Go.transform.position = new Vector3(-0.55f, 0f, 0f);
            p1Go.AddComponent<PlayerInputHandler>();
            var p1 = p1Go.AddComponent<FighterController>();
            p1.SetFighterData(attackerData);

            var p2Go = Track(new GameObject("Defender"));
            p2Go.transform.position = new Vector3(0.55f, 0f, 0f);
            p2Go.AddComponent<PlayerInputHandler>();
            var p2 = p2Go.AddComponent<FighterController>();
            p2.SetFighterData(defenderData);

            sim.SetParticipants(p1, p2);
            p1.InputHandler.SetSimulatedInput(default, true);
            p2.InputHandler.SetSimulatedInput(default, true);

            MoveData hp = attackerData.heavyPunch;
            totalTicks = hp.TotalTicks;

            float defStartX = p2.SimPosition.x;
            p1.InputHandler.BufferButton(FighterInputButton.HeavyPunch);

            // Step until first active tick + hitstop + full hitstun
            sim.StepSimulationTicks(1 + hp.FirstActiveTick + hp.hitstopTicks + hp.hitstunTicks + 2);

            damageDealt = p2.MaxHealth - p2.CurrentHealth;
            knockbackDistance = p2.SimPosition.x - defStartX;
        }
    }
}
