using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

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
            public AttackType Type;
            public int Startup;
            public int Active;
            public int Recovery;
            public int Damage;
            public int Hitstun;
            public int Blockstun;
            public int Hitstop;
            public float Knockback;
            public HitHeight Height;
            public bool CausesKnockdown;
            public Vector2 HitboxOffset;
            public Vector2 HitboxSize;

            public int TotalTicks => Startup + Active + Recovery;
        }

        private const string BaseControllerPath = "Assets/UnDFight/Art/Animations/AC_Fighter.controller";
        private const string LucasFbxPath = "Assets/UnDFight/Art/Models/Lucas/Lucas.fbx";
        private const string TiagoFbxPath = "Assets/UnDFight/Art/Models/Tiago/Tiago.fbx";
        private const string LucasOverridePath = "Assets/UnDFight/Art/Animations/AOC_Lucas.overrideController";
        private const string TiagoOverridePath = "Assets/UnDFight/Art/Animations/AOC_Tiago.overrideController";
        private const string LucasPrefabPath = "Assets/UnDFight/Prefabs/Fighter_Lucas.prefab";
        private const string TiagoPrefabPath = "Assets/UnDFight/Prefabs/Fighter_Tiago.prefab";
        private const string LucasDataPath = "Assets/UnDFight/Data/FighterData_Lucas.asset";
        private const string TiagoDataPath = "Assets/UnDFight/Data/FighterData_Tiago.asset";

        private static readonly HashSet<string> LoopClipNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Idle",
            "WalkForward",
            "WalkBackward"
        };

        private static readonly string[] RequiredClips =
        {
            "Idle", "WalkForward", "WalkBackward", "Jump", "Crouch", "BlockStand",
            "LightPunch", "HeavyPunch", "LightKick", "HeavyKick",
            "HitReact", "KnockDown", "GetUp", "Victory", "Defeat"
        };

        public static MoveSpec[] GetWeightClassPresetSpecs(WeightClass weightClass)
        {
            return weightClass switch
            {
                WeightClass.Light => new[]
                {
                    new MoveSpec { Type = AttackType.LightPunch, Startup = 3, Active = 3, Recovery = 7,  Damage = 42,  Hitstun = 11, Blockstun = 7,  Hitstop = 4, Knockback = 1.1f, Height = HitHeight.Mid,  CausesKnockdown = false, HitboxOffset = new Vector2(0.66f, 1.15f), HitboxSize = new Vector2(0.76f, 0.40f) },
                    new MoveSpec { Type = AttackType.HeavyPunch, Startup = 5, Active = 4, Recovery = 12, Damage = 92,  Hitstun = 16, Blockstun = 11, Hitstop = 6, Knockback = 2.2f, Height = HitHeight.Mid,  CausesKnockdown = false, HitboxOffset = new Vector2(0.74f, 1.15f), HitboxSize = new Vector2(0.88f, 0.44f) },
                    new MoveSpec { Type = AttackType.LightKick,  Startup = 4, Active = 3, Recovery = 9,  Damage = 52,  Hitstun = 12, Blockstun = 8,  Hitstop = 4, Knockback = 1.4f, Height = HitHeight.Low,  CausesKnockdown = false, HitboxOffset = new Vector2(0.72f, 0.50f), HitboxSize = new Vector2(0.88f, 0.42f) },
                    new MoveSpec { Type = AttackType.HeavyKick,  Startup = 7, Active = 4, Recovery = 16, Damage = 112, Hitstun = 20, Blockstun = 13, Hitstop = 7, Knockback = 2.9f, Height = HitHeight.High, CausesKnockdown = true,  HitboxOffset = new Vector2(0.84f, 1.20f), HitboxSize = new Vector2(1.04f, 0.54f) },
                },
                WeightClass.Medium => new[]
                {
                    new MoveSpec { Type = AttackType.LightPunch, Startup = 4, Active = 3, Recovery = 9,  Damage = 48,  Hitstun = 12, Blockstun = 8,  Hitstop = 4, Knockback = 1.3f, Height = HitHeight.Mid,  CausesKnockdown = false, HitboxOffset = new Vector2(0.68f, 1.18f), HitboxSize = new Vector2(0.78f, 0.42f) },
                    new MoveSpec { Type = AttackType.HeavyPunch, Startup = 7, Active = 4, Recovery = 16, Damage = 108, Hitstun = 18, Blockstun = 12, Hitstop = 7, Knockback = 2.7f, Height = HitHeight.Mid,  CausesKnockdown = false, HitboxOffset = new Vector2(0.76f, 1.18f), HitboxSize = new Vector2(0.92f, 0.46f) },
                    new MoveSpec { Type = AttackType.LightKick,  Startup = 5, Active = 3, Recovery = 11, Damage = 62,  Hitstun = 14, Blockstun = 9,  Hitstop = 5, Knockback = 1.7f, Height = HitHeight.Low,  CausesKnockdown = false, HitboxOffset = new Vector2(0.74f, 0.52f), HitboxSize = new Vector2(0.92f, 0.44f) },
                    new MoveSpec { Type = AttackType.HeavyKick,  Startup = 9, Active = 4, Recovery = 20, Damage = 130, Hitstun = 22, Blockstun = 14, Hitstop = 8, Knockback = 3.5f, Height = HitHeight.High, CausesKnockdown = true,  HitboxOffset = new Vector2(0.86f, 1.24f), HitboxSize = new Vector2(1.10f, 0.56f) },
                },
                _ => new[]
                {
                    new MoveSpec { Type = AttackType.LightPunch, Startup = 5, Active = 3, Recovery = 11, Damage = 62,  Hitstun = 14, Blockstun = 10, Hitstop = 5, Knockback = 1.9f, Height = HitHeight.Mid,  CausesKnockdown = false, HitboxOffset = new Vector2(0.68f, 1.18f), HitboxSize = new Vector2(0.78f, 0.42f) },
                    new MoveSpec { Type = AttackType.HeavyPunch, Startup = 9, Active = 5, Recovery = 19, Damage = 140, Hitstun = 22, Blockstun = 15, Hitstop = 9, Knockback = 3.7f, Height = HitHeight.Mid,  CausesKnockdown = false, HitboxOffset = new Vector2(0.78f, 1.18f), HitboxSize = new Vector2(0.94f, 0.48f) },
                    new MoveSpec { Type = AttackType.LightKick,  Startup = 6, Active = 4, Recovery = 13, Damage = 78,  Hitstun = 16, Blockstun = 11, Hitstop = 6, Knockback = 2.3f, Height = HitHeight.Low,  CausesKnockdown = false, HitboxOffset = new Vector2(0.74f, 0.52f), HitboxSize = new Vector2(0.92f, 0.44f) },
                    new MoveSpec { Type = AttackType.HeavyKick,  Startup = 11, Active = 5, Recovery = 24, Damage = 165, Hitstun = 26, Blockstun = 17, Hitstop = 10, Knockback = 4.6f, Height = HitHeight.High, CausesKnockdown = true, HitboxOffset = new Vector2(0.88f, 1.24f), HitboxSize = new Vector2(1.12f, 0.56f) },
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

            // 5. Generate fighter-specific MoveData assets using weight-class presets + Tiago mata-cobra hitbox
            MoveSpec[] lucasSpecs = GetWeightClassPresetSpecs(WeightClass.Heavy);
            MoveSpec[] tiagoSpecs = GetWeightClassPresetSpecs(WeightClass.Medium);
            // Tiago HeavyPunch: "mata-cobra" slightly wider hitbox
            for (int i = 0; i < tiagoSpecs.Length; i++)
            {
                if (tiagoSpecs[i].Type == AttackType.HeavyPunch)
                {
                    tiagoSpecs[i].HitboxOffset = new Vector2(0.82f, 1.18f);
                    tiagoSpecs[i].HitboxSize = new Vector2(1.06f, 0.50f);
                }
            }

            CreateOrUpdateFighterMoveset("Lucas", FightingArchetype.HeavyBrawler, LucasDataPath, lucasSpecs);
            CreateOrUpdateFighterMoveset("Tiago", FightingArchetype.StreetScrappy, TiagoDataPath, tiagoSpecs);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "Phase 9 Unity setup complete: Lucas (Heavy/HeavyBrawler) & Tiago (Medium/StreetScrappy) FBXs, AOCs, Prefabs, AC_Fighter transitions, and MoveData assets updated.";
        }

        public static void ReimportAndConfigureFbxClips(string fbxPath, string expectedTakePrefix)
        {
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"ModelImporter not found at {fbxPath}");
            }

            importer.animationType = ModelImporterAnimationType.Human;
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
                match.heightFromFeet = true;
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

            string[] attackStates = { "LightPunch", "HeavyPunch", "LightKick", "HeavyKick" };
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

            EditorUtility.SetDirty(controller);
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
                string moveName = spec.Type.ToString();
                string assetPath = $"{folder}/MoveData_{fighterName}_{moveName}.asset";
                MoveData move = AssetDatabase.LoadAssetAtPath<MoveData>(assetPath);
                if (move == null)
                {
                    move = ScriptableObject.CreateInstance<MoveData>();
                    AssetDatabase.CreateAsset(move, assetPath);
                }

                move.Configure(
                    moveName,
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
                    spec.HitboxSize);
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
