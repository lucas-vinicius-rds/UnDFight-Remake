using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Central definition and PhysX collision matrix isolation helper for stage interaction layers.
    /// Ensures StageDebris collides only with StagePhysics (and optionally other StageDebris),
    /// StageProjectile collides with StagePhysics and trigger-overlaps FighterHurtbox,
    /// and FighterHurtbox never experiences physical pushes from PhysX.
    /// </summary>
    public static class StagePhysicsLayers
    {
        public const string StagePhysicsLayerName = "StagePhysics";
        public const string StageDebrisLayerName = "StageDebris";
        public const string FighterHurtboxLayerName = "FighterHurtbox";
        public const string StageProjectileLayerName = "StageProjectile";

        public const int PreferredStagePhysicsLayer = 8;
        public const int PreferredStageDebrisLayer = 9;
        public const int PreferredFighterHurtboxLayer = 10;
        public const int PreferredStageProjectileLayer = 11;

        public static int StagePhysicsLayer => ResolveLayerOrFallback(StagePhysicsLayerName, 0);
        public static int StageDebrisLayer => ResolveLayerOrFallback(StageDebrisLayerName, 0);
        public static int FighterHurtboxLayer => ResolveLayerOrFallback(FighterHurtboxLayerName, 0);
        public static int StageProjectileLayer => ResolveLayerOrFallback(StageProjectileLayerName, 0);

        public static bool HasDedicatedLayers =>
            LayerMask.NameToLayer(StagePhysicsLayerName) >= 0 &&
            LayerMask.NameToLayer(StageDebrisLayerName) >= 0 &&
            LayerMask.NameToLayer(FighterHurtboxLayerName) >= 0 &&
            LayerMask.NameToLayer(StageProjectileLayerName) >= 0;

        public static int ResolveLayerOrFallback(string layerName, int fallback = 0)
        {
            int idx = LayerMask.NameToLayer(layerName);
            return idx >= 0 ? idx : fallback;
        }

        /// <summary>
        /// Enforces the PhysX layer collision matrix so debris only collides with StagePhysics
        /// and StageDebris, thrown props (StageProjectile) collide with StagePhysics and trigger-detect
        /// FighterHurtbox, and FighterHurtbox ignores all other PhysX layers.
        /// Safe to call in both Edit Mode setup and runtime Awake.
        /// </summary>
        public static void ApplyCollisionIsolation(bool debrisCollidesWithDebris = true)
        {
            int stagePhysics = LayerMask.NameToLayer(StagePhysicsLayerName);
            int stageDebris = LayerMask.NameToLayer(StageDebrisLayerName);
            int fighterHurtbox = LayerMask.NameToLayer(FighterHurtboxLayerName);
            int stageProjectile = LayerMask.NameToLayer(StageProjectileLayerName);

            if (stageDebris >= 0)
            {
                for (int other = 0; other < 32; other++)
                {
                    bool allow = (other == stagePhysics) || (debrisCollidesWithDebris && other == stageDebris);
                    Physics.IgnoreLayerCollision(stageDebris, other, !allow);
                }
            }

            if (stageProjectile >= 0)
            {
                for (int other = 0; other < 32; other++)
                {
                    bool allow = (other == stagePhysics) || (other == fighterHurtbox);
                    Physics.IgnoreLayerCollision(stageProjectile, other, !allow);
                }
            }

            if (stagePhysics >= 0)
            {
                for (int other = 0; other < 32; other++)
                {
                    bool allow = (other == stageDebris) || (other == stageProjectile);
                    Physics.IgnoreLayerCollision(stagePhysics, other, !allow);
                }
            }

            if (fighterHurtbox >= 0)
            {
                for (int other = 0; other < 32; other++)
                {
                    bool allow = stageProjectile >= 0 && other == stageProjectile;
                    Physics.IgnoreLayerCollision(fighterHurtbox, other, !allow);
                }
            }
        }
    }
}
