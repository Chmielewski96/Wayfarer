using UnityEngine;

namespace Wayfarer.Spells
{
    /// <summary>
    /// Attached to the hand-conjure VFX spawned while casting a spell (Ice Bolt/Frost Cone -
    /// ice orbs; Shatter - purple lightning crackle plus a translucent "plasma ball" shell).
    /// Call BeginCast with however long the cast animation lasts; this stops new particles
    /// emitting right as the gesture ends (so the glow doesn't outlive the cast) and
    /// self-destructs once the already-emitted particles finish their own lifetime, instead of
    /// yanking the GameObject away mid-fade.
    ///
    /// The glow light either fades out smoothly over that same active window (default) or, if
    /// flickerLight is set, crackles via Perlin noise on top of the same fade envelope - used for
    /// the Shatter lightning variant so the light reads as electrical rather than a steady glow.
    /// The optional plasma sphere (a static mesh, not a particle) fades its alpha in step with
    /// the same envelope and gets a small breathing scale pulse so it reads as a living plasma
    /// shell rather than a flat translucent ball.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class HandCastVfx : MonoBehaviour
    {
        [SerializeField] private bool flickerLight = false;
        [SerializeField] private float flickerSpeed = 25f;

        [Header("Plasma Sphere (optional)")]
        [SerializeField] private MeshRenderer plasmaSphere;
        [SerializeField] private float plasmaPulseSpeed = 5f;
        [SerializeField] private float plasmaPulseAmount = 0.08f;

        private ParticleSystem ps;
        private Light glowLight;
        private float baseLightIntensity;
        private float fadeStartTime;
        private float fadeDuration;
        private bool fading;
        private float noiseSeed;

        private Material plasmaMaterialInstance;
        private Color plasmaBaseColor;
        private Vector3 plasmaBaseScale;

        private void Awake()
        {
            ps = GetComponent<ParticleSystem>();
            glowLight = GetComponentInChildren<Light>();
            if (glowLight != null)
            {
                baseLightIntensity = glowLight.intensity;
            }
            noiseSeed = Random.Range(0f, 100f);

            if (plasmaSphere != null)
            {
                // .material instantiates a per-object copy so fading alpha here doesn't affect
                // the shared asset or other simultaneously-active hand VFX instances.
                plasmaMaterialInstance = plasmaSphere.material;
                plasmaBaseColor = plasmaMaterialInstance.HasProperty("_BaseColor") ? plasmaMaterialInstance.GetColor("_BaseColor") : Color.white;
                plasmaBaseScale = plasmaSphere.transform.localScale;
            }
        }

        public void BeginCast(float duration)
        {
            CancelInvoke(nameof(StopEmitting));
            Invoke(nameof(StopEmitting), duration);

            // The light/plasma sphere fade across the whole active window (cast duration plus
            // the tail while already-emitted particles finish dying out), so they reach zero
            // right as the object is destroyed instead of cutting out abruptly.
            fadeStartTime = Time.time;
            fadeDuration = duration + ps.main.startLifetime.constantMax + 0.2f;
            fading = glowLight != null || plasmaMaterialInstance != null;
        }

        private void Update()
        {
            if (!fading) return;

            float t = fadeDuration > 0f ? Mathf.Clamp01((Time.time - fadeStartTime) / fadeDuration) : 1f;
            float envelope = 1f - t;

            if (glowLight != null)
            {
                if (flickerLight)
                {
                    float flicker = Mathf.PerlinNoise((Time.time + noiseSeed) * flickerSpeed, 0f);
                    glowLight.intensity = baseLightIntensity * envelope * Mathf.Lerp(0.35f, 1.15f, flicker);
                }
                else
                {
                    glowLight.intensity = baseLightIntensity * envelope;
                }
            }

            if (plasmaMaterialInstance != null)
            {
                float pulse = 1f + Mathf.Sin((Time.time + noiseSeed) * plasmaPulseSpeed) * plasmaPulseAmount;
                plasmaSphere.transform.localScale = plasmaBaseScale * pulse;

                Color c = plasmaBaseColor;
                c.a = plasmaBaseColor.a * envelope;
                plasmaMaterialInstance.SetColor("_BaseColor", c);
                if (plasmaMaterialInstance.HasProperty("_Color")) { plasmaMaterialInstance.SetColor("_Color", c); }
            }

            if (t >= 1f)
            {
                fading = false;
            }
        }

        private void StopEmitting()
        {
            if (ps == null) return;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(gameObject, ps.main.startLifetime.constantMax + 0.2f);
        }

        private void OnDestroy()
        {
            if (plasmaMaterialInstance != null)
            {
                Destroy(plasmaMaterialInstance);
            }
        }
    }
}
