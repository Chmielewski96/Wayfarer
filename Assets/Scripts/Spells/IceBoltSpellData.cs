using UnityEngine;

namespace Wayfarer.Spells
{
    [CreateAssetMenu(menuName = "Wayfarer/Spells/Ice Bolt", fileName = "IceBoltSpellData")]
    public class IceBoltSpellData : SpellData
    {
        public GameObject projectilePrefab;
        public float damage = 15f;
        public float speed = 25f;
        public float waterBlobAmount = 5f;

public override void Cast(SpellCastContext context)
        {
            if (projectilePrefab == null)
            {
                Debug.LogWarning("IceBoltSpellData has no projectilePrefab assigned.");
                return;
            }

            Vector3 spawnPos = context.Origin.position;
            Vector3 direction = context.AimPoint - spawnPos;
            Quaternion rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction.normalized) : context.Origin.rotation;

            var instance = Object.Instantiate(projectilePrefab, spawnPos, rotation);
            var projectile = instance.GetComponent<IceBoltProjectile>();
            if (projectile != null)
            {
                projectile.Initialize(damage, speed, context.TargetMask, context.WaterBlobPrefab, waterBlobAmount);
            }
        }
    }
}
