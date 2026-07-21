using UnityEngine;

namespace Wayfarer.Player
{
    // Keeps a spell-cast anchor point positioned on the player's body (chest height,
    // slightly forward) while orienting it to match the camera's full aim direction
    // (yaw + pitch), so spell effects originate from the character but point exactly
    // where the player is looking, instead of spawning at the camera itself.
    public class SpellOriginFollow : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float heightOffset = 1.4f;
        [SerializeField] private float forwardOffset = 0.5f;

        private void LateUpdate()
        {
            if (player == null) return;

            Vector3 aimForward = cameraTransform != null ? cameraTransform.forward : player.forward;
            transform.position = player.position + Vector3.up * heightOffset + aimForward * forwardOffset;
            transform.rotation = Quaternion.LookRotation(aimForward, Vector3.up);
        }
    }
}
