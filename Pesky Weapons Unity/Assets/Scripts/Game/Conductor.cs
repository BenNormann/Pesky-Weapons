using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// Marks a weapon part whose collider is METAL for the circuit's purposes: the Hammer's head conducts, its wooden
    /// handle does not; the Mace's head, not its haft; the Sword and the Dagger throughout; the Staff, now iron. A break
    /// (CircuitBridge) closes when a conducting part touches both of its stubs. A metal weapon (WeaponDef.metal) with no
    /// Conductor on any part conducts with every solid collider it has.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Conductor : MonoBehaviour
    {
    }
}
