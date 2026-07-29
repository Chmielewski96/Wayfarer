using UnityEngine;
using UnityEngine.UI;
using Wayfarer.Player;

namespace Wayfarer.UI
{
    /// <summary>
    /// Flashes a red frame around the water bar whenever something tries to spend more Water
    /// than is available (spell cast, surf activation, surf boost, drain hitting empty).
    /// Subscribes to WaterResource.InsufficientWater; each trigger (re)starts a short
    /// fade-in/out pulse sequence on the frame Image. The frame is fully transparent when idle.
    /// </summary>
    public class LowWaterFlashUI : MonoBehaviour
    {
        [SerializeField] private WaterResource waterResource;
        [SerializeField] private Image frameImage;

        [Header("Flash")]
        [Tooltip("Total length of one flash sequence, in seconds.")]
        [SerializeField] private float flashDuration = 0.8f;
        [Tooltip("How many full fade-in/out pulses fit in one flash sequence.")]
        [SerializeField] private int pulseCount = 2;
        [Tooltip("Peak opacity of the frame at the top of each pulse.")]
        [SerializeField] private float maxAlpha = 0.9f;

        private float flashStartTime = float.NegativeInfinity;

        private void Awake()
        {
            if (frameImage == null)
            {
                frameImage = GetComponent<Image>();
            }
            SetAlpha(0f);
        }

        private void OnEnable()
        {
            if (waterResource != null)
            {
                waterResource.InsufficientWater += OnInsufficientWater;
            }
        }

        private void OnDisable()
        {
            if (waterResource != null)
            {
                waterResource.InsufficientWater -= OnInsufficientWater;
            }
            SetAlpha(0f);
        }

        // Retriggering mid-flash restarts the sequence from the beginning, so mashing a
        // too-expensive ability keeps the frame visibly pulsing rather than queueing flashes.
        private void OnInsufficientWater()
        {
            flashStartTime = Time.unscaledTime;
        }

        private void Update()
        {
            float elapsed = Time.unscaledTime - flashStartTime;
            if (elapsed < 0f || elapsed >= flashDuration)
            {
                SetAlpha(0f);
                return;
            }

            // pulseCount full sine arches over flashDuration - alpha eases in and back out
            // each pulse and always lands on 0 at the end.
            float t = elapsed / flashDuration;
            float alpha = maxAlpha * Mathf.Abs(Mathf.Sin(Mathf.PI * pulseCount * t));
            SetAlpha(alpha);
        }

        private void SetAlpha(float a)
        {
            if (frameImage == null) return;
            Color c = frameImage.color;
            c.a = a;
            frameImage.color = c;
        }
    }
}
