using System;
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
    /// Scene-level 60 Hz fight simulation coordinator. Steps both fighters, resolves pushboxes
    /// and side-facing on the 2D XY fighting plane (Z = 0), and keeps interpolation independent.
    /// </summary>
    public class FightSimulation : MonoBehaviour
    {
        public const int TickRate = FixedTickClock.DefaultTickRate;
        public const float TickDeltaTime = 1f / TickRate;

        [Header("Stage Bounds (XY Plane, Z = 0)")]
        [SerializeField] private float stageMinX = -8.0f;
        [SerializeField] private float stageMaxX = 8.0f;
        [SerializeField] private float groundY = 0.0f;

        [Header("Participants")]
        [SerializeField] private FighterController fighter1;
        [SerializeField] private FighterController fighter2;
        [SerializeField] private FightCameraController fightCamera;

        private FixedTickClock _clock;

        public int CurrentTick => _clock != null ? _clock.CurrentTick : 0;
        public float InterpolationAlpha => _clock != null ? _clock.InterpolationAlpha : 1f;
        public float StageMinX => stageMinX;
        public float StageMaxX => stageMaxX;
        public float GroundY => groundY;
        public FighterController Fighter1 => fighter1;
        public FighterController Fighter2 => fighter2;

        private void Awake()
        {
            _clock = new FixedTickClock(TickRate);
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
            if (fighter1 != null) fighter1.InitializeSimulation(this, 0);
            if (fighter2 != null) fighter2.InitializeSimulation(this, 1);
            UpdateFacing(force: true);
        }

        private void Update()
        {
            if (_clock == null)
            {
                _clock = new FixedTickClock(TickRate);
            }

            _clock.Advance(Time.deltaTime, StepSingleTick);
        }

        private void StepSingleTick(int tick, float dt)
        {
            if (fighter1 != null) fighter1.StepTick(tick, dt);
            if (fighter2 != null) fighter2.StepTick(tick, dt);

            ResolvePushboxCollision();
            UpdateFacing(force: false);

            if (fightCamera != null)
            {
                fightCamera.StepTick(dt);
            }
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

            // Clamp within stage bounds and push the other fighter out if against a corner
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
                fighter1.SetFacing(p1Facing);
            }
            if (force || fighter2.IsGrounded)
            {
                fighter2.SetFacing(p2Facing);
            }
        }
    }
}
