using UnityEngine;

// Rotates this transform to always face the main camera. Used for world-space
// interact prompts (e.g. "F - Collect") that float above collectibles.
public class BillboardToCamera : MonoBehaviour
{
    private Transform cam;

    private void OnEnable()
    {
        if (Camera.main != null)
        {
            cam = Camera.main.transform;
        }
    }

    private void LateUpdate()
    {
        if (cam == null)
        {
            if (Camera.main == null)
            {
                return;
            }
            cam = Camera.main.transform;
        }

        transform.rotation = Quaternion.LookRotation(transform.position - cam.position, Vector3.up);
    }
}
