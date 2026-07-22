using UnityEngine;
using Wayfarer.Combat;

namespace Wayfarer.Spells
{
    /// <summary>Simple forward-traveling projectile for Ice Bolt. Damages Health on hit and
    /// drops a water blob, per the design brief's water-sustain loop. Expires early (in a puff
    /// of snow, no damage) either after a short travel distance or the instant it hits anything
    /// that isn't a valid target - e.g. the ground/environment - rather than always waiting out
    /// its full lifetime.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class IceBoltProjectile : MonoBehaviour
    {
        [SerializeField] private float lifetime = 5f;
        [Tooltip("Maximum distance the bolt can travel before it expires in a puff of snow, even without hitting anything.")]
        [SerializeField] private float maxTravelDistance = 12f;
        [SerializeField] private GameObject impactPuffPrefab;
        [SerializeField] private float impactPuffLifetime = 1f;

        private float damage;
        private float speed;
        private LayerMask targetMask;
        private GameObject waterBlobPrefab;
        private float waterBlobAmount;
        private Vector3 spawnPosition;
        private bool expired;

        public void Initialize(float damage, float speed, LayerMask targetMask, GameObject waterBlobPrefab, float waterBlobAmount)
        {
            this.damage = damage;
            this.speed = speed;
            this.targetMask = targetMask;
            this.waterBlobPrefab = waterBlobPrefab;
            this.waterBlobAmount = waterBlobAmount;

            spawnPosition = transform.position;

            var rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearVelocity = transform.forward * speed;

            Destroy(gameObject, lifetime);
        }

        private void Update()
        {
            if (expired) return;

            if (Vector3.Distance(transform.position, spawnPosition) >= maxTravelDistance)
            {
                ExpireInPuff();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (expired) return;

            // Never collide with the caster themselves - the bolt spawns close to the player's
            // cast origin and would otherwise immediately expire on its own CharacterController.
            if (other.GetComponentInParent<PlayerController>() != null) return;

            if (((1 << other.gameObject.layer) & targetMask) != 0)
            {
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

                expired = true;
                Destroy(gameObject);
                return;
            }

            // Hit something that isn't a valid target - treat as the ground/environment and
            // fizzle out early instead of punching through.
            ExpireInPuff();
        }

        private void ExpireInPuff()
        {
            if (expired) return;
            expired = true;

            if (impactPuffPrefab != null)
            {
                var puff = Instantiate(impactPuffPrefab, transform.position, Quaternion.identity);
                Destroy(puff, impactPuffLifetime);
            }

            Destroy(gameObject);
        }
    }
}
