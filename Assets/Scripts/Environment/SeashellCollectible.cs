using UnityEngine;
using UnityEngine.InputSystem;

// A floating collectible the player can pick up with an interact key press
// while in range. Meant to be hand-placed around the exploration map. Uses
// direct Keyboard polling for the interact key rather than the formal
// PlayerControls input asset, matching how IceSurfController handles its
// one-off Q boost key - keeps this self-contained without touching shared
// input action maps.
public class SeashellCollectible : MonoBehaviour
{
    [Header("Float Animation")]
    [SerializeField] private float bobHeight = 0.15f;
    [SerializeField] private float bobSpeed = 1.2f;
    [SerializeField] private float rotateSpeed = 40f;

    [Header("Interaction")]
    [SerializeField] private float interactRange = 2.5f;
    [SerializeField] private Key interactKey = Key.F;

    [Header("In-Range Feedback")]
    [SerializeField] private float inRangePulseSpeed = 4f;
    [SerializeField] private float inRangePulseAmount = 0.08f;
    [SerializeField] private GameObject promptRoot;

    [Header("Pickup")]
    [SerializeField] private GameObject pickupVfxPrefab;

    private Transform player;
    private Vector3 baseLocalPosition;
    private Vector3 baseScale;
    private bool collected;
    private bool playerInRange;

    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
        baseScale = transform.localScale;

        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null)
        {
            player = pc.transform;
        }

        if (promptRoot != null)
        {
            promptRoot.SetActive(false);
        }
    }

    private void Update()
    {
        if (collected)
        {
            return;
        }

        float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.localPosition = baseLocalPosition + new Vector3(0f, bobOffset, 0f);
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);

        if (player == null)
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

        if (playerInRange)
        {
            float pulse = 1f + Mathf.Sin(Time.time * inRangePulseSpeed) * inRangePulseAmount;
            transform.localScale = baseScale * pulse;

            if (Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame)
            {
                Collect();
            }
        }
        else
        {
            transform.localScale = baseScale;
        }
    }

    private void Collect()
    {
        if (collected)
        {
            return;
        }

        collected = true;

        if (promptRoot != null)
        {
            promptRoot.SetActive(false);
        }

        if (pickupVfxPrefab != null)
        {
            GameObject vfx = Instantiate(pickupVfxPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, 3f);
        }

        if (SeashellManager.Instance != null)
        {
            SeashellManager.Instance.CollectShell(this);
        }
        else
        {
            Debug.LogWarning("[SeashellCollectible] No SeashellManager in scene - collection not tracked.");
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.6f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
