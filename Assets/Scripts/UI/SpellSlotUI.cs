using UnityEngine;
using UnityEngine.UI;
using Wayfarer.Player;
using Wayfarer.Spells;

namespace Wayfarer.UI
{
    /// <summary>
    /// Drives one spell-bar slot's cooldown display: a radial-filled dark overlay on top of
    /// the spell icon that sweeps away as the cooldown runs out (WoW-style pie countdown).
    /// The overlay Image must be set to Filled / Radial360; fillAmount is driven here from
    /// PlayerSpellCaster's cooldown state each frame. fillAmount 0 = ready (no shading).
    /// </summary>
    public class SpellSlotUI : MonoBehaviour
    {
        [SerializeField] private PlayerSpellCaster spellCaster;
        [Tooltip("Which PlayerSpellCaster slot this UI square represents (0-5).")]
        [SerializeField] private int slotIndex;
        [SerializeField] private Image cooldownOverlay;
        [Tooltip("Optional label above the slot showing the select key - text is filled from the slot's actual input binding on Start.")]
        [SerializeField] private Text keyLabel;
        [Tooltip("White border shown while this slot's spell is the currently selected/armed one (PlayerSpellCaster.SelectedSlotIndex).")]
        [SerializeField] private GameObject selectionHighlight;

        private void Start()
        {
            if (keyLabel != null && spellCaster != null)
            {
                keyLabel.text = spellCaster.GetSlotKeyLabel(slotIndex);
            }
        }
private void Update()
        {
            if (spellCaster == null) return;

            if (selectionHighlight != null)
            {
                selectionHighlight.SetActive(spellCaster.SelectedSlotIndex == slotIndex);
            }

            if (cooldownOverlay == null) return;

            SpellData spell = spellCaster.GetSpellInSlot(slotIndex);
            if (spell == null || spell.cooldown <= 0f)
            {
                cooldownOverlay.fillAmount = 0f;
                return;
            }

            float remaining = spellCaster.GetCooldownRemaining(slotIndex);
            cooldownOverlay.fillAmount = Mathf.Clamp01(remaining / spell.cooldown);
        }
    }
}
