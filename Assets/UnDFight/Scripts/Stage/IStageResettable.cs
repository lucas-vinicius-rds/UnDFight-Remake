using System.Collections.Generic;
using UnityEngine;

namespace UnDFight
{
    public enum StagePropMaterialType
    {
        WoodOrCrate = 0,
        Plastic = 1,
        Glass = 2,
        ConcreteOrMetal = 3
    }

    /// <summary>
    /// Contract for any interactive stage object that resets cleanly at the start of every round.
    /// </summary>
    public interface IStageResettable
    {
        void ResetProp();
    }

    /// <summary>
    /// Shared base component for stage props discovered automatically by StageInteractableManager.
    /// Provides common intact visual caching, fallback debris pool creation, and fragment burst helpers.
    /// </summary>
    public abstract class StagePropBase : MonoBehaviour, IStageResettable
    {
        private static Mesh s_sharedCubeMesh;

        private readonly List<Renderer> _cachedIntactRenderers = new List<Renderer>();
        private readonly List<Collider> _cachedIntactColliders = new List<Collider>();
        private CombatVfxController _cachedVfxController;

        public abstract bool IsBroken { get; }

        public abstract void ResetProp();

        public abstract bool TryHandleCombatImpact(
            Vector2 contactPoint,
            int impactDamage,
            float pushDirX,
            bool isHeavyOrSpecial,
            Vector2 attackerPos,
            Vector2 defenderPos,
            int simulationTick = -1);

        public virtual bool TryHandleKnockdownImpact(Vector2 fighterPosition, float pushDirX, int simulationTick = -1)
        {
            return TryHandleCombatImpact(fighterPosition, 2, pushDirX, true, fighterPosition, fighterPosition, simulationTick);
        }

        protected void CacheIntactVisuals(GameObject intactRoot, Transform poolRoot, out Quaternion initialIntactRot)
        {
            _cachedIntactRenderers.Clear();
            _cachedIntactColliders.Clear();
            initialIntactRot = Quaternion.identity;

            if (intactRoot != null)
            {
                initialIntactRot = intactRoot.transform.localRotation;
                return;
            }

            Transform effectivePool = poolRoot != null ? poolRoot : transform.Find("DebrisPool");
            Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                Renderer r = allRenderers[i];
                if (r == null) continue;
                if (effectivePool != null && r.transform.IsChildOf(effectivePool)) continue;
                _cachedIntactRenderers.Add(r);
            }

            Collider[] allColliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < allColliders.Length; i++)
            {
                Collider c = allColliders[i];
                if (c == null) continue;
                if (effectivePool != null && c.transform.IsChildOf(effectivePool)) continue;
                _cachedIntactColliders.Add(c);
            }
        }

        protected void SetIntactVisualsVisible(GameObject intactRoot, Collider mainCollider, bool visible)
        {
            if (intactRoot != null)
            {
                intactRoot.SetActive(visible);
            }
            else
            {
                for (int i = 0; i < _cachedIntactRenderers.Count; i++)
                {
                    if (_cachedIntactRenderers[i] != null)
                    {
                        _cachedIntactRenderers[i].enabled = visible;
                    }
                }

                for (int i = 0; i < _cachedIntactColliders.Count; i++)
                {
                    if (_cachedIntactColliders[i] != null)
                    {
                        _cachedIntactColliders[i].enabled = visible;
                    }
                }
            }

            if (mainCollider != null)
            {
                mainCollider.enabled = visible;
            }
        }

        protected void EnsureDebrisPoolFragments(
            GameObject intactRoot,
            ref Transform poolRoot,
            List<StageDebrisFragment> fragments,
            int generatedCount)
        {
            fragments.Clear();

            if (poolRoot == null)
            {
                Transform existingPool = transform.Find("DebrisPool");
                if (existingPool != null)
                {
                    poolRoot = existingPool;
                }
            }

            if (poolRoot != null)
            {
                poolRoot.GetComponentsInChildren(true, fragments);
            }

            if (fragments.Count > 0)
            {
                for (int i = 0; i < fragments.Count; i++)
                {
                    fragments[i].EnsureComponents();
                    fragments[i].CacheInitialTransform();
                }
                return;
            }

            var poolGo = new GameObject("DebrisPool");
            poolGo.transform.SetParent(transform, false);
            poolRoot = poolGo.transform;

            Bounds localBounds = ComputeIntactBoundsLocal(intactRoot, poolRoot, out Material primaryMat);
            int count = Mathf.Clamp(generatedCount, 5, 8);
            Mesh cubeMesh = GetSharedCubeMesh();

            for (int i = 0; i < count; i++)
            {
                var shardGo = new GameObject($"Fragment_{i}");
                shardGo.transform.SetParent(poolRoot, false);
                shardGo.layer = StagePhysicsLayers.StageDebrisLayer;

                float nx = ((i % 3) - 1) * 0.28f;
                float ny = 0.25f + (i / 3) * 0.28f;
                float nz = (i % 2) == 0 ? -0.18f : 0.18f;
                shardGo.transform.localPosition = new Vector3(
                    localBounds.center.x + localBounds.size.x * nx,
                    Mathf.Max(0.12f, localBounds.min.y + localBounds.size.y * ny),
                    localBounds.center.z + localBounds.size.z * nz);
                shardGo.transform.localRotation = Quaternion.Euler(i * 19f, i * 37f, i * 11f);
                shardGo.transform.localScale = new Vector3(
                    Mathf.Clamp(localBounds.size.x * 0.36f, 0.08f, 0.36f),
                    Mathf.Clamp(localBounds.size.y * 0.16f, 0.04f, 0.16f),
                    Mathf.Clamp(localBounds.size.z * 0.32f, 0.07f, 0.30f));

                var mf = shardGo.AddComponent<MeshFilter>();
                mf.sharedMesh = cubeMesh;

                var mr = shardGo.AddComponent<MeshRenderer>();
                if (primaryMat != null)
                {
                    mr.sharedMaterial = primaryMat;
                }

                shardGo.AddComponent<BoxCollider>();
                shardGo.AddComponent<Rigidbody>();
                var frag = shardGo.AddComponent<StageDebrisFragment>();
                frag.EnsureComponents();
                frag.CacheInitialTransform();
                fragments.Add(frag);
            }
        }

        protected void LaunchDebrisBurst(
            List<StageDebrisFragment> fragments,
            Vector3 impactOrigin,
            float pushDirX,
            float explosionForce,
            float explosionRadius,
            float directionalImpulse,
            float upwardImpulse,
            float torqueStrength,
            float freezeSeconds,
            float hideSeconds)
        {
            Vector3 pushVec = new Vector3(pushDirX * directionalImpulse, upwardImpulse, Random.Range(0.15f, 0.65f));
            for (int i = 0; i < fragments.Count; i++)
            {
                StageDebrisFragment frag = fragments[i];
                if (frag == null)
                {
                    continue;
                }

                Vector3 jitter = new Vector3(
                    Random.Range(-0.55f, 0.55f),
                    Random.Range(0.1f, 0.65f),
                    Random.Range(-0.45f, 0.55f));
                frag.Launch(
                    impactOrigin,
                    explosionForce,
                    explosionRadius,
                    pushVec + jitter,
                    torqueStrength,
                    freezeSeconds,
                    hideSeconds);
            }
        }

        protected Vector3 GetWorldVisualCenter(GameObject intactRoot)
        {
            if (intactRoot != null)
            {
                Renderer[] renderers = intactRoot.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                    {
                        b.Encapsulate(renderers[i].bounds);
                    }
                    return b.center;
                }
            }

            return transform.position + Vector3.up * 0.45f;
        }

        protected CombatVfxController ResolveVfxController()
        {
            if (_cachedVfxController == null && Application.isPlaying)
            {
                _cachedVfxController = FindAnyObjectByType<CombatVfxController>();
            }
            return _cachedVfxController;
        }

        private Bounds ComputeIntactBoundsLocal(GameObject intactRoot, Transform poolRoot, out Material primaryMaterial)
        {
            primaryMaterial = null;
            Transform searchRoot = intactRoot != null ? intactRoot.transform : transform;
            Renderer[] renderers = searchRoot.GetComponentsInChildren<Renderer>(true);

            bool hasBounds = false;
            Bounds combined = new Bounds(Vector3.up * 0.35f, new Vector3(0.5f, 0.5f, 0.5f));

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                if (poolRoot != null && r.transform.IsChildOf(poolRoot)) continue;

                if (primaryMaterial == null && r.sharedMaterial != null)
                {
                    primaryMaterial = r.sharedMaterial;
                }

                Bounds b = r.bounds;
                Vector3 localMin = transform.InverseTransformPoint(b.min);
                Vector3 localMax = transform.InverseTransformPoint(b.max);
                if (!hasBounds)
                {
                    combined = new Bounds((localMin + localMax) * 0.5f, Vector3.zero);
                    hasBounds = true;
                }
                combined.Encapsulate(localMin);
                combined.Encapsulate(localMax);
            }

            if (combined.size.sqrMagnitude < 0.01f)
            {
                combined = new Bounds(Vector3.up * 0.35f, new Vector3(0.5f, 0.5f, 0.5f));
            }
            return combined;
        }

        private static Mesh GetSharedCubeMesh()
        {
            if (s_sharedCubeMesh != null)
            {
                return s_sharedCubeMesh;
            }

            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            s_sharedCubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying)
            {
                Destroy(temp);
            }
            else
            {
                DestroyImmediate(temp);
            }
            return s_sharedCubeMesh;
        }
    }
}
