using UnityEngine;

namespace Wayfarer.Spells
{
    /// <summary>
    /// Persistent (non-fading) trapezoid ground mesh, used by SpellRangeIndicatorController to
    /// preview Frost Cone's range/angle for as long as the spell stays armed - unlike the brief
    /// cast-time flash GroundConeIndicator plays on an actual cast.
    ///
    /// Every vertex samples the actual terrain height beneath it (see GroundCircleIndicator for
    /// why), so the shape hugs slopes instead of clipping through them. Rebuilt every call to
    /// UpdateIndicator rather than cached, since the terrain under it changes as the caster
    /// moves even when range/angle don't.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class GroundConeIndicatorLive : MonoBehaviour
    {
        [SerializeField] private int segments = 24;
        [SerializeField] private float yOffset = 0.03f;
        [Tooltip("Near edge distance from the caster, as a fraction of range - keeps the shape a trapezoid instead of pinching to a point (matches GroundConeIndicator).")]
        [SerializeField] private float innerRadiusFraction = 0.22f;
        [SerializeField] private float minInnerRadius = 0.6f;
        [Tooltip("Optional - auto-found via Terrain.activeTerrain. Used to sample ground height per vertex.")]
        [SerializeField] private Terrain terrain;

        private Mesh mesh;
        private Vector3[] vertices;
        private int[] triangles;

        private void Awake()
        {
            if (terrain == null)
            {
                terrain = Terrain.activeTerrain;
            }

            mesh = new Mesh { name = "GroundConeIndicatorLive" };
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        private float SampleGroundY(Vector3 worldPos)
        {
            if (terrain == null) return worldPos.y + yOffset;
            return terrain.SampleHeight(worldPos) + terrain.transform.position.y + yOffset;
        }

        public void UpdateIndicator(Vector3 worldCenter, Quaternion facing, float range, float halfAngle)
        {
            float centerGroundY = SampleGroundY(worldCenter);
            transform.position = new Vector3(worldCenter.x, centerGroundY, worldCenter.z);

            float innerRadius = Mathf.Min(range * 0.9f, Mathf.Max(minInnerRadius, range * innerRadiusFraction));

            int ringVerts = segments + 1;
            int vertCount = ringVerts * 2;
            int triCount = segments * 6;
            if (vertices == null || vertices.Length != vertCount) { vertices = new Vector3[vertCount]; }
            if (triangles == null || triangles.Length != triCount) { triangles = new int[triCount]; }

            float startAngle = -halfAngle;
            float angleStep = (halfAngle * 2f) / segments;
            for (int i = 0; i < ringVerts; i++)
            {
                float angle = (startAngle + angleStep * i) * Mathf.Deg2Rad;
                Vector3 localDir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Vector3 dir = facing * localDir;

                Vector3 innerWorld = worldCenter + dir * innerRadius;
                Vector3 outerWorld = worldCenter + dir * range;
                float innerY = SampleGroundY(innerWorld) - centerGroundY;
                float outerY = SampleGroundY(outerWorld) - centerGroundY;

                vertices[i * 2] = new Vector3(dir.x * innerRadius, innerY, dir.z * innerRadius);
                vertices[i * 2 + 1] = new Vector3(dir.x * range, outerY, dir.z * range);
            }

            for (int i = 0; i < segments; i++)
            {
                int inner0 = i * 2;
                int outer0 = i * 2 + 1;
                int inner1 = (i + 1) * 2;
                int outer1 = (i + 1) * 2 + 1;

                int triIndex = i * 6;
                triangles[triIndex] = inner0;
                triangles[triIndex + 1] = outer0;
                triangles[triIndex + 2] = outer1;

                triangles[triIndex + 3] = inner0;
                triangles[triIndex + 4] = outer1;
                triangles[triIndex + 5] = inner1;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}
