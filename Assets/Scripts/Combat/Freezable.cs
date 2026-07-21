using UnityEngine;

namespace Wayfarer.Combat
{
    /// <summary>
    /// Tracks frozen status for the Frost Cone -> Shatter combo. Frost Cone calls Freeze();
    /// Shatter checks IsFrozen to decide bonus damage and whether this target counts toward
    /// the water-blob payout.
    /// </summary>
    public class Freezable : MonoBehaviour
    {
        [SerializeField] private float freezeDuration = 3f;

        private float frozenUntil = -1f;

        public bool IsFrozen => Time.time < frozenUntil;

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
    }
}
