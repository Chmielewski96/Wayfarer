using UnityEngine;
using UnityEngine.InputSystem;
using Wayfarer.Player;

namespace Wayfarer.Movement
{
    /// <summary>
    /// Ice Surfing movement ability for the Sea mage kit: persistent velocity + acceleration
    /// model, carving (turn-rate shrinks with speed, over-turning bleeds speed), ground-plane
    /// projected velocity so slopes are followed downhill and launch you at convex transitions
    /// (ramp crests/ends), and a Water drain/sec cost shared with the rest of the Sea kit.
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

        [Header("Input")]
        [SerializeField] private InputActionReference moveInput;
        [SerializeField] private InputActionReference jumpInput;
        [SerializeField] private InputActionReference surfInput;

        [Header("Surf Speed")]
        [SerializeField] private float surfBaseSpeed = 9f;
        [SerializeField] private float surfMaxSpeed = 38f;
        [SerializeField] private float surfAccel = 18f;
        [SerializeField] private float passiveDecayPerSecond = 1f;

        [Header("Surf Carving")]
        [SerializeField] private float carveRateAtBase = 180f;
        [SerializeField] private float carveRateAtMax = 45f;
        [SerializeField] private float carveBleedPerDegree = 0.15f;

        [Header("Surf Slope")]
        [SerializeField] private float slopeAccelFactor = 12f;

        [Header("Surf Resource (talent tree will scale this down toward 0)")]
        [SerializeField] private float surfWaterDrainPerSecond = 8f;

        [Header("Jump")]
        [SerializeField] private float jumpHeight = 1.8f;

        [Header("Surf Grounding")]
        [SerializeField] private float groundCheckDistance = 0.3f;
        [SerializeField] private float gravity = -20f;

        private CharacterController controller;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private bool isSurfing;

        public bool IsSurfing => isSurfing;
        public float CurrentSpeed => horizontalVelocity.magnitude;
        public float SurfMaxSpeed => surfMaxSpeed;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
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
                // pick up whatever momentum the player already had from normal movement
                Vector3 v = controller.velocity;
                v.y = 0f;
                horizontalVelocity = v;
                verticalVelocity = 0f;
            }
            else
            {
                // hand momentum back so it carries into normal movement instead of stopping dead
                if (playerController != null)
                {
                    playerController.AddExternalVelocity(horizontalVelocity);
                }
                horizontalVelocity = Vector3.zero;
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

        private bool CheckGrounded(out Vector3 normal)
        {
            normal = Vector3.up;
            Vector3 origin = transform.position + Vector3.up * 0.1f;
            float rayDist = (controller.height * 0.5f) + groundCheckDistance;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayDist, ~0, QueryTriggerInteraction.Ignore))
            {
                normal = hit.normal;
                return true;
            }
            return controller.isGrounded;
        }

        private void Update()
        {
            if (surfInput.action.WasPressedThisFrame())
            {
                if (!isSurfing && CheckGrounded(out _))
                {
                    SetSurfing(true);
                }
                else if (isSurfing)
                {
                    SetSurfing(false);
                }
            }

            if (!isSurfing) return;

            if (jumpInput.action.WasPressedThisFrame() && CheckGrounded(out _))
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            UpdateSurf();
        }

        private void UpdateSurf()
        {
            if (waterResource == null || !waterResource.TryConsume(surfWaterDrainPerSecond * Time.deltaTime))
            {
                SetSurfing(false);
                return;
            }

            bool grounded = CheckGrounded(out Vector3 groundNormal);

            Vector3 wishDir = GetWishDir();

            float currentSpeed = horizontalVelocity.magnitude;
            Vector3 currentDir = currentSpeed > 0.01f ? horizontalVelocity / currentSpeed : (wishDir.sqrMagnitude > 0.001f ? wishDir : transform.forward);

            if (wishDir.sqrMagnitude > 0.001f)
            {
                float angleDiff = Vector3.SignedAngle(currentDir, wishDir, Vector3.up);
                float speedRatio = Mathf.Clamp01(Mathf.InverseLerp(surfBaseSpeed, surfMaxSpeed, currentSpeed));
                float carveRateNow = Mathf.Lerp(carveRateAtBase, carveRateAtMax, speedRatio);
                float maxTurnThisFrame = carveRateNow * Time.deltaTime;

                float turnAmount = Mathf.Clamp(angleDiff, -maxTurnThisFrame, maxTurnThisFrame);
                currentDir = Quaternion.AngleAxis(turnAmount, Vector3.up) * currentDir;

                float excess = Mathf.Abs(angleDiff) - maxTurnThisFrame;
                if (excess > 0f)
                {
                    currentSpeed = Mathf.Max(surfBaseSpeed * 0.5f, currentSpeed - excess * carveBleedPerDegree * Time.deltaTime);
                }

                float alignment = Vector3.Dot(currentDir, wishDir);
                if (alignment > 0.3f)
                {
                    currentSpeed = Mathf.Min(surfMaxSpeed, currentSpeed + surfAccel * Time.deltaTime);
                }
            }
            else
            {
                currentSpeed = Mathf.Max(0f, currentSpeed - passiveDecayPerSecond * Time.deltaTime);
            }

            Vector3 downSlope = grounded ? Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized : Vector3.zero;
            if (downSlope.sqrMagnitude > 0.0001f)
            {
                float slopeDot = Vector3.Dot(currentDir, downSlope);
                currentSpeed = Mathf.Max(0f, currentSpeed + slopeDot * slopeAccelFactor * Time.deltaTime);
            }

            currentSpeed = Mathf.Min(currentSpeed, surfMaxSpeed);
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
