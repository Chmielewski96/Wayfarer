using UnityEngine;
using UnityEngine.InputSystem;
using Wayfarer.Player;
using Wayfarer.Spells;

namespace Wayfarer.Movement
{
    /// <summary>
    /// Ice Surfing movement ability for the Sea mage kit: persistent velocity + acceleration
    /// model, carving (turn-rate shrinks with speed, over-turning bleeds speed, and sharpens
    /// the longer a turn is held), ground-plane projected velocity so slopes are followed
    /// downhill and launch you at convex transitions (ramp crests/ends), and a Water drain/sec
    /// cost shared with the rest of the Sea kit. Can be toggled on or off at any time, including
    /// mid-air.
    ///
    /// Also owns the Q speed-boost while surfing: an instant forward burst that's allowed to
    /// exceed surfMaxSpeed via a decaying "extra ceiling" (boostBonus), so the character visibly
    /// overshoots the normal cap and eases back down to it shortly after, rather than snapping
    /// back or being hard-clamped away entirely.
    ///
    /// Coexists with PlayerController rather than replacing it: PlayerController owns all
    /// normal movement/rotation/jump, and hands off control here only while surfing is active
    /// (toggled via the Surf input). On exit, current velocity is handed back to
    /// PlayerController as external momentum instead of snapping to zero.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class IceSurfController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private WaterResource waterResource;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private Animator animator;
        [SerializeField] private GameObject iceBoardVisual;
        [SerializeField] private ParticleSystem snowPuffVfx;
        [SerializeField] private Transform modelTransform;
        [Tooltip("Optional - auto-found on the same GameObject. Surf activation is blocked while swimming (board on water makes no sense); SwimController also force-ends surf on water entry.")]
        [SerializeField] private SwimController swimController;

        [Header("Input")]
        [SerializeField] private InputActionReference moveInput;
        [SerializeField] private InputActionReference jumpInput;
        [SerializeField] private InputActionReference surfInput;

        [Header("Surf Speed")]
        [SerializeField] private float surfBaseSpeed = 9f;
        [SerializeField] private float surfMaxSpeed = 22f;
        [SerializeField] private float surfAccel = 9f;
        [SerializeField] private float passiveDecayPerSecond = 1f;

        [Header("Surf Carving")]
        [SerializeField] private float carveRateAtBase = 180f;
        [SerializeField] private float carveRateAtMax = 45f;
        [SerializeField] private float carveBleedPerDegree = 0.15f;

        [Header("Surf Carving - Hold to Sharpen (experimental)")]
        [Tooltip("Holding a turn in the same direction ramps the carve rate up to this multiplier over turnRampDuration seconds. 1 = no effect.")]
        [SerializeField] private float turnRampMaxMultiplier = 2.5f;
        [Tooltip("Seconds of continuously holding the same turn direction to reach turnRampMaxMultiplier.")]
        [SerializeField] private float turnRampDuration = 1.5f;
        [Tooltip("Minimum angle (degrees) between current and wish direction to count as \"actively turning\" for ramp purposes.")]
        [SerializeField] private float turnRampEngageAngle = 2f;

        [Header("Surf Slope")]
        [Tooltip("Speed gained per second, scaled by how steep the downhill slope is (0-1).")]
        [SerializeField] private float slopeAccelFactor = 12f;
        [Tooltip("Speed lost per second, scaled by how steep the uphill slope is (0-1). Higher than slopeAccelFactor so climbing robs momentum faster than descending builds it.")]
        [SerializeField] private float uphillDecelFactor = 26f;

        [Header("Surf Resource (talent tree will scale this down toward 0)")]
        [SerializeField] private float surfWaterDrainPerSecond = 8f;

        [Header("Surf Activation Gating")]
        [Tooltip("Minimum seconds between surf activations - stops toggle-mashing from repeatedly exploiting the activation momentum grab and dodging the per-second drain.")]
        [SerializeField] private float activationCooldown = 1.5f;
        [Tooltip("Minimum Water required to turn surf on. Once active, drain works normally down to 0 - this only gates entry so surf can't be flickered on at near-zero Water.")]
        [SerializeField] private float minWaterToActivate = 5f;

        [Header("Jump")]
        [SerializeField] private float jumpHeight = 1.8f;

        [Header("Surf Grounding")]
        [Tooltip("Extra buffer (in world units, below the feet) the ground raycast checks beyond contact. Small on purpose - this is not a body-height allowance.")]
        [SerializeField] private float groundCheckDistance = 0.3f;
        [SerializeField] private float gravity = -20f;

        [Header("Surf Lean")]
        [SerializeField] private float leanMaxAngle = 25f;
        [SerializeField] private float leanSmoothing = 8f;
        [SerializeField] private bool invertLean = false;

        [Header("Surf Jump Kickflip")]
        [Tooltip("How long (seconds) the board takes to complete one full 360 spin after a surf jump, and how long the character's Jump animation is shown for.")]
        [SerializeField] private float kickflipDuration = 0.6f;

        [Header("Speed Boost (Q while surfing)")]
        [Tooltip("Instant speed added on top of current speed when boosting - this can push current speed above surfMaxSpeed.")]
        [SerializeField] private float boostBurstAmount = 10f;
        [Tooltip("How fast the temporary over-surfMaxSpeed ceiling decays back down per second after a boost.")]
        [SerializeField] private float boostDecayPerSecond = 16f;
        [SerializeField] private float boostCooldown = 1.5f;
        [SerializeField] private float boostWaterCost = 10f;
        [SerializeField] private GameObject handIceVfxPrefab;
        [SerializeField] private GameObject sonicBoomRingPrefab;
        [SerializeField] private float boostHandVfxDuration = 0.35f;

        private CharacterController controller;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private bool isSurfing;

        private float turnRateDegPerSec;
        private float turnHoldTime;
        private float lastTurnSign;
        private float currentLean;
        private Quaternion modelBaseLocalRotation = Quaternion.identity;
        private Quaternion boardBaseLocalRotation = Quaternion.identity;
        private bool baseRotationsCaptured;

        private bool isKickflipping;
        private float kickflipElapsed;

        // Decaying "extra ceiling" above surfMaxSpeed granted by a boost - UpdateSurf clamps
        // against (surfMaxSpeed + boostBonus) instead of a flat surfMaxSpeed, so speed can
        // exceed the normal cap right after boosting and eases back down as this decays to 0.
        private float boostBonus;
        private float boostCooldownEndTime;
        private float nextActivationAllowedTime;
        private Transform rightHandSocket;
        private Transform leftHandSocket;

        public bool IsSurfing => isSurfing;
        public float CurrentSpeed => horizontalVelocity.magnitude;
        public float SurfMaxSpeed => surfMaxSpeed;

private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (swimController == null)
            {
                swimController = GetComponent<SwimController>();
            }
            if (animator != null && animator.isHuman)
            {
                rightHandSocket = animator.GetBoneTransform(HumanBodyBones.RightHand);
                leftHandSocket = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            }
        }

        private void OnEnable()
        {
            moveInput.action.Enable();
            jumpInput.action.Enable();
            surfInput.action.Enable();
        }

        private void OnDisable()
        {
            moveInput.action.Disable();
            jumpInput.action.Disable();
            surfInput.action.Disable();
        }

        public void SetSurfing(bool value)
        {
            if (value == isSurfing) return;
            isSurfing = value;

            if (playerController != null)
            {
                playerController.SetSurfing(value);
            }

            if (isSurfing)
            {
                // Pick up whatever momentum the player already had - including vertical speed,
                // so activating mid-air (falling or still rising out of a jump) continues
                // smoothly instead of snapping vertical motion to zero.
                Vector3 v = controller.velocity;
                verticalVelocity = v.y;
                v.y = 0f;
                horizontalVelocity = v;
            }
            else
            {
                // hand momentum back so it carries into normal movement instead of stopping dead
                if (playerController != null)
                {
                    playerController.AddExternalVelocity(horizontalVelocity);
                }
                horizontalVelocity = Vector3.zero;
                turnRateDegPerSec = 0f;
                turnHoldTime = 0f;
                lastTurnSign = 0f;
                boostBonus = 0f;

                EndKickflip();
            }

            // Board pops in/out with a puff of snow on both ends of the transition.
            if (iceBoardVisual != null)
            {
                iceBoardVisual.SetActive(isSurfing);
            }

            if (snowPuffVfx != null)
            {
                snowPuffVfx.Play();
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
            if (wishDir.sqrMagnitude > 0.001f)
            {
                wishDir.Normalize();
            }
            return wishDir;
        }

        // transform.position sits at the character's feet (not center), so the raycast only
        // needs to check a small buffer below the feet for near-contact - NOT half the body
        // height, which previously let this report "grounded" up to ~1 unit above the real
        // surface (the character would visibly float and stop mid-fall while surfing).
        private bool CheckGrounded(out Vector3 normal)
        {
            normal = Vector3.up;
            float originOffset = 0.1f;
            Vector3 origin = transform.position + Vector3.up * originOffset;
            float rayDist = originOffset + groundCheckDistance;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayDist, ~0, QueryTriggerInteraction.Ignore))
            {
                normal = hit.normal;
                return true;
            }
            return controller.isGrounded;
        }

        private void CaptureBaseRotationsIfNeeded()
        {
            if (baseRotationsCaptured) return;
            if (modelTransform != null) { modelBaseLocalRotation = modelTransform.localRotation; }
            if (iceBoardVisual != null) { boardBaseLocalRotation = iceBoardVisual.transform.localRotation; }
            baseRotationsCaptured = true;
        }

private void Update()
        {
            // A conversation freezes PlayerController's own Update() and forcibly ends surf
            // the moment it starts (see NPCInteractable.StartDialogue), but this Update() runs
            // independently of that - without this guard, the surf toggle/jump/boost inputs
            // would still be processed and could re-activate surfing (or fire a kickflip)
            // right underneath a supposedly-frozen conversation.
            if (playerController != null && (playerController.IsTalking || playerController.IsMenuOpen)) return;

            CaptureBaseRotationsIfNeeded();

            // Surf can be toggled OFF at any time; turning it ON is gated (see CanActivateSurf).
            if (surfInput.action.WasPressedThisFrame())
            {
                if (isSurfing)
                {
                    SetSurfing(false);
                }
                else if (CanActivateSurf())
                {
                    SetSurfing(true);
                    nextActivationAllowedTime = Time.time + activationCooldown;
                }
            }

            UpdateLean();
            UpdateKickflip();

            if (!isSurfing) return;

            if (jumpInput.action.WasPressedThisFrame() && CheckGrounded(out _))
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                StartKickflip();
            }

            // Q is free to reuse as a surf-only boost here: PlayerSpellCaster's own Q binding
            // selects the Ice Bolt spell slot, but spell casting is already deselected the
            // instant surfing starts (see PlayerController.SetSurfing), so there's no conflict.
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            {
                TryBoost();
            }

            UpdateSurf();
        }

// Gates for turning surf ON via the player's toggle input (code-driven SetSurfing,
        // e.g. swim force-ending surf, is intentionally not restricted):
        // - not while swimming or physically inside the water volume (the volume check
        //   catches the jump-out grace window, when IsSwimming briefly reads false underwater)
        // - a short cooldown between activations, so mashing the toggle can't repeatedly
        //   grab activation momentum or flicker the drain on and off
        // - a minimum Water floor to enter; once active, the normal per-second drain applies
        //   all the way down to 0 as before
        private bool CanActivateSurf()
        {
            if (swimController != null &&
                (swimController.IsSwimming || swimController.IsInWaterVolume(transform.position)))
            {
                return false;
            }

            if (Time.time < nextActivationAllowedTime)
            {
                return false;
            }

            if (waterResource != null && !waterResource.HasEnough(minWaterToActivate))
            {
                // Pre-check only (nothing consumed), so fire the insufficient-Water signal
                // explicitly - TryConsume isn't involved here to do it for us.
                waterResource.NotifyInsufficientWater();
                return false;
            }

            return true;
        }


        // Purely cosmetic: banks the character mesh and board into turns based on how sharply
        // we're currently carving, smoothed for fluidity, decaying back to upright when not
        // turning or not surfing. The CharacterController / root transform stay untouched so
        // this never affects actual movement or collision.
        private void UpdateLean()
        {
            float targetLean = 0f;
            if (isSurfing)
            {
                float normalizedRate = carveRateAtBase > 0.01f ? Mathf.Clamp(turnRateDegPerSec / carveRateAtBase, -1f, 1f) : 0f;
                targetLean = -normalizedRate * leanMaxAngle;
                if (invertLean) targetLean = -targetLean;
            }

            currentLean = Mathf.Lerp(currentLean, targetLean, 1f - Mathf.Exp(-leanSmoothing * Time.deltaTime));

            if (modelTransform != null)
            {
                modelTransform.localRotation = modelBaseLocalRotation * Quaternion.Euler(0f, 0f, currentLean);
            }

            // While a kickflip is spinning, it takes over the board's rotation entirely (see
            // UpdateKickflip, which runs after this and overwrites iceBoardVisual's rotation) -
            // lean still gets applied here first so it resumes seamlessly the instant the flip
            // finishes.
            if (iceBoardVisual != null)
            {
                iceBoardVisual.transform.localRotation = boardBaseLocalRotation * Quaternion.Euler(0f, 0f, currentLean);
            }
        }

        // Jumping while surfing fires the character's normal Jump animation (see StartKickflip)
        // and spins the board one full 360 degrees around its own forward axis - the direction
        // of travel, since the player transform is kept facing that way by UpdateSurf's
        // LookRotation - purely cosmetic, like a skateboard kickflip. Runs on a fixed timer
        // rather than tracking the actual landing so it stays simple and consistent with the
        // rest of the surf-jump animation timing.
        private void UpdateKickflip()
        {
            if (!isKickflipping) return;

            kickflipElapsed += Time.deltaTime;
            float t = kickflipDuration > 0.0001f ? Mathf.Clamp01(kickflipElapsed / kickflipDuration) : 1f;

            if (iceBoardVisual != null)
            {
                iceBoardVisual.transform.localRotation = boardBaseLocalRotation * Quaternion.AngleAxis(t * 360f, Vector3.forward);
            }

            if (t >= 1f)
            {
                EndKickflip();
            }
        }

        private void StartKickflip()
        {
            isKickflipping = true;
            kickflipElapsed = 0f;

            if (animator != null)
            {
                animator.SetBool("IsSurfJumping", true);
                animator.SetTrigger("Jump");
            }

            CancelInvoke(nameof(EndSurfJumpAnimation));
            Invoke(nameof(EndSurfJumpAnimation), kickflipDuration);
        }

        private void EndKickflip()
        {
            isKickflipping = false;

            // Cancel any pending natural end-of-kickflip callback and apply it immediately
            // instead - SetSurfing(false) can be called mid-flip (surf toggled off manually,
            // or something else force-ending it, e.g. an NPC interaction), and without this
            // the IsSurfJumping animator bool would otherwise stay true until the original
            // timer catches up, showing a jump pose for a stray fraction of a second after
            // surf has already ended.
            CancelInvoke(nameof(EndSurfJumpAnimation));
            EndSurfJumpAnimation();
        }

        private void EndSurfJumpAnimation()
        {
            if (animator != null)
            {
                animator.SetBool("IsSurfJumping", false);
            }
        }

        // Instant forward speed burst that's allowed to exceed surfMaxSpeed - see boostBonus,
        // consumed by UpdateSurf's clamps, which decays back down afterward instead of being
        // hard-capped immediately. Costs Water and is gated by a cooldown so it can't be
        // spammed. Visuals: both hands flare with the same ice-orb VFX used for casting, plus an
        // icy shockwave ring that expands out behind the character like a sonic boom.
        private void TryBoost()
        {
            if (Time.time < boostCooldownEndTime) return;
            if (waterResource == null || !waterResource.TryConsume(boostWaterCost)) return;

            boostCooldownEndTime = Time.time + boostCooldown;
            boostBonus += boostBurstAmount;

            Vector3 boostDir = horizontalVelocity.sqrMagnitude > 0.01f ? horizontalVelocity.normalized : transform.forward;
            float newSpeed = horizontalVelocity.magnitude + boostBurstAmount;
            horizontalVelocity = boostDir * newSpeed;

            SpawnBoostVfx(boostDir);
        }

        private void SpawnBoostVfx(Vector3 boostDir)
        {
            if (handIceVfxPrefab != null)
            {
                SpawnHandBoostVfx(rightHandSocket);
                SpawnHandBoostVfx(leftHandSocket);
            }

            if (sonicBoomRingPrefab != null)
            {
                Vector3 ringPos = transform.position + Vector3.up * 1f - boostDir * 0.4f;
                Quaternion ringRot = boostDir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(boostDir) : transform.rotation;
                var ring = Instantiate(sonicBoomRingPrefab, ringPos, ringRot);
                Destroy(ring, 1f);
            }
        }

        private void SpawnHandBoostVfx(Transform socket)
        {
            if (socket == null) return;

            var instance = Instantiate(handIceVfxPrefab, socket.position, socket.rotation, socket);
            var vfx = instance.GetComponent<HandCastVfx>();
            if (vfx != null)
            {
                vfx.BeginCast(boostHandVfxDuration);
            }
            else
            {
                Destroy(instance, boostHandVfxDuration + 0.5f);
            }
        }

        private void UpdateSurf()
        {
            // Hard stop if surfing ever ends up inside the water volume (e.g. activated
            // mid-air over water and descended below the surface during swim's jump-out
            // grace window, when water entry re-detection is suppressed). Ending it here
            // immediately - rather than waiting out the grace period - also prevents surf
            // gravity/boost speeds from tunneling the character through the terrain
            // collider underwater. PlayerController takes over; swim re-engages on its own
            // once the grace window expires.
            if (swimController != null && swimController.IsInWaterVolume(transform.position))
            {
                SetSurfing(false);
                return;
            }

            if (waterResource == null || !waterResource.TryConsume(surfWaterDrainPerSecond * Time.deltaTime))
            {
                SetSurfing(false);
                return;
            }

            // The boosted ceiling decays back toward surfMaxSpeed over time rather than
            // vanishing instantly - this is what makes the post-boost speed ease back down to
            // normal instead of hard-clamping the frame after.
            boostBonus = Mathf.Max(0f, boostBonus - boostDecayPerSecond * Time.deltaTime);
            float effectiveMaxSpeed = surfMaxSpeed + boostBonus;

            bool grounded = CheckGrounded(out Vector3 groundNormal);

            Vector3 wishDir = GetWishDir();

            float currentSpeed = horizontalVelocity.magnitude;
            Vector3 currentDir = currentSpeed > 0.01f ? horizontalVelocity / currentSpeed : (wishDir.sqrMagnitude > 0.001f ? wishDir : transform.forward);

            float turnAmount = 0f;

            if (wishDir.sqrMagnitude > 0.001f)
            {
                float angleDiff = Vector3.SignedAngle(currentDir, wishDir, Vector3.up);

                // Hold-to-sharpen: track how long we've been continuously steering the same way.
                // Releasing the turn, going straight, or reversing direction resets the ramp.
                if (Mathf.Abs(angleDiff) > turnRampEngageAngle)
                {
                    float sign = Mathf.Sign(angleDiff);
                    if (lastTurnSign != 0f && sign != lastTurnSign)
                    {
                        turnHoldTime = 0f;
                    }
                    lastTurnSign = sign;
                    turnHoldTime += Time.deltaTime;
                }
                else
                {
                    turnHoldTime = 0f;
                    lastTurnSign = 0f;
                }

                float rampT = turnRampDuration > 0.01f ? Mathf.Clamp01(turnHoldTime / turnRampDuration) : 1f;
                float turnRampMultiplier = Mathf.Lerp(1f, turnRampMaxMultiplier, rampT);

                float speedRatio = Mathf.Clamp01(Mathf.InverseLerp(surfBaseSpeed, surfMaxSpeed, currentSpeed));
                float carveRateNow = Mathf.Lerp(carveRateAtBase, carveRateAtMax, speedRatio) * turnRampMultiplier;
                float maxTurnThisFrame = carveRateNow * Time.deltaTime;

                turnAmount = Mathf.Clamp(angleDiff, -maxTurnThisFrame, maxTurnThisFrame);
                currentDir = Quaternion.AngleAxis(turnAmount, Vector3.up) * currentDir;

                float excess = Mathf.Abs(angleDiff) - maxTurnThisFrame;
                if (excess > 0f)
                {
                    currentSpeed = Mathf.Max(surfBaseSpeed * 0.5f, currentSpeed - excess * carveBleedPerDegree * Time.deltaTime);
                }

                float alignment = Vector3.Dot(currentDir, wishDir);
                if (alignment > 0.3f)
                {
                    currentSpeed = Mathf.Min(effectiveMaxSpeed, currentSpeed + surfAccel * Time.deltaTime);
                }
            }
            else
            {
                currentSpeed = Mathf.Max(0f, currentSpeed - passiveDecayPerSecond * Time.deltaTime);
                turnHoldTime = 0f;
                lastTurnSign = 0f;
            }

            turnRateDegPerSec = Time.deltaTime > 0.0001f ? turnAmount / Time.deltaTime : 0f;

            // Downhill builds speed, uphill robs it - using separate factors so climbing can
            // scrub momentum harder than descending builds it (asymmetric on purpose).
            Vector3 downSlope = grounded ? Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized : Vector3.zero;
            if (downSlope.sqrMagnitude > 0.0001f)
            {
                float slopeDot = Vector3.Dot(currentDir, downSlope);
                float slopeFactor = slopeDot >= 0f ? slopeAccelFactor : uphillDecelFactor;
                currentSpeed = Mathf.Max(0f, currentSpeed + slopeDot * slopeFactor * Time.deltaTime);
            }

            currentSpeed = Mathf.Min(currentSpeed, effectiveMaxSpeed);
            horizontalVelocity = currentDir * currentSpeed;

            // While grounded (and not actively launching off a jump), velocity follows the ground
            // plane's actual tilt rather than staying flat - downhill slopes stay stuck (the
            // projected vector dips into the slope, so CharacterController.Move keeps contact)
            // while uphill momentum naturally carries you into the air the instant the slope
            // falls away faster than the projection can follow (a ramp cresting or ending).
            bool stayGrounded = grounded && verticalVelocity <= 0f;
            Vector3 motion;

            if (stayGrounded)
            {
                Vector3 slopeVelocity = Vector3.ProjectOnPlane(currentDir, groundNormal);
                slopeVelocity = (slopeVelocity.sqrMagnitude > 0.0001f ? slopeVelocity.normalized : currentDir) * currentSpeed;

                verticalVelocity = slopeVelocity.y;
                motion = slopeVelocity * Time.deltaTime;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
                motion = (horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime;
            }

            controller.Move(motion);

            if (horizontalVelocity.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(horizontalVelocity.normalized, Vector3.up);
            }

            if (animator != null)
            {
                animator.SetFloat("Speed", Mathf.Clamp(currentSpeed / surfBaseSpeed, 0f, 2f));
                animator.SetBool("IsGrounded", grounded);
            }
        }
    }
}
