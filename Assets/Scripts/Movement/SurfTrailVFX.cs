using UnityEngine;

namespace Wayfarer.Movement
{
    /// <summary>
    /// Toggles a foot-level particle trail on/off with surf state, so surfing reads clearly
    /// even from a distance. Emission intensity scales with current surf speed - matches the
    /// design sketch's "trail VFX intensifies" note for high-speed surfing.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class SurfTrailVFX : MonoBehaviour
    {
        [SerializeField] private IceSurfController surfController;
        [SerializeField] private float minEmissionRate = 15f;
        [SerializeField] private float maxEmissionRate = 70f;

        private ParticleSystem ps;
        private ParticleSystem.EmissionModule emission;
        private bool wasSurfing;

        private void Awake()
        {
            ps = GetComponent<ParticleSystem>();
            emission = ps.emission;
        }

        private void Update()
        {
            if (surfController == null) return;

            bool isSurfing = surfController.IsSurfing;
            if (isSurfing != wasSurfing)
            {
                if (isSurfing)
                {
                    ps.Play();
                }
                else
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
                wasSurfing = isSurfing;
            }

            if (isSurfing)
            {
                float speedRatio = surfController.SurfMaxSpeed > 0f
                    ? Mathf.Clamp01(surfController.CurrentSpeed / surfController.SurfMaxSpeed)
                    : 0f;
                emission.rateOverTime = Mathf.Lerp(minEmissionRate, maxEmissionRate, speedRatio);
            }
        }
    }
}
