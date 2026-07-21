using UnityEngine;

namespace Wayfarer.Spells
{
    /// <summary>
    /// Everything a SpellData needs to actually cast itself, gathered by PlayerSpellCaster
    /// so individual spells don't need direct references back to the player.
    /// </summary>
    public class SpellCastContext
    {
        public Transform Origin;
        public Vector3 AimPoint;
        public LayerMask TargetMask;
        public GameObject WaterBlobPrefab;
    }
}
