using UnityEngine;

namespace Pesky.Data
{
    /// <summary>A modifier that lives on a weapon until that weapon breaks (docs/SLICE-1.md section 3).</summary>
    [CreateAssetMenu(fileName = "ModifierDef", menuName = "Pesky/Modifier Def")]
    public sealed class ModifierDef : ScriptableObject
    {
        public int id;
        public string displayName = "Modifier";
        [Tooltip("Multiplies the weapon's impact damage. Rune_Metal = 1.25.")]
        public float damageMultiplier = 1f;
    }
}
