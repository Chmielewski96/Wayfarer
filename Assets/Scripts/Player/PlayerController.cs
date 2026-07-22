using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float speed = 5f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -9.8f;
    [SerializeField] private bool shouldFaceMoveDirection = false;
    [SerializeField] private Animator animator;
    [SerializeField] private float externalVelocityDecay = 8f;
    [SerializeField] private float groundedExternalVelocityDecay = 25f;
    [SerializeField] private Wayfarer.Player.PlayerSpellCaster spellCaster;

    private CharacterController controller;
    private Vector2 moveInput;
    private Vector3 velocity;
    private bool isAiming;
    private bool isSurfing;
    private Vector3 externalVelocity;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (isSurfing) return;

        if (context.performed && controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (animator != null)
            {
                animator.SetTrigger("Jump");
            }
        }
    }

    public void SetAiming(bool aiming)
    {
        isAiming = aiming;
    }

    public bool IsAiming => isAiming;

    public bool IsSurfing => isSurfing;

    // Reads live input state (not the animator's Speed, which lags a frame behind input
    // callbacks) so cast-time decisions like "should this play as a full-body animation"
    // reflect what the player is doing right now.
    public bool IsMoving => moveInput.sqrMagnitude > 0.01f;

    public void SetSurfing(bool surfing)
    {
        isSurfing = surfing;
        if (animator != null)
        {
            animator.SetBool("IsSurfing", surfing);
        }

        // Standing in a "goalkeeper ready" pose only makes sense while grounded and not surfing;
        // clear the spell selection so the animator falls back to the default idle.
        if (surfing && spellCaster != null)
        {
            spellCaster.Deselect();
        }

        // externalVelocity's contribution is already folded into controller.velocity, which
        // IceSurfController reads to seed its own horizontalVelocity the instant surfing starts.
        // If we don't clear it here, it just sits frozen (Update() below returns early while
        // surfing, so it never decays) and gets added to AGAIN on the next AddExternalVelocity
        // call when surf turns back off - rapidly toggling surf on/off compounds this every
        // cycle and produces runaway speed.
        if (surfing)
        {
            externalVelocity = Vector3.zero;
        }
    }

    // Called when surfing hands movement control back so momentum carries into normal
    // movement instead of snapping to zero. Decays at externalVelocityDecay while airborne
    // (near-ballistic, since there's no surface to scrub speed against), and much faster at
    // groundedExternalVelocityDecay the moment the character is actually touching the ground
    // (reads as real ground friction biting once you're no longer sliding on ice).
    public void AddExternalVelocity(Vector3 addedVelocity)
    {
        externalVelocity += addedVelocity;
    }

    // Update is called once per frame
    void Update()
    {
        if (isSurfing) return;

        if (controller.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0;
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;

        velocity.y += gravity * Time.deltaTime;

        // Everything that moves the character this frame - input-driven movement, leftover surf
        // momentum, and gravity - is combined into a single Move() call. CharacterController's
        // isGrounded/velocity are only ever accurate for the most recent Move() call in a frame,
        // so splitting this across multiple Move() calls (as it used to be) made both of those
        // reads unreliable for anything checking them later in the frame or from another script
        // (e.g. IceSurfController re-seeding momentum from controller.velocity on activation).
        Vector3 totalVelocity = (moveDirection * speed) + externalVelocity + new Vector3(0f, velocity.y, 0f);
        controller.Move(totalVelocity * Time.deltaTime);

        if (externalVelocity.sqrMagnitude > 0.0001f)
        {
            float decay = controller.isGrounded ? groundedExternalVelocityDecay : externalVelocityDecay;
            externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, decay * Time.deltaTime);
        }

        if (isAiming)
        {
            Quaternion aimRotation = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, aimRotation, 20f * Time.deltaTime);
        }
        else if (shouldFaceMoveDirection && moveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion toRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, toRotation, 10f * Time.deltaTime);
        }

        if (animator != null)
        {
            animator.SetFloat("Speed", moveDirection.magnitude);
            animator.SetBool("IsGrounded", controller.isGrounded);
            animator.SetBool("IsAiming", isAiming);
            animator.SetFloat("MoveX", moveInput.x);
            animator.SetInteger("SelectedSpellSlot", spellCaster != null ? spellCaster.SelectedSlotIndex : -1);
        }
    }
}
