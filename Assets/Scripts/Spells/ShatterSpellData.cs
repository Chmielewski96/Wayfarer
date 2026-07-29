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
        [Tooltip("Blast radius at the impact point - how far the strike's damage reaches from where it lands, NOT how far the player can target it (see castRange).")]
        public float range = 8f;
        [Tooltip("Max distance from the caster the ground target point can be. Aiming further than this clamps the strike to land at the edge of range, in the aimed direction, rather than letting it land anywhere the camera can see.")]
        public float castRange = 15f;
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

            Vector3 groundPos = GroundTargetingUtility.ResolveGroundPosition(context.AimPoint, context.TargetMask);
            groundPos = GroundTargetingUtility.ClampToRange(groundPos, context.Origin.position, castRange, context.TargetMask);

            var instance = Object.Instantiate(strikePrefab, groundPos, Quaternion.identity);
            var controller = instance.GetComponent<ShatterStrikeController>();
            if (controller != null)
            {
                controller.Initialize(telegraphDuration, range, baseDamage, bonusDamageToFrozen,
                    waterBlobAmountPerFrozenTarget, blobsPerFrozenTarget,
                    context.TargetMask, context.WaterBlobPrefab);
            }
        }
    }
}
