using UnityEngine;

namespace hp55games.Mobile.Core.Gameplay.Damage
{
    /// <summary>
    /// Immutable payload delivered to IDamageable.TakeDamage.
    /// Passed by value (readonly struct) to avoid heap allocations per contact.
    /// </summary>
    public readonly struct DamageContext
    {
        /// <summary>The GameObject that owns the ContactDamager.</summary>
        public readonly GameObject Source;

        /// <summary>World-space position of the first contact point.</summary>
        public readonly Vector3 ContactPoint;

        /// <summary>
        /// Damage amount. Meaningful only when IsFatal is false.
        /// Interpretation is up to the IDamageable implementer
        /// (e.g. raw HP reduction for HP-based entities).
        /// </summary>
        public readonly float Amount;

        /// <summary>
        /// When true the contact is unconditionally lethal regardless of Amount.
        /// IDamageable implementers that lack an HP system should only act on fatal contacts.
        /// </summary>
        public readonly bool IsFatal;

        public DamageContext(GameObject source, Vector3 contactPoint, float amount, bool isFatal)
        {
            Source       = source;
            ContactPoint = contactPoint;
            Amount       = amount;
            IsFatal      = isFatal;
        }
    }
}
