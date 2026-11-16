using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnDFight.Editor
{
    /// <summary>
    /// Idempotent Editor setup utility configuring PhysX layer isolation (StagePhysics, StageDebris,
    /// FighterHurtbox), reusable breakable stage prop prefabs, and Arena_Favela stage interactables.
    /// </summary>
    public static class StagePhysicsSetupEditor
    {
        public const string StagePropsPrefabFolder = "Assets/UnDFight/Prefabs/StageProps";
        public const string CratePrefabPath = StagePropsPrefabFolder + "/Prop_BreakableCrate.prefab";
        public const string CrateStackPrefabPath = StagePropsPrefabFolder + "/Prop_BreakableCrateStack.prefab";
        public const string TablePrefabPath = StagePropsPrefabFolder + "/Prop_BreakableTable.prefab";
        public const string ThrowableChairPrefabPath = StagePropsPrefabFolder + "/Prop_ThrowableChair.prefab";
        public const string ThrowableBottlePrefabPath = StagePropsPrefabFolder + "/Prop_ThrowableBottle.prefab";
        public const string ArenaFavelaScenePath = "Assets/UnDFight/Scenes/Arena_Favela.unity";

        private static readonly string[] FighterPrefabPaths =
        {
            "Assets/UnDFight/Prefabs/Fighter_Lucas.prefab",
            "Assets/UnDFight/Prefabs/Fighter_Tiago.prefab",
            "Assets/UnDFight/Prefabs/Fighter_Jompes.prefab",
            "Assets/UnDFight/Prefabs/Fighter_Rochard.prefab"
        };

        [MenuItem("UnDFight/Setup/Configure Stage Physics & Arena Favela Props", false, 120)]
        public static void ApplyPhase12Setup()
        {
            EnsurePhysicsLayersAndMatrix();
            AssignFighterPrefabsLayerAndKinematicBody();
            CreateOrUpdateStagePropPrefabs();
            SetupArenaFavelaPropsAndManager();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UnDFight] Fase 12.2 setup applied: PhysX layers isolated (incl. StageProjectile), Fighter hurtbox Rigidbodies updated, breakable & throwable StageProps prefabs generated, Arena_Favela updated.");
        }

        public static void EnsurePhysicsLayersAndMatrix()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets != null && assets.Length > 0)
            {
                var tagManager = new SerializedObject(assets[0]);
                SerializedProperty layersProp = tagManager.FindProperty("layers");
                if (layersProp != null && layersProp.isArray)
                {
                    EnsureLayerSlot(layersProp, StagePhysicsLayers.PreferredStagePhysicsLayer, StagePhysicsLayers.StagePhysicsLayerName);
                    EnsureLayerSlot(layersProp, StagePhysicsLayers.PreferredStageDebrisLayer, StagePhysicsLayers.StageDebrisLayerName);
                    EnsureLayerSlot(layersProp, StagePhysicsLayers.PreferredFighterHurtboxLayer, StagePhysicsLayers.FighterHurtboxLayerName);
                    EnsureLayerSlot(layersProp, StagePhysicsLayers.PreferredStageProjectileLayer, StagePhysicsLayers.StageProjectileLayerName);
                    tagManager.ApplyModifiedProperties();
                }
            }

            StagePhysicsLayers.ApplyCollisionIsolation(debrisCollidesWithDebris: true);
            AssetDatabase.SaveAssets();
        }

        public static void AssignFighterPrefabsLayerAndKinematicBody()
        {
            int fighterLayer = LayerMask.NameToLayer(StagePhysicsLayers.FighterHurtboxLayerName);

            for (int i = 0; i < FighterPrefabPaths.Length; i++)
            {
                string path = FighterPrefabPaths[i];
                if (!File.Exists(path))
                {
                    continue;
                }

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                {
                    continue;
                }

                bool dirty = false;
                if (fighterLayer >= 0 && root.layer != fighterLayer)
                {
                    root.layer = fighterLayer;
                    dirty = true;
                }

                Rigidbody rb = root.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = root.AddComponent<Rigidbody>();
                    dirty = true;
                }
                if (!rb.isKinematic || rb.useGravity || rb.collisionDetectionMode != CollisionDetectionMode.ContinuousSpeculative || rb.interpolation != RigidbodyInterpolation.None)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    rb.interpolation = RigidbodyInterpolation.None;
                    dirty = true;
                }

                CapsuleCollider hurtbox = root.GetComponent<CapsuleCollider>();
                if (hurtbox != null && !hurtbox.isTrigger)
                {
                    hurtbox.isTrigger = true;
                    dirty = true;
                }

                if (dirty)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void CreateOrUpdateStagePropPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(StagePropsPrefabFolder))
            {
                Directory.CreateDirectory(StagePropsPrefabFolder);
                AssetDatabase.Refresh();
            }

            Material redPlastic = LoadMaterialOrFallback("Assets/UnDFight/Art/Materials/StageFavela/Favela_PlasticRed.mat");
            Material yellowPlastic = LoadMaterialOrFallback("Assets/UnDFight/Art/Materials/StageFavela/Favela_PlasticYellow.mat");
            Material amberBottle = LoadMaterialOrFallback("Assets/UnDFight/Art/Materials/StageFavela/Favela_BottleAmber.mat");
            Material greenBottle = LoadMaterialOrFallback("Assets/UnDFight/Art/Materials/StageFavela/Favela_BottleGreen.mat");

            BuildCratePrefab(CratePrefabPath, redPlastic, yellowPlastic);
            BuildCrateStackPrefab(CrateStackPrefabPath, redPlastic, yellowPlastic);
            BuildTablePrefab(TablePrefabPath, redPlastic, amberBottle, greenBottle);
            BuildThrowableChairPrefab(ThrowableChairPrefabPath, redPlastic, yellowPlastic);
            BuildThrowableBottlePrefab(ThrowableBottlePrefabPath, amberBottle, greenBottle, yellowPlastic);
        }

        public static void SetupArenaFavelaPropsAndManager()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            Scene scene = EditorSceneManager.OpenScene(ArenaFavelaScenePath, OpenSceneMode.Single);

            FightSimulation sim = Object.FindAnyObjectByType<FightSimulation>();
            MatchManager match = Object.FindAnyObjectByType<MatchManager>();

            GameObject managerGo = GameObject.Find("StageInteractableManager");
            if (managerGo == null)
            {
                managerGo = new GameObject("StageInteractableManager");
            }
            StageInteractableManager manager = managerGo.GetComponent<StageInteractableManager>();
            if (manager == null)
            {
                manager = managerGo.AddComponent<StageInteractableManager>();
            }

            // Wire serialized fields cleanly
            var managerSo = new SerializedObject(manager);
            managerSo.FindProperty("simulation").objectReferenceValue = sim;
            managerSo.FindProperty("matchManager").objectReferenceValue = match;
            managerSo.FindProperty("ensureStagePhysicsBounds").boolValue = true;
            managerSo.ApplyModifiedPropertiesWithoutUndo();

            if (match != null)
            {
                var matchSo = new SerializedObject(match);
                SerializedProperty propRef = matchSo.FindProperty("stageInteractableManager");
                if (propRef != null)
                {
                    propRef.objectReferenceValue = manager;
                    matchSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            manager.EnsureStagePhysicsBounds();

            GameObject propsGroup = GameObject.Find("InteractiveStageProps");
            if (propsGroup != null)
            {
                Object.DestroyImmediate(propsGroup);
            }
            propsGroup = new GameObject("InteractiveStageProps");

            GameObject cratePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CratePrefabPath);
            GameObject crateStackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrateStackPrefabPath);
            GameObject tablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TablePrefabPath);
            GameObject chairPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThrowableChairPrefabPath);
            GameObject bottlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThrowableBottlePrefabPath);

            InstantiateProp(crateStackPrefab, "Breakable_CrateStack_Left", propsGroup.transform, new Vector3(-6.35f, 0f, 1.32f), 8f);
            InstantiateProp(cratePrefab, "Breakable_Crate_MidLeft", propsGroup.transform, new Vector3(-4.85f, 0f, 1.42f), -12f);
            InstantiateProp(chairPrefab, "Throwable_Chair_Left", propsGroup.transform, new Vector3(-3.35f, 0f, 1.22f), -28f);
            InstantiateProp(bottlePrefab, "Throwable_Bottle_Right", propsGroup.transform, new Vector3(3.45f, 0f, 1.22f), 14f);
            InstantiateProp(tablePrefab, "Breakable_Table_Right", propsGroup.transform, new Vector3(5.45f, 0f, 1.38f), 6f);
            InstantiateProp(crateStackPrefab, "Breakable_CrateStack_Right", propsGroup.transform, new Vector3(6.85f, 0f, 1.28f), -10f);

            manager.RefreshDiscoveredProps();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != ArenaFavelaScenePath && File.Exists(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
            else
            {
                EditorSceneManager.OpenScene(PlayFromMainMenuEditor.MainMenuScenePath, OpenSceneMode.Single);
            }
        }

        private static void InstantiateProp(GameObject prefab, string instanceName, Transform parent, Vector3 pos, float rotY)
        {
            if (prefab == null)
            {
                return;
            }

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.name = instanceName;
            inst.transform.localPosition = pos;
            inst.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            inst.transform.localScale = Vector3.one;
        }

        private static void BuildCratePrefab(string prefabPath, Material redMat, Material yellowMat)
        {
            var root = new GameObject("Prop_BreakableCrate");
            try
            {
                var intact = new GameObject("IntactVisual");
                intact.transform.SetParent(root.transform, false);

                CreatePrimitivePart("CrateBody", PrimitiveType.Cube, intact.transform, new Vector3(0f, 0.16f, 0f), Vector3.zero, new Vector3(0.46f, 0.32f, 0.36f), redMat);
                CreatePrimitivePart("CrateRimTop", PrimitiveType.Cube, intact.transform, new Vector3(0f, 0.31f, 0f), Vector3.zero, new Vector3(0.49f, 0.045f, 0.39f), yellowMat);

                var col = root.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.17f, 0f);
                col.size = new Vector3(0.50f, 0.35f, 0.40f);

                var pool = new GameObject("DebrisPool");
                pool.transform.SetParent(root.transform, false);
                for (int i = 0; i < 6; i++)
                {
                    Material mat = (i % 3 == 0) ? yellowMat : redMat;
                    Vector3 pos = new Vector3(((i % 3) - 1) * 0.14f, 0.10f + (i / 3) * 0.14f, (i % 2 == 0 ? -0.09f : 0.09f));
                    Vector3 rot = new Vector3(i * 17f, i * 41f, i * 13f);
                    Vector3 scl = new Vector3(0.22f, 0.06f, 0.14f);
                    CreateDebrisShard($"Shard_{i}", PrimitiveType.Cube, pool.transform, pos, rot, scl, mat);
                }

                ConfigureBreakableComponent(root, intact, col, pool.transform, StagePropMaterialType.Plastic, 2, 1.45f, 1.85f);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildCrateStackPrefab(string prefabPath, Material redMat, Material yellowMat)
        {
            var root = new GameObject("Prop_BreakableCrateStack");
            try
            {
                var intact = new GameObject("IntactVisual");
                intact.transform.SetParent(root.transform, false);

                CreatePrimitivePart("Crate_0", PrimitiveType.Cube, intact.transform, new Vector3(0f, 0.16f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0.46f, 0.32f, 0.36f), redMat);
                CreatePrimitivePart("Crate_1", PrimitiveType.Cube, intact.transform, new Vector3(0f, 0.49f, 0f), new Vector3(0f, 4f, 0f), new Vector3(0.46f, 0.32f, 0.36f), redMat);
                CreatePrimitivePart("Crate_2", PrimitiveType.Cube, intact.transform, new Vector3(0f, 0.82f, 0f), new Vector3(0f, 9f, 0f), new Vector3(0.46f, 0.32f, 0.36f), yellowMat);

                var col = root.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.50f, 0f);
                col.size = new Vector3(0.52f, 1.00f, 0.42f);

                var pool = new GameObject("DebrisPool");
                pool.transform.SetParent(root.transform, false);
                for (int i = 0; i < 8; i++)
                {
                    Material mat = (i >= 5) ? yellowMat : redMat;
                    Vector3 pos = new Vector3(((i % 2) == 0 ? -0.12f : 0.12f), 0.16f + (i / 2) * 0.22f, ((i % 3) - 1) * 0.08f);
                    Vector3 rot = new Vector3(i * 21f, i * 33f, i * 15f);
                    Vector3 scl = new Vector3(0.24f, 0.08f, 0.16f);
                    CreateDebrisShard($"Shard_{i}", PrimitiveType.Cube, pool.transform, pos, rot, scl, mat);
                }

                ConfigureBreakableComponent(root, intact, col, pool.transform, StagePropMaterialType.Plastic, 2, 1.65f, 1.85f);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildTablePrefab(string prefabPath, Material redMat, Material amberBottleMat, Material greenBottleMat)
        {
            var root = new GameObject("Prop_BreakableTable");
            try
            {
                var intact = new GameObject("IntactVisual");
                intact.transform.SetParent(root.transform, false);

                CreatePrimitivePart("TableTop", PrimitiveType.Cube, intact.transform, new Vector3(0f, 0.72f, 0f), Vector3.zero, new Vector3(0.84f, 0.05f, 0.84f), redMat);
                CreatePrimitivePart("Leg_FL", PrimitiveType.Cube, intact.transform, new Vector3(-0.34f, 0.36f, -0.34f), Vector3.zero, new Vector3(0.05f, 0.72f, 0.05f), redMat);
                CreatePrimitivePart("Leg_BL", PrimitiveType.Cube, intact.transform, new Vector3(-0.34f, 0.36f, 0.34f), Vector3.zero, new Vector3(0.05f, 0.72f, 0.05f), redMat);
                CreatePrimitivePart("Leg_FR", PrimitiveType.Cube, intact.transform, new Vector3(0.34f, 0.36f, -0.34f), Vector3.zero, new Vector3(0.05f, 0.72f, 0.05f), redMat);
                CreatePrimitivePart("Leg_BR", PrimitiveType.Cube, intact.transform, new Vector3(0.34f, 0.36f, 0.34f), Vector3.zero, new Vector3(0.05f, 0.72f, 0.05f), redMat);
                CreatePrimitivePart("TableBottle_1", PrimitiveType.Cylinder, intact.transform, new Vector3(0.06f, 0.86f, -0.05f), Vector3.zero, new Vector3(0.06f, 0.13f, 0.06f), amberBottleMat);
                CreatePrimitivePart("TableBottle_2", PrimitiveType.Cylinder, intact.transform, new Vector3(-0.14f, 0.86f, 0.10f), Vector3.zero, new Vector3(0.06f, 0.13f, 0.06f), greenBottleMat);

                var col = root.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.48f, 0f);
                col.size = new Vector3(0.88f, 0.96f, 0.88f);

                var pool = new GameObject("DebrisPool");
                pool.transform.SetParent(root.transform, false);
                CreateDebrisShard("TopShard_0", PrimitiveType.Cube, pool.transform, new Vector3(-0.20f, 0.72f, -0.18f), new Vector3(4f, 12f, 8f), new Vector3(0.40f, 0.05f, 0.40f), redMat);
                CreateDebrisShard("TopShard_1", PrimitiveType.Cube, pool.transform, new Vector3(0.20f, 0.72f, -0.18f), new Vector3(-5f, -15f, 6f), new Vector3(0.40f, 0.05f, 0.40f), redMat);
                CreateDebrisShard("TopShard_2", PrimitiveType.Cube, pool.transform, new Vector3(-0.18f, 0.72f, 0.20f), new Vector3(6f, 22f, -7f), new Vector3(0.40f, 0.05f, 0.40f), redMat);
                CreateDebrisShard("TopShard_3", PrimitiveType.Cube, pool.transform, new Vector3(0.20f, 0.72f, 0.20f), new Vector3(-4f, -8f, -5f), new Vector3(0.40f, 0.05f, 0.40f), redMat);
                CreateDebrisShard("LegShard_0", PrimitiveType.Cube, pool.transform, new Vector3(-0.30f, 0.34f, -0.26f), new Vector3(12f, 0f, 15f), new Vector3(0.06f, 0.48f, 0.06f), redMat);
                CreateDebrisShard("LegShard_1", PrimitiveType.Cube, pool.transform, new Vector3(0.30f, 0.34f, 0.26f), new Vector3(-14f, 10f, -12f), new Vector3(0.06f, 0.48f, 0.06f), redMat);
                CreateDebrisShard("BottleShard_1", PrimitiveType.Cube, pool.transform, new Vector3(0.06f, 0.85f, -0.05f), new Vector3(18f, 35f, 24f), new Vector3(0.07f, 0.14f, 0.07f), amberBottleMat);
                CreateDebrisShard("BottleShard_2", PrimitiveType.Cube, pool.transform, new Vector3(-0.14f, 0.85f, 0.10f), new Vector3(-20f, 45f, -18f), new Vector3(0.07f, 0.14f, 0.07f), greenBottleMat);

                ConfigureBreakableComponent(root, intact, col, pool.transform, StagePropMaterialType.Plastic, 2, 1.65f, 1.85f);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureBreakableComponent(
            GameObject root,
            GameObject intactVisual,
            Collider intactCol,
            Transform poolTransform,
            StagePropMaterialType matType,
            int integrity,
            float radiusX,
            float zTolerance)
        {
            var prop = root.AddComponent<StageBreakableProp>();
            var so = new SerializedObject(prop);
            so.FindProperty("maxIntegrity").intValue = integrity;
            so.FindProperty("triggerRadiusX").floatValue = radiusX;
            so.FindProperty("maxZDistanceFromFightPlane").floatValue = zTolerance;
            so.FindProperty("fightPlaneZ").floatValue = 0f;
            so.FindProperty("materialType").enumValueIndex = (int)matType;
            so.FindProperty("intactVisualRoot").objectReferenceValue = intactVisual;
            so.FindProperty("intactCollider").objectReferenceValue = intactCol;
            so.FindProperty("debrisPoolRoot").objectReferenceValue = poolTransform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreatePrimitivePart(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 localPos,
            Vector3 localRot,
            Vector3 localScale,
            Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localRot);
            go.transform.localScale = localScale;

            Collider primCol = go.GetComponent<Collider>();
            if (primCol != null)
            {
                Object.DestroyImmediate(primCol);
            }

            if (mat != null)
            {
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        private static void CreateDebrisShard(
            string name,
            PrimitiveType type,
            Transform poolParent,
            Vector3 localPos,
            Vector3 localRot,
            Vector3 localScale,
            Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = StagePhysicsLayers.StageDebrisLayer;
            go.transform.SetParent(poolParent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localRot);
            go.transform.localScale = localScale;

            if (mat != null)
            {
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var frag = go.AddComponent<StageDebrisFragment>();
            frag.EnsureComponents();
            go.SetActive(false);
        }

        private static void BuildThrowableChairPrefab(string prefabPath, Material redMat, Material yellowMat)
        {
            var root = new GameObject("Prop_ThrowableChair");
            root.layer = StagePhysicsLayers.StageProjectileLayer;
            try
            {
                var intact = new GameObject("IntactVisual");
                intact.transform.SetParent(root.transform, false);

                CreatePrimitivePart("Seat", PrimitiveType.Cube, intact.transform, new Vector3(0f, 0.44f, 0f), Vector3.zero, new Vector3(0.44f, 0.045f, 0.44f), redMat);
                CreatePrimitivePart("Backrest", PrimitiveType.Cube, intact.transform, new Vector3(0f, 0.68f, 0.19f), new Vector3(6f, 0f, 0f), new Vector3(0.44f, 0.44f, 0.04f), redMat);
                CreatePrimitivePart("BackStripe", PrimitiveType.Cube, intact.transform, new Vector3(0f, 0.72f, 0.165f), new Vector3(6f, 0f, 0f), new Vector3(0.32f, 0.10f, 0.02f), yellowMat);
                CreatePrimitivePart("Leg_FL", PrimitiveType.Cube, intact.transform, new Vector3(-0.18f, 0.22f, -0.18f), Vector3.zero, new Vector3(0.05f, 0.44f, 0.05f), redMat);
                CreatePrimitivePart("Leg_FR", PrimitiveType.Cube, intact.transform, new Vector3(0.18f, 0.22f, -0.18f), Vector3.zero, new Vector3(0.05f, 0.44f, 0.05f), redMat);
                CreatePrimitivePart("Leg_BL", PrimitiveType.Cube, intact.transform, new Vector3(-0.18f, 0.22f, 0.18f), Vector3.zero, new Vector3(0.05f, 0.44f, 0.05f), redMat);
                CreatePrimitivePart("Leg_BR", PrimitiveType.Cube, intact.transform, new Vector3(0.18f, 0.22f, 0.18f), Vector3.zero, new Vector3(0.05f, 0.44f, 0.05f), redMat);

                var col = root.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.45f, 0f);
                col.size = new Vector3(0.48f, 0.88f, 0.48f);

                var rb = root.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.mass = 1.0f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                var pool = new GameObject("DebrisPool");
                pool.transform.SetParent(root.transform, false);
                CreateDebrisShard("SeatShard_0", PrimitiveType.Cube, pool.transform, new Vector3(-0.10f, 0.44f, 0f), new Vector3(5f, 18f, -10f), new Vector3(0.22f, 0.045f, 0.36f), redMat);
                CreateDebrisShard("SeatShard_1", PrimitiveType.Cube, pool.transform, new Vector3(0.10f, 0.44f, 0f), new Vector3(-6f, -14f, 12f), new Vector3(0.22f, 0.045f, 0.36f), redMat);
                CreateDebrisShard("BackShard_0", PrimitiveType.Cube, pool.transform, new Vector3(0f, 0.68f, 0.18f), new Vector3(12f, 20f, 8f), new Vector3(0.36f, 0.24f, 0.04f), redMat);
                CreateDebrisShard("StripeShard", PrimitiveType.Cube, pool.transform, new Vector3(0f, 0.72f, 0.16f), new Vector3(-8f, 35f, 15f), new Vector3(0.24f, 0.08f, 0.03f), yellowMat);
                CreateDebrisShard("LegShard_0", PrimitiveType.Cube, pool.transform, new Vector3(-0.16f, 0.22f, -0.14f), new Vector3(16f, 12f, -20f), new Vector3(0.05f, 0.38f, 0.05f), redMat);
                CreateDebrisShard("LegShard_1", PrimitiveType.Cube, pool.transform, new Vector3(0.16f, 0.22f, 0.14f), new Vector3(-14f, -18f, 18f), new Vector3(0.05f, 0.38f, 0.05f), redMat);

                ConfigureThrowableComponent(
                    root,
                    rb,
                    col,
                    intact,
                    pool.transform,
                    promptLabel: "ARREMESSAR CADEIRA",
                    damage: 85,
                    knockdown: true,
                    shatterOnFighter: true,
                    shatterOnStage: false,
                    matType: StagePropMaterialType.Plastic,
                    radiusX: 1.65f,
                    zTolerance: 2.0f);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildThrowableBottlePrefab(string prefabPath, Material amberMat, Material greenMat, Material labelMat)
        {
            var root = new GameObject("Prop_ThrowableBottle");
            root.layer = StagePhysicsLayers.StageProjectileLayer;
            try
            {
                var intact = new GameObject("IntactVisual");
                intact.transform.SetParent(root.transform, false);

                CreatePrimitivePart("BottleBody", PrimitiveType.Cylinder, intact.transform, new Vector3(0f, 0.16f, 0f), Vector3.zero, new Vector3(0.16f, 0.16f, 0.16f), amberMat);
                CreatePrimitivePart("BottleNeck", PrimitiveType.Cylinder, intact.transform, new Vector3(0f, 0.36f, 0f), Vector3.zero, new Vector3(0.075f, 0.10f, 0.075f), greenMat);
                CreatePrimitivePart("BottleLabel", PrimitiveType.Cylinder, intact.transform, new Vector3(0f, 0.17f, 0f), Vector3.zero, new Vector3(0.166f, 0.075f, 0.166f), labelMat);
                CreatePrimitivePart("BottleCap", PrimitiveType.Cylinder, intact.transform, new Vector3(0f, 0.47f, 0f), Vector3.zero, new Vector3(0.085f, 0.02f, 0.085f), labelMat);

                var col = root.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.24f, 0f);
                col.size = new Vector3(0.24f, 0.50f, 0.24f);

                var rb = root.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.mass = 1.0f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                var pool = new GameObject("DebrisPool");
                pool.transform.SetParent(root.transform, false);
                for (int i = 0; i < 6; i++)
                {
                    Material mat = (i % 3 == 0) ? greenMat : amberMat;
                    Vector3 pos = new Vector3(((i % 3) - 1) * 0.06f, 0.12f + (i / 3) * 0.14f, (i % 2 == 0 ? -0.05f : 0.05f));
                    Vector3 rot = new Vector3(i * 22f, i * 48f, i * 19f);
                    Vector3 scl = new Vector3(0.08f, 0.12f, 0.04f);
                    CreateDebrisShard($"GlassShard_{i}", PrimitiveType.Cube, pool.transform, pos, rot, scl, mat);
                }

                ConfigureThrowableComponent(
                    root,
                    rb,
                    col,
                    intact,
                    pool.transform,
                    promptLabel: "ARREMESSAR GARRAFA",
                    damage: 75,
                    knockdown: false,
                    shatterOnFighter: true,
                    shatterOnStage: true,
                    matType: StagePropMaterialType.Glass,
                    radiusX: 1.65f,
                    zTolerance: 2.0f);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureThrowableComponent(
            GameObject root,
            Rigidbody body,
            Collider propCollider,
            GameObject intactVisual,
            Transform poolTransform,
            string promptLabel,
            int damage,
            bool knockdown,
            bool shatterOnFighter,
            bool shatterOnStage,
            StagePropMaterialType matType,
            float radiusX,
            float zTolerance)
        {
            var throwable = root.AddComponent<StageThrowableProp>();
            var so = new SerializedObject(throwable);
            so.FindProperty("interactionRadiusX").floatValue = radiusX;
            so.FindProperty("maxZDistanceFromFightPlane").floatValue = zTolerance;
            so.FindProperty("fightPlaneZ").floatValue = 0f;
            so.FindProperty("promptLabel").stringValue = promptLabel;
            so.FindProperty("damage").intValue = damage;
            so.FindProperty("causesKnockdown").boolValue = knockdown;
            so.FindProperty("breakOnFighterHit").boolValue = shatterOnFighter;
            so.FindProperty("breakOnStageHit").boolValue = shatterOnStage;
            so.FindProperty("materialType").enumValueIndex = (int)matType;
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("propCollider").objectReferenceValue = propCollider;
            so.FindProperty("intactVisualRoot").objectReferenceValue = intactVisual;
            so.FindProperty("debrisPoolRoot").objectReferenceValue = poolTransform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material LoadMaterialOrFallback(string path)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                return mat;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            return urpLit != null ? new Material(urpLit) : null;
        }

        private static void EnsureLayerSlot(SerializedProperty layersProp, int preferredSlot, string layerName)
        {
            for (int i = 0; i < layersProp.arraySize; i++)
            {
                if (layersProp.GetArrayElementAtIndex(i).stringValue == layerName)
                {
                    return;
                }
            }

            if (preferredSlot >= 8 && preferredSlot < layersProp.arraySize)
            {
                SerializedProperty slot = layersProp.GetArrayElementAtIndex(preferredSlot);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = layerName;
                    return;
                }
            }

            for (int i = 8; i < layersProp.arraySize; i++)
            {
                SerializedProperty slot = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = layerName;
                    return;
                }
            }
        }
    }
}
