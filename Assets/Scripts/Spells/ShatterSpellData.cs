using UnityEngine;

namespace Wayfarer.Spells
{
    /// <summary>
    /// Signature Frost Cone -> Shatter combo payoff: bonus damage to frozen targets, and the
    /// primary water-sustain play (blobs scale with how many frozen targets get shattered).
    ///
    /// Ground-targeted at the aim point rather than centered on the caster: casting drops a
    /// telegraph (a purple circle that fades in on the ground) showing where a lightning strike
    /// will land, then telegraphDuration seconds later the strike actually hits - a beam of
    /// light-purple light from the sky plus a purple particle burst out of the ground - and only
    /// then is damage/frozen-shatter resolved. All of that timing lives in
    /// ShatterStrikeController, spawned here and handed the spell's tunables.
    /// </summary>
    [CreateAssetMenu(menuName = "Wayfarer/Spells/Shatter", fileName = "ShatterSpellData")]
    public class ShatterSpellData : SpellData
    {
        public float range = 8f;
        public float baseDamage = 10f;
        public float bonusDamageToFrozen = 40f;
        public float waterBlobAmountPerFrozenTarget = 8f;
        public int blobsPerFrozenTarget = 4;

        [Header("Telegraph / Strike")]
        public GameObject strikePrefab;
        [Tooltip("Seconds between casting (telegraph circle appears) and the actual thunder strike.")]
        public float telegraphDuration = 0.5f;

        public override void Cast(SpellCastContext context)
        {
            if (strikePrefab == null)
            {
                Debug.LogWarning("ShatterSpellData has no strikePrefab assigned.");
                return;
            }

            Vector3 groundPos = ResolveGroundPosition(context.AimPoint, context.TargetMask);

            var instance = Object.Instantiate(strikePrefab, groundPos, Quaternion.identity);
            var controller = instance.GetComponent<ShatterStrikeController>();
            if (controller != null)
            {
                controller.Initialize(telegraphDuration, range, baseDamage, bonusDamageToFrozen,
                    waterBlobAmountPerFrozenTarget, blobsPerFrozenTarget,
                    context.TargetMask, context.WaterBlobPrefab);
            }
        }

        // AimPoint can land on a target's own body (e.g. aiming at an enemy hits their collider
        // partway up, not the ground beneath them) rather than the actual ground - so the
        // downward snap-to-ground raycast explicitly excludes target-layer colliders (and the
        // Ignore Raycast layer) to make sure it passes through the enemy and lands on the real
        // terrain/environment underneath instead of re-hitting the same body.
        private Vector3 ResolveGroundPosition(Vector3 aimPoint, LayerMask targetMask)
        {
            int ignoreRaycastLayer = 1 << 2;
            int groundMask = ~(targetMask.value | ignoreRaycastLayer);

            Vector3 rayOrigin = aimPoint + Vector3.up * 2f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 10f, groundMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }
            return aimPoint;
        }
    }
}
