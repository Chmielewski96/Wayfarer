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

    private bool isAimHeld;
    private bool npcInteractionActive;

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
        // Aim input shouldn't do anything while a conversation is open - the camera is
        // already locked onto the NPC, and showing the aim reticle over someone you're
        // talking to (rather than aiming a spell at) looks wrong. Right-clicking during
        // dialogue is a plausible habitual click, not an actual aim attempt.
        if (npcInteractionActive) return;

        isAimHeld = true;

        if (aimCameraController != null && Camera.main != null)
        {
            aimCameraController.SyncToCurrentOrientation(Camera.main.transform.eulerAngles);
        }

        UpdateAimCameraPriority();

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
        isAimHeld = false;
        UpdateAimCameraPriority();

        if (aimReticle != null)
        {
            aimReticle.SetActive(false);
        }

        if (playerController != null)
        {
            playerController.SetAiming(false);
        }
    }

    // Called by NPCInteractable when a conversation starts/ends. Reuses the same
    // over-the-shoulder aim camera a spell-aim uses (it already frames the character well for
    // a face-to-face moment) without touching the aim reticle or PlayerController.IsAiming -
    // those are specific to actual spell-aiming input and shouldn't turn on just because a
    // dialogue box is open. isAimHeld and npcInteractionActive are OR'd together rather than
    // each independently setting the priority, so the aim camera doesn't get incorrectly
    // dropped back to resting if the player happens to still be holding the aim button (or
    // starts aiming) while a conversation is active.
    public void SetNpcInteractionActive(bool active, Vector3? lookAtPosition = null)
    {
        npcInteractionActive = active;

        // Covers the edge case where the player was already holding aim right as the
        // conversation started - without this, the reticle would stay stuck on screen for
        // the whole conversation since OnAimStarted (the only other place that hides it)
        // never fires again while the button stays held.
        if (active && isAimHeld)
        {
            isAimHeld = false;

            if (aimReticle != null)
            {
                aimReticle.SetActive(false);
            }

            if (playerController != null)
            {
                playerController.SetAiming(false);
            }
        }

        if (active && aimCameraController != null)
        {
            if (lookAtPosition.HasValue)
            {
                // "Locks on" - points the camera at the NPC and stops it from drifting off
                // with further mouse/gamepad look input for the rest of the conversation,
                // rather than just setting a one-time starting direction.
                aimCameraController.LockOnto(lookAtPosition.Value);
            }
            else if (Camera.main != null)
            {
                aimCameraController.SyncToCurrentOrientation(Camera.main.transform.eulerAngles);
            }
        }
        else if (!active && aimCameraController != null)
        {
            aimCameraController.Unlock();
        }

        UpdateAimCameraPriority();
    }

    private void UpdateAimCameraPriority()
    {
        aimCamera.Priority = (isAimHeld || npcInteractionActive) ? aimActivePriority : aimRestingPriority;
    }
}
