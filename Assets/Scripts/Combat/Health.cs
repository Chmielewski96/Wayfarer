using UnityEngine;

namespace Wayfarer.Combat
{
    /// <summary>
    /// Shared damageable component. Anything spells can hit (enemies, test dummies,
    /// eventually the player) carries one of these.
    /// </summary>
    public class Health : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead { get; private set; }

        public System.Action<float> OnDamaged; // passes damage amount
        public System.Action OnDied;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            OnDamaged?.Invoke(amount);

            if (currentHealth <= 0f)
            {
                IsDead = true;
                OnDied?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        }
    }
}
