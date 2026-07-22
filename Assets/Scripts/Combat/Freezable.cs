using UnityEngine;

namespace Wayfarer.Combat
{
    /// <summary>
    /// Tracks frozen status for the Frost Cone -> Shatter combo. Frost Cone calls Freeze();
    /// Shatter checks IsFrozen to decide bonus damage and whether this target counts toward
    /// the water-blob payout. While frozen, all renderers on this object (and its children)
    /// swap to iceMaterial, reverting to their original materials the moment the freeze ends
    /// (naturally expiring or broken early via ConsumeFreeze/Shatter).
    /// </summary>
    public class Freezable : MonoBehaviour
    {
        [SerializeField] private float freezeDuration = 3f;
        [SerializeField] private Material iceMaterial;

        private float frozenUntil = -1f;
        private bool wasFrozen;

        private Renderer[] renderers;
        private Material[][] originalMaterials;

        public bool IsFrozen => Time.time < frozenUntil;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            originalMaterials = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                originalMaterials[i] = renderers[i].sharedMaterials;
            }
        }

        private void Update()
        {
            bool frozenNow = IsFrozen;
            if (frozenNow != wasFrozen)
            {
                if (frozenNow) { ApplyIceMaterial(); } else { RestoreOriginalMaterials(); }
                wasFrozen = frozenNow;
            }
        }

        public void Freeze()
        {
            Freeze(freezeDuration);
        }

        public void Freeze(float duration)
        {
            frozenUntil = Time.time + duration;
        }

        /// <summary>Consumes the frozen state (used by Shatter, which breaks the freeze on hit).</summary>
        public void ConsumeFreeze()
        {
            frozenUntil = -1f;
        }

        private void ApplyIceMaterial()
        {
            if (iceMaterial == null || renderers == null) return;

            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = iceMaterial;
                }
                r.materials = mats;
            }
        }

        private void RestoreOriginalMaterials()
        {
            if (renderers == null || originalMaterials == null) return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].materials = originalMaterials[i];
            }
        }
    }
}
