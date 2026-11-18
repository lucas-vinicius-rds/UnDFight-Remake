using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Central definition and PhysX collision matrix isolation helper for stage interaction layers.
    /// Ensures:
    /// - StagePhysics (8): collides with StageDebris (9) and StageProjectile (11).
    /// - StageDebris (9): collides with StagePhysics (8), StageDebris (9) (optional), and FighterDebrisPusher (12).
    /// - FighterHurtbox (10): trigger-overlaps ONLY with StageProjectile (11); never experiences PhysX pushes.
    /// - StageProjectile (11): collides with StagePhysics (8) and trigger-overlaps FighterHurtbox (10).
    /// - FighterDebrisPusher (12): kinematic one-way pusher colliding EXCLUSIVELY with StageDebris (9).
    /// </summary>
    public static class StagePhysicsLayers
    {
        public const string StagePhysicsLayerName = "StagePhysics";
        public const string StageDebrisLayerName = "StageDebris";
        public const string FighterHurtboxLayerName = "FighterHurtbox";
        public const string StageProjectileLayerName = "StageProjectile";
        public const string FighterDebrisPusherLayerName = "FighterDebrisPusher";

        public const int PreferredStagePhysicsLayer = 8;
        public const int PreferredStageDebrisLayer = 9;
        public const int PreferredFighterHurtboxLayer = 10;
        public const int PreferredStageProjectileLayer = 11;
        public const int PreferredFighterDebrisPusherLayer = 12;

        public static int StagePhysicsLayer => ResolveLayerOrFallback(StagePhysicsLayerName, 0);
        public static int StageDebrisLayer => ResolveLayerOrFallback(StageDebrisLayerName, 0);
        public static int FighterHurtboxLayer => ResolveLayerOrFallback(FighterHurtboxLayerName, 0);
        public static int StageProjectileLayer => ResolveLayerOrFallback(StageProjectileLayerName, 0);
        public static int FighterDebrisPusherLayer => ResolveLayerOrFallback(FighterDebrisPusherLayerName, 0);

        public static bool HasDedicatedLayers =>
            LayerMask.NameToLayer(StagePhysicsLayerName) >= 0 &&
            LayerMask.NameToLayer(StageDebrisLayerName) >= 0 &&
            LayerMask.NameToLayer(FighterHurtboxLayerName) >= 0 &&
            LayerMask.NameToLayer(StageProjectileLayerName) >= 0 &&
            LayerMask.NameToLayer(FighterDebrisPusherLayerName) >= 0;

        public static int ResolveLayerOrFallback(string layerName, int fallback = 0)
        {
            int idx = LayerMask.NameToLayer(layerName);
            return idx >= 0 ? idx : fallback;
        }

        /// <summary>
        /// Enforces the 5-layer PhysX collision matrix isolating deterministic fighters from PhysX pushes
        /// while enabling one-way kinematic fighter -> dynamic debris coupling via FighterDebrisPusher.
        /// Safe to call in both Edit Mode setup and runtime Awake.
        /// </summary>
        public static void ApplyCollisionIsolation(bool debrisCollidesWithDebris = true)
        {
            int stagePhysics = LayerMask.NameToLayer(StagePhysicsLayerName);
            int stageDebris = LayerMask.NameToLayer(StageDebrisLayerName);
            int fighterHurtbox = LayerMask.NameToLayer(FighterHurtboxLayerName);
            int stageProjectile = LayerMask.NameToLayer(StageProjectileLayerName);
            int fighterDebrisPusher = LayerMask.NameToLayer(FighterDebrisPusherLayerName);

            if (stageDebris >= 0)
            {
                for (int other = 0; other < 32; other++)
                {
                    bool allow =
                        (other == stagePhysics) ||
                        (debrisCollidesWithDebris && other == stageDebris) ||
                        (fighterDebrisPusher >= 0 && other == fighterDebrisPusher);
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

            if (fighterDebrisPusher >= 0)
            {
                for (int other = 0; other < 32; other++)
                {
                    bool allow = stageDebris >= 0 && other == stageDebris;
                    Physics.IgnoreLayerCollision(fighterDebrisPusher, other, !allow);
                }
            }
        }
    }
}
