using UnityEngine;

// Procedurally builds a stylized scallop-shell mesh (a domed fan with fluted
// ridges radiating from a hinge point) directly into a MeshFilter. This is a
// placeholder art asset - no 3D model generation provider is configured in
// this project yet - but it reads clearly as a seashell silhouette and every
// parameter is tweakable in the inspector, so it can be swapped for an
// authored/generated model later without touching any gameplay code.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class ShellMeshGenerator : MonoBehaviour
{
    [Header("Shape")]
    [SerializeField] private float radius = 0.35f;
    [SerializeField] private int ridgeCount = 12;
    [SerializeField] private float ridgeDepth = 0.12f;
    [SerializeField] private float domeHeight = 0.18f;
    [SerializeField] private float bottomCurve = 0.05f;
    [SerializeField] private int radialSegments = 40;
    [SerializeField] private int ringSegments = 10;

    private Mesh generatedMesh;

    private void OnEnable()
    {
        Generate();
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0.05f, radius);
        ridgeCount = Mathf.Max(3, ridgeCount);
        radialSegments = Mathf.Max(6, radialSegments);
        ringSegments = Mathf.Max(2, ringSegments);
        Generate();
    }

    public void Generate()
    {
        if (generatedMesh == null)
        {
            generatedMesh = new Mesh();
            generatedMesh.name = "ProceduralShell";
        }
        else
        {
            generatedMesh.Clear();
        }

        int vertsPerRing = radialSegments + 1;
        int totalRings = ringSegments + 1;

        System.Collections.Generic.List<Vector3> vertices = new System.Collections.Generic.List<Vector3>();
        System.Collections.Generic.List<Vector3> normals = new System.Collections.Generic.List<Vector3>();
        System.Collections.Generic.List<int> triangles = new System.Collections.Generic.List<int>();

        // Top (domed, fluted) surface - fan shape spanning -90..+90 degrees,
        // hinge at the origin, outer curved edge fluted by ridgeCount.
        for (int ring = 0; ring < totalRings; ring++)
        {
            float t = ring / (float)ringSegments;
            float r = t * radius;

            for (int seg = 0; seg <= radialSegments; seg++)
            {
                float u = seg / (float)radialSegments;
                float angle = Mathf.Lerp(-90f, 90f, u) * Mathf.Deg2Rad;

                float ridgeWave = Mathf.Cos(ridgeCount * angle);
                float edgeFlare = 1f + 0.06f * ridgeWave;
                float rr = r * edgeFlare;

                float x = Mathf.Sin(angle) * rr;
                float z = Mathf.Cos(angle) * rr;

                float domeFalloff = 1f - t * t;
                float y = domeHeight * domeFalloff + ridgeDepth * domeFalloff * ridgeWave;

                vertices.Add(new Vector3(x, y, z));
            }
        }

        for (int ring = 0; ring < ringSegments; ring++)
        {
            for (int seg = 0; seg < radialSegments; seg++)
            {
                int a = ring * vertsPerRing + seg;
                int b = a + 1;
                int c = a + vertsPerRing;
                int d = c + 1;

                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);

                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(d);
            }
        }

        int topVertCount = vertices.Count;

        // Bottom (shallow, mostly flat) surface, closing the shell into a
        // thin solid volume.
        for (int ring = 0; ring < totalRings; ring++)
        {
            float t = ring / (float)ringSegments;
            float r = t * radius;

            for (int seg = 0; seg <= radialSegments; seg++)
            {
                float u = seg / (float)radialSegments;
                float angle = Mathf.Lerp(-90f, 90f, u) * Mathf.Deg2Rad;

                float ridgeWave = Mathf.Cos(ridgeCount * angle);
                float edgeFlare = 1f + 0.06f * ridgeWave;
                float rr = r * edgeFlare;

                float x = Mathf.Sin(angle) * rr;
                float z = Mathf.Cos(angle) * rr;

                float domeFalloff = 1f - t * t;
                float y = -bottomCurve * domeFalloff;

                vertices.Add(new Vector3(x, y, z));
            }
        }

        for (int ring = 0; ring < ringSegments; ring++)
        {
            for (int seg = 0; seg < radialSegments; seg++)
            {
                int a = topVertCount + ring * vertsPerRing + seg;
                int b = a + 1;
                int c = a + vertsPerRing;
                int d = c + 1;

                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);

                triangles.Add(b);
                triangles.Add(d);
                triangles.Add(c);
            }
        }

        // Outer rim, stitching top edge to bottom edge so the shell reads as
        // a closed solid rather than two floating surfaces.
        int topOuterStart = ringSegments * vertsPerRing;
        int bottomOuterStart = topVertCount + ringSegments * vertsPerRing;

        for (int seg = 0; seg < radialSegments; seg++)
        {
            int at = topOuterStart + seg;
            int bt = at + 1;
            int ab = bottomOuterStart + seg;
            int bb = ab + 1;

            triangles.Add(at);
            triangles.Add(bt);
            triangles.Add(ab);

            triangles.Add(bt);
            triangles.Add(bb);
            triangles.Add(ab);
        }

        generatedMesh.SetVertices(vertices);
        generatedMesh.SetTriangles(triangles, 0);
        generatedMesh.RecalculateNormals();
        generatedMesh.RecalculateBounds();
        generatedMesh.RecalculateTangents();

        MeshFilter mf = GetComponent<MeshFilter>();
        mf.sharedMesh = generatedMesh;
    }
}
