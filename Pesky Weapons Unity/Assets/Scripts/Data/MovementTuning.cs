using UnityEngine;

namespace Pesky.Data
{
    /// <summary>Movement numbers shared by every weapon (docs/SLICE-1.md section 2).</summary>
    [CreateAssetMenu(fileName = "MovementTuning", menuName = "Pesky/Movement Tuning")]
    public sealed class MovementTuning : ScriptableObject
    {
        [Header("Pitch to lift map (camera pitch: + looks down, - looks up)")]
        [Tooltip("Lift at and below the resting camera pitch. Never lower than this when grounded.")]
        public float minLiftDeg = 15f;
        public float maxLiftDeg = 80f;
        [Tooltip("Camera pitch that gives the minimum lift (the resting view, looking down at the weapon).")]
        public float restingCameraPitchDeg = 20f;
        [Tooltip("Camera pitch that gives the maximum lift (view tilted fully up).")]
        public float maxLiftCameraPitchDeg = -35f;

        [Header("Launch")]
        public float launchCooldown = 0.25f;
        [Tooltip("Adds g*dt/2 to the launch velocity so the 50 Hz integrator lands exactly on the ballistic arc that the preview and the room maths use.")]
        public bool compensateIntegrator = true;

        [Header("Contacts")]
        [Tooltip("A contact with normal.y above this counts as ground.")]
        public float groundedNormalY = 0.6f;
        public float groundedGrace = 0.1f;
        [Tooltip("A contact with |normal.y| below this counts as a wall.")]
        public float wallNormalY = 0.3f;
        public float wallGrace = 0.15f;
        [Tooltip("Layers a weapon can stand on (World, Weapon, Enemy).")]
        public LayerMask groundMask = 2816;
        [Tooltip("Layers that count as a wall for the wall jump (World).")]
        public LayerMask wallMask = 256;

        [Header("Wall jump")]
        public int wallJumpsPerAirtime = 1;
        [Tooltip("0 = keep the (reflected) aim, 1 = straight out along the wall normal.")]
        [Range(0f, 1f)] public float wallReboundBlend = 0.5f;
        [Tooltip("The rebound always satisfies dot(dir, wallNormal) > this.")]
        public float wallMinDot = 0.3f;
        [Tooltip("Safety margin added to wallMinDot when the direction has to be pushed out.")]
        public float wallDotMargin = 0.05f;

        [Header("World")]
        public float gravity = 20f;
        public float killY = -20f;
        [Tooltip("Height above the last safe position a recovered weapon is dropped from.")]
        public float recoverLift = 0.5f;
        [Tooltip("The last safe position is only recorded while slower than this.")]
        public float safeSpeed = 1f;

        [Header("Trajectory preview")]
        public float previewSeconds = 1f;
        public int previewPoints = 24;        [Tooltip("Layers the preview arc is cast against (World + Enemy).")]
        public LayerMask previewBlockMask = 2304;
        [Tooltip("Radius of the opaque landing circle drawn at the predicted hit.")]
        public float previewMarkerRadius = 0.35f;
        [Tooltip("Metres the landing circle is lifted off the surface to avoid z-fighting.")]
        public float previewMarkerLift = 0.02f;

        [Header("Animate weapon (what a goblin can perceive as alive)")]
        [Tooltip("Seconds a weapon still counts as ANIMATE after a launch or an Orb roll input.")]
        public float animateSeconds = 1.5f;
        [Tooltip("A weapon moving faster than this counts as ANIMATE whatever it did last.")]
        public float animateSpeed = 1f;

        [Header("Stick in wood (bladed weapons on a WoodSurface)")]
        [Tooltip("Impact speed in m/s at or above which a bladed weapon sticks.")]
        public float stickMinSpeed = 6f;
        [Tooltip("Largest angle between the point and the line straight into the wood that still sticks. Generous on purpose.")]
        [Range(5f, 90f)] public float stickMaxAngleDeg = 60f;
        [Tooltip("Metres the point is pushed into the wood when it sticks (0 = held exactly at the contact pose).")]
        public float stickSinkDepth = 0f;
        [Tooltip("Seconds after being freed during which the weapon cannot stick again, so a launch gets clear of the wood.")]
        public float stickRearmSeconds = 0.3f;

        [Header("Blade flight (bladed weapons fly point-first, so sticking is aimable)")]
        [Tooltip("How hard an airborne bladed weapon turns its point into its velocity, in rad/s per radian of error. 0 = off (free tumble, as before).")]
        public float bladeAlignRate = 10f;
        [Tooltip("Seconds after a launch before the turn starts, so the blade clears the floor or the beam it left.")]
        public float bladeAlignDelay = 0.15f;
        [Tooltip("No turning below this speed in m/s.")]
        public float bladeAlignMinSpeed = 3f;



    }
}
