using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// Marker: the colliders on this object and its children are WOOD. A bladed weapon that hits wood fast
    /// enough and roughly point-first sticks in it (WeaponBody does the test; numbers in MovementTuning).
    /// Put it on a beam, a plank wall or a wooden door; it holds no state of its own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WoodSurface : MonoBehaviour
    {
        [Tooltip("Off = this wood is too hard to stick in (lets a designer switch one beam off without removing the marker).")]
        [SerializeField] bool stickable = true;

        public bool Stickable { get { return stickable && isActiveAndEnabled; } }
    }
}
