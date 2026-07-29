using UnityEngine;
using Wayfarer.Player;

namespace Wayfarer.Spells
{
    /// <summary>
    /// Shows each spell's range/effect area on the ground for as long as it stays armed
    /// (selected via Q/E/R), so the player can see where a cast will land before committing to
    /// it. Three persistent indicator objects are built once in Awake and just
    /// repositioned/toggled each frame rather than spawned/destroyed per selection:
    ///  - a ring for Ice Bolt's max travel distance and Shatter's max cast range (both centered
    ///    on the caster)
    ///  - a filled disc for Shatter's strike zone (impact blast radius), tracking the live aim
    ///    point clamped exactly the way the real cast clamps it (see GroundTargetingUtility) -
    ///    so it shows both "how far you can reach" (the ring) and "what will actually get hit"
    ///    (the disc) at once
    ///  - a trapezoid for Frost Cone's range/angle, oriented to the caster's forward
    /// Ground-target math for Shatter's preview is shared with ShatterSpellData.Cast via
    /// GroundTargetingUtility, so the preview can never show a landing spot the real cast
    /// wouldn't also reach.
    /// </summary>
    public class SpellRangeIndicatorController : MonoBehaviour
    {
        [SerializeField] private PlayerSpellCaster spellCaster;

        [Header("Materials")]
        [Tooltip("Used for Ice Bolt's range ring and Frost Cone's range/angle trapezoid.")]
        [SerializeField] private Material iceIndicatorMaterial;
        [Tooltip("Used for Shatter's max-range ring.")]
        [SerializeField] private Material shatterRingMaterial;
        [Tooltip("Used for Shatter's strike-zone (impact radius) disc.")]
        [SerializeField] private Material shatterStrikeZoneMaterial;

        [Header("Ice Bolt")]
        [Tooltip("Ice Bolt's range is shown as a forward-facing arc rather than a full circle, since it can only ever be fired in one direction at a time.")]
        [SerializeField] private float iceBoltArcAngle = 40f;

        private GroundCircleIndicator rangeRing;
        private MeshRenderer rangeRingRenderer;
        private GroundCircleIndicator strikeZone;
        private GroundConeIndicatorLive cone;

        private void Awake()
        {
            if (spellCaster == null)
            {
                spellCaster = GetComponent<PlayerSpellCaster>();
            }

            // rangeRing is shared between Ice Bolt and Shatter (only one is ever shown at
            // once) - its material is swapped per-frame in Update() to match whichever spell
            // is currently armed, since they use different colors.
            rangeRing = BuildCircle("SpellRangeRing", iceIndicatorMaterial);
            rangeRingRenderer = rangeRing.GetComponent<MeshRenderer>();
            strikeZone = BuildCircle("SpellStrikeZoneIndicator", shatterStrikeZoneMaterial);
            cone = BuildCone("SpellRangeConeIndicator", iceIndicatorMaterial);
        }

        private GroundCircleIndicator BuildCircle(string goName, Material mat)
        {
            var go = new GameObject(goName, typeof(MeshFilter), typeof(MeshRenderer));
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var indicator = go.AddComponent<GroundCircleIndicator>();
            go.SetActive(false);
            return indicator;
        }

        private GroundConeIndicatorLive BuildCone(string goName, Material mat)
        {
            var go = new GameObject(goName, typeof(MeshFilter), typeof(MeshRenderer));
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var indicator = go.AddComponent<GroundConeIndicatorLive>();
            go.SetActive(false);
            return indicator;
        }

        private void Update()
        {
            if (spellCaster == null) return;

            SpellData spell = spellCaster.SelectedSpell;
            bool showRing = false, showStrikeZone = false, showCone = false;

            Transform origin = spellCaster.CastOrigin;
            LayerMask targetMask = spellCaster.TargetMask;

            if (spell is IceBoltSpellData iceBolt)
            {
                float maxDist = 12f;
                if (iceBolt.projectilePrefab != null)
                {
                    var proj = iceBolt.projectilePrefab.GetComponent<IceBoltProjectile>();
                    if (proj != null) { maxDist = proj.MaxTravelDistance; }
                }

                rangeRingRenderer.sharedMaterial = iceIndicatorMaterial;
                Quaternion iceFacing = FlatFacingRotation(origin.forward);
                rangeRing.UpdateIndicator(origin.position, iceFacing, maxDist, false, 0.4f, iceBoltArcAngle);
                showRing = true;
            }
            else if (spell is ShatterSpellData shatter)
            {
                rangeRingRenderer.sharedMaterial = shatterRingMaterial;
                rangeRing.UpdateIndicator(origin.position, Quaternion.identity, shatter.castRange, false);
                showRing = true;

                Vector3 aimPoint = spellCaster.ComputeAimPoint();
                Vector3 groundPos = GroundTargetingUtility.ResolveGroundPosition(aimPoint, targetMask);
                groundPos = GroundTargetingUtility.ClampToRange(groundPos, origin.position, shatter.castRange, targetMask);
                strikeZone.UpdateIndicator(groundPos, Quaternion.identity, shatter.range, true);
                showStrikeZone = true;
            }
            else if (spell is FrostConeSpellData frostCone)
            {
                Quaternion coneFacing = FlatFacingRotation(origin.forward);
                cone.UpdateIndicator(origin.position, coneFacing, frostCone.range, frostCone.halfAngle);
                showCone = true;
            }

            rangeRing.gameObject.SetActive(showRing);
            strikeZone.gameObject.SetActive(showStrikeZone);
            cone.gameObject.SetActive(showCone);
        }

        // Shared by the arc ring and the cone - both need to face the caster's horizontal
        // facing direction, ignoring any pitch (looking up/down shouldn't tilt a ground-plane
        // shape).
        private Quaternion FlatFacingRotation(Vector3 forward)
        {
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward.normalized) : Quaternion.identity;
        }

        private void OnDestroy()
        {
            if (rangeRing != null) { Destroy(rangeRing.gameObject); }
            if (strikeZone != null) { Destroy(strikeZone.gameObject); }
            if (cone != null) { Destroy(cone.gameObject); }
        }
    }
}
