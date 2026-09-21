using UnityEngine;

namespace Pesky.Data
{
    /// <summary>Enemy numbers (docs/SLICE-1.md section 5). Goblin is the only one in slice 1.</summary>
    [CreateAssetMenu(fileName = "EnemyDef", menuName = "Pesky/Enemy Def")]
    public sealed class EnemyDef : ScriptableObject
    {
        public int id;
        public string displayName = "Goblin";

        [Header("Body")]
        [Tooltip("Capsule radius, metres.")]
        public float radius = 0.5f;
        [Tooltip("Capsule total height, metres.")]
        public float height = 1.4f;

        [Header("Movement")]
        public float moveSpeed = 3f;
        public float angularSpeed = 720f;
        public float acceleration = 20f;

        [Header("Life")]
        public float maxHp = 30f;
        [Tooltip("A goblin turns hostile on an ANIMATE weapon inside this range, in its own room, with line of sight.")]
        public float aggroRange = 8f;

        [Header("Wander (Idle)")]
        [Tooltip("Speed while wandering, slower than the chase speed.")]
        public float wanderSpeed = 1.1f;
        [Tooltip("How far from its anchor a wander point may be picked.")]
        public float wanderRadius = 4f;
        [Tooltip("Seconds it stands still at a wander point, minimum.")]
        public float wanderPauseMin = 1.5f;
        [Tooltip("Seconds it stands still at a wander point, maximum.")]
        public float wanderPauseMax = 3.5f;

        [Header("Curious")]
        [Tooltip("It notices a weapon this close, with line of sight, in its own room.")]
        public float curiousRange = 7f;
        [Tooltip("It stops about this far from the weapon and looks at it.")]
        public float curiousStopDistance = 1.5f;
        [Tooltip("Seconds of looking before it loses interest and wanders off.")]
        public float curiousSeconds = 4f;
        [Tooltip("Speed while walking over to look.")]
        public float curiousSpeed = 1.8f;

        [Header("Alarm")]
        [Tooltip("The '!' reaction before the chase starts.")]
        public float alarmedSeconds = 0.4f;
        [Tooltip("Turning hostile alerts other goblins this close, in the same room.")]
        public float alertRadius = 6f;
        [Tooltip("If the target stays INANIMATE this long and the goblin was not hurt, it drops back to Curious.")]
        public float calmSeconds = 5f;

        [Header("Attack")]
        public float attackRange = 1.4f;
        [Tooltip("Telegraph seconds: the blade is held raised on one side, the body turns red and swells.")]
        public float windupSeconds = 0.5f;
        [Tooltip("Seconds the silver arc takes to sweep across the front.")]
        public float strikeSeconds = 0.25f;
        [Tooltip("Half-angle of the swept arc: the blade goes from -this to +this about the forward axis.")]
        public float strikeHalfAngleDeg = 80f;
        [Tooltip("Reach of the swept arc; damage only lands inside it.")]
        public float strikeReach = 1.7f;
        public float attackDamage = 15f;
        [Tooltip("Impulse applied to the weapon it hits.")]
        public float attackKnockback = 8f;
        [Tooltip("Seconds of Recover after an attack.")]
        public float attackCooldown = 1.2f;

        [Header("Reactions")]
        [Tooltip("Metres per second of knockback per point of impact damage.")]
        public float knockbackPerDamage = 0.25f;
        public float maxKnockbackSpeed = 9f;
        public float knockbackDamping = 6f;
        public float hitFlashSeconds = 0.12f;
        [Tooltip("Scale multiplier at the end of the windup telegraph.")]
        public float windupSwell = 1.25f;

        [Header("Perception")]
        [Tooltip("Eye height above the goblin's feet for the line of sight ray.")]
        public float eyeHeight = 1.1f;
        [Tooltip("Layers that block line of sight (World).")]
        public LayerMask sightBlockers = 256;

                [Header("View cone (a goblin only sees what is in front of it)")]
        [Tooltip("TOTAL horizontal angle the goblin can see, centred on its forward axis. 110 = 55 degrees either side.")]
        [Range(20f, 360f)] public float viewConeDeg = 110f;
        [Tooltip("Anything closer than this is noticed whatever the cone says (it is right next to the goblin).")]
        public float closeSightRange = 1.5f;

        [Header("Patrol (a PatrolRoute wired on the goblin)")]
        [Tooltip("Speed while walking a patrol route.")]
        public float patrolSpeed = 1.6f;
        [Tooltip("How close it has to get to a waypoint to count as arrived.")]
        public float patrolArrive = 0.6f;

        [Header("Sleep (startAsleep on the goblin)")]
        [Tooltip("An ANIMATE weapon this close wakes it, cone or no cone.")]
        public float wakeAnimateRange = 4f;
        [Tooltip("A hard weapon impact this close wakes it.")]
        public float wakeImpactRange = 8f;
        [Tooltip("Impact speed in m/s that counts as loud enough to wake it.")]
        public float wakeImpactSpeed = 4f;
        [Tooltip("Waking also wakes sleeping goblins this close (they do not wake further neighbours).")]
        public float wakeNeighbourRadius = 6f;

        [Header("Porter (role = Porter)")]
        [Tooltip("How close it has to be to pick a weapon up or to reach its stand.")]
        public float porterReach = 2f;
        [Tooltip("A weapon must have been INANIMATE for this long before the porter will pick it up.")]
        public float porterInanimateSeconds = 2f;
        [Tooltip("How far away it notices a still weapon worth carrying.")]
        public float porterNoticeRange = 8f;
        [Tooltip("Speed while carrying.")]
        public float porterCarrySpeed = 2.2f;
        [Tooltip("Seconds it stands at the stand before setting the weapon down.")]
        public float porterPlaceSeconds = 0.6f;
        [Tooltip("Seconds after a delivery during which it will not pick anything up again.")]
        public float porterCooldownSeconds = 5f;

        [Header("Shield boss (role = ShieldBoss)")]
        [Tooltip("Shield hit points. 0 = no shield, which is every ordinary goblin.")]
        public float shieldMaxHp;
        [Tooltip("Only a weapon at least this heavy damages the shield; lighter weapons bounce off it.")]
        public float shieldMinMass = 8f;
        [Tooltip("Damage multiplier for BLADED weapons once the shield is gone. 1 = no bonus (ordinary goblins).")]
        public float bladedDamageBonus = 1f;

        [Header("Health bar")]
        [Tooltip("The bar shows when HP drops below this fraction, or whenever the goblin is hostile.")]
        [Range(0f, 1f)] public float healthBarShowBelow = 0.999f;
    }
}
