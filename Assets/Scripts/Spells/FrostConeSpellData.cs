using UnityEngine;
using Wayfarer.Combat;

namespace Wayfarer.Spells
{
    [CreateAssetMenu(menuName = "Wayfarer/Spells/Frost Cone", fileName = "FrostConeSpellData")]
    public class FrostConeSpellData : SpellData
    {
        public float range = 8f;
        [Range(1f, 179f)] public float halfAngle = 30f;
        public float freezeDuration = 3f;

        [Header("Ground Indicator")]
        public GameObject groundIndicatorPrefab;
        [Tooltip("How long the ice-blue ground cone stays visible (fade in + hold + fade out).")]
        public float groundIndicatorDuration = 1f;

        public override void Cast(SpellCastContext context)
        {
            Collider[] hits = Physics.OverlapSphere(context.Origin.position, range, context.TargetMask);

            foreach (var hit in hits)
            {
                Vector3 toTarget = hit.transform.position - context.Origin.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude < 0.0001f) continue;

                float angle = Vector3.Angle(context.Origin.forward, toTarget.normalized);
                if (angle > halfAngle) continue;

                var freezable = hit.GetComponentInParent<Freezable>();
                if (freezable != null)
                {
                    freezable.Freeze(freezeDuration);
                }
            }

            var vfx = SpawnVfx(context);
            if (vfx != null)
            {
                // Applies to both the small-snow stream and the big gust-sphere sub-emitter -
                // both share the same Cone shape convention, so both need the actual cast angle.
                var particleSystems = vfx.GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in particleSystems)
                {
                    var shape = ps.shape;
                    shape.angle = halfAngle;
                    shape.radius = 0.5f;
                }
            }

            SpawnGroundIndicator(context);
        }

        // Shows an ice-blue pie-slice on the ground matching this cast's range/halfAngle, snapped
        // to the actual terrain beneath the caster (not the caster's own, possibly elevated,
        // SpellOrigin height) and oriented to the caster's flattened forward direction.
        private void SpawnGroundIndicator(SpellCastContext context)
        {
            if (groundIndicatorPrefab == null) return;

            Vector3 groundPos = ResolveGroundPosition(context.Origin.position, context.TargetMask);

            Vector3 forwardFlat = context.Origin.forward;
            forwardFlat.y = 0f;
            Quaternion rotation = forwardFlat.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(forwardFlat.normalized)
                : Quaternion.identity;

            var instance = Object.Instantiate(groundIndicatorPrefab, groundPos, rotation);
            var indicator = instance.GetComponent<GroundConeIndicator>();
            if (indicator != null)
            {
                indicator.Initialize(range, halfAngle, groundIndicatorDuration);
            }
            else
            {
                Object.Destroy(instance, groundIndicatorDuration + 0.5f);
            }
        }

        // Mirrors ShatterSpellData's ground-snap approach: raycast straight down excluding
        // target-layer colliders (and Ignore Raycast) so it lands on real terrain rather than
        // stopping on an enemy collider standing under the caster's SpellOrigin.
        private Vector3 ResolveGroundPosition(Vector3 originPos, LayerMask targetMask)
        {
            int ignoreRaycastLayer = 1 << 2;
            int groundMask = ~(targetMask.value | ignoreRaycastLayer);

            Vector3 rayOrigin = originPos + Vector3.up * 2f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 10f, groundMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }
            return originPos;
        }
    }
}
