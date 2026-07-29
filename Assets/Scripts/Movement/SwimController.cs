using UnityEngine;
using UnityEngine.InputSystem;

namespace Wayfarer.Movement
{
    /// <summary>
    /// Floating/swimming movement. Coexists with PlayerController the same way
    /// IceSurfController does: PlayerController owns all normal movement and hands off control
    /// here only while swimming is active. Unlike surf (toggled by an input action), swimming
    /// is triggered automatically by world position - falling/jumping below the water
    /// surface's height starts it, and it ends either by pressing Jump (propelling the
    /// character up and out, handing momentum back to PlayerController) or by walking into
    /// water shallow enough to reach shore.
    ///
    /// Water/land is determined by comparing actual terrain height at the character's XZ
    /// position against the water surface (see IsWaterAt), not by the character's own dynamic
    /// float height - using the animated float position for the shore-exit check used to cause
    /// entry and exit to disagree right at the shoreline (exiting at the float height, which
    /// was still below the water surface by the entry check's own threshold, immediately
    /// re-triggered entry the next frame), producing a rapid in/out flicker.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class SwimController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform waterSurface;
        [SerializeField] private Terrain terrain;
        [Tooltip("Optional - auto-found on the same GameObject. Entering water force-ends an active surf so the two modes never fight over movement.")]
        [SerializeField] private IceSurfController iceSurfController;
        [Tooltip("Optional - auto-found by child name 'SplashVFX'. One-shot burst played at the water surface whenever the character crosses it, entering or leaving.")]
        [SerializeField] private ParticleSystem splashVfx;

        [Header("Input")]
        [SerializeField] private InputActionReference moveInput;
        [SerializeField] private InputActionReference jumpInput;

        [Header("Floating")]
        [Tooltip("How far below the water surface the character's feet (transform origin) sit while actively swimming (moving).")]
        [SerializeField] private float floatDepth = 0.9f;
        [Tooltip("How far below the water surface the character's feet sit while treading water (stationary) - deeper than floatDepth so the idle pose's shoulders/neck stay submerged and only the head pokes out, instead of the shoulders breaking the surface.")]
        [SerializeField] private float treadFloatDepth = 1.6f;
        [Tooltip("How quickly the character eases toward the floating depth when entering water or bobbing back after being disturbed.")]
        [SerializeField] private float floatCorrectionSpeed = 6f;
        [Tooltip("Ground must be at least this far below the water surface to count as \"real water\" here - avoids flicker exactly at the shoreline.")]
        [SerializeField] private float minWaterDepth = 0.3f;

        [Header("Swim Movement")]
        [SerializeField] private float swimSpeed = 3f;
        [Tooltip("Input magnitude below this counts as \"stationary\" -> Treading Water instead of Swimming.")]
        [SerializeField] private float movingThreshold = 0.15f;
        [SerializeField] private float turnSpeedDegPerSec = 320f;

        [Header("Jump Out")]
        [Tooltip("Vertical launch speed on jump-out, applied as real gravity-driven velocity (same as a normal jump) rather than decaying external velocity, so it reliably clears the surface.")]
        [SerializeField] private float jumpOutSpeed = 7f;
        [Tooltip("Seconds after a jump-out during which water-entry re-detection is suppressed, so the upward impulse has time to actually carry the character above the surface before it's checked again.")]
        [SerializeField] private float jumpOutGracePeriod = 0.6f;

        private CharacterController controller;
        private bool isSwimming;
        private float entryCheckSuppressedUntil;

        public bool IsSwimming => isSwimming;

        // "Physically inside the water volume right now" - true even while the isSwimming flag
        // is off (e.g. during the jump-out grace window, when entry re-detection is suppressed
        // but the character is still below the surface). Used by IceSurfController to block
        // surf activation in water without being fooled by that window.
        public bool IsInWaterVolume(Vector3 worldPos)
        {
            if (waterSurface == null) return false;
            return worldPos.y < waterSurface.position.y && IsWaterAt(worldPos);
        }


        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (terrain == null)
            {
                terrain = Terrain.activeTerrain;
            }

            if (iceSurfController == null)
            {
                iceSurfController = GetComponent<IceSurfController>();
            }

            if (splashVfx == null)
            {
                Transform found = transform.Find("SplashVFX");
                if (found != null) { splashVfx = found.GetComponent<ParticleSystem>(); }
            }
        }

        private void OnEnable()
        {
            moveInput.action.Enable();
            jumpInput.action.Enable();
        }

        private void OnDisable()
        {
            moveInput.action.Disable();
            jumpInput.action.Disable();
        }

        private void Update()
        {
            if (waterSurface == null) return;

            if (!isSwimming)
            {
                CheckWaterEntry();
                return;
            }

            UpdateSwimming();
        }

        // Ground truth for "is there actually water at this XZ position" - the terrain floor
        // has to sit at least minWaterDepth below the water surface. Stable across frames
        // (unlike the character's own animated float height), so it doesn't flicker.
        private bool IsWaterAt(Vector3 worldPos)
        {
            if (terrain == null) return true;
            float groundY = terrain.SampleHeight(worldPos) + terrain.transform.position.y;
            return groundY < waterSurface.position.y - minWaterDepth;
        }

        // transform.position sits at the character's feet (matches the convention used
        // elsewhere in this project, e.g. IceSurfController's ground check) - so "in water"
        // means the feet have dropped below the surface height, at a spot that's actually
        // water. Suppressed briefly right after a jump-out (see jumpOutGracePeriod) so the
        // upward impulse gets a chance to actually carry the character above the surface
        // instead of being re-caught here the very next frame while still technically below it.
        private void CheckWaterEntry()
        {
            if (Time.time < entryCheckSuppressedUntil) return;

            if (transform.position.y < waterSurface.position.y && IsWaterAt(transform.position))
            {
                EnterWater();
            }
        }

        // Repositions the splash burst to the water surface at the character's current XZ
        // and fires it. Spawned at the surface height rather than the character's own feet
        // position, since feet are typically already a bit below/above the surface by the
        // time entry/exit is detected - the surface itself is where the splash should read as
        // happening. Uses World simulation space (see SplashVFX setup) so repositioning the
        // emitter just before Play() doesn't drag already-emitted particles from a previous burst.
        private void PlaySplash()
        {
            if (splashVfx == null || waterSurface == null) return;

            Vector3 pos = transform.position;
            pos.y = waterSurface.position.y;
            splashVfx.transform.position = pos;
            splashVfx.Play();
        }

private void EnterWater()
        {
            // Surfing ends the moment real water is entered - surf hands its momentum back to
            // PlayerController first (SetSurfing(false) -> AddExternalVelocity), then swim
            // activation zeroes it out (SetSwimming(true) clears velocity/externalVelocity),
            // so no leftover surf speed leaks into or out of the water.
            if (iceSurfController != null && iceSurfController.IsSurfing)
            {
                iceSurfController.SetSurfing(false);
            }

            isSwimming = true;
            playerController.SetSwimming(true);
            PlaySplash();
        }

        // verticalVelocity is handed off via SetVerticalVelocity (pure gravity-driven, same as
        // a normal jump) rather than AddExternalVelocity, since external velocity decays
        // quickly (tuned for horizontal ground-friction-style handoffs like surf) and would eat
        // into a vertical launch before gravity could arc it clear of the surface.
private void ExitWater(float verticalVelocity, float suppressEntryFor)
        {
            isSwimming = false;
            entryCheckSuppressedUntil = Time.time + suppressEntryFor;
            playerController.SetSwimming(false);
            playerController.SetVerticalVelocity(verticalVelocity);
            PlaySplash();

            if (animator != null)
            {
                animator.SetBool("IsSwimMoving", false);

                // PlayerController.Update() early-returns the entire time swimming is active,
                // so its IsGrounded animator param never gets refreshed and is left holding
                // whatever it was the instant before EnterWater - stale for the whole swim.
                // TreadingWater/Swimming's own exit-to-Idle transition is gated on IsGrounded
                // (see AnimatorController) specifically so a jump-out doesn't fall through to
                // Idle - that guard only works if this stale value is corrected to the real,
                // live CharacterController.isGrounded right now, before the animator evaluates
                // this frame's transitions. controller.isGrounded is accurate here regardless
                // of exit type: false while airborne (jump-out), true when this is called from
                // the walk-to-shore path (which only fires once controller.isGrounded is
                // already true - see UpdateSwimming).
                animator.SetBool("IsGrounded", controller.isGrounded);

                // A real jump-out (positive launch speed) plays the normal Jump animation,
                // same trigger OnJump/surf's kickflip use. Reaching shore on foot
                // (verticalVelocity == 0, from the walk-out path) skips this - Idle/Run take
                // over via the IsGrounded-gated exit transition above instead.
                if (verticalVelocity > 0f)
                {
                    animator.SetTrigger("Jump");
                }
            }
        }

        private Vector3 GetWishDir()
        {
            Vector2 input = moveInput.action.ReadValue<Vector2>();

            Vector3 camForward = cameraTransform != null ? cameraTransform.forward : transform.forward;
            Vector3 camRight = cameraTransform != null ? cameraTransform.right : transform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 wishDir = camForward * input.y + camRight * input.x;
            if (wishDir.sqrMagnitude > 1f)
            {
                wishDir.Normalize();
            }
            return wishDir;
        }

        private void UpdateSwimming()
        {
            if (jumpInput.action.WasPressedThisFrame())
            {
                ExitWater(jumpOutSpeed, jumpOutGracePeriod);
                return;
            }

            // Reaching shore: ground truth check, not the character's own float height (see
            // class comment for why that caused flicker).
            if (!IsWaterAt(transform.position) && controller.isGrounded)
            {
                ExitWater(0f, 0f);
                return;
            }

            Vector3 wishDir = GetWishDir();
            bool moving = wishDir.sqrMagnitude > movingThreshold * movingThreshold;

            // Treading (stationary) sits a bit deeper than active swimming - its idle pose
            // holds the hands higher, so a shallower depth let them poke out above the surface.
            float floatY = waterSurface.position.y - (moving ? floatDepth : treadFloatDepth);

            Vector3 horizontalMotion = wishDir * swimSpeed * Time.deltaTime;

            float currentY = transform.position.y;
            float newY = Mathf.Lerp(currentY, floatY, 1f - Mathf.Exp(-floatCorrectionSpeed * Time.deltaTime));
            float verticalDelta = newY - currentY;

            controller.Move(horizontalMotion + Vector3.up * verticalDelta);

            // Safety net: high-speed underwater surf entries (boost-spam) could tunnel through
            // the terrain heightmap collider before swim took over, leaving the character
            // stuck below the floor and falling forever. If that ever happens, snap back up
            // onto the terrain surface.
            if (terrain != null)
            {
                float groundY = terrain.SampleHeight(transform.position) + terrain.transform.position.y;
                if (transform.position.y < groundY)
                {
                    Vector3 corrected = transform.position;
                    corrected.y = groundY + 0.1f;
                    controller.enabled = false;
                    transform.position = corrected;
                    controller.enabled = true;
                }
            }

            if (moving)
            {
                Quaternion toRotation = Quaternion.LookRotation(wishDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, toRotation, turnSpeedDegPerSec * Time.deltaTime);
            }

            if (animator != null)
            {
                animator.SetBool("IsSwimMoving", moving);
            }
        }
    }
}
