using UnityEngine;
using UnityEngine.InputSystem;
using Wayfarer.Movement;
using Wayfarer.UI;

// A stationary NPC the player can talk to with an interact key press while in range - same
// proximity/prompt pattern as SeashellCollectible (same key, same "billboarded world-space
// text that fades in on approach" feel), but non-consuming: talking to the NPC doesn't destroy
// or disable it, so the conversation can be repeated.
public class NPCInteractable : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string npcName = "Villager";
    [TextArea(2, 4)]
    [SerializeField] private string[] dialogueLines = new string[]
    {
        "Hello, traveler.",
        "Not many folks come this way anymore.",
        "Safe travels, whatever you're looking for out here."
    };

    [Header("Interaction")]
    [SerializeField] private float interactRange = 3f;
    [SerializeField] private Key interactKey = Key.F;

    [Header("In-Range Feedback")]
    [SerializeField] private GameObject promptRoot;

    [Header("Camera")]
    [Tooltip("Optional - auto-found by child name 'CameraFocusPoint'. Where the locked-on dialogue camera aims instead of the NPC's own position - lets the shot be biased off-center (e.g. slightly left) instead of dead-centering the NPC's face. Only meaningful because the NPC always turns to face the player when a conversation starts (see StartDialogue), so an offset defined in the NPC's own local space maps to a consistent, predictable side of the screen every time. Falls back to an approximate head-height point on the NPC itself if not set.")]
    [SerializeField] private Transform cameraFocusPoint;

    private Transform player;
    private PlayerController playerController;
    private IceSurfController iceSurfController;
    private SwimController swimController;
    private CharacterController playerCharacterController;
    private CameraSwitcher cameraSwitcher;
    private Terrain terrain;
    private bool playerInRange;
    private bool talking;

    private void Awake()
    {
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null)
        {
            player = pc.transform;
            playerController = pc;
            // IceSurfController/SwimController run their own Update() loops independently of
            // PlayerController - they aren't gated by SetTalking at all, so left running
            // they'd keep moving the character underneath a "frozen" conversation (this is
            // what caused surfing+jumping mid-air into an NPC to keep sliding/falling through
            // the ground while the dialogue box was open). StartDialogue() force-stops
            // whichever of these is active before freezing movement.
            iceSurfController = pc.GetComponent<IceSurfController>();
            swimController = pc.GetComponent<SwimController>();
            playerCharacterController = pc.GetComponent<CharacterController>();
        }

        terrain = Terrain.activeTerrain;
        cameraSwitcher = FindFirstObjectByType<CameraSwitcher>();

        if (cameraFocusPoint == null)
        {
            Transform found = transform.Find("CameraFocusPoint");
            if (found != null) { cameraFocusPoint = found; }
        }

        if (promptRoot != null)
        {
            promptRoot.SetActive(false);
        }
    }

    private void Update()
    {
        if (talking || player == null)
        {
            return;
        }

        float sqrDist = (player.position - transform.position).sqrMagnitude;
        bool nowInRange = sqrDist <= interactRange * interactRange;

        if (nowInRange != playerInRange && promptRoot != null)
        {
            promptRoot.SetActive(nowInRange);
        }

        playerInRange = nowInRange;

        if (playerInRange
            && Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame
            && DialogueUI.Instance != null && !DialogueUI.Instance.IsOpen)
        {
            StartDialogue();
        }
    }

    private void StartDialogue()
    {
        talking = true;

        if (promptRoot != null)
        {
            promptRoot.SetActive(false);
        }

        // Stop whatever alternate movement the player is mid-action on BEFORE freezing
        // PlayerController, so control fully hands back rather than leaving surf/swim still
        // driving the character underneath the conversation. SetSurfing/ForceExitForInteraction
        // hand any leftover momentum to PlayerController, which SetTalking(true) then clears.
        if (iceSurfController != null && iceSurfController.IsSurfing)
        {
            iceSurfController.SetSurfing(false);
        }

        if (swimController != null && swimController.IsSwimming)
        {
            swimController.ForceExitForInteraction();
        }

        if (playerController != null)
        {
            playerController.SetTalking(true);
        }

        // Plant the character on solid ground regardless of what they were doing when they
        // pressed F (mid-jump, mid-surf-kickflip, etc.) - otherwise, even with movement
        // frozen, they'd stay suspended wherever they happened to be in the air for the whole
        // conversation instead of standing to talk.
        SnapPlayerToGround();

        // Both face each other for the duration of the conversation.
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
        }

        Vector3 toNpc = -toPlayer;
        if (toNpc.sqrMagnitude > 0.001f && player != null)
        {
            player.rotation = Quaternion.LookRotation(toNpc.normalized, Vector3.up);
        }

        if (cameraSwitcher != null)
        {
            Vector3 focusPoint = cameraFocusPoint != null ? cameraFocusPoint.position : transform.position + Vector3.up * 0.8f;
            cameraSwitcher.SetNpcInteractionActive(true, focusPoint);
        }

        DialogueUI.Instance.Open(npcName, dialogueLines, OnDialogueClosed);
    }

    // CharacterController.Move() is relative, so a direct position snap needs the controller
    // disabled for one frame first (same trick used elsewhere in this project for teleports) -
    // otherwise it fights the assignment and the character doesn't actually move.
    private void SnapPlayerToGround()
    {
        if (player == null || playerCharacterController == null || terrain == null) return;

        Vector3 pos = player.position;
        float groundY = terrain.SampleHeight(pos) + terrain.transform.position.y;

        playerCharacterController.enabled = false;
        player.position = new Vector3(pos.x, groundY, pos.z);
        playerCharacterController.enabled = true;
    }

    private void OnDialogueClosed()
    {
        talking = false;

        if (playerController != null)
        {
            playerController.SetTalking(false);
        }

        if (cameraSwitcher != null)
        {
            cameraSwitcher.SetNpcInteractionActive(false);
        }

        // Re-evaluate range immediately rather than waiting a frame, so the prompt reappears
        // right away if the player is still standing in range when the conversation ends.
        if (player != null && promptRoot != null)
        {
            playerInRange = (player.position - transform.position).sqrMagnitude <= interactRange * interactRange;
            promptRoot.SetActive(playerInRange);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.85f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
