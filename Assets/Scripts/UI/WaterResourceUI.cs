using UnityEngine;
using UnityEngine.UI;
using Wayfarer.Player;

namespace Wayfarer.UI
{
    public class WaterResourceUI : MonoBehaviour
    {
        [SerializeField] private WaterResource waterResource;
        [SerializeField] private Slider slider;

        private void Update()
        {
            if (waterResource == null || slider == null) return;
            slider.value = waterResource.NormalizedWater;
        }
    }
}
