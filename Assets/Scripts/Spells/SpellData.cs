using UnityEngine;

namespace Wayfarer.Spells
{
    /// <summary>
    /// Base class for all spells, authored as ScriptableObject data assets rather than new
    /// MonoBehaviours per spell, per the project's architectural approach (matches the
    /// ScriptableObject-driven pattern used in the earlier Beachhead project).
    /// </summary>
    public abstract class SpellData : ScriptableObject
    {
        public string spellName;
        public float waterCost;
        public float cooldown;
        public GameObject castVfxPrefab;
        public float vfxLifetime = 3f;

        public abstract void Cast(SpellCastContext context);

        protected GameObject SpawnVfx(SpellCastContext context)
        {
            if (castVfxPrefab == null) return null;

            var instance = Object.Instantiate(castVfxPrefab, context.Origin.position, context.Origin.rotation);
            Object.Destroy(instance, vfxLifetime);
            return instance;
        }
    }
}
