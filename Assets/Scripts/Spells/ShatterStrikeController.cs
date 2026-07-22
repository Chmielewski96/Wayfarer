using System.Collections;
using UnityEngine;
using Wayfarer.Combat;

namespace Wayfarer.Spells
{
    /// <summary>
    /// Standalone runtime controller for a single Shatter cast. Shows a purple telegraph
    /// circle on the ground that fades in over telegraphDuration seconds, then strikes: the
    /// beam of light-purple light from the sky flickers on/off a few times (like a lightning
    /// bolt actually striking) rather than staying lit, while a purple particle burst out of
    /// the ground keeps playing for its own full lifetime regardless of the beam. Damage (bonus
    /// to frozen targets, which also get their freeze consumed and drop water blobs) resolves
    /// the instant the strike happens - moved here from the old instant-cast version of the
    /// spell so the strike is delayed and telegraphed instead of resolving immediately.
    /// </summary>
    public class ShatterStrikeController : MonoBehaviour
    {
        [Header("Telegraph")]
        [SerializeField] private Transform telegraphCircle;

        [Header("Strike Visual")]
        [SerializeField] private GameObject lightBeam;
        [SerializeField] private ParticleSystem groundBurst;
        [SerializeField] private Light strikeFlashLight;
        [Tooltip("How many times the beam+light flash on strike.")]
        [SerializeField] private int beamFlashCount = 3;
        [Tooltip("How long each flash stays lit.")]
        [SerializeField] private float beamFlashOnDuration = 0.05f;
        [Tooltip("How long the gap between flashes is.")]
        [SerializeField] private float beamFlashOffDuration = 0.06f;
        [SerializeField] private float visualCleanupDelay = 2.7f;

        private float telegraphDuration;
        private float range;
        private float baseDamage;
        private float bonusDamageToFrozen;
        private float waterBlobAmountPerFrozenTarget;
        private int blobsPerFrozenTarget;
        private LayerMask targetMask;
        private GameObject waterBlobPrefab;

        private Renderer telegraphRenderer;
        private Color telegraphBaseColor;
        private Color telegraphBaseEmission;

        public void Initialize(float telegraphDuration, float range, float baseDamage, float bonusDamageToFrozen,
            float waterBlobAmountPerFrozenTarget, int blobsPerFrozenTarget,
            LayerMask targetMask, GameObject waterBlobPrefab)
        {
            this.telegraphDuration = telegraphDuration;
            this.range = range;
            this.baseDamage = baseDamage;
            this.bonusDamageToFrozen = bonusDamageToFrozen;
            this.waterBlobAmountPerFrozenTarget = waterBlobAmountPerFrozenTarget;
            this.blobsPerFrozenTarget = blobsPerFrozenTarget;
            this.targetMask = targetMask;
            this.waterBlobPrefab = waterBlobPrefab;

            if (telegraphCircle != null)
            {
                float diameter = range * 2f;
                telegraphCircle.localScale = new Vector3(diameter, telegraphCircle.localScale.y, diameter);

                telegraphRenderer = telegraphCircle.GetComponent<Renderer>();
                if (telegraphRenderer != null)
                {
                    // Instance the material so fading this circle doesn't affect every other
                    // Shatter cast sharing the same source asset.
                    telegraphRenderer.material = new Material(telegraphRenderer.sharedMaterial);
                    telegraphBaseColor = telegraphRenderer.material.GetColor("_BaseColor");
                    // Emission is unlit self-glow and does NOT get dimmed by the base color's
                    // alpha automatically - fade it alongside alpha or the circle's glow pops in
                    // at full brightness immediately.
                    telegraphBaseEmission = telegraphRenderer.material.GetColor("_EmissionColor");
                    SetTelegraphAlpha(0f);
                }
            }

            if (lightBeam != null) { lightBeam.SetActive(false); }
            if (strikeFlashLight != null) { strikeFlashLight.enabled = false; }

            StartCoroutine(RunSequence());
        }

        private void SetTelegraphAlpha(float alpha01)
        {
            if (telegraphRenderer == null) return;
            Color c = telegraphBaseColor;
            c.a = telegraphBaseColor.a * alpha01;
            telegraphRenderer.material.SetColor("_BaseColor", c);
            telegraphRenderer.material.SetColor("_EmissionColor", telegraphBaseEmission * alpha01);
        }

        private IEnumerator RunSequence()
        {
            float t = 0f;
            while (t < telegraphDuration)
            {
                t += Time.deltaTime;
                SetTelegraphAlpha(Mathf.Clamp01(t / telegraphDuration));
                yield return null;
            }

            Strike();

            yield return new WaitForSeconds(visualCleanupDelay);
            Destroy(gameObject);
        }

        private void Strike()
        {
            if (telegraphCircle != null) { telegraphCircle.gameObject.SetActive(false); }
            if (groundBurst != null) { groundBurst.Play(); }

            StartCoroutine(FlickerBeam());

            Collider[] hits = Physics.OverlapSphere(transform.position, range, targetMask);
            foreach (var hit in hits)
            {
                var health = hit.GetComponentInParent<Health>();
                var freezable = hit.GetComponentInParent<Freezable>();
                bool wasFrozen = freezable != null && freezable.IsFrozen;

                float damage = baseDamage + (wasFrozen ? bonusDamageToFrozen : 0f);
                if (health != null)
                {
                    health.TakeDamage(damage);
                }

                if (wasFrozen)
                {
                    freezable.ConsumeFreeze();

                    if (waterBlobPrefab != null)
                    {
                        float perBlobAmount = waterBlobAmountPerFrozenTarget / Mathf.Max(1, blobsPerFrozenTarget);
                        for (int i = 0; i < blobsPerFrozenTarget; i++)
                        {
                            WaterBlob.Spawn(waterBlobPrefab, hit.bounds.center, perBlobAmount);
                        }
                    }
                }
            }
        }

        // Flashes the beam and its light on/off a few times in quick succession, like an actual
        // lightning strike, instead of leaving the beam lit for the whole strike-visual window -
        // the ground particle burst is untouched by this and keeps playing on its own timeline.
        private IEnumerator FlickerBeam()
        {
            for (int i = 0; i < beamFlashCount; i++)
            {
                if (lightBeam != null) { lightBeam.SetActive(true); }
                if (strikeFlashLight != null) { strikeFlashLight.enabled = true; }

                yield return new WaitForSeconds(beamFlashOnDuration);

                if (lightBeam != null) { lightBeam.SetActive(false); }
                if (strikeFlashLight != null) { strikeFlashLight.enabled = false; }

                if (i < beamFlashCount - 1)
                {
                    yield return new WaitForSeconds(beamFlashOffDuration);
                }
            }
        }
    }
}
