using UnityEngine;
using UnityEngine.InputSystem;
using Wayfarer.Spells;

namespace Wayfarer.Player
{
    /// <summary>
    /// Holds up to 6 spell slots (selected via Q/E/R/Z/X/C), casts the currently-selected
    /// spell on left-click while aiming. Checks and consumes Water cost via WaterResource,
    /// enforces per-spell cooldowns.
    /// </summary>
    public class PlayerSpellCaster : MonoBehaviour
    {
        [SerializeField] private SpellData[] spellSlots = new SpellData[6];
        [SerializeField] private GameObject waterBlobPrefab;
        [SerializeField] private LayerMask targetMask;

        [SerializeField] private InputActionReference castInput;
        [SerializeField] private InputActionReference[] selectInputs = new InputActionReference[6];

        [SerializeField] private PlayerController playerController;
        [SerializeField] private WaterResource waterResource;
        [SerializeField] private Transform castOrigin;
        [SerializeField] private float aimRayDistance = 200f;

        private int selectedSlot = -1;
        private readonly float[] cooldownEndTimes = new float[6];

        public SpellData SelectedSpell => selectedSlot >= 0 && selectedSlot < spellSlots.Length ? spellSlots[selectedSlot] : null;

        private void OnEnable()
        {
            castInput.action.Enable();
            castInput.action.performed += OnCastPerformed;

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

            foreach (var input in selectInputs)
            {
                if (input == null) continue;
                input.action.Disable();
            }
        }

        public void SelectSlot(int index)
        {
            if (index < 0 || index >= spellSlots.Length) return;
            if (spellSlots[index] == null) return;
            selectedSlot = index;
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
            return true;
        }
    }
}
