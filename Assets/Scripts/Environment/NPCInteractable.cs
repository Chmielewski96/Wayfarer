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

    [Header("Quest (placeholder)")]
    [Tooltip("When on, this NPC ignores dialogueLines above and instead runs the placeholder fetch-quest flow below (built on the existing seashell collection system as a stand-in for a real quest/inventory system).")]
    [SerializeField] private bool enableQuest = true;
    [SerializeField] private int seashellQuestTarget = 1;
    [Tooltip("Stable id this quest is tracked under in QuestManager/the Journal - keep unique per quest-giver.")]
    [SerializeField] private string questId = "villager_seashell";
    [SerializeField] private string questTitle = "A Lost Seashell";

    private enum QuestState { NotOffered, Accepted, Completed }
    [Tooltip("Persists only for this play session (not saved) - reset to NotOffered when you re-enter Play mode.")]
    [SerializeField] private QuestState questState = QuestState.NotOffered;

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
            && DialogueUI.Instance != null && !DialogueUI.Instance.IsOpen
            && (JournalUI.Instance == null || !JournalUI.Instance.IsOpen))
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

        BeginConversationContent();
    }

    // Picks which conversation to open based on quest state. Kept separate from the
    // interruption/camera/facing setup above so that logic (which every NPC needs) doesn't
    // get tangled up with this NPC's specific placeholder narrative content.
    private void BeginConversationContent()
    {
        if (!enableQuest)
        {
            DialogueUI.Instance.Open(npcName, dialogueLines, OnDialogueClosed);
            return;
        }

        switch (questState)
        {
            case QuestState.Accepted:
                OpenQuestCheckIn();
                break;
            case QuestState.Completed:
                OpenQuestCompletedFlavor();
                break;
            case QuestState.NotOffered:
            default:
                OpenQuestOffer();
                break;
        }
    }

    // Placeholder quest, step 1: offer it. "I'll help" hands the player off to the existing
    // seashell-collection system (SeashellManager/SeashellCollectible) as the quest's actual
    // objective, rather than inventing a separate item/inventory system just for this.
    private void OpenQuestOffer()
    {
        string[] intro =
        {
            "Oh - a traveler! Haven't seen a new face around here in some time.",
            "Actually... you look like someone who doesn't mind getting their feet wet.",
            "I lost a seashell somewhere along the shore. Would you bring it back to me?"
        };

        var choices = new DialogueUI.DialogueChoice[]
        {
            new DialogueUI.DialogueChoice
            {
                Label = "I'll find it for you.",
                OnChosen = () =>
                {
                    questState = QuestState.Accepted;
                    ReportQuestProgress();
                    DialogueUI.Instance.ShowLines(new[]
                    {
                        "Wonderful! Look for it along the shoreline - you can't miss the sparkle."
                    });
                }
            },
            new DialogueUI.DialogueChoice
            {
                Label = "Not right now.",
                OnChosen = () =>
                {
                    DialogueUI.Instance.ShowLines(new[]
                    {
                        "No trouble at all. Come find me if you change your mind."
                    });
                }
            }
        };

        DialogueUI.Instance.Open(npcName, intro, choices, OnDialogueClosed);
    }

    // Placeholder quest, step 2: check progress. SeashellManager.TotalCollected is a running,
    // scene-wide total (not specific to this quest), which is a fine stand-in for now since
    // there's only ever one active fetch-quest in this placeholder.
    private void OpenQuestCheckIn()
    {
        int collected = SeashellManager.Instance != null ? SeashellManager.Instance.TotalCollected : 0;

        if (collected >= seashellQuestTarget)
        {
            string[] lines = { "Is that the seashell? You actually found it!" };

            var choices = new DialogueUI.DialogueChoice[]
            {
                new DialogueUI.DialogueChoice
                {
                    Label = "Here you go.",
                    OnChosen = () =>
                    {
                        questState = QuestState.Completed;
                        ReportQuestProgress();
                        DialogueUI.Instance.ShowLines(new[]
                        {
                            "Thank you, truly. I won't forget this."
                        });
                    }
                },
                new DialogueUI.DialogueChoice
                {
                    Label = "Not yet, one moment.",
                    OnChosen = () =>
                    {
                        DialogueUI.Instance.ShowLines(new[] { "Take your time." });
                    }
                }
            };

            DialogueUI.Instance.Open(npcName, lines, choices, OnDialogueClosed);
        }
        else
        {
            string[] lines = { "Any luck finding that seashell along the shore?" };
            DialogueUI.Instance.Open(npcName, lines, OnDialogueClosed);
        }
    }

    // Pushes the quest's current state/description into QuestManager so the Journal (opened
    // with J) reflects it. Called whenever questState changes, not on every conversation open,
    // so the journal entry only updates at real milestones rather than re-writing itself with
    // identical text each check-in.
    private void ReportQuestProgress()
    {
        if (QuestManager.Instance == null) return;

        string description;
        var status = QuestManager.Status.InProgress;

        switch (questState)
        {
            case QuestState.Accepted:
                description = "Bring the seashell " + npcName + " lost along the shore back to them.";
                break;
            case QuestState.Completed:
                description = "Returned the lost seashell to " + npcName + ". Quest complete.";
                status = QuestManager.Status.Completed;
                break;
            default:
                return;
        }

        QuestManager.Instance.AddOrUpdateQuest(questId, questTitle, description, status);
    }

    // Placeholder quest, step 3: repeatable flavor line once turned in.
    private void OpenQuestCompletedFlavor()
    {
        string[] lines = { "Thanks again for finding that seashell.", "Safe travels out there." };
        DialogueUI.Instance.Open(npcName, lines, OnDialogueClosed);
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
