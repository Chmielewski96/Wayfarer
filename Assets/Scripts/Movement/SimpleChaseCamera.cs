using UnityEngine;

namespace Wayfarer.Movement
{
    /// <summary>Minimal follow camera for the Ice Surfing prototype scene - trails behind the
    /// target's facing direction. Not meant to replace the real Cinemachine rig in the main
    /// scene, just enough to see where you're going while tuning surf feel.</summary>
    public class SimpleChaseCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 4f, -8f);
        [SerializeField] private float positionSmoothTime = 0.15f;
        [SerializeField] private float lookHeight = 1.5f;

        private Vector3 velocity;

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPos = target.position + target.TransformDirection(offset);
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref velocity, positionSmoothTime);
            transform.LookAt(target.position + Vector3.up * lookHeight);
        }
    }
}
