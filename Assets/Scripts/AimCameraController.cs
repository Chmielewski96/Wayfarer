using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class AimCameraController : MonoBehaviour
{
    [SerializeField] private Transform yawTarget;
    [SerializeField] private Transform pitchTarget;

    [SerializeField] private InputActionReference lookInput;

    [SerializeField] private float mouseSensitivity = 0.05f;
    [SerializeField] private float gamepadSensitivity = 0.5f;
    [SerializeField] private float sensitivity = 1.5f;

    [SerializeField] private float pitchMin = -40f;
    [SerializeField] private float pitchMax = 80f;

    private CinemachineThirdPersonFollow aimCam;

    private float yaw;
    private float pitch;

    private void Awake()
    {
        aimCam = GetComponent<CinemachineThirdPersonFollow>();
    }

    public void SyncToCurrentOrientation(Vector3 eulerAngles)
    {
        yaw = eulerAngles.y;
        pitch = eulerAngles.x > 180f ? eulerAngles.x - 360f : eulerAngles.x;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        yawTarget.rotation = Quaternion.Euler(0f, yaw, 0f);
        pitchTarget.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Vector3 angles = yawTarget.rotation.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;

        lookInput.asset.Enable();
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 look = lookInput.action.ReadValue<Vector2>();

        if (Mouse.current != null && Mouse.current.delta.IsActuated())
        {
            look *= mouseSensitivity;
        }
        else if (Gamepad.current != null && Gamepad.current.rightStick.IsActuated())
        {
            look *= gamepadSensitivity;
        }

        yaw += look.x * sensitivity;
        pitch -= look.y * sensitivity;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        yawTarget.rotation = Quaternion.Euler(0f, yaw, 0f);
        pitchTarget.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
