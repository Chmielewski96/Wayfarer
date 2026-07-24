using UnityEngine;

namespace Wayfarer.Spells
{
    /// <summary>
    /// Builds a flat trapezoid (annular sector) mesh on the ground showing a cone-shaped spell's
    /// range and angle (used by Frost Cone). Instead of a pie slice that pinches to a point at
    /// the caster, the near edge starts at innerRadius (a fraction of range) so the shape reads
    /// as a trapezoid that's already got width right in front of the caster's feet, widening out
    /// to the full halfAngle spread at range. Fades in quickly, holds, then fades out and
    /// self-destructs - the same "instance the material so fading doesn't affect other
    /// simultaneous casts" approach as ShatterStrikeController's telegraph circle.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class GroundConeIndicator : MonoBehaviour
    {
        [SerializeField] private int segments = 24;
        [SerializeField] private float fadeInDuration = 0.12f;
        [SerializeField] private float fadeOutDuration = 0.35f;
        [SerializeField] private float yOffset = 0.03f;
        [Tooltip("Near edge distance from the caster, as a fraction of range - keeps the shape a trapezoid instead of pinching to a point.")]
        [SerializeField] private float innerRadiusFraction = 0.22f;
        [SerializeField] private float minInnerRadius = 0.6f;

        private Renderer rend;
        private Color baseColor;
        private Color baseEmission;
        private float holdDuration;
        private float elapsed;

        private enum Phase { FadeIn, Hold, FadeOut, Done }
        private Phase phase;

        public void Initialize(float range, float halfAngle, float displayDuration)
        {
            BuildMesh(range, halfAngle);

            rend = GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material = new Material(rend.sharedMaterial);
                baseColor = rend.material.GetColor("_BaseColor");
                baseEmission = rend.material.HasProperty("_EmissionColor") ? rend.material.GetColor("_EmissionColor") : Color.black;
                SetAlpha(0f);
            }

            holdDuration = Mathf.Max(0f, displayDuration - fadeInDuration - fadeOutDuration);
            phase = Phase.FadeIn;
            elapsed = 0f;
        }

        private void BuildMesh(float range, float halfAngle)
        {
            var mesh = new Mesh { name = "GroundConeIndicator" };

            float innerRadius = Mathf.Min(range * 0.9f, Mathf.Max(minInnerRadius, range * innerRadiusFraction));

            int ringVerts = segments + 1;
            var vertices = new Vector3[ringVerts * 2];
            var triangles = new int[segments * 6];

            float startAngle = -halfAngle;
            float angleStep = (halfAngle * 2f) / segments;
            for (int i = 0; i < ringVerts; i++)
            {
                float angle = (startAngle + angleStep * i) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                vertices[i * 2] = dir * innerRadius + new Vector3(0f, yOffset, 0f);
                vertices[i * 2 + 1] = dir * range + new Vector3(0f, yOffset, 0f);
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

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        private void SetAlpha(float alpha01)
        {
            if (rend == null) return;
            Color c = baseColor;
            c.a = baseColor.a * alpha01;
            rend.material.SetColor("_BaseColor", c);
            rend.material.SetColor("_EmissionColor", baseEmission * alpha01);
        }

        private void Update()
        {
            if (phase == Phase.Done) return;
            elapsed += Time.deltaTime;

            switch (phase)
            {
                case Phase.FadeIn:
                    SetAlpha(fadeInDuration > 0f ? Mathf.Clamp01(elapsed / fadeInDuration) : 1f);
                    if (elapsed >= fadeInDuration) { phase = Phase.Hold; elapsed = 0f; }
                    break;
                case Phase.Hold:
                    if (elapsed >= holdDuration) { phase = Phase.FadeOut; elapsed = 0f; }
                    break;
                case Phase.FadeOut:
                    SetAlpha(fadeOutDuration > 0f ? 1f - Mathf.Clamp01(elapsed / fadeOutDuration) : 0f);
                    if (elapsed >= fadeOutDuration) { phase = Phase.Done; Destroy(gameObject); }
                    break;
            }
        }
    }
}
