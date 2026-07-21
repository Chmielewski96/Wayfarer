using UnityEngine;
using Wayfarer.Player;

namespace Wayfarer.Spells
{
    /// <summary>
    /// Pickup dropped by ice/water spells (Ice Bolt on hit, Hydrocutter later, Frost Cone via
    /// Shatter). Springs out of the source position (typically the target enemy) on a slow, big
    /// arc, lands a short random distance away, then hovers above the ground with a slow
    /// meandering drift and liquid pulse until picked up (refills the player's Water resource on
    /// contact) or its lifetime expires.
    ///
    /// The visible mesh lives on a child ("Visual") that gets squashed/pulsed for the liquid
    /// look; this root object's scale stays fixed at (1,1,1) so the pickup trigger radius
    /// (sized generously so it's easy to walk into) never gets distorted by that animation.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class WaterBlob : MonoBehaviour
    {
        [SerializeField] private float waterAmount = 5f;
        [SerializeField] private float lifetime = 20f;
        [SerializeField] private float pickupRadius = 1.1f;

        [Header("Pop / Landing")]
        [SerializeField] private float popRadiusMin = 1.2f;
        [SerializeField] private float popRadiusMax = 2.8f;
        [SerializeField] private float popHeight = 2.6f;
        [SerializeField] private float popDuration = 1.3f;
        [SerializeField] private float groundRaycastDistance = 20f;

        [Header("Flowing Idle")]
        [SerializeField] private float hoverHeight = 0.6f;
        [SerializeField] private float flowPulseSpeed = 4f;
        [SerializeField] private float flowPulseAmount = 0.18f;
        [SerializeField] private float flowDriftRadius = 0.35f;
        [SerializeField] private float flowDriftSpeed = 0.8f;

        private Transform visual;
        private Vector3 visualBaseScale;
        private Vector3 startPos;
        private Vector3 landPos;
        private float elapsed;
        private bool landed;
        private float wobbleSeed;

        private void Awake()
        {
            var col = GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = pickupRadius;

            visual = transform.Find("Visual");
        }

        private void Start()
        {
            Destroy(gameObject, lifetime);

            visualBaseScale = visual != null ? visual.localScale : Vector3.one;
            startPos = transform.position;
            landPos = ComputeLandPosition(startPos);
            wobbleSeed = Random.Range(0f, 100f);
        }

        private Vector3 ComputeLandPosition(Vector3 origin)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(popRadiusMin, popRadiusMax);
            Vector3 horizontalOffset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
            Vector3 candidate = origin + horizontalOffset;

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            int mask = enemyLayer >= 0 ? ~(1 << enemyLayer) : ~0;

            Vector3 rayStart = candidate + Vector3.up * 8f;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundRaycastDistance, mask, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            return candidate;
        }

        private void Update()
        {
            if (!landed)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / popDuration);
                float eased = Mathf.SmoothStep(0f, 1f, t);

                Vector3 flatPos = Vector3.Lerp(startPos, landPos, eased);
                float arc = Mathf.Sin(t * Mathf.PI) * popHeight;
                float groundY = landPos.y + hoverHeight;
                transform.position = new Vector3(flatPos.x, Mathf.Lerp(startPos.y, groundY, eased) + arc, flatPos.z);

                if (visual != null)
                {
                    float squash = 1f - 0.25f * Mathf.Sin(t * Mathf.PI);
                    visual.localScale = new Vector3(visualBaseScale.x * (2f - squash), visualBaseScale.y * squash, visualBaseScale.z * (2f - squash));
                }

                if (t >= 1f)
                {
                    landed = true;
                }
            }
            else
            {
                float time = Time.time + wobbleSeed;

                Vector3 drift = new Vector3(
                    Mathf.Sin(time * flowDriftSpeed) * flowDriftRadius,
                    0f,
                    Mathf.Cos(time * flowDriftSpeed * 0.7f) * flowDriftRadius);

                transform.position = new Vector3(landPos.x + drift.x, landPos.y + hoverHeight, landPos.z + drift.z);

                if (visual != null)
                {
                    float pulse = 1f + Mathf.Sin(time * flowPulseSpeed) * flowPulseAmount;
                    float inversePulse = 1f - (pulse - 1f);
                    visual.localScale = new Vector3(visualBaseScale.x * pulse, visualBaseScale.y * inversePulse, visualBaseScale.z * pulse);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var water = other.GetComponentInParent<WaterResource>();
            if (water == null) return;

            water.Add(waterAmount);
            Destroy(gameObject);
        }

        public static WaterBlob Spawn(GameObject prefab, Vector3 position, float waterAmount)
        {
            var instance = Instantiate(prefab, position, Quaternion.identity);
            var blob = instance.GetComponent<WaterBlob>();
            if (blob != null)
            {
                blob.waterAmount = waterAmount;
            }
            return blob;
        }
    }
}
