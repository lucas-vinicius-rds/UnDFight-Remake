using System.Collections.Generic;
using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// One-way kinematic -> dynamic PhysX coupler mounted on a dedicated child GameObject
    /// (layer FighterDebrisPusher, slot 12) under each FighterController.
    /// Moves via Rigidbody.MovePosition synchronized with the deterministic 60 Hz FighterController
    /// and kicks/pushes StageDebris Rigidbodies without EVER applying forces back to the fighter.
    /// </summary>
    public class FighterDebrisPusher : MonoBehaviour
    {
        public const string DefaultChildName = "FighterDebrisPusher";

        [Header("References")]
        [SerializeField] private FighterController fighter;
        [SerializeField] private Rigidbody body;
        [SerializeField] private CapsuleCollider pusherCollider;

        [Header("One-Way Kick/Push Tuning")]
        [SerializeField, Min(0.1f)] private float horizontalImpulseScale = 0.65f;
        [SerializeField, Min(0.1f)] private float minContactPushImpulse = 1.25f;
        [SerializeField, Min(0f)] private float upwardKickImpulse = 0.75f;
        [SerializeField, Min(0f)] private float lateralZScatter = 0.45f;
        [SerializeField, Min(1f)] private float dashMultiplier = 1.65f;
        [SerializeField, Min(1f)] private float attackMultiplier = 1.50f;
        [SerializeField, Min(0.02f)] private float stayPushCooldownSeconds = 0.08f;

        private readonly Dictionary<Rigidbody, float> _lastPushTimes = new Dictionary<Rigidbody, float>();
        private Vector3 _lastSyncedWorldPos;

        public FighterController Fighter => fighter;
        public Rigidbody Body => body;
        public CapsuleCollider PusherCollider => pusherCollider;

        private void Awake()
        {
            if (fighter == null)
            {
                fighter = GetComponentInParent<FighterController>();
            }
            EnsureComponents();
            SyncFromFighter(useMovePosition: false);
        }

        private void OnDisable()
        {
            _lastPushTimes.Clear();
        }

        public void BindFighter(FighterController targetFighter)
        {
            fighter = targetFighter;
            EnsureComponents();
            SyncFromFighter(useMovePosition: false);
        }

        public void EnsureComponents()
        {
            int pusherLayer = LayerMask.NameToLayer(StagePhysicsLayers.FighterDebrisPusherLayerName);
            if (pusherLayer >= 0)
            {
                gameObject.layer = pusherLayer;
            }

            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            if (pusherCollider == null)
            {
                pusherCollider = GetComponent<CapsuleCollider>();
            }
            if (pusherCollider == null)
            {
                pusherCollider = gameObject.AddComponent<CapsuleCollider>();
            }
            pusherCollider.isTrigger = false;
            pusherCollider.direction = 1; // Y-axis capsule
        }

        /// <summary>
        /// Synchronizes capsule dimensions and world position strictly from the deterministic FighterController.
        /// Never writes back to FighterController.SimPosition or SimVelocity.
        /// </summary>
        public void SyncFromFighter(bool useMovePosition = true)
        {
            if (fighter == null)
            {
                fighter = GetComponentInParent<FighterController>();
                if (fighter == null)
                {
                    return;
                }
            }

            EnsureComponents();

            float radius = Mathf.Max(0.20f, fighter.PushboxRadius * 1.05f);
            float fullHeight = fighter.Data != null ? fighter.Data.heightMeters : FighterScaling.ReferenceHeightMeters;
            float targetHeight;

            if (fighter.State == FighterState.KnockDown)
            {
                targetHeight = Mathf.Max(radius * 2f, fullHeight * 0.34f);
            }
            else
            {
                targetHeight = Mathf.Max(radius * 2f, fighter.CurrentHurtboxHeight);
            }

            pusherCollider.radius = radius;
            pusherCollider.height = targetHeight;
            pusherCollider.center = new Vector3(0f, targetHeight * 0.5f, 0f);

            Vector3 targetWorldPos = new Vector3(fighter.SimPosition.x, fighter.SimPosition.y, 0f);

            if (useMovePosition && body != null)
            {
                body.MovePosition(targetWorldPos);
                body.MoveRotation(Quaternion.identity);
            }
            else
            {
                transform.SetPositionAndRotation(targetWorldPos, Quaternion.identity);
                if (body != null)
                {
                    body.position = targetWorldPos;
                    body.rotation = Quaternion.identity;
                }
            }

            _lastSyncedWorldPos = targetWorldPos;
        }

        private void FixedUpdate()
        {
            if (fighter == null || !fighter.isActiveAndEnabled)
            {
                return;
            }
            SyncFromFighter(useMovePosition: true);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null)
            {
                return;
            }
            Rigidbody otherRb = collision.rigidbody;
            Vector3 contact = collision.contactCount > 0
                ? collision.GetContact(0).point
                : (otherRb != null ? otherRb.worldCenterOfMass : transform.position);
            ApplyPushToBody(otherRb, contact, isStay: false);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (collision == null)
            {
                return;
            }
            Rigidbody otherRb = collision.rigidbody;
            Vector3 contact = collision.contactCount > 0
                ? collision.GetContact(0).point
                : (otherRb != null ? otherRb.worldCenterOfMass : transform.position);
            ApplyPushToBody(otherRb, contact, isStay: true);
        }

        /// <summary>
        /// Applies a calibrated one-way kick/push impulse to a dynamic StageDebris Rigidbody.
        /// Wakes sleeping debris bodies automatically.
        /// </summary>
        public bool ApplyPushToBody(Rigidbody otherRb, Vector3 contactPoint, bool isStay = false)
        {
            if (otherRb == null || otherRb.isKinematic)
            {
                return false;
            }

            int debrisLayer = LayerMask.NameToLayer(StagePhysicsLayers.StageDebrisLayerName);
            if (debrisLayer >= 0 && otherRb.gameObject.layer != debrisLayer)
            {
                return false;
            }

            float now = Time.time;
            if (isStay && _lastPushTimes.TryGetValue(otherRb, out float lastPush) && (now - lastPush) < stayPushCooldownSeconds)
            {
                return false;
            }

            float velX = fighter != null ? fighter.SimVelocity.x : 0f;
            float speedX = Mathf.Abs(velX);
            FighterState state = fighter != null ? fighter.State : FighterState.Idle;

            bool isDashing = state == FighterState.DashForward || state == FighterState.DashBackward;
            bool isAttacking = state == FighterState.Attack;

            if (isStay && speedX < 0.15f && !isDashing && !isAttacking)
            {
                return false;
            }

            float dx = otherRb.worldCenterOfMass.x - _lastSyncedWorldPos.x;
            float pushSign = speedX > 0.2f
                ? Mathf.Sign(velX)
                : (Mathf.Abs(dx) > 0.01f ? Mathf.Sign(dx) : (fighter != null ? fighter.FacingSign : 1f));

            float stateMult = 1f;
            if (isDashing) stateMult = dashMultiplier;
            else if (isAttacking) stateMult = attackMultiplier;

            float rawMag = Mathf.Max(minContactPushImpulse, speedX * horizontalImpulseScale) * stateMult;
            if (isStay)
            {
                rawMag *= 0.45f;
            }

            float dz = otherRb.worldCenterOfMass.z - _lastSyncedWorldPos.z;
            float zSign = Mathf.Abs(dz) > 0.02f ? Mathf.Sign(dz) : ((otherRb.GetHashCode() & 1) == 0 ? 1f : -1f);
            float upFactor = isStay ? 0.25f : 1f;

            Vector3 impulse = new Vector3(
                pushSign * rawMag,
                upwardKickImpulse * stateMult * upFactor,
                zSign * lateralZScatter * Mathf.Clamp(stateMult, 1f, 1.5f) * upFactor);

            otherRb.WakeUp();
            otherRb.AddForceAtPosition(impulse, contactPoint, ForceMode.Impulse);
            _lastPushTimes[otherRb] = now;
            return true;
        }
    }
}
