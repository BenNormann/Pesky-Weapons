using System;

namespace Pesky.Protocol
{
    /// <summary>
    /// The bits that ride every POSE (0x01). Teleport tells a remote view to
    /// snap instead of interpolating (a MagicDoor trip, a respawn, a recover).
    /// Animate is the "moved under its own power recently" bit the host's
    /// goblins perceive. Soul means the body is the free soul, not a weapon.
    /// </summary>
    [Flags]
    public enum PoseFlags : byte { None = 0, Teleport = 1, Animate = 2, Soul = 4 }

    /// <summary>Which family of kit piece a KIT_STATE or KIT_REQ is about. Stable: append only.</summary>
    public enum KitKind : byte
    {
        Door = 0, Plate = 1, Key = 2, Rune = 3, Anvil = 4, Magnet = 5, Lift = 6, Rope = 7, Pot = 8,
        Lever = 9, Counterweight = 10, Scales = 11, CrackedWall = 12, PorterGate = 13,
        /// <summary>Cosmetic: a lightning field struck the actor weapon (the damage is a WEAPON_DAMAGED).</summary>
        Lightning = 14,
        /// <summary>pieceId is the porter's enemy id, actor the weapon. state 1 = picked up, 0 = dropped in alarm, 2 = placed.</summary>
        PorterCarry = 15,
    }

    [Flags]
    public enum EnemyFlags : byte { None = 0, Alive = 1, Hostile = 2, Awake = 4, Carrying = 8 }

    [Flags]
    public enum WeaponFlags : byte { None = 0, Broken = 1 }

    /// <summary>The ENEMY_HEALTH result bits.</summary>
    [Flags]
    public enum HitFlags : byte { None = 0, HitShield = 1, ShieldBroke = 2, Died = 4, Blocked = 8 }

    /// <summary>Why a weapon broke. Stable: append only.</summary>
    public enum BreakCause : byte { Damage = 0, ReleasedInCombat = 1, HostForced = 2 }
}
