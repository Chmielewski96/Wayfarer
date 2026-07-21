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
                var ps = vfx.GetComponentInChildren<ParticleSystem>();
                if (ps != null)
                {
                    var shape = ps.shape;
                    shape.angle = halfAngle;
                    shape.radius = 0.5f;
                    var main = ps.main;
                    main.startLifetime = 0.4f;
                }
            }
        }
    }
}
