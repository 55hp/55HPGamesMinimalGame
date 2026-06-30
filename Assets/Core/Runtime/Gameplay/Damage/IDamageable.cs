namespace hp55games.Mobile.Core.Gameplay.Damage
{
    /// <summary>
    /// Implemented by any entity that can receive and react to damage.
    ///
    /// Implementations are responsible for guarding their own alive state.
    /// Callers (e.g. ContactDamager) do NOT check IsAlive before calling TakeDamage;
    /// the contract is that TakeDamage is always safe to call.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(DamageContext ctx);
    }
}
