using UnityEngine;
using UnityEngine.InputSystem;
using Wayfarer.Spells;

namespace Wayfarer.Player
{
    /// <summary>
    /// Holds up to 6 spell slots (selected via Q/E/R/Z/X/C), casts the currently-selected
    /// spell on left-click while aiming. Checks and consumes Water cost via WaterResource,
    /// enforces per-spell cooldowns. Pressing a slot's key again, pressing Deselect (Tab),
    /// or entering surf state clears the current selection.
    /// </summary>
    public class PlayerSpellCaster : MonoBehaviour
    {
        [SerializeField] private SpellData[] spellSlots = new SpellData[6];
        [SerializeField] private GameObject waterBlobPrefab;
        [SerializeField] private LayerMask targetMask;

        [SerializeField] private InputActionReference castInput;
        [SerializeField] private InputActionReference deselectInput;
        [SerializeField] private InputActionReference[] selectInputs = new InputActionReference[6];

        [SerializeField] private PlayerController playerController;
        [SerializeField] private WaterResource waterResource;
        [SerializeField] private Transform castOrigin;
        [SerializeField] private float aimRayDistance = 200f;

        [Header("Cast Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string upperBodyLayerName = "UpperBody";
        [SerializeField] private float iceBoltCastAnimDuration = 1.15f;
        [SerializeField] private float shatterCastAnimDuration = 1.5f;
        [SerializeField] private float frostConeCastAnimDuration = 1.2f;

        private int selectedSlot = -1;
        private readonly float[] cooldownEndTimes = new float[6];
        private int upperBodyLayerIndex = -1;
        private const int BaseLayerIndex = 0;

        // Tracks an in-progress stationary (full-body) cast so Update() can hand it off to the
        // UpperBody layer the instant the player starts moving mid-animation - see PlayCastAnimation.
        // The state name always matches the trigger name (CastIceBolt/CastShatter/CastFrostCone
        // exist as identically-named states on both the base and UpperBody layers).
        private string activeStationaryCastState;
        private bool stationaryCastHandedOff;

        public SpellData SelectedSpell => selectedSlot >= 0 && selectedSlot < spellSlots.Length ? spellSlots[selectedSlot] : null;

        public int SelectedSlotIndex => selectedSlot;

        private void Awake()
        {
            if (animator == null) { animator = GetComponentInChildren<Animator>(); }
            if (animator != null) { upperBodyLayerIndex = animator.GetLayerIndex(upperBodyLayerName); }
        }

        private void OnEnable()
        {
            castInput.action.Enable();
            castInput.action.performed += OnCastPerformed;

            if (deselectInput != null)
            {
                deselectInput.action.Enable();
                deselectInput.action.performed += OnDeselectPerformed;
            }

            for (int i = 0; i < selectInputs.Length; i++)
            {
                if (selectInputs[i] == null) continue;
                int slotIndex = i;
                selectInputs[i].action.Enable();
                selectInputs[i].action.performed += ctx => SelectSlot(slotIndex);
            }
        }

        private void OnDisable()
        {
            castInput.action.performed -= OnCastPerformed;
            castInput.action.Disable();

            if (deselectInput != null)
            {
                deselectInput.action.performed -= OnDeselectPerformed;
                deselectInput.action.Disable();
            }

            foreach (var input in selectInputs)
            {
                if (input == null) continue;
                input.action.Disable();
            }
        }

        // Pressing the same slot's key again deselects it (toggle); pressing a different slot's
        // key switches to it.
        public void SelectSlot(int index)
        {
            if (index < 0 || index >= spellSlots.Length) return;
            if (spellSlots[index] == null) return;

            if (selectedSlot == index)
            {
                Deselect();
                return;
            }

            selectedSlot = index;
        }

        public void Deselect()
        {
            selectedSlot = -1;
        }

        private void OnDeselectPerformed(InputAction.CallbackContext context)
        {
            Deselect();
        }

        private Vector3 ComputeAimPoint()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                return castOrigin != null ? castOrigin.position + castOrigin.forward * aimRayDistance : transform.position + transform.forward * aimRayDistance;
            }

            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, aimRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            return ray.origin + ray.direction * aimRayDistance;
        }

        private void OnCastPerformed(InputAction.CallbackContext context)
        {
            if (playerController == null || !playerController.IsAiming) return;
            TryCast();
        }

        public bool TryCast()
        {
            var spell = SelectedSpell;
            if (spell == null) return false;

            if (Time.time < cooldownEndTimes[selectedSlot]) return false;
            if (waterResource == null || !waterResource.TryConsume(spell.waterCost)) return false;

            var context = new SpellCastContext
            {
                Origin = castOrigin != null ? castOrigin : transform,
                AimPoint = ComputeAimPoint(),
                TargetMask = targetMask,
                WaterBlobPrefab = waterBlobPrefab
            };

            spell.Cast(context);
            cooldownEndTimes[selectedSlot] = Time.time + spell.cooldown;

            if (spell is IceBoltSpellData)
            {
                PlayCastAnimation("CastIceBolt", iceBoltCastAnimDuration);
            }
            else if (spell is ShatterSpellData)
            {
                PlayCastAnimation("CastShatter", shatterCastAnimDuration);
            }
            else if (spell is FrostConeSpellData)
            {
                PlayCastAnimation("CastFrostCone", frostConeCastAnimDuration);
            }

            return true;
        }

        // While moving, the cast plays on the UpperBody layer only (masked to spine/arms/head)
        // so the base layer keeps driving legs normally - e.g. running continues under the cast.
        // While stationary, the base layer itself plays the full-body version of the same clip
        // (its own AnyState transition gated on Speed < 0.1), so legs join in too - the animator
        // controller picks the right one automatically based on Speed once the trigger fires;
        // this just decides whether the UpperBody layer needs to be turned on for it, and guards
        // the base-layer Idle/GoalkeeperIdle transitions via IsCasting so the full-body pose
        // isn't immediately preempted the frame after the trigger is consumed (Run/AimSidestep
        // are left unguarded so real movement can always interrupt it - see Update()).
        private void PlayCastAnimation(string stateName, float duration)
        {
            if (animator == null) return;

            bool isMoving = playerController != null && playerController.IsMoving;
            if (isMoving)
            {
                if (upperBodyLayerIndex >= 0) { animator.SetLayerWeight(upperBodyLayerIndex, 1f); }
                activeStationaryCastState = null;
            }
            else
            {
                animator.SetBool("IsCasting", true);
                activeStationaryCastState = stateName;
                stationaryCastHandedOff = false;
            }

            animator.SetTrigger(stateName);
            CancelInvoke(nameof(ClearCastAnimationState));
            Invoke(nameof(ClearCastAnimationState), duration);
        }

        private void Update()
        {
            // A stationary full-body cast is playing on the base layer, but the player has
            // started moving mid-animation - the base layer's Run/AimSidestep transitions will
            // already take the legs the instant Speed > 0.1 (no guard on those), so this just
            // hands the arm gesture off to the UpperBody layer so it keeps playing on top of
            // whatever the legs are now doing. We read the base layer's current normalizedTime
            // and Play() the same state on the UpperBody layer at that exact point, rather than
            // firing the trigger again, which would restart the clip from frame 0.
            if (activeStationaryCastState != null && !stationaryCastHandedOff
                && playerController != null && playerController.IsMoving)
            {
                stationaryCastHandedOff = true;
                if (animator != null && upperBodyLayerIndex >= 0)
                {
                    float normalizedTime = animator.GetCurrentAnimatorStateInfo(BaseLayerIndex).normalizedTime;
                    animator.SetLayerWeight(upperBodyLayerIndex, 1f);
                    animator.Play(activeStationaryCastState, upperBodyLayerIndex, normalizedTime);
                }
            }
        }

        private void ClearCastAnimationState()
        {
            activeStationaryCastState = null;
            if (animator == null) return;
            if (upperBodyLayerIndex >= 0) { animator.SetLayerWeight(upperBodyLayerIndex, 0f); }
            animator.SetBool("IsCasting", false);
        }
    }
}
