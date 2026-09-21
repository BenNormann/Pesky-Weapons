namespace Pesky.Protocol
{
    /// <summary>
    /// The one type byte that starts every payload. Owns the id space: M0
    /// keeps 0x01 to 0x03, gameplay uses 0x10 to 0x7E grouped by domain in
    /// the high nibble, and 0x7F is the FRAME container.
    /// </summary>
    public static class MsgId
    {
        // M0, untouched (see M0Messages).
        public const byte Transform = M0Messages.TypeTransform;   // 0x01
        public const byte Hello = M0Messages.TypeHello;           // 0x02
        public const byte Ping = M0Messages.TypePing;             // 0x03

        // Session and clock, 0x10-0x1F.
        public const byte SessionInfo = 0x10;
        public const byte TimeSync = 0x11;
        public const byte ClockPing = 0x12;
        public const byte ClockPong = 0x13;
        public const byte JoinRequest = 0x14;
        public const byte Snapshot = 0x15;
        public const byte SessionPhase = 0x16;
        public const byte PeerSlots = 0x17;
        public const byte SessionEnd = 0x18;
        public const byte ResyncReq = 0x19;
        // 0x1A-0x1F reserved for host migration. Do not assign.

        // Pesky Weapons' gameplay domains, 0x20 to 0x7E, are assigned by the
        // stage that adds them: players 0x20-0x2F, the labyrinth 0x30-0x3F,
        // enemies 0x40-0x4F, world (kit pieces) 0x50-0x5F. 0x60-0x7E are free.

        // Players and their weapons, 0x20-0x2F.
        public const byte PossessReq = 0x20;
        public const byte ReleaseReq = 0x21;
        public const byte WeaponOwner = 0x22;
        public const byte WeaponState = 0x23;
        public const byte WeaponBroken = 0x24;
        public const byte WeaponRespawned = 0x25;
        public const byte WeaponDamaged = 0x26;
        public const byte BatClaim = 0x27;
        public const byte BatEvent = 0x28;
        // 0x29-0x2F unused.

        // The labyrinth, 0x30-0x3F. Two of them are Replies, which is how a
        // secret reaches one player and nobody else.
        public const byte SwapReq = 0x30;
        public const byte CompassBendReq = 0x31;
        public const byte LabLayout = 0x32;
        public const byte LabSwapped = 0x33;
        public const byte CompassTargets = 0x34;
        public const byte RoleAssign = 0x35;
        public const byte RoundResult = 0x36;
        public const byte PlayerDown = 0x37;
        public const byte PlayerRespawn = 0x38;
        public const byte Legend = 0x39;
        // The team's shared scratch pad, 0x3A-0x3C. Public knowledge: everybody draws on one canvas.
        public const byte PadStrokeReq = 0x3A;
        public const byte PadStroke = 0x3B;
        public const byte PadClear = 0x3C;
        // 0x3D-0x3F unused.

        // Enemies, 0x40-0x4F.
        public const byte EnemyState = 0x40;
        public const byte HitClaim = 0x41;
        public const byte EnemyHealth = 0x42;
        // 0x43-0x4F unused.

        // World and kit, 0x50-0x5F.
        public const byte KitState = 0x50;
        public const byte KitReq = 0x51;
        public const byte WorldReset = 0x52;
        // 0x53-0x5F unused.

        
/// <summary>The container: count:u8 then [len:u16 body] per message. See <see cref="Frame"/>.</summary>
        public const byte Frame = 0x7F;
    }
}
