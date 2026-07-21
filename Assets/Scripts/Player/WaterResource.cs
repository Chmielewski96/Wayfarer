using UnityEngine;

namespace Wayfarer.Player
{
    /// <summary>
    /// Sea mage resource pool. Large capacity, slow passive regen by design (per North Star
    /// spec) — meant to be topped up more by drinking water sources / collecting water blobs
    /// dropped by ice/water spells than by waiting it out.
    /// </summary>
    public class WaterResource : MonoBehaviour
    {
        [SerializeField] private float maxWater = 100f;
        [SerializeField] private float currentWater;
        [SerializeField] private float passiveRegenPerSecond = 1f;

        public float MaxWater => maxWater;
        public float CurrentWater => currentWater;
        public float NormalizedWater => maxWater > 0f ? currentWater / maxWater : 0f;

        private void Awake()
        {
            currentWater = maxWater;
        }

        private void Update()
        {
            if (currentWater < maxWater)
            {
                currentWater = Mathf.Min(maxWater, currentWater + passiveRegenPerSecond * Time.deltaTime);
            }
        }

        public bool HasEnough(float amount)
        {
            return currentWater >= amount;
        }

        public bool TryConsume(float amount)
        {
            if (!HasEnough(amount)) return false;
            currentWater -= amount;
            return true;
        }

        public void Add(float amount)
        {
            if (amount <= 0f) return;
            currentWater = Mathf.Min(maxWater, currentWater + amount);
        }
    }
}
