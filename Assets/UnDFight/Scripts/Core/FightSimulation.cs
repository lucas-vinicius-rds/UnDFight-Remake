using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Deterministic fixed-rate 60 Hz clock accumulator independent of render framerate.
    /// Extracted as a pure C# class so Edit Mode tests can validate timing deterministically.
    /// </summary>
    public sealed class FixedTickClock
    {
        public const int DefaultTickRate = 60;
        public const float DefaultMaxAccumulatedTime = 0.25f;

        public int TickRate { get; }
        public float TickDeltaTime { get; }
        public float MaxAccumulatedTime { get; }

        public int CurrentTick { get; private set; }
        public float Accumulator { get; private set; }
        public float InterpolationAlpha => Mathf.Clamp01(Accumulator / TickDeltaTime);

        public FixedTickClock(int tickRate = DefaultTickRate, float maxAccumulatedTime = DefaultMaxAccumulatedTime)
        {
            TickRate = Mathf.Max(1, tickRate);
            TickDeltaTime = 1f / TickRate;
            MaxAccumulatedTime = Mathf.Max(TickDeltaTime, maxAccumulatedTime);
        }

        public void Reset()
        {
            CurrentTick = 0;
            Accumulator = 0f;
        }

        public void ClearAccumulator()
        {
            Accumulator = 0f;
        }

        /// <summary>
        /// Advances the clock by frameDeltaTime and invokes onTick for each fixed 60 Hz step.
        /// Returns the number of simulation ticks executed in this frame.
        /// </summary>
        public int Advance(float frameDeltaTime, Action<int, float> onTick)
        {
            if (frameDeltaTime <= 0f)
            {
                return 0;
            }

            Accumulator = Mathf.Min(Accumulator + frameDeltaTime, MaxAccumulatedTime);
            int executed = 0;

            while (Accumulator + 1e-6f >= TickDeltaTime)
            {
                Accumulator = Mathf.Max(0f, Accumulator - TickDeltaTime);
                CurrentTick++;
                executed++;
                onTick?.Invoke(CurrentTick, TickDeltaTime);
            }

            return executed;
        }
    }

    /// <summary>
    /// Scene-level 60 Hz fight simulation coordinator. Steps both fighters, resolves pushboxes,
    /// side-facing, deterministic XY hitbox/hurtbox combat, hit-stop freeze frames, and match flow.
    /// </summary>
    public class FightSimulation : MonoBehaviour
    {
        public const int TickRate = FixedTickClock.DefaultTickRate;
        public const float TickDeltaTime = 1f / TickRate;

        public sealed class SimulatedProjectile
        {
            public int Id;
            public FighterController Owner;
            public FighterController Target;
            public MoveData Move;
            public Vector2 Position;
            public float VelocityX;
            public int FacingSign;
            public int RemainingTicks;
            public bool Active;
            public GameObject VisualObject;

            public Rect GetHitboxRect()
            {
                float scale = Owner != null && Owner.Data != null ? Owner.Data.HurtboxScale : 1f;
                Vector2 size = Move != null ? Move.projectileHitboxSize : new Vector2(0.55f, 0.45f);
                float width = Mathf.Max(0.1f, size.x) * scale;
                float height = Mathf.Max(0.1f, size.y) * scale;
                return new Rect(Position.x - width * 0.5f, Position.y - height * 0.5f, width, height);
            }
        }

        private struct PendingStrike
        {
            public bool Valid;
            public FighterController Attacker;
            public FighterController Defender;
            public MoveData Move;
            public Vector2 ContactPoint;
        }

        [Header("Stage Bounds (XY Plane, Z = 0)")]
        [SerializeField] private float stageMinX = -8.0f;
        [SerializeField] private float stageMaxX = 8.0f;
        [SerializeField] private float groundY = 0.0f;

        [Header("Participants")]
        [SerializeField] private FighterController fighter1;
        [SerializeField] private FighterController fighter2;
        [SerializeField] private FightCameraController fightCamera;
        [SerializeField] private MatchManager matchManager;
        [SerializeField] private CpuAiController cpuAiController;

        private readonly List<SimulatedProjectile> _activeProjectiles = new List<SimulatedProjectile>(4);
        private FixedTickClock _clock;
        private int _hitstopTicksRemaining;
        private int _superFlashTicksRemaining;
        private FighterController _superFlashCaster;
        private int _nextProjectileId = 1;
        private int _manualTickCounter;
        private bool _isPaused;

        public int CurrentTick => _clock != null && _clock.CurrentTick > 0 ? _clock.CurrentTick : _manualTickCounter;
        public float InterpolationAlpha => _clock != null ? _clock.InterpolationAlpha : 1f;
        public float StageMinX => stageMinX;
        public float StageMaxX => stageMaxX;
        public float GroundY => groundY;
        public FighterController Fighter1 => fighter1;
        public FighterController Fighter2 => fighter2;
        public FightCameraController FightCamera => fightCamera;
        public MatchManager MatchManager => matchManager;
        public CpuAiController CpuAi => cpuAiController;
        public int HitstopTicksRemaining => _hitstopTicksRemaining;
        public int SuperFlashTicksRemaining => _superFlashTicksRemaining;
        public FighterController SuperFlashCaster => _superFlashCaster;
        public IReadOnlyList<SimulatedProjectile> ActiveProjectiles => _activeProjectiles;
        public bool IsPaused => _isPaused;

        public event Action<CombatHitEvent> OnCombatHit;
        public event Action<FighterController> OnFighterGroundImpact;
        public event Action<FighterController, FighterController> OnParticipantsChanged;
        public event Action<FighterController, int> OnSuperFlashTriggered;
        public event Action<Vector2> OnProjectileClashed;

        private void Awake()
        {
            _clock = new FixedTickClock(TickRate);
            if (matchManager == null)
            {
                matchManager = GetComponent<MatchManager>();
            }
            if (cpuAiController == null)
            {
                cpuAiController = GetComponent<CpuAiController>();
            }
            if (fighter1 != null) fighter1.InitializeSimulation(this, 0);
            if (fighter2 != null) fighter2.InitializeSimulation(this, 1);
            UpdateFacing(force: true);
        }

        public void SetParticipants(FighterController p1, FighterController p2, FightCameraController cam = null)
        {
            fighter1 = p1;
            fighter2 = p2;
            if (cam != null) fightCamera = cam;

            if (_clock == null) _clock = new FixedTickClock(TickRate);
            _hitstopTicksRemaining = 0;
            _superFlashTicksRemaining = 0;
            _superFlashCaster = null;
            ClearProjectiles();
            if (fighter1 != null) fighter1.InitializeSimulation(this, 0);
            if (fighter2 != null) fighter2.InitializeSimulation(this, 1);
            if (fightCamera != null && fighter1 != null && fighter2 != null)
            {
                fightCamera.SetTargets(fighter1.transform, fighter2.transform);
            }
            if (cpuAiController != null)
            {
                cpuAiController.Configure(this, fighter2, fighter1, matchManager);
            }
            UpdateFacing(force: true);
            OnParticipantsChanged?.Invoke(fighter1, fighter2);
        }

        public void SetCpuAiController(CpuAiController ai)
        {
            cpuAiController = ai;
            if (cpuAiController != null)
            {
                cpuAiController.Configure(this, fighter2, fighter1, matchManager);
            }
        }

        public void SetPaused(bool paused)
        {
            _isPaused = paused;
            if (!paused)
            {
                _clock?.ClearAccumulator();
            }
        }

        public void SetMatchManager(MatchManager manager)
        {
            matchManager = manager;
        }

        public void ClearHitstop()
        {
            _hitstopTicksRemaining = 0;
            _superFlashTicksRemaining = 0;
            _superFlashCaster = null;
            ClearProjectiles();
        }

        public void TriggerSuperFlash(FighterController caster, int freezeTicks)
        {
            if (freezeTicks <= 0)
            {
                return;
            }

            _superFlashTicksRemaining = Mathf.Max(_superFlashTicksRemaining, freezeTicks);
            _superFlashCaster = caster;
            if (fightCamera != null && caster != null)
            {
                fightCamera.TriggerSuperCameraFocus(caster.transform, freezeTicks * TickDeltaTime);
            }
            OnSuperFlashTriggered?.Invoke(caster, freezeTicks);
        }

        public SimulatedProjectile SpawnProjectile(FighterController owner, MoveData move)
        {
            if (owner == null || move == null)
            {
                return null;
            }

            float scale = owner.Data != null ? owner.Data.HurtboxScale : 1f;
            Vector2 spawnPos = new Vector2(
                owner.SimPosition.x + owner.FacingSign * move.projectileSpawnOffset.x * scale,
                owner.SimPosition.y + move.projectileSpawnOffset.y * scale);
            FighterController target = owner == fighter1 ? fighter2 : fighter1;

            var proj = new SimulatedProjectile
            {
                Id = _nextProjectileId++,
                Owner = owner,
                Target = target,
                Move = move,
                Position = spawnPos,
                VelocityX = owner.FacingSign * Mathf.Max(0.5f, move.projectileSpeed),
                FacingSign = owner.FacingSign,
                RemainingTicks = Mathf.Max(1, move.projectileLifetimeTicks),
                Active = true,
                VisualObject = CreateProjectileVisual(spawnPos, move.projectileHitboxSize * scale, move.specialMechanic)
            };

            _activeProjectiles.Add(proj);
            return proj;
        }

        public void ClearProjectiles()
        {
            for (int i = _activeProjectiles.Count - 1; i >= 0; i--)
            {
                DestroyProjectileVisual(_activeProjectiles[i]);
            }
            _activeProjectiles.Clear();
        }

        private void OnDestroy()
        {
            ClearProjectiles();
        }

        public void ForceUpdateFacing()
        {
            UpdateFacing(force: true);
        }

        /// <summary>
        /// Deterministically steps N fixed 60 Hz simulation ticks (used in Edit Mode tests and scripted captures).
        /// </summary>
        public void StepSimulationTicks(int tickCount)
        {
            for (int i = 0; i < tickCount; i++)
            {
                _manualTickCounter++;
                StepSingleTick(_manualTickCounter, TickDeltaTime);
            }
        }

        private void Update()
        {
            if (_isPaused)
            {
                return;
            }

            if (_clock == null)
            {
                _clock = new FixedTickClock(TickRate);
            }

            _clock.Advance(Time.deltaTime, StepSingleTick);
        }

        private void StepSingleTick(int tick, float dt)
        {
            // During Super Flash cinematic freeze, halt fighter simulation while camera zooms onto caster
            if (_superFlashTicksRemaining > 0)
            {
                _superFlashTicksRemaining--;
                fighter1?.InputHandler?.SyncSimulationTick(tick);
                fighter2?.InputHandler?.SyncSimulationTick(tick);
                fighter1?.HoldVisualForHitstop();
                fighter2?.HoldVisualForHitstop();
                if (fightCamera != null)
                {
                    fightCamera.StepTick(dt);
                }
                if (_superFlashTicksRemaining == 0)
                {
                    _superFlashCaster = null;
                }
                return;
            }

            // During hit-stop freeze frames, halt fighter movement, state advancement, and Animator Update
            if (_hitstopTicksRemaining > 0)
            {
                _hitstopTicksRemaining--;
                fighter1?.InputHandler?.SyncSimulationTick(tick);
                fighter2?.InputHandler?.SyncSimulationTick(tick);
                fighter1?.HoldVisualForHitstop();
                fighter2?.HoldVisualForHitstop();
                return;
            }

            // Step CPU AI strictly on the 60 Hz simulation clock before advancing fighters
            if (cpuAiController != null && cpuAiController.IsEnabled)
            {
                cpuAiController.StepAiTick(tick);
            }

            bool f1WasGrounded = fighter1 != null && fighter1.IsGrounded;
            bool f2WasGrounded = fighter2 != null && fighter2.IsGrounded;

            if (fighter1 != null) fighter1.StepTick(tick, dt);
            if (fighter2 != null) fighter2.StepTick(tick, dt);

            ResolvePushboxCollision();
            UpdateFacing(force: false);

            bool combatAllowed = matchManager == null || matchManager.CombatActive;
            if (combatAllowed)
            {
                ResolveCombatHits();
                StepProjectiles(dt);
            }
            else if (_activeProjectiles.Count > 0)
            {
                ClearProjectiles();
            }

            CheckFighterGroundImpact(fighter1, f1WasGrounded);
            CheckFighterGroundImpact(fighter2, f2WasGrounded);

            if (cpuAiController != null && cpuAiController.IsEnabled)
            {
                cpuAiController.ObserveTick(tick);
            }

            matchManager?.StepTick(tick);

            if (fightCamera != null)
            {
                fightCamera.StepTick(dt);
            }
        }

        private void ResolveCombatHits()
        {
            if (fighter1 == null || fighter2 == null)
            {
                return;
            }

            // Evaluate both attackers first so simultaneous active overlaps trigger clean trades / throw techs
            PendingStrike p1Strike = EvaluateStrike(fighter1, fighter2);
            PendingStrike p2Strike = EvaluateStrike(fighter2, fighter1);

            if (!p1Strike.Valid && !p2Strike.Valid)
            {
                return;
            }

            int maxHitstop = 0;
            bool p1IsThrow = p1Strike.Valid && IsThrowMove(p1Strike.Move);
            bool p2IsThrow = p2Strike.Valid && IsThrowMove(p2Strike.Move);

            if (p1IsThrow && p2IsThrow)
            {
                Vector2 midpoint = (p1Strike.ContactPoint + p2Strike.ContactPoint) * 0.5f;
                ApplyThrowTech(p1Strike.Attacker, p2Strike.Attacker, p1Strike.Move, midpoint, isTrade: true, ref maxHitstop);
                p2Strike.Attacker.MarkHitRegistered();
            }
            else
            {
                bool isTrade = p1Strike.Valid && p2Strike.Valid;
                if (p1Strike.Valid)
                {
                    ApplyStrike(p1Strike, isTrade, ref maxHitstop);
                }
                if (p2Strike.Valid)
                {
                    ApplyStrike(p2Strike, isTrade, ref maxHitstop);
                }
            }

            if (maxHitstop > 0)
            {
                _hitstopTicksRemaining = maxHitstop;
            }
        }

        private static bool IsThrowMove(MoveData move)
        {
            return move != null && (move.isThrow || move.attackType == AttackType.Throw);
        }

        private static PendingStrike EvaluateStrike(FighterController attacker, FighterController defender)
        {
            if (attacker == null || defender == null || defender.IsInvulnerable)
            {
                return default;
            }

            if (!attacker.TryGetActiveHitbox(out Rect hitbox, out MoveData move))
            {
                return default;
            }

            if (defender.IsAntiAirInvulnerable && (!attacker.IsGrounded || move.attackType == AttackType.JumpAttack))
            {
                return default;
            }

            if (IsThrowMove(move))
            {
                if (!defender.IsGrounded ||
                    defender.State == FighterState.Hit ||
                    defender.State == FighterState.KnockDown ||
                    defender.State == FighterState.GetUp ||
                    (defender.State == FighterState.Block && defender.StateTotalTicks > 0))
                {
                    return default;
                }
            }

            Rect hurtbox = defender.GetHurtboxRect();
            if (!hitbox.Overlaps(hurtbox))
            {
                return default;
            }

            float minX = Mathf.Max(hitbox.xMin, hurtbox.xMin);
            float maxX = Mathf.Min(hitbox.xMax, hurtbox.xMax);
            float minY = Mathf.Max(hitbox.yMin, hurtbox.yMin);
            float maxY = Mathf.Min(hitbox.yMax, hurtbox.yMax);

            return new PendingStrike
            {
                Valid = true,
                Attacker = attacker,
                Defender = defender,
                Move = move,
                ContactPoint = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f)
            };
        }

        private void ApplyThrowTech(
            FighterController attacker,
            FighterController defender,
            MoveData move,
            Vector2 contactPoint,
            bool isTrade,
            ref int maxHitstop)
        {
            attacker.MarkHitRegistered();
            attacker.InputHandler?.ConsumeThrowTechInput();
            defender.InputHandler?.ConsumeThrowTechInput();

            attacker.ApplyThrowTechPushback(defender.FacingSign);
            defender.ApplyThrowTechPushback(attacker.FacingSign);

            const int techHitstop = 6;
            if (techHitstop > maxHitstop)
            {
                maxHitstop = techHitstop;
            }

            EmitCombatHitEffects(
                attacker,
                defender,
                move,
                contactPoint,
                HitOutcomeType.ThrowTech,
                damageDealt: 0,
                knockbackVelocity: defender.SimVelocity.x,
                wasTrade: isTrade,
                comboCount: 0,
                comboDamage: 0,
                isSpecial: false);
        }

        private void ApplyStrike(PendingStrike strike, bool isTrade, ref int maxHitstop)
        {
            if (IsThrowMove(strike.Move) && strike.Defender.CanTechIncomingThrow(CurrentTick))
            {
                ApplyThrowTech(strike.Attacker, strike.Defender, strike.Move, strike.ContactPoint, isTrade, ref maxHitstop);
                return;
            }

            bool isSecondaryHit = strike.Move != null &&
                                  strike.Move.specialMechanic == SpecialMechanicType.MultiHitRush &&
                                  strike.Move.IsSecondaryActiveTick(strike.Attacker.StateTicks);

            strike.Defender.ApplyIncomingAttack(
                strike.Move,
                strike.Attacker.FacingSign,
                out HitOutcomeType outcome,
                out int damageDealt,
                out float knockbackVelocity,
                out int comboCount,
                out int comboDamage,
                out bool wasArmorAbsorbed,
                isSecondaryMultiHit: isSecondaryHit);
            strike.Attacker.MarkContactRegistered(outcome);

            bool isSpecial = strike.Move.attackType == AttackType.Special;
            bool isExMove = strike.Move.isExMove ||
                            strike.Move.attackType == AttackType.ExHeavyPunch ||
                            strike.Move.attackType == AttackType.ExHeavyKick;
            int consumedStartBonus = strike.Attacker.ConsumeStartSuperBonus();
            if (outcome == HitOutcomeType.Blocked)
            {
                if (!isSpecial && !isExMove)
                {
                    strike.Attacker.AddSuperGauge(Mathf.Max(0, FighterController.SuperGainBlockAttacker - consumedStartBonus));
                }
                strike.Defender.AddSuperGauge(FighterController.SuperGainBlockDefender);
            }
            else
            {
                if (!isSpecial && !isExMove)
                {
                    bool isHeavy = strike.Move.attackType == AttackType.HeavyPunch ||
                                   strike.Move.attackType == AttackType.HeavyKick ||
                                   strike.Move.attackType == AttackType.ComboPunchFinisher ||
                                   strike.Move.attackType == AttackType.ComboKickFinisher;
                    int hitGain = isHeavy ? FighterController.SuperGainHeavyHitAttacker : FighterController.SuperGainLightHitAttacker;
                    strike.Attacker.AddSuperGauge(Mathf.Max(0, hitGain - consumedStartBonus));
                }
                if (!wasArmorAbsorbed)
                {
                    strike.Defender.AddSuperGauge(FighterController.SuperGainHitDefender);
                }
            }

            int hitstop = strike.Move.hitstopTicks;
            if (hitstop > maxHitstop)
            {
                maxHitstop = hitstop;
            }

            EmitCombatHitEffects(
                strike.Attacker,
                strike.Defender,
                strike.Move,
                strike.ContactPoint,
                outcome,
                damageDealt,
                knockbackVelocity,
                isTrade,
                comboCount,
                comboDamage,
                isSpecial,
                isExMove,
                wasArmorAbsorbed);
        }

        private void StepProjectiles(float dt)
        {
            if (_activeProjectiles.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _activeProjectiles.Count; i++)
            {
                SimulatedProjectile p = _activeProjectiles[i];
                if (!p.Active) continue;

                p.Position.x += p.VelocityX * dt;
                p.RemainingTicks--;
                if (p.VisualObject != null)
                {
                    p.VisualObject.transform.position = new Vector3(p.Position.x, p.Position.y, 0f);
                }

                if (p.RemainingTicks <= 0 || p.Position.x < stageMinX || p.Position.x > stageMaxX)
                {
                    p.Active = false;
                }
            }

            // Deterministic projectile-vs-projectile clash
            for (int i = 0; i < _activeProjectiles.Count; i++)
            {
                SimulatedProjectile a = _activeProjectiles[i];
                if (!a.Active) continue;
                Rect rectA = a.GetHitboxRect();

                for (int j = i + 1; j < _activeProjectiles.Count; j++)
                {
                    SimulatedProjectile b = _activeProjectiles[j];
                    if (!b.Active || a.Owner == b.Owner) continue;

                    if (rectA.Overlaps(b.GetHitboxRect()))
                    {
                        a.Active = false;
                        b.Active = false;
                        Vector2 clashPt = (a.Position + b.Position) * 0.5f;
                        OnProjectileClashed?.Invoke(clashPt);
                        break;
                    }
                }
            }

            // Deterministic projectile-vs-fighter hit resolution
            int maxHitstop = 0;
            for (int i = 0; i < _activeProjectiles.Count; i++)
            {
                SimulatedProjectile p = _activeProjectiles[i];
                if (!p.Active || p.Target == null || p.Target.IsInvulnerable)
                {
                    continue;
                }

                // Prevent BossDecree shockwave from double-hitting on the exact same attack execution if melee strike already connected
                if (p.Move != null &&
                    p.Move.specialMechanic == SpecialMechanicType.BossDecree &&
                    p.Owner != null &&
                    p.Owner.State == FighterState.Attack &&
                    p.Owner.CurrentMoveData == p.Move &&
                    p.Owner.HasRegisteredContactOnMove)
                {
                    p.Active = false;
                    continue;
                }

                Rect hurtbox = p.Target.GetHurtboxRect();
                Rect projRect = p.GetHitboxRect();
                if (!projRect.Overlaps(hurtbox))
                {
                    continue;
                }

                p.Active = false;
                Vector2 contactPt = new Vector2(
                    Mathf.Clamp(p.Position.x, hurtbox.xMin, hurtbox.xMax),
                    Mathf.Clamp(p.Position.y, hurtbox.yMin, hurtbox.yMax));

                p.Target.ApplyIncomingAttack(
                    p.Move,
                    p.FacingSign,
                    out HitOutcomeType outcome,
                    out int damageDealt,
                    out float knockbackVelocity,
                    out int comboCount,
                    out int comboDamage,
                    out bool wasArmorAbsorbed);

                p.Target.AddSuperGauge(outcome == HitOutcomeType.Blocked
                    ? FighterController.SuperGainBlockDefender
                    : (wasArmorAbsorbed ? 0 : FighterController.SuperGainHitDefender));

                if (p.Move != null && p.Move.hitstopTicks > maxHitstop)
                {
                    maxHitstop = p.Move.hitstopTicks;
                }

                bool isSpecial = p.Move != null && p.Move.attackType == AttackType.Special;
                bool isExMove = p.Move != null && p.Move.isExMove;
                EmitCombatHitEffects(
                    p.Owner,
                    p.Target,
                    p.Move,
                    contactPt,
                    outcome,
                    damageDealt,
                    knockbackVelocity,
                    wasTrade: false,
                    comboCount: comboCount,
                    comboDamage: comboDamage,
                    isSpecial: isSpecial,
                    isExMove: isExMove,
                    wasArmorAbsorbed: wasArmorAbsorbed);
            }

            if (maxHitstop > _hitstopTicksRemaining)
            {
                _hitstopTicksRemaining = maxHitstop;
            }

            for (int i = _activeProjectiles.Count - 1; i >= 0; i--)
            {
                if (!_activeProjectiles[i].Active)
                {
                    DestroyProjectileVisual(_activeProjectiles[i]);
                    _activeProjectiles.RemoveAt(i);
                }
            }
        }

        private GameObject CreateProjectileVisual(Vector2 spawnPos, Vector2 size, SpecialMechanicType mechanic = SpecialMechanicType.ProjectileShot)
        {
            if (!Application.isPlaying)
            {
                return null;
            }

            bool isBossDecree = mechanic == SpecialMechanicType.BossDecree;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = isBossDecree ? "BossDecreeShockwave_Visual" : "CombatProjectile_Visual";
            go.transform.position = new Vector3(spawnPos.x, spawnPos.y, 0f);
            float diameter = Mathf.Max(0.28f, Mathf.Min(size.x, size.y));
            go.transform.localScale = isBossDecree
                ? new Vector3(diameter * 1.45f, diameter * 1.15f, diameter * 1.15f)
                : new Vector3(diameter * 1.25f, diameter, diameter);
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) unlit = Shader.Find("Sprites/Default");
            if (unlit != null)
            {
                var mat = new Material(unlit);
                Color c = isBossDecree
                    ? new Color(1.0f, 0.72f, 0.16f, 1.0f)
                    : new Color(0.58f, 0.25f, 1.0f, 1.0f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                }
            }
            return go;
        }

        private static void DestroyProjectileVisual(SimulatedProjectile proj)
        {
            if (proj?.VisualObject != null)
            {
                if (Application.isPlaying) Destroy(proj.VisualObject);
                else DestroyImmediate(proj.VisualObject);
                proj.VisualObject = null;
            }
        }

        /// <summary>
        /// Emits hitstop, camera shake, VFX/Audio notifications, and CombatHitEvent for a PhysX stage projectile impact.
        /// </summary>
        public void NotifyExternalProjectileImpact(
            FighterController attacker,
            FighterController defender,
            MoveData projectileMove,
            Vector2 contactPoint,
            HitOutcomeType outcome,
            int damageDealt,
            float knockbackVelocity,
            int comboCount,
            int comboDamage)
        {
            if (defender == null || projectileMove == null)
            {
                return;
            }

            int hitstop = projectileMove.hitstopTicks;
            if (hitstop > _hitstopTicksRemaining)
            {
                _hitstopTicksRemaining = hitstop;
            }

            EmitCombatHitEffects(
                attacker,
                defender,
                projectileMove,
                contactPoint,
                outcome,
                damageDealt,
                knockbackVelocity,
                wasTrade: false,
                comboCount: comboCount,
                comboDamage: comboDamage,
                isSpecial: false);
        }

        private void EmitCombatHitEffects(
            FighterController attacker,
            FighterController defender,
            MoveData move,
            Vector2 contactPoint,
            HitOutcomeType outcome,
            int damageDealt,
            float knockbackVelocity,
            bool wasTrade,
            int comboCount,
            int comboDamage,
            bool isSpecial,
            bool isExMove = false,
            bool wasArmorAbsorbed = false)
        {
            int hitstop = outcome == HitOutcomeType.ThrowTech
                ? 6
                : (move != null ? move.hitstopTicks : 0);

            if (fightCamera != null)
            {
                if (outcome == HitOutcomeType.ThrowTech)
                {
                    fightCamera.TriggerImpactShake(0.08f, 0.10f);
                }
                else if (outcome == HitOutcomeType.Blocked)
                {
                    fightCamera.TriggerImpactShake(isSpecial ? 0.07f : 0.03f, isSpecial ? 0.09f : 0.06f);
                }
                else if (isSpecial)
                {
                    fightCamera.TriggerImpactShake(0.22f, 0.24f);
                }
                else if (isExMove || outcome == HitOutcomeType.KnockDown ||
                         (move != null &&
                          (move.causesKnockdown ||
                           move.attackType == AttackType.HeavyPunch ||
                           move.attackType == AttackType.HeavyKick ||
                           move.attackType == AttackType.ComboPunchFinisher ||
                           move.attackType == AttackType.ComboKickFinisher)))
                {
                    fightCamera.TriggerImpactShake(0.14f, 0.16f);
                }
                else
                {
                    fightCamera.TriggerImpactShake(0.06f, 0.09f);
                }
            }

            OnCombatHit?.Invoke(new CombatHitEvent
            {
                Attacker = attacker,
                Defender = defender,
                Move = move,
                Outcome = outcome,
                ContactPoint = contactPoint,
                DamageDealt = damageDealt,
                HitstopTicks = hitstop,
                KnockbackVelocity = knockbackVelocity,
                WasTrade = wasTrade,
                ComboCount = comboCount,
                ComboDamage = comboDamage,
                IsSpecial = isSpecial,
                IsExMove = isExMove,
                WasArmorAbsorbed = wasArmorAbsorbed,
                ComboStringName = attacker != null ? attacker.ActiveComboStringName : string.Empty
            });
        }

        private void ResolvePushboxCollision()
        {
            if (fighter1 == null || fighter2 == null)
            {
                return;
            }

            Vector2 p1 = fighter1.SimPosition;
            Vector2 p2 = fighter2.SimPosition;

            float minDistance = fighter1.PushboxRadius + fighter2.PushboxRadius;
            float verticalSeparation = Mathf.Abs(p1.y - p2.y);
            float combinedHeight = (fighter1.CurrentHurtboxHeight + fighter2.CurrentHurtboxHeight) * 0.45f;

            if (verticalSeparation >= combinedHeight)
            {
                return;
            }

            float dx = p2.x - p1.x;
            float absDx = Mathf.Abs(dx);
            if (absDx >= minDistance)
            {
                return;
            }

            float pushSign = absDx > 0.001f ? Mathf.Sign(dx) : (fighter1.FacingSign >= 0 ? 1f : -1f);
            float overlap = (minDistance - absDx) * 0.5f;

            p1.x -= pushSign * overlap;
            p2.x += pushSign * overlap;

            if (p1.x < stageMinX)
            {
                float excess = stageMinX - p1.x;
                p1.x = stageMinX;
                p2.x = Mathf.Min(stageMaxX, p2.x + excess);
            }
            else if (p2.x > stageMaxX)
            {
                float excess = p2.x - stageMaxX;
                p2.x = stageMaxX;
                p1.x = Mathf.Max(stageMinX, p1.x - excess);
            }

            fighter1.SetSimPositionX(p1.x);
            fighter2.SetSimPositionX(p2.x);
        }

        private void UpdateFacing(bool force)
        {
            if (fighter1 == null || fighter2 == null)
            {
                return;
            }

            float dx = fighter2.SimPosition.x - fighter1.SimPosition.x;
            if (Mathf.Abs(dx) < 0.02f)
            {
                return;
            }

            int p1Facing = dx >= 0f ? 1 : -1;
            int p2Facing = -p1Facing;

            if (force || fighter1.IsGrounded)
            {
                fighter1.SetFacing(p1Facing, force);
            }
            if (force || fighter2.IsGrounded)
            {
                fighter2.SetFacing(p2Facing, force);
            }
        }

        private void CheckFighterGroundImpact(FighterController fighter, bool wasGrounded)
        {
            if (fighter == null || fighter.State != FighterState.KnockDown || !fighter.IsGrounded)
            {
                return;
            }

            if (!wasGrounded || fighter.StateTicks == 10)
            {
                OnFighterGroundImpact?.Invoke(fighter);
            }
        }
    }
}
