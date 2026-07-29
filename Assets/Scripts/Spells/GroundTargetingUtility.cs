using UnityEngine;

namespace Wayfarer.Spells
{
    /// <summary>
    /// Shared ground-targeting math used by ground-target spells (currently Shatter) and their
    /// live selection-time range indicators (SpellRangeIndicatorController), so the preview shown
    /// while a spell is armed always matches exactly where the real cast would land - one source
    /// of truth instead of the indicator guessing at a duplicate implementation that could drift.
    /// </summary>
    public static class GroundTargetingUtility
    {
        // Raycasts straight down from a point 2 units above aimPoint, excluding target-layer
        // colliders (aiming at an enemy's body shouldn't stop the ground-snap partway up their
        // collider) and the Ignore Raycast layer, landing on the actual terrain/environment
        // beneath instead.
        public static Vector3 ResolveGroundPosition(Vector3 aimPoint, LayerMask targetMask)
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

        // If groundPos is further than maxRange from originPos (horizontally - vertical
        // difference is ignored so standing on a hill/cliff doesn't skew this), pulls it in to
        // the range boundary along the same direction and re-resolves ground height there from
        // high above (the clamped point can sit on very different terrain height than the
        // original, unreachable aim point - e.g. clamping down off a hill).
        public static Vector3 ClampToRange(Vector3 groundPos, Vector3 originPos, float maxRange, LayerMask targetMask)
        {
            Vector3 flatDelta = groundPos - originPos;
            flatDelta.y = 0f;

            if (flatDelta.magnitude <= maxRange) return groundPos;

            Vector3 clampedFlat = flatDelta.normalized * maxRange;
            Vector3 clampedXZ = originPos + clampedFlat;

            int ignoreRaycastLayer = 1 << 2;
            int groundMask = ~(targetMask.value | ignoreRaycastLayer);
            Vector3 rayOrigin = new Vector3(clampedXZ.x, originPos.y + 50f, clampedXZ.z);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 200f, groundMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }
            return new Vector3(clampedXZ.x, originPos.y, clampedXZ.z);
        }
    }
}
