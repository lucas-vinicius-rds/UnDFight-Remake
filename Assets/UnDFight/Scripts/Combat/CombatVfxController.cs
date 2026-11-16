using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnDFight
{
    /// <summary>
    /// Spawns procedural Hit (orange/yellow) and Block (blue) impact sparks at contact points
    /// and draws optional F1-toggleable world-space Debug Hitbox (red) & Hurtbox (green) rectangles.
    /// </summary>
    public class CombatVfxController : MonoBehaviour
    {
        private sealed class ActiveBurst
        {
            public GameObject Root;
            public Transform Core;
            public Material CoreMat;
            public Color BaseColor;
            public readonly List<Transform> Rays = new List<Transform>();
            public readonly List<Vector3> RayDirs = new List<Vector3>();
            public Material RayMat;
            public Color RayBaseColor;
            public float Elapsed;
            public float Duration;
            public float MaxCoreScale;
            public float RaySpeed;
        }

        [Header("References")]
        [SerializeField] private FightSimulation simulation;
        [SerializeField] private MatchManager matchManager;
        [SerializeField] private TrainingDebugHUD debugHud;

        [Header("Debug Visualization (F1)")]
        [SerializeField] private bool showDebugBoxes = false;

        private readonly List<ActiveBurst> _activeBursts = new List<ActiveBurst>();
        private Shader _unlitShader;
        private Mesh _sphereMesh;
        private Mesh _cubeMesh;

        private LineRenderer _p1HurtboxLine;
        private LineRenderer _p2HurtboxLine;
        private LineRenderer _p1HitboxLine;
        private LineRenderer _p2HitboxLine;
        private Material _hurtboxLineMat;
        private Material _hitboxLineMat;

        public bool ShowDebugBoxes => showDebugBoxes;

        public void Configure(FightSimulation sim, TrainingDebugHUD hud = null)
        {
            if (simulation != null)
            {
                simulation.OnCombatHit -= HandleCombatHit;
                simulation.OnSuperFlashTriggered -= HandleSuperFlashTriggered;
                simulation.OnProjectileClashed -= HandleProjectileClashed;
            }
            simulation = sim;
            if (hud != null)
            {
                debugHud = hud;
            }
            if (simulation != null && isActiveAndEnabled)
            {
                simulation.OnCombatHit += HandleCombatHit;
                simulation.OnSuperFlashTriggered += HandleSuperFlashTriggered;
                simulation.OnProjectileClashed += HandleProjectileClashed;
            }
        }

        private void Awake()
        {
            if (simulation == null)
            {
                simulation = FindAnyObjectByType<FightSimulation>();
            }
            if (matchManager == null)
            {
                matchManager = FindAnyObjectByType<MatchManager>();
            }
            if (debugHud == null)
            {
                debugHud = FindAnyObjectByType<TrainingDebugHUD>();
            }

            _unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (_unlitShader == null)
            {
                _unlitShader = Shader.Find("Sprites/Default");
            }

            CachePrimitiveMeshes();
            CreateDebugLineRenderers();
        }

        private void OnEnable()
        {
            if (simulation != null)
            {
                simulation.OnCombatHit -= HandleCombatHit;
                simulation.OnCombatHit += HandleCombatHit;
                simulation.OnSuperFlashTriggered -= HandleSuperFlashTriggered;
                simulation.OnSuperFlashTriggered += HandleSuperFlashTriggered;
                simulation.OnProjectileClashed -= HandleProjectileClashed;
                simulation.OnProjectileClashed += HandleProjectileClashed;
            }
            if (matchManager != null)
            {
                matchManager.OnMatchStateChanged -= HandleMatchStateChanged;
                matchManager.OnMatchStateChanged += HandleMatchStateChanged;
            }
        }

        private void OnDisable()
        {
            if (simulation != null)
            {
                simulation.OnCombatHit -= HandleCombatHit;
                simulation.OnSuperFlashTriggered -= HandleSuperFlashTriggered;
                simulation.OnProjectileClashed -= HandleProjectileClashed;
            }
            if (matchManager != null)
            {
                matchManager.OnMatchStateChanged -= HandleMatchStateChanged;
            }
        }

        private void HandleMatchStateChanged(MatchManager manager)
        {
            if (manager.Phase == MatchPhase.RoundIntro)
            {
                ClearActiveBursts();
            }
        }

        public void ClearActiveBursts()
        {
            for (int i = 0; i < _activeBursts.Count; i++)
            {
                DestroyBurst(_activeBursts[i]);
            }
            _activeBursts.Clear();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _activeBursts.Count; i++)
            {
                DestroyBurst(_activeBursts[i]);
            }
            _activeBursts.Clear();

            if (_hurtboxLineMat != null) Destroy(_hurtboxLineMat);
            if (_hitboxLineMat != null) Destroy(_hitboxLineMat);
        }

        public void SetDebugBoxesVisible(bool visible)
        {
            showDebugBoxes = visible;
            if (debugHud != null)
            {
                debugHud.SetDebugBoxesIndicator(visible);
            }
            UpdateDebugBoxes();
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame)
            {
                SetDebugBoxesVisible(!showDebugBoxes);
            }

            UpdateActiveBursts(Mathf.Min(Time.deltaTime, 0.05f));
            UpdateDebugBoxes();
        }

        private void HandleSuperFlashTriggered(FighterController caster, int freezeTicks)
        {
            if (caster == null) return;
            Vector2 center = caster.SimPosition + new Vector2(0f, 1.15f);
            SpawnCustomSparkBurst(
                "VFX_SuperFlash",
                center,
                new Color(1.0f, 0.90f, 0.28f, 1.0f),
                new Color(0.95f, 0.65f, 1.0f, 1.0f),
                duration: Mathf.Max(0.28f, freezeTicks * FightSimulation.TickDeltaTime * 1.4f),
                initialScale: 0.68f,
                maxCoreScale: 1.55f,
                rayCount: 20,
                raySpeed: 5.6f);
        }

        private void HandleProjectileClashed(Vector2 clashPoint)
        {
            SpawnCustomSparkBurst(
                "VFX_ProjectileClash",
                clashPoint,
                new Color(0.65f, 0.96f, 1.0f, 1.0f),
                new Color(0.85f, 0.55f, 1.0f, 1.0f),
                duration: 0.30f,
                initialScale: 0.45f,
                maxCoreScale: 0.92f,
                rayCount: 14,
                raySpeed: 4.4f);
        }

        private void HandleCombatHit(CombatHitEvent hit)
        {
            if (hit.WasArmorAbsorbed)
            {
                SpawnCustomSparkBurst(
                    "VFX_ArmorSpark",
                    hit.ContactPoint,
                    new Color(0.86f, 0.92f, 1.0f, 1.0f),
                    new Color(1.0f, 0.88f, 0.50f, 1.0f),
                    duration: 0.24f,
                    initialScale: 0.40f,
                    maxCoreScale: 0.78f,
                    rayCount: 12,
                    raySpeed: 3.8f);
                return;
            }

            bool isSpecial = hit.IsSpecial || (hit.Move != null && hit.Move.attackType == AttackType.Special);
            if (hit.IsExMove && hit.Outcome != HitOutcomeType.Blocked)
            {
                SpawnCustomSparkBurst(
                    "VFX_ExSpark",
                    hit.ContactPoint,
                    new Color(1.0f, 0.84f, 0.16f, 1.0f),
                    new Color(1.0f, 0.95f, 0.55f, 1.0f),
                    duration: 0.34f,
                    initialScale: 0.44f,
                    maxCoreScale: 0.90f,
                    rayCount: 14,
                    raySpeed: 4.2f);
                return;
            }

            SpawnSparkBurst(hit.ContactPoint, hit.Outcome, isSpecial);
        }

        private void SpawnCustomSparkBurst(
            string objectName,
            Vector2 contactPoint,
            Color coreColor,
            Color rayColor,
            float duration,
            float initialScale,
            float maxCoreScale,
            int rayCount,
            float raySpeed)
        {
            var root = new GameObject(objectName);
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(contactPoint.x, contactPoint.y, -0.25f);

            Material coreMat = CreateUnlitMaterial(coreColor);
            Material rayMat = CreateUnlitMaterial(rayColor);

            GameObject coreObj = new GameObject("Core");
            coreObj.transform.SetParent(root.transform, false);
            coreObj.AddComponent<MeshFilter>().sharedMesh = _sphereMesh;
            var coreRenderer = coreObj.AddComponent<MeshRenderer>();
            coreRenderer.sharedMaterial = coreMat;
            coreRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            coreRenderer.receiveShadows = false;
            coreObj.transform.localScale = Vector3.one * initialScale;

            var burst = new ActiveBurst
            {
                Root = root,
                Core = coreObj.transform,
                CoreMat = coreMat,
                BaseColor = coreColor,
                RayMat = rayMat,
                RayBaseColor = rayColor,
                Elapsed = 0f,
                Duration = duration,
                MaxCoreScale = maxCoreScale,
                RaySpeed = raySpeed
            };

            for (int i = 0; i < rayCount; i++)
            {
                float angle = (i / (float)rayCount) * Mathf.PI * 2f + 0.15f;
                Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

                GameObject rayObj = new GameObject($"Ray_{i}");
                rayObj.transform.SetParent(root.transform, false);
                rayObj.transform.localPosition = dir * 0.22f;
                rayObj.transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                rayObj.transform.localScale = new Vector3(0.44f, 0.065f, 0.065f);

                rayObj.AddComponent<MeshFilter>().sharedMesh = _cubeMesh;
                var rr = rayObj.AddComponent<MeshRenderer>();
                rr.sharedMaterial = rayMat;
                rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rr.receiveShadows = false;

                burst.Rays.Add(rayObj.transform);
                burst.RayDirs.Add(dir);
            }

            _activeBursts.Add(burst);
        }

        public void SpawnSparkBurst(Vector2 contactPoint, HitOutcomeType outcome)
        {
            SpawnSparkBurst(contactPoint, outcome, isSpecial: false);
        }

        public void SpawnSparkBurst(Vector2 contactPoint, HitOutcomeType outcome, bool isSpecial)
        {
            bool isThrowTech = outcome == HitOutcomeType.ThrowTech;
            bool isBlock = outcome == HitOutcomeType.Blocked;
            bool isKnockDown = outcome == HitOutcomeType.KnockDown;

            Color coreColor = isThrowTech
                ? new Color(0.55f, 0.96f, 1.0f, 1.0f)
                : (isBlock
                    ? new Color(0.25f, 0.78f, 1.0f, 0.95f)
                    : (isSpecial
                        ? new Color(0.95f, 0.55f, 1.0f, 1.0f)
                        : (isKnockDown ? new Color(1.0f, 0.38f, 0.08f, 0.98f) : new Color(1.0f, 0.65f, 0.12f, 0.96f))));
            Color rayColor = isThrowTech
                ? new Color(0.95f, 0.99f, 1.0f, 1.0f)
                : (isBlock
                    ? new Color(0.65f, 0.92f, 1.0f, 1.0f)
                    : (isSpecial ? new Color(1.0f, 0.86f, 0.22f, 1.0f) : new Color(1.0f, 0.92f, 0.28f, 1.0f)));

            var root = new GameObject(isThrowTech ? "VFX_ThrowTechSpark" : (isBlock ? "VFX_BlockSpark" : (isSpecial ? "VFX_SpecialSpark" : "VFX_HitSpark")));
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(contactPoint.x, contactPoint.y, -0.25f);

            Material coreMat = CreateUnlitMaterial(coreColor);
            Material rayMat = CreateUnlitMaterial(rayColor);

            GameObject coreObj = new GameObject("Core");
            coreObj.transform.SetParent(root.transform, false);
            coreObj.AddComponent<MeshFilter>().sharedMesh = _sphereMesh;
            var coreRenderer = coreObj.AddComponent<MeshRenderer>();
            coreRenderer.sharedMaterial = coreMat;
            coreRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            coreRenderer.receiveShadows = false;
            float initialScale = isThrowTech ? 0.48f : (isBlock ? 0.42f : (isSpecial ? 0.52f : (isKnockDown ? 0.36f : 0.26f)));
            coreObj.transform.localScale = isBlock
                ? new Vector3(initialScale * 0.55f, initialScale * 1.65f, initialScale * 0.45f)
                : Vector3.one * initialScale;

            var burst = new ActiveBurst
            {
                Root = root,
                Core = coreObj.transform,
                CoreMat = coreMat,
                BaseColor = coreColor,
                RayMat = rayMat,
                RayBaseColor = rayColor,
                Elapsed = 0f,
                Duration = isThrowTech ? 0.32f : (isSpecial && !isBlock ? 0.42f : (isKnockDown ? 0.34f : 0.26f)),
                MaxCoreScale = isThrowTech ? 0.88f : (isBlock ? 0.68f : (isSpecial ? 1.08f : (isKnockDown ? 0.72f : 0.48f))),
                RaySpeed = isThrowTech ? 4.2f : (isBlock ? 2.4f : (isSpecial ? 4.8f : (isKnockDown ? 3.8f : 3.0f)))
            };

            int rayCount = isThrowTech ? 14 : (isBlock ? 10 : (isSpecial ? 16 : (isKnockDown ? 12 : 9)));
            for (int i = 0; i < rayCount; i++)
            {
                float angle = (i / (float)rayCount) * Mathf.PI * 2f + (isBlock ? 0f : 0.2f);
                Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

                GameObject rayObj = new GameObject($"Ray_{i}");
                rayObj.transform.SetParent(root.transform, false);
                rayObj.transform.localPosition = dir * (isBlock ? 0.26f : 0.22f);
                rayObj.transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                rayObj.transform.localScale = isBlock
                    ? new Vector3(0.24f, 0.08f, 0.06f)
                    : new Vector3(0.42f, 0.065f, 0.065f);

                rayObj.AddComponent<MeshFilter>().sharedMesh = isBlock ? _sphereMesh : _cubeMesh;
                var rr = rayObj.AddComponent<MeshRenderer>();
                rr.sharedMaterial = rayMat;
                rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rr.receiveShadows = false;

                burst.Rays.Add(rayObj.transform);
                burst.RayDirs.Add(dir);
            }

            _activeBursts.Add(burst);
        }

        private void UpdateActiveBursts(float dt)
        {
            for (int i = _activeBursts.Count - 1; i >= 0; i--)
            {
                ActiveBurst burst = _activeBursts[i];
                burst.Elapsed += dt;
                float t = Mathf.Clamp01(burst.Elapsed / burst.Duration);

                if (burst.Core != null)
                {
                    float scale = Mathf.Lerp(0.18f, burst.MaxCoreScale, t);
                    if (burst.Root.name.Contains("Block"))
                    {
                        burst.Core.localScale = new Vector3(scale * 0.4f, scale * 1.55f, scale * 0.35f);
                    }
                    else
                    {
                        burst.Core.localScale = Vector3.one * scale * (1f - t * 0.35f);
                    }
                }

                float alpha = 1f - t * t;
                SetMaterialColor(burst.CoreMat, new Color(burst.BaseColor.r, burst.BaseColor.g, burst.BaseColor.b, burst.BaseColor.a * alpha));
                SetMaterialColor(burst.RayMat, new Color(burst.RayBaseColor.r, burst.RayBaseColor.g, burst.RayBaseColor.b, burst.RayBaseColor.a * alpha));

                for (int r = 0; r < burst.Rays.Count; r++)
                {
                    Transform ray = burst.Rays[r];
                    if (ray == null) continue;
                    ray.localPosition = burst.RayDirs[r] * (0.22f + burst.RaySpeed * burst.Elapsed);
                    float rayShrink = Mathf.Lerp(1f, 0.25f, t);
                    Vector3 s = ray.localScale;
                    ray.localScale = new Vector3(s.x, Mathf.Max(0.015f, 0.065f * rayShrink), s.z);
                }

                if (burst.Elapsed >= burst.Duration)
                {
                    DestroyBurst(burst);
                    _activeBursts.RemoveAt(i);
                }
            }
        }

        private static void DestroyBurst(ActiveBurst burst)
        {
            if (burst.Root != null) Destroy(burst.Root);
            if (burst.CoreMat != null) Destroy(burst.CoreMat);
            if (burst.RayMat != null) Destroy(burst.RayMat);
        }

        private void CreateDebugLineRenderers()
        {
            _hurtboxLineMat = CreateUnlitMaterial(new Color(0.15f, 0.95f, 0.35f, 0.95f));
            _hitboxLineMat = CreateUnlitMaterial(new Color(1.0f, 0.18f, 0.18f, 0.95f));

            _p1HurtboxLine = CreateBoxLine("Debug_P1_Hurtbox", _hurtboxLineMat, 0.025f);
            _p2HurtboxLine = CreateBoxLine("Debug_P2_Hurtbox", _hurtboxLineMat, 0.025f);
            _p1HitboxLine = CreateBoxLine("Debug_P1_Hitbox", _hitboxLineMat, 0.032f);
            _p2HitboxLine = CreateBoxLine("Debug_P2_Hitbox", _hitboxLineMat, 0.032f);
        }

        private LineRenderer CreateBoxLine(string objectName, Material mat, float width)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = 4;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.sharedMaterial = mat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;
            return lr;
        }

        private void UpdateDebugBoxes()
        {
            if (!showDebugBoxes || simulation == null)
            {
                if (_p1HurtboxLine != null) _p1HurtboxLine.enabled = false;
                if (_p2HurtboxLine != null) _p2HurtboxLine.enabled = false;
                if (_p1HitboxLine != null) _p1HitboxLine.enabled = false;
                if (_p2HitboxLine != null) _p2HitboxLine.enabled = false;
                return;
            }

            UpdateFighterDebugBoxes(simulation.Fighter1, _p1HurtboxLine, _p1HitboxLine);
            UpdateFighterDebugBoxes(simulation.Fighter2, _p2HurtboxLine, _p2HitboxLine);
        }

        private static void UpdateFighterDebugBoxes(FighterController fighter, LineRenderer hurtLine, LineRenderer hitLine)
        {
            if (fighter == null)
            {
                if (hurtLine != null) hurtLine.enabled = false;
                if (hitLine != null) hitLine.enabled = false;
                return;
            }

            if (hurtLine != null)
            {
                if (!fighter.IsInvulnerable)
                {
                    Rect hurt = fighter.GetHurtboxRect();
                    ApplyRectToLine(hurtLine, hurt, -0.05f);
                    hurtLine.enabled = true;
                }
                else
                {
                    hurtLine.enabled = false;
                }
            }

            if (hitLine != null)
            {
                if (fighter.TryGetActiveHitbox(out Rect hitRect, out _))
                {
                    ApplyRectToLine(hitLine, hitRect, -0.08f);
                    hitLine.enabled = true;
                }
                else
                {
                    hitLine.enabled = false;
                }
            }
        }

        private static void ApplyRectToLine(LineRenderer lr, Rect r, float z)
        {
            lr.SetPosition(0, new Vector3(r.xMin, r.yMin, z));
            lr.SetPosition(1, new Vector3(r.xMax, r.yMin, z));
            lr.SetPosition(2, new Vector3(r.xMax, r.yMax, z));
            lr.SetPosition(3, new Vector3(r.xMin, r.yMax, z));
        }

        private Material CreateUnlitMaterial(Color color)
        {
            var mat = new Material(_unlitShader);
            SetMaterialColor(mat, color);
            return mat;
        }

        private static void SetMaterialColor(Material mat, Color color)
        {
            if (mat == null) return;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        }

        private void CachePrimitiveMeshes()
        {
            var tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sphereMesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempSphere);

            var tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cubeMesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempCube);
        }
    }
}
