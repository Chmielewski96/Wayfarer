using UnityEngine;

namespace Wayfarer.Spells
{
    /// <summary>
    /// Flat-shaped ground mesh - either a thin ring outline (range boundary), a partial-sweep
    /// arc of that ring (e.g. Ice Bolt's forward-facing cone of reach), or a filled disc
    /// (impact/strike area) - used by SpellRangeIndicatorController to preview a spell's
    /// range/effect area on the ground while it's armed.
    ///
    /// Every vertex samples the actual terrain height beneath it (not just one flat height at
    /// the center), so the shape hugs slopes instead of half the ring floating above the ground
    /// or clipping underground on anything but perfectly flat terrain. This means the mesh is
    /// rebuilt every call to UpdateIndicator rather than only when the radius/shape changes -
    /// unavoidable since the terrain under it changes as the caster moves, even if the radius
    /// doesn't. Segment counts here are low enough (48) that this is cheap.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class GroundCircleIndicator : MonoBehaviour
    {
        [SerializeField] private int segments = 96;
        [Tooltip("Vertical offset above the sampled terrain height. Small enough to avoid z-fighting, but also has to clear the worst-case 'chord sag' on sloped terrain - between two adjacent sampled vertices the mesh is flat, so on a slope the true ground can bulge up above that straight edge; this needs to be taller than that bulge or the ring visibly clips into the slope even though every individual vertex sits exactly on the ground.")]
        [SerializeField] private float yOffset = 0.2f;
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

            mesh = new Mesh { name = "GroundCircleIndicator" };
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        private float SampleGroundY(Vector3 worldPos)
        {
            if (terrain == null) return worldPos.y + yOffset;
            return terrain.SampleHeight(worldPos) + terrain.transform.position.y + yOffset;
        }

        // worldCenter only needs XZ to be meaningful - its Y is ignored in favor of the actual
        // sampled terrain height at that XZ. facing only matters when sweepAngleDegrees < 360
        // (an arc) or for a filled disc (ignored - disc is always a full symmetric area).
        public void UpdateIndicator(Vector3 worldCenter, Quaternion facing, float radius, bool filled,
            float ringThickness = 0.4f, float sweepAngleDegrees = 360f)
        {
            float centerGroundY = SampleGroundY(worldCenter);
            transform.position = new Vector3(worldCenter.x, centerGroundY, worldCenter.z);

            if (filled) BuildFilledDisc(worldCenter, centerGroundY, radius);
            else BuildRing(worldCenter, centerGroundY, facing, radius, ringThickness, sweepAngleDegrees);
        }

        private void EnsureBuffers(int vertCount, int triCount)
        {
            if (vertices == null || vertices.Length != vertCount)
            {
                vertices = new Vector3[vertCount];
            }
            if (triangles == null || triangles.Length != triCount)
            {
                triangles = new int[triCount];
            }
        }

        private void BuildFilledDisc(Vector3 worldCenter, float centerGroundY, float radius)
        {
            EnsureBuffers(segments + 1, segments * 3);

            vertices[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float angle = (360f / segments) * i * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Vector3 worldPoint = worldCenter + dir * radius;
                float localY = SampleGroundY(worldPoint) - centerGroundY;
                vertices[i + 1] = new Vector3(dir.x * radius, localY, dir.z * radius);
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = next + 1;
            }

            ApplyMesh();
        }

        // sweepAngleDegrees < 360 produces an open arc band instead of a closed ring - centered
        // on the given facing direction, spanning +/- sweepAngleDegrees/2. At exactly 360 this
        // reduces to the original full-circle formula, so full-ring callers are unaffected by
        // facing (a full circle looks the same at any rotation).
        private void BuildRing(Vector3 worldCenter, float centerGroundY, Quaternion facing,
            float radius, float thickness, float sweepAngleDegrees)
        {
            float outer = radius;
            float inner = Mathf.Max(0.05f, radius - thickness);
            float startAngle = -sweepAngleDegrees * 0.5f;
            float angleStep = sweepAngleDegrees / segments;

            int ringVerts = segments + 1;
            EnsureBuffers(ringVerts * 2, segments * 6);

            for (int i = 0; i < ringVerts; i++)
            {
                float angle = (startAngle + angleStep * i) * Mathf.Deg2Rad;
                Vector3 localDir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Vector3 dir = facing * localDir;

                Vector3 innerWorld = worldCenter + dir * inner;
                Vector3 outerWorld = worldCenter + dir * outer;
                float innerY = SampleGroundY(innerWorld) - centerGroundY;
                float outerY = SampleGroundY(outerWorld) - centerGroundY;

                vertices[i * 2] = new Vector3(dir.x * inner, innerY, dir.z * inner);
                vertices[i * 2 + 1] = new Vector3(dir.x * outer, outerY, dir.z * outer);
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

            ApplyMesh();
        }

        private void ApplyMesh()
        {
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}
