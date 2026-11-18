using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
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
        public void FighterRoster_ContainsSevenPlayableFightersInExpectedOrder()
        {
            var roster = AssetDatabase.LoadAssetAtPath<FighterRoster>("Assets/UnDFight/Data/FighterRoster.asset");
            Assert.IsNotNull(roster);
            Assert.AreEqual(7, roster.Count);
            Assert.AreEqual("Lucas", roster.GetFighter(0).fighterName);
            Assert.AreEqual("Tiago", roster.GetFighter(1).fighterName);
            Assert.AreEqual("Jompes", roster.GetFighter(2).fighterName);
            Assert.AreEqual("Rochard", roster.GetFighter(3).fighterName);
            Assert.AreEqual("Isaac", roster.GetFighter(4).fighterName);
            Assert.AreEqual("Enomoto", roster.GetFighter(5).fighterName);
            Assert.AreEqual("Gabubu", roster.GetFighter(6).fighterName);
        }

        [Test]
        public void FighterAssets_IsaacEnomotoAndGabubu_HaveCompleteMovesets52Bones24ClipsAndHurtboxPhysics()
        {
            var isaac = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Isaac.asset");
            var enomoto = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Enomoto.asset");
            var gabubu = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Gabubu.asset");
            Assert.IsNotNull(isaac, "Missing FighterData_Isaac");
            Assert.IsNotNull(enomoto, "Missing FighterData_Enomoto");
            Assert.IsNotNull(gabubu, "Missing FighterData_Gabubu");

            // Isaac: 1.73 m / 73 kg / Light / AgileStriker — Punho Esmeralda
            Assert.AreEqual(1.73f, isaac.heightMeters, 0.001f);
            Assert.AreEqual(73f, isaac.weightKg, 0.01f);
            Assert.AreEqual(WeightClass.Light, isaac.WeightCategory);
            Assert.AreEqual(FightingArchetype.AgileStriker, isaac.archetype);
            Assert.IsTrue(isaac.fbxHeightPrebaked);
            Assert.AreEqual(1.0f, isaac.VisualScale, 1e-5f);
            AssertLightTicks(isaac);
            Assert.AreEqual("Punho Esmeralda", isaac.specialMove.moveName);
            Assert.AreEqual(SpecialMechanicType.AntiAirSpin, isaac.specialMove.specialMechanic);
            Assert.IsTrue(isaac.specialMove.antiAirInvulnerable);
            Assert.AreEqual(4.4f, isaac.specialMove.lungeSpeed, 0.01f);
            Assert.AreEqual(14, isaac.specialMove.superFreezeTicks);
            Assert.AreEqual(22, isaac.specialMove.TotalTicks);

            // Enomoto: 1.77 m / 80 kg / Medium / TechnicalStriker — Estocada do Cavalheiro
            Assert.AreEqual(1.77f, enomoto.heightMeters, 0.001f);
            Assert.AreEqual(80f, enomoto.weightKg, 0.01f);
            Assert.AreEqual(WeightClass.Medium, enomoto.WeightCategory);
            Assert.AreEqual(FightingArchetype.TechnicalStriker, enomoto.archetype);
            Assert.IsTrue(enomoto.fbxHeightPrebaked);
            Assert.AreEqual(1.0f, enomoto.VisualScale, 1e-5f);
            AssertMediumTicks(enomoto);
            Assert.AreEqual("Estocada do Cavalheiro", enomoto.specialMove.moveName);
            Assert.AreEqual(SpecialMechanicType.ProjectileShot, enomoto.specialMove.specialMechanic);
            Assert.IsTrue(enomoto.specialMove.spawnsProjectile);
            Assert.AreEqual(10.2f, enomoto.specialMove.projectileSpeed, 0.01f);
            Assert.AreEqual(14, enomoto.specialMove.superFreezeTicks);
            Assert.AreEqual(28, enomoto.specialMove.TotalTicks);

            // Gabubu: 1.62 m / 67 kg / Light / StreetScrappy — Riff Destruidor
            Assert.AreEqual(1.62f, gabubu.heightMeters, 0.001f);
            Assert.AreEqual(67f, gabubu.weightKg, 0.01f);
            Assert.AreEqual(WeightClass.Light, gabubu.WeightCategory);
            Assert.AreEqual(FightingArchetype.StreetScrappy, gabubu.archetype);
            Assert.IsTrue(gabubu.fbxHeightPrebaked);
            Assert.AreEqual(1.0f, gabubu.VisualScale, 1e-5f);
            AssertLightTicks(gabubu);
            Assert.AreEqual("Riff Destruidor", gabubu.specialMove.moveName);
            Assert.AreEqual(SpecialMechanicType.MultiHitRush, gabubu.specialMove.specialMechanic);
            Assert.Greater(gabubu.specialMove.secondActiveStartTick, gabubu.specialMove.LastActiveTick);
            Assert.GreaterOrEqual(gabubu.specialMove.secondActiveEndTick, gabubu.specialMove.secondActiveStartTick);
            Assert.Greater(gabubu.specialMove.secondHitDamage, 0);
            Assert.AreEqual(3.9f, gabubu.specialMove.lungeSpeed, 0.01f);
            Assert.AreEqual(14, gabubu.specialMove.superFreezeTicks);
            Assert.AreEqual(24, gabubu.specialMove.TotalTicks);

            // Verify 52 Humanoid bones, 24 60fps clips (heightFromFeet=false), AOC_*, and PhysX FighterHurtbox setup
            string isaacFbx = "Assets/UnDFight/Art/Models/Isaac/Isaac.fbx";
            string enomotoFbx = "Assets/UnDFight/Art/Models/Enomoto/Enomoto.fbx";
            string gabubuFbx = "Assets/UnDFight/Art/Models/Gabubu/Gabubu.fbx";

            AssertMovesetMatchesFbx(isaac, isaacFbx, "AOC_Isaac");
            AssertMovesetMatchesFbx(enomoto, enomotoFbx, "AOC_Enomoto");
            AssertMovesetMatchesFbx(gabubu, gabubuFbx, "AOC_Gabubu");

            Assert.AreEqual(52, ((ModelImporter)AssetImporter.GetAtPath(isaacFbx)).humanDescription.human.Length);
            Assert.AreEqual(52, ((ModelImporter)AssetImporter.GetAtPath(enomotoFbx)).humanDescription.human.Length);
            Assert.AreEqual(52, ((ModelImporter)AssetImporter.GetAtPath(gabubuFbx)).humanDescription.human.Length);

            AssertPrefabHurtboxPhysics(isaac.prefab);
            AssertPrefabHurtboxPhysics(enomoto.prefab);
            AssertPrefabHurtboxPhysics(gabubu.prefab);
        }

        private static void AssertMediumTicks(FighterData data)
        {
            Assert.AreEqual(16, data.lightPunch.TotalTicks);
            Assert.AreEqual(27, data.heavyPunch.TotalTicks);
            Assert.AreEqual(19, data.lightKick.TotalTicks);
            Assert.AreEqual(33, data.heavyKick.TotalTicks);
            Assert.AreEqual(27, data.comboPunchFinisher.TotalTicks);
            Assert.AreEqual(29, data.comboKickFinisher.TotalTicks);
            Assert.AreEqual(28, data.crouchKick.TotalTicks);
            Assert.AreEqual(18, data.jumpAttack.TotalTicks);
            Assert.AreEqual(22, data.throwMove.TotalTicks);
        }

        private static void AssertPrefabHurtboxPhysics(GameObject prefab)
        {
            Assert.IsNotNull(prefab);
            Assert.AreEqual(StagePhysicsLayers.FighterHurtboxLayer, prefab.layer, $"{prefab.name} must be on FighterHurtbox layer (10)");

            CapsuleCollider col = prefab.GetComponent<CapsuleCollider>();
            Assert.IsNotNull(col, $"{prefab.name} missing CapsuleCollider");
            Assert.IsTrue(col.isTrigger, $"{prefab.name} CapsuleCollider must be trigger");

            Rigidbody rb = prefab.GetComponent<Rigidbody>();
            Assert.IsNotNull(rb, $"{prefab.name} missing Rigidbody");
            Assert.IsTrue(rb.isKinematic, $"{prefab.name} Rigidbody must be kinematic");
            Assert.IsFalse(rb.useGravity, $"{prefab.name} Rigidbody must disable gravity");
            Assert.AreEqual(CollisionDetectionMode.ContinuousSpeculative, rb.collisionDetectionMode);
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
            Assert.AreNotSame(jompes.crouchKick, rochard.crouchKick);
            Assert.AreNotSame(jompes.jumpAttack, rochard.jumpAttack);
            Assert.AreNotSame(jompes.throwMove, rochard.throwMove);
            Assert.AreNotSame(jompes.exHeavyPunch, rochard.exHeavyPunch);
            Assert.AreNotSame(jompes.exHeavyKick, rochard.exHeavyKick);
            Assert.AreNotSame(jompes.specialMove, rochard.specialMove);

            Assert.AreEqual(SpecialMechanicType.ProjectileShot, jompes.specialMove.specialMechanic);
            Assert.IsTrue(jompes.specialMove.spawnsProjectile);
            Assert.AreEqual(14, jompes.specialMove.superFreezeTicks);

            Assert.AreEqual(SpecialMechanicType.AntiAirSpin, rochard.specialMove.specialMechanic);
            Assert.IsTrue(rochard.specialMove.antiAirInvulnerable);
            Assert.AreEqual(14, rochard.specialMove.superFreezeTicks);

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
            Assert.AreEqual(24, data.comboPunchFinisher.TotalTicks);
            Assert.AreEqual(26, data.comboKickFinisher.TotalTicks);
            Assert.AreEqual(25, data.crouchKick.TotalTicks);
            Assert.AreEqual(18, data.jumpAttack.TotalTicks);
            Assert.AreEqual(22, data.throwMove.TotalTicks);
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
            Assert.AreNotSame(lucas.crouchKick, tiago.crouchKick);
            Assert.AreNotSame(lucas.jumpAttack, tiago.jumpAttack);
            Assert.AreNotSame(lucas.throwMove, tiago.throwMove);
            Assert.AreNotSame(lucas.exHeavyPunch, tiago.exHeavyPunch);
            Assert.AreNotSame(lucas.exHeavyKick, tiago.exHeavyKick);
            Assert.AreNotSame(lucas.specialMove, tiago.specialMove);

            Assert.AreEqual(SpecialMechanicType.ArmoredSmash, lucas.specialMove.specialMechanic);
            Assert.Greater(lucas.specialMove.armorStartTick, 0);
            Assert.AreEqual(14, lucas.specialMove.superFreezeTicks);

            Assert.AreEqual(SpecialMechanicType.MultiHitRush, tiago.specialMove.specialMechanic);
            Assert.AreEqual(12, tiago.specialMove.secondActiveStartTick);
            Assert.AreEqual(14, tiago.specialMove.superFreezeTicks);

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

            Assert.AreEqual(26, clips.Count, $"Expected 26 clips in {fbxPath}");
            Assert.IsTrue(clips.TryGetValue("Turn180", out AnimationClip turnClip), $"Missing Turn180 clip in {fbxPath}");
            Assert.IsFalse(turnClip.isLooping, $"{fbxPath} Turn180 must not loop");
            Assert.AreEqual(60, Mathf.RoundToInt(turnClip.frameRate), $"Turn180 clip must be 60 fps in {fbxPath}");
            Assert.AreEqual(8, Mathf.RoundToInt(turnClip.length * 60f), $"Turn180 clip must be 8 frames in {fbxPath}");

            foreach (string crouchWalkClipName in new[] { "CrouchWalkForward", "CrouchWalkBackward" })
            {
                Assert.IsTrue(clips.TryGetValue(crouchWalkClipName, out AnimationClip cwClip), $"Missing {crouchWalkClipName} clip in {fbxPath}");
                Assert.IsTrue(cwClip.isLooping, $"{fbxPath} {crouchWalkClipName} must loop");
                Assert.AreEqual(60, Mathf.RoundToInt(cwClip.frameRate), $"{crouchWalkClipName} clip must be 60 fps in {fbxPath}");
                Assert.AreEqual(48, Mathf.RoundToInt(cwClip.length * 60f), $"{crouchWalkClipName} clip must be 48 frames in {fbxPath}");
            }

            foreach (var (clipName, expectedFrames) in new[] { ("DashForward", 14), ("DashBackward", 16) })
            {
                Assert.IsTrue(clips.TryGetValue(clipName, out AnimationClip dashClip), $"Missing {clipName} clip in {fbxPath}");
                Assert.IsFalse(dashClip.isLooping, $"{fbxPath} {clipName} must not loop");
                Assert.AreEqual(60, Mathf.RoundToInt(dashClip.frameRate), $"{clipName} clip must be 60 fps in {fbxPath}");
                Assert.AreEqual(expectedFrames, Mathf.RoundToInt(dashClip.length * 60f), $"{clipName} clip must be {expectedFrames} frames in {fbxPath}");
            }

            foreach (AttackType attackType in new[]
                     {
                         AttackType.LightPunch, AttackType.HeavyPunch, AttackType.LightKick, AttackType.HeavyKick,
                         AttackType.ComboPunchFinisher, AttackType.ComboKickFinisher,
                         AttackType.CrouchKick, AttackType.JumpAttack, AttackType.Throw, AttackType.Special
                     })
            {
                MoveData move = data.GetMoveData(attackType);
                Assert.IsNotNull(move, $"{data.fighterName} missing {attackType}");
                Assert.Greater(move.TotalTicks, 0);
                Assert.Greater(move.damage, 0);
                Assert.IsTrue(clips.TryGetValue(attackType.ToString(), out AnimationClip clip), $"Missing clip {attackType} in {fbxPath}");
                Assert.IsFalse(clip.isLooping, $"{fbxPath} {attackType} must not loop");
                Assert.AreEqual(60, Mathf.RoundToInt(clip.frameRate), $"Clip {clip.name} must be 60 fps");
                int clipFrames = Mathf.RoundToInt(clip.length * 60f);
                Assert.AreEqual(move.TotalTicks, clipFrames, $"{data.fighterName} {attackType} MoveData.TotalTicks must equal FBX clip frame length");
            }

            Assert.IsNotNull(data.comboStrings, $"{data.fighterName} missing comboStrings");
            Assert.GreaterOrEqual(data.comboStrings.Length, 3, $"{data.fighterName} must have at least 3 comboStrings");
            bool hasOverheadFinisher = false;
            bool hasLowFinisher = false;
            foreach (ComboStringDefinition def in data.comboStrings)
            {
                Assert.IsNotNull(def);
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.stringName), $"{data.fighterName} has comboString without stringName");
                Assert.IsNotNull(def.steps);
                Assert.GreaterOrEqual(def.steps.Length, 3, $"{data.fighterName} comboString '{def.stringName}' must have >= 3 steps");

                ComboStringStep lastStep = def.steps[def.steps.Length - 1];
                MoveData lastMove = lastStep.stepMoveOverride != null
                    ? lastStep.stepMoveOverride
                    : data.GetMoveData(lastStep.attackType);
                Assert.IsNotNull(lastMove, $"{data.fighterName} comboString '{def.stringName}' final step missing MoveData");
                if (lastMove.hitHeight == HitHeight.Overhead)
                {
                    hasOverheadFinisher = true;
                }
                if (lastMove.hitHeight == HitHeight.Low || (lastStep.requireCrouch && lastStep.attackType == AttackType.CrouchKick))
                {
                    hasLowFinisher = true;
                }
            }
            Assert.IsTrue(hasOverheadFinisher, $"{data.fighterName} comboStrings must include at least one Overhead finisher mix-up");
            Assert.IsTrue(hasLowFinisher, $"{data.fighterName} comboStrings must include at least one Low/CrouchKick finisher mix-up");

            Assert.IsNotNull(data.exHeavyPunch, $"{data.fighterName} missing exHeavyPunch");
            Assert.IsTrue(data.exHeavyPunch.isExMove);
            Assert.AreEqual(FighterData.ExSuperCost, data.exHeavyPunch.superCost);
            Assert.Greater(data.exHeavyPunch.damage, data.heavyPunch.damage);

            Assert.IsNotNull(data.exHeavyKick, $"{data.fighterName} missing exHeavyKick");
            Assert.IsTrue(data.exHeavyKick.isExMove);
            Assert.AreEqual(FighterData.ExSuperCost, data.exHeavyKick.superCost);
            Assert.Greater(data.exHeavyKick.damage, data.heavyKick.damage);

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

        [Test]
        public void AnimatorController_ContainsTurn180StateAndCancelTransitions()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/UnDFight/Art/Animations/AC_Fighter.controller");
            Assert.IsNotNull(controller, "AC_Fighter.controller missing");

            bool hasIsTurning = false;
            foreach (var p in controller.parameters)
            {
                if (p.name == "IsTurning" && p.type == AnimatorControllerParameterType.Bool)
                {
                    hasIsTurning = true;
                    break;
                }
            }
            Assert.IsTrue(hasIsTurning, "AC_Fighter must expose Bool parameter 'IsTurning'");

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState turnState = null;
            foreach (var cs in sm.states)
            {
                if (cs.state.name == "Turn180")
                {
                    turnState = cs.state;
                    break;
                }
            }
            Assert.IsNotNull(turnState, "AC_Fighter must contain Turn180 state");
            Assert.IsNotNull(turnState.motion, "Turn180 state must have an AnimationClip assigned");
            Assert.IsTrue(turnState.writeDefaultValues, "Turn180 state must keep writeDefaultValues=true");

            var transitionTargets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tr in turnState.transitions)
            {
                if (tr.destinationState != null)
                {
                    transitionTargets.Add(tr.destinationState.name);
                }
            }

            foreach (string requiredTarget in new[]
                     { "Idle", "WalkForward", "WalkBackward", "Crouch", "CrouchWalkForward", "CrouchWalkBackward", "DashForward", "DashBackward", "Jump", "Block", "HitReact", "KnockDown", "GetUp", "Victory", "Defeat" })
            {
                Assert.IsTrue(transitionTargets.Contains(requiredTarget), $"Turn180 missing exit transition to {requiredTarget}");
            }

            var stateNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var cs in sm.states)
            {
                stateNames.Add(cs.state.name);
            }

            foreach (string requiredState in new[]
                     {
                         "CrouchWalkForward", "CrouchWalkBackward", "DashForward", "DashBackward",
                         "CrouchKick", "JumpAttack", "Throw", "ComboPunchFinisher", "ComboKickFinisher"
                     })
            {
                Assert.IsTrue(stateNames.Contains(requiredState), $"AC_Fighter missing required state '{requiredState}'");
            }

            // Phase 13.5: AlreadyCrouched parameter & dual AnyState -> Crouch transitions
            bool hasAlreadyCrouched = false;
            foreach (var p in controller.parameters)
            {
                if (p.name == "AlreadyCrouched" && p.type == AnimatorControllerParameterType.Bool)
                {
                    hasAlreadyCrouched = true;
                    break;
                }
            }
            Assert.IsTrue(hasAlreadyCrouched, "AC_Fighter must expose Bool parameter 'AlreadyCrouched'");

            bool foundStandingToCrouch = false;
            bool foundAlreadyCrouchedToCrouch = false;
            foreach (var tr in sm.anyStateTransitions)
            {
                if (tr.destinationState == null || tr.destinationState.name != "Crouch") continue;
                foreach (var cond in tr.conditions)
                {
                    if (cond.parameter == "AlreadyCrouched")
                    {
                        if (cond.mode == AnimatorConditionMode.IfNot && Mathf.Approximately(tr.offset, 0f))
                        {
                            foundStandingToCrouch = true;
                        }
                        else if (cond.mode == AnimatorConditionMode.If && tr.offset >= 0.2f)
                        {
                            foundAlreadyCrouchedToCrouch = true;
                        }
                    }
                }
            }
            Assert.IsTrue(foundStandingToCrouch, "AC_Fighter missing AnyState->Crouch transition with AlreadyCrouched=false and offset=0");
            Assert.IsTrue(foundAlreadyCrouchedToCrouch, "AC_Fighter missing AnyState->Crouch transition with AlreadyCrouched=true and offset>=0.2");
        }

        [Test]
        public void CrouchWalkToCrouch_EntersCrouchAtAlreadyCrouchedOffset_WithoutStandingUp()
        {
            var lucasData = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Lucas.asset");
            var tiagoData = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Tiago.asset");
            Assert.IsNotNull(lucasData);
            Assert.IsNotNull(lucasData.prefab);

            var simGo = Track(new GameObject("Sim_CrouchOffset"));
            var sim = simGo.AddComponent<FightSimulation>();

            var p1Go = Track(UnityEngine.Object.Instantiate(lucasData.prefab));
            p1Go.transform.position = new Vector3(-1.5f, 0f, 0f);
            var p1 = p1Go.GetComponent<FighterController>();
            p1.SetFighterData(lucasData);

            var p2Go = Track(UnityEngine.Object.Instantiate(tiagoData.prefab));
            p2Go.transform.position = new Vector3(1.5f, 0f, 0f);
            var p2 = p2Go.GetComponent<FighterController>();
            p2.SetFighterData(tiagoData);

            sim.SetParticipants(p1, p2);
            p1.InputHandler.SetSimulatedInput(default, true);
            p2.InputHandler.SetSimulatedInput(default, true);

            Animator anim = p1Go.GetComponentInChildren<Animator>();
            Assert.IsNotNull(anim);

            // 1. Idle -> Crouch (coming from standing): normalizedTime starts < 0.20f
            p1.InputHandler.SetSimulatedInput(new FighterInputState { MoveX = 0f, MoveY = -1f }, true);
            sim.StepSimulationTicks(1);
            Assert.AreEqual(FighterState.Crouch, p1.State);
            Assert.IsFalse(anim.GetBool("AlreadyCrouched"));

            AnimatorStateInfo fromIdleInfo = anim.IsInTransition(0)
                ? anim.GetNextAnimatorStateInfo(0)
                : anim.GetCurrentAnimatorStateInfo(0);
            Assert.IsTrue(fromIdleInfo.IsName("Crouch"));
            Assert.Less(fromIdleInfo.normalizedTime, 0.20f, "Idle -> Crouch must start near frame 1 (normalizedTime < 0.20f)");

            // 2. Advance into CrouchWalkForward for 10 ticks
            p1.InputHandler.SetSimulatedInput(new FighterInputState { MoveX = 1f, MoveY = -1f }, true);
            sim.StepSimulationTicks(10);
            Assert.AreEqual(FighterState.CrouchWalkForward, p1.State);

            // 3. Release MoveX while keeping MoveY = -1 -> enters Crouch with AlreadyCrouched = true and normalizedTime >= 0.20f
            p1.InputHandler.SetSimulatedInput(new FighterInputState { MoveX = 0f, MoveY = -1f }, true);
            for (int tick = 1; tick <= 6; tick++)
            {
                sim.StepSimulationTicks(1);
                Assert.AreEqual(FighterState.Crouch, p1.State);
                Assert.IsTrue(anim.GetBool("AlreadyCrouched"));

                AnimatorStateInfo crouchInfo = anim.IsInTransition(0)
                    ? anim.GetNextAnimatorStateInfo(0)
                    : anim.GetCurrentAnimatorStateInfo(0);
                Assert.IsTrue(crouchInfo.IsName("Crouch"), $"Tick {tick}: expected Crouch state");
                Assert.GreaterOrEqual(crouchInfo.normalizedTime, 0.20f, $"Tick {tick}: CrouchWalkForward -> Crouch must skip standing frames (normalizedTime >= 0.20f)");
            }
        }

        [Test]
        public void NewFighters_SpecialMoves_ExecuteDistinctMechanicsDeterministically_AndSelectCardsAutoSize()
        {
            var isaacData = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Isaac.asset");
            var enomotoData = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Enomoto.asset");
            var gabubuData = AssetDatabase.LoadAssetAtPath<FighterData>("Assets/UnDFight/Data/FighterData_Gabubu.asset");
            var roster = AssetDatabase.LoadAssetAtPath<FighterRoster>("Assets/UnDFight/Data/FighterRoster.asset");

            // 1. CharacterSelectController 4x2 grid card auto-sizing (13..24, NoWrap) for all 7 fighters
            var canvasGo = Track(new GameObject("TestSelectCanvas", typeof(RectTransform), typeof(Canvas)));
            var selectCtrl = canvasGo.AddComponent<CharacterSelectController>();
            var tex = Track(new Texture2D(2, 2));
            var sprite = Track(Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f)));
            selectCtrl.ConfigureReferences(roster, null, sprite, null, null);
            selectCtrl.BuildUI(canvasGo.transform, sprite);

            Transform grid = canvasGo.transform.Find("RosterGridPanel/Grid");
            Assert.IsNotNull(grid);
            Assert.AreEqual(7, grid.childCount);
            for (int i = 0; i < grid.childCount; i++)
            {
                var nameTmp = grid.GetChild(i).Find("Name")?.GetComponent<TMPro.TextMeshProUGUI>();
                Assert.IsNotNull(nameTmp);
                Assert.IsTrue(nameTmp.enableAutoSizing, $"Card {i} Name must enable auto-sizing");
                Assert.AreEqual(13f, nameTmp.fontSizeMin, 0.01f);
                Assert.AreEqual(24f, nameTmp.fontSizeMax, 0.01f);
                Assert.AreEqual(TMPro.TextWrappingModes.NoWrap, nameTmp.textWrappingMode);
            }

            // 2. Gabubu's Riff Destruidor (MultiHitRush): 2 distinct active hits + knockdown
            {
                var simGo = Track(new GameObject("Sim_Gabubu"));
                var sim = simGo.AddComponent<FightSimulation>();
                var p1Go = Track(new GameObject("Gabubu"));
                p1Go.transform.position = new Vector3(-0.65f, 0f, 0f);
                p1Go.AddComponent<PlayerInputHandler>();
                var p1 = p1Go.AddComponent<FighterController>();
                p1.SetFighterData(gabubuData);

                var p2Go = Track(new GameObject("Defender"));
                p2Go.transform.position = new Vector3(0.45f, 0f, 0f);
                p2Go.AddComponent<PlayerInputHandler>();
                var p2 = p2Go.AddComponent<FighterController>();
                p2.SetFighterData(isaacData);

                sim.SetParticipants(p1, p2);
                p1.InputHandler.SetSimulatedInput(default, true);
                p2.InputHandler.SetSimulatedInput(default, true);
                p1.SetSuperGauge(100);

                var hits = new List<CombatHitEvent>();
                sim.OnCombatHit += e => hits.Add(e);

                p1.InputHandler.BufferButton(FighterInputButton.Special);
                sim.StepSimulationTicks(1 + 14 + 28); // 1 start + 14 superFreeze + hit1/hitstop/hit2

                Assert.AreEqual(2, hits.Count, "Gabubu's Riff Destruidor must register 2 distinct hits");
                Assert.AreEqual(2, hits[1].ComboCount);
                Assert.AreEqual(FighterState.KnockDown, p2.State);
            }

            // 3. Enomoto's Estocada do Cavalheiro (ProjectileShot): spawns projectile at 10.2 m/s and hits from afar
            {
                var simGo = Track(new GameObject("Sim_Enomoto"));
                var sim = simGo.AddComponent<FightSimulation>();
                var p1Go = Track(new GameObject("Enomoto"));
                p1Go.transform.position = new Vector3(-1.50f, 0f, 0f);
                p1Go.AddComponent<PlayerInputHandler>();
                var p1 = p1Go.AddComponent<FighterController>();
                p1.SetFighterData(enomotoData);

                var p2Go = Track(new GameObject("Defender"));
                p2Go.transform.position = new Vector3(1.50f, 0f, 0f);
                p2Go.AddComponent<PlayerInputHandler>();
                var p2 = p2Go.AddComponent<FighterController>();
                p2.SetFighterData(isaacData);

                sim.SetParticipants(p1, p2);
                p1.InputHandler.SetSimulatedInput(default, true);
                p2.InputHandler.SetSimulatedInput(default, true);
                p1.SetSuperGauge(100);

                var hits = new List<CombatHitEvent>();
                sim.OnCombatHit += e => hits.Add(e);

                p1.InputHandler.BufferButton(FighterInputButton.Special);
                sim.StepSimulationTicks(1 + 14 + 35); // start + freeze + startup(8) + projectile flight

                Assert.AreEqual(1, hits.Count, "Enomoto's projectile must hit distant opponent");
                Assert.Less(p2.CurrentHealth, p2.MaxHealth);
                Assert.AreEqual(FighterState.KnockDown, p2.State);
            }

            // 4. Isaac's Punho Esmeralda (AntiAirSpin): anti-air invulnerability + knockdown hit
            {
                var simGo = Track(new GameObject("Sim_Isaac"));
                var sim = simGo.AddComponent<FightSimulation>();
                var p1Go = Track(new GameObject("Isaac"));
                p1Go.transform.position = new Vector3(-0.55f, 0f, 0f);
                p1Go.AddComponent<PlayerInputHandler>();
                var p1 = p1Go.AddComponent<FighterController>();
                p1.SetFighterData(isaacData);

                var p2Go = Track(new GameObject("Jumper"));
                p2Go.transform.position = new Vector3(0.55f, 0f, 0f);
                p2Go.AddComponent<PlayerInputHandler>();
                var p2 = p2Go.AddComponent<FighterController>();
                p2.SetFighterData(gabubuData);

                sim.SetParticipants(p1, p2);
                p1.InputHandler.SetSimulatedInput(default, true);
                p2.InputHandler.SetSimulatedInput(new FighterInputState { MoveX = -1f, MoveY = 1f }, true);
                p1.SetSuperGauge(100);

                sim.StepSimulationTicks(3); // P2 jumps forward
                p2.InputHandler.SetSimulatedInput(default, true);
                p2.InputHandler.BufferButton(FighterInputButton.HeavyKick); // JumpAttack!
                p1.InputHandler.BufferButton(FighterInputButton.Special);   // Punho Esmeralda!

                sim.StepSimulationTicks(1 + 14 + 14);
                Assert.AreEqual(p1.MaxHealth, p1.CurrentHealth, "Isaac must evade aerial JumpAttack during Punho Esmeralda");
                Assert.Less(p2.CurrentHealth, p2.MaxHealth, "Isaac's Punho Esmeralda must punish jumping attacker");
            }
        }
    }
}
