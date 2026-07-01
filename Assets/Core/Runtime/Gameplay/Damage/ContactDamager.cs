using UnityEngine;

namespace hp55games.Mobile.Core.Gameplay.Damage
{
    /// <summary>
    /// Inflicts damage to any IDamageable that enters or collides with this GameObject.
    ///
    /// Physics mode is implicit: Unity dispatches 2D callbacks (OnTriggerEnter2D,
    /// OnCollisionEnter2D) when a Collider2D is present on this object, and 3D callbacks
    /// (OnTriggerEnter, OnCollisionEnter) when a Collider (3D) is present.
    /// Both can coexist on the same GameObject when mixed-pipeline contact is needed.
    ///
    /// Performance note: LayerMask is a fast bitwise pre-filter that avoids the
    /// more expensive TryGetComponent call on non-target colliders. Set it to
    /// the exact layers that carry IDamageable components.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ContactDamager : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Layers eligible to receive damage. " +
                 "Narrow this to avoid unnecessary TryGetComponent calls per contact event.")]
        private LayerMask _targetLayers = ~0;   // ~0 = Everything

        [SerializeField]
        [Tooltip("When true this contact is unconditionally lethal. " +
                 "Amount is ignored and locked to 0 — the IDamageable decides what 'fatal' means.")]
        private bool _isFatal = false;

        [SerializeField]
        [Tooltip("Damage dealt per contact. Ignored (and locked to 0) when IsFatal is true.")]
        private float _damageAmount = 1f;

        // ── 2D Physics ─────────────────────────────────────────────────────────

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsTargetLayer(other.gameObject.layer))
                return;
            if (!other.TryGetComponent(out IDamageable target))
                return;

            // ClosestPoint gives the best available approximation for trigger contacts
            // (no ContactPoint2D is available for triggers).
            Vector3 contactPoint = other.ClosestPoint(transform.position);
            target.TakeDamage(BuildContext(contactPoint));
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!IsTargetLayer(collision.gameObject.layer))
                return;
            if (!collision.collider.TryGetComponent(out IDamageable target))
                return;

            Vector3 contactPoint = collision.GetContact(0).point;
            target.TakeDamage(BuildContext(contactPoint));
        }

        // ── 3D Physics ─────────────────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (!IsTargetLayer(other.gameObject.layer))
                return;
            if (!other.TryGetComponent(out IDamageable target))
                return;

            Vector3 contactPoint = other.ClosestPoint(transform.position);
            target.TakeDamage(BuildContext(contactPoint));
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsTargetLayer(collision.gameObject.layer))
                return;
            if (!collision.collider.TryGetComponent(out IDamageable target))
                return;

            Vector3 contactPoint = collision.GetContact(0).point;
            target.TakeDamage(BuildContext(contactPoint));
        }

        // ── Internal ───────────────────────────────────────────────────────────

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool IsTargetLayer(int layer) => (_targetLayers.value & (1 << layer)) != 0;

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private DamageContext BuildContext(Vector3 contactPoint) =>
            new DamageContext(gameObject, contactPoint, _isFatal ? 0f : _damageAmount, _isFatal);

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_isFatal)
                _damageAmount = 0f;   // lock amount when fatal — no partial damage makes sense
            else
                _damageAmount = Mathf.Max(0f, _damageAmount);
        }
#endif
    }
}
