using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraSwitcher : MonoBehaviour
{
    [SerializeField] private CinemachineCamera exploreCamera;
    [SerializeField] private CinemachineCamera aimCamera;
    [SerializeField] private AimCameraController aimCameraController;
    [SerializeField] private PlayerController playerController;

    [SerializeField] private InputActionReference aimInput;
    [SerializeField] private GameObject aimReticle;

    [SerializeField] private int explorePriority = 10;
    [SerializeField] private int aimRestingPriority = 0;
    [SerializeField] private int aimActivePriority = 20;

    private void Awake()
    {
        exploreCamera.Priority = explorePriority;
        aimCamera.Priority = aimRestingPriority;
    }

    private void OnEnable()
    {
        aimInput.action.Enable();
        aimInput.action.started += OnAimStarted;
        aimInput.action.canceled += OnAimCanceled;
    }

    private void OnDisable()
    {
        aimInput.action.started -= OnAimStarted;
        aimInput.action.canceled -= OnAimCanceled;
        aimInput.action.Disable();
    }

    private void OnAimStarted(InputAction.CallbackContext context)
    {
        if (aimCameraController != null && Camera.main != null)
        {
            aimCameraController.SyncToCurrentOrientation(Camera.main.transform.eulerAngles);
        }

        aimCamera.Priority = aimActivePriority;

        if (aimReticle != null)
        {
            aimReticle.SetActive(true);
        }

        if (playerController != null)
        {
            playerController.SetAiming(true);
        }
    }

    private void OnAimCanceled(InputAction.CallbackContext context)
    {
        aimCamera.Priority = aimRestingPriority;

        if (aimReticle != null)
        {
            aimReticle.SetActive(false);
        }

        if (playerController != null)
        {
            playerController.SetAiming(false);
        }
    }
}
