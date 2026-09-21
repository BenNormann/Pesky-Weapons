using UnityEngine;

namespace Pesky.Data
{
    /// <summary>Static numbers for one weapon type. Starting values live in docs/SLICE-1.md section 4.</summary>
    [CreateAssetMenu(fileName = "WeaponDef", menuName = "Pesky/Weapon Def")]
    public sealed class WeaponDef : ScriptableObject
    {
        [Header("Identity")]
        public int id;
        public string displayName = "Weapon";

        [Header("Launch")]
        [Tooltip("Launch speed in m/s. Velocity is SET to dir * launchSpeed, so mass does not change the arc.")]
        public float launchSpeed = 12f;
        [Tooltip("Tumble added on launch, as an angular velocity change in rad/s (mass independent, nose-up).")]
        public float launchTorque = 2f;

        [Header("Body")]
        public float mass = 3f;
        public float linearDrag = 0.05f;
        public float angularDrag = 1f;
        public PhysicsMaterial physicsMaterial;

        [Header("Combat")]
        public float damage = 15f;
        public float maxHp = 100f;

        [Header("Tags (what the kit pieces ask about)")]
        [Tooltip("Has a cutting edge: cuts ropes, sticks point-first into WoodSurface.")]
        public bool bladed;
        [Tooltip("Smashes: breaks pots and cracked walls.")]
        public bool blunt;
        [Tooltip("Pulled by a MagnetZone, struck by lightning.")]
        public bool metal;
        [Tooltip("Ignored by magnets and lightning.")]
        public bool wooden;
        [Tooltip("Local direction from the grip to the point. A bladed weapon sticks into wood when this leads into the surface.")]
        public Vector3 tipAxis = new Vector3(0f, 0f, 1f);


        [Header("Rolling (Orb only)")]
        public bool canRoll;
        [Tooltip("Camera-relative rolling torque in N*m while the Roll input is held.")]
        public float rollTorque;

        [Header("Prefab")]
        public GameObject prefab;
    }
}
