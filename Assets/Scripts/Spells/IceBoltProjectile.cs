using UnityEngine;
using Wayfarer.Combat;

namespace Wayfarer.Spells
{
    /// <summary>Simple forward-traveling projectile for Ice Bolt. Damages Health on hit and
    /// drops a water blob, per the design brief's water-sustain loop.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class IceBoltProjectile : MonoBehaviour
    {
        [SerializeField] private float lifetime = 5f;

        private float damage;
        private float speed;
        private LayerMask targetMask;
        private GameObject waterBlobPrefab;
        private float waterBlobAmount;

        public void Initialize(float damage, float speed, LayerMask targetMask, GameObject waterBlobPrefab, float waterBlobAmount)
        {
            this.damage = damage;
            this.speed = speed;
            this.targetMask = targetMask;
            this.waterBlobPrefab = waterBlobPrefab;
            this.waterBlobAmount = waterBlobAmount;

            var rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearVelocity = transform.forward * speed;

            Destroy(gameObject, lifetime);
        }

private void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & targetMask) == 0) return;

            var health = other.GetComponentInParent<Health>();
            if (health != null)
            {
                health.TakeDamage(damage);
            }

            if (waterBlobPrefab != null)
            {
                Vector3 popSource = other.bounds.center;
                WaterBlob.Spawn(waterBlobPrefab, popSource, waterBlobAmount);
            }

            Destroy(gameObject);
        }
    }
}
