using UnityEngine;
using Wayfarer.Combat;

namespace Wayfarer.Spells
{
    /// <summary>
    /// Signature Frost Cone -> Shatter combo payoff: bonus damage to frozen targets, and the
    /// primary water-sustain play (blobs scale with how many frozen targets get shattered).
    /// </summary>
    [CreateAssetMenu(menuName = "Wayfarer/Spells/Shatter", fileName = "ShatterSpellData")]
    public class ShatterSpellData : SpellData
    {
        public float range = 8f;
        public float baseDamage = 10f;
        public float bonusDamageToFrozen = 40f;
        public float waterBlobAmountPerFrozenTarget = 8f;
        public int blobsPerFrozenTarget = 4;

public override void Cast(SpellCastContext context)
        {
            Collider[] hits = Physics.OverlapSphere(context.Origin.position, range, context.TargetMask);
            int frozenShattered = 0;

            foreach (var hit in hits)
            {
                var health = hit.GetComponentInParent<Health>();
                var freezable = hit.GetComponentInParent<Freezable>();
                bool wasFrozen = freezable != null && freezable.IsFrozen;

                float damage = baseDamage + (wasFrozen ? bonusDamageToFrozen : 0f);
                if (health != null)
                {
                    health.TakeDamage(damage);
                }

                if (wasFrozen)
                {
                    freezable.ConsumeFreeze();
                    frozenShattered++;

                    if (context.WaterBlobPrefab != null)
                    {
                        float perBlobAmount = waterBlobAmountPerFrozenTarget / Mathf.Max(1, blobsPerFrozenTarget);
                        for (int i = 0; i < blobsPerFrozenTarget; i++)
                        {
                            WaterBlob.Spawn(context.WaterBlobPrefab, hit.bounds.center, perBlobAmount);
                        }
                    }
                }
            }

            var vfx = SpawnVfx(context);
            if (vfx != null)
            {
                var ps = vfx.GetComponentInChildren<ParticleSystem>();
                if (ps != null)
                {
                    var shape = ps.shape;
                    shape.radius = range;
                }

                float vfxScale = Mathf.Clamp(range / 8f, 0.5f, 3f);
                vfx.transform.localScale = Vector3.one * vfxScale;
            }
        }
    }
}
