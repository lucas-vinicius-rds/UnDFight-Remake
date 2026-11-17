using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnDFight.Editor
{
    /// <summary>
    /// Editor utility & weight-class preset generator for creating MoveData sets,
    /// configuring FBX 60fps clips, and building AnimatorOverrideControllers per fighter.
    /// </summary>
    public static class FighterArchetypeSetupEditor
    {
        public struct MoveSpec
        {
            public string MoveName;
            public AttackType Type;
            public int Startup;
            public int Active;
            public int Recovery;
            public int Damage;
            public int ChipDamage;
            public int Hitstun;
            public int Blockstun;
            public int Hitstop;
            public float Knockback;
            public HitHeight Height;
            public bool CausesKnockdown;
            public Vector2 HitboxOffset;
            public Vector2 HitboxSize;
            public float LungeSpeed;
            public bool IsThrow;
            public bool AirOnly;
            public SpecialMechanicType SpecialMechanic;
            public int SuperCost;
            public bool IsExMove;
            public int ArmorStartTick;
            public int ArmorEndTick;
            public bool AntiAirInvulnerable;
            public int AntiAirStartTick;
            public int AntiAirEndTick;
            public int SecondActiveStartTick;
            public int SecondActiveEndTick;
            public int SecondHitDamage;
            public bool SpawnsProjectile;
            public float ProjectileSpeed;
            public int ProjectileLifetimeTicks;
            public Vector2 ProjectileSpawnOffset;
            public Vector2 ProjectileHitboxSize;
            public int SuperFreezeTicks;

            public int TotalTicks => Startup + Active + Recovery;
        }

        private const string BaseControllerPath = "Assets/UnDFight/Art/Animations/AC_Fighter.controller";
        private const string FighterRosterPath = "Assets/UnDFight/Data/FighterRoster.asset";
        private const string LucasFbxPath = "Assets/UnDFight/Art/Models/Lucas/Lucas.fbx";
        private const string TiagoFbxPath = "Assets/UnDFight/Art/Models/Tiago/Tiago.fbx";
        private const string JompesFbxPath = "Assets/UnDFight/Art/Models/Jompes/Jompes.fbx";
        private const string RochardFbxPath = "Assets/UnDFight/Art/Models/Rochard/Rochard.fbx";
        private const string LucasOverridePath = "Assets/UnDFight/Art/Animations/AOC_Lucas.overrideController";
        private const string TiagoOverridePath = "Assets/UnDFight/Art/Animations/AOC_Tiago.overrideController";
        private const string JompesOverridePath = "Assets/UnDFight/Art/Animations/AOC_Jompes.overrideController";
        private const string RochardOverridePath = "Assets/UnDFight/Art/Animations/AOC_Rochard.overrideController";
        private const string JompesMatPath = "Assets/UnDFight/Art/Materials/Jompes_Mat.mat";
        private const string RochardMatPath = "Assets/UnDFight/Art/Materials/Rochard_Mat.mat";
        private const string JompesTextureFolder = "Assets/UnDFight/Art/Source/Jompes";
        private const string RochardTextureFolder = "Assets/UnDFight/Art/Source/Rochard";
        private const string LucasPrefabPath = "Assets/UnDFight/Prefabs/Fighter_Lucas.prefab";
        private const string TiagoPrefabPath = "Assets/UnDFight/Prefabs/Fighter_Tiago.prefab";
        private const string JompesPrefabPath = "Assets/UnDFight/Prefabs/Fighter_Jompes.prefab";
        private const string RochardPrefabPath = "Assets/UnDFight/Prefabs/Fighter_Rochard.prefab";
        private const string LucasDataPath = "Assets/UnDFight/Data/FighterData_Lucas.asset";
        private const string TiagoDataPath = "Assets/UnDFight/Data/FighterData_Tiago.asset";
        private const string JompesDataPath = "Assets/UnDFight/Data/FighterData_Jompes.asset";
        private const string RochardDataPath = "Assets/UnDFight/Data/FighterData_Rochard.asset";

        private static readonly HashSet<string> LoopClipNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Idle",
            "WalkForward",
            "WalkBackward",
            "CrouchWalkForward",
            "CrouchWalkBackward"
        };

        private static readonly string[] RequiredClips =
        {
            "Idle", "WalkForward", "WalkBackward", "CrouchWalkForward", "CrouchWalkBackward",
            "DashForward", "DashBackward",
            "Jump", "Crouch", "BlockStand",
            "LightPunch", "HeavyPunch", "LightKick", "HeavyKick",
            "CrouchKick", "JumpAttack", "Throw", "Special",
            "HitReact", "KnockDown", "GetUp", "Victory", "Defeat", "Turn180"
        };

        public static MoveSpec[] GetWeightClassPresetSpecs(WeightClass weightClass)
        {
            return weightClass switch
            {
                WeightClass.Light => new[]
                {
                    new MoveSpec { Type = AttackType.LightPunch, Startup = 3, Active = 3, Recovery = 7,  Damage = 42,  Hitstun = 11, Blockstun = 7,  Hitstop = 4, Knockback = 1.1f, Height = HitHeight.Mid,         CausesKnockdown = false, HitboxOffset = new Vector2(0.66f, 1.15f), HitboxSize = new Vector2(0.76f, 0.40f) },
                    new MoveSpec { Type = AttackType.HeavyPunch, Startup = 5, Active = 4, Recovery = 12, Damage = 92,  Hitstun = 16, Blockstun = 11, Hitstop = 6, Knockback = 2.2f, Height = HitHeight.Mid,         CausesKnockdown = false, HitboxOffset = new Vector2(0.74f, 1.15f), HitboxSize = new Vector2(0.88f, 0.44f) },
                    new MoveSpec { Type = AttackType.LightKick,  Startup = 4, Active = 3, Recovery = 9,  Damage = 52,  Hitstun = 12, Blockstun = 8,  Hitstop = 4, Knockback = 1.4f, Height = HitHeight.Low,         CausesKnockdown = false, HitboxOffset = new Vector2(0.72f, 0.50f), HitboxSize = new Vector2(0.88f, 0.42f) },
                    new MoveSpec { Type = AttackType.HeavyKick,  Startup = 7, Active = 4, Recovery = 16, Damage = 112, Hitstun = 20, Blockstun = 13, Hitstop = 7, Knockback = 2.9f, Height = HitHeight.High,        CausesKnockdown = true,  HitboxOffset = new Vector2(0.84f, 1.20f), HitboxSize = new Vector2(1.04f, 0.54f) },
                    new MoveSpec { Type = AttackType.CrouchKick, Startup = 6, Active = 4, Recovery = 15, Damage = 85,  Hitstun = 18, Blockstun = 12, Hitstop = 6, Knockback = 2.4f, Height = HitHeight.Low,         CausesKnockdown = true,  HitboxOffset = new Vector2(0.86f, 0.34f), HitboxSize = new Vector2(1.10f, 0.42f) },
                    new MoveSpec { Type = AttackType.JumpAttack, Startup = 4, Active = 8, Recovery = 6,  Damage = 75,  Hitstun = 18, Blockstun = 12, Hitstop = 6, Knockback = 1.6f, Height = HitHeight.Overhead,    CausesKnockdown = false, AirOnly = true, HitboxOffset = new Vector2(0.66f, 0.56f), HitboxSize = new Vector2(0.96f, 0.74f) },
                    new MoveSpec { Type = AttackType.Throw,      Startup = 5, Active = 3, Recovery = 14, Damage = 110, Hitstun = 20, Blockstun = 10, Hitstop = 8, Knockback = 3.2f, Height = HitHeight.Unblockable, CausesKnockdown = true,  IsThrow = true, HitboxOffset = new Vector2(0.58f, 1.02f), HitboxSize = new Vector2(0.64f, 0.56f) },
                },
                WeightClass.Medium => new[]
                {
                    new MoveSpec { Type = AttackType.LightPunch, Startup = 4, Active = 3, Recovery = 9,  Damage = 48,  Hitstun = 12, Blockstun = 8,  Hitstop = 4, Knockback = 1.3f, Height = HitHeight.Mid,         CausesKnockdown = false, HitboxOffset = new Vector2(0.68f, 1.18f), HitboxSize = new Vector2(0.78f, 0.42f) },
                    new MoveSpec { Type = AttackType.HeavyPunch, Startup = 7, Active = 4, Recovery = 16, Damage = 108, Hitstun = 18, Blockstun = 12, Hitstop = 7, Knockback = 2.7f, Height = HitHeight.Mid,         CausesKnockdown = false, HitboxOffset = new Vector2(0.76f, 1.18f), HitboxSize = new Vector2(0.92f, 0.46f) },
                    new MoveSpec { Type = AttackType.LightKick,  Startup = 5, Active = 3, Recovery = 11, Damage = 62,  Hitstun = 14, Blockstun = 9,  Hitstop = 5, Knockback = 1.7f, Height = HitHeight.Low,         CausesKnockdown = false, HitboxOffset = new Vector2(0.74f, 0.52f), HitboxSize = new Vector2(0.92f, 0.44f) },
                    new MoveSpec { Type = AttackType.HeavyKick,  Startup = 9, Active = 4, Recovery = 20, Damage = 130, Hitstun = 22, Blockstun = 14, Hitstop = 8, Knockback = 3.5f, Height = HitHeight.High,        CausesKnockdown = true,  HitboxOffset = new Vector2(0.86f, 1.24f), HitboxSize = new Vector2(1.10f, 0.56f) },
                    new MoveSpec { Type = AttackType.CrouchKick, Startup = 7, Active = 4, Recovery = 17, Damage = 95,  Hitstun = 20, Blockstun = 13, Hitstop = 7, Knockback = 2.8f, Height = HitHeight.Low,         CausesKnockdown = true,  HitboxOffset = new Vector2(0.88f, 0.36f), HitboxSize = new Vector2(1.14f, 0.44f) },
                    new MoveSpec { Type = AttackType.JumpAttack, Startup = 4, Active = 8, Recovery = 6,  Damage = 85,  Hitstun = 18, Blockstun = 12, Hitstop = 6, Knockback = 1.9f, Height = HitHeight.Overhead,    CausesKnockdown = false, AirOnly = true, HitboxOffset = new Vector2(0.68f, 0.58f), HitboxSize = new Vector2(0.98f, 0.76f) },
                    new MoveSpec { Type = AttackType.Throw,      Startup = 5, Active = 3, Recovery = 14, Damage = 115, Hitstun = 20, Blockstun = 10, Hitstop = 8, Knockback = 3.5f, Height = HitHeight.Unblockable, CausesKnockdown = true,  IsThrow = true, HitboxOffset = new Vector2(0.58f, 1.02f), HitboxSize = new Vector2(0.64f, 0.56f) },
                },
                _ => new[]
                {
                    new MoveSpec { Type = AttackType.LightPunch, Startup = 5, Active = 3, Recovery = 11, Damage = 62,  Hitstun = 14, Blockstun = 10, Hitstop = 5, Knockback = 1.9f, Height = HitHeight.Mid,         CausesKnockdown = false, HitboxOffset = new Vector2(0.68f, 1.18f), HitboxSize = new Vector2(0.78f, 0.42f) },
                    new MoveSpec { Type = AttackType.HeavyPunch, Startup = 9, Active = 5, Recovery = 19, Damage = 140, Hitstun = 22, Blockstun = 15, Hitstop = 9, Knockback = 3.7f, Height = HitHeight.Mid,         CausesKnockdown = false, HitboxOffset = new Vector2(0.78f, 1.18f), HitboxSize = new Vector2(0.94f, 0.48f) },
                    new MoveSpec { Type = AttackType.LightKick,  Startup = 6, Active = 4, Recovery = 13, Damage = 78,  Hitstun = 16, Blockstun = 11, Hitstop = 6, Knockback = 2.3f, Height = HitHeight.Low,         CausesKnockdown = false, HitboxOffset = new Vector2(0.74f, 0.52f), HitboxSize = new Vector2(0.92f, 0.44f) },
                    new MoveSpec { Type = AttackType.HeavyKick,  Startup = 11, Active = 5, Recovery = 24, Damage = 165, Hitstun = 26, Blockstun = 17, Hitstop = 10, Knockback = 4.6f, Height = HitHeight.High,       CausesKnockdown = true,  HitboxOffset = new Vector2(0.88f, 1.24f), HitboxSize = new Vector2(1.12f, 0.56f) },
                    new MoveSpec { Type = AttackType.CrouchKick, Startup = 8, Active = 5, Recovery = 19, Damage = 110, Hitstun = 22, Blockstun = 14, Hitstop = 8, Knockback = 3.4f, Height = HitHeight.Low,         CausesKnockdown = true,  HitboxOffset = new Vector2(0.90f, 0.36f), HitboxSize = new Vector2(1.18f, 0.44f) },
                    new MoveSpec { Type = AttackType.JumpAttack, Startup = 4, Active = 8, Recovery = 6,  Damage = 98,  Hitstun = 18, Blockstun = 12, Hitstop = 7, Knockback = 2.2f, Height = HitHeight.Overhead,    CausesKnockdown = false, AirOnly = true, HitboxOffset = new Vector2(0.70f, 0.60f), HitboxSize = new Vector2(1.02f, 0.78f) },
                    new MoveSpec { Type = AttackType.Throw,      Startup = 5, Active = 3, Recovery = 14, Damage = 125, Hitstun = 20, Blockstun = 10, Hitstop = 8, Knockback = 4.0f, Height = HitHeight.Unblockable, CausesKnockdown = true,  IsThrow = true, HitboxOffset = new Vector2(0.58f, 1.02f), HitboxSize = new Vector2(0.64f, 0.56f) },
                }
            };
        }

        [MenuItem("UnDFight/Setup Fase 9 - Movesets & Overrides")]
        public static void ApplyPhase9SetupMenu()
        {
            string summary = ApplyPhase9Setup();
            Debug.Log("[FighterArchetypeSetupEditor] " + summary);
        }

        public static string ApplyPhase9Setup()
        {
            // 1. Reimport FBXs and configure exact 15 clips at 60fps without root motion
            ReimportAndConfigureFbxClips(LucasFbxPath, "Lucas_Armature|");
            ReimportAndConfigureFbxClips(TiagoFbxPath, "Tiago_Armature|");

            // 2. Ensure AC_Fighter attack exit transitions handle 13..40 tick attacks cleanly
            EnsureAttackExitTransitions(BaseControllerPath);

            // 3. Generate/update AnimatorOverrideControllers from FBX clips
            AnimatorOverrideController aocLucas = CreateOrUpdateOverrideController(LucasOverridePath, BaseControllerPath, LucasFbxPath);
            AnimatorOverrideController aocTiago = CreateOrUpdateOverrideController(TiagoOverridePath, BaseControllerPath, TiagoFbxPath);

            // 4. Bind override controllers to fighter prefabs
            AssignAnimatorControllerToPrefab(LucasPrefabPath, aocLucas);
            AssignAnimatorControllerToPrefab(TiagoPrefabPath, aocTiago);

            // 5. Generate fighter-specific MoveData assets using archetype + Special moveset
            MoveSpec[] lucasSpecs = BuildArchetypeMoveSpecs(WeightClass.Heavy, FightingArchetype.HeavyBrawler);
            MoveSpec[] tiagoSpecs = BuildArchetypeMoveSpecs(WeightClass.Medium, FightingArchetype.StreetScrappy);

            CreateOrUpdateFighterMoveset("Lucas", FightingArchetype.HeavyBrawler, LucasDataPath, lucasSpecs);
            CreateOrUpdateFighterMoveset("Tiago", FightingArchetype.StreetScrappy, TiagoDataPath, tiagoSpecs);
            SetPrebakedHeightFlag(LucasDataPath, true);
            SetPrebakedHeightFlag(TiagoDataPath, true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int lucasBones = CountMappedHumanBones(LucasFbxPath);
            int tiagoBones = CountMappedHumanBones(TiagoFbxPath);
            return $"Phase 9.1 Unity setup complete: Lucas ({lucasBones}/52 human bones, Heavy/HeavyBrawler) & Tiago ({tiagoBones}/52 human bones, Medium/StreetScrappy) updated cleanly.";
        }

        [MenuItem("UnDFight/Setup Fase 11 - Pés no Chão & Especiais")]
        public static void ApplyPhase11SetupMenu()
        {
            string summary = ApplyPhase10Setup();
            Debug.Log("[FighterArchetypeSetupEditor] " + summary);
        }

        [MenuItem("UnDFight/Setup Fase 10 - Jompes & Rochard")]
        public static void ApplyPhase10SetupMenu()
        {
            string summary = ApplyPhase10Setup();
            Debug.Log("[FighterArchetypeSetupEditor] " + summary);
        }

        public static string ApplyPhase10Setup()
        {
            ApplyPhase9Setup();

            SetupCompleteFighter(
                fighterName: "Jompes",
                archetype: FightingArchetype.TechnicalStriker,
                heightMeters: 1.65f,
                weightKg: 70.0f,
                walkForwardSpeed: 4.35f,
                walkBackwardSpeed: 3.35f,
                jumpForce: 12.6f,
                fbxPath: JompesFbxPath,
                expectedTakePrefix: "Jompes_Armature|",
                overridePath: JompesOverridePath,
                materialPath: JompesMatPath,
                textureFolder: JompesTextureFolder,
                prefabPath: JompesPrefabPath,
                fighterDataPath: JompesDataPath);

            SetupCompleteFighter(
                fighterName: "Rochard",
                archetype: FightingArchetype.AgileStriker,
                heightMeters: 1.75f,
                weightKg: 67.0f,
                walkForwardSpeed: 4.55f,
                walkBackwardSpeed: 3.55f,
                jumpForce: 13.0f,
                fbxPath: RochardFbxPath,
                expectedTakePrefix: "Rochard_Armature|",
                overridePath: RochardOverridePath,
                materialPath: RochardMatPath,
                textureFolder: RochardTextureFolder,
                prefabPath: RochardPrefabPath,
                fighterDataPath: RochardDataPath);

            UpdateFighterRoster(LucasDataPath, TiagoDataPath, JompesDataPath, RochardDataPath);
            SerializeFightHudInScenes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int lucasBones = CountMappedHumanBones(LucasFbxPath);
            int tiagoBones = CountMappedHumanBones(TiagoFbxPath);
            int jompesBones = CountMappedHumanBones(JompesFbxPath);
            int rochardBones = CountMappedHumanBones(RochardFbxPath);
            return $"Phase 13.4 setup complete: Lucas({lucasBones}), Tiago({tiagoBones}), Jompes({jompesBones}), Rochard({rochardBones}) [24 clips, heightFromFeet=false].";
        }

        public static int CountMappedHumanBones(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            return importer != null && importer.humanDescription.human != null
                ? importer.humanDescription.human.Length
                : 0;
        }

        private static HumanDescription BuildFullHumanoidDescription(GameObject fbxRoot, HumanDescription existing)
        {
            var transforms = fbxRoot.GetComponentsInChildren<Transform>(true);
            var availableNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (Transform t in transforms)
            {
                availableNames.Add(t.name);
            }

            var humanList = new List<HumanBone>(52);
            foreach (string traitBoneName in HumanTrait.BoneName)
            {
                string rigBoneName = traitBoneName.Replace(" ", string.Empty);
                if (!availableNames.Contains(rigBoneName))
                {
                    continue;
                }

                bool isFinger = traitBoneName.Contains("Thumb", StringComparison.Ordinal) ||
                                traitBoneName.Contains("Index", StringComparison.Ordinal) ||
                                traitBoneName.Contains("Middle", StringComparison.Ordinal) ||
                                traitBoneName.Contains("Ring", StringComparison.Ordinal) ||
                                traitBoneName.Contains("Little", StringComparison.Ordinal);

                // Symmetric [-125, +125] bounds around T-pose zero avoid -45/-50 default clamping regardless of bone roll
                HumanLimit boneLimit = isFinger
                    ? new HumanLimit
                    {
                        useDefaultValues = false,
                        min = new Vector3(-90f, -60f, -125f),
                        max = new Vector3(90f, 60f, 125f),
                        center = Vector3.zero,
                        axisLength = 0f
                    }
                    : new HumanLimit { useDefaultValues = true };

                humanList.Add(new HumanBone
                {
                    humanName = traitBoneName,
                    boneName = rigBoneName,
                    limit = boneLimit
                });
            }

            HumanDescription desc = existing;
            desc.human = humanList.ToArray();
            if (desc.skeleton == null || desc.skeleton.Length == 0)
            {
                var skeletonList = new List<SkeletonBone>(transforms.Length);
                foreach (Transform t in transforms)
                {
                    skeletonList.Add(new SkeletonBone
                    {
                        name = t.name,
                        position = t.localPosition,
                        rotation = t.localRotation,
                        scale = t.localScale
                    });
                }
                desc.skeleton = skeletonList.ToArray();
            }

            desc.upperArmTwist = 0.5f;
            desc.lowerArmTwist = 0.5f;
            desc.upperLegTwist = 0.5f;
            desc.lowerLegTwist = 0.5f;
            desc.armStretch = 0.05f;
            desc.legStretch = 0.05f;
            desc.feetSpacing = 0.0f;
            desc.hasTranslationDoF = false;
            return desc;
        }

        public static void ReimportAndConfigureFbxClips(string fbxPath, string expectedTakePrefix)
        {
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"ModelImporter not found at {fbxPath}");
            }

            // Reset humanDescription once so Unity regenerates the pure T-Pose bind skeleton
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.humanDescription = new HumanDescription();
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();

            var fbxRoot = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxRoot == null)
            {
                throw new InvalidOperationException($"FBX root GameObject not found at {fbxPath}");
            }

            HumanDescription fullDesc = BuildFullHumanoidDescription(fbxRoot, importer.humanDescription);
            if (fullDesc.human.Length != 52)
            {
                throw new InvalidOperationException($"Expected 52 mapped Humanoid bones in {fbxPath}, got {fullDesc.human.Length}");
            }

            importer.humanDescription = fullDesc;
            ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
            var configured = new List<ModelImporterClipAnimation>();

            foreach (string clipName in RequiredClips)
            {
                ModelImporterClipAnimation match = null;
                foreach (ModelImporterClipAnimation def in defaults)
                {
                    string stripped = def.takeName.StartsWith(expectedTakePrefix, StringComparison.Ordinal)
                        ? def.takeName.Substring(expectedTakePrefix.Length)
                        : def.takeName;
                    if (string.Equals(stripped, clipName, StringComparison.Ordinal) ||
                        string.Equals(def.name, clipName, StringComparison.Ordinal))
                    {
                        match = def;
                        break;
                    }
                }

                if (match == null)
                {
                    throw new InvalidOperationException($"Missing animation take '{clipName}' in {fbxPath}");
                }

                bool shouldLoop = LoopClipNames.Contains(clipName);
                match.name = clipName;
                match.loop = shouldLoop;
                match.loopTime = shouldLoop;
                match.loopPose = shouldLoop;
                match.lockRootRotation = true;
                match.lockRootHeightY = true;
                match.lockRootPositionXZ = true;
                match.keepOriginalOrientation = true;
                match.keepOriginalPositionY = true;
                match.keepOriginalPositionXZ = true;
                match.heightFromFeet = false;
                configured.Add(match);
            }

            importer.clipAnimations = configured.ToArray();
            importer.SaveAndReimport();
        }

        public static void EnsureAttackExitTransitions(string controllerPath)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null || controller.layers.Length == 0)
            {
                throw new InvalidOperationException($"AnimatorController not found at {controllerPath}");
            }

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState idleState = FindState(sm, "Idle");
            if (idleState == null)
            {
                throw new InvalidOperationException("Idle state not found in AC_Fighter.controller");
            }

            AnimatorState specialState = EnsureAnimatorStateFromBaseFbx(sm, "Special", new Vector3(480f, 260f, 0f));
            AnimatorState crouchKickState = EnsureAnimatorStateFromBaseFbx(sm, "CrouchKick", new Vector3(480f, 320f, 0f));
            AnimatorState jumpAttackState = EnsureAnimatorStateFromBaseFbx(sm, "JumpAttack", new Vector3(480f, 380f, 0f));
            AnimatorState throwState = EnsureAnimatorStateFromBaseFbx(sm, "Throw", new Vector3(480f, 440f, 0f));
            _ = (specialState, crouchKickState, jumpAttackState, throwState);

            (string stateName, AttackType atkType)[] attackAnyStates =
            {
                ("LightPunch", AttackType.LightPunch),
                ("HeavyPunch", AttackType.HeavyPunch),
                ("LightKick", AttackType.LightKick),
                ("HeavyKick", AttackType.HeavyKick),
                ("Special", AttackType.Special),
                ("CrouchKick", AttackType.CrouchKick),
                ("JumpAttack", AttackType.JumpAttack),
                ("Throw", AttackType.Throw),
                ("HeavyPunch", AttackType.ExHeavyPunch),
                ("HeavyKick", AttackType.ExHeavyKick),
            };

            foreach (var (stateName, atkType) in attackAnyStates)
            {
                AnimatorState dst = FindState(sm, stateName);
                if (dst == null) continue;

                AnimatorStateTransition anyTr = null;
                foreach (AnimatorStateTransition tr in sm.anyStateTransitions)
                {
                    if (tr.destinationState != dst) continue;
                    foreach (AnimatorCondition cond in tr.conditions)
                    {
                        if (cond.parameter == "AttackType" &&
                            cond.mode == AnimatorConditionMode.Equals &&
                            Mathf.RoundToInt(cond.threshold) == (int)atkType)
                        {
                            anyTr = tr;
                            break;
                        }
                    }
                    if (anyTr != null) break;
                }
                if (anyTr == null)
                {
                    anyTr = sm.AddAnyStateTransition(dst);
                    anyTr.AddCondition(AnimatorConditionMode.If, 0f, "AttackTrigger");
                    anyTr.AddCondition(AnimatorConditionMode.Equals, (int)atkType, "AttackType");
                }
                anyTr.hasExitTime = false;
                anyTr.duration = 0f;
                anyTr.hasFixedDuration = true;
                anyTr.canTransitionToSelf = true;
            }

            string[] attackStates =
            {
                "LightPunch", "HeavyPunch", "LightKick", "HeavyKick",
                "Special", "CrouchKick", "JumpAttack", "Throw"
            };
            foreach (string stateName in attackStates)
            {
                AnimatorState st = FindState(sm, stateName);
                if (st == null) continue;

                bool hasExitTimeTransition = false;
                bool hasStateExitTransition = false;

                foreach (AnimatorStateTransition tr in st.transitions)
                {
                    if (tr.destinationState != idleState) continue;

                    if (tr.hasExitTime && tr.conditions.Length == 0)
                    {
                        tr.exitTime = 1f;
                        tr.duration = 0f;
                        tr.hasFixedDuration = true;
                        hasExitTimeTransition = true;
                    }
                    else if (!tr.hasExitTime && tr.conditions.Length == 1 &&
                             tr.conditions[0].parameter == "State" &&
                             tr.conditions[0].mode == AnimatorConditionMode.NotEqual &&
                             Mathf.RoundToInt(tr.conditions[0].threshold) == (int)FighterState.Attack)
                    {
                        tr.duration = 0f;
                        tr.hasFixedDuration = true;
                        hasStateExitTransition = true;
                    }
                }

                if (!hasExitTimeTransition)
                {
                    AnimatorStateTransition tr = st.AddTransition(idleState);
                    tr.hasExitTime = true;
                    tr.exitTime = 1f;
                    tr.duration = 0f;
                    tr.hasFixedDuration = true;
                }

                if (!hasStateExitTransition)
                {
                    AnimatorStateTransition tr = st.AddTransition(idleState);
                    tr.hasExitTime = false;
                    tr.duration = 0f;
                    tr.hasFixedDuration = true;
                    tr.AddCondition(AnimatorConditionMode.NotEqual, (int)FighterState.Attack, "State");
                }
            }

            EnsureCrouchWalkStatesAndTransitions(sm, "IsTurning");
            EnsureTurn180StateAndTransitions(controller, sm, idleState);
            EnsureDashStatesAndTransitions(sm, idleState);

            EditorUtility.SetDirty(controller);
        }

        private static void EnsureCrouchWalkStatesAndTransitions(AnimatorStateMachine sm, string isTurningParam)
        {
            (string stateName, FighterState fState, Vector3 pos)[] crouchWalkDefs =
            {
                ("CrouchWalkForward", FighterState.CrouchWalkForward, new Vector3(480f, -300f, 0f)),
                ("CrouchWalkBackward", FighterState.CrouchWalkBackward, new Vector3(480f, -360f, 0f)),
            };

            foreach (var (stateName, fState, pos) in crouchWalkDefs)
            {
                AnimatorState dst = EnsureAnimatorStateFromBaseFbx(sm, stateName, pos);

                AnimatorStateTransition anyTr = null;
                foreach (AnimatorStateTransition tr in sm.anyStateTransitions)
                {
                    if (tr.destinationState == dst)
                    {
                        anyTr = tr;
                        break;
                    }
                }
                if (anyTr == null)
                {
                    anyTr = sm.AddAnyStateTransition(dst);
                }

                anyTr.hasExitTime = false;
                anyTr.duration = 0.06f;
                anyTr.hasFixedDuration = true;
                anyTr.canTransitionToSelf = false;
                anyTr.conditions = new[]
                {
                    new AnimatorCondition { mode = AnimatorConditionMode.Equals, parameter = "State", threshold = (int)fState },
                    new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = isTurningParam, threshold = 0f },
                };
            }
        }

        private static AnimatorState EnsureAnimatorStateFromBaseFbx(
            AnimatorStateMachine sm,
            string stateName,
            Vector3 position)
        {
            AnimatorState st = FindState(sm, stateName);
            if (st == null)
            {
                st = sm.AddState(stateName, position);
            }

            AnimationClip baseClip = null;
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(LucasFbxPath))
            {
                if (obj is AnimationClip clip && string.Equals(clip.name, stateName, StringComparison.Ordinal))
                {
                    baseClip = clip;
                    break;
                }
            }
            if (baseClip != null)
            {
                st.motion = baseClip;
            }
            st.writeDefaultValues = true;
            return st;
        }

        private static void EnsureDashStatesAndTransitions(AnimatorStateMachine sm, AnimatorState idleState)
        {
            (string stateName, FighterState fState, Vector3 pos)[] dashDefs =
            {
                ("DashForward", FighterState.DashForward, new Vector3(480f, -180f, 0f)),
                ("DashBackward", FighterState.DashBackward, new Vector3(480f, -240f, 0f)),
            };

            foreach (var (stateName, fState, pos) in dashDefs)
            {
                AnimatorState dst = EnsureAnimatorStateFromBaseFbx(sm, stateName, pos);

                AnimatorStateTransition anyTr = null;
                foreach (AnimatorStateTransition tr in sm.anyStateTransitions)
                {
                    if (tr.destinationState == dst)
                    {
                        anyTr = tr;
                        break;
                    }
                }
                if (anyTr == null)
                {
                    anyTr = sm.AddAnyStateTransition(dst);
                }
                anyTr.hasExitTime = false;
                anyTr.duration = 0.03f;
                anyTr.hasFixedDuration = true;
                anyTr.canTransitionToSelf = false;
                anyTr.conditions = new[]
                {
                    new AnimatorCondition { mode = AnimatorConditionMode.Equals, parameter = "State", threshold = (int)fState },
                    new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "IsTurning", threshold = 0f },
                };

                bool hasExitTime = false;
                bool hasStateExit = false;
                foreach (AnimatorStateTransition tr in dst.transitions)
                {
                    if (tr.destinationState != idleState) continue;
                    if (tr.hasExitTime && tr.conditions.Length == 0)
                    {
                        tr.exitTime = 1f;
                        tr.duration = 0.03f;
                        tr.hasFixedDuration = true;
                        hasExitTime = true;
                    }
                    else if (!tr.hasExitTime && tr.conditions.Length == 1 &&
                             tr.conditions[0].parameter == "State" &&
                             tr.conditions[0].mode == AnimatorConditionMode.NotEqual &&
                             Mathf.RoundToInt(tr.conditions[0].threshold) == (int)fState)
                    {
                        tr.duration = 0.03f;
                        tr.hasFixedDuration = true;
                        hasStateExit = true;
                    }
                }

                if (!hasExitTime)
                {
                    AnimatorStateTransition tr = dst.AddTransition(idleState);
                    tr.hasExitTime = true;
                    tr.exitTime = 1f;
                    tr.duration = 0.03f;
                    tr.hasFixedDuration = true;
                }
                if (!hasStateExit)
                {
                    AnimatorStateTransition tr = dst.AddTransition(idleState);
                    tr.hasExitTime = false;
                    tr.duration = 0.03f;
                    tr.hasFixedDuration = true;
                    tr.AddCondition(AnimatorConditionMode.NotEqual, (int)fState, "State");
                }
            }
        }

        private static void EnsureTurn180StateAndTransitions(
            AnimatorController controller,
            AnimatorStateMachine sm,
            AnimatorState idleState)
        {
            const string isTurningParam = "IsTurning";
            bool hasIsTurningParam = false;
            foreach (AnimatorControllerParameter p in controller.parameters)
            {
                if (string.Equals(p.name, isTurningParam, StringComparison.Ordinal))
                {
                    hasIsTurningParam = true;
                    break;
                }
            }
            if (!hasIsTurningParam)
            {
                controller.AddParameter(new AnimatorControllerParameter
                {
                    name = isTurningParam,
                    type = AnimatorControllerParameterType.Bool,
                    defaultBool = false
                });
            }

            // Guard AnyState grounded-locomotion transitions on IsTurning == false so Turn180 is not immediately aborted
            var guardedAnyTargets = new HashSet<string>(StringComparer.Ordinal)
            {
                "Idle", "WalkForward", "WalkBackward", "Crouch", "CrouchWalkForward", "CrouchWalkBackward"
            };
            foreach (AnimatorStateTransition tr in sm.anyStateTransitions)
            {
                if (tr.destinationState == null || !guardedAnyTargets.Contains(tr.destinationState.name))
                {
                    continue;
                }
                bool hasGuard = false;
                foreach (AnimatorCondition cond in tr.conditions)
                {
                    if (string.Equals(cond.parameter, isTurningParam, StringComparison.Ordinal))
                    {
                        hasGuard = true;
                        break;
                    }
                }
                if (!hasGuard)
                {
                    tr.AddCondition(AnimatorConditionMode.IfNot, 0f, isTurningParam);
                }
            }

            AnimatorState turnState = FindState(sm, "Turn180");
            if (turnState == null)
            {
                turnState = sm.AddState("Turn180", new Vector3(480f, -120f, 0f));
            }

            AnimationClip turnBaseClip = null;
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(LucasFbxPath))
            {
                if (obj is AnimationClip clip && string.Equals(clip.name, "Turn180", StringComparison.Ordinal))
                {
                    turnBaseClip = clip;
                    break;
                }
            }
            if (turnBaseClip != null)
            {
                turnState.motion = turnBaseClip;
            }
            turnState.writeDefaultValues = true;

            // Natural completion back to Idle
            bool hasIdleExitTime = false;
            foreach (AnimatorStateTransition tr in turnState.transitions)
            {
                if (tr.destinationState == idleState && tr.hasExitTime && tr.conditions.Length == 0)
                {
                    tr.exitTime = 1f;
                    tr.duration = 0.04f;
                    tr.hasFixedDuration = true;
                    hasIdleExitTime = true;
                    break;
                }
            }
            if (!hasIdleExitTime)
            {
                AnimatorStateTransition tr = turnState.AddTransition(idleState);
                tr.hasExitTime = true;
                tr.exitTime = 1f;
                tr.duration = 0.04f;
                tr.hasFixedDuration = true;
            }

            // Immediate exits (duration 0.04s) for locomotion & action states
            (string stateName, FighterState fState, bool requireTurnEnd)[] cancelExits =
            {
                ("Idle", FighterState.Idle, true),
                ("WalkForward", FighterState.WalkForward, true),
                ("WalkBackward", FighterState.WalkBackward, true),
                ("Crouch", FighterState.Crouch, true),
                ("CrouchWalkForward", FighterState.CrouchWalkForward, true),
                ("CrouchWalkBackward", FighterState.CrouchWalkBackward, true),
                ("DashForward", FighterState.DashForward, false),
                ("DashBackward", FighterState.DashBackward, false),
                ("Jump", FighterState.Jump, false),
                ("Block", FighterState.Block, false),
                ("HitReact", FighterState.Hit, false),
                ("KnockDown", FighterState.KnockDown, false),
                ("GetUp", FighterState.GetUp, false),
                ("Victory", FighterState.Victory, false),
                ("Defeat", FighterState.Defeat, false),
            };

            foreach (var (dstName, fState, requireTurnEnd) in cancelExits)
            {
                AnimatorState dst = FindState(sm, dstName);
                if (dst == null) continue;

                AnimatorStateTransition existing = null;
                foreach (AnimatorStateTransition tr in turnState.transitions)
                {
                    if (tr.destinationState != dst || tr.hasExitTime) continue;
                    existing = tr;
                    break;
                }
                if (existing == null)
                {
                    existing = turnState.AddTransition(dst);
                }

                existing.hasExitTime = false;
                existing.duration = 0.04f;
                existing.hasFixedDuration = true;
                existing.conditions = requireTurnEnd
                    ? new[]
                    {
                        new AnimatorCondition { mode = AnimatorConditionMode.Equals, parameter = "State", threshold = (int)fState },
                        new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = isTurningParam, threshold = 0f },
                    }
                    : new[]
                    {
                        new AnimatorCondition { mode = AnimatorConditionMode.Equals, parameter = "State", threshold = (int)fState },
                    };
            }
        }

        private static AnimatorState FindState(AnimatorStateMachine sm, string name)
        {
            foreach (ChildAnimatorState cs in sm.states)
            {
                if (string.Equals(cs.state.name, name, StringComparison.Ordinal))
                {
                    return cs.state;
                }
            }
            return null;
        }

        public static AnimatorOverrideController CreateOrUpdateOverrideController(
            string overridePath,
            string baseControllerPath,
            string fbxPath)
        {
            var baseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(baseControllerPath);
            if (baseController == null)
            {
                throw new InvalidOperationException($"Base controller missing at {baseControllerPath}");
            }

            var clipsByName = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                {
                    clipsByName[clip.name] = clip;
                }
            }

            foreach (string req in RequiredClips)
            {
                if (!clipsByName.ContainsKey(req))
                {
                    throw new InvalidOperationException($"Clip '{req}' missing from {fbxPath}");
                }
            }

            AnimatorOverrideController aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
            if (aoc == null)
            {
                aoc = new AnimatorOverrideController(baseController);
                AssetDatabase.CreateAsset(aoc, overridePath);
            }
            else
            {
                aoc.runtimeAnimatorController = baseController;
            }

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(aoc.overridesCount);
            aoc.GetOverrides(overrides);
            for (int i = 0; i < overrides.Count; i++)
            {
                AnimationClip orig = overrides[i].Key;
                if (orig != null && clipsByName.TryGetValue(orig.name, out AnimationClip targetClip))
                {
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(orig, targetClip);
                }
            }
            aoc.ApplyOverrides(overrides);
            EditorUtility.SetDirty(aoc);
            return aoc;
        }

        public static void AssignAnimatorControllerToPrefab(string prefabPath, RuntimeAnimatorController controller)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Animator anim = root.GetComponentInChildren<Animator>(true);
                if (anim == null)
                {
                    throw new InvalidOperationException($"No Animator found inside {prefabPath}");
                }
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void CreateOrUpdateFighterMoveset(
            string fighterName,
            FightingArchetype archetype,
            string fighterDataPath,
            MoveSpec[] specs)
        {
            FighterData data = AssetDatabase.LoadAssetAtPath<FighterData>(fighterDataPath);
            if (data == null)
            {
                throw new InvalidOperationException($"FighterData not found at {fighterDataPath}");
            }

            string folder = $"Assets/UnDFight/Data/Moves/{fighterName}";
            EnsureFolderExists(folder);

            foreach (MoveSpec spec in specs)
            {
                string typeKey = spec.Type.ToString();
                string displayName = string.IsNullOrEmpty(spec.MoveName) ? typeKey : spec.MoveName;
                string assetPath = $"{folder}/MoveData_{fighterName}_{typeKey}.asset";
                MoveData move = AssetDatabase.LoadAssetAtPath<MoveData>(assetPath);
                if (move == null)
                {
                    move = ScriptableObject.CreateInstance<MoveData>();
                    AssetDatabase.CreateAsset(move, assetPath);
                }

                move.Configure(
                    displayName,
                    spec.Type,
                    spec.Startup,
                    spec.Active,
                    spec.Recovery,
                    spec.Damage,
                    spec.Hitstun,
                    spec.Blockstun,
                    spec.Hitstop,
                    spec.Knockback,
                    spec.Height,
                    spec.CausesKnockdown,
                    spec.HitboxOffset,
                    spec.HitboxSize,
                    spec.ChipDamage,
                    spec.LungeSpeed,
                    spec.IsThrow,
                    spec.AirOnly);
                move.specialMechanic = spec.SpecialMechanic;
                move.superCost = spec.SuperCost;
                move.isExMove = spec.IsExMove;
                move.armorStartTick = spec.ArmorStartTick;
                move.armorEndTick = spec.ArmorEndTick;
                move.antiAirInvulnerable = spec.AntiAirInvulnerable;
                move.antiAirStartTick = spec.AntiAirStartTick;
                move.antiAirEndTick = spec.AntiAirEndTick;
                move.secondActiveStartTick = spec.SecondActiveStartTick;
                move.secondActiveEndTick = spec.SecondActiveEndTick;
                move.secondHitDamage = spec.SecondHitDamage;
                move.spawnsProjectile = spec.SpawnsProjectile;
                if (spec.ProjectileSpeed > 0f) move.projectileSpeed = spec.ProjectileSpeed;
                if (spec.ProjectileLifetimeTicks > 0) move.projectileLifetimeTicks = spec.ProjectileLifetimeTicks;
                if (spec.ProjectileSpawnOffset != Vector2.zero) move.projectileSpawnOffset = spec.ProjectileSpawnOffset;
                if (spec.ProjectileHitboxSize != Vector2.zero) move.projectileHitboxSize = spec.ProjectileHitboxSize;
                move.superFreezeTicks = spec.SuperFreezeTicks;
                EditorUtility.SetDirty(move);

                switch (spec.Type)
                {
                    case AttackType.LightPunch:
                        data.lightPunch = move;
                        break;
                    case AttackType.HeavyPunch:
                        data.heavyPunch = move;
                        break;
                    case AttackType.LightKick:
                        data.lightKick = move;
                        break;
                    case AttackType.HeavyKick:
                        data.heavyKick = move;
                        break;
                    case AttackType.CrouchKick:
                        data.crouchKick = move;
                        break;
                    case AttackType.JumpAttack:
                        data.jumpAttack = move;
                        break;
                    case AttackType.Throw:
                        data.throwMove = move;
                        break;
                    case AttackType.ExHeavyPunch:
                        data.exHeavyPunch = move;
                        break;
                    case AttackType.ExHeavyKick:
                        data.exHeavyKick = move;
                        break;
                    case AttackType.Special:
                        data.specialMove = move;
                        break;
                }
            }

            data.archetype = archetype;
            EditorUtility.SetDirty(data);
        }

        /// <summary>
        /// One-call helper to scaffold a future fighter's moveset and AnimatorOverrideController
        /// using the appropriate weight-class preset.
        /// </summary>
        public static void SetupFighterFromPreset(
            string fighterName,
            FightingArchetype archetype,
            string fighterDataPath,
            string prefabPath,
            string fbxPath,
            string overridePath,
            string expectedTakePrefix)
        {
            FighterData data = AssetDatabase.LoadAssetAtPath<FighterData>(fighterDataPath);
            if (data == null) throw new InvalidOperationException($"FighterData missing at {fighterDataPath}");

            ReimportAndConfigureFbxClips(fbxPath, expectedTakePrefix);
            EnsureAttackExitTransitions(BaseControllerPath);
            AnimatorOverrideController aoc = CreateOrUpdateOverrideController(overridePath, BaseControllerPath, fbxPath);
            AssignAnimatorControllerToPrefab(prefabPath, aoc);
            MoveSpec[] specs = GetWeightClassPresetSpecs(data.WeightCategory);
            CreateOrUpdateFighterMoveset(fighterName, archetype, fighterDataPath, specs);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void SetupCompleteFighter(
            string fighterName,
            FightingArchetype archetype,
            float heightMeters,
            float weightKg,
            float walkForwardSpeed,
            float walkBackwardSpeed,
            float jumpForce,
            string fbxPath,
            string expectedTakePrefix,
            string overridePath,
            string materialPath,
            string textureFolder,
            string prefabPath,
            string fighterDataPath)
        {
            Material mat = CreateOrUpdateUrpMaterial(materialPath, textureFolder);
            ReimportAndConfigureFbxClips(fbxPath, expectedTakePrefix);
            EnsureAttackExitTransitions(BaseControllerPath);
            AnimatorOverrideController aoc = CreateOrUpdateOverrideController(overridePath, BaseControllerPath, fbxPath);

            FighterData data = CreateOrUpdateFighterData(
                fighterDataPath,
                fighterName,
                archetype,
                heightMeters,
                weightKg,
                walkForwardSpeed,
                walkBackwardSpeed,
                jumpForce);

            MoveSpec[] specs = BuildArchetypeMoveSpecs(data.WeightCategory, archetype);
            CreateOrUpdateFighterMoveset(fighterName, archetype, fighterDataPath, specs);

            GameObject prefab = CreateOrUpdateFighterPrefab(prefabPath, fighterName, fbxPath, mat, aoc, data);
            data.prefab = prefab;
            EditorUtility.SetDirty(data);
        }

        private static MoveSpec[] BuildArchetypeMoveSpecs(WeightClass weightClass, FightingArchetype archetype)
        {
            var list = new List<MoveSpec>(GetWeightClassPresetSpecs(weightClass));
            MoveSpec[] specs = list.ToArray();

            MoveSpec baseHp = default;
            MoveSpec baseHk = default;
            foreach (MoveSpec s in specs)
            {
                if (s.Type == AttackType.HeavyPunch) baseHp = s;
                else if (s.Type == AttackType.HeavyKick) baseHk = s;
            }

            bool armorExHp = archetype == FightingArchetype.HeavyBrawler || archetype == FightingArchetype.StreetScrappy;
            bool antiAirExHk = archetype == FightingArchetype.AgileStriker || archetype == FightingArchetype.TechnicalStriker;

            var exHpSpec = new MoveSpec
            {
                MoveName = "EX Soco Forte",
                Type = AttackType.ExHeavyPunch,
                Startup = Mathf.Max(4, baseHp.Startup - 2),
                Active = baseHp.Active + 1,
                Recovery = Mathf.Max(10, baseHp.Recovery - 2),
                Damage = Mathf.RoundToInt(baseHp.Damage * 1.30f),
                ChipDamage = 18,
                Hitstun = baseHp.Hitstun + 4,
                Blockstun = baseHp.Blockstun + 3,
                Hitstop = baseHp.Hitstop + 2,
                Knockback = baseHp.Knockback + 0.8f,
                Height = HitHeight.Mid,
                CausesKnockdown = false,
                HitboxOffset = baseHp.HitboxOffset + new Vector2(0.08f, 0f),
                HitboxSize = baseHp.HitboxSize + new Vector2(0.14f, 0.08f),
                LungeSpeed = armorExHp ? 2.2f : 4.2f,
                SuperCost = FighterData.ExSuperCost,
                IsExMove = true,
                ArmorStartTick = armorExHp ? 2 : 0,
                ArmorEndTick = armorExHp ? (Mathf.Max(4, baseHp.Startup - 2) + baseHp.Active + 1) : 0,
            };

            var exHkSpec = new MoveSpec
            {
                MoveName = "EX Chute Forte",
                Type = AttackType.ExHeavyKick,
                Startup = Mathf.Max(5, baseHk.Startup - 2),
                Active = baseHk.Active + 1,
                Recovery = Mathf.Max(12, baseHk.Recovery - 2),
                Damage = Mathf.RoundToInt(baseHk.Damage * 1.30f),
                ChipDamage = 20,
                Hitstun = baseHk.Hitstun + 4,
                Blockstun = baseHk.Blockstun + 3,
                Hitstop = baseHk.Hitstop + 2,
                Knockback = baseHk.Knockback + 0.9f,
                Height = HitHeight.High,
                CausesKnockdown = true,
                HitboxOffset = baseHk.HitboxOffset + new Vector2(0.10f, 0.04f),
                HitboxSize = baseHk.HitboxSize + new Vector2(0.16f, 0.14f),
                LungeSpeed = antiAirExHk ? 3.0f : 2.6f,
                SuperCost = FighterData.ExSuperCost,
                IsExMove = true,
                AntiAirInvulnerable = antiAirExHk,
                AntiAirStartTick = antiAirExHk ? 1 : 0,
                AntiAirEndTick = antiAirExHk ? (Mathf.Max(5, baseHk.Startup - 2) + baseHk.Active + 1) : 0,
            };

            MoveSpec specialSpec;
            switch (archetype)
            {
                case FightingArchetype.HeavyBrawler:
                    specialSpec = new MoveSpec
                    {
                        MoveName = "Martelo do Asfalto",
                        Type = AttackType.Special,
                        Startup = 10,
                        Active = 5,
                        Recovery = 17,
                        Damage = 250,
                        ChipDamage = 35,
                        Hitstun = 24,
                        Blockstun = 16,
                        Hitstop = 14,
                        Knockback = 5.4f,
                        Height = HitHeight.Mid,
                        CausesKnockdown = true,
                        HitboxOffset = new Vector2(0.92f, 1.16f),
                        HitboxSize = new Vector2(1.18f, 0.72f),
                        LungeSpeed = 1.8f,
                        SpecialMechanic = SpecialMechanicType.ArmoredSmash,
                        SuperCost = FighterData.SpecialSuperCost,
                        ArmorStartTick = 2,
                        ArmorEndTick = 14,
                        SuperFreezeTicks = 14,
                    };
                    break;

                case FightingArchetype.StreetScrappy:
                    for (int i = 0; i < specs.Length; i++)
                    {
                        if (specs[i].Type == AttackType.HeavyPunch)
                        {
                            specs[i].HitboxOffset = new Vector2(0.82f, 1.18f);
                            specs[i].HitboxSize = new Vector2(1.06f, 0.50f);
                        }
                    }
                    specialSpec = new MoveSpec
                    {
                        MoveName = "Sequência de Rua",
                        Type = AttackType.Special,
                        Startup = 7,
                        Active = 3,
                        Recovery = 18,
                        Damage = 105,
                        SecondHitDamage = 145,
                        SecondActiveStartTick = 12,
                        SecondActiveEndTick = 14,
                        ChipDamage = 30,
                        Hitstun = 22,
                        Blockstun = 15,
                        Hitstop = 10,
                        Knockback = 4.6f,
                        Height = HitHeight.Mid,
                        CausesKnockdown = true,
                        HitboxOffset = new Vector2(0.88f, 1.18f),
                        HitboxSize = new Vector2(1.16f, 0.66f),
                        LungeSpeed = 3.8f,
                        SpecialMechanic = SpecialMechanicType.MultiHitRush,
                        SuperCost = FighterData.SpecialSuperCost,
                        SuperFreezeTicks = 14,
                    };
                    break;

                case FightingArchetype.TechnicalStriker:
                    for (int i = 0; i < specs.Length; i++)
                    {
                        if (specs[i].Type == AttackType.LightPunch)
                        {
                            specs[i].Damage = 44;
                            specs[i].HitboxOffset = new Vector2(0.64f, 1.12f);
                        }
                        else if (specs[i].Type == AttackType.LightKick)
                        {
                            specs[i].Damage = 54;
                            specs[i].HitboxOffset = new Vector2(0.72f, 0.48f);
                        }
                    }
                    specialSpec = new MoveSpec
                    {
                        MoveName = "Golpe Sombrio",
                        Type = AttackType.Special,
                        Startup = 7,
                        Active = 4,
                        Recovery = 13,
                        Damage = 210,
                        ChipDamage = 28,
                        Hitstun = 22,
                        Blockstun = 14,
                        Hitstop = 10,
                        Knockback = 4.4f,
                        Height = HitHeight.Mid,
                        CausesKnockdown = true,
                        HitboxOffset = new Vector2(0.90f, 1.14f),
                        HitboxSize = new Vector2(1.14f, 0.58f),
                        LungeSpeed = 1.6f,
                        SpecialMechanic = SpecialMechanicType.ProjectileShot,
                        SuperCost = FighterData.SpecialSuperCost,
                        SpawnsProjectile = true,
                        ProjectileSpeed = 10.5f,
                        ProjectileLifetimeTicks = 45,
                        ProjectileSpawnOffset = new Vector2(0.70f, 1.15f),
                        ProjectileHitboxSize = new Vector2(0.58f, 0.48f),
                        SuperFreezeTicks = 14,
                    };
                    break;

                default: // AgileStriker (Rochard)
                    for (int i = 0; i < specs.Length; i++)
                    {
                        if (specs[i].Type == AttackType.LightKick)
                        {
                            specs[i].HitboxOffset = new Vector2(0.78f, 0.54f);
                            specs[i].HitboxSize = new Vector2(0.92f, 0.44f);
                        }
                        else if (specs[i].Type == AttackType.HeavyKick)
                        {
                            specs[i].Knockback = 3.0f;
                            specs[i].HitboxOffset = new Vector2(0.88f, 1.24f);
                            specs[i].HitboxSize = new Vector2(1.08f, 0.56f);
                        }
                    }
                    specialSpec = new MoveSpec
                    {
                        MoveName = "Tornado Relâmpago",
                        Type = AttackType.Special,
                        Startup = 6,
                        Active = 4,
                        Recovery = 12,
                        Damage = 225,
                        ChipDamage = 26,
                        Hitstun = 22,
                        Blockstun = 14,
                        Hitstop = 10,
                        Knockback = 4.4f,
                        Height = HitHeight.High,
                        CausesKnockdown = true,
                        HitboxOffset = new Vector2(0.86f, 1.32f),
                        HitboxSize = new Vector2(1.26f, 1.05f),
                        LungeSpeed = 4.6f,
                        SpecialMechanic = SpecialMechanicType.AntiAirSpin,
                        SuperCost = FighterData.SpecialSuperCost,
                        AntiAirInvulnerable = true,
                        AntiAirStartTick = 1,
                        AntiAirEndTick = 12,
                        SuperFreezeTicks = 14,
                    };
                    break;
            }

            list = new List<MoveSpec>(specs) { exHpSpec, exHkSpec, specialSpec };
            return list.ToArray();
        }

        private static void SetPrebakedHeightFlag(string fighterDataPath, bool prebaked)
        {
            FighterData data = AssetDatabase.LoadAssetAtPath<FighterData>(fighterDataPath);
            if (data != null && data.fbxHeightPrebaked != prebaked)
            {
                data.fbxHeightPrebaked = prebaked;
                EditorUtility.SetDirty(data);
            }
        }

        public static Material CreateOrUpdateUrpMaterial(string materialPath, string textureFolder)
        {
            EnsureFolderExists(Path.GetDirectoryName(materialPath)?.Replace('\\', '/'));

            string baseColorPath = $"{textureFolder}/meshy_basecolor.png";
            string metallicPath = $"{textureFolder}/meshy_metallic_smoothness.png";
            string normalPath = $"{textureFolder}/meshy_normal.png";

            var normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }

            Texture2D baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(baseColorPath);
            Texture2D metallicTex = AssetDatabase.LoadAssetAtPath<Texture2D>(metallicPath);
            Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (baseTex == null)
            {
                throw new InvalidOperationException($"Missing basecolor texture at {baseColorPath}");
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                throw new InvalidOperationException("Universal Render Pipeline/Lit shader not found.");
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null)
            {
                mat = new Material(urpLit);
                AssetDatabase.CreateAsset(mat, materialPath);
            }
            else if (mat.shader != urpLit)
            {
                mat.shader = urpLit;
            }

            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_Color", Color.white);
            mat.SetTexture("_BaseMap", baseTex);
            mat.SetTexture("_MainTex", baseTex);

            if (metallicTex != null)
            {
                mat.SetTexture("_MetallicGlossMap", metallicTex);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.SetFloat("_BumpScale", 1.0f);
                mat.EnableKeyword("_NORMALMAP");
            }

            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.35f);
            mat.SetFloat("_GlossMapScale", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static FighterData CreateOrUpdateFighterData(
            string fighterDataPath,
            string fighterName,
            FightingArchetype archetype,
            float heightMeters,
            float weightKg,
            float walkForwardSpeed,
            float walkBackwardSpeed,
            float jumpForce)
        {
            EnsureFolderExists(Path.GetDirectoryName(fighterDataPath)?.Replace('\\', '/'));

            FighterData data = AssetDatabase.LoadAssetAtPath<FighterData>(fighterDataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<FighterData>();
                AssetDatabase.CreateAsset(data, fighterDataPath);
            }

            data.fighterName = fighterName;
            data.archetype = archetype;
            data.heightMeters = heightMeters;
            data.weightKg = weightKg;
            data.fbxHeightPrebaked = true;
            data.maxHealth = 1000;
            data.walkForwardSpeed = walkForwardSpeed;
            data.walkBackwardSpeed = walkBackwardSpeed;
            data.jumpForce = jumpForce;
            data.baseGravity = 34.0f;
            data.crouchHeightRatio = 0.62f;
            data.pushboxRadius = 0.36f;
            EditorUtility.SetDirty(data);
            return data;
        }

        public static GameObject CreateOrUpdateFighterPrefab(
            string prefabPath,
            string fighterName,
            string fbxPath,
            Material material,
            RuntimeAnimatorController controller,
            FighterData fighterData)
        {
            EnsureFolderExists(Path.GetDirectoryName(prefabPath)?.Replace('\\', '/'));

            GameObject fbxRoot = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxRoot == null)
            {
                throw new InvalidOperationException($"FBX asset not found at {fbxPath}");
            }

            Avatar fbxAvatar = null;
            foreach (UnityEngine.Object subAsset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (subAsset is Avatar av)
                {
                    fbxAvatar = av;
                    break;
                }
            }

            GameObject root = new GameObject($"Fighter_{fighterName}");
            try
            {
                if (StagePhysicsLayers.HasDedicatedLayers)
                {
                    root.layer = StagePhysicsLayers.FighterHurtboxLayer;
                }

                CapsuleCollider hurtbox = root.AddComponent<CapsuleCollider>();
                hurtbox.isTrigger = true;
                hurtbox.radius = fighterData.pushboxRadius;
                hurtbox.height = fighterData.heightMeters;
                hurtbox.center = new Vector3(0f, fighterData.heightMeters * 0.5f, 0f);

                Rigidbody hurtboxRb = root.AddComponent<Rigidbody>();
                hurtboxRb.isKinematic = true;
                hurtboxRb.useGravity = false;
                hurtboxRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                hurtboxRb.interpolation = RigidbodyInterpolation.None;

                PlayerInputHandler inputHandler = root.AddComponent<PlayerInputHandler>();
                FighterController controllerComp = root.AddComponent<FighterController>();

                GameObject visual = PrefabUtility.InstantiatePrefab(fbxRoot, root.transform) as GameObject;
                if (visual == null)
                {
                    throw new InvalidOperationException($"Failed to instantiate {fbxPath} into prefab.");
                }

                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                visual.transform.localScale = Vector3.one;

                foreach (SkinnedMeshRenderer smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.sharedMaterial = material;
                }

                Animator anim = visual.GetComponent<Animator>();
                if (anim == null)
                {
                    anim = visual.AddComponent<Animator>();
                }
                if (fbxAvatar != null)
                {
                    anim.avatar = fbxAvatar;
                }
                anim.runtimeAnimatorController = controller;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                anim.applyRootMotion = false;

                var so = new SerializedObject(controllerComp);
                so.FindProperty("fighterData").objectReferenceValue = fighterData;
                so.FindProperty("visualRoot").objectReferenceValue = visual.transform;
                so.FindProperty("hurtboxCollider").objectReferenceValue = hurtbox;
                so.FindProperty("fighterAnimator").objectReferenceValue = anim;
                so.FindProperty("squashVisualOnCrouch").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();

                _ = inputHandler;
                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static void UpdateFighterRoster(params string[] orderedDataPaths)
        {
            FighterRoster roster = AssetDatabase.LoadAssetAtPath<FighterRoster>(FighterRosterPath);
            if (roster == null)
            {
                throw new InvalidOperationException($"FighterRoster not found at {FighterRosterPath}");
            }

            var fighters = new List<FighterData>();
            foreach (string path in orderedDataPaths)
            {
                FighterData data = AssetDatabase.LoadAssetAtPath<FighterData>(path);
                if (data == null)
                {
                    throw new InvalidOperationException($"FighterData missing for roster at {path}");
                }
                fighters.Add(data);
            }

            var so = new SerializedObject(roster);
            SerializedProperty listProp = so.FindProperty("fighters");
            listProp.ClearArray();
            listProp.arraySize = fighters.Count;
            for (int i = 0; i < fighters.Count; i++)
            {
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = fighters[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(roster);
        }

        /// <summary>
        /// Ensures serialized Super Gauge and Combo Counter widgets exist in Arena_Favela and Training scenes,
        /// then restores MainMenu as the active scene.
        /// </summary>
        public static void SerializeFightHudInScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            string[] fightScenePaths =
            {
                "Assets/UnDFight/Scenes/Arena_Favela.unity",
                "Assets/UnDFight/Scenes/Training.unity"
            };

            foreach (string scenePath in fightScenePaths)
            {
                if (!File.Exists(scenePath))
                {
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                FightHUD hud = UnityEngine.Object.FindAnyObjectByType<FightHUD>();
                if (hud != null)
                {
                    hud.EnsureSuperAndComboWidgets();
                    EditorUtility.SetDirty(hud);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }

            const string mainMenuPath = "Assets/UnDFight/Scenes/MainMenu.unity";
            if (File.Exists(mainMenuPath))
            {
                EditorSceneManager.OpenScene(mainMenuPath, OpenSceneMode.Single);
            }
        }

        private static void EnsureFolderExists(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;
            string parent = Path.GetDirectoryName(assetFolderPath)?.Replace('\\', '/');
            string leaf = Path.GetFileName(assetFolderPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolderExists(parent);
            }
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
